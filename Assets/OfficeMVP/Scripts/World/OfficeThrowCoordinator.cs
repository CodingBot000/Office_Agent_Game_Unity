using UnityEngine;

public sealed class OfficeThrowCoordinator : MonoBehaviour
{
    private sealed class PendingThrow
    {
        public string actionId;
        public string objectId;
        public string targetId;
        public Sprite sprite;
        public Vector3 launchPosition;
        public Vector3 launchScale;
        public Transform targetTransform;
        public OfficeNpcFallView fallView;
    }

    public static OfficeThrowCoordinator Instance { get; private set; }

    private PendingThrow pendingThrow;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool PrepareThrow(OfficeAvailableGameActionDto action)
    {
        if (action == null || action.family != "throw_held_object" || string.IsNullOrEmpty(action.object_id) || string.IsNullOrEmpty(action.target_id))
        {
            return false;
        }

        var heldObject = GameObject.Find($"HeldObject_{action.object_id}");
        if (heldObject == null)
        {
            return false;
        }

        var heldRenderer = heldObject.GetComponent<SpriteRenderer>();
        if (heldRenderer == null || heldRenderer.sprite == null)
        {
            return false;
        }

        InteractablePoint targetPoint = null;
        foreach (var point in FindObjectsByType<InteractablePoint>(FindObjectsInactive.Include))
        {
            if (point != null && point.TargetId == action.target_id)
            {
                targetPoint = point;
                break;
            }
        }

        if (targetPoint == null)
        {
            return false;
        }

        pendingThrow = new PendingThrow
        {
            actionId = action.id,
            objectId = action.object_id,
            targetId = action.target_id,
            sprite = heldRenderer.sprite,
            launchPosition = heldObject.transform.position,
            launchScale = heldObject.transform.lossyScale,
            targetTransform = targetPoint.transform,
            fallView = targetPoint.GetComponent<OfficeNpcFallView>(),
        };
        return true;
    }

    public bool IsThrowPendingFor(string targetId)
    {
        return pendingThrow != null && pendingThrow.targetId == targetId;
    }

    public bool IsThrowPendingForObject(string objectId)
    {
        return pendingThrow != null && pendingThrow.objectId == objectId;
    }

    public void ConfirmThrow(OfficeAvailableGameActionDto action)
    {
        if (pendingThrow == null || action == null || pendingThrow.actionId != action.id)
        {
            return;
        }

        var projectileObject = new GameObject($"ThrownObject_{action.object_id}");
        var projectile = projectileObject.AddComponent<OfficeThrownObjectProjectile>();
        projectile.Configure(
            pendingThrow.sprite,
            pendingThrow.launchPosition,
            pendingThrow.launchScale,
            pendingThrow.targetTransform,
            () =>
            {
                if (pendingThrow != null && pendingThrow.actionId == action.id)
                {
                    pendingThrow.fallView?.SetFallen(true);
                    pendingThrow = null;
                }
            }
        );
    }

    public void CancelThrow(OfficeAvailableGameActionDto action)
    {
        if (pendingThrow != null && action != null && pendingThrow.actionId == action.id)
        {
            pendingThrow = null;
        }
    }
}
