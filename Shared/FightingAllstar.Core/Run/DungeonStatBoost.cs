using System;
using System.Security.Cryptography;
using System.Text;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Run
{
    /// <summary>A generated, run-only stat reward. Never supplies randomness to a battle.</summary>
    [Serializable]
    public sealed class DungeonStatBoost
    {
        public string NodeId;
        public StatId Stat;
        public int AmountBp;
        public DungeonStatBoost Clone() => (DungeonStatBoost)MemberwiseClone();
        public string DisplayName => Stat == StatId.MaxHealth ? "MAX HP" : Stat == StatId.Attack ? "ATK" : "DEF";
        public string Description => "+" + (AmountBp / 100) + "% " + DisplayName;

        public static DungeonStatBoost Generate(ulong dungeonSeed, string nodeId)
        {
            // A separate generation stream prevents changes to boon rewards from reshuffling enemies/routes.
            byte[] bytes;
            using (var sha = SHA256.Create()) bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(dungeonSeed + ":stat-boost:" + nodeId));
            ulong seed = 0;
            for (var i = 0; i < 8; i++) seed |= (ulong)bytes[i] << (i * 8);
            var rng = new DeterministicRandom(seed);
            var stats = new[] { StatId.Attack, StatId.Defense, StatId.MaxHealth };
            // Starting value: 3–8%. See the dungeon architecture playtest plan for tuning criteria.
            return new DungeonStatBoost { NodeId = nodeId, Stat = stats[rng.Next(stats.Length)], AmountBp = (3 + rng.Next(6)) * 100 };
        }

        public void Apply(StatBlock stats)
        {
            var before = stats.Get(Stat);
            var increase = Math.Max(1L, (long)before * AmountBp / 10000);
            stats.Set(Stat, (int)Math.Min(int.MaxValue, before + increase));
            CombatClassRules.Recalculate(stats);
        }
    }
}
