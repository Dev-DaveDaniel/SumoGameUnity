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

    [Header("Shove")]
    public float pushForce = 25f;
    public float pushCooldown = 0.3f;
    [SerializeField] private float shoveRadius = 0.8f;
    [SerializeField] private float shoveOffsetDistance = 0.9f;
    public float shoveLungeForce = 12f;
    public float lungeDuration = 0.15f;
    public float whiffCooldownPenalty = 0.8f; // movement/dodge lock when a shove misses

    [Header("Physics Interaction")]
    [SerializeField] private float minimumVelocityThreshold = 1.5f;
    [SerializeField] private float intentionalDirectionThreshold = 0.25f; // 0 = any angle, 0.8 = strict head-on
    public float trackingWindowDuration = 2.0f; // seconds a hit stays valid for kill credit

    [Header("Knockback")]
    [SerializeField] private AnimationCurve knockbackCurve = AnimationCurve.Linear(0, 1, 1, 0);
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

    private Rigidbody2D rb;
    private float lastDodgeTime = -999f, lastPushTime = -999f;
    private bool dodgeLeftNext, isWhiffRecovering, isLungingForward, isBeingKnockedBack;
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

        knockbackCurve = new AnimationCurve(
            new Keyframe(0f, 1f, 0f, -2.5f),
            new Keyframe(0.25f, 0.15f, -0.6f, -0.6f),
            new Keyframe(1f, 0f, 0f, 0f));
    }

    private void Update()
    {
        if (controls == null) return;
        bool locked = isWhiffRecovering || isLungingForward || isBeingKnockedBack;

        if (controls.dodgePressed)
        {
            controls.ResetDodge();
            if (!locked && Time.time - lastDodgeTime >= dodgeCooldown) PerformDodge();
        }
        if (controls.pushPressed)
        {
            controls.ResetPush();
            if (!locked && Time.time - lastPushTime >= pushCooldown) ExecuteInstantShove();
        }
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
        // A hit interrupts any lunge or whiff in progress
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

        bool pushing = controls != null && controls.moveForwardHeld;
        if (collision.relativeVelocity.magnitude < minimumVelocityThreshold && !pushing) return;

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

        // Cycle through clip variants; PlayOneShot avoids cutting off a clip when mashed
        if (playerAudioSource != null && shoveAudioClips != null && shoveAudioClips.Length > 0)
        {
            AudioClip clip = shoveAudioClips[currentShoveAudioIndex];
            if (clip != null) playerAudioSource.PlayOneShot(clip);
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

        // Sweep for enemies at the end of the lunge
        Vector2 strikeOrigin = (Vector2)transform.position + lungeDirection * shoveOffsetDistance;
        int targetMask = hitAnythingWithRigidbody ? ~0 : playerLayer.value;

        foreach (var col in Physics2D.OverlapCircleAll(strikeOrigin, shoveRadius, targetMask))
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

        // Missed: lock movement and dodge for the whiff penalty
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
