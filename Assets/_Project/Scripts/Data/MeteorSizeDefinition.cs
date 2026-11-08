using UnityEngine;

namespace VoidProspector.Data
{
    [CreateAssetMenu(menuName = "Void Prospector/Meteor Size", fileName = "MeteorSize")]
    public class MeteorSizeDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        [Min(0)] public float hpMultiplier = 1;
        [Min(0)] public int chunkMin = 1;
        [Min(0)] public int chunkMax = 1;
        [Tooltip("World units")]
        [Min(0.05f)] public float radius = 0.5f;
        [Min(0)] public float spawnWeight = 1;
        [Tooltip("Large meteors: the last chunks from the core roll one purity grade higher")]
        public bool hasCore;

        /// <summary>Chunk count between chunkMin and chunkMax inclusive.</summary>
        public int RollChunks(System.Random rng) => rng.Next(chunkMin, chunkMax + 1);

        void OnValidate()
        {
            if (chunkMax < chunkMin) chunkMax = chunkMin;
            if (string.IsNullOrWhiteSpace(displayName)) displayName = name;
        }
    }
}
