using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum StatusEffectType
{
    Burn,
    Ice,
    Shock
}

public class StatusEffectManager : MonoBehaviour
{
    private readonly Dictionary<StatusEffectType, Coroutine>
        activeEffects = new();

    public void Apply(StatusEffect effect)
    {
        if (effect == null)
            return;

        effect.Apply(gameObject, this);
    }

    public void StartEffectCoroutine(
        StatusEffectType effectType,
        IEnumerator routine)
    {
        if (routine == null)
            return;

        StopEffect(effectType);

        Coroutine coroutine =
            StartCoroutine(
                RunEffectCoroutine(effectType, routine)
            );

        activeEffects[effectType] = coroutine;
    }

    public void StopEffect(StatusEffectType effectType)
    {
        if (!activeEffects.TryGetValue(
                effectType,
                out Coroutine coroutine))
        {
            return;
        }

        if (coroutine != null)
        {
            StopCoroutine(coroutine);
        }

        activeEffects.Remove(effectType);
    }

    public bool IsEffectActive(
        StatusEffectType effectType)
    {
        return activeEffects.ContainsKey(effectType);
    }

    private IEnumerator RunEffectCoroutine(
        StatusEffectType effectType,
        IEnumerator routine)
    {
        yield return routine;

        activeEffects.Remove(effectType);
    }

    private void OnDisable()
    {
        StopAllEffects();
    }

    private void StopAllEffects()
    {
        foreach (Coroutine coroutine
                 in activeEffects.Values)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }
        }

        activeEffects.Clear();
    }
}