using UnityEngine;
using FMOD.Studio;

/// <summary>
/// Simple test script for the audio system.
/// Demonstrates basic usage for your test scene setup (walls, player, looping test song).
/// </summary>
public class AudioTest : MonoBehaviour
{
    [Header("Test Settings")]
    [SerializeField] private string testEventPath = "event:/TestSong";
    [SerializeField] private bool useOcclusion = true;

    [Header("Runtime Controls (Toggle in Inspector)")]
    [SerializeField] private bool playOneShotNoOcclusion = false;
    [SerializeField] private bool playOneShotWithOcclusion = false;
    [SerializeField] private bool startLoopingSound = false;
    [SerializeField] private bool stopLoopingSound = false;

    private AudioOcclusion occlusion;
    private const string LOOP_ID = "test_loop";

    void Start()
    {
        occlusion = GetComponent<AudioOcclusion>();
    }

    void Update()
    {
        // Runtime controls
        if (playOneShotNoOcclusion)
        {
            playOneShotNoOcclusion = false;
            TestOneShotNoOcclusion();
        }

        if (playOneShotWithOcclusion)
        {
            playOneShotWithOcclusion = false;
            TestOneShotWithOcclusion();
        }

        if (startLoopingSound)
        {
            startLoopingSound = false;
            TestStartLooping();
        }

        if (stopLoopingSound)
        {
            stopLoopingSound = false;
            TestStopLooping();
        }

        // Update looping sound occlusion if it's playing
        UpdateLoopingOcclusion();
    }

    void TestOneShotNoOcclusion()
    {
        Debug.Log("[AudioTest] Playing one-shot without occlusion");
        AudioManager.Instance.PlayOneShot(testEventPath, gameObject);
    }

    void TestOneShotWithOcclusion()
    {
        if (occlusion == null)
        {
            Debug.LogWarning("[AudioTest] No AudioOcclusion component found!");
            return;
        }

        Debug.Log("[AudioTest] Playing one-shot with occlusion");
        occlusion.PlaySound(testEventPath, useDirectional: true, useSpatial: true);
    }

    void TestStartLooping()
    {
        Debug.Log("[AudioTest] Starting looping sound");
        AudioManager.Instance.CreatePersistentInstance(LOOP_ID, testEventPath, gameObject);
        AudioManager.Instance.StartPersistentInstance(LOOP_ID);
    }

    void TestStopLooping()
    {
        Debug.Log("[AudioTest] Stopping looping sound");
        AudioManager.Instance.StopPersistentInstance(LOOP_ID);
    }

    void UpdateLoopingOcclusion()
    {
        if (!useOcclusion || occlusion == null) return;
        
        // Only update if the sound is playing
        if (AudioManager.Instance.IsPersistentInstancePlaying(LOOP_ID))
        {
            occlusion.UpdateOcclusion(LOOP_ID, useDirectional: true, useSpatial: true);
        }
    }

    void OnDrawGizmos()
    {
        if (occlusion == null) return;

        // Draw line to listener
        Transform listener = occlusion.GetListener();
        if (listener != null)
        {
            bool hasLOS = occlusion.HasLineOfSightToListener();
            Gizmos.color = hasLOS ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position, listener.position);
            
            // Draw distance text
            float distance = occlusion.GetDistanceToListener();
            UnityEditor.Handles.Label(transform.position + Vector3.up, 
                $"Dist: {distance:F1}m | LOS: {hasLOS}");
        }
    }
}
