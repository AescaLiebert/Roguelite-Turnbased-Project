using System;
using System.Collections.Generic;

namespace FightingAllstar.Core.Content
{
    /// <summary>Context validation for canonical native skill/ultimate payloads.</summary>
    public static class SkillEffectValidator
    {
        public static List<string> Validate(CharacterDefinition character)
        {
            var errors = new List<string>();
            void Conditions(IEnumerable<EffectConditionDefinition> list, string path, bool outcomes, int depth = 0)
            {
                if (depth > 32) { errors.Add(path + ": condition depth exceeds 32."); return; }
                foreach (var c in list ?? new List<EffectConditionDefinition>())
                {
                    if (c == null) continue;
                    if (!Enum.IsDefined(typeof(ConditionLogic), c.Logic) || !Enum.IsDefined(typeof(ConditionSubject), c.Subject) ||
                        !Enum.IsDefined(typeof(EffectConditionKind), c.Kind)) errors.Add(path + ": invalid condition.");
                    if (c.Logic == ConditionLogic.Not && c.Children.Count != 1) errors.Add(path + ": Not requires one child.");
                    if (c.Logic != ConditionLogic.Leaf && c.Children.Count == 0) errors.Add(path + ": condition group requires children.");
                    if (c.Logic == ConditionLogic.Leaf)
                    {
                        if (!outcomes && (c.Kind == EffectConditionKind.WasCritical || c.Kind == EffectConditionKind.WasBlocked))
                            errors.Add(path + ": hit outcomes are unavailable before primary damage resolves.");
                        if (c.Kind == EffectConditionKind.ResourceEnteredAtLeast || c.Kind == EffectConditionKind.ResourceExitedBelow ||
                            c.Kind == EffectConditionKind.RemovedLayersAtLeast || c.Kind == EffectConditionKind.RemovedStatusFromEffectOwner ||
                            c.Kind == EffectConditionKind.BindingValueAtLeast)
                            errors.Add(path + ": this predicate requires an event/action-program context; use a granted status reaction.");
                    }
                    Conditions(c.Children, path, outcomes, depth + 1);
                }
            }
            void Status(StatusRecipeDefinition recipe, string path, int depth)
            {
                if (recipe == null) return;
                if (depth > 16) { errors.Add(path + ": status reaction nesting exceeds 16."); return; }
                errors.AddRange(PassiveRuleValidator.Validate(new PassiveDefinition { Id = path, Reactions = recipe.Reactions }));
                foreach (var reaction in recipe.Reactions)
                    foreach (var command in reaction.Commands)
                        Status(command.Effect?.Status?.InlineRecipe, path + "/" + reaction.Id, depth + 1);
            }
            void Operation(EffectOperationDefinition op, string path, bool outcomes, bool root)
            {
                if (op == null) return;
                Conditions(op.Conditions, path, outcomes);
                if (op.DamageRecipe != null)
                {
                    var d = op.DamageRecipe;
                    if (root || op.Kind != EffectKind.Damage) errors.Add(path + ": a packet recipe belongs to a sequence damage operation.");
                    if (d.CoefficientBp < 0 || d.FixedAmount < 0 || d.KeywordFactorBp < 0 ||
                        !Enum.IsDefined(typeof(DamageFamily), d.Family) || !Enum.IsDefined(typeof(DamageStatSource), d.StatSource) ||
                        !Enum.IsDefined(typeof(StatScaling), d.Scaling)) errors.Add(path + ": invalid damage packet.");
                }
                Status(op.StatusRecipe, path, 0);
            }
            void Effect(EffectDefinition effect, string path)
            {
                if (effect == null) return;
                Operation(effect, path, false, true);
                foreach (var step in effect.Sequence)
                    if (step?.Effect != null) Operation(step.Effect, path + "/" + step.Effect.Id,
                        step.Timing == CardEffectTiming.AfterDamage || step.Timing == CardEffectTiming.AfterAction, false);
            }
            foreach (var skill in character.Skills)
            {
                foreach (var rank in skill.Ranks) Effect(rank.Effect, skill.Id + "/rank" + rank.Rank);
                foreach (var variant in skill.Variants) foreach (var rank in variant.Ranks) Effect(rank.Effect, variant.Id + "/rank" + rank.Rank);
            }
            foreach (var tier in character.UltimateTiers) Effect(tier.Effect, character.Id + "/ultimate" + tier.Tier);
            return errors;
        }
    }
}
