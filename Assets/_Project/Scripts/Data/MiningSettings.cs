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

        [Header("Juice")]
        [Min(0)] public float hitShake = 0.05f;
        [Tooltip("Multiplied by the meteor radius")]
        [Min(0)] public float crackShake = 0.25f;
        [Tooltip("Multiplied by the meteor radius")]
        [Min(0)] public float breakShake = 0.5f;
        [Tooltip("Seconds of slow motion when the top purity grade drops")]
        [Min(0)] public float topGradeHitStop = 0.25f;

        void OnValidate()
        {
            if (maxMeteors < minMeteors) maxMeteors = minMeteors;
            if (driftSpeed.y < driftSpeed.x) driftSpeed.y = driftSpeed.x;
            if (respawnDelay.y < respawnDelay.x) respawnDelay.y = respawnDelay.x;
            if (chunkBurstSpeed.y < chunkBurstSpeed.x) chunkBurstSpeed.y = chunkBurstSpeed.x;
        }
    }
}
