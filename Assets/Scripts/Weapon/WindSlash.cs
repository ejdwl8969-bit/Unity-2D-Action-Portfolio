using System.Collections.Generic;
using UnityEngine;

public class WindSlash : MonoBehaviour
{
    [SerializeField] private float speed = 10f;
    [SerializeField] private float knockbackForce = 5f;

    private float maxDistance;
    private DamageData damageData;
    private Vector2 direction;
    private Vector3 startPosition;
    private Vector3 originalScale;
    private SpriteRenderer spriteRenderer;

    private readonly HashSet<EnemyHealth> hitEnemies = new();

    private void Awake()
    {
        originalScale = transform.localScale;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Initialize(
     DamageData damageData,
     Vector2 direction,
     float distance)
    {
        this.damageData = damageData;
        this.direction = direction.normalized;
        maxDistance = distance;

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = this.direction.x < 0f;
        }

        startPosition = transform.position;
    }

    private void Update()
    {
        transform.position +=
            (Vector3)(direction * speed * Time.deltaTime);

        float distance =
            Vector3.Distance(startPosition, transform.position);

        if (distance >= maxDistance)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        EnemyHealth enemyHealth =
            other.GetComponentInParent<EnemyHealth>();

        if (enemyHealth == null)
            return;

        if (hitEnemies.Contains(enemyHealth))
            return;

        hitEnemies.Add(enemyHealth);

        enemyHealth.TakeDamage(damageData);

        EnemyKnockback knockback =
            enemyHealth.GetComponent<EnemyKnockback>();

        if (knockback != null)
        {
            knockback.Knockback(direction, knockbackForce);
        }
    }
}
