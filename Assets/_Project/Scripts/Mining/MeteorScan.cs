using System.Collections.Generic;
using VoidProspector.Data;

namespace VoidProspector.Mining
{
    /// <summary>What the scanner reads from a meteor: the minerals still inside, and whether it's worth chasing.</summary>
    public static class MeteorScan
    {
        /// <summary>Counts of each mineral still inside (from chunk <paramref name="from"/> on), most valuable first.</summary>
        public static List<(MineralDefinition mineral, int count)> Contents(MeteorSpawn spawn, int from)
        {
            var counts = new Dictionary<MineralDefinition, int>();
            for (int i = from; i < spawn.minerals.Length; i++)
            {
                var m = spawn.minerals[i];
                counts[m] = counts.TryGetValue(m, out int n) ? n + 1 : 1;
            }
            var list = new List<(MineralDefinition, int)>();
            foreach (var kv in counts) list.Add((kv.Key, kv.Value));
            list.Sort((a, b) => (b.Item1.baseValue * b.Item2).CompareTo(a.Item1.baseValue * a.Item2));
            return list;
        }

        /// <summary>Rare = at least <c>rareMinChunks</c> chunks of tier <c>rareMinTier</c> or higher.</summary>
        public static bool IsRare(MeteorSpawn spawn, ScannerSettings settings)
        {
            if (spawn == null || settings == null) return false;
            int valuable = 0;
            foreach (var m in spawn.minerals)
                if (m != null && m.tier >= settings.rareMinTier) valuable++;
            return valuable >= settings.rareMinChunks;
        }
    }
}
