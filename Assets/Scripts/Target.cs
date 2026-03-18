using System.Collections;
using TMPro;
using UnityEngine;

public class Target : MonoBehaviour
{
    // ── Scoring ───────────────────────────────────────────────────────────────
    [Header("Scoring")]
    [Tooltip("Score awarded when the player hits dead-centre.")]
    [SerializeField] private int maxScore = 100;

    [Tooltip("Score awarded when the player hits the outermost ring.")]
    [SerializeField] private int minScore = 10;

    // ── Particle ──────────────────────────────────────────────────────────────
    [Header("Hit Particle")]
    [Tooltip("Particle system prefab to spawn at the contact point. Leave null to skip.")]
    [SerializeField] private GameObject hitParticlePrefab;

    // ── Score Indicator ───────────────────────────────────────────────────────
    [Header("Score Indicator – Line")]
    [Tooltip("Line length as a multiplier of the target's world-space outer radius.")]
    [SerializeField] private float lineLengthMultiplier = 0.55f;

    [Tooltip("Seconds taken for the line to fully draw.")]
    [SerializeField] private float lineDuration = 0.35f;

    [Tooltip("Line width as a fraction of the target's world-space outer radius.")]
    [SerializeField] private float lineWidthFraction = 0.012f;

    [Header("Score Indicator – Text")]
    [Tooltip("Score text size as a fraction of the target's world-space outer radius.")]
    [SerializeField] private float scoreFontFraction = 0.18f;

    [Tooltip("Seconds taken for the score text to fade in.")]
    [SerializeField] private float textFadeDuration = 0.3f;

    // ── State ─────────────────────────────────────────────────────────────────
    private bool alreadyHit = false;
    /// The Rigidbody instance ID of the object that already scored, so multiple
    /// body parts (ragdoll bones) all attached to the same root Rigidbody don't
    /// each trigger a separate score popup.
    private int scoringRigidbodyId = -1;

    // ── Shared Resources ──────────────────────────────────────────────────────
    [Header("Shared Resources")]
    [SerializeField] private Material scoreLineMaterial;
    private static Material sharedScoreLineMaterial;

    private Material GetScoreLineMaterial()
    {
        if (scoreLineMaterial != null) return scoreLineMaterial;
        if (sharedScoreLineMaterial == null)
            sharedScoreLineMaterial = new Material(Shader.Find("Sprites/Default"));
        return sharedScoreLineMaterial;
    }

    // ── Spawned Indicator Objects ─────────────────────────────────────────────
    private GameObject spawnedScoreLine;
    private GameObject spawnedScoreText;

    // ── Cached Scene References ───────────────────────────────────────────────
    private static CameraController cachedCamCtrl;
    private static CameraShake      cachedShake;

    // ── FTUE Signal ────────────────────────────────────────────────────────────
    /// Raised whenever ANY target on the scene is successfully hit. Arg: score earned.
    public static event System.Action<int> OnAnyTargetHit;
    // ═════════════════════════════════════════════════════════════════════════
    #region Unity Lifecycle

    private void Awake()
    {
        if (cachedCamCtrl == null) cachedCamCtrl = FindFirstObjectByType<CameraController>();
        if (cachedShake   == null) cachedShake   = FindFirstObjectByType<CameraShake>();
    }

    #endregion

    // ═════════════════════════════════════════════════════════════════════════
    #region Public API

    /// <summary>Re-arms the target so it can be hit again (called on reset).</summary>
    public void ResetTarget()
    {
        alreadyHit         = false;
        scoringRigidbodyId = -1;
        if (spawnedScoreLine != null) { Destroy(spawnedScoreLine); spawnedScoreLine = null; }
        if (spawnedScoreText != null) { Destroy(spawnedScoreText); spawnedScoreText = null; }
    }

    #endregion

    // ═════════════════════════════════════════════════════════════════════════
    #region Collision

    void OnCollisionEnter(Collision collision)
    {
        if (alreadyHit) return;
        if (!collision.collider.CompareTag("Player")) return;

        // Find the root Rigidbody of the incoming object (handles ragdoll bones).
        Rigidbody incomingRb = collision.rigidbody
                            ?? collision.collider.GetComponentInParent<Rigidbody>();
        int rbId = incomingRb != null ? incomingRb.GetInstanceID() : collision.collider.GetInstanceID();

        // If a different body-part from the same Rigidbody already scored, skip.
        if (scoringRigidbodyId != -1 && scoringRigidbodyId == rbId) return;

        alreadyHit         = true;
        scoringRigidbodyId = rbId;

        // ── Resolve contact point ──
        // Use the incoming object's center projected onto the target's face plane.
        // Raw physics contacts often resolve at the cylinder rim (curved side),
        // which would always give minimum score regardless of aim accuracy.
        Vector3 incomingCenter = incomingRb != null
            ? incomingRb.position
            : collision.collider.bounds.center;

        Vector3 faceNormal  = transform.up;
        float   distToPlane = Vector3.Dot(incomingCenter - transform.position, faceNormal);
        Vector3 contactPoint = incomingCenter - distToPlane * faceNormal;

        // ── Calculate score ──
        int score = CalculateScore(contactPoint);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Target] incomingCenter={incomingCenter:F2} → facePoint={contactPoint:F2} → Score: {score}");
#endif

        // ── Notify FTUE ────────────────────────────────────────────────────────────
        OnAnyTargetHit?.Invoke(score);

        // ── Spawn particle at contact point ──
        if (hitParticlePrefab != null)
        {
            GameObject fx = Instantiate(hitParticlePrefab, contactPoint, Quaternion.identity);
            ParticleSystem ps = fx.GetComponent<ParticleSystem>();
            float lifetime = ps != null
                ? ps.main.duration + ps.main.startLifetime.constantMax
                : 5f;
            Destroy(fx, lifetime);
        }

        // ── Animate score indicator ──
        StartCoroutine(AnimateScoreIndicator(contactPoint, score));

        // ── Freeze camera (with zoom-out so score indicator stays in frame) ──
        if (cachedCamCtrl != null) cachedCamCtrl.FreezeWithZoomOut();

        // ── Camera shake ──
        if (cachedShake != null) cachedShake.Shake(0.8f, 0.5f);

        // ── Hit-pause (freeze-frame) ──
        StartCoroutine(HitPause(0.06f));
    }

    private System.Collections.IEnumerator HitPause(float realSeconds)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(realSeconds);
        Time.timeScale = 1f;
    }

    #endregion

    // ═════════════════════════════════════════════════════════════════════════
    #region Score Calculation

    /// <summary>
    /// Returns the outer radius in LOCAL space by reading the mesh bounds.
    /// Works for any mesh or scale — no manual field needed.
    /// </summary>
    float GetLocalOuterRadius()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Vector3 e = mf.sharedMesh.bounds.extents;
            return Mathf.Max(e.x, e.z);
        }
        // Fallback: use renderer world extents ÷ scale
        Renderer r = GetComponent<Renderer>();
        if (r != null)
        {
            Vector3 we = r.bounds.extents;
            float worldR = Mathf.Max(we.x, we.z);
            float scale  = Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
            if (scale > 0.0001f) return worldR / scale;
        }
        return 0.5f; // Unity Cylinder default
    }

    int CalculateScore(Vector3 facePoint)
    {
        // facePoint is already projected onto the target's face plane.
        // Convert to local space — for a Unity Cylinder/Disc the face lies in
        // the local XZ plane, so sqrt(x²+z²) is the radial distance from centre.
        Vector3 local     = transform.InverseTransformPoint(facePoint);
        float   localDist = new Vector2(local.x, local.z).magnitude;
        float   localR    = GetLocalOuterRadius();
        float   t         = Mathf.Clamp01(localDist / localR);
        int     score     = Mathf.RoundToInt(Mathf.Lerp(maxScore, minScore, t));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Target] localDist={localDist:F3}  outerR={localR:F3}  t={t:F2}  → {score}");
#endif
        return score;
    }

    #endregion

    // ═════════════════════════════════════════════════════════════════════════
    #region Score Indicator Animation

    IEnumerator AnimateScoreIndicator(Vector3 contactPoint, int score)
    {
        // ── Auto-derive world-space sizes from the target's actual scale ──────
        float worldRadius = GetLocalOuterRadius() *
            Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
        float lineLength  = worldRadius * lineLengthMultiplier;
        float lineWidth   = worldRadius * lineWidthFraction;
        float fontSize    = worldRadius * scoreFontFraction;

        // ── Choose colours based on score ────────────────────────────────────
        Color lineColor  = ScoreColor(score);
        Color textColor  = lineColor;

        // ── Compute line endpoint ─────────────────────────────────────────────
        // Project radially away from the face centre, then lift slightly so the
        // tip sits cleanly above the hit ring (matches annotation-pointer style).
        Vector3 radial = contactPoint - transform.position;
        // The target face is the XZ plane in local space (cylinder axis = local Y).
        // Strip out the Y component in world space by projecting onto the face normal.
        Vector3 faceNormal = transform.up;
        radial -= Vector3.Dot(radial, faceNormal) * faceNormal;
        if (radial.sqrMagnitude < 0.001f) radial = transform.right;
        radial.Normalize();

        // Short outward + slight upward lift
        Vector3 liftDir = (radial + faceNormal * 0.35f).normalized;
        Vector3 lineEnd = contactPoint + liftDir * lineLength;

        // ── Build LineRenderer ────────────────────────────────────────────────
        GameObject lineGO = new GameObject("ScoreLine");
        lineGO.transform.SetParent(null);
        spawnedScoreLine = lineGO;

        LineRenderer lr = lineGO.AddComponent<LineRenderer>();
        lr.useWorldSpace     = true;
        lr.positionCount     = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows    = false;
        lr.startWidth        = lineWidth;
        lr.endWidth          = lineWidth * 0.5f;
        lr.numCapVertices    = 4;

        lr.material   = GetScoreLineMaterial();
        lr.startColor = lineColor;
        lr.endColor   = lineColor;

        lr.SetPosition(0, contactPoint);
        lr.SetPosition(1, contactPoint);   // starts collapsed

        // ── PHASE 1 — Draw the line ───────────────────────────────────────────
        float elapsed = 0f;
        while (elapsed < lineDuration)
        {
            elapsed += Time.deltaTime;
            float t   = Easing.OutCubic(Mathf.Clamp01(elapsed / lineDuration));
            lr.SetPosition(1, Vector3.Lerp(contactPoint, lineEnd, t));
            yield return null;
            if (lineGO == null) yield break;   // destroyed by ResetTarget mid-flight
        }
        lr.SetPosition(1, lineEnd);

        // ── Build TextMeshPro 3-D text ────────────────────────────────────────
        // Offset text further along the pointer so it clears the line tip.
        Vector3 textOffset = liftDir * (lineLength * 0.35f);

        GameObject textGO = new GameObject("ScoreText");
        textGO.transform.SetParent(null);
        textGO.transform.position = lineEnd + textOffset;
        spawnedScoreText = textGO;

        TextMeshPro tmp = textGO.AddComponent<TextMeshPro>();
        tmp.text               = score.ToString();
        tmp.fontSize           = fontSize;
        tmp.fontStyle          = FontStyles.Bold;
        tmp.alignment          = TextAlignmentOptions.Center;
        tmp.color              = new Color(textColor.r, textColor.g, textColor.b, 0f);
        tmp.outlineWidth       = 0.45f;
        tmp.outlineColor       = new Color(0f, 0f, 0f, 0f);
        // Ensure the text mesh is large enough to avoid clipping at high font sizes
        tmp.rectTransform.sizeDelta = Vector2.one * fontSize * 2f;

        FaceCamera(textGO.transform);

        // Start tiny → punches in via OutBack easing
        textGO.transform.localScale = Vector3.one * 0.01f;

        // ── PHASE 2 — Fade in + scale punch ──────────────────────────────────
        elapsed = 0f;
        while (elapsed < textFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t   = Easing.OutBack(Mathf.Clamp01(elapsed / textFadeDuration));

            tmp.color            = new Color(textColor.r, textColor.g, textColor.b, t);
            tmp.outlineColor     = new Color(0f, 0f, 0f, t);
            textGO.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, t);

            FaceCamera(textGO.transform);
            yield return null;
            if (textGO == null) yield break;   // destroyed by ResetTarget mid-flight
        }

        if (textGO == null) yield break;
        tmp.color        = new Color(textColor.r, textColor.g, textColor.b, 1f);
        tmp.outlineColor = new Color(0f, 0f, 0f, 1f);
        textGO.transform.localScale = Vector3.one;

        // Line and text stay permanently — nothing more to do.
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static Color ScoreColor(int score)
    {
        // 80-100 → gold, 50-79 → bright orange, 10-49 → vivid cyan
        if (score >= 80) return new Color(1.00f, 0.82f, 0.00f); // gold
        if (score >= 50) return new Color(1.00f, 0.50f, 0.05f); // orange
        return              new Color(0.00f, 0.90f, 1.00f);      // cyan
    }

    static void FaceCamera(Transform t)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        t.rotation = Quaternion.LookRotation(t.position - cam.transform.position);
    }

    #endregion

    // ═════════════════════════════════════════════════════════════════════════
    #region Easing Helpers

    static class Easing
    {
        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

        // Overshoots slightly then settles — great for a punchy text appear
        public static float OutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }

    #endregion
}
