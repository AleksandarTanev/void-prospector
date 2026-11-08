using System;

namespace VoidProspector.Core
{
    public enum EffectMode
    {
        /// <summary>value = base + perLevel × level</summary>
        Add,
        /// <summary>value = base × perLevel ^ level</summary>
        Multiply,
    }

    /// <summary>The economy formulas from the GDD. Pure functions, covered by EditMode tests.</summary>
    public static class Formulas
    {
        /// <summary>cost(L) = ceil(base × growth^L)</summary>
        public static GameNumber UpgradeCost(GameNumber baseCost, double growth, int level)
        {
            if (level < 0) throw new ArgumentOutOfRangeException(nameof(level));
            return GameNumber.Ceil(baseCost * Math.Pow(growth, level));
        }

        /// <summary>value = mineralBase × purityMult × (1 + depthValueStep × depth) × codexBonus × prestigeMult</summary>
        public static GameNumber ChunkValue(GameNumber mineralBase, double purityMultiplier, int depth,
            double depthValueStep = 0.15, double codexBonus = 1, double prestigeMultiplier = 1)
        {
            return mineralBase * purityMultiplier * (1 + depthValueStep * depth) * codexBonus * prestigeMultiplier;
        }

        /// <summary>HP = sectorBaseHp × depthHpGrowth^depth × sizeMult × typeMult</summary>
        public static GameNumber MeteorHp(GameNumber sectorBaseHp, int depth, double sizeMultiplier,
            double typeMultiplier, double depthHpGrowth = 1.18)
        {
            return sectorBaseHp * Math.Pow(depthHpGrowth, depth) * sizeMultiplier * typeMultiplier;
        }

        /// <summary>Stardust = floor(√(runCredits / 1,000,000)) + voidstoneBonus</summary>
        public static GameNumber Stardust(GameNumber runCredits, GameNumber voidstoneBonus)
        {
            if (runCredits <= GameNumber.Zero) return voidstoneBonus;
            return GameNumber.Floor(GameNumber.Sqrt(runCredits / 1_000_000d)) + voidstoneBonus;
        }

        /// <summary>Upgrade effect at a level: Add → base + per × L, Multiply → base × per^L.</summary>
        public static double UpgradeEffect(double baseValue, double perLevel, int level, EffectMode mode)
        {
            if (level < 0) throw new ArgumentOutOfRangeException(nameof(level));
            return mode == EffectMode.Add
                ? baseValue + perLevel * level
                : baseValue * Math.Pow(perLevel, level);
        }
    }
}
