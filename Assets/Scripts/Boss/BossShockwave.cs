using UnityEngine;

public class BossShockwave : MonoBehaviour
{
    private Vector2 direction;
    private Vector2 startPosition;

    private float speed;
    private float maxDistance;
    private int damage;
    private float knockbackForce;

    private bool hasHitPlayer;

    private SpriteRenderer spriteRenderer;

    [Header("Ground")]
    [SerializeField, Min(0.1f)]
    private float groundCheckDistance = 5f;

    [SerializeField]
    private float groundOffset = 0f;

    private float groundY;

    private void Awake()
    {
        spriteRenderer =
            GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
        {
            spriteRenderer =
                GetComponentInChildren<SpriteRenderer>();
        }
    }

    public void Initialize(
        Vector2 moveDirection,
        float moveSpeed,
        float distance,
        int attackDamage,
        float knockback)
    {
        direction =
            moveDirection.normalized;

        speed =
            moveSpeed;

        maxDistance =
            distance;

        damage =
            attackDamage;

        knockbackForce =
            knockback;

        UpdateVisualDirection();

        // 생성될 때 현재 층의 바닥을
        // 딱 한 번만 찾는다.
        FindGround();

        // 바닥에 정확하게 붙인다.
        transform.position =
            new Vector3(
                transform.position.x,
                groundY,
                transform.position.z
            );

        startPosition =
            transform.position;
    }

    private void Update()
    {
        // X축으로만 이동.
        // Y축은 처음 찾은 바닥 높이로 고정.
        transform.position =
            new Vector3(
                transform.position.x +
                direction.x *
                speed *
                Time.deltaTime,

                groundY,

                transform.position.z
            );

        float traveledDistance =
            Mathf.Abs(
                transform.position.x -
                startPosition.x
            );

        if (traveledDistance >=
            maxDistance)
        {
            Destroy(gameObject);
        }
    }

    private void FindGround()
    {
        RaycastHit2D[] hits =
            Physics2D.RaycastAll(
                transform.position,
                Vector2.down,
                groundCheckDistance
            );

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (!hit.collider.CompareTag("Ground"))
                continue;

            groundY =
                hit.point.y +
                groundOffset;

            return;
        }

        // Ground를 못 찾았으면
        // 생성 위치의 Y를 그대로 사용
        groundY =
            transform.position.y;
    }

    private void UpdateVisualDirection()
    {
        if (spriteRenderer == null)
            return;

        // 현재 Sprite는 기본 방향이 왼쪽.
        // 오른쪽 진행일 때 반전.
        spriteRenderer.flipX =
            direction.x > 0f;
    }

    private void OnTriggerEnter2D(
        Collider2D other)
    {
        if (hasHitPlayer)
            return;

        PlayerHealth playerHealth =
            other.GetComponent<PlayerHealth>();

        if (playerHealth == null)
        {
            playerHealth =
                other.GetComponentInParent
                    <PlayerHealth>();
        }

        if (playerHealth == null)
            return;

        hasHitPlayer = true;

        playerHealth.TakeDamage(
            damage,
            direction,
            knockbackForce
        );
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawLine(
            transform.position,
            transform.position +
            Vector3.down *
            groundCheckDistance
        );
    }
}