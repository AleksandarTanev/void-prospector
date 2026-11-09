using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using R3;
using UnityEditor;
using UnityEngine;
using VoidProspector.Core;
using VoidProspector.Data;
using VoidProspector.Drones;
using VoidProspector.Economy;
using VoidProspector.Mining;

namespace VoidProspector.Tests
{
    /// <summary>Phase 6 data: Sector 2, Metallic meteors, drones and milestones.</summary>
    public class Phase6DataTests
    {
        const string DatabasePath = "Assets/_Project/Data/GameDatabase.asset";
        GameDatabase _db;

        [SetUp]
        public void Setup() => _db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);

        [Test]
        public void Sector2_IsTheIronBelt_WithIronAndNickel()
        {
            var s2 = _db.sectors.Single(s => s.index == 2);
            Assert.AreEqual("iron_belt", s2.id);
            var minerals = s2.meteorTypes.SelectMany(t => t.type.composition).Where(c => c.weight > 0).Select(c => c.mineral.id).ToList();
            CollectionAssert.Contains(minerals, "iron");
            CollectionAssert.Contains(minerals, "nickel");
            Assert.IsTrue(s2.meteorTypes.Any(t => t.type.id == "metallic"));
        }

        [Test]
        public void JumpDrive_IsPaidWithTheSignatureMineralOfSector1()
        {
            var s1 = _db.sectors.Single(s => s.index == 1);
            var s2 = _db.sectors.Single(s => s.index == 2);
            Assert.IsTrue(s2.unlockCost.Any(c => c.mineral == s1.signatureMineral && c.amount > 0));
            Assert.Less(s2.unlockCost.Where(c => c.mineral != null).Max(c => c.amount), 50, "fits in the starting cargo hold");
        }

        [Test]
        public void MetallicMeteors_AreTougherAndRicherThanStony()
        {
            var stony = _db.GetMeteorType("stony");
            var metallic = _db.GetMeteorType("metallic");
            Assert.Greater(metallic.hpMultiplier, stony.hpMultiplier);
            double Avg(MeteorTypeDefinition t) => t.composition.Sum(c => c.weight * c.mineral.baseValue) / t.composition.Sum(c => c.weight);
            Assert.Greater(Avg(metallic), Avg(stony) * 3);
        }

        [Test]
        public void DroneUpgrades_UnlockWithSector2()
        {
            foreach (var e in new[] { UpgradeEffect.DroneCount, UpgradeEffect.DroneDamage, UpgradeEffect.DroneTargeting })
            {
                var u = _db.GetUpgrade(e);
                Assert.IsNotNull(u, e.ToString());
                Assert.AreEqual(2, u.requiresSector, e.ToString());
            }
        }

        [Test]
        public void EveryMilestone_HasATitleAndAReward()
        {
            Assert.GreaterOrEqual(_db.milestones.Length, 5);
            foreach (var m in _db.milestones)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(m.Title), m.id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(m.RewardText), m.id);
            }
            Assert.IsTrue(_db.milestones.Any(m => m.goal == MilestoneGoal.MineMineral && m.mineral.id == "iron"), "a 'Mine 1,000 Iron' style milestone");
        }
    }

    /// <summary>Sectors, the jump drive and travel, on the real data.</summary>
    public class SectorTests
    {
        const string DatabasePath = "Assets/_Project/Data/GameDatabase.asset";
        GameDatabase _db;
        SectorDefinition _s1, _s2;
        MineralDefinition _copper, _rock;

        [SetUp]
        public void Setup()
        {
            _db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            _s1 = _db.sectors.Single(s => s.index == 1);
            _s2 = _db.sectors.Single(s => s.index == 2);
            _copper = _db.GetMineral("copper");
            _rock = _db.GetMineral("rock");
        }

        int CopperNeeded => (int)_s2.unlockCost.First(c => c.mineral == _copper).amount;

        GameSession AtLastDepth()
        {
            var s = new GameSession(_db, _s1, 1);
            s.Upgrades.SetLevel(UpgradeEffect.CargoCapacity, 10);
            s.Depths.Unlock(int.MaxValue, 0);
            return s;
        }

        [Test]
        public void JumpDrive_NeedsTheLastDepthFirst()
        {
            var s = new GameSession(_db, _s1, 1);
            Assert.AreEqual(_s2, s.NextSector);
            Assert.AreEqual(JumpState.NeedsDepth, s.JumpState);
            CollectionAssert.IsEmpty(s.JumpReserve, "nothing is kept back before the jump drive is available");
            s.Depths.Unlock(int.MaxValue, 0);
            Assert.AreEqual(JumpState.Saving, s.JumpState);
        }

        [Test]
        public void BuyingTheJumpDrive_PaysCreditsAndCopper_AndUnlocksSector2()
        {
            var s = AtLastDepth();
            s.Wallet.Add(_s2.UnlockCredits + 7);
            for (int i = 0; i < CopperNeeded - 1; i++) s.TryCollect(new ChunkData(_copper, 0, 5));
            Assert.AreEqual(JumpState.Saving, s.JumpState, "one Copper short");
            Assert.IsFalse(s.TryBuyJumpDrive());

            s.TryCollect(new ChunkData(_copper, 2, 20));
            s.TryCollect(new ChunkData(_copper, 3, 50));   // the dearest chunk should be left over
            Assert.AreEqual(JumpState.Affordable, s.JumpState);
            Assert.IsTrue(s.TryBuyJumpDrive());

            Assert.AreEqual(2, s.UnlockedSector);
            Assert.AreEqual(7d, s.Wallet.Credits.Value);
            Assert.AreEqual(1, s.Cargo.CountOf(_copper), "pays with the cheapest chunks");
            Assert.AreEqual(1, s.Cargo.CountOf(_copper, 3));
            Assert.AreEqual(50d, s.Cargo.Value.Value, 1e-9);
            Assert.AreEqual(JumpState.NeedsDepth, s.JumpState, "the next jump needs the Iron Belt's last depth");
        }

        [Test]
        public void Travel_OnlyToUnlockedSectors_AndEachSectorKeepsItsDepth()
        {
            var s = AtLastDepth();
            s.Depths.TrySetDepth(5);
            Assert.IsFalse(s.TravelTo(_s2), "locked");
            s.DebugUnlockNextSector();
            Assert.IsTrue(s.TravelTo(_s2));
            Assert.AreEqual(_s2, s.Sector);
            Assert.AreEqual(0, s.Depth, "a new sector starts at depth 1");
            CollectionAssert.Contains(_s2.meteorTypes.Select(t => t.type).ToList(), s.RollMeteor().type, "meteors come from the new sector");
            Assert.IsTrue(s.TravelTo(_s1));
            Assert.AreEqual(5, s.Depth, "back where we left");
            Assert.IsFalse(s.TravelTo(_s1), "already here");
        }

        [Test]
        public void Selling_KeepsCopperForTheJumpDrive()
        {
            var s = AtLastDepth();
            for (int i = 0; i < CopperNeeded + 5; i++) s.TryCollect(new ChunkData(_copper, 0, 5));
            s.TryCollect(new ChunkData(_rock, 0, 1));
            Assert.AreEqual(5d * 5 + 1, s.SellableValue.Value, 1e-9);
            Assert.AreEqual(5d * 5 + 1, s.SellCargo().Value, 1e-9);
            Assert.AreEqual(CopperNeeded, s.Cargo.CountOf(_copper));
            Assert.AreEqual(CopperNeeded, s.Cargo.Count, "the rock and the extra Copper were sold");
        }

        [Test]
        public void AutoSell_KeepsCopperUntilTheJumpDriveHasEnough()
        {
            var s = AtLastDepth();
            s.SetAutoSellBelow(1);
            Assert.AreEqual(CollectResult.AutoSold, s.TryCollect(new ChunkData(_rock, 0, 1)));
            for (int i = 0; i < CopperNeeded; i++)
                Assert.AreEqual(CollectResult.Stored, s.TryCollect(new ChunkData(_copper, 0, 5)));
            Assert.AreEqual(CollectResult.AutoSold, s.TryCollect(new ChunkData(_copper, 0, 5)), "enough kept now");
        }

        [Test]
        public void Selling_KeepsNothing_BeforeTheLastDepth()
        {
            var s = new GameSession(_db, _s1, 1);
            for (int i = 0; i < 10; i++) s.TryCollect(new ChunkData(_copper, 0, 5));
            Assert.AreEqual(50d, s.SellCargo().Value);
            Assert.IsTrue(s.Cargo.IsEmpty);
        }

        [Test]
        public void DroneUpgrades_CanOnlyBeBoughtOnceSector2IsUnlocked()
        {
            var s = new GameSession(_db, _s1, 1);
            var drones = s.Upgrades.Get(UpgradeEffect.DroneCount);
            s.Wallet.Add(1e9);
            Assert.IsFalse(s.IsAvailable(drones));
            Assert.AreEqual(0, s.TryBuy(drones, BuyMode.One));
            s.DebugUnlockNextSector();
            Assert.AreEqual(1, s.TryBuy(drones, BuyMode.One));
            Assert.AreEqual(1, s.DroneCount);
        }
    }

    public class CargoReserveTests
    {
        MineralDefinition _copper, _rock;

        [SetUp]
        public void Setup()
        {
            _copper = ScriptableObject.CreateInstance<MineralDefinition>();
            _rock = ScriptableObject.CreateInstance<MineralDefinition>();
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(_copper);
            Object.DestroyImmediate(_rock);
        }

        [Test]
        public void Remove_TakesLowestGradesFirst_AndTheirValue()
        {
            var hold = new CargoHold(10);
            hold.TryAdd(new ChunkData(_copper, 2, 30));
            hold.TryAdd(new ChunkData(_copper, 0, 4));
            hold.TryAdd(new ChunkData(_copper, 0, 6));
            hold.TryAdd(new ChunkData(_rock, 0, 1));
            Assert.AreEqual(2, hold.Remove(_copper, 2));
            Assert.AreEqual(1, hold.CountOf(_copper, 2));
            Assert.AreEqual(31d, hold.Value.Value, 1e-9);
            Assert.AreEqual(2, hold.Count);
            Assert.AreEqual(1, hold.Remove(_copper, 5), "only one left");
            Assert.AreEqual(1d, hold.Value.Value, 1e-9);
        }

        [Test]
        public void TakeAllExcept_KeepsTheCheapestChunks()
        {
            var hold = new CargoHold(10);
            hold.TryAdd(new ChunkData(_copper, 3, 100));
            hold.TryAdd(new ChunkData(_copper, 0, 5));
            hold.TryAdd(new ChunkData(_rock, 0, 1));
            var taken = hold.TakeAllExcept(new List<(MineralDefinition, int)> { (_copper, 1) });
            Assert.AreEqual(101d, taken.Value, 1e-9);
            Assert.AreEqual(1, hold.Count);
            Assert.AreEqual(1, hold.CountOf(_copper, 0));
            Assert.AreEqual(5d, hold.Value.Value, 1e-9);
        }

        [Test]
        public void PartialStacks_SplitTheirValueEvenly()
        {
            var hold = new CargoHold(10);
            hold.TryAdd(new ChunkData(_copper, 1, 9));
            hold.TryAdd(new ChunkData(_copper, 1, 11));
            Assert.AreEqual(10d, hold.ValueOfCheapest(_copper, 1).Value, 1e-9);
            Assert.AreEqual(20d, hold.ValueOfCheapest(_copper, 5).Value, 1e-9);
        }
    }

    public class DroneTests
    {
        [Test]
        public void Bay_FiresEachDroneOncePerInterval()
        {
            var bay = new DroneBay();
            bay.Sync(2, 1.0);
            var ready = new List<int>();
            int shots = 0;
            for (int i = 0; i < 1000; i++)   // 10 s in 0.01 s steps
            {
                bay.Tick(0.01, ready);
                foreach (int d in ready) { bay.Fired(d, 1.0); shots++; }
            }
            Assert.AreEqual(20, shots, 1);
        }

        [Test]
        public void Bay_DronesWithoutTargetWait_ButDontStackShots()
        {
            var bay = new DroneBay();
            bay.Sync(1, 1.0);
            var ready = new List<int>();
            bay.Tick(5.0, ready);   // a long time with nothing to shoot
            CollectionAssert.AreEqual(new[] { 0 }, ready);
            bay.Tick(1.0, ready);
            CollectionAssert.AreEqual(new[] { 0 }, ready, "still waiting for a target");
            bay.Fired(0, 1.0);
            bay.Tick(0.0, ready);
            CollectionAssert.IsEmpty(ready, "no burst of catch-up shots");
            bay.Tick(0.5, ready);
            CollectionAssert.AreEqual(new[] { 0 }, ready);
        }

        [Test]
        public void Bay_NewDronesAreStaggered()
        {
            var bay = new DroneBay();
            bay.Sync(3, 1.0);
            var ready = new List<int>();
            bay.Tick(0.3, ready);
            Assert.AreEqual(1, ready.Count, "only the first drone is ready after 0.3 s");
            bay.Sync(1, 1.0);
            Assert.AreEqual(1, bay.Count);
        }

        static readonly TargetInfo[] Targets =
        {
            new TargetInfo(0, 5, hp: 50, value: 10),   // near, healthy, cheap
            new TargetInfo(0, 9, hp: 5, value: 20),    // far, nearly broken
            new TargetInfo(6, 6, hp: 80, value: 300),  // rich
        };

        [TestCase(TargetRule.Nearest, 0)]
        [TestCase(TargetRule.Weakest, 1)]
        [TestCase(TargetRule.Richest, 2)]
        public void Targeting_PicksByRule(TargetRule rule, int expected)
        {
            Assert.AreEqual(expected, DroneTargeting.Pick(Targets, 0, 0, rule, new System.Random(1)));
        }

        [Test]
        public void Targeting_HandlesNoTargets_AndRandomStaysInRange()
        {
            Assert.AreEqual(-1, DroneTargeting.Pick(new TargetInfo[0], 0, 0, TargetRule.Nearest, new System.Random(1)));
            var rng = new System.Random(3);
            for (int i = 0; i < 100; i++) Assert.That(DroneTargeting.Pick(Targets, 0, 0, TargetRule.Random, rng), Is.InRange(0, 2));
            Assert.AreEqual(TargetRule.Richest, DroneTargeting.RuleForLevel(99));
        }

        [Test]
        public void Session_DronesFollowTheUpgrades()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Project/Data/GameDatabase.asset");
            var s = new GameSession(db, db.sectors.Single(x => x.index == 2), 1);
            s.Upgrades.SetLevel(UpgradeEffect.DrillDamage, 9);   // 10 damage
            s.Upgrades.SetLevel(UpgradeEffect.DroneCount, 3);
            s.Upgrades.SetLevel(UpgradeEffect.DroneDamage, 2);   // 150%
            var ready = new List<int>();
            s.TickDrones(0.01, ready);
            Assert.AreEqual(3, s.Drones.Count);
            Assert.AreEqual(1.5, s.DroneDamageFactor, 1e-9);
            var hit = s.RollDroneHit();
            Assert.AreEqual(15d, hit.crit ? hit.damage.Value / db.mining.critMultiplier : hit.damage.Value, 1e-9);
            Assert.IsTrue(s.HasIdleMining);
        }
    }

    public class MilestoneTests
    {
        const string DatabasePath = "Assets/_Project/Data/GameDatabase.asset";
        GameDatabase _db;
        SectorDefinition _s1;

        [SetUp]
        public void Setup()
        {
            _db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            _s1 = _db.sectors.Single(s => s.index == 1);
        }

        [Test]
        public void MineMilestone_CompletesOnce_PaysAndRaisesValue()
        {
            var s = new GameSession(_db, _s1, 1);
            var copper = _db.GetMineral("copper");
            var m = _db.milestones.First(x => x.goal == MilestoneGoal.MineMineral && x.mineral == copper);
            var fired = new List<MilestoneDefinition>();
            using var sub = s.Milestones.Completed.Subscribe(fired.Add);
            s.SetAutoSellBelow(1);

            for (int i = 0; i < m.target - 1; i++) s.TryCollect(new ChunkData(copper, 0, 0));
            Assert.IsFalse(s.Milestones.IsDone(m));
            Assert.AreEqual(m.target - 1, s.Milestones.Progress(m));

            s.TryCollect(new ChunkData(copper, 0, 0));
            s.TryCollect(new ChunkData(copper, 0, 0));
            CollectionAssert.AreEqual(new[] { m }, fired.Where(x => x == m).ToArray(), "fires once");
            Assert.AreEqual(m.rewardCredits, s.Wallet.Credits.Value, 1e-9);
            Assert.AreEqual(m.target, s.Milestones.Progress(m), "progress is capped at the target");

            // Same purity roll, now with the bonus.
            var rolled = MeteorRoller.RollChunk(copper, _db.purity, _s1, 0, false, new System.Random(5), s.Milestones.ValueMultiplier(copper));
            var plain = MeteorRoller.RollChunk(copper, _db.purity, _s1, 0, false, new System.Random(5));
            Assert.AreEqual(plain.value.Value * (1 + m.valueBonus), rolled.value.Value, 1e-6);
        }

        [Test]
        public void DepthMilestone_CompletesWhenTheDepthUnlocks()
        {
            var s = new GameSession(_db, _s1, 1);
            var m = _db.milestones.First(x => x.goal == MilestoneGoal.ReachDepth && x.sector == _s1);
            s.Depths.Unlock((int)m.target - 2, 0);   // one short
            int perDepth = s.Depths.MeteorsPerDepth;
            for (int i = 0; i < perDepth; i++) s.RegisterBreak(s.Depths.Deepest);
            Assert.AreEqual(m.target, s.Depths.Deepest + 1);
            Assert.IsTrue(s.Milestones.IsDone(m));
            Assert.Greater(s.Milestones.ValueMultiplier(_db.GetMineral("rock")), 1.0, "an all-minerals bonus");
        }
    }

    /// <summary>The Phase 6 "Done when" checks, run on the real data with the simulator.</summary>
    public class Phase6PacingTests
    {
        const string DatabasePath = "Assets/_Project/Data/GameDatabase.asset";
        GameDatabase _db;

        [SetUp]
        public void Setup() => _db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);

        /// <summary>Reaching Sector 2 takes about 10 minutes for an active player.</summary>
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ReachingSector2_TakesAbout10Minutes(int seed)
        {
            var r = EconomySimulator.Run(_db, _db.sectors.Single(s => s.index == 1),
                new EconomySimulator.Settings { seed = seed, duration = 1200 });
            Assert.That(r.FirstJumpTime / 60, Is.InRange(7.0, 14.0), $"jumped at {r.FirstJumpTime / 60:0.0} min");
            Assert.AreEqual("iron_belt", r.jumps[0].sectorId);
        }

        /// <summary>With drones and no input at all, the game keeps buying upgrades.</summary>
        [Test]
        public void IdleWithDrones_ProgressesSteadily()
        {
            var s2 = _db.sectors.Single(s => s.index == 2);
            var session = new GameSession(_db, s2, 1);
            // Roughly the ship of a player who just jumped (see the 10-minute simulation).
            session.Upgrades.SetLevel(UpgradeEffect.DrillDamage, 22);
            session.Upgrades.SetLevel(UpgradeEffect.AutoFireInterval, 11);
            session.Upgrades.SetLevel(UpgradeEffect.CritChance, 6);
            session.Upgrades.SetLevel(UpgradeEffect.CollectorRadius, 15);
            session.Upgrades.SetLevel(UpgradeEffect.CargoCapacity, 11);
            session.Upgrades.SetLevel(UpgradeEffect.DroneCount, 1);

            var r = EconomySimulator.Run(session, new EconomySimulator.Settings { duration = 900, tapsPerSecond = 0, buyJumpDrive = false });
            Assert.GreaterOrEqual(r.purchases.Count, 8, "keeps buying with no input");
            Assert.Less(r.Gaps(0, 900).Max(), 240, "no gap over 4 minutes");
            Assert.Greater(session.DroneCount, 1, "buys more drones");
            Assert.Greater(session.Depths.Deepest, 0, "goes deeper on its own");
        }
    }
}
