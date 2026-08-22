using UnityEngine;
using UnityEngine.UI;

public static class OfficeEmotionText
{
    public static string ToKorean(string emotion)
    {
        switch (emotion)
        {
            case "neutral":
                return "중립";
            case "tense":
                return "긴장";
            case "worried":
                return "걱정";
            case "guarded":
                return "경계";
            case "urgent":
                return "초조";
            case "defensive":
                return "방어적";
            case "relieved":
                return "안도";
            case "afraid":
                return "두려움";
            case "shocked":
                return "충격";
            case "angry":
                return "분노";
            case "cautiously_relieved":
                return "조심스러운 안도";
            case "supported":
                return "지지받음";
            case "attentive":
                return "주의 깊음";
            default:
                return string.IsNullOrEmpty(emotion) ? "알 수 없음" : emotion;
        }
    }

    public static Color ToColor(string emotion)
    {
        switch (emotion)
        {
            case "angry":
            case "afraid":
            case "shocked":
                return new Color(1f, 0.55f, 0.48f);
            case "relieved":
            case "cautiously_relieved":
            case "supported":
                return new Color(0.58f, 0.95f, 0.72f);
            case "neutral":
            case "attentive":
                return new Color(0.82f, 0.88f, 0.96f);
            default:
                return new Color(1f, 0.86f, 0.50f);
        }
    }
}

public sealed class OfficeCharacterEmotionLabel : MonoBehaviour
{
    private string targetId;
    private Text emotionText;

    public string TargetId => targetId;

    public void Configure(string id)
    {
        targetId = id;
        BuildLabel();
        SetEmotion("neutral");
    }

    public void SetEmotion(string emotion)
    {
        if (emotionText == null)
        {
            return;
        }

        emotionText.text = OfficeEmotionText.ToKorean(emotion);
        emotionText.color = OfficeEmotionText.ToColor(emotion);
    }

    private void BuildLabel()
    {
        var canvasObject = new GameObject("EmotionLabel");
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = new Vector3(0f, 1.38f, 0f);
        canvasObject.transform.localScale = Vector3.one * 0.005f;

        var labelCanvas = canvasObject.AddComponent<Canvas>();
        labelCanvas.renderMode = RenderMode.WorldSpace;
        labelCanvas.overrideSorting = true;
        labelCanvas.sortingOrder = 205;

        var rect = canvasObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(260f, 30f);

        var background = canvasObject.AddComponent<Image>();
        background.color = new Color(0.02f, 0.03f, 0.05f, 0.78f);
        background.raycastTarget = false;

        var textObject = new GameObject("Text");
        textObject.transform.SetParent(canvasObject.transform, false);
        var textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        emotionText = textObject.AddComponent<Text>();
        emotionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        emotionText.fontSize = 18;
        emotionText.alignment = TextAnchor.MiddleCenter;
        emotionText.raycastTarget = false;

        var shadow = textObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(2f, -2f);
    }
}
