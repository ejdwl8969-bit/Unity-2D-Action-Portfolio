using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class RangedEnemyAI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Movement")]
    [SerializeField, Min(0f)]
    private float moveSpeed = 2f;

    [Header("Detection")]
    [SerializeField, Min(0f)]
    private float detectRange = 8f;

    [SerializeField, Min(0f)]
    private float preferredDistance = 5f;

    [SerializeField, Min(0f)]
    private float distanceTolerance = 0.5f;

    [Header("Line Of Sight")]
    [Tooltip("Geometry layers considered blockers. When empty, Ground-tagged geometry is used.")]
    [SerializeField]
    private LayerMask obstacleMask;

    [Header("Cliff Avoidance")]
    [SerializeField] private bool showCliffDebug;

    [Header("Attack")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;

    [SerializeField, Min(0.01f)]
    private float attackCooldown = 1.5f;

    [SerializeField, Min(0.1f)]
    private float projectileSpeed = 7f;

    [SerializeField, Min(0)]
    private int projectileDamage = 10;

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    private float lastAttackTime = -Mathf.Infinity;

    private Vector3 firePointStartLocalPosition;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (firePoint != null)
        {
            firePointStartLocalPosition =
                firePoint.localPosition;
        }
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
            StopMovement();
            return;
        }

        float distance =
            Vector2.Distance(
                transform.position,
                target.position
            );

        if (distance > detectRange)
        {
            StopMovement();
            return;
        }

        UpdateFacingDirection();

        HandleMovement(distance);
        TryAttack(distance);
    }

    private void HandleMovement(float distance)
    {
        float minDistance =
            preferredDistance - distanceTolerance;

        float maxDistance =
            preferredDistance + distanceTolerance;

        if (distance > maxDistance)
        {
            MoveTowardTarget();
        }
        else if (distance < minDistance)
        {
            MoveAwayFromTarget();
        }
        else
        {
            StopMovement();
        }
    }

    private void MoveTowardTarget()
    {
        float direction = target.position.x > transform.position.x ? 1f : -1f;
        if (!EnemyGroundDetector.HasGroundAhead(transform, direction))
        {
            StopMovement();
            return;
        }

        rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
        SetMoving(true);
    }

    private void MoveAwayFromTarget()
    {
        float direction = target.position.x > transform.position.x ? -1f : 1f;
        if (!EnemyGroundDetector.HasGroundAhead(transform, direction))
        {
            StopMovement();
            return;
        }

        rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
        SetMoving(true);
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

    private void UpdateFacingDirection()
    {
        if (target == null)
            return;

        bool facingLeft =
            target.position.x < transform.position.x;

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = facingLeft;
        }

        // flipX does not mirror child transforms, so mirror the FirePoint explicitly.
        if (firePoint != null)
        {
            Vector3 position =
                firePointStartLocalPosition;

            position.x =
                Mathf.Abs(
                    firePointStartLocalPosition.x
                ) *
                (facingLeft ? -1f : 1f);

            firePoint.localPosition = position;
        }
    }

    private void TryAttack(float distance)
    {
        if (distance > detectRange)
            return;

        if (!HasLineOfSight())
            return;

        if (Time.time <
            lastAttackTime + attackCooldown)
        {
            return;
        }

        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        lastAttackTime = Time.time;
    }

    // Called by the attack Animation Event to preserve authored shot timing.
    public void FireProjectile()
    {
        if (projectilePrefab == null ||
            firePoint == null ||
            target == null ||
            !HasLineOfSight())
        {
            return;
        }

        Vector2 direction =
            (target.position -
             firePoint.position).normalized;

        GameObject projectileObject =
            Instantiate(
                projectilePrefab,
                firePoint.position,
                Quaternion.identity
            );

        EnemyProjectile projectile =
            projectileObject
                .GetComponent<EnemyProjectile>();

        if (projectile == null)
        {
            Destroy(projectileObject);
            return;
        }

        projectile.Initialize(
            direction,
            projectileSpeed,
            projectileDamage
        );
    }

    private bool HasLineOfSight()
    {
        if (target == null)
            return false;

        Vector2 origin = firePoint != null
            ? (Vector2)firePoint.position
            : (Vector2)transform.position;
        Vector2 destination = target.position;
        Vector2 toTarget = destination - origin;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
            return true;

        int mask = obstacleMask.value != 0
            ? obstacleMask.value
            : Physics2D.DefaultRaycastLayers;

        RaycastHit2D[] hits = Physics2D.RaycastAll(
            origin,
            toTarget / distance,
            distance,
            mask
        );

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null || hitCollider.isTrigger)
                continue;

            if (hitCollider.transform.IsChildOf(transform) ||
                transform.IsChildOf(hitCollider.transform))
                continue;

            if (IsPlayerCollider(hitCollider))
                continue;

            if (IsGeometryCollider(hitCollider))
                return false;
        }

        return true;
    }

    private bool IsPlayerCollider(Collider2D collider)
    {
        if (target == null)
            return false;

        return collider.transform == target ||
               collider.transform.IsChildOf(target) ||
               target.IsChildOf(collider.transform);
    }

    private static bool IsGeometryCollider(Collider2D collider)
    {
        if (collider == null)
            return false;

        string tag = collider.tag;
        if (string.Equals(tag, "Ground", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tag, "Platform", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tag, "Wall", System.StringComparison.OrdinalIgnoreCase))
            return true;

        string objectName = collider.gameObject.name;
        return objectName.IndexOf("Ground", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("Platform", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("Wall", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("Walkway", System.StringComparison.OrdinalIgnoreCase) >= 0;
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            detectRange
        );

        Gizmos.DrawWireSphere(
            transform.position,
            preferredDistance
        );

        float cliffDirection = target != null ? Mathf.Sign(target.position.x - transform.position.x) : 1f;
        EnemyGroundDetector.DrawProbeGizmo(transform, cliffDirection, showCliffDebug);

        if (firePoint != null && target != null)
        {
            Gizmos.color = HasLineOfSight()
                ? Color.green
                : Color.red;
            Gizmos.DrawLine(firePoint.position, target.position);
        }
    }
}


