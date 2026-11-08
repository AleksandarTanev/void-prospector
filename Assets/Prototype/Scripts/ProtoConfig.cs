using System;
using UnityEngine;

namespace VoidProspector.Prototype
{
    [Serializable]
    public class MineralDef
    {
        public string name;
        public double value;
        public float weight;
        public Color color;
    }

    [Serializable]
    public class GradeDef
    {
        public string name;
        public double multiplier;
        public float chance;
        [Tooltip("0 = no glow, 1 = strong glow")] public float glow;
    }

    [Serializable]
    public class MeteorSizeDef
    {
        public string name;
        public float hp;
        public int chunks;
        public float radius;
        public float weight;
    }

    [Serializable]
    public class UpgradeDef
    {
        public string name;
        public double baseCost;
        public float growth = 1.15f;
        public int maxLevel = 50;

        public double CostAt(int level) => Math.Ceiling(baseCost * Math.Pow(growth, level));
    }

    public enum UpgradeId { Drill, AutoFire, Collector, Drone }

    /// <summary>Every tuning number in the prototype. Edit live in the Inspector during Play mode.</summary>
    [Serializable]
    public class ProtoConfig
    {
        [Header("Minerals")]
        public MineralDef[] minerals =
        {
            new MineralDef { name = "Rock",   value = 1,  weight = 60, color = new Color(0.55f, 0.52f, 0.50f) },
            new MineralDef { name = "Copper", value = 5,  weight = 25, color = new Color(0.90f, 0.50f, 0.25f) },
            new MineralDef { name = "Tin",    value = 6,  weight = 10, color = new Color(0.70f, 0.80f, 0.85f) },
            new MineralDef { name = "Iron",   value = 25, weight = 5,  color = new Color(0.75f, 0.30f, 0.25f) },
        };

        [Header("Purity grades")]
        public GradeDef[] grades =
        {
            new GradeDef { name = "Crude",    multiplier = 1,  chance = 60f,  glow = 0f },
            new GradeDef { name = "Standard", multiplier = 2,  chance = 28f,  glow = 0.15f },
            new GradeDef { name = "Refined",  multiplier = 5,  chance = 9f,   glow = 0.45f },
            new GradeDef { name = "Pristine", multiplier = 15, chance = 2.7f, glow = 0.8f },
            new GradeDef { name = "Flawless", multiplier = 50, chance = 0.3f, glow = 1.2f },
        };
        [Tooltip("Grade index at or above which a toast + sting plays")] public int rareGrade = 3;

        [Header("Meteors")]
        public MeteorSizeDef[] sizes =
        {
            new MeteorSizeDef { name = "Small",  hp = 10, chunks = 3,  radius = 0.45f, weight = 55 },
            new MeteorSizeDef { name = "Medium", hp = 30, chunks = 8,  radius = 0.75f, weight = 33 },
            new MeteorSizeDef { name = "Large",  hp = 80, chunks = 15, radius = 1.15f, weight = 12 },
        };
        public int minMeteors = 6;
        public int maxMeteors = 10;
        public Vector2 driftSpeed = new Vector2(0.25f, 0.8f);
        [Tooltip("Large meteors: this many final chunks roll one grade higher")] public int coreBonusChunks = 3;

        [Header("Tapping")]
        public float tapDamageBase = 1f;
        public float tapDamagePerLevel = 1f;
        [Tooltip("Extra forgiveness around a meteor's edge for taps (world units)")] public float tapPadding = 0.25f;

        [Header("Cargo and collection")]
        public int cargoCapacity = 50;
        public float collectorRadiusBase = 4.5f;
        public float collectorRadiusPerLevel = 0.6f;
        public float magnetSpeed = 9f;
        [Tooltip("Speed at which chunks outside the collector radius drift toward the ship")] public float chunkDriftToShip = 1.5f;
        public float chunkLifetime = 12f;

        [Header("Auto-fire and drones")]
        public float autoFireIntervalBase = 1.2f;
        [Tooltip("Interval is multiplied by this per level after the first")] public float autoFireIntervalPerLevel = 0.88f;
        public float droneInterval = 1.0f;
        [Tooltip("Drone damage as a fraction of drill damage")] public float droneDamageFactor = 0.6f;
        public float droneOrbitRadius = 1.0f;
        [Tooltip("Drones orbit this far above the ship, so they stay clear of the bottom UI")] public float droneOrbitHeight = 1.3f;

        [Header("Upgrades")]
        public UpgradeDef drill     = new UpgradeDef { name = "Drill",     baseCost = 10 };
        public UpgradeDef autoFire  = new UpgradeDef { name = "Auto-fire", baseCost = 50 };
        public UpgradeDef collector = new UpgradeDef { name = "Collector", baseCost = 25, maxLevel = 15 };
        public UpgradeDef drone     = new UpgradeDef { name = "Drone",     baseCost = 200, growth = 1.6f, maxLevel = 8 };

        [Header("Refine")]
        [Tooltip("Chance a Crude chunk goes up one grade")] public float refineUpChance = 0.5f;
        [Tooltip("Up-chance is multiplied by this for each grade above Crude")] public float refineFalloff = 0.6f;
        public float refineLossChance = 0.1f;
        [Tooltip("Seconds before Refine can be used again")] public float refineCooldown = 4f;

        public UpgradeDef Upgrade(UpgradeId id) => id switch
        {
            UpgradeId.Drill => drill,
            UpgradeId.AutoFire => autoFire,
            UpgradeId.Collector => collector,
            _ => drone,
        };
    }
}
