using System;
using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.Prototype.Delivery
{
    public enum UpgradeStat
    {
        Stamina,
        Balance,
        Agility
    }

    [Serializable]
    public sealed class UpgradeLevelEntry
    {
        [SerializeField, Min(1)] private int level = 1;
        [SerializeField, Min(0)] private int price;
        [SerializeField] private float effectValue;

        public int Level => level;
        public int Price => price;
        public float EffectValue => effectValue;
    }

    [CreateAssetMenu(
        fileName = "UpgradeData_",
        menuName = "SubwayCarry/Delivery/Upgrade Data")]

    public sealed class UpgradeData : ScriptableObject
    {
        [SerializeField] private UpgradeStat stat;
        [SerializeField] private List<UpgradeLevelEntry> levels = new List<UpgradeLevelEntry>();

        public UpgradeStat Stat => stat;
        public int MaxLevel => levels.Count;

        public UpgradeLevelEntry GetLevelEntry(int level)
        {
            foreach (UpgradeLevelEntry entry in levels)
            {
                if (entry != null && entry.Level == level)
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
