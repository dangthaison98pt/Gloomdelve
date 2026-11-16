using System.Collections.Generic;

namespace Gloomdelve
{
    /// <summary>Deterministic xorshift RNG (same results on every platform), used for level generation.</summary>
    public sealed class Rng
    {
        ulong s;

        public Rng(int seed)
        {
            s = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL;
            if (s == 0) s = 1;
            NextU(); NextU();
        }

        public ulong NextU()
        {
            s ^= s << 13; s ^= s >> 7; s ^= s << 17;
            return s;
        }

        public int Next() => (int)(NextU() >> 33);

        /// <summary>Integer in [min, maxExclusive).</summary>
        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            return min + (int)(NextU() % (ulong)(maxExclusive - min));
        }

        public float Value() => (NextU() >> 40) / (float)(1UL << 24);
        public float Range(float a, float b) => a + (b - a) * Value();
        public bool Chance(float p) => p > 0f && Value() < p;
        public T Pick<T>(IList<T> list) => list[Range(0, list.Count)];

        public static int Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)a * 0x85EBCA6Bu ^ ((uint)b + 0x9E3779B9u) * 0xC2B2AE35u;
                h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
                return (int)(h & 0x7FFFFFFF);
            }
        }

        public static int Hash(int a, int b, int c) => Hash(Hash(a, b), c);
    }
}
