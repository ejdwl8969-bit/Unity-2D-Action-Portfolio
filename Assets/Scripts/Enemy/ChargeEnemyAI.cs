using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ChargeEnemyAI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Detection")]
    [SerializeField, Min(0f)]
    private float detectRange = 7f;

    [SerializeField, Min(0f)]
    private float chargeRange = 5f;

    [SerializeField, Min(0f)]
    private float verticalDetectRange = 1.5f;

    [Header("Movement")]
    [SerializeField, Min(0f)]
    private float moveSpeed = 1.5f;

    [Header("Charge")]
    [SerializeField, Min(0.1f)]
    private float chargeSpeed = 10f;

    [SerializeField, Min(0f)]
    private float chargePrepareTime = 0.5f;

    [SerializeField, Min(0.05f)]
    private float chargeDuration = 0.45f;

    [SerializeField, Min(0f)]
    private float chargeCooldown = 2f;

    [Header("Cliff Avoidance")]
    [SerializeField] private bool showCliffDebug;

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    private bool isCharging;
    private bool isPreparing;

    private float lastChargeTime =
        -Mathf.Infinity;

    private Vector2 chargeDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        FindTargetIfNeeded();
    }

    private void FixedUpdate()
    {
        if (isCharging ||
            isPreparing)
        {
            return;
        }

        if (target == null)
        {
            FindTargetIfNeeded();
            StopMovement();
            return;
        }

        float xDistance =
            Mathf.Abs(
                target.position.x -
                transform.position.x
            );

        float yDistance =
            Mathf.Abs(
                target.position.y -
                transform.position.y
            );

        // Ignore targets on another platform level to avoid charging through floors.
        if (yDistance > verticalDetectRange)
        {
            StopMovement();
            return;
        }

        if (xDistance > detectRange)
        {
            StopMovement();
            return;
        }

        FaceTarget();

        if (CanCharge(xDistance))
        {
            StartCoroutine(
                ChargeRoutine()
            );

            return;
        }

        MoveTowardTarget();
    }

    private bool CanCharge(float distance)
    {
        if (distance > chargeRange)
            return false;

        if (Time.time < lastChargeTime + chargeCooldown)
            return false;

        float expectedTravel = chargeSpeed * chargeDuration;
        return EnemyGroundDetector.HasGroundAheadForDistance(transform, target.position.x - transform.position.x, expectedTravel);
    }

    private void MoveTowardTarget()
    {
        float directionX = target.position.x > transform.position.x ? 1f : -1f;
        if (!EnemyGroundDetector.HasGroundAhead(transform, directionX))
        {
            StopMovement();
            return;
        }

        rb.linearVelocity = new Vector2(directionX * moveSpeed, rb.linearVelocity.y);
        SetMoving(true);
        SetFacing(directionX);
    }

    private IEnumerator ChargeRoutine()
    {
        isPreparing = true;

        StopMovement();

        chargeDirection =
            target.position -
            transform.position;

        chargeDirection =
            new Vector2(
                Mathf.Sign(chargeDirection.x),
                0f
            );

        SetFacing(chargeDirection.x);

        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        yield return new WaitForSeconds(
            chargePrepareTime
        );

        isPreparing = false;
        isCharging = true;

        SetMoving(true);

        float elapsedTime = 0f;

        while (elapsedTime <
               chargeDuration)
        {
            if (!EnemyGroundDetector.HasGroundAhead(transform, chargeDirection.x))
            {
                StopMovement();
                isCharging = false;
                lastChargeTime = Time.time;
                yield break;
            }

            rb.linearVelocity =
                new Vector2(
                    chargeDirection.x *
                    chargeSpeed,
                    rb.linearVelocity.y
                );

            elapsedTime +=
                Time.fixedDeltaTime;

            yield return new WaitForFixedUpdate();
        }

        StopMovement();

        isCharging = false;

        lastChargeTime =
            Time.time;
    }

    private void StopMovement()
    {
        rb.linearVelocity =
            new Vector2(
                0f,
                rb.linearVelocity.y
            );

        SetMoving(false);
    }

    private void SetMoving(bool isMoving)
    {
        if (animator != null)
        {
            animator.SetBool(
                "IsMoving",
                isMoving
            );
        }
    }

    private void FaceTarget()
    {
        if (target == null)
            return;

        float directionX =
            target.position.x >
            transform.position.x
                ? 1f
                : -1f;

        SetFacing(directionX);
    }

    private void SetFacing(float directionX)
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.flipX =
            directionX < 0f;
    }

    private void FindTargetIfNeeded()
    {
        if (target != null)
            return;

        PlayerController player =
            FindFirstObjectByType<PlayerController>();

        if (player != null)
        {
            target = player.transform;
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();

        isCharging = false;
        isPreparing = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            detectRange
        );

        Gizmos.DrawWireSphere(
            transform.position,
            chargeRange
        );
        float direction = target != null ? Mathf.Sign(target.position.x - transform.position.x) : 1f;
        EnemyGroundDetector.DrawProbeGizmo(transform, direction, showCliffDebug);
    }
}
