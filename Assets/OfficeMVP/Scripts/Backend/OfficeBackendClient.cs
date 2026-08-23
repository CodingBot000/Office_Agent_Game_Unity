using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public enum OfficeBackendHealthStatus
{
    Checking,
    Connected,
    Disconnected
}

public enum OfficeBackendEndpoint
{
    Local,
    Remote
}

[Serializable]
public sealed class OfficeNpcDynamicStateDto
{
    public string emotion;
    public int stress;
    public int trust_toward_player;
    public int cooperation;
}

[Serializable]
public sealed class OfficeNpcDto
{
    public string id;
    public string name;
    public string role;
    public OfficeNpcDynamicStateDto dynamic_state;
    public string physical_state;
    public bool is_fallen;
}

[Serializable]
public sealed class OfficeWorldObjectDto
{
    public string id;
    public string name;
    public string owner_id;
    public string location;
    public string evidence_id;
    public bool portable;
    public bool destructible;
    public string holder_id;
    public string condition;
    public bool is_dropped;
    public string throw_effect;
    public int throw_severity;
    public string throw_impact;
}

[Serializable]
public sealed class OfficePlayerInventoryDto
{
    public string[] held_object_ids;
    public int max_held_objects;
    public bool unlimited;
}

[Serializable]
public sealed class OfficeEvidenceDto
{
    public string id;
    public string title;
    public string summary;
    public string content;
    public string source_npc_id;
    public bool discovered;
}

[Serializable]
public sealed class OfficeAvailableGameActionDto
{
    public string id;
    public string family;
    public string label;
    public string object_id;
    public string target_id;
    public string owner_id;
    public string scope;
    public string location;
    public bool enabled;
    public string disabled_reason;
}

[Serializable]
public sealed class OfficeSnapshotDto
{
    public string session_id;
    public int turn;
    public string current_location;
    public string incident_status;
    public OfficeNpcDto[] npcs;
    public OfficeWorldObjectDto[] world_objects;
    public OfficeEvidenceDto[] evidences;
    public OfficeAvailableGameActionDto[] available_game_actions;
    public OfficePlayerInventoryDto player_inventory;
}

[Serializable]
public sealed class OfficeActionResponseDto
{
    public OfficeSnapshotDto snapshot;
    public string classified_action;
    public string message;
    public string intent_provider;
    public float intent_confidence;
    public bool intent_fallback_used;
    public bool blocked;
    public string alert;
}

[Serializable]
public sealed class OfficeGameActionResponseDto
{
    public OfficeSnapshotDto snapshot;
    public string action_id;
    public string message;
    public bool blocked;
    public string alert;
}

[Serializable]
public sealed class OfficeDialogueRequestPayload
{
    public string text;
    public string target_hint;
}

[Serializable]
public sealed class OfficeIntentHintDto
{
    public string intent;
    public string location;
    public float confidence;
}

[Serializable]
public sealed class OfficeMoveRequestPayload
{
    public string text;
    public OfficeIntentHintDto intent_hint;
}

[Serializable]
public sealed class OfficeGameActionRequestPayload
{
    public string action_id;
}

public sealed class OfficeBackendClient : MonoBehaviour
{
    private const float HealthCheckIntervalSeconds = 30f;

    public static OfficeBackendClient Instance { get; private set; }

    [SerializeField] private string baseUrl = "http://127.0.0.1:8000";
    [SerializeField] private string localBaseUrl = "http://127.0.0.1:8000";
    [SerializeField] private string remoteBaseUrl = "https://api.heartsignal.cloud/office-agent-backend";
    [SerializeField] private bool autoStartSession = true;

    public string SessionId { get; private set; }
    public OfficeSnapshotDto CurrentSnapshot { get; private set; }
    public event Action<OfficeSnapshotDto> SnapshotUpdated;
    public event Action SessionReady;
    public event Action<string> LocationSyncStarted;
    public event Action<string> LocationSyncCompleted;
    public string PendingLocation { get; private set; }
    public bool IsLocationSyncing { get; private set; }
    public bool IsRequestInFlight => requestInFlight;
    public bool IsReady => !string.IsNullOrEmpty(SessionId);
    public bool IsConnectionSelectionOpen => !IsReady;
    public string LocalBaseUrl => localBaseUrl;
    public string RemoteBaseUrl => remoteBaseUrl;
    public OfficeBackendHealthStatus HealthStatus { get; private set; } = OfficeBackendHealthStatus.Disconnected;
    public long HealthLatencyMilliseconds { get; private set; }
    public event Action<OfficeBackendHealthStatus, long> HealthStatusChanged;
    public event Action<OfficeBackendEndpoint, OfficeBackendHealthStatus, long> EndpointHealthChanged;

    private bool requestInFlight;
    private int moveRequestVersion;
    private bool connectionSelectionStarted;
    private bool selectedEndpointHealthLoopStarted;
    private OfficeBackendEndpoint? selectedEndpoint;
    private OfficeBackendHealthStatus localEndpointHealth = OfficeBackendHealthStatus.Disconnected;
    private OfficeBackendHealthStatus remoteEndpointHealth = OfficeBackendHealthStatus.Disconnected;
    private long localEndpointLatencyMilliseconds;
    private long remoteEndpointLatencyMilliseconds;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (autoStartSession)
        {
            BeginConnectionSelection();
        }
    }

    public void ConfigureEndpoints(string localUrl, string remoteUrl)
    {
        if (!string.IsNullOrWhiteSpace(localUrl))
        {
            localBaseUrl = localUrl.Trim().TrimEnd('/');
        }

        if (!string.IsNullOrWhiteSpace(remoteUrl))
        {
            remoteBaseUrl = remoteUrl.Trim().TrimEnd('/');
        }
        if (selectedEndpoint == null)
        {
            baseUrl = localBaseUrl;
        }
    }

    public void BeginConnectionSelection()
    {
        if (connectionSelectionStarted || IsReady)
        {
            return;
        }

        connectionSelectionStarted = true;
        StartCoroutine(EndpointHealthLoop());
    }

    public OfficeBackendHealthStatus GetEndpointHealth(OfficeBackendEndpoint endpoint)
    {
        return endpoint == OfficeBackendEndpoint.Local ? localEndpointHealth : remoteEndpointHealth;
    }

    public long GetEndpointLatency(OfficeBackendEndpoint endpoint)
    {
        return endpoint == OfficeBackendEndpoint.Local
            ? localEndpointLatencyMilliseconds
            : remoteEndpointLatencyMilliseconds;
    }

    public bool SelectEndpoint(OfficeBackendEndpoint endpoint)
    {
        var url = endpoint == OfficeBackendEndpoint.Local ? localBaseUrl : remoteBaseUrl;
        if (string.IsNullOrWhiteSpace(url) || GetEndpointHealth(endpoint) != OfficeBackendHealthStatus.Connected || IsReady || requestInFlight)
        {
            return false;
        }

        selectedEndpoint = endpoint;
        baseUrl = url;
        connectionSelectionStarted = false;
        if (!selectedEndpointHealthLoopStarted)
        {
            selectedEndpointHealthLoopStarted = true;
            StartCoroutine(SelectedHealthCheckLoop());
        }

        if (autoStartSession)
        {
            StartCoroutine(CreateSessionRoutine());
        }

        return true;
    }

    private IEnumerator EndpointHealthLoop()
    {
        while (connectionSelectionStarted && !IsReady)
        {
            yield return CheckEndpointHealthRoutine(OfficeBackendEndpoint.Local, localBaseUrl);
            yield return CheckEndpointHealthRoutine(OfficeBackendEndpoint.Remote, remoteBaseUrl);
            yield return new WaitForSecondsRealtime(HealthCheckIntervalSeconds);
        }
    }

    private IEnumerator SelectedHealthCheckLoop()
    {
        while (selectedEndpoint != null && !string.IsNullOrEmpty(baseUrl))
        {
            yield return CheckHealthRoutine();
            yield return new WaitForSecondsRealtime(HealthCheckIntervalSeconds);
        }
    }

    private IEnumerator CheckEndpointHealthRoutine(OfficeBackendEndpoint endpoint, string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            SetEndpointHealth(endpoint, OfficeBackendHealthStatus.Disconnected, 0L);
            yield break;
        }

        SetEndpointHealth(endpoint, OfficeBackendHealthStatus.Checking, 0L);
        var startedAt = Time.realtimeSinceStartup;
        using (var request = UnityWebRequest.Get($"{url}/health"))
        {
            request.timeout = 3;
            yield return request.SendWebRequest();

            var latency = (long)Mathf.Max(0f, (Time.realtimeSinceStartup - startedAt) * 1000f);
            var healthy = request.result == UnityWebRequest.Result.Success
                && request.responseCode >= 200
                && request.responseCode < 300;
            SetEndpointHealth(
                endpoint,
                healthy ? OfficeBackendHealthStatus.Connected : OfficeBackendHealthStatus.Disconnected,
                latency
            );
        }
    }

    private IEnumerator CheckHealthRoutine()
    {
        SetHealthStatus(OfficeBackendHealthStatus.Checking, 0L);
        var startedAt = Time.realtimeSinceStartup;

        using (var request = UnityWebRequest.Get($"{baseUrl}/health"))
        {
            request.timeout = 3;
            yield return request.SendWebRequest();

            var latency = (long)Mathf.Max(0f, (Time.realtimeSinceStartup - startedAt) * 1000f);
            var healthy = request.result == UnityWebRequest.Result.Success
                && request.responseCode >= 200
                && request.responseCode < 300;

            SetHealthStatus(
                healthy ? OfficeBackendHealthStatus.Connected : OfficeBackendHealthStatus.Disconnected,
                latency
            );
        }
    }

    private void SetHealthStatus(OfficeBackendHealthStatus status, long latency)
    {
        HealthStatus = status;
        HealthLatencyMilliseconds = latency;
        HealthStatusChanged?.Invoke(status, latency);
    }

    private void SetEndpointHealth(OfficeBackendEndpoint endpoint, OfficeBackendHealthStatus status, long latency)
    {
        if (endpoint == OfficeBackendEndpoint.Local)
        {
            localEndpointHealth = status;
            localEndpointLatencyMilliseconds = latency;
        }
        else
        {
            remoteEndpointHealth = status;
            remoteEndpointLatencyMilliseconds = latency;
        }

        EndpointHealthChanged?.Invoke(endpoint, status, latency);
        if (selectedEndpoint == endpoint)
        {
            SetHealthStatus(status, latency);
        }
    }

    public void NotifyInteraction(InteractablePoint point)
    {
        Debug.Log($"[OfficeMVP] Backend action hook ready for {point.TargetId}.");
    }

    public void SubmitDialogue(string text, string targetId, Action<OfficeActionResponseDto> onSuccess, Action<string> onFailure)
    {
        if (requestInFlight)
        {
            onFailure?.Invoke("Backend 요청이 처리 중입니다.");
            return;
        }

        StartCoroutine(SubmitDialogueRoutine(text, targetId, onSuccess, onFailure));
    }

    public void SubmitMove(string location, Action<OfficeActionResponseDto> onSuccess, Action<string> onFailure)
    {
        if (requestInFlight)
        {
            onFailure?.Invoke("Backend 요청이 처리 중입니다.");
            return;
        }

        var requestVersion = ++moveRequestVersion;
        PendingLocation = location;
        IsLocationSyncing = true;
        LocationSyncStarted?.Invoke(location);
        StartCoroutine(SubmitMoveRoutine(location, requestVersion, onSuccess, onFailure));
    }

    public void SubmitGameAction(string actionId, Action<OfficeGameActionResponseDto> onSuccess, Action<string> onFailure)
    {
        if (requestInFlight)
        {
            onFailure?.Invoke("Backend 요청이 처리 중입니다.");
            return;
        }

        StartCoroutine(SubmitGameActionRoutine(actionId, onSuccess, onFailure));
    }

    public void RefreshSnapshot()
    {
        if (!requestInFlight && IsReady)
        {
            StartCoroutine(GetSnapshotRoutine());
        }
    }

    private IEnumerator CreateSessionRoutine()
    {
        requestInFlight = true;

        using (var request = CreatePostRequest("/api/v1/sessions", "{}"))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                requestInFlight = false;
                Debug.LogError($"[OfficeMVP] Backend session create failed: {request.responseCode} {request.error}");
                yield break;
            }

            var snapshot = ParseSnapshot(request.downloadHandler.text);
            if (snapshot == null || string.IsNullOrEmpty(snapshot.session_id))
            {
                requestInFlight = false;
                Debug.LogError("[OfficeMVP] Backend session create returned an invalid snapshot.");
                yield break;
            }

            ApplySnapshot(snapshot);
            SessionId = snapshot.session_id;
            SessionReady?.Invoke();
            Debug.Log($"[OfficeMVP] Backend session created: {SessionId}");
        }

        requestInFlight = false;
        yield return GetSnapshotRoutine();
    }

    private IEnumerator GetSnapshotRoutine()
    {
        if (!IsReady)
        {
            yield break;
        }

        requestInFlight = true;

        using (var request = UnityWebRequest.Get($"{baseUrl}/api/v1/sessions/{SessionId}"))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 30;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                requestInFlight = false;
                Debug.LogError($"[OfficeMVP] Backend snapshot failed: {request.responseCode} {request.error}");
                yield break;
            }

            var snapshot = ParseSnapshot(request.downloadHandler.text);
            if (snapshot == null)
            {
                requestInFlight = false;
                Debug.LogError("[OfficeMVP] Backend snapshot returned invalid JSON.");
                yield break;
            }

            ApplySnapshot(snapshot);
            var npcCount = snapshot.npcs == null ? 0 : snapshot.npcs.Length;
            var objectCount = snapshot.world_objects == null ? 0 : snapshot.world_objects.Length;
            var actionCount = snapshot.available_game_actions == null ? 0 : snapshot.available_game_actions.Length;
            var heldCount = snapshot.player_inventory == null || snapshot.player_inventory.held_object_ids == null ? 0 : snapshot.player_inventory.held_object_ids.Length;
            Debug.Log($"[OfficeMVP] Backend snapshot received: turn={snapshot.turn}, location={snapshot.current_location}, npcs={npcCount}, objects={objectCount}, held={heldCount}, actions={actionCount}");
        }

        requestInFlight = false;
    }

    private IEnumerator SubmitMoveRoutine(string location, int requestVersion, Action<OfficeActionResponseDto> onSuccess, Action<string> onFailure)
    {
        requestInFlight = true;
        var payload = new OfficeMoveRequestPayload
        {
            text = $"\\uC774\\uB3D9: {location}",
            intent_hint = new OfficeIntentHintDto
            {
                intent = "move",
                location = location,
                confidence = 1f
            }
        };

        using (var request = CreatePostRequest($"/api/v1/sessions/{SessionId}/actions", JsonUtility.ToJson(payload)))
        {
            request.timeout = 60;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                requestInFlight = false;
                if (requestVersion == moveRequestVersion)
                {
                    IsLocationSyncing = false;
                    PendingLocation = null;
                    LocationSyncCompleted?.Invoke(location);
                }

                var error = $"Backend 이동 요청 실패: {request.responseCode} {request.error}";
                Debug.LogError($"[OfficeMVP] {error}");
                onFailure?.Invoke(error);
                yield break;
            }

            var response = ParseActionResponse(request.downloadHandler.text);
            if (response == null)
            {
                requestInFlight = false;
                onFailure?.Invoke("Backend 이동 응답을 해석하지 못했습니다.");
                yield break;
            }

            if (response.snapshot != null && requestVersion == moveRequestVersion)
            {
                ApplySnapshot(response.snapshot);
            }

            requestInFlight = false;
            if (requestVersion == moveRequestVersion)
            {
                IsLocationSyncing = false;
                PendingLocation = null;
                LocationSyncCompleted?.Invoke(location);
            }

            onSuccess?.Invoke(response);
        }
    }

    private IEnumerator SubmitDialogueRoutine(string text, string targetId, Action<OfficeActionResponseDto> onSuccess, Action<string> onFailure)
    {
        requestInFlight = true;
        var payload = new OfficeDialogueRequestPayload
        {
            text = text,
            target_hint = targetId
        };

        using (var request = CreatePostRequest($"/api/v1/sessions/{SessionId}/actions", JsonUtility.ToJson(payload)))
        {
            request.timeout = 120;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                requestInFlight = false;
                var error = $"Backend 대화 요청 실패: {request.responseCode} {request.error}";
                Debug.LogError($"[OfficeMVP] {error}");
                onFailure?.Invoke(error);
                yield break;
            }

            var response = ParseActionResponse(request.downloadHandler.text);
            if (response == null)
            {
                requestInFlight = false;
                onFailure?.Invoke("Backend 대화 응답을 해석하지 못했습니다.");
                yield break;
            }

            if (response.snapshot != null) ApplySnapshot(response.snapshot);
            onSuccess?.Invoke(response);
        }

        requestInFlight = false;
    }

    private IEnumerator SubmitGameActionRoutine(string actionId, Action<OfficeGameActionResponseDto> onSuccess, Action<string> onFailure)
    {
        requestInFlight = true;
        var payload = new OfficeGameActionRequestPayload
        {
            action_id = actionId
        };

        using (var request = CreatePostRequest($"/api/v1/sessions/{SessionId}/game-actions", JsonUtility.ToJson(payload)))
        {
            request.timeout = 60;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                requestInFlight = false;
                var error = $"Backend 액션 요청 실패: {request.responseCode} {request.error}";
                Debug.LogError($"[OfficeMVP] {error}");
                onFailure?.Invoke(error);
                yield break;
            }

            var response = ParseGameActionResponse(request.downloadHandler.text);
            if (response == null)
            {
                requestInFlight = false;
                onFailure?.Invoke("Backend 액션 응답을 해석하지 못했습니다.");
                yield break;
            }

            if (response.snapshot != null) ApplySnapshot(response.snapshot);
            onSuccess?.Invoke(response);
        }

        requestInFlight = false;
    }

    private UnityWebRequest CreatePostRequest(string path, string json)
    {
        var request = new UnityWebRequest($"{baseUrl}{path}", UnityWebRequest.kHttpVerbPOST)
        {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = 30
        };
        request.SetRequestHeader("Content-Type", "application/json");
        return request;
    }

    private void ApplySnapshot(OfficeSnapshotDto snapshot)
    {
        CurrentSnapshot = snapshot;
        if (!string.IsNullOrEmpty(snapshot.session_id))
        {
            SessionId = snapshot.session_id;
        }

        SnapshotUpdated?.Invoke(snapshot);
    }

    private static OfficeSnapshotDto ParseSnapshot(string json)
    {
        try
        {
            return JsonUtility.FromJson<OfficeSnapshotDto>(json);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[OfficeMVP] Backend JSON parse failed: {exception.Message}");
            return null;
        }
    }

    private static OfficeActionResponseDto ParseActionResponse(string json)
    {
        try
        {
            return JsonUtility.FromJson<OfficeActionResponseDto>(json);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[OfficeMVP] Backend dialogue response parse failed: {exception.Message}");
            return null;
        }
    }

    private static OfficeGameActionResponseDto ParseGameActionResponse(string json)
    {
        try
        {
            return JsonUtility.FromJson<OfficeGameActionResponseDto>(json);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[OfficeMVP] Backend game action response parse failed: {exception.Message}");
            return null;
        }
    }
}
// Unity MCP location sync compile marker
