using UnityEngine;

/// <summary>
/// Throw-aiming overlay shown while the car is airborne and the player taps on it.
///
///  � Vertical POWER BAR drawn via OnGUI � identical primitives and style to
///    FTUEOverlay.DrawAimRelease (gray background, green?red fill, white dot, orange chevron).
///  � 3-D AIM ARROW (LineRenderer) in front of the car, steered by drag delta.
///
/// Activated / deactivated by CarController when entering / leaving Aiming phase.
/// </summary>
public class ThrowAimUI : MonoBehaviour
{
    // -- Power Bar -------------------------------------------------------------
    [Header("Power Bar")]
    [Tooltip("Full oscillation cycles per second (uses unscaled time).")]
    [SerializeField] private float oscillateSpeed = 1.5f;

    [Tooltip("Horizontal centre of the bar as a fraction of screen width (0 = left edge).")]
    [Range(0f, 0.25f)]
    [SerializeField] private float barScreenX = 0.07f;

    [Tooltip("Vertical centre of the bar as a fraction of screen height (0 = bottom, 1 = top).")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float barScreenY = 0.5f;

    [Tooltip("Controls the overall size of the bar � same meaning as FTUEOverlay's radiusFraction.")]
    [Range(0.02f, 0.12f)]
    [SerializeField] private float barRadiusFraction = 0.046f;

    // -- Aim Arrow -------------------------------------------------------------
    [Header("Aim Arrow")]
    [SerializeField] private float    arrowLength    = 5f;
    [SerializeField] private float    aimSensitivity = 0.25f; // degrees per screen pixel of drag
    [Tooltip("Material for the aim arrow. If null, Sprites/Default is used at runtime.")]
    [SerializeField] private Material arrowMaterial;

    // -- Public State ----------------------------------------------------------
    /// <summary>Normalised throw power 0-1 (oscillates automatically while active).</summary>
    public float Power { get; private set; }

    /// <summary>World-space aim direction (normalised).</summary>
    public Vector3 AimDirection { get; private set; } = Vector3.forward;

    public bool IsActive { get; private set; }

    // -- Internals -------------------------------------------------------------
    private LineRenderer arrowLine;
    private Transform    carTransform;

    private float oscillateT;   // drives Power via PingPong � scaled by oscillateSpeed
    private float rawAnimT;     // unscaled, used for chevron/pulse sub-animations

    private float aimYaw;
    private float aimPitch = 25f;

    // OnGUI textures (built lazily in EnsureGUIResources)
    private Texture2D texWhite;
    private Texture2D texCircle;

    // ---------------------------------------------------------------------------
    #region Public API

    /// <summary>Show the aiming overlay anchored to the given car.</summary>
    public void Activate(Transform car)
    {
        carTransform = car;
        IsActive     = true;
        oscillateT   = 0f;
        rawAnimT     = 0f;
        Power        = 0f;
        aimYaw       = car.eulerAngles.y;
        aimPitch     = 25f;
        AimDirection = Quaternion.Euler(-aimPitch, aimYaw, 0f) * Vector3.forward;

        EnsureArrowLine();
        arrowLine.enabled = true;
        UpdateArrowVisual();
    }

    /// <summary>Hide the overlay.</summary>
    public void Deactivate()
    {
        IsActive = false;
        if (arrowLine != null) arrowLine.enabled = false;
    }

    /// <summary>
    /// Call every frame while aiming.
    /// pointerDelta is screen-pixel change since last frame.
    /// </summary>
    public void Tick(Vector2 pointerDelta)
    {
        if (!IsActive || carTransform == null) return;

        // Advance timers (unscaledDeltaTime keeps the bar moving even in slow-mo)
        oscillateT += Time.unscaledDeltaTime * oscillateSpeed;
        rawAnimT    = (rawAnimT + Time.unscaledDeltaTime) % 1000f;
        Power       = Mathf.PingPong(oscillateT, 1f);

        // Horizontal drag steers the aim yaw
        aimYaw += pointerDelta.x * aimSensitivity;
        AimDirection = Quaternion.Euler(-aimPitch, aimYaw, 0f) * Vector3.forward;

        UpdateArrowVisual();
    }

    /// <summary>Destroy the arrow helper object entirely (full reset).</summary>
    public void Cleanup()
    {
        Deactivate();
        if (arrowLine != null)
        {
            if (arrowMaterial == null && arrowLine.material != null)
                Destroy(arrowLine.material); // only destroy if we created it at runtime
            Destroy(arrowLine.gameObject);
            arrowLine = null;
        }
        if (texWhite  != null) { Destroy(texWhite);  texWhite  = null; }
        if (texCircle != null) { Destroy(texCircle); texCircle = null; }
    }

    #endregion

    // ---------------------------------------------------------------------------
    #region Unity Lifecycle

    private void OnDestroy()
    {
        if (texWhite  != null) Destroy(texWhite);
        if (texCircle != null) Destroy(texCircle);
    }

    private void OnGUI()
    {
        if (!IsActive) return;
        EnsureGUIResources();

        float sh = Screen.height;
        float sw = Screen.width;
        float r  = sh * barRadiusFraction;

        // Convert from bottom-origin fraction to GUI top-origin pixels
        float cx = sw * barScreenX;
        float cy = sh * (1f - barScreenY);

        DrawPowerBar(cx, cy, r);
    }

    #endregion

    // ---------------------------------------------------------------------------
    #region Power Bar (OnGUI � identical to FTUEOverlay.DrawAimRelease)

    private void DrawPowerBar(float cx, float cy, float r)
    {
        float t    = Power;                          // 0..1 oscillating
        float barH = r * 5f;
        float dotY = cy + barH * 0.5f - barH * t;   // top of fill = current power dot

        // Gray background
        DrawFilledRect(cx - 6.9f, cy - barH * 0.5f, 13.8f, barH,
                       new Color(0.5f, 0.5f, 0.5f, 0.5f));

        // Green-to-red fill growing from the bottom
        Color barCol = Color.Lerp(Color.green, Color.red, t);
        barCol.a = 0.85f;
        DrawFilledRect(cx - 5.75f, dotY, 11.5f, (cy + barH * 0.5f) - dotY, barCol);

        // White dot at current power level
        DrawFilledCircle(cx, dotY, r * 0.65f, new Color(1f, 1f, 1f, 0.92f));

        // Pulsing orange chevron at top of bar � "release here at full power" cue
        float chevAlpha = Mathf.Abs(Mathf.Sin(rawAnimT * 3f));
        DrawChevron(cx + r * 1.8f, cy - barH * 0.5f, r * 0.7f, pointDown: false, chevAlpha);
    }

    #endregion

    // ---------------------------------------------------------------------------
    #region Aim Arrow (LineRenderer)

    private void EnsureArrowLine()
    {
        if (arrowLine != null) return;

        var go = new GameObject("AimArrow");
        arrowLine = go.AddComponent<LineRenderer>();
        arrowLine.positionCount     = 5;
        arrowLine.useWorldSpace     = true;
        arrowLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        arrowLine.receiveShadows    = false;
        arrowLine.material          = arrowMaterial != null
            ? arrowMaterial
            : new Material(Shader.Find("Sprites/Default"));
        arrowLine.startWidth        = 0.18f;
        arrowLine.endWidth          = 0.18f;
        arrowLine.startColor        = Color.yellow;
        arrowLine.endColor          = new Color(1f, 0.4f, 0f); // orange-red
        arrowLine.enabled           = false;
    }

    private void UpdateArrowVisual()
    {
        if (arrowLine == null || carTransform == null) return;

        Vector3 origin = carTransform.position + Vector3.up * 0.6f;
        Vector3 tip    = origin + AimDirection * arrowLength;

        Vector3 right = Vector3.Cross(AimDirection, Vector3.up).normalized;
        if (right.sqrMagnitude < 0.001f)
            right = Vector3.Cross(AimDirection, Vector3.forward).normalized;
        Vector3 headBack = tip - AimDirection * 0.7f;

        arrowLine.SetPosition(0, origin);
        arrowLine.SetPosition(1, tip);
        arrowLine.SetPosition(2, headBack + right * 0.35f);
        arrowLine.SetPosition(3, tip);
        arrowLine.SetPosition(4, headBack - right * 0.35f);
    }

    #endregion

    // ---------------------------------------------------------------------------
    #region OnGUI Primitive Drawers (mirrors FTUEOverlay helpers exactly)

    private void DrawFilledCircle(float cx, float cy, float radius, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(cx - radius, cy - radius, radius * 2f, radius * 2f), texCircle);
        GUI.color = old;
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

    /// <param name="pointDown">true = V shape (downward chevron), false = ^^ shape (upward chevron).</param>
    private void DrawChevron(float cx, float cy, float size, bool pointDown, float alpha)
    {
        Color c = new Color(1f, 0.55f, 0f, 0.9f * alpha);
        float h = size * 0.5f;
        if (pointDown)
        {
            DrawLine(cx - size, cy - h, cx, cy + h, 4f, c);
            DrawLine(cx,        cy + h, cx + size, cy - h, 4f, c);
        }
        else
        {
            DrawLine(cx - size, cy + h, cx, cy - h, 4f, c);
            DrawLine(cx,        cy - h, cx + size, cy + h, 4f, c);
        }
    }

    #endregion

    // ---------------------------------------------------------------------------
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
            const int sz = 64;
            texCircle = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            float half = sz * 0.5f;
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float dx = x - half + 0.5f, dy = y - half + 0.5f;
                    float a  = Mathf.Clamp01((half - Mathf.Sqrt(dx * dx + dy * dy)) / 1.5f);
                    texCircle.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            texCircle.Apply();
        }
    }

    #endregion
}
