using UnityEngine;

public class AttackBox : MonoBehaviour
{
    private DamageData damageData;
    private int comboCount;

    public void Initialize(DamageData damageData, int comboCount = 1)
    {
        this.damageData = damageData;
        this.comboCount = comboCount;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        EnemyHealth enemyHealth =
            other.GetComponentInParent<EnemyHealth>();

        if (enemyHealth == null)
            return;

        enemyHealth.TakeDamage(damageData);

        EnemyKnockback knockback =
            enemyHealth.GetComponent<EnemyKnockback>();

        if (knockback != null)
        {
            Vector2 direction =
                (enemyHealth.transform.position - transform.position).normalized;

            float force = comboCount == 3 ? 6f : 4f;
            knockback.Knockback(direction, force);
        }

        if (comboCount == 3 && CameraShake.Instance != null)
        {
            CameraShake.Instance.Shake(duration: 0.08f, strength: 0.08f);
        }
    }
}