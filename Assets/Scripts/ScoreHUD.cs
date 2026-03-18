using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Displays current score, high score, and round number.
/// Attach to a Screen Space Overlay Canvas named "ScoreHUD".
/// </summary>
public class ScoreHUD : MonoBehaviour
{
    [Header("Text References")]
    [SerializeField] private TextMeshProUGUI currentScoreText;
    [SerializeField] private TextMeshProUGUI highScoreText;
    [SerializeField] private TextMeshProUGUI roundText;

    [Header("New Best")]
    [SerializeField] private GameObject newBestLabel;
    [SerializeField] private float newBestFlashDuration = 2f;

    [Header("Animation")]
    [SerializeField] private float scorePunchScale = 1.3f;
    [SerializeField] private float scorePunchDuration = 0.2f;

    [Header("Visibility")]
    [SerializeField] private CanvasGroup hudCanvasGroup;
    [SerializeField] private float fadeInDuration = 0.4f;

    private ScoreManager scoreManager;
    private Coroutine punchRoutine;
    private Coroutine newBestRoutine;
    private Vector3 scoreBaseScale;
    private bool isVisible;

    // ═══════════════════════════════════════════════════════════════════════════
    #region Unity Lifecycle

    private void Awake()
    {
        if (currentScoreText != null)
            scoreBaseScale = currentScoreText.transform.localScale;

        if (newBestLabel != null)
            newBestLabel.SetActive(false);

        // Start hidden
        if (hudCanvasGroup != null)
            hudCanvasGroup.alpha = 0f;
    }

    private void Start()
    {
        scoreManager = ScoreManager.Instance;
        if (scoreManager == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[ScoreHUD] ScoreManager not found!", this);
#endif
            return;
        }

        // Initialize display
        UpdateScoreDisplay(scoreManager.CurrentRunScore);
        UpdateHighScoreDisplay(scoreManager.HighScore);
        UpdateRoundDisplay(scoreManager.RoundNumber);

        // Subscribe
        scoreManager.OnScoreChanged += HandleScoreChanged;
        scoreManager.OnHighScoreBeaten += HandleHighScoreBeaten;
        scoreManager.OnRoundChanged += HandleRoundChanged;
    }

    private void OnDestroy()
    {
        if (scoreManager != null)
        {
            scoreManager.OnScoreChanged -= HandleScoreChanged;
            scoreManager.OnHighScoreBeaten -= HandleHighScoreBeaten;
            scoreManager.OnRoundChanged -= HandleRoundChanged;
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Public API

    /// <summary>Fade in the HUD. Call when gameplay starts.</summary>
    public void Show()
    {
        if (isVisible) return;
        isVisible = true;
        StartCoroutine(FadeIn());
    }

    /// <summary>Hide immediately.</summary>
    public void Hide()
    {
        isVisible = false;
        if (hudCanvasGroup != null)
            hudCanvasGroup.alpha = 0f;
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Event Handlers

    private void HandleScoreChanged(int newTotal)
    {
        UpdateScoreDisplay(newTotal);

        // Punch animation
        if (punchRoutine != null) StopCoroutine(punchRoutine);
        punchRoutine = StartCoroutine(ScorePunch());
    }

    private void HandleHighScoreBeaten(int newHigh)
    {
        UpdateHighScoreDisplay(newHigh);

        // Flash "NEW BEST!" label
        if (newBestLabel != null)
        {
            if (newBestRoutine != null) StopCoroutine(newBestRoutine);
            newBestRoutine = StartCoroutine(FlashNewBest());
        }
    }

    private void HandleRoundChanged(int newRound)
    {
        UpdateRoundDisplay(newRound);
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Display Updates

    private void UpdateScoreDisplay(int score)
    {
        if (currentScoreText != null)
            currentScoreText.text = score.ToString();
    }

    private void UpdateHighScoreDisplay(int highScore)
    {
        if (highScoreText != null)
            highScoreText.text = $"BEST: {highScore}";
    }

    private void UpdateRoundDisplay(int round)
    {
        if (roundText != null)
            roundText.text = $"ROUND {round}";
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Animations

    private IEnumerator ScorePunch()
    {
        if (currentScoreText == null) yield break;

        Transform t = currentScoreText.transform;
        Vector3 bigScale = scoreBaseScale * scorePunchScale;
        float elapsed = 0f;
        float halfDuration = scorePunchDuration * 0.5f;

        // Scale up
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float frac = elapsed / halfDuration;
            t.localScale = Vector3.Lerp(scoreBaseScale, bigScale, frac);
            yield return null;
        }

        // Scale down
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float frac = elapsed / halfDuration;
            t.localScale = Vector3.Lerp(bigScale, scoreBaseScale, frac);
            yield return null;
        }

        t.localScale = scoreBaseScale;
        punchRoutine = null;
    }

    private IEnumerator FlashNewBest()
    {
        newBestLabel.SetActive(true);

        CanvasGroup group = newBestLabel.GetComponent<CanvasGroup>();
        if (group == null) group = newBestLabel.AddComponent<CanvasGroup>();

        // Fade in
        float elapsed = 0f;
        while (elapsed < 0.2f)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = elapsed / 0.2f;
            yield return null;
        }
        group.alpha = 1f;

        // Hold
        yield return new WaitForSecondsRealtime(newBestFlashDuration - 0.4f);

        // Fade out
        elapsed = 0f;
        while (elapsed < 0.2f)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = 1f - (elapsed / 0.2f);
            yield return null;
        }

        newBestLabel.SetActive(false);
        newBestRoutine = null;
    }

    private IEnumerator FadeIn()
    {
        if (hudCanvasGroup == null) yield break;

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            hudCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }
        hudCanvasGroup.alpha = 1f;
    }

    #endregion
}
