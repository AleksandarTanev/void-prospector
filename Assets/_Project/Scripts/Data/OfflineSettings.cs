using System;
using UnityEngine;

namespace VoidProspector.Data
{
    /// <summary>Rules for earnings while the game isn't running. Part of <see cref="GameDatabase"/>.</summary>
    [Serializable]
    public class OfflineSettings
    {
        [Tooltip("Longest time away that pays out (GDD: 8 hours at first; an upgrade raises it later)")]
        [Min(0)] public float capHours = 8f;

        [Tooltip("Shorter absences pay nothing and show no popup")]
        [Min(0)] public float minAwaySeconds = 60f;

        [Tooltip("Share of the estimated idle income paid while away (1 = all of it)")]
        [Min(0)] public float efficiency = 1f;

        [Tooltip("Seconds of idle play simulated to estimate income per second")]
        [Min(1)] public float incomeSampleSeconds = 600f;

        public double CapSeconds => capHours * 3600.0;
    }
}
