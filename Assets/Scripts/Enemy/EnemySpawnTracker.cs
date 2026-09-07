using UnityEngine;

public class EnemySpawnTracker : MonoBehaviour
{
    private EnemySpawner ownerSpawner;
    private bool notified;

    public void Initialize(
        EnemySpawner spawner)
    {
        ownerSpawner = spawner;
        notified = false;
    }

    private void OnDestroy()
    {
        NotifySpawner();
    }

    private void NotifySpawner()
    {
        if (notified)
            return;

        notified = true;

        if (ownerSpawner != null)
        {
            ownerSpawner.NotifyEnemyDead();
        }
    }
}