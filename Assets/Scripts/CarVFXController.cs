using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Manages all particle / visual effects for the pullback car.
///
/// ATTACH TO: CarMain alongside CarController.
///
/// SETUP IN INSPECTOR:
///   • Assign the four wheel transforms (drag from Hierarchy).
///   • Assign each particle/VFX prefab slot (see comments per slot).
///   • Assign cameraShake reference (drag Camera GameObject).
///   • Optionally drag the scene's URP Global Volume for slow-mo post-fx.
///
/// PREFABS TO CREATE (Assets/Prefab/VFX/):
///   TireSmoke  — see plan: white puffs, cone emission, Billboard renderer
///   Exhaust    — dark smoke, local-space, continuous burst on launch
///   Sparks     — bright orange stretched-billboard sparks with trails
///   (Launch dust, landing dust, eject flash → reuse Hyper Casual FX prefabs)
/// </summary>
public class CarVFXController : MonoBehaviour
{
    // ── Wheel Transforms ──────────────────────────────────────────────────────
    [Header("Wheel Transforms")]
    [Tooltip("Drag the four wheel child transforms from the Hierarchy.")]
    [SerializeField] private Transform wheelFL;
    [SerializeField] private Transform wheelFR;
    [SerializeField] private Transform wheelBL;
    [SerializeField] private Transform wheelBR;

    // ── Continuous Particle Prefabs ───────────────────────────────────────────
    [Header("Continuous Particles (assign prefabs)")]
    [Tooltip("TireSmoke.prefab — white puffy smoke emitted at each wheel while driving.")]
    [SerializeField] private ParticleSystem tireSmokePrefab;

    [Tooltip("Exhaust.prefab — dark smoke trail emitted from rear of car while moving.")]
    [SerializeField] private ParticleSystem exhaustPrefab;

    [Tooltip("Optional exact exhaust spawn point. If null, a default local offset is used.")]
    [SerializeField] private Transform exhaustAnchor;

    [Tooltip("Sparks.prefab — orange sparks from undercarriage when scraping ramps/slopes.")]
    [SerializeField] private ParticleSystem sparksPrefab;

    // ── One-Shot VFX Prefabs ──────────────────────────────────────────────────
    [Header("One-Shot VFX Prefabs (assign from Hyper Casual FX / Stylized packs)")]
    [Tooltip("Instantiated at launch point when the car is released. " +
             "Suggested: Dust_permanently_blue (color overridden to tan/brown).")]
    [SerializeField] private GameObject launchBurstPrefab;

    [Tooltip("Instantiated at ragdoll spawn position when thrown. " +
             "Suggested: Flash_star_ellow or similar from Hyper Casual FX.")]
    [SerializeField] private GameObject ragdollEjectPrefab;

    // ── Ragdoll Trail ─────────────────────────────────────────────────────────
    [Header("Ragdoll Trail")]
    [Tooltip("TrailRenderer prefab attached to the ragdoll's hips when thrown. " +
             "Width curve: 0.3 → 0, time 0.5 s, semi-transparent white.")]
    [SerializeField] private TrailRenderer ragdollTrailPrefab;

    // ── Camera Shake ──────────────────────────────────────────────────────────
    [Header("References")]
    [Tooltip("Drag the Camera GameObject here.")]
    [SerializeField] private CameraShake cameraShake;

    // ── URP Post-Processing (Slow-Mo) ─────────────────────────────────────────
    [Header("URP Post-Processing (optional — drag Global Volume here)")]
    [Tooltip("The scene's Global Volume. Used to lerp Vignette on aiming slow-mo." +
             " Leave null to skip post-processing.")]
    [SerializeField] private Volume globalVolume;

    [SerializeField] private float aimVignetteIntensity    = 0.45f;
    [SerializeField] private float aimChromaticAberration  = 0.3f;
    [SerializeField] private float aimPostFxLerpSpeed      = 8f;
    [SerializeField] private Color aimVignetteColor        = Color.black;
    [SerializeField] private Color launchVignetteColor     = new Color(0.12f, 0.18f, 0.35f, 1f);
    [SerializeField] private Color speedVignetteColor      = new Color(0.10f, 0.28f, 0.60f, 1f);

    // ── Spark Thresholds ──────────────────────────────────────────────────────
    [Header("Spark Settings")]
    [Tooltip("Minimum car speed (m/s) for sparks to fire.")]
    [SerializeField] private float sparkSpeedThreshold = 4f;

    [Tooltip("Minimum surface angle from horizontal that triggers sparks.")]
    [SerializeField] private float sparkSlopeAngle = 12f;

    // ── Tire Smoke Emission Scaling ───────────────────────────────────────────
    [Header("Tire Smoke Scaling")]
    [SerializeField] private float smokeLowSpeed  = 2f;
    [SerializeField] private float smokeHighSpeed = 12f;
    [SerializeField] private float smokeLowRate   = 8f;
    [SerializeField] private float smokeHighRate  = 60f;

    [Header("Tire Smoke Placement")]
    [SerializeField] private Vector3 tireSmokeOffsetFL = new Vector3(0f, -0.15f, 0f);
    [SerializeField] private Vector3 tireSmokeOffsetFR = new Vector3(0f, -0.15f, 0f);
    [SerializeField] private Vector3 tireSmokeOffsetBL = new Vector3(0f, -0.15f, 0f);
    [SerializeField] private Vector3 tireSmokeOffsetBR = new Vector3(0f, -0.15f, 0f);
    [SerializeField] private bool enableSmokeWhilePulling = true;

    [Header("Speed Burst Post-FX")]
    [SerializeField] private float accelSpeedThreshold = 14f;
    [SerializeField] private float accelVignetteBoost = 0.18f;
    [SerializeField] private float accelChromaticBoost = 0.25f;
    [SerializeField] private float accelLensDistortionBoost = -0.18f;
    [SerializeField] private float accelFxDecaySpeed = 3.5f;
    [SerializeField] private float launchFxPulseDuration = 0.30f;
    [SerializeField] private float accelDeltaSpeedThreshold = 0.45f;
    [SerializeField] private float speedMotionBlurBoost = 0.35f;
    [SerializeField] private float launchMotionBlurBoost = 0.20f;

    [Header("Speed Lines FX")]
    [SerializeField] private ParticleSystem speedLinesPrefab;
    [SerializeField] private Transform speedLinesAnchor;
    [SerializeField] private Vector3 speedLinesLocalOffset = new Vector3(0f, 0f, 1.5f);
    [SerializeField] private float speedLinesMaxRate = 90f;
    [SerializeField] private float speedLinesPlayThreshold = 0.15f;

    [Header("Exhaust Priming")]
    [SerializeField] private Vector3 exhaustLocalOffset = new Vector3(0f, 0.2f, -0.8f);
    [SerializeField] private float exhaustBaseRateFallback = 32f;
    [SerializeField] private float pullExhaustRateMinFactor = 0.35f;
    [SerializeField] private float pullExhaustRateMaxFactor = 1.10f;

    // ═══════════════════════════════════════════════════════════════════════════
    #region Internals

    private CarController   car;
    private bool            wasAirborne;
    private bool            wasLaunched;
    private TrailRenderer   activeRagdollTrail;
    private Transform[]     wheelTransforms;

    // Live instances of continuous particle systems
    private ParticleSystem[] tireSmokeInstances; // 4 — FL, FR, BL, BR
    private ParticleSystem   exhaustInstance;
    private ParticleSystem   sparksInstance;
    private ParticleSystem   speedLinesInstance;
    private float            baseExhaustRate;

    // URP volume override components (fetched once)
    private Vignette           vignette;
    private ChromaticAberration chromaticAberration;
    private LensDistortion      lensDistortion;
    private MotionBlur          motionBlur;
    private float               targetVignette;
    private float               targetChromatic;
    private float               targetLensDistortion;
    private float               targetMotionBlur;
    private Color               targetVignetteColor = Color.black;
    private float               accelFxWeight;
    private float               previousSpeed;
    private float               launchFxPulseTimer;

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Unity Lifecycle

    private void Awake()
    {
        car = GetComponent<CarController>();

        if (cameraShake == null)
            cameraShake = FindFirstObjectByType<CameraShake>();

        FetchVolumeComponents();
        SpawnContinuousParticles();
    }

    private void OnEnable()
    {
        car.OnPhaseChanged += HandlePhaseChanged;
    }

    private void OnDisable()
    {
        car.OnPhaseChanged -= HandlePhaseChanged;
    }

    private void Update()
    {
        var phase = car.CurrentPhase;

        UpdateTireSmoke();
        UpdateExhaust();
        CheckAirborneTransition();

        // These are only meaningful during active gameplay phases
        if (phase != CarController.Phase.Idle)
        {
            UpdateSparks();
            UpdatePostFx();
        }

        if (phase == CarController.Phase.Launched)
        {
            UpdateSpeedLines();
        }
        else if (speedLinesInstance != null && speedLinesInstance.isPlaying)
        {
            speedLinesInstance.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Phase Events

    private void HandlePhaseChanged(CarController.Phase prev, CarController.Phase next)
    {
        switch (next)
        {
            // ── Launched: car just released from pullback ──
            case CarController.Phase.Launched:
                SpawnOneShot(launchBurstPrefab, transform.position, Quaternion.identity);
                cameraShake?.Shake(car.PullFraction * 0.4f, 0.25f);
                SetExhaustEmitting(true);
                launchFxPulseTimer = launchFxPulseDuration;
                accelFxWeight = 1f;
                targetVignetteColor = Color.black;
                targetMotionBlur = 0f;
                wasLaunched = true;
                break;

            // ── Idle: car has stopped ──
            case CarController.Phase.Idle:
                StopAllContinuousParticles();
                wasLaunched = false;
                // Reset post-fx targets
                targetVignette  = 0f;
                targetChromatic = 0f;
                targetLensDistortion = 0f;
                targetMotionBlur = 0f;
                targetVignetteColor = Color.black;
                accelFxWeight = 0f;
                break;

            // ── Pulling: engine/spool priming ──
            case CarController.Phase.Pulling:
                SetExhaustEmitting(true);
                targetVignetteColor = Color.black;
                break;

            // ── Aiming: slow-motion begins ──
            case CarController.Phase.Aiming:
                targetVignette  = aimVignetteIntensity;
                targetChromatic = aimChromaticAberration;
                targetLensDistortion = 0f;
                targetMotionBlur = 0f;
                targetVignetteColor = aimVignetteColor;
                break;

            // ── Thrown: ragdoll ejected ──
            case CarController.Phase.Thrown:
                targetVignette  = 0f;
                targetChromatic = 0f;
                targetLensDistortion = 0f;
                targetMotionBlur = 0f;
                targetVignetteColor = Color.black;
                SpawnEjectEffects();
                SetExhaustEmitting(false);
                break;
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Continuous Particle Updates

    private void UpdateTireSmoke()
    {
        if (tireSmokeInstances == null) return;

        bool shouldSmoke = car.CurrentPhase == CarController.Phase.Launched
                        && !car.IsAirborne
                        && car.Speed > smokeLowSpeed;

        if (!shouldSmoke && enableSmokeWhilePulling && car.CurrentPhase == CarController.Phase.Pulling)
            shouldSmoke = car.PullFraction > 0.01f;

        float rate = shouldSmoke
            ? Mathf.Lerp(smokeLowRate, smokeHighRate,
                         Mathf.InverseLerp(smokeLowSpeed, smokeHighSpeed, car.Speed))
            : 0f;

        for (int i = 0; i < tireSmokeInstances.Length; i++)
        {
            var ps = tireSmokeInstances[i];
            if (ps == null) continue;

            var em = ps.emission;
            em.rateOverTime = rate;

            if (shouldSmoke && !ps.isPlaying) ps.Play();
            if (!shouldSmoke && ps.isPlaying)  ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void UpdateSparks()
    {
        if (sparksInstance == null) return;

        bool shouldSpark = car.CurrentPhase == CarController.Phase.Launched
                        && !car.IsAirborne
                        && car.Speed > sparkSpeedThreshold
                        && car.IsGrounded;

        if (shouldSpark)
        {
            float angle = Vector3.Angle(car.GroundNormal, Vector3.up);
            if (angle >= sparkSlopeAngle)
            {
                if (!sparksInstance.isPlaying) sparksInstance.Play();
                return;
            }
        }

        if (sparksInstance.isPlaying)
            sparksInstance.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void UpdatePostFx()
    {
        // Uses Time.unscaledDeltaTime so it works inside slow-motion
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

        float launchWeight = 0f;
        if (launchFxPulseTimer > 0f)
        {
            launchWeight = Mathf.Clamp01(launchFxPulseTimer / Mathf.Max(0.01f, launchFxPulseDuration));
            launchFxPulseTimer -= dt;
            accelFxWeight = 1f;
        }

        if (car.CurrentPhase == CarController.Phase.Launched)
        {
            float speedDelta = car.Speed - previousSpeed;
            float acceleration = (car.Speed - previousSpeed) / dt;
            if (acceleration > accelSpeedThreshold || speedDelta > accelDeltaSpeedThreshold)
                accelFxWeight = 1f;
        }
        accelFxWeight = Mathf.MoveTowards(accelFxWeight, 0f, accelFxDecaySpeed * dt);

        float desiredVignette  = Mathf.Clamp01(targetVignette + accelVignetteBoost * accelFxWeight);
        float desiredChromatic = Mathf.Clamp01(targetChromatic + accelChromaticBoost * accelFxWeight);
        float desiredLens      = targetLensDistortion + accelLensDistortionBoost * accelFxWeight;
        float desiredMotionBlur = Mathf.Clamp01(
            targetMotionBlur
            + speedMotionBlurBoost * accelFxWeight
            + launchMotionBlurBoost * launchWeight);

        Color desiredVignetteColor = targetVignetteColor;
        if (launchWeight > 0.001f)
            desiredVignetteColor = Color.Lerp(desiredVignetteColor, launchVignetteColor, launchWeight);
        else if (accelFxWeight > 0.001f)
            desiredVignetteColor = Color.Lerp(desiredVignetteColor, speedVignetteColor, accelFxWeight);

        if (vignette != null)
        {
            vignette.intensity.value = Mathf.Lerp(
                vignette.intensity.value,
                desiredVignette,
                aimPostFxLerpSpeed * dt);
            vignette.color.value = Color.Lerp(
                vignette.color.value,
                desiredVignetteColor,
                aimPostFxLerpSpeed * dt);
        }

        if (chromaticAberration != null)
        {
            chromaticAberration.intensity.value = Mathf.Lerp(
                chromaticAberration.intensity.value,
                desiredChromatic,
                aimPostFxLerpSpeed * dt);
        }

        if (lensDistortion != null)
        {
            lensDistortion.intensity.value = Mathf.Lerp(
                lensDistortion.intensity.value,
                desiredLens,
                aimPostFxLerpSpeed * dt);
        }

        if (motionBlur != null)
        {
            motionBlur.intensity.value = Mathf.Lerp(
                motionBlur.intensity.value,
                desiredMotionBlur,
                aimPostFxLerpSpeed * dt);
        }

        previousSpeed = car.Speed;
    }

    private void UpdateSpeedLines()
    {
        if (speedLinesInstance == null) return;

        bool launched = car.CurrentPhase == CarController.Phase.Launched;
        float weight = launched ? Mathf.Clamp01(accelFxWeight) : 0f;
        float targetRate = weight >= speedLinesPlayThreshold
            ? Mathf.Lerp(0f, speedLinesMaxRate, weight)
            : 0f;

        var emission = speedLinesInstance.emission;
        emission.rateOverTime = targetRate;

        if (targetRate > 0.01f)
        {
            if (!speedLinesInstance.isPlaying) speedLinesInstance.Play();
        }
        else if (speedLinesInstance.isPlaying)
        {
            speedLinesInstance.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void UpdateExhaust()
    {
        if (exhaustInstance == null) return;

        var emission = exhaustInstance.emission;
        float targetRate = 0f;

        if (car.CurrentPhase == CarController.Phase.Pulling)
        {
            float t = Mathf.Clamp01(car.PullFraction);
            targetRate = Mathf.Lerp(baseExhaustRate * pullExhaustRateMinFactor,
                                    baseExhaustRate * pullExhaustRateMaxFactor, t);
            if (!exhaustInstance.isPlaying) exhaustInstance.Play();
        }
        else if (car.CurrentPhase == CarController.Phase.Launched)
        {
            targetRate = baseExhaustRate;
            if (!exhaustInstance.isPlaying) exhaustInstance.Play();
        }

        emission.rateOverTime = targetRate;
        if (targetRate <= 0f && exhaustInstance.isPlaying)
            exhaustInstance.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void CheckAirborneTransition()
    {
        bool airborne = car.IsAirborne;

        // Grounded this frame after being airborne last frame → landing
        if (wasAirborne && !airborne && car.CurrentPhase == CarController.Phase.Launched)
        {
            OnLanded();
        }

        wasAirborne = airborne;
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Events

    private void OnLanded()
    {
        cameraShake?.Shake(0.4f, 0.2f);
    }

    private void SpawnEjectEffects()
    {
        cameraShake?.Shake(0.5f, 0.3f);

        if (ragdollEjectPrefab != null)
        {
            Vector3 spawnPos = transform.position + transform.up * 1.5f;
            SpawnOneShot(ragdollEjectPrefab, spawnPos, transform.rotation);
        }

        // Attach a trail to the thrown ragdoll's hips
        if (ragdollTrailPrefab != null && car.ThrownRagdoll != null)
        {
            var trail = Instantiate(ragdollTrailPrefab, car.ThrownRagdoll);
            trail.transform.localPosition = Vector3.zero;
            activeRagdollTrail = trail;
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Particle Spawning / Management

    private void SpawnContinuousParticles()
    {
        wheelTransforms = new[] { wheelFL, wheelFR, wheelBL, wheelBR };
        tireSmokeInstances = new ParticleSystem[4];

        if (tireSmokePrefab != null)
        {
            for (int i = 0; i < 4; i++)
            {
                if (wheelTransforms[i] == null) continue;
                var ps = Instantiate(tireSmokePrefab, wheelTransforms[i]);
                ps.transform.localPosition = GetWheelSmokeOffset(i);
                ps.transform.localRotation = Quaternion.identity;
                var smokeMain = ps.main;
                smokeMain.simulationSpace = ParticleSystemSimulationSpace.Local;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                tireSmokeInstances[i] = ps;
            }
        }

        if (exhaustPrefab != null)
        {
            // Exhaust emits from behind/below the car
            Transform parent = exhaustAnchor != null ? exhaustAnchor : transform;
            exhaustInstance = Instantiate(exhaustPrefab, parent);
            if (exhaustAnchor == null)
                exhaustInstance.transform.localPosition = exhaustLocalOffset;
            else
                exhaustInstance.transform.localPosition = Vector3.zero;
            exhaustInstance.transform.localRotation = Quaternion.identity;
            var exhaustMain = exhaustInstance.main;
            exhaustMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = exhaustInstance.emission;
            baseExhaustRate = GetBaseEmissionRate(emission, exhaustBaseRateFallback);
            emission.rateOverTime = 0f;
            exhaustInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (sparksPrefab != null)
        {
            sparksInstance = Instantiate(sparksPrefab, transform);
            sparksInstance.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            sparksInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (speedLinesPrefab != null)
        {
            Transform parent = speedLinesAnchor;
            if (parent == null && cameraShake != null)
                parent = cameraShake.transform;
            if (parent == null && Camera.main != null)
                parent = Camera.main.transform;
            if (parent == null)
                parent = transform;

            speedLinesInstance = Instantiate(speedLinesPrefab, parent);
            speedLinesInstance.transform.localPosition = speedLinesLocalOffset;
            speedLinesInstance.transform.localRotation = Quaternion.identity;
            var linesMain = speedLinesInstance.main;
            linesMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            var linesEmission = speedLinesInstance.emission;
            linesEmission.rateOverTime = 0f;
            speedLinesInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        else
        {
            Debug.LogWarning("[CarVFXController] Speed lines prefab is not assigned. Use Tools > Pullback Fight > Generate Speedline Prefab and assign Assets/Prefab/VFX/SpeedLines.prefab.", this);
        }
    }

    private void SetExhaustEmitting(bool emit)
    {
        if (exhaustInstance == null) return;
        if (emit)
        {
            if (!exhaustInstance.isPlaying) exhaustInstance.Play();
        }
        else
        {
            exhaustInstance.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private static float GetBaseEmissionRate(ParticleSystem.EmissionModule emission, float fallback)
    {
        var rate = emission.rateOverTime;
        switch (rate.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return Mathf.Max(1f, rate.constant);
            case ParticleSystemCurveMode.TwoConstants:
                return Mathf.Max(1f, rate.constantMax);
            default:
                return Mathf.Max(1f, fallback);
        }
    }

    private Vector3 GetWheelSmokeOffset(int wheelIndex)
    {
        switch (wheelIndex)
        {
            case 0: return tireSmokeOffsetFL;
            case 1: return tireSmokeOffsetFR;
            case 2: return tireSmokeOffsetBL;
            case 3: return tireSmokeOffsetBR;
            default: return new Vector3(0f, -0.15f, 0f);
        }
    }

    private void StopAllContinuousParticles()
    {
        SetExhaustEmitting(false);
        if (sparksInstance != null)
            sparksInstance.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (tireSmokeInstances != null)
        {
            foreach (var ps in tireSmokeInstances)
                if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        if (speedLinesInstance != null)
            speedLinesInstance.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private static void SpawnOneShot(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        if (prefab == null) return;
        var go = Instantiate(prefab, pos, rot);

        // Auto-destroy: use ParticleSystem duration if available, else 5 s
        ParticleSystem ps = go.GetComponent<ParticleSystem>();
        float lifetime = ps != null
            ? ps.main.duration + ps.main.startLifetime.constantMax
            : 5f;
        Destroy(go, lifetime);
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region URP Volume Setup

    private void FetchVolumeComponents()
    {
        if (globalVolume == null) return;

        globalVolume.profile.TryGet(out vignette);
        globalVolume.profile.TryGet(out chromaticAberration);
        globalVolume.profile.TryGet(out lensDistortion);
        globalVolume.profile.TryGet(out motionBlur);

        if (vignette != null)
            vignette.intensity.overrideState = true;

        if (vignette != null)
            vignette.color.overrideState = true;

        if (chromaticAberration != null)
            chromaticAberration.intensity.overrideState = true;

        if (lensDistortion != null)
            lensDistortion.intensity.overrideState = true;

        if (motionBlur != null)
            motionBlur.intensity.overrideState = true;

        if (vignette == null && chromaticAberration == null && lensDistortion == null && motionBlur == null)
            Debug.LogWarning("[CarVFXController] No Vignette/ChromaticAberration/LensDistortion/MotionBlur overrides found in Global Volume profile. Speed post-FX has nothing to drive.", this);
    }

    #endregion
}
