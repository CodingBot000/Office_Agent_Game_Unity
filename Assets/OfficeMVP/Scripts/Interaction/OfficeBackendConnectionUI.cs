using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class OfficeBackendConnectionUI : MonoBehaviour
{
    private Canvas canvas;
    private GameObject panel;
    private Button localButton;
    private Button remoteButton;
    private Text localStatus;
    private Text remoteStatus;
    private Text selectionStatus;
    private OfficeBackendClient backend;

    private void Start()
    {
        backend = OfficeBackendClient.Instance;
        EnsureEventSystem();
        BuildUi();

        if (backend == null)
        {
            selectionStatus.text = "Backend client를 찾을 수 없습니다.";
            return;
        }

        backend.EndpointHealthChanged += OnEndpointHealthChanged;
        backend.BeginConnectionSelection();
        Render();
    }

    private void OnDestroy()
    {
        if (backend != null)
        {
            backend.EndpointHealthChanged -= OnEndpointHealthChanged;
        }
    }

    private void OnEndpointHealthChanged(OfficeBackendEndpoint endpoint, OfficeBackendHealthStatus status, long latency)
    {
        Render();
    }

    private void Render()
    {
        if (backend == null || panel == null)
        {
            return;
        }

        var localConfigured = !string.IsNullOrEmpty(backend.LocalBaseUrl);
        var remoteConfigured = !string.IsNullOrEmpty(backend.RemoteBaseUrl);
        var localHealth = backend.GetEndpointHealth(OfficeBackendEndpoint.Local);
        var remoteHealth = backend.GetEndpointHealth(OfficeBackendEndpoint.Remote);

        localStatus.text = FormatEndpointStatus("LOCAL", backend.LocalBaseUrl, localConfigured ? localHealth : OfficeBackendHealthStatus.Disconnected, backend.GetEndpointLatency(OfficeBackendEndpoint.Local), localConfigured);
        remoteStatus.text = FormatEndpointStatus("REMOTE", backend.RemoteBaseUrl, remoteConfigured ? remoteHealth : OfficeBackendHealthStatus.Disconnected, backend.GetEndpointLatency(OfficeBackendEndpoint.Remote), remoteConfigured);
        localStatus.color = HealthColor(localConfigured ? localHealth : OfficeBackendHealthStatus.Disconnected);
        remoteStatus.color = HealthColor(remoteConfigured ? remoteHealth : OfficeBackendHealthStatus.Disconnected);
        localButton.interactable = localConfigured && localHealth == OfficeBackendHealthStatus.Connected;
        remoteButton.interactable = remoteConfigured && remoteHealth == OfficeBackendHealthStatus.Connected;

        if (backend.IsReady)
        {
            panel.SetActive(false);
            return;
        }

        selectionStatus.text = remoteConfigured
            ? "연결할 백엔드를 선택하세요. 두 서버의 상태를 확인한 뒤 활성화된 버튼만 선택할 수 있습니다."
            : "로컬 서버와 원격 서버 상태를 확인 중입니다. 원격 URL은 OfficeMvpBootstrap에서 설정할 수 있습니다.";
    }

    private void SelectEndpoint(OfficeBackendEndpoint endpoint)
    {
        if (backend == null || !backend.SelectEndpoint(endpoint))
        {
            selectionStatus.text = "선택한 서버가 아직 연결되지 않았습니다.";
            return;
        }

        panel.SetActive(false);
    }

    private string FormatEndpointStatus(string label, string url, OfficeBackendHealthStatus status, long latency, bool configured)
    {
        if (!configured)
        {
            return $"{label}\nURL 미설정";
        }

        var state = status == OfficeBackendHealthStatus.Checking
            ? "CHECKING..."
            : status == OfficeBackendHealthStatus.Connected ? $"ONLINE · {latency}ms" : "OFFLINE";
        return $"{label}\n{state}\n{url}";
    }

    private Color HealthColor(OfficeBackendHealthStatus status)
    {
        switch (status)
        {
            case OfficeBackendHealthStatus.Connected:
                return new Color(0.45f, 0.92f, 0.70f);
            case OfficeBackendHealthStatus.Checking:
                return new Color(0.96f, 0.74f, 0.35f);
            default:
                return new Color(0.95f, 0.45f, 0.42f);
        }
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
        var canvasObject = new GameObject("BackendConnectionCanvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        panel = CreatePanel(canvas.transform, new Vector2(1280f, 720f), new Color(0.025f, 0.035f, 0.06f, 0.98f));
        CreateText(panel.transform, "Backend Connection", 30, Color.white, TextAnchor.MiddleCenter, new Vector2(680f, 48f), new Vector2(0f, 120f));
        CreateText(panel.transform, "게임 시작 전에 연결할 백엔드를 선택하세요", 17, new Color(0.70f, 0.80f, 0.90f), TextAnchor.MiddleCenter, new Vector2(680f, 30f), new Vector2(0f, 78f));

        localButton = CreateButton(panel.transform, "LOCAL", new Vector2(360f, 170f), new Vector2(-205f, -50f));
        localButton.onClick.AddListener(() => SelectEndpoint(OfficeBackendEndpoint.Local));
        localStatus = localButton.GetComponentInChildren<Text>();

        remoteButton = CreateButton(panel.transform, "REMOTE", new Vector2(360f, 170f), new Vector2(205f, -50f));
        remoteButton.onClick.AddListener(() => SelectEndpoint(OfficeBackendEndpoint.Remote));
        remoteStatus = remoteButton.GetComponentInChildren<Text>();

        selectionStatus = CreateText(panel.transform, "두 서버의 /health 상태를 확인하는 중...", 14, new Color(0.62f, 0.72f, 0.82f), TextAnchor.MiddleCenter, new Vector2(900f, 52f), new Vector2(0f, -180f));
        CreateText(panel.transform, "LOCAL: 127.0.0.1:8000  ·  REMOTE: OfficeMvpBootstrap 설정값", 11, new Color(0.38f, 0.48f, 0.58f), TextAnchor.MiddleCenter, new Vector2(900f, 26f), new Vector2(0f, -225f));
    }

    private GameObject CreatePanel(Transform parent, Vector2 size, Color color)
    {
        var panelObject = new GameObject("ConnectionPanel");
        panelObject.transform.SetParent(parent, false);
        var rect = panelObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        panelObject.AddComponent<Image>().color = color;
        return panelObject;
    }

    private Button CreateButton(Transform parent, string label, Vector2 size, Vector2 position)
    {
        var buttonObject = new GameObject(label + "Button");
        buttonObject.transform.SetParent(parent, false);
        var rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        var image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.08f, 0.16f, 0.22f, 1f);
        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = new Color(0.08f, 0.16f, 0.22f, 1f);
        colors.highlightedColor = new Color(0.12f, 0.32f, 0.40f, 1f);
        colors.pressedColor = new Color(0.06f, 0.12f, 0.18f, 1f);
        colors.disabledColor = new Color(0.13f, 0.14f, 0.18f, 0.55f);
        button.colors = colors;
        CreateText(buttonObject.transform, label, 16, Color.white, TextAnchor.MiddleCenter, size - new Vector2(18f, 18f), Vector2.zero);
        return button;
    }

    private Text CreateText(Transform parent, string value, int fontSize, Color color, TextAnchor alignment, Vector2 size, Vector2 position)
    {
        var textObject = new GameObject("Text");
        textObject.transform.SetParent(parent, false);
        var rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
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
}
