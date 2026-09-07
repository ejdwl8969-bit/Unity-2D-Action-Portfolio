using UnityEngine;

public class Bow : Weapon
{
    [Header("References")]
    [SerializeField] private ObjectPool arrowPool;
    [SerializeField] private Transform firePoint;

    [Header("Arrow Stat")]
    [SerializeField, Min(0.1f)]
    private float arrowSpeed = 12f;

    [SerializeField, Min(0.1f)]
    private float arrowDistance = 8f;

    [SerializeField, Min(0)]
    private int additionalPierce = 0;

    protected override void Attack()
    {
        if (arrowPool == null ||
            firePoint == null)
        {
            Debug.LogWarning(
                $"{name}: Bow의 ArrowPool 또는 FirePoint가 연결되지 않았습니다."
            );

            return;
        }

        float directionX =
            GetFacingDirection();

        GameObject arrowObject =
            arrowPool.GetObject();

        if (arrowObject == null)
            return;

        SetArrowTransform(
            arrowObject,
            directionX
        );

        Arrow arrow =
            arrowObject.GetComponent<Arrow>();

        if (arrow == null)
        {
            Debug.LogWarning(
                $"{arrowObject.name}: Arrow 컴포넌트가 없습니다."
            );

            PoolObject poolObject =
                arrowObject.GetComponent<PoolObject>();

            if (poolObject != null)
            {
                poolObject.ReturnToPool();
            }
            else
            {
                Destroy(arrowObject);
            }

            return;
        }

        DamageData damageData =
            CreateDamageData();

        arrow.Initialize(
            damageData,
            new Vector2(directionX, 0f),
            GetPierceCount(),
            arrowSpeed,
            arrowDistance
        );
        SfxPlayer.Play(SfxId.BowAttack);

        transform.root.GetComponentInChildren<PlayerAnimationController>()?
            .PlayBowAttack(this, GetAttackCooldown());
    }

    private float GetFacingDirection()
    {
        PlayerController playerController =
            transform.root.GetComponent<PlayerController>();

        if (playerController != null &&
            !playerController.IsFacingRight)
        {
            return -1f;
        }

        return 1f;
    }

    private void SetArrowTransform(
        GameObject arrowObject,
        float directionX)
    {
        Vector3 localSpawnPosition =
            firePoint.localPosition;

        localSpawnPosition.x =
            Mathf.Abs(localSpawnPosition.x) *
            directionX;

        Vector3 worldSpawnPosition =
            transform.root.TransformPoint(
                localSpawnPosition
            );

        arrowObject.transform.position =
            worldSpawnPosition;

        arrowObject.transform.rotation =
            Quaternion.identity;
    }

    private int GetPierceCount()
    {
        int pierceCount =
            additionalPierce;

        if (IsWindActive)
        {
            pierceCount +=
                WindLevel;
        }

        return Mathf.Max(
            0,
            pierceCount
        );
    }

    public void AddArrowSpeed(float value)
    {
        arrowSpeed += value;

        arrowSpeed =
            Mathf.Max(
                0.1f,
                arrowSpeed
            );
    }

    public void AddArrowDistance(float value)
    {
        arrowDistance += value;

        arrowDistance =
            Mathf.Max(
                0.1f,
                arrowDistance
            );
    }

    public void AddArrowPierce(int value)
    {
        additionalPierce += value;

        additionalPierce =
            Mathf.Max(
                0,
                additionalPierce
            );
    }
}