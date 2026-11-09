using System;
using UnityEngine;
using VoidProspector.Core;

namespace VoidProspector.Data
{
    [Serializable]
    public class MineralWeight
    {
        public MineralDefinition mineral;
        [Min(0)] public float weight = 1;
    }

    /// <summary>A meteor's composition: which minerals it contains and how often.</summary>
    [CreateAssetMenu(menuName = "Void Prospector/Meteor Type", fileName = "MeteorType")]
    public class MeteorTypeDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        [Min(0)] public float hpMultiplier = 1;
        [Tooltip("Body colour, so types are easy to tell apart (greybox)")]
        public Color bodyColor = new Color(0.4f, 0.38f, 0.36f);
        public MineralWeight[] composition = Array.Empty<MineralWeight>();

        public MineralDefinition RollMineral(System.Random rng)
        {
            var pick = WeightedRandom.Pick(composition, c => c.mineral != null ? c.weight : 0f, rng);
            return pick?.mineral;
        }

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName)) displayName = name;
        }
    }
}
