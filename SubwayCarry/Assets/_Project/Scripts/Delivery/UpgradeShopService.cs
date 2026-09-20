using System;
using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.Delivery
{
    public sealed class UpgradeShopService : MonoBehaviour
    {
        [SerializeField] private EconomyService economyService;
        [SerializeField] private List<UpgradeData> catalog = new List<UpgradeData>();

        private readonly Dictionary<UpgradeStat, int> currentLevels = new Dictionary<UpgradeStat, int>();

        public event Action<UpgradeStat, int, float> UpgradePurchased;

        public int GetCurrentLevel(UpgradeStat stat)
        {
            return currentLevels.TryGetValue(stat, out int level) ? level : 0;
        }

        public float GetCurrentEffectValue(UpgradeStat stat)
        {
            UpgradeData data = FindData(stat);
            UpgradeLevelEntry entry = data != null
                ? data.GetLevelEntry(GetCurrentLevel(stat))
                : null;
            return entry != null ? entry.EffectValue : 0f;
        }

        public bool TryPurchase(UpgradeStat stat)
        {
            UpgradeData data = FindData(stat);
            if (data == null)
            {
                return false;
            }

            int nextLevel = GetCurrentLevel(stat) + 1;
            UpgradeLevelEntry entry = data.GetLevelEntry(nextLevel);
            if (entry == null || nextLevel > data.MaxLevel)
            {
                return false;
            }

            if (economyService == null || !economyService.TrySpend(entry.Price))
            {
                return false;
            }

            currentLevels[stat] = nextLevel;
            UpgradePurchased?.Invoke(stat, nextLevel, entry.EffectValue);
            return true;
        }

        private UpgradeData FindData(UpgradeStat stat)
        {
            foreach (UpgradeData data in catalog)
            {
                if (data != null && data.Stat == stat)
                {
                    return data;
                }
            }

            return null;
        }
    }
}
