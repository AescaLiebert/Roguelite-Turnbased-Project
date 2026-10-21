using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Player-facing summaries of temporary incoming-attack responses.</summary>
    public static class StatusReactionText
    {
        public static IEnumerable<string> Describe(StatusRecipeDefinition recipe)
        {
            foreach (var rule in recipe.Reactions)
            {
                if (rule.Trigger != PassiveEventKind.DamageResolved || rule.ActorRelation != PassiveRelation.Enemies ||
                    rule.TargetRelation != PassiveRelation.Self || rule.RequireCritical || rule.RequireBlocked ||
                    rule.FilterCategory || rule.FilterDamageFamily || rule.OwnTeamTurnOnly) continue;
                var women = rule.Conditions.Any(c => c.Logic == ConditionLogic.Leaf && !c.Negate &&
                    c.Kind == EffectConditionKind.TargetTraitIs && c.Subject == ConditionSubject.Actor && c.StringValue == "trait.women");
                // Complex programs retain their authored skill wording; do not claim an unconditional response.
                if (rule.Conditions.Any(c => c.Logic != ConditionLogic.Leaf || c.Negate || c.Kind != EffectConditionKind.Always &&
                    !(women && c.Kind == EffectConditionKind.TargetTraitIs && c.StringValue == "trait.women"))) continue;
                foreach (var command in rule.Commands)
                {
                    var operation = command.Effect;
                    if (command.Kind != PassiveCommandKind.ExecuteEffect || operation?.Kind != CardEffectOperationKind.ApplyStatus ||
                        operation.Target != EffectTargetScope.TriggerActor || operation.TargetQuery != null) continue;
                    var status = operation.Status;
                    var id = status?.InlineRecipe?.Id ?? status?.StatusRecipeId;
                    if (string.IsNullOrEmpty(id)) continue;
                    var name = status.InlineRecipe?.NameKey;
                    if (string.IsNullOrWhiteSpace(name)) name = StatusLibrary.GetDisplayName(id, status.InlineRecipe?.Polarity ?? StatusPolarity.Debuff);
                    var potency = status.PotencySource == StatusPotencySource.Authored && status.PotencyBp != 0
                        ? " (" + (status.PotencyBp / 100d).ToString("0.##") + "%)" : "";
                    var duration = status.DurationOverride > 0 ? status.DurationOverride : status.InlineRecipe?.DefaultDuration ?? 0;
                    yield return "While active, when attacked" + (women ? " by an enemy with the Women trait" : "") +
                        (rule.ExcludeUltimate ? " (except Ultimate attacks)" : "") + ", applies " + name + potency + " to the attacker" + (duration > 0 ? " for " + duration + " turns." : ".");
                }
            }
        }
    }
}
