using System.Collections.Generic;

[System.Serializable]
public class RunData
{
    // =========================
    // Player Level
    // =========================

    public int level = 1;
    public int currentExp = 0;
    public int needExp = 30;


    // =========================
    // Player Health
    // =========================

    public int currentHP = 100;
    public int maxHP = 100;


    // =========================
    // Weapon Stat
    // =========================

    public int weaponDamage = 10;
    public float attackCooldown = 0.5f;

    public WeaponType weaponType = WeaponType.Sword;
    public int swordLevel = 1;
    public int bowLevel = 1;
    public int daggerLevel = 1;
    public float criticalChance = 0f;
    public float criticalDamageMultiplier = 2f;


    // =========================
    // Element
    // =========================

    public int fireLevel = 0;
    public int lightningLevel = 0;
    public int iceLevel = 0;
    public int windLevel = 0;
    public ElementType selectedElement = ElementType.None;
    public bool hasSelectedElement = false;

    // =========================
    // Chapter Progress
    // =========================

    public int currentChapter = 1;
    public bool runCleared = false;


    // =========================
    // Upgrade Stack
    // =========================

    public Dictionary<UpgradeData, int>
        upgradeStacks = new();
}