using System;
using System.Collections.Generic;

namespace VoidProspector.Drones
{
    /// <summary>How drones pick a meteor. The Drone Targeting upgrade level is the rule's number.</summary>
    public enum TargetRule
    {
        Random = 0,
        /// <summary>Closest to the drone (less wasted travel, steady hits).</summary>
        Nearest = 1,
        /// <summary>Least HP left: finishes meteors off, so chunks come sooner.</summary>
        Weakest = 2,
        /// <summary>Most valuable contents (the minerals the veins hint at).</summary>
        Richest = 3,
    }

    /// <summary>A meteor as drones see it.</summary>
    public readonly struct TargetInfo
    {
        public readonly float x, y;
        public readonly double hp;
        public readonly double value;

        public TargetInfo(float x, float y, double hp, double value)
        {
            this.x = x;
            this.y = y;
            this.hp = hp;
            this.value = value;
        }
    }

    public static class DroneTargeting
    {
        public static TargetRule RuleForLevel(double level) =>
            (TargetRule)Math.Max(0, Math.Min((int)Math.Floor(level), (int)TargetRule.Richest));

        public static string Describe(TargetRule rule) => rule switch
        {
            TargetRule.Random => "random",
            TargetRule.Nearest => "nearest",
            TargetRule.Weakest => "weakest",
            TargetRule.Richest => "richest",
            _ => rule.ToString(),
        };

        /// <summary>Index of the meteor to shoot, or -1 if there are none. Ties go to the nearer meteor.</summary>
        public static int Pick(IReadOnlyList<TargetInfo> targets, float fromX, float fromY, TargetRule rule, Random rng)
        {
            if (targets == null || targets.Count == 0) return -1;
            if (rule == TargetRule.Random) return rng.Next(targets.Count);

            int best = -1;
            double bestScore = double.MaxValue, bestDist = double.MaxValue;
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                double dx = t.x - fromX, dy = t.y - fromY;
                double dist = dx * dx + dy * dy;
                double score = rule switch
                {
                    TargetRule.Nearest => dist,
                    TargetRule.Weakest => t.hp,
                    TargetRule.Richest => -t.value,
                    _ => dist,
                };
                if (score < bestScore || (score == bestScore && dist < bestDist))
                {
                    best = i;
                    bestScore = score;
                    bestDist = dist;
                }
            }
            return best;
        }
    }
}
