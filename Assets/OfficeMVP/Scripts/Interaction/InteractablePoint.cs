using UnityEngine;

public sealed class InteractablePoint : MonoBehaviour
{
    [SerializeField] private string targetId;
    [SerializeField] private string displayName;
    [SerializeField] private string locationId;

    public string TargetId => targetId;
    public string DisplayName => displayName;
    public string LocationId => locationId;

    public void Configure(string id, string label, string location = "")
    {
        targetId = id;
        displayName = label;
        locationId = location;
    }

    public void Interact()
    {
        Debug.Log($"[OfficeMVP] Interaction requested: {displayName} ({targetId})");
        if (OfficeBackendClient.Instance != null)
        {
            OfficeBackendClient.Instance.NotifyInteraction(this);
        }
    }
}
