using UnityEngine;

public abstract class StatusEffect
{
    public abstract void Apply(GameObject target, StatusEffectManager manager);
}