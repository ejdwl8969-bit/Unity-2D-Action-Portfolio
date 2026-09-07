using UnityEngine;

public enum UpgradeType
{
    // 공통
    AttackDamage = 0,
    MoveSpeed = 1,
    MaxHP = 2,
    CriticalChance = 3,
    CriticalDamage = 4,

    // 검
    SwordThirdHitDamage = 5,
    WindSlashDamage = 6,
    WindSlashDistance = 7,

    // 활
    ArrowSpeed = 8,
    ArrowDistance = 9,
    ArrowPierce = 10,

    // 단검
    DaggerAttackSpeed = 14,
    DaggerEchoChance = 15,
    DaggerEchoDamage = 16,

    // 속성
    BurnDamage = 17,
    BurnDuration = 18,
    ShockChance = 19,
    ShockDamage = 20,
    ShockStunDuration = 21,
    IceTickDamage = 22,
    IceDuration = 23,
    IceSlowAmount = 24,

    // 속성 레벨
    FireLevel = 25,
    LightningLevel = 26,
    IceLevel = 27,
    WindLevel = 28,

    // Weapon level (append-only for serialized enum safety)
    WeaponLevel = 29
}

public enum UpgradeRarity
{
    Common,
    Rare,
    Legendary
}

[CreateAssetMenu(
    fileName = "UpgradeData",
    menuName = "Game/Upgrade Data"
)]
public class UpgradeData : ScriptableObject
{
    public string upgradeName;

    [TextArea]
    public string description;

    public UpgradeType upgradeType;
    public UpgradeRarity rarity;

    public float value;

    [Header("Weapon Level Target")]
    public WeaponType targetWeapon = WeaponType.Sword;

    [Min(1)]
    public int maxStack = 10;

    public Sprite icon;

    public bool IsCompatibleWithWeapon(WeaponType weaponType)
    {
        switch (upgradeType)
        {
            case UpgradeType.SwordThirdHitDamage:
            case UpgradeType.WindSlashDamage:
            case UpgradeType.WindSlashDistance:
                return weaponType == WeaponType.Sword;

            case UpgradeType.ArrowSpeed:
            case UpgradeType.ArrowDistance:
            case UpgradeType.ArrowPierce:
                return weaponType == WeaponType.Bow;

            case UpgradeType.DaggerAttackSpeed:
            case UpgradeType.DaggerEchoChance:
            case UpgradeType.DaggerEchoDamage:
                return weaponType == WeaponType.Dagger;

            case UpgradeType.WeaponLevel:
                return weaponType == targetWeapon;

            default:
                return true;
        }
    }

    public bool IsWeaponLevelUpgrade()
    {
        return upgradeType == UpgradeType.WeaponLevel;
    }

    public bool IsElementLevelUpgrade()
    {
        return upgradeType == UpgradeType.FireLevel ||
               upgradeType == UpgradeType.LightningLevel ||
               upgradeType == UpgradeType.IceLevel ||
               upgradeType == UpgradeType.WindLevel;
    }

    public ElementType GetElementType()
    {
        switch (upgradeType)
        {
            case UpgradeType.FireLevel:
                return ElementType.Fire;
            case UpgradeType.LightningLevel:
                return ElementType.Lightning;
            case UpgradeType.IceLevel:
                return ElementType.Ice;
            case UpgradeType.WindLevel:
                return ElementType.Wind;
            default:
                return ElementType.None;
        }
    }
}
