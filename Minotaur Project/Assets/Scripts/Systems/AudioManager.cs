using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

/// <summary>
/// AudioManager: owns FMOD event instance creation, listener caching, and centralized occlusion ticking for AudioEmitter.
/// Must exist once in the game (place a prefab in the first scene). No auto-respawn after destruction or during quit.
/// </summary>
public class AudioManager : MonoBehaviour
{
    // Singleton (no auto create after startup)
    private static AudioManager instance;
    private static bool applicationQuitting;

    public static AudioManager Instance
    {
        get
        {
            if (applicationQuitting) return instance; // do not recreate while quitting
            if (instance != null) return instance;
            instance = FindFirstObjectByType<AudioManager>();
            return instance; // may be null if not placed in scene
        }
    }

    [Header("Debug")] [SerializeField] private bool logSoundPlayback;
    [SerializeField] private bool showActiveInstances;

    // Runtime collections
    private readonly Dictionary<string, EventInstance> persistentInstances = new();
    private readonly List<EventInstance> activeOneShots = new();
    private readonly List<AudioEmitter> emitters = new();

    private Transform listenerTransform;
    private int staggerIndex;

    #region Unity Lifecycle
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        CacheListener();
        if (logSoundPlayback) Debug.Log("[AudioManager] Awake initialized");
    }

    private void Update()
    {
        CleanupFinishedOneShots();
        TickEmitters();
        if (showActiveInstances)
            Debug.Log($"[AudioManager] OneShots={activeOneShots.Count} Persistent={persistentInstances.Count} Emitters={emitters.Count}");
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            CleanupAll(FMOD.Studio.STOP_MODE.IMMEDIATE);
            instance = null;
        }
    }

    private void OnApplicationQuit()
    {
        applicationQuitting = true;
        CleanupAll(FMOD.Studio.STOP_MODE.IMMEDIATE);
    }
    #endregion

    #region Listener
    private void CacheListener()
    {
        var l = FindFirstObjectByType<StudioListener>();
        if (l != null) listenerTransform = l.transform;
    }

    public Transform GetListener()
    {
        if (listenerTransform == null) CacheListener();
        return listenerTransform;
    }
    #endregion

    #region Emitter Management
    public void RegisterEmitter(AudioEmitter e)
    {
        if (e == null || emitters.Contains(e)) return;
        emitters.Add(e);
    }

    public void UnregisterEmitter(AudioEmitter e)
    {
        if (e == null) return;
        emitters.Remove(e);
    }

    private void TickEmitters()
    {
        if (emitters.Count == 0) return;
        staggerIndex = (staggerIndex + 1) % emitters.Count;
        double now = AudioSettings.dspTime;
        for (int i = 0; i < emitters.Count; i++)
        {
            var e = emitters[i];
            if (e == null)
            {
                emitters.RemoveAt(i--);
                continue;
            }
            if (i == staggerIndex || e.IsOcclusionDue(now))
                e.TickOcclusion(now);
        }
    }
    #endregion

    #region Playback - One Shot
    public EventInstance PlayOneShot(string eventPath, GameObject attachTo, Dictionary<string, float> parameters = null)
    {
        if (attachTo == null) return default;
        var inst = CreateInstance(eventPath, attachTo.transform.position);
        if (!inst.isValid()) return inst;
        RuntimeManager.AttachInstanceToGameObject(inst, attachTo);
        if (parameters != null) SetParameters(inst, parameters);
        inst.start();
        inst.release(); // auto cleanup after playback
        activeOneShots.Add(inst);
        if (logSoundPlayback) Debug.Log($"[AudioManager] OneShot {eventPath} on {attachTo.name}");
        return inst;
    }
    #endregion

    #region Playback - Persistent
    public EventInstance CreatePersistentInstance(string id, string eventPath, GameObject attachTo)
    {
        if (attachTo == null) return default;
        if (persistentInstances.ContainsKey(id)) StopPersistentInstance(id);
        var inst = CreateInstance(eventPath, attachTo.transform.position);
        if (!inst.isValid()) return inst;
        RuntimeManager.AttachInstanceToGameObject(inst, attachTo);
        persistentInstances[id] = inst;
        if (logSoundPlayback) Debug.Log($"[AudioManager] Persistent created {id}");
        return inst;
    }

    public void StartPersistentInstance(string id)
    {
        if (persistentInstances.TryGetValue(id, out var inst) && inst.isValid())
        {
            inst.start();
            if (logSoundPlayback) Debug.Log($"[AudioManager] Persistent started {id}");
        }
    }

    public void StopPersistentInstance(string id, FMOD.Studio.STOP_MODE mode = FMOD.Studio.STOP_MODE.ALLOWFADEOUT)
    {
        if (persistentInstances.TryGetValue(id, out var inst) && inst.isValid())
        {
            inst.stop(mode);
            inst.release();
            persistentInstances.Remove(id);
            if (logSoundPlayback) Debug.Log($"[AudioManager] Persistent stopped {id}");
        }
    }

    public EventInstance GetPersistentInstance(string id)
    {
        persistentInstances.TryGetValue(id, out var inst);
        return inst;
    }

    public bool IsPersistentInstancePlaying(string id)
    {
        if (persistentInstances.TryGetValue(id, out var inst) && inst.isValid())
        {
            inst.getPlaybackState(out PLAYBACK_STATE s);
            return s == PLAYBACK_STATE.PLAYING;
        }
        return false;
    }
    #endregion

    #region Parameter Helpers
    private EventInstance CreateInstance(string eventPath, Vector3 position)
    {
        var inst = RuntimeManager.CreateInstance(eventPath);
        if (!inst.isValid())
        {
            Debug.LogError($"[AudioManager] Failed to create instance for {eventPath}");
            return inst;
        }
        inst.set3DAttributes(RuntimeUtils.To3DAttributes(position));
        return inst;
    }

    public void SetParameters(EventInstance inst, Dictionary<string, float> parameters)
    {
        if (!inst.isValid() || parameters == null) return;
        foreach (var p in parameters) inst.setParameterByName(p.Key, p.Value);
    }

    public void SetParameter(EventInstance inst, string name, float value)
    {
        if (inst.isValid()) inst.setParameterByName(name, value);
    }

    public void SetGlobalParameter(string name, float value)
    {
        RuntimeManager.StudioSystem.setParameterByName(name, value);
        if (logSoundPlayback) Debug.Log($"[AudioManager] Global {name}={value}");
    }
    #endregion

    #region Global Control
    public void StopAllSounds(FMOD.Studio.STOP_MODE mode = FMOD.Studio.STOP_MODE.ALLOWFADEOUT)
    {
        foreach (var inst in activeOneShots)
            if (inst.isValid()) inst.stop(mode);
        activeOneShots.Clear();

        foreach (var kv in persistentInstances)
            if (kv.Value.isValid()) { kv.Value.stop(mode); kv.Value.release(); }
        persistentInstances.Clear();

        if (logSoundPlayback) Debug.Log("[AudioManager] Stopped all sounds");
    }

    public void PauseAll(bool pause)
    {
        RuntimeManager.GetBus("bus:/").setPaused(pause);
        if (logSoundPlayback) Debug.Log($"[AudioManager] {(pause ? "Paused" : "Unpaused")} all");
    }
    #endregion

    #region Cleanup
    private void CleanupFinishedOneShots()
    {
        activeOneShots.RemoveAll(inst =>
        {
            if (!inst.isValid()) return true;
            inst.getPlaybackState(out PLAYBACK_STATE s);
            return s == PLAYBACK_STATE.STOPPED;
        });
    }

    private void CleanupAll(FMOD.Studio.STOP_MODE mode)
    {
        StopAllSounds(mode);
        emitters.Clear();
    }
    #endregion
}
