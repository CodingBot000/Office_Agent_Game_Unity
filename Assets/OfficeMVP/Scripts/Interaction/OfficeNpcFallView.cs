using UnityEngine;

public sealed class OfficeNpcFallView : MonoBehaviour
{
    private SpriteRenderer originalRenderer;
    private InteractablePoint interactable;
    private Collider2D[] colliders;
    private GameObject fallenVisual;
    private bool isFallen;

    public bool IsFallen => isFallen;

    public void SetFallen(bool value)
    {
        EnsureReferences();

        if (value == isFallen)
        {
            return;
        }

        isFallen = value;
        if (isFallen)
        {
            CreateFallenVisual();
            if (originalRenderer != null)
            {
                originalRenderer.enabled = false;
            }

            // Keep colliders and InteractablePoint enabled so the player can inspect this NPC's items.
        }
        else
        {
            if (fallenVisual != null)
            {
                Destroy(fallenVisual);
                fallenVisual = null;
            }

            if (originalRenderer != null)
            {
                originalRenderer.enabled = true;
            }

            // Colliders and InteractablePoint remain enabled while the NPC is comatose.
        }
    }

    private void EnsureReferences()
    {
        if (originalRenderer == null)
        {
            originalRenderer = GetComponent<SpriteRenderer>();
        }

        if (interactable == null)
        {
            interactable = GetComponent<InteractablePoint>();
        }

        if (colliders == null || colliders.Length == 0)
        {
            colliders = GetComponents<Collider2D>();
        }
    }

    private void CreateFallenVisual()
    {
        if (fallenVisual != null || originalRenderer == null || originalRenderer.sprite == null)
        {
            return;
        }

        fallenVisual = new GameObject("FallenVisual");
        fallenVisual.transform.SetParent(transform, false);
        fallenVisual.transform.localPosition = new Vector3(0.18f, 0.26f, -0.02f);
        fallenVisual.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        fallenVisual.transform.localScale = Vector3.one;

        var renderer = fallenVisual.AddComponent<SpriteRenderer>();
        renderer.sprite = originalRenderer.sprite;
        renderer.color = originalRenderer.color;
        renderer.sortingOrder = originalRenderer.sortingOrder;
    }
}
