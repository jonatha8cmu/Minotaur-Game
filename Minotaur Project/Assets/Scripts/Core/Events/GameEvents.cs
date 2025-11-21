using UnityEngine;
using Core.Events;

// Facade for higher-level composite gameplay events (UI layer can subscribe here instead of raw system events).
// Keeps this optional and lightweight.
public class GameEvents : MonoBehaviour
{
    public static GameEvents Instance { get; private set; }

    // Example composite event payloads
    public struct GameplayReady { } // Fired when GameState enters Playing
    public struct PlayerContextChanged { public GameObject Player; public Transform CameraTarget; }

    private GameObject _player;
    private Transform _cameraTarget;
    private bool _gameplayReadyFired;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        EventRouter.Subscribe<GameStateManager.GameStateChanged>(OnGameStateChanged);
        EventRouter.Subscribe<PlayerManager.PlayerSpawned>(OnPlayerSpawned);
        EventRouter.Subscribe<CameraManager.CameraTargetChanged>(OnCameraTargetChanged);
    }

    private void OnDisable()
    {
        EventRouter.Unsubscribe<GameStateManager.GameStateChanged>(OnGameStateChanged);
        EventRouter.Unsubscribe<PlayerManager.PlayerSpawned>(OnPlayerSpawned);
        EventRouter.Unsubscribe<CameraManager.CameraTargetChanged>(OnCameraTargetChanged);
    }

    private void OnGameStateChanged(GameStateManager.GameStateChanged evt)
    {
        if (evt.Current == GameStateManager.GameState.Playing && !_gameplayReadyFired)
        {
            _gameplayReadyFired = true;
            EventRouter.Publish(new GameplayReady());
        }
        if (evt.Current != GameStateManager.GameState.Playing)
        {
            _gameplayReadyFired = false; // reset if leaving playing
        }
    }

    private void OnPlayerSpawned(PlayerManager.PlayerSpawned evt)
    {
        _player = evt.Player;
        PublishPlayerContext();
    }

    private void OnCameraTargetChanged(CameraManager.CameraTargetChanged evt)
    {
        _cameraTarget = evt.NewTarget;
        PublishPlayerContext();
    }

    private void PublishPlayerContext()
    {
        if (_player == null || _cameraTarget == null) return; // Wait until both available
        EventRouter.Publish(new PlayerContextChanged { Player = _player, CameraTarget = _cameraTarget });
    }
}
