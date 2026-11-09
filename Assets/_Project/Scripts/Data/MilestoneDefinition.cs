using System.Globalization;
using UnityEngine;
using VoidProspector.Core;

namespace VoidProspector.Data
{
    public enum MilestoneGoal
    {
        /// <summary>Collect <c>target</c> chunks of <c>mineral</c> (stored or auto-sold), across all sectors.</summary>
        MineMineral,
        /// <summary>Break <c>target</c> meteors in <c>sector</c> (or anywhere if it's empty).</summary>
        BreakMeteors,
        /// <summary>Unlock depth <c>target</c> (as shown, starting at 1) in <c>sector</c>.</summary>
        ReachDepth,
    }

    /// <summary>A one-off goal ("Mine 1,000 Iron") with a credit reward and a permanent value bonus.</summary>
    [CreateAssetMenu(menuName = "Void Prospector/Milestone", fileName = "Milestone")]
    public class MilestoneDefinition : ScriptableObject
    {
        [Tooltip("Stable id used in saves; never change after release")]
        public string id;
        [Tooltip("Empty = generated from the goal")]
        public string displayName;

        [Header("Goal")]
        public MilestoneGoal goal;
        public MineralDefinition mineral;
        public SectorDefinition sector;
        [Min(1)] public long target = 100;

        [Header("Reward")]
        [Min(0)] public double rewardCredits;
        [Tooltip("Permanent value bonus (0.05 = +5%)")]
        [Min(0)] public float valueBonus;
        [Tooltip("Mineral the value bonus applies to; empty = all minerals")]
        public MineralDefinition bonusMineral;

        public string Title
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(displayName)) return displayName;
                string n = target.ToString("N0", CultureInfo.InvariantCulture);
                switch (goal)
                {
                    case MilestoneGoal.MineMineral: return $"Mine {n} {(mineral ? mineral.displayName : "?")}";
                    case MilestoneGoal.BreakMeteors: return sector ? $"Break {n} meteors in {sector.displayName}" : $"Break {n} meteors";
                    case MilestoneGoal.ReachDepth: return $"Reach depth {target} in {(sector ? sector.displayName : "?")}";
                    default: return id;
                }
            }
        }

        public string RewardText
        {
            get
            {
                string text = rewardCredits > 0 ? "$ " + NumberFormatter.Format(rewardCredits) : "";
                if (valueBonus > 0)
                {
                    string bonus = $"+{(valueBonus * 100).ToString("0", CultureInfo.InvariantCulture)}% {(bonusMineral ? bonusMineral.displayName : "all")} value";
                    text = text.Length > 0 ? text + ", " + bonus : bonus;
                }
                return text;
            }
        }
    }
}
