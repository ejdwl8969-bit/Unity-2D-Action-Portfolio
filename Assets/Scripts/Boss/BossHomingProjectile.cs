using UnityEngine;

public class BossHomingProjectile : MonoBehaviour
{
    private Transform target;

    private Vector2 direction;

    private float speed;
    private float homingTime;
    private float turnSpeed;
    private float lifeTime;

    private int damage;
    private float knockbackForce;

    private float homingTimer;
    private float lifeTimer;

    private bool hasHitPlayer;

    [Header("Visual")]
    [SerializeField]
    private bool rotateWithDirection = true;

    [SerializeField]
    private float spriteAngleOffset = 90f;

    public void Initialize(
        Transform playerTarget,
        Vector2 startDirection,
        float projectileSpeed,
        float projectileHomingTime,
        float projectileTurnSpeed,
        float projectileLifeTime,
        int projectileDamage,
        float knockback)
    {
        target = playerTarget;

        direction =
            startDirection.normalized;

        speed =
            projectileSpeed;

        homingTime =
            projectileHomingTime;

        turnSpeed =
            projectileTurnSpeed;

        lifeTime =
            projectileLifeTime;

        damage =
            projectileDamage;

        knockbackForce =
            knockback;

        // 생성 직후부터 발사 방향을 바라보게 함
        RotateToDirection();
    }

    private void Update()
    {
        homingTimer += Time.deltaTime;
        lifeTimer += Time.deltaTime;

        // 일정 시간 동안만 플레이어 추적
        if (target != null &&
            homingTimer <= homingTime)
        {
            Vector2 targetDirection =
                ((Vector2)target.position -
                 (Vector2)transform.position)
                .normalized;

            direction =
                Vector2.Lerp(
                    direction,
                    targetDirection,
                    turnSpeed * Time.deltaTime
                ).normalized;
        }

        // 현재 이동 방향을 바라보도록 회전
        RotateToDirection();

        // 이동
        transform.position +=
            (Vector3)(
                direction *
                speed *
                Time.deltaTime
            );

        // 수명 종료
        if (lifeTimer >= lifeTime)
        {
            Destroy(gameObject);
        }
    }

    private void RotateToDirection()
    {
        if (!rotateWithDirection)
            return;

        if (direction.sqrMagnitude < 0.001f)
            return;

        float angle =
            Mathf.Atan2(
                direction.y,
                direction.x
            ) * Mathf.Rad2Deg;

        transform.rotation =
            Quaternion.Euler(
                0f,
                0f,
                angle + spriteAngleOffset
            );
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

        Destroy(gameObject);
    }
}