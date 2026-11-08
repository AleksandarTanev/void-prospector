using System;
using UnityEngine;
using VoidProspector.Core;

namespace VoidProspector.Data
{
    [Serializable]
    public class MeteorTypeWeight
    {
        public MeteorTypeDefinition type;
        [Min(0)] public float weight = 1;
    }

    /// <summary>An amount of credits (mineral = null) or of a specific mineral.</summary>
    [Serializable]
    public class ResourceCost
    {
        [Tooltip("Leave empty to pay in credits")]
        public MineralDefinition mineral;
        [Min(0)] public double amount;
    }

    [CreateAssetMenu(menuName = "Void Prospector/Sector", fileName = "Sector")]
    public class SectorDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        [Tooltip("Order on the star map, starting at 1")]
        [Min(1)] public int index = 1;
        [Min(0)] public double baseHp = 10;
        [Min(1)] public int maxDepth = 10;
        public MeteorTypeWeight[] meteorTypes = Array.Empty<MeteorTypeWeight>();
        [Tooltip("The mineral the jump drive to the next sector is paid with")]
        public MineralDefinition signatureMineral;
        [Tooltip("Cost to unlock this sector; empty for the starting sector")]
        public ResourceCost[] unlockCost = Array.Empty<ResourceCost>();

        [Header("Depth scaling")]
        [Tooltip("HP is multiplied by this per depth level")]
        [Min(1)] public double depthHpGrowth = 1.18;
        [Tooltip("Value gains this fraction per depth level (0.15 = +15%)")]
        [Min(0)] public double depthValueStep = 0.15;

        public MeteorTypeDefinition RollMeteorType(System.Random rng)
        {
            var pick = WeightedRandom.Pick(meteorTypes, t => t.type != null ? t.weight : 0f, rng);
            return pick?.type;
        }

        public GameNumber MeteorHp(MeteorSizeDefinition size, MeteorTypeDefinition type, int depth) =>
            Formulas.MeteorHp(baseHp, depth, size.hpMultiplier, type.hpMultiplier, depthHpGrowth);

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName)) displayName = name;
        }
    }
}
