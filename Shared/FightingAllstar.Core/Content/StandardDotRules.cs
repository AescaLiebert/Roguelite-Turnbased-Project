using System;

namespace FightingAllstar.Core.Content
{
    /// <summary>Database-owned DOT coefficients shared by authored content, live statuses and old snapshots.</summary>
    public static class StandardDotRules
    {
        public const int ShockBp = 4500, PoisonBp = 4500, BleedBp = 7000;

        public static int CoefficientFor(string recipeId)
        {
            switch (recipeId)
            {
                case "status.debuff.shock": case CombatTags.Shock: return ShockBp;
                case "status.debuff.poison": case CombatTags.Poison: return PoisonBp;
                case "status.debuff.bleed": case CombatTags.Bleed: return BleedBp;
                default: return 0;
            }
        }

        public static int CoefficientFor(StatusRecipeDefinition recipe)
        {
            if (recipe == null) return 0;
            var coefficient = CoefficientFor(recipe.Id);
            if (coefficient != 0) return coefficient;
            if (recipe.Tags != null) foreach (var tag in recipe.Tags)
            {
                coefficient = CoefficientFor(tag);
                if (coefficient != 0) return coefficient;
            }
            return 0;
        }

        public static void Normalize(StatusRecipeDefinition recipe)
        {
            var coefficient = CoefficientFor(recipe);
            if (coefficient == 0) return;
            recipe.Behavior |= StatusBehavior.DamageOverTime;
            recipe.PeriodicDamage = recipe.PeriodicDamage ?? new PeriodicDamageDefinition();
            recipe.PeriodicDamage.Family = DamageFamily.DamageOverTime;
            recipe.PeriodicDamage.Scaling = StatusSnapshotScaling.TriggeringHealthDamage;
            recipe.PeriodicDamage.CoefficientBp = coefficient;
            if (recipe.PeriodicDamage.Timing == StatusTickTiming.None)
                recipe.PeriodicDamage.Timing = StatusTickTiming.TargetTurnEnd;
        }
    }
}
