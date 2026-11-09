using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using R3;
using UnityEditor;
using UnityEngine;
using VoidProspector.Core;
using VoidProspector.Data;
using VoidProspector.Economy;
using VoidProspector.Mining;
using VoidProspector.Refining;

namespace VoidProspector.Tests
{
    /// <summary>Phase 7 data: Sector 3, Crystalline meteors, refinery and scanner upgrades.</summary>
    public class Phase7DataTests
    {
        GameDatabase _db;

        [SetUp]
        public void Setup() => _db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Project/Data/GameDatabase.asset");

        [Test]
        public void Sector3_IsTheCrystalDrift_WithSilverAndQuartz()
        {
            var s3 = _db.sectors.Single(s => s.index == 3);
            Assert.AreEqual("crystal_drift", s3.id);
            var minerals = s3.meteorTypes.SelectMany(t => t.type.composition).Where(c => c.weight > 0).Select(c => c.mineral.id).ToList();
            CollectionAssert.Contains(minerals, "silver");
            CollectionAssert.Contains(minerals, "quartz");
            var s2 = _db.sectors.Single(s => s.index == 2);
            Assert.IsTrue(s3.unlockCost.Any(c => c.mineral == s2.signatureMineral), "paid with the Iron Belt's signature mineral");
        }

        [Test]
        public void CrystallineMeteors_AreBrittleButValuable()
        {
            var crystalline = _db.GetMeteorType("crystalline");
            Assert.Less(crystalline.hpMultiplier, _db.GetMeteorType("stony").hpMultiplier);
            Assert.IsTrue(crystalline.composition.Any(c => c.mineral.tier >= 3 && c.weight > 0));
        }

        [Test]
        public void RefineryAndScannerUpgrades_UnlockWithSector3()
        {
            foreach (var e in new[] { UpgradeEffect.RefinerySpeed, UpgradeEffect.RefineSuccess, UpgradeEffect.RefineryBatch,
                         UpgradeEffect.RefineryStabiliser, UpgradeEffect.ScannerRange })
            {
                var u = _db.GetUpgrade(e);
                Assert.IsNotNull(u, e.ToString());
                Assert.AreEqual(3, u.requiresSector, e.ToString());
            }
            Assert.AreEqual(3, _db.refinery.unlockSector);
        }

        /// <summary>The Phase 7 "Done when": refining is sometimes right and sometimes not.</summary>
        [Test]
        public void Refining_IsWorthItAtLowGrades_AndAGambleAtTheTop_UntilUpgraded()
        {
            var r = new Refinery(_db.refinery, _db.purity, new System.Random(1));
            var none = new RefineryStats(1, 0, 0);
            Assert.Greater(r.ExpectedValueRatio(0, none), 1.3, "Crude → Standard pays off on average");
            Assert.Less(r.ExpectedValueRatio(_db.purity.TopGrade - 1, none), 1.0, "Pristine → Flawless loses on average without upgrades");
            var stabilised = new RefineryStats(1, 0, 1);
            Assert.Greater(r.ExpectedValueRatio(_db.purity.TopGrade - 1, stabilised), 1.0, "a full Stabiliser makes it worth it");
        }
    }

    public class RefineryTests
    {
        GameDatabase _db;
        MineralDefinition _copper;

        [SetUp]
        public void Setup()
        {
            _db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Project/Data/GameDatabase.asset");
            _copper = _db.GetMineral("copper");
        }

        static RefinerySettings Always(float chance, float loss) => new RefinerySettings
        {
            stages = Enumerable.Range(0, 4).Select(_ => new RefineStage { seconds = 10, successChance = chance, failLoss = loss }).ToArray(),
            maxSuccessChance = 1,
        };

        static readonly RefineryStats Plain = new RefineryStats(1, 0, 0);

        [Test]
        public void Success_RaisesGradeAndValue_ByThePurityRatio()
        {
            var r = new Refinery(Always(1, 0), _db.purity, new System.Random(1));
            Assert.IsTrue(r.Load(_copper, 0, 10, 50));
            r.TargetGrade = 1;
            r.Tick(9.9, Plain);
            Assert.AreEqual(0, r.Grade, "not yet");
            r.Tick(0.2, Plain);
            Assert.AreEqual(1, r.Grade);
            Assert.AreEqual(50 * _db.purity[1].valueMultiplier / _db.purity[0].valueMultiplier, r.Value.Value, 1e-9);
            Assert.IsTrue(r.IsDone);
            r.Tick(100, Plain);
            Assert.AreEqual(1, r.Grade, "stops at the target");
        }

        [Test]
        public void Failure_LosesPartOfTheBatch_UnlessStabilised()
        {
            var r = new Refinery(Always(0, 0.25f), _db.purity, new System.Random(1));
            r.Load(_copper, 0, 10, 100);
            r.Tick(10, Plain);
            Assert.AreEqual(7, r.Count, "ceil(2.5) = 3 lost");
            Assert.AreEqual(70d, r.Value.Value, 1e-9);
            Assert.AreEqual(0, r.Grade);

            var safe = new Refinery(Always(0, 0.25f), _db.purity, new System.Random(1));
            safe.Load(_copper, 0, 10, 100);
            safe.Tick(100, new RefineryStats(1, 0, 1));
            Assert.AreEqual(10, safe.Count);
        }

        [Test]
        public void RepeatedFailures_CanLoseTheWholeBatch()
        {
            var r = new Refinery(Always(0, 0.5f), _db.purity, new System.Random(1));
            r.Load(_copper, 0, 4, 20);
            r.Tick(1000, Plain);
            Assert.IsTrue(r.IsEmpty);
            Assert.AreEqual(0d, r.Value.Value);
        }

        [Test]
        public void OneLongTick_RunsManyAttempts_AndSpeedShortensThem()
        {
            var r = new Refinery(Always(1, 0), _db.purity, new System.Random(1));
            r.Load(_copper, 0, 5, 5);
            r.Tick(39.9, Plain);
            Assert.AreEqual(3, r.Grade, "three 10 s attempts in one tick (offline time)");
            r.Tick(1, Plain);
            Assert.AreEqual(_db.purity.TopGrade, r.Grade);

            var fast = new Refinery(Always(1, 0), _db.purity, new System.Random(1));
            fast.Load(_copper, 0, 5, 5);
            fast.Tick(5.01, new RefineryStats(2, 0, 0));
            Assert.AreEqual(1, fast.Grade, "speed ×2 halves the time");
        }

        [Test]
        public void Load_OnlyWhenEmpty_AndNotAtTheTopGrade()
        {
            var r = new Refinery(_db.refinery, _db.purity, new System.Random(1));
            Assert.IsFalse(r.Load(_copper, _db.purity.TopGrade, 5, 5));
            Assert.IsTrue(r.Load(_copper, 0, 5, 5));
            Assert.IsFalse(r.Load(_copper, 0, 5, 5));
        }

        [Test]
        public void SuccessChance_IsCappedAndBoosted()
        {
            var r = new Refinery(_db.refinery, _db.purity, new System.Random(1));
            double plain = r.SuccessChance(1, Plain);
            Assert.AreEqual(plain + 0.1, r.SuccessChance(1, new RefineryStats(1, 0.1, 0)), 1e-6);
            Assert.AreEqual(_db.refinery.maxSuccessChance, r.SuccessChance(0, new RefineryStats(1, 5, 0)), 1e-6);
        }
    }

    public class SessionRefineryTests
    {
        GameDatabase _db;
        SectorDefinition _s1;
        MineralDefinition _copper;

        [SetUp]
        public void Setup()
        {
            _db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Project/Data/GameDatabase.asset");
            _s1 = _db.sectors.Single(s => s.index == 1);
            _copper = _db.GetMineral("copper");
        }

        GameSession WithRefinery()
        {
            var s = new GameSession(_db, _s1, 1);
            s.DebugUnlockNextSector();
            s.DebugUnlockNextSector();
            return s;
        }

        [Test]
        public void Refinery_AppearsWithSector3()
        {
            var s = new GameSession(_db, _s1, 1);
            s.TryCollect(new ChunkData(_copper, 0, 5));
            Assert.IsFalse(s.RefineryUnlocked);
            Assert.IsFalse(s.TryLoadRefinery(_copper, 0));
            s.DebugUnlockNextSector();
            s.DebugUnlockNextSector();
            Assert.IsTrue(s.RefineryUnlocked);
            Assert.IsTrue(s.TryLoadRefinery(_copper, 0));
        }

        [Test]
        public void Loading_TakesUpToABatchFromOneStack()
        {
            var s = WithRefinery();
            int batch = s.RefineryBatchSize;
            for (int i = 0; i < batch + 3; i++) s.TryCollect(new ChunkData(_copper, 1, 10));
            s.TryCollect(new ChunkData(_copper, 0, 5));
            Assert.IsTrue(s.TryLoadRefinery(_copper, 1));
            Assert.AreEqual(batch, s.Refinery.Count);
            Assert.AreEqual(batch * 10d, s.Refinery.Value.Value, 1e-9);
            Assert.AreEqual(3, s.Cargo.CountOf(_copper, 1));
            Assert.AreEqual(1, s.Cargo.CountOf(_copper, 0), "other grades stay");
            Assert.IsFalse(s.TryLoadRefinery(_copper, 0), "one batch at a time");
        }

        [Test]
        public void CollectAndSell()
        {
            var s = WithRefinery();
            for (int i = 0; i < 5; i++) s.TryCollect(new ChunkData(_copper, 2, 20));
            s.TryLoadRefinery(_copper, 2);
            Assert.IsTrue(s.CollectRefinery());
            Assert.AreEqual(5, s.Cargo.CountOf(_copper, 2));
            Assert.AreEqual(100d, s.Cargo.Value.Value, 1e-9);

            s.TryLoadRefinery(_copper, 2);
            Assert.AreEqual(100d, s.SellRefinery().Value, 1e-9);
            Assert.AreEqual(100d, s.Wallet.Credits.Value, 1e-9);
            Assert.IsTrue(s.Refinery.IsEmpty);
        }

        [Test]
        public void Collect_NeedsRoomForTheWholeBatch()
        {
            var s = WithRefinery();
            for (int i = 0; i < 5; i++) s.TryCollect(new ChunkData(_copper, 2, 20));
            s.TryLoadRefinery(_copper, 2);
            var rock = _db.GetMineral("rock");
            while (!s.Cargo.IsFull) s.TryCollect(new ChunkData(rock, 0, 1));
            Assert.IsFalse(s.CollectRefinery());
            Assert.AreEqual(5, s.Refinery.Count, "still in the refinery");
        }

        [Test]
        public void RefiningToANewGrade_FillsTheCodex()
        {
            var s = WithRefinery();
            s.Upgrades.SetLevel(UpgradeEffect.RefineSuccess, 50);   // chance capped at the maximum
            for (int i = 0; i < 5; i++) s.TryCollect(new ChunkData(_copper, 0, 5));
            s.TryLoadRefinery(_copper, 0);
            s.Refinery.TargetGrade = 1;
            for (int i = 0; i < 1000 && !s.Refinery.IsDone && !s.Refinery.IsEmpty; i++) s.TickRefinery(1);
            Assert.IsTrue(s.Refinery.IsDone);
            Assert.IsTrue(s.Codex.Has(_copper, 1));
        }
    }

    public class CodexTests
    {
        GameDatabase _db;
        SectorDefinition _s1;

        [SetUp]
        public void Setup()
        {
            _db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Project/Data/GameDatabase.asset");
            _s1 = _db.sectors.Single(s => s.index == 1);
        }

        [Test]
        public void FirstFind_FillsTheEntry_AndRaisesThatMineralsValue()
        {
            var s = new GameSession(_db, _s1, 1);
            var copper = _db.GetMineral("copper");
            var rock = _db.GetMineral("rock");
            var found = new List<(MineralDefinition, int)>();
            using var sub = s.Codex.Discovered.Subscribe(e => found.Add(e));

            s.TryCollect(new ChunkData(copper, 2, 10));
            s.TryCollect(new ChunkData(copper, 2, 10));
            Assert.AreEqual(1, found.Count, "once per entry");
            Assert.IsTrue(s.Codex.Has(copper, 2));
            Assert.AreEqual(1 + _db.codex.bonusPerGrade[2], s.Codex.ValueMultiplier(copper), 1e-6);
            Assert.AreEqual(1d, s.Codex.ValueMultiplier(rock), "other minerals unchanged");
            Assert.AreEqual(s.Milestones.ValueMultiplier(copper) * s.Codex.ValueMultiplier(copper), s.ValueMultiplier(copper), 1e-9);
        }

        [Test]
        public void CodexAndRefinery_SurviveASave()
        {
            var a = new GameSession(_db, _s1, 1);
            var copper = _db.GetMineral("copper");
            a.DebugUnlockNextSector();
            a.DebugUnlockNextSector();
            for (int i = 0; i < 6; i++) a.TryCollect(new ChunkData(copper, 1, 12));
            a.TryLoadRefinery(copper, 1);
            a.Refinery.TargetGrade = 3;
            a.TickRefinery(3.5);

            var b = new GameSession(_db, _s1, 1);
            var warnings = new List<string>();
            SaveSystem.Apply(b, SaveSystem.FromJson(SaveSystem.ToJson(SaveSystem.Capture(a, DateTime.UtcNow)), out _), warnings);
            CollectionAssert.IsEmpty(warnings);
            Assert.IsTrue(b.Codex.Has(copper, 1));
            Assert.AreEqual(a.Codex.Count, b.Codex.Count);
            Assert.AreEqual(a.Refinery.Count, b.Refinery.Count);
            Assert.AreEqual(a.Refinery.Grade, b.Refinery.Grade);
            Assert.AreEqual(a.Refinery.Value.Value, b.Refinery.Value.Value, 1e-9);
            Assert.AreEqual(a.Refinery.Progress, b.Refinery.Progress, 1e-9);
            Assert.AreEqual(3, b.Refinery.TargetGrade);
            Assert.AreEqual(copper, b.Refinery.Mineral);
        }
    }

    public class ScannerTests
    {
        MineralDefinition _rock, _silver;
        static readonly ScannerSettings Settings = new ScannerSettings { rareMinTier = 3, rareMinChunks = 3 };

        [SetUp]
        public void Setup()
        {
            _rock = ScriptableObject.CreateInstance<MineralDefinition>();
            _rock.baseValue = 1;
            _silver = ScriptableObject.CreateInstance<MineralDefinition>();
            _silver.tier = 3;
            _silver.baseValue = 120;
        }

        [TearDown]
        public void Teardown()
        {
            UnityEngine.Object.DestroyImmediate(_rock);
            UnityEngine.Object.DestroyImmediate(_silver);
        }

        MeteorSpawn Spawn(params MineralDefinition[] minerals) => new MeteorSpawn { minerals = minerals };

        [Test]
        public void Rare_NeedsEnoughValuableChunks()
        {
            Assert.IsFalse(MeteorScan.IsRare(Spawn(_rock, _rock, _silver, _silver), Settings));
            Assert.IsTrue(MeteorScan.IsRare(Spawn(_rock, _silver, _silver, _silver), Settings));
        }

        [Test]
        public void Contents_ShowsWhatIsLeft_MostValuableFirst()
        {
            var spawn = Spawn(_silver, _rock, _rock, _rock, _silver);
            var all = MeteorScan.Contents(spawn, 0);
            Assert.AreEqual(_silver, all[0].mineral);
            Assert.AreEqual(2, all[0].count);
            Assert.AreEqual(3, all[1].count);
            var left = MeteorScan.Contents(spawn, 4);
            Assert.AreEqual(1, left.Count);
            Assert.AreEqual(_silver, left[0].mineral);
        }
    }

    public class Phase7PacingTests
    {
        /// <summary>The GDD's target for reaching Sector 3 on a first run is about 30 minutes.</summary>
        [TestCase(1)]
        [TestCase(2)]
        public void ReachingSector3_TakesAbout30Minutes(int seed)
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Project/Data/GameDatabase.asset");
            var r = EconomySimulator.Run(db, db.sectors.Single(s => s.index == 1),
                new EconomySimulator.Settings { seed = seed, duration = 3600 });
            Assert.GreaterOrEqual(r.jumps.Count, 2, "reached Sector 3 within an hour");
            double minutes = r.jumps[1].time / 60;
            Assert.That(minutes, Is.InRange(18.0, 50.0), $"Sector 3 at {minutes:0.0} min");
            Assert.AreEqual("crystal_drift", r.jumps[1].sectorId);
        }
    }
}
