using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using VoidProspector.Core;
using VoidProspector.Data;
using VoidProspector.Idle;
using VoidProspector.Mining;

namespace VoidProspector.Tests
{
    /// <summary>Save/load round trips against the real data assets.</summary>
    public class SaveSystemTests
    {
        const string DatabasePath = "Assets/_Project/Data/GameDatabase.asset";
        static readonly DateTime Now = new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);
        GameDatabase _db;
        SectorDefinition _sector;

        [SetUp]
        public void Setup()
        {
            _db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            _sector = _db.sectors.Single(s => s.index == 1);
        }

        GameSession NewSession() => new GameSession(_db, _sector, 1);

        GameSession PlayedSession()
        {
            var s = NewSession();
            s.Upgrades.SetLevel(UpgradeEffect.DrillDamage, 7);
            s.Upgrades.SetLevel(UpgradeEffect.CargoCapacity, 2);
            s.Upgrades.SetLevel(UpgradeEffect.AutoFireInterval, 3);
            s.Wallet.Add(1234567.891);
            s.Depths.Unlock(3, 2);
            for (int i = 0; i < 5; i++) s.RegisterBreak(3);
            s.SetAutoSellBelow(1);
            var rock = _db.GetMineral("rock");
            var copper = _db.GetMineral("copper");
            s.TryCollect(new ChunkData(copper, 2, 12.5));
            s.TryCollect(new ChunkData(copper, 2, 12.5));
            s.TryCollect(new ChunkData(rock, 3, 7.25));
            s.DebugUnlockNextSector();
            s.DepthsOf(IronBelt).Unlock(4, 1);
            return s;
        }

        SectorDefinition IronBelt => _db.sectors.Single(x => x.index == 2);

        [Test]
        public void RoundTrip_ThroughJson_RestoresEverything()
        {
            var a = PlayedSession();
            var json = SaveSystem.ToJson(SaveSystem.Capture(a, Now));
            var data = SaveSystem.FromJson(json, out var error);
            Assert.IsNull(error);

            var b = NewSession();
            var warnings = new List<string>();
            SaveSystem.Apply(b, data, warnings);

            CollectionAssert.IsEmpty(warnings);
            Assert.AreEqual(Now, data.SavedAtUtc);
            Assert.AreEqual(a.Wallet.Credits.Value, b.Wallet.Credits.Value, "credits survive JSON exactly");
            foreach (var track in a.Upgrades.All)
                Assert.AreEqual(track.Level, b.Upgrades.Get(track.Effect).Level, track.Definition.id);
            Assert.AreEqual(a.Cargo.Capacity, b.Cargo.Capacity);
            Assert.AreEqual(3, b.Cargo.Count);
            Assert.AreEqual(a.Cargo.Value.Value, b.Cargo.Value.Value);
            Assert.AreEqual(2, b.Cargo.CountOf(_db.GetMineral("copper"), 2));
            Assert.AreEqual(1, b.Cargo.CountOf(_db.GetMineral("rock"), 3));
            Assert.AreEqual(a.Depths.Deepest, b.Depths.Deepest);
            Assert.AreEqual(a.Depths.Current, b.Depths.Current);
            Assert.AreEqual(5, b.Depths.Progress);
            Assert.AreEqual(1, b.AutoSellBelow);

            Assert.AreEqual(2, b.UnlockedSector);
            Assert.AreEqual(4, b.DepthsOf(IronBelt).Deepest, "each sector keeps its own depth");
            Assert.AreEqual(1, b.DepthsOf(IronBelt).Current);
            Assert.AreEqual(a.Cargo.ValueOfCheapest(_db.GetMineral("copper"), 1).Value,
                b.Cargo.ValueOfCheapest(_db.GetMineral("copper"), 1).Value, 1e-9, "values are kept per stack");
            Assert.AreEqual(2, b.Milestones.MinedCount(_db.GetMineral("copper")));
            Assert.AreEqual(5, b.Milestones.BrokenCount(_sector));
            Assert.AreEqual(5, b.Milestones.BrokenCount(null));
        }

        [Test]
        public void CompletedMilestones_AreRestored_WithoutPayingAgain()
        {
            var a = NewSession();
            var copper = _db.GetMineral("copper");
            var m = _db.milestones.First(x => x.goal == MilestoneGoal.MineMineral && x.mineral == copper);
            a.SetAutoSellBelow(1);   // Crude chunks are sold on pickup, so the hold never fills
            for (int i = 0; i < m.target; i++) a.TryCollect(new ChunkData(copper, 0, 0));
            Assert.IsTrue(a.Milestones.IsDone(m));
            Assert.AreEqual(m.rewardCredits, a.Wallet.Credits.Value, 1e-9);

            var b = NewSession();
            SaveSystem.Apply(b, SaveSystem.FromJson(SaveSystem.ToJson(SaveSystem.Capture(a, Now)), out _));
            Assert.IsTrue(b.Milestones.IsDone(m));
            Assert.AreEqual(m.rewardCredits, b.Wallet.Credits.Value, 1e-9, "reward not paid twice");
            Assert.AreEqual(1 + m.valueBonus, b.Milestones.ValueMultiplier(copper), 1e-6, "bonus restored");
        }

        [Test]
        public void Version1Saves_AreMigrated()
        {
            const string v1 = "{\"version\":1,\"savedAtUtcTicks\":0,\"credits\":42,\"sectorId\":\"debris_ring\",\"autoSellBelow\":0," +
                              "\"depth\":{\"current\":2,\"deepest\":3,\"progress\":4},\"upgrades\":[]," +
                              "\"cargo\":{\"value\":30,\"stacks\":[{\"mineralId\":\"copper\",\"grade\":0,\"count\":3}]}}";
            var data = SaveSystem.FromJson(v1, out var error);
            Assert.IsNull(error);
            Assert.AreEqual(SaveSystem.CurrentVersion, data.version);

            var b = NewSession();
            var warnings = new List<string>();
            SaveSystem.Apply(b, data, warnings);
            CollectionAssert.IsEmpty(warnings);
            Assert.AreEqual(3, b.Depths.Deepest);
            Assert.AreEqual(2, b.Depths.Current);
            Assert.AreEqual(4, b.Depths.Progress);
            Assert.AreEqual(1, b.UnlockedSector);
            Assert.AreEqual(30d, b.Cargo.Value.Value);
            Assert.AreEqual(10d, b.Cargo.ValueOfCheapest(_db.GetMineral("copper"), 1).Value, 1e-9, "old total shared out per chunk");
        }

        [Test]
        public void UnknownIds_AreSkipped_AndLevelsClamped()
        {
            var data = SaveSystem.Capture(PlayedSession(), Now);
            data.upgrades.Add(new UpgradeSave { id = "removed-upgrade", level = 3 });
            var crit = _db.GetUpgrade(UpgradeEffect.CritChance);
            data.upgrades.Add(new UpgradeSave { id = crit.id, level = crit.maxLevel + 50 });
            data.cargo.stacks.Add(new CargoStackSave { mineralId = "unobtainium", grade = 0, count = 4 });

            var b = NewSession();
            var warnings = new List<string>();
            SaveSystem.Apply(b, data, warnings);

            Assert.AreEqual(3, warnings.Count, string.Join("\n", warnings));
            Assert.AreEqual(crit.maxLevel, b.Upgrades.Get(UpgradeEffect.CritChance).Level);
            Assert.AreEqual(3, b.Cargo.Count, "unknown mineral not added");
        }

        [Test]
        public void Depth_IsClampedToTheSector()
        {
            var data = SaveSystem.Capture(NewSession(), Now);
            data.sectors.Clear();
            data.sectors.Add(new SectorSave { id = _sector.id, deepest = 999, current = 999, progress = 999 });
            var b = NewSession();
            SaveSystem.Apply(b, data);
            Assert.AreEqual(_sector.maxDepth - 1, b.Depths.Deepest);
            Assert.AreEqual(b.Depths.Deepest, b.Depths.Current);
            Assert.AreEqual(0, b.Depths.Progress, "no progress once the last depth is unlocked");
        }

        [TestCase("")]
        [TestCase("not json at all")]
        [TestCase("{}")]
        [TestCase("{\"version\":99}")]
        public void BadOrFutureSaves_AreRejected(string json)
        {
            Assert.IsNull(SaveSystem.FromJson(json, out var error));
            Assert.IsNotNull(error);
        }

        [Test]
        public void Saves_CarryTheCurrentVersion()
        {
            var data = SaveSystem.FromJson(SaveSystem.ToJson(SaveSystem.Capture(NewSession(), Now)), out _);
            Assert.AreEqual(SaveSystem.CurrentVersion, data.version);
        }
    }

    public class SaveStoreTests
    {
        string _dir;
        SaveStore _store;

        [SetUp]
        public void Setup()
        {
            _dir = Path.Combine(Path.GetTempPath(), "vp-save-tests-" + Guid.NewGuid().ToString("N"));
            _store = new SaveStore(Path.Combine(_dir, "save.json"));
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        static string Save(double credits) =>
            SaveSystem.ToJson(new SaveData { version = SaveSystem.CurrentVersion, credits = credits });

        [Test]
        public void NewPlayer_HasNoSave()
        {
            Assert.IsFalse(_store.Exists);
            Assert.IsNull(_store.Load());
        }

        [Test]
        public void Write_KeepsThePreviousSaveAsBackup_AndLeavesNoTempFile()
        {
            _store.Write(Save(1));
            _store.Write(Save(2));
            Assert.AreEqual(2d, _store.Load().credits);
            Assert.IsTrue(File.Exists(_store.BackupPath));
            Assert.IsFalse(File.Exists(_store.TempPath));
        }

        [Test]
        public void CorruptSave_FallsBackToBackup()
        {
            _store.Write(Save(1));
            _store.Write(Save(2));
            File.WriteAllText(_store.FilePath, "{\"version\":1,\"cred");   // cut off mid-write
            var problems = new List<string>();
            var data = _store.Load(problems);
            Assert.AreEqual(1d, data.credits);
            Assert.AreEqual(1, problems.Count);
        }

        [Test]
        public void CrashBetweenRenames_StillLoadsTheBackup()
        {
            _store.Write(Save(1));
            // Simulate a crash after the old save became the backup but before the temp file moved into place.
            File.Move(_store.FilePath, _store.BackupPath);
            File.WriteAllText(_store.TempPath, Save(2));
            Assert.AreEqual(1d, _store.Load().credits);
            _store.Write(Save(3));
            Assert.AreEqual(3d, _store.Load().credits, "the next write recovers");
        }

        [Test]
        public void Delete_RemovesEverything()
        {
            _store.Write(Save(1));
            _store.Write(Save(2));
            _store.Delete();
            Assert.IsFalse(_store.Exists);
        }
    }

    public class OfflineEarningsTests
    {
        const string DatabasePath = "Assets/_Project/Data/GameDatabase.asset";
        static readonly DateTime Saved = new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);
        readonly OfflineSettings _settings = new OfflineSettings { capHours = 8, minAwaySeconds = 60, efficiency = 1 };

        [Test]
        public void PaysIncomeTimesTimeAway()
        {
            var r = OfflineEarnings.Calculate(Saved, Saved.AddHours(2), 10, _settings);
            Assert.AreEqual(7200, r.paidSeconds, 1e-6);
            Assert.AreEqual(72000d, r.credits.Value);
            Assert.IsFalse(r.capped);
        }

        [Test]
        public void CapsAtEightHours()
        {
            var r = OfflineEarnings.Calculate(Saved, Saved.AddHours(30), 1, _settings);
            Assert.IsTrue(r.capped);
            Assert.AreEqual(8 * 3600, r.paidSeconds, 1e-6);
            Assert.AreEqual(30 * 3600, r.awaySeconds, 1e-6);
            Assert.AreEqual(28800d, r.credits.Value);
        }

        [Test]
        public void ShortAbsences_PayNothing()
        {
            var r = OfflineEarnings.Calculate(Saved, Saved.AddSeconds(59), 100, _settings);
            Assert.IsFalse(r.IsAway);
            Assert.AreEqual(0d, r.credits.Value);
        }

        [Test]
        public void ClockMovedBack_PaysNothing()
        {
            var r = OfflineEarnings.Calculate(Saved, Saved.AddHours(-3), 100, _settings);
            Assert.IsTrue(r.clockWentBack);
            Assert.IsFalse(r.IsAway);
            Assert.AreEqual(0d, r.credits.Value);
        }

        [Test]
        public void Efficiency_ScalesThePayout()
        {
            var half = new OfflineSettings { capHours = 8, minAwaySeconds = 60, efficiency = 0.5f };
            Assert.AreEqual(1800d, OfflineEarnings.Calculate(Saved, Saved.AddHours(1), 1, half).credits.Value);
        }

        [Test]
        public void IdleIncome_CountsDrones()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            var s = new GameSession(db, db.sectors.Single(x => x.index == 2), 1);
            s.Upgrades.SetLevel(UpgradeEffect.DrillDamage, 20);
            Assert.AreEqual(0d, OfflineEarnings.EstimateIncomePerSecond(s, 600));

            s.Upgrades.SetLevel(UpgradeEffect.DroneCount, 1);
            double one = OfflineEarnings.EstimateIncomePerSecond(s, 600);
            Assert.Greater(one, 0, "drones alone mine while away");
            s.Upgrades.SetLevel(UpgradeEffect.DroneCount, 3);
            Assert.Greater(OfflineEarnings.EstimateIncomePerSecond(s, 600), one * 2, "three drones earn well over twice one");
        }

        [Test]
        public void IdleIncome_IsZeroWithoutAutoFire_AndPositiveWithIt()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            var s = new GameSession(db, db.sectors.Single(x => x.index == 1), 1);
            Assert.AreEqual(0d, OfflineEarnings.EstimateIncomePerSecond(s, 600));

            s.Upgrades.SetLevel(UpgradeEffect.AutoFireInterval, 1);
            double rate = OfflineEarnings.EstimateIncomePerSecond(s, 600);
            Assert.Greater(rate, 0);
            Assert.AreEqual(rate, OfflineEarnings.EstimateIncomePerSecond(s, 600), "deterministic");

            s.Upgrades.SetLevel(UpgradeEffect.DrillDamage, 20);
            Assert.Greater(OfflineEarnings.EstimateIncomePerSecond(s, 600), rate, "a stronger drill earns more");
        }

        [Test]
        public void EstimatingIncome_DoesNotChangeTheLiveSession()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            var s = new GameSession(db, db.sectors.Single(x => x.index == 1), 1);
            s.Upgrades.SetLevel(UpgradeEffect.AutoFireInterval, 1);
            s.Wallet.Add(500);
            OfflineEarnings.EstimateIncomePerSecond(s, 600);
            Assert.AreEqual(500d, s.Wallet.Credits.Value);
            Assert.AreEqual(0, s.Cargo.Count);
        }

        [TestCase(0, "0s")]
        [TestCase(45, "45s")]
        [TestCase(125, "2m")]
        [TestCase(7500, "2h 05m")]
        [TestCase(100000, "1d 3h")]
        public void TimeFormatter_ShortDurations(double seconds, string expected)
        {
            Assert.AreEqual(expected, TimeFormatter.Format(seconds));
        }
    }
}
