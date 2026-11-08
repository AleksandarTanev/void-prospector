using System;
using NUnit.Framework;
using VoidProspector.Core;

namespace VoidProspector.Tests
{
    public class FormulasTests
    {
        [TestCase(10, 1.15, 0, 10)]
        [TestCase(10, 1.15, 1, 12)]      // 11.5 → ceil 12
        [TestCase(10, 1.15, 10, 41)]     // 40.455… → 41
        [TestCase(50, 1.15, 5, 101)]     // 100.567… → 101
        [TestCase(200, 1.6, 3, 820)]     // 819.2 → 820
        public void UpgradeCost_IsCeilOfBaseTimesGrowthToTheLevel(double baseCost, double growth, int level, double expected)
        {
            Assert.AreEqual(expected, Formulas.UpgradeCost(baseCost, growth, level).Value);
        }

        [Test]
        public void UpgradeCost_MatchesBaseTimesGrowthToTheLevel_ForManyLevels()
        {
            for (int level = 0; level <= 100; level++)
            {
                double expected = Math.Ceiling(25 * Math.Pow(1.07, level));
                Assert.AreEqual(expected, Formulas.UpgradeCost(25, 1.07, level).Value, 1e-6, $"level {level}");
            }
        }

        [Test]
        public void UpgradeCost_NegativeLevel_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Formulas.UpgradeCost(10, 1.15, -1));
        }

        [Test]
        public void ChunkValue_AppliesPurityDepthCodexAndPrestige()
        {
            // 5 × 15 × (1 + 0.15 × 4) × 1.1 × 2 = 264
            Assert.AreEqual(264, Formulas.ChunkValue(5, 15, 4, 0.15, 1.1, 2).Value, 1e-9);
        }

        [Test]
        public void ChunkValue_AtDepthZeroWithDefaults_IsBaseTimesPurity()
        {
            Assert.AreEqual(50, Formulas.ChunkValue(25, 2, 0).Value, 1e-9);
        }

        [Test]
        public void MeteorHp_ScalesWithDepthSizeAndType()
        {
            // 10 × 1.18² × 3 × 1.5 = 62.658
            Assert.AreEqual(62.658, Formulas.MeteorHp(10, 2, 3, 1.5).Value, 1e-9);
        }

        [TestCase(0, 0, 0)]
        [TestCase(999_999, 0, 0)]
        [TestCase(1_000_000, 0, 1)]
        [TestCase(4_000_000, 0, 2)]
        [TestCase(99_000_000, 3, 12)]   // floor(√99) = 9, + 3
        public void Stardust_IsFloorOfSqrtMillionsPlusBonus(double credits, double bonus, double expected)
        {
            Assert.AreEqual(expected, Formulas.Stardust(credits, bonus).Value);
        }

        [Test]
        public void UpgradeEffect_Add()
        {
            Assert.AreEqual(4.5 + 0.6 * 3, Formulas.UpgradeEffect(4.5, 0.6, 3, EffectMode.Add), 1e-9);
        }

        [Test]
        public void UpgradeEffect_Multiply()
        {
            Assert.AreEqual(1.2 * Math.Pow(0.88, 4), Formulas.UpgradeEffect(1.2, 0.88, 4, EffectMode.Multiply), 1e-9);
        }
    }
}
