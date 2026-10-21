#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public sealed partial class CharacterCreatorStudioWindow
    {
        private StudioActionNode NewAction(StudioActionKind kind)
        {
            var node = new StudioActionNode { Id = "action." + Guid.NewGuid().ToString("N"), Kind = kind, Name = kind.ToString(),
                ResourceKey = "resource.new", StatusRecipeId = "status.new" };
            if (kind == StudioActionKind.Branch) node.ConditionRootId = NewLeaf();
            if (kind == StudioActionKind.ForEachTarget) node.Query = NewQuery(EffectTargetScope.AllEnemies);
            _document.Actions.Add(node); return node;
        }

        private void DrawAction(VisualElement parent, string id, HashSet<string> path, int depth, bool inspectorOnly = false)
        {
            if (depth > 64 || !path.Add(id ?? "")) { Body(parent, "Action/module cycle or excessive depth at " + id, "studio-error"); return; }
            var action = _document.Actions.FirstOrDefault(a => a.Id == id);
            if (action == null) { Body(parent, "Missing action " + id, "studio-error"); return; }
            var block = new Foldout { text = ActionFoldoutHeading(action), value = depth == 0, name = action.Id };
            block.AddToClassList("studio-node"); parent.Add(block);
            var nameField = new TextField("Action node name") { value = action.Name ?? "" };
            nameField.tooltip = "A designer-facing label for this node. It does not change the action ID or runtime behavior.";
            nameField.RegisterValueChangedCallback(e => Edit("Rename action node", () => action.Name = e.newValue));
            nameField.RegisterValueChangedCallback(_ => block.text = ActionFoldoutHeading(action));
            block.Add(nameField);
            EnumControl(block, "Action kind", action.Kind, v =>
            {
                action.Kind = (StudioActionKind)v;
                if (action.Kind == StudioActionKind.Module) { action.Children.Clear(); action.ElseChildren.Clear(); PruneActions(); }
                if (action.Kind == StudioActionKind.Branch && string.IsNullOrEmpty(action.ConditionRootId)) action.ConditionRootId = NewLeaf();
                if (action.Kind == StudioActionKind.ForEachTarget && action.Query == null) action.Query = NewQuery(action.Target);
            }, true);
            if (action.Kind == StudioActionKind.Branch)
            {
                Section(block, "IF"); DrawCondition(block, action.ConditionRootId, new HashSet<string>(), 0);
            }
            else
            {
                ToggleControl(block, "Guard this action", !string.IsNullOrEmpty(action.ConditionRootId), v =>
                { action.ConditionRootId = v ? NewLeaf() : null; PruneConditions(); }, true);
                if (!string.IsNullOrEmpty(action.ConditionRootId)) DrawCondition(block, action.ConditionRootId, new HashSet<string>(), 0);
            }
            switch (action.Kind)
            {
                case StudioActionKind.Command: DrawCommand(block, action); break;
                case StudioActionKind.ForEachTarget:
                    IntControl(block, "Maximum targets (1–32)", action.MaximumTargets, v => action.MaximumTargets = v);
                    DrawQuery(block, action.Query); break;
                case StudioActionKind.SpendResource:
                case StudioActionKind.TransferResource:
                    ResourcePicker(block, "Resource", action.ResourceKey, v => action.ResourceKey = v);
                    IntControl(block, "Amount", action.Amount, v => action.Amount = v);
                    ParameterBinding(block, "Amount source", action.AmountParameterId, StudioParameterUnit.Integer, v => action.AmountParameterId = v);
                    if (action.Kind == StudioActionKind.TransferResource)
                    {
                        IntControl(block, "Recipient cap", action.ResourceCap, v => action.ResourceCap = v);
                        Body(block, "Transfers from owner to OperationTarget. Use inside a target loop to choose the recipient. Both values commit before observers run.", "studio-subtle");
                    }
                    else Body(block, "Exact spend from owner. Insufficient funds execute the failure branch without consuming resources.", "studio-subtle");
                    break;
                case StudioActionKind.ConsumeStatusLayers:
                    StatusPicker(block, "Status", action.StatusRecipeId, v => action.StatusRecipeId = v);
                    IntControl(block, "Layer count", action.Amount, v => action.Amount = v);
                    ParameterBinding(block, "Layer source", action.AmountParameterId, StudioParameterUnit.Integer, v => action.AmountParameterId = v);
                    ToggleControl(block, "Only statuses applied by owner", action.StatusOwnerOnly, v => action.StatusOwnerOnly = v);
                    ToggleControl(block, "Require full amount", action.RequireFullAmount, v => action.RequireFullAmount = v);
                    EnumControl(block, "Removal reason", action.RemovalReason, v => action.RemovalReason = (StatusRemovalReason)v);
                    TextControl(block, "Consumed count binding", action.ResultBinding, v => action.ResultBinding = v);
                    Body(block, "Consumes OperationTarget layers, oldest first. Children can read the removal snapshot and bound count.", "studio-subtle"); break;
                case StudioActionKind.Module:
                    ChoiceControl(block, "Module", _document.Modules.Select(m => m.Id).ToList(), action.ModuleId, v => action.ModuleId = v, true);
                    var module = _document.Modules.FirstOrDefault(m => m.Id == action.ModuleId);
                    if (module != null)
                    {
                        Body(block, "Shared module: " + module.Name + ". Editing the module updates every local call.", "studio-subtle");
                        if (inspectorOnly) Button(block, "Edit shared module", () => Navigate("modules", module.Id));
                        else DrawAction(block, module.RootActionId, new HashSet<string>(path), depth + 1);
                    }
                    break;
            }
            if (action.Kind != StudioActionKind.Module && !inspectorOnly)
            {
                var guarded = action.Kind == StudioActionKind.SpendResource || action.Kind == StudioActionKind.TransferResource || action.Kind == StudioActionKind.ConsumeStatusLayers;
                DrawActionChildren(block, action.Children, guarded ? "ON SUCCESS" : action.Kind == StudioActionKind.Branch ? "THEN" : "NEXT · IN ORDER", path, depth);
                if (guarded || action.Kind == StudioActionKind.Branch || !string.IsNullOrEmpty(action.ConditionRootId))
                    DrawActionChildren(block, action.ElseChildren, guarded ? "ON FAILURE" : "ELSE", path, depth);
            }
            Body(block, action.Id, "studio-code");
        }

        private void DrawActionChildren(VisualElement parent, List<string> children, string label, HashSet<string> path, int depth)
        {
            Section(parent, label);
            foreach (var child in children.ToArray())
            {
                DrawAction(parent, child, new HashSet<string>(path), depth + 1);
                var row = Row(parent);
                Button(row, "Remove step", () => Edit("Remove action reference", () => { children.Remove(child); PruneActions(); PruneConditions(); }, true));
                Button(row, "Move up", () => Edit("Reorder action", () => MoveUp(children, child), true));
            }
            var addKind = new EnumField("New step", StudioActionKind.Command); parent.Add(addKind);
            Button(parent, "+ Add step", () => Edit("Add action", () => children.Add(NewAction((StudioActionKind)addKind.value).Id), true));
        }

        private void DrawCommand(VisualElement parent, StudioActionNode node, bool native = false, bool? calculationOverride = null)
        {
            var command = node.Command;
            var calculation = calculationOverride ?? (_document.Rules.Find(r => r.Id == _selectedRule)?.Trigger == PassiveEventKind.BeforeDamage);
            bool Local(PassiveCommandKind k) => k == PassiveCommandKind.IncreaseCurrentAttackPercent || k == PassiveCommandKind.IncreaseCurrentCritChancePercent ||
                k == PassiveCommandKind.IncreaseCurrentDamageDealtPercent || k == PassiveCommandKind.IgnoreCurrentTargetCritDefensePercent ||
                k == PassiveCommandKind.IncreaseCurrentDamageDealtByBaseStatPercent;
            var kinds = Enum.GetValues(typeof(PassiveCommandKind)).Cast<PassiveCommandKind>().Where(k => Local(k) == calculation || k == command.Kind).ToArray();
            if (calculation) Body(parent, "This stage accepts fixed modifiers for the current hit. State-changing actions are rejected by validation.", "studio-subtle");
            ChoiceControl(parent, "Command", kinds.Select(k => k.ToString()).ToList(), command.Kind.ToString(), v =>
            {
                command.Kind = (PassiveCommandKind)Enum.Parse(typeof(PassiveCommandKind), v);
                if (command.Kind == PassiveCommandKind.ExecuteEffect && command.Effect == null) command.Effect = NewOperation(CardEffectOperationKind.ChangePowerGauge);
            }, true);
            if (command.Kind == PassiveCommandKind.SetAbilityVariant)
            {
                SkillChoice(parent, node.Command.SkillId, v => command.SkillId = v);
                ChoiceControl(parent, "Variant", _document.AbilityVariants.Where(v => v.SkillId == command.SkillId).Select(v => v.Definition.Id)
                    .Concat(_document.EditingCharacter.Skills.Where(s => s.Id == command.SkillId).SelectMany(s => s.Variants.Select(v => v.Id))).Distinct().ToList(),
                    command.VariantId, v => command.VariantId = v);
                ToggleControl(parent, "Unlocked", command.VariantUnlocked, v => command.VariantUnlocked = v);
                return;
            }
            if (command.Kind == PassiveCommandKind.ExecuteEffect) DrawOperation(parent, node, native);
            else
            {
                if (command.Kind == PassiveCommandKind.IncrementCounter || command.Kind == PassiveCommandKind.SetCounter)
                {
                    ResourcePicker(parent, "Resource", command.CounterKey, v => command.CounterKey = v);
                    var sharedResource = _document.Resources.Find(r => r.Id == command.CounterKey);
                    if (sharedResource == null) IntControl(parent, "Resource cap", command.CounterCap, v => command.CounterCap = v);
                    else Body(parent, "Shared maximum: " + sharedResource.Cap + " · edit in Resources & statuses", "studio-subtle");
                }
                if (Local(command.Kind) && command.Kind != PassiveCommandKind.IncreaseCurrentDamageDealtByBaseStatPercent)
                    BasisPointPercentControl(parent, "Bonus (%)", command.Amount, v => command.Amount = v);
                else IntControl(parent, "Amount", command.Amount, v => command.Amount = v);
                if (command.Kind == PassiveCommandKind.IncreaseCurrentDamageDealtByBaseStatPercent)
                    EnumControl(parent, "Source base stat", command.ScalingStat, v => command.ScalingStat = (StatId)v);
                if (!native) ParameterBinding(parent, "Amount source", node.AmountParameterId, StudioParameterUnit.Integer, v => node.AmountParameterId = v);
            }
            if (native) return;
            var expression = new Foldout { text = "Event values and typed bindings", value = false }; parent.Add(expression);
            ChoiceControl(expression, "Value basis", AvailableValueSources(node.Id).Select(v => v.ToString()).ToList(), command.ValueSource.ToString(),
                v => command.ValueSource = (PassiveValueSource)Enum.Parse(typeof(PassiveValueSource), v), true);
            if (command.ValueSource == PassiveValueSource.Binding) BindingPicker(expression, "Value binding", node.Id, command.ValueBinding, false, v => command.ValueBinding = v);
            if (command.Kind == PassiveCommandKind.ExecuteEffect)
            {
                var fields = new List<PassiveEffectBindingField> { PassiveEffectBindingField.None, PassiveEffectBindingField.Magnitude };
                if (command.Effect?.Kind == CardEffectOperationKind.Damage) fields.AddRange(new[] { PassiveEffectBindingField.DamageCoefficientBp, PassiveEffectBindingField.DamageFixedAmount });
                if (command.Effect?.Kind == CardEffectOperationKind.Heal) fields.AddRange(new[] { PassiveEffectBindingField.HealCoefficientBp, PassiveEffectBindingField.HealFixedAmount });
                ChoiceControl(expression, "Computed field", fields.Select(f => f.ToString()).ToList(), command.EffectBinding.ToString(), v => command.EffectBinding = (PassiveEffectBindingField)Enum.Parse(typeof(PassiveEffectBindingField), v));
                IntControl(expression, "Multiplier", command.Amount, v => command.Amount = v);
                ParameterBinding(expression, "Multiplier source", node.MultiplierParameterId, StudioParameterUnit.Integer, v => node.MultiplierParameterId = v);
            }
            IntControl(expression, "Scale (basis points)", command.ScalingCoefficientBp, v => command.ScalingCoefficientBp = v);
            Body(expression, "Computed value = basis × multiplier × scale / 10000. Removed layers and damage/healing values come from committed trigger facts.", "studio-subtle");
        }

        private static CardEffectOperationDefinition NewOperation(CardEffectOperationKind kind) => new CardEffectOperationDefinition {
            Kind = kind, Target = EffectTargetScope.TriggerTarget, Magnitude = 1,
            Damage = kind == CardEffectOperationKind.Damage ? new DamageEffectRecipe { CoefficientBp = 10000 } : null,
            Value = kind == CardEffectOperationKind.Heal ? new EffectValueDefinition { Source = EffectValueSource.TargetMaxHealth, CoefficientBp = 2000 } : null,
            Status = kind == CardEffectOperationKind.ApplyStatus ? new StatusApplicationRecipe { StatusRecipeId = StandardEffectDatabase.CreateStatusRecipes().FirstOrDefault()?.Id } : null };

        private void DrawOperation(VisualElement parent, StudioActionNode node, bool native = false)
        {
            var op = node.Command.Effect;
            ChoiceControl(parent, "Operation", StudioAuthoringCatalog.Operations.Select(k => k.ToString()).ToList(), op.Kind.ToString(),
                v => node.Command.Effect = NewOperation((CardEffectOperationKind)Enum.Parse(typeof(CardEffectOperationKind), v)), true);
            EnumControl(parent, "Operation target", op.Target, v => op.Target = (EffectTargetScope)v);
            ToggleControl(parent, "Use target query", op.TargetQuery != null, v => op.TargetQuery = v ? NewQuery(op.Target) : null, true);
            if (op.TargetQuery != null) DrawQuery(parent, op.TargetQuery);
            if (op.Kind == CardEffectOperationKind.Damage)
            {
                DrawDamagePacket(parent, op.Damage, node.Id);
            }
            else if (op.Kind == CardEffectOperationKind.Heal)
            {
                EnumControl(parent, "Healing basis", op.Value.Source, v => op.Value.Source = (EffectValueSource)v);
                IntControl(parent, "Heal coefficient (bp)", op.Value.CoefficientBp, v => op.Value.CoefficientBp = v);
                IntControl(parent, "Fixed healing", op.Value.FixedAmount, v => op.Value.FixedAmount = v);
                IntControl(parent, "Maximum healing", op.Value.MaximumAmount, v => op.Value.MaximumAmount = v);
            }
            else if (op.Kind == CardEffectOperationKind.ApplyStatus) DrawStatusApplication(parent, op.Status);
            else if (op.Kind == CardEffectOperationKind.Cleanse || op.Kind == CardEffectOperationKind.RemoveStatus)
            {
                EnumControl(parent, "Remove polarity", op.RemovePolarity, v => op.RemovePolarity = (StatusPolarity)v);
                TextControl(parent, "Only recipe (empty = any)", op.RemoveRecipeId, v => op.RemoveRecipeId = v);
                ToggleControl(parent, "Remove all matches", op.RemoveAll, v => op.RemoveAll = v);
            }
            else IntControl(parent, op.Kind == CardEffectOperationKind.ChangePowerGauge ? "Gauge change (-5…5)" : "Rank change (-2…2)", op.Magnitude, v => op.Magnitude = v);
            if (!native && op.Kind != CardEffectOperationKind.Cleanse && op.Kind != CardEffectOperationKind.RemoveStatus)
                ParameterBinding(parent, "Fixed amount / layer binding", node.AmountParameterId, StudioParameterUnit.Integer, v => node.AmountParameterId = v);
            if (!native && (op.Kind == CardEffectOperationKind.Damage || op.Kind == CardEffectOperationKind.Heal))
                ParameterBinding(parent, "Coefficient binding", node.CoefficientParameterId, StudioParameterUnit.BasisPoints, v => node.CoefficientParameterId = v);
        }

        private void DrawDamagePacket(VisualElement parent, DamageEffectRecipe damage, string id)
        {
                EnumControl(parent, "Damage family", damage.Family, v => damage.Family = (DamageFamily)v);
                EnumControl(parent, "Scaling stat", damage.Scaling, v => damage.Scaling = (StatScaling)v);
                EnumControl(parent, "Stat source", damage.StatSource, v => damage.StatSource = (DamageStatSource)v);
                if (damage.Scaling == StatScaling.SpecificStat) EnumControl(parent, "Specific stat", damage.ScalingStat, v => damage.ScalingStat = (StatId)v);
                IntControl(parent, "Damage coefficient (bp)", damage.CoefficientBp, v => damage.CoefficientBp = v);
                IntControl(parent, "Fixed damage", damage.FixedAmount, v => damage.FixedAmount = v);
                ToggleControl(parent, "Cannot crit", damage.CannotCrit, v => damage.CannotCrit = v);
                ToggleControl(parent, "Cannot block", damage.CannotBlock, v => damage.CannotBlock = v);
                ToggleControl(parent, "Explicit damage profile", damage.Profile != null, v => damage.Profile = v ? new DamageProfileDefinition { Id = id + ".profile" } : null, true);
                if (damage.Profile != null) DrawDamageProfile(parent, damage.Profile);
        }

        private void DrawStatusApplication(VisualElement parent, StatusApplicationRecipe app, bool definitionOnly = false)
        {
            if (!definitionOnly) ToggleControl(parent, "Author inline status", app.InlineRecipe != null, v =>
            { app.InlineRecipe = v ? new StatusRecipeDefinition { Id = "status." + Guid.NewGuid().ToString("N"), Stacking = StatusStackingPolicy.AddStacks, MaxStacks = 6 } : null; }, true);
            if (app.InlineRecipe == null)
                StatusPicker(parent, "Shared / standard status", app.StatusRecipeId, v => app.StatusRecipeId = v);
            else
            {
                var s = app.InlineRecipe;
                StatusPicker(parent, "Inline status recipe", s.Id, v => s.Id = v);
                EnumControl(parent, "Polarity", s.Polarity, v => s.Polarity = (StatusPolarity)v);
                EnumControl(parent, "Color", s.Color, v => s.Color = (StatusColor)v);
                EnumControl(parent, "Stacking", s.Stacking, v => s.Stacking = (StatusStackingPolicy)v);
                EnumControl(parent, "Identity scope", s.Identity, v => s.Identity = (StatusIdentityScope)v);
                EnumControl(parent, "Duration clock", s.DurationClock, v => s.DurationClock = (StatusDurationClock)v);
                IntControl(parent, "Default duration", s.DefaultDuration, v => s.DefaultDuration = v);
                IntControl(parent, "Maximum layers", s.MaxStacks, v => s.MaxStacks = v);
                EnumControl(parent, "Break / source lifetime", s.BreakRule, v => s.BreakRule = (StatusBreakRule)v);
                TextControl(parent, "Tags (comma separated)", string.Join(", ", s.Tags), v => s.Tags = v.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).Distinct().ToList());
                ToggleControl(parent, "Prevents recovery", (s.Behavior & StatusBehavior.PreventsRecovery) != 0, v => s.Behavior = v ? s.Behavior | StatusBehavior.PreventsRecovery : s.Behavior & ~StatusBehavior.PreventsRecovery);
                ToggleControl(parent, "Additional damage immunity", s.AdditionalDamageImmunity, v => s.AdditionalDamageImmunity = v);
                foreach (var m in s.Modifiers.ToArray())
                {
                    var row = Node(parent, "Stat / damage modifier");
                    EnumControl(row, "Target", m.ResolvedTarget, v => { m.Target = (ModifierTarget)v; m.SerializedTarget = -1; });
                    EnumControl(row, "Stat", m.ResolvedStat, v => { m.Stat = (StatId)v; m.SerializedStat = -1; });
                    EnumControl(row, "Bundle", m.ResolvedBundle, v => { m.Bundle = (StatBundleKind)v; m.SerializedBundle = -1; });
                    EnumControl(row, "Operation", m.ResolvedOperation, v => { m.Operation = (ModifierOperation)v; m.SerializedOperation = -1; });
                    EnumControl(row, "Damage family", m.ResolvedFamily, v => { m.Family = (DamageFamily)v; m.SerializedFamily = -1; });
                    IntControl(row, "Amount", m.Amount, v => m.Amount = v);
                    Button(row, "Remove modifier", () => Edit("Remove modifier", () => s.Modifiers.Remove(m), true));
                }
                Button(parent, "+ Status modifier", () => Edit("Add modifier", () => s.Modifiers.Add(new StatModifierDefinition { Stat = StatId.Attack, Operation = ModifierOperation.PercentOfBase, Amount = 1000 }), true));
            }
            Body(parent, "The matching StatusVisualData asset is resolved by this ID; the asset supplies presentation while the recipe supplies gameplay behavior.", "studio-subtle");
            if (definitionOnly) return;
            IntControl(parent, "Layers", app.StackCount, v => app.StackCount = v);
            IntControl(parent, "Chance (basis points)", app.ProcChanceBp, v => app.ProcChanceBp = v);
            IntControl(parent, "Duration override (0 = recipe)", app.DurationOverride, v => app.DurationOverride = v);
        }

        private static TargetQueryDefinition NewQuery(EffectTargetScope scope) => new TargetQueryDefinition {
            BaseScope = scope, Filter = new PassiveTargetFilter { Relation = PassiveRelation.Any, IncludeReserve = false }, Limit = 16 };
        private void DrawQuery(VisualElement parent, TargetQueryDefinition q)
        {
            var fold = new Foldout { text = "Target selector", value = true }; parent.Add(fold);
            EnumControl(fold, "Candidate scope", q.BaseScope, v => q.BaseScope = (EffectTargetScope)v);
            EnumControl(fold, "Scope anchor", q.Anchor, v => q.Anchor = (TargetQueryAnchor)v);
            DrawFilter(fold, q.Filter);
            ToggleControl(fold, "Exclude actor", q.ExcludeActor, v => q.ExcludeActor = v);
            ToggleControl(fold, "Exclude owner", q.ExcludeOwner, v => q.ExcludeOwner = v);
            ToggleControl(fold, "Exclude event target", q.ExcludeEventTarget, v => q.ExcludeEventTarget = v);
            ToggleControl(fold, "Adjacent logical slots only", q.AdjacentToEventTarget, v => q.AdjacentToEventTarget = v);
            EnumControl(fold, "Order", q.Sort, v => q.Sort = (TargetQuerySort)v, true);
            if (q.Sort == TargetQuerySort.LowestRecipeLayers) TextControl(fold, "Layer recipe", q.RecipeId, v => q.RecipeId = v);
            IntControl(fold, "Result limit (1–32)", q.Limit, v => q.Limit = v);
            IntControl(fold, "Sample without replacement (0 = all)", q.SampleCount, v => q.SampleCount = v);
            BindingPicker(fold, "Reuse saved targets", _selectedNode, q.ReuseBinding, true, v => q.ReuseBinding = v, true);
            BindingPicker(fold, "Exclude saved targets", _selectedNode, q.ExcludeBinding, true, v => q.ExcludeBinding = v, true);
            TextControl(fold, "Store target binding", q.StoreBinding, v => q.StoreBinding = v);
        }

        private void DrawModules()
        {
            var fold = new Foldout { text = "Reusable local modules (" + _document.Modules.Count + ")", value = false }; _host.Add(fold);
            foreach (var m in _document.Modules.ToArray())
            {
                TextControl(fold, "Module name", m.Name, v => m.Name = v); Body(fold, m.Id, "studio-code");
                DrawAction(fold, m.RootActionId, new HashSet<string>(), 0);
                Button(fold, "Remove module", () => Edit("Remove module", () => { _document.Modules.Remove(m); PruneActions(); PruneConditions(); }, true));
            }
            Button(fold, "+ Module", () => Edit("Add module", () => _document.Modules.Add(new StudioModule {
                Id = "module." + Guid.NewGuid().ToString("N"), Name = "New module", RootActionId = NewAction(StudioActionKind.Sequence).Id }), true));
        }

        private void DrawDamageProfile(VisualElement parent, DamageProfileDefinition p)
        {
            ToggleControl(parent, "Explicit true damage", p.ExplicitTrueDamage, v => p.ExplicitTrueDamage = v);
            IntControl(parent, "True damage bonus (bp)", p.TrueDamageBonusBp, v => p.TrueDamageBonusBp = v);
            ToggleControl(parent, "Bypass defense", p.BypassDefense, v => p.BypassDefense = v);
            ToggleControl(parent, "Bypass resistance", p.BypassResistance, v => p.BypassResistance = v);
            ToggleControl(parent, "Bypass generic reduction", p.BypassGenericReduction, v => p.BypassGenericReduction = v);
            ToggleControl(parent, "Bypass final reduction", p.BypassFinalReduction, v => p.BypassFinalReduction = v);
            ToggleControl(parent, "Bypass flat reduction", p.BypassFlatReduction, v => p.BypassFlatReduction = v);
            ToggleControl(parent, "Bypass shield", p.BypassShield, v => p.BypassShield = v);
            ToggleControl(parent, "Bypass bypassable cap", p.BypassDamageCap, v => p.BypassDamageCap = v);
            ToggleControl(parent, "Bypass bypassable fatal save", p.BypassFatalSave, v => p.BypassFatalSave = v);
        }

        private void PruneActions()
        {
            var keep = new HashSet<string>(); Action<string> visit = null;
            visit = id => {
                if (id == null || !keep.Add(id)) return;
                var node = _document.Actions.FirstOrDefault(a => a.Id == id); if (node == null) return;
                foreach (var child in node.Children.Concat(node.ElseChildren)) visit(child);
                var module = _document.Modules.FirstOrDefault(m => m.Id == node.ModuleId);
                if (node.Kind == StudioActionKind.Module && module != null) visit(module.RootActionId);
            };
            foreach (var rule in _document.Rules) { visit(rule.ActionRootId); visit(rule.ElseActionRootId); }
            foreach (var module in _document.Modules) visit(module.RootActionId);
            _document.Actions.RemoveAll(a => !keep.Contains(a.Id));
        }
    }
}
#endif
