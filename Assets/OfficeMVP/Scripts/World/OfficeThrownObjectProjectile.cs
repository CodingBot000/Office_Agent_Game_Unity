using System;
using System.Collections;
using UnityEngine;

public sealed class OfficeThrownObjectProjectile : MonoBehaviour
{
    private Sprite sprite;
    private Sprite[] animationFrames;
    private SpriteRenderer spriteRenderer;
    private Vector3 launchPosition;
    private Vector3 launchScale;
    private Transform targetTransform;
    private string impactEffect;
    private Action onImpact;

    public void Configure(
        Sprite sourceSprite,
        Sprite[] runningFrames,
        Vector3 sourcePosition,
        Vector3 sourceScale,
        Transform target,
        string impactStyle,
        Action impactCallback
    )
    {
        sprite = sourceSprite;
        animationFrames = runningFrames;
        launchPosition = sourcePosition;
        launchScale = sourceScale;
        targetTransform = target;
        impactEffect = impactStyle;
        onImpact = impactCallback;

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.sortingOrder = 70;
        transform.position = launchPosition;
        transform.localScale = launchScale;

        StartCoroutine(FlyToTarget());
    }

    private IEnumerator FlyToTarget()
    {
        var elapsed = 0f;
        const float duration = 1.44f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var progress = Mathf.Clamp01(elapsed / duration);
            var targetPosition = targetTransform == null
                ? transform.position
                : targetTransform.position + new Vector3(0f, 0.55f, -0.05f);

            transform.position = Vector3.Lerp(launchPosition, targetPosition, progress);
            transform.Rotate(0f, 0f, -1080f * Time.deltaTime);
            UpdateRunningFrame(elapsed);
            yield return null;
        }

        Impact();
    }

    private void Impact()
    {
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            sprite = spriteRenderer.sprite;
        }

        if (string.Equals(impactEffect, "blink", StringComparison.OrdinalIgnoreCase))
        {
            OfficeBlinkEffect.Play(sprite, transform.position, transform.localScale, 72);
        }
        else
        {
            OfficeBreakEffect.Play(sprite, transform.position, transform.localScale, 72);
        }

        onImpact?.Invoke();
        Destroy(gameObject);
    }

    private void UpdateRunningFrame(float elapsed)
    {
        if (animationFrames == null || animationFrames.Length == 0 || spriteRenderer == null)
        {
            return;
        }

        var frameIndex = Mathf.FloorToInt(elapsed / 0.09f) % animationFrames.Length;
        spriteRenderer.sprite = animationFrames[frameIndex];
    }
}
