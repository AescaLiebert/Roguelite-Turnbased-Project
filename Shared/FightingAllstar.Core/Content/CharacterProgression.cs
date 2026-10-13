using System;
using System.Collections.Generic;
using FightingAllstar.Core.Run;

namespace FightingAllstar.Core.Content
{
    /// <summary>Linear growth as a fraction of level-one stats, in basis points per level.</summary>
    [Serializable]
    public sealed class CharacterStatGrowth
    {
        public int AttackBp = 100, DefenseBp = 100, HealthBp = 100;
        // Legacy serialized data only. CC is derived from stat gains, never this rate.
        public int CombatClassBp { get; set; }
        public CharacterStatGrowth Clone() => (CharacterStatGrowth)MemberwiseClone();
    }

    public static class CharacterProgression
    {
        public const int MaxLevel = 90, InitialCap = 10;

        public static int NormalizeLevel(int level) => Math.Max(1, Math.Min(MaxLevel, level));
        // Missing caps in old saves migrate to the smallest legal cap containing their level.
        public static int NormalizeCap(int level, int cap) => Math.Max(((NormalizeLevel(level) + 9) / 10) * 10,
            Math.Max(InitialCap, Math.Min(MaxLevel, cap / 10 * 10)));

        public static StatBlock StatsAtLevel(StatBlock basis, CharacterStatGrowth growth, int level)
        {
            if (basis == null) throw new ArgumentNullException(nameof(basis));
            var stats = basis.Clone();
            var steps = NormalizeLevel(level) - 1;
            growth = growth ?? new CharacterStatGrowth();
            stats.Attack = Scale(basis.Attack, growth.AttackBp, steps);
            stats.Defense = Scale(basis.Defense, growth.DefenseBp, steps);
            stats.MaxHealth = Scale(basis.MaxHealth, growth.HealthBp, steps);
            var maximum = basis.Clone();
            maximum.Attack = Scale(basis.Attack, growth.AttackBp, MaxLevel - 1);
            maximum.Defense = Scale(basis.Defense, growth.DefenseBp, MaxLevel - 1);
            maximum.MaxHealth = Scale(basis.MaxHealth, growth.HealthBp, MaxLevel - 1);
            CombatClassRules.Calibrate(stats, basis, maximum);
            return stats;
        }

        private static int Scale(int basis, int rate, int steps) => checked((int)Math.Min(int.MaxValue,
            decimal.Round((decimal)Math.Max(0, basis) * (10000m + (decimal)Math.Max(0, rate) * steps) / 10000m, 0, MidpointRounding.AwayFromZero)));

        // Starting economy values: one level costs 100 + 25 * currentLevel squared.
        public static long LevelCoinCost(int level) => 100L + 25L * NormalizeLevel(level) * NormalizeLevel(level);
        public static long BreakCoinCost(int cap) => 5000L * (cap / 10) * (cap / 10);

        public static bool TryCosts(int categoryId, string seriesId, int level, int cap, bool limitBreak,
            out List<ItemAmount> costs, out string error)
        {
            costs = new List<ItemAmount>(); error = null;
            if (level < 1 || level > MaxLevel || cap < InitialCap || cap > MaxLevel || cap % 10 != 0 || level > cap)
            { error = "Invalid fighter progression."; return false; }
            if (level == MaxLevel) { error = "Maximum level reached."; return false; }
            if (!limitBreak)
            {
                if (level == cap) { error = "Limit break required to raise the level cap."; return false; }
                costs.Add(new ItemAmount { ItemId = ItemCatalog.Coin, Amount = LevelCoinCost(level) }); return true;
            }
            if (level != cap || cap == MaxLevel) { error = "Reach the current level cap before limit breaking."; return false; }
            var phase = categoryId / 1000; var sub = categoryId / 100 % 10;
            if (phase < 1 || phase > 9 || sub < 0 || sub > 4 || string.IsNullOrWhiteSpace(seriesId) ||
                ItemCatalog.Find(ItemCatalog.SeriesId(seriesId)) == null)
            { error = "This fighter needs a valid phase, sub-phase and series identity."; return false; }
            var stage = cap / 10;
            costs.Add(new ItemAmount { ItemId = ItemCatalog.Coin, Amount = BreakCoinCost(cap) });
            costs.Add(new ItemAmount { ItemId = ItemCatalog.PhaseId(phase), Amount = 5 * stage });
            costs.Add(new ItemAmount { ItemId = ItemCatalog.SubPhaseId(sub), Amount = 3 * stage });
            costs.Add(new ItemAmount { ItemId = ItemCatalog.SeriesId(seriesId), Amount = 2 * stage });
            return true;
        }
    }
}
