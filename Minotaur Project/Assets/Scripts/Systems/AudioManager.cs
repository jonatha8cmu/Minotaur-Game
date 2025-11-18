using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

/// <summary>
/// Central audio system manager. Handles sound playback, instance management, and audio system initialization.
/// Designed to be extended with features like occlusion, mixing, and spatial audio.
/// </summary>
public class AudioManager : MonoBehaviour
{
    #region Singleton
    private static AudioManager instance;
    public static AudioManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<AudioManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("AudioManager");
                    instance = go.AddComponent<AudioManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }
    #endregion

    #region Serialized Fields
    [Header("Debug")]
    [SerializeField] private bool logSoundPlayback = false;
    [SerializeField] private bool showActiveInstances = false;
    #endregion

    #region Private Fields
    // Track active persistent instances
    private readonly Dictionary<string, EventInstance> persistentInstances = new();
    
    // Track all one-shot instances for cleanup
    private readonly List<EventInstance> activeSounds = new();
    
    // Cached listener reference
    private Transform listenerTransform;
    #endregion

    #region Unity Callbacks
    void Awake()
    {
        // Singleton enforcement
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        Initialize();
    }

    void Update()
    {
        CleanupFinishedSounds();

        if (showActiveInstances)
            LogActiveInstances();
    }

    void OnDestroy()
    {
        if (instance == this)
            Cleanup();
    }
    #endregion

    #region Initialization & Cleanup
    /// <summary>Initialize the audio system and cache references.</summary>
    private void Initialize()
    {
        CacheListener();

        if (logSoundPlayback)
            Debug.Log("[AudioManager] Initialized");
    }

    /// <summary>Cache or refresh the FMOD listener transform.</summary>
    private void CacheListener()
    {
        var listener = FindFirstObjectByType<StudioListener>();
        if (listener != null)
            listenerTransform = listener.transform;
    }

    /// <summary>Clean up all active sound instances.</summary>
    private void Cleanup()
    {
        // Stop and release persistent instances
        foreach (var kvp in persistentInstances)
        {
            if (kvp.Value.isValid())
            {
                kvp.Value.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                kvp.Value.release();
            }
        }
        persistentInstances.Clear();

        // Clean active one-shots
        foreach (var instance in activeSounds)
        {
            if (instance.isValid())
            {
                instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                instance.release();
            }
        }
        activeSounds.Clear();

        if (logSoundPlayback)
            Debug.Log("[AudioManager] Cleaned up all audio instances");
    }

    /// <summary>Remove finished one-shot sounds from tracking.</summary>
    private void CleanupFinishedSounds()
    {
        activeSounds.RemoveAll(instance =>
        {
            if (!instance.isValid())
                return true;

            instance.getPlaybackState(out PLAYBACK_STATE state);
            return state == PLAYBACK_STATE.STOPPED;
        });
    }
    #endregion

    #region Core Playback - One-Shot
    /// <summary>Play a one-shot sound attached to a GameObject with optional parameters.</summary>
    public EventInstance PlayOneShot(string eventPath, GameObject attachTo, Dictionary<string, float> parameters = null)
    {
        if (attachTo == null)
        {
            Debug.LogWarning("[AudioManager] PlayOneShot called with null GameObject. Sound will not play.");
            return default;
        }

        var instance = CreateInstance(eventPath, attachTo.transform.position);
        if (!instance.isValid()) return instance;

        RuntimeManager.AttachInstanceToGameObject(instance, attachTo);

        if (parameters != null)
            SetParameters(instance, parameters);

        instance.start();
        instance.release();
        activeSounds.Add(instance);

        if (logSoundPlayback)
            Debug.Log($"[AudioManager] Playing one-shot: {eventPath} on {attachTo.name}");

        return instance;
    }
    #endregion

    #region Core Playback - Persistent Instances
    /// <summary>Create and start a persistent looping sound with a unique ID.</summary>
    public EventInstance CreatePersistentInstance(string id, string eventPath, GameObject attachTo)
    {
        if (attachTo == null)
        {
            Debug.LogWarning($"[AudioManager] CreatePersistentInstance called with null GameObject for {id}. Sound will not be created.");
            return default;
        }

        // Stop existing instance with same ID
        if (persistentInstances.ContainsKey(id))
        {
            StopPersistentInstance(id);
        }

        var instance = CreateInstance(eventPath, attachTo.transform.position);
        if (!instance.isValid()) return instance;

        RuntimeManager.AttachInstanceToGameObject(instance, attachTo);
        persistentInstances[id] = instance;

        if (logSoundPlayback)
            Debug.Log($"[AudioManager] Created persistent instance: {id} ({eventPath})");

        return instance;
    }

    /// <summary>Start a persistent instance by ID.</summary>
    public void StartPersistentInstance(string id)
    {
        if (persistentInstances.TryGetValue(id, out EventInstance instance) && instance.isValid())
        {
            instance.start();
            if (logSoundPlayback)
                Debug.Log($"[AudioManager] Started persistent instance: {id}");
        }
    }

    /// <summary>Stop a persistent instance by ID.</summary>
    public void StopPersistentInstance(string id, FMOD.Studio.STOP_MODE stopMode = FMOD.Studio.STOP_MODE.ALLOWFADEOUT)
    {
        if (persistentInstances.TryGetValue(id, out EventInstance instance) && instance.isValid())
        {
            instance.stop(stopMode);
            instance.release();
            persistentInstances.Remove(id);

            if (logSoundPlayback)
                Debug.Log($"[AudioManager] Stopped persistent instance: {id}");
        }
    }

    /// <summary>Get a persistent instance by ID for manual control.</summary>
    public EventInstance GetPersistentInstance(string id)
    {
        persistentInstances.TryGetValue(id, out EventInstance instance);
        return instance;
    }

    /// <summary>Check if a persistent instance exists and is playing.</summary>
    public bool IsPersistentInstancePlaying(string id)
    {
        if (persistentInstances.TryGetValue(id, out EventInstance instance) && instance.isValid())
        {
            instance.getPlaybackState(out PLAYBACK_STATE state);
            return state == PLAYBACK_STATE.PLAYING;
        }
        return false;
    }
    #endregion

    #region Instance Creation & Management
    /// <summary>Create an FMOD event instance without starting it.</summary>
    private EventInstance CreateInstance(string eventPath, Vector3 position)
    {
        EventInstance instance = RuntimeManager.CreateInstance(eventPath);
        
        if (!instance.isValid())
        {
            Debug.LogError($"[AudioManager] Failed to create instance for: {eventPath}");
            return instance;
        }

        instance.set3DAttributes(RuntimeUtils.To3DAttributes(position));
        return instance;
    }

    /// <summary>Set multiple parameters on an event instance.</summary>
    public void SetParameters(EventInstance instance, Dictionary<string, float> parameters)
    {
        if (!instance.isValid() || parameters == null) return;

        foreach (var param in parameters)
        {
            instance.setParameterByName(param.Key, param.Value);
        }
    }

    /// <summary>Set a single parameter on an event instance.</summary>
    public void SetParameter(EventInstance instance, string parameterName, float value)
    {
        if (instance.isValid())
            instance.setParameterByName(parameterName, value);
    }

    /// <summary>Set a global parameter across the entire FMOD system.</summary>
    public void SetGlobalParameter(string parameterName, float value)
    {
        RuntimeManager.StudioSystem.setParameterByName(parameterName, value);

        if (logSoundPlayback)
            Debug.Log($"[AudioManager] Set global parameter: {parameterName} = {value}");
    }
    #endregion

    #region Utility Methods
    /// <summary>Stop all currently playing sounds.</summary>
    public void StopAllSounds(FMOD.Studio.STOP_MODE stopMode = FMOD.Studio.STOP_MODE.ALLOWFADEOUT)
    {
        // Stop all one-shots
        foreach (var instance in activeSounds)
        {
            if (instance.isValid())
                instance.stop(stopMode);
        }
        activeSounds.Clear();

        // Stop all persistent instances
        foreach (var kvp in persistentInstances)
        {
            if (kvp.Value.isValid())
            {
                kvp.Value.stop(stopMode);
                kvp.Value.release();
            }
        }
        persistentInstances.Clear();

        if (logSoundPlayback)
            Debug.Log("[AudioManager] Stopped all sounds");
    }

    /// <summary>Pause/unpause all sounds.</summary>
    public void PauseAll(bool pause)
    {
        RuntimeManager.GetBus("bus:/").setPaused(pause);

        if (logSoundPlayback)
            Debug.Log($"[AudioManager] {(pause ? "Paused" : "Unpaused")} all audio");
    }

    /// <summary>Get the listener transform (cached).</summary>
    public Transform GetListener()
    {
        if (listenerTransform == null)
            CacheListener();
        return listenerTransform;
    }
    #endregion

    #region Debug
    private void LogActiveInstances()
    {
        Debug.Log($"[AudioManager] Active one-shots: {activeSounds.Count} | Persistent: {persistentInstances.Count}");
    }
    #endregion
}
