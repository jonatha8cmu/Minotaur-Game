using UnityEngine;

/// <summary>
/// Perceptual occlusion (Directional + Spatial) with three probes.
/// Final simplified version: minimal debug, fractional directional optional.
/// </summary>
public class OcclusionComponent : MonoBehaviour
{
    [Header("Probe Settings")] [SerializeField] private LayerMask occlusionMask;
    [SerializeField] private float lateralOffset = 0.75f;
    [SerializeField] private float maxDistance = 40f;
    [SerializeField] private float recalcInterval = 0.12f;
    [SerializeField] private float movementSqrThreshold = 0.05f;

    [Header("Spatial Weights")] [SerializeField] private float spatialBlocked1 = 0.35f;
    [SerializeField] private float spatialBlocked2 = 0.65f;
    [SerializeField] private float spatialBlocked3 = 1.0f;

    [Header("Directional Fraction")] [SerializeField] private bool useFractionalDirectional = true;
    [SerializeField] private float minBlockedDirectional = 0.15f;

    [Header("Debug (optional)")] [SerializeField] private bool debug; // single toggle
    [SerializeField] private Color directClearColor = Color.green;
    [SerializeField] private Color directBlockedColor = Color.red;
    [SerializeField] private Color lateralClearColor = Color.cyan;
    [SerializeField] private Color lateralBlockedColor = new(1f,0.5f,0f);

    private Transform listener;
    private OcclusionResult cached;
    private double nextAllowedTime;
    private Vector3 lastSourcePos;
    private Vector3 lastListenerPos;

    public struct OcclusionResult { public float Directional; public float Spatial; public float Distance; public double Timestamp; }

    void Awake()
    {
        listener = AudioManager.Instance?.GetListener();
        lastSourcePos = transform.position;
        lastListenerPos = listener ? listener.position : Vector3.zero;
        cached = new OcclusionResult { Directional = 0, Spatial = 0, Distance = float.MaxValue, Timestamp = AudioSettings.dspTime };
    }

    void OnEnable() => ForceRecalculate();

    void Update()
    {
        if (listener == null) listener = AudioManager.Instance?.GetListener();
        if (listener == null) return;
        bool moved = (transform.position - lastSourcePos).sqrMagnitude > movementSqrThreshold || (listener.position - lastListenerPos).sqrMagnitude > movementSqrThreshold;
        if (moved)
        {
            ForceRecalculate();
            lastSourcePos = transform.position;
            lastListenerPos = listener.position;
        }
        else if (debug)
        {
            DrawCached();
        }
    }

    public OcclusionResult GetCurrentOcclusion(bool force = false)
    {
        if (listener == null) listener = AudioManager.Instance?.GetListener();
        if (listener == null) return cached;
        float dist = Vector2.Distance(transform.position, listener.position);
        if (dist > maxDistance)
        {
            cached = new OcclusionResult { Directional = 0f, Spatial = 0f, Distance = dist, Timestamp = AudioSettings.dspTime };
            return cached;
        }
        if (!force && AudioSettings.dspTime < nextAllowedTime) return cached;
        cached = Compute(dist);
        nextAllowedTime = AudioSettings.dspTime + recalcInterval;
        return cached;
    }

    public void ForceRecalculate()
    {
        if (listener == null) listener = AudioManager.Instance?.GetListener();
        if (listener == null) return;
        float dist = Vector2.Distance(transform.position, listener.position);
        cached = Compute(dist);
        nextAllowedTime = AudioSettings.dspTime + recalcInterval;
    }

    private OcclusionResult Compute(float dist)
    {
        Vector2 src = transform.position;
        Vector2 dst = listener.position;
        Vector2 dir = (dst - src).normalized;
        Vector2 perp = new(-dir.y, dir.x);
        if (perp.sqrMagnitude < 0.0001f) perp = Vector2.right;

        var directHit = Physics2D.Raycast(src, dir, dist, occlusionMask);
        float directional = 0f;
        if (directHit.collider != null)
        {
            float hitFrac = Mathf.Clamp01(directHit.distance / dist);
            directional = useFractionalDirectional ? Mathf.Lerp(minBlockedDirectional, 1f, 1f - hitFrac) : 1f;
        }

        Vector2 leftOrigin = src + perp * lateralOffset;
        Vector2 rightOrigin = src - perp * lateralOffset;
        float leftDist = Vector2.Distance(leftOrigin, dst);
        float rightDist = Vector2.Distance(rightOrigin, dst);
        var leftHit = Physics2D.Raycast(leftOrigin, (dst - leftOrigin).normalized, leftDist, occlusionMask);
        var rightHit = Physics2D.Raycast(rightOrigin, (dst - rightOrigin).normalized, rightDist, occlusionMask);

        int blockedCount = (directHit.collider != null ? 1 : 0) + (leftHit.collider != null ? 1 : 0) + (rightHit.collider != null ? 1 : 0);
        float spatial = blockedCount switch { 0 => 0f, 1 => spatialBlocked1, 2 => spatialBlocked2, 3 => spatialBlocked3, _ => 0f };

        if (debug) DrawDebug(src, dst, directHit, leftOrigin, leftHit, rightOrigin, rightHit);

        return new OcclusionResult { Directional = directional, Spatial = spatial, Distance = dist, Timestamp = AudioSettings.dspTime };
    }

    private void DrawDebug(Vector2 src, Vector2 dst, RaycastHit2D directHit, Vector2 leftOrigin, RaycastHit2D leftHit, Vector2 rightOrigin, RaycastHit2D rightHit)
    {
#if UNITY_EDITOR
        if (directHit.collider != null)
        {
            Debug.DrawLine(src, directHit.point, directBlockedColor);
            Debug.DrawLine(directHit.point, dst, directClearColor * 0.3f);
        }
        else Debug.DrawLine(src, dst, directClearColor);

        if (leftHit.collider != null)
        {
            Debug.DrawLine(leftOrigin, leftHit.point, lateralBlockedColor);
            Debug.DrawLine(leftHit.point, dst, lateralClearColor * 0.3f);
        }
        else Debug.DrawLine(leftOrigin, dst, lateralClearColor);

        if (rightHit.collider != null)
        {
            Debug.DrawLine(rightOrigin, rightHit.point, lateralBlockedColor);
            Debug.DrawLine(rightHit.point, dst, lateralClearColor * 0.3f);
        }
        else Debug.DrawLine(rightOrigin, dst, lateralClearColor);
#endif
    }

    private void DrawCached()
    {
#if UNITY_EDITOR
        if (listener == null) return;
        Vector2 src = transform.position;
        Vector2 dst = listener.position;
        Vector2 dir = (dst - src).normalized;
        Vector2 perp = new(-dir.y, dir.x); if (perp.sqrMagnitude < 0.0001f) perp = Vector2.right;
        Vector2 leftOrigin = src + perp * lateralOffset;
        Vector2 rightOrigin = src - perp * lateralOffset;
        bool directClear = cached.Directional < minBlockedDirectional;
        Debug.DrawLine(src, dst, directClear ? directClearColor : directBlockedColor);
        Debug.DrawLine(leftOrigin, dst, lateralClearColor);
        Debug.DrawLine(rightOrigin, dst, lateralClearColor);
#endif
    }
}
