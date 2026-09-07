using System.Collections;
using UnityEngine;

public class EnemyDeathEffect : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Death Effect")]
    [SerializeField, Min(0.01f)]
    private float duration = 0.25f;

    [SerializeField, Min(1f)]
    private float scaleUp = 1.2f;

    [SerializeField, Min(0f)]
    private float animationWaitTime = 0.5f;

    private EnemyAI enemyAI;
    private EnemyAttack enemyAttack;
    private Rigidbody2D rb;

    private Collider2D[] colliders;

    private bool isPlaying;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer =
                GetComponent<SpriteRenderer>();
        }

        enemyAI =
            GetComponent<EnemyAI>();

        enemyAttack = 
            GetComponentInChildren<EnemyAttack>();

        rb =
            GetComponent<Rigidbody2D>();

        colliders = 
            GetComponentsInChildren<Collider2D>();
    }

    public void Play()
    {
        if (isPlaying)
            return;

        isPlaying = true;

        StartCoroutine(
            DeathRoutine()
        );
    }

    private IEnumerator DeathRoutine()
    {
        DisableGameplayComponents();

        // Death 애니메이션이 끝날 때까지 기다림
        yield return new WaitForSeconds(animationWaitTime);

        if (spriteRenderer == null)
        {
            Destroy(gameObject);
            yield break;
        }

        Vector3 startScale =
            transform.localScale;

        Vector3 endScale =
            startScale * scaleUp;

        Color startColor =
            spriteRenderer.color;

        Color endColor =
            new Color(
                startColor.r,
                startColor.g,
                startColor.b,
                0f
            );

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsedTime / duration
                );

            transform.localScale =
                Vector3.Lerp(
                    startScale,
                    endScale,
                    t
                );

            spriteRenderer.color =
                Color.Lerp(
                    startColor,
                    endColor,
                    t
                );

            yield return null;
        }

        Destroy(gameObject);
    }

    private void DisableGameplayComponents()
    {
        if (colliders != null)
        {
            foreach (Collider2D col in colliders)
            {
                if (col != null)
                {
                    col.enabled = false;
                }
            }
        }

        if (enemyAI != null)
        {
            enemyAI.enabled = false;
        }

        if (enemyAttack != null)
        {
            enemyAttack.enabled = false;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;

            // 죽은 뒤 물리 연산 자체를 중지
            rb.simulated = false;
        }
    }
}