using System.Collections;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy")]
    [SerializeField] private GameObject enemyPrefab;

    [Header("Spawn Area")]
    [SerializeField] private Transform leftSpawnPoint;
    [SerializeField] private Transform rightSpawnPoint;

    [Header("Spawn Settings")]
    [SerializeField, Min(0.1f)]
    private float spawnInterval = 2f;

    [SerializeField, Min(1)]
    private int maxAliveEnemies = 10;

    private int aliveEnemyCount;
    private bool isSpawning;

    public int AliveEnemyCount => aliveEnemyCount;

    public void StartSpawning()
    {
        if (isSpawning)
            return;

        if (!CanSpawn())
            return;

        isSpawning = true;

        StartCoroutine(
            SpawnRoutine()
        );
    }

    public void StopSpawning()
    {
        isSpawning = false;
    }

    private IEnumerator SpawnRoutine()
    {
        while (isSpawning)
        {
            if (aliveEnemyCount < maxAliveEnemies)
            {
                SpawnEnemy();
            }

            yield return new WaitForSeconds(
                spawnInterval
            );
        }
    }

    private void SpawnEnemy()
    {
        if (!CanSpawn())
            return;

        Vector3 spawnPosition =
            GetRandomSpawnPosition();

        GameObject enemy =
            Instantiate(
                enemyPrefab,
                spawnPosition,
                Quaternion.identity
            );

        EnemySpawnTracker tracker =
            enemy.GetComponent<EnemySpawnTracker>();

        if (tracker == null)
        {
            tracker =
                enemy.AddComponent<EnemySpawnTracker>();
        }

        tracker.Initialize(this);

        aliveEnemyCount++;
    }

    private Vector3 GetRandomSpawnPosition()
    {
        float x =
            Random.Range(
                leftSpawnPoint.position.x,
                rightSpawnPoint.position.x
            );

        return new Vector3(
            x,
            leftSpawnPoint.position.y,
            0f
        );
    }

    private bool CanSpawn()
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning(
                $"{name}: Enemy Prefab이 연결되지 않았습니다."
            );

            return false;
        }

        if (leftSpawnPoint == null ||
            rightSpawnPoint == null)
        {
            Debug.LogWarning(
                $"{name}: Spawn Point가 연결되지 않았습니다."
            );

            return false;
        }

        return true;
    }

    public void NotifyEnemyDead()
    {
        aliveEnemyCount =
            Mathf.Max(
                0,
                aliveEnemyCount - 1
            );
    }

}