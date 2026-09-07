using System.Collections;
using UnityEngine;

public class BossSlamAttack : BossAttackBase
{
    [Header("Target")]
    [SerializeField]
    private Transform target;

    [Header("Attack Position")]
    [SerializeField]
    private Transform attackCenterReference;

    [Header("Warning")]
    [SerializeField]
    private GameObject warningPrefab;

    [Header("Impact")]
    [SerializeField]
    private GameObject impactPrefab;

    [SerializeField, Min(0.01f)]
    private float impactVisualScale = 1f;

    [Header("Slam Visual")]
    [SerializeField, Min(0f)]
    private float slamStartHeight = 5f;

    [SerializeField, Min(0.01f)]
    private float slamFallDuration = 0.15f;

    [SerializeField, Min(0f)]
    private float impactHoldDuration = 0.12f;

    [Header("Camera Shake")]
    [SerializeField, Min(0f)]
    private float shakeDuration = 0.12f;

    [SerializeField, Min(0f)]
    private float shakeStrength = 0.08f;

    [Header("Warning Time")]
    [SerializeField, Min(0f)]
    private float phase1WarningTime = 0.9f;

    [SerializeField, Min(0f)]
    private float phase2WarningTime = 0.6f;

    [Header("Attack")]
    [SerializeField]
    private Vector2 attackSize =
        new Vector2(3f, 2f);

    [SerializeField]
    private LayerMask playerLayer;

    [SerializeField, Min(0)]
    private int phase1Damage = 20;

    [SerializeField, Min(0)]
    private int phase2Damage = 25;

    [SerializeField, Min(0f)]
    private float knockbackForce = 8f;

    [SerializeField, Min(0f)]
    private float recoveryTime = 0.25f;

    public override IEnumerator Execute(
        int phase)
    {
        FindTargetIfNeeded();

        if (target == null)
            yield break;

        Vector2 attackPosition =
            GetAttackPosition();

        GameObject warning =
            CreateWarning(
                attackPosition
            );

        float warningTime =
            phase >= 2
                ? phase2WarningTime
                : phase1WarningTime;

        // 경고 표시
        yield return new WaitForSeconds(
            warningTime
        );

        // 경고 제거
        if (warning != null)
        {
            Destroy(warning);
        }

        // 주먹 생성
        GameObject impact =
            CreateImpact(
                attackPosition
            );

        // 위에서 아래로 Slam
        if (impact != null)
        {
            yield return StartCoroutine(
                SlamImpact(
                    impact,
                    attackPosition
                )
            );
        }

        // 바닥에 닿는 순간 카메라 흔들림
        CameraShake.Instance?.Shake(
            shakeDuration,
            shakeStrength
        );

        SfxPlayer.Play(SfxId.BossSmash);

        // 바닥에 닿는 순간 데미지
        DealDamage(
            attackPosition,
            phase
        );

        // 주먹을 바닥에 잠깐 유지
        if (impact != null)
        {
            yield return new WaitForSeconds(
                impactHoldDuration
            );

            Destroy(impact);
        }

        // 공격 후 딜레이
        yield return new WaitForSeconds(
            recoveryTime
        );
    }

    private Vector2 GetAttackPosition()
    {
        float y =
            attackCenterReference != null
                ? attackCenterReference.position.y
                : target.position.y;

        return new Vector2(
            target.position.x,
            y
        );
    }

    private GameObject CreateWarning(
        Vector2 position)
    {
        if (warningPrefab == null)
            return null;

        GameObject warning =
            Instantiate(
                warningPrefab,
                position,
                Quaternion.identity
            );

        // 실제 공격 범위를 보여주기 위해
        // Warning은 attackSize 크기로 표시
        warning.transform.localScale =
            new Vector3(
                attackSize.x,
                attackSize.y,
                1f
            );

        return warning;
    }

    private GameObject CreateImpact(
        Vector2 attackPosition)
    {
        if (impactPrefab == null)
            return null;

        // 실제 공격 위치보다 위쪽에서 주먹 생성
        Vector2 startPosition =
            attackPosition +
            Vector2.up *
            slamStartHeight;

        GameObject impact =
            Instantiate(
                impactPrefab,
                startPosition,
                Quaternion.identity
            );

        // 주먹 Sprite 원본 비율 유지
        impact.transform.localScale =
            Vector3.one *
            impactVisualScale;

        return impact;
    }

    private IEnumerator SlamImpact(
        GameObject impact,
        Vector2 attackPosition)
    {
        if (impact == null)
            yield break;

        Vector3 startPosition =
            impact.transform.position;

        Vector3 endPosition =
            attackPosition;

        float elapsed = 0f;

        while (elapsed <
               slamFallDuration)
        {
            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    slamFallDuration
                );

            // 처음에는 조금 느리고
            // 아래로 갈수록 빠르게 낙하
            float easedT =
                t * t;

            impact.transform.position =
                Vector3.Lerp(
                    startPosition,
                    endPosition,
                    easedT
                );

            yield return null;
        }

        // 마지막 위치 정확하게 고정
        impact.transform.position =
            endPosition;
    }

    private void DealDamage(
        Vector2 attackPosition,
        int phase)
    {
        Collider2D[] hits =
            Physics2D.OverlapBoxAll(
                attackPosition,
                attackSize,
                0f,
                playerLayer
            );

        foreach (
            Collider2D hit in hits)
        {
            PlayerHealth playerHealth =
                hit.GetComponentInParent
                    <PlayerHealth>();

            if (playerHealth == null)
                continue;

            Vector2 direction =
                (Vector2)
                playerHealth.transform.position
                - attackPosition;

            if (direction.sqrMagnitude <
                0.01f)
            {
                direction =
                    Vector2.up;
            }
            else
            {
                direction.Normalize();
            }

            int damage =
                phase >= 2
                    ? phase2Damage
                    : phase1Damage;

            playerHealth.TakeDamage(
                damage,
                direction,
                knockbackForce
            );

            // 플레이어는 한 명이므로
            // 한 번 맞았으면 종료
            break;
        }
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

    private void OnDrawGizmosSelected()
    {
        if (attackCenterReference == null)
            return;

        Gizmos.DrawWireCube(
            attackCenterReference.position,
            attackSize
        );
    }
}