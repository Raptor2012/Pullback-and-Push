using System.Collections;
using UnityEngine;

/// <summary>
/// Randomizes gameplay parameters each round for variety and progressive difficulty.
/// Subscribes to CarController.OnCarReset to trigger variation changes.
/// </summary>
public class RoundManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Auto-found if null.")]
    [SerializeField] private CarController carController;

    [Tooltip("The target transform to reposition each round.")]
    [SerializeField] private Transform targetTransform;

    [Tooltip("The ramp transform to resize/rotate each round.")]
    [SerializeField] private Transform rampTransform;

    [Tooltip("Variation config asset. Create via Assets > Create > Pullback Fight > Round Variation.")]
    [SerializeField] private RoundVariation variation;

    [Header("Animation")]
    [Tooltip("Seconds to lerp the target to its new position between rounds.")]
    [SerializeField] private float targetMoveDuration = 0.6f;

    [Tooltip("Seconds to lerp the ramp to its new shape between rounds.")]
    [SerializeField] private float rampTransitionDuration = 0.4f;

    // ── Base transforms (captured at start) ───────────────────────────────────
    private Vector3    targetBasePosition;
    private Quaternion targetBaseRotation;
    private Vector3    rampBasePosition;
    private Quaternion rampBaseRotation;
    private Vector3    rampBaseScale;

    private ScoreManager scoreManager;
    private int lastAppliedRound = -1;
    private Coroutine moveTargetRoutine;
    private Coroutine moveRampRoutine;

    // ═══════════════════════════════════════════════════════════════════════════
    #region Unity Lifecycle

    private void Awake()
    {
        // Auto-find references
        if (carController == null)
            carController = FindFirstObjectByType<CarController>();

        if (targetTransform == null)
        {
            Target t = FindFirstObjectByType<Target>();
            if (t != null) targetTransform = t.transform;
        }

        // Cache base transforms
        if (targetTransform != null)
        {
            targetBasePosition = targetTransform.position;
            targetBaseRotation = targetTransform.rotation;
        }

        if (rampTransform != null)
        {
            rampBasePosition = rampTransform.position;
            rampBaseRotation = rampTransform.rotation;
            rampBaseScale = rampTransform.localScale;
        }
    }

    private void Start()
    {
        scoreManager = ScoreManager.Instance;
    }

    private void OnEnable()
    {
        if (carController != null)
            carController.OnCarReset += HandleCarReset;
    }

    private void OnDisable()
    {
        if (carController != null)
            carController.OnCarReset -= HandleCarReset;
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Event Handler

    private void HandleCarReset()
    {
        int currentRound = scoreManager != null ? scoreManager.RoundNumber : 1;

        // Prevent double-application on the same round
        if (currentRound == lastAppliedRound) return;
        lastAppliedRound = currentRound;

        // Skip variation on round 1 (use base/default layout)
        if (currentRound <= 1) return;

        if (variation == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[RoundManager] No RoundVariation asset assigned. Skipping variation.", this);
#endif
            return;
        }

        ApplyVariation(currentRound);
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Variation Logic

    private void ApplyVariation(int round)
    {
        float difficulty = variation.GetDifficulty(round);

        // ── Target position ───────────────────────────────────────────────────
        if (targetTransform != null)
        {
            // Scale randomization range by difficulty (easy rounds = small offsets)
            float lateralSpread  = Mathf.Lerp(0f, 1f, difficulty);
            float distanceSpread = difficulty;
            float heightSpread   = Mathf.Lerp(0f, 1f, difficulty * difficulty); // height ramps up slower

            float lateralOffset = Random.Range(
                variation.targetLateralRange.x * lateralSpread,
                variation.targetLateralRange.y * lateralSpread);

            float distanceOffset = Mathf.Lerp(
                variation.targetDistanceRange.x,
                variation.targetDistanceRange.y,
                distanceSpread) + Random.Range(-1f, 1f) * (1f - difficulty) * 0.5f;

            float heightOffset = Random.Range(0f, variation.targetHeightRange.y * heightSpread);

            Vector3 newTargetPos = targetBasePosition
                + Vector3.right   * lateralOffset
                + Vector3.forward * distanceOffset
                + Vector3.up      * heightOffset;

            // Animate movement
            if (moveTargetRoutine != null) StopCoroutine(moveTargetRoutine);
            moveTargetRoutine = StartCoroutine(
                AnimateTransformMove(targetTransform, newTargetPos, targetMoveDuration));
        }

        // ── Ramp shape ────────────────────────────────────────────────────────
        if (rampTransform != null)
        {
            // Shorter ramp at higher difficulty
            float lengthScale = Mathf.Lerp(
                variation.rampLengthScaleRange.y,   // longer (easier) when low difficulty
                variation.rampLengthScaleRange.x,   // shorter (harder) when high difficulty
                difficulty);

            // Add slight randomness
            lengthScale += Random.Range(-0.05f, 0.05f);

            Vector3 newScale  = rampBaseScale;
            newScale.z       *= lengthScale;

            // Steeper ramp at higher difficulty
            float angleOffset = Mathf.Lerp(
                variation.rampAngleRange.x,
                variation.rampAngleRange.y,
                difficulty) + Random.Range(-2f, 2f);

            Quaternion newRot = rampBaseRotation * Quaternion.Euler(angleOffset, 0f, 0f);

            // Animate ramp
            if (moveRampRoutine != null) StopCoroutine(moveRampRoutine);
            moveRampRoutine = StartCoroutine(
                AnimateRampChange(rampTransform, rampBasePosition, newRot, newScale, rampTransitionDuration));
        }

        // ── Car tuning ───────────────────────────────────────────────────────
        if (carController != null)
        {
            // Reduce force at higher difficulty
            float launchForce = Mathf.Lerp(
                variation.launchForceRange.y,    // higher force (easier)
                variation.launchForceRange.x,    // lower force (harder)
                difficulty);
            carController.SetMaxLaunchForce(launchForce);

            // Vary steer speed (mostly cosmetic — keep close to base)
            float steerSpeed = Mathf.Lerp(
                variation.steerSpeedRange.y,
                variation.steerSpeedRange.x,
                difficulty * 0.5f); // only shift steer speed by half the difficulty
            carController.SetSteerSpeed(steerSpeed);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[RoundManager] Round {round}: difficulty={difficulty:F2}");
#endif
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Animation Coroutines

    private IEnumerator AnimateTransformMove(Transform t, Vector3 targetPos, float duration)
    {
        Vector3 startPos = t.position;
        float   elapsed  = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float frac = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            t.position = Vector3.Lerp(startPos, targetPos, frac);
            yield return null;
        }

        t.position      = targetPos;
        moveTargetRoutine = null;
    }

    private IEnumerator AnimateRampChange(
        Transform t, Vector3 targetPos, Quaternion targetRot, Vector3 targetScale, float duration)
    {
        Vector3    startPos   = t.position;
        Quaternion startRot   = t.rotation;
        Vector3    startScale = t.localScale;
        float      elapsed    = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float frac = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            t.position   = Vector3.Lerp(startPos, targetPos, frac);
            t.rotation   = Quaternion.Slerp(startRot, targetRot, frac);
            t.localScale = Vector3.Lerp(startScale, targetScale, frac);
            yield return null;
        }

        t.position    = targetPos;
        t.rotation    = targetRot;
        t.localScale  = targetScale;
        moveRampRoutine = null;
    }

    #endregion
}
