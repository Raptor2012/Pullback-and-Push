using UnityEngine;

/// <summary>
/// Two-mode camera controller for the pullback car.
///
/// TOP-DOWN — Idle / Repositioning / Aiming / Pulling
///   Keeps its initial world-space offset from the car.
///   Zooms out (rises) while the player drags / pulls.
///
/// CHASE — Launched
///   Moves behind and above the car, looking along its nose.
///   Transitions quickly but smoothly from wherever the top-down camera was.
///
/// SETUP:
///   Attach to the Camera GameObject (tagged MainCamera).
///   CarTransform is auto-found if left empty.
/// </summary>
public class CameraController : MonoBehaviour
{
    // ── Target ────────────────────────────────────────────────────────────────
    [Header("Target")]
    [Tooltip("Drag CarMain here, or leave empty to auto-find.")]
    [SerializeField] private Transform carTransform;

    // ── Top-Down ──────────────────────────────────────────────────────────────
    [Header("Top-Down Follow")]
    [Tooltip("How quickly the camera follows the car in top-down mode.")]
    [SerializeField] private float topDownFollowSpeed = 8f;

    [Tooltip("Additional height gain at full pull power.")]
    [SerializeField] private float pullZoom = 8f;

    [Tooltip("How quickly the zoom offset changes.")]
    [SerializeField] private float zoomLerpSpeed = 6f;

    // ── Chase (Third-Person) ──────────────────────────────────────────────────
    [Header("Chase Camera (Launched)")]
    [Tooltip("Camera offset in car-local space (Y-rotation only).\n" +
             "(0, 3.5, −9) = behind and above.")]
    [SerializeField] private Vector3 chaseOffset = new Vector3(0f, 3.5f, -9f);

    [Tooltip("World-space look-at offset above the car pivot.")]
    [SerializeField] private Vector3 chaseLookOffset = new Vector3(0f, 1.2f, 0f);

    [Tooltip("Steady-state chase follow speed.")]
    [SerializeField] private float chaseFollowSpeed = 8f;

    // ── Transition ────────────────────────────────────────────────────────────
    [Header("Top-Down → Chase Transition")]
    [Tooltip("Duration (s) of the fast camera swoosh when launched.")]
    [SerializeField] private float transitionTime = 0.5f;

    [Tooltip("Speed multiplier at the start of the transition (relative to chase follow speed).")]
    [SerializeField] private float transitionBoost = 4f;

    // ── Hit Zoom-Out ──────────────────────────────────────────────────────────
    [Header("Hit Zoom-Out (on target hit)")]
    [Tooltip("How far the camera pulls back along its own -Z axis when a target is hit.")]
    [SerializeField] private float hitPullBackDistance = 4f;

    [Tooltip("Duration of the pull-back movement in seconds.")]
    [SerializeField] private float hitZoomOutDuration = 0.35f;

    // ── Internals ─────────────────────────────────────────────────────────────
    private CarController carCtrl;

    // Captured at startup — defines the top-down perspective
    private Vector3    topDownOffset;
    private Quaternion topDownRotation;

    // Runtime
    private float currentZoomY;
    private float launchTimestamp = -999f;
    private bool  wasLaunched;
    private bool  frozen;          // when true the camera holds its current transform

    // ═══════════════════════════════════════════════════════════════════════════
    #region Public API

    /// <summary>Lock the camera at its current position and rotation (e.g. on target hit).</summary>
    public void Freeze() => frozen = true;

    /// <summary>
    /// Smoothly pulls the camera back, then locks it — keeps the whole score indicator in frame.
    /// </summary>
    public void FreezeWithZoomOut()
    {
        StartCoroutine(ZoomOutThenFreeze());
    }

    private System.Collections.IEnumerator ZoomOutThenFreeze()
    {
        Vector3 startPos = transform.position;
        // Pull back along the camera's own backward axis so the framing improves
        Vector3 endPos   = startPos - transform.forward * hitPullBackDistance;
        float elapsed = 0f;
        while (elapsed < hitZoomOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / hitZoomOutDuration);
            transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
        transform.position = endPos;
        frozen = true;
    }

    /// <summary>Resume normal camera behaviour.</summary>
    public void Unfreeze() => frozen = false;

    /// <summary>True while the camera is frozen (read by CameraShake).</summary>
    public bool IsFrozen => frozen;

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Unity Lifecycle

    private void Awake()
    {
        // Auto-find the car if not assigned
        if (carTransform == null)
        {
            carCtrl = FindFirstObjectByType<CarController>();
            if (carCtrl != null) carTransform = carCtrl.transform;
        }
        else
        {
            carCtrl = carTransform.GetComponent<CarController>();
        }

        if (carTransform == null || carCtrl == null)
        {
            Debug.LogError("[CameraController] CarController not found in scene!", this);
            return;
        }

        // Lock in the top-down reference frame from wherever the camera starts
        topDownOffset   = transform.position - carTransform.position;
        topDownRotation = transform.rotation;
    }

    private void LateUpdate()
    {
        if (carCtrl == null) return;
        if (frozen) return;   // camera is locked — do nothing

        var phase = carCtrl.CurrentPhase;

        // Detect the exact frame the car enters Launched
        if (phase == CarController.Phase.Launched && !wasLaunched)
        {
            launchTimestamp = Time.time;
            wasLaunched     = true;
        }
        else if (phase != CarController.Phase.Launched)
        {
            wasLaunched = false;
        }

        switch (phase)
        {
            case CarController.Phase.Idle:
                ApplyTopDown(0f);
                break;

            case CarController.Phase.Pulling:
                ApplyTopDown(carCtrl.PullFraction * pullZoom);
                break;

            case CarController.Phase.Launched:
                ApplyChaseCamera();
                break;

            case CarController.Phase.Aiming:
                // Keep following the car but use unscaled time so slow-mo doesn't freeze the cam
                ApplyChaseCamera(useUnscaledTime: true);
                break;

            case CarController.Phase.Thrown:
                ApplyRagdollChase();
                break;
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Camera Modes

    private void ApplyTopDown(float targetZoomHeight)
    {
        // Smoothly adjust zoom
        currentZoomY = Mathf.Lerp(currentZoomY, targetZoomHeight, zoomLerpSpeed * Time.deltaTime);

        Vector3 desiredPos = carTransform.position + topDownOffset + Vector3.up * currentZoomY;

        transform.position = Vector3.Lerp(transform.position, desiredPos,
                                          topDownFollowSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, topDownRotation,
                                              topDownFollowSpeed * Time.deltaTime);
    }

    private void ApplyChaseCamera(bool useUnscaledTime = false)
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        // Desired position: behind & above the car, using car's yaw only
        Quaternion yRot       = Quaternion.Euler(0f, carTransform.eulerAngles.y, 0f);
        Vector3    desiredPos = carTransform.position + yRot * chaseOffset;
        Vector3    lookTarget = carTransform.position + chaseLookOffset;

        // Fast-then-settle speed curve
        float elapsed = (useUnscaledTime ? Time.unscaledTime : Time.time) - launchTimestamp;
        float blend   = Mathf.Clamp01(elapsed / transitionTime);
        float speed   = Mathf.Lerp(chaseFollowSpeed * transitionBoost,
                                   chaseFollowSpeed, blend);

        // Frame-rate independent exponential interpolation
        float t = 1f - Mathf.Exp(-speed * dt);

        transform.position = Vector3.Lerp(transform.position, desiredPos, t);

        Vector3 lookDir = lookTarget - transform.position;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRot = Quaternion.LookRotation(lookDir, Vector3.up);
            transform.rotation    = Quaternion.Slerp(transform.rotation, desiredRot, t);
        }

        // Decay zoom offset so returning to top-down later starts from zero
        currentZoomY = Mathf.Lerp(currentZoomY, 0f, t);
    }

    /// <summary>Chase the thrown ragdoll from behind its velocity direction.</summary>
    private void ApplyRagdollChase()
    {
        Transform ragdoll = carCtrl.ThrownRagdoll;
        if (ragdoll == null) return;

        Vector3 ragPos = ragdoll.position;

        // Follow from behind the ragdoll's velocity direction
        Rigidbody ragRb = ragdoll.GetComponent<Rigidbody>();
        Vector3 flatVel = Vector3.forward;
        if (ragRb != null && ragRb.linearVelocity.sqrMagnitude > 1f)
        {
            flatVel = ragRb.linearVelocity;
            flatVel.y = 0f;
            if (flatVel.sqrMagnitude > 0.01f) flatVel.Normalize();
            else flatVel = Vector3.forward;
        }

        Vector3 desiredPos = ragPos - flatVel * Mathf.Abs(chaseOffset.z)
                                    + Vector3.up * chaseOffset.y;
        Vector3 lookTarget = ragPos + chaseLookOffset;

        float t = 1f - Mathf.Exp(-chaseFollowSpeed * Time.deltaTime);

        transform.position = Vector3.Lerp(transform.position, desiredPos, t);

        Vector3 lookDir = lookTarget - transform.position;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRot = Quaternion.LookRotation(lookDir, Vector3.up);
            transform.rotation    = Quaternion.Slerp(transform.rotation, desiredRot, t);
        }
    }

    #endregion
}
