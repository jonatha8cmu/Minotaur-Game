using UnityEngine;
using Core.Events;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    [Header("Player Prefab Reference")]
    [SerializeField] private GameObject playerPrefab; // prefab asset reference
    [SerializeField] private Transform defaultSpawnPoint; // spawn location
    [SerializeField] private Transform playerParent; // optional parent for spawned player (e.g., Gameplay)

    public Transform PlayerTransform { get; private set; }
    private GameObject _playerInstance;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Early subscriptions; adoption happens only after scene activation
        EventRouter.Subscribe<SceneActivated>(OnSceneActivated);
        EventRouter.Subscribe<PlayerSpawnRequested>(OnPlayerSpawnRequested);
        EventRouter.Subscribe<PlayerDespawnRequested>(OnPlayerDespawnRequested);
    }

    private void OnDestroy()
    {
        EventRouter.Unsubscribe<SceneActivated>(OnSceneActivated);
        EventRouter.Unsubscribe<PlayerSpawnRequested>(OnPlayerSpawnRequested);
        EventRouter.Unsubscribe<PlayerDespawnRequested>(OnPlayerDespawnRequested);
        if (Instance == this) Instance = null;
    }

    private void OnSceneActivated(SceneActivated evt)
    {
        // HOT FIX: adopt-if-present else spawn. Never both.
        TryAdoptExistingPlayer();

        if (_playerInstance == null && defaultSpawnPoint != null)
        {
            EventRouter.Publish(new PlayerSpawnRequested { SpawnPoint = defaultSpawnPoint });
        }
    }

    private void OnPlayerSpawnRequested(PlayerSpawnRequested evt)
    {
        if (_playerInstance != null) return; // Already adopted or spawned

        // Only instantiate from prefab asset (not a scene object)
        if (playerPrefab == null || playerPrefab.scene.IsValid()) return;

        Transform spawn = evt.SpawnPoint != null ? evt.SpawnPoint : defaultSpawnPoint;
        if (spawn == null) return;

        _playerInstance = playerParent != null
            ? Instantiate(playerPrefab, spawn.position, spawn.rotation, playerParent)
            : Instantiate(playerPrefab, spawn.position, spawn.rotation);

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

    private void TryAdoptExistingPlayer()
    {
        // Adopt existing scene player if present (tag-based or by component later)
        var existing = GameObject.FindWithTag("Player");
        if (existing != null)
        {
            _playerInstance = existing;
            PlayerTransform = _playerInstance.transform;
            EventRouter.Publish(new PlayerSpawned { Player = _playerInstance });
        }
    }

    public void NotifyPlayerDied(string cause)
    {
        if (_playerInstance == null) return;
        EventRouter.Publish(new PlayerDied { Cause = cause });
    }

    public void Respawn(Transform spawnPoint)
    {
        if (_playerInstance == null) return;
        Transform spawn = spawnPoint != null ? spawnPoint : defaultSpawnPoint;
        if (spawn == null) return;
        _playerInstance.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        EventRouter.Publish(new PlayerRespawned { Player = _playerInstance });
    }

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
