using System.Collections;
using UnityEngine;

public class ShockEffect : StatusEffect
{
    private readonly int shockDamage;
    private readonly float stunDuration;

    public ShockEffect(
        int shockDamage,
        float stunDuration)
    {
        this.shockDamage = Mathf.Max(0, shockDamage);
        this.stunDuration = Mathf.Max(0f, stunDuration);
    }

    public override void Apply(
        GameObject target,
        StatusEffectManager manager)
    {
        if (target == null || manager == null)
            return;

        manager.StartEffectCoroutine(
            StatusEffectType.Shock,
            ShockRoutine(target)
        );
    }

    private IEnumerator ShockRoutine(GameObject target)
    {
        if (target == null)
            yield break;

        EnemyHealth enemyHealth =
            target.GetComponent<EnemyHealth>();

        if (enemyHealth != null &&
            shockDamage > 0)
        {
            enemyHealth.TakeDamage(
                new DamageData(
                    shockDamage,
                    false,
                    ElementType.None
                )
            );
        }

        EnemyStun enemyStun =
            target.GetComponent<EnemyStun>();

        if (enemyStun != null &&
            stunDuration > 0f)
        {
            enemyStun.Stun(stunDuration);
        }

        yield break;
    }
}