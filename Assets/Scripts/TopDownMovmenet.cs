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

    [Header("Juice & Knockback Physics Curves")]
    [SerializeField] private AnimationCurve knockbackCurve = AnimationCurve.Linear(0, 1, 1, 0);
    [SerializeField] private float knockbackDuration = 0.45f;

    [Header("Debug & Safety Configuration")]
    [Tooltip("If true, ignores layer setup and hits ANY Rigidbody2D nearby (Great for debugging!).")]
    [SerializeField] private bool hitAnythingWithRigidbody = true;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private bool showDebugTraces = true;

    [Header("Control Script Reference")]
    public MobileControls controls;

    private Rigidbody2D rb;
    private bool isDodging = false;
    private float lastDodgeTime = -999f;
    private float lastPushTime = -999f;

    // --- NEW ENGINE ATTR_VARIABLES ---
    [HideInInspector] public int playerIndex = 0;
    [HideInInspector] public int lastAttackerIndex = -1; // -1 means no one has hit them yet
    [HideInInspector] public int consecutiveHitCount = 0; // Tracking for Combo KOs
    [HideInInspector] public bool hasDealtDamageThisRound = false; // Engagement Check

    // Dynamic penalty scaling if the player didn't engage last round
    [HideInInspector] public float currentKnockbackReceivedMultiplier = 1.0f;

    private bool isBeingKnockedBack = false;
    private Vector2 knockbackDirection;
    private float knockbackMaxForce;
    private float knockbackTimer;

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

        if (controls.dodgePressed)
        {
            if (Time.time - lastDodgeTime >= dodgeCooldown)
            {
                controls.ResetDodge();
                PerformDodge();
            }
            else controls.ResetDodge();
        }

        if (controls.pushPressed)
        {
            if (Time.time - lastPushTime >= pushCooldown)
            {
                controls.ResetPush();
                ExecuteInstantShove();
            }
            else controls.ResetPush();
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
                // Apply the incoming force multiplied by any passive danger penalty multiplier!
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

        if (controls.rotateRightHeld)
        {
            float rotAmount = -1f * rotationSpeed * Time.fixedDeltaTime;
            rb.MoveRotation(rb.rotation + rotAmount);
        }

        if (controls.moveForwardHeld)
        {
            rb.linearVelocity = (Vector2)transform.up * moveSpeed;
        }
        else
        {
            if (rb.linearVelocity.magnitude <= moveSpeed + 0.1f)
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

        // Track combo states if hit consecutively by the same attacker
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

        foreach (var col in hitColliders)
        {
            if (col.gameObject == gameObject || col.transform.IsChildOf(transform) || col.isTrigger)
                continue;

            Rigidbody2D enemyRb = col.attachedRigidbody != null ? col.attachedRigidbody : col.GetComponentInParent<Rigidbody2D>();

            if (enemyRb != null)
            {
                if (enemyRb.gameObject == gameObject) continue;

                if (enemyRb.TryGetComponent<TopDownMovement>(out var enemyMovement))
                {
                    if (enemyMovement.isDodging) continue;

                    // Flag that this player actively engaged in combat
                    this.hasDealtDamageThisRound = true;

                    // Pass our player index to the victim for KO credit processing
                    enemyMovement.ApplyExplosiveKnockback(dir, pushForce, this.playerIndex);

                    if (JuiceManager.Instance != null)
                    {
                        JuiceManager.Instance.TriggerImpactJuice(0.06f, 0.15f, 0.12f, 0.15f);
                    }
                    return;
                }
            }
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