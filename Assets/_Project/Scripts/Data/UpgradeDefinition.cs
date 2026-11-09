using UnityEngine;
using VoidProspector.Core;

namespace VoidProspector.Data
{
    /// <summary>What an upgrade changes. Systems look up the upgrade by this, not by id.</summary>
    public enum UpgradeEffect
    {
        DrillDamage,
        AutoFireInterval,
        CritChance,
        CollectorRadius,
        CargoCapacity,
        DroneCount,
        DroneDamage,
        RefinerySpeed,
        RefineSuccess,
        ScannerRange,
        OfflineCap,
        /// <summary>Which meteor drones pick: 0 random, 1 nearest, 2 weakest, 3 richest (see DroneTargeting).</summary>
        DroneTargeting,
        /// <summary>Chunks per refinery batch.</summary>
        RefineryBatch,
        /// <summary>Share of the failure loss prevented (1 = refining never loses chunks).</summary>
        RefineryStabiliser,
    }

    [CreateAssetMenu(menuName = "Void Prospector/Upgrade", fileName = "Upgrade")]
    public class UpgradeDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;

        [Header("Effect")]
        public UpgradeEffect effect;
        public EffectMode mode = EffectMode.Add;
        public double baseValue;
        public double perLevel = 1;
        [Tooltip("Level 0 means the feature is off (e.g. auto-fire); effects start counting from level 1")]
        public bool offAtLevelZero;
        [Tooltip("Shown and buyable once the sector with this index is unlocked (1 = from the start)")]
        [Min(1)] public int requiresSector = 1;

        [Header("Cost")]
        [Min(0)] public double baseCost = 10;
        [Min(1)] public double growth = 1.15;
        [Min(1)] public int maxLevel = 50;
        [Tooltip("Leave empty to pay in credits")]
        public MineralDefinition costMineral;

        public bool IsMaxed(int level) => level >= maxLevel;

        /// <summary>Cost to buy the next level when currently at <paramref name="level"/>.</summary>
        public GameNumber CostAt(int level) => Formulas.UpgradeCost(baseCost, growth, level);

        /// <summary>Effect value at a level. Returns 0 at level 0 when offAtLevelZero is set.</summary>
        public double EffectAt(int level)
        {
            if (offAtLevelZero)
            {
                if (level <= 0) return 0;
                return Formulas.UpgradeEffect(baseValue, perLevel, level - 1, mode);
            }
            return Formulas.UpgradeEffect(baseValue, perLevel, level, mode);
        }

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName)) displayName = name;
        }
    }
}
