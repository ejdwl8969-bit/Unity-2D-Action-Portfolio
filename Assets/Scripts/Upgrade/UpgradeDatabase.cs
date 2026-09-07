using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "UpgradeDatabase", menuName = "Game/Upgrade Database")]
public class UpgradeDatabase : ScriptableObject
{
    public List<UpgradeData> commonUpgrades;
    public List<UpgradeData> rareUpgrades;
    public List<UpgradeData> legendaryUpgrades;

    public UpgradeData GetRandomUpgradeByRarity(
    UpgradeRarity rarity,
    List<UpgradeData> excludeList,
    UpgradeManager upgradeManager)
    {
        List<UpgradeData> sourceList = GetListByRarity(rarity);
        List<UpgradeData> candidates = new List<UpgradeData>();

        foreach (UpgradeData upgrade in sourceList)
        {
            if (upgrade == null)
                continue;

            if (excludeList.Contains(upgrade))
                continue;

            if (!upgradeManager.CanAcquire(upgrade))
                continue;

            candidates.Add(upgrade);
        }

        if (candidates.Count == 0)
            return null;

        int index = Random.Range(0, candidates.Count);
        return candidates[index];
    }

    public List<UpgradeData> GetEligibleUpgrades(
        List<UpgradeData> excludeList,
        UpgradeManager upgradeManager)
    {
        List<UpgradeData> candidates = new List<UpgradeData>();
        AddEligible(commonUpgrades, excludeList, upgradeManager, candidates);
        AddEligible(rareUpgrades, excludeList, upgradeManager, candidates);
        AddEligible(legendaryUpgrades, excludeList, upgradeManager, candidates);
        return candidates;
    }

    private static void AddEligible(
        List<UpgradeData> source,
        List<UpgradeData> excludeList,
        UpgradeManager upgradeManager,
        List<UpgradeData> destination)
    {
        if (source == null)
            return;

        foreach (UpgradeData upgrade in source)
        {
            if (upgrade == null || excludeList.Contains(upgrade) ||
                destination.Contains(upgrade) || !upgradeManager.CanAcquire(upgrade))
                continue;

            destination.Add(upgrade);
        }
    }

    private List<UpgradeData> GetListByRarity(UpgradeRarity rarity)
    {
        switch (rarity)
        {
            case UpgradeRarity.Common:
                return commonUpgrades;

            case UpgradeRarity.Rare:
                return rareUpgrades;

            case UpgradeRarity.Legendary:
                return legendaryUpgrades;
        }

        return commonUpgrades;
    }
}
