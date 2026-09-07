using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class EnemyFallKillZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerHealth playerHealth =
            other.GetComponentInParent<PlayerHealth>();

        if (playerHealth != null)
        {
            if (!playerHealth.IsDead)
            {
                playerHealth.KillByFall();
            }

            return;
        }

        EnemyHealth enemyHealth =
            other.GetComponentInParent<EnemyHealth>();

        if (enemyHealth == null || enemyHealth.IsDead)
            return;

        if (enemyHealth.GetComponentInParent<BossController>() != null ||
            enemyHealth.GetComponentInParent<BossChapterProgress>() != null)
        {
            return;
        }

        enemyHealth.KillByFall();
    }
}
