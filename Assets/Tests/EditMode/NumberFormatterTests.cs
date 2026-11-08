using NUnit.Framework;
using VoidProspector.Core;

namespace VoidProspector.Tests
{
    public class NumberFormatterTests
    {
        [TestCase(0, "0")]
        [TestCase(0.6, "0.6")]
        [TestCase(7.5, "7.5")]
        [TestCase(7.99, "7.9")]          // truncated, never rounded up
        [TestCase(10, "10")]
        [TestCase(999, "999")]
        [TestCase(999.99, "999")]
        [TestCase(1000, "1.00K")]
        [TestCase(1234, "1.23K")]
        [TestCase(1239, "1.23K")]
        [TestCase(45_600, "45.6K")]
        [TestCase(789_000, "789K")]
        [TestCase(999_999, "999K")]
        [TestCase(1_000_000, "1.00M")]
        [TestCase(4_560_000, "4.56M")]
        [TestCase(7_890_000_000, "7.89B")]
        [TestCase(1.5e12, "1.50T")]
        [TestCase(2e15, "2.00Qa")]
        [TestCase(3e18, "3.00Qi")]
        [TestCase(1.23e33, "1.23Dc")]
        [TestCase(9.99e35, "999Dc")]
        [TestCase(1.23e36, "1.23e36")]
        [TestCase(4.5e100, "4.50e100")]
        [TestCase(-1234, "-1.23K")]
        public void Format(double value, string expected)
        {
            Assert.AreEqual(expected, NumberFormatter.Format(value));
        }

        [Test]
        public void GameNumber_ToString_UsesFormatter()
        {
            Assert.AreEqual("4.56M", new GameNumber(4_560_000).ToString());
        }
    }
}
