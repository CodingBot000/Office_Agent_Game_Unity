using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class OfficeInventoryUI : MonoBehaviour
{
    private enum InventoryTab
    {
        Status,
        OwnedItems
    }

    private Canvas canvas;
    private OfficeBackendClient backend;
    private GameObject panel;
    private RectTransform panelRect;
    private Transform content;
    private Text statusText;
    private Button toggleButton;
    private Button statusTabButton;
    private Button ownedItemsTabButton;

    private InventoryTab selectedTab = InventoryTab.Status;
    private Coroutine panelAnimation;
    private readonly Vector2 panelOpenPosition = new Vector2(-390f, -305f);
    private readonly Vector2 panelClosedPosition = new Vector2(230f, -305f);

    private void Start()
    {
        backend = OfficeBackendClient.Instance;
        EnsureEventSystem();
        BuildUi();

        panel.SetActive(false);
        if (backend != null)
        {
            backend.SnapshotUpdated += OnSnapshotUpdated;
            OnSnapshotUpdated(backend.CurrentSnapshot);
        }
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.iKey.wasPressedThisFrame && !IsTextInputFocused())
        {
            TogglePanel();
        }
    }

    private void OnDestroy()
    {
        if (backend != null)
        {
            backend.SnapshotUpdated -= OnSnapshotUpdated;
        }

        if (panelAnimation != null)
        {
            StopCoroutine(panelAnimation);
        }
    }

    private void TogglePanel()
    {
        if (panel == null || panelRect == null)
        {
            return;
        }

        if (panelAnimation != null)
        {
            StopCoroutine(panelAnimation);
            panelAnimation = null;
        }

        if (!panel.activeSelf)
        {
            panel.SetActive(true);
            panelRect.anchoredPosition = panelClosedPosition;
            RebuildList(backend == null ? null : backend.CurrentSnapshot);
            panelAnimation = StartCoroutine(AnimatePanel(true));
        }
        else
        {
            panelAnimation = StartCoroutine(AnimatePanel(false));
        }
    }

    private IEnumerator AnimatePanel(bool opening)
    {
        var from = panelRect.anchoredPosition;
        var to = opening ? panelOpenPosition : panelClosedPosition;
        const float duration = 0.24f;
        var elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            var progress = Mathf.Clamp01(elapsed / duration);
            var eased = 1f - Mathf.Pow(1f - progress, 3f);
            panelRect.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
            yield return null;
        }

        panelRect.anchoredPosition = to;
        panelAnimation = null;

        if (!opening)
        {
            panel.SetActive(false);
        }
    }

    private void SelectTab(InventoryTab tab)
    {
        selectedTab = tab;
        UpdateTabVisuals();
        RebuildList(backend == null ? null : backend.CurrentSnapshot);
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        var eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    private void BuildUi()
    {
        var canvasObject = new GameObject("OfficeInventoryCanvas");
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.AddComponent<Canvas>();
        var mainCamera = Camera.main;
        canvas.renderMode = mainCamera == null ? RenderMode.ScreenSpaceOverlay : RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = mainCamera;
        canvas.planeDistance = 1f;
        canvas.sortingOrder = 520;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        toggleButton = CreateButton(canvas.transform, "InventoryToggle", "i", new Vector2(44f, 44f), new Vector2(-205f, -100f));
        var toggleRect = toggleButton.GetComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(1f, 1f);
        toggleRect.anchorMax = new Vector2(1f, 1f);
        toggleRect.pivot = new Vector2(0.5f, 0.5f);
        toggleRect.anchoredPosition = new Vector2(-205f, -100f);

        var toggleImage = toggleButton.GetComponent<Image>();
        toggleImage.sprite = CreateCircleSprite();
        toggleImage.type = Image.Type.Simple;
        toggleImage.preserveAspect = true;
        toggleButton.GetComponentInChildren<Text>().fontSize = 22;
        toggleButton.onClick.AddListener(TogglePanel);

        panel = CreatePanel(
            "InventoryPanel",
            canvas.transform,
            new Vector2(430f, 460f),
            new Vector2(1f, 1f),
            panelOpenPosition,
            new Color(0.035f, 0.045f, 0.07f, 0.96f)
        );
        panelRect = panel.GetComponent<RectTransform>();

        CreateText(panel.transform, "InventoryTitle", "정보", 24, Color.white, TextAnchor.MiddleLeft, new Vector2(170f, 34f), new Vector2(-115f, 205f));
        var closeButton = CreateButton(panel.transform, "InventoryClose", "닫기", new Vector2(78f, 34f), new Vector2(165f, 205f));
        closeButton.onClick.AddListener(TogglePanel);

        statusTabButton = CreateButton(panel.transform, "StatusTab", "상태", new Vector2(175f, 44f), new Vector2(-105f, 160f));
        ownedItemsTabButton = CreateButton(panel.transform, "OwnedItemsTab", "소유 물건", new Vector2(175f, 44f), new Vector2(85f, 160f));
        ConfigureTabHitArea(statusTabButton);
        ConfigureTabHitArea(ownedItemsTabButton);
        statusTabButton.onClick.AddListener(() => SelectTab(InventoryTab.Status));
        ownedItemsTabButton.onClick.AddListener(() => SelectTab(InventoryTab.OwnedItems));

        CreateScrollView(panel.transform, "InventoryScroll", new Vector2(0f, -15f), new Vector2(390f, 340f), out content);
        statusText = CreateText(panel.transform, "InventoryStatus", "서버 연결 대기 중...", 15, new Color(0.72f, 0.80f, 0.90f), TextAnchor.MiddleLeft, new Vector2(390f, 28f), new Vector2(-10f, -215f));
        UpdateTabVisuals();
    }

    private void ConfigureTabHitArea(Button button)
    {
        if (button == null)
        {
            return;
        }

        var rect = button.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(175f, 44f);

        var image = button.GetComponent<Image>();
        image.raycastTarget = true;

        button.targetGraphic = image;
        button.interactable = true;
        button.transition = Selectable.Transition.ColorTint;
    }

    private void UpdateTabVisuals()
    {
        SetTabColor(statusTabButton, selectedTab == InventoryTab.Status);
        SetTabColor(ownedItemsTabButton, selectedTab == InventoryTab.OwnedItems);
    }

    private void SetTabColor(Button button, bool selected)
    {
        if (button == null)
        {
            return;
        }

        var colors = button.colors;
        colors.normalColor = selected
            ? new Color(0.20f, 0.52f, 0.56f, 1f)
            : new Color(0.12f, 0.30f, 0.38f, 0.98f);
        colors.highlightedColor = new Color(0.25f, 0.62f, 0.64f, 1f);
        colors.pressedColor = new Color(0.08f, 0.20f, 0.26f, 1f);
        button.colors = colors;
    }

    private void OnSnapshotUpdated(OfficeSnapshotDto snapshot)
    {
        if (content == null)
        {
            return;
        }

        RebuildList(snapshot);
    }

    private void RebuildList(OfficeSnapshotDto snapshot)
    {
        for (var index = content.childCount - 1; index >= 0; index--)
        {
            Destroy(content.GetChild(index).gameObject);
        }

        if (snapshot == null)
        {
            statusText.text = "서버 스냅샷을 기다리는 중...";
            return;
        }

        if (selectedTab == InventoryTab.Status)
        {
            RebuildStatusList(snapshot);
        }
        else
        {
            RebuildOwnedItemsList(snapshot);
        }

        statusText.text = $"턴 {snapshot.turn}  ·  서버 동기화 완료";
    }

    private void RebuildStatusList(OfficeSnapshotDto snapshot)
    {
        AddSectionHeader("캐릭터 현재 상태");

        if (snapshot.npcs == null || snapshot.npcs.Length == 0)
        {
            AddRow("서버에서 캐릭터 상태를 받지 못했습니다.", new Color(0.92f, 0.65f, 0.42f), 82f);
            return;
        }

        foreach (var npc in snapshot.npcs)
        {
            if (npc == null)
            {
                continue;
            }

            var name = string.IsNullOrEmpty(npc.name) ? npc.role : npc.name;
            var state = npc.dynamic_state;
            if (state == null)
            {
                AddRow($"캐릭터: {name}\n상태 데이터 없음", new Color(0.80f, 0.82f, 0.88f), 82f);
                continue;
            }

            var emotion = OfficeEmotionText.ToKorean(state.emotion);
            AddRow(
                $"캐릭터: {name}\n감정: {emotion}\nStress: {state.stress}  |  신뢰도: {state.trust_toward_player}  |  협조도: {state.cooperation}",
                Color.white,
                82f
            );
        }
    }

    private void RebuildOwnedItemsList(OfficeSnapshotDto snapshot)
    {
        var objects = snapshot.world_objects ?? Array.Empty<OfficeWorldObjectDto>();
        var npcNames = BuildNpcNames(snapshot);
        var playerHeld = BuildPlayerHeldIds(snapshot);
        var heldCount = snapshot.player_inventory == null || snapshot.player_inventory.held_object_ids == null
            ? 0
            : snapshot.player_inventory.held_object_ids.Length;
        var maxHeld = snapshot.player_inventory == null ? 1 : snapshot.player_inventory.max_held_objects;

        AddSectionHeader($"플레이어 보유 ({heldCount}/{maxHeld})");
        if (heldCount == 0)
        {
            AddRow("현재 들고 있는 물건이 없습니다.", new Color(0.65f, 0.68f, 0.76f), 82f);
        }
        else
        {
            foreach (var objectId in playerHeld)
            {
                var state = FindObject(objects, objectId);
                var itemName = state == null ? objectId : state.name;
                AddRow($"물품: {itemName}\n현재 소지자: 플레이어", Color.white, 82f);
            }
        }

        AddSectionHeader("전체 물건");
        if (objects.Length == 0)
        {
            AddRow("서버에서 물건 정보를 받지 못했습니다.", new Color(0.92f, 0.65f, 0.42f), 82f);
            return;
        }

        foreach (var state in objects)
        {
            if (state == null)
            {
                continue;
            }

            var ownerName = ResolveNpcName(npcNames, state.owner_id);
            var holderName = string.IsNullOrEmpty(state.holder_id)
                ? "없음"
                : ResolveNpcName(npcNames, state.holder_id);

            if (string.Equals(state.holder_id, "player", StringComparison.OrdinalIgnoreCase))
            {
                holderName = "플레이어";
            }

            var condition = ResolveCondition(state.condition);
            var location = ResolveLocation(state.location);
            AddRow($"소유자: {ownerName}\n물품: {state.name}\n위치: {location}  |  상태: {condition}\n현재 소지자: {holderName}", Color.white, 82f);
        }
    }

    private Dictionary<string, string> BuildNpcNames(OfficeSnapshotDto snapshot)
    {
        var names = new Dictionary<string, string>();
        if (snapshot == null || snapshot.npcs == null)
        {
            return names;
        }

        foreach (var npc in snapshot.npcs)
        {
            if (npc != null && !string.IsNullOrEmpty(npc.id))
            {
                names[npc.id] = string.IsNullOrEmpty(npc.name) ? npc.role : npc.name;
            }
        }

        return names;
    }

    private List<string> BuildPlayerHeldIds(OfficeSnapshotDto snapshot)
    {
        var ids = new List<string>();
        if (snapshot != null && snapshot.player_inventory != null && snapshot.player_inventory.held_object_ids != null)
        {
            ids.AddRange(snapshot.player_inventory.held_object_ids);
        }

        return ids;
    }

    private OfficeWorldObjectDto FindObject(OfficeWorldObjectDto[] objects, string id)
    {
        if (objects == null)
        {
            return null;
        }

        foreach (var state in objects)
        {
            if (state != null && state.id == id)
            {
                return state;
            }
        }

        return null;
    }

    private string ResolveNpcName(Dictionary<string, string> names, string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return "공용";
        }

        return names.TryGetValue(id, out var name) ? name : id;
    }

    private string ResolveCondition(string condition)
    {
        if (string.Equals(condition, "damaged", StringComparison.OrdinalIgnoreCase))
        {
            return "손상";
        }

        if (string.Equals(condition, "destroyed", StringComparison.OrdinalIgnoreCase))
        {
            return "파괴됨";
        }

        return "정상";
    }

    private string ResolveLocation(string location)
    {
        switch (location)
        {
            case "meeting_room":
                return "회의실";
            case "dev_area":
                return "개발 구역";
            case "qa_desk":
                return "QA 데스크";
            case "pm_desk":
                return "PM 데스크";
            default:
                return string.IsNullOrEmpty(location) ? "알 수 없음" : location;
        }
    }

    private void AddSectionHeader(string value)
    {
        var header = CreateText(content, "SectionHeader", value, 18, new Color(0.48f, 0.88f, 0.82f), TextAnchor.MiddleLeft, new Vector2(350f, 32f), Vector2.zero);
        var element = header.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 32f;
        element.preferredHeight = 32f;
    }

    private void AddRow(string value, Color color, float height)
    {
        var row = CreateText(content, "InventoryRow", value, 16, color, TextAnchor.UpperLeft, new Vector2(350f, height), Vector2.zero);
        row.horizontalOverflow = HorizontalWrapMode.Wrap;
        row.verticalOverflow = VerticalWrapMode.Overflow;
        var element = row.gameObject.AddComponent<LayoutElement>();
        element.minHeight = height;
        element.preferredHeight = height;
    }

    private bool IsTextInputFocused()
    {
        var selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
        return selected != null && selected.GetComponentInParent<InputField>() != null;
    }

    private Sprite CreateCircleSprite()
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "OfficeInventoryCircleTexture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        var center = (size - 1) * 0.5f;
        var radius = size * 0.46f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                texture.SetPixel(x, y, distance <= radius ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private GameObject CreatePanel(string name, Transform parent, Vector2 size, Vector2 anchor, Vector2 position, Color color)
    {
        var panel = new GameObject(name);
        panel.transform.SetParent(parent, false);

        var rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var image = panel.AddComponent<Image>();
        image.color = color;
        return panel;
    }

    private Text CreateText(Transform parent, string name, string value, int fontSize, Color color, TextAnchor alignment, Vector2 size, Vector2 position)
    {
        var textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);

        var rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateButton(Transform parent, string name, string label, Vector2 size, Vector2 position)
    {
        var buttonObject = new GameObject(name);
        buttonObject.transform.SetParent(parent, false);

        var rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.12f, 0.30f, 0.38f, 0.98f);
        image.raycastTarget = true;

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = new Color(0.12f, 0.30f, 0.38f, 0.98f);
        colors.highlightedColor = new Color(0.20f, 0.52f, 0.56f, 1f);
        colors.pressedColor = new Color(0.08f, 0.20f, 0.26f, 1f);
        colors.disabledColor = new Color(0.18f, 0.18f, 0.22f, 0.70f);
        button.colors = colors;

        var text = CreateText(buttonObject.transform, "Label", label, 17, Color.white, TextAnchor.MiddleCenter, size - new Vector2(10f, 6f), Vector2.zero);
        text.raycastTarget = false;
        return button;
    }

    private ScrollRect CreateScrollView(Transform parent, string name, Vector2 position, Vector2 size, out Transform content)
    {
        var scrollObject = new GameObject(name);
        scrollObject.transform.SetParent(parent, false);
        var scrollRect = scrollObject.AddComponent<ScrollRect>();

        var scrollTransform = scrollObject.GetComponent<RectTransform>();
        scrollTransform.anchorMin = new Vector2(0.5f, 0.5f);
        scrollTransform.anchorMax = new Vector2(0.5f, 0.5f);
        scrollTransform.sizeDelta = size;
        scrollTransform.anchoredPosition = position;

        var viewport = CreatePanel("Viewport", scrollObject.transform, size, new Vector2(0.5f, 0.5f), Vector2.zero, new Color(0.02f, 0.03f, 0.05f, 0.70f));
        var viewportRect = viewport.GetComponent<RectTransform>();
        var mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var contentObject = new GameObject("Content");
        contentObject.transform.SetParent(viewport.transform, false);
        var contentRect = contentObject.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 0f);
        contentRect.anchoredPosition = Vector2.zero;

        var layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.padding = new RectOffset(20, 20, 15, 15);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        content = contentObject.transform;
        return scrollRect;
    }
}
