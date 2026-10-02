namespace FightingAllstar.Core.Combat
{
    /// <summary>Versioned xorshift64* stream. A stream is copied with state and draw count for replay.</summary>
    public sealed class DeterministicRandom
    {
        private ulong _state;
        public ulong State => _state;
        public ulong DrawCount { get; private set; }

        public DeterministicRandom(ulong seed) { _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed; }
        private DeterministicRandom(ulong state, ulong drawCount) { _state = state; DrawCount = drawCount; }
        public DeterministicRandom Clone() => new DeterministicRandom(_state, DrawCount);
        public static DeterministicRandom Restore(ulong state, ulong drawCount) => new DeterministicRandom(state, drawCount);

        public uint NextUInt()
        {
            var x = _state;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            _state = x;
            DrawCount++;
            return (uint)((x * 2685821657736338717UL) >> 32);
        }

        public int NextBasisPoints() => (int)(NextUInt() % 10000U);
        public int Next(int exclusiveMaximum) => exclusiveMaximum <= 1 ? 0 : (int)(NextUInt() % (uint)exclusiveMaximum);
    }
}
