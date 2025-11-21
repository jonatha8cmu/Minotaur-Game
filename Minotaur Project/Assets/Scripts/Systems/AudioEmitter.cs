using UnityEngine;
using FMODUnity;
using FMOD.Studio;

/// <summary>
/// Minimal emitter: centralized occlusion; looping flag removed (FMOD handles loops).
/// </summary>
[RequireComponent(typeof(OcclusionComponent))]
public class AudioEmitter : MonoBehaviour
{
    [SerializeField] private string eventPath;
    [SerializeField] private bool playOnEnable;
    [SerializeField] private float occlusionUpdateInterval = 0.15f;
    [SerializeField] private float maxOcclusionDistance = 45f;

    [Header("FMOD Parameters")] 
    [SerializeField] private string directionalParam = "Directional Occlusion";
    [SerializeField] private string spatialParam = "Spatial Occlusion";

    [Header("One-Shot Window")] 
    [SerializeField] private float oneShotUpdateDuration = 0.75f;

    [Header("Debug")] 
    [SerializeField] private bool logParameterChanges;

    private EventInstance instance;
    private OcclusionComponent occlusion;
    private bool started;
    private double oneShotEnd;
    private double nextDue;

    public bool IsOcclusionDue(double now) => started && now >= nextDue && now < oneShotEnd; // one-shot only

    void Awake()
    {
        occlusion = GetComponent<OcclusionComponent>();
        if (!string.IsNullOrEmpty(eventPath))
        {
            instance = RuntimeManager.CreateInstance(eventPath);
            instance.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject));
            RuntimeManager.AttachInstanceToGameObject(instance, gameObject);
        }
    }

    void OnEnable()
    {
        AudioManager.Instance?.RegisterEmitter(this);
        if (playOnEnable) Play();
    }

    void OnDisable()
    {
        AudioManager.Instance?.UnregisterEmitter(this);
        Stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
    }

    public void Play()
    {
        if (!instance.isValid()) return;
        instance.start();
        started = true;
        oneShotEnd = AudioSettings.dspTime + oneShotUpdateDuration;
        TickOcclusion(AudioSettings.dspTime, true);
    }

    public void Stop(FMOD.Studio.STOP_MODE mode = FMOD.Studio.STOP_MODE.ALLOWFADEOUT)
    {
        if (!instance.isValid()) return;
        instance.stop(mode);
        started = false;
    }

    public void Release()
    {
        if (instance.isValid())
        {
            instance.release();
        }
    }

    public void TickOcclusion(double now, bool force = false)
    {
        if (!instance.isValid() || occlusion == null) return;
        if (now >= oneShotEnd) { started = false; return; }
        var r = occlusion.GetCurrentOcclusion(force);
        float dir = r.Distance > maxOcclusionDistance ? 0f : r.Directional;
        float spat = r.Distance > maxOcclusionDistance ? 0f : r.Spatial;
        instance.setParameterByName(directionalParam, dir);
        instance.setParameterByName(spatialParam, spat);
        if (logParameterChanges) Debug.Log($"[AudioEmitter] {name} Dir={dir:F2} Spatial={spat:F2} Dist={r.Distance:F1}");
        nextDue = now + occlusionUpdateInterval;
    }
}
