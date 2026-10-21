using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Studio
{
    public static partial class StudioCompiler
    {
        private static void ValidateM3(StudioDocument document, Dictionary<string, StudioParameter> parameters,
            Dictionary<string, StudioConditionNode> conditions, Dictionary<string, StudioRule> rules,
            Dictionary<string, StudioActionNode> actions, Dictionary<string, StudioModule> modules,
            Dictionary<string, StudioPolicy> policies, StudioCompileResult result)
        {
            if (actions.Count > 1024 || modules.Count > 128 || rules.Count > 256 || policies.Count > 128)
                Error(result, "action.table-budget", document.Id, "Maximum tables: 1024 actions, 128 modules, 256 rules, and 128 policies.");
            foreach (var rule in rules.Values.Where(r => !string.IsNullOrEmpty(r.ActionRootId)))
            {
                if (rule.RuntimeVersion != 0 && rule.RuntimeVersion != 1)
                    Error(result, "rule.execution-version", rule.Id, "Unsupported execution version.");
                if (!string.IsNullOrEmpty(rule.ElseActionRootId) && !actions.ContainsKey(rule.ElseActionRootId))
                    Error(result, "reference.else-action", rule.Id, "Else command root is missing.");
                if (rule.Trigger == PassiveEventKind.BeforeDamage && !string.IsNullOrEmpty(rule.ElseActionRootId))
                    Error(result, "rule.calculation-else", rule.Id, "BeforeDamage does not execute an else program.");
                var settings = rule.Settings;
                if (settings == null) { Error(result, "rule.settings", rule.Id, "Execution controls are missing."); continue; }
                if (!Enum.IsDefined(typeof(PassiveExecutionPhase), settings.Phase) ||
                    !Enum.IsDefined(typeof(PassiveLimitScope), settings.LimitScope) ||
                    !Enum.IsDefined(typeof(PassiveDamageTiming), settings.DamageTiming) ||
                    !Enum.IsDefined(typeof(PassiveRelation), settings.ActorRelation) ||
                    !Enum.IsDefined(typeof(PassiveRelation), settings.TargetRelation) || settings.CooldownOwnerTurns < 0 ||
                    (rule.RuntimeVersion == 1 && settings.MaximumActivations < 1) || settings.MinimumRank < 0 || settings.MinimumRank > 3)
                    Error(result, "rule.controls", rule.Id, "Use valid phase, relation, activation scope, positive activation count, and nonnegative cooldown.");
            }
            foreach (var module in modules.Values)
                if (string.IsNullOrEmpty(module.RootActionId) || !actions.ContainsKey(module.RootActionId))
                    Error(result, "reference.module-root", module.Id, "Reusable module action root is missing.");
            foreach (var action in actions.Values)
            {
                if (!Enum.IsDefined(typeof(StudioActionKind), action.Kind)) Error(result, "action.kind", action.Id, "Unknown action kind.");
                if (!string.IsNullOrEmpty(action.ConditionRootId) && !conditions.ContainsKey(action.ConditionRootId))
                    Error(result, "reference.action-condition", action.Id, "Action condition root is missing.");
                foreach (var child in (action.Children ?? new List<string>()).Concat(action.ElseChildren ?? new List<string>()))
                    if (string.IsNullOrEmpty(child) || !actions.ContainsKey(child)) Error(result, "reference.action-child", action.Id, "Missing action child: " + child);
                if (action.Kind == StudioActionKind.Module && (string.IsNullOrEmpty(action.ModuleId) || !modules.ContainsKey(action.ModuleId)))
                    Error(result, "reference.module", action.Id, "Reusable module reference is missing.");
                if (action.Kind == StudioActionKind.Branch && string.IsNullOrEmpty(action.ConditionRootId))
                    Error(result, "action.branch-condition", action.Id, "A branch requires its condition root.");
                if (!Enum.IsDefined(typeof(EffectTargetScope), action.Target) || action.MaximumTargets < 1 || action.MaximumTargets > 32)
                    Error(result, "action.targets", action.Id, "Target loop bounds must be 1–32 and use a supported scope.");
                ValidateQuery(action.Query, action.Id, result);
                var amountUnit = StudioParameterUnit.Integer;
                if (action.Kind == StudioActionKind.Command)
                {
                    var command = action.Command;
                    if (command == null || !StudioCommandSupported(command.Kind))
                        Error(result, "action.command", action.Id, "The command is not enabled for general action execution.");
                    else if (command.Kind == PassiveCommandKind.ExecuteEffect)
                    {
                        var operation = command.Effect;
                        if (operation == null || !StudioAuthoringCatalog.Operations.Contains(operation.Kind))
                            Error(result, "action.operation", action.Id, "The effect operation is missing or unsupported.");
                        else ValidateOperation(operation, action.Id, result);
                    }
                    else if ((command.Kind == PassiveCommandKind.IncrementCounter || command.Kind == PassiveCommandKind.SetCounter) &&
                        (string.IsNullOrWhiteSpace(command.CounterKey) || command.CounterCap < 1 || command.Amount < 0))
                        Error(result, "action.counter", action.Id, "Counter commands require a key, positive cap, and nonnegative amount.");
                }
                else if (action.Kind == StudioActionKind.SpendResource || action.Kind == StudioActionKind.TransferResource)
                {
                    if (string.IsNullOrWhiteSpace(action.ResourceKey) || action.Amount < 0 || action.ResourceCap < 1)
                        Error(result, "action.resource", action.Id, "A resource operation requires a key, nonnegative amount, and positive cap.");
                }
                else if (action.Kind == StudioActionKind.ConsumeStatusLayers &&
                    (string.IsNullOrWhiteSpace(action.StatusRecipeId) || action.Amount < 1 || !Enum.IsDefined(typeof(StatusRemovalReason), action.RemovalReason)))
                    Error(result, "action.consume", action.Id, "Layer consumption requires a recipe ID, positive layer count, and removal reason.");
                Bound(action.AmountParameterId, amountUnit, parameters, action.Id, result);
                Bound(action.CoefficientParameterId, StudioParameterUnit.BasisPoints, parameters, action.Id, result);
                Bound(action.MultiplierParameterId, StudioParameterUnit.Integer, parameters, action.Id, result);
                var boundAmount = BoundValue(action.AmountParameterId, action.Amount, parameters);
                if ((action.Kind == StudioActionKind.SpendResource || action.Kind == StudioActionKind.TransferResource ||
                     action.Kind == StudioActionKind.ConsumeStatusLayers) && boundAmount < 1)
                    Error(result, "action.positive-amount", action.Id, "Resource spend/transfer and layer consumption need a positive bound amount.");
                if (action.Kind == StudioActionKind.Command && action.Command?.Kind == PassiveCommandKind.ExecuteEffect && action.Command.Effect != null)
                {
                    var operation = action.Command.Effect.Clone();
                    ApplyOperationBindings(operation, action, parameters);
                    ValidateOperation(operation, action.Id, result);
                }
                if (action.Kind == StudioActionKind.Command && action.Command?.Kind == PassiveCommandKind.SetAbilityVariant &&
                    document.AbilityVariants?.Any(v => v?.SkillId == action.Command.SkillId && v.Definition?.Id == action.Command.VariantId) != true &&
                    document.EditingCharacter?.Skills?.Any(s => s?.Id == action.Command.SkillId && s.Variants.Any(v => v?.Id == action.Command.VariantId)) != true)
                    Error(result, "action.variant-reference", action.Id, "Variant unlock/relock must reference a skill and one of its authored or imported variants.");
            }
            foreach (var action in actions.Values)
            {
                var remaining = 1024;
                ValidateActionPath(action.Id, actions, modules, conditions, new HashSet<string>(), 0, ref remaining, result);
            }
            foreach (var policy in policies.Values)
            {
                if (policy.Definition == null || !Enum.IsDefined(typeof(CombatPolicyKind), policy.Definition.Kind))
                { Error(result, "policy.kind", policy.Id, "Policy definition is missing or unknown."); continue; }
                if (document.EditingCharacter?.Passive?.Policies?.Any(p => p?.Id == policy.Id) == true)
                    Error(result, "id.legacy-collision", policy.Id, "Policy ID collides with preserved imported content.");
                if (!string.IsNullOrEmpty(policy.ConditionRootId) && !conditions.ContainsKey(policy.ConditionRootId))
                    Error(result, "reference.policy-condition", policy.Id, "Policy condition root is missing.");
                Bound(policy.CapParameterId, StudioParameterUnit.BasisPoints, parameters, policy.Id, result);
                Bound(policy.CostParameterId, StudioParameterUnit.Integer, parameters, policy.Id, result);
                Bound(policy.HealParameterId, StudioParameterUnit.BasisPoints, parameters, policy.Id, result);
                var cap = BoundValue(policy.CapParameterId, policy.Definition.MaximumHealthDamageBp, parameters);
                var cost = BoundValue(policy.CostParameterId, policy.Definition.ResourceCost, parameters);
                var heal = BoundValue(policy.HealParameterId, policy.Definition.HealMaxHealthBp, parameters);
                if (cap < 1 || cap > 10000 || cost < 0 || heal < 0 || heal > 10000 || cost > 0 && string.IsNullOrWhiteSpace(policy.Definition.ResourceKey))
                    Error(result, "policy.values", policy.Id, "Cap must be 1–10000 bp; heal 0–10000 bp; resource cost nonnegative with a resource key when used.");
                var gate = policy.Definition.Gate;
                if (gate == null || gate.MinimumTier < 0 || gate.MaximumTier > 5 || gate.MinimumTier > gate.MaximumTier ||
                    !Enum.IsDefined(typeof(PassivePresence), gate.Presence) || gate.Modes == 0 || (gate.Modes & ~BattleModeMask.All) != 0 || gate.AllowDefeatedOwner)
                    Error(result, "policy.gate", policy.Id, "Policy requires a supported living-owner availability gate.");
            }
        }

        private static bool StudioCommandSupported(PassiveCommandKind kind) =>
            kind == PassiveCommandKind.ExecuteEffect || kind == PassiveCommandKind.IncrementCounter ||
            kind == PassiveCommandKind.SetCounter || kind == PassiveCommandKind.ChangePowerGauge || kind == PassiveCommandKind.SetAbilityVariant ||
            kind == PassiveCommandKind.IncreaseCurrentAttackPercent || kind == PassiveCommandKind.IncreaseCurrentDamageDealtPercent ||
            kind == PassiveCommandKind.IgnoreCurrentTargetCritDefensePercent || kind == PassiveCommandKind.IncreaseCurrentDamageDealtByBaseStatPercent ||
            kind == PassiveCommandKind.IncreaseCurrentCritChancePercent;

        private static void Bound(string id, StudioParameterUnit unit, Dictionary<string, StudioParameter> parameters, string source, StudioCompileResult result)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!parameters.TryGetValue(id, out var parameter)) Error(result, "reference.action-parameter", source, "Missing parameter: " + id);
            else if (parameter.Unit != unit) Error(result, "parameter.unit-mismatch", source, "Parameter " + id + " must use " + unit + ".");
        }

        private static int BoundValue(string id, int fallback, Dictionary<string, StudioParameter> parameters) =>
            !string.IsNullOrEmpty(id) && parameters.TryGetValue(id, out var parameter) ? parameter.Value : fallback;

        private static void ValidateQuery(TargetQueryDefinition query, string id, StudioCompileResult result)
        {
            if (query == null) return;
            if (!Enum.IsDefined(typeof(EffectTargetScope), query.BaseScope) || !Enum.IsDefined(typeof(TargetQuerySort), query.Sort) ||
                !Enum.IsDefined(typeof(TargetQueryAnchor), query.Anchor) ||
                query.Filter == null || !Enum.IsDefined(typeof(PassiveRelation), query.Filter.Relation) ||
                query.Limit < 1 || query.Limit > 32 || query.SampleCount < 0 || query.SampleCount > query.Limit)
                Error(result, "query.invalid", id, "A selector needs a supported scope/filter/sort, limit 1–32, and sample count within that limit.");
            if (query.Sort == TargetQuerySort.LowestRecipeLayers && string.IsNullOrWhiteSpace(query.RecipeId))
                Error(result, "query.recipe", id, "Lowest-layer ordering requires a status recipe ID.");
        }

        private static void ValidateOperation(CardEffectOperationDefinition operation, string id, StudioCompileResult result)
        {
            if (!Enum.IsDefined(typeof(EffectTargetScope), operation.Target)) Error(result, "operation.target", id, "Unsupported operation target.");
            ValidateQuery(operation.TargetQuery, id, result);
            if (operation.Kind == CardEffectOperationKind.Summon)
            {
                if (operation.Summon == null) Error(result, "operation.summon", id, "Summon requires an entity definition.");
                else foreach (var error in operation.Summon.Validate()) Error(result, "operation.summon", id, error);
                if (operation.Target != EffectTargetScope.Self)
                    Error(result, "operation.summon-target", id, "Summon belongs to the effect owner and must target Self.");
            }
            if (operation.Kind == CardEffectOperationKind.Damage &&
                (operation.Damage == null || !Enum.IsDefined(typeof(DamageFamily), operation.Damage.Family) ||
                 !Enum.IsDefined(typeof(StatScaling), operation.Damage.Scaling) || !Enum.IsDefined(typeof(DamageStatSource), operation.Damage.StatSource) ||
                 !Enum.IsDefined(typeof(StatId), operation.Damage.ScalingStat) || operation.Damage.CoefficientBp < 0 || operation.Damage.FixedAmount < 0))
                Error(result, "operation.damage", id, "Damage requires a valid nonnegative scaling recipe.");
            if (operation.Damage?.Profile != null && operation.Damage.Profile.TrueDamageBonusBp < 0)
                Error(result, "operation.profile", id, "An explicit true damage bonus cannot be negative.");
            if (operation.Kind == CardEffectOperationKind.Heal &&
                (operation.Value == null || !Enum.IsDefined(typeof(EffectValueSource), operation.Value.Source) || operation.Value.CoefficientBp < 0 || operation.Value.FixedAmount < 0))
                Error(result, "operation.heal", id, "Healing requires a nonnegative value and an explicit scaling source.");
            if (operation.Kind == CardEffectOperationKind.ChangePowerGauge && (operation.Magnitude < -5 || operation.Magnitude > 5))
                Error(result, "operation.gauge", id, "Gauge change must be between -5 and 5 points.");
            if (operation.Kind == CardEffectOperationKind.ApplyStatus)
            {
                var application = operation.Status;
                if (application == null || application.StackCount < 1 || application.ProcChanceBp < 0 || application.ProcChanceBp > 10000)
                    Error(result, "operation.status", id, "Status application requires positive layers and a chance from 0 to 10000 bp.");
                else
                {
                    var recipe = application.InlineRecipe;
                    if (recipe == null && StandardEffectDatabase.CreateStatusRecipes().All(s => s.Id != application.StatusRecipeId))
                        Error(result, "operation.status-reference", id, "Choose an existing standard recipe or author an inline recipe.");
                    if (recipe != null)
                        foreach (var error in EffectRecipeValidator.Validate(new[] { recipe }, null)) Error(result, "operation.status-recipe", id, error);
                }
            }
            if (operation.Kind == CardEffectOperationKind.ModifyCardRank && (operation.Magnitude < -2 || operation.Magnitude > 2 || operation.Magnitude == 0))
                Error(result, "operation.rank", id, "Card rank change must be -2, -1, 1, or 2.");
        }

        private static void ValidateActionPath(string id, Dictionary<string, StudioActionNode> actions,
            Dictionary<string, StudioModule> modules, Dictionary<string, StudioConditionNode> conditions,
            HashSet<string> path, int depth, ref int remaining, StudioCompileResult result)
        {
            if (!actions.TryGetValue(id ?? "", out var action)) return;
            if (depth > 64) { Error(result, "action.depth", id, "Maximum expanded action nesting is 64."); return; }
            if (--remaining < 0) { Error(result, "action.expansion-budget", id, "Expanded action graph exceeds 1024 nodes."); return; }
            if (!string.IsNullOrEmpty(action.ConditionRootId) && conditions.ContainsKey(action.ConditionRootId) &&
                !WithinExpansionBudget(action.ConditionRootId, conditions, ref remaining))
            { Error(result, "action.condition-expansion-budget", id, "Action and condition expansion together exceed 1024 runtime nodes."); return; }
            if (!path.Add(id)) { Error(result, "action.cycle", id, "Action/module cycle detected."); return; }
            if (action.Kind == StudioActionKind.Module && modules.TryGetValue(action.ModuleId ?? "", out var module))
                ValidateActionPath(module.RootActionId, actions, modules, conditions, path, depth + 1, ref remaining, result);
            foreach (var child in (action.Children ?? new List<string>()).Concat(action.ElseChildren ?? new List<string>()))
                ValidateActionPath(child, actions, modules, conditions, path, depth + 1, ref remaining, result);
            path.Remove(id);
        }

        internal static PassiveActionDefinition CompileAction(string id, Dictionary<string, StudioConditionNode> conditions,
            Dictionary<string, StudioActionNode> actions, Dictionary<string, StudioModule> modules, Dictionary<string, StudioParameter> parameters)
        {
            var node = actions[id];
            if (node.Kind == StudioActionKind.Module)
            {
                var wrapper = new PassiveActionDefinition { Id = node.Id, Kind = PassiveActionKind.Sequence,
                    Children = new List<PassiveActionDefinition> { CompileAction(modules[node.ModuleId].RootActionId, conditions, actions, modules, parameters) } };
                if (!string.IsNullOrEmpty(node.ConditionRootId)) wrapper.Conditions.Add(CompileNode(node.ConditionRootId, conditions, parameters));
                return wrapper;
            }
            var runtime = new PassiveActionDefinition { Id = node.Id, Kind = (PassiveActionKind)node.Kind,
                Target = node.Target, Query = node.Query?.Clone(), MaximumTargets = node.MaximumTargets,
                ResourceKey = node.ResourceKey, Amount = BoundValue(node.AmountParameterId, node.Amount, parameters),
                ResourceCap = node.ResourceCap, StatusRecipeId = node.StatusRecipeId, StatusOwnerOnly = node.StatusOwnerOnly,
                RequireFullAmount = node.RequireFullAmount, RemovalReason = node.RemovalReason, ResultBinding = node.ResultBinding };
            if (!string.IsNullOrEmpty(node.ConditionRootId)) runtime.Conditions.Add(CompileNode(node.ConditionRootId, conditions, parameters));
            if (node.Kind == StudioActionKind.Command)
            {
                var command = node.Command.Clone();
                command.Amount = BoundValue(command.Kind == PassiveCommandKind.ExecuteEffect ? node.MultiplierParameterId : node.AmountParameterId, command.Amount, parameters);
                if (command.Effect != null)
                {
                    command.Effect.Id = node.Id + ".effect";
                    ApplyOperationBindings(command.Effect, node, parameters);
                }
                runtime.Commands.Add(command);
            }
            runtime.Children = (node.Children ?? new List<string>()).Select(child => CompileAction(child, conditions, actions, modules, parameters)).ToList();
            runtime.ElseChildren = (node.ElseChildren ?? new List<string>()).Select(child => CompileAction(child, conditions, actions, modules, parameters)).ToList();
            return runtime;
        }

        private static void ApplyOperationBindings(CardEffectOperationDefinition operation, StudioActionNode node,
            Dictionary<string, StudioParameter> parameters)
        {
            operation.Magnitude = BoundValue(node.AmountParameterId, operation.Magnitude, parameters);
            if (operation.Damage != null)
            {
                operation.Damage.FixedAmount = BoundValue(node.AmountParameterId, operation.Damage.FixedAmount, parameters);
                operation.Damage.CoefficientBp = BoundValue(node.CoefficientParameterId, operation.Damage.CoefficientBp, parameters);
            }
            if (operation.Value != null)
            {
                operation.Value.FixedAmount = BoundValue(node.AmountParameterId, operation.Value.FixedAmount, parameters);
                operation.Value.CoefficientBp = BoundValue(node.CoefficientParameterId, operation.Value.CoefficientBp, parameters);
            }
            if (operation.Status != null) operation.Status.StackCount = BoundValue(node.AmountParameterId, operation.Status.StackCount, parameters);
        }

        private static CombatPolicyDefinition CompilePolicy(StudioPolicy source, Dictionary<string, StudioConditionNode> conditions,
            Dictionary<string, StudioParameter> parameters)
        {
            var policy = source.Definition.Clone(); policy.Id = source.Id;
            policy.MaximumHealthDamageBp = BoundValue(source.CapParameterId, policy.MaximumHealthDamageBp, parameters);
            policy.ResourceCost = BoundValue(source.CostParameterId, policy.ResourceCost, parameters);
            policy.HealMaxHealthBp = BoundValue(source.HealParameterId, policy.HealMaxHealthBp, parameters);
            policy.Conditions = new List<EffectConditionDefinition>();
            if (!string.IsNullOrEmpty(source.ConditionRootId)) policy.Conditions.Add(CompileNode(source.ConditionRootId, conditions, parameters));
            return policy;
        }

        private static void CompileAbilities(StudioDocument document, CharacterDefinition character,
            Dictionary<string, StudioParameter> parameters, StudioCompileResult result)
        {
            if (document.AbilityVariants == null || document.AbilityOverrides == null)
            { Error(result, "ability.table", document.Id, "Ability authoring tables are missing."); return; }
            var ids = new HashSet<string>(document.Parameters.Select(p => p.Id).Concat(document.Conditions.Select(n => n.Id))
                .Concat(document.Rules.Select(r => r.Id)).Concat(document.Actions.Select(a => a.Id)).Concat(document.Modules.Select(m => m.Id))
                .Concat(document.Policies.Select(p => p.Id)), StringComparer.Ordinal);
            foreach (var item in document.AbilityVariants)
            {
                if (item?.Definition == null || string.IsNullOrWhiteSpace(item.Definition.Id) || !ids.Add(item.Definition.Id))
                { Error(result, "ability.variant-id", document.Id, "Every variant needs a unique local ID."); continue; }
                var skill = character.Skills?.Find(s => s?.Id == item.SkillId);
                if (skill == null) { Error(result, "ability.skill-reference", item.Definition.Id, "Variant source skill is missing."); continue; }
                if (skill.Variants.Any(v => v?.Id == item.Definition.Id))
                { Error(result, "ability.legacy-collision", item.Definition.Id, "Variant ID collides with preserved source content."); continue; }
                skill.Variants.Add(item.Definition.Clone());
            }
            foreach (var item in document.AbilityOverrides)
            {
                if (item?.Definition == null || string.IsNullOrWhiteSpace(item.Definition.Id) || !ids.Add(item.Definition.Id))
                { Error(result, "ability.override-id", document.Id, "Every ability override needs a unique local ID."); continue; }
                var skill = character.Skills?.Find(s => s?.Id == item.SkillId);
                if (skill == null) { Error(result, "ability.skill-reference", item.Definition.Id, "Override source skill is missing."); continue; }
                var value = item.Definition.Clone();
                var unit = value.Field == AbilityValueField.CoefficientBp || value.Field == AbilityValueField.HealCoefficientBp
                    ? StudioParameterUnit.BasisPoints : StudioParameterUnit.Integer;
                Bound(item.ValueParameterId, unit, parameters, value.Id, result);
                value.Value = BoundValue(item.ValueParameterId, value.Value, parameters);
                if (string.IsNullOrEmpty(item.VariantId)) skill.ValueOverrides.Add(value);
                else
                {
                    var variant = skill.Variants.Find(v => v?.Id == item.VariantId);
                    if (variant == null) Error(result, "ability.variant-reference", value.Id, "Override variant reference is missing.");
                    else variant.Overrides.Add(value);
                }
            }
        }
    }
}
