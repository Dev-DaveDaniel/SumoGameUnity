using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class TopDownMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 250f;

    [Header("Dodge")]
    public float dodgeDistance = 3f;
    public float dodgeSpeed = 15f;
    public float dodgeCooldown = 0.3f;

    [Header("Push / Shove")]
    public float pushForce = 25f;
    public float pushCooldown = 0.3f;
    [SerializeField] private float shoveRadius = 0.8f;
    [SerializeField] private float shoveOffsetDistance = 0.9f;
    public float shoveLungeForce = 12f;
    public float lungeDuration = 0.15f;
    public float whiffCooldownPenalty = 0.8f; // movement/dodge lock when shove misses

    [Header("Interaction Tracking")]
    [SerializeField] private float minimumVelocityThreshold = 1.5f;
    [SerializeField] private float intentionalDirectionThreshold = 0.25f; // dot(facing, toEnemy) needed to count as a push
    public float trackingWindowDuration = 2.0f; // seconds a hit stays valid for kill credit

    [Header("Knockback")]
    [SerializeField] private float knockbackDuration = 0.45f;

    [Header("Audio")]
    [SerializeField] private AudioSource playerAudioSource;
    [SerializeField] private AudioClip[] shoveAudioClips;

    [Header("Debug")]
    [SerializeField] private bool hitAnythingWithRigidbody = true;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private bool showDebugTraces = true;

    public MobileControls controls;

    [HideInInspector] public bool isDodging;
    [HideInInspector] public int playerIndex;
    [HideInInspector] public int lastAttackerIndex = -1;
    [HideInInspector] public float lastInteractionTimestamp = -999f;
    [HideInInspector] public int consecutiveHitCount;
    [HideInInspector] public bool hasDealtDamageThisRound;
    [HideInInspector] public float currentKnockbackReceivedMultiplier = 1.0f;

    private readonly AnimationCurve knockbackCurve = new AnimationCurve(
        new Keyframe(0f, 1f, 0f, -2.5f),
        new Keyframe(0.25f, 0.15f, -0.6f, -0.6f),
        new Keyframe(1f, 0f, 0f, 0f));

    private Rigidbody2D rb;
    private bool dodgeLeftNext, isWhiffRecovering, isLungingForward, isBeingKnockedBack;
    private float lastDodgeTime = -999f, lastPushTime = -999f;
    private int currentShoveAudioIndex;
    private Vector2 knockbackDirection;
    private float knockbackMaxForce, knockbackTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (playerAudioSource == null) playerAudioSource = GetComponent<AudioSource>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    private void Update()
    {
        if (controls == null) return;

        bool locked = isWhiffRecovering || isLungingForward || isBeingKnockedBack;
        bool dodge = controls.dodgePressed, push = controls.pushPressed;
        // Consume presses every frame so they don't buffer through locks or cooldowns.
        if (dodge || locked) controls.ResetDodge();
        if (push || locked) controls.ResetPush();
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

        if (isWhiffRecovering || isLungingForward || isDodging || controls == null) return;

        if (controls.rotateRightHeld) rb.MoveRotation(rb.rotation - rotationSpeed * Time.fixedDeltaTime);

        if (controls.moveForwardHeld) rb.linearVelocity = (Vector2)transform.up * moveSpeed;
        else if (rb.linearVelocity.magnitude <= moveSpeed + 0.1f) rb.linearVelocity = Vector2.zero;
    }

    public void ApplyExplosiveKnockback(Vector2 direction, float force, int attackerID)
    {
        isLungingForward = false; // getting hit interrupts lunge/whiff
        isWhiffRecovering = false;
        isBeingKnockedBack = true;
        knockbackDirection = direction.normalized;
        knockbackMaxForce = force;
        knockbackTimer = 0f;

        consecutiveHitCount = lastAttackerIndex == attackerID ? consecutiveHitCount + 1 : 1;
        RegisterInteractionData(attackerID);
    }

    private void OnCollisionEnter2D(Collision2D collision) => TrackPhysicsPush(collision);
    private void OnCollisionStay2D(Collision2D collision) => TrackPhysicsPush(collision);

    private void TrackPhysicsPush(Collision2D collision)
    {
        if (!collision.gameObject.TryGetComponent<TopDownMovement>(out var enemy)) return;

        bool pressing = controls != null && controls.moveForwardHeld;
        if (collision.relativeVelocity.magnitude < minimumVelocityThreshold && !pressing) return;

        Vector2 toEnemy = (enemy.transform.position - transform.position).normalized;
        if (Vector2.Dot(transform.up, toEnemy) >= intentionalDirectionThreshold)
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
        Vector2 dir = dodgeLeftNext ? -transform.right : transform.right;
        dodgeLeftNext = !dodgeLeftNext;
        StartCoroutine(DodgeRoutine(dir));
    }

    private IEnumerator DodgeRoutine(Vector2 dir)
    {
        isDodging = true;
        Vector2 start = rb.position, end = start + dir.normalized * dodgeDistance;
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
        PlaySequentialShoveAudio();
        StartCoroutine(ShoveAttackRoutine());
    }

    private void PlaySequentialShoveAudio()
    {
        if (playerAudioSource == null || shoveAudioClips == null || shoveAudioClips.Length == 0) return;
        var clip = shoveAudioClips[currentShoveAudioIndex];
        if (clip != null) playerAudioSource.PlayOneShot(clip); // PlayOneShot doesn't cut off when mashed
        currentShoveAudioIndex = (currentShoveAudioIndex + 1) % shoveAudioClips.Length;
    }

    private IEnumerator ShoveAttackRoutine()
    {
        isLungingForward = true;
        Vector2 dir = transform.up;
        rb.linearVelocity = dir * shoveLungeForce;
        yield return new WaitForSeconds(lungeDuration);
        rb.linearVelocity = Vector2.zero;
        isLungingForward = false;

        Vector2 origin = (Vector2)transform.position + dir * shoveOffsetDistance;
        LayerMask mask = hitAnythingWithRigidbody ? ~0 : playerLayer;

        foreach (var col in Physics2D.OverlapCircleAll(origin, shoveRadius, mask))
        {
            if (col.isTrigger || col.transform.IsChildOf(transform)) continue;

            Rigidbody2D enemyRb = col.attachedRigidbody != null ? col.attachedRigidbody : col.GetComponentInParent<Rigidbody2D>();
            if (enemyRb == null || enemyRb.gameObject == gameObject) continue;
            if (!enemyRb.TryGetComponent<TopDownMovement>(out var enemy) || enemy.isDodging) continue;

            hasDealtDamageThisRound = true;
            enemy.ApplyExplosiveKnockback(dir, pushForce, playerIndex);
            if (JuiceManager.Instance != null) JuiceManager.Instance.TriggerImpactJuice(0.06f, 0.15f, 0.12f, 0.15f);
            yield break;
        }

        // Missed: lock movement and actions as a whiff penalty.
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
