using System.Collections;
using UnityEngine;

public sealed class OfficeBlinkEffect : MonoBehaviour
{
    private Sprite sourceSprite;
    private Vector3 worldScale = Vector3.one;
    private int sortingOrder = 60;

    public static void Play(Sprite sprite, Vector3 position, Vector3 scale, int order)
    {
        if (sprite == null)
        {
            return;
        }

        var effectObject = new GameObject("OfficeBlinkEffect");
        effectObject.transform.position = position;
        var effect = effectObject.AddComponent<OfficeBlinkEffect>();
        effect.sourceSprite = sprite;
        effect.worldScale = scale;
        effect.sortingOrder = order;
    }

    private void Start()
    {
        var renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sourceSprite;
        renderer.sortingOrder = sortingOrder;
        transform.localScale = worldScale;
        StartCoroutine(Animate(renderer));
    }

    private IEnumerator Animate(SpriteRenderer renderer)
    {
        const float duration = 0.48f;
        var elapsed = 0f;
        var blinkElapsed = 0f;
        var visible = true;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            blinkElapsed += Time.deltaTime;
            if (blinkElapsed >= 0.08f)
            {
                visible = !visible;
                renderer.enabled = visible;
                blinkElapsed = 0f;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}
