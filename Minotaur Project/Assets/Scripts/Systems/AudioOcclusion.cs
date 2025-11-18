using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

/// <summary>
/// Audio occlusion system for 2D environments using raycasting with bounces.
/// Calculates directional (focused cone) and spatial (360°) occlusion values.
/// Works with AudioManager for sound playback.
/// </summary>
public class AudioOcclusion : MonoBehaviour
{
    #region Serialized Fields
    [Header("Occlusion Settings")]
    [Tooltip("Maximum distance for audio raycast checks")]
    [SerializeField] private float maxSoundDistance = 35f;

    [Tooltip("Number of bounces per occlusion ray")]
    [SerializeField] private int maxBounces = 3;

    [Tooltip("Layers that block sound")]
    [SerializeField] private LayerMask occlusionLayer;

    [Header("Directional Occlusion (Focused Cone)")]
    [Tooltip("Number of rays in the directional cone")]
    [SerializeField] private int directionalRayCount = 7;

    [Tooltip("Angle of the directional cone in degrees")]
    [SerializeField] private float directionalConeAngle = 80f;

    [Header("Spatial Occlusion (360° Coverage)")]
    [Tooltip("Number of rays for spatial occlusion")]
    [SerializeField] private int spatialRayCount = 5;

    [Tooltip("Radius around listener to consider a ray hit valid")]
    [SerializeField] private float listenerHitRadius = 4f;

    [Header("Debug")]
    [SerializeField] private bool showDebugRays = false;
    [SerializeField] private bool showListenerRadius = false;
    [SerializeField] private bool printOcclusionValues = false;
    #endregion

    #region Private Fields
    private Transform listenerTransform;
    private float spatialConeAngle;
    private float lastDirectionalOcclusion;
    private float lastSpatialOcclusion;
    #endregion

    #region Unity Callbacks
    void Awake()
    {
        CacheListener();
        // Calculate spatial cone angle to ensure even 360° distribution
        spatialConeAngle = 360f - 360f / spatialRayCount;
    }

    void Update()
    {
        // Recache listener if it was null
        if (listenerTransform == null)
            CacheListener();

        #if UNITY_EDITOR
        if (showListenerRadius && listenerTransform != null)
        {
            DrawDebugCircle(listenerTransform.position, listenerHitRadius, 12, Color.cyan);
        }
        
        // Print occlusion values for debugging
        if (printOcclusionValues && listenerTransform != null)
        {
            float dirOcclusion = CalculateDirectionalOcclusionToListener();
            float spatOcclusion = CalculateSpatialOcclusion();
            Debug.Log($"[AudioOcclusion] {gameObject.name} - Directional: {dirOcclusion:F3} | Spatial: {spatOcclusion:F3} | Distance: {GetDistanceToListener():F2}");
        }
        #endif
    }
    #endregion

    #region Public API - Occlusion Calculation
    /// <summary>
    /// Calculate directional occlusion in a specific direction.
    /// Returns 0 (clear) to 1 (fully blocked).
    /// </summary>
    public float CalculateDirectionalOcclusion(Vector2 direction)
    {
        if (listenerTransform == null) return 0f;

        return CalculateOcclusion(transform.position, direction, 
                                  directionalConeAngle, directionalRayCount);
    }

    /// <summary>
    /// Calculate directional occlusion towards the listener.
    /// Returns 0 (clear) to 1 (fully blocked).
    /// </summary>
    public float CalculateDirectionalOcclusionToListener()
    {
        if (listenerTransform == null) return 0f;

        Vector2 toListener = (listenerTransform.position - transform.position).normalized;
        return CalculateDirectionalOcclusion(toListener);
    }

    /// <summary>
    /// Calculate spatial occlusion (360° around source).
    /// Returns 0 (clear) to 1 (fully blocked).
    /// </summary>
    public float CalculateSpatialOcclusion()
    {
        if (listenerTransform == null) return 0f;

        // Use right vector as base direction (doesn't matter for 360° coverage)
        return CalculateOcclusion(transform.position, Vector2.right, 
                                  spatialConeAngle, spatialRayCount);
    }

    /// <summary>
    /// Calculate both occlusion values at once (more efficient).
    /// </summary>
    public void CalculateBothOcclusions(out float directional, out float spatial)
    {
        if (listenerTransform == null)
        {
            directional = 0f;
            spatial = 0f;
            return;
        }

        Vector2 toListener = (listenerTransform.position - transform.position).normalized;
        directional = CalculateOcclusion(transform.position, toListener, 
                                        directionalConeAngle, directionalRayCount);
        spatial = CalculateOcclusion(transform.position, Vector2.right, 
                                    spatialConeAngle, spatialRayCount);
    }
    #endregion

    #region Public API - Play with Occlusion (via AudioManager)
    /// <summary>
    /// Play a one-shot sound with automatic directional and spatial occlusion.
    /// Uses AudioManager for playback.
    /// </summary>
    public EventInstance PlaySound(string eventPath, bool useDirectional = true, bool useSpatial = true)
    {
        if (AudioManager.Instance == null)
        {
            Debug.LogError("[AudioOcclusion] AudioManager not found!");
            return default;
        }

        // Calculate occlusion values
        float dirOcclusion = useDirectional ? CalculateDirectionalOcclusionToListener() : 0f;
        float spatOcclusion = useSpatial ? CalculateSpatialOcclusion() : 0f;

        // Build parameters dictionary
        var parameters = new Dictionary<string, float>
        {
            { "Directional Occlusion", dirOcclusion },
            { "Spatial Occlusion", spatOcclusion }
        };

        return AudioManager.Instance.PlayOneShot(eventPath, gameObject, parameters);
    }

    /// <summary>
    /// Play a sound with directional occlusion in a specific direction.
    /// </summary>
    public EventInstance PlaySoundWithDirection(string eventPath, Vector2 direction, bool useSpatial = true)
    {
        if (AudioManager.Instance == null)
        {
            Debug.LogError("[AudioOcclusion] AudioManager not found!");
            return default;
        }

        float dirOcclusion = CalculateDirectionalOcclusion(direction);
        float spatOcclusion = useSpatial ? CalculateSpatialOcclusion() : 0f;

        var parameters = new Dictionary<string, float>
        {
            { "Directional Occlusion", dirOcclusion },
            { "Spatial Occlusion", spatOcclusion }
        };

        return AudioManager.Instance.PlayOneShot(eventPath, gameObject, parameters);
    }

    /// <summary>
    /// Update occlusion parameters on an existing event instance.
    /// Useful for persistent/looping sounds.
    /// </summary>
    public void UpdateOcclusion(EventInstance instance, bool useDirectional = true, bool useSpatial = true)
    {
        if (!instance.isValid() || AudioManager.Instance == null) return;

        float dirOcclusion = useDirectional ? CalculateDirectionalOcclusionToListener() : 0f;
        float spatOcclusion = useSpatial ? CalculateSpatialOcclusion() : 0f;

        AudioManager.Instance.SetParameter(instance, "Directional Occlusion", dirOcclusion);
        AudioManager.Instance.SetParameter(instance, "Spatial Occlusion", spatOcclusion);
    }

    /// <summary>
    /// Update occlusion on a persistent sound by ID (requires AudioManager).
    /// Call this in Update() for moving sources or listeners.
    /// </summary>
    public void UpdateOcclusion(string persistentId, bool useDirectional = true, bool useSpatial = true)
    {
        if (AudioManager.Instance == null) return;

        EventInstance instance = AudioManager.Instance.GetPersistentInstance(persistentId);
        UpdateOcclusion(instance, useDirectional, useSpatial);
    }
    #endregion

    #region Core Occlusion Logic
    /// <summary>
    /// Unified occlusion calculator using cone-based raycasting with bounces.
    /// Returns 0 (clear) to 1 (fully blocked).
    /// </summary>
    private float CalculateOcclusion(Vector2 source, Vector2 direction, float coneAngle, int rayCount)
    {
        if (rayCount <= 0) return 0f;

        int hits = 0;
        float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        for (int i = 0; i < rayCount; i++)
        {
            float t = rayCount > 1 ? (float)i / (rayCount - 1) : 0.5f;
            float offset = Mathf.Lerp(-coneAngle / 2f, coneAngle / 2f, t);
            float angle = (baseAngle + offset) * Mathf.Deg2Rad;
            Vector2 rayDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            if (CastBouncingRay(source, rayDirection))
                hits++;
        }

        // Return occlusion value: 0 = all rays hit listener, 1 = no rays hit listener
        return 1f - (hits / (float)rayCount);
    }

    /// <summary>
    /// Casts a bouncing ray that reflects off surfaces.
    /// Returns true if any bounce reaches within listenerHitRadius of the listener.
    /// </summary>
    private bool CastBouncingRay(Vector2 origin, Vector2 direction)
    {
        Vector2 currentPosition = origin;
        Vector2 currentDirection = direction.normalized;
        float remainingDistance = maxSoundDistance;
        bool reachedListener = false;

        for (int bounce = 0; bounce <= maxBounces; bounce++)
        {
            RaycastHit2D hit = Physics2D.Raycast(currentPosition, currentDirection, 
                                                  remainingDistance, occlusionLayer);

            if (!hit.collider)
            {
                // No obstacle hit - ray travels to max distance
                Vector2 endPoint = currentPosition + currentDirection * remainingDistance;
                
                if (showDebugRays)
                {
                    Color segmentColor = reachedListener ? Color.green : Color.red;
                    Debug.DrawLine(currentPosition, endPoint, segmentColor, 0f);
                }
                break;
            }

            float hitDistance = Vector2.Distance(currentPosition, hit.point);
            remainingDistance -= hitDistance;

            // Check if this bounce point is within radius of listener
            bool hitListenerThisBounce = Vector2.Distance(hit.point, listenerTransform.position) <= listenerHitRadius;
            
            if (hitListenerThisBounce)
                reachedListener = true;
            
            if (showDebugRays)
            {
                // Color code: Green = reached listener at some point, Yellow = bouncing, Red = never reached
                Color segmentColor;
                if (reachedListener)
                    segmentColor = Color.green;
                else if (bounce > 0)
                    segmentColor = Color.yellow;
                else
                    segmentColor = Color.cyan;
                    
                Debug.DrawLine(currentPosition, hit.point, segmentColor, 0f);
                
                // Draw small cross at bounce point
                Vector2 perpendicular = new Vector2(-currentDirection.y, currentDirection.x) * 0.2f;
                Debug.DrawLine((Vector3)hit.point - (Vector3)perpendicular, 
                              (Vector3)hit.point + (Vector3)perpendicular, 
                              Color.magenta, 0f);
                
                // Draw reflected direction as a small arrow
                if (bounce < maxBounces && remainingDistance > 0f)
                {
                    Vector2 reflectedDir = Vector2.Reflect(currentDirection, hit.normal).normalized;
                    Debug.DrawRay(hit.point, reflectedDir * 0.5f, Color.magenta, 0f);
                }
            }

            if (remainingDistance <= 0f)
                break;

            // Reflect direction and continue from hit point (with small offset to avoid self-intersection)
            currentDirection = Vector2.Reflect(currentDirection, hit.normal).normalized;
            currentPosition = hit.point + currentDirection * 0.01f;
        }

        return reachedListener;
    }
    #endregion

    #region Utility Methods
    /// <summary>Cache or refresh the FMOD listener transform.</summary>
    private void CacheListener()
    {
        var listener = FindFirstObjectByType<StudioListener>();
        if (listener != null)
            listenerTransform = listener.transform;
    }

    /// <summary>Get the current listener transform.</summary>
    public Transform GetListener() => listenerTransform;

    /// <summary>Check if there's a direct line of sight to the listener (no occlusion check).</summary>
    public bool HasLineOfSightToListener()
    {
        if (listenerTransform == null) return false;

        Vector2 toListener = listenerTransform.position - transform.position;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, toListener.normalized, 
                                              toListener.magnitude, occlusionLayer);

        return !hit.collider;
    }

    /// <summary>Get distance to listener.</summary>
    public float GetDistanceToListener()
    {
        if (listenerTransform == null) return float.MaxValue;
        return Vector2.Distance(transform.position, listenerTransform.position);
    }
    #endregion

    #region Debug Helpers
    private void DrawDebugCircle(Vector3 center, float radius, int segments, Color color)
    {
        float angleStep = 360f / segments;
        Vector3 previousPoint = center + Vector3.right * radius;

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Debug.DrawLine(previousPoint, nextPoint, color);
            previousPoint = nextPoint;
        }
    }

    #if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!showDebugRays || listenerTransform == null) return;

        // Draw detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, maxSoundDistance);

        // Draw directional cone
        Vector2 toListener = (listenerTransform.position - transform.position).normalized;
        float baseAngle = Mathf.Atan2(toListener.y, toListener.x) * Mathf.Rad2Deg;

        Gizmos.color = Color.green;
        for (int i = 0; i < directionalRayCount; i++)
        {
            float t = directionalRayCount > 1 ? (float)i / (directionalRayCount - 1) : 0.5f;
            float offset = Mathf.Lerp(-directionalConeAngle / 2f, directionalConeAngle / 2f, t);
            float angle = (baseAngle + offset) * Mathf.Deg2Rad;
            Vector2 rayDir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Gizmos.DrawRay(transform.position, (Vector3)rayDir * 3f);
        }
    }
    #endif
    #endregion
}