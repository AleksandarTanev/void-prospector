using System.Linq;
using NUnit.Framework;
using R3;
using UnityEditor;
using UnityEngine;
using VoidProspector.Core;
using VoidProspector.Data;
using VoidProspector.Economy;
using VoidProspector.Mining;
using VoidProspector.Progression;

namespace VoidProspector.Tests
{
    public class UpgradeTrackTests
    {
        UpgradeDefinition _def;

        [SetUp]
        public void Setup()
        {
            _def = ScriptableObject.CreateInstance<UpgradeDefinition>();
            _def.id = "test";
            _def.effect = UpgradeEffect.DrillDamage;
            _def.baseValue = 1;
            _def.perLevel = 1;
            _def.baseCost = 10;
            _def.growth = 2;
            _def.maxLevel = 5;
        }

        [TearDown]
        public void Teardown() => Object.DestroyImmediate(_def);

        [Test]
        public void CostFor_SumsTheNextLevels()
        {
            var t = new UpgradeTrack(_def, 1);
            Assert.AreEqual(20d + 40 + 80, t.CostFor(3).Value);
            Assert.AreEqual(20d + 40 + 80 + 160, t.CostFor(10).Value, "capped at max level");
        }

        [Test]
        public void QuoteOne_AndTen()
        {
            var t = new UpgradeTrack(_def);
            var one = t.Quote(BuyMode.One, 15);
            Assert.AreEqual(1, one.count);
            Assert.AreEqual(10d, one.cost.Value);
            Assert.IsTrue(one.affordable);

            var ten = t.Quote(BuyMode.Ten, 15);
            Assert.AreEqual(5, ten.count, "only 5 levels exist");
            Assert.AreEqual(310d, ten.cost.Value);
            Assert.IsFalse(ten.affordable);
        }

        [Test]
        public void QuoteMax_BuysAsManyAsCreditsCover()
        {
            var t = new UpgradeTrack(_def);
            var q = t.Quote(BuyMode.Max, 75);   // 10 + 20 + 40 = 70
            Assert.AreEqual(3, q.count);
            Assert.AreEqual(70d, q.cost.Value);
            Assert.IsTrue(q.affordable);

            var none = t.Quote(BuyMode.Max, 5);
            Assert.AreEqual(1, none.count);
            Assert.IsFalse(none.affordable);
        }

        [Test]
        public void Upgrades_TryBuy_SpendsAndLevels()
        {
            var wallet = new Wallet(100);
            var upgrades = new Upgrades(new[] { _def }, wallet);
            var track = upgrades.Get(UpgradeEffect.DrillDamage);
            int events = 0, levelChanges = 0;
            using var bought = upgrades.Bought.Subscribe(_ => events++);
            using var level = track.LevelRx.Subscribe(_ => levelChanges++);

            Assert.AreEqual(3, upgrades.TryBuy(track, BuyMode.Max));
            Assert.AreEqual(30d, wallet.Credits.Value);
            Assert.AreEqual(3, track.Level);
            Assert.AreEqual(4d, track.Value);
            Assert.AreEqual(0, upgrades.TryBuy(track, BuyMode.One), "80 > 30");
            Assert.AreEqual(1, events);
            Assert.AreEqual(2, levelChanges, "initial value + one purchase");
        }

        [Test]
        public void MaxedTrack_QuotesNothing()
        {
            var t = new UpgradeTrack(_def, 5);
            Assert.IsTrue(t.IsMaxed);
            Assert.AreEqual(0, t.Quote(BuyMode.Max, 1e9).count);
        }
    }

    public class DepthProgressTests
    {
        [Test]
        public void UnlocksNextDepth_AfterEnoughBreaks()
        {
            var d = new DepthProgress(3, 2);
            Assert.IsFalse(d.RegisterBreak(0));
            Assert.IsTrue(d.RegisterBreak(0));
            Assert.AreEqual(1, d.Deepest);
            Assert.AreEqual(0, d.Current, "unlocking doesn't move the player");
            Assert.IsTrue(d.CanGoDeeper);
        }

        [Test]
        public void OnlyBreaksAtTheDeepestDepthCount()
        {
            var d = new DepthProgress(3, 2);
            d.Unlock(1, 0);
            d.RegisterBreak(0);
            d.RegisterBreak(0);
            Assert.AreEqual(1, d.Deepest);
            Assert.AreEqual(0, d.Progress);
        }

        [Test]
        public void CannotGoPastTheLastDepth_OrIntoLockedOnes()
        {
            var d = new DepthProgress(2, 1);
            Assert.IsFalse(d.TrySetDepth(1));
            d.RegisterBreak(0);
            Assert.IsTrue(d.TrySetDepth(1));
            Assert.IsTrue(d.IsDeepestUnlocked);
            Assert.IsFalse(d.RegisterBreak(1));
            Assert.AreEqual(1f, d.ProgressFraction);
        }

        [Test]
        public void Bindings_EmitOnlyOnChange()
        {
            var d = new DepthProgress(3, 2);
            int current = 0, deepest = 0, progress = 0;
            using var a = d.CurrentRx.Subscribe(_ => current++);
            using var b = d.DeepestRx.Subscribe(_ => deepest++);
            using var c = d.ProgressRx.Subscribe(_ => progress++);
            d.RegisterBreak(0);           // progress 1
            d.RegisterBreak(0);           // unlock: progress 0, deepest 1
            d.TrySetDepth(1);
            d.TrySetDepth(1);             // no change
            Assert.AreEqual(2, current, "initial + one move");
            Assert.AreEqual(2, deepest, "initial + one unlock");
            Assert.AreEqual(3, progress, "initial + 1 + reset to 0");
        }
    }

    /// <summary>Session and pacing checks against the real data assets.</summary>
    public class EconomyTests
    {
        const string DatabasePath = "Assets/_Project/Data/GameDatabase.asset";
        GameDatabase _db;
        SectorDefinition _sector;

        [SetUp]
        public void Setup()
        {
            _db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            _sector = _db.sectors.Single(s => s.index == 1);
        }

        [Test]
        public void BuyingCargo_RaisesCapacity()
        {
            var s = new GameSession(_db, _sector, 1);
            var cargo = s.Upgrades.Get(UpgradeEffect.CargoCapacity);
            s.Wallet.Add(cargo.NextCost);
            Assert.AreEqual(1, s.Upgrades.TryBuy(cargo, BuyMode.One));
            Assert.AreEqual(60, s.Cargo.Capacity);
            s.Upgrades.SetLevel(UpgradeEffect.CargoCapacity, 3);
            Assert.AreEqual(80, s.Cargo.Capacity, "setting a level without buying (save load) also updates the hold");
        }

        [Test]
        public void AutoSellSetting_EmitsOnChange()
        {
            var s = new GameSession(_db, _sector, 1);
            var seen = new System.Collections.Generic.List<int>();
            using var sub = s.AutoSellBelowRx.Subscribe(seen.Add);
            s.SetAutoSellBelow(2);
            s.SetAutoSellBelow(2);
            s.SetAutoSellBelow(0);
            CollectionAssert.AreEqual(new[] { 0, 2, 0 }, seen);
        }

        [Test]
        public void AutoFire_IsOffUntilBought()
        {
            var s = new GameSession(_db, _sector, 1);
            Assert.AreEqual(0f, s.AutoFireInterval);
            s.Upgrades.SetLevel(UpgradeEffect.AutoFireInterval, 1);
            Assert.AreEqual(1.2f, s.AutoFireInterval, 1e-5f);
        }

        [Test]
        public void AutoSell_SellsLowGradesImmediately()
        {
            var s = new GameSession(_db, _sector, 1);
            var rock = _db.GetMineral("rock");
            s.SetAutoSellBelow(1);   // below Standard
            Assert.AreEqual(CollectResult.AutoSold, s.TryCollect(new ChunkData(rock, 0, 1)));
            Assert.AreEqual(CollectResult.Stored, s.TryCollect(new ChunkData(rock, 1, 2)));
            Assert.AreEqual(1d, s.Wallet.Credits.Value);
            Assert.AreEqual(1, s.Cargo.Count);
        }

        [Test]
        public void Crits_HappenAtTheUpgradedRate()
        {
            var s = new GameSession(_db, _sector, 9);
            s.Upgrades.SetLevel(UpgradeEffect.CritChance, 10);   // 20%
            int crits = 0;
            for (int i = 0; i < 10000; i++)
            {
                var hit = s.RollHit(false);
                if (hit.crit)
                {
                    crits++;
                    Assert.AreEqual(s.DrillDamage.Value * _db.mining.critMultiplier, hit.damage.Value, 1e-9);
                }
            }
            Assert.AreEqual(0.2, crits / 10000.0, 0.02);
        }

        [Test]
        public void DeeperMeteors_HaveMoreHpAndWorthMore()
        {
            var s = new GameSession(_db, _sector, 1);
            var size = _db.meteorSizes[0];
            var type = _sector.meteorTypes[0].type;
            Assert.AreEqual(_sector.MeteorHp(size, type, 0).Value * 1.18 * 1.18, _sector.MeteorHp(size, type, 2).Value, 1e-9);
            var copper = _db.GetMineral("copper");
            var deep = MeteorRoller.RollChunk(copper, _db.purity, _sector, 2, false, new System.Random(1));
            var shallow = MeteorRoller.RollChunk(copper, _db.purity, _sector, 0, false, new System.Random(1));
            Assert.AreEqual(shallow.value.Value * 1.3, deep.value.Value, 1e-9);
        }

        /// <summary>The Phase 4 "Done when": a purchase every 5–15 seconds in the first 5 minutes.</summary>
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void FirstFiveMinutes_BuySomethingEvery5To15Seconds(int seed)
        {
            var r = EconomySimulator.Run(_db, _sector, new EconomySimulator.Settings { seed = seed, duration = 300 });
            double median = r.MedianGap(0, 300);
            Assert.That(median, Is.InRange(5.0, 15.0), $"median gap {median:0.0}s");
            Assert.Less(r.Gaps(0, 300).Max(), 45, "no long wall in the first 5 minutes");
        }
    }
}
