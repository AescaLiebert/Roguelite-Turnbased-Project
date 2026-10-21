#if UNITY_EDITOR
using System;
using System.Linq;
using FightingAllstar.Core.Studio;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public sealed partial class CharacterCreatorStudioWindow
    {
        public void RunScenario()
        {
            Compile();
            if (_compiled?.Success != true) { RefreshProblems(); SetStatus("Resolve compilation errors before running."); return; }
            _run = StudioSimulation.Run(_document, _library, unchecked((ulong)Math.Max(0, _seed)),
                new StudioSimulationOptions { Level = _scenarioLevel, Rank = _scenarioRank, Tier = _scenarioTier, Preset = _scenarioPreset });
            _selectedTrace = -1; _evidenceTab = "trace";
            _page = "scenario"; Rebuild();
            ResetWorkspaceScroll();
            SetStatus(_run.MatchesLegacy ? "Production-engine run matches the frozen legacy behavior." : "Run complete. Authored changes differ from the frozen legacy behavior; inspect the state differences.");
        }

        private void BuildScenario()
        {
            Heading(_host, "Production-engine scenario and actor playback");
            Body(_host, "Fixed teams with an opening-skill, incoming-cap or fatal-save acceptance scenario. Incoming presets use one synthetic lethal contact; these overrides belong to the scenario.", "studio-subtle");
            var preset = new EnumField("Acceptance scenario", _scenarioPreset);
            preset.RegisterValueChangedCallback(e => _scenarioPreset = (StudioScenarioPreset)e.newValue); _host.Add(preset);
            var seed = new IntegerField("Seed") { value = _seed };
            seed.RegisterValueChangedCallback(e => _seed = Math.Max(0, e.newValue)); _host.Add(seed);
            var level = new IntegerField("Character level") { value = _scenarioLevel };
            level.RegisterValueChangedCallback(e => _scenarioLevel = Math.Max(1, e.newValue)); _host.Add(level);
            var rank = new IntegerField("Opening card rank (1–3)") { value = _scenarioRank };
            rank.RegisterValueChangedCallback(e => _scenarioRank = Math.Max(1, Math.Min(3, e.newValue))); _host.Add(rank);
            var tier = new IntegerField("Constellation tier (0–5)") { value = _scenarioTier };
            tier.RegisterValueChangedCallback(e => _scenarioTier = Math.Max(0, Math.Min(5, e.newValue))); _host.Add(tier);
            Button(_host, "Run current draft", RunScenario);
            var saved = new ObjectField("Saved scenario") { objectType = typeof(StudioScenarioAsset), value = _scenario };
            saved.RegisterValueChangedCallback(e => _scenario = e.newValue as StudioScenarioAsset); _host.Add(saved);
            if (_scenario != null)
                Button(_host, "Replay saved state and exact input", () =>
                { _scenario.Replay(out var result, out var message); SetStatus(message); });
            if (_run == null) { Body(_host, "Run a scenario to record facts, passive decisions and actor cues."); return; }
            Button(_host, "Save this scenario and frozen input", () =>
            { _scenario = StudioScenarioAsset.Save(_draft, _run); Rebuild(); SetStatus("Saved scenario with opening state, exact plan, definitions, RNG state, seed and expected result."); });
            var fresh = _compiled?.Success == true && _run.Hashes.Bundle == _compiled.Hashes.Bundle;
            Body(_host, fresh ? "Run uses the current bundle." : "Historical run · the draft has changed. Rerun to inspect current behavior.", fresh ? "studio-success" : "studio-warning");
            Body(_host, "Legacy parity: " + (_run.MatchesLegacy ? "PASS" : "Different authored behavior") + " · seed " + _run.Seed);
            Body(_host, "Frozen configuration: level " + _run.Options.Level + " · card rank " + _run.Options.Rank + " · tier " + _run.Options.Tier);
            var battle = _run.Battle;
            _actorPreview = new StudioPreviewActor();
            _host.Add(_actorPreview.Root);
            _actorPreview.SetScenario(_draft.SourceCharacter, battle.Events, _run.InputPlan.RequestId == "studio.m3.incoming-hit" ? battle.Opponent.Fighters[0].Id : battle.Player.Fighters[0].Id);
            Section(_host, "ACTUAL PASSIVE DISPATCH TRACE");
            foreach (var trace in _run.Trace)
            {
                var captured = trace;
                var row = Button(_host, (trace.Fired ? "FIRED" : "SKIPPED") + " · " + trace.Trigger + " · " + trace.RuleId + "\n" + trace.Reason,
                    () => SelectSource(string.IsNullOrEmpty(captured.ActionNodeId) ? captured.RuleId : captured.ActionNodeId));
                row.AddToClassList("studio-event"); row.style.whiteSpace = WhiteSpace.Normal;
                foreach (var condition in trace.Conditions)
                {
                    var selectedCondition = condition;
                    var detail = Button(_host, "    " + (condition.Passed ? "PASS" : "FAIL") + " · " + condition.NodeId + " · " + condition.Explanation,
                        () => SelectSource(selectedCondition.NodeId));
                    detail.style.whiteSpace = WhiteSpace.Normal;
                }
            }
            Section(_host, "COMMITTED BATTLE FACTS");
            foreach (var fact in battle.Events)
                Body(_host, "#" + fact.Id + " " + fact.Kind + " · " + fact.SourceId + " → " + fact.TargetId + " · " + fact.Amount + " · " + fact.Message, "studio-event");
            Section(_inspector, "STATE DIFFERENCES");
            if (_run.Changes.Count == 0) Body(_inspector, "No authoritative state differences from legacy.", "studio-success");
            foreach (var change in _run.Changes) Body(_inspector, change);
            Section(_inspector, "FINAL PLAYER STATE");
            foreach (var fighter in battle.Player.Fighters)
                Body(_inspector, fighter.Id + "\nHP " + fighter.Health + " · Gauge " + fighter.PowerGauge + (fighter.IsReserve ? " · reserve" : ""));
            Section(_inspector, "FROZEN IDENTITIES");
            Body(_inspector, "Gameplay " + Short(_run.Hashes.Gameplay), "studio-code");
            Body(_inspector, "Text " + Short(_run.Hashes.Text), "studio-code");
            Body(_inspector, "State " + Short(_run.StateHash), "studio-code");
            Body(_inspector, "Trace is captured by the production dispatcher. Animations observe copied facts and never calculate damage or reroll a target.", "studio-subtle");
        }
    }
}
#endif
