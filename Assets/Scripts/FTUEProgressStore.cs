using UnityEngine;

/// <summary>
/// Thin static wrapper around PlayerPrefs for FTUE completion state.
/// All keys are versioned so a game update can re-trigger the tutorial.
/// </summary>
public static class FTUEProgressStore
{
    private const string CompletionKey = "ftue_completed_v1";

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>True after the player has finished the full FTUE flow at least once.</summary>
    public static bool IsCompleted => PlayerPrefs.GetInt(CompletionKey, 0) == 1;

    /// <summary>Persist FTUE as completed. Call once when the last step is acknowledged.</summary>
    public static void MarkCompleted()
    {
        PlayerPrefs.SetInt(CompletionKey, 1);
        PlayerPrefs.Save();
        Debug.Log("[FTUE] Tutorial marked as completed.");
    }

    /// <summary>
    /// Clears the completion flag so the tutorial will run again on next session start.
    /// Call from a dev-menu or cheat code.
    /// </summary>
    public static void Reset()
    {
        PlayerPrefs.DeleteKey(CompletionKey);
        PlayerPrefs.Save();
        Debug.Log("[FTUE] Tutorial completion reset.");
    }
}
