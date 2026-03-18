using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Pullback toy car controller — faithful to real pullback car mechanics.
///
/// FLOW:
///   Idle     → tap the car, drag backward to charge → Pulling
///   Pulling  → release                              → Launched
///   Launched → steer left/right halves of screen    → (stops) → Idle
///
/// HOW IT WORKS:
///   • Tap the car and immediately drag backward to wind up.
///   • The car slides backward along its own axis only — no rotation.
///   • Release to shoot forward with force proportional to pull distance.
///   • While launched, tap-and-hold left/right screen halves to steer.
///
/// SETUP:
///   • Rigidbody on this GameObject (constraints are managed by code).
///   • At least one Collider in the hierarchy (child MeshColliders are fine).
///   • CameraController on the camera.
/// </summary>
public class CarController : MonoBehaviour
{
    // ── Phases ────────────────────────────────────────────────────────────────
    public enum Phase { Idle, Pulling, Launched, Aiming, Thrown }

    /// Fired on every phase change. Args: (previousPhase, newPhase).
    public event System.Action<Phase, Phase> OnPhaseChanged;

    // ── FTUE Tutorial Signals ─────────────────────────────────────────────────
    /// Fired every Update frame during pulling with the current pull fraction (0–1).
    public event System.Action<float> OnPullFractionUpdated;

    /// Fired when the car steer input direction changes (-1, 0, +1) while Launched.
    public event System.Action<int> OnSteerInputChanged;

    /// Fired when the ragdoll steer input changes (-1, 0, +1) while Thrown.
    public event System.Action<int> OnRagdollSteerChanged;

    /// Fired the first moment the car leaves the ground (grounded → airborne).
    public event System.Action OnBecameAirborne;

    /// Fired at the very end of a full car reset.
    public event System.Action OnCarReset;

    // ── Pullback ──────────────────────────────────────────────────────────────
    [Header("Pullback")]
    [Tooltip("Maximum backward drag distance that gives full launch power (world units).")]
    [SerializeField] private float maxPullDistance = 5f;

    [Tooltip("Launch impulse magnitude at full pull.")]
    [SerializeField] private float maxLaunchForce = 55f;

    // ── Steering ──────────────────────────────────────────────────────────────
    [Header("Steering (During Launch)")]
    [Tooltip("Degrees per second the car can steer at full speed.")]
    [SerializeField] private float steerSpeed = 120f;

    [Tooltip("1 = perfect grip (car follows forward), 0 = pure ice drift.")]
    [Range(0f, 1f)]
    [SerializeField] private float lateralGrip = 0.85f;

    [Tooltip("Speed below which the car is considered stopped (triggers Idle).")]
    [SerializeField] private float stopSpeed = 0.35f;

    // ── Physics ───────────────────────────────────────────────────────────────
    [Header("Physics")]
    [SerializeField] private float launchLinearDrag    = 0.8f;
    [SerializeField] private float airborneLinearDrag  = 0.05f;
    [SerializeField] private float launchAngularDrag   = 2f;
    [SerializeField] private float airborneDebounceTime = 0.1f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayers = ~0;

    // ── Pull Indicator ────────────────────────────────────────────────────────
    [Header("Pull Indicator")]
    [Tooltip("Leave null to auto-create at runtime.")]
    [SerializeField] private LineRenderer pullLine;

    [Tooltip("Material for the pull indicator line. If null, Sprites/Default is used at runtime.")]
    [SerializeField] private Material pullLineMaterial;

    [SerializeField] private Color indicatorColorMin = Color.green;
    [SerializeField] private Color indicatorColorMax = Color.red;
    [SerializeField] private float indicatorWidthMin = 0.05f;
    [SerializeField] private float indicatorWidthMax = 0.25f;

    // ── Ragdoll Throw ─────────────────────────────────────────────────────────
    [Header("Ragdoll Throw")]
    [Tooltip("Ragdoll character prefab to spawn and throw.")]
    [SerializeField] private GameObject ragdollPrefab;

    [Tooltip("Time scale during aiming (slow-motion).")]
    [SerializeField] private float aimTimeScale = 0.15f;

    [Tooltip("Min throw impulse.")]
    [SerializeField] private float minThrowForce = 8f;

    [Tooltip("Max throw impulse.")]
    [SerializeField] private float maxThrowForce = 45f;

    [Tooltip("Spawn offset above the car pivot.")]
    [SerializeField] private Vector3 ragdollSpawnOffset = new Vector3(0f, 1.5f, 0f);

    [Tooltip("How much of the player steer input is transferred to the ragdoll (0 = none, 1 = full).")]
    [Range(0f, 1f)]
    [SerializeField] private float ragdollControlInfluence = 0.30f;

    [Tooltip("Peak lateral acceleration applied to the ragdoll hips at full control influence.")]
    [SerializeField] private float ragdollControlForce = 28f;

    // ── Public state (read by CameraController etc.) ──────────────────────────
    public Phase CurrentPhase { get; private set; } = Phase.Idle;
    /// Normalised pull power 0–1 (used by camera to zoom out).
    public float PullFraction { get; private set; }

    /// The thrown ragdoll's main transform (read by CameraController to follow it).
    public Transform ThrownRagdoll { get; private set; }

    /// True while the car is in the air (read by CarVFXController / CarSFXController).
    public bool IsAirborne => isAirborne;

    /// True if the car was grounded this physics frame.
    public bool IsGrounded { get; private set; }

    /// Last ground check surface normal (read by CarVFXController for spark angle check).
    public Vector3 GroundNormal { get; private set; } = Vector3.up;

    /// Current speed in world units per second.
    public float Speed => rb != null ? rb.linearVelocity.magnitude : 0f;

    /// Current world-space velocity (read by CarVFXController for lateral slip).
    public Vector3 Velocity => rb != null ? rb.linearVelocity : Vector3.zero;

    /// Forward direction locked at pull start (used for launch VFX orientation).
    public Vector3 LaunchDirection => launchDirection;

    // ── Internals ─────────────────────────────────────────────────────────────
    private Rigidbody rb;
    private Camera    cam;
    private float     groundY;
    private CameraController cameraCtrl;

    // Pulling
    private Vector3 aimOrigin;        // car world-pos when pull began
    private Vector3 dragStartWorld;   // finger world-pos when pull began
    private Vector3 launchDirection;  // car's forward at pull start (locked, horizontal)
    private float   pullDistance;     // how far backward the car has slid

    // Steering
    private int   steerInput; // −1 left, 0 none, +1 right
    private float currentYaw; // cached yaw — avoids reading unstable Euler angles each frame

    // Prevents the stop-check from firing immediately after launch
    private float launchGraceTimer;

    // Suppresses upward velocity from collider depenetration right after launch (mobile fix)
    private float launchGroundClampTimer;

    // Airborne debounce — prevents single-frame grounded flickers at ramp lips
    private float airborneMinTimer;

    // Debug reset
    private Vector3    startPosition;
    private Quaternion startRotation;

    // Scene-object caches (populated in Awake to avoid per-reset FindObjectsByType)
    private Target[] cachedTargets;

    // Aiming / Throw
    private ThrowAimUI        throwAimUI;
    private bool              isAirborne;
    private Vector2           prevPointerPos;
    private bool              pointerWasDown;
    private GameObject        spawnedRagdoll;
    private float             savedFixedDelta;
    private RagdollController ragdollCtrl;

    // Pointer tracking (mobile-safe + UI-safe)
    private int               activeTouchFingerId = -1;
    private bool              mousePointerActive;

    // ═══════════════════════════════════════════════════════════════════════════
    #region Unity Lifecycle

    private void Awake()
    {
        rb  = GetComponent<Rigidbody>();
        cam = Camera.main ?? FindFirstObjectByType<Camera>();
        if (cam == null) Debug.LogError("[CarController] No camera found!", this);

        groundY = transform.position.y;

        startPosition = transform.position;
        startRotation = transform.rotation;

        // Gravity is irrelevant for a flat-surface arcade car; manage everything in code.
        rb.useGravity = false;

        SetRigidbodyKinematic();
        InitIndicator();

        // ThrowAimUI lives on the same GameObject
        throwAimUI = GetComponent<ThrowAimUI>();
        if (throwAimUI == null)
            throwAimUI = gameObject.AddComponent<ThrowAimUI>();

        cameraCtrl = FindFirstObjectByType<CameraController>();
        cachedTargets = FindObjectsByType<Target>(FindObjectsSortMode.None);
    }

    private void Update()
    {
        ProcessPointer();
        RefreshIndicator();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(KeyCode.R)) ResetCar();
#endif
    }

    private void FixedUpdate()
    {
    // Aiming: car is dynamic; Time.timeScale slows its physics naturally. Thrown: frozen.
        if (CurrentPhase == Phase.Aiming || CurrentPhase == Phase.Thrown) return;
        if (CurrentPhase != Phase.Launched) return;

        // Tick down the grace period before the stop check is allowed to fire
        launchGraceTimer -= Time.fixedDeltaTime;

        // ── Suppress upward depenetration pop right after launch (mobile fix) ──
        // When the car transitions from kinematic→dynamic, any collider overlap
        // with the ground causes the solver to push the car up violently.  On
        // mobile the lower solver-iteration count makes this much worse.
        if (launchGroundClampTimer > 0f)
        {
            launchGroundClampTimer -= Time.fixedDeltaTime;
            Vector3 v = rb.linearVelocity;
            if (v.y > 0f)
                rb.linearVelocity = new Vector3(v.x, 0f, v.z);
        }

        bool grounded = CheckGrounded(out Vector3 surfaceNormal);

        // ── Airborne debounce: prevent re-grounding too quickly after leaving the ground ──
        if (!grounded)
        {
            if (!isAirborne) // just became airborne this frame
                airborneMinTimer = airborneDebounceTime;
            else
                airborneMinTimer -= Time.fixedDeltaTime;
        }
        else if (isAirborne && airborneMinTimer > 0f)
        {
            // Still within debounce window — treat as still airborne
            grounded = false;
            airborneMinTimer -= Time.fixedDeltaTime;
        }

        bool wasAirborne = isAirborne;
        isAirborne       = !grounded;
        if (isAirborne && !wasAirborne)
            OnBecameAirborne?.Invoke();

        IsGrounded   = grounded;
        GroundNormal = surfaceNormal;

        if (grounded)
        {
            rb.linearDamping = launchLinearDrag;

            // ── Steering ──
            if (steerInput != 0)
            {
                float speed  = rb.linearVelocity.magnitude;
                float factor = Mathf.Clamp01(speed / 5f);
                currentYaw  += steerInput * steerSpeed * factor * Time.fixedDeltaTime;
            }

            // ── Align to slope — project steering forward onto the surface, then Slerp ──
            // This avoids fighting the ramp's contact force each tick (the cause of slope jitter).
            Vector3 steerFwd    = Quaternion.Euler(0f, currentYaw, 0f) * Vector3.forward;
            Vector3 fwdOnSlope  = Vector3.ProjectOnPlane(steerFwd, surfaceNormal).normalized;
            Quaternion targetRot = Quaternion.LookRotation(fwdOnSlope, surfaceNormal);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, 18f * Time.fixedDeltaTime));

            // ── Lateral friction via impulse ──
            Vector3 vel    = rb.linearVelocity;
            Vector3 fwdH   = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            Vector3 latVel = vel - Vector3.Dot(vel, fwdH) * fwdH;
            latVel.y = 0f;
            if (latVel.sqrMagnitude > 0.0001f)
                rb.AddForce(-latVel * lateralGrip, ForceMode.VelocityChange);

            // ── Stop check (guarded by grace period) ──
            if (launchGraceTimer <= 0f && rb.linearVelocity.sqrMagnitude < stopSpeed * stopSpeed)
                SetPhase(Phase.Idle);
        }
        else
        {
            rb.linearDamping = airborneLinearDrag;

            // ── Airborne — light steering only, free pitch/roll ──
            if (steerInput != 0)
            {
                currentYaw += steerInput * steerSpeed * 0.3f * Time.fixedDeltaTime;
                rb.MoveRotation(Quaternion.Euler(
                    rb.rotation.eulerAngles.x, currentYaw, rb.rotation.eulerAngles.z));
            }
        }
    }

    /// <summary>SphereCast downward to detect the ground and return its surface normal.</summary>
    private bool CheckGrounded(out Vector3 surfaceNormal)
    {
        // Small sphere is more stable than a thin ray at slope edges — avoids rapid grounded toggling
        if (Physics.SphereCast(
                transform.position + Vector3.up * 0.25f,
                0.08f,
                Vector3.down,
                out RaycastHit hit,
                0.35f,          // slightly longer reach for mobile tolerance
                groundLayers,
                QueryTriggerInteraction.Ignore))
        {
            surfaceNormal = hit.normal;
            return true;
        }
        surfaceNormal = Vector3.up;
        return false;
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Debug

    /// <summary>Public wrapper for UI Button OnClick events.</summary>
    public void ResetFromUIButton()
    {
        ResetCar();
    }

    /// <summary>Instantly resets the car to its starting position and wipes all pull state.</summary>
    private void ResetCar()
    {
        // Force-exit whatever phase we're in without running normal cleanup side-effects
        if (CurrentPhase == Phase.Launched || CurrentPhase == Phase.Aiming)
        {
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Clean up ragdoll throw state
        if (spawnedRagdoll != null)
        {
            Destroy(spawnedRagdoll);
            spawnedRagdoll = null;
            ThrownRagdoll  = null;
        }
        ragdollCtrl  = null;
        throwAimUI.Deactivate();
        Time.timeScale      = 1f;
        Time.fixedDeltaTime = 0.02f;
        isAirborne          = false;
        pointerWasDown      = false;
        activeTouchFingerId = -1;
        mousePointerActive  = false;

        steerInput             = 0;
        pullDistance           = 0f;
        PullFraction           = 0f;
        launchGraceTimer       = 0f;
        launchGroundClampTimer = 0f;
        CurrentPhase     = Phase.Idle;      // bypass SetPhase to avoid event spam

        SetRigidbodyKinematic();

        transform.SetPositionAndRotation(startPosition, startRotation);
        currentYaw = startRotation.eulerAngles.y;
        groundY    = startPosition.y;

        pullLine.enabled = false;

        // Notify listeners (e.g. camera) that we're back to Idle
        OnPhaseChanged?.Invoke(Phase.Launched, Phase.Idle);

        // Unfreeze camera and reset all targets
        cameraCtrl?.Unfreeze();
        foreach (var target in cachedTargets)
            target.ResetTarget();

        OnCarReset?.Invoke();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[CarController] Reset to start.");
#endif
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Phase Transitions

    private void SetPhase(Phase next)
    {
        Phase prev = CurrentPhase;
        if (prev == next) return;

        // — Exit old phase —
        switch (prev)
        {
            case Phase.Pulling:
                pullLine.enabled = false;
                break;
            case Phase.Launched:
                // Don't zero velocity when entering Aiming — the car must keep its arc.
                if (next != Phase.Aiming)
                {
                    rb.linearVelocity  = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                steerInput = 0;
                break;
            case Phase.Aiming:
                throwAimUI.Deactivate();
                break;
        }

        CurrentPhase = next;

        // — Enter new phase —
        switch (next)
        {
            case Phase.Idle:
                SetRigidbodyKinematic();
                // Snap upright so the car looks correct after a ramp tumble
                transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                PullFraction = 0f;
                break;

            case Phase.Pulling:
                pullLine.enabled = true;
                PullFraction = 0f;
                pullDistance  = 0f;
                break;

            case Phase.Launched:
                launchGraceTimer       = 0.25f;  // ignore stop check for the first 0.25 s
                launchGroundClampTimer = 0.15f;  // suppress upward depenetration pop
                // Sync yaw from current rotation so steering starts from the right angle
                currentYaw = transform.eulerAngles.y;
                SetRigidbodyDynamic();
                rb.linearVelocity  = Vector3.zero;   // clean slate before impulse
                rb.angularVelocity = Vector3.zero;
                break;

            case Phase.Aiming:
                throwAimUI.Activate(transform);
                steerInput = 0;
                break;

            case Phase.Thrown:
                // Freeze the car — it's finished
                rb.linearVelocity  = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                SetRigidbodyKinematic();
                break;
        }

        OnPhaseChanged?.Invoke(prev, next);
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Input Processing

    private void ProcessPointer()
    {
        GetPointer(out bool down, out bool held, out bool up, out Vector2 screenPos);

        switch (CurrentPhase)
        {
            case Phase.Idle:
                if (down && IsPointerOnCar(screenPos))
                    BeginPulling(screenPos);
                break;

            case Phase.Pulling:
                if (held)       ContinuePulling(screenPos);
                else if (up)    FinishPull();
                break;

            case Phase.Launched:
                if (down && isAirborne && IsPointerOnCar(screenPos))
                {
                    BeginAiming(screenPos);
                }
                else if (down || held)
                {
                    int newSteer = (screenPos.x < Screen.width * 0.5f) ? -1 : 1;
                    if (newSteer != steerInput)
                    {
                        steerInput = newSteer;
                        OnSteerInputChanged?.Invoke(steerInput);
                    }
                }
                else if (up)
                {
                    if (steerInput != 0)
                    {
                        steerInput = 0;
                        OnSteerInputChanged?.Invoke(0);
                    }
                }
                break;

            case Phase.Aiming:
                if (held)
                {
                    Vector2 delta = pointerWasDown ? (screenPos - prevPointerPos) : Vector2.zero;
                    throwAimUI.Tick(delta);
                }
                else if (up)
                {
                    FinishAiming();
                }
                prevPointerPos = screenPos;
                pointerWasDown = held || down;
                break;

            case Phase.Thrown:
                // Steer the ragdoll left / right
                if (ragdollCtrl != null)
                {
                    if (down || held)
                    {
                        int rdir = (screenPos.x < Screen.width * 0.5f) ? -1 : 1;
                        ragdollCtrl.SetSteer(rdir);
                        OnRagdollSteerChanged?.Invoke(rdir);
                    }
                    else if (up)
                    {
                        ragdollCtrl.SetSteer(0);
                        OnRagdollSteerChanged?.Invoke(0);
                    }
                }
                break;
        }
    }

    private void GetPointer(out bool down, out bool held, out bool up, out Vector2 pos)
    {
        down = false; held = false; up = false; pos = Vector2.zero;

        if (Input.touchCount > 0)
        {
            // Continue controlling with the currently active gameplay touch.
            if (activeTouchFingerId >= 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch tracked = Input.GetTouch(i);
                    if (tracked.fingerId != activeTouchFingerId) continue;

                    pos = tracked.position;
                    switch (tracked.phase)
                    {
                        case TouchPhase.Moved:
                        case TouchPhase.Stationary:
                            held = true;
                            break;
                        case TouchPhase.Ended:
                        case TouchPhase.Canceled:
                            up = true;
                            activeTouchFingerId = -1;
                            break;
                    }
                    return;
                }

                // Finger disappeared unexpectedly; clear tracking safely.
                activeTouchFingerId = -1;
            }

            // Acquire a new gameplay touch (ignore touches that start on UI).
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.phase != TouchPhase.Began) continue;
                if (IsPointerOverUI(t.fingerId)) continue;

                activeTouchFingerId = t.fingerId;
                pos = t.position;
                down = true;
                return;
            }

            return;
        }

        // Mouse fallback (editor / standalone): also ignore UI clicks for gameplay capture.
        pos = Input.mousePosition;

        if (!mousePointerActive)
        {
            if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
            {
                down = true;
                mousePointerActive = true;
            }
            return;
        }

        if (Input.GetMouseButton(0))
        {
            held = true;
        }
        else
        {
            up = true;
            mousePointerActive = false;
        }
    }

    private static bool IsPointerOverUI(int touchFingerId = -1)
    {
        if (EventSystem.current == null) return false;
        return touchFingerId >= 0
            ? EventSystem.current.IsPointerOverGameObject(touchFingerId)
            : EventSystem.current.IsPointerOverGameObject();
    }

    private bool IsPointerOnCar(Vector2 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f))
            return hit.transform == transform || hit.transform.IsChildOf(transform);
        return false;
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Pulling (Pullback)

    private void BeginPulling(Vector2 screenPos)
    {
        aimOrigin      = transform.position;
        dragStartWorld = ScreenToGround(screenPos);

        // Lock the car's horizontal forward — this is the launch direction
        launchDirection   = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        SetPhase(Phase.Pulling);
    }

    private void ContinuePulling(Vector2 screenPos)
    {
        Vector3 fingerWorld = ScreenToGround(screenPos);
        Vector3 dragDelta   = fingerWorld - dragStartWorld;
        dragDelta.y = 0f;

        // Project drag onto car's BACKWARD axis — only backward movement charges the spring
        float backAmount = Vector3.Dot(dragDelta, -launchDirection);
        backAmount = Mathf.Clamp(backAmount, 0f, maxPullDistance);

        pullDistance  = backAmount;
        PullFraction = pullDistance / maxPullDistance;
        OnPullFractionUpdated?.Invoke(PullFraction);

        // Slide car backward from its placed position (no rotation)
        Vector3 pos = aimOrigin - launchDirection * pullDistance;
        transform.position = new Vector3(pos.x, aimOrigin.y, pos.z);
    }

    private void FinishPull()
    {
        if (PullFraction > 0.02f)
        {
            float force = PullFraction * maxLaunchForce;

            // Nudge the car up slightly before going dynamic to prevent
            // collider overlap with the ground that causes a physics pop
            // when the solver resolves the penetration (fixes mobile launch).
            transform.position += Vector3.up * 0.02f;

            SetPhase(Phase.Launched);                          // rb goes dynamic here
            rb.AddForce(launchDirection * force, ForceMode.Impulse);  // rb is live — force lands
        }
        else
        {
            // Negligible pull — snap back to origin and wait
            transform.position = new Vector3(aimOrigin.x, aimOrigin.y, aimOrigin.z);
            SetPhase(Phase.Idle);
        }

        PullFraction = 0f;
        pullDistance  = 0f;
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Aiming / Throwing

    private void BeginAiming(Vector2 screenPos)
    {
        prevPointerPos = screenPos;
        pointerWasDown = true;

        // Slow-motion — Unity physics is naturally slowed by timeScale.
        // The car rigidbody stays dynamic and keeps its arc at the reduced rate.
        savedFixedDelta     = Time.fixedDeltaTime;
        Time.timeScale      = aimTimeScale;
        Time.fixedDeltaTime = 0.02f * aimTimeScale;

        SetPhase(Phase.Aiming);
    }

    private void FinishAiming()
    {
        // Capture the car's current velocity (still slow-mo scaled) BEFORE restoring time,
        // so the ragdoll inherits the same world-space momentum.
        Vector3 carVelAtThrow = rb.linearVelocity;

        // Restore time.
        Time.timeScale      = 1f;
        Time.fixedDeltaTime = savedFixedDelta;

        // Capture aim values before deactivating UI.
        Vector3 aimDir = throwAimUI.AimDirection;
        float   power  = throwAimUI.Power;
        float   force  = Mathf.Lerp(minThrowForce, maxThrowForce, power);

        throwAimUI.Deactivate();

        // Spawn & throw ragdoll.
        if (ragdollPrefab != null)
        {
            Vector3 spawnPos = transform.position + transform.rotation * ragdollSpawnOffset;
            spawnedRagdoll   = Instantiate(ragdollPrefab, spawnPos, transform.rotation);

            // Prefer root Rigidbody (hips) for follow target and steering.
            Rigidbody mainBody = spawnedRagdoll.GetComponent<Rigidbody>();
            if (mainBody == null)
                mainBody = spawnedRagdoll.GetComponentInChildren<Rigidbody>();
            ThrownRagdoll = (mainBody != null) ? mainBody.transform : spawnedRagdoll.transform;

            // Fix jitter: interpolation + continuous collision on every body.
            Rigidbody[] bodies = spawnedRagdoll.GetComponentsInChildren<Rigidbody>(true);
            foreach (var body in bodies)
            {
                body.isKinematic            = false;
                body.interpolation          = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                // Inherit car momentum so the ragdoll doesn't snap to world-space zero.
                body.linearVelocity         = carVelAtThrow;
                body.AddForce(aimDir * force, ForceMode.Impulse);
            }

            // Ragdoll steering after throw.
            ragdollCtrl = spawnedRagdoll.AddComponent<RagdollController>();
            ragdollCtrl.Init(mainBody, ragdollControlInfluence, ragdollControlForce);

            SetPhase(Phase.Thrown);
        }
        else
        {
            Debug.LogWarning("[CarController] No ragdoll prefab assigned! Returning to Launched.");
            SetPhase(Phase.Launched);
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Rigidbody Configuration

    private void SetRigidbodyKinematic()
    {
        rb.interpolation  = RigidbodyInterpolation.None;
        rb.isKinematic    = true;
        rb.useGravity     = false;
        rb.linearDamping  = 0f;
        rb.angularDamping = 0f;
        rb.constraints    = RigidbodyConstraints.None;
    }

    private void SetRigidbodyDynamic()
    {
        // Gravity ON so the car can fly off ramps and land naturally.
        // No rotation freeze — upright enforcement is handled in FixedUpdate while grounded,
        // and the car must be free to pitch/roll when airborne.
        // Interpolate smooths the rendered position between physics ticks — eliminates jitter.
        rb.interpolation  = RigidbodyInterpolation.Interpolate;
        rb.useGravity     = true;
        rb.constraints    = RigidbodyConstraints.None;

        rb.isKinematic    = false;
        rb.linearDamping  = launchLinearDrag;
        rb.angularDamping = launchAngularDrag;
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Pull Indicator

    private void InitIndicator()
    {
        if (pullLine == null)
        {
            var go = new GameObject("PullIndicator");
            go.transform.SetParent(transform, false);
            pullLine = go.AddComponent<LineRenderer>();
        }

        pullLine.positionCount     = 5;
        pullLine.useWorldSpace     = true;
        pullLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pullLine.receiveShadows    = false;
        pullLine.material          = pullLineMaterial != null
            ? pullLineMaterial
            : new Material(Shader.Find("Sprites/Default"));
        pullLine.enabled           = false;
    }

    private void RefreshIndicator()
    {
        if (!pullLine.enabled) return;

        float t    = PullFraction;
        float yOff = 0.18f;

        Vector3 carTip    = transform.position + Vector3.up * yOff;
        Vector3 originTip = new Vector3(aimOrigin.x, groundY + yOff, aimOrigin.z);
        Vector3 dir       = originTip - carTip;
        if (dir.sqrMagnitude < 0.001f) return;
        dir.Normalize();

        Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;
        float   hw   = Mathf.Lerp(0.12f, 0.45f, t);

        pullLine.SetPosition(0, carTip);
        pullLine.SetPosition(1, originTip);
        pullLine.SetPosition(2, originTip - dir * hw + side * hw * 0.55f);
        pullLine.SetPosition(3, originTip);
        pullLine.SetPosition(4, originTip - dir * hw - side * hw * 0.55f);

        float w = Mathf.Lerp(indicatorWidthMin, indicatorWidthMax, t);
        pullLine.startWidth = w;
        pullLine.endWidth   = w * 0.4f;

        Color c = Color.Lerp(indicatorColorMin, indicatorColorMax, t);
        pullLine.startColor = c;
        pullLine.endColor   = Color.Lerp(c, Color.white, 0.25f);
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Helpers

    private Vector3 ScreenToGround(Vector2 screenPos)
    {
        var plane = new Plane(Vector3.up, new Vector3(0f, aimOrigin.y, 0f));
        Ray ray   = cam.ScreenPointToRay(screenPos);
        return plane.Raycast(ray, out float d) ? ray.GetPoint(d) : transform.position;
    }

    /// <summary>Override max launch force at runtime (used by RoundManager).</summary>
    public void SetMaxLaunchForce(float force) => maxLaunchForce = Mathf.Clamp(force, 5f, 200f);

    /// <summary>Override steer speed at runtime (used by RoundManager).</summary>
    public void SetSteerSpeed(float speed) => steerSpeed = Mathf.Clamp(speed, 30f, 300f);

    /// <summary>Reset tuning to Inspector defaults (called on game restart).</summary>
    public void ResetTuning()
    {
        // Caller should cache original values if needed.
        // For now these just clamp to safe ranges.
    }

    #endregion
}
