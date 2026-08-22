using UnityEngine;

public sealed class OfficeLocationZone : MonoBehaviour
{
    [SerializeField] private string location;

    private OfficeBackendClient backend;
    private Collider2D zoneCollider;
    private Transform playerTransform;
    private bool playerInside;
    private float nextRetryTime;

    public void Configure(string value)
    {
        location = value;
    }

    private void Start()
    {
        backend = OfficeBackendClient.Instance;
        zoneCollider = GetComponent<Collider2D>();

        var player = GameObject.Find("Player");
        playerTransform = player == null ? null : player.transform;

        if (backend != null)
        {
            backend.SessionReady += OnBackendSessionReady;
        }
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            var player = GameObject.Find("Player");
            playerTransform = player == null ? null : player.transform;
        }

        if (playerTransform != null && zoneCollider != null)
        {
            playerInside = zoneCollider.OverlapPoint(playerTransform.position);
        }

        TrySubmitLocation();
    }

    private void OnDestroy()
    {
        if (backend != null)
        {
            backend.SessionReady -= OnBackendSessionReady;
        }
    }

    private void OnBackendSessionReady()
    {
        TrySubmitLocation();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerMovement2D>() == null)
        {
            return;
        }

        playerInside = true;
        TrySubmitLocation();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerMovement2D>() != null)
        {
            playerInside = false;
        }
    }

    private void TrySubmitLocation()
    {
        if (!playerInside || backend == null || !backend.IsReady || backend.IsRequestInFlight || backend.IsLocationSyncing)
        {
            return;
        }

        if (backend.CurrentSnapshot != null && backend.CurrentSnapshot.current_location == location)
        {
            return;
        }

        if (Time.unscaledTime < nextRetryTime)
        {
            return;
        }

        nextRetryTime = Time.unscaledTime + 0.5f;
        backend.SubmitMove(
            location,
            response => Debug.Log($"[OfficeMVP] Backend location synchronized: {location}"),
            error => Debug.LogError($"[OfficeMVP] Backend location sync failed: {error}")
        );
    }
}
