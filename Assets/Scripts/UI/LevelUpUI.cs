using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class LevelUpUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    public bool IsOpen => panel != null && panel.activeInHierarchy;
    [SerializeField] private UpgradeManager upgradeManager;
    [SerializeField] private UpgradeDatabase upgradeDatabase;

    [Header("Rarity Chance")]
    [SerializeField, Min(0f)]
    private float commonChance = 85f;

    [SerializeField, Min(0f)]
    private float rareChance = 12f;

    [SerializeField, Min(0f)]
    private float legendaryChance = 3f;

    [Header("Option Text")]
    [SerializeField] private TMP_Text option1Text;
    [SerializeField] private TMP_Text option2Text;
    [SerializeField] private TMP_Text option3Text;

    private UpgradeData option1;
    private UpgradeData option2;
    private UpgradeData option3;

    public void Show()
    {
        if (RunManager.Instance != null && RunManager.Instance.IsRunCleared)
            return;

        bool wasOpen = IsOpen;

        List<UpgradeData> selectedUpgrades =
            GetRandomUpgrades(3);

        if (selectedUpgrades.Count < 3)
        {
            Debug.LogWarning(
                "선택 가능한 업그레이드가 3개보다 적습니다."
            );

            return;
        }

        option1 = selectedUpgrades[0];
        option2 = selectedUpgrades[1];
        option3 = selectedUpgrades[2];

        option1Text.text = GetOptionText(option1);
        option2Text.text = GetOptionText(option2);
        option3Text.text = GetOptionText(option3);

        panel.SetActive(true);
        if (!wasOpen && panel.activeInHierarchy)
            SfxPlayer.Play(SfxId.LevelUpOpen);
        Time.timeScale = 0f;
    }

    private List<UpgradeData> GetRandomUpgrades(int count)
    {
        List<UpgradeData> selectedUpgrades =
            new List<UpgradeData>();

        int attempts = 0;
        const int maxAttempts = 100;

        while (
            selectedUpgrades.Count < count &&
            attempts < maxAttempts)
        {
            attempts++;

            UpgradeRarity rarity = RollRarity();

            UpgradeData upgrade =
                upgradeDatabase.GetRandomUpgradeByRarity(
                    rarity,
                    selectedUpgrades,
                    upgradeManager
                );

            if (upgrade != null)
            {
                selectedUpgrades.Add(upgrade);
            }
        }

        while (selectedUpgrades.Count < count)
        {
            List<UpgradeData> fallback =
                upgradeDatabase.GetEligibleUpgrades(selectedUpgrades, upgradeManager);
            if (fallback.Count == 0)
                break;

            selectedUpgrades.Add(fallback[Random.Range(0, fallback.Count)]);
        }

        return selectedUpgrades;
    }

    private UpgradeRarity RollRarity()
    {
        float totalChance =
            commonChance +
            rareChance +
            legendaryChance;

        if (totalChance <= 0f)
            return UpgradeRarity.Common;

        float randomValue =
            Random.Range(0f, totalChance);

        if (randomValue < commonChance)
            return UpgradeRarity.Common;

        randomValue -= commonChance;

        if (randomValue < rareChance)
            return UpgradeRarity.Rare;

        return UpgradeRarity.Legendary;
    }

    private string GetOptionText(UpgradeData upgrade)
    {
        int currentStack = upgradeManager.GetStack(upgrade);
        string progressText = GetProgressText(upgrade, currentStack);

        string description =
            upgrade.description.Replace(
                "{value}",
                GetDisplayValue(upgrade)
            );

        return
            $"<size=15><color=#BBA88D>[{upgrade.rarity}]</color></size>\n" +
            $"<size=23><color=#EFDEBF>{upgrade.upgradeName}</color></size>\n" +
            $"<size=18>{description}</size>\n" +
            $"<size=16><color=#BBA88D>{progressText}</color></size>";
    }

    private string GetProgressText(
        UpgradeData upgrade,
        int currentStack)
    {
        if (upgrade.IsWeaponLevelUpgrade())
        {
            int level = upgradeManager.GetWeaponLevel(upgrade.targetWeapon);
            return $"Weapon Lv.{level} / {Weapon.MaxLevel}";
        }

        if (upgrade.IsElementLevelUpgrade())
        {
            int level = upgradeManager.GetElementLevel(upgrade.GetElementType());
            return $"Element Lv.{level} / {upgrade.maxStack}";
        }

        return $"({currentStack}/{upgrade.maxStack})";
    }
    private string GetDisplayValue(UpgradeData upgrade)
    {
        switch (upgrade.upgradeType)
        {
            case UpgradeType.CriticalChance:
                // CriticalChance uses percentage points (0-100) in data and runtime.
                return $"{upgrade.value:0.#}%";

            case UpgradeType.DaggerEchoChance:
            case UpgradeType.ShockChance:
                return $"{upgrade.value * 100f:0.#}%";

            case UpgradeType.SwordThirdHitDamage:
            case UpgradeType.WindSlashDamage:
            case UpgradeType.DaggerAttackSpeed:
            case UpgradeType.DaggerEchoDamage:
            case UpgradeType.CriticalDamage:
                return $"{upgrade.value * 100f:0.#}%";

            default:
                return $"{upgrade.value:0.##}";
        }
    }

    public void SelectOption1()
    {
        TrySelect(option1);
    }

    public void SelectOption2()
    {
        TrySelect(option2);
    }

    public void SelectOption3()
    {
        TrySelect(option3);
    }

    private void TrySelect(UpgradeData upgrade)
    {
        if (upgradeManager != null && upgradeManager.ApplyUpgrade(upgrade))
        {
            SfxPlayer.Play(SfxId.UpgradeSelect);
            Hide();
        }
    }

    private void Hide()
    {
        panel.SetActive(false);
        if (!SceneTransition.IsTransitioning && !PauseManager.IsGamePaused && !(RunManager.Instance != null && RunManager.Instance.IsRunCleared))
            Time.timeScale = 1f;
    }
}
