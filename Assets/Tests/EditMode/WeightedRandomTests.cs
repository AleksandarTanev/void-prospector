using System;
using NUnit.Framework;
using VoidProspector.Core;

namespace VoidProspector.Tests
{
    public class WeightedRandomTests
    {
        [Test]
        public void ZeroWeightItems_AreNeverPicked()
        {
            var weights = new[] { 0f, 5f, 0f, -2f };
            var rng = new Random(42);
            for (int i = 0; i < 10_000; i++)
                Assert.AreEqual(1, WeightedRandom.PickIndex(weights, w => w, rng));
        }

        [Test]
        public void NoPositiveWeight_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, WeightedRandom.PickIndex(new[] { 0f, 0f }, w => w, new Random(1)));
            Assert.AreEqual(-1, WeightedRandom.PickIndex(Array.Empty<float>(), w => w, new Random(1)));
        }

        [Test]
        public void Distribution_MatchesWeights()
        {
            // Same weights as the purity grades.
            var weights = new[] { 60f, 28f, 9f, 2.7f, 0.3f };
            var counts = new int[weights.Length];
            var rng = new Random(12345);
            const int rolls = 200_000;
            for (int i = 0; i < rolls; i++) counts[WeightedRandom.PickIndex(weights, w => w, rng)]++;

            for (int i = 0; i < weights.Length; i++)
            {
                double expected = weights[i] / 100.0;
                double actual = (double)counts[i] / rolls;
                Assert.AreEqual(expected, actual, 0.015, $"index {i}");
            }
            Assert.Greater(counts[4], 0, "the 0.3% grade should still appear");
        }

        [Test]
        public void SameSeed_GivesSameSequence()
        {
            var weights = new[] { 1f, 2f, 3f };
            var a = new Random(7);
            var b = new Random(7);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(WeightedRandom.PickIndex(weights, w => w, a), WeightedRandom.PickIndex(weights, w => w, b));
        }
    }
}
