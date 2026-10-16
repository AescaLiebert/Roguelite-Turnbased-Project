using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Presentation.Combat
{
    public static class CombatSupportText
    {
        public static string Format(CombatSupportDefinition support)
        {
            var clauses = new List<string>();
            if (support.ExplosionMultiplierBp > 10000) clauses.Add("While active, multiply allied Explosion damage by " + support.ExplosionMultiplierBp / 10000f + "× (including Baron Bunny and Overload).");
            if (support.SwirlStrengthBp > 0) clauses.Add("While in SUB, increase allied Swirl damage by " + support.SwirlStrengthBp / 100f + "%.");
            if (support.OpeningTeamGauge > 0) clauses.Add("At battle start while active, grant active allies " + support.OpeningTeamGauge + " Power Gauge orb.");
            if (support.AttackerGaugeGainBp > 0) clauses.Add("When an ally attacks an enemy with Frost Bite, that attacker gains +" + support.AttackerGaugeGainBp / 100f + "% Power Gauge generation for one turn. Fractional orbs carry forward.");
            if (support.BonusCritChanceBp > 0) clauses.Add("Against Frost Bite, this hero gains +" + support.BonusCritChanceBp / 100f + " percentage points Crit Chance and +" + support.BonusDamageBp / 100f + "% damage for that attacking card.");
            if (!string.IsNullOrEmpty(support.AfterAttackStatusRecipeId)) clauses.Add("With a full Genshin team including SUB, this hero's attacks apply one permanent grey Conductive stack to each enemy hit. Conductive reduces Defense by 20% per stack, up to three stacks.");
            if (support.DamageHealLowestAllyBp > 0) clauses.Add("After this hero's attacking card, heal another living active ally with the lowest HP ratio for " + support.DamageHealLowestAllyBp / 100f + "% of this hero's actual HP damage.");
            if (support.AttackCadence > 0) clauses.Add("Every " + support.AttackCadence + " attacking cards used by this hero, that card gains +" + support.CadenceDamageBp / 100f + "% damage and +" + support.CadenceDotBp / 100f + "% DOT damage, then the count resets.");
            return string.Join("\n\n", clauses);
        }
    }
}
