using UnityEngine;

public abstract class Weapon : MonoBehaviour
{
    private const float GlobalPlayerDamageMultiplier = 1.5f;

    public const int MinLevel = 1;
    public const int MaxLevel = 5;
    [Header("Basic")]
    [SerializeField]
    private string weaponName = "Weapon";

    [SerializeField, Min(1)]
    private int level = 1;

    [Header("Stat")]
    [SerializeField]
    protected WeaponStat stat = new WeaponStat();

    [Header("Element")]
    [SerializeField]
    protected WeaponElement element = new WeaponElement();

    private float lastAttackTime =
        -Mathf.Infinity;

    public string WeaponName => weaponName;
    public int Level => level;
    public bool IsMaxLevel => level >= MaxLevel;
    public WeaponStat Stat => stat;
    public WeaponElement Element => element;

    protected bool IsWindActive =>
        element != null &&
        element.WindLevel > 0;

    protected int WindLevel =>
        element != null
            ? element.WindLevel
            : 0;

    public bool CanAttack()
    {
        return Time.time >=
               lastAttackTime +
               GetAttackCooldown();
    }

    public void TryAttack()
    {
        if (!CanAttack())
            return;

        lastAttackTime =
            Time.time;

        Attack();
    }

    protected virtual float GetAttackCooldown()
    {
        if (stat == null)
            return 0.5f;

        return Mathf.Max(
            0.01f,
            stat.attackCooldown
        );
    }

    protected DamageData CreateDamageData()
    {
        if (stat == null)
        {
            Debug.LogWarning(
                $"{name}: WeaponStat이 없습니다."
            );

            return new DamageData(
                1,
                false,
                ElementType.None
            );
        }

        bool isCritical =
            RollCritical();

        int finalDamage = GetBaseDamage();

        if (isCritical)
        {
            finalDamage =
                Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        finalDamage *
                        stat.criticalDamageMultiplier
                    )
                );
        }

        ElementType elementType =
            element != null
                ? element.GetMainElement()
                : ElementType.None;

        int elementLevel =
            element != null
                ? element.GetMainElementLevel()
                : 0;

        return new DamageData(
            finalDamage,
            isCritical,
            elementType,
            elementLevel
        );
    }

    private bool RollCritical()
    {
        if (stat == null)
            return false;

        float chance =
            Mathf.Clamp(
                stat.criticalChance,
                0f,
                100f
            );

        return Random.Range(
                   0f,
                   100f
               ) < chance;
    }

    public void SetLevel(int value)
    {
        level = Mathf.Clamp(
            value,
            MinLevel,
            MaxLevel
        );
    }

    public void AddLevel(int value)
    {
        if (value <= 0)
            return;

        SetLevel(level + value);
    }

    public float GetLevelDamageMultiplier()
    {
        return 1f + (level - MinLevel) * 0.10f;
    }

    public int GetBaseDamage()
    {
        if (stat == null)
            return 1;

        // Apply shared and weapon-level multipliers in one place. Secondary
        // attacks derive from this result instead of multiplying them again.
        return Mathf.Max(
            1,
            Mathf.RoundToInt(
                stat.damage *
                GetLevelDamageMultiplier() *
                GlobalPlayerDamageMultiplier
            )
        );
    }

    protected abstract void Attack();

    protected virtual void ApplyWindEffect()
    {
        // 필요한 무기만 override.
    }
}