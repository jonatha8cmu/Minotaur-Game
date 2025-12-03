using UnityEngine;
using Core.Events;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    [Header("Player Prefab Reference")]
    [SerializeField] private GameObject playerPrefab; // prefab asset reference (must be an asset, not a scene object)
    [SerializeField] private Transform playerParent;   // optional parent for spawned player (e.g., Gameplay)

    public Transform PlayerTransform { get; private set; }
    private GameObject _playerInstance;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Subscriptions
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
        // Manager-controlled spawn: never adopt. Find scene-defined spawn and request spawn if none exists.
        if (_playerInstance == null)
        {
            var spawn = FindSpawnInScene();
            EventRouter.Publish(new PlayerSpawnRequested { SpawnPoint = spawn });
        }
    }

    private void OnPlayerSpawnRequested(PlayerSpawnRequested evt)
    {
        if (_playerInstance != null) return; // Already spawned
        if (playerPrefab == null || playerPrefab.scene.IsValid()) return; // Require prefab asset

        var spawn = evt.SpawnPoint != null ? evt.SpawnPoint : FindSpawnInScene();
        var pos = spawn != null ? spawn.position : Vector3.zero;
        var rot = spawn != null ? spawn.rotation : Quaternion.identity;

        _playerInstance = playerParent != null
            ? Instantiate(playerPrefab, pos, rot, playerParent)
            : Instantiate(playerPrefab, pos, rot);

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

    private Transform FindSpawnInScene()
    {
        var spawnGo = GameObject.FindWithTag("PlayerSpawn");
        return spawnGo != null ? spawnGo.transform : null;
    }

    public void Respawn(Transform spawnPoint)
    {
        if (_playerInstance == null) return;
        var spawn = spawnPoint != null ? spawnPoint : FindSpawnInScene();
        if (spawn == null) return;
        _playerInstance.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        EventRouter.Publish(new PlayerRespawned { Player = _playerInstance });
    }

    public void RequestSpawnAt(Transform spawnPoint) => EventRouter.Publish(new PlayerSpawnRequested { SpawnPoint = spawnPoint });
    public void RequestDespawn() => EventRouter.Publish(new PlayerDespawnRequested());
    public GameObject GetPlayerObject() => PlayerTransform ? PlayerTransform.gameObject : null;
}
