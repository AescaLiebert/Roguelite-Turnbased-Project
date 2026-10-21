using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Studio
{
    public static partial class StudioCompiler
    {
        private static StudioDocument ExpandLibraryInputs(StudioDocument source, StudioCompileResult result)
        {
            var doc = StudioDocumentService.Clone(source);
            var resources = Index(doc.Resources, r => r.Id, result, "resource");
            var statuses = Index(doc.Statuses, s => s.Id, result, "status");
            var localIds = new HashSet<string>((doc.Parameters ?? new List<StudioParameter>()).Select(p => p?.Id)
                .Concat((doc.Actions ?? new List<StudioActionNode>()).Select(a => a?.Id))
                .Concat((doc.Rules ?? new List<StudioRule>()).Select(r => r?.Id))
                .Concat((doc.Conditions ?? new List<StudioConditionNode>()).Select(c => c?.Id))
                .Concat((doc.Modules ?? new List<StudioModule>()).Select(m => m?.Id))
                .Concat((doc.Policies ?? new List<StudioPolicy>()).Select(p => p?.Id)));
            foreach (var resource in resources.Values)
            {
                if (!localIds.Add(resource.Id)) Error(result, "id.duplicate", resource.Id, "Library IDs must be unique across authoring tables.");
                if (resource.Cap < 1) Error(result, "resource.cap", resource.Id, "Resource cap must be positive.");
            }
            foreach (var status in statuses.Values)
            {
                if (!localIds.Add(status.Id)) Error(result, "id.duplicate", status.Id, "Library IDs must be unique across authoring tables.");
                if (status.Definition == null || status.Definition.Id != status.Id)
                    Error(result, "status.identity", status.Id, "Shared status and recipe must have the same stable ID.");
                else foreach (var error in EffectRecipeValidator.Validate(new[] { status.Definition }, null))
                    Error(result, "status.recipe", status.Id, error);
            }
            foreach (var rule in doc.Rules ?? new List<StudioRule>())
                if (!string.IsNullOrEmpty(rule?.StatusId) && !statuses.ContainsKey(rule.StatusId))
                    Error(result, "status.rule-owner", rule.Id, "Status-owned rule references a missing shared status.");
            foreach (var action in doc.Actions ?? new List<StudioActionNode>())
            {
                if (action == null) continue;
                if (resources.TryGetValue(action.ResourceKey ?? "", out var resource)) action.ResourceCap = resource.Cap;
                if (resources.TryGetValue(action.Command?.CounterKey ?? "", out resource)) action.Command.CounterCap = resource.Cap;
                var app = action.Command?.Effect?.Status;
                if (app != null && app.InlineRecipe == null && statuses.TryGetValue(app.StatusRecipeId ?? "", out var status))
                    app.InlineRecipe = status.Definition?.Clone();
            }
            foreach (var variant in doc.AbilityVariants ?? new List<StudioAbilityVariant>())
                foreach (var rank in variant?.Definition?.Ranks ?? new List<SkillRankDefinition>())
                {
                    ReplaceShared(rank?.Effect);
                    foreach (var step in rank?.Effect?.Sequence ?? new List<CardEffectStep>()) ReplaceShared(step?.Effect);
                }
            void ReplaceShared(EffectOperationDefinition effect)
            {
                if (effect?.StatusRecipe != null && statuses.TryGetValue(effect.StatusRecipe.Id ?? "", out var status))
                    effect.StatusRecipe = status.Definition?.Clone();
            }
            return doc;
        }

        private static void AttachStatusRules(StudioDocument document, CharacterDefinition character,
            List<KeyValuePair<string, PassiveReactionDefinition>> localRules, StudioCompileResult result)
        {
            var definitions = (document.Statuses ?? new List<StudioStatus>()).Where(s => s?.Definition != null)
                .GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First().Definition);
            foreach (var group in localRules.GroupBy(r => r.Key))
                foreach (var error in PassiveRuleValidator.Validate(new PassiveDefinition { Id = group.Key,
                    Reactions = group.Select(p => p.Value).ToList() })) Error(result, "status.rule", group.Key, error);
            StatusRecipeDefinition Recipe(StatusRecipeDefinition source, HashSet<string> path)
            {
                if (source == null || !definitions.TryGetValue(source.Id ?? "", out var canonical)) return source;
                if (!path.Add(source.Id)) { Error(result, "status.rule-cycle", source.Id, "Status-local rules form a recipe dependency cycle. Use a character rule for recursive event chains."); return canonical.Clone(); }
                var copy = canonical.Clone();
                copy.Reactions = localRules.Where(p => p.Key == source.Id).Select(p => p.Value.Clone()).ToList();
                foreach (var reaction in copy.Reactions) Actions(reaction.Actions, path);
                path.Remove(source.Id); return copy;
            }
            void Actions(List<PassiveActionDefinition> actions, HashSet<string> path)
            {
                foreach (var action in actions ?? new List<PassiveActionDefinition>())
                {
                    foreach (var command in action.Commands ?? new List<PassiveCommandDefinition>())
                        if (command.Effect?.Status?.InlineRecipe != null)
                            command.Effect.Status.InlineRecipe = Recipe(command.Effect.Status.InlineRecipe, path);
                    Actions(action.Children, path); Actions(action.ElseChildren, path);
                }
            }
            foreach (var reaction in character.Passive.Reactions) Actions(reaction.Actions, new HashSet<string>());
            foreach (var variant in character.Skills.SelectMany(s => s.Variants))
                foreach (var rank in variant.Ranks)
                {
                    rank.Effect.StatusRecipe = Recipe(rank.Effect.StatusRecipe, new HashSet<string>());
                    foreach (var step in rank.Effect.Sequence) if (step?.Effect != null)
                        step.Effect.StatusRecipe = Recipe(step.Effect.StatusRecipe, new HashSet<string>());
                }
            // Check unused status-local programs as well; an invalid library entry cannot hide until first application.
            foreach (var definition in definitions.Values) Recipe(definition, new HashSet<string>());
        }
    }
}
