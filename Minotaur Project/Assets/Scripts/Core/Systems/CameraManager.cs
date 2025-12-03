using UnityEngine;
using Core.Events;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    public Transform CurrentTarget { get; private set; }

    [Header("Camera Settings")] [SerializeField]
    private float followLerp = 10f;
    [SerializeField] private Vector3 offset = new Vector3(0, 5, -8);

    private CameraMode _currentMode = CameraMode.Idle;
    private Camera _cam;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _cam = GetComponentInChildren<Camera>();

        // Early subscribe to avoid missing Awake publications
        EventRouter.Subscribe<PlayerSpawned>(OnPlayerSpawned);
        EventRouter.Subscribe<LoadCompleted>(OnLoadCompleted);
        EventRouter.Subscribe<GameStateChanged>(OnGameStateChanged);
        EventRouter.Subscribe<CameraTargetSetRequested>(OnCameraTargetSetRequested);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        EventRouter.Unsubscribe<PlayerSpawned>(OnPlayerSpawned);
        EventRouter.Unsubscribe<LoadCompleted>(OnLoadCompleted);
        EventRouter.Unsubscribe<GameStateChanged>(OnGameStateChanged);
        EventRouter.Unsubscribe<CameraTargetSetRequested>(OnCameraTargetSetRequested);
    }

    private void Update()
    {
        if (CurrentTarget != null && _cam != null && _currentMode == CameraMode.Gameplay)
        {
            Vector3 desired = CurrentTarget.position + offset;
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, desired, followLerp * Time.deltaTime);
            _cam.transform.LookAt(CurrentTarget);
        }
    }

    private void OnPlayerSpawned(PlayerSpawned evt)
    {
        if (evt.Player != null && CurrentTarget == null)
        {
            SetFollowTarget(evt.Player.transform);
        }
    }

    private void OnLoadCompleted(LoadCompleted evt) { }

    private void OnGameStateChanged(GameStateChanged evt)
    {
        CameraMode newMode = _currentMode;
        if (evt.Current == GameState.Playing) newMode = CameraMode.Gameplay;
        else if (evt.Current == GameState.Paused) newMode = CameraMode.Paused;
        else if (evt.Current == GameState.Loading) newMode = CameraMode.Loading;
        else if (evt.Current == GameState.GameOver) newMode = CameraMode.GameOver;
        else if (evt.Current == GameState.Boot) newMode = CameraMode.Boot;

        if (newMode != _currentMode)
        {
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

    public void SetFollowTarget(Transform target)
    {
        EventRouter.Publish(new CameraTargetSetRequested { Target = target });
    }

    public CameraMode GetMode() => _currentMode;
}
