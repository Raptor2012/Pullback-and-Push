using UnityEngine;

/// <summary>
/// Attached at runtime to the spawned ragdoll root.
/// Steers the hips rigidbody left / right while the ragdoll is in the air,
/// applying a gentle lateral force scaled by <see cref="controlInfluence"/>.
/// </summary>
public class RagdollController : MonoBehaviour
{
    // ── Settings (set by CarController.FinishAiming before use) ──────────────
    /// <summary>0 = no player influence, 1 = full control force applied.</summary>
    [Range(0f, 1f)]
    public float controlInfluence = 0.30f;

    /// <summary>Max lateral force (world units/s²) at controlInfluence = 1.</summary>
    public float controlForce = 28f;

    // ── Internals ─────────────────────────────────────────────────────────────
    private Rigidbody hipsBody;
    private int       steerInput; // −1, 0, +1

    // ═══════════════════════════════════════════════════════════════════════════
    #region Public API

    /// <summary>Call once after the ragdoll is spawned to register the hips body.</summary>
    public void Init(Rigidbody hips, float influence, float force)
    {
        hipsBody         = hips;
        controlInfluence = Mathf.Clamp01(influence);
        controlForce     = force;
    }

    /// <summary>-1 = left, 0 = none, +1 = right. Call from CarController.ProcessPointer.</summary>
    public void SetSteer(int dir) => steerInput = dir;

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Physics

    private void FixedUpdate()
    {
        if (hipsBody == null || steerInput == 0 || controlInfluence <= 0f) return;

        // Determine the ragdoll's current horizontal travel direction so "right" is relative.
        Vector3 vel  = hipsBody.linearVelocity;
        Vector3 flat = new Vector3(vel.x, 0f, vel.z);
        Vector3 fwd  = flat.sqrMagnitude > 0.25f ? flat.normalized : transform.forward;

        // Perpendicular right in world-space (ignore vertical)
        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

        float scaled = controlForce * controlInfluence * steerInput;
        hipsBody.AddForce(right * scaled, ForceMode.Acceleration);
    }

    #endregion
}
