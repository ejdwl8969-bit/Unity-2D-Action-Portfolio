using System.Collections;
using UnityEngine;

public class IceEffect : StatusEffect
{
    private readonly int tickDamage;
    private readonly float duration;
    private readonly float tickInterval;
    private readonly float slowMultiplier;

    public IceEffect(
        int tickDamage,
        float duration,
        float tickInterval,
        float slowMultiplier)
    {
        this.tickDamage = Mathf.Max(0, tickDamage);
        this.duration = Mathf.Max(0f, duration);
        this.tickInterval = Mathf.Max(0.01f, tickInterval);
        this.slowMultiplier = Mathf.Clamp01(slowMultiplier);
    }

    public override void Apply(
        GameObject target,
        StatusEffectManager manager)
    {
        if (target == null || manager == null)
            return;

        manager.StartEffectCoroutine(
            StatusEffectType.Ice,
            IceRoutine(target)
);
    }

    private IEnumerator IceRoutine(GameObject target)
    {
        if (target == null)
            yield break;

        EnemyAI enemyAI =
            target.GetComponent<EnemyAI>();

        EnemyHealth enemyHealth =
            target.GetComponent<EnemyHealth>();

        float originalSpeed = 0f;
        bool speedChanged = false;

        if (enemyAI != null)
        {
            originalSpeed = enemyAI.MoveSpeed;

            enemyAI.MoveSpeed =
                originalSpeed * slowMultiplier;

            speedChanged = true;
        }

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            if (target == null)
                yield break;

            if (enemyHealth != null &&
                tickDamage > 0)
            {
                enemyHealth.TakeDamage(
                    new DamageData(
                        tickDamage,
                        false,
                        ElementType.None
                    )
                );
            }

            yield return new WaitForSeconds(
                tickInterval
            );

            elapsedTime += tickInterval;
        }

        if (speedChanged &&
            enemyAI != null)
        {
            enemyAI.MoveSpeed = originalSpeed;
        }
    }
}