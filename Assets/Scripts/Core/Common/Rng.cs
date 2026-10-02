using System;
using System.Collections.Generic;

namespace FiveKingdoms.Core
{
    /// <summary>
    /// Deterministic PRNG (PCG32). The same seed gives the same sequence on every platform and .NET runtime,
    /// so floors and simulated runs are reproducible. Not for anything security-related.
    /// </summary>
    public sealed class Rng
    {
        const ulong Multiplier = 6364136223846793005UL;
        const ulong DefaultStream = 0xDA3E39CB94B95BDBUL;

        ulong state;
        readonly ulong increment;

        public Rng(int seed)
        {
            increment = (DefaultStream << 1) | 1UL;
            NextUInt();
            state += (uint)seed;
            NextUInt();
        }

        public uint NextUInt()
        {
            ulong old = state;
            state = unchecked(old * Multiplier + increment);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorShifted >> rot) | (xorShifted << (-rot & 31));
        }

        /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentException($"Empty range [{minInclusive}, {maxExclusive})");
            ulong span = (ulong)((long)maxExclusive - minInclusive);
            return (int)(minInclusive + (long)((NextUInt() * span) >> 32));
        }

        /// <summary>True with the given chance, in percent (0-100).</summary>
        public bool Chance(int percent) => Range(0, 100) < percent;

        public T Pick<T>(IReadOnlyList<T> items) => items[Range(0, items.Count)];

        public void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }

        /// <summary>Derives an independent seed from a base seed and a salt such as a floor number.</summary>
        public static int DeriveSeed(int seed, int salt)
        {
            unchecked
            {
                ulong z = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL + (ulong)(uint)salt * 0xBF58476D1CE4E5B9UL + 0x94D049BB133111EBUL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return (int)(uint)(z ^ (z >> 31));
            }
        }
    }
}
