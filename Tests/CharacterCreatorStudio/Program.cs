using System;
using System.IO;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using CharacterCreatorStudio.Checks;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); checks++; }
    private static bool HasError(StudioCompileResult result, string code) => !result.Success && result.Diagnostics.Any(d => d.Code == code);

    private static void Main(string[] args)
    {
        var path = args.Length > 0 ? args[0] : "Artifacts/CharacterCreatorStudio/M0/KaeyaLegacyDefinition.json";
        var baseline = SnapshotJson.Deserialize<CharacterDefinition>(File.ReadAllText(path));
        Check(baseline.Id == "fighter.kaeya", "Frozen Kaeya baseline required.");
        checks += SharedCardSettingsChecks.Run(baseline);
        Check(baseline.Passive.Support.OpeningTeamGauge == 1, "Baseline opening gauge is one.");
        var sourceJson = SnapshotJson.Serialize(baseline);
        var doc = StudioDocumentService.ImportOpeningGauge(baseline);
        var initial = StudioCompiler.Compile(doc);
        Check(initial.Success, string.Join("; ", initial.Diagnostics));
        Check(initial.Character.Passive.Support.OpeningTeamGauge == 0, "Migrated source does not execute opening twice.");
        Check(SnapshotJson.Serialize(baseline) == sourceJson, "Import and compile never mutate original.");
        var expectedSupport = baseline.Passive.Support.Clone(); expectedSupport.OpeningTeamGauge = 0;
        Check(SnapshotJson.Serialize(initial.Character.Passive.Support) == SnapshotJson.Serialize(expectedSupport), "Other support capabilities are preserved.");
        Check(initial.Character.Skills.Count == baseline.Skills.Count, "Imported skills retained.");
        Check(initial.Character.Passive.Reactions.Any(r => r.Id == doc.Rules[0].Id &&
            r.Commands[0].Kind == PassiveCommandKind.ExecuteEffect && r.Commands[0].Effect.Kind == CardEffectOperationKind.ChangePowerGauge), "Compiles into shared runtime operation.");
        Check(initial.Description.PlainText.Contains("1") && initial.Description.PlainText.Contains("Power Gauge"), "Bound description and keyword compile.");

        var roundTrip = SnapshotJson.Deserialize<StudioDocument>(SnapshotJson.Serialize(doc));
        Check(StudioCompiler.Compile(roundTrip).Hashes.Bundle == initial.Hashes.Bundle, "Flat nested document round-trips deterministically.");
        Check(roundTrip.Conditions.Single(n => n.Logic == ConditionLogic.Not).Children.Count == 1, "Not node survives serialization.");
        var reordered = StudioDocumentService.Clone(doc); reordered.Parameters.Reverse(); reordered.Conditions.Reverse(); reordered.Rules.Reverse();
        Check(StudioCompiler.Compile(reordered).Hashes.Bundle == initial.Hashes.Bundle, "Table storage order does not affect compilation.");
        var editorOnly = StudioDocumentService.Clone(doc); editorOnly.Name = "Other workspace label"; editorOnly.Parameters[0].Name = "Other label";
        Check(StudioCompiler.Compile(editorOnly).Hashes.Bundle == initial.Hashes.Bundle, "Editor labels do not alter semantic hashes.");
        var text = StudioDocumentService.Clone(doc); text.Description.Locales[0].Blocks[0].Source += " เพิ่มพลังให้ทีม";
        var textResult = StudioCompiler.Compile(text);
        Check(textResult.Success && textResult.Hashes.Text != initial.Hashes.Text && textResult.Hashes.Gameplay == initial.Hashes.Gameplay &&
            textResult.Hashes.Presentation == initial.Hashes.Presentation && textResult.Hashes.Bundle != initial.Hashes.Bundle, "Thai text changes only text/bundle hashes.");
        var visual = StudioDocumentService.Clone(doc); visual.Presentation.DurationMilliseconds = 2200;
        var visualResult = StudioCompiler.Compile(visual);
        Check(visualResult.Hashes.Gameplay == initial.Hashes.Gameplay && visualResult.Hashes.Text == initial.Hashes.Text &&
            visualResult.Hashes.Presentation != initial.Hashes.Presentation, "Presentation changes remain isolated.");
        var gameplay = StudioDocumentService.Clone(doc); gameplay.Parameters.First(p => p.Id == "param.opening-gauge").Value = 2;
        var gameplayResult = StudioCompiler.Compile(gameplay);
        Check(gameplayResult.Success && gameplayResult.Hashes.Gameplay != initial.Hashes.Gameplay && gameplayResult.Description.PlainText.Contains("2"), "Edited mechanic updates runtime and wording.");

        var copied = StudioDocumentService.Duplicate(doc);
        Check(copied.Id != doc.Id && copied.SourceCharacterId == doc.SourceCharacterId && copied.LegacyCharacter.Id == doc.LegacyCharacter.Id,
            "Duplication preserves source identity while remapping local IDs.");
        Check(!copied.Parameters.Select(p => p.Id).Intersect(doc.Parameters.Select(p => p.Id)).Any() &&
            !copied.Conditions.Select(n => n.Id).Intersect(doc.Conditions.Select(n => n.Id)).Any(), "Local IDs remap.");
        var copiedResult = StudioCompiler.Compile(copied);
        Check(copiedResult.Success && copiedResult.Description.PlainText == initial.Description.PlainText, "Duplicate parameter and snippet references resolve.");
        var future = StudioDocumentService.Clone(doc); future.SchemaVersion = 99;
        var futureBefore = SnapshotJson.Serialize(future);
        Check(!StudioDocumentService.TryMigrate(future, out var migrated, out var diagnostics) && migrated == null && diagnostics.Count > 0 &&
            SnapshotJson.Serialize(future) == futureBefore, "Unknown schema preserves original and refuses activation.");
        Check(HasError(StudioCompiler.Compile(future), "schema.unsupported"), "Unknown compile schema rejected.");

        var broken = StudioDocumentService.Clone(doc); broken.Rules[0].AmountParameterId = "missing";
        Check(HasError(StudioCompiler.Compile(broken), "reference.parameter"), "Dangling amount reference rejected.");
        broken = StudioDocumentService.Clone(doc); broken.Conditions[0].Children.Add("missing");
        Check(HasError(StudioCompiler.Compile(broken), "reference.child"), "Dangling child rejected.");
        broken = StudioDocumentService.Clone(doc); broken.Conditions[0].Children.Add(broken.Conditions[0].Id);
        Check(HasError(StudioCompiler.Compile(broken), "graph.cycle"), "Cycles rejected.");
        broken = StudioDocumentService.Clone(doc); broken.Conditions[1].Kind = (EffectConditionKind)999;
        Check(HasError(StudioCompiler.Compile(broken), "condition.unsupported"), "Runtime-only unsupported condition rejected.");
        broken = StudioDocumentService.Clone(doc); broken.Rules[0].Trigger = PassiveEventKind.Healed;
        Check(HasError(StudioCompiler.Compile(broken), "trigger.legacy-slice"), "Legacy opening-gauge rule cannot silently become another event.");
        broken = StudioDocumentService.Clone(doc); broken.Parameters[0].Unit = StudioParameterUnit.BasisPoints;
        Check(HasError(StudioCompiler.Compile(broken), "parameter.gauge"), "Typed gauge amount rejects basis points.");
        broken = StudioDocumentService.Clone(doc); broken.Conditions[1].Kind = EffectConditionKind.SourceHealthAtMost;
        Check(HasError(StudioCompiler.Compile(broken), "parameter.unit-mismatch"), "Typed health threshold rejects integer.");
        broken = StudioDocumentService.Clone(doc); broken.Parameters.Add(broken.Parameters[0]);
        Check(HasError(StudioCompiler.Compile(broken), "id.duplicate"), "Duplicate IDs rejected.");
        broken = StudioDocumentService.Clone(doc); broken.Description.Locales[0].Blocks[0].Source = "<b>Broken";
        Check(HasError(StudioCompiler.Compile(broken), "description.invalid"), "Invalid text blocks activation without destroying source.");
        Check(StudioCapabilities.All.Any(c => c.Id == "limits.cooldown" && c.Status == StudioCapabilityStatus.Enabled), "M3 cooldown is backed by executable opt-in runtime controls.");

        broken = StudioDocumentService.Clone(doc);
        broken.Conditions.Clear();
        broken.Conditions.Add(new StudioConditionNode { Id = "leaf", Kind = EffectConditionKind.Always });
        for (var i = 1; i < 24; i++) broken.Conditions.Add(new StudioConditionNode { Id = "expansion." + i,
            Logic = ConditionLogic.All, Children = new System.Collections.Generic.List<string> {
                i == 1 ? "leaf" : "expansion." + (i - 1), i == 1 ? "leaf" : "expansion." + (i - 1) } });
        broken.Rules[0].ConditionRootId = "expansion.23";
        Check(HasError(StudioCompiler.Compile(broken), "graph.expansion-budget"), "Flat DAG cannot expand exponentially into runtime allocations.");

        var nested = StudioDocumentService.Clone(doc);
        var alternative = nested.Conditions.Single(n => n.Id == "condition.always");
        alternative.Kind = EffectConditionKind.SourceGaugeAtLeast; alternative.Threshold = 1;
        Check(StudioSimulation.Run(nested).MatchesLegacy, "Any first branch false and nested Not true execute through production engine.");
        nested.Conditions.Single(n => n.Id == "condition.never").Threshold = 0;
        Check(StudioSimulation.Run(nested).Trace.Any(t => t.RuleId == doc.Rules[0].Id && !t.Fired), "Any false branches and Not false skip through production engine.");
        var healthCondition = StudioDocumentService.Clone(doc);
        healthCondition.Parameters.Add(new StudioParameter { Id = "param.health", Unit = StudioParameterUnit.BasisPoints, Value = 10000 });
        healthCondition.Conditions.Single(n => n.Id == "condition.gauge").Kind = EffectConditionKind.SourceHealthAtMost;
        healthCondition.Conditions.Single(n => n.Id == "condition.gauge").ThresholdParameterId = "param.health";
        Check(StudioSimulation.Run(healthCondition).MatchesLegacy, "Typed basis-point health threshold executes through production engine.");
        healthCondition.Parameters.Single(p => p.Id == "param.health").Value = 9000;
        Check(!StudioSimulation.Run(healthCondition).MatchesLegacy, "Health threshold controls real eligibility.");
        var rosterCondition = StudioDocumentService.Clone(doc);
        var rosterNode = rosterCondition.Conditions.Single(n => n.Id == "condition.gauge");
        rosterNode.Kind = EffectConditionKind.RosterCountAtLeast; rosterNode.Threshold = 4; rosterNode.ThresholdParameterId = null;
        rosterNode.RosterFilter = new PassiveTargetFilter { Relation = PassiveRelation.Allies, IncludeReserve = true, SeriesId = baseline.SeriesId };
        Check(StudioSimulation.Run(rosterCondition).MatchesLegacy, "Filtered matching roster including reserve can qualify the mechanic.");
        rosterNode.RosterFilter.IncludeReserve = false;
        Check(!StudioSimulation.Run(rosterCondition).MatchesLegacy, "Roster reserve filter controls real eligibility.");

        foreach (var seed in new ulong[] { 7, 12345, 973 })
        {
            var run = StudioSimulation.Run(roundTrip, seed: seed);
            Check(run.MatchesLegacy, "Legacy/gameplay parity at seed " + seed + ": " + string.Join("; ", run.Changes));
            Check(run.Trace.Any(t => t.RuleId == doc.Rules[0].Id && t.Fired && t.ConditionsPassed), "Actual runtime fired trace.");
            Check(run.Battle.Events.Any(e => e.Kind == BattleEventKind.DamageApplied && e.HitCount > 1), "Actual multi-hit damage feeds presentation.");
            Check(StudioSimulation.Run(roundTrip, seed: seed).StateHash == run.StateHash, "Seeded run repeats.");
            Check(StudioSimulation.Run(text, seed: seed).StateHash == run.StateHash, "Text edit changes neither results nor RNG.");
        }
        var frozen = StudioSimulation.Run(roundTrip);
        var frozenBefore = frozen.StateHash;
        roundTrip.Parameters[0].Value = 4;
        var leaked = frozen.Battle; leaked.Player.Fighters[0].Health = 1;
        var leakedSource = frozen.Source; leakedSource.Parameters[0].Value = 5;
        Check(frozen.StateHash == frozenBefore && frozen.Battle.Player.Fighters[0].Health != 1 && frozen.Source.Parameters[0].Value == 1,
            "Frozen run owns independent source/state snapshots.");
        var skipped = StudioDocumentService.Clone(doc); skipped.Parameters.First(p => p.Id == "param.minimum-gauge").Value = 1;
        var skipRun = StudioSimulation.Run(skipped);
        Check(skipRun.Trace.Any(t => t.RuleId == doc.Rules[0].Id && !t.Fired && !t.ConditionsPassed && t.Reason.Contains("failed")), "Actual runtime skip explanation.");
        Check(!skipRun.MatchesLegacy, "Changing eligibility changes scenario outcome.");
        var changedRun = StudioSimulation.Run(gameplay);
        Check(!changedRun.MatchesLegacy && changedRun.Changes.Any(c => c.Contains("gauge")), "Activation diff shows parameter effect.");
        for (var tier = 0; tier <= 5; tier++)
        {
            var legacy = StudioSimulation.CreateFixture(baseline, 12345, tier: tier);
            var compiled = StudioSimulation.CreateFixture(initial.Character, 12345, tier: tier);
            Check(StudioSimulation.StateHash(legacy) == StudioSimulation.StateHash(compiled), "C" + tier + " initial parity.");
        }
        var legacyReserve = StudioSimulation.CreateFixture(baseline, 12345, ownerInReserve: true);
        var compiledReserve = StudioSimulation.CreateFixture(initial.Character, 12345, ownerInReserve: true);
        Check(StudioSimulation.StateHash(legacyReserve) == StudioSimulation.StateHash(compiledReserve), "Reserve owner eligibility parity.");
        var reversedLegacy = StudioSimulation.CreateFixture(baseline, 12345, firstSide: TeamSide.Opponent);
        var reversedNew = StudioSimulation.CreateFixture(initial.Character, 12345, firstSide: TeamSide.Opponent);
        Check(StudioSimulation.StateHash(reversedLegacy) == StudioSimulation.StateHash(reversedNew), "Going-second opening parity.");
        Check(frozen.OpeningBattle.Player.Fighters.Last().IsReserve && frozen.OpeningBattle.Player.Fighters.Last().PowerGauge == 0,
            "Opening gauge excludes reserve allies.");
        var untraced = StudioSimulation.CreateFixture(initial.Character, 12345, captureTrace: false);
        var traced = StudioSimulation.CreateFixture(initial.Character, 12345, captureTrace: true);
        Check(untraced.PassiveTrace.Count == 0 && traced.PassiveTrace.Count > 0 && StudioSimulation.StateHash(untraced) == StudioSimulation.StateHash(traced),
            "Trace capture is a side-effect-free observer.");

        var originSource = baseline.Clone();
        originSource.Passive.Reactions.Add(new PassiveReactionDefinition { Id = "fixture.card-gauge-observer",
            Trigger = PassiveEventKind.GaugeChanged, CardOriginOnly = true, ActorRelation = PassiveRelation.Self,
            Commands = new System.Collections.Generic.List<PassiveCommandDefinition> {
                new PassiveCommandDefinition { Kind = PassiveCommandKind.IncrementCounter, CounterKey = "card-gauge-only", Amount = 1 } } });
        var originDraft = StudioDocumentService.ImportOpeningGauge(originSource);
        var originCompiled = StudioCompiler.Compile(originDraft);
        Check(originCompiled.Success, "Origin probe compiles with preserved imported reactions.");
        var originBattle = StudioSimulation.CreateFixture(originCompiled.Character, 12345);
        Check(CharacterPassiveRuntime.Counter(originBattle.Player.Fighters[0], "card-gauge-only") == 0,
            "Passive ExecuteEffect gauge changes cannot masquerade as card-origin effects.");
        CharacterPassiveRuntime.ChangePowerGauge(originBattle, originBattle.Player.Fighters[0], originBattle.Player.Fighters[0], 1, cardOrigin: true);
        Check(CharacterPassiveRuntime.Counter(originBattle.Player.Fighters[0], "card-gauge-only") == 1,
            "Actual card-origin gauge change still triggers imported card-only reactions.");

        var traceCopy = frozen.Trace[0]; traceCopy.Reason = "tampered";
        Check(frozen.Trace[0].Reason != "tampered", "Trace snapshots cannot mutate frozen evidence.");

        DescriptionChecks.Run();
        var m3Variants = M3VariantChecks.Run();
        var m3Policies = M3PolicyChecks.Run();
        var m3Execution = M3ExecutionChecks.Run();
        var m3Studio = M3StudioChecks.Run();
        var m3Context = M3ContextChecks.Run();
        var m3Closure = M3ClosureChecks.Run();
        var m4Description = M4DescriptionChecks.Run(baseline);
        var m5BattleLab = M5BattleLabChecks.Run(baseline);
        var m6Presentation = M6PresentationChecks.Run();
        var bridge = BridgeChecks.Run(baseline);
        var kitAuthoring = KitAuthoringChecks.Run(baseline);
        var passiveCentralization = PassiveCentralizationChecks.Run(baseline);
        if (args.Length > 1)
        {
            var evidencePath = Path.GetFullPath(args[1]);
            Directory.CreateDirectory(Path.GetDirectoryName(evidencePath));
            File.WriteAllText(evidencePath, SnapshotJson.Serialize(new {
                SchemaVersion = StudioDocument.CurrentSchemaVersion, PassiveCentralizationAssertions = passiveCentralization, KitAuthoringAssertions = kitAuthoring, BridgeAssertions = bridge, M6PresentationAssertions = m6Presentation, M5BattleLabAssertions = m5BattleLab, M4DescriptionAssertions = m4Description, M3ClosureAssertions = m3Closure, CoreAssertions = checks, M3VariantAssertions = m3Variants, M3PolicyAssertions = m3Policies,
                M3ExecutionAssertions = m3Execution, M3StudioAssertions = m3Studio, M3ContextAssertions = m3Context, Fixture = Path.GetFullPath(path),
                Hashes = frozen.Hashes, frozen.Seed, frozen.MatchesLegacy, frozen.StateHash, frozen.BaselineStateHash,
                RuntimeTrace = frozen.Trace, SkipTrace = skipRun.Trace,
                CommittedDamageEvents = frozen.Battle.Events.Where(e => e.Kind == BattleEventKind.DamageApplied).ToList(),
                EditedParameterChanges = changedRun.Changes,
                Scope = "Shared core and description contracts; Unity editor/renderer/actor gates require separate evidence." }));
        }
        Console.WriteLine("PASS: " + checks + " Studio core checks plus description suite; real-engine parity, nested serialization, validation, hashes, cloning, immutable snapshots, traces and damage playback facts.");
    }
}

