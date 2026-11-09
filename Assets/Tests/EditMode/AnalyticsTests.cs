using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using VoidProspector.Analytics;
using VoidProspector.Core;
using VoidProspector.Data;
using VoidProspector.Economy;
using VoidProspector.Mining;

namespace VoidProspector.Tests
{
    public class AnalyticsEventTests
    {
        [Test]
        public void ToJson_WritesOneEscapedObject()
        {
            var e = new AnalyticsEvent("test", new DateTime(2026, 11, 9, 12, 30, 0, DateTimeKind.Utc))
                .Add("text", "a \"quote\"\nline")
                .Add("count", 3)
                .Add("value", 1.5)
                .Add("flag", true);
            Assert.AreEqual("{\"event\":\"test\",\"utc\":\"2026-11-09T12:30:00.000Z\",\"text\":\"a \\\"quote\\\"\\nline\",\"count\":3,\"value\":1.5,\"flag\":true}", e.ToJson());
            Assert.AreEqual(3L, e.Get("count"));
        }

        [Test]
        public void JsonLinesSink_AppendsOneLinePerEvent()
        {
            string dir = Path.Combine(Path.GetTempPath(), "vp-analytics-" + Guid.NewGuid().ToString("N"));
            try
            {
                var sink = new JsonLinesSink(Path.Combine(dir, "a.jsonl"));
                sink.Send(new AnalyticsEvent("one", DateTime.UtcNow));
                sink.Send(new AnalyticsEvent("two", DateTime.UtcNow));
                Assert.IsFalse(File.Exists(sink.FilePath), "buffered until flushed");
                sink.Flush();
                sink.Send(new AnalyticsEvent("three", DateTime.UtcNow));
                sink.Flush();
                var lines = File.ReadAllLines(sink.FilePath);
                Assert.AreEqual(3, lines.Length);
                StringAssert.Contains("\"event\":\"three\"", lines[2]);
                Assert.AreEqual(3, sink.Written);
                Assert.IsNull(sink.Error);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }

    public class GameAnalyticsTests
    {
        GameDatabase _db;
        GameSession _s;
        MemorySink _sink;
        GameAnalytics _a;

        [SetUp]
        public void Setup()
        {
            _db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Project/Data/GameDatabase.asset");
            _s = new GameSession(_db, _db.sectors.Single(x => x.index == 1), 1);
            _sink = new MemorySink();
            _a = new GameAnalytics(_s, _sink, () => new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc));
        }

        [TearDown]
        public void Teardown() => _a.Dispose();

        AnalyticsEvent[] Named(string name) => _sink.events.Where(e => e.Name == name).ToArray();

        [Test]
        public void Purchases_AreLogged_ButRestoredLevelsAreNot()
        {
            var drill = _s.Upgrades.Get(UpgradeEffect.DrillDamage);
            _s.Upgrades.SetLevel(UpgradeEffect.CargoCapacity, 3);   // like loading a save
            _s.Wallet.Add(drill.NextCost);
            _s.TryBuy(drill, BuyMode.One);
            var bought = Named("upgrade_bought");
            Assert.AreEqual(1, bought.Length);
            Assert.AreEqual("drill_damage", bought[0].Get("id"));
            Assert.AreEqual(1L, bought[0].Get("level"));
            Assert.AreEqual("debris_ring", bought[0].Get("sector"));
        }

        [Test]
        public void Sessions_RecordLengthAndWhereThePlayerStopped()
        {
            _a.StartSession("launch");
            Assert.AreEqual(1, _s.Stats.Sessions);
            _s.Stats.Tick(100);
            var drill = _s.Upgrades.Get(UpgradeEffect.DrillDamage);
            _s.Wallet.Add(drill.NextCost);
            _s.TryBuy(drill, BuyMode.One);
            _s.Stats.Tick(40);
            _a.EndSession("pause");
            _a.EndSession("quit");   // already ended: ignored

            var end = Named("session_end").Single();
            Assert.AreEqual("pause", end.Get("reason"));
            Assert.AreEqual(140.0, (double)end.Get("session_s"), 1e-6);
            Assert.AreEqual(40.0, (double)end.Get("since_purchase_s"), 1e-6);
            Assert.AreEqual("drill_damage", end.Get("last_upgrade"));

            _a.StartSession("resume");
            Assert.AreEqual(1, _s.Stats.Sessions, "a resume isn't a new launch");
            Assert.AreEqual(2, Named("session_start").Length);
        }

        [Test]
        public void Progress_IsLogged()
        {
            _s.Depths.Unlock(1, 0);
            _s.DebugUnlockNextSector();
            var copper = _db.GetMineral("copper");
            _s.TryCollect(new ChunkData(copper, 1, 5));
            Assert.AreEqual(2L, Named("depth_unlocked").Single().Get("unlocked_depth"));
            Assert.AreEqual(2L, Named("sector_unlocked").Single().Get("sector_index"));
            Assert.IsTrue(Named("codex_entry").Any(e => (string)e.Get("mineral") == "copper"));
        }

        [Test]
        public void PlayStats_SurviveASave()
        {
            _a.StartSession("launch");
            _s.Stats.Tick(321.5);
            var b = new GameSession(_db, _s.Sector, 1);
            SaveSystem.Apply(b, SaveSystem.FromJson(SaveSystem.ToJson(SaveSystem.Capture(_s, DateTime.UtcNow)), out _));
            Assert.AreEqual(321.5, b.Stats.PlaySeconds, 1e-9);
            Assert.AreEqual(1, b.Stats.Sessions);
            Assert.AreEqual(_s.Stats.FirstPlayUtc, b.Stats.FirstPlayUtc);
        }
    }

    /// <summary>Phase 8 tuning checks on the whole first hour (simulated active player).</summary>
    public class Phase8PacingTests
    {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void FirstHour_HasNoWalls_AndHitsTheSectorTargets(int seed)
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Project/Data/GameDatabase.asset");
            var r = EconomySimulator.Run(db, db.sectors.Single(s => s.index == 1), new EconomySimulator.Settings { seed = seed, duration = 3600 });
            Assert.Less(r.LongestGap(0, 3600), 600, "GDD: a wall is >10 min to the next upgrade\n" + r.PacingSummary());
            Assert.AreEqual(2, r.jumps.Count, r.PacingSummary());
            Assert.That(r.jumps[0].time / 60, Is.InRange(8.0, 13.0), "Sector 2 (~10 min)\n" + r.PacingSummary());
            Assert.That(r.jumps[1].time / 60, Is.InRange(24.0, 40.0), "Sector 3 (~30 min)\n" + r.PacingSummary());
        }
    }
}
