using UnityEngine;

/// <summary>
/// Attach to the Camera GameObject alongside CameraController.
/// Call <see cref="Shake"/> from anywhere to trigger a shake.
///
/// Uses Perlin-noise displacement with exponential trauma decay so
/// multiple overlapping shakes feel additive rather than stacking rigidly.
///
/// Execution order is set high so this always runs AFTER CameraController.LateUpdate.
/// </summary>
[DefaultExecutionOrder(100)]
public class CameraShake : MonoBehaviour
{
    // ── Shake parameters ───────────────────────────────────────────────────────
    [Header("Shake Settings")]
    [Tooltip("How fast the trauma decays per second (higher = shorter shake).")]
    [SerializeField] private float traumaDecay   = 1.5f;

    [Tooltip("Maximum angular displacement (degrees) at full trauma.")]
    [SerializeField] private float maxAngle      = 3f;

    [Tooltip("Maximum positional offset (world units) at full trauma.")]
    [SerializeField] private float maxOffset     = 0.15f;

    [Tooltip("Perlin noise frequency — higher = more frantic shake.")]
    [SerializeField] private float frequency     = 25f;

    [Tooltip("When true, shake is suppressed while CameraController is frozen (target-hit score view).")]
    [SerializeField] private bool suppressWhenCameraFrozen = true;

    // ── Runtime ────────────────────────────────────────────────────────────────
    private float   trauma;        // 0–1; shake magnitude = trauma²
    private float   traumaTimer;   // used as the Perlin seed / time axis
    private float   traumaDurationTimer;
    private CameraController cameraController;

    // Perlin seed offsets for independent XYZ axes
    private static readonly float SeedX = 0f;
    private static readonly float SeedY = 31.41f;
    private static readonly float SeedZ = 64.26f;
    private static readonly float SeedRoll  = 97.11f;
    private static readonly float SeedPitch = 128.55f;
    private static readonly float SeedYaw   = 159.73f;

    // ═══════════════════════════════════════════════════════════════════════════
    #region Public API

    /// <summary>
    /// Trigger a camera shake.
    /// Calls are additive — if the new intensity is higher, it overrides; otherwise
    /// the shake extends its remaining time to whichever is longer.
    /// </summary>
    /// <param name="intensity">0–1 trauma magnitude.</param>
    /// <param name="duration">Real-time seconds the shake lasts.</param>
    public void Shake(float intensity, float duration)
    {
        // Take the greater intensity so multiple shakes don't cancel each other
        trauma = Mathf.Max(trauma, Mathf.Clamp01(intensity));

        // Extend duration if this is a longer shake
        traumaDurationTimer = Mathf.Max(traumaDurationTimer, duration);
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Unity Lifecycle

    private void Awake()
    {
        cameraController = GetComponent<CameraController>();
        if (cameraController == null)
            cameraController = FindFirstObjectByType<CameraController>();
    }

    private void LateUpdate()
    {
        if (trauma <= 0.001f) return;

        if (suppressWhenCameraFrozen && cameraController != null && cameraController.IsFrozen)
        {
            // Keep frozen score view stable and clear any queued shake.
            trauma = 0f;
            traumaDurationTimer = 0f;
            return;
        }

        // Use unscaledDeltaTime so shake works correctly during slow-mo aiming
        float dt = Time.unscaledDeltaTime;

        // Advance Perlin time axis
        traumaTimer += dt * frequency;

        // Trauma decays over time — but we hold peak trauma for the duration window
        traumaDurationTimer -= dt;
        if (traumaDurationTimer <= 0f)
        {
            trauma = Mathf.Max(0f, trauma - traumaDecay * dt);
        }

        // Shake magnitude is trauma² for a more physical feel (soft start, hard peak)
        float shake = trauma * trauma;

        // Sample Perlin noise for each axis (range −1 to +1 via * 2 - 1)
        float noiseX     = (Mathf.PerlinNoise(SeedX,     traumaTimer) * 2f - 1f) * maxOffset * shake;
        float noiseY     = (Mathf.PerlinNoise(SeedY,     traumaTimer) * 2f - 1f) * maxOffset * shake;
        float noiseZ     = (Mathf.PerlinNoise(SeedZ,     traumaTimer) * 2f - 1f) * maxOffset * shake;
        float noisePitch = (Mathf.PerlinNoise(SeedPitch, traumaTimer) * 2f - 1f) * maxAngle  * shake;
        float noiseYaw   = (Mathf.PerlinNoise(SeedYaw,   traumaTimer) * 2f - 1f) * maxAngle  * shake;
        float noiseRoll  = (Mathf.PerlinNoise(SeedRoll,  traumaTimer) * 2f - 1f) * maxAngle  * shake * 0.5f;

        // Offset is applied ON TOP of whatever CameraController already set this frame
        transform.position += new Vector3(noiseX, noiseY, noiseZ);
        transform.rotation *= Quaternion.Euler(noisePitch, noiseYaw, noiseRoll);
    }

    #endregion
}
