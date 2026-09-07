using UnityEngine;

[System.Serializable]
public class WeaponStat
{
    [Header("Attack")]
    public int damage = 10;
    public float attackCooldown = 0.5f;

    [Header("Critical")]
    public float criticalChance = 0f;
    public float criticalDamageMultiplier = 2f;

    public void AddDamage(int value)
    {
        damage += value;

        damage = Mathf.Max(1, damage);
    }

    public void AddCriticalChance(float value)
    {
        criticalChance += value;

        criticalChance = Mathf.Clamp(
            criticalChance,
            0f,
            100f
        );
    }

    public void AddCriticalDamage(float value)
    {
        criticalDamageMultiplier += value;

        criticalDamageMultiplier = Mathf.Max(
            1f,
            criticalDamageMultiplier
        );
    }

    public void AddAttackSpeed(float value)
    {
        attackCooldown -= value;

        attackCooldown = Mathf.Max(
            0.05f,
            attackCooldown
        );
    }

    public void SetValues(
    int newDamage,
    float newAttackCooldown,
    float newCriticalChance,
    float newCriticalDamageMultiplier)
    {
        damage =
            Mathf.Max(
                1,
                newDamage
            );

        attackCooldown =
            Mathf.Max(
                0.05f,
                newAttackCooldown
            );

        criticalChance =
            Mathf.Clamp(
                newCriticalChance,
                0f,
                100f
            );

        criticalDamageMultiplier =
            Mathf.Max(
                1f,
                newCriticalDamageMultiplier
            );
    }
}