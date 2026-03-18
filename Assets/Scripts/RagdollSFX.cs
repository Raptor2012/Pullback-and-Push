using UnityEngine;

/// <summary>
/// Attach to ThrowableCharacterModel.prefab (root GameObject).
///
/// Listens for collision events on ANY collider in the ragdoll hierarchy
/// and plays impact SFX + spawns VFX accordingly.
///
/// SETUP:
///   1. Add this component to the ThrowableCharacterModel prefab root.
///   2. Add an AudioSource component on the same GameObject (auto-detected).
///   3. Assign hitClip, bounceClip, hitVFXPrefab, bounceVFXPrefab in the Inspector.
///      hitVFXPrefab    → Confetti_blast_multicolor (Hyper Casual FX)
///      bounceVFXPrefab → FX_PowerUp_Coin_AA or similar (Stylized VFX pack)
///   4. CameraShake is found automatically at runtime.
///
/// CLIPS (place in Assets/Audio/SFX/):
///   hitClip    — Heavy impact splat + score bell/chime. ~0.5 s. Mono.
///   bounceClip — Rag-doll body thud. ~0.2 s. Mono.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class RagdollSFX : MonoBehaviour
{
    // ── Clips ─────────────────────────────────────────────────────────────────
    [Header("SFX Clips")]
    [Tooltip("Played on collision with a Target.")]
    [SerializeField] private AudioClip hitClip;

    [Tooltip("Played on collision with ground or walls (if impact is hard enough).")]
    [SerializeField] private AudioClip bounceClip;

    // ── VFX Prefabs ───────────────────────────────────────────────────────────
    [Header("VFX Prefabs")]
    [Tooltip("Spawned at the contact point when this ragdoll hits a Target.\n" +
             "Suggested: Confetti_blast_multicolor (Lana Studio / Hyper Casual FX).")]
    [SerializeField] private GameObject hitVFXPrefab;

    [Tooltip("Spawned at the contact point on hard ground/wall impacts.\n" +
             "Suggested: FX_PowerUp_Coin_AA or an Impact prefab from HOVL pack.")]
    [SerializeField] private GameObject bounceVFXPrefab;

    // ── Thresholds ────────────────────────────────────────────────────────────
    [Header("Thresholds")]
    [Tooltip("Minimum collision relative velocity (m/s) to trigger bounce SFX.")]
    [SerializeField] private float bounceImpactThreshold = 2.5f;

    [Tooltip("Seconds before another bounce SFX can play (prevents machine-gunning).")]
    [SerializeField] private float bounceCooldown = 0.15f;

    [Tooltip("Camera shake intensity for target hit.")]
    [SerializeField] private float hitShakeIntensity  = 0.8f;
    [SerializeField] private float hitShakeDuration   = 0.5f;

    [Tooltip("Camera shake intensity for a hard bounce.")]
    [SerializeField] private float bounceShakeIntensity = 0.3f;
    [SerializeField] private float bounceShakeDuration  = 0.2f;

    // ═══════════════════════════════════════════════════════════════════════════
    #region Internals

    private AudioSource  audioSource;
    private CameraShake  cameraShake;
    private float        bounceCooldownTimer;
    private bool         hasHitTarget;
    private bool         warnedMissingHitClip;
    private bool         warnedMissingBounceClip;

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Unity Lifecycle

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.spatialBlend = 1f;   // full 3D
        audioSource.playOnAwake  = false;
        audioSource.loop         = false;

        // Register all child colliders early so collisions right after spawn are captured.
        RegisterCollisionForwarders();
    }

    private void Start()
    {
        // Find CameraShake after all Awake() calls
        cameraShake = FindFirstObjectByType<CameraShake>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Fallback for collisions occurring on the root rigidbody/collider itself.
        OnChildCollision(collision);
    }

    private void Update()
    {
        bounceCooldownTimer = Mathf.Max(0f, bounceCooldownTimer - Time.deltaTime);
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Collision Handling

    /// <summary>Called by CollisionForwarder components on child colliders.</summary>
    public void OnChildCollision(Collision collision)
    {
        if (collision.contactCount == 0) return;

        Vector3 contactPoint = collision.GetContact(0).point;
        float   impactSpeed  = collision.relativeVelocity.magnitude;

        // — Target hit —
        if (!hasHitTarget && IsTargetCollision(collision))
        {
            hasHitTarget = true;
            if (hitClip != null)
            {
                audioSource.PlayOneShot(hitClip, 1f);
            }
            else if (!warnedMissingHitClip)
            {
                warnedMissingHitClip = true;
                Debug.LogWarning("[RagdollSFX] hitClip is not assigned on ragdoll prefab.", this);
            }
            SpawnOneShot(hitVFXPrefab, contactPoint);
            cameraShake?.Shake(hitShakeIntensity, hitShakeDuration);
            return;
        }

        // — Ground / wall bounce —
        if (bounceCooldownTimer <= 0f && impactSpeed >= bounceImpactThreshold)
        {
            bounceCooldownTimer = bounceCooldown;
            float vol = Mathf.Clamp01(Mathf.InverseLerp(bounceImpactThreshold, 8f, impactSpeed));
            if (bounceClip != null)
            {
                audioSource.PlayOneShot(bounceClip, vol);
            }
            else if (!warnedMissingBounceClip)
            {
                warnedMissingBounceClip = true;
                Debug.LogWarning("[RagdollSFX] bounceClip is not assigned on ragdoll prefab.", this);
            }
            SpawnOneShot(bounceVFXPrefab, contactPoint);
            cameraShake?.Shake(bounceShakeIntensity * vol, bounceShakeDuration);
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    #region Helpers

    private static void SpawnOneShot(GameObject prefab, Vector3 pos)
    {
        if (prefab == null) return;
        var go = Instantiate(prefab, pos, Quaternion.identity);

        ParticleSystem ps = go.GetComponent<ParticleSystem>();
        float lifetime = ps != null
            ? ps.main.duration + ps.main.startLifetime.constantMax
            : 5f;
        Destroy(go, lifetime);
    }

    private static bool IsTargetCollision(Collision collision)
    {
        if (collision.collider.CompareTag("Target")) return true;

        if (collision.collider.GetComponent<Target>() != null) return true;
        if (collision.collider.GetComponentInParent<Target>() != null) return true;

        return false;
    }

    private void RegisterCollisionForwarders()
    {
        foreach (var col in GetComponentsInChildren<Collider>(true))
        {
            var forwarder = col.gameObject.GetComponent<CollisionForwarder>();
            if (forwarder == null)
                forwarder = col.gameObject.AddComponent<CollisionForwarder>();
            forwarder.Owner = this;
        }
    }

    #endregion
}

// ────────────────────────────────────────────────────────────────────────────────
/// <summary>
/// Lightweight helper attached to each ragdoll limb collider at runtime.
/// Forwards OnCollisionEnter to the root RagdollSFX script so we only need
/// one listener script rather than one-per-bone.
/// </summary>
public class CollisionForwarder : MonoBehaviour
{
    public RagdollSFX Owner;

    private void OnCollisionEnter(Collision collision)
    {
        Owner?.OnChildCollision(collision);
    }
}
