using System;

namespace ConstructionEmpire.Utilities
{
    /// <summary>
    /// Thin wrapper around System.Random that remembers its seed, so
    /// systems that need randomness later (bidding, events, market
    /// fluctuations...) can be reproduced deterministically for tests and
    /// for reasoning about a specific save (spec section 72, Rule 9).
    /// </summary>
    public class SeededRandom
    {
        public int Seed { get; }
        private readonly Random _random;

        public SeededRandom(int seed)
        {
            Seed = seed;
            _random = new Random(seed);
        }

        public int NextInt(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
        public double NextDouble() => _random.NextDouble();
        public float NextFloat(float minInclusive, float maxInclusive) => (float)(minInclusive + _random.NextDouble() * (maxInclusive - minInclusive));
        public bool NextBool(float trueChance = 0.5f) => _random.NextDouble() < trueChance;
    }
}
