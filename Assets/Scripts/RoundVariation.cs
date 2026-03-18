using UnityEngine;

/// <summary>
/// Defines the parameter ranges for randomized round generation.
/// Create via Assets > Create > Pullback Fight > Round Variation.
/// </summary>
[CreateAssetMenu(fileName = "DefaultVariation", menuName = "Pullback Fight/Round Variation")]
public class RoundVariation : ScriptableObject
{
    [Header("Target Position Offsets (from base position)")]
    [Tooltip("Min/max lateral (X) offset from the target's base position.")]
    public Vector2 targetLateralRange = new Vector2(-3f, 3f);

    [Tooltip("Min/max forward (Z) offset — positive = farther from car start.")]
    public Vector2 targetDistanceRange = new Vector2(-2f, 5f);

    [Tooltip("Min/max height (Y) offset.")]
    public Vector2 targetHeightRange = new Vector2(0f, 2f);

    [Header("Ramp Modifications")]
    [Tooltip("Min/max scale multiplier on the ramp's Z (length) axis.")]
    public Vector2 rampLengthScaleRange = new Vector2(0.8f, 1.3f);

    [Tooltip("Min/max X-rotation offset for ramp steepness (degrees).")]
    public Vector2 rampAngleRange = new Vector2(-5f, 10f);

    [Header("Car Tuning Overrides")]
    [Tooltip("Min/max launch force override.")]
    public Vector2 launchForceRange = new Vector2(40f, 65f);

    [Tooltip("Min/max steer speed override.")]
    public Vector2 steerSpeedRange = new Vector2(90f, 140f);

    [Header("Difficulty Curve")]
    [Tooltip("Round at which difficulty reaches maximum (100%).")]
    public int maxDifficultyRound = 12;

    /// <summary>
    /// Returns a difficulty factor 0–1 based on round number.
    /// Round 1 = 0 (easiest), maxDifficultyRound+ = 1 (hardest).
    /// </summary>
    public float GetDifficulty(int round)
    {
        return Mathf.Clamp01((float)(round - 1) / Mathf.Max(1, maxDifficultyRound - 1));
    }
}
