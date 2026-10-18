using System;
namespace FightingAllstar.Core.Content
{
    /// <summary>Starting weights: HP .2, ATK 1, DEF 1.5, secondary percentage points 100. Integer score units are tenths.</summary>
    public static class CombatClassRules
    {
        public const int HealthWeight = 2, AttackWeight = 10, DefenseWeight = 15, SecondaryBasisPointWeight = 10;
        public static long Score(StatBlock stats)
        {
            if (stats == null) return 0;
            long secondary = (long)stats.PierceBp + stats.ResistanceBp + stats.RegenerationBp + stats.CritChanceBp + stats.CritDamageBp +
                stats.CritResistanceBp + stats.CritDefenseBp + stats.RecoveryBp + stats.BlockChanceBp + stats.BlockPowerBp + stats.LifeStealBp +
                stats.EvadeBp + stats.PerceptionBp + stats.ControlBp + stats.AvoidanceBp + stats.ReflectDamageBp +
                stats.DamageOverTimeDealtBp + stats.DamageDealtBp + stats.AdditionalDamageDealtBp;
            return (long)stats.MaxHealth * HealthWeight + (long)stats.Attack * AttackWeight + (long)stats.Defense * DefenseWeight + secondary * SecondaryBasisPointWeight;
        }
        public static void Calibrate(StatBlock stats, StatBlock levelOne, StatBlock levelNinety)
        {
            stats.HasDerivedCombatClass = true;
            stats.CombatClassReferenceValue = Math.Max(0, levelOne.CombatClass);
            stats.CombatClassReferenceScore = Score(levelOne);
            stats.CombatClassGrowthScore = Score(levelNinety) - stats.CombatClassReferenceScore;
            stats.CombatClassFlatBonus = 0; Recalculate(stats);
        }
        public static void Recalculate(StatBlock stats)
        {
            // Frozen legacy runs retain their recorded CC. New progression snapshots carry their calibration.
            if (stats == null || !stats.HasDerivedCombatClass) return;
            var delta = Score(stats) - stats.CombatClassReferenceScore;
            var gain = stats.CombatClassGrowthScore > 0 ? (decimal)delta * 9m * stats.CombatClassReferenceValue / stats.CombatClassGrowthScore : delta / 10m;
            var value = decimal.Round(stats.CombatClassReferenceValue + gain + stats.CombatClassFlatBonus, 0, MidpointRounding.AwayFromZero);
            stats.CombatClass = (int)Math.Max(0m, Math.Min(int.MaxValue, value));
        }
    }
}
