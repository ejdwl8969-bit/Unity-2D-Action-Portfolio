using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyStun : MonoBehaviour
{
    private EnemyAI enemyAI;
    private Rigidbody2D rb;

    private Coroutine stunCoroutine;
    private float stunEndTime;

    public bool IsStunned { get; private set; }

    private void Awake()
    {
        enemyAI = GetComponent<EnemyAI>();
        rb = GetComponent<Rigidbody2D>();
    }

    public void Stun(float duration)
    {
        if (duration <= 0f)
            return;

        float newEndTime =
            Time.time + duration;

        if (newEndTime > stunEndTime)
        {
            stunEndTime = newEndTime;
        }

        if (stunCoroutine == null)
        {
            stunCoroutine =
                StartCoroutine(StunRoutine());
        }
    }

    private IEnumerator StunRoutine()
    {
        IsStunned = true;

        if (enemyAI != null)
        {
            enemyAI.enabled = false;
        }

        StopMovement();

        while (Time.time < stunEndTime)
        {
            yield return null;
        }

        RecoverFromStun();
    }

    private void StopMovement()
    {
        if (rb == null)
            return;

        rb.linearVelocity = Vector2.zero;
    }

    private void RecoverFromStun()
    {
        IsStunned = false;

        if (enemyAI != null)
        {
            enemyAI.enabled = true;
        }

        stunCoroutine = null;
    }

    private void OnDisable()
    {
        if (stunCoroutine != null)
        {
            StopCoroutine(stunCoroutine);
            stunCoroutine = null;
        }

        IsStunned = false;
        stunEndTime = 0f;

        if (enemyAI != null)
        {
            enemyAI.enabled = true;
        }
    }
}