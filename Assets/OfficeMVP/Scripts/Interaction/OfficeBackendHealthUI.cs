using UnityEngine;
using UnityEngine.UI;

public sealed class OfficeBackendHealthUI : MonoBehaviour
{
    private OfficeBackendClient backend;
    private Image background;
    private Text statusText;

    private void Start()
    {
        backend = OfficeBackendClient.Instance;
        BuildUi();

        if (backend != null)
        {
            backend.HealthStatusChanged += OnHealthStatusChanged;
            OnHealthStatusChanged(backend.HealthStatus, backend.HealthLatencyMilliseconds);
        }
        else
        {
            ApplyVisual(OfficeBackendHealthStatus.Disconnected, 0L);
        }
    }

    private void OnDestroy()
    {
        if (backend != null)
        {
            backend.HealthStatusChanged -= OnHealthStatusChanged;
        }
    }

    private void BuildUi()
    {
        var canvasObject = new GameObject("OfficeBackendHealthCanvas");
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        var mainCamera = Camera.main;
        canvas.renderMode = mainCamera == null ? RenderMode.ScreenSpaceOverlay : RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = mainCamera;
        canvas.planeDistance = 1f;
        canvas.sortingOrder = 540;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        var panel = new GameObject("BackendHealthStatus");
        panel.transform.SetParent(canvas.transform, false);
        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(250f, 38f);
        panelRect.anchoredPosition = new Vector2(285f, -100f);

        background = panel.AddComponent<Image>();
        background.color = new Color(0.12f, 0.12f, 0.16f, 0.94f);
        background.raycastTarget = false;

        var textObject = new GameObject("StatusText");
        textObject.transform.SetParent(panel.transform, false);
        var textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 0f);
        textRect.offsetMax = new Vector2(-12f, 0f);

        statusText = textObject.AddComponent<Text>();
        statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        statusText.fontSize = 16;
        statusText.alignment = TextAnchor.MiddleLeft;
        statusText.color = Color.white;
        statusText.raycastTarget = false;
    }

    private void OnHealthStatusChanged(OfficeBackendHealthStatus status, long latency)
    {
        ApplyVisual(status, latency);
    }

    private void ApplyVisual(OfficeBackendHealthStatus status, long latency)
    {
        if (statusText == null || background == null)
        {
            return;
        }

        switch (status)
        {
            case OfficeBackendHealthStatus.Checking:
                statusText.text = "Backend 확인 중...";
                background.color = new Color(0.34f, 0.25f, 0.04f, 0.94f);
                break;
            case OfficeBackendHealthStatus.Connected:
                statusText.text = $"Backend 연결됨  ·  {latency}ms";
                background.color = new Color(0.04f, 0.30f, 0.16f, 0.94f);
                break;
            default:
                statusText.text = "Backend 연결 끊김";
                background.color = new Color(0.38f, 0.08f, 0.08f, 0.94f);
                break;
        }
    }
}
