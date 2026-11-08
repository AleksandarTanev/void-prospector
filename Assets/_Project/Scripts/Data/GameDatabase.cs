using System;
using System.Collections.Generic;
using UnityEngine;
using VoidProspector.Core;

namespace VoidProspector.Data
{
    /// <summary>The one asset that references all game data. Systems get their definitions from here.</summary>
    [CreateAssetMenu(menuName = "Void Prospector/Game Database", fileName = "GameDatabase")]
    public class GameDatabase : ScriptableObject
    {
        public MineralDefinition[] minerals = Array.Empty<MineralDefinition>();
        public PurityTable purity;
        public MeteorSizeDefinition[] meteorSizes = Array.Empty<MeteorSizeDefinition>();
        public MeteorTypeDefinition[] meteorTypes = Array.Empty<MeteorTypeDefinition>();
        public SectorDefinition[] sectors = Array.Empty<SectorDefinition>();
        public UpgradeDefinition[] upgrades = Array.Empty<UpgradeDefinition>();

        public MineralDefinition GetMineral(string id) => Find(minerals, id);
        public MeteorSizeDefinition GetMeteorSize(string id) => Find(meteorSizes, id);
        public MeteorTypeDefinition GetMeteorType(string id) => Find(meteorTypes, id);
        public SectorDefinition GetSector(string id) => Find(sectors, id);
        public UpgradeDefinition GetUpgrade(string id) => Find(upgrades, id);

        public UpgradeDefinition GetUpgrade(UpgradeEffect effect)
        {
            foreach (var u in upgrades)
                if (u != null && u.effect == effect) return u;
            return null;
        }

        public MeteorSizeDefinition RollMeteorSize(System.Random rng) =>
            WeightedRandom.Pick(meteorSizes, s => s != null ? s.spawnWeight : 0f, rng);

        static T Find<T>(T[] items, string id) where T : ScriptableObject
        {
            foreach (var item in items)
                if (item != null && IdOf(item) == id) return item;
            return null;
        }

        static string IdOf(ScriptableObject so) => so switch
        {
            MineralDefinition m => m.id,
            MeteorSizeDefinition s => s.id,
            MeteorTypeDefinition t => t.id,
            SectorDefinition s => s.id,
            UpgradeDefinition u => u.id,
            _ => so.name,
        };

        // ---------- Validation ----------

        /// <summary>Checks the data for mistakes. Returns true when there are no errors.</summary>
        public bool Validate(List<string> errors)
        {
            int before = errors.Count;

            CheckList(minerals, "Minerals", errors);
            CheckList(meteorSizes, "Meteor sizes", errors);
            CheckList(meteorTypes, "Meteor types", errors);
            CheckList(sectors, "Sectors", errors);
            CheckList(upgrades, "Upgrades", errors);

            ValidatePurity(errors);

            foreach (var s in meteorSizes)
            {
                if (s == null) continue;
                if (s.chunkMax < s.chunkMin) errors.Add($"Meteor size '{s.id}': chunkMax < chunkMin");
                if (s.hpMultiplier <= 0) errors.Add($"Meteor size '{s.id}': hpMultiplier must be > 0");
            }
            if (!HasPositiveWeight(meteorSizes, s => s.spawnWeight)) errors.Add("Meteor sizes: no size has a positive spawn weight");

            foreach (var t in meteorTypes)
            {
                if (t == null) continue;
                if (t.composition.Length == 0) errors.Add($"Meteor type '{t.id}': empty composition");
                foreach (var c in t.composition)
                    if (c == null || c.mineral == null) errors.Add($"Meteor type '{t.id}': composition entry without a mineral");
                if (!HasPositiveWeight(t.composition, c => c.mineral != null ? c.weight : 0)) errors.Add($"Meteor type '{t.id}': no mineral has a positive weight");
            }

            var sectorIndexes = new HashSet<int>();
            foreach (var s in sectors)
            {
                if (s == null) continue;
                if (!sectorIndexes.Add(s.index)) errors.Add($"Sector '{s.id}': duplicate index {s.index}");
                if (s.meteorTypes.Length == 0) errors.Add($"Sector '{s.id}': no meteor types");
                foreach (var w in s.meteorTypes)
                    if (w == null || w.type == null) errors.Add($"Sector '{s.id}': meteor type entry without a type");
                if (!HasPositiveWeight(s.meteorTypes, w => w.type != null ? w.weight : 0)) errors.Add($"Sector '{s.id}': no meteor type has a positive weight");
                if (s.baseHp <= 0) errors.Add($"Sector '{s.id}': baseHp must be > 0");
            }

            var effects = new HashSet<UpgradeEffect>();
            foreach (var u in upgrades)
            {
                if (u == null) continue;
                if (!effects.Add(u.effect)) errors.Add($"Upgrade '{u.id}': another upgrade already has effect {u.effect}");
                if (u.baseCost <= 0) errors.Add($"Upgrade '{u.id}': baseCost must be > 0");
                if (u.mode == EffectMode.Multiply && u.perLevel <= 0) errors.Add($"Upgrade '{u.id}': Multiply mode needs perLevel > 0");
            }

            return errors.Count == before;
        }

        void ValidatePurity(List<string> errors)
        {
            if (purity == null) { errors.Add("Purity table is missing"); return; }
            var g = purity.grades;
            if (g.Length == 0) { errors.Add("Purity table has no grades"); return; }
            if (g[0].minPurity != 0) errors.Add("Purity: the lowest grade must start at 0%");
            if (g[g.Length - 1].maxPurity != 100) errors.Add("Purity: the highest grade must end at 100%");
            for (int i = 0; i < g.Length; i++)
            {
                if (g[i].maxPurity < g[i].minPurity) errors.Add($"Purity '{g[i].name}': max < min");
                if (i > 0)
                {
                    if (g[i].minPurity != g[i - 1].maxPurity + 1) errors.Add($"Purity '{g[i].name}': range must start right after '{g[i - 1].name}'");
                    if (g[i].valueMultiplier < g[i - 1].valueMultiplier) errors.Add($"Purity '{g[i].name}': multiplier lower than the grade below");
                }
            }
            if (!HasPositiveWeight(g, x => x.dropWeight)) errors.Add("Purity: no grade has a positive drop weight");
        }

        static void CheckList<T>(T[] items, string label, List<string> errors) where T : ScriptableObject
        {
            var ids = new HashSet<string>();
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (item == null) { errors.Add($"{label}: entry {i} is empty"); continue; }
                string id = IdOf(item);
                if (string.IsNullOrWhiteSpace(id)) errors.Add($"{label}: '{item.name}' has no id");
                else if (!ids.Add(id)) errors.Add($"{label}: duplicate id '{id}'");
            }
        }

        static bool HasPositiveWeight<T>(IEnumerable<T> items, Func<T, float> weight)
        {
            foreach (var item in items)
                if (item != null && weight(item) > 0) return true;
            return false;
        }
    }
}
