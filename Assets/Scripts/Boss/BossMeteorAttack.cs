using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossMeteorAttack : BossAttackBase
{
    [Header("Target")]
    [SerializeField]
    private Transform target;

    [Header("Warning")]
    [SerializeField]
    private GameObject warningPrefab;

    [Header("Meteor Visual")]
    [SerializeField]
    private GameObject meteorPrefab;

    [SerializeField, Min(0f)]
    private float meteorStartHeight = 8f;

    [SerializeField, Min(0.01f)]
    private float meteorVisualScale = 1f;

    [Header("Arena X Range")]
    [SerializeField]
    private float minX = -8f;

    [SerializeField]
    private float maxX = 8f;

    [Header("Floor Y")]
    [SerializeField]
    private float floor1Y = -3f;

    [SerializeField]
    private float floor2Y = 1f;

    [Header("Attack Area")]
    [SerializeField]
    private float attackWidth = 1.5f;

    [SerializeField]
    private float attackHeight = 1.2f;

    [SerializeField]
    private float spacing = 2.5f;

    [Header("Phase 1")]
    [SerializeField, Min(1)]
    private int phase1AttackCount = 3;

    [SerializeField, Min(0f)]
    private float phase1WarningTime = 0.8f;

    [SerializeField, Min(0)]
    private int phase1Damage = 15;

    [Header("Phase 2")]
    [SerializeField, Min(1)]
    private int phase2AttackCount = 5;

    [SerializeField, Min(0f)]
    private float phase2WarningTime = 0.5f;

    [SerializeField, Min(0)]
    private int phase2Damage = 20;

    [Header("Timing")]
    [SerializeField, Min(0f)]
    private float recoveryTime = 0.6f;

    [Header("Knockback")]
    [SerializeField]
    private float knockbackForce = 6f;

    public override IEnumerator Execute(int phase)
    {
        FindTargetIfNeeded();

        if (target == null ||
            warningPrefab == null)
        {
            yield break;
        }

        int attackCount =
            phase >= 2
                ? phase2AttackCount
                : phase1AttackCount;

        float warningTime =
            phase >= 2
                ? phase2WarningTime
                : phase1WarningTime;

        int damage =
            phase >= 2
                ? phase2Damage
                : phase1Damage;

        // 공격 시작 순간 플레이어가
        // 어느 층에 있는지 확인
        float targetFloorY =
            GetClosestFloorY();

        List<Vector2> attackPositions =
            CreateAttackPositions(
                attackCount,
                targetFloorY
            );

        // 바닥 경고 표시 생성
        List<GameObject> warnings =
            CreateWarnings(
                attackPositions
            );

        // 화면 위쪽에 메테오 생성
        List<GameObject> meteors =
            CreateMeteors(
                attackPositions
            );

        if (meteors.Count > 0)
            SfxPlayer.Play(SfxId.BossMeteor);

        // 경고 시간 동안
        // 메테오가 위에서 아래로 낙하
        if (meteors.Count > 0)
        {
            yield return StartCoroutine(
                MoveMeteors(
                    meteors,
                    attackPositions,
                    warningTime
                )
            );
        }
        else
        {
            // Meteor Prefab이 없어도
            // 기존 공격은 작동
            yield return new WaitForSeconds(
                warningTime
            );
        }

        // 착지 순간 데미지 판정
        DamageAllAreas(
            attackPositions,
            damage
        );

        // 경고 제거
        DestroyObjects(
            warnings
        );

        // 착지한 메테오 제거
        DestroyObjects(
            meteors
        );

        // 공격 후 딜레이
        yield return new WaitForSeconds(
            recoveryTime
        );
    }

    private float GetClosestFloorY()
    {
        float distanceToFloor1 =
            Mathf.Abs(
                target.position.y -
                floor1Y
            );

        float distanceToFloor2 =
            Mathf.Abs(
                target.position.y -
                floor2Y
            );

        if (distanceToFloor1 <=
            distanceToFloor2)
        {
            return floor1Y;
        }

        return floor2Y;
    }

    private List<Vector2> CreateAttackPositions(
        int count,
        float floorY)
    {
        List<Vector2> positions =
            new List<Vector2>();

        float totalWidth =
            (count - 1) *
            spacing;

        float halfWidth =
            totalWidth *
            0.5f;

        float centerX =
            target.position.x;

        float minimumCenter =
            minX +
            halfWidth;

        float maximumCenter =
            maxX -
            halfWidth;

        if (minimumCenter <=
            maximumCenter)
        {
            centerX =
                Mathf.Clamp(
                    centerX,
                    minimumCenter,
                    maximumCenter
                );
        }
        else
        {
            centerX =
                (minX + maxX) *
                0.5f;
        }

        float startX =
            centerX -
            halfWidth;

        for (int i = 0;
             i < count;
             i++)
        {
            float x =
                startX +
                spacing *
                i;

            positions.Add(
                new Vector2(
                    x,
                    floorY
                )
            );
        }

        return positions;
    }

    private List<GameObject> CreateWarnings(
        List<Vector2> positions)
    {
        List<GameObject> warnings =
            new List<GameObject>();

        foreach (Vector2 position in positions)
        {
            Vector2 warningPosition =
                GetAttackCenter(
                    position
                );

            GameObject warning =
                Instantiate(
                    warningPrefab,
                    warningPosition,
                    Quaternion.identity
                );

            warning.transform.localScale =
                new Vector3(
                    attackWidth,
                    attackHeight,
                    1f
                );

            warnings.Add(
                warning
            );
        }

        return warnings;
    }

    private List<GameObject> CreateMeteors(
        List<Vector2> positions)
    {
        List<GameObject> meteors =
            new List<GameObject>();

        if (meteorPrefab == null)
            return meteors;

        foreach (Vector2 position in positions)
        {
            Vector2 landingPosition =
                GetMeteorLandingPosition(
                    position
                );

            Vector2 startPosition =
                landingPosition +
                Vector2.up *
                meteorStartHeight;

            GameObject meteor =
                Instantiate(
                    meteorPrefab,
                    startPosition,
                    Quaternion.identity
                );

            meteor.transform.localScale =
                Vector3.one *
                meteorVisualScale;

            meteors.Add(
                meteor
            );
        }

        return meteors;
    }

    private IEnumerator MoveMeteors(
        List<GameObject> meteors,
        List<Vector2> positions,
        float duration)
    {
        if (duration <= 0f)
        {
            for (int i = 0;
                 i < meteors.Count;
                 i++)
            {
                if (meteors[i] == null)
                    continue;

                meteors[i]
                    .transform
                    .position =
                    GetMeteorLandingPosition(
                        positions[i]
                    );
            }

            yield break;
        }

        List<Vector3> startPositions =
            new List<Vector3>();

        for (int i = 0;
             i < meteors.Count;
             i++)
        {
            if (meteors[i] != null)
            {
                startPositions.Add(
                    meteors[i]
                        .transform
                        .position
                );
            }
            else
            {
                startPositions.Add(
                    Vector3.zero
                );
            }
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    duration
                );

            // 처음에는 조금 느리고
            // 바닥에 가까워질수록 빠르게 낙하
            float easedT =
                t * t;

            for (int i = 0;
                 i < meteors.Count;
                 i++)
            {
                GameObject meteor =
                    meteors[i];

                if (meteor == null)
                    continue;

                Vector3 endPosition =
                    GetMeteorLandingPosition(
                        positions[i]
                    );

                meteor.transform.position =
                    Vector3.Lerp(
                        startPositions[i],
                        endPosition,
                        easedT
                    );
            }

            yield return null;
        }

        // 마지막 위치 정확하게 고정
        for (int i = 0;
             i < meteors.Count;
             i++)
        {
            if (meteors[i] == null)
                continue;

            meteors[i]
                .transform
                .position =
                GetMeteorLandingPosition(
                    positions[i]
                );
        }
    }

    private void DamageAllAreas(
        List<Vector2> positions,
        int damage)
    {
        HashSet<PlayerHealth> damagedPlayers =
            new HashSet<PlayerHealth>();

        foreach (Vector2 position in positions)
        {
            Vector2 attackCenter =
                GetAttackCenter(
                    position
                );

            Collider2D[] hits =
                Physics2D.OverlapBoxAll(
                    attackCenter,
                    new Vector2(
                        attackWidth,
                        attackHeight
                    ),
                    0f
                );

            foreach (Collider2D hit in hits)
            {
                PlayerHealth playerHealth =
                    hit.GetComponent<PlayerHealth>();

                if (playerHealth == null)
                {
                    playerHealth =
                        hit.GetComponentInParent
                            <PlayerHealth>();
                }

                if (playerHealth == null)
                    continue;

                if (damagedPlayers.Contains(
                    playerHealth))
                {
                    continue;
                }

                damagedPlayers.Add(
                    playerHealth
                );

                Vector2 direction =
                    (Vector2)
                    playerHealth.transform.position -
                    attackCenter;

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

                playerHealth.TakeDamage(
                    damage,
                    direction,
                    knockbackForce
                );
            }
        }
    }

    private Vector2 GetAttackCenter(
        Vector2 floorPosition)
    {
        return new Vector2(
            floorPosition.x,
            floorPosition.y +
            attackHeight *
            0.5f
        );
    }

    private Vector2 GetMeteorLandingPosition(
        Vector2 floorPosition)
    {
        return new Vector2(
            floorPosition.x,
            floorPosition.y
        );
    }

    private void DestroyObjects(
        List<GameObject> objects)
    {
        foreach (GameObject obj in objects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
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
}