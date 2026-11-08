using System;
using System.Linq;
using NUnit.Framework;
using R3;
using UnityEditor;
using VoidProspector.Core;
using VoidProspector.Data;
using VoidProspector.Economy;
using VoidProspector.Mining;

namespace VoidProspector.Tests
{
    public class WalletTests
    {
        [Test]
        public void AddAndSpend()
        {
            var w = new Wallet();
            int changes = 0;
            using var sub = w.CreditsRx.Subscribe(_ => changes++);   // emits the current value on subscribe

            w.Add(100);
            Assert.AreEqual(100d, w.Credits.Value);
            Assert.IsTrue(w.TrySpend(40));
            Assert.AreEqual(60d, w.Credits.Value);
            Assert.IsFalse(w.TrySpend(61));
            Assert.AreEqual(60d, w.Credits.Value);
            Assert.AreEqual(3, changes, "initial value + 2 changes; a failed spend emits nothing");
        }

        [Test]
        public void NegativeAmounts_Throw()
        {
            var w = new Wallet();
            Assert.Throws<ArgumentOutOfRangeException>(() => w.Add(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => w.TrySpend(-1));
        }
    }

    public class CargoHoldTests
    {
        MineralDefinition _copper;

        [SetUp]
        public void Setup()
        {
            _copper = UnityEngine.ScriptableObject.CreateInstance<MineralDefinition>();
            _copper.id = "copper";
        }

        [TearDown]
        public void Teardown() => UnityEngine.Object.DestroyImmediate(_copper);

        [Test]
        public void FillsUpToCapacity_ThenRejects()
        {
            var hold = new CargoHold(3);
            for (int i = 0; i < 3; i++) Assert.IsTrue(hold.TryAdd(new ChunkData(_copper, 0, 5)));
            Assert.IsTrue(hold.IsFull);
            Assert.IsFalse(hold.TryAdd(new ChunkData(_copper, 0, 5)));
            Assert.AreEqual(3, hold.Count);
            Assert.AreEqual(15d, hold.Value.Value);
            Assert.AreEqual(1f, hold.Fill);
        }

        [Test]
        public void CountsByMineralAndGrade()
        {
            var hold = new CargoHold(10);
            hold.TryAdd(new ChunkData(_copper, 0, 5));
            hold.TryAdd(new ChunkData(_copper, 2, 25));
            hold.TryAdd(new ChunkData(_copper, 2, 25));
            Assert.AreEqual(1, hold.CountOf(_copper, 0));
            Assert.AreEqual(2, hold.CountOf(_copper, 2));
            Assert.AreEqual(3, hold.CountOf(_copper));
        }

        [Test]
        public void TakeAll_ReturnsValueAndEmpties()
        {
            var hold = new CargoHold(10);
            hold.TryAdd(new ChunkData(_copper, 1, 10));
            hold.TryAdd(new ChunkData(_copper, 3, 75));
            Assert.AreEqual(85d, hold.TakeAll().Value);
            Assert.IsTrue(hold.IsEmpty);
            Assert.AreEqual(0d, hold.Value.Value);
            Assert.AreEqual(0, hold.CountOf(_copper));
        }

        [Test]
        public void Bindings_SeeConsistentCountAndValue()
        {
            var hold = new CargoHold(10);
            var seen = new System.Collections.Generic.List<(int count, double value)>();
            using var sub = hold.CountRx.Subscribe(n => seen.Add((n, hold.Value.Value)));
            hold.TryAdd(new ChunkData(_copper, 0, 5));
            hold.TryAdd(new ChunkData(_copper, 0, 5));
            hold.TakeAll();
            CollectionAssert.AreEqual(new[] { (0, 0d), (1, 5d), (2, 10d), (0, 0d) }, seen);
        }
    }

    public class MeteorStateTests
    {
        static readonly float[] Thresholds = { 0.66f, 0.33f };

        [Test]
        public void CracksAtThresholds_AndSpillsAQuarterEach()
        {
            var m = new MeteorState(100, 20, Thresholds, 0.25f);

            var r = m.Hit(30);   // 70%
            Assert.AreEqual(0, r.cracks);
            Assert.AreEqual(0, r.TotalChunks);

            r = m.Hit(5);        // 65%
            Assert.AreEqual(1, r.cracks);
            Assert.AreEqual(5, r.crackChunks);
            Assert.AreEqual(1, m.Stage);

            r = m.Hit(40);       // 25%
            Assert.AreEqual(1, r.cracks);
            Assert.AreEqual(5, r.crackChunks);
            Assert.AreEqual(10, m.RemainingChunks);

            r = m.Hit(1000);
            Assert.IsTrue(r.broke);
            Assert.AreEqual(10, r.breakChunks);
            Assert.AreEqual(0, m.RemainingChunks);
            Assert.AreEqual(0d, m.Hp.Value);
        }

        [Test]
        public void OneShot_SpillsEverythingOnce()
        {
            var m = new MeteorState(10, 7, Thresholds, 0.25f);
            var r = m.Hit(50);
            Assert.AreEqual(2, r.cracks);
            Assert.IsTrue(r.broke);
            Assert.AreEqual(7, r.TotalChunks);
        }

        [Test]
        public void HitsAfterBreaking_DoNothing()
        {
            var m = new MeteorState(5, 2, Thresholds, 0.25f);
            m.Hit(5);
            var r = m.Hit(5);
            Assert.IsFalse(r.broke);
            Assert.AreEqual(0, r.TotalChunks);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(40)]
        public void TotalSpilled_EqualsChunkCount(int chunks)
        {
            var m = new MeteorState(100, chunks, Thresholds, 0.25f);
            int spilled = 0;
            while (!m.IsBroken) spilled += m.Hit(7).TotalChunks;
            Assert.AreEqual(chunks, spilled);
        }
    }

    /// <summary>Rolls against the real data assets.</summary>
    public class MeteorRollerTests
    {
        const string DatabasePath = "Assets/_Project/Data/GameDatabase.asset";
        GameDatabase _db;
        SectorDefinition _sector;

        [SetUp]
        public void Setup()
        {
            _db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            Assert.IsNotNull(_db);
            _sector = _db.sectors.Single(s => s.index == 1);
        }

        [Test]
        public void Meteors_HaveDataDrivenHpAndChunks()
        {
            var rng = new Random(42);
            for (int i = 0; i < 500; i++)
            {
                var m = MeteorRoller.RollMeteor(_db, _sector, 0, rng);
                Assert.AreEqual(_sector.baseHp * m.size.hpMultiplier * m.type.hpMultiplier, m.hp.Value, 1e-9);
                Assert.That(m.minerals.Length, Is.InRange(m.size.chunkMin, m.size.chunkMax));
                Assert.IsTrue(m.minerals.All(x => m.type.composition.Any(c => c.mineral == x && c.weight > 0)));
            }
        }

        [Test]
        public void SameSeed_SameMeteor()
        {
            var a = MeteorRoller.RollMeteor(_db, _sector, 0, new Random(7));
            var b = MeteorRoller.RollMeteor(_db, _sector, 0, new Random(7));
            Assert.AreSame(a.size, b.size);
            CollectionAssert.AreEqual(a.minerals, b.minerals);
        }

        [Test]
        public void ChunkValue_IsBaseTimesPurity()
        {
            var copper = _db.GetMineral("copper");
            var rng = new Random(3);
            for (int i = 0; i < 200; i++)
            {
                var c = MeteorRoller.RollChunk(copper, _db.purity, _sector, 0, false, rng);
                Assert.AreEqual(copper.baseValue * _db.purity[c.grade].valueMultiplier, c.value.Value, 1e-9);
            }
        }

        [Test]
        public void CoreChunks_AreNeverCrude()
        {
            var rock = _db.GetMineral("rock");
            var rng = new Random(5);
            for (int i = 0; i < 500; i++)
                Assert.GreaterOrEqual(MeteorRoller.RollChunk(rock, _db.purity, _sector, 0, true, rng).grade, 1);
        }

        [Test]
        public void Session_SellMovesCargoValueToWallet()
        {
            var session = new GameSession(_db, _sector, 1);
            Assert.AreEqual(50, session.CargoCapacity);
            Assert.AreEqual(1d, session.DrillDamage.Value);

            var copper = _db.GetMineral("copper");
            session.TryCollect(new ChunkData(copper, 0, 5));
            session.TryCollect(new ChunkData(copper, 2, 25));
            Assert.AreEqual(30d, session.SellCargo().Value);
            Assert.AreEqual(30d, session.Wallet.Credits.Value);
            Assert.IsTrue(session.Cargo.IsEmpty);
        }
    }
}
