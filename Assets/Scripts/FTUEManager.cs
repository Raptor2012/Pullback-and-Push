using System.Collections;
using UnityEngine;

/// <summary>
/// Orchestrates the first-time user experience tutorial.
///
/// LIFECYCLE:
///   • Call <see cref="TryStart"/> from MenuController when play begins.
///   • Subscribes to CarController events to advance steps automatically.
///   • Persists completion state via <see cref="FTUEProgressStore"/>.
///   • A replay run can be forced via <see cref="ForceRestart"/>.
///
/// STEP ORDER (mirrors FTUEStepConfig.steps[]):
///   0  TapCar           — tap the car to begin pulling
///   1  DragBack         — drag backward to charge
///   2  Release          — release to launch
///   3  SteerCar         — steer left/right while rolling
///   4  AirborneTapHold  — tap and hold car while airborne
///   5  AimRelease       — release at good power/direction
///   6  SteerRagdoll     — steer the ragdoll after throw
///   7  ScoreObserve     — observe the score display
///   8  ResetRun         — tap reset to play again
/// </summary>
public class FTUEManager : MonoBehaviour
{
    // ── Inspector refs ────────────────────────────────────────────────────────
    [Header("References")]
    [Tooltip("The gameplay car. Can be left null — auto-found on Start.")]
    [SerializeField] private CarController carController;

    [Tooltip("Step text/animation config asset. A default is created at runtime if null.")]
    [SerializeField] private FTUEStepConfig config;

    [Header("Tuning")]
    [Tooltip("How many steer inputs (any direction) count as completing the SteerCar step.")]
    [SerializeField] private int steerInputsRequired = 3;

    [Tooltip("How many ragdoll steer inputs count as completing the SteerRagdoll step.")]
    [SerializeField] private int ragdollSteerInputsRequired = 2;

    [Tooltip("Minimum pull fraction that completes the DragBack step.")]
    [SerializeField][Range(0f, 1f)] private float dragBackThreshold = 0.35f;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Try to start the FTUE. Does nothing if already completed.
    /// Call from MenuController after gameplay is enabled.
    /// </summary>
    public void TryStart()
    {
        if (FTUEProgressStore.IsCompleted)
        {
            Debug.Log("[FTUE] Already completed — skipping tutorial.");
            return;
        }
        StartFTUE();
    }

    /// <summary>Force full FTUE regardless of completion state (replay from menu).</summary>
    public void ForceRestart()
    {
        FTUEProgressStore.Reset();
        StopAllCoroutines();
        currentStep = -1;
        isRunning   = false;
        StartFTUE();
    }

    /// <summary>
    /// Inspector context-menu shortcut — clears the saved completion flag so the
    /// tutorial will show again on the next TryStart() call (or next Play session).
    /// Right-click the FTUEManager component header in the Inspector to invoke.
    /// </summary>
    [ContextMenu("Reset FTUE Progress")]
    private void ContextMenu_ResetProgress()
    {
        FTUEProgressStore.Reset();
        Debug.Log("[FTUE] Progress reset via context menu.");
    }

    /// <summary>
    /// Inspector context-menu shortcut — immediately forces the full tutorial to
    /// restart right now, even while in Play mode.
    /// </summary>
    [ContextMenu("Force Restart FTUE (Play Mode)")]
    private void ContextMenu_ForceRestart()
    {
        ForceRestart();
        Debug.Log("[FTUE] Tutorial force-restarted via context menu.");
    }

    // ── State ─────────────────────────────────────────────────────────────────
    private FTUEOverlay  overlay;
    private bool         isRunning;
    private int          currentStep = -1;

    // Step-specific counters
    private int   steerCount;
    private int   ragdollSteerCount;
    private bool  hasSteppedToAirborne;    // guard: only enter step 4 once per run
    private bool  pullFractionReached;

    // Follow-up timers
    private Coroutine followUpRoutine;

    // ── Step indices (must match config.steps order) ──────────────────────────
    private const int StepTapCar          = 0;
    private const int StepDragBack        = 1;
    private const int StepRelease         = 2;
    private const int StepSteerCar        = 3;
    private const int StepAirborneTapHold = 4;
    private const int StepAimRelease      = 5;
    private const int StepSteerRagdoll    = 6;
    private const int StepScoreObserve    = 7;
    private const int StepResetRun        = 8;

    // ═══════════════════════════════════════════════════════════════════════════
    #region Unity Lifecycle

    private void Awake()
    {
        // Use the existing FTUEOverlay if one was added in the Inspector; otherwise add one.
        overlay = GetComponent<FTUEOverlay>() ?? gameObject.AddComponent<FTUEOverlay>();
        overlay.Hide();

        // Build a default config if none assigned
        if (config == null)
            config = ScriptableObject.CreateInstance<FTUEStepConfig>();
    }

    private void Start()
    {
        if (carController == null)
            carController = FindFirstObjectByType<CarController>();

        if (carController == null)
        {
            Debug.LogError("[FTUE] CarController not found — tutorial disabled.");
            return;
        }

        SubscribeToEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Event Wiring

    private void SubscribeToEvents()
    {
        carController.OnPhaseChanged       += HandlePhaseChanged;
        carController.OnPullFractionUpdated += HandlePullFraction;
        carController.OnSteerInputChanged   += HandleSteerInput;
        carController.OnRagdollSteerChanged += HandleRagdollSteer;
        carController.OnCarReset            += HandleCarReset;
        carController.OnBecameAirborne      += HandleBecameAirborne;
        Target.OnAnyTargetHit               += HandleTargetHit;
    }

    private void UnsubscribeFromEvents()
    {
        if (carController == null) return;
        carController.OnPhaseChanged        -= HandlePhaseChanged;
        carController.OnPullFractionUpdated -= HandlePullFraction;
        carController.OnSteerInputChanged   -= HandleSteerInput;
        carController.OnRagdollSteerChanged -= HandleRagdollSteer;
        carController.OnCarReset            -= HandleCarReset;
        carController.OnBecameAirborne      -= HandleBecameAirborne;
        Target.OnAnyTargetHit               -= HandleTargetHit;
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Event Handlers

    private void HandlePhaseChanged(CarController.Phase prev, CarController.Phase next)
    {
        if (!isRunning) return;

        // Idle→Pulling: player tapped the car → step 0 complete
        if (currentStep == StepTapCar && prev == CarController.Phase.Idle && next == CarController.Phase.Pulling)
        {
            AdvanceStep();
            return;
        }

        // DragBack safety: player released before threshold but car still launched
        // (PullFraction > 2% but < dragBackThreshold) — skip Release hint, go to Steer
        if (currentStep == StepDragBack && prev == CarController.Phase.Pulling && next == CarController.Phase.Launched)
        {
            AdvanceToStep(StepSteerCar);
            return;
        }

        // DragBack safety: negligible pull snapped back to Idle — re-show drag hint
        if (currentStep == StepDragBack && prev == CarController.Phase.Pulling && next == CarController.Phase.Idle)
        {
            ShowStep(StepDragBack);
            return;
        }

        // Pulling→Launched: player released after reaching drag threshold → step 2 complete
        if (currentStep == StepRelease && prev == CarController.Phase.Pulling && next == CarController.Phase.Launched)
        {
            AdvanceStep();
            return;
        }

        // Release safety: negligible release went back to Idle — re-show release hint
        if (currentStep == StepRelease && prev == CarController.Phase.Pulling && next == CarController.Phase.Idle)
        {
            ShowStep(StepRelease);
            return;
        }

        // Launched→Aiming: player tapped car while airborne → step 4 complete
        if (currentStep == StepAirborneTapHold && prev == CarController.Phase.Launched && next == CarController.Phase.Aiming)
        {
            AdvanceStep();
            return;
        }

        // AirborneTapHold: car landed without tapping → loop back to steer / wait for next air
        if (currentStep == StepAirborneTapHold && next == CarController.Phase.Idle)
        {
            // Car stopped on the ground without going airborne — reset to Steer step
            hasSteppedToAirborne = false;
            AdvanceToStep(StepSteerCar);
            return;
        }

        // Aiming→Thrown: player released aim → step 5 complete
        if (currentStep == StepAimRelease && prev == CarController.Phase.Aiming && next == CarController.Phase.Thrown)
        {
            AdvanceStep();
            return;
        }

        // SteerCar: car stopped either by itself or after steering — step done
        if (currentStep == StepSteerCar && next == CarController.Phase.Idle)
        {
            AdvanceStep();
            return;
        }
    }

    private void HandlePullFraction(float fraction)
    {
        if (!isRunning) return;

        // DragBack: advance to Release hint once threshold reached
        if (currentStep == StepDragBack && !pullFractionReached && fraction >= dragBackThreshold)
        {
            pullFractionReached = true;
            AdvanceStep(); // → StepRelease
        }
    }

    private void HandleSteerInput(int dir)
    {
        if (!isRunning) return;

        if (currentStep == StepSteerCar && dir != 0)
        {
            steerCount++;
            if (steerCount >= steerInputsRequired)
                AdvanceStep(); // → StepAirborneTapHold (or skip if car not airborne yet)
        }
    }

    private void HandleBecameAirborne()
    {
        if (!isRunning) return;

        if (currentStep == StepSteerCar)
        {
            // Skip steer step early if car became airborne before threshold
            // (e.g. went off a ramp) — don't miss the airborne window
            hasSteppedToAirborne = false;
            steerCount = 0;
            AdvanceToStep(StepAirborneTapHold);
            return;
        }

        if (currentStep == StepAirborneTapHold && !hasSteppedToAirborne)
        {
            hasSteppedToAirborne = true;
            // Refresh prompt so player can see the instruction while still airborne
            ShowStep(StepAirborneTapHold);
        }
    }

    private void HandleRagdollSteer(int dir)
    {
        if (!isRunning) return;

        if (currentStep == StepSteerRagdoll && dir != 0)
        {
            ragdollSteerCount++;
            if (ragdollSteerCount >= ragdollSteerInputsRequired)
                AdvanceStep();
        }
    }

    private void HandleTargetHit(int score)
    {
        if (!isRunning) return;

        if (currentStep == StepScoreObserve)
        {
            ShowStep(StepScoreObserve); // show "nice hit" immediately then advance
            StartCoroutine(DelayedAdvance(2.2f));
        }
        else if (currentStep == StepSteerRagdoll)
        {
            // Ragdoll hit target without steering — still counts, advance both steps
            AdvanceToStep(StepScoreObserve);
            StartCoroutine(DelayedAdvance(2.2f));
        }
    }

    private void HandleCarReset()
    {
        if (!isRunning) return;

        if (currentStep == StepResetRun)
            CompleteFTUE();

        // If reset during earlier steps, restart tutorial from step 0
        else if (currentStep > StepTapCar)
        {
            StopAllCoroutines();
            ResetStepCounters();
            AdvanceToStep(StepTapCar);
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Step Management

    private void StartFTUE()
    {
        isRunning = true;
        ResetStepCounters();
        Debug.Log("[FTUE] Tutorial started.");
        AdvanceToStep(StepTapCar);
    }

    private void AdvanceStep()
    {
        AdvanceToStep(currentStep + 1);
    }

    private void AdvanceToStep(int step)
    {
        if (step >= config.steps.Length)
        {
            CompleteFTUE();
            return;
        }

        currentStep = step;
        ShowStep(step);
        Debug.Log($"[FTUE] Step {step}: {config.steps[step].id}");
    }

    private void ShowStep(int step)
    {
        StopFollowUpTimers();

        var s = config.steps[step];
        overlay.Show(s.promptText, s.handAnim,
                     s.overrideHandPosition, s.handScreenX, s.handScreenY,
                     !s.disableHandAnimation);

        if (s.followUpDelay > 0f && !string.IsNullOrEmpty(s.followUpText))
            followUpRoutine = StartCoroutine(FollowUpSequence(s));
    }

    private void CompleteFTUE()
    {
        isRunning = false;
        overlay.Hide();
        FTUEProgressStore.MarkCompleted();
        Debug.Log("[FTUE] Tutorial completed!");

        // Brief congratulatory flash
        StartCoroutine(FlashCompletion());
    }

    private void ResetStepCounters()
    {
        steerCount           = 0;
        ragdollSteerCount    = 0;
        hasSteppedToAirborne = false;
        pullFractionReached  = false;
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Follow-up / Delay Coroutines

    private IEnumerator FollowUpSequence(FTUEStepConfig.Step step)
    {
        // First follow-up
        yield return new WaitForSecondsRealtime(step.followUpDelay);
        if (isRunning && currentStep == config.steps.IndexOf(step))
            overlay.ShowHint(step.followUpText);

        // Second follow-up (if configured)
        if (step.secondFollowUpDelay > 0f && !string.IsNullOrEmpty(step.secondFollowUpText))
        {
            yield return new WaitForSecondsRealtime(step.secondFollowUpDelay);
            if (isRunning && currentStep == config.steps.IndexOf(step))
                overlay.ShowHint(step.secondFollowUpText);
        }
    }

    private IEnumerator DelayedAdvance(float realSeconds)
    {
        yield return new WaitForSecondsRealtime(realSeconds);
        if (isRunning) AdvanceStep();
    }

    private IEnumerator FlashCompletion()
    {
        overlay.Show("TUTORIAL COMPLETE! \nNow you know the ropes!", FTUEStepConfig.HandAnim.Celebrate);
        yield return new WaitForSecondsRealtime(2.8f);
        overlay.Hide();
    }

    private void StopFollowUpTimers()
    {
        if (followUpRoutine != null)
        {
            StopCoroutine(followUpRoutine);
            followUpRoutine = null;
        }
    }

    #endregion
}

// ── Extension helper — IndexOf on arrays ─────────────────────────────────────
internal static class ArrayExtensions
{
    internal static int IndexOf<T>(this T[] arr, T item)
    {
        for (int i = 0; i < arr.Length; i++)
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(arr[i], item)) return i;
        return -1;
    }
}
