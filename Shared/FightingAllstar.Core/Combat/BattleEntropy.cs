using System;
using System.Security.Cryptography;

namespace FightingAllstar.Core.Combat
{
    /// <summary>Creates unpredictable seeds for battle-local random streams.</summary>
    public static class BattleEntropy
    {
        public static ulong CreateSeed()
        {
            var bytes = new byte[sizeof(ulong)];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            var seed = BitConverter.ToUInt64(bytes, 0);
            return seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        }
    }
}
