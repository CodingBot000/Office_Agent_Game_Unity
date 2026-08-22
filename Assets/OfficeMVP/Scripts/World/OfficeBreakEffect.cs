using System.Collections;
using UnityEngine;

public sealed class OfficeBreakEffect : MonoBehaviour
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

        var effectObject = new GameObject("OfficeBreakEffect");
        effectObject.transform.position = position;
        var effect = effectObject.AddComponent<OfficeBreakEffect>();
        effect.sourceSprite = sprite;
        effect.worldScale = scale;
        effect.sortingOrder = order;
    }

    private void Start()
    {
        CreateShard("LeftShard", true);
        CreateShard("RightShard", false);
        StartCoroutine(Animate());
    }

    private void CreateShard(string objectName, bool left)
    {
        var shard = new GameObject(objectName);
        shard.transform.SetParent(transform, false);
        shard.transform.localPosition = Vector3.zero;

        var renderer = shard.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateHalfSprite(sourceSprite, left);
        renderer.sortingOrder = sortingOrder;
        renderer.color = Color.white;
    }

    private Sprite CreateHalfSprite(Sprite source, bool left)
    {
        var rect = source.textureRect;
        var halfWidth = Mathf.Max(1f, Mathf.Floor(rect.width * 0.5f));
        var x = left ? rect.x : rect.x + rect.width - halfWidth;
        var pivot = left ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);

        return Sprite.Create(
            source.texture,
            new Rect(x, rect.y, halfWidth, rect.height),
            pivot,
            source.pixelsPerUnit,
            0,
            SpriteMeshType.FullRect
        );
    }

    private IEnumerator Animate()
    {
        transform.localScale = worldScale;

        var left = transform.Find("LeftShard");
        var right = transform.Find("RightShard");
        var leftRenderer = left == null ? null : left.GetComponent<SpriteRenderer>();
        var rightRenderer = right == null ? null : right.GetComponent<SpriteRenderer>();

        const float duration = 1.10f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var progress = Mathf.Clamp01(elapsed / duration);
            var eased = 1f - Mathf.Pow(1f - progress, 3f);

            if (left != null)
            {
                left.localPosition = new Vector3(-0.34f * eased, 0.22f * eased, 0f);
                left.localRotation = Quaternion.Euler(0f, 0f, 28f * eased);
            }

            if (right != null)
            {
                right.localPosition = new Vector3(0.34f * eased, 0.22f * eased, 0f);
                right.localRotation = Quaternion.Euler(0f, 0f, -28f * eased);
            }

            if (leftRenderer != null)
            {
                leftRenderer.color = new Color(1f, 1f, 1f, 1f - progress);
            }

            if (rightRenderer != null)
            {
                rightRenderer.color = new Color(1f, 1f, 1f, 1f - progress);
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}
