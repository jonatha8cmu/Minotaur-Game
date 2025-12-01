using UnityEngine;
using Core.Events;

namespace Core.Bootstrap
{
    public class BootstrapSequence : MonoBehaviour
    {
        public static BootstrapSequence Instance { get; private set; }

        private enum Phase { None, RequestedScene, SceneActivated, PlayerSpawned, LoadCompleted, CameraAttached, PlayingEntered }
        private Phase _phase = Phase.None;

        [SerializeField] private string initialSceneId = "Gameplay";
        [SerializeField] private int autoLoadSlot = -1;

        private GameObject _player;
        private Transform _cameraTarget;

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
            EventRouter.Publish(new GameStateEnterRequested { Target = GameState.Boot });
            EventRouter.Publish(new GameStateEnterRequested { Target = GameState.Loading });
            EventRouter.Publish(new SceneLoadRequested { SceneId = initialSceneId });
        }

        private void OnSceneActivated(SceneActivated evt)
        {
            if (_phase != Phase.RequestedScene) return;
            _phase = Phase.SceneActivated;
        }

        private void OnPlayerSpawned(PlayerSpawned evt)
        {
            if (_phase != Phase.SceneActivated) return;
            _phase = Phase.PlayerSpawned;
            _player = evt.Player;
            if (autoLoadSlot >= 0)
            {
                EventRouter.Publish(new LoadRequested { SlotId = autoLoadSlot });
            }
            else
            {
                EventRouter.Publish(new LoadCompleted { SlotId = -1, Success = true });
            }
        }

        private void OnLoadCompleted(LoadCompleted evt)
        {
            if (_phase != Phase.PlayerSpawned) return;
            _phase = Phase.LoadCompleted;
            if (_player != null && _cameraTarget == null)
            {
                EventRouter.Publish(new CameraTargetSetRequested { Target = _player.transform });
            }
            else if (_cameraTarget != null)
            {
                EventRouter.Publish(new CameraTargetChanged { OldTarget = null, NewTarget = _cameraTarget });
            }
        }

        private void OnCameraTargetChanged(CameraTargetChanged evt)
        {
            _cameraTarget = evt.NewTarget;
            if (_phase != Phase.LoadCompleted) return;
            _phase = Phase.CameraAttached;
            EventRouter.Publish(new GameStateEnterRequested { Target = GameState.Playing });
        }

        private void OnGameStateChanged(GameStateChanged evt)
        {
            if (evt.Current == GameState.Playing && _phase == Phase.CameraAttached)
            {
                _phase = Phase.PlayingEntered;
            }
        }
    }
}
