using UnityEngine;
using Core.Events;

// Lifecycle Order (Bootstrap):
// GameStateManager requests initial scene -> waits for SceneActivated + PlayerSpawned + (optional) LoadCompleted -> enters Playing.
public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    public enum GameState { None, Boot, Loading, Playing, Paused, GameOver }

    // Outgoing events
    public struct GameStateChanging { public GameState From; public GameState To; }
    public struct GameStateChanged { public GameState Previous; public GameState Current; }
    public struct PauseToggled { public bool IsPaused; }

    // Incoming events used for gating transitions
    public struct SceneActivated { public string SceneId; }
    public struct PlayerSpawned { public GameObject Player; }
    public struct LoadCompleted { public int SlotId; public bool Success; }

    // Requests
    public struct GameStateEnterRequested { public GameState Target; }
    public struct PauseToggleRequested { }

    public GameState Current { get; private set; } = GameState.None;

    private bool _sceneReady;
    private bool _playerReady;
    private bool _loadDoneOrSkipped;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        EventRouter.Subscribe<GameStateEnterRequested>(OnGameStateEnterRequested);
        EventRouter.Subscribe<PauseToggleRequested>(OnPauseToggleRequested);
        EventRouter.Subscribe<SceneActivated>(OnSceneActivated);
        EventRouter.Subscribe<PlayerSpawned>(OnPlayerSpawned);
        EventRouter.Subscribe<LoadCompleted>(OnLoadCompleted);
    }

    private void OnDisable()
    {
        EventRouter.Unsubscribe<GameStateEnterRequested>(OnGameStateEnterRequested);
        EventRouter.Unsubscribe<PauseToggleRequested>(OnPauseToggleRequested);
        EventRouter.Unsubscribe<SceneActivated>(OnSceneActivated);
        EventRouter.Unsubscribe<PlayerSpawned>(OnPlayerSpawned);
        EventRouter.Unsubscribe<LoadCompleted>(OnLoadCompleted);
    }

    private void OnGameStateEnterRequested(GameStateEnterRequested evt)
    {
        // Validate transitions; Boot -> Loading -> Playing typical path
        if (!IsTransitionAllowed(Current, evt.Target)) return;
        PublishChanging(evt.Target);
        Current = evt.Target;
        PublishChanged(evt.Target);

        if (evt.Target == GameState.Loading)
        {
            // Reset readiness flags
            _sceneReady = false;
            _playerReady = false;
            _loadDoneOrSkipped = false; // SaveManager will set or we will treat as skipped externally
        }

        // If entering Playing directly (e.g., skipping load) ensure gating satisfied or mark as satisfied
        if (evt.Target == GameState.Playing)
        {
            // If gating flags already satisfied nothing else needed; else remain in Playing anyway.
        }
    }

    private bool IsTransitionAllowed(GameState from, GameState to)
    {
        if (from == to) return false;
        switch (from)
        {
            case GameState.None:
                return to == GameState.Boot;
            case GameState.Boot:
                return to == GameState.Loading || to == GameState.GameOver; // allow immediate exit if needed
            case GameState.Loading:
                // Only allow Playing after gating conditions
                if (to == GameState.Playing)
                {
                    return _sceneReady && _playerReady && _loadDoneOrSkipped;
                }
                return false;
            case GameState.Playing:
                return to == GameState.Paused || to == GameState.GameOver;
            case GameState.Paused:
                return to == GameState.Playing || to == GameState.GameOver;
            case GameState.GameOver:
                return to == GameState.Loading || to == GameState.Boot; // restart flow
            default:
                return false;
        }
    }

    private void PublishChanging(GameState to)
    {
        EventRouter.Publish(new GameStateChanging { From = Current, To = to });
    }

    private void PublishChanged(GameState to)
    {
        EventRouter.Publish(new GameStateChanged { Previous = Current, Current = to });
    }

    private void OnPauseToggleRequested(PauseToggleRequested evt)
    {
        if (Current != GameState.Playing && Current != GameState.Paused) return;
        bool willPause = Current == GameState.Playing;
        var target = willPause ? GameState.Paused : GameState.Playing;
        PublishChanging(target);
        Current = target;
        PublishChanged(target);
        EventRouter.Publish(new PauseToggled { IsPaused = willPause });
        Time.timeScale = willPause ? 0f : 1f; // simple pause mechanic
    }

    private void OnSceneActivated(SceneActivated evt)
    {
        _sceneReady = true;
        TryAutoAdvanceFromLoading();
    }

    private void OnPlayerSpawned(PlayerSpawned evt)
    {
        _playerReady = true;
        TryAutoAdvanceFromLoading();
    }

    private void OnLoadCompleted(LoadCompleted evt)
    {
        _loadDoneOrSkipped = true; // Success or fail both allow proceed (could add fail handling)
        TryAutoAdvanceFromLoading();
    }

    private void TryAutoAdvanceFromLoading()
    {
        if (Current == GameState.Loading && _sceneReady && _playerReady && _loadDoneOrSkipped)
        {
            // Request enter Playing via event to keep consistency
            EventRouter.Publish(new GameStateEnterRequested { Target = GameState.Playing });
        }
    }

    // Public API wrappers
    public void RequestEnterState(GameState target)
    {
        EventRouter.Publish(new GameStateEnterRequested { Target = target });
    }

    public void RequestTogglePause()
    {
        EventRouter.Publish(new PauseToggleRequested());
    }

    public bool IsPaused() => Current == GameState.Paused;
}
