using UnityEngine;
using Core.Events;

// Lifecycle Order (Bootstrap):
// After SaveManager LoadCompleted and PlayerSpawned, CameraManager binds to player then GameStateManager enters Playing.
public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    // Outgoing events
    public struct CameraTargetChanged { public Transform OldTarget; public Transform NewTarget; }
    public struct CameraModeChanged { public string Mode; }

    // Incoming events
    public struct PlayerSpawned { public GameObject Player; }
    public struct LoadCompleted { public int SlotId; public bool Success; }
    public struct GameStateChanged { public GameStateManager.GameState Previous; public GameStateManager.GameState Current; }

    public struct CameraTargetSetRequested { public Transform Target; }

    public Transform CurrentTarget { get; private set; }

    [Header("Camera Settings")] [SerializeField]
    private float followLerp = 10f; // smoothing factor
    [SerializeField] private Vector3 offset = new Vector3(0, 5, -8);

    private string _currentMode = "Idle";
    private Camera _cam;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _cam = GetComponentInChildren<Camera>();
    }

    private void OnEnable()
    {
        EventRouter.Subscribe<PlayerSpawned>(OnPlayerSpawned);
        EventRouter.Subscribe<LoadCompleted>(OnLoadCompleted);
        EventRouter.Subscribe<GameStateChanged>(OnGameStateChanged);
        EventRouter.Subscribe<CameraTargetSetRequested>(OnCameraTargetSetRequested);
    }

    private void OnDisable()
    {
        EventRouter.Unsubscribe<PlayerSpawned>(OnPlayerSpawned);
        EventRouter.Unsubscribe<LoadCompleted>(OnLoadCompleted);
        EventRouter.Unsubscribe<GameStateChanged>(OnGameStateChanged);
        EventRouter.Unsubscribe<CameraTargetSetRequested>(OnCameraTargetSetRequested);
    }

    private void Update()
    {
        if (CurrentTarget != null && _cam != null && _currentMode == "Gameplay")
        {
            Vector3 desired = CurrentTarget.position + offset;
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, desired, followLerp * Time.deltaTime);
            _cam.transform.LookAt(CurrentTarget);
        }
    }

    private void OnPlayerSpawned(PlayerSpawned evt)
    {
        // Auto-bind first player spawn if no target yet
        if (evt.Player != null && CurrentTarget == null)
        {
            SetFollowTarget(evt.Player.transform);
        }
    }

    private void OnLoadCompleted(LoadCompleted evt)
    {
        // Potential place to restore camera mode/offset from save snapshot (noop for now)
    }

    private void OnGameStateChanged(GameStateChanged evt)
    {
        // Switch camera mode based on state
        string newMode = _currentMode;
        if (evt.Current == GameStateManager.GameState.Playing) newMode = "Gameplay";
        else if (evt.Current == GameStateManager.GameState.Paused) newMode = "Paused";
        else if (evt.Current == GameStateManager.GameState.Loading) newMode = "Loading";
        else if (evt.Current == GameStateManager.GameState.GameOver) newMode = "GameOver";
        else if (evt.Current == GameStateManager.GameState.Boot) newMode = "Boot";

        if (newMode != _currentMode)
        {
            var old = _currentMode;
            _currentMode = newMode;
            EventRouter.Publish(new CameraModeChanged { Mode = _currentMode });
        }
    }

    private void OnCameraTargetSetRequested(CameraTargetSetRequested evt)
    {
        if (evt.Target == null) return;
        if (evt.Target == CurrentTarget) return;
        var old = CurrentTarget;
        CurrentTarget = evt.Target;
        EventRouter.Publish(new CameraTargetChanged { OldTarget = old, NewTarget = CurrentTarget });
    }

    // Public API wrapper
    public void SetFollowTarget(Transform target)
    {
        EventRouter.Publish(new CameraTargetSetRequested { Target = target });
    }

    public string GetMode() => _currentMode;
}
