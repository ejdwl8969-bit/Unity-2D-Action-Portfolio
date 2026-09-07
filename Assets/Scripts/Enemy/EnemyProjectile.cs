using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [Header("Projectile")]
    [SerializeField, Min(0.1f)]
    private float maxDistance = 8f;

    [Header("Knockback")]
    [SerializeField, Min(0f)]
    private float knockbackForce = 5f;

    private Vector2 direction;
    private float speed;
    private int damage;

    private Vector3 startPosition;
    private bool isInitialized;
    private bool hasMovedSinceSpawn;

    public void Initialize(
        Vector2 direction,
        float speed,
        int damage)
    {
        this.direction = direction.normalized;
        this.speed = Mathf.Max(0.1f, speed);
        this.damage = Mathf.Max(0, damage);
        startPosition = transform.position;
        isInitialized = true;
        hasMovedSinceSpawn = false;

        float angle = Mathf.Atan2(this.direction.y, this.direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Update()
    {
        if (!isInitialized)
            return;

        Move();
        CheckDistance();
    }

    private void Move()
    {
        transform.position += (Vector3)(direction * speed * Time.deltaTime);
        hasMovedSinceSpawn = true;
    }

    private void CheckDistance()
    {
        if (Vector3.Distance(startPosition, transform.position) >= maxDistance)
            ReleaseProjectile();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleTrigger(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleTrigger(other);
    }

    private void HandleTrigger(Collider2D other)
    {
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage, direction, knockbackForce);
            ReleaseProjectile();
            return;
        }

        if (IsTerrainCollider(other) && hasMovedSinceSpawn)
            ReleaseProjectile();
    }

    private static bool IsTerrainCollider(Collider2D other)
    {
        if (other == null || other.isTrigger)
            return false;

        string tag = other.tag;
        if (string.Equals(tag, "Ground", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tag, "Platform", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tag, "Wall", System.StringComparison.OrdinalIgnoreCase))
            return true;

        string objectName = other.gameObject.name;
        return objectName.IndexOf("Ground", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("Platform", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("Wall", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("Walkway", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void ReleaseProjectile()
    {
        if (!isInitialized)
            return;

        isInitialized = false;
        PoolObject poolObject = GetComponent<PoolObject>();
        if (poolObject != null)
            poolObject.ReturnToPool();
        else
            Destroy(gameObject);
    }
}
