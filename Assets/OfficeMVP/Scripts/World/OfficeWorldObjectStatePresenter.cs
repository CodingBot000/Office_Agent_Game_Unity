using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public sealed class OfficeWorldObjectStatePresenter : MonoBehaviour
{
    private OfficeBackendClient backend;
    private OfficeWorldObjectView[] views;
    private OfficeCharacterEmotionLabel[] emotionLabels;
    private OfficeNpcFallView[] npcFallViews;
    private OfficeNpcFearShake[] npcFearShakes;
    private Transform playerTransform;
    private readonly Dictionary<string, GameObject> carriedVisuals = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, string> previousConditions = new Dictionary<string, string>();

    private void Start()
    {
        views = FindObjectsByType<OfficeWorldObjectView>(FindObjectsInactive.Include);
        emotionLabels = FindObjectsByType<OfficeCharacterEmotionLabel>(FindObjectsInactive.Include);
        npcFallViews = FindObjectsByType<OfficeNpcFallView>(FindObjectsInactive.Include);
        npcFearShakes = FindObjectsByType<OfficeNpcFearShake>(FindObjectsInactive.Include);
        backend = OfficeBackendClient.Instance;

        var player = GameObject.Find("Player");
        playerTransform = player == null ? null : player.transform;

        if (backend == null)
        {
            Debug.LogWarning("[OfficeMVP] World object presenter could not find BackendClient.");
            return;
        }

        backend.SnapshotUpdated += ApplySnapshot;
        ApplySnapshot(backend.CurrentSnapshot);
    }

    private void OnDestroy()
    {
        if (backend != null)
        {
            backend.SnapshotUpdated -= ApplySnapshot;
        }

        DestroyCarriedVisual();
    }

    private void ApplySnapshot(OfficeSnapshotDto snapshot)
    {
        if (snapshot == null || snapshot.world_objects == null || views == null)
        {
            return;
        }

        var states = new Dictionary<string, OfficeWorldObjectDto>();
        foreach (var state in snapshot.world_objects)
        {
            if (state != null && !string.IsNullOrEmpty(state.id))
            {
                states[state.id] = state;
            }
        }

        PlayBreakEffects(states);

        foreach (var view in views)
        {
            if (view != null && states.TryGetValue(view.ObjectId, out var state))
            {
                view.Apply(state);
                UpdateWorldObjectPosition(view, state);
            }
        }

        UpdateCarriedVisual(states);
        UpdateEmotionLabels(snapshot);
        UpdateNpcFallViews(snapshot);

        previousConditions.Clear();
        foreach (var pair in states)
        {
            previousConditions[pair.Key] = pair.Value.condition;
        }
    }

    private void UpdateWorldObjectPosition(OfficeWorldObjectView view, OfficeWorldObjectDto state)
    {
        if (state == null || !string.IsNullOrEmpty(state.holder_id) || string.Equals(state.condition, "destroyed", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (state.is_dropped)
        {
            view.SetDroppedPosition(ResolveDropAnchor(state.location));
        }
        else if (state.location == view.InitialLocation)
        {
            view.RestoreInitialPosition();
        }
    }

    private Vector3 ResolveDropAnchor(string location)
    {
        switch (location)
        {
            case "dev_area":
                return new Vector3(-4.2f, -2.85f, 0f);
            case "qa_desk":
                return new Vector3(3.65f, 1.35f, 0f);
            case "pm_desk":
                return new Vector3(3.65f, -4.25f, 0f);
            default:
                return new Vector3(0f, -2.1f, 0f);
        }
    }

    private void PlayBreakEffects(Dictionary<string, OfficeWorldObjectDto> states)
    {
        foreach (var pair in states)
        {
            var state = pair.Value;
            if (!string.Equals(state.condition, "destroyed", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (previousConditions.TryGetValue(state.id, out var previousCondition)
                && string.Equals(previousCondition, "destroyed", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (OfficeThrowCoordinator.Instance != null && OfficeThrowCoordinator.Instance.IsThrowPendingForObject(state.id))
            {
                continue;
            }

            var view = FindView(state.id);
            if (view == null || view.Sprite == null)
            {
                continue;
            }

            var position = GetCurrentObjectPosition(state.id, view);
            var scale = GetCurrentObjectScale(state.id, view);
            OfficeBreakEffect.Play(view.Sprite, position, scale, 60);
        }
    }

    private void UpdateCarriedVisual(Dictionary<string, OfficeWorldObjectDto> states)
    {
        if (playerTransform == null)
        {
            DestroyCarriedVisual();
            return;
        }

        var activeObjectIds = new HashSet<string>();
        var carriedIndex = 0;
        foreach (var state in states.Values)
        {
            if (state == null
                || !string.Equals(state.holder_id, "player", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state.condition, "destroyed", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            activeObjectIds.Add(state.id);
            var sourceView = FindView(state.id);
            var sprite = sourceView == null ? OfficeItemSpriteCatalog.Load(state.id) : sourceView.Sprite;
            if (sprite == null)
            {
                continue;
            }

            if (!carriedVisuals.TryGetValue(state.id, out var carriedVisual) || carriedVisual == null)
            {
                carriedVisual = new GameObject($"HeldObject_{state.id}");
                carriedVisuals[state.id] = carriedVisual;
                carriedVisual.AddComponent<SpriteRenderer>();
            }

            var renderer = carriedVisual.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 60;
            carriedVisual.transform.SetParent(playerTransform, false);
            carriedVisual.transform.localPosition = new Vector3(
                0.22f + 0.16f * carriedIndex,
                0.34f + 0.08f * carriedIndex,
                -0.35f - 0.02f * carriedIndex
            );
            carriedVisual.transform.localRotation = Quaternion.identity;

            var heldScale = sourceView == null ? Vector3.one * 0.55f : sourceView.transform.localScale * 0.72f;
            if (OfficeItemSpriteCatalog.IsPersonItem(state.id))
            {
                heldScale *= 0.55f;
            }

            carriedVisual.transform.localScale = new Vector3(
                heldScale.x * 0.5f,
                heldScale.y,
                heldScale.z
            );

            carriedIndex++;
            Debug.Log($"[OfficeMVP] Carried object displayed at player hand: {state.id}");
        }

        var staleObjectIds = new List<string>();
        foreach (var pair in carriedVisuals)
        {
            if (!activeObjectIds.Contains(pair.Key))
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value);
                }
                staleObjectIds.Add(pair.Key);
            }
        }

        foreach (var objectId in staleObjectIds)
        {
            carriedVisuals.Remove(objectId);
        }
    }

    private void UpdateEmotionLabels(OfficeSnapshotDto snapshot)
    {
        if (snapshot == null || snapshot.npcs == null)
        {
            return;
        }

        var emotions = new Dictionary<string, string>();
        foreach (var npc in snapshot.npcs)
        {
            if (npc != null && npc.dynamic_state != null && !string.IsNullOrEmpty(npc.id))
            {
                emotions[npc.id] = npc.dynamic_state.emotion;
            }
        }

        foreach (var label in emotionLabels)
        {
            if (label != null && emotions.TryGetValue(label.TargetId, out var emotion))
            {
                label.SetPhysicalState("normal");
                label.SetEmotion(emotion);
            }
        }

        foreach (var shake in npcFearShakes)
        {
            if (shake != null && emotions.TryGetValue(shake.GetComponent<InteractablePoint>()?.TargetId, out var emotion))
            {
                shake.SetEmotion(emotion);
            }
        }
    }

    private void UpdateNpcFallViews(OfficeSnapshotDto snapshot)
    {
        if (snapshot == null || snapshot.npcs == null || npcFallViews == null)
        {
            return;
        }

        var fallenById = new Dictionary<string, bool>();
        foreach (var npc in snapshot.npcs)
        {
            if (npc != null && !string.IsNullOrEmpty(npc.id))
            {
                fallenById[npc.id] = npc.physical_state == "comatose" || npc.is_fallen;
            }
        }

        foreach (var fallView in npcFallViews)
        {
            if (fallView == null)
            {
                continue;
            }

            var point = fallView.GetComponent<InteractablePoint>();
            if (point == null || !fallenById.TryGetValue(point.TargetId, out var isFallen))
            {
                continue;
            }

            if (isFallen && OfficeThrowCoordinator.Instance != null && OfficeThrowCoordinator.Instance.IsThrowPendingFor(point.TargetId))
            {
                continue;
            }

            fallView.SetFallen(isFallen);
        }
    }

    private OfficeWorldObjectView FindView(string objectId)
    {
        if (views == null || string.IsNullOrEmpty(objectId))
        {
            return null;
        }

        foreach (var view in views)
        {
            if (view != null && view.ObjectId == objectId)
            {
                return view;
            }
        }

        return null;
    }

    private Vector3 GetCurrentObjectPosition(string objectId, OfficeWorldObjectView fallback)
    {
        if (carriedVisuals.TryGetValue(objectId, out var carriedVisual) && carriedVisual != null)
        {
            return carriedVisual.transform.position;
        }

        return fallback.transform.position;
    }

    private Vector3 GetCurrentObjectScale(string objectId, OfficeWorldObjectView fallback)
    {
        if (carriedVisuals.TryGetValue(objectId, out var carriedVisual) && carriedVisual != null)
        {
            return carriedVisual.transform.lossyScale;
        }

        return fallback.WorldScale;
    }

    private void DestroyCarriedVisual()
    {
        foreach (var carriedVisual in carriedVisuals.Values)
        {
            if (carriedVisual != null)
            {
                Destroy(carriedVisual);
            }
        }

        carriedVisuals.Clear();
    }
}
