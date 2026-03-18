using UnityEngine;

/// <summary>
/// Manages all sound effects for the pullback car.
///
/// ATTACH TO: CarMain alongside CarController.
/// REQUIRES: AudioSource components on the same GameObject.
///           Missing sources are auto-created at runtime.
///
/// AUDIO CLIP REQUIREMENTS (import to Assets/Audio/SFX/):
///   pullChargeClip      — Mechanical spring wind-up / ratchet. ~1 s, loopable. Mono.
///   chargeReleaseClip   — Violent spring snap + chassis thump + air crack. 0.2–0.4 s. Mono.
///   launchClip          — Spring snap + deep whoosh. 0.3–0.5 s. Mono.
///   engineLoopClip      — Toy motor whirr. Seamless loop. Mono.
///   tireScreechClip     — Rubber squeal. Seamless loop. Mono.
///   landingClip         — Metallic thud + suspension creak. 0.2–0.4 s. Mono.
///   airborneClip        — Short upward whoosh. 0.2 s. Mono.
///   sparksClip          — Metallic scrape / grind. 0.2–0.3 s. Mono.
///   slowMoEnterClip     — Low-pass downward sweep ("woooomp"). 0.5 s. Mono.
///   slowMoExitClip      — Upward snap-back sweep. 0.3 s. Mono.
///   ragdollEjectClip    — Spring boing + comic whoosh. 0.4 s. Mono.
///   stopClip            — Brake squeal + chirp. Short. Mono.
///
/// FORMAT: .ogg or .wav, 44100 Hz, Mono (Force To Mono ON).
///         One-shots: Vorbis compression. Loops: ADPCM for lower latency.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class CarSFXController : MonoBehaviour
{
    // ── Audio Sources ──────────────────────────────────────────────────────────
    [Header("Audio Sources")]
    [Tooltip("Used for one-shot SFX (launch, land, sparks, etc.). 3D spatialization ON.")]
    [SerializeField] private AudioSource sfxSource;

    [Tooltip("Dedicated source for continuous engine loop.")]
    [SerializeField] private AudioSource engineSource;

    [Tooltip("Dedicated source for tire screech loop.")]
    [SerializeField] private AudioSource tireSource;

    [Tooltip("Dedicated source for pull-charge loop — separate so it can fade independently.")]
    [SerializeField] private AudioSource chargeSource;

    [Tooltip("Dedicated source for the release transient so pitch/volume can scale with pull strength.")]
    [SerializeField] private AudioSource releaseSource;

    // ── Clips ─────────────────────────────────────────────────────────────────
    [Header("Clips — assign from Assets/Audio/SFX/")]
    [SerializeField] private AudioClip pullChargeClip;
    [SerializeField] private AudioClip chargeReleaseClip;
    [SerializeField] private AudioClip engineStartClip;
    [SerializeField] private AudioClip launchClip;
    [SerializeField] private AudioClip engineLoopClip;
    [SerializeField] private AudioClip tireScreechClip;
    [SerializeField] private AudioClip landingClip;
    [SerializeField] private AudioClip airborneClip;
    [SerializeField] private AudioClip slowMoEnterClip;
    [SerializeField] private AudioClip slowMoExitClip;
    [SerializeField] private AudioClip ragdollEjectClip;
    [SerializeField] private AudioClip stopClip;

    // ── Engine Tuning ─────────────────────────────────────────────────────────
    [Header("Engine Pitch Tuning")]
    [SerializeField] private float enginePitchMin  = 0.8f;
    [SerializeField] private float enginePitchMax  = 1.6f;
    [SerializeField] private float engineSpeedRef  = 15f;

    [SerializeField] private float engineVolMin    = 0.3f;
    [SerializeField] private float engineVolMax    = 0.9f;

    // ── Tire Screech Tuning ───────────────────────────────────────────────────
    [Header("Tire Screech Tuning")]
    [Tooltip("Lateral slip speed (m/s) that triggers screech.")]
    [SerializeField] private float screechSlipThreshold  = 2f;

    [Tooltip("Lateral slip speed that reaches full screech volume.")]
    [SerializeField] private float screechSlipFullVolume = 5f;

    // ── Pull Charge Tuning ────────────────────────────────────────────────────
    [Header("Pull Charge Tuning")]
    [SerializeField] private float chargePitchMin = 0.8f;
    [SerializeField] private float chargePitchMax = 1.5f;
    [SerializeField] private float chargeVol      = 0.7f;

    [Header("Charge Release Tuning")]
    [Tooltip("Minimum release volume for a light pull.")]
    [SerializeField] private float releaseVolMin = 0.35f;

    [Tooltip("Maximum release volume for a full-power launch.")]
    [SerializeField] private float releaseVolMax = 1f;

    [Tooltip("Pitch for a lighter release.")]
    [SerializeField] private float releasePitchMin = 0.92f;

    [Tooltip("Pitch for a full-power release.")]
    [SerializeField] private float releasePitchMax = 1.06f;

    [Tooltip("How much the existing launch clip is reinforced by pull strength.")]
    [SerializeField] private float launchReleaseVolMin = 0.7f;

    [Tooltip("How loud the existing launch clip gets at full pull.")]
    [SerializeField] private float launchReleaseVolMax = 1f;

    // ── Slow-Mo Audio ─────────────────────────────────────────────────────────
    [Header("Slow-Mo Audio")]
    [Tooltip("How much the engine pitch drops when slow-motion starts (0 = full speed, 1 = near zero).")]
    [SerializeField] private float slowMoPitchMult = 0.2f;

    // ═══════════════════════════════════════════════════════════════════════════
    #region Internals

    private CarController car;
    private bool wasAirborne;

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Unity Lifecycle

    private void Awake()
    {
        car = GetComponent<CarController>();

        EnsureAudioSources();
        ConfigureSource(sfxSource, false, 1f);
        ConfigureSource(engineSource, true);
        ConfigureSource(tireSource, true);
        ConfigureSource(chargeSource, true);
        ConfigureSource(releaseSource, false, 1f);
    }

    private void OnEnable()
    {
        if (car != null)
            car.OnPhaseChanged += HandlePhaseChanged;
    }

    private void OnDisable()
    {
        if (car != null)
            car.OnPhaseChanged -= HandlePhaseChanged;
    }

    private void Update()
    {
        UpdateEngineAudio();
        UpdateTireScreech();
        UpdatePullChargeAudio();
        TrackAirborne();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Phase Events

    private void HandlePhaseChanged(CarController.Phase prev, CarController.Phase next)
    {
        switch (next)
        {
            case CarController.Phase.Launched:
                if (prev == CarController.Phase.Pulling)
                {
                    PlayChargeRelease(car != null ? car.PullFraction : 1f);
                }
                else
                {
                    PlayOneShot(launchClip, 1f);
                }

                StartEngineLoop();
                StopChargeLoop();
                break;

            case CarController.Phase.Idle:
                PlayOneShot(stopClip, 0.8f);
                StopEngineLoop();
                StopChargeLoop();
                break;

            case CarController.Phase.Pulling:
                PlayOneShot(engineStartClip, 0.9f);
                StartEngineLoop(true);
                StartChargeLoop();
                break;

            case CarController.Phase.Aiming:
                PlayUnscaled(slowMoEnterClip, 1f);
                if (engineSource != null && engineSource.isPlaying)
                    engineSource.pitch = slowMoPitchMult;
                break;

            case CarController.Phase.Thrown:
                PlayOneShot(slowMoExitClip, 1f);
                PlayOneShot(ragdollEjectClip, 1f);
                StopEngineLoop();
                break;
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Continuous Audio Updates

    private void UpdateEngineAudio()
    {
        if (engineSource == null || !engineSource.isPlaying) return;
        if (car.CurrentPhase == CarController.Phase.Aiming) return;

        float pitch;
        float vol;

        if (car.CurrentPhase == CarController.Phase.Pulling)
        {
            float tPull = Mathf.Clamp01(car.PullFraction);
            pitch = Mathf.Lerp(enginePitchMin * 0.90f, enginePitchMax * 0.95f, tPull);
            vol   = Mathf.Lerp(engineVolMin * 0.45f, engineVolMax * 0.80f, tPull);
        }
        else
        {
            float tSpeed = Mathf.Clamp01(car.Speed / engineSpeedRef);
            pitch = Mathf.Lerp(enginePitchMin, enginePitchMax, tSpeed);
            vol = Mathf.Lerp(engineVolMin, engineVolMax, tSpeed);
        }

        engineSource.pitch = Mathf.Lerp(engineSource.pitch, pitch, 10f * Time.deltaTime);
        engineSource.volume = Mathf.Lerp(engineSource.volume, vol, 6f * Time.deltaTime);
    }

    private void UpdateTireScreech()
    {
        if (tireSource == null || tireScreechClip == null) return;

        if (car.CurrentPhase != CarController.Phase.Launched)
        {
            FadeOutTireSource();
            return;
        }

        Vector3 vel = car.Velocity;
        Vector3 fwdH = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        Vector3 latVel = vel - Vector3.Dot(vel, fwdH) * fwdH;
        latVel.y = 0f;
        float slip = latVel.magnitude;

        float targetVol = Mathf.InverseLerp(screechSlipThreshold, screechSlipFullVolume, slip);
        targetVol = Mathf.Clamp01(targetVol) * 0.8f;

        if (targetVol > 0.05f)
        {
            if (tireSource.clip != tireScreechClip)
                tireSource.clip = tireScreechClip;

            if (!tireSource.isPlaying)
            {
                tireSource.volume = 0f;
                tireSource.Play();
            }

            tireSource.volume = Mathf.Lerp(tireSource.volume, targetVol, 8f * Time.deltaTime);
        }
        else
        {
            FadeOutTireSource();
        }
    }

    private void UpdatePullChargeAudio()
    {
        if (chargeSource == null || car.CurrentPhase != CarController.Phase.Pulling) return;

        float t = car.PullFraction;
        chargeSource.pitch = Mathf.Lerp(chargePitchMin, chargePitchMax, t);
        chargeSource.volume = Mathf.Lerp(0f, chargeVol, t > 0.01f ? 1f : 0f);
    }

    private void TrackAirborne()
    {
        bool airborne = car.IsAirborne;

        if (!wasAirborne && airborne && car.CurrentPhase == CarController.Phase.Launched)
        {
            PlayOneShot(airborneClip, 0.7f);
        }
        else if (wasAirborne && !airborne && car.CurrentPhase == CarController.Phase.Launched)
        {
            float impact = Mathf.Clamp01(car.Speed / engineSpeedRef);
            PlayOneShot(landingClip, 0.5f + impact * 0.5f);
        }

        wasAirborne = airborne;
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Engine Loop Management

    private void StartEngineLoop(bool preserveIfPlaying = false)
    {
        if (engineSource == null || engineLoopClip == null) return;

        if (preserveIfPlaying && engineSource.isPlaying && engineSource.clip == engineLoopClip)
            return;

        engineSource.clip = engineLoopClip;
        if (!engineSource.isPlaying)
        {
            engineSource.pitch = enginePitchMin;
            engineSource.volume = engineVolMin;
        }
        if (!engineSource.isPlaying)
            engineSource.Play();
    }

    private void StopEngineLoop()
    {
        if (engineSource != null)
        {
            engineSource.Stop();
            engineSource.volume = 0f;
        }

        if (tireSource != null)
        {
            tireSource.Stop();
            tireSource.volume = 0f;
        }
    }

    private void StartChargeLoop()
    {
        if (chargeSource == null || pullChargeClip == null) return;

        chargeSource.clip = pullChargeClip;
        chargeSource.pitch = chargePitchMin;
        chargeSource.volume = 0f;
        if (!chargeSource.isPlaying)
            chargeSource.Play();
    }

    private void StopChargeLoop()
    {
        if (chargeSource == null) return;
        chargeSource.Stop();
        chargeSource.volume = 0f;
    }

    private void FadeOutTireSource()
    {
        if (tireSource == null || !tireSource.isPlaying) return;

        tireSource.volume = Mathf.MoveTowards(tireSource.volume, 0f, 3f * Time.deltaTime);
        if (tireSource.volume <= 0.01f)
            tireSource.Stop();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Audio Helpers

    private void PlayOneShot(AudioClip clip, float volume = 1f)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip, volume);
    }

    private void PlayUnscaled(AudioClip clip, float volume = 1f)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.ignoreListenerPause = true;
        sfxSource.PlayOneShot(clip, volume);
        sfxSource.ignoreListenerPause = false;
    }

    private void PlayChargeRelease(float pullFraction)
    {
        float releaseStrength = Mathf.Clamp01(pullFraction);
        float emphasizedStrength = Mathf.SmoothStep(0f, 1f, releaseStrength);

        PlayOneShot(launchClip, Mathf.Lerp(launchReleaseVolMin, launchReleaseVolMax, emphasizedStrength));

        if (releaseSource == null || chargeReleaseClip == null) return;

        releaseSource.pitch = Mathf.Lerp(releasePitchMin, releasePitchMax, emphasizedStrength);
        releaseSource.PlayOneShot(chargeReleaseClip, Mathf.Lerp(releaseVolMin, releaseVolMax, emphasizedStrength));
    }

    private void EnsureAudioSources()
    {
        if (sfxSource == null)
            sfxSource = GetComponent<AudioSource>();

        if (engineSource == null)
            engineSource = gameObject.AddComponent<AudioSource>();

        if (tireSource == null)
            tireSource = gameObject.AddComponent<AudioSource>();

        if (chargeSource == null)
            chargeSource = gameObject.AddComponent<AudioSource>();

        if (releaseSource == null)
            releaseSource = gameObject.AddComponent<AudioSource>();
    }

    private static void ConfigureSource(AudioSource source, bool loop, float initialVolume = 0f)
    {
        if (source == null) return;
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 1f;
        source.volume = Mathf.Clamp01(initialVolume);
    }

    #endregion
}
