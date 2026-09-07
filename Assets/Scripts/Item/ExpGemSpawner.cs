using UnityEngine;

public class ExpGemSpawner : MonoBehaviour
{
    [Header("Pool")]
    [SerializeField] private ObjectPool expGemPool;

    [Header("Spawn")]
    [SerializeField, Min(0f)]
    private float spreadRadius = 0.5f;

    [Header("Gem Distribution")]
    [SerializeField, Min(1)]
    private int maxGemCount = 10;

    public void Spawn(
        Vector3 position,
        int totalExp)
    {
        if (totalExp <= 0)
            return;

        if (expGemPool == null)
        {
            Debug.LogWarning(
                "ExpGemSpawner: ExpGem Pool이 연결되지 않았습니다."
            );

            return;
        }

        int gemValue =
            GetGemValue(totalExp);

        int count =
            totalExp / gemValue;

        int remain =
            totalExp % gemValue;

        count =
            Mathf.Min(
                count,
                maxGemCount
            );

        if (count >= maxGemCount)
        {
            gemValue =
                Mathf.Max(
                    1,
                    totalExp / maxGemCount
                );

            count =
                totalExp / gemValue;

            remain =
                totalExp % gemValue;
        }

        for (int i = 0; i < count; i++)
        {
            SpawnOneGem(
                position,
                gemValue
            );
        }

        if (remain > 0)
        {
            SpawnOneGem(
                position,
                remain
            );
        }
    }

    private int GetGemValue(int totalExp)
    {
        if (totalExp <= 20)
            return 2;

        if (totalExp <= 40)
            return 5;

        if (totalExp <= 100)
            return 10;

        return 50;
    }

    private void SpawnOneGem(
        Vector3 position,
        int expAmount)
    {
        GameObject gemObject =
            expGemPool.GetObject();

        if (gemObject == null)
            return;

        Vector2 offset =
            Random.insideUnitCircle *
            spreadRadius;

        gemObject.transform.position =
            position +
            (Vector3)offset;

        gemObject.transform.rotation =
            Quaternion.identity;

        ExpGem expGem =
            gemObject.GetComponent<ExpGem>();

        if (expGem == null)
        {
            Debug.LogWarning(
                "ExpGemSpawner: Pool 오브젝트에 ExpGem이 없습니다."
            );

            PoolObject poolObject =
                gemObject.GetComponent<PoolObject>();

            if (poolObject != null)
            {
                poolObject.ReturnToPool();
            }

            return;
        }

        expGem.Initialize(
            expAmount
        );
    }
}