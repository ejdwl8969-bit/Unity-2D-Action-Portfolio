using System.Collections;
using UnityEngine;

public class BossShockwaveAttack : BossAttackBase
{
    [Header("Origin")]
    [SerializeField]
    private Transform shockwaveOrigin;

    [Header("Warning")]
    [SerializeField]
    private GameObject warningPrefab;

    [Header("Warning Position")]
    [SerializeField]
    private float warningYOffset = 0.5f;

    [Header("Prefab")]
    [SerializeField]
    private GameObject shockwavePrefab;

    [Header("Phase 1")]
    [SerializeField]
    private float phase1Speed = 8f;

    [SerializeField]
    private int phase1Damage = 15;

    [Header("Phase 2")]
    [SerializeField]
    private float phase2Speed = 10f;

    [SerializeField]
    private int phase2Damage = 20;

    [SerializeField]
    private int phase2WaveCount = 2;

    [SerializeField]
    private float phase2WaveInterval = 0.5f;

    [Header("Common")]
    [SerializeField]
    private float maxDistance = 12f;

    [SerializeField]
    private float knockbackForce = 7f;

    [Header("Timing")]
    [SerializeField]
    private float prepareTime = 0.6f;

    [SerializeField]
    private float recoveryTime = 0.5f;

    public override IEnumerator Execute(int phase)
    {
        if (shockwaveOrigin == null ||
            shockwavePrefab == null)
        {
            yield break;
        }

        GameObject warning =
            CreateWarning();

        yield return new WaitForSeconds(
            prepareTime
        );

        if (warning != null)
        {
            Destroy(warning);
        }

        if (phase >= 2)
        {
            for (int i = 0;
                 i < phase2WaveCount;
                 i++)
            {
                SpawnWavePair(
                    phase2Speed,
                    phase2Damage
                );

                if (i < phase2WaveCount - 1)
                {
                    yield return new WaitForSeconds(
                        phase2WaveInterval
                    );
                }
            }
        }
        else
        {
            SpawnWavePair(
                phase1Speed,
                phase1Damage
            );
        }

        yield return new WaitForSeconds(
            recoveryTime
        );
    }

    private GameObject CreateWarning()
    {
        if (warningPrefab == null)
            return null;

        Vector3 warningPosition =
            shockwaveOrigin.position;

        warningPosition.y +=
            warningYOffset;

        GameObject warning =
            Instantiate(
                warningPrefab,
                warningPosition,
                Quaternion.identity
            );

        return warning;
    }

    private void SpawnWavePair(
        float speed,
        int damage)
    {
        SpawnShockwave(
            Vector2.left,
            speed,
            damage
        );

        SpawnShockwave(
            Vector2.right,
            speed,
            damage
        );
    }

    private void SpawnShockwave(
        Vector2 direction,
        float speed,
        int damage)
    {
        GameObject waveObject =
            Instantiate(
                shockwavePrefab,
                shockwaveOrigin.position,
                Quaternion.identity
            );

        BossShockwave wave =
            waveObject.GetComponent<BossShockwave>();

        if (wave == null)
        {
            Destroy(waveObject);
            return;
        }

        wave.Initialize(
            direction,
            speed,
            maxDistance,
            damage,
            knockbackForce
        );
    }
}