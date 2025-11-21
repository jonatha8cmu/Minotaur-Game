using UnityEngine;
using Core.Events;
using System.Collections;
using System.Collections.Generic;

// Lifecycle Order (Bootstrap):
// PlayerManager publishes PlayerSpawned -> SaveManager performs Load (if requested) -> publishes LoadCompleted -> GameStateManager advances.
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    // Outgoing event payloads
    public struct SaveStarted { public int SlotId; }
    public struct SaveCompleted { public int SlotId; public bool Success; }
    public struct LoadStarted { public int SlotId; }
    public struct LoadCompleted { public int SlotId; public bool Success; }

    // Incoming request payloads
    public struct SaveRequested { public int SlotId; }
    public struct LoadRequested { public int SlotId; }
    public struct PlayerSpawned { public GameObject Player; } // mirror for subscription to trigger auto-load

    // Simple in-memory representation of save slots (placeholder for real persistence)
    private readonly Dictionary<int, string> _slotData = new();
    private bool _isSaving;
    private bool _isLoading;
    private int _autoLoadSlot = -1; // Set externally if auto-load desired

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        EventRouter.Subscribe<SaveRequested>(OnSaveRequested);
        EventRouter.Subscribe<LoadRequested>(OnLoadRequested);
        EventRouter.Subscribe<PlayerSpawned>(OnPlayerSpawned);
    }

    private void OnDisable()
    {
        EventRouter.Unsubscribe<SaveRequested>(OnSaveRequested);
        EventRouter.Unsubscribe<LoadRequested>(OnLoadRequested);
        EventRouter.Unsubscribe<PlayerSpawned>(OnPlayerSpawned);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void OnPlayerSpawned(PlayerSpawned evt)
    {
        // Auto-load once after spawn if configured
        if (_autoLoadSlot >= 0)
        {
            RequestLoad(_autoLoadSlot);
            _autoLoadSlot = -1; // Prevent repeat
        }
    }

    private void OnSaveRequested(SaveRequested evt)
    {
        if (_isSaving || _isLoading) return; // Guard concurrent ops
        if (evt.SlotId < 0) return;
        StartCoroutine(SaveRoutine(evt.SlotId));
    }

    private void OnLoadRequested(LoadRequested evt)
    {
        if (_isSaving || _isLoading) return;
        if (evt.SlotId < 0) return;
        StartCoroutine(LoadRoutine(evt.SlotId));
    }

    private IEnumerator SaveRoutine(int slotId)
    {
        _isSaving = true;
        EventRouter.Publish(new SaveStarted { SlotId = slotId });

        // Simulate async delay (replace with real IO later)
        yield return null; // one frame

        // Collect snapshot (placeholder)
        string serialized = SerializeSnapshot();
        _slotData[slotId] = serialized;

        // Simulate commit delay
        yield return null;

        EventRouter.Publish(new SaveCompleted { SlotId = slotId, Success = true });
        _isSaving = false;
    }

    private IEnumerator LoadRoutine(int slotId)
    {
        _isLoading = true;
        EventRouter.Publish(new LoadStarted { SlotId = slotId });

        yield return null; // simulate IO latency

        bool success = _slotData.TryGetValue(slotId, out var serialized);
        if (success)
        {
            DeserializeSnapshot(serialized);
        }

        yield return null; // post-processing frame

        EventRouter.Publish(new LoadCompleted { SlotId = slotId, Success = success });
        _isLoading = false;
    }

    private string SerializeSnapshot()
    {
        // Placeholder: gather player/game state using events or direct queries
        // For now store timestamp
        return System.DateTime.UtcNow.ToString("o");
    }

    private void DeserializeSnapshot(string data)
    {
        // Placeholder: apply restored state (player stats, scene target etc.) via publishing specialized events
        // Currently no-op
    }

    // Public API wrappers
    public void RequestSave(int slotId)
    {
        EventRouter.Publish(new SaveRequested { SlotId = slotId });
    }

    public void RequestLoad(int slotId)
    {
        EventRouter.Publish(new LoadRequested { SlotId = slotId });
    }

    public bool IsBusy() => _isSaving || _isLoading;

    public void ConfigureAutoLoadSlot(int slotId)
    {
        _autoLoadSlot = slotId;
    }
}
