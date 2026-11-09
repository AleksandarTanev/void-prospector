using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VoidProspector.Data;

namespace VoidProspector.Core
{
    /// <summary>Converts between a <see cref="GameSession"/> and <see cref="SaveData"/>, and to and from JSON.</summary>
    public static class SaveSystem
    {
        /// <summary>
        /// Version written by this build. Older saves are migrated in <see cref="Migrate"/>.
        /// 1: Phase 5. 2: depth per sector, unlocked sector, milestones, value per cargo stack.
        /// </summary>
        public const int CurrentVersion = 2;

        public static SaveData Capture(GameSession session, DateTime utcNow)
        {
            var data = new SaveData
            {
                version = CurrentVersion,
                savedAtUtcTicks = utcNow.ToUniversalTime().Ticks,
                credits = session.Wallet.Credits.Value,
                sectorId = session.Sector.id,
                unlockedSector = session.UnlockedSector,
                autoSellBelow = session.AutoSellBelow,
            };
            foreach (var kv in session.SectorDepths)
                data.sectors.Add(new SectorSave { id = kv.Key.id, current = kv.Value.Current, deepest = kv.Value.Deepest, progress = kv.Value.Progress });

            foreach (var track in session.Upgrades.All)
                data.upgrades.Add(new UpgradeSave { id = track.Definition.id, level = track.Level });

            data.cargo.value = session.Cargo.Value.Value;
            foreach (var (mineral, grade, count, value) in session.Cargo.Stacks)
                data.cargo.stacks.Add(new CargoStackSave { mineralId = mineral.id, grade = grade, count = count, value = value.Value });

            var ms = session.Milestones;
            foreach (var m in ms.Done) data.milestones.done.Add(m.id);
            foreach (var kv in ms.Mined) data.milestones.mined.Add(new CountSave { id = kv.Key.id, count = kv.Value });
            foreach (var kv in ms.Broken) data.milestones.broken.Add(new CountSave { id = kv.Key.id, count = kv.Value });
            data.milestones.brokenTotal = ms.BrokenTotal;
            return data;
        }

        /// <summary>
        /// Loads saved state into a fresh session (created in the saved sector). Anything that no longer matches the
        /// data (unknown ids, out-of-range levels or depths) is skipped or clamped and reported in <paramref name="warnings"/>.
        /// </summary>
        public static void Apply(GameSession session, SaveData data, List<string> warnings = null)
        {
            var db = session.Database;

            // Upgrades first: the Cargo upgrade sets the hold's capacity.
            foreach (var u in data.upgrades)
            {
                var def = db.GetUpgrade(u.id);
                if (def == null) { warnings?.Add($"Unknown upgrade '{u.id}' skipped"); continue; }
                if (u.level > def.maxLevel) warnings?.Add($"Upgrade '{u.id}' level {u.level} clamped to {def.maxLevel}");
                session.Upgrades.SetLevel(def.effect, u.level);
            }

            if (data.sectorId != session.Sector.id)
                warnings?.Add($"Saved sector '{data.sectorId}' differs from the session's '{session.Sector.id}'");
            session.RestoreUnlockedSector(data.unlockedSector);

            foreach (var sv in data.sectors)
            {
                var sector = db.GetSector(sv.id);
                if (sector == null) { warnings?.Add($"Unknown sector '{sv.id}' skipped"); continue; }
                session.DepthsOf(sector).Restore(sv.deepest, sv.current, sv.progress);
            }

            var stacks = new List<(MineralDefinition, int, int, GameNumber)>();
            foreach (var st in data.cargo.stacks)
            {
                var mineral = db.GetMineral(st.mineralId);
                if (mineral == null) { warnings?.Add($"Unknown mineral '{st.mineralId}' in cargo skipped"); continue; }
                stacks.Add((mineral, Mathf.Clamp(st.grade, 0, Math.Max(0, session.Purity.Count - 1)), st.count, st.value));
            }
            session.Cargo.Restore(stacks, data.cargo.value);

            var done = new List<MilestoneDefinition>();
            foreach (var id in data.milestones.done)
            {
                var m = db.GetMilestone(id);
                if (m == null) warnings?.Add($"Unknown milestone '{id}' skipped");
                else done.Add(m);
            }
            var mined = data.milestones.mined.Select(c => (db.GetMineral(c.id), c.count));
            var broken = data.milestones.broken.Select(c => (db.GetSector(c.id), c.count));
            session.Milestones.Restore(done, mined, broken, data.milestones.brokenTotal);

            session.Wallet.Restore(data.credits);
            session.SetAutoSellBelow(data.autoSellBelow);
        }

        public static string ToJson(SaveData data) => JsonUtility.ToJson(data);

        /// <summary>Parses and migrates a save. Returns null (with a reason) when the text isn't a usable save.</summary>
        public static SaveData FromJson(string json, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(json)) { error = "empty"; return null; }
            SaveData data;
            try { data = JsonUtility.FromJson<SaveData>(json); }
            catch (Exception e) { error = "not valid JSON: " + e.Message; return null; }

            if (data == null || data.version <= 0) { error = "no version"; return null; }
            if (data.version > CurrentVersion) { error = $"made by a newer build (version {data.version})"; return null; }
            data.upgrades ??= new List<UpgradeSave>();
            data.cargo ??= new CargoSave();
            data.cargo.stacks ??= new List<CargoStackSave>();
            data.sectors ??= new List<SectorSave>();
            data.milestones ??= new MilestoneSave();
            data.milestones.done ??= new List<string>();
            data.milestones.mined ??= new List<CountSave>();
            data.milestones.broken ??= new List<CountSave>();
            Migrate(data);
            return data;
        }

        /// <summary>Upgrades an older save to <see cref="CurrentVersion"/>, one version at a time.</summary>
        static void Migrate(SaveData data)
        {
            if (data.version == 1)
            {
                // Depth was stored for the current sector only.
                if (data.depth != null && !string.IsNullOrEmpty(data.sectorId) && data.sectors.Count == 0)
                    data.sectors.Add(new SectorSave { id = data.sectorId, current = data.depth.current, deepest = data.depth.deepest, progress = data.depth.progress });
                data.version = 2;
            }
            data.depth = new DepthSave();   // legacy field, no longer read
            data.version = CurrentVersion;
        }

        /// <summary>Picks the sector a save was in, or null if it isn't in the database any more.</summary>
        public static SectorDefinition SectorOf(SaveData data, GameDatabase db) =>
            data?.sectorId != null ? db.sectors.FirstOrDefault(s => s != null && s.id == data.sectorId) : null;
    }
}
