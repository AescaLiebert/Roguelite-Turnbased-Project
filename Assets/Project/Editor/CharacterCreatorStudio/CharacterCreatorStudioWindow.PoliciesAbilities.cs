#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public sealed partial class CharacterCreatorStudioWindow
    {
        private void DrawPolicies(VisualElement parent = null, bool selectedOnly = false)
        {
            var fold = new Foldout { text = "Interception policies", value = selectedOnly }; (parent ?? _host).Add(fold);
            Body(fold, "Policies intercept incoming committed contacts. A fatal-save policy spends its cost and restores HP before defeat commits. Matching policies resolve by priority and stable ID.", "studio-subtle");
            foreach (var policy in _document.Policies.ToArray())
            {
                if (selectedOnly && policy.Id != _selectedNode) continue;
                var block = Node(fold, policy.Name ?? policy.Id); var p = policy.Definition;
                block.name = policy.Id;
                TextControl(block, "Policy name", policy.Name, v => policy.Name = v);
                EnumControl(block, "Kind", p.Kind, v => p.Kind = (CombatPolicyKind)v, true);
                IntControl(block, "Priority", p.Priority, v => p.Priority = v);
                DrawGate(block, p.Gate);
                ToggleControl(block, "Condition guard", !string.IsNullOrEmpty(policy.ConditionRootId), v => policy.ConditionRootId = v ? NewLeaf() : null, true);
                if (!string.IsNullOrEmpty(policy.ConditionRootId)) DrawCondition(block, policy.ConditionRootId, new HashSet<string>(), 0);
                if (p.Kind == CombatPolicyKind.IncomingDamageCap)
                {
                    IntControl(block, "Max HP loss / contact (bp)", p.MaximumHealthDamageBp, v => p.MaximumHealthDamageBp = v);
                    ParameterBinding(block, "Cap binding", policy.CapParameterId, StudioParameterUnit.BasisPoints, v => policy.CapParameterId = v);
                }
                if (p.Kind == CombatPolicyKind.BeforeDefeat)
                {
                    ResourcePicker(block, "Resource", p.ResourceKey, v => p.ResourceKey = v);
                    IntControl(block, "Atomic resource cost", p.ResourceCost, v => p.ResourceCost = v);
                    ParameterBinding(block, "Cost binding", policy.CostParameterId, StudioParameterUnit.Integer, v => policy.CostParameterId = v);
                    IntControl(block, "Restore max HP (bp)", p.HealMaxHealthBp, v => p.HealMaxHealthBp = v);
                    ParameterBinding(block, "Heal binding", policy.HealParameterId, StudioParameterUnit.BasisPoints, v => policy.HealParameterId = v);
                    ToggleControl(block, "Cleanse debuffs", p.CleanseDebuffs, v => p.CleanseDebuffs = v);
                }
                if (p.Kind != CombatPolicyKind.GaugeLossGuard)
                    ToggleControl(block, "Damage profile may bypass", p.Bypassable, v => p.Bypassable = v);
                Body(block, policy.Id, "studio-code");
                Button(block, "Remove policy", () => Edit("Remove policy", () => _document.Policies.Remove(policy), true));
            }
            Button(fold, "+ Interception policy", () => Edit("Add policy", () => _document.Policies.Add(new StudioPolicy {
                Id = "policy." + Guid.NewGuid().ToString("N"), Name = "Incoming HP cap" }), true));
        }

        private void SkillChoice(VisualElement parent, string skillId, Action<string> setter) =>
            ChoiceControl(parent, "Source skill", _document.EditingCharacter.Skills.OrderBy(s => s.Slot).Select(s => s.Id).ToList(), skillId, setter, true);

        private void DrawAbilities()
        {
            var fold = new Foldout { text = "Ability variants and upgrade values (" + _document.AbilityVariants.Count + " variants)", value = _page == "abilities" }; _host.Add(fold);
            Body(fold, "Level, card rank and constellation tier are independent axes. A ready variant changes resolution and card metadata while retaining card identity and rank. Imported base ranks remain frozen.", "studio-subtle");
            foreach (var item in _document.AbilityVariants.ToArray())
            {
                var v = item.Definition;
                var group = Node(fold, v.Name ?? v.Id);
                group.name = v.Id;
                TextControl(group, "Variant name", v.Name, value => v.Name = value);
                SkillChoice(group, item.SkillId, value => item.SkillId = value);
                IntControl(group, "Priority", v.Priority, value => v.Priority = value);
                ToggleControl(group, "Initially unlocked", v.InitiallyUnlocked, value => v.InitiallyUnlocked = value);
                ToggleControl(group, "Relock after successful use", v.RelockAfterUse, value => v.RelockAfterUse = value);
                IntControl(group, "Minimum level", v.MinimumLevel, value => v.MinimumLevel = value);
                IntControl(group, "Maximum level", v.MaximumLevel, value => v.MaximumLevel = value);
                IntControl(group, "Minimum tier", v.MinimumTier, value => v.MinimumTier = value);
                IntControl(group, "Maximum tier", v.MaximumTier, value => v.MaximumTier = value);
                ResourcePicker(group, "Readiness resource", v.RequiredResourceKey, value => v.RequiredResourceKey = value);
                IntControl(group, "Required resource amount", v.RequiredResourceAmount, value => v.RequiredResourceAmount = value);
                IntControl(group, "Resource cost per use", v.ResourceCost, value => v.ResourceCost = value);
                Body(group, "Unlock/relock this variant from an action's SetAbilityVariant command.", "studio-subtle");
                foreach (var rank in v.Ranks.OrderBy(r => r.Rank).ToArray())
                {
                    var r = new Foldout { text = "Variant card rank " + rank.Rank, value = false }; group.Add(r);
                    IntControl(r, "Card rank", rank.Rank, value => rank.Rank = value);
                    ToggleControl(r, "Override card category / target", rank.HasCardKind, value => rank.HasCardKind = value, true);
                    if (rank.HasCardKind)
                    {
                        EnumControl(r, "Card category", rank.Category, value => rank.Category = (CardCategory)value);
                        EnumControl(r, "Card target", rank.TargetScope, value => rank.TargetScope = (EffectTargetScope)value);
                    }
                    DrawVariantEffect(r, rank.Effect, "Primary effect");
                    foreach (var step in rank.Effect.Sequence.ToArray())
                    {
                        var sequence = new Foldout { text = "Sequence · " + step.Timing + " · " + step.Effect?.Id, value = false }; r.Add(sequence);
                        EnumControl(sequence, "Timing", step.Timing, value => step.Timing = (CardEffectTiming)value);
                        DrawVariantOperation(sequence, step.Effect);
                        Button(sequence, "Move step earlier", () => Edit("Reorder variant sequence", () => {
                            var index = rank.Effect.Sequence.IndexOf(step); if (index > 0) { rank.Effect.Sequence.RemoveAt(index); rank.Effect.Sequence.Insert(index - 1, step); }
                        }, true));
                        Button(sequence, "Remove sequence step", () => Edit("Remove variant step", () => rank.Effect.Sequence.Remove(step), true));
                    }
                    var addKind = new EnumField("New effect", EffectKind.Damage); r.Add(addKind);
                    Button(r, "+ Sequence step", () => Edit("Add variant step", () => rank.Effect.Sequence.Add(new CardEffectStep {
                        Timing = CardEffectTiming.AfterAction, Effect = CreateVariantOperation((EffectKind)addKind.value) }), true));
                }
                Button(group, "Reset ranks from selected base skill", () => Edit("Reset variant ranks", () => {
                    var skill = _document.EditingCharacter.Skills.FirstOrDefault(s => s.Id == item.SkillId);
                    if (skill != null) v.Ranks = skill.Ranks.ConvertAll(rank => rank.Clone());
                }, true));
                Body(group, v.Id, "studio-code");
                Button(group, "Remove variant", () => Edit("Remove variant", () => _document.AbilityVariants.Remove(item), true));
            }
            Button(fold, "+ Variant from base skill", () => Edit("Add ability variant", () =>
            {
                var skill = _document.EditingCharacter.Skills.OrderBy(s => s.Slot).FirstOrDefault();
                if (skill == null) return;
                _document.AbilityVariants.Add(new StudioAbilityVariant { SkillId = skill.Id, Definition = new AbilityVariantDefinition {
                    Id = "variant." + Guid.NewGuid().ToString("N"), Name = "EX", InitiallyUnlocked = true, Ranks = skill.Ranks.ConvertAll(r => r.Clone()) } });
            }, true));
            Section(fold, "PARAMETER OVERRIDES · APPLIED IN DISPLAYED ORDER");
            foreach (var item in _document.AbilityOverrides.ToArray())
            {
                var o = item.Definition; var row = Node(fold, o.Id);
                row.name = o.Id;
                SkillChoice(row, item.SkillId, value => item.SkillId = value);
                var variants = new List<string> { "Base skill" };
                variants.AddRange(_document.AbilityVariants.Where(v => v.SkillId == item.SkillId).Select(v => v.Definition.Id));
                ChoiceControl(row, "Variant", variants, string.IsNullOrEmpty(item.VariantId) ? "Base skill" : item.VariantId,
                    value => item.VariantId = value == "Base skill" ? null : value);
                TextControl(row, "Effect ID (empty = primary)", o.EffectId, value => o.EffectId = value);
                EnumControl(row, "Value field", o.Field, value => o.Field = (AbilityValueField)value, true);
                IntControl(row, "Fixed override value", o.Value, value => o.Value = value);
                ParameterBinding(row, "Value parameter", item.ValueParameterId,
                    o.Field == AbilityValueField.CoefficientBp || o.Field == AbilityValueField.HealCoefficientBp ? StudioParameterUnit.BasisPoints : StudioParameterUnit.Integer,
                    value => item.ValueParameterId = value);
                IntControl(row, "Minimum level", o.MinimumLevel, value => o.MinimumLevel = value);
                IntControl(row, "Maximum level", o.MaximumLevel, value => o.MaximumLevel = value);
                IntControl(row, "Minimum card rank", o.MinimumRank, value => o.MinimumRank = value);
                IntControl(row, "Maximum card rank", o.MaximumRank, value => o.MaximumRank = value);
                IntControl(row, "Minimum tier", o.MinimumTier, value => o.MinimumTier = value);
                IntControl(row, "Maximum tier", o.MaximumTier, value => o.MaximumTier = value);
                var buttons = Row(row);
                Button(buttons, "Move earlier", () => Edit("Reorder overrides", () => {
                    var index = _document.AbilityOverrides.IndexOf(item); if (index > 0) { _document.AbilityOverrides.RemoveAt(index); _document.AbilityOverrides.Insert(index - 1, item); }
                }, true));
                Button(buttons, "Remove override", () => Edit("Remove override", () => _document.AbilityOverrides.Remove(item), true));
            }
            Button(fold, "+ Upgrade value override", () => Edit("Add override", () =>
            {
                var skill = _document.EditingCharacter.Skills.OrderBy(s => s.Slot).FirstOrDefault(); if (skill == null) return;
                _document.AbilityOverrides.Add(new StudioAbilityOverride { SkillId = skill.Id, Definition = new AbilityValueOverride {
                    Id = "override." + Guid.NewGuid().ToString("N"), Field = AbilityValueField.CoefficientBp, Value = 10000 } });
            }, true));
        }

        private void DrawVariantEffect(VisualElement parent, EffectDefinition effect, string title)
        {
            if (effect == null) { Body(parent, "Missing variant effect", "studio-error"); return; }
            Section(parent, title); DrawVariantOperation(parent, effect);
        }
        private void DrawVariantOperation(VisualElement parent, EffectOperationDefinition effect)
        {
            if (effect == null) { Body(parent, "Missing sequence effect", "studio-error"); return; }
            Body(parent, effect.Id, "studio-code");
            EnumControl(parent, "Effect kind", effect.Kind, value => {
                effect.Kind = (EffectKind)value;
                if (effect.Kind == EffectKind.ApplyStatus && effect.StatusRecipe == null) effect.StatusRecipe = NewVariantStatus();
                if (effect.Kind == EffectKind.Heal && effect.HealValue == null) effect.HealValue = new EffectValueDefinition { Source = EffectValueSource.TargetMaxHealth, CoefficientBp = 2000 };
            }, true);
            EnumControl(parent, "Target", effect.Target, value => effect.Target = (EffectTargetScope)value);
            if (effect.Kind == EffectKind.ApplyStatus)
            {
                var ids = new List<string> { "Inline definition" }; ids.AddRange(_document.Statuses.Select(s => s.Id));
                ids.AddRange(StandardEffectDatabase.CreateStatusRecipes().Select(s => s.Id));
                var shared = _document.Statuses.Any(s => s.Id == effect.StatusRecipe?.Id);
                ChoiceControl(parent, "Status definition", ids.Distinct().ToList(), shared ? effect.StatusRecipe.Id : "Inline definition", value => {
                    effect.StatusRecipe = value == "Inline definition" ? NewVariantStatus() : _document.Statuses.Find(s => s.Id == value)?.Definition.Clone() ?? StandardEffectDatabase.CreateStatusRecipes().First(s => s.Id == value).Clone();
                }, true);
                if (!shared && effect.StatusRecipe != null) DrawStatusApplication(parent, new StatusApplicationRecipe { InlineRecipe = effect.StatusRecipe }, true);
                IntControl(parent, "Status layers", effect.StatusStackCount, value => effect.StatusStackCount = value);
                IntControl(parent, "Apply chance (bp)", effect.StatusApplyChanceBp, value => effect.StatusApplyChanceBp = value);
                IntControl(parent, "Duration override", effect.StatusDurationOverride, value => effect.StatusDurationOverride = value);
                return;
            }
            if (effect.Kind == EffectKind.Heal)
            {
                var healing = effect.HealValue ?? new EffectValueDefinition { Source = EffectValueSource.SourceAttack, CoefficientBp = effect.HealCoefficientBp };
                if (effect.HealValue == null) Body(parent, "Imported recovery scales from " + effect.HealScalingStat + ". Choosing an explicit basis replaces that imported value recipe.", "studio-subtle");
                EnumControl(parent, "Healing basis", healing.Source, value => { effect.HealValue = healing; healing.Source = (EffectValueSource)value; });
                IntControl(parent, "Healing coefficient (bp)", healing.CoefficientBp, value => {
                    if (effect.HealValue == null) effect.HealCoefficientBp = value;
                    else healing.CoefficientBp = value;
                });
                IntControl(parent, "Fixed recovery", healing.FixedAmount, value => { effect.HealValue = healing; healing.FixedAmount = value; });
                return;
            }
            if (effect.Kind == EffectKind.ChangePowerGauge) { IntControl(parent, "Gauge change", effect.PowerGaugeAmount, value => effect.PowerGaugeAmount = value); return; }
            if (effect.Kind == EffectKind.ModifyCardRank) { IntControl(parent, "Rank change", effect.Magnitude, value => effect.Magnitude = value); return; }
            if (effect.Kind != EffectKind.Damage) return;
            EnumControl(parent, "Scaling", effect.Scaling, value => effect.Scaling = (StatScaling)value);
            if (effect.Scaling == StatScaling.SpecificStat) EnumControl(parent, "Scaling stat", effect.ScalingStat, value => effect.ScalingStat = (StatId)value);
            EnumControl(parent, "Family", effect.Family, value => effect.Family = (DamageFamily)value);
            IntControl(parent, "Coefficient (bp)", effect.CoefficientBp, value => effect.CoefficientBp = value);
            IntControl(parent, "Magnitude", effect.Magnitude, value => effect.Magnitude = value);
            var attack = effect.Attack ?? new DamageAttackDefinition();
            IntControl(parent, "Logical hit count", attack.HitCount, value => { effect.Attack = attack; attack.HitCount = value; });
            ToggleControl(parent, "Explicit damage profile", effect.DamageProfile != null,
                value => effect.DamageProfile = value ? new DamageProfileDefinition { Id = effect.Id + ".profile" } : null, true);
            if (effect.DamageProfile != null) DrawDamageProfile(parent, effect.DamageProfile);
        }
        private static StatusRecipeDefinition NewVariantStatus() => new StatusRecipeDefinition {
            Id = "status." + Guid.NewGuid().ToString("N"), NameKey = "Variant status", MaxStacks = 1 };
        private static EffectOperationDefinition CreateVariantOperation(EffectKind kind) => new EffectOperationDefinition {
            Id = "effect." + Guid.NewGuid().ToString("N"), Kind = kind, Scaling = StatScaling.Attack,
            Magnitude = kind == EffectKind.ModifyCardRank ? 1 : 0,
            Target = kind == EffectKind.Heal ? EffectTargetScope.Self : EffectTargetScope.SelectedEnemy,
            StatusRecipe = kind == EffectKind.ApplyStatus ? NewVariantStatus() : null,
            HealValue = kind == EffectKind.Heal ? new EffectValueDefinition { Source = EffectValueSource.TargetMaxHealth, CoefficientBp = 2000 } : null };
    }
}
#endif
