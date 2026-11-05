using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class TopDownMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 250f;

    [Header("Dodge Settings")]
    public float dodgeDistance = 3f;
    public float dodgeSpeed = 15f;
    public float dodgeCooldown = 0.3f;
    private bool dodgeLeftNext = false;

    [Header("Push / Shove Settings")]
    public float pushForce = 25f;
    public float pushCooldown = 0.3f;
    [SerializeField] private float shoveRadius = 0.8f;
    [SerializeField] private float shoveOffsetDistance = 0.9f;

    [Header("Whiff & Lunge Penalty Engine")]
    [Tooltip("Forward force of the shove lunge.")]
    public float shoveLungeForce = 12f;
    [Tooltip("Duration of the lunge dash.")]
    public float lungeDuration = 0.15f;
    [Tooltip("Lockout if the shove misses (no move/dodge).")]
    public float whiffCooldownPenalty = 0.8f;

    private bool isWhiffRecovering = false;
    private bool isLungingForward = false;

    [Header("Physics Interaction System")]
    [Tooltip("Minimum collision speed to register an interaction.")]
    [SerializeField] private float minimumVelocityThreshold = 1.5f;
    [Tooltip("0 = any hit, 0.5 = intentional push, 0.8 = strict direct hit.")]
    [SerializeField] private float intentionalDirectionThreshold = 0.25f;
    [Tooltip("Seconds an interaction stays valid for kill credit.")]
    public float trackingWindowDuration = 2.0f;

    [Header("Juice & Knockback Physics Curves")]
    [SerializeField] private AnimationCurve knockbackCurve = AnimationCurve.Linear(0, 1, 1, 0);
    [SerializeField] private float knockbackDuration = 0.45f;

    [Header("Shove Audio Juice Engine")]
    [Tooltip("AudioSource on this prefab (auto-found if empty).")]
    [SerializeField] private AudioSource playerAudioSource;
    [Tooltip("Shove clips, played in rotation.")]
    [SerializeField] private AudioClip[] shoveAudioClips;
    private int currentShoveAudioIndex = 0;

    [Header("Debug & Safety Configuration")]
    [SerializeField] private bool hitAnythingWithRigidbody = true;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private bool showDebugTraces = true;

    [Header("Control Script Reference")]
    public MobileControls controls;

    private Rigidbody2D rb;
    [HideInInspector] public bool isDodging = false;
    private float lastDodgeTime = -999f;
    private float lastPushTime = -999f;

    [HideInInspector] public int playerIndex = 0;
    [HideInInspector] public int lastAttackerIndex = -1;
    [HideInInspector] public float lastInteractionTimestamp = -999f;
    [HideInInspector] public int consecutiveHitCount = 0;
    [HideInInspector] public bool hasDealtDamageThisRound = false;
    [HideInInspector] public float currentKnockbackReceivedMultiplier = 1.0f;

    private bool isBeingKnockedBack = false;
    private Vector2 knockbackDirection;
    private float knockbackMaxForce;
    private float knockbackTimer;

    private bool IsLocked => isWhiffRecovering || isLungingForward;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (playerAudioSource == null) playerAudioSource = GetComponent<AudioSource>();

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // overrides the inspector curve on purpose: sharp hit, long tail
        knockbackCurve = new AnimationCurve(
            new Keyframe(0f, 1f, 0f, -2.5f),
            new Keyframe(0.25f, 0.15f, -0.6f, -0.6f),
            new Keyframe(1f, 0f, 0f, 0f)
        );
    }

    private void Update()
    {
        if (controls == null) return;

        bool locked = IsLocked || isBeingKnockedBack;
        bool dodge = controls.dodgePressed, push = controls.pushPressed;
        if (locked || dodge) controls.ResetDodge();
        if (locked || push) controls.ResetPush();
        if (locked) return;

        if (dodge && Time.time - lastDodgeTime >= dodgeCooldown) PerformDodge();
        if (push && Time.time - lastPushTime >= pushCooldown) ExecuteInstantShove();
    }

    private void FixedUpdate()
    {
        if (isBeingKnockedBack)
        {
            knockbackTimer += Time.fixedDeltaTime;
            float progress = knockbackTimer / knockbackDuration;
            if (progress >= 1f)
            {
                isBeingKnockedBack = false;
                rb.linearVelocity = Vector2.zero;
            }
            else rb.linearVelocity = knockbackDirection * (knockbackMaxForce * knockbackCurve.Evaluate(progress) * currentKnockbackReceivedMultiplier);
            return;
        }

        if (IsLocked || isDodging || controls == null) return;

        if (controls.rotateRightHeld) rb.MoveRotation(rb.rotation - rotationSpeed * Time.fixedDeltaTime);

        if (controls.moveForwardHeld) rb.linearVelocity = (Vector2)transform.up * moveSpeed;
        else if (rb.linearVelocity.magnitude <= moveSpeed + 0.1f) rb.linearVelocity = Vector2.zero;
    }

    public void ApplyExplosiveKnockback(Vector2 direction, float force, int attackerID)
    {
        // getting hit cancels a lunge or whiff
        isLungingForward = false;
        isWhiffRecovering = false;

        isBeingKnockedBack = true;
        knockbackDirection = direction.normalized;
        knockbackMaxForce = force;
        knockbackTimer = 0f;

        consecutiveHitCount = lastAttackerIndex == attackerID ? consecutiveHitCount + 1 : 1;
        RegisterInteractionData(attackerID);
    }

    private void OnCollisionEnter2D(Collision2D collision) => HandleDynamicPhysicsPushTracking(collision);
    private void OnCollisionStay2D(Collision2D collision) => HandleDynamicPhysicsPushTracking(collision);

    private void HandleDynamicPhysicsPushTracking(Collision2D collision)
    {
        if (!collision.gameObject.TryGetComponent<TopDownMovement>(out var enemy)) return;

        bool pushing = collision.relativeVelocity.magnitude >= minimumVelocityThreshold || (controls != null && controls.moveForwardHeld);
        Vector2 toEnemy = (enemy.transform.position - transform.position).normalized;

        if (pushing && Vector2.Dot(transform.up, toEnemy) >= intentionalDirectionThreshold)
        {
            enemy.RegisterInteractionData(playerIndex);
            hasDealtDamageThisRound = true;
        }
    }

    public void RegisterInteractionData(int attackerID)
    {
        lastInteractionTimestamp = Time.time;
        lastAttackerIndex = attackerID;
    }

    private void PerformDodge()
    {
        lastDodgeTime = Time.time;
        Vector2 dodgeDir = dodgeLeftNext ? -transform.right : transform.right;
        dodgeLeftNext = !dodgeLeftNext;
        StartCoroutine(DodgeRoutine(dodgeDir));
    }

    private IEnumerator DodgeRoutine(Vector2 dir)
    {
        isDodging = true;
        Vector2 start = rb.position;
        Vector2 end = start + dir.normalized * dodgeDistance;
        float duration = dodgeDistance / dodgeSpeed;

        for (float t = 0f; t < duration; )
        {
            t += Time.deltaTime;
            rb.position = Vector2.Lerp(start, end, t / duration);
            yield return null;
        }

        rb.position = end;
        isDodging = false;
    }

    private void ExecuteInstantShove()
    {
        lastPushTime = Time.time;

        if (playerAudioSource != null && shoveAudioClips != null && shoveAudioClips.Length > 0)
        {
            AudioClip clip = shoveAudioClips[currentShoveAudioIndex];
            if (clip != null) playerAudioSource.PlayOneShot(clip); // PlayOneShot doesn't cut off when mashed
            currentShoveAudioIndex = (currentShoveAudioIndex + 1) % shoveAudioClips.Length;
        }

        StartCoroutine(ShoveAttackRoutine());
    }

    private IEnumerator ShoveAttackRoutine()
    {
        isLungingForward = true;
        Vector2 lungeDirection = transform.up;
        rb.linearVelocity = lungeDirection * shoveLungeForce;

        yield return new WaitForSeconds(lungeDuration);

        rb.linearVelocity = Vector2.zero;
        isLungingForward = false;

        // sweep for an enemy at the end of the lunge
        Vector2 strikeOrigin = (Vector2)transform.position + lungeDirection * shoveOffsetDistance;
        foreach (var col in Physics2D.OverlapCircleAll(strikeOrigin, shoveRadius, hitAnythingWithRigidbody ? ~0 : playerLayer.value))
        {
            if (col.gameObject == gameObject || col.transform.IsChildOf(transform) || col.isTrigger) continue;

            Rigidbody2D enemyRb = col.attachedRigidbody != null ? col.attachedRigidbody : col.GetComponentInParent<Rigidbody2D>();
            if (enemyRb == null || enemyRb.gameObject == gameObject) continue;
            if (!enemyRb.TryGetComponent<TopDownMovement>(out var enemy) || enemy.isDodging) continue;

            hasDealtDamageThisRound = true;
            enemy.ApplyExplosiveKnockback(lungeDirection, pushForce, playerIndex);
            if (JuiceManager.Instance != null) JuiceManager.Instance.TriggerImpactJuice(0.06f, 0.15f, 0.12f, 0.15f);
            yield break;
        }

        // missed: lock the player for the whiff penalty
        isWhiffRecovering = true;
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(whiffCooldownPenalty);
        isWhiffRecovering = false;
    }

    private void OnDrawGizmos()
    {
        if (!showDebugTraces) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + transform.up * shoveOffsetDistance, shoveRadius);
    }
}
