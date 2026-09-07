using System.Collections;
using UnityEngine;

public class BurnEffect : StatusEffect
{
    private readonly int damagePerTick;
    private readonly float duration;
    private readonly float tickInterval;

    public BurnEffect(
        int damagePerTick,
        float duration,
        float tickInterval)
    {
        this.damagePerTick = Mathf.Max(0, damagePerTick);
        this.duration = Mathf.Max(0f, duration);
        this.tickInterval = Mathf.Max(0.01f, tickInterval);
    }

    public override void Apply(
        GameObject target,
        StatusEffectManager manager)
    {
        if (target == null || manager == null)
            return;

        manager.StartEffectCoroutine(
            StatusEffectType.Burn,
            BurnRoutine(target)
        );
    }

    private IEnumerator BurnRoutine(GameObject target)
    {
        if (target == null)
            yield break;

        EnemyHealth enemyHealth =
            target.GetComponent<EnemyHealth>();

        if (enemyHealth == null)
            yield break;

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            yield return new WaitForSeconds(
                tickInterval
            );

            if (target == null)
                yield break;

            enemyHealth.TakeDamage(
                new DamageData(
                    damagePerTick,
                    false,
                    ElementType.None
                )
            );

            elapsedTime += tickInterval;
        }
    }
}