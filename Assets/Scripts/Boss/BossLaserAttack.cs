using System.Collections;
using UnityEngine;

public class BossLaserAttack : BossAttackBase
{
    [Header("Laser Positions")]
    [SerializeField] private Transform laserOrigin1F;
    [SerializeField] private Transform laserOrigin2F;

    [Header("Warning")]
    [SerializeField] private GameObject warningObject;

    [SerializeField, Min(0f)]
    private float phase1WarningTime = 0.8f;

    [SerializeField, Min(0f)]
    private float phase2WarningTime = 0.5f;

    [Header("Laser")]
    [SerializeField] private GameObject laserObject;

    [SerializeField, Min(0f)]
    private float phase1LaserDuration = 0.4f;

    [SerializeField, Min(0f)]
    private float phase2LaserDuration = 0.6f;

    [SerializeField, Min(0)]
    private int phase1Damage = 20;

    [SerializeField, Min(0)]
    private int phase2Damage = 25;

    [SerializeField, Min(0f)]
    private float knockbackForce = 10f;

    [SerializeField] private LayerMask playerLayer;

    [Header("Attack Area")]
    [SerializeField]
    private Vector2 laserSize =
        new Vector2(12f, 1.5f);

    [SerializeField]
    private Vector2 laserOffset =
        new Vector2(-6f, 0f);

    [Header("Recovery")]
    [SerializeField, Min(0f)]
    private float recoveryTime = 0.4f;

    private Transform currentLaserOrigin;

    private void Awake()
    {
        ShowWarning(false);
        ShowLaser(false);
    }

    public override IEnumerator Execute(int phase)
    {
        currentLaserOrigin =
            SelectLaserOrigin();

        if (currentLaserOrigin == null)
            yield break;

        // 선택한 층으로 전조와 레이저 이동
        SetLaserObjectPosition();

        float warningTime =
            phase >= 2
                ? phase2WarningTime
                : phase1WarningTime;

        float laserDuration =
            phase >= 2
                ? phase2LaserDuration
                : phase1LaserDuration;

        int damage =
            phase >= 2
                ? phase2Damage
                : phase1Damage;

        // 전조
        ShowWarning(true);

        yield return new WaitForSeconds(
            warningTime
        );

        ShowWarning(false);

        // 실제 레이저
        ShowLaser(true);

        float elapsedTime = 0f;
        bool hasHitPlayer = false;

        while (elapsedTime < laserDuration)
        {
            if (!hasHitPlayer)
            {
                hasHitPlayer =
                    TryDamagePlayer(damage);
            }

            elapsedTime +=
                Time.deltaTime;

            yield return null;
        }

        ShowLaser(false);

        yield return new WaitForSeconds(
            recoveryTime
        );
    }

    private Transform SelectLaserOrigin()
    {
        if (laserOrigin1F == null)
            return laserOrigin2F;

        if (laserOrigin2F == null)
            return laserOrigin1F;

        return Random.value < 0.5f
            ? laserOrigin1F
            : laserOrigin2F;
    }

    private void SetLaserObjectPosition()
    {
        if (currentLaserOrigin == null)
            return;

        if (warningObject != null)
        {
            warningObject.transform.position =
                currentLaserOrigin.position;
        }

        if (laserObject != null)
        {
            laserObject.transform.position =
                currentLaserOrigin.position;
        }
    }

    private bool TryDamagePlayer(int damage)
    {
        if (currentLaserOrigin == null)
            return false;

        Vector2 center =
            (Vector2)currentLaserOrigin.position +
            laserOffset;

        Collider2D hit =
            Physics2D.OverlapBox(
                center,
                laserSize,
                0f,
                playerLayer
            );

        if (hit == null)
            return false;

        PlayerHealth playerHealth =
            hit.GetComponentInParent<PlayerHealth>();

        if (playerHealth == null)
            return false;

        Vector2 direction =
            ((Vector2)playerHealth.transform.position -
             (Vector2)currentLaserOrigin.position)
            .normalized;

        playerHealth.TakeDamage(
            damage,
            direction,
            knockbackForce
        );

        return true;
    }

    private void ShowWarning(bool show)
    {
        if (warningObject != null)
        {
            warningObject.SetActive(show);
        }
    }

    private void ShowLaser(bool show)
    {
        if (laserObject != null)
        {
            laserObject.SetActive(show);
        }
    }

    private void OnDrawGizmosSelected()
    {
        DrawLaserArea(laserOrigin1F);
        DrawLaserArea(laserOrigin2F);
    }

    private void DrawLaserArea(
        Transform origin)
    {
        if (origin == null)
            return;

        Vector2 center =
            (Vector2)origin.position +
            laserOffset;

        Gizmos.DrawWireCube(
            center,
            laserSize
        );
    }
}