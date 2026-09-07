using System.Collections;
using UnityEngine;

public class BossProjectileAttack : BossAttackBase
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Fire Point")]
    [SerializeField] private Transform firePoint;

    [Header("Projectile")]
    [SerializeField] private GameObject projectilePrefab;

    [SerializeField, Min(0.1f)]
    private float projectileSpeed = 7f;

    [Header("Phase 1")]
    [SerializeField, Min(1)]
    private int phase1ProjectileCount = 3;

    [SerializeField, Min(0f)]
    private float phase1SpreadAngle = 30f;

    [SerializeField, Min(0)]
    private int phase1Damage = 12;

    [Header("Phase 2")]
    [SerializeField, Min(1)]
    private int phase2ProjectileCount = 5;

    [SerializeField, Min(0f)]
    private float phase2SpreadAngle = 45f;

    [SerializeField, Min(0)]
    private int phase2Damage = 15;

    [Header("Burst")]
    [SerializeField, Min(1)]
    private int burstCount = 3;

    [SerializeField, Min(0f)]
    private float burstInterval = 0.25f;

    [Header("Timing")]
    [SerializeField, Min(0f)]
    private float prepareTime = 0.5f;

    [SerializeField, Min(0f)]
    private float recoveryTime = 0.3f;

    public override IEnumerator Execute(int phase)
    {
        FindTargetIfNeeded();

        if (target == null ||
            firePoint == null ||
            projectilePrefab == null)
        {
            yield break;
        }

        // 공격 전 준비 시간
        yield return new WaitForSeconds(
            prepareTime
        );

        // 부채꼴 탄막 연속 발사
        for (int i = 0;
             i < burstCount;
             i++)
        {
            FireProjectiles(phase);

            // 마지막 발사 뒤에는 기다리지 않음
            if (i < burstCount - 1)
            {
                yield return new WaitForSeconds(
                    burstInterval
                );
            }
        }

        // 공격 후 딜레이
        yield return new WaitForSeconds(
            recoveryTime
        );
    }

    private void FireProjectiles(int phase)
    {
        Vector2 baseDirection =
            ((Vector2)target.position -
             (Vector2)firePoint.position)
            .normalized;

        int projectileCount =
            phase >= 2
                ? phase2ProjectileCount
                : phase1ProjectileCount;

        float spreadAngle =
            phase >= 2
                ? phase2SpreadAngle
                : phase1SpreadAngle;

        int damage =
            phase >= 2
                ? phase2Damage
                : phase1Damage;

        if (projectileCount <= 1)
        {
            SpawnProjectile(
                baseDirection,
                damage
            );

            return;
        }

        float startAngle =
            -spreadAngle / 2f;

        float angleStep =
            spreadAngle /
            (projectileCount - 1);

        for (int i = 0;
             i < projectileCount;
             i++)
        {
            float angle =
                startAngle +
                angleStep * i;

            Vector2 direction =
                RotateDirection(
                    baseDirection,
                    angle
                );

            SpawnProjectile(
                direction,
                damage
            );
        }
    }

    private void SpawnProjectile(
        Vector2 direction,
        int damage)
    {
        // 투사체가 날아가는 방향을 바라보도록 회전
        float angle =
            Mathf.Atan2(
                direction.y,
                direction.x
            ) * Mathf.Rad2Deg;

        GameObject projectileObject =
            Instantiate(
                projectilePrefab,
                firePoint.position,
                Quaternion.Euler(
                    0f,
                    0f,
                    angle
                )
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
            damage
        );
    }

    private Vector2 RotateDirection(
        Vector2 direction,
        float angle)
    {
        float rad =
            angle * Mathf.Deg2Rad;

        float cos =
            Mathf.Cos(rad);

        float sin =
            Mathf.Sin(rad);

        return new Vector2(
            direction.x * cos -
            direction.y * sin,

            direction.x * sin +
            direction.y * cos
        ).normalized;
    }

    private void FindTargetIfNeeded()
    {
        if (target != null)
            return;

        PlayerController player =
            FindFirstObjectByType
                <PlayerController>();

        if (player != null)
        {
            target =
                player.transform;
        }
    }
}