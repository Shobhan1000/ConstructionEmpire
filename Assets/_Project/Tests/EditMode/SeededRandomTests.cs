using ConstructionEmpire.Systems.Randomness;
using NUnit.Framework;

namespace ConstructionEmpire.Tests.EditMode
{
    public class SeededRandomTests
    {
        [TestCase(0)]
        [TestCase(74219)]
        [TestCase(-13)]
        public void SameSeedAndOrderedCalls_ReplayTheSameMixedSequence(int savedSeed)
        {
            var original = new SeededRandom(savedSeed);
            var replay = new SeededRandom(savedSeed);
            Assert.That(replay.Seed, Is.EqualTo(savedSeed));
            for (int i = 0; i < 128; i++)
            {
                Assert.That(replay.Next(), Is.EqualTo(original.Next()));
                Assert.That(replay.Next(100), Is.EqualTo(original.Next(100)));
                Assert.That(replay.Next(-10, 10), Is.EqualTo(original.Next(-10, 10)));
                Assert.That(replay.NextDouble(), Is.EqualTo(original.NextDouble()));
                var expectedBytes = new byte[7];
                var actualBytes = new byte[7];
                original.NextBytes(expectedBytes);
                replay.NextBytes(actualBytes);
                Assert.That(actualBytes, Is.EqualTo(expectedBytes));
            }
        }

        [Test]
        public void Values_RespectExclusiveUpperBounds()
        {
            var random = new SeededRandom(42);
            for (int i = 0; i < 256; i++)
            {
                Assert.That(random.Next(3), Is.InRange(0, 2));
                Assert.That(random.Next(-5, 5), Is.InRange(-5, 4));
                Assert.That(random.NextDouble(), Is.GreaterThanOrEqualTo(0d).And.LessThan(1d));
            }
        }
    }
}
