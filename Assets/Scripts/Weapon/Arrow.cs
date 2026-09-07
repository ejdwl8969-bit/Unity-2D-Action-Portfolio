using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PoolObject))]
public class Arrow : MonoBehaviour
{
    [Header("Default Stat")]
    [SerializeField, Min(0.1f)]
    private float speed = 12f;

    [SerializeField, Min(0.1f)]
    private float maxDistance = 8f;

    [Header("Knockback")]
    [SerializeField, Min(0f)]
    private float knockbackForce = 4f;

    private DamageData damageData;
    private Vector2 direction;
    private Vector3 startPosition;

    private PoolObject poolObject;
    private SpriteRenderer spriteRenderer;

    private int remainingPierce;

    private bool isInitialized;
    private bool isReturning;

    private readonly HashSet<EnemyHealth>
        hitEnemies = new();

    private void Awake()
    {
        poolObject =
            GetComponent<PoolObject>();

        // Arrow 오브젝트 또는 자식에서 SpriteRenderer 검색
        spriteRenderer =
            GetComponentInChildren<SpriteRenderer>(true);
    }

    public void Initialize(
        DamageData damageData,
        Vector2 direction,
        int pierceCount,
        float arrowSpeed,
        float arrowDistance)
    {
        this.damageData =
            damageData;

        this.direction =
            direction.normalized;

        speed =
            Mathf.Max(
                0.1f,
                arrowSpeed
            );

        maxDistance =
            Mathf.Max(
                0.1f,
                arrowDistance
            );

        remainingPierce =
            Mathf.Max(
                0,
                pierceCount
            );

        startPosition =
            transform.position;

        hitEnemies.Clear();

        isReturning = false;
        isInitialized = true;

        // ============================
        // 화살 이미지 좌우 방향 설정
        // ============================

        if (spriteRenderer != null)
        {
            // 원본 Arrow Sprite가 오른쪽을 향한다고 가정
            // 오른쪽 발사 = 그대로
            // 왼쪽 발사 = 좌우 반전
            spriteRenderer.flipX =
                this.direction.x < 0f;
        }
    }

    private void Update()
    {
        if (!isInitialized ||
            isReturning)
        {
            return;
        }

        Move();

        CheckMaxDistance();
    }

    private void Move()
    {
        transform.position +=
            (Vector3)(
                direction *
                speed *
                Time.deltaTime
            );
    }

    private void CheckMaxDistance()
    {
        float distance =
            Vector3.Distance(
                startPosition,
                transform.position
            );

        if (distance >= maxDistance)
        {
            Return();
        }
    }

    private void OnTriggerEnter2D(
        Collider2D other)
    {
        if (!isInitialized ||
            isReturning)
        {
            return;
        }

        EnemyHealth enemyHealth =
            other.GetComponentInParent<EnemyHealth>();

        if (enemyHealth == null)
            return;

        if (hitEnemies.Contains(
                enemyHealth))
        {
            return;
        }

        hitEnemies.Add(
            enemyHealth
        );

        HitEnemy(
            enemyHealth
        );
    }

    private void HitEnemy(
        EnemyHealth enemyHealth)
    {
        enemyHealth.TakeDamage(
            damageData
        );

        ApplyKnockback(
            enemyHealth
        );

        if (remainingPierce > 0)
        {
            remainingPierce--;
            return;
        }

        Return();
    }

    private void ApplyKnockback(
        EnemyHealth enemyHealth)
    {
        if (knockbackForce <= 0f)
            return;

        EnemyKnockback knockback =
            enemyHealth.GetComponent<EnemyKnockback>();

        if (knockback == null)
            return;

        knockback.Knockback(
            direction,
            knockbackForce
        );
    }

    private void Return()
    {
        if (isReturning)
            return;

        isReturning = true;
        isInitialized = false;

        hitEnemies.Clear();

        if (poolObject != null)
        {
            poolObject.ReturnToPool();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDisable()
    {
        isInitialized = false;
        isReturning = false;

        direction =
            Vector2.zero;

        remainingPierce = 0;

        hitEnemies.Clear();

        // Pool 재사용 시 기본 상태로 초기화
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = false;
        }
    }
}