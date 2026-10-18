using System;

namespace ConstructionEmpire.Systems.Randomness
{
    /// <summary>
    /// Share one instance across simulation systems and call it on the simulation thread.
    /// Replay requires the same seed, ordered calls, inputs, and System.Random implementation.
    /// The seed restarts a sequence; it is not a snapshot of a partly consumed sequence.
    /// </summary>
    public sealed class SeededRandom
    {
        private readonly Random random;

        public int Seed { get; }
        public long Calls {get;private set;}

        public SeededRandom(int seed)
        {
            Seed = seed;
            random = new Random(seed);
        }

        public int Next(){Calls++;return random.Next();}

        /// <summary>Returns an integer in [0, maxExclusive).</summary>
        public int Next(int maxExclusive){Calls++;return random.Next(maxExclusive);}

        /// <summary>Returns an integer in [minInclusive, maxExclusive).</summary>
        public int Next(int minInclusive,int maxExclusive){Calls++;return random.Next(minInclusive,maxExclusive);}

        /// <summary>Returns a double in [0, 1).</summary>
        public double NextDouble(){Calls++;return random.NextDouble();}

        public void NextBytes(byte[] buffer){Calls++;random.NextBytes(buffer);}
    }
}
