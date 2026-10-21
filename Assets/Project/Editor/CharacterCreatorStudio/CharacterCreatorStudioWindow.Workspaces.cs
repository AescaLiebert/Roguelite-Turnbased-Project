#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public sealed partial class CharacterCreatorStudioWindow
    {
        [SerializeField] private string _selectedNode;
        [SerializeField] private string _evidenceTab = "problems";
        [SerializeField] private int _selectedTrace = -1;
        [SerializeField] private string _outlineSearch = "";
        [SerializeField] private string _selectedModule;
        private static readonly string[] Workspaces = { "overview", "identity", "skill1", "skill2", "ultimate", "passive", "rules", "modules", "resources", "abilities", "parameters", "descriptions", "scenario", "lab", "presentation", "capabilities", "bundle" };
        private StudioBindingAnalysis _bindings;

        private void Navigate(string page, string node = null)
        { _selectedNode = node; if (page == "modules" && _document.Modules.Any(m => m.Id == node)) _selectedModule = node; ShowWorkspace(page); }

        private string DisplayChoice(string value)
        {
            if (string.IsNullOrEmpty(value)) return "None";
            return _document?.Parameters?.Find(p => p.Id == value)?.Name ??
                _document?.Resources?.Find(r => r.Id == value)?.Name ?? _document?.Statuses?.Find(s => s.Id == value)?.Name ??
                _document?.Modules?.Find(m => m.Id == value)?.Name ?? Humanize(value);
        }
        private static string Humanize(string value) => Regex.Replace(value ?? "", "([a-z])([A-Z])", "$1 $2");
        private string Amount(string id, int fallback, bool percent = false)
        {
            var p = _document.Parameters.Find(v => v.Id == id);
            var number = p?.Value ?? fallback;
            return (percent ? (number / 100f).ToString("0.##") + "%" : number.ToString()) + (p == null ? "" : " · " + p.Name);
        }
        private string TargetName(EffectTargetScope scope) => scope == EffectTargetScope.TriggerTarget ? "event target" :
            scope == EffectTargetScope.TriggerActor ? "event actor" : scope == EffectTargetScope.StatusOwner ? "owner" :
            scope == EffectTargetScope.OperationTarget ? "current loop target" : Humanize(scope.ToString()).ToLowerInvariant();
        private string ActionSentence(StudioActionNode node)
        {
            if (node.Kind == StudioActionKind.Module) return "Use " + DisplayChoice(node.ModuleId);
            if (node.Kind == StudioActionKind.Sequence) return "In order · " + node.Children.Count + " steps";
            if (node.Kind == StudioActionKind.Branch) return "If " + ConditionSentence(node.ConditionRootId, new HashSet<string>());
            if (node.Kind == StudioActionKind.ForEachTarget) return node.Query == null ? "For each " + TargetName(node.Target) :
                "For each " + (!string.IsNullOrEmpty(node.Query.ReuseBinding) ? "saved selection “" + node.Query.ReuseBinding + "”" : TargetName(node.Query.BaseScope)) +
                (node.Query.SampleCount > 0 ? " · choose " + node.Query.SampleCount + " randomly" : " · " + Humanize(node.Query.Sort.ToString()));
            if (node.Kind == StudioActionKind.SpendResource || node.Kind == StudioActionKind.TransferResource)
                return (node.Kind == StudioActionKind.SpendResource ? "Spend " : "Transfer ") + Amount(node.AmountParameterId, node.Amount) + " " + DisplayChoice(node.ResourceKey);
            if (node.Kind == StudioActionKind.ConsumeStatusLayers) return "Consume " + Amount(node.AmountParameterId, node.Amount) + " layers of " + DisplayChoice(node.StatusRecipeId) +
                (node.StatusOwnerOnly ? " · applied by owner" : "") + " → “" + node.ResultBinding + "”";
            var c = node.Command;
            if (c?.Kind == PassiveCommandKind.SetAbilityVariant) return (c.VariantUnlocked ? "Unlock " : "Relock ") + c.VariantId;
            if (c?.Kind == PassiveCommandKind.IncreaseCurrentCritChancePercent || c?.Kind == PassiveCommandKind.IncreaseCurrentDamageDealtPercent ||
                c?.Kind == PassiveCommandKind.IncreaseCurrentAttackPercent || c?.Kind == PassiveCommandKind.IgnoreCurrentTargetCritDefensePercent)
                return Humanize(c.Kind.ToString()) + " · " + Amount(node.AmountParameterId, c.Amount, true) + " · current qualifying hit";
            if (c?.Kind != PassiveCommandKind.ExecuteEffect) return Humanize(c?.Kind.ToString()) + " " + Amount(node.AmountParameterId, c?.Amount ?? 0) + " " + DisplayChoice(c?.CounterKey);
            var op = c.Effect;
            if (op == null) return "Choose an operation";
            var detail = op.Kind == CardEffectOperationKind.Heal ? Amount(node.CoefficientParameterId, op.Value?.CoefficientBp ?? 0, true) + " of " + Humanize(op.Value?.Source.ToString()) :
                op.Kind == CardEffectOperationKind.Damage ? Amount(node.CoefficientParameterId, op.Damage?.CoefficientBp ?? 0, true) + " " + Humanize(op.Damage?.Scaling.ToString()) :
                op.Kind == CardEffectOperationKind.ApplyStatus ? Amount(node.AmountParameterId, op.Status?.StackCount ?? 1) + " layers of " + DisplayChoice(op.Status?.InlineRecipe?.Id ?? op.Status?.StatusRecipeId) :
                op.Kind == CardEffectOperationKind.Cleanse || op.Kind == CardEffectOperationKind.RemoveStatus ? (op.RemoveAll ? "all matching " : "one matching ") + Humanize(op.RemovePolarity.ToString()) : Amount(node.AmountParameterId, op.Magnitude);
            if (c.ValueSource == PassiveValueSource.Binding) detail += " · using “" + c.ValueBinding + "”";
            return Humanize(op.Kind.ToString()) + " " + detail + " → " + TargetName(op.Target);
        }
        private static string ActionNodeTitle(StudioActionNode node)
        {
            var name = string.IsNullOrWhiteSpace(node?.Name) ? node?.Kind.ToString() ?? "Action" : node.Name.Trim();
            return (node?.Kind == StudioActionKind.Branch ? "IF · " : "DO · ") + name;
        }
        private static string ActionFoldoutHeading(StudioActionNode node) =>
            (string.IsNullOrWhiteSpace(node?.Name) ? node?.Kind.ToString() ?? "Action" : node.Name.Trim()) + " · " + StudioAuthoringCatalog.Summary(node);
        private string ConditionSentence(string id, HashSet<string> path)
        {
            if (id == null || !path.Add(id)) return "Choose condition";
            var c = _document.Conditions.Find(n => n.Id == id);
            if (c == null) return "Missing condition";
            if (c.Logic != ConditionLogic.Leaf) return (c.Negate ? "NOT · " : "") + c.Logic + ": " + string.Join(c.Logic == ConditionLogic.Any ? " or " : " and ", c.Children.Select(child => ConditionSentence(child, new HashSet<string>(path))));
            if (c.Kind == EffectConditionKind.Always) return c.Negate ? "Never (NOT Always)" : "Always";
            return (c.Negate ? "NOT · " : "") + Humanize(c.Subject.ToString()) + " · " + Humanize(c.Kind.ToString()) + " " +
                (!string.IsNullOrEmpty(c.StringValue) ? DisplayChoice(c.StringValue) + " · " : "") +
                Amount(c.ThresholdParameterId, c.Threshold, IsHealthCondition(c.Kind));
        }

        private void BuildKitOutline()
        {
            Section(_outline, "CHARACTER");
            Body(_outline, _document.EditingCharacter?.DisplayName ?? _document.Name, "studio-kit-title");
            var search = new TextField("Find in kit") { name = "studio-outline-search", value = _outlineSearch, isDelayed = true, tooltip = "Find a rule or library entry; press Enter to search" };
            search.RegisterValueChangedCallback(e => { _outlineSearch = e.newValue; _outline.schedule.Execute(() => { _outline.Clear(); BuildKitOutline(); }); });
            _outline.Add(search);
            bool Matches(string name) => string.IsNullOrEmpty(_outlineSearch) || (name ?? "").IndexOf(_outlineSearch, StringComparison.OrdinalIgnoreCase) >= 0;
            void Nav(string title, string page, string id = null)
            { var b = Button(_outline, title, () => Navigate(page, id)); b.name = "studio-nav-" + page; b.AddToClassList("studio-nav"); b.EnableInClassList("studio-nav-active", _page == page && (id == null || _selectedRule == id || _selectedNode == id)); }
            Section(_outline, "KIT OUTLINE");
            Nav("Overview", "overview"); Nav("Identity & stats", "identity");
            Section(_outline, "SKILLS");
            Nav("Skill 1 · Ranks 1–3", "skill1"); Nav("Skill 2 · Ranks 1–3", "skill2"); Nav("Ultimate · C0–C5", "ultimate");
            Nav("Variants & upgrades", "abilities");
            Section(_outline, "PASSIVE");
            Nav("Passive Overview", "passive");
            foreach (var rule in _document.Rules.Where(r => Matches(r.Name)))
            {
                var r = rule;
                var b = Button(_outline, (r.Enabled ? "" : "○ ") + r.Name, () => { _selectedRule = r.Id; Navigate("rules", r.Id); });
                b.name = "nav." + r.Id;
                b.AddToClassList("studio-nav"); b.EnableInClassList("studio-nav-active", _page == "rules" && _selectedRule == r.Id);
            }
            Button(_outline, "+ Add rule", AddRule).name = "studio-add-rule";
            Section(_outline, "SHARED MECHANICS");
            Nav("Parameters", "parameters"); Nav("Resources & statuses", "resources");
            Section(_outline, "REUSABLE MODULES");
            foreach (var module in _document.Modules.Where(m => Matches(m.Name))) Nav(module.Name, "modules", module.Id);
            Nav("Manage modules", "modules");
            Section(_outline, "WORKSPACES");
            Nav("Descriptions", "descriptions"); Nav("Battle Lab", "lab"); Nav("Actor lab", "presentation"); Nav("Bundle review", "bundle"); Nav("Capabilities", "capabilities");
        }

        private void BuildRules()
        {
            var rule = _document.Rules.Find(r => r.Id == _selectedRule) ?? _document.Rules.FirstOrDefault();
            Button(_host, "← Passive Overview", () => ShowWorkspace("passive")).name = "studio-rules-passive-overview";
            if (rule == null) { Heading(_host, "Passive trigger rules"); Body(_host, "Add a rule to connect a trigger, conditions and ordered actions."); Button(_host, "+ Add rule", AddRule); return; }
            _selectedRule = rule.Id;
            if (string.IsNullOrEmpty(_selectedNode)) _selectedNode = rule.Id;
            Body(_host, "PASSIVE RULES  /  " + rule.Name, "studio-breadcrumb");
            Heading(_host, rule.Name).name = "studio-rule-title";
            var view = StudioPassiveView.Rule(rule);
            Body(_host, view.Execution + "\n" + view.Availability, "studio-subtle").name = "studio-rule-execution-summary";
            RuleSectionCard("WHEN", view.When, rule, "when", "when");
            RuleSectionCard("WHO", view.Who, rule, "who", "to");
            DrawConditionCards(_host, rule.ConditionRootId, 0, new HashSet<string>());
            Section(_host, "DO · ORDERED ACTIONS");
            if (string.IsNullOrEmpty(rule.ActionRootId))
            {
                SemanticCard(_host, "DO", "Give active allies " + Amount(rule.AmountParameterId, 0) + " Power Gauge", rule.Id, "do");
                Button(_host, "Convert imported rule to ordered actions", () => Edit("Convert rule", () => {
                    var a = NewAction(StudioActionKind.Command); a.Command.Effect.Target = rule.Target;
                    a.AmountParameterId = rule.AmountParameterId; rule.ActionRootId = a.Id;
                }, true));
            }
            else DrawActionCards(_host, rule.ActionRootId, 0, new HashSet<string>());
            if (!string.IsNullOrEmpty(rule.ElseActionRootId))
            { Section(_host, "ELSE · ELIGIBILITY FAILED"); DrawActionCards(_host, rule.ElseActionRootId, 0, new HashSet<string>()); }
            BuildRuleInspector(rule);
        }
        [SerializeField] private string _ruleSection;
        private void RuleSectionCard(string label, string sentence, StudioRule rule, string section, string tone)
        {
            var card = new Button(() => { _selectedNode = rule.Id; _ruleSection = section; Rebuild(); }) { name = "card." + rule.Id + "." + section };
            card.AddToClassList("studio-rule-card"); card.AddToClassList("studio-tone-" + tone);
            card.EnableInClassList("studio-card-selected", _selectedNode == rule.Id && _ruleSection == section);
            var tag = new Label(label); tag.AddToClassList("studio-card-tag"); card.Add(tag);
            var text = new Label(sentence) { name = "summary." + rule.Id + "." + section, enableRichText = false }; text.AddToClassList("studio-card-sentence"); card.Add(text); _host.Add(card);
        }
        private void SemanticCard(VisualElement parent, string label, string sentence, string id, string tone, string title = null)
        {
            var card = new Button(() => { _selectedNode = id; Rebuild(); }) { name = "card." + id };
            card.AddToClassList("studio-rule-card"); card.AddToClassList("studio-tone-" + tone);
            card.EnableInClassList("studio-card-selected", _selectedNode == id);
            var tag = new Label(title ?? label); if (title != null) tag.name = "action-title." + id; tag.AddToClassList("studio-card-tag"); card.Add(tag);
            var text = new Label(sentence) { name = "summary." + id, enableRichText = false }; text.AddToClassList("studio-card-sentence"); card.Add(text);
            parent.Add(card);
        }
        private void DrawConditionCards(VisualElement parent, string id, int depth, HashSet<string> path)
        {
            if (id == null || depth > 32 || !path.Add(id)) return;
            var c = _document.Conditions.Find(n => n.Id == id); if (c == null) return;
            SemanticCard(parent, depth == 0 ? "IF" : c.Logic.ToString().ToUpperInvariant(), ConditionSentence(id, new HashSet<string>()), id, "if");
            if (c.Logic != ConditionLogic.Leaf && c.Children.Count > 0)
            {
                var group = new Foldout { text = "Inspect " + c.Children.Count + " conditions", value = false }; parent.Add(group);
                foreach (var child in c.Children) DrawConditionCards(group, child, depth + 1, new HashSet<string>(path));
            }
        }
        private void DrawActionCards(VisualElement parent, string id, int depth, HashSet<string> path)
        {
            if (id == null || depth > 64 || !path.Add(id)) return;
            var a = _document.Actions.Find(n => n.Id == id); if (a == null) return;
            SemanticCard(parent, a.Kind == StudioActionKind.Branch ? "IF" : "DO", ActionSentence(a), id, "do", ActionNodeTitle(a));
            if (a.Kind == StudioActionKind.Command && a.Children.Count == 0 && a.ElseChildren.Count == 0) return;
            var body = new VisualElement(); body.AddToClassList("studio-action-children"); parent.Add(body);
            if (a.Kind == StudioActionKind.Module)
            {
                var module = _document.Modules.Find(m => m.Id == a.ModuleId);
                if (module != null) { Body(body, "Shared module · " + module.Name, "studio-subtle"); DrawActionCards(body, module.RootActionId, depth + 1, new HashSet<string>(path)); }
                return;
            }
            if (a.Children.Count > 0) Section(body, a.Kind == StudioActionKind.SpendResource || a.Kind == StudioActionKind.ConsumeStatusLayers || a.Kind == StudioActionKind.TransferResource ? "ON SUCCESS" : a.Kind == StudioActionKind.Branch ? "THEN" : "IN ORDER");
            foreach (var child in a.Children) DrawActionCards(body, child, depth + 1, new HashSet<string>(path));
            if (a.ElseChildren.Count > 0) { Section(body, "ON FAILURE / ELSE"); foreach (var child in a.ElseChildren) DrawActionCards(body, child, depth + 1, new HashSet<string>(path)); }
            var add = Row(body);
            var kind = new EnumField(StudioActionKind.Command); add.Add(kind);
            Button(add, "+ Add step", () => Edit("Add ordered step", () => { var next = NewAction((StudioActionKind)kind.value); a.Children.Add(next.Id); _selectedNode = next.Id; }, true));
            if (a.Kind == StudioActionKind.Branch || a.Kind == StudioActionKind.SpendResource || a.Kind == StudioActionKind.ConsumeStatusLayers || a.Kind == StudioActionKind.TransferResource)
                Button(add, "+ Failure step", () => Edit("Add failure step", () => { var next = NewAction((StudioActionKind)kind.value); a.ElseChildren.Add(next.Id); _selectedNode = next.Id; }, true));
        }
        private void BuildRuleInspector(StudioRule rule)
        {
            Section(_inspector, "SELECTION INSPECTOR");
            var action = _document.Actions.Find(a => a.Id == _selectedNode);
            var condition = _document.Conditions.Find(c => c.Id == _selectedNode);
            if (action != null)
            {
                DrawAction(_inspector, action.Id, new HashSet<string>(), 0, true);
                var row = Row(_inspector);
                Button(row, "Duplicate", () => Edit("Duplicate action", () => DuplicateAction(action), true));
                Button(row, "Move up", () => Edit("Move action", () => { foreach (var a in _document.Actions) { MoveUp(a.Children, action.Id); MoveUp(a.ElseChildren, action.Id); } }, true));
                Button(row, "Remove", () => Edit("Remove action", () => { foreach (var a in _document.Actions) { a.Children.Remove(action.Id); a.ElseChildren.Remove(action.Id); } foreach (var r in _document.Rules.Where(r => r.ActionRootId == action.Id)) r.ActionRootId = NewAction(StudioActionKind.Sequence).Id; _selectedNode = rule.Id; PruneActions(); PruneConditions(); }, true));
            }
            else if (condition != null) DrawCondition(_inspector, condition.Id, new HashSet<string>(), 0);
            else
            {
                Section(_inspector, _ruleSection == "who" ? "WHO · EVENT FILTERS" : "WHEN · TRIGGER & EXECUTION");
                TextControl(_inspector, "Rule name", rule.Name, v => rule.Name = v);
                ToggleControl(_inspector, "Enabled", rule.Enabled, v => rule.Enabled = v);
                if (_ruleSection == "who")
                {
                EnumControl(_inspector, "Actor relation", rule.Settings.ActorRelation, v => rule.Settings.ActorRelation = (PassiveRelation)v);
                EnumControl(_inspector, "Event target relation", rule.Settings.TargetRelation, v => rule.Settings.TargetRelation = (PassiveRelation)v);
                Body(_inspector, "These filter the event. Each action chooses its own recipients in DO.", "studio-subtle");
                DrawEventFilters(_inspector, rule);
                }
                else
                {
                    ChoiceControl(_inspector, "Trigger", StudioAuthoringCatalog.Triggers.Select(t => t.ToString()).ToList(), rule.Trigger.ToString(), v => rule.Trigger = (PassiveEventKind)Enum.Parse(typeof(PassiveEventKind), v), true);
                    if (rule.Trigger == PassiveEventKind.BeforeDamage) Body(_inspector, "Calculation stage · fixed modifiers affect this hit only. Critical/blocked outcomes are not known yet.", "studio-subtle");
                }
                Section(_inspector, "FREQUENCY");
                var frequency = new VisualElement(); _inspector.Add(frequency);
                EnumControl(frequency, "Activation scope", rule.Settings.LimitScope, v => rule.Settings.LimitScope = (PassiveLimitScope)v);
                IntControl(frequency, "Maximum activations per scope", rule.Settings.MaximumActivations, v => rule.Settings.MaximumActivations = v);
                IntControl(frequency, "Cooldown (owner turns)", rule.Settings.CooldownOwnerTurns, v => rule.Settings.CooldownOwnerTurns = v);
                if (rule.RuntimeVersion == 0) { frequency.SetEnabled(false); Body(_inspector, "Native version-zero receipts are preserved. Advanced frequency/ordering requires a verified runtime migration.", "studio-warning"); }
                Section(_inspector, "SOURCE LIFETIME");
                EnumControl(_inspector, "Owner presence", rule.Gate.Presence, v => rule.Gate.Presence = (PassivePresence)v);
                var advanced = new Foldout { text = "Ownership, availability & ordering", value = false }; _inspector.Add(advanced);
                ChoiceControl(advanced, "Rule owner", new List<string> { "Character" }.Concat(_document.Statuses.Select(s => s.Id)).ToList(), string.IsNullOrEmpty(rule.StatusId) ? "Character" : rule.StatusId, v => rule.StatusId = v == "Character" ? null : v);
                var order = new VisualElement(); advanced.Add(order);
                EnumControl(order, "Phase", rule.Settings.Phase, v => rule.Settings.Phase = (PassiveExecutionPhase)v);
                IntControl(order, "Priority (higher first)", rule.Settings.Priority, v => rule.Settings.Priority = v);
                order.SetEnabled(rule.RuntimeVersion == 1);
                EnumControl(advanced, "Battle mode", rule.Gate.Modes, v => rule.Gate.Modes = (BattleModeMask)v);
                IntControl(advanced, "Minimum tier", rule.Gate.MinimumTier, v => rule.Gate.MinimumTier = v);
                IntControl(advanced, "Maximum tier", rule.Gate.MaximumTier, v => rule.Gate.MaximumTier = v);
                Button(_inspector, "Duplicate rule", () => Edit("Duplicate rule", () => {
                    var copy = SnapshotJson.Deserialize<StudioRule>(SnapshotJson.Serialize(rule)); copy.Id = "rule." + Guid.NewGuid().ToString("N"); copy.Name += " (copy)";
                    copy.ConditionRootId = CloneCondition(rule.ConditionRootId, new Dictionary<string, string>());
                    if (!string.IsNullOrEmpty(rule.ActionRootId)) copy.ActionRootId = CloneAction(rule.ActionRootId, new Dictionary<string, string>(), new Dictionary<string, string>());
                    if (!string.IsNullOrEmpty(rule.ElseActionRootId)) copy.ElseActionRootId = CloneAction(rule.ElseActionRootId, new Dictionary<string, string>(), new Dictionary<string, string>());
                    _document.Rules.Add(copy); _selectedRule = copy.Id; _selectedNode = copy.Id;
                }, true));
                Button(_inspector, "Remove rule", () => Edit("Remove rule", () => { _document.Rules.Remove(rule); _selectedRule = null; _selectedNode = null; PruneActions(); PruneConditions(); }, true));
            }
            Section(_inspector, "CONTEXT BINDINGS");
            Body(_inspector, "Owner · rule provider\nEvent actor · caused the trigger\nEvent target · original recipient\nOperation target · current loop member", "studio-subtle");
            _bindings = StudioBindingAnalysis.Analyze(_document);
            if (_selectedNode != null)
            {
                if (_bindings.Values.TryGetValue(_selectedNode, out var values)) Body(_inspector, "Available values: " + (values.Count == 0 ? "none" : string.Join(", ", values)));
                if (_bindings.Targets.TryGetValue(_selectedNode, out var targets)) Body(_inspector, "Saved target sets: " + (targets.Count == 0 ? "none" : string.Join(", ", targets)));
            }
            DrawObservedContext(_inspector);
        }

        private void DuplicateAction(StudioActionNode source)
        {
            var parents = _document.Actions.Where(a => a.Children.Contains(source.Id) || a.ElseChildren.Contains(source.Id)).ToArray();
            var id = CloneAction(source.Id, new Dictionary<string, string>(), new Dictionary<string, string>());
            if (parents.Length == 0)
            {
                var wrapper = NewAction(StudioActionKind.Sequence); wrapper.Children.AddRange(new[] { source.Id, id });
                foreach (var r in _document.Rules.Where(r => r.ActionRootId == source.Id)) r.ActionRootId = wrapper.Id;
            }
            foreach (var parent in parents) { var list = parent.Children.Contains(source.Id) ? parent.Children : parent.ElseChildren; list.Insert(list.IndexOf(source.Id) + 1, id); }
            _selectedNode = id;
        }
        private string CloneCondition(string id, Dictionary<string, string> map)
        {
            if (string.IsNullOrEmpty(id)) return id;
            if (map.TryGetValue(id, out var previous)) return previous;
            var source = _document.Conditions.Find(c => c.Id == id); if (source == null) return id;
            var copy = SnapshotJson.Deserialize<StudioConditionNode>(SnapshotJson.Serialize(source));
            copy.Id = "condition." + Guid.NewGuid().ToString("N"); map[id] = copy.Id; _document.Conditions.Add(copy);
            copy.Children = copy.Children.Select(child => CloneCondition(child, map)).ToList(); return copy.Id;
        }
        private string CloneAction(string id, Dictionary<string, string> actions, Dictionary<string, string> conditions)
        {
            if (string.IsNullOrEmpty(id)) return id;
            if (actions.TryGetValue(id, out var previous)) return previous;
            var source = _document.Actions.Find(a => a.Id == id); if (source == null) return id;
            var copy = SnapshotJson.Deserialize<StudioActionNode>(SnapshotJson.Serialize(source));
            copy.Id = "action." + Guid.NewGuid().ToString("N"); actions[id] = copy.Id; _document.Actions.Add(copy);
            copy.ConditionRootId = CloneCondition(copy.ConditionRootId, conditions);
            copy.Children = copy.Children.Select(child => CloneAction(child, actions, conditions)).ToList();
            copy.ElseChildren = copy.ElseChildren.Select(child => CloneAction(child, actions, conditions)).ToList(); return copy.Id;
        }

        private void BuildParameters()
        {
            Heading(_host, "Parameters"); Body(_host, "One typed value can drive actions, conditions, overrides and description wording.", "studio-subtle");
            foreach (var p in _document.Parameters.ToArray())
            {
                var box = Node(_host, p.Name); box.name = p.Id;
                TextControl(box, "Name", p.Name, v => p.Name = v); EnumControl(box, "Unit", p.Unit, v => p.Unit = (StudioParameterUnit)v, true);
                IntControl(box, "Value", p.Value, v => p.Value = v);
                Body(box, p.Id, "studio-code"); Button(box, "Remove parameter", () => Edit("Remove parameter", () => _document.Parameters.Remove(p), true));
            }
            Button(_host, "+ Integer parameter", () => AddParameter(StudioParameterUnit.Integer));
            Button(_host, "+ Percentage parameter", () => AddParameter(StudioParameterUnit.BasisPoints));
        }
        private void BuildModules()
        {
            Heading(_host, "Reusable modules"); Body(_host, "Module calls share this definition. Change it here to update every local use.", "studio-subtle");
            var module = _document.Modules.Find(m => m.Id == _selectedModule || m.Id == _selectedNode) ?? _document.Modules.FirstOrDefault();
            if (module != null)
            {
                _selectedModule = module.Id;
                TextControl(_inspector, "Module name", module.Name, v => module.Name = v); DrawActionCards(_host, module.RootActionId, 0, new HashSet<string>());
                var referenced = _document.Actions.Any(a => a.Kind == StudioActionKind.Module && a.ModuleId == module.Id);
                Button(_inspector, "Remove module", () => Edit("Remove module", () => { _document.Modules.Remove(module); _selectedModule = null; _selectedNode = null; PruneActions(); PruneConditions(); }, true)).SetEnabled(!referenced);
                if (referenced) Body(_inspector, "This module is used by a rule. Remove its calls before deleting it.", "studio-subtle");
            }
            Button(_host, "+ Module", () => Edit("Add module", () => { var m = new StudioModule { Id = "module." + Guid.NewGuid().ToString("N"), Name = "New module", RootActionId = NewAction(StudioActionKind.Sequence).Id }; _document.Modules.Add(m); _selectedNode = m.Id; _selectedModule = m.Id; }, true));
            var selected = _document.Actions.Find(a => a.Id == _selectedNode); if (selected != null) DrawAction(_inspector, selected.Id, new HashSet<string>(), 0, true);
        }
        private void BuildAbilities()
        { Heading(_host, "Skill variants & upgrades"); DrawAbilities(); }
        private void RefreshSemanticSummaries()
        {
            if (_document == null || _host == null) return;
            StudioPassiveOverviewPresenter.Refresh(_host, StudioPassiveView.Mechanics(_document));
            if (_page == "passive") RefreshPassiveAvailability();
            var availability = _inspector.Q<Label>("studio-passive-availability");
            if (availability != null) availability.text = StudioPassiveView.AvailabilitySummary(_document);
            foreach (var a in _document.Actions)
            {
                var label = _host.Q<Label>("summary." + a.Id); if (label != null) label.text = ActionSentence(a);
                var title = _host.Q<Label>("action-title." + a.Id); if (title != null) title.text = ActionNodeTitle(a);
            }
            foreach (var c in _document.Conditions) { var label = _host.Q<Label>("summary." + c.Id); if (label != null) label.text = ConditionSentence(c.Id, new HashSet<string>()); }
            foreach (var r in _document.Resources) { var label = _host.Q<Label>("summary." + r.Id); if (label != null) label.text = r.Name + " · maximum " + r.Cap; }
            foreach (var s in _document.Statuses) { var label = _host.Q<Label>("summary." + s.Id); if (label != null) label.text = s.Name + " · " + s.Definition.MaxStacks + " layers · " + Humanize(s.Definition.DurationClock.ToString()); }
            foreach (var p in _document.Policies) { var label = _host.Q<Label>("summary." + p.Id); if (label != null) label.text = p.Name + " · " + Humanize(p.Definition.Kind.ToString()); }
            foreach (var r in _document.Rules)
            {
                var view = StudioPassiveView.Rule(r);
                var when = _host.Q<Label>("summary." + r.Id + ".when"); if (when != null) when.text = view.When;
                var who = _host.Q<Label>("summary." + r.Id + ".who"); if (who != null) who.text = view.Who;
                if (r.Id == _selectedRule) { var execution = _host.Q<Label>("studio-rule-execution-summary"); if (execution != null) execution.text = view.Execution + "\n" + view.Availability; }
                var nav = _outline.Q<Button>("nav." + r.Id); if (nav != null) nav.text = (r.Enabled ? "" : "○ ") + r.Name;
                var title = _host.Q<Label>("studio-rule-title"); if (title != null && r.Id == _selectedRule) title.text = r.Name;
            }
        }

        private void ResourcePicker(VisualElement parent, string label, string value, Action<string> setter)
        {
            var choices = _document.Resources.Select(r => r.Id).ToList();
            if (!string.IsNullOrEmpty(value) && !choices.Contains(value)) choices.Add(value);
            if (choices.Count > 0) { choices.Insert(0, "None"); ChoiceControl(parent, label, choices, string.IsNullOrEmpty(value) ? "None" : value, v => setter(v == "None" ? null : v), true); }
            else TextControl(parent, label + " key", value, setter);
            if (_document.Resources.Count == 0) Button(parent, "Define shared resources", () => Navigate("resources"));
        }
        private void StatusPicker(VisualElement parent, string label, string value, Action<string> setter)
        {
            var visuals = FindStatusVisualAssets();
            var iconRow = Row(parent); iconRow.name = "studio-condition-status-preview";
            var statusIcon = new Image { scaleMode = ScaleMode.ScaleToFit };
            statusIcon.style.width = 28; statusIcon.style.height = 28; statusIcon.name = "studio-condition-status-icon";
            iconRow.Add(statusIcon);
            var statusLabel = new Label(); iconRow.Add(statusLabel);
            void RefreshStatusIcon(string id)
            {
                var visual = visuals.FirstOrDefault(v => string.Equals(v.Id, id, StringComparison.OrdinalIgnoreCase))
                    ?? StatusVisualData.Get(id, StatusPolarity.Neutral);
                statusIcon.sprite = visual?.Icon; statusIcon.tooltip = id;
                statusLabel.text = string.IsNullOrWhiteSpace(id) ? "Select a status" : visual?.DisplayName ?? id;
            }
            RefreshStatusIcon(value);
            var choices = _document.Statuses.Select(s => s.Id)
                .Concat(StandardEffectDatabase.CreateStatusRecipes().Select(s => s.Id))
                .Concat(visuals.Select(v => v.Id))
                .Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList();
            const string select = "Select…";
            if (string.IsNullOrWhiteSpace(value)) choices.Insert(0, select);
            else if (!choices.Contains(value)) choices.Add(value);

            var popup = new PopupField<string>(label, choices,
                Math.Max(0, choices.IndexOf(string.IsNullOrWhiteSpace(value) ? select : value)), DisplayChoice, DisplayChoice);
            parent.Add(popup);
            var idField = new TextField("Status recipe / visual ID") { value = value ?? "", isDelayed = true };
            idField.tooltip = "This ID is stored by the action. It must match StatusVisualData.Id to resolve its icon and display metadata.";
            parent.Add(idField);
            var assetField = new ObjectField("Matching StatusVisualData")
            {
                objectType = typeof(StatusVisualData),
                allowSceneObjects = false,
                value = visuals.FirstOrDefault(v => string.Equals(v.Id, value, StringComparison.OrdinalIgnoreCase)),
                tooltip = "Choose a StatusVisualData asset to copy its ID into the status reference."
            };
            parent.Add(assetField);

            void SetStatusId(string id, string editLabel)
            {
                id = string.IsNullOrWhiteSpace(id) ? null : id.Trim();
                idField.SetValueWithoutNotify(id ?? "");
                assetField.SetValueWithoutNotify(visuals.FirstOrDefault(v => string.Equals(v.Id, id, StringComparison.OrdinalIgnoreCase)));
                var popupValue = id ?? select;
                if (!popup.choices.Contains(popupValue)) popup.choices.Add(popupValue);
                popup.SetValueWithoutNotify(popupValue);
                RefreshStatusIcon(id);
                Edit(editLabel, () => setter(id));
            }

            popup.RegisterValueChangedCallback(e => SetStatusId(e.newValue == select ? null : e.newValue, "Choose " + label));
            idField.RegisterValueChangedCallback(e => SetStatusId(e.newValue, "Edit status recipe / visual ID"));
            assetField.RegisterValueChangedCallback(e => SetStatusId((e.newValue as StatusVisualData)?.Id, "Link StatusVisualData"));
        }

        private static StatusVisualData[] FindStatusVisualAssets() => AssetDatabase.FindAssets("t:StatusVisualData")
            .Select(guid => AssetDatabase.LoadAssetAtPath<StatusVisualData>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(visual => visual != null && !string.IsNullOrWhiteSpace(visual.Id))
            .OrderBy(visual => visual.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(visual => visual.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        private void BindingPicker(VisualElement parent, string label, string node, string value, bool targets, Action<string> setter, bool optional = false)
        {
            var analysis = StudioBindingAnalysis.Analyze(_document); var table = targets ? analysis.Targets : analysis.Values;
            var choices = node != null && table.TryGetValue(node, out var names) ? new List<string>(names) : new List<string>();
            if (optional) choices.Insert(0, "None");
            if (!string.IsNullOrEmpty(value) && !choices.Contains(value)) choices.Add(value);
            ChoiceControl(parent, label, choices, string.IsNullOrEmpty(value) && optional ? "None" : value, v => setter(v == "None" ? null : v));
            if (choices.Count == 0 || choices.Count == 1 && optional) Body(parent, "Create a selection or consumed-count binding in an earlier step to use it here.", "studio-subtle");
        }
        private List<PassiveValueSource> AvailableValueSources(string actionId)
        {
            var rule = _document.Rules.Find(r => ContainsAction(r.ActionRootId, actionId, new HashSet<string>()));
            var result = new List<PassiveValueSource> { PassiveValueSource.Fixed, PassiveValueSource.Binding };
            var command = _document.Actions.Find(action => action.Id == actionId)?.Command;
            if (rule?.Trigger == PassiveEventKind.BeforeDamage &&
                command?.Kind == PassiveCommandKind.IncreaseCurrentCritChancePercent)
                result.Add(PassiveValueSource.OwnerMissingHealthPercent);
            if (rule?.Trigger == PassiveEventKind.DamageResolved) result.Add(PassiveValueSource.EventDamage);
            if (rule?.Trigger == PassiveEventKind.Healed) result.Add(PassiveValueSource.EventHealing);
            if (rule?.Trigger == PassiveEventKind.GaugeChanged && rule.Settings.RequireGaugeLoss) result.Add(PassiveValueSource.ActualGaugeLost);
            if (rule?.Trigger == PassiveEventKind.StatusRemoved || _document.Actions.Any(a => a.Kind == StudioActionKind.ConsumeStatusLayers && a.Children.Concat(a.ElseChildren).Any(c => ContainsAction(c, actionId, new HashSet<string>())))) result.Add(PassiveValueSource.RemovedStatusLayers);
            return result;
        }
    }
}
#endif

