using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class OfficeInteractionUI : MonoBehaviour
{
    private Canvas canvas;
    private PlayerInteractionDetector detector;
    private OfficeBackendClient backend;
    private InteractablePoint currentTarget;

    private GameObject interactionMenu;
    private GameObject dialoguePanel;
    private GameObject actionPanel;

    private Text interactionTitle;
    private Button talkButton;
    private Button actionButton;
    private Text dialogueTitle;
    private Text dialogueText;
    private Text dialogueStatus;
    private Text actionTitle;
    private Text actionStatus;
    private GameObject evidenceActionPanel;
    private Transform evidenceActionContent;

    private InputField dialogueInput;
    private Button dialogueSendButton;
    private ScrollRect dialogueScroll;
    private RectTransform dialogueContentRect;
    private Coroutine dialogueScrollRoutine;
    private Transform actionContent;

    private readonly Dictionary<string, string> dialogueHistories = new Dictionary<string, string>();
    private readonly Dictionary<string, string> dialogueDisplayNames = new Dictionary<string, string>();
    private readonly Dictionary<string, Button> dialogueTabButtons = new Dictionary<string, Button>();
    private readonly HashSet<string> dialogueStartedTargets = new HashSet<string>();
    private readonly HashSet<string> observedEvidenceIds = new HashSet<string>();

    private string activeDialogueTargetId;
    private string activeDialogueTargetName;
    private string viewedDialogueTargetId;
    private bool dialogueRequestInFlight;
    private bool evidenceSnapshotInitialized;
    private Coroutine dialogueWaitingAnimation;

    private void Start()
    {
        detector = FindAnyObjectByType<PlayerInteractionDetector>();
        backend = OfficeBackendClient.Instance;

        EnsureEventSystem();
        BuildUi();
        BuildDialogueTabs();

        interactionMenu.SetActive(false);
        dialoguePanel.SetActive(false);
        actionPanel.SetActive(false);
        evidenceActionPanel.SetActive(false);

        if (backend != null)
        {
            backend.SnapshotUpdated += OnBackendSnapshotUpdated;
            backend.LocationSyncStarted += OnLocationSyncStarted;
            backend.LocationSyncCompleted += OnLocationSyncCompleted;
        }
    }

    private void OnDestroy()
    {
        if (backend != null)
        {
            backend.SnapshotUpdated -= OnBackendSnapshotUpdated;
            backend.LocationSyncStarted -= OnLocationSyncStarted;
            backend.LocationSyncCompleted -= OnLocationSyncCompleted;
        }
    }

    private void Update()
    {
        if (dialoguePanel != null && dialoguePanel.activeSelf && IsDialogueInputFocused())
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                SendDialogue();
            }
        }

        if (detector == null)
        {
            detector = FindAnyObjectByType<PlayerInteractionDetector>();
        }

        var nextTarget = detector == null ? null : detector.Current;
        if (nextTarget == currentTarget)
        {
            return;
        }

        currentTarget = nextTarget;
        UpdateTargetMenu();

        if (dialoguePanel != null && dialoguePanel.activeSelf)
        {
            UpdateDialogueInputAvailability(true);
        }

        if (currentTarget == null)
        {
            CloseDialogue();
            actionPanel.SetActive(false);
        }
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        var eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    private void BuildUi()
    {
        var canvasObject = new GameObject("OfficeInteractionCanvas");
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.AddComponent<Canvas>();
        var mainCamera = Camera.main;
        canvas.renderMode = mainCamera == null ? RenderMode.ScreenSpaceOverlay : RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = mainCamera;
        canvas.planeDistance = 1f;
        canvas.sortingOrder = 500;

        var canvasScaler = canvasObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1280f, 720f);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        interactionMenu = CreatePanel(
            "InteractionMenu",
            canvas.transform,
            new Vector2(460f, 92f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -120f),
            new Color(0.04f, 0.05f, 0.08f, 0.94f)
        );
        interactionTitle = CreateText(interactionMenu.transform, "InteractionTitle", "대상 선택", 22, Color.white, TextAnchor.MiddleCenter, new Vector2(430f, 28f), new Vector2(0f, 25f));

        talkButton = CreateButton(interactionMenu.transform, "TalkButton", "대화하기", new Vector2(175f, 38f), new Vector2(-105f, -20f));
        talkButton.onClick.AddListener(OpenDialogue);

        actionButton = CreateButton(interactionMenu.transform, "ActionButton", "액션취하기", new Vector2(175f, 38f), new Vector2(105f, -20f));
        actionButton.onClick.AddListener(OpenActions);

        dialoguePanel = CreatePanel(
            "DialoguePanel",
            canvas.transform,
            new Vector2(820f, 430f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -245f),
            new Color(0.035f, 0.045f, 0.07f, 0.94f)
        );
        dialogueTitle = CreateText(dialoguePanel.transform, "DialogueTitle", "대화하기", 24, Color.white, TextAnchor.MiddleLeft, new Vector2(650f, 34f), new Vector2(-40f, 175f));
        var dialogueClose = CreateButton(dialoguePanel.transform, "DialogueClose", "닫기", new Vector2(80f, 34f), new Vector2(350f, 175f));
        dialogueClose.onClick.AddListener(CloseDialogue);

        dialogueScroll = CreateScrollView(dialoguePanel.transform, "DialogueScroll", new Vector2(0f, -10f), new Vector2(760f, 185f), out dialogueText);
        dialogueText.supportRichText = true;
        dialogueStatus = CreateText(dialoguePanel.transform, "DialogueStatus", "", 16, new Color(0.72f, 0.80f, 0.90f), TextAnchor.MiddleLeft, new Vector2(760f, 24f), new Vector2(-20f, -105f));

        evidenceActionPanel = CreatePanel(
            "EvidenceActionPanel",
            dialoguePanel.transform,
            new Vector2(760f, 34f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -137f),
            new Color(0.035f, 0.045f, 0.07f, 0.35f)
        );
        evidenceActionContent = evidenceActionPanel.transform;

        dialogueInput = CreateInputField(dialoguePanel.transform, "DialogueInput", "대화 내용을 입력하세요", new Vector2(575f, 42f), new Vector2(-85f, -170f));
        dialogueSendButton = CreateButton(dialoguePanel.transform, "DialogueSend", "전송", new Vector2(105f, 42f), new Vector2(265f, -170f));
        dialogueSendButton.onClick.AddListener(SendDialogue);

        actionPanel = CreatePanel(
            "ActionPanel",
            canvas.transform,
            new Vector2(820f, 430f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -245f),
            new Color(0.035f, 0.045f, 0.07f, 0.94f)
        );
        actionTitle = CreateText(actionPanel.transform, "ActionTitle", "액션취하기", 24, Color.white, TextAnchor.MiddleLeft, new Vector2(650f, 34f), new Vector2(-40f, 180f));
        var actionClose = CreateButton(actionPanel.transform, "ActionClose", "닫기", new Vector2(80f, 34f), new Vector2(350f, 180f));
        actionClose.onClick.AddListener(() => actionPanel.SetActive(false));

        var actionScroll = CreateActionScrollView(actionPanel.transform, "ActionScroll", new Vector2(0f, 20f), new Vector2(760f, 300f), out actionContent);
        actionStatus = CreateText(actionPanel.transform, "ActionStatus", "", 16, new Color(0.72f, 0.80f, 0.90f), TextAnchor.MiddleLeft, new Vector2(760f, 50f), new Vector2(-20f, -160f));
        actionStatus.supportRichText = true;
    }

    private void UpdateTargetMenu()
    {
        var hasTarget = currentTarget != null;
        interactionMenu.SetActive(hasTarget);

        if (!hasTarget)
        {
            return;
        }

        interactionTitle.text = $"{currentTarget.DisplayName}와 상호작용";
        talkButton.GetComponentInChildren<Text>().text = $"{currentTarget.DisplayName}와 대화하기";
        actionButton.GetComponentInChildren<Text>().text = $"{currentTarget.DisplayName}에게 액션취하기";
    }

    private void OpenDialogue()
    {
        if (currentTarget == null)
        {
            return;
        }

        activeDialogueTargetId = currentTarget.TargetId;
        activeDialogueTargetName = currentTarget.DisplayName;
        EnsureDialogueHistory(activeDialogueTargetId, activeDialogueTargetName);

        if (!dialogueStartedTargets.Contains(activeDialogueTargetId))
        {
            dialogueHistories[activeDialogueTargetId] =
                $"-- {activeDialogueTargetName} --\n입력한 내용은 서버로 전송됩니다.\n\n";
            dialogueStartedTargets.Add(activeDialogueTargetId);
        }

        dialogueInput.text = "";
        interactionMenu.SetActive(false);
        dialoguePanel.SetActive(true);
        actionPanel.SetActive(false);
        SelectDialogueTab(activeDialogueTargetId);
        RebuildEvidenceActions(backend == null ? null : backend.CurrentSnapshot);
    }

    private void BuildDialogueTabs()
    {
        var targetIds = new[] { "backend_01", "frontend_01", "qa_01", "pm_01" };
        var shortLabels = new[] { "Backend", "Frontend", "QA", "PM" };
        var fallbackNames = new[] { "Backend Developer", "Frontend Developer", "QA Engineer", "PM / Planner" };
        var positions = new[] { -270f, -90f, 90f, 270f };

        var points = FindObjectsByType<InteractablePoint>(FindObjectsInactive.Include);
        for (var index = 0; index < targetIds.Length; index++)
        {
            var targetId = targetIds[index];
            var displayName = fallbackNames[index];
            foreach (var point in points)
            {
                if (point != null && point.TargetId == targetId)
                {
                    displayName = point.DisplayName;
                    break;
                }
            }

            dialogueDisplayNames[targetId] = displayName;
            EnsureDialogueHistory(targetId, displayName);

            var button = CreateButton(
                dialoguePanel.transform,
                $"DialogueTab_{targetId}",
                shortLabels[index],
                new Vector2(165f, 34f),
                new Vector2(positions[index], 128f)
            );

            var capturedTargetId = targetId;
            button.onClick.AddListener(() => SelectDialogueTab(capturedTargetId));
            dialogueTabButtons[targetId] = button;
        }

        UpdateDialogueTabVisuals();
    }

    private void EnsureDialogueHistory(string targetId, string displayName)
    {
        if (string.IsNullOrEmpty(targetId))
        {
            return;
        }

        if (!dialogueDisplayNames.ContainsKey(targetId))
        {
            dialogueDisplayNames[targetId] = displayName;
        }

        if (!dialogueHistories.ContainsKey(targetId))
        {
            dialogueHistories[targetId] = $"-- {displayName} --\n아직 대화 기록이 없습니다.\n\n";
        }
    }

    private void SelectDialogueTab(string targetId)
    {
        if (string.IsNullOrEmpty(targetId))
        {
            return;
        }

        viewedDialogueTargetId = targetId;
        RenderViewedDialogue();
    }

    private void RenderViewedDialogue()
    {
        if (string.IsNullOrEmpty(viewedDialogueTargetId))
        {
            return;
        }

        var viewedName = GetDialogueDisplayName(viewedDialogueTargetId);
        EnsureDialogueHistory(viewedDialogueTargetId, viewedName);
        dialogueText.text = dialogueHistories[viewedDialogueTargetId];

        var isActiveTab = viewedDialogueTargetId == activeDialogueTargetId;
        dialogueTitle.text = isActiveTab
            ? $"{viewedName}와 대화하기"
            : $"{viewedName} 대화 기록";

        UpdateDialogueTabVisuals();
        UpdateDialogueInputAvailability(true);
        RebuildEvidenceActions(backend == null ? null : backend.CurrentSnapshot);
        ScrollDialogueToBottom();
    }

    private void UpdateDialogueTabVisuals()
    {
        foreach (var pair in dialogueTabButtons)
        {
            var colors = pair.Value.colors;
            if (pair.Key == viewedDialogueTargetId)
            {
                colors.normalColor = new Color(0.20f, 0.52f, 0.56f, 1f);
            }
            else if (pair.Key == activeDialogueTargetId)
            {
                colors.normalColor = new Color(0.42f, 0.30f, 0.10f, 0.98f);
            }
            else
            {
                colors.normalColor = new Color(0.12f, 0.30f, 0.38f, 0.98f);
            }

            pair.Value.colors = colors;
        }
    }

    private string GetDialogueDisplayName(string targetId)
    {
        return dialogueDisplayNames.TryGetValue(targetId, out var displayName)
            ? displayName
            : targetId;
    }

    private bool CanTalkToViewedTarget()
    {
        return dialoguePanel != null
            && dialoguePanel.activeSelf
            && !dialogueRequestInFlight
            && backend != null
            && backend.IsReady
            && currentTarget != null
            && currentTarget.TargetId == activeDialogueTargetId
            && viewedDialogueTargetId == activeDialogueTargetId;
    }

    private void UpdateDialogueInputAvailability(bool updateStatus)
    {
        var canTalk = CanTalkToViewedTarget();
        dialogueInput.interactable = canTalk;
        dialogueSendButton.interactable = canTalk;

        if (!updateStatus)
        {
            return;
        }

        if (dialogueRequestInFlight)
        {
            dialogueStatus.text = $"{activeDialogueTargetName} 응답 중...";
        }
        else if (viewedDialogueTargetId != activeDialogueTargetId)
        {
            dialogueStatus.text =
                $"{GetDialogueDisplayName(viewedDialogueTargetId)} 기록 보기 · 현재 대화 상대: {activeDialogueTargetName}";
        }
        else if (currentTarget == null || currentTarget.TargetId != activeDialogueTargetId)
        {
            dialogueStatus.text = "현재 대화 상대와 가까이 있지 않아 입력할 수 없습니다.";
        }
        else if (backend == null || !backend.IsReady)
        {
            dialogueStatus.text = "Backend 세션 준비 중...";
        }
        else
        {
            dialogueStatus.text = "대화 입력 가능";
        }

        if (canTalk)
        {
            RestoreDialogueInputFocus();
        }
        else if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == dialogueInput.gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void AppendDialogueHistory(string targetId, string line)
    {
        var displayName = GetDialogueDisplayName(targetId);
        EnsureDialogueHistory(targetId, displayName);
        dialogueHistories[targetId] += OfficeDisplayText.EscapeRichText(line);

        if (viewedDialogueTargetId == targetId)
        {
            dialogueText.text = dialogueHistories[targetId];
            ScrollDialogueToBottom();
        }
    }

    private void AppendRichDialogueHistory(string targetId, string line)
    {
        var displayName = GetDialogueDisplayName(targetId);
        EnsureDialogueHistory(targetId, displayName);
        dialogueHistories[targetId] += line;

        if (viewedDialogueTargetId == targetId)
        {
            dialogueText.text = dialogueHistories[targetId];
            ScrollDialogueToBottom();
        }
    }

    private bool IsDialogueInputFocused()
    {
        if (dialogueInput == null || EventSystem.current == null)
        {
            return false;
        }

        var selected = EventSystem.current.currentSelectedGameObject;
        return selected == dialogueInput.gameObject || selected != null && selected.GetComponentInParent<InputField>() == dialogueInput;
    }

    private void CloseDialogue()
    {
        StopDialogueWaitingAnimation();
        if (dialogueScrollRoutine != null)
        {
            StopCoroutine(dialogueScrollRoutine);
            dialogueScrollRoutine = null;
        }
        dialoguePanel.SetActive(false);
        if (evidenceActionPanel != null)
        {
            evidenceActionPanel.SetActive(false);
        }
        if (currentTarget != null)
        {
            interactionMenu.SetActive(true);
        }
        if (EventSystem.current != null && dialogueInput != null && EventSystem.current.currentSelectedGameObject == dialogueInput.gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void StartDialogueWaitingAnimation()
    {
        StopDialogueWaitingAnimation();
        dialogueWaitingAnimation = StartCoroutine(AnimateDialogueWaiting());
    }

    private void StopDialogueWaitingAnimation()
    {
        if (dialogueWaitingAnimation != null)
        {
            StopCoroutine(dialogueWaitingAnimation);
            dialogueWaitingAnimation = null;
        }
    }

    private IEnumerator AnimateDialogueWaiting()
    {
        var dotCount = 1;
        while (true)
        {
            dialogueStatus.text = "응답 중" + new string('.', dotCount);
            dotCount = dotCount >= 5 ? 1 : dotCount + 1;
            yield return new WaitForSeconds(0.35f);
        }
    }

    private void RestoreDialogueInputFocus()
    {
        if (dialoguePanel == null || !dialoguePanel.activeSelf || dialogueInput == null || !dialogueInput.interactable)
        {
            return;
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(dialogueInput.gameObject);
        }

        dialogueInput.Select();
        dialogueInput.ActivateInputField();
    }

    private void SendDialogue()
    {
        if (!CanTalkToViewedTarget())
        {
            dialogueStatus.text =
                $"{GetDialogueDisplayName(viewedDialogueTargetId)} 기록은 볼 수 있지만 현재 대화 상대가 아니므로 입력할 수 없습니다.";
            return;
        }

        var text = dialogueInput.text == null ? "" : dialogueInput.text.Trim();
        if (string.IsNullOrEmpty(text))
        {
            dialogueStatus.text = "대화 내용을 입력하세요.";
            return;
        }

        SubmitDialogueMessage(text, text);
    }

    private void PresentEvidence(OfficeEvidenceDto evidence)
    {
        if (evidence == null || !evidence.discovered || !CanTalkToViewedTarget())
        {
            return;
        }

        var displayText = $"{evidence.title} 증거를 제시했습니다.";
        var requestText = $"{evidence.title} 증거를 {activeDialogueTargetName}에게 제시해줘.";
        SubmitDialogueMessage(requestText, displayText);
    }

    private void SubmitDialogueMessage(string requestText, string displayText)
    {
        var targetId = activeDialogueTargetId;
        var targetName = activeDialogueTargetName;
        AppendDialogueHistory(targetId, $"Player: {displayText}\n");

        dialogueRequestInFlight = true;
        dialogueInput.text = "";
        if (evidenceActionPanel != null)
        {
            evidenceActionPanel.SetActive(false);
        }
        StartDialogueWaitingAnimation();
        UpdateDialogueInputAvailability(false);
        ScrollDialogueToBottom();

        backend.SubmitDialogue(
            requestText,
            targetId,
            response =>
            {
                StopDialogueWaitingAnimation();
                dialogueRequestInFlight = false;

                var isEvidenceRequest = string.Equals(response.classified_action, "request_evidence", StringComparison.OrdinalIgnoreCase);
                if (!isEvidenceRequest)
                {
                    var message = string.IsNullOrEmpty(response.message) ? "(응답 없음)" : response.message;
                    AppendDialogueHistory(targetId, $"{targetName}: {message}\n");
                }
                if (response.blocked && !string.IsNullOrEmpty(response.alert))
                {
                    AppendDialogueHistory(targetId, $"[차단됨] {response.alert}\n");
                }

                dialogueStatus.text = response.blocked
                    ? "Backend가 대화를 차단했습니다."
                    : isEvidenceRequest ? "증거 확보 완료"
                    : "응답 수신 완료";

                UpdateDialogueInputAvailability(false);
                RebuildEvidenceActions(response.snapshot ?? (backend == null ? null : backend.CurrentSnapshot));
                if (viewedDialogueTargetId == targetId)
                {
                    RestoreDialogueInputFocus();
                }
            },
            error =>
            {
                StopDialogueWaitingAnimation();
                dialogueRequestInFlight = false;
                AppendDialogueHistory(targetId, $"[오류] {error}\n");
                dialogueStatus.text = error;
                UpdateDialogueInputAvailability(false);
                RebuildEvidenceActions(backend == null ? null : backend.CurrentSnapshot);

                if (viewedDialogueTargetId == targetId)
                {
                    RestoreDialogueInputFocus();
                }
            }
        );
    }

    private void OpenActions()
    {
        if (currentTarget == null)
        {
            return;
        }

        actionTitle.text = $"{currentTarget.DisplayName}에게 액션취하기";
        actionStatus.text = backend != null && backend.IsReady ? "Backend가 현재 가능한 액션을 확인합니다." : "Backend 세션 준비 중...";
        interactionMenu.SetActive(false);
        actionPanel.SetActive(true);
        CloseDialogue();
        interactionMenu.SetActive(false);
        EnsureTargetLocationSync();
        RebuildActionList();
    }

    private void RebuildActionList()
    {
        for (var index = actionContent.childCount - 1; index >= 0; index--)
        {
            Destroy(actionContent.GetChild(index).gameObject);
        }

        if (!IsTargetLocationReady())
        {
            ShowActionLoading();
            return;
        }

        var snapshot = backend == null ? null : backend.CurrentSnapshot;
        var actions = snapshot == null ? null : snapshot.available_game_actions;
        var targetId = currentTarget == null ? "" : currentTarget.TargetId;
        var targetActions = new List<OfficeAvailableGameActionDto>();
        var heldItemActions = new List<OfficeAvailableGameActionDto>();

        if (actions != null)
        {
            foreach (var action in actions)
            {
                if (action == null)
                {
                    continue;
                }

                var scope = string.IsNullOrEmpty(action.scope) ? "world" : action.scope;
                if (string.Equals(scope, "held_item", StringComparison.OrdinalIgnoreCase))
                {
                    heldItemActions.Add(action);
                }
                else if (action.target_id == targetId)
                {
                    targetActions.Add(action);
                }
            }
        }

        if (targetActions.Count > 0)
        {
            CreateActionSectionHeader($"{currentTarget.DisplayName} 관련 액션");
            foreach (var action in targetActions)
            {
                CreateGameActionButton(action, snapshot);
            }
        }

        if (heldItemActions.Count > 0)
        {
            CreateActionSectionHeader("내가 들고 있는 물건");
            foreach (var action in heldItemActions)
            {
                CreateGameActionButton(action, snapshot);
            }
        }

        var actionCount = targetActions.Count + heldItemActions.Count;
        actionStatus.text = actionCount > 0
            ? $"현재 가능한 액션 {actionCount}개"
            : "현재 이 위치에서 이 대상에게 가능한 액션이 없습니다.";

        if (actionCount == 0)
        {
            var empty = CreateText(actionContent, "NoActions", "현재 이 위치에서 이 대상에게 가능한 액션이 없습니다.", 18, new Color(0.74f, 0.78f, 0.86f), TextAnchor.MiddleCenter, new Vector2(680f, 70f), Vector2.zero);
            empty.transform.localScale = Vector3.one;
        }
    }

    private void CreateActionSectionHeader(string label)
    {
        var header = CreateText(
            actionContent,
            "ActionSectionHeader",
            label,
            18,
            new Color(0.73f, 0.86f, 0.94f),
            TextAnchor.MiddleLeft,
            new Vector2(680f, 30f),
            Vector2.zero
        );

        var layout = header.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 30f;
        layout.preferredHeight = 30f;
    }

    private void CreateGameActionButton(OfficeAvailableGameActionDto action, OfficeSnapshotDto snapshot)
    {
        var buttonLabel = BuildActionLabel(action, snapshot);
        var button = CreateButton(actionContent, $"Action_{action.id}", buttonLabel, new Vector2(680f, 72f), Vector2.zero);
        button.interactable = action.enabled;
        var capturedAction = action;
        button.onClick.AddListener(() => SubmitGameAction(capturedAction));
    }

    private void OnBackendSnapshotUpdated(OfficeSnapshotDto snapshot)
    {
        ObserveEvidenceSnapshot(snapshot);
        RebuildEvidenceActions(snapshot);

        if (actionPanel != null && actionPanel.activeSelf && currentTarget != null)
        {
            RebuildActionList();
        }
    }

    private void ObserveEvidenceSnapshot(OfficeSnapshotDto snapshot)
    {
        if (snapshot == null || snapshot.evidences == null)
        {
            return;
        }

        var newlyDiscovered = new List<OfficeEvidenceDto>();
        foreach (var evidence in snapshot.evidences)
        {
            if (evidence == null || !evidence.discovered || string.IsNullOrEmpty(evidence.id))
            {
                continue;
            }

            if (evidenceSnapshotInitialized && observedEvidenceIds.Add(evidence.id))
            {
                newlyDiscovered.Add(evidence);
            }
            else
            {
                observedEvidenceIds.Add(evidence.id);
            }
        }

        evidenceSnapshotInitialized = true;
        foreach (var evidence in newlyDiscovered)
        {
            var targetId = string.IsNullOrEmpty(evidence.source_npc_id) ? activeDialogueTargetId : evidence.source_npc_id;
            var targetName = GetDialogueDisplayName(targetId);
            EnsureDialogueHistory(targetId, targetName);
            var evidenceTitle = OfficeDisplayText.EscapeRichText(evidence.title);
            var evidenceContent = OfficeDisplayText.EscapeRichText(evidence.content);
            AppendRichDialogueHistory(
                targetId,
                $"<color=#FF5D6C><b>증거를 확보했습니다.</b>\n{evidenceTitle}\n{evidenceContent}</color>\n\n"
            );
        }

        if (newlyDiscovered.Count > 0 && !string.IsNullOrEmpty(viewedDialogueTargetId))
        {
            RenderViewedDialogue();
        }
    }

    private void RebuildEvidenceActions(OfficeSnapshotDto snapshot)
    {
        if (evidenceActionPanel == null || evidenceActionContent == null)
        {
            return;
        }

        for (var index = evidenceActionContent.childCount - 1; index >= 0; index--)
        {
            Destroy(evidenceActionContent.GetChild(index).gameObject);
        }

        var canPresent = dialoguePanel != null
            && dialoguePanel.activeSelf
            && CanTalkToViewedTarget()
            && snapshot != null
            && snapshot.evidences != null;
        if (!canPresent)
        {
            evidenceActionPanel.SetActive(false);
            return;
        }

        var discovered = new List<OfficeEvidenceDto>();
        foreach (var evidence in snapshot.evidences)
        {
            if (evidence != null && evidence.discovered)
            {
                discovered.Add(evidence);
            }
        }

        if (discovered.Count == 0)
        {
            evidenceActionPanel.SetActive(false);
            return;
        }

        var buttonWidth = discovered.Count == 1 ? 300f : 235f;
        var spacing = 10f;
        for (var index = 0; index < discovered.Count; index++)
        {
            var evidence = discovered[index];
            var x = (index - (discovered.Count - 1) * 0.5f) * (buttonWidth + spacing);
            var button = CreateButton(
                evidenceActionContent,
                $"PresentEvidence_{evidence.id}",
                $"증거 제시하기 · {evidence.title}",
                new Vector2(buttonWidth, 30f),
                new Vector2(x, 0f)
            );
            var capturedEvidence = evidence;
            button.onClick.AddListener(() => PresentEvidence(capturedEvidence));
        }

        evidenceActionPanel.SetActive(true);
    }

    private void OnLocationSyncStarted(string location)
    {
        if (actionPanel != null && actionPanel.activeSelf && currentTarget != null && currentTarget.LocationId == location)
        {
            actionStatus.text = "현재 위치의 액션을 확인하는 중...";
            RebuildActionList();
        }
    }

    private void OnLocationSyncCompleted(string location)
    {
        if (actionPanel != null && actionPanel.activeSelf && currentTarget != null && currentTarget.LocationId == location)
        {
            RebuildActionList();
        }
    }

    private void EnsureTargetLocationSync()
    {
        if (backend == null || !backend.IsReady || currentTarget == null || string.IsNullOrEmpty(currentTarget.LocationId))
        {
            return;
        }

        var snapshot = backend.CurrentSnapshot;
        if (snapshot != null && snapshot.current_location == currentTarget.LocationId && !backend.IsLocationSyncing)
        {
            return;
        }

        if (backend.IsRequestInFlight || backend.IsLocationSyncing)
        {
            return;
        }

        actionStatus.text = "현재 위치의 액션을 확인하는 중...";
        backend.SubmitMove(
            currentTarget.LocationId,
            response => RebuildActionList(),
            error => actionStatus.text = error
        );
    }

    private bool IsTargetLocationReady()
    {
        if (backend == null || !backend.IsReady || currentTarget == null)
        {
            return false;
        }

        if (string.IsNullOrEmpty(currentTarget.LocationId))
        {
            return true;
        }

        var snapshot = backend.CurrentSnapshot;
        return snapshot != null
            && snapshot.current_location == currentTarget.LocationId
            && !backend.IsLocationSyncing;
    }

    private void ShowActionLoading()
    {
        actionStatus.text = "현재 위치의 액션을 확인하는 중...";
        var loading = CreateText(
            actionContent,
            "ActionLoading",
            "현재 위치의 액션을 확인하는 중...",
            18,
            new Color(0.74f, 0.82f, 0.92f),
            TextAnchor.MiddleCenter,
            new Vector2(680f, 70f),
            Vector2.zero
        );
        loading.transform.localScale = Vector3.one;
    }

    private string BuildActionLabel(OfficeAvailableGameActionDto action, OfficeSnapshotDto snapshot)
    {
        var state = FindWorldObject(snapshot, action.object_id);
        var label = state == null
            ? OfficeDisplayText.EscapeRichText(action.label)
            : OfficeDisplayText.FormatActionLabel(action.label, state.name);
        if (state != null)
        {
            label += $"\n물건 상태: {OfficeDisplayText.FormatItemNameRich(state.name)} · {ResolveObjectCondition(state.condition)}";
        }

        if (!action.enabled && !string.IsNullOrEmpty(action.disabled_reason))
        {
            label += $"\n{OfficeDisplayText.EscapeRichText(action.disabled_reason)}";
        }

        return label;
    }

    private OfficeWorldObjectDto FindWorldObject(OfficeSnapshotDto snapshot, string objectId)
    {
        if (snapshot == null || snapshot.world_objects == null || string.IsNullOrEmpty(objectId))
        {
            return null;
        }

        foreach (var state in snapshot.world_objects)
        {
            if (state != null && state.id == objectId)
            {
                return state;
            }
        }

        return null;
    }

    private string ResolveObjectCondition(string condition)
    {
        if (string.Equals(condition, "damaged", StringComparison.OrdinalIgnoreCase))
        {
            return "손상";
        }

        if (string.Equals(condition, "destroyed", StringComparison.OrdinalIgnoreCase))
        {
            return "파괴됨";
        }

        return "정상";
    }

    private void SubmitGameAction(OfficeAvailableGameActionDto action)
    {
        if (backend == null || !backend.IsReady)
        {
            actionStatus.text = "Backend 세션이 아직 준비되지 않았습니다.";
            return;
        }

        if (!action.enabled)
        {
            actionStatus.text = action.disabled_reason;
            return;
        }

        var throwCoordinator = OfficeThrowCoordinator.Instance;
        var isThrow = string.Equals(action.family, "throw_held_object", StringComparison.OrdinalIgnoreCase);
        var throwPrepared = isThrow && throwCoordinator != null && throwCoordinator.PrepareThrow(action);

        var actionObject = FindWorldObject(backend.CurrentSnapshot, action.object_id);
        actionStatus.text = actionObject == null
            ? $"{OfficeDisplayText.EscapeRichText(action.label)} 처리 중..."
            : $"{OfficeDisplayText.FormatActionLabel(action.label, actionObject.name)} 처리 중...";
        backend.SubmitGameAction(
            action.id,
            response =>
            {
                var responseObjects = response.snapshot == null
                    ? backend.CurrentSnapshot == null ? null : backend.CurrentSnapshot.world_objects
                    : response.snapshot.world_objects;
                var message = string.IsNullOrEmpty(response.message)
                    ? "(응답 없음)"
                    : OfficeDisplayText.FormatKnownItemNames(response.message, responseObjects);
                var alert = OfficeDisplayText.FormatKnownItemNames(response.alert, responseObjects);
                actionStatus.text = response.blocked
                    ? $"차단됨: {message} {alert}"
                    : $"결과: {message}";

                if (throwPrepared)
                {
                    if (response.blocked)
                    {
                        throwCoordinator.CancelThrow(action);
                    }
                    else
                    {
                        throwCoordinator.ConfirmThrow(action);
                    }
                }

                RebuildActionList();
            },
            error =>
            {
                if (throwPrepared)
                {
                    throwCoordinator.CancelThrow(action);
                }

                actionStatus.text = error;
            }
        );
    }

    private GameObject CreatePanel(string name, Transform parent, Vector2 size, Vector2 anchor, Vector2 position, Color color)
    {
        var panel = new GameObject(name);
        panel.transform.SetParent(parent, false);

        var rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var image = panel.AddComponent<Image>();
        image.color = color;
        return panel;
    }

    private Text CreateText(Transform parent, string name, string value, int fontSize, Color color, TextAnchor alignment, Vector2 size, Vector2 position)
    {
        var textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);

        var rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateButton(Transform parent, string name, string label, Vector2 size, Vector2 position)
    {
        var buttonObject = new GameObject(name);
        buttonObject.transform.SetParent(parent, false);

        var rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.12f, 0.30f, 0.38f, 0.98f);

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        var layoutElement = buttonObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = size.y;
        layoutElement.preferredHeight = size.y;
        layoutElement.flexibleWidth = 1f;

        var colors = button.colors;
        colors.normalColor = new Color(0.12f, 0.30f, 0.38f, 0.98f);
        colors.highlightedColor = new Color(0.20f, 0.52f, 0.56f, 1f);
        colors.pressedColor = new Color(0.08f, 0.20f, 0.26f, 1f);
        colors.disabledColor = new Color(0.18f, 0.18f, 0.22f, 0.70f);
        button.colors = colors;

        var text = CreateText(buttonObject.transform, "Label", label, 17, Color.white, TextAnchor.MiddleCenter, size - new Vector2(10f, 6f), Vector2.zero);
        text.supportRichText = true;
        text.raycastTarget = false;
        return button;
    }

    private InputField CreateInputField(Transform parent, string name, string placeholderText, Vector2 size, Vector2 position)
    {
        var inputObject = CreatePanel(name, parent, size, new Vector2(0.5f, 0.5f), position, new Color(0.10f, 0.12f, 0.17f, 1f));
        var input = inputObject.AddComponent<InputField>();

        var text = CreateText(inputObject.transform, "Text", "", 18, Color.white, TextAnchor.MiddleLeft, size - new Vector2(22f, 8f), Vector2.zero);
        var placeholder = CreateText(inputObject.transform, "Placeholder", placeholderText, 17, new Color(0.54f, 0.60f, 0.70f), TextAnchor.MiddleLeft, size - new Vector2(22f, 8f), Vector2.zero);
        input.textComponent = text;
        input.placeholder = placeholder;
        input.lineType = InputField.LineType.SingleLine;
        return input;
    }

    private ScrollRect CreateScrollView(Transform parent, string name, Vector2 position, Vector2 size, out Text contentText)
    {
        var scrollObject = new GameObject(name);
        scrollObject.transform.SetParent(parent, false);
        var scrollRect = scrollObject.AddComponent<ScrollRect>();

        var scrollTransform = scrollObject.GetComponent<RectTransform>();
        scrollTransform.anchorMin = new Vector2(0.5f, 0.5f);
        scrollTransform.anchorMax = new Vector2(0.5f, 0.5f);
        scrollTransform.sizeDelta = size;
        scrollTransform.anchoredPosition = position;

        var viewport = CreatePanel("Viewport", scrollObject.transform, size, new Vector2(0.5f, 0.5f), Vector2.zero, new Color(0.02f, 0.03f, 0.05f, 0.70f));
        var viewportRect = viewport.GetComponent<RectTransform>();
        var mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var contentRect = content.AddComponent<RectTransform>();
        dialogueContentRect = contentRect;
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 0f);
        contentRect.anchoredPosition = Vector2.zero;

        contentText = CreateText(content.transform, "Text", "", 18, new Color(0.92f, 0.94f, 0.98f), TextAnchor.UpperLeft, new Vector2(size.x - 30f, 0f), new Vector2(0f, -15f));
        var contentTextRect = contentText.rectTransform;
        contentTextRect.anchorMin = new Vector2(0f, 1f);
        contentTextRect.anchorMax = new Vector2(1f, 1f);
        contentTextRect.pivot = new Vector2(0.5f, 1f);
        contentTextRect.anchoredPosition = new Vector2(0f, -15f);
        contentTextRect.sizeDelta = new Vector2(-30f, 0f);
        contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
        contentText.verticalOverflow = VerticalWrapMode.Overflow;
        var fitter = contentText.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        return scrollRect;
    }

    private ScrollRect CreateActionScrollView(Transform parent, string name, Vector2 position, Vector2 size, out Transform content)
    {
        var scrollObject = new GameObject(name);
        scrollObject.transform.SetParent(parent, false);
        var scrollRect = scrollObject.AddComponent<ScrollRect>();

        var scrollTransform = scrollObject.GetComponent<RectTransform>();
        scrollTransform.anchorMin = new Vector2(0.5f, 0.5f);
        scrollTransform.anchorMax = new Vector2(0.5f, 0.5f);
        scrollTransform.sizeDelta = size;
        scrollTransform.anchoredPosition = position;

        var viewport = CreatePanel("ActionViewport", scrollObject.transform, size, new Vector2(0.5f, 0.5f), Vector2.zero, new Color(0.02f, 0.03f, 0.05f, 0.70f));
        var viewportRect = viewport.GetComponent<RectTransform>();
        var mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var contentObject = new GameObject("ActionContent");
        contentObject.transform.SetParent(viewport.transform, false);
        var contentRect = contentObject.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 0f);
        contentRect.anchoredPosition = Vector2.zero;

        var layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.padding = new RectOffset(20, 20, 15, 15);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        content = contentObject.transform;
        return scrollRect;
    }

    private void ScrollDialogueToBottom()
    {
        if (dialogueScrollRoutine != null)
        {
            StopCoroutine(dialogueScrollRoutine);
        }

        dialogueScrollRoutine = StartCoroutine(ScrollDialogueToBottomRoutine());
    }

    private IEnumerator ScrollDialogueToBottomRoutine()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        if (dialogueText != null && dialogueContentRect != null)
        {
            var textRect = dialogueText.rectTransform;
            var preferredHeight = Mathf.Max(dialogueScroll.viewport.rect.height, dialogueText.preferredHeight + 24f);
            textRect.sizeDelta = new Vector2(textRect.sizeDelta.x, preferredHeight);
            dialogueContentRect.sizeDelta = new Vector2(dialogueContentRect.sizeDelta.x, preferredHeight);
            LayoutRebuilder.ForceRebuildLayoutImmediate(dialogueContentRect);
        }

        yield return null;
        Canvas.ForceUpdateCanvases();
        if (dialogueScroll != null)
        {
            dialogueScroll.verticalNormalizedPosition = 0f;
        }

        dialogueScrollRoutine = null;
    }
}
