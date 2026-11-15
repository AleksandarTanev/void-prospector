using UnityEngine;

namespace VoidProspector.Data
{
    /// <summary>Feel and pacing numbers for the meteor field, chunks and juice. Balance numbers live in the other definitions.</summary>
    [CreateAssetMenu(menuName = "Void Prospector/Mining Settings", fileName = "MiningSettings")]
    public class MiningSettings : ScriptableObject
    {
        [Header("Meteor field")]
        [Min(1)] public int minMeteors = 6;
        [Min(1)] public int maxMeteors = 12;
        [Tooltip("World units per second (min, max)")]
        public Vector2 driftSpeed = new Vector2(0.25f, 0.8f);
        [Tooltip("Seconds between respawns while the field is below its target (min, max)")]
        public Vector2 respawnDelay = new Vector2(0.3f, 1f);

        [Header("Tapping")]
        [Tooltip("Extra forgiveness around a meteor's edge for taps (world units)")]
        [Min(0)] public float tapPadding = 0.25f;

        [Tooltip("Tap damage = drill damage × this (the GDD suggests a bonus for active play)")]
        [Min(0)] public float tapDamageMultiplier = 1f;
        [Tooltip("Damage multiplier of a critical hit")]
        [Min(1)] public float critMultiplier = 2f;

        [Header("Depth")]
        [Tooltip("Meteors to break at the deepest unlocked depth to unlock the next one")]
        [Min(1)] public int meteorsPerDepth = 15;

        [Header("Cracking")]
        [Tooltip("HP fractions at which the meteor cracks, highest first")]
        public float[] crackThresholds = { 0.66f, 0.33f };
        [Tooltip("Fraction of the meteor's chunks spilled at each crack")]
        [Range(0, 0.5f)] public float crackSpillFraction = 0.25f;
        [Tooltip("Meteors with a core: this many of the final chunks roll one purity grade higher")]
        [Min(0)] public int coreChunks = 3;

        [Header("Chunks")]
        [Tooltip("Burst speed when spilled (min, max)")]
        public Vector2 chunkBurstSpeed = new Vector2(2.5f, 5f);
        [Tooltip("Seconds a chunk flies freely before the collector can pull it")]
        [Min(0)] public float chunkSettleTime = 0.35f;
        [Tooltip("Speed of chunks inside the collector radius")]
        [Min(0)] public float magnetSpeed = 9f;
        [Tooltip("Speed at which chunks outside the collector radius drift toward the ship")]
        [Min(0)] public float driftToShip = 1.5f;
        [Tooltip("Seconds a chunk bounced off a full cargo hold takes to fade")]
        [Min(0.1f)] public float rejectedFadeTime = 1.2f;
        [Tooltip("Purity grade index at and above which a toast and sting play")]
        [Min(0)] public int rareGrade = 3;

        [Header("Drones (look only; balance is in GameDatabase.drones and the upgrades)")]
        [Tooltip("Distance of the drone orbit from the ship (world units)")]
        [Min(0.5f)] public float droneOrbitRadius = 1.4f;
        [Tooltip("Degrees per second")]
        public float droneOrbitSpeed = 50f;
        [Min(0.05f)] public float droneSize = 0.28f;

        [Header("Juice")]
        [Min(0)] public float hitShake = 0.05f;
        [Tooltip("Multiplied by the meteor radius")]
        [Min(0)] public float crackShake = 0.25f;
        [Tooltip("Multiplied by the meteor radius")]
        [Min(0)] public float breakShake = 0.5f;
        [Tooltip("Seconds of slow motion when the top purity grade drops")]
        [Min(0)] public float topGradeHitStop = 0.25f;

        [Header("Travel (look only: the ship flies forward through the belt)")]
        [Tooltip("Background star layers, far to near. Each scrolls down at its own speed (parallax).")]
        public StarLayer[] starLayers =
        {
            new StarLayer { count = 45, size = new Vector2(0.02f, 0.05f), alpha = new Vector2(0.15f, 0.35f), speed = 0.12f, stretch = 1f },
            new StarLayer { count = 30, size = new Vector2(0.04f, 0.08f), alpha = new Vector2(0.3f, 0.55f), speed = 0.45f, stretch = 1f },
            new StarLayer { count = 10, size = new Vector2(0.025f, 0.04f), alpha = new Vector2(0.15f, 0.3f), speed = 2.2f, stretch = 10f },
        };
        [Tooltip("Downward drift of meteors, world units per second (min, max)")]
        public Vector2 meteorFallSpeed = new Vector2(0.1f, 0.35f);
        [Tooltip("Chance a new meteor enters from the top instead of a side")]
        [Range(0, 1)] public float meteorTopEntryChance = 0.5f;
        [Tooltip("Engine trail particles per second, per engine")]
        [Min(0)] public float trailRate = 28f;
        [Tooltip("Speed the trail streams away from the engines")]
        [Min(0)] public float trailSpeed = 2.5f;
        [Min(0.01f)] public float trailSize = 0.12f;
        [Tooltip("Trail particle lifetime in seconds (min, max)")]
        public Vector2 trailLife = new Vector2(0.25f, 0.45f);
        public Color trailColor = new Color(1f, 0.6f, 0.25f, 0.8f);

        void OnValidate()
        {
            if (maxMeteors < minMeteors) maxMeteors = minMeteors;
            if (driftSpeed.y < driftSpeed.x) driftSpeed.y = driftSpeed.x;
            if (respawnDelay.y < respawnDelay.x) respawnDelay.y = respawnDelay.x;
            if (chunkBurstSpeed.y < chunkBurstSpeed.x) chunkBurstSpeed.y = chunkBurstSpeed.x;
            if (meteorFallSpeed.y < meteorFallSpeed.x) meteorFallSpeed.y = meteorFallSpeed.x;
            if (trailLife.y < trailLife.x) trailLife.y = trailLife.x;
        }
    }

    /// <summary>One parallax layer of the scrolling starfield.</summary>
    [System.Serializable]
    public class StarLayer
    {
        [Min(0)] public int count = 30;
        [Tooltip("Star diameter (streak width when stretched), world units (min, max)")]
        public Vector2 size = new Vector2(0.03f, 0.06f);
        public Vector2 alpha = new Vector2(0.2f, 0.5f);
        [Tooltip("Scroll speed, world units per second")]
        [Min(0)] public float speed = 0.3f;
        [Tooltip("Length ÷ width; above 1 draws streaks")]
        [Min(1)] public float stretch = 1f;
    }
}
