using UnityEngine;

namespace Core.Events
{
    // Canonical event definitions for cross-manager communication.
    // Keep neutral: no manager-type references; only payload data.

    // Scene lifecycle
    public struct SceneLoadRequested { public string SceneId; }
    public struct SceneLoadStarted { public string SceneId; }
    public struct SceneLoadProgress { public string SceneId; public float Progress; }
    public struct SceneActivated { public string SceneId; }
    public struct SceneUnloadRequested { public string SceneId; }
    public struct SceneUnloadStarted { public string SceneId; }
    public struct SceneUnloaded { public string SceneId; }

    // Player lifecycle
    public struct PlayerSpawnRequested { public Transform SpawnPoint; }
    public struct PlayerSpawned { public GameObject Player; }
    public struct PlayerDespawnRequested { }
    public struct PlayerDespawned { }
    public struct PlayerDied { public string Cause; }
    public struct PlayerRespawned { public GameObject Player; }

    // Save operations
    public struct SaveRequested { public int SlotId; }
    public struct SaveStarted { public int SlotId; }
    public struct SaveCompleted { public int SlotId; public bool Success; }
    public struct LoadRequested { public int SlotId; }
    public struct LoadStarted { public int SlotId; }
    public struct LoadCompleted { public int SlotId; public bool Success; }

    // Game state
    public enum GameState { None, Boot, Loading, Playing, Paused, GameOver }
    public struct GameStateEnterRequested { public GameState Target; }
    public struct GameStateChanging { public GameState From; public GameState To; }
    public struct GameStateChanged { public GameState Previous; public GameState Current; }
    public struct PauseToggleRequested { }
    public struct PauseToggled { public bool IsPaused; }

    // Camera
    public enum CameraMode { Idle, Gameplay, Paused, Loading, GameOver, Boot }
    public struct CameraTargetSetRequested { public Transform Target; }
    public struct CameraTargetChanged { public Transform OldTarget; public Transform NewTarget; }
    public struct CameraModeChanged { public CameraMode Mode; }

    // Composite domain events (for UI)
    public struct GameplayReady { }
    public struct PlayerContextChanged { public GameObject Player; public Transform CameraTarget; }
}
