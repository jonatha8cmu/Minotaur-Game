using UnityEngine;
using Core.Events;

// Lifecycle Order (Bootstrap):
// After SceneManager publishes SceneActivated -> PlayerManager spawns -> publishes PlayerSpawned -> SaveManager can restore -> CameraManager attaches.
public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    [Header("Player Prefab Reference")]
    [SerializeField] private GameObject playerPrefab; // Assign via inspector
    [SerializeField] private Transform defaultSpawnPoint; // Fallback spawn

    // Event payloads (outgoing)
    public struct PlayerSpawned { public GameObject Player; }
    public struct PlayerDespawned { }
    public struct PlayerDied { public string Cause; }
    public struct PlayerRespawned { public GameObject Player; }

    // Incoming request / trigger events
    public struct PlayerSpawnRequested { public Transform SpawnPoint; }
    public struct PlayerDespawnRequested { }
    public struct SceneActivated { public string SceneId; } // mirrored signature from SceneManager for subscription

    public Transform PlayerTransform { get; private set; }
    private GameObject _playerInstance;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        EventRouter.Subscribe<SceneActivated>(OnSceneActivated);
        EventRouter.Subscribe<PlayerSpawnRequested>(OnPlayerSpawnRequested);
        EventRouter.Subscribe<PlayerDespawnRequested>(OnPlayerDespawnRequested);
    }

    private void OnDisable()
    {
        EventRouter.Unsubscribe<SceneActivated>(OnSceneActivated);
        EventRouter.Unsubscribe<PlayerSpawnRequested>(OnPlayerSpawnRequested);
        EventRouter.Unsubscribe<PlayerDespawnRequested>(OnPlayerDespawnRequested);
    }

    private void OnSceneActivated(SceneActivated evt)
    {
        // If automatic spawn desired: publish request using default spawn
        if (defaultSpawnPoint != null)
        {
            EventRouter.Publish(new PlayerSpawnRequested { SpawnPoint = defaultSpawnPoint });
        }
    }

    private void OnPlayerSpawnRequested(PlayerSpawnRequested evt)
    {
        if (_playerInstance != null) return; // Already spawned; use respawn logic instead
        Transform spawn = evt.SpawnPoint != null ? evt.SpawnPoint : defaultSpawnPoint;
        if (spawn == null || playerPrefab == null) return;

        _playerInstance = Instantiate(playerPrefab, spawn.position, spawn.rotation);
        PlayerTransform = _playerInstance.transform;

        EventRouter.Publish(new PlayerSpawned { Player = _playerInstance });
    }

    private void OnPlayerDespawnRequested(PlayerDespawnRequested evt)
    {
        if (_playerInstance == null) return;
        Destroy(_playerInstance);
        _playerInstance = null;
        PlayerTransform = null;
        EventRouter.Publish(new PlayerDespawned());
    }

    // Optional external call for death handling (without coupling to health system yet)
    public void NotifyPlayerDied(string cause)
    {
        if (_playerInstance == null) return;
        EventRouter.Publish(new PlayerDied { Cause = cause });
        // Placeholder: choose to respawn automatically or rely on GameStateManager
    }

    public void Respawn(Transform spawnPoint)
    {
        if (_playerInstance == null) return; // Use spawn flow if not present
        Transform spawn = spawnPoint != null ? spawnPoint : defaultSpawnPoint;
        if (spawn == null) return;
        _playerInstance.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        EventRouter.Publish(new PlayerRespawned { Player = _playerInstance });
    }

    // Public API wrappers publishing requests
    public void RequestSpawnAt(Transform spawnPoint)
    {
        EventRouter.Publish(new PlayerSpawnRequested { SpawnPoint = spawnPoint });
    }

    public void RequestDespawn()
    {
        EventRouter.Publish(new PlayerDespawnRequested());
    }

    public GameObject GetPlayerObject() => PlayerTransform ? PlayerTransform.gameObject : null;
}
