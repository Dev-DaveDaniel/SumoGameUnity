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
    [Tooltip("Size of the push area window.")]
    [SerializeField] private float shoveRadius = 0.8f;
    [Tooltip("Distance out in front of the player center to check for targets.")]
    [SerializeField] private float shoveOffsetDistance = 0.9f;

    [Header("New Whiff & Stagger Mechanical Profiles")]
    [SerializeField] private float shoveLungeForceWhiff = 6f;
    [SerializeField] private float shoveLungeForceEvaded = 12f;
    [Space]
    [Tooltip("Scenario A: Duration in seconds to slow down actions when missing a shove.")]
    public float whiffRecoveryDuration = 2.0f;
    [SerializeField] private float whiffSpeedMultiplier = 0.5f;
    [SerializeField] private float whiffRotationMultiplier = 0.5f;
    [Space]
    [Tooltip("Scenario B: Duration in seconds for staggered state if your shove gets dodged.")]
    public float staggerDuration = 2.0f;
    [SerializeField] private float staggerSpeedMultiplier = 0.25f;
    [SerializeField] private float staggerRotationMultiplier = 0.2f;
    [Space]
    [Tooltip("Scenario B Reward: How long the lucky dodger keeps their speed boost.")]
    public float dodgerBoostDuration = 2.0f;
    [Tooltip("Multiplier value added to base speed when perfectly dodging (e.g., 1.35 = 35% faster).")]
    [SerializeField] private float dodgerSpeedBoostMultiplier = 1.35f;

    [Header("Juice & Knockback Physics Curves")]
    [SerializeField] private AnimationCurve knockbackCurve = AnimationCurve.Linear(0, 1, 1, 0);
    [SerializeField] private float knockbackDuration = 0.45f;

    [Header("Debug & Safety Configuration")]
    [Tooltip("If true, ignores layer setup and hits ANY Rigidbody2D nearby.")]
    [SerializeField] private bool hitAnythingWithRigidbody = true;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private bool showDebugTraces = true;

    [Header("Control Script Reference")]
    public MobileControls controls;

    private Rigidbody2D rb;
    private bool isDodging = false;
    private float lastDodgeTime = -999f;
    private float lastPushTime = -999f;

    // --- GAME ENGINE SYSTEM SYSTEM VARIABLES ---
    [HideInInspector] public int playerIndex = 0;
    [HideInInspector] public int lastAttackerIndex = -1;
    [HideInInspector] public int consecutiveHitCount = 0;
    [HideInInspector] public bool hasDealtDamageThisRound = false;

    [HideInInspector] public float currentKnockbackReceivedMultiplier = 1.0f;

    private bool isBeingKnockedBack = false;
    private Vector2 knockbackDirection;
    private float knockbackMaxForce;
    private float knockbackTimer;

    // Dynamic State Trackers
    private bool isInsideWhiffRecovery = false;
    private bool isStaggered = false;
    private float dynamicSpeedModifier = 1.0f;
    private float dynamicRotationModifier = 1.0f;

    // Public properties to share state checks safely across instances
    public bool IsDodging => isDodging;
    public bool CanAct => !isDodging && !isInsideWhiffRecovery && !isStaggered && !isBeingKnockedBack;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        knockbackCurve = new AnimationCurve(
            new Keyframe(0f, 1f, 0f, -2.5f),
            new Keyframe(0.25f, 0.15f, -0.6f, -0.6f),
            new Keyframe(1f, 0f, 0f, 0f)
        );
    }

    private void Update()
    {
        if (controls == null) return;

        // Process Dodge Intent
        if (controls.dodgePressed)
        {
            if (CanAct && (Time.time - lastDodgeTime >= dodgeCooldown))
            {
                controls.ResetDodge();
                PerformDodge();
            }
            else
            {
                controls.ResetDodge(); // Consume input if button pressed during penalty windows
            }
        }

        // Process Push Intent
        if (controls.pushPressed)
        {
            if (CanAct && (Time.time - lastPushTime >= pushCooldown))
            {
                controls.ResetPush();
                ExecuteInstantShove();
            }
            else
            {
                controls.ResetPush(); // Input drop tracking safely managed
            }
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
            else
            {
                float currentForceMultiplier = knockbackCurve.Evaluate(progress);
                rb.linearVelocity = knockbackDirection * (knockbackMaxForce * currentForceMultiplier * currentKnockbackReceivedMultiplier);
            }
            return;
        }

        if (isDodging) return;
        HandleMovementAndRotation();
    }

    private void HandleMovementAndRotation()
    {
        if (controls == null) return;

        // Calculate custom modifiers based on active whiffs, staggers, or dodge boosts
        float activeRotationSpeed = rotationSpeed * dynamicRotationModifier;
        float activeMoveSpeed = moveSpeed * dynamicSpeedModifier;

        if (controls.rotateRightHeld)
        {
            float rotAmount = -1f * activeRotationSpeed * Time.fixedDeltaTime;
            rb.MoveRotation(rb.rotation + rotAmount);
        }

        if (controls.moveForwardHeld)
        {
            rb.linearVelocity = (Vector2)transform.up * activeMoveSpeed;
        }
        else
        {
            if (rb.linearVelocity.magnitude <= activeMoveSpeed + 0.1f)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }
    }

    public void ApplyExplosiveKnockback(Vector2 direction, float force, int attackerID)
    {
        isBeingKnockedBack = true;
        knockbackDirection = direction.normalized;
        knockbackMaxForce = force;
        knockbackTimer = 0f;

        if (lastAttackerIndex == attackerID)
        {
            consecutiveHitCount++;
        }
        else
        {
            lastAttackerIndex = attackerID;
            consecutiveHitCount = 1;
        }
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

        float t = 0f;
        while (t < duration)
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
        Vector2 dir = transform.up;
        Vector2 strikeOrigin = (Vector2)transform.position + (dir * shoveOffsetDistance);

        LayerMask targetMask = hitAnythingWithRigidbody ? ~0 : playerLayer;
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(strikeOrigin, shoveRadius, targetMask);

        TopDownMovement targetOpponent = null;

        foreach (var col in hitColliders)
        {
            if (col.gameObject == gameObject || col.transform.IsChildOf(transform) || col.isTrigger)
                continue;

            Rigidbody2D enemyRb = col.attachedRigidbody != null ? col.attachedRigidbody : col.GetComponentInParent<Rigidbody2D>();
            if (enemyRb != null && enemyRb.gameObject != gameObject)
            {
                if (enemyRb.TryGetComponent<TopDownMovement>(out var enemyMovement))
                {
                    targetOpponent = enemyMovement;
                    break; // Found our prime combat target reference
                }
            }
        }

        // --- SCENARIO A: WHIFF (No valid target in front) ---
        if (targetOpponent == null)
        {
            // Lunge Forward force application
            rb.AddForce(dir * shoveLungeForceWhiff, ForceMode2D.Impulse);

            // Execute 2-Second slow down penalty routine
            StartCoroutine(WhiffRecoveryRoutine());
            return;
        }

        // --- SCENARIO B: EVADED (Target was caught but is currently executing a dodge) ---
        if (targetOpponent.IsDodging)
        {
            // Lunges forward significantly further past them
            rb.AddForce(dir * shoveLungeForceEvaded, ForceMode2D.Impulse);

            // Enters full Staggered State profile
            StartCoroutine(AttackerStaggerRoutine());

            // Reward target/dodger with an instant temporary speed boost
            targetOpponent.RewardPerfectDodgeBoost(dodgerBoostDuration);
            return;
        }

        // --- SCENARIO C: STANDARD VALID IMPACT ---
        this.hasDealtDamageThisRound = true;
        targetOpponent.ApplyExplosiveKnockback(dir, pushForce, this.playerIndex);

        if (JuiceManager.Instance != null)
        {
            JuiceManager.Instance.TriggerImpactJuice(0.06f, 0.15f, 0.12f, 0.15f);
        }
    }

    // SCENARIO A: WHIFF ACTION SLOWDOWN
    private IEnumerator WhiffRecoveryRoutine()
    {
        isInsideWhiffRecovery = true;
        dynamicSpeedModifier = whiffSpeedMultiplier;
        dynamicRotationModifier = whiffRotationMultiplier;

        yield return new WaitForSeconds(whiffRecoveryDuration);

        dynamicSpeedModifier = 1.0f;
        dynamicRotationModifier = 1.0f;
        isInsideWhiffRecovery = false;
    }

    // SCENARIO B: ATTACKER STAGGER RECOVERY
    private IEnumerator AttackerStaggerRoutine()
    {
        isStaggered = true;
        dynamicSpeedModifier = staggerSpeedMultiplier;
        dynamicRotationModifier = staggerRotationMultiplier;

        yield return new WaitForSeconds(staggerDuration);

        dynamicSpeedModifier = 1.0f;
        dynamicRotationModifier = 1.0f;
        isStaggered = false;
    }

    // SCENARIO B: REWARD SYSTEM COROUTINE
    public void RewardPerfectDodgeBoost(float duration)
    {
        StartCoroutine(SpeedBoostDurationRoutine(duration));
    }

    private IEnumerator SpeedBoostDurationRoutine(float duration)
    {
        dynamicSpeedModifier = dodgerSpeedBoostMultiplier;

        yield return new WaitForSeconds(duration);

        // Reset speed modifier if we aren't currently clamped by an active penalty state
        if (!isStaggered && !isInsideWhiffRecovery)
        {
            dynamicSpeedModifier = 1.0f;
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugTraces) return;
        Vector2 dir = transform.up;
        Vector3 strikeOrigin = transform.position + (Vector3)(dir * shoveOffsetDistance);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(strikeOrigin, shoveRadius);
    }
}