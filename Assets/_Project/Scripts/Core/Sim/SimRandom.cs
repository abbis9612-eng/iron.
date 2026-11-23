using System;

namespace IronHorizon.Core.Sim
{
    /// <summary>Complete generator state, for saves and state hashes.</summary>
    public readonly struct SimRandomState : IEquatable<SimRandomState>
    {
        public readonly uint S0;
        public readonly uint S1;
        public readonly uint S2;
        public readonly uint S3;

        public SimRandomState(uint s0, uint s1, uint s2, uint s3)
        {
            S0 = s0;
            S1 = s1;
            S2 = s2;
            S3 = s3;
        }

        public bool Equals(SimRandomState other)
        {
            return S0 == other.S0 && S1 == other.S1 && S2 == other.S2 && S3 == other.S3;
        }

        public override bool Equals(object obj)
        {
            return obj is SimRandomState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (int)(S0 ^ (S1 * 31) ^ (S2 * 961) ^ (S3 * 29791));
            }
        }
    }

    /// <summary>
    /// The only source of randomness inside the simulation (GDD section 0, rule 6).
    /// xoshiro128** seeded through SplitMix64: integer arithmetic only, so it gives the same sequence
    /// on every CPU and platform. Never use UnityEngine.Random or System.Random in the simulation.
    /// </summary>
    public sealed class SimRandom
    {
        private uint _s0;
        private uint _s1;
        private uint _s2;
        private uint _s3;

        public SimRandom(ulong seed)
        {
            Reseed(seed);
        }

        public SimRandomState State
        {
            get => new SimRandomState(_s0, _s1, _s2, _s3);
            set
            {
                if ((value.S0 | value.S1 | value.S2 | value.S3) == 0)
                {
                    throw new ArgumentException("An all-zero state is invalid for xoshiro128**.");
                }

                _s0 = value.S0;
                _s1 = value.S1;
                _s2 = value.S2;
                _s3 = value.S3;
            }
        }

        public void Reseed(ulong seed)
        {
            ulong x = seed;
            ulong a = SplitMix64(ref x);
            ulong b = SplitMix64(ref x);
            _s0 = (uint)a;
            _s1 = (uint)(a >> 32);
            _s2 = (uint)b;
            _s3 = (uint)(b >> 32);
            if ((_s0 | _s1 | _s2 | _s3) == 0)
            {
                _s0 = 1;
            }
        }

        /// <summary>Uniform 32-bit value.</summary>
        public uint NextUInt()
        {
            unchecked
            {
                uint result = RotateLeft(_s1 * 5, 7) * 9;
                uint t = _s1 << 9;
                _s2 ^= _s0;
                _s3 ^= _s1;
                _s1 ^= _s2;
                _s0 ^= _s3;
                _s2 ^= t;
                _s3 = RotateLeft(_s3, 11);
                return result;
            }
        }

        /// <summary>Uniform value in [0, maxExclusive), without modulo bias.</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Must be positive.");
            }

            return (int)NextUIntBelow((uint)maxExclusive);
        }

        /// <summary>Uniform value in [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Must be greater than minInclusive.");
            }

            long span = (long)maxExclusive - minInclusive;
            return (int)(minInclusive + (long)NextUIntBelow((uint)span));
        }

        /// <summary>True with probability numerator / denominator, e.g. Chance(25, 100) for 25%.</summary>
        public bool Chance(int numerator, int denominator)
        {
            if (denominator <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(denominator), "Must be positive.");
            }

            if (numerator < 0 || numerator > denominator)
            {
                throw new ArgumentOutOfRangeException(nameof(numerator), "Must be between 0 and denominator.");
            }

            return NextUIntBelow((uint)denominator) < (uint)numerator;
        }

        // Lemire's multiply-shift with rejection: unbiased and branch-light.
        private uint NextUIntBelow(uint range)
        {
            unchecked
            {
                ulong m = (ulong)NextUInt() * range;
                uint low = (uint)m;
                if (low < range)
                {
                    uint threshold = (0u - range) % range;
                    while (low < threshold)
                    {
                        m = (ulong)NextUInt() * range;
                        low = (uint)m;
                    }
                }

                return (uint)(m >> 32);
            }
        }

        private static ulong SplitMix64(ref ulong x)
        {
            unchecked
            {
                x += 0x9E3779B97F4A7C15UL;
                ulong z = x;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        private static uint RotateLeft(uint value, int bits)
        {
            return (value << bits) | (value >> (32 - bits));
        }
    }
}
