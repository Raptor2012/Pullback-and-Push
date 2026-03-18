using System.Collections;
using UnityEngine;

public class MenuController : MonoBehaviour
{
    [Header("Gameplay References")]
    [SerializeField] private CarController carController;
    [SerializeField] private GameObject startUI;
    [SerializeField] private GameObject resetButton;

    [Header("FTUE")]
    [Tooltip("Auto-found at runtime if not assigned.")]
    [SerializeField] private FTUEManager ftueManager;

    [Header("Menu UI Elements")]
    [SerializeField] private RectTransform titleRect;
    [SerializeField] private RectTransform playButtonRect;
    [SerializeField] private RectTransform resetButtonRect;

    [Header("Audio")]
    [SerializeField] private AudioSource menuSfxSource;
    [SerializeField] private AudioClip titleAppearClip;
    [SerializeField] private AudioClip playAppearClip;
    [SerializeField] private AudioClip playPressClip;
    [SerializeField] private AudioClip titleDisappearClip;
    [SerializeField] private AudioClip playDisappearClip;
    [SerializeField] private AudioClip resetAppearClip;

    [Header("Appear Animation")]
    [SerializeField] private float titleAppearDuration = 0.50f;
    [SerializeField] private float playAppearDuration = 0.42f;
    [SerializeField] private float playAppearDelay = 0.10f;
    [SerializeField] private float titleAppearYOffset = 80f;
    [SerializeField] private float playAppearYOffset = -50f;

    [Header("Idle Animation")]
    [SerializeField] private float titleIdleScaleAmplitude = 0.03f;
    [SerializeField] private float titleIdleScaleSpeed = 1.5f;
    [SerializeField] private float titleIdleTiltAmplitude = 2.5f;
    [SerializeField] private float titleIdleTiltSpeed = 1.2f;
    [SerializeField] private float playIdlePulseAmplitude = 0.07f;
    [SerializeField] private float playIdlePulseSpeed = 2.6f;
    [SerializeField] private float playIdleBobAmplitude = 8f;
    [SerializeField] private float playIdleBobSpeed = 2.2f;

    [Header("Disappear Animation")]
    [SerializeField] private float titleDisappearDuration = 0.34f;
    [SerializeField] private float playDisappearDuration = 0.30f;
    [SerializeField] private float titleDisappearYOffset = 90f;
    [SerializeField] private float playDisappearYOffset = -120f;
    [SerializeField] private float titleDisappearScaleBoost = 0.12f;

    [Header("Reset Button Reveal")]
    [SerializeField] private float resetRevealDuration = 0.32f;
    [SerializeField] private float resetRevealYOffset = -35f;

    private CanvasGroup titleCanvasGroup;
    private CanvasGroup playCanvasGroup;
    private CanvasGroup resetCanvasGroup;

    private Vector2 titleBasePos;
    private Vector2 playBasePos;
    private Vector2 resetBasePos;
    private Vector3 titleBaseScale;
    private Vector3 playBaseScale;
    private Vector3 resetBaseScale;

    private Coroutine idleRoutine;
    private bool isStarting;

    private void Awake()
    {
        AutoAssignReferences();

        titleCanvasGroup = EnsureCanvasGroup(titleRect);
        playCanvasGroup = EnsureCanvasGroup(playButtonRect);
        resetCanvasGroup = EnsureCanvasGroup(resetButtonRect);

        CacheBaseState();
        PrepareInitialState();
    }

    private void Start()
    {
        StartCoroutine(IntroSequence());
    }

    /// <summary>Called from a "Replay Tutorial" UI button.</summary>
    public void ReplayTutorial()
    {
        if (ftueManager != null)
            ftueManager.ForceRestart();
    }

    public void StartGame()
    {
        if (isStarting)
            return;

        StartCoroutine(StartGameSequence());
    }

    private IEnumerator IntroSequence()
    {
        yield return AnimateAppear(
            titleRect,
            titleCanvasGroup,
            titleBasePos + Vector2.up * titleAppearYOffset,
            titleBaseScale * 0.85f,
            titleBasePos,
            titleBaseScale,
            titleAppearDuration,
            titleAppearClip);

        if (playAppearDelay > 0f)
            yield return new WaitForSecondsRealtime(playAppearDelay);

        yield return AnimateAppear(
            playButtonRect,
            playCanvasGroup,
            playBasePos + Vector2.up * playAppearYOffset,
            playBaseScale * 0.75f,
            playBasePos,
            playBaseScale,
            playAppearDuration,
            playAppearClip);

        idleRoutine = StartCoroutine(IdleLoop());
    }

    private IEnumerator StartGameSequence()
    {
        isStarting = true;
        PlaySfx(playPressClip);

        if (idleRoutine != null)
        {
            StopCoroutine(idleRoutine);
            idleRoutine = null;
        }

        float waitDuration = Mathf.Max(titleDisappearDuration, playDisappearDuration);

        StartCoroutine(AnimateTitleDisappear());
        StartCoroutine(AnimatePlayDisappear());
        yield return new WaitForSecondsRealtime(waitDuration);

        if (startUI != null)
            startUI.SetActive(false);

        if (carController != null)
            carController.enabled = true;

        // Reset score for new game
        if (ScoreManager.Instance != null) ScoreManager.Instance.ResetRun();

        // Show score HUD
        ScoreHUD scoreHUD = FindFirstObjectByType<ScoreHUD>();
        if (scoreHUD != null) scoreHUD.Show();

        if (resetButton != null)
            resetButton.SetActive(true);

        yield return AnimateResetReveal();

        // Start tutorial for first-time players (no-op if already completed)
        ftueManager?.TryStart();
    }

    private IEnumerator IdleLoop()
    {
        float elapsed = 0f;

        while (!isStarting)
        {
            elapsed += Time.unscaledDeltaTime;

            if (titleRect != null)
            {
                float titleScaleWave = Mathf.Sin(elapsed * titleIdleScaleSpeed) * titleIdleScaleAmplitude;
                float titleTiltWave = Mathf.Sin(elapsed * titleIdleTiltSpeed) * titleIdleTiltAmplitude;

                titleRect.localScale = titleBaseScale * (1f + titleScaleWave);
                titleRect.localRotation = Quaternion.Euler(0f, 0f, titleTiltWave);
            }

            if (playButtonRect != null)
            {
                float pulse = (Mathf.Sin(elapsed * playIdlePulseSpeed) * 0.5f) + 0.5f;
                float bob = Mathf.Sin(elapsed * playIdleBobSpeed) * playIdleBobAmplitude;
                float scale = 1f + playIdlePulseAmplitude * pulse;

                playButtonRect.localScale = playBaseScale * scale;
                playButtonRect.anchoredPosition = playBasePos + Vector2.up * bob;
            }

            yield return null;
        }
    }

    private IEnumerator AnimateTitleDisappear()
    {
        if (titleRect == null)
            yield break;

        PlaySfx(titleDisappearClip);

        Vector2 startPos = titleRect.anchoredPosition;
        Vector2 endPos = titleBasePos + Vector2.up * titleDisappearYOffset;
        Vector3 startScale = titleRect.localScale;
        Vector3 endScale = titleBaseScale * (1f + titleDisappearScaleBoost);

        float elapsed = 0f;
        while (elapsed < titleDisappearDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / titleDisappearDuration);
            float eased = EaseInCubic(t);

            titleRect.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, eased);
            titleRect.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);

            if (titleCanvasGroup != null)
                titleCanvasGroup.alpha = 1f - t;

            yield return null;
        }
    }

    private IEnumerator AnimatePlayDisappear()
    {
        if (playButtonRect == null)
            yield break;

        PlaySfx(playDisappearClip);

        Vector2 startPos = playButtonRect.anchoredPosition;
        Vector2 endPos = playBasePos + Vector2.up * playDisappearYOffset;
        Vector3 startScale = playButtonRect.localScale;

        float elapsed = 0f;
        while (elapsed < playDisappearDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / playDisappearDuration);
            float eased = EaseInBack(t);

            playButtonRect.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, eased);
            playButtonRect.localScale = Vector3.LerpUnclamped(startScale, playBaseScale * 0.45f, eased);
            playButtonRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, -24f, eased));

            if (playCanvasGroup != null)
                playCanvasGroup.alpha = 1f - t;

            yield return null;
        }
    }

    private IEnumerator AnimateResetReveal()
    {
        if (resetButtonRect == null)
            yield break;

        PlaySfx(resetAppearClip);

        Vector2 startPos = resetBasePos + Vector2.up * resetRevealYOffset;
        Vector2 endPos = resetBasePos;
        Vector3 startScale = resetBaseScale * 0.5f;

        resetButtonRect.anchoredPosition = startPos;
        resetButtonRect.localScale = startScale;
        resetButtonRect.localRotation = Quaternion.identity;

        if (resetCanvasGroup != null)
            resetCanvasGroup.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < resetRevealDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / resetRevealDuration);
            float eased = EaseOutBack(t);

            resetButtonRect.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, eased);
            resetButtonRect.localScale = Vector3.LerpUnclamped(startScale, resetBaseScale, eased);

            if (resetCanvasGroup != null)
                resetCanvasGroup.alpha = t;

            yield return null;
        }

        resetButtonRect.anchoredPosition = endPos;
        resetButtonRect.localScale = resetBaseScale;
        if (resetCanvasGroup != null)
            resetCanvasGroup.alpha = 1f;
    }

    private IEnumerator AnimateAppear(
        RectTransform rect,
        CanvasGroup group,
        Vector2 fromPos,
        Vector3 fromScale,
        Vector2 toPos,
        Vector3 toScale,
        float duration,
        AudioClip sfx)
    {
        if (rect == null)
            yield break;

        PlaySfx(sfx);

        rect.anchoredPosition = fromPos;
        rect.localScale = fromScale;
        rect.localRotation = Quaternion.identity;
        if (group != null)
            group.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutBack(t);

            rect.anchoredPosition = Vector2.LerpUnclamped(fromPos, toPos, eased);
            rect.localScale = Vector3.LerpUnclamped(fromScale, toScale, eased);
            if (group != null)
                group.alpha = t;

            yield return null;
        }

        rect.anchoredPosition = toPos;
        rect.localScale = toScale;
        if (group != null)
            group.alpha = 1f;
    }

    private void PrepareInitialState()
    {
        if (carController != null)
            carController.enabled = false;

        if (startUI != null)
            startUI.SetActive(true);

        if (resetButton != null)
            resetButton.SetActive(false);

        if (titleRect != null)
        {
            titleRect.anchoredPosition = titleBasePos + Vector2.up * titleAppearYOffset;
            titleRect.localScale = titleBaseScale * 0.85f;
            titleRect.localRotation = Quaternion.identity;
        }

        if (playButtonRect != null)
        {
            playButtonRect.anchoredPosition = playBasePos + Vector2.up * playAppearYOffset;
            playButtonRect.localScale = playBaseScale * 0.75f;
            playButtonRect.localRotation = Quaternion.identity;
        }

        if (titleCanvasGroup != null)
            titleCanvasGroup.alpha = 0f;
        if (playCanvasGroup != null)
            playCanvasGroup.alpha = 0f;
        if (resetCanvasGroup != null)
            resetCanvasGroup.alpha = 0f;
    }

    private void CacheBaseState()
    {
        if (titleRect != null)
        {
            titleBasePos = titleRect.anchoredPosition;
            titleBaseScale = titleRect.localScale;
        }

        if (playButtonRect != null)
        {
            playBasePos = playButtonRect.anchoredPosition;
            playBaseScale = playButtonRect.localScale;
        }

        if (resetButtonRect != null)
        {
            resetBasePos = resetButtonRect.anchoredPosition;
            resetBaseScale = resetButtonRect.localScale;
        }
    }

    private void AutoAssignReferences()
    {
        if (startUI == null)
        {
            Transform startUIChild = transform.Find("StartUI");
            if (startUIChild != null)
                startUI = startUIChild.gameObject;
        }

        if (titleRect == null && startUI != null)
        {
            Transform title = startUI.transform.Find("TitleText");
            if (title != null)
                titleRect = title.GetComponent<RectTransform>();
        }

        if (playButtonRect == null && startUI != null)
        {
            Transform play = startUI.transform.Find("StartUI/PlayButton");
            if (play == null)
                play = startUI.transform.Find("PlayButton");
            if (play != null)
                playButtonRect = play.GetComponent<RectTransform>();
        }

        if (resetButtonRect == null && resetButton != null)
            resetButtonRect = resetButton.GetComponent<RectTransform>();

        if (menuSfxSource == null)
            menuSfxSource = GetComponent<AudioSource>();

        if (ftueManager == null)
            ftueManager = FindFirstObjectByType<FTUEManager>();
    }

    private CanvasGroup EnsureCanvasGroup(RectTransform rect)
    {
        if (rect == null)
            return null;

        CanvasGroup group = rect.GetComponent<CanvasGroup>();
        if (group == null)
            group = rect.gameObject.AddComponent<CanvasGroup>();
        return group;
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null || menuSfxSource == null)
            return;

        menuSfxSource.PlayOneShot(clip);
    }

    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    private float EaseInBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return c3 * t * t * t - c1 * t * t;
    }

    private float EaseInCubic(float t)
    {
        return t * t * t;
    }
}
