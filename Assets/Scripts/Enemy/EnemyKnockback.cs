using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyKnockback : MonoBehaviour
{
    [Header("Knockback")]
    [SerializeField, Min(0f)]
    private float defaultForce = 4f;

    [SerializeField, Min(0f)]
    private float stunTime = 0.1f;

    private Rigidbody2D rb;
    private EnemyStun enemyStun;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        enemyStun = GetComponent<EnemyStun>();
    }

    public void Knockback(Vector2 direction)
    {
        Knockback(direction, defaultForce);
    }

    public void Knockback(
        Vector2 direction,
        float force)
    {
        if (rb == null)
            return;

        if (direction.sqrMagnitude <= 0f)
            return;

        force = Mathf.Max(0f, force);

        if (force <= 0f)
            return;

        if (enemyStun != null &&
            stunTime > 0f)
        {
            enemyStun.Stun(stunTime);
        }

        rb.linearVelocity = Vector2.zero;

        rb.AddForce(
            direction.normalized * force,
            ForceMode2D.Impulse
        );
    }
}