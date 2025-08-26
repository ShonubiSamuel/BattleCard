// DeterministicRng.cs
// A portable, reproducible RNG based on PCG32 (XSH-RR).
// - Deterministic across platforms/Unity/.NET versions
// - Seed + sequence (stream) support for independent substreams
// - Save/Restore snapshots for replays and netcode
// - Utilities: ints, floats, doubles, bools, bytes, Gaussian, shuffle, skip/advance, fork
//
// Not thread-safe by design; wrap in your own lock if used from multiple threads.

using System;
using System.Collections.Generic;

namespace YGO.Duel.Foundation
{
    /// <summary>
    /// Deterministic RNG using PCG32. Suitable for replays, networking lockstep, and shuffles.
    /// </summary>
    public sealed class DeterministicRng
    {
        // PCG 64-bit LCG state: state = state * MULT + inc (mod 2^64), with odd 'inc'
        // Output function: XSH RR variant → 32-bit output
        private const ulong MULT = 6364136223846793005UL;

        private ulong _state;
        private ulong _inc; // must be odd

        /// <summary>
        /// Create RNG with a 32-bit seed. Uses a default stream sequence.
        /// </summary>
        public DeterministicRng(int seed) : this(unchecked((ulong)(uint)seed), 0x9E3779B97F4A7C15UL) { }

        /// <summary>
        /// Create RNG with full 64-bit seed and optional 64-bit stream id. Streams are independent if 'seq' differs.
        /// </summary>
        /// <param name="seed">Initial seed (state entropy).</param>
        /// <param name="seq">Stream id (sequence). Will be forced to odd internally.</param>
        public DeterministicRng(ulong seed, ulong seq)
        {
            Reseed(seed, seq);
        }

        /// <summary>
        /// Reseed the generator (resets the sequence). Safe to call anytime.
        /// </summary>
        public void Reseed(ulong seed, ulong seq)
        {
            _state = 0UL;
            _inc = (seq << 1) | 1UL;   // sequence selector (must be odd)
            Step();                     // mix inc into state path
            _state += seed;
            Step();
        }

        /// <summary>
        /// A snapshot you can serialize to resume exact RNG state later (replays, rollback).
        /// </summary>
        [Serializable]
        public struct Snapshot
        {
            public ulong State;
            public ulong Inc;
            public int Version; // for future-proofing
        }

        public Snapshot Capture() => new Snapshot { State = _state, Inc = _inc, Version = 1 };

        public void Restore(Snapshot s)
        {
            _state = s.State;
            _inc = s.Inc;
        }

        /// <summary>
        /// Fork a new RNG that shares no correlation by using a different stream id. Seed is derived from current state.
        /// </summary>
        public DeterministicRng Fork(ulong streamId)
        {
            // derive a new seed from current state, keep independent stream via streamId
            ulong newSeed = Mix64(_state ^ 0xD1342543DE82EF95UL);
            return new DeterministicRng(newSeed, streamId);
        }

        // ----------------------------- Core PCG32 -----------------------------

        /// <summary>
        /// Advance internal state once and return next 32-bit value.
        /// </summary>
        public uint NextUInt()
        {
            ulong old = _state;
            _state = unchecked(old * MULT + _inc);

            // XSH RR output transformation
            uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
        }

        /// <summary>
        /// Advance the underlying LCG by 'delta' steps in O(log n).
        /// Useful for skipping ahead/rolling back deterministically.
        /// </summary>
        public void Advance(ulong delta)
        {
            ulong curMult = MULT;
            ulong curPlus = _inc;
            ulong accMult = 1UL;
            ulong accPlus = 0UL;

            ulong d = delta;
            while (d > 0)
            {
                if ((d & 1UL) != 0)
                {
                    accMult = unchecked(accMult * curMult);
                    accPlus = unchecked(accPlus * curMult + curPlus);
                }
                curPlus = unchecked((curMult + 1UL) * curPlus);
                curMult = unchecked(curMult * curMult);
                d >>= 1;
            }
            _state = unchecked(accMult * _state + accPlus);
        }

        private void Step() => _state = unchecked(_state * MULT + _inc);

        private static ulong Mix64(ulong x)
        {
            // SplitMix64 mixing function for deriving seeds
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            return x ^ (x >> 31);
        }

        // ----------------------------- Convenience APIs -----------------------------

        /// <summary>
        /// Next integer in [minInclusive, maxExclusive). Throws if range invalid.
        /// </summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be > minInclusive");

            uint range = (uint)(maxExclusive - minInclusive);
            // Avoid modulo bias via rejection sampling
            uint limit = (uint)(uint.MaxValue - (uint.MaxValue % range));
            uint r;
            do { r = NextUInt(); } while (r >= limit);
            return (int)(r % range) + minInclusive;
        }

        /// <summary>Next non-negative int less than maxExclusive.</summary>
        public int NextInt(int maxExclusive) => NextInt(0, maxExclusive);

        /// <summary>Uniform double in [0,1).</summary>
        public double NextDouble()
        {
            // 53-bit precision
            ulong hi = NextUInt();
            ulong lo = NextUInt();
            ulong bits = ((hi & 0x001FFFFFUL) << 32) | lo; // 21 + 32 = 53 bits
            return bits / 9007199254740992.0; // 2^53
        }

        /// <summary>Uniform float in [0,1).</summary>
        public float NextFloat()
        {
            // Use top 24 bits → exact float mantissa coverage
            return (NextUInt() >> 8) * (1.0f / 16777216.0f); // 2^24
        }

        /// <summary>Bernoulli trial: true with probability p (0..1).</summary>
        public bool NextBool(float p = 0.5f)
        {
            if (p <= 0f) return false;
            if (p >= 1f) return true;
            return NextFloat() < p;
        }

        /// <summary>Return either -1 or +1 with equal probability (or thresholded by p for +1).</summary>
        public int NextSign(float pPlus = 0.5f) => NextBool(pPlus) ? +1 : -1;

        /// <summary>Gaussian/normal value using Box–Muller transform.</summary>
        public double NextGaussian(double mean = 0.0, double stdDev = 1.0)
        {
            // Two uniforms in (0,1]; avoid log(0)
            double u1 = 1.0 - NextDouble();
            double u2 = 1.0 - NextDouble();
            double r = Math.Sqrt(-2.0 * Math.Log(u1));
            double theta = 2.0 * Math.PI * u2;
            double z = r * Math.Cos(theta);
            return mean + z * stdDev;
        }

        /// <summary>Fill a buffer with random bytes.</summary>
        public void NextBytes(byte[] buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            int i = 0;
            while (i + 4 <= buffer.Length)
            {
                uint v = NextUInt();
                buffer[i++] = (byte)(v);
                buffer[i++] = (byte)(v >> 8);
                buffer[i++] = (byte)(v >> 16);
                buffer[i++] = (byte)(v >> 24);
            }
            while (i < buffer.Length)
                buffer[i++] = (byte)NextUInt();
        }

        /// <summary>Fisher–Yates shuffle (in-place, unbiased).</summary>
        public void Shuffle<T>(IList<T> list)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = NextInt(i + 1); // [0, i]
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        /// Choose a uniform element from a non-empty list.
        /// </summary>
        public T Choice<T>(IList<T> list)
        {
            if (list == null || list.Count == 0) throw new ArgumentException("List must be non-empty", nameof(list));
            return list[NextInt(list.Count)];
        }

        /// <summary>
        /// Choose a uniform enum value of TEnum.
        /// </summary>
        public TEnum NextEnum<TEnum>() where TEnum : struct, Enum
        {
            var values = (TEnum[])Enum.GetValues(typeof(TEnum));
            return values[NextInt(values.Length)];
        }

        public override string ToString() => $"DeterministicRng(state=0x{_state:X16}, inc=0x{_inc:X16})";
    }
}
