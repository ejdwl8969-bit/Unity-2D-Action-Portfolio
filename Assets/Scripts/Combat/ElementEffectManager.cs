using UnityEngine;

[RequireComponent(typeof(StatusEffectManager))]
public class ElementEffectManager : MonoBehaviour
{
    // ==================================================
    // Fire
    // ==================================================

    [Header("Fire - Base")]
    [SerializeField, Min(0)]
    private int burnBaseDamage = 2;

    [SerializeField, Min(0)]
    private int burnDamagePerLevel = 1;

    [SerializeField, Min(0f)]
    private float burnBaseDuration = 3f;

    [SerializeField, Min(0f)]
    private float burnDurationPerLevel = 0.5f;

    [SerializeField, Min(0.01f)]
    private float burnTickInterval = 1f;


    // ==================================================
    // Lightning
    // ==================================================

    [Header("Lightning - Base")]
    [SerializeField, Range(0f, 100f)]
    private float shockBaseChance = 20f;

    [SerializeField, Min(0f)]
    private float shockChancePerLevel = 5f;

    [SerializeField, Range(0f, 1f)]
    private float shockBaseDamageRatio = 0.2f;

    [SerializeField, Min(0f)]
    private float shockDamageRatioPerLevel = 0.05f;

    [SerializeField, Min(0f)]
    private float shockBaseStunDuration = 0.5f;

    [SerializeField, Min(0f)]
    private float shockStunPerLevel = 0.1f;


    // ==================================================
    // Ice
    // ==================================================

    [Header("Ice - Base")]
    [SerializeField, Range(0f, 1f)]
    private float iceBaseDamageRatio = 0.1f;

    [SerializeField, Min(0f)]
    private float iceDamageRatioPerLevel = 0.02f;

    [SerializeField, Min(0f)]
    private float iceBaseDuration = 2f;

    [SerializeField, Min(0f)]
    private float iceDurationPerLevel = 0.25f;

    [SerializeField, Range(0f, 1f)]
    private float iceBaseSlowMultiplier = 0.8f;

    [SerializeField, Range(0f, 1f)]
    private float iceSlowPerLevel = 0.05f;

    [SerializeField, Min(0.01f)]
    private float iceTickInterval = 0.5f;


    private StatusEffectManager statusEffectManager;

    private void Awake()
    {
        statusEffectManager =
            GetComponent<StatusEffectManager>();
    }

    public void ApplyElementEffect(
        DamageData damageData)
    {
        if (statusEffectManager == null)
            return;

        if (damageData.Element == ElementType.None)
            return;

        int elementLevel =
            Mathf.Max(
                1,
                damageData.ElementLevel
            );

        switch (damageData.Element)
        {
            case ElementType.Fire:
                ApplyFire(
                    elementLevel
                );
                break;

            case ElementType.Lightning:
                ApplyLightning(
                    damageData,
                    elementLevel
                );
                break;

            case ElementType.Ice:
                ApplyIce(
                    damageData,
                    elementLevel
                );
                break;

            case ElementType.Wind:
                // Wind는 무기별 효과로 처리
                break;
        }
    }


    // ==================================================
    // Fire
    // ==================================================

    private void ApplyFire(
        int level)
    {
        int damagePerTick =
            burnBaseDamage +
            (level - 1) *
            burnDamagePerLevel;

        float duration =
            burnBaseDuration +
            (level - 1) *
            burnDurationPerLevel;

        if (damagePerTick <= 0 ||
            duration <= 0f)
        {
            return;
        }

        statusEffectManager.Apply(
            new BurnEffect(
                damagePerTick,
                duration,
                burnTickInterval
            )
        );
    }


    // ==================================================
    // Lightning
    // ==================================================

    private void ApplyLightning(
        DamageData damageData,
        int level)
    {
        float shockChance =
            shockBaseChance +
            (level - 1) *
            shockChancePerLevel;

        shockChance =
            Mathf.Clamp(
                shockChance,
                0f,
                100f
            );

        if (Random.Range(0f, 100f) >=
            shockChance)
        {
            return;
        }

        float damageRatio =
            shockBaseDamageRatio +
            (level - 1) *
            shockDamageRatioPerLevel;

        int shockDamage =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    damageData.Damage *
                    damageRatio
                )
            );

        float stunDuration =
            shockBaseStunDuration +
            (level - 1) *
            shockStunPerLevel;

        statusEffectManager.Apply(
            new ShockEffect(
                shockDamage,
                stunDuration
            )
        );
    }


    // ==================================================
    // Ice
    // ==================================================

    private void ApplyIce(
        DamageData damageData,
        int level)
    {
        float damageRatio =
            iceBaseDamageRatio +
            (level - 1) *
            iceDamageRatioPerLevel;

        int tickDamage =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    damageData.Damage *
                    damageRatio
                )
            );

        float duration =
            iceBaseDuration +
            (level - 1) *
            iceDurationPerLevel;

        float slowMultiplier =
            iceBaseSlowMultiplier -
            (level - 1) *
            iceSlowPerLevel;

        slowMultiplier =
            Mathf.Clamp(
                slowMultiplier,
                0.2f,
                1f
            );

        statusEffectManager.Apply(
            new IceEffect(
                tickDamage,
                duration,
                iceTickInterval,
                slowMultiplier
            )
        );
    }
}