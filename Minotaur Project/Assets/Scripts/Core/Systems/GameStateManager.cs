using UnityEngine;
using Core.Events;

// Lifecycle Order (Bootstrap):
// GameStateManager requests initial scene -> waits for SceneActivated + PlayerSpawned + (optional) LoadCompleted -> enters Playing.
public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

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
        if (!IsTransitionAllowed(Current, evt.Target)) return;
        EventRouter.Publish(new GameStateChanging { From = Current, To = evt.Target });
        Current = evt.Target;
        EventRouter.Publish(new GameStateChanged { Previous = Current, Current = evt.Target });

        if (evt.Target == GameState.Loading)
        {
            _sceneReady = false;
            _playerReady = false;
            _loadDoneOrSkipped = false;
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
                return to == GameState.Loading || to == GameState.GameOver;
            case GameState.Loading:
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
                return to == GameState.Loading || to == GameState.Boot;
            default:
                return false;
        }
    }

    private void OnPauseToggleRequested(PauseToggleRequested evt)
    {
        if (Current != GameState.Playing && Current != GameState.Paused) return;
        bool willPause = Current == GameState.Playing;
        var target = willPause ? GameState.Paused : GameState.Playing;
        EventRouter.Publish(new GameStateChanging { From = Current, To = target });
        Current = target;
        EventRouter.Publish(new GameStateChanged { Previous = Current, Current = target });
        EventRouter.Publish(new PauseToggled { IsPaused = willPause });
        Time.timeScale = willPause ? 0f : 1f;
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
        _loadDoneOrSkipped = true;
        TryAutoAdvanceFromLoading();
    }

    private void TryAutoAdvanceFromLoading()
    {
        if (Current == GameState.Loading && _sceneReady && _playerReady && _loadDoneOrSkipped)
        {
            EventRouter.Publish(new GameStateEnterRequested { Target = GameState.Playing });
        }
    }

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
