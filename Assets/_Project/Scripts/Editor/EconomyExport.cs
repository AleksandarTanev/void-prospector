using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using VoidProspector.Data;
using VoidProspector.Economy;

namespace VoidProspector.EditorTools
{
    /// <summary>
    /// Exports the economy data and three 30-minute simulations to docs/economy/data as CSV.
    /// Then run tools/economy/build_economy_xlsx.py to rebuild docs/economy/Economy.xlsx.
    /// </summary>
    public static class EconomyExport
    {
        const string DatabasePath = "Assets/_Project/Data/GameDatabase.asset";

        [MenuItem("Void Prospector/Export Economy Data")]
        public static void Export()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            var sector = db.sectors.Where(s => s != null).OrderBy(s => s.index).First();
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/economy/data"));
            Directory.CreateDirectory(dir);

            for (int seed = 1; seed <= 3; seed++)
            {
                var r = EconomySimulator.Run(db, sector, new EconomySimulator.Settings { seed = seed, duration = 1800 });
                File.WriteAllText(Path.Combine(dir, $"sim_seed{seed}.csv"), r.ToCsv());
                Debug.Log($"Seed {seed}: median gap first 5 min {r.MedianGap(0, 300):0.0}s, {r.purchases.Count} purchases in 30 min");
            }

            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new StringBuilder("kind,id,a,b,c,d,e\n");
            foreach (var u in db.upgrades)
                sb.AppendLine(string.Format(inv, "upgrade,{0},{1},{2},{3},{4},{5}|{6}|{7}", u.id, u.baseCost, u.growth, u.maxLevel, u.baseValue, u.perLevel, u.mode, u.offAtLevelZero));
            var composition = sector.meteorTypes.SelectMany(t => t.type.composition).ToList();
            foreach (var m in db.minerals)
            {
                float w = composition.Where(c => c.mineral == m).Sum(c => c.weight);
                sb.AppendLine(string.Format(inv, "mineral,{0},{1},{2},,,", m.id, m.baseValue, m.tier));
                if (w > 0) sb.AppendLine(string.Format(inv, "composition,{0},{1},,,,", m.id, w));
            }
            foreach (var g in db.purity.grades) sb.AppendLine(string.Format(inv, "purity,{0},{1},{2},,,", g.name, g.valueMultiplier, g.dropWeight));
            foreach (var z in db.meteorSizes)
                sb.AppendLine(string.Format(inv, "size,{0},{1},{2},{3},{4},{5}", z.id, z.hpMultiplier, z.chunkMin, z.chunkMax, z.spawnWeight, z.hasCore));
            sb.AppendLine(string.Format(inv, "sector,{0},{1},{2},{3},{4},{5}", sector.id, sector.baseHp, sector.depthHpGrowth, sector.depthValueStep, sector.maxDepth, db.mining.meteorsPerDepth));
            File.WriteAllText(Path.Combine(dir, "data.csv"), sb.ToString());

            Debug.Log($"Economy data exported to {dir}. Rebuild the workbook: python tools/economy/build_economy_xlsx.py docs/economy/data docs/economy/Economy.xlsx");
        }
    }
}
