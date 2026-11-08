using NUnit.Framework;
using VoidProspector.Core;

namespace VoidProspector.Tests
{
    public class GameNumberTests
    {
        [Test]
        public void Arithmetic()
        {
            GameNumber a = 10, b = 4;
            Assert.AreEqual(14, (a + b).Value);
            Assert.AreEqual(6, (a - b).Value);
            Assert.AreEqual(40, (a * b).Value);
            Assert.AreEqual(2.5, (a / b).Value);
            Assert.AreEqual(-10, (-a).Value);
        }

        [Test]
        public void Comparisons()
        {
            GameNumber a = 1.5, b = 2;
            Assert.IsTrue(a < b);
            Assert.IsTrue(b > a);
            Assert.IsTrue(a <= 1.5);
            Assert.IsTrue(b >= 2);
            Assert.IsTrue(a == new GameNumber(1.5));
            Assert.IsTrue(a != b);
            Assert.AreEqual(-1, a.CompareTo(b));
        }

        [Test]
        public void Helpers()
        {
            Assert.AreEqual(8, GameNumber.Pow(2, 3).Value);
            Assert.AreEqual(2, GameNumber.Floor(2.9).Value);
            Assert.AreEqual(3, GameNumber.Ceil(2.1).Value);
            Assert.AreEqual(3, GameNumber.Sqrt(9).Value);
            Assert.AreEqual(5, GameNumber.Max(5, 3).Value);
            Assert.AreEqual(3, GameNumber.Min(5, 3).Value);
        }

        [Test]
        public void ImplicitConversions()
        {
            GameNumber fromInt = 7;
            GameNumber fromDouble = 7.25;
            Assert.AreEqual(7, fromInt.Value);
            Assert.AreEqual(7.25, (double)fromDouble);
            Assert.AreEqual(GameNumber.Zero, new GameNumber());
        }
    }
}
