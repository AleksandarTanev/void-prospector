using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using VoidProspector.Data;

namespace VoidProspector.Tests
{
    /// <summary>Checks the real data assets, so a bad edit in the Inspector fails a test.</summary>
    public class DataValidationTests
    {
        const string DatabasePath = "Assets/_Project/Data/GameDatabase.asset";

        GameDatabase _db;

        [SetUp]
        public void LoadDatabase()
        {
            _db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            Assert.IsNotNull(_db, $"GameDatabase not found at {DatabasePath}");
        }

        [Test]
        public void Database_HasNoValidationErrors()
        {
            var errors = new List<string>();
            bool ok = _db.Validate(errors);
            Assert.IsTrue(ok, "Validation errors:\n" + string.Join("\n", errors));
        }

        [Test]
        public void Sector1_ContainsRockCopperTinOnly()
        {
            var sector = _db.sectors.Single(s => s.index == 1);
            var minerals = sector.meteorTypes
                .SelectMany(t => t.type.composition)
                .Where(c => c.weight > 0)
                .Select(c => c.mineral.id)
                .Distinct()
                .OrderBy(id => id)
                .ToArray();
            CollectionAssert.AreEqual(new[] { "copper", "rock", "tin" }, minerals);
        }

        [Test]
        public void PurityWeights_SumTo100()
        {
            float sum = _db.purity.grades.Sum(g => g.dropWeight);
            Assert.AreEqual(100f, sum, 0.001f);
        }

        [Test]
        public void AllFiveMeteorSizes_Exist()
        {
            CollectionAssert.AreEquivalent(
                new[] { "pebble", "small", "medium", "large", "colossal" },
                _db.meteorSizes.Select(s => s.id).ToArray());
        }

        [Test]
        public void UpgradeCosts_FollowTheFormula()
        {
            foreach (var u in _db.upgrades)
                for (int level = 0; level < 20; level++)
                    Assert.AreEqual(System.Math.Ceiling(u.baseCost * System.Math.Pow(u.growth, level)), u.CostAt(level).Value, 1e-6, $"{u.id} level {level}");
        }

        [Test]
        public void SeededRolls_StayWithinSector1()
        {
            var rng = new System.Random(99);
            var sector = _db.sectors.Single(s => s.index == 1);
            for (int i = 0; i < 1000; i++)
            {
                var type = sector.RollMeteorType(rng);
                Assert.IsNotNull(type);
                var mineral = type.RollMineral(rng);
                Assert.IsNotNull(mineral);
                var size = _db.RollMeteorSize(rng);
                Assert.IsNotNull(size);
                int chunks = size.RollChunks(rng);
                Assert.That(chunks, Is.InRange(size.chunkMin, size.chunkMax));
                int grade = _db.purity.Roll(rng);
                Assert.That(grade, Is.InRange(0, _db.purity.TopGrade));
            }
        }
    }
}
