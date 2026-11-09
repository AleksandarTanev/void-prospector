using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VoidProspector.Data;
using VoidProspector.Economy;

namespace VoidProspector.EditorTools
{
    /// <summary>Phase 8 helpers: a simulated pacing report against the GDD targets, and the analytics log folder.</summary>
    public static class PlaytestTools
    {
        const string DatabasePath = "Assets/_Project/Data/GameDatabase.asset";

        [MenuItem("Void Prospector/Pacing Report (simulated 60 min)")]
        public static void PacingReport()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            var first = db.sectors.Where(s => s != null).OrderBy(s => s.index).First();
            for (int seed = 1; seed <= 3; seed++)
            {
                var r = EconomySimulator.Run(db, first, new EconomySimulator.Settings { seed = seed, duration = 3600 });
                Debug.Log($"Seed {seed}:\n{r.PacingSummary()}");
            }
            Debug.Log("GDD targets: first 5 min a purchase every 5–15 s; Sector 2 at ~10 min, Sector 3 at ~30 min; " +
                      "no wall (gap) over ~10 min. The simulated player buys one level at a time, so its gaps run short.");
        }

        [MenuItem("Void Prospector/Open Save and Analytics Folder")]
        public static void OpenDataFolder()
        {
            string log = Path.Combine(Application.persistentDataPath, "analytics.jsonl");
            EditorUtility.RevealInFinder(File.Exists(log) ? log : Application.persistentDataPath);
        }
    }
}
