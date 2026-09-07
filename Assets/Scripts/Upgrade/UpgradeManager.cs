using System.Collections.Generic;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerAttack playerAttack;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerHealth playerHealth;

    private readonly Dictionary<UpgradeData, int>
        upgradeStacks = new();
    private bool restoredPersistentEffects;

    private Weapon CurrentWeapon =>
        playerAttack != null
        ? playerAttack.CurrentWeapon
        : null;

    public PlayerAttack PlayerAttack => playerAttack;

    public bool CanAcquire(UpgradeData upgrade)
    {
        if (upgrade == null)
            return false;

        if (playerAttack == null ||
            !upgrade.IsCompatibleWithWeapon(playerAttack.CurrentWeaponType))
        {
            return false;
        }

        if (!IsCompatibleWithCurrentElement(upgrade))
            return false;

        if (upgrade.IsWeaponLevelUpgrade())
        {
            Weapon targetWeapon = playerAttack.GetWeapon(upgrade.targetWeapon);
            if (targetWeapon == null || targetWeapon.IsMaxLevel)
                return false;
        }

        return GetStack(upgrade) < upgrade.maxStack;
    }

    public int GetStack(UpgradeData upgrade)
    {
        if (upgrade == null)
            return 0;

        if (upgradeStacks.TryGetValue(
                upgrade,
                out int stack))
        {
            return stack;
        }

        return 0;
    }

    public bool ApplyUpgrade(UpgradeData upgrade)
    {
        if (!CanAcquire(upgrade))
            return false;

        bool applied =
            ApplyUpgradeEffect(upgrade);

        if (!applied)
            return false;

        upgradeStacks[upgrade] =
            GetStack(upgrade) + 1;

        return true;
    }

    private bool ApplyUpgradeEffect(
        UpgradeData upgrade)
    {
        switch (upgrade.upgradeType)
        {
            // =========================
            // 공통 업그레이드
            // =========================

            case UpgradeType.AttackDamage:
                return ApplyAttackDamage(
                    upgrade.value
                );

            case UpgradeType.MoveSpeed:
                return ApplyMoveSpeed(
                    upgrade.value
                );

            case UpgradeType.MaxHP:
                return ApplyMaxHP(
                    upgrade.value
                );

            case UpgradeType.CriticalChance:
                return ApplyCriticalChance(
                    upgrade.value
                );

            case UpgradeType.CriticalDamage:
                return ApplyCriticalDamage(
                    upgrade.value
                );


            // =========================
            // Sword
            // =========================

            case UpgradeType.SwordThirdHitDamage:
                return ApplySwordThirdHitDamage(
                    upgrade.value
                );

            case UpgradeType.WindSlashDamage:
                return ApplyWindSlashDamage(
                    upgrade.value
                );

            case UpgradeType.WindSlashDistance:
                return ApplyWindSlashDistance(
                    upgrade.value
                );


            // =========================
            // Bow
            // =========================

            case UpgradeType.ArrowSpeed:
                return ApplyArrowSpeed(
                    upgrade.value
                );

            case UpgradeType.ArrowDistance:
                return ApplyArrowDistance(
                    upgrade.value
                );

            case UpgradeType.ArrowPierce:
                return ApplyArrowPierce(
                    upgrade.value
                );


            // =========================
            // Dagger
            // =========================

            case UpgradeType.DaggerAttackSpeed:
                return ApplyDaggerAttackSpeed(
                    upgrade.value
                );

            case UpgradeType.DaggerEchoChance:
                return ApplyDaggerEchoChance(
                    upgrade.value
                );

            case UpgradeType.DaggerEchoDamage:
                return ApplyDaggerEchoDamage(
                    upgrade.value
                );


            // =========================
            // Weapon Level
            // =========================

            case UpgradeType.WeaponLevel:
                return ApplyWeaponLevel(
                    upgrade.targetWeapon,
                    upgrade.value
                );


            // =========================
            // Element Level
            // =========================

            case UpgradeType.FireLevel:
                return ApplyFireLevel(
                    upgrade.value
                );

            case UpgradeType.LightningLevel:
                return ApplyLightningLevel(
                    upgrade.value
                );

            case UpgradeType.IceLevel:
                return ApplyIceLevel(
                    upgrade.value
                );

            case UpgradeType.WindLevel:
                return ApplyWindLevel(
                    upgrade.value
                );



            default:
                Debug.LogWarning(
                    $"아직 적용되지 않은 업그레이드: " +
                    $"{upgrade.upgradeType}"
                );

                return false;
        }
    }


    // ==================================================
    // Common
    // ==================================================

    private bool ApplyAttackDamage(float value)
    {
        Weapon weapon = CurrentWeapon;

        if (weapon == null ||
            weapon.Stat == null)
        {
            return false;
        }

        weapon.Stat.AddDamage(
            Mathf.RoundToInt(value)
        );

        return true;
    }

    private bool ApplyMoveSpeed(float value)
    {
        if (playerController == null)
            return false;

        playerController.AddMoveSpeed(value);

        return true;
    }

    private bool ApplyMaxHP(float value)
    {
        if (playerHealth == null)
            return false;

        playerHealth.AddMaxHP(
            Mathf.RoundToInt(value)
        );

        return true;
    }

    private bool ApplyCriticalChance(float value)
    {
        if (!HasValidWeapon())
            return false;

        CurrentWeapon.Stat.AddCriticalChance(
            value
        );

        return true;
    }

    private bool ApplyCriticalDamage(float value)
    {
        if (!HasValidWeapon())
            return false;

        CurrentWeapon.Stat.AddCriticalDamage(
            value
        );

        return true;
    }


    // ==================================================
    // Sword
    // ==================================================

    private bool ApplySwordThirdHitDamage(float value)
    {
        if (CurrentWeapon is not Sword sword)
            return false;

        sword.AddThirdHitDamage(value);

        return true;
    }

    private bool ApplyWindSlashDamage(float value)
    {
        if (CurrentWeapon is not Sword sword)
            return false;

        sword.AddWindSlashDamage(value);

        return true;
    }

    private bool ApplyWindSlashDistance(float value)
    {
        if (CurrentWeapon is not Sword sword)
            return false;

        sword.AddWindSlashDistance(value);

        return true;
    }


    // ==================================================
    // Bow
    // ==================================================

    private bool ApplyArrowSpeed(float value)
    {
        if (CurrentWeapon is not Bow bow)
            return false;

        bow.AddArrowSpeed(value);

        return true;
    }

    private bool ApplyArrowDistance(float value)
    {
        if (CurrentWeapon is not Bow bow)
            return false;

        bow.AddArrowDistance(value);

        return true;
    }

    private bool ApplyArrowPierce(float value)
    {
        if (CurrentWeapon is not Bow bow)
            return false;

        bow.AddArrowPierce(
            Mathf.RoundToInt(value)
        );

        return true;
    }


    // ==================================================
    // Dagger
    // ==================================================

    private bool ApplyDaggerAttackSpeed(float value)
    {
        if (CurrentWeapon is not Dagger dagger)
            return false;

        dagger.AddDaggerAttackSpeed(value);

        return true;
    }

    private bool ApplyDaggerEchoChance(float value)
    {
        if (CurrentWeapon is not Dagger dagger)
            return false;

        dagger.AddEchoChance(value);

        return true;
    }

    private bool ApplyDaggerEchoDamage(float value)
    {
        if (CurrentWeapon is not Dagger dagger)
            return false;

        dagger.AddEchoDamage(value);

        return true;
    }


    // ==================================================
    // Weapon Level
    // ==================================================

    private bool ApplyWeaponLevel(
        WeaponType weaponType,
        float value)
    {
        if (playerAttack == null)
            return false;

        Weapon weapon = playerAttack.GetWeapon(weaponType);
        if (weapon == null || weapon.IsMaxLevel)
            return false;

        weapon.AddLevel(Mathf.Max(1, Mathf.RoundToInt(value)));
        return true;
    }

    // ==================================================
    // Element Level
    // ==================================================

    private bool ApplyFireLevel(float value)
    {
        return ApplyElementLevel(ElementType.Fire, value);
    }

    private bool ApplyLightningLevel(float value)
    {
        return ApplyElementLevel(ElementType.Lightning, value);
    }

    private bool ApplyIceLevel(float value)
    {
        return ApplyElementLevel(ElementType.Ice, value);
    }

    private bool ApplyWindLevel(float value)
    {
        return ApplyElementLevel(ElementType.Wind, value);
    }

    private bool ApplyElementLevel(
        ElementType elementType,
        float value)
    {
        if (!HasValidElement())
            return false;

        int amount = Mathf.Max(1, Mathf.RoundToInt(value));
        return playerAttack.AddElementLevelForAllWeapons(elementType, amount);
    }


    // ==================================================
    // Utility
    // ==================================================

    private bool HasValidWeapon()
    {
        return CurrentWeapon != null &&
               CurrentWeapon.Stat != null;
    }

    private bool HasValidElement()
    {
        return playerAttack != null &&
               CurrentWeapon != null &&
               CurrentWeapon.Element != null;
    }

    private bool IsCompatibleWithCurrentElement(UpgradeData upgrade)
    {
        if (!upgrade.IsElementLevelUpgrade())
            return true;

        if (!HasValidElement())
            return false;

        ElementType selectedElement = RunManager.Instance != null
            ? RunManager.Instance.SelectedElement
            : CurrentWeapon.Element.GetMainElement();

        if (selectedElement == ElementType.None ||
            selectedElement != upgrade.GetElementType())
        {
            return false;
        }

        return CurrentWeapon.Element.GetLevel(selectedElement) < upgrade.maxStack;
    }

    public bool TryAcquireInitialElement(UpgradeData upgrade)
    {
        RunManager runManager = RunManager.Instance;
        if (upgrade == null || !upgrade.IsElementLevelUpgrade() ||
            runManager == null || runManager.HasSelectedElement ||
            !HasValidElement() ||
            GetStack(upgrade) != 0)
        {
            return false;
        }

        ElementType elementType = upgrade.GetElementType();

        // RunData's unselected flag is authoritative for a true new run.
        // Scene-authored weapon components can still contain old preview/test
        // element levels, so normalize them before applying the one allowed
        // initial choice through the existing level/stack path.
        playerAttack.SetElementLevelsForAllWeapons(0, 0, 0, 0);
        if (!playerAttack.AddElementLevelForAllWeapons(elementType, 1))
            return false;

        upgradeStacks[upgrade] = 1;
        runManager.CommitInitialElement(elementType, playerAttack);
        return true;
    }

    public int GetWeaponLevel(WeaponType weaponType)
    {
        return playerAttack != null
            ? playerAttack.GetWeaponLevel(weaponType)
            : Weapon.MinLevel;
    }

    public int GetElementLevel(ElementType elementType)
    {
        return HasValidElement()
            ? CurrentWeapon.Element.GetLevel(elementType)
            : 0;
    }
    public void ClearUpgrades()
    {
        upgradeStacks.Clear();
    }

    public Dictionary<UpgradeData, int>
    GetUpgradeStacksCopy()
    {
        return new Dictionary<UpgradeData, int>(
            upgradeStacks
        );
    }

    public void RestoreUpgradeStacks(
        Dictionary<UpgradeData, int> savedStacks)
    {
        upgradeStacks.Clear();

        if (savedStacks == null)
            return;

        // Snapshot-backed stats and levels are restored by RunManager.
        // Replay only component-local bonuses, once, to avoid double application.
        bool replayEffects = !restoredPersistentEffects;
        foreach (
            KeyValuePair<UpgradeData, int> pair
            in savedStacks)
        {
            if (pair.Key == null)
                continue;

            int stack = Mathf.Max(0, pair.Value);
            upgradeStacks[pair.Key] = stack;

            if (!replayEffects || !ShouldReplayOnSceneLoad(pair.Key.upgradeType))
                continue;

            for (int i = 0; i < stack; i++)
                ApplyUpgradeEffect(pair.Key);
        }

        restoredPersistentEffects = true;
    }

    private static bool ShouldReplayOnSceneLoad(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.MoveSpeed:
            case UpgradeType.SwordThirdHitDamage:
            case UpgradeType.WindSlashDamage:
            case UpgradeType.WindSlashDistance:
            case UpgradeType.ArrowSpeed:
            case UpgradeType.ArrowDistance:
            case UpgradeType.ArrowPierce:
            case UpgradeType.DaggerAttackSpeed:
            case UpgradeType.DaggerEchoChance:
            case UpgradeType.DaggerEchoDamage:
                return true;

            default:
                return false;
        }
    }
}
