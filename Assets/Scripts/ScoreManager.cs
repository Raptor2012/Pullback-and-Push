using UnityEngine;

/// <summary>
/// Aggregates scores across rounds, tracks high score, and persists via SaveSystem.
/// Attach to a dedicated "ScoreManager" GameObject in the scene.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    public static ScoreManager Instance { get; private set; }

    // ── Public State ──────────────────────────────────────────────────────────
    public int CurrentRunScore { get; private set; }
    public int CurrentRoundScore { get; private set; }
    public int HighScore { get; private set; }
    public int RoundNumber { get; private set; } = 1;

    // ── Events ────────────────────────────────────────────────────────────────
    /// <summary>Fired when the score changes. Arg: new total run score.</summary>
    public event System.Action<int> OnScoreChanged;

    /// <summary>Fired when a new high score is set. Arg: new high score.</summary>
    public event System.Action<int> OnHighScoreBeaten;

    /// <summary>Fired when the round number changes. Arg: new round number.</summary>
    public event System.Action<int> OnRoundChanged;

    // ── Internals ─────────────────────────────────────────────────────────────
    private SaveSystem.SaveData saveData;
    private bool isDirty;
    private CarController carController;

    // ═══════════════════════════════════════════════════════════════════════════
    #region Unity Lifecycle

    private void Awake()
    {
        // Simple singleton (single-scene game — no DontDestroyOnLoad needed)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Load persisted data
        saveData = SaveSystem.Load();
        HighScore = saveData.highScore;
    }

    private void Start()
    {
        carController = FindFirstObjectByType<CarController>();
        SubscribeToEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        if (isDirty) PersistSave();
        if (Instance == this) Instance = null;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && isDirty) PersistSave();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Public API

    /// <summary>
    /// Reset for a brand new game (called from MenuController on Play).
    /// Does NOT reset high score.
    /// </summary>
    public void ResetRun()
    {
        CurrentRunScore = 0;
        CurrentRoundScore = 0;
        RoundNumber = 1;
        OnScoreChanged?.Invoke(0);
        OnRoundChanged?.Invoke(1);
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Event Wiring

    private void SubscribeToEvents()
    {
        Target.OnAnyTargetHit += HandleTargetHit;
        if (carController != null)
            carController.OnCarReset += HandleCarReset;
    }

    private void UnsubscribeFromEvents()
    {
        Target.OnAnyTargetHit -= HandleTargetHit;
        if (carController != null)
            carController.OnCarReset -= HandleCarReset;
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Event Handlers

    private void HandleTargetHit(int score)
    {
        CurrentRoundScore += score;
        CurrentRunScore += score;
        OnScoreChanged?.Invoke(CurrentRunScore);

        // Check high score
        if (CurrentRunScore > HighScore)
        {
            HighScore = CurrentRunScore;
            saveData.highScore = HighScore;
            isDirty = true;
            OnHighScoreBeaten?.Invoke(HighScore);
        }

        // Track best single-round score
        if (CurrentRoundScore > saveData.bestRoundScore)
        {
            saveData.bestRoundScore = CurrentRoundScore;
            isDirty = true;
        }
    }

    private void HandleCarReset()
    {
        // Finalize round
        saveData.totalRuns++;
        isDirty = true;

        // Start new round
        CurrentRoundScore = 0;
        RoundNumber++;
        OnRoundChanged?.Invoke(RoundNumber);

        // Persist periodically (every reset)
        PersistSave();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Persistence

    private void PersistSave()
    {
        SaveSystem.Save(saveData);
        isDirty = false;
    }

    #endregion
}
