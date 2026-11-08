using UnityEngine;
using VoidProspector.Core;

namespace VoidProspector.Data
{
    [CreateAssetMenu(menuName = "Void Prospector/Mineral", fileName = "Mineral")]
    public class MineralDefinition : ScriptableObject
    {
        [Tooltip("Stable id used in saves; never change after release")]
        public string id;
        public string displayName;
        [Min(0)] public int tier;
        [Tooltip("Sell value of one Crude chunk at depth 0, in credits")]
        [Min(0)] public double baseValue = 1;
        public Color color = Color.gray;
        public Sprite icon;

        public GameNumber BaseValue => baseValue;

        void OnValidate()
        {
            if (baseValue < 0) baseValue = 0;
            if (string.IsNullOrWhiteSpace(displayName)) displayName = name;
        }
    }
}
