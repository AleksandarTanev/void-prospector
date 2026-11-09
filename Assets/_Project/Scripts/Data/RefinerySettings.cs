using System;
using UnityEngine;

namespace VoidProspector.Data
{
    /// <summary>One refining step: the cycle that tries to raise a batch to the next grade.</summary>
    [Serializable]
    public class RefineStage
    {
        [Tooltip("Seconds per attempt at refinery speed ×1")]
        [Min(0.1f)] public float seconds = 15f;
        [Tooltip("Chance that an attempt raises the grade")]
        [Range(0, 1)] public float successChance = 0.8f;
        [Tooltip("Share of the batch's chunks lost when an attempt fails (the Stabiliser reduces it)")]
        [Range(0, 1)] public float failLoss = 0.1f;
    }

    /// <summary>Balance rules for the refinery. Part of <see cref="GameDatabase"/>; speed, success, batch size and the Stabiliser are upgrades.</summary>
    [Serializable]
    public class RefinerySettings
    {
        [Tooltip("The refinery appears once the sector with this index is unlocked")]
        [Min(1)] public int unlockSector = 3;

        [Tooltip("Index 0 = Crude → Standard, 1 = Standard → Refined, ... One entry per grade above the lowest")]
        public RefineStage[] stages =
        {
            new RefineStage { seconds = 15, successChance = 0.85f, failLoss = 0.10f },
            new RefineStage { seconds = 30, successChance = 0.65f, failLoss = 0.15f },
            new RefineStage { seconds = 60, successChance = 0.40f, failLoss = 0.25f },
            new RefineStage { seconds = 120, successChance = 0.15f, failLoss = 0.50f },
        };

        [Tooltip("Success chance never goes above this, whatever the upgrades")]
        [Range(0, 1)] public float maxSuccessChance = 0.95f;
    }

    /// <summary>The Mineral Codex: the value bonus a mineral gets for each purity grade found.</summary>
    [Serializable]
    public class CodexSettings
    {
        [Tooltip("Value bonus per grade entry, lowest grade first (0.01 = +1% to that mineral)")]
        public float[] bonusPerGrade = { 0.01f, 0.02f, 0.03f, 0.04f, 0.05f };
    }

    /// <summary>When the scanner calls a meteor rare.</summary>
    [Serializable]
    public class ScannerSettings
    {
        [Tooltip("Minerals of this tier or higher count as valuable")]
        [Min(0)] public int rareMinTier = 3;
        [Tooltip("A meteor with at least this many valuable chunks is rare")]
        [Min(1)] public int rareMinChunks = 4;
    }
}
