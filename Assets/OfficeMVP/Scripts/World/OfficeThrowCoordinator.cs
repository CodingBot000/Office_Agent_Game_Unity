using UnityEngine;

public sealed class OfficeThrowCoordinator : MonoBehaviour
{
    private sealed class PendingThrow
    {
        public string actionId;
        public string objectId;
        public string targetId;
        public Sprite sprite;
        public Sprite[] runningFrames;
        public Vector3 launchPosition;
        public Vector3 launchScale;
        public Transform targetTransform;
        public OfficeNpcFallView fallView;
        public bool shouldFall;
        public string impactEffect;
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

        var launchScale = heldObject.transform.lossyScale;
        var runningFrames = (Sprite[])null;
        if (OfficeItemSpriteCatalog.IsPersonItem(action.object_id))
        {
            // Person items should fly at the same scale as the character they represent,
            // rather than inheriting the hidden held-item anchor scale.
            launchScale = targetPoint.transform.lossyScale;
            var movingRight = targetPoint.transform.position.x >= heldObject.transform.position.x;
            runningFrames = OfficeItemSpriteCatalog.LoadPersonRunFrames(action.object_id, movingRight);
        }

        pendingThrow = new PendingThrow
        {
            actionId = action.id,
            objectId = action.object_id,
            targetId = action.target_id,
            sprite = heldRenderer.sprite,
            runningFrames = runningFrames,
            launchPosition = heldObject.transform.position,
            launchScale = launchScale,
            targetTransform = targetPoint.transform,
            fallView = targetPoint.GetComponent<OfficeNpcFallView>(),
            shouldFall = IsPhysicalAssault(action.object_id),
            impactEffect = ResolveImpactEffect(action.object_id),
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
            pendingThrow.runningFrames,
            pendingThrow.launchPosition,
            pendingThrow.launchScale,
            pendingThrow.targetTransform,
            pendingThrow.impactEffect,
            () =>
            {
                if (pendingThrow != null && pendingThrow.actionId == action.id)
                {
                    if (pendingThrow.shouldFall)
                    {
                        pendingThrow.fallView?.SetFallen(true);
                    }
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

    private bool IsPhysicalAssault(string objectId)
    {
        var state = FindWorldObject(objectId);
        return state == null || string.Equals(state.throw_effect, "physical_assault", System.StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveImpactEffect(string objectId)
    {
        if (string.Equals(objectId, "team_leader_person", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(objectId, "division_head_person", System.StringComparison.OrdinalIgnoreCase))
        {
            return "blink";
        }

        var state = FindWorldObject(objectId);
        return state == null || string.IsNullOrEmpty(state.throw_impact) ? "split" : state.throw_impact;
    }

    private OfficeWorldObjectDto FindWorldObject(string objectId)
    {
        if (OfficeBackendClient.Instance?.CurrentSnapshot?.world_objects == null || string.IsNullOrEmpty(objectId))
        {
            return null;
        }

        foreach (var state in OfficeBackendClient.Instance.CurrentSnapshot.world_objects)
        {
            if (state != null && state.id == objectId)
            {
                return state;
            }
        }

        return null;
    }
}
