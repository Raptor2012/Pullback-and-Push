using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// FTUE prompt overlay — Canvas + TextMeshProUGUI for fully designer-controllable text.
/// Every visual property (font, size, color, outline, position, scale, panel color/shape)
/// is exposed in the Inspector. Hand animations are drawn procedurally via OnGUI.
/// Managed entirely by FTUEManager.
/// </summary>
public class FTUEOverlay : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════════════
    #region Serializable Config Types

    [System.Serializable]
    public class PanelConfig
    {
        [Tooltip("Background panel color — adjust alpha for transparency.")]
        public Color color = new Color(0.05f, 0.05f, 0.05f, 0.78f);

        [Header("Anchors  (0 = bottom-left   1 = top-right)")]
        [Tooltip("Panel anchor min — (0,0) is the bottom-left corner of the screen.")]
        public Vector2 anchorMin = new Vector2(0f, 0f);

        [Tooltip("Panel anchor max.")]
        public Vector2 anchorMax = new Vector2(1f, 0.17f);

        [Tooltip("Pixel inset from anchor edges — shrinks the panel inward (Left, Bottom, Right, Top).")]
        public Vector4 padding = new Vector4(18f, 10f, 18f, 10f);

        [Tooltip("How fast the panel and text fade in (alpha units per second, unscaled time).")]
        public float fadeInSpeed = 5f;
    }

    [System.Serializable]
    public class TextConfig
    {
        [Tooltip("TMP font asset. Leave null to use the TMP default font.")]
        public TMP_FontAsset font;

        [Tooltip("Font size in points.")]
        public float fontSize = 38f;

        [Tooltip("Font style — Bold, Italic, Underline, etc.")]
        public FontStyles fontStyle = FontStyles.Bold;

        [Tooltip("Text color.")]
        public Color color = Color.white;

        [Tooltip("Outline width (0 = none, 0.5 = full).")]
        [Range(0f, 0.5f)]
        public float outlineWidth = 0.22f;

        [Tooltip("Outline color.")]
        public Color outlineColor = new Color(0f, 0f, 0f, 1f);

        [Tooltip("Alignment of text within its rect.")]
        public TextAlignmentOptions alignment = TextAlignmentOptions.Center;

        [Tooltip("Uniform scale applied to this text object's Transform.")]
        public Vector3 scale = Vector3.one;

        [Header("Position — Canvas anchors 0 to 1")]
        [Tooltip("RectTransform anchor min (0,0 = bottom-left of the whole screen canvas).")]
        public Vector2 anchorMin = new Vector2(0.04f, 0.04f);

        [Tooltip("RectTransform anchor max.")]
        public Vector2 anchorMax = new Vector2(0.96f, 0.13f);

        [Tooltip("Additional pixel nudge from anchors (Left, Bottom, Right, Top).")]
        public Vector4 offsetDelta = Vector4.zero;
    }

    [System.Serializable]
    public class HandConfig
    {
        [Tooltip("Primary finger / dot fill color.")]
        public Color fingerColor = new Color(1f, 1f, 1f, 0.92f);

        [Tooltip("Ring / arrow / accent stroke color.")]
        public Color ringColor = new Color(1f, 0.55f, 0f, 0.90f);

        [Tooltip("Base radius of the hand graphic as a fraction of screen height.")]
        [Range(0.02f, 0.12f)]
        public float radiusFraction = 0.046f;

        [Tooltip("Horizontal centre of the hand icon as fraction of screen width (0.5 = middle).")]
        [Range(0f, 1f)]
        public float horizontalFraction = 0.5f;

        [Tooltip("How far above the panel top edge the hand sits, in hand-radius multiples.")]
        public float verticalLift = 1.8f;
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Inspector Fields

    [Header("Background Panel")]
    [SerializeField] private PanelConfig panel = new PanelConfig();

    [Header("Prompt Text — Main Instruction")]
    [SerializeField] private TextConfig promptCfg = new TextConfig
    {
        fontSize     = 38f,
        fontStyle    = FontStyles.Bold,
        color        = Color.white,
        outlineWidth = 0.22f,
        anchorMin    = new Vector2(0.04f, 0.05f),
        anchorMax    = new Vector2(0.96f, 0.12f),
    };

    [Header("Hint Text — Follow-up")]
    [SerializeField] private TextConfig hintCfg = new TextConfig
    {
        fontSize     = 26f,
        fontStyle    = FontStyles.Normal,
        color        = new Color(1f, 0.85f, 0.3f, 1f),
        outlineWidth = 0.15f,
        anchorMin    = new Vector2(0.04f, 0.01f),
        anchorMax    = new Vector2(0.96f, 0.06f),
    };

    [Header("Hand Animation Icon")]
    [SerializeField] private HandConfig hand = new HandConfig();

    [Header("Canvas")]
    [Tooltip("Sorting order of the overlay canvas — raise if other canvases draw on top.")]
    [SerializeField] private int canvasSortOrder = 99;

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Public API

    /// <summary>True while the overlay is visible.</summary>
    public bool IsVisible { get; private set; }

    /// <summary>Shows the overlay with the given prompt and hand animation.</summary>
    public void Show(string prompt, FTUEStepConfig.HandAnim anim,
                     bool overrideHandPos = false, float handScreenX = 0.5f, float handScreenY = 0.5f,
                     bool showHandAnimation = true)
    {
        promptText      = prompt;
        hintText        = string.Empty;
        currentAnim     = anim;
        showHandAnim    = showHandAnimation;
        animT           = 0f;
        IsVisible       = true;
        handPosOverride = overrideHandPos;
        handOverrideX   = handScreenX;
        handOverrideY   = handScreenY;

        if (promptLabel != null) { promptLabel.text = prompt; promptLabel.gameObject.SetActive(true); }
        if (hintLabel   != null) { hintLabel.text   = string.Empty; hintLabel.gameObject.SetActive(false); }
        if (panelImage  != null)   panelImage.gameObject.SetActive(true);
    }

    /// <summary>Updates the secondary hint text without resetting the main animation.</summary>
    public void ShowHint(string hint)
    {
        hintText = hint;
        if (hintLabel != null)
        {
            hintLabel.text = hint;
            hintLabel.gameObject.SetActive(!string.IsNullOrEmpty(hint));
        }
    }

    /// <summary>Hides the overlay immediately.</summary>
    public void Hide()
    {
        IsVisible  = false;
        promptText = string.Empty;
        hintText   = string.Empty;
        panelAlpha = 0f;

        if (panelImage  != null) panelImage.gameObject.SetActive(false);
        if (promptLabel != null) promptLabel.gameObject.SetActive(false);
        if (hintLabel   != null) hintLabel.gameObject.SetActive(false);
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Internals

    private string                  promptText  = string.Empty;
    private string                  hintText    = string.Empty;
    private FTUEStepConfig.HandAnim currentAnim = FTUEStepConfig.HandAnim.TapPulse;
    private bool                    showHandAnim = true;
    private float                   animT;
    private float                   panelAlpha;

    // Per-step hand position override (set via Show overload)
    private bool  handPosOverride;
    private float handOverrideX = 0.5f;   // 0-1 screen fraction, 0=left
    private float handOverrideY = 0.5f;   // 0-1 screen fraction, 0=bottom  1=top

    // Canvas hierarchy (built at Awake)
    private Canvas          overlayCanvas;
    private Image           panelImage;
    private TextMeshProUGUI promptLabel;
    private TextMeshProUGUI hintLabel;

    // OnGUI draw textures (built lazily in EnsureGUIResources)
    private Texture2D texWhite;
    private Texture2D texCircle;

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Unity Lifecycle

    private void Awake()
    {
        BuildCanvas();
        Hide();
    }

    private void Update()
    {
        if (!IsVisible) return;
        animT      = (animT + Time.unscaledDeltaTime) % 1000f;
        panelAlpha = Mathf.MoveTowards(panelAlpha, 1f, Time.unscaledDeltaTime * panel.fadeInSpeed);

        // Drive alpha on the canvas group so panel + text fade together
        if (overlayCanvas != null)
        {
            CanvasGroup cg = overlayCanvas.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = panelAlpha;
        }
    }

    private void OnGUI()
    {
        if (!IsVisible || string.IsNullOrEmpty(promptText) || !showHandAnim) return;
        EnsureGUIResources();

        float sh    = Screen.height;
        float sw    = Screen.width;

        // Hand position — use per-step override if set, otherwise default above panel
        float handR = sh * hand.radiusFraction;
        float handCX, handCY;
        if (handPosOverride)
        {
            handCX = sw * handOverrideX;
            handCY = sh * (1f - handOverrideY);   // GUI Y=0 is top, so invert
        }
        else
        {
            float panelTopY = sh * (1f - panel.anchorMax.y);
            handCX = sw * hand.horizontalFraction;
            handCY = panelTopY - handR * hand.verticalLift;
        }

        DrawHandAnim(currentAnim, handCX, handCY, handR, sw, sh);
    }

    private void OnDestroy()
    {
        if (texWhite  != null) Destroy(texWhite);
        if (texCircle != null) Destroy(texCircle);
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Canvas Construction

    private void BuildCanvas()
    {
        var canvasGO             = new GameObject("FTUECanvas");
        canvasGO.transform.SetParent(transform, false);

        overlayCanvas            = canvasGO.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = canvasSortOrder;

        canvasGO.AddComponent<CanvasScaler>();
        var gr             = canvasGO.AddComponent<GraphicRaycaster>();
        gr.blockingMask    = 0;   // non-blocking so gameplay touches pass through

        var cg             = canvasGO.AddComponent<CanvasGroup>();
        cg.alpha           = 0f;
        cg.interactable    = false;
        cg.blocksRaycasts  = false;

        // Panel background image
        panelImage = MakeImage("FTUEPanel", canvasGO.transform);
        panelImage.color = panel.color;
        SetAnchors(panelImage.rectTransform,
            panel.anchorMin, panel.anchorMax,
            new Vector2( panel.padding.x,  panel.padding.y),
            new Vector2(-panel.padding.z, -panel.padding.w));

        // Prompt label
        promptLabel = MakeTMPLabel("FTUEPrompt", canvasGO.transform, promptCfg);

        // Hint label
        hintLabel = MakeTMPLabel("FTUEHint", canvasGO.transform, hintCfg);
    }

    private static Image MakeImage(string goName, Transform parent)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go.AddComponent<Image>();
    }

    private static TextMeshProUGUI MakeTMPLabel(string goName, Transform parent, TextConfig cfg)
    {
        var go  = new GameObject(goName);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        ApplyTextConfig(tmp, cfg);
        return tmp;
    }

    private static void ApplyTextConfig(TextMeshProUGUI tmp, TextConfig cfg)
    {
        if (cfg.font != null)   tmp.font = cfg.font;
        tmp.fontSize            = cfg.fontSize;
        tmp.fontStyle           = cfg.fontStyle;
        tmp.color               = cfg.color;
        tmp.outlineWidth        = cfg.outlineWidth;
        tmp.outlineColor        = cfg.outlineColor;
        tmp.alignment           = cfg.alignment;
        tmp.enableWordWrapping  = true;
        tmp.overflowMode        = TextOverflowModes.Overflow;
        tmp.raycastTarget       = false;
        tmp.transform.localScale = cfg.scale;

        SetAnchors(tmp.rectTransform,
            cfg.anchorMin, cfg.anchorMax,
            new Vector2( cfg.offsetDelta.x,  cfg.offsetDelta.y),
            new Vector2(-cfg.offsetDelta.z, -cfg.offsetDelta.w));
    }

    private static void SetAnchors(RectTransform rt,
        Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.offsetMin = oMin;
        rt.offsetMax = oMax;
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Hand Animation Drawing

    private void DrawHandAnim(FTUEStepConfig.HandAnim anim, float cx, float cy, float r, float sw, float sh)
    {
        switch (anim)
        {
            case FTUEStepConfig.HandAnim.TapPulse:    DrawTapPulse(cx, cy, r);            break;
            case FTUEStepConfig.HandAnim.DragDown:    DrawDragDown(cx, cy, r);            break;
            case FTUEStepConfig.HandAnim.Release:     DrawRelease(cx, cy, r);             break;
            case FTUEStepConfig.HandAnim.SteerLeft:   DrawArrow(cx - r * 2f, cy, r * 2.5f, true);  break;
            case FTUEStepConfig.HandAnim.SteerRight:  DrawArrow(cx + r * 2f, cy, r * 2.5f, false); break;
            case FTUEStepConfig.HandAnim.SteerBoth:
                DrawArrow(cx - r * 3.2f, cy, r * 2.2f, true);
                DrawArrow(cx + r * 3.2f, cy, r * 2.2f, false);
                break;
            case FTUEStepConfig.HandAnim.TapHold:     DrawTapHold(cx, cy, r);            break;
            case FTUEStepConfig.HandAnim.AimRelease:  DrawAimRelease(cx, cy, r, sh);     break;
            case FTUEStepConfig.HandAnim.SteerRagdoll:DrawSteerRagdoll(cx, cy, r);       break;
            case FTUEStepConfig.HandAnim.Celebrate:   DrawCelebrate(cx, cy, r);          break;
            case FTUEStepConfig.HandAnim.TapButton:   DrawTapPulse(cx, cy, r);           break;
        }
    }

    // ── Tap pulse: growing ring + solid dot ───────────────────────────────────
    private void DrawTapPulse(float cx, float cy, float r)
    {
        float cycle = Mathf.PingPong(animT * 1.8f, 1f);
        float scale = Mathf.Lerp(0.6f, 1.4f, cycle);
        float alpha = Mathf.Lerp(1f, 0.3f, cycle) * panelAlpha;
        DrawCircleOutline(cx, cy, r * scale * 1.5f, 6f, C(hand.ringColor, alpha));
        DrawFilledCircle(cx, cy, r * 0.5f, C(hand.fingerColor, panelAlpha));
    }

    // ── Drag down: finger slides downward and resets ──────────────────────────
    private void DrawDragDown(float cx, float cy, float r)
    {
        float t       = Mathf.PingPong(animT * 1.2f, 1f);
        float offset  = Mathf.Lerp(0f, r * 3.2f, EaseInOut(t));
        float fingerY = cy - r * 1.5f + offset;
        DrawFilledRect(cx - 3f, cy - r * 1.5f, 6f, offset, C(hand.ringColor, 0.35f * panelAlpha));
        DrawFilledCircle(cx, fingerY, r * 0.7f, C(hand.fingerColor, Mathf.Lerp(1f, 0.5f, t) * panelAlpha));
        DrawChevron(cx, fingerY + r * 1.2f, r * 0.8f, true,  panelAlpha * 0.7f);
        DrawChevron(cx, fingerY + r * 2.2f, r * 0.7f, true,  panelAlpha * 0.4f);
    }

    // ── Release: pulsing circle + radiating rays ──────────────────────────────
    private void DrawRelease(float cx, float cy, float r)
    {
        float breathe = 0.85f + Mathf.Sin(animT * 3f) * 0.15f;
        DrawCircleOutline(cx, cy, r * breathe * 1.4f, 5f, C(hand.ringColor, panelAlpha));
        int rays = 6;
        for (int i = 0; i < rays; i++)
        {
            float angle = i * Mathf.PI * 2f / rays + animT * 0.8f;
            float ra    = Mathf.Abs(Mathf.Sin(animT * 2f + i * 0.5f)) * panelAlpha * 0.6f;
            DrawLine(cx + Mathf.Cos(angle) * r * 1.6f, cy + Mathf.Sin(angle) * r * 1.6f,
                     cx + Mathf.Cos(angle) * r * 2.6f, cy + Mathf.Sin(angle) * r * 2.6f,
                     4f, C(hand.ringColor, ra));
        }
    }

    // ── Tap hold: circle expands then holds with concentric rings ─────────────
    private void DrawTapHold(float cx, float cy, float r)
    {
        float cycle = Mathf.Clamp01(Mathf.PingPong(animT * 0.7f, 1f));
        float scale = Mathf.Lerp(0.5f, 1.2f, EaseOut(cycle));
        DrawCircleOutline(cx, cy, r * scale * 1.8f, 7f, C(hand.ringColor, Mathf.Lerp(1f, 0.4f, cycle) * panelAlpha));
        DrawFilledCircle(cx, cy, r * Mathf.Lerp(0.4f, 0.8f, scale), C(hand.fingerColor, panelAlpha));
        float holdA = Mathf.Clamp01((Mathf.PingPong(animT * 0.7f, 1f) - 0.5f) * 4f) * panelAlpha * 0.5f;
        DrawCircleOutline(cx, cy, r * 2.5f, 4f, C(hand.ringColor, holdA));
        DrawCircleOutline(cx, cy, r * 3.2f, 3f, C(hand.ringColor, holdA * 0.6f));
    }

    // ── Aim + oscillating power bar with release cue ──────────────────────────
    private void DrawAimRelease(float cx, float cy, float r, float sh)
    {
        float t    = Mathf.PingPong(animT * 1.4f, 1f);
        float barH = r * 5f;
        float dotY = cy + barH * 0.5f - barH * t;
        DrawFilledRect(cx - 6f, cy - barH * 0.5f, 12f, barH, C(Color.gray, 0.5f * panelAlpha));
        Color barCol = Color.Lerp(Color.green, Color.red, t);
        barCol.a = 0.85f * panelAlpha;
        DrawFilledRect(cx - 5f, dotY, 10f, (cy + barH * 0.5f) - dotY, barCol);
        DrawFilledCircle(cx, dotY, r * 0.65f, C(hand.fingerColor, panelAlpha));
        DrawChevron(cx + r * 1.8f, cy - barH * 0.5f, r * 0.7f, false, Mathf.Abs(Mathf.Sin(animT * 3f)) * panelAlpha);
    }

    // ── Ragdoll steer: alternating left + right arrows ────────────────────────
    private void DrawSteerRagdoll(float cx, float cy, float r)
    {
        bool leftActive = Mathf.Sin(animT * 1.5f) > 0f;
        DrawArrow(cx - r * 3f, cy, r * 2.2f, true,  leftActive  ? panelAlpha : panelAlpha * 0.25f);
        DrawArrow(cx + r * 3f, cy, r * 2.2f, false, !leftActive ? panelAlpha : panelAlpha * 0.25f);
    }

    // ── Celebrate: rotating starburst ────────────────────────────────────────
    private void DrawCelebrate(float cx, float cy, float r)
    {
        float rot = animT * 1.5f;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * 2f / 8f + rot;
            float pulse = 0.5f + 0.5f * Mathf.Sin(animT * 3f + i);
            DrawLine(cx, cy,
                cx + Mathf.Cos(angle) * r * (2f + pulse),
                cy + Mathf.Sin(angle) * r * (2f + pulse),
                5f, C(new Color(1f, 0.85f, 0.1f), (0.5f + 0.5f * pulse) * panelAlpha));
        }
        DrawFilledCircle(cx, cy, r * 0.7f, C(new Color(1f, 0.9f, 0f), panelAlpha));
    }

    // ── Directional arrow (three sequential chevrons) ─────────────────────────
    private void DrawArrow(float cx, float cy, float size, bool pointsLeft, float alpha = -1f)
    {
        float a     = alpha < 0f ? panelAlpha : alpha;
        float pulse = 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(animT * 2.5f));
        float s     = size * pulse;
        float dir   = pointsLeft ? -1f : 1f;
        for (int i = 0; i < 3; i++)
            DrawChevronH(cx + dir * i * s * 0.55f, cy, s * 0.7f, pointsLeft, a * Mathf.Lerp(1f, 0.25f, i * 0.4f));
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Primitive OnGUI Drawers

    private void DrawFilledCircle(float cx, float cy, float radius, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(cx - radius, cy - radius, radius * 2, radius * 2), texCircle);
        GUI.color = old;
    }

    private void DrawCircleOutline(float cx, float cy, float radius, float thickness, Color color)
    {
        float t = thickness * 0.5f;
        for (int i = 0; i < 20; i++)
        {
            float angle = i * Mathf.PI * 2f / 20;
            Color old   = GUI.color;
            GUI.color   = color;
            GUI.DrawTexture(new Rect(
                cx + Mathf.Cos(angle) * radius - t,
                cy + Mathf.Sin(angle) * radius - t,
                thickness, thickness), texCircle);
            GUI.color = old;
        }
    }

    private void DrawFilledRect(float x, float y, float w, float h, Color color)
    {
        if (w <= 0f || h <= 0f) return;
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(x, y, w, h), texWhite);
        GUI.color = old;
    }

    private void DrawLine(float x1, float y1, float x2, float y2, float thickness, Color color)
    {
        Vector2 dir   = new Vector2(x2 - x1, y2 - y1);
        float   len   = dir.magnitude;
        if (len < 0.1f) return;
        float   angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Vector2 mid   = new Vector2((x1 + x2) * 0.5f, (y1 + y2) * 0.5f);
        Matrix4x4 m   = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, mid);
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(mid.x - len * 0.5f, mid.y - thickness * 0.5f, len, thickness), texWhite);
        GUI.color  = old;
        GUI.matrix = m;
    }

    private void DrawChevron(float cx, float cy, float size, bool down, float alpha)
    {
        Color c = C(hand.ringColor, alpha);
        float d = down ? 1f : -1f;
        DrawLine(cx - size, cy,            cx,          cy + size * d, 5f, c);
        DrawLine(cx,         cy + size * d, cx + size,  cy,            5f, c);
    }

    private void DrawChevronH(float cx, float cy, float size, bool pointsLeft, float alpha)
    {
        Color c = C(hand.ringColor, alpha);
        float d = pointsLeft ? -1f : 1f;
        DrawLine(cx,            cy - size, cx + size * d, cy,         5f, c);
        DrawLine(cx + size * d, cy,        cx,            cy + size,  5f, c);
    }

    // Returns a copy of src with a different combined alpha (avoids struct mutation)
    private static Color C(Color src, float alpha) => new Color(src.r, src.g, src.b, src.a * alpha);

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region GUI Resource Init

    private void EnsureGUIResources()
    {
        if (texWhite == null)
        {
            texWhite = new Texture2D(1, 1);
            texWhite.SetPixel(0, 0, Color.white);
            texWhite.Apply();
        }

        if (texCircle == null)
        {
            int   size  = 64;
            texCircle   = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half  = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(half, half));
                    float a    = 1f - Mathf.Clamp01((dist - (half - 2f)) / 2f);
                    texCircle.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            texCircle.Apply();
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Easing Helpers

    private static float EaseInOut(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
    private static float EaseOut(float t)   => 1f - Mathf.Pow(1f - t, 2f);

    #endregion
}
