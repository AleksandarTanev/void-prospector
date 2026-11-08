using System;
using VoidProspector.Core;
using VoidProspector.Data;

namespace VoidProspector.Economy
{
    public enum BuyMode { One, Ten, Max }

    /// <summary>What a purchase would buy: how many levels and for how much. Count 0 = nothing to buy.</summary>
    public readonly struct UpgradeQuote
    {
        public readonly int count;
        public readonly GameNumber cost;
        public readonly bool affordable;

        public UpgradeQuote(int count, GameNumber cost, bool affordable)
        {
            this.count = count;
            this.cost = cost;
            this.affordable = affordable;
        }
    }

    /// <summary>Runtime state of one upgrade: its level, the cost of the next levels and its current effect.</summary>
    public sealed class UpgradeTrack
    {
        public UpgradeDefinition Definition { get; }
        public int Level { get; private set; }

        public UpgradeTrack(UpgradeDefinition definition, int level = 0)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Level = Math.Max(0, Math.Min(level, definition.maxLevel));
        }

        public UpgradeEffect Effect => Definition.effect;
        public bool IsMaxed => Definition.IsMaxed(Level);
        public double Value => Definition.EffectAt(Level);
        public double NextValue => Definition.EffectAt(Math.Min(Level + 1, Definition.maxLevel));
        public GameNumber NextCost => Definition.CostAt(Level);
        public int LevelsLeft => Math.Max(0, Definition.maxLevel - Level);

        /// <summary>Total cost of the next <paramref name="count"/> levels.</summary>
        public GameNumber CostFor(int count)
        {
            var total = GameNumber.Zero;
            count = Math.Min(count, LevelsLeft);
            for (int i = 0; i < count; i++) total += Definition.CostAt(Level + i);
            return total;
        }

        /// <summary>
        /// ×1 and ×10 quote a fixed number of levels (capped at max level) and are affordable only if all of them are.
        /// Max quotes as many levels as the credits cover, or one level (unaffordable) if none.
        /// </summary>
        public UpgradeQuote Quote(BuyMode mode, GameNumber credits)
        {
            if (IsMaxed) return new UpgradeQuote(0, GameNumber.Zero, false);

            if (mode == BuyMode.Max)
            {
                int n = 0;
                var total = GameNumber.Zero;
                while (n < LevelsLeft)
                {
                    var next = total + Definition.CostAt(Level + n);
                    if (next > credits) break;
                    total = next;
                    n++;
                }
                return n > 0 ? new UpgradeQuote(n, total, true) : new UpgradeQuote(1, NextCost, false);
            }

            int count = Math.Min(mode == BuyMode.Ten ? 10 : 1, LevelsLeft);
            var cost = CostFor(count);
            return new UpgradeQuote(count, cost, credits >= cost);
        }

        /// <summary>Raises the level. Payment is handled by <see cref="Upgrades"/>.</summary>
        internal void AddLevels(int count) => Level = Math.Min(Definition.maxLevel, Level + Math.Max(0, count));

        internal void SetLevel(int level) => Level = Math.Max(0, Math.Min(level, Definition.maxLevel));
    }
}
