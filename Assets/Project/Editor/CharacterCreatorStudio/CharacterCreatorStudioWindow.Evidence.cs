#if UNITY_EDITOR
using System;
using System.Linq;
using FightingAllstar.Core.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public sealed partial class CharacterCreatorStudioWindow
    {
        [SerializeField] private bool _traceSelectedOnly;
        private void BuildExecutionEvidence()
        {
            if (_run == null) { Body(_problems, "Run a scenario to record committed results and rule decisions.", "studio-subtle"); return; }
            var fresh = _compiled?.Success == true && _compiled.Hashes.Bundle == _run.Hashes.Bundle;
            Body(_problems, (fresh ? "Current revision" : "Historical revision · rerun after editing") + " · seed " + _run.Seed +
                " · level " + _run.Options.Level + " / rank " + _run.Options.Rank + " / tier " + _run.Options.Tier, fresh ? "studio-success" : "studio-warning");
            if (_evidenceTab == "state")
            {
                foreach (var difference in _run.Changes) Body(_problems, difference, "studio-event");
                foreach (var fighter in _run.Battle.Player.Fighters.Concat(_run.Battle.Opponent.Fighters))
                {
                    var counters = fighter.PassiveCounters == null ? "" : string.Join(", ", fighter.PassiveCounters.Select(c => c.Key + "=" + c.Value));
                    Body(_problems, fighter.Id + " · HP " + fighter.Health + " · Gauge " + fighter.PowerGauge + (counters.Length > 0 ? " · " + counters : ""), "studio-event");
                }
                return;
            }
            var traces = _run.Trace;
            var filter = new Toggle("Selected rule only") { value = _traceSelectedOnly };
            filter.RegisterValueChangedCallback(e => { _traceSelectedOnly = e.newValue; RefreshProblems(); }); _problems.Add(filter);
            foreach (var i in Enumerable.Range(0, traces.Count).OrderBy(i => traces[i].FactSequence).ThenBy(i => traces[i].FirstEventIndex).ThenByDescending(i => traces[i].EventCount))
            {
                var trace = traces[i]; var index = i;
                if (_traceSelectedOnly && FindRuleId(trace.RuleId) != _selectedRule) continue;
                var resource = string.IsNullOrEmpty(trace.ResourceKey) ? "" : " · " + DisplayChoice(trace.ResourceKey) + " " + trace.ResourceBefore + " → " + trace.ResourceAfter;
                var title = "#" + trace.FactSequence + "  " + (trace.Outcome ?? (trace.Fired ? "Applied" : "Skipped")) + " · " + Humanize(trace.Trigger) + resource;
                var row = Button(_problems, title + "\n" + TraceSourceName(trace) + " · " + trace.Reason, () => {
                    _selectedTrace = index; SelectSource(string.IsNullOrEmpty(trace.ActionNodeId) ? FindRuleId(trace.RuleId) : trace.ActionNodeId);
                });
                row.AddToClassList("studio-event"); row.EnableInClassList("studio-active", _selectedTrace == index);
            }
        }
        private string FindRuleId(string runtimeId) => _document.Rules.FirstOrDefault(r => r.Id == runtimeId || runtimeId?.EndsWith(":" + r.Id, StringComparison.Ordinal) == true)?.Id ?? runtimeId;
        private string TraceSourceName(PassiveRuleTrace trace)
        {
            var action = _document.Actions.Find(a => a.Id == trace.ActionNodeId);
            if (action != null) return string.IsNullOrEmpty(action.Name) || action.Name == action.Kind.ToString() ? ActionSentence(action) : action.Name;
            return _document.Rules.Find(r => r.Id == FindRuleId(trace.RuleId))?.Name ?? trace.RuleId;
        }
        private void DrawObservedContext(VisualElement parent)
        {
            if (_run == null || _selectedTrace < 0 || _selectedTrace >= _run.Trace.Count) return;
            var trace = _run.Trace[_selectedTrace];
            Section(parent, "OBSERVED EXECUTION");
            Body(parent, "Owner · " + trace.OwnerId + "\nActor · " + trace.ActorId + "\nEvent target · " + trace.EventTargetId + "\nOperation target · " + trace.OperationTargetId, "studio-code");
            Body(parent, (trace.Outcome ?? trace.Reason) + " · branch " + (trace.Branch ?? "eligibility"));
            if (!string.IsNullOrEmpty(trace.ResourceKey)) Body(parent, DisplayChoice(trace.ResourceKey) + " " + trace.ResourceBefore + " → " + trace.ResourceAfter);
            if (trace.SelectedTargets.Count > 0) Body(parent, "Selected targets: " + string.Join(", ", trace.SelectedTargets));
            foreach (var value in trace.Values) Body(parent, value.Key + " = " + value.Value);
            foreach (var condition in trace.Conditions) Body(parent, (condition.Passed ? "PASS · " : "FAIL · ") + condition.Explanation,
                condition.Passed ? "studio-success" : "studio-warning");
            Body(parent, "Root " + trace.RootActionId + " · committed events " + trace.FirstEventIndex + " + " + trace.EventCount, "studio-subtle");
        }
    }
}
#endif
