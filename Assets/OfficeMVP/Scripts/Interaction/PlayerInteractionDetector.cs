using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public sealed class PlayerInteractionDetector : MonoBehaviour
{
    [SerializeField] private float interactionDistance = 2.25f;

    private InteractablePoint[] allPoints;
    private InteractablePoint current;

    public InteractablePoint Current => current;

    private void Start()
    {
        allPoints = FindObjectsByType<InteractablePoint>(FindObjectsSortMode.None);
    }

    private void Update()
    {
        if (OfficeBackendClient.Instance != null && !OfficeBackendClient.Instance.IsReady)
        {
            current = null;
            return;
        }

        RefreshCurrent();

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.eKey.wasPressedThisFrame && current != null)
        {
            current.Interact();
        }
    }

    private void RefreshCurrent()
    {
        if (allPoints == null || allPoints.Length == 0)
        {
            allPoints = FindObjectsByType<InteractablePoint>(FindObjectsSortMode.None);
        }

        InteractablePoint nearest = null;
        var nearestDistance = float.MaxValue;

        foreach (var point in allPoints)
        {
            if (point == null || !point.isActiveAndEnabled)
            {
                continue;
            }

            var distance = (point.transform.position - transform.position).sqrMagnitude;
            if (distance <= interactionDistance * interactionDistance && distance < nearestDistance)
            {
                nearest = point;
                nearestDistance = distance;
            }
        }

        if (nearest == current)
        {
            return;
        }

        current = nearest;
        if (current != null)
        {
            Debug.Log($"[OfficeMVP] Interaction target: {current.DisplayName} ({current.TargetId})");
        }
    }
}
