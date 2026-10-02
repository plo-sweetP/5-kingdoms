using FiveKingdoms.Core;
using NUnit.Framework;

namespace FiveKingdoms.Tests
{
    public class RngTests
    {
        [Test]
        public void SameSeedGivesSameSequence()
        {
            var a = new Rng(42);
            var b = new Rng(42);
            for (int i = 0; i < 100; i++) Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void RangeStaysInBoundsAndCoversEveryValue()
        {
            var rng = new Rng(1);
            var seen = new bool[4];
            for (int i = 0; i < 10000; i++)
            {
                int value = rng.Range(3, 7);
                Assert.That(value, Is.InRange(3, 6));
                seen[value - 3] = true;
            }
            CollectionAssert.AreEqual(new[] { true, true, true, true }, seen);
        }

        [Test]
        public void DerivedSeedsDifferBySeedAndSalt()
        {
            Assert.AreNotEqual(Rng.DeriveSeed(5, 1), Rng.DeriveSeed(5, 2));
            Assert.AreNotEqual(Rng.DeriveSeed(5, 1), Rng.DeriveSeed(6, 1));
        }
    }
}
