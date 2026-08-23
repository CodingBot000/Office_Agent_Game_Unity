using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class OfficePlayerActionUI : MonoBehaviour
{
    private enum MenuStep
    {
        Closed,
        SelectObject,
        SelectTarget,
    }

    private Canvas canvas;
    private OfficeBackendClient backend;
    private Transform player;
    private SpriteRenderer playerRenderer;
    private RectTransform actionAnchor;
    private Button actionButton;
    private GameObject menuPanel;
    private Text menuTitle;
    private Text menuStatus;
    private Transform menuContent;
    private string selectedObjectId;
    private MenuStep step;
    private bool playerAnchorVisible;

    private void Start()
    {
        backend = OfficeBackendClient.Instance;
        player = GameObject.Find("Player")?.transform;
        playerRenderer = player == null ? null : player.GetComponent<SpriteRenderer>();

        EnsureEventSystem();
        BuildUi();
        CloseMenu();

        if (backend != null)
        {
            backend.SnapshotUpdated += OnSnapshotUpdated;
        }
    }

    private void OnDestroy()
    {
        if (backend != null)
        {
            backend.SnapshotUpdated -= OnSnapshotUpdated;
        }
    }

    private void Update()
    {
        if (player == null)
        {
            player = GameObject.Find("Player")?.transform;
            playerRenderer = player == null ? null : player.GetComponent<SpriteRenderer>();
        }

        UpdateActionAnchor();
        UpdateActionButtonVisibility();
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
        var canvasObject = new GameObject("PlayerActionCanvas");
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = Camera.main == null ? RenderMode.ScreenSpaceOverlay : RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = Camera.main;
        canvas.planeDistance = 0.9f;
        canvas.sortingOrder = 520;
        canvasObject.AddComponent<GraphicRaycaster>();

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        actionAnchor = new GameObject("PlayerActionAnchor").AddComponent<RectTransform>();
        actionAnchor.SetParent(canvas.transform, false);
        actionAnchor.sizeDelta = new Vector2(74f, 30f);

        actionButton = CreateButton(actionAnchor, "PlayerActionButton", "액션", new Vector2(74f, 30f), Vector2.zero);
        actionButton.onClick.AddListener(OpenObjectStep);

        menuPanel = CreatePanel(
            "PlayerActionMenu",
            canvas.transform,
            new Vector2(590f, 370f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 45f),
            new Color(0.035f, 0.05f, 0.075f, 0.96f)
        );

        menuTitle = CreateText(menuPanel.transform, "PlayerActionTitle", "소지 물건 선택", 23, Color.white, TextAnchor.MiddleLeft, new Vector2(430f, 34f), new Vector2(-55f, 145f));
        var closeButton = CreateButton(menuPanel.transform, "PlayerActionClose", "닫기", new Vector2(78f, 32f), new Vector2(235f, 145f));
        closeButton.onClick.AddListener(CloseMenu);

        menuContent = CreateContent(menuPanel.transform);
        menuStatus = CreateText(menuPanel.transform, "PlayerActionStatus", "", 15, new Color(0.72f, 0.82f, 0.92f), TextAnchor.MiddleLeft, new Vector2(510f, 28f), new Vector2(0f, -148f));
    }

    private void UpdateActionAnchor()
    {
        if (player == null || actionAnchor == null || canvas == null)
        {
            return;
        }

        var camera = canvas.worldCamera ?? Camera.main;
        if (camera == null)
        {
            return;
        }

        var anchorWorldPosition = player.position + new Vector3(0f, 2.8f, 0f);
        if (playerRenderer != null && playerRenderer.sprite != null)
        {
            anchorWorldPosition = new Vector3(
                playerRenderer.bounds.center.x,
                playerRenderer.bounds.max.y + 0.55f,
                player.position.z
            );
        }

        var screenPosition = camera.WorldToScreenPoint(anchorWorldPosition);
        var visible = screenPosition.z > 0f
            && screenPosition.x >= 0f
            && screenPosition.x <= Screen.width
            && screenPosition.y >= 0f
            && screenPosition.y <= Screen.height;

        playerAnchorVisible = visible;
        if (!visible)
        {
            actionAnchor.gameObject.SetActive(false);
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            screenPosition,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : camera,
            out var localPosition
        );
        actionAnchor.anchoredPosition = localPosition;
    }

    private void UpdateActionButtonVisibility()
    {
        if (actionAnchor == null)
        {
            return;
        }

        var isOverlayOpen = IsPanelOpen("DialoguePanel") || IsPanelOpen("ActionPanel");
        var canOpen = playerAnchorVisible && HasHeldThrowActions() && !isOverlayOpen;
        if (actionAnchor.gameObject.activeSelf != canOpen)
        {
            actionAnchor.gameObject.SetActive(canOpen);
        }

        if (!canOpen && step != MenuStep.Closed)
        {
            CloseMenu();
        }
    }

    private static bool IsPanelOpen(string objectName)
    {
        var panel = GameObject.Find(objectName);
        return panel != null && panel.activeInHierarchy;
    }

    private bool HasHeldThrowActions()
    {
        if (backend == null || !backend.IsReady || backend.CurrentSnapshot == null || backend.CurrentSnapshot.available_game_actions == null)
        {
            return false;
        }

        foreach (var action in backend.CurrentSnapshot.available_game_actions)
        {
            if (IsEnabledThrowAction(action))
            {
                return true;
            }
        }

        return false;
    }

    private void OpenObjectStep()
    {
        if (!HasHeldThrowActions())
        {
            return;
        }

        step = MenuStep.SelectObject;
        selectedObjectId = null;
        menuPanel.SetActive(true);
        RebuildMenu();
    }

    private void OpenTargetStep(string objectId)
    {
        selectedObjectId = objectId;
        step = MenuStep.SelectTarget;
        RebuildMenu();
    }

    private void CloseMenu()
    {
        step = MenuStep.Closed;
        selectedObjectId = null;
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
        }
    }

    private void OnSnapshotUpdated(OfficeSnapshotDto snapshot)
    {
        if (step == MenuStep.Closed)
        {
            return;
        }

        if (!HasHeldThrowActions())
        {
            CloseMenu();
            return;
        }

        RebuildMenu();
    }

    private void RebuildMenu()
    {
        if (menuContent == null)
        {
            return;
        }

        for (var index = menuContent.childCount - 1; index >= 0; index--)
        {
            Destroy(menuContent.GetChild(index).gameObject);
        }

        if (step == MenuStep.SelectObject)
        {
            menuTitle.text = "던질 물건 선택";
            menuStatus.text = "물건을 고른 뒤 대상 NPC를 선택하세요.";

            var objectIds = new HashSet<string>();
            foreach (var action in GetThrowActions())
            {
                if (!string.IsNullOrEmpty(action.object_id))
                {
                    objectIds.Add(action.object_id);
                }
            }

            foreach (var objectId in objectIds)
            {
                var objectState = FindWorldObject(objectId);
                var label = objectState == null
                    ? OfficeDisplayText.FormatItemNameRich(objectId)
                    : OfficeDisplayText.FormatItemNameRich(objectState.name);
                var button = CreateButton(menuContent, $"ThrowObject_{objectId}", $"{label} 던지기", new Vector2(500f, 54f), Vector2.zero);
                var capturedObjectId = objectId;
                button.onClick.AddListener(() => OpenTargetStep(capturedObjectId));
            }
            return;
        }

        if (step == MenuStep.SelectTarget)
        {
            var objectState = FindWorldObject(selectedObjectId);
            var objectName = objectState == null
                ? OfficeDisplayText.FormatItemNameRich(selectedObjectId)
                : OfficeDisplayText.FormatItemNameRich(objectState.name);
            menuTitle.text = $"{objectName} 대상 선택";
            menuStatus.text = "대상을 선택하면 서버 검증 후 투척합니다.";

            var backButton = CreateButton(menuContent, "PlayerActionBack", "← 물건 다시 선택", new Vector2(500f, 36f), Vector2.zero);
            backButton.onClick.AddListener(OpenObjectStep);

            foreach (var action in GetThrowActions())
            {
                if (action.object_id != selectedObjectId || string.IsNullOrEmpty(action.target_id))
                {
                    continue;
                }

                var npc = FindNpc(action.target_id);
                if (npc == null || npc.is_fallen)
                {
                    continue;
                }

                var label = $"{npc.name} · {npc.role}\n{ResolveNpcLocation(action.target_id)} · {npc.dynamic_state.emotion}";
                var button = CreateButton(menuContent, $"ThrowTarget_{action.target_id}", label, new Vector2(500f, 62f), Vector2.zero);
                button.interactable = action.enabled;
                var capturedAction = action;
                button.onClick.AddListener(() => SubmitThrow(capturedAction));
            }
        }
    }

    private void SubmitThrow(OfficeAvailableGameActionDto action)
    {
        if (backend == null || !backend.IsReady || !action.enabled)
        {
            menuStatus.text = "현재 액션을 실행할 수 없습니다.";
            return;
        }

        var coordinator = OfficeThrowCoordinator.Instance;
        var prepared = coordinator != null && coordinator.PrepareThrow(action);
        var actionObject = FindWorldObject(action.object_id);
        menuStatus.text = actionObject == null
            ? $"{OfficeDisplayText.EscapeRichText(action.label)} 처리 중..."
            : $"{OfficeDisplayText.FormatActionLabel(action.label, actionObject.name)} 처리 중...";

        backend.SubmitGameAction(
            action.id,
            response =>
            {
                var responseObjects = response.snapshot == null
                    ? backend.CurrentSnapshot == null ? null : backend.CurrentSnapshot.world_objects
                    : response.snapshot.world_objects;
                var responseMessage = OfficeDisplayText.FormatKnownItemNames(response.message, responseObjects);
                if (response.blocked)
                {
                    coordinator?.CancelThrow(action);
                    menuStatus.text = $"차단됨: {responseMessage}";
                    RebuildMenu();
                    return;
                }

                if (prepared)
                {
                    coordinator.ConfirmThrow(action);
                }

                CloseMenu();
            },
            error =>
            {
                coordinator?.CancelThrow(action);
                menuStatus.text = error;
            }
        );
    }

    private List<OfficeAvailableGameActionDto> GetThrowActions()
    {
        var actions = new List<OfficeAvailableGameActionDto>();
        if (backend == null || backend.CurrentSnapshot == null || backend.CurrentSnapshot.available_game_actions == null)
        {
            return actions;
        }

        foreach (var action in backend.CurrentSnapshot.available_game_actions)
        {
            if (IsEnabledThrowAction(action))
            {
                actions.Add(action);
            }
        }

        return actions;
    }

    private static bool IsEnabledThrowAction(OfficeAvailableGameActionDto action)
    {
        return action != null
            && action.enabled
            && string.Equals(action.family, "throw_held_object", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(action.object_id)
            && !string.IsNullOrEmpty(action.target_id);
    }

    private OfficeWorldObjectDto FindWorldObject(string objectId)
    {
        if (backend?.CurrentSnapshot?.world_objects == null)
        {
            return null;
        }

        foreach (var worldObject in backend.CurrentSnapshot.world_objects)
        {
            if (worldObject != null && worldObject.id == objectId)
            {
                return worldObject;
            }
        }

        return null;
    }

    private OfficeNpcDto FindNpc(string npcId)
    {
        if (backend?.CurrentSnapshot?.npcs == null)
        {
            return null;
        }

        foreach (var npc in backend.CurrentSnapshot.npcs)
        {
            if (npc != null && npc.id == npcId)
            {
                return npc;
            }
        }

        return null;
    }

    private static string ResolveNpcLocation(string npcId)
    {
        if (npcId == "backend_01" || npcId == "frontend_01")
        {
            return "DEV";
        }

        if (npcId == "qa_01")
        {
            return "QA";
        }

        return "PM";
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
        panel.AddComponent<Image>().color = color;
        return panel;
    }

    private Transform CreateContent(Transform parent)
    {
        var viewport = CreatePanel("PlayerActionViewport", parent, new Vector2(530f, 245f), new Vector2(0.5f, 0.5f), new Vector2(0f, -5f), new Color(0.02f, 0.03f, 0.05f, 0.72f));
        var viewportRect = viewport.GetComponent<RectTransform>();
        var mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var contentObject = new GameObject("PlayerActionContent");
        contentObject.transform.SetParent(viewport.transform, false);
        var contentRect = contentObject.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = Vector2.zero;
        contentRect.anchoredPosition = Vector2.zero;

        var layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(12, 12, 10, 10);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = viewport.AddComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        return contentObject.transform;
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
        text.supportRichText = true;
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

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = new Color(0.12f, 0.30f, 0.38f, 0.98f);
        colors.highlightedColor = new Color(0.20f, 0.52f, 0.56f, 1f);
        colors.pressedColor = new Color(0.08f, 0.20f, 0.26f, 1f);
        colors.disabledColor = new Color(0.18f, 0.18f, 0.22f, 0.70f);
        button.colors = colors;

        var layout = buttonObject.AddComponent<LayoutElement>();
        layout.minHeight = size.y;
        layout.preferredHeight = size.y;
        layout.flexibleWidth = 1f;

        CreateText(buttonObject.transform, "Label", label, 16, Color.white, TextAnchor.MiddleCenter, size - new Vector2(12f, 6f), Vector2.zero);
        return button;
    }
}
