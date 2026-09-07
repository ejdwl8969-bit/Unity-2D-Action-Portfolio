using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;

    [Header("Detection")]
    [SerializeField] private float detectRange = 6f;
    [SerializeField] private float stopDistance = 0.8f;

    [Header("Cliff Avoidance")]
    [SerializeField] private bool showCliffDebug;

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    public float MoveSpeed
    {
        get => moveSpeed;
        set => moveSpeed = Mathf.Max(0f, value);
    }

    public Transform Target => target;

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
        if (target == null)
        {
            FindTargetIfNeeded();
            StopHorizontalMovement();
            return;
        }

        float distance = Vector2.Distance(
            transform.position,
            target.position
        );

        if (!IsTargetInRange(distance))
        {
            StopHorizontalMovement();
            return;
        }

        if (distance <= stopDistance)
        {
            StopHorizontalMovement();
            return;
        }

        MoveTowardTarget();
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

    private bool IsTargetInRange(float distance)
    {
        return distance <= detectRange;
    }

    private void MoveTowardTarget()
    {
        float direction = target.position.x > transform.position.x ? 1f : -1f;
        if (spriteRenderer != null)
            spriteRenderer.flipX = direction < 0f;

        if (!EnemyGroundDetector.HasGroundAhead(transform, direction))
        {
            StopHorizontalMovement();
            return;
        }

        rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
        if (animator != null)
            animator.SetBool("IsMoving", true);
    }

    private void StopHorizontalMovement()
    {
        rb.linearVelocity = new Vector2(
            0f,
            rb.linearVelocity.y
        );

        if (animator != null)
        {
            animator.SetBool("IsMoving", false);
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void StopMovement()
    {
        StopHorizontalMovement();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            detectRange
        );
        float direction = target != null ? Mathf.Sign(target.position.x - transform.position.x) : 1f;
        EnemyGroundDetector.DrawProbeGizmo(transform, direction, showCliffDebug);
    }
}

