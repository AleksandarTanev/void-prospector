using System;
using UnityEngine;
using VoidProspector.Core;

namespace VoidProspector.Data
{
    [Serializable]
    public class PurityGrade
    {
        public string name;
        [Range(0, 100)] public int minPurity;
        [Range(0, 100)] public int maxPurity;
        [Min(0)] public double valueMultiplier = 1;
        [Tooltip("Relative chance of dropping at this grade")]
        [Min(0)] public float dropWeight;
        [Tooltip("0 = no glow, 1+ = strong glow")]
        [Min(0)] public float glow;
        public Color tint = Color.white;
    }

    /// <summary>Purity grades from lowest to highest. A chunk's grade index points into this table.</summary>
    [CreateAssetMenu(menuName = "Void Prospector/Purity Table", fileName = "PurityTable")]
    public class PurityTable : ScriptableObject
    {
        public PurityGrade[] grades = Array.Empty<PurityGrade>();

        public int Count => grades.Length;
        public int TopGrade => grades.Length - 1;
        public PurityGrade this[int index] => grades[index];

        /// <summary>Rolls a grade index; bonusGrades shifts the result up (capped at the top grade).</summary>
        public int Roll(System.Random rng, int bonusGrades = 0)
        {
            int i = WeightedRandom.PickIndex(grades, g => g.dropWeight, rng);
            if (i < 0) return 0;
            return Mathf.Clamp(i + bonusGrades, 0, TopGrade);
        }

        void OnValidate()
        {
            if (grades == null) return;
            foreach (var g in grades)
            {
                if (g == null) continue;
                if (g.maxPurity < g.minPurity) g.maxPurity = g.minPurity;
                if (g.dropWeight < 0) g.dropWeight = 0;
            }
        }
    }
}
