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
        private void DrawEventFilters(VisualElement parent, StudioRule rule)
        {
            var s = rule.Settings;
            ToggleControl(parent, "Own team turn only", s.OwnTeamTurnOnly, v => s.OwnTeamTurnOnly = v);
            ToggleControl(parent, "Card origin only", s.CardOriginOnly, v => s.CardOriginOnly = v);
            if (rule.RuntimeVersion == 1) ToggleControl(parent, "Allow reaction-origin facts", s.AllowReactionOrigin, v => s.AllowReactionOrigin = v);
            ToggleControl(parent, "Exclude ultimate", s.ExcludeUltimate, v => s.ExcludeUltimate = v);
            if (rule.Trigger == PassiveEventKind.DamageResolved || rule.Trigger == PassiveEventKind.BeforeDamage)
            {
                if (rule.Trigger == PassiveEventKind.DamageResolved)
                {
                EnumControl(parent, "Damage timing", s.DamageTiming, v => s.DamageTiming = (PassiveDamageTiming)v);
                ToggleControl(parent, "Critical required", s.RequireCritical, v => s.RequireCritical = v);
                ToggleControl(parent, "Blocked required", s.RequireBlocked, v => s.RequireBlocked = v);
                }
                ToggleControl(parent, "Filter damage family", s.FilterDamageFamily, v => s.FilterDamageFamily = v, true);
                if (s.FilterDamageFamily) EnumControl(parent, "Damage family", s.DamageFamily, v => s.DamageFamily = (DamageFamily)v);
            }
            ToggleControl(parent, "Filter card category", s.FilterCategory, v => s.FilterCategory = v, true);
            if (s.FilterCategory)
            {
                EnumControl(parent, "Card category", s.Category, v => s.Category = (CardCategory)v);
                IntControl(parent, "Minimum card rank (0 = any)", s.MinimumRank, v => s.MinimumRank = v);
            }
            if (rule.Trigger == PassiveEventKind.GaugeChanged) ToggleControl(parent, "Gauge loss required", s.RequireGaugeLoss, v => s.RequireGaugeLoss = v);
            if (rule.Trigger == PassiveEventKind.ResourceChanged) ResourcePicker(parent, "Event resource", s.EventResourceKey, v => s.EventResourceKey = v);
            if (rule.Trigger == PassiveEventKind.StatusApplied || rule.Trigger == PassiveEventKind.StatusRemoved)
                TextControl(parent, "Status recipe (empty = any)", s.EventStatusRecipeId, v => s.EventStatusRecipeId = v);
            if (rule.Trigger == PassiveEventKind.StatusRemoved)
            {
                ToggleControl(parent, "Filter removal reason", s.FilterRemovalReason, v => s.FilterRemovalReason = v, true);
                if (s.FilterRemovalReason) EnumControl(parent, "Removal reason", s.RemovalReason, v => s.RemovalReason = (StatusRemovalReason)v);
            }
        }

        private void DrawCondition(VisualElement parent, string id, HashSet<string> path, int depth)
        {
            if (depth > 32 || !path.Add(id ?? "")) { Body(parent, "Cycle/depth error at " + id, "studio-error"); return; }
            var node = _document.Conditions.FirstOrDefault(n => n.Id == id);
            if (node == null) { Body(parent, "Missing condition " + id, "studio-error"); return; }
            var block = Node(parent, "Condition"); block.name = node.Id;
            ToggleControl(block, "Negate result", node.Negate, v => node.Negate = v);
            if (node.Id == _selectedCondition) block.AddToClassList("studio-active");
            EnumControl(block, "Group", node.Logic, v =>
            {
                node.Logic = (ConditionLogic)v;
                if (node.Logic == ConditionLogic.Leaf) node.Children.Clear();
                else if (node.Children.Count == 0) node.Children.Add(NewLeaf());
                if (node.Logic == ConditionLogic.Not && node.Children.Count > 1) node.Children.RemoveRange(1, node.Children.Count - 1);
                PruneConditions();
            }, true);
            if (node.Logic == ConditionLogic.Leaf)
            {
                DrawConditionPredicate(block, node);
            }
            else
            {
                foreach (var child in node.Children.ToArray())
                {
                    DrawCondition(block, child, new HashSet<string>(path), depth + 1);
                    var row = Row(block);
                    Button(row, "Remove child", () => Edit("Remove condition", () => { node.Children.Remove(child); PruneConditions(); }, true));
                    Button(row, "Move up", () => Edit("Reorder conditions", () => MoveUp(node.Children, child), true));
                }
                if (node.Logic != ConditionLogic.Not || node.Children.Count == 0) Button(block, "+ Condition", () => Edit("Add condition", () => node.Children.Add(NewLeaf()), true));
            }
            Body(block, node.Id, "studio-code");
        }

        private void DrawConditionPredicate(VisualElement parent, StudioConditionNode node, Action apply = null)
        {
                ChoiceControl(parent, "Condition", StudioAuthoringCatalog.Conditions.Select(k => k.ToString()).ToList(), node.Kind.ToString(), v =>
                { node.Kind = (EffectConditionKind)Enum.Parse(typeof(EffectConditionKind), v); node.ThresholdParameterId = null; node.Threshold = IsHealthCondition(node.Kind) ? 10000 : 1; apply?.Invoke(); }, true);
                EnumControl(parent, "Subject", node.Subject, v => { node.Subject = (ConditionSubject)v; apply?.Invoke(); });
                if (node.Kind != EffectConditionKind.Always)
                {
                    IntControl(parent, IsHealthCondition(node.Kind) ? "Threshold (basis points)" : "Threshold (count)", node.Threshold, v => { node.Threshold = v; apply?.Invoke(); });
                    if (apply == null) ParameterBinding(parent, "Threshold source", node.ThresholdParameterId, IsHealthCondition(node.Kind) ? StudioParameterUnit.BasisPoints : StudioParameterUnit.Integer, v => node.ThresholdParameterId = v);
                }
                if (node.Kind == EffectConditionKind.SourceHasStatusTag || node.Kind == EffectConditionKind.TargetHasStatusTag) TextControl(parent, "Status tag", node.Tag, v => { node.Tag = v; apply?.Invoke(); });
                if (node.Kind == EffectConditionKind.TargetHasRecipe || node.Kind == EffectConditionKind.TargetHasRecipeFromEffectOwner || node.Kind == EffectConditionKind.RemovedLayersAtLeast)
                    StatusPicker(parent, "Status", node.RecipeId, v => { node.RecipeId = v; apply?.Invoke(); });
                if (node.Kind == EffectConditionKind.CounterAtLeast || node.Kind == EffectConditionKind.ResourceEnteredAtLeast || node.Kind == EffectConditionKind.ResourceExitedBelow)
                    ResourcePicker(parent, "Resource", node.StringValue, v => { node.StringValue = v; apply?.Invoke(); });
                if (apply == null && node.Kind == EffectConditionKind.BindingValueAtLeast) BindingPicker(parent, "Value binding", node.Id, node.StringValue, false, v => { node.StringValue = v; apply?.Invoke(); });
                if (node.Kind == EffectConditionKind.TargetAttributeIs || node.Kind == EffectConditionKind.TargetTraitIs || node.Kind == EffectConditionKind.TargetSeriesIs)
                    TextControl(parent, "Content identity", node.StringValue, v => { node.StringValue = v; apply?.Invoke(); });
                if (node.Kind == EffectConditionKind.RosterCountAtLeast)
                { DrawFilter(parent, node.RosterFilter, apply); ToggleControl(parent, "Use initial roster snapshot", node.InitialRoster, v => { node.InitialRoster = v; apply?.Invoke(); }); }
        }

        private void DrawGate(VisualElement parent, PassiveGate gate)
        {
            ChoiceControl(parent, "Active position", new List<string> { "On-Field", "Off-Field" },
                gate.Presence == PassivePresence.LivingActive ? "On-Field" : gate.Presence == PassivePresence.LivingReserve ? "Off-Field" : "Both positions (existing)",
                v => { if (v == "On-Field" || v == "Off-Field") gate.Presence = v == "On-Field" ? PassivePresence.LivingActive : PassivePresence.LivingReserve; });
            ChoiceControl(parent, "Battle mode", new List<string> { "ALL", "PVP", "PVE" },
                gate.Modes == BattleModeMask.PvP ? "PVP" : gate.Modes == BattleModeMask.PvE ? "PVE" : "ALL",
                v => gate.Modes = v == "PVP" ? BattleModeMask.PvP : v == "PVE" ? BattleModeMask.PvE : BattleModeMask.All);
            var tiers = new Foldout { text = "Constellation range", value = gate.MinimumTier != 0 || gate.MaximumTier != 5 }; parent.Add(tiers);
            IntControl(tiers, "Minimum tier", gate.MinimumTier, v => gate.MinimumTier = v);
            IntControl(tiers, "Maximum tier", gate.MaximumTier, v => gate.MaximumTier = v);
        }
        private void DrawFilter(VisualElement parent, PassiveTargetFilter filter, Action apply = null)
        {
            if (filter == null) { Body(parent, "Missing roster filter", "studio-error"); return; }
            EnumControl(parent, "Relation", filter.Relation, v => { filter.Relation = (PassiveRelation)v; apply?.Invoke(); });
            TextControl(parent, "Trait ID", filter.TraitId, v => { filter.TraitId = v; apply?.Invoke(); });
            TextControl(parent, "Series ID", filter.SeriesId, v => { filter.SeriesId = v; apply?.Invoke(); });
            TextControl(parent, "Attribute ID", filter.AttributeId, v => { filter.AttributeId = v; apply?.Invoke(); });
            ToggleControl(parent, "Include reserve", filter.IncludeReserve, v => { filter.IncludeReserve = v; apply?.Invoke(); });
            ToggleControl(parent, "Include owner", filter.IncludeOwner, v => { filter.IncludeOwner = v; apply?.Invoke(); });
        }
        private void AddParameter(StudioParameterUnit unit) => Edit("Add parameter", () => _document.Parameters.Add(new StudioParameter
        { Id = "param." + Guid.NewGuid().ToString("N"), Name = "New " + unit, Unit = unit }), true);
        private string NewLeaf()
        {
            var id = "condition." + Guid.NewGuid().ToString("N");
            _document.Conditions.Add(new StudioConditionNode { Id = id, Kind = EffectConditionKind.Always, Subject = ConditionSubject.Owner }); return id;
        }
        private void AddRule() => Edit("Add rule", () =>
        {
            var id = "rule." + Guid.NewGuid().ToString("N");
            _document.Rules.Add(new StudioRule { Id = id, Name = "New combat rule", ConditionRootId = NewLeaf(),
                ActionRootId = NewAction(StudioActionKind.Sequence).Id, Gate = NewPassiveGate() }); _selectedRule = id; _selectedNode = id; _page = "rules";
        }, true);
        private void PruneConditions()
        {
            var reachable = new HashSet<string>(); Action<string> visit = null;
            visit = id => { if (id == null || !reachable.Add(id)) return; var n = _document.Conditions.FirstOrDefault(x => x.Id == id); if (n != null) foreach (var c in n.Children) visit(c); };
            foreach (var r in _document.Rules) visit(r.ConditionRootId);
            foreach (var a in _document.Actions) visit(a.ConditionRootId);
            foreach (var p in _document.Policies) visit(p.ConditionRootId);
            _document.Conditions.RemoveAll(n => !reachable.Contains(n.Id));
        }
        private static bool IsHealthCondition(EffectConditionKind k) => k == EffectConditionKind.SourceHealthAtMost || k == EffectConditionKind.TargetHealthAtMost;
        private static void MoveUp(List<string> values, string id) { var i = values.IndexOf(id); if (i > 0) { values.RemoveAt(i); values.Insert(i - 1, id); } }
        private VisualElement Node(VisualElement parent, string title) { var n = new VisualElement(); n.AddToClassList("studio-node"); parent.Add(n); Section(n, title); return n; }
        private void TextControl(VisualElement p, string label, string value, Action<string> setter)
        { var f = new TextField(label) { value = value ?? "" }; f.RegisterValueChangedCallback(e => Edit("Edit " + label, () => setter(e.newValue))); p.Add(f); }
        private void IntControl(VisualElement p, string label, int value, Action<int> setter)
        { var f = new IntegerField(label) { value = value }; f.RegisterValueChangedCallback(e => Edit("Edit " + label, () => setter(e.newValue))); p.Add(f); }
        private void ToggleControl(VisualElement p, string label, bool value, Action<bool> setter, bool rebuild = false)
        { var f = new Toggle(label) { value = value }; f.RegisterValueChangedCallback(e => Edit("Edit " + label, () => setter(e.newValue), rebuild)); p.Add(f); }
        private void EnumControl(VisualElement p, string label, Enum value, Action<Enum> setter, bool rebuild = false)
        { var f = new EnumField(label, value); f.RegisterValueChangedCallback(e => Edit("Edit " + label, () => setter(e.newValue), rebuild)); p.Add(f); }
        private void ChoiceControl(VisualElement p, string label, List<string> choices, string value, Action<string> setter, bool rebuild = false)
        {
            if (choices.Count == 0) { Body(p, label + ": no available choice", "studio-warning"); return; }
            if (string.IsNullOrEmpty(value)) { choices.Insert(0, "Select…"); value = "Select…"; }
            if (!string.IsNullOrEmpty(value) && !choices.Contains(value)) choices.Add(value);
            var f = new PopupField<string>(label, choices, Math.Max(0, choices.IndexOf(value)), DisplayChoice, DisplayChoice);
            f.RegisterValueChangedCallback(e => { if (e.newValue != "Select…") Edit("Edit " + label, () => setter(e.newValue), rebuild); }); p.Add(f);
        }
        private void ParameterBinding(VisualElement p, string label, string value, StudioParameterUnit unit, Action<string> setter, bool optional = true)
        {
            var choices = _document.Parameters.Where(x => x.Unit == unit).Select(x => x.Id).ToList(); if (optional) choices.Insert(0, "Fixed value");
            ChoiceControl(p, label, choices, string.IsNullOrEmpty(value) ? "Fixed value" : value, selected => setter(selected == "Fixed value" ? null : selected));
        }
    }
}
#endif
