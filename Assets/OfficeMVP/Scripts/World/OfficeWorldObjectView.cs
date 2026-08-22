using System;
using UnityEngine;

public sealed class OfficeWorldObjectView : MonoBehaviour
{
    [SerializeField] private string objectId;
    [SerializeField] private string initialLocation;

    private SpriteRenderer spriteRenderer;
    private Color normalColor = Color.white;
    private Vector3 initialLocalPosition;
    private bool configured;

    public string ObjectId => objectId;
    public string InitialLocation => initialLocation;
    public Sprite Sprite => spriteRenderer == null ? GetComponent<SpriteRenderer>()?.sprite : spriteRenderer.sprite;
    public Vector3 WorldScale => transform.lossyScale;

    public void Configure(string id, string location)
    {
        objectId = id;
        initialLocation = location;
        initialLocalPosition = transform.localPosition;
        configured = true;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            normalColor = spriteRenderer.color;
        }
    }

    public void Apply(OfficeWorldObjectDto state)
    {
        if (state == null || string.IsNullOrEmpty(state.id) || state.id != objectId)
        {
            return;
        }

        EnsureRenderer();
        if (spriteRenderer == null)
        {
            return;
        }

        var destroyed = string.Equals(state.condition, "destroyed", StringComparison.OrdinalIgnoreCase);
        var heldByPlayer = string.Equals(state.holder_id, "player", StringComparison.OrdinalIgnoreCase);
        spriteRenderer.enabled = !destroyed && !heldByPlayer;

        if (destroyed)
        {
            return;
        }

        if (string.Equals(state.condition, "damaged", StringComparison.OrdinalIgnoreCase))
        {
            spriteRenderer.color = new Color(normalColor.r * 0.60f, normalColor.g * 0.60f, normalColor.b * 0.60f, normalColor.a);
        }
        else if (!string.IsNullOrEmpty(state.holder_id))
        {
            spriteRenderer.color = new Color(normalColor.r * 0.82f, normalColor.g * 0.92f, normalColor.b, normalColor.a);
        }
        else
        {
            spriteRenderer.color = normalColor;
        }
    }

    public void SetDroppedPosition(Vector3 position)
    {
        transform.position = position;
    }

    public void RestoreInitialPosition()
    {
        if (configured)
        {
            transform.localPosition = initialLocalPosition;
        }
    }

    private void EnsureRenderer()
    {
        if (spriteRenderer != null)
        {
            return;
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            normalColor = spriteRenderer.color;
        }
    }
}
