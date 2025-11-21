using UnityEngine;
using Core.Events;

namespace Core.Bootstrap
{
    // Deterministic lifecycle ordering (single player):
    // Phase 0: Request initial scene (external trigger) -> SceneLoadRequested
    // Phase 1: SceneManager publishes SceneActivated -> request player spawn
    // Phase 2: PlayerManager publishes PlayerSpawned -> request load (optional) or skip
    // Phase 3: SaveManager publishes LoadCompleted (or skipped) -> ensure camera bound
    // Phase 4: CameraManager publishes CameraTargetChanged -> request enter Playing state
    public class BootstrapSequence : MonoBehaviour
    {
        public static BootstrapSequence Instance { get; private set; }

        private enum Phase { None, RequestedScene, SceneActivated, PlayerSpawned, LoadCompleted, CameraAttached, PlayingEntered }
        private Phase _phase = Phase.None;

        [SerializeField] private string initialSceneId = "Gameplay"; // Assign desired initial gameplay scene
        [SerializeField] private int autoLoadSlot = -1; // -1 means skip load

        private GameObject _player;
        private Transform _cameraTarget;

        // Imported payloads
        private struct SceneActivated { public string SceneId; }
        private struct PlayerSpawned { public GameObject Player; }
        private struct LoadCompleted { public int SlotId; public bool Success; }
        private struct CameraTargetChanged { public Transform OldTarget; public Transform NewTarget; }
        private struct GameStateChanged { public GameStateManager.GameState Previous; public GameStateManager.GameState Current; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            EventRouter.Subscribe<SceneActivated>(OnSceneActivated);
            EventRouter.Subscribe<PlayerSpawned>(OnPlayerSpawned);
            EventRouter.Subscribe<LoadCompleted>(OnLoadCompleted);
            EventRouter.Subscribe<CameraTargetChanged>(OnCameraTargetChanged);
            EventRouter.Subscribe<GameStateChanged>(OnGameStateChanged);

            KickOff();
        }

        private void OnDisable()
        {
            EventRouter.Unsubscribe<SceneActivated>(OnSceneActivated);
            EventRouter.Unsubscribe<PlayerSpawned>(OnPlayerSpawned);
            EventRouter.Unsubscribe<LoadCompleted>(OnLoadCompleted);
            EventRouter.Unsubscribe<CameraTargetChanged>(OnCameraTargetChanged);
            EventRouter.Unsubscribe<GameStateChanged>(OnGameStateChanged);
        }

        private void KickOff()
        {
            if (_phase != Phase.None) return;
            _phase = Phase.RequestedScene;
            // Request loading state then scene load
            EventRouter.Publish(new GameStateManager.GameStateEnterRequested { Target = GameStateManager.GameState.Boot });
            EventRouter.Publish(new GameStateManager.GameStateEnterRequested { Target = GameStateManager.GameState.Loading });
            EventRouter.Publish(new SceneManager.SceneLoadRequested { SceneId = initialSceneId });
        }

        private void OnSceneActivated(SceneActivated evt)
        {
            if (_phase != Phase.RequestedScene) return;
            _phase = Phase.SceneActivated;
            // After scene activation request player spawn (PlayerManager may auto-spawn already)
            // If player manager uses automatic spawn this can be skipped; otherwise publish explicit request
            // EventRouter.Publish(new PlayerManager.PlayerSpawnRequested { SpawnPoint = null }); // spawnpoint resolved internally
        }

        private void OnPlayerSpawned(PlayerSpawned evt)
        {
            if (_phase != Phase.SceneActivated) return;
            _phase = Phase.PlayerSpawned;
            _player = evt.Player;
            // Trigger load if configured
            if (autoLoadSlot >= 0)
            {
                EventRouter.Publish(new SaveManager.LoadRequested { SlotId = autoLoadSlot });
            }
            else
            {
                // Simulate load completed gating skip
                EventRouter.Publish(new SaveManager.LoadCompleted { SlotId = -1, Success = true });
            }
        }

        private void OnLoadCompleted(LoadCompleted evt)
        {
            if (_phase != Phase.PlayerSpawned) return;
            _phase = Phase.LoadCompleted;
            // Ensure camera target set (if not auto-bound)
            if (_player != null && _cameraTarget == null)
            {
                EventRouter.Publish(new CameraManager.CameraTargetSetRequested { Target = _player.transform });
            }
            else if (_cameraTarget != null)
            {
                // Already bound, progress artificially
                EventRouter.Publish(new CameraManager.CameraTargetChanged { OldTarget = null, NewTarget = _cameraTarget });
            }
        }

        private void OnCameraTargetChanged(CameraTargetChanged evt)
        {
            _cameraTarget = evt.NewTarget;
            if (_phase != Phase.LoadCompleted) return;
            _phase = Phase.CameraAttached;
            // Request enter Playing state
            EventRouter.Publish(new GameStateManager.GameStateEnterRequested { Target = GameStateManager.GameState.Playing });
        }

        private void OnGameStateChanged(GameStateChanged evt)
        {
            if (evt.Current == GameStateManager.GameState.Playing && _phase == Phase.CameraAttached)
            {
                _phase = Phase.PlayingEntered;
            }
        }
    }
}
