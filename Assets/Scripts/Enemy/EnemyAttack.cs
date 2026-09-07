using UnityEngine;

public class EnemyAttack : MonoBehaviour
{
    [SerializeField] private int damage = 10;

    [SerializeField, Min(0f)]
    private float attackCooldown = 1f;

    [SerializeField, Min(0f)]
    private float knockbackForce = 5f;

    private float lastAttackTime = -Mathf.Infinity;

    private Animator animator;

    private void Awake()
    {
        animator = GetComponentInParent<Animator>();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        PlayerHealth playerHealth =
            other.GetComponentInParent<PlayerHealth>();

        if (playerHealth == null)
            return;

        if (Time.time <
            lastAttackTime + attackCooldown)
        {
            return;
        }

        // 공격 애니메이션
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        Vector2 direction =
            other.transform.position -
            transform.position;

        direction = direction.normalized;

        playerHealth.TakeDamage(
            damage,
            direction,
            knockbackForce
        );

        lastAttackTime = Time.time;
    }

    public void SetDamage(int value)
    {
        damage = Mathf.Max(0, value);
    }

    public void SetAttackCooldown(float value)
    {
        attackCooldown = Mathf.Max(0f, value);
    }

    public void SetKnockbackForce(float value)
    {
        knockbackForce = Mathf.Max(0f, value);
    }
}