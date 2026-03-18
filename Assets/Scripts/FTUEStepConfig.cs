using UnityEngine;

/// <summary>
/// ScriptableObject data container for all FTUE tutorial steps.
/// Create via Assets ▸ Create ▸ FTUE ▸ Step Config.
/// Each entry maps to one gameplay instruction screen.
/// </summary>
[CreateAssetMenu(menuName = "FTUE/Step Config", fileName = "FTUEStepConfig")]
public class FTUEStepConfig : ScriptableObject
{
    // ── Hand animation types ──────────────────────────────────────────────────
    public enum HandAnim
    {
        TapPulse,      // pulsing circle — "tap here"
        DragDown,      // finger sliding downward (pull back)
        Release,       // shrinking circle — "let go"
        SteerLeft,     // arrow pointing left half of screen
        SteerRight,    // arrow pointing right half of screen
        SteerBoth,     // left+right arrows together       
        TapHold,       // growing hold circle then shrink
        AimRelease,    // oscillating vertical line then cut
        SteerRagdoll,  // alternate left/right arrows
        Celebrate,     // starburst/radiate
        TapButton,     // tap pointing at reset button
    }

    // ── Single step definition ────────────────────────────────────────────────
    [System.Serializable]
    public class Step
    {
        [Tooltip("Unique identifier used in logs.")]
        public string id;

        [Tooltip("Main instruction shown immediately.")]
        [TextArea(1, 3)]
        public string promptText;

        [Tooltip("Which hand animation type to run.")]
        public HandAnim handAnim;

        [Tooltip("Disable the hand animation for this step (prompt text still shows).")]
        public bool disableHandAnimation = false;

        [Tooltip("Seconds before the follow-up hint appears (0 = never).")]
        public float followUpDelay = 3.0f;

        [Tooltip("Follow-up hint shown when player hasn't progressed.")]
        [TextArea(1, 2)]
        public string followUpText;

        [Tooltip("Seconds before a second (stronger) follow-up hint appears (0 = skip).")]
        public float secondFollowUpDelay = 5.0f;

        [Tooltip("Second follow-up hint text.")]
        [TextArea(1, 2)]
        public string secondFollowUpText;

        [Header("Hand Position Override")]
        [Tooltip("Enable to place the hand icon at a fixed screen position instead of above the panel.")]
        public bool overrideHandPosition = false;

        [Tooltip("Horizontal screen position (0 = left, 1 = right) when override is enabled.")]
        [Range(0f, 1f)]
        public float handScreenX = 0.5f;

        [Tooltip("Vertical screen position (0 = bottom, 1 = top) when override is enabled.")]
        [Range(0f, 1f)]
        public float handScreenY = 0.5f;
    }

    // ── Step list (ordered) ───────────────────────────────────────────────────
    [Tooltip("All FTUE steps in the order they are presented to the player.")]
    public Step[] steps = new Step[]
    {
        new Step
        {
            id                 = "TapCar",
            promptText         = "TAP THE CAR",
            handAnim           = HandAnim.TapPulse,
            followUpDelay      = 2.5f,
            followUpText       = "Tap directly on the car to grab it!",
            secondFollowUpDelay = 4f,
            secondFollowUpText  = "Touch the orange car to start!"
        },
        new Step
        {
            id                 = "DragBack",
            promptText         = "DRAG BACK TO WIND UP",
            handAnim           = HandAnim.DragDown,
            followUpDelay      = 2.5f,
            followUpText       = "Keep dragging backward — feel the tension!",
            secondFollowUpDelay = 4f,
            secondFollowUpText  = "Drag further back for more power!"
        },
        new Step
        {
            id                 = "Release",
            promptText         = "RELEASE TO LAUNCH!",
            handAnim           = HandAnim.Release,
            followUpDelay      = 2.0f,
            followUpText       = "Lift your finger to fire the car forward!",
            secondFollowUpDelay = 0f,
            secondFollowUpText  = ""
        },
        new Step
        {
            id                 = "SteerCar",
            promptText         = "STEER LEFT  /  RIGHT",
            handAnim           = HandAnim.SteerBoth,
            followUpDelay      = 3.0f,
            followUpText       = "Tap and hold the left or right side of the screen!",
            secondFollowUpDelay = 5f,
            secondFollowUpText  = "Hold left or right side to turn the car!"
        },
        new Step
        {
            id                 = "AirborneTap",
            promptText         = "CAR AIRBORNE — TAP & HOLD IT",
            handAnim           = HandAnim.TapHold,
            followUpDelay      = 2.0f,
            followUpText       = "When the car leaves the ramp, tap and HOLD it!",
            secondFollowUpDelay = 0f,
            secondFollowUpText  = ""
        },
        new Step
        {
            id                 = "AimRelease",
            promptText         = "AIM THEN RELEASE AT FULL POWER",
            handAnim           = HandAnim.AimRelease,
            followUpDelay      = 0f,
            followUpText       = "",
            secondFollowUpDelay = 0f,
            secondFollowUpText  = ""
        },
        new Step
        {
            id                 = "SteerRagdoll",
            promptText         = "STEER THE RAGDOLL!",
            handAnim           = HandAnim.SteerRagdoll,
            followUpDelay      = 2.5f,
            followUpText       = "Tap left or right side of screen to steer the character!",
            secondFollowUpDelay = 0f,
            secondFollowUpText  = ""
        },
        new Step
        {
            id                 = "ScoreObserve",
            promptText         = "NICE HIT — THAT\'S YOUR SCORE!",
            handAnim           = HandAnim.Celebrate,
            followUpDelay      = 0f,
            followUpText       = "",
            secondFollowUpDelay = 0f,
            secondFollowUpText  = ""
        },
        new Step
        {
            id                 = "ResetRun",
            promptText         = "TAP RESET TO PLAY AGAIN",
            handAnim           = HandAnim.TapButton,
            followUpDelay      = 3.0f,
            followUpText       = "Hit the reset button to go again!",
            secondFollowUpDelay = 0f,
            secondFollowUpText  = ""
        },
    };
}
