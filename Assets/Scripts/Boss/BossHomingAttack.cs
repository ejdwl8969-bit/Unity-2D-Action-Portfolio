using System.Collections;
using UnityEngine;

public class BossHomingAttack : BossAttackBase
{
    [Header("Target")]
    [SerializeField]
    private Transform target;

    [Header("Fire Point")]
    [SerializeField]
    private Transform firePoint;

    [Header("Prefab")]
    [SerializeField]
    private GameObject projectilePrefab;

    [Header("Phase 1")]
    [SerializeField, Min(1)]
    private int phase1ProjectileCount = 2;

    [SerializeField]
    private float phase1Speed = 5f;

    [SerializeField]
    private int phase1Damage = 12;

    [Header("Phase 2")]
    [SerializeField, Min(1)]
    private int phase2ProjectileCount = 4;

    [SerializeField]
    private float phase2Speed = 6f;

    [SerializeField]
    private int phase2Damage = 15;

    [Header("Homing")]
    [SerializeField]
    private float homingTime = 1.2f;

    [SerializeField]
    private float turnSpeed = 3f;

    [SerializeField]
    private float lifeTime = 5f;

    [Header("Timing")]
    [SerializeField]
    private float prepareTime = 0.5f;

    [SerializeField]
    private float projectileInterval = 0.2f;

    [SerializeField]
    private float recoveryTime = 0.5f;

    [Header("Knockback")]
    [SerializeField]
    private float knockbackForce = 5f;

    public override IEnumerator Execute(int phase)
    {
        FindTargetIfNeeded();

        if (target == null ||
            firePoint == null ||
            projectilePrefab == null)
        {
            yield break;
        }

        // 공격 준비
        yield return new WaitForSeconds(
            prepareTime
        );

        int projectileCount =
            phase >= 2
                ? phase2ProjectileCount
                : phase1ProjectileCount;

        float speed =
            phase >= 2
                ? phase2Speed
                : phase1Speed;

        int damage =
            phase >= 2
                ? phase2Damage
                : phase1Damage;

        // 한 발씩 연속 발사
        for (int i = 0;
             i < projectileCount;
             i++)
        {
            SpawnProjectile(
                speed,
                damage,
                i,
                projectileCount
            );

            if (i < projectileCount - 1)
            {
                yield return new WaitForSeconds(
                    projectileInterval
                );
            }
        }

        yield return new WaitForSeconds(
            recoveryTime
        );
    }

    private void SpawnProjectile(
        float speed,
        int damage,
        int index,
        int totalCount)
    {
        Vector2 baseDirection =
            ((Vector2)target.position -
             (Vector2)firePoint.position)
            .normalized;

        // 처음부터 완전히 겹쳐서 나가지 않도록
        // 발사 방향을 조금씩 다르게 한다.
        float offsetAngle =
            (index -
             (totalCount - 1) / 2f)
            * 12f;

        Vector2 startDirection =
            RotateDirection(
                baseDirection,
                offsetAngle
            );

        GameObject projectileObject =
            Instantiate(
                projectilePrefab,
                firePoint.position,
                Quaternion.identity
            );

        BossHomingProjectile projectile =
            projectileObject
                .GetComponent
                    <BossHomingProjectile>();

        if (projectile == null)
        {
            Destroy(projectileObject);
            return;
        }

        projectile.Initialize(
            target,
            startDirection,
            speed,
            homingTime,
            turnSpeed,
            lifeTime,
            damage,
            knockbackForce
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