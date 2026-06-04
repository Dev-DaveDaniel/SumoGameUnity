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
    [Tooltip("How much forward force is applied to lunge the player during a shove execution.")]
    public float shoveLungeForce = 12f;
    [Tooltip("Duration of the forward attack lunging dash movement.")]
    public float lungeDuration = 0.15f;
    [Tooltip("Total recovery penalty duration if the shove misses entirely (locks movement/dodge).")]
    public float whiffCooldownPenalty = 0.8f;

    private bool isWhiffRecovering = false;
    private bool isLungingForward = false;

    [Header("Physics Interaction System")]
    [Tooltip("Minimum collision speed required to register an interaction.")]
    [SerializeField] private float minimumVelocityThreshold = 1.5f;
    [Tooltip("0 = side swipe/any hit, 0.5 = intentional forward pushing angle, 0.8 = strict direct hit.")]
    [SerializeField] private float intentionalDirectionThreshold = 0.25f;
    [Tooltip("How many seconds an interaction remains valid for kill credit.")]
    public float trackingWindowDuration = 2.0f;

    [Header("Juice & Knockback Physics Curves")]
    [SerializeField] private AnimationCurve knockbackCurve = AnimationCurve.Linear(0, 1, 1, 0);
    [SerializeField] private float knockbackDuration = 0.45f;

    [Header("Shove Audio Juice Engine")]
    [Tooltip("The local AudioSource component attached to this player prefab.")]
    [SerializeField] private AudioSource playerAudioSource;
    [Tooltip("Add multiple unique grunt/swoosh variations here to cycle through when shoving.")]
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

    // --- TRACKING PARAMETERS ---
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

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // Auto-assign AudioSource if left blank in the Inspector layout
        if (playerAudioSource == null)
        {
            playerAudioSource = GetComponent<AudioSource>();
        }

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

        // BLOCK: Cannot dodge or push if currently locked in a whiff recovery or actively lunging
        if (isWhiffRecovering || isLungingForward || isBeingKnockedBack)
        {
            controls.ResetDodge();
            controls.ResetPush();
            return;
        }

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
                rb.linearVelocity = knockbackDirection * (knockbackMaxForce * currentForceMultiplier * currentKnockbackReceivedMultiplier);
            }
            return;
        }

        // BLOCK: Physics movement completely cut if recovering from a whiff or lunging
        if (isWhiffRecovering || isLungingForward || isDodging) return;

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
        // Interrupt attacks if hit mid-lunge or mid-whiff
        isLungingForward = false;
        isWhiffRecovering = false;

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

        RegisterInteractionData(attackerID);
    }

    private void OnCollisionEnter2D(Collision2D collision) => HandleDynamicPhysicsPushTracking(collision);
    private void OnCollisionStay2D(Collision2D collision) => HandleDynamicPhysicsPushTracking(collision);

    private void HandleDynamicPhysicsPushTracking(Collision2D collision)
    {
        if (collision.gameObject.TryGetComponent<TopDownMovement>(out var enemyMovement))
        {
            float relativeVelocity = collision.relativeVelocity.magnitude;
            bool isPressingButtons = controls != null && (controls.moveForwardHeld);

            if (relativeVelocity >= minimumVelocityThreshold || isPressingButtons)
            {
                Vector2 vectorToEnemy = (enemyMovement.transform.position - transform.position).normalized;
                float forwardIntentionalityDot = Vector2.Dot(transform.up, vectorToEnemy);

                if (forwardIntentionalityDot >= intentionalDirectionThreshold)
                {
                    enemyMovement.RegisterInteractionData(this.playerIndex);
                    this.hasDealtDamageThisRound = true;
                }
            }
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

        // --- AUDIO CYCLING INSTIGATION POOL ---
        PlaySequentialShoveAudio();

        StartCoroutine(ShoveAttackRoutine());
    }

    private void PlaySequentialShoveAudio()
    {
        if (playerAudioSource == null || shoveAudioClips == null || shoveAudioClips.Length == 0) return;

        // Extract clip variant located at pointer
        AudioClip clipToPlay = shoveAudioClips[currentShoveAudioIndex];

        if (clipToPlay != null)
        {
            // PlayOneShot prevents cutting off audio elements mid-clip if mashed quickly
            playerAudioSource.PlayOneShot(clipToPlay);
        }

        // Advance to next index; loop back cleanly using the remainder operator
        currentShoveAudioIndex = (currentShoveAudioIndex + 1) % shoveAudioClips.Length;
    }

    private IEnumerator ShoveAttackRoutine()
    {
        isLungingForward = true;
        Vector2 lungeDirection = transform.up;

        // Apply the mechanical physical lunge forward force
        rb.linearVelocity = lungeDirection * shoveLungeForce;

        yield return new WaitForSeconds(lungeDuration);

        rb.linearVelocity = Vector2.zero;
        isLungingForward = false;

        // Sweep for enemies at the apex/finish of our lunge window
        Vector2 strikeOrigin = (Vector2)transform.position + (lungeDirection * shoveOffsetDistance);
        LayerMask targetMask = hitAnythingWithRigidbody ? ~0 : playerLayer;
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(strikeOrigin, shoveRadius, targetMask);

        bool hitRegistered = false;

        foreach (var col in hitColliders)
        {
            if (col.gameObject == gameObject || col.transform.IsChildOf(transform) || col.isTrigger)
                continue;

            Rigidbody2D enemyRb = col.attachedRigidbody != null ? col.attachedRigidbody : col.GetComponentInParent<Rigidbody2D>();

            if (enemyRb != null && enemyRb.gameObject != gameObject)
            {
                if (enemyRb.TryGetComponent<TopDownMovement>(out var enemyMovement))
                {
                    if (enemyMovement.isDodging) continue;

                    this.hasDealtDamageThisRound = true;
                    enemyMovement.ApplyExplosiveKnockback(lungeDirection, pushForce, this.playerIndex);
                    hitRegistered = true;

                    if (JuiceManager.Instance != null)
                    {
                        JuiceManager.Instance.TriggerImpactJuice(0.06f, 0.15f, 0.12f, 0.15f);
                    }
                    break;
                }
            }
        }

        // If the lunge complete and nobody was found, lock down everything into a whiff state
        if (!hitRegistered)
        {
            yield return StartCoroutine(WhiffRecoveryPenaltyRoutine());
        }
    }

    private IEnumerator WhiffRecoveryPenaltyRoutine()
    {
        isWhiffRecovering = true;
        rb.linearVelocity = Vector2.zero; // Stops all slide drift completely during punishment

        // Locks the player completely for the duration defined by whiffCooldownPenalty
        yield return new WaitForSeconds(whiffCooldownPenalty);

        isWhiffRecovering = false;
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