using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;

public static class M3StudioChecks
{
    private static int checks;
    private static void Check(bool pass, string message) { if (!pass) throw new InvalidOperationException("M3 Studio: " + message); checks++; }
    private static bool Error(StudioDocument doc, string code) => StudioCompiler.Compile(doc).Diagnostics.Any(d => d.Severity == StudioDiagnosticSeverity.Error && d.Code == code);
    private static StudioCompileResult Compile(StudioDocument doc)
    {
        var result = StudioCompiler.Compile(doc); Check(result.Success, string.Join("; ", result.Diagnostics)); return result;
    }

    public static int Run()
    {
        checks = 0;
        var baseline = SnapshotJson.Deserialize<CharacterDefinition>(File.ReadAllText("Artifacts/CharacterCreatorStudio/M0/KaeyaLegacyDefinition.json"));
        var sourceJson = SnapshotJson.Serialize(baseline);
        var original = StudioDocumentService.ImportOpeningGauge(baseline);
        var initial = Compile(original);
        Check(original.SchemaVersion == StudioDocument.CurrentSchemaVersion, "New documents use the current schema.");
        var old = StudioDocumentService.Clone(original); old.SchemaVersion = 1;
        old.Actions = null; old.Modules = null; old.Policies = null; old.AbilityVariants = null; old.AbilityOverrides = null;
        var oldJson = SnapshotJson.Serialize(old);
        Check(StudioDocumentService.TryMigrate(old, out var migrated, out var notices), "Schema 1 migrates.");
        Check(SnapshotJson.Serialize(old) == oldJson && migrated.SchemaVersion == StudioDocument.CurrentSchemaVersion && notices.Any(n => n.Code == "migration.v1-v2"), "Migration is explicit and leaves source untouched.");
        Check(Compile(migrated).Hashes.Gameplay == initial.Hashes.Gameplay, "Migration preserves legacy-slice gameplay.");
        Check(Compile(migrated).Hashes.Text == initial.Hashes.Text, "Migration preserves bound descriptions.");

        var doc = ActionDocument(original);
        var result = Compile(doc);
        var runtime = result.Character.Passive.Reactions.Single(r => r.Id == doc.Rules[0].Id);
        Check(runtime.ExecutionVersion == 1 && runtime.Actions[0].Children[0].Commands[0].Effect.Magnitude == 1, "Flat actions compile into versioned runtime trees with bound values.");
        Check(StudioSimulation.Run(doc).MatchesLegacy, "Converted generic action retains real-engine opening parity.");
        var renamed = StudioDocumentService.Clone(doc); renamed.Actions[0].Name = "Editor note"; renamed.Rules[0].Name = "Other title";
        Check(Compile(renamed).Hashes.Bundle == result.Hashes.Bundle, "Rule/action editor labels do not change hashes.");
        var reordered = StudioDocumentService.Clone(doc); reordered.Actions.Reverse(); reordered.Conditions.Reverse(); reordered.Parameters.Reverse();
        Check(Compile(reordered).Hashes.Bundle == result.Hashes.Bundle, "Flat storage order does not change compiled identities.");

        var module = new StudioModule { Id = "module.open", Name = "Opening resource", RootActionId = "action.gauge" };
        doc.Modules.Add(module);
        doc.Conditions.Add(new StudioConditionNode { Id = "condition.closed", Kind = EffectConditionKind.SourceGaugeAtLeast, Threshold = 5 });
        doc.Actions[0].Children.Clear();
        doc.Actions[0].Children.Add("action.module");
        doc.Actions.Add(new StudioActionNode { Id = "action.module", Kind = StudioActionKind.Module, ModuleId = module.Id, ConditionRootId = "condition.closed" });
        var guarded = Compile(doc);
        Check(guarded.Character.Passive.Reactions.Last().Actions[0].Children[0].Conditions.Count == 1, "Module call guard survives expansion.");
        Check(StudioSimulation.CreateFixture(guarded.Character, 1).Player.Fighters[0].PowerGauge == 0, "A false module guard skips its body in the production dispatcher.");
        doc.Conditions.Last().Threshold = 0;
        Check(StudioSimulation.CreateFixture(Compile(doc).Character, 1).Player.Fighters[0].PowerGauge == 1, "A true module guard executes the reused body.");

        var copy = StudioDocumentService.Duplicate(doc);
        Check(Compile(copy).Success && copy.Modules[0].Id != doc.Modules[0].Id &&
              copy.Actions.Single(a => a.Kind == StudioActionKind.Module).ModuleId == copy.Modules[0].Id, "Duplication remaps module calls and action roots.");
        Check(copy.Actions.Single(a => a.Id == copy.Modules[0].RootActionId).AmountParameterId == copy.Parameters[0].Id, "Duplicated module parameters remain bound.");
        var bad = StudioDocumentService.Clone(doc); bad.Modules[0].RootActionId = "action.module";
        Check(Error(bad, "action.cycle"), "Recursive module calls are rejected.");
        bad = StudioDocumentService.Clone(doc); bad.Actions[0].Children.Add("missing");
        Check(Error(bad, "reference.action-child"), "Missing action references are rejected.");
        bad = StudioDocumentService.Clone(doc); bad.Actions.Single(a => a.Kind == StudioActionKind.Module).ModuleId = "missing";
        Check(Error(bad, "reference.module"), "Missing modules are rejected.");
        bad = ActionDocument(original); bad.Actions[1].AmountParameterId = "missing";
        Check(Error(bad, "reference.action-parameter"), "Broken bound parameters are rejected.");
        bad = ActionDocument(original); bad.Parameters[0].Value = 6;
        Check(Error(bad, "operation.gauge"), "Bound gauge values are validated after substitution.");
        bad = ActionDocument(original); bad.Actions[1].AmountParameterId = null;
        bad.Actions[1].CoefficientParameterId = bad.Parameters[0].Id;
        Check(Error(bad, "parameter.unit-mismatch"), "Coefficient bindings reject integer units.");

        var costs = ActionDocument(original);
        costs.Actions[0].Children = new List<string> { "action.grant", "action.spend" };
        costs.Actions.Add(new StudioActionNode { Id = "action.grant", Kind = StudioActionKind.Command,
            Command = new PassiveCommandDefinition { Kind = PassiveCommandKind.SetCounter, CounterKey = "test.resource", CounterCap = 6, Amount = 3 } });
        costs.Actions.Add(new StudioActionNode { Id = "action.spend", Kind = StudioActionKind.SpendResource,
            ResourceKey = "test.resource", Amount = 2, Children = new List<string> { "action.gauge" } });
        // Seeded opening hand merges can independently grant gauge; retain the same-seed control.
        var controlGauge = StudioSimulation.CreateFixture(Compile(ActionDocument(original)).Character, 5).Player.Fighters[0].PowerGauge;
        var costState = StudioSimulation.CreateFixture(Compile(costs).Character, 5);
        Check(CharacterPassiveRuntime.Counter(costState.Player.Fighters[0], "test.resource") == 1 && costState.Player.Fighters[0].PowerGauge == controlGauge,
            "Compiled sequence grants, atomically spends, and executes success children in order.");
        costs.Actions.Last().Amount = 4;
        costState = StudioSimulation.CreateFixture(Compile(costs).Character, 5);
        Check(CharacterPassiveRuntime.Counter(costState.Player.Fighters[0], "test.resource") == 3 && costState.Player.Fighters[0].PowerGauge == controlGauge - 1,
            "Insufficient compiled cost leaves resources intact and skips success effects.");
        costs.Actions.Last().Amount = 0;
        Check(Error(costs, "action.positive-amount"), "Zero-cost spend cannot silently suppress the branch.");

        var selectors = ActionDocument(original); var op = selectors.Actions[1].Command.Effect;
        op.TargetQuery = new TargetQueryDefinition { BaseScope = EffectTargetScope.AllAllies, Anchor = TargetQueryAnchor.Owner,
            Filter = new PassiveTargetFilter { Relation = PassiveRelation.Allies, IncludeReserve = false }, Limit = 3, SampleCount = 1, StoreBinding = "chosen" };
        Check(Compile(selectors).Character.Passive.Reactions.Last().Actions[0].Children[0].Commands[0].Effect.TargetQuery.Anchor == TargetQueryAnchor.Owner,
            "Selector anchor/filter/sample/binding survive compilation.");
        op.TargetQuery.SampleCount = 4;
        Check(Error(selectors, "query.invalid"), "Sample counts are bounded by selection limits.");

        var upgrades = ActionDocument(original); var skill = baseline.Skills.OrderBy(s => s.Slot).First();
        upgrades.AbilityVariants.Add(new StudioAbilityVariant { SkillId = skill.Id, Definition = new AbilityVariantDefinition {
            Id = "variant.ex", Name = "EX test", InitiallyUnlocked = true, MinimumLevel = 2, Ranks = skill.Ranks.ConvertAll(r => r.Clone()) } });
        upgrades.Parameters.Add(new StudioParameter { Id = "param.ex-coefficient", Name = "EX coefficient", Unit = StudioParameterUnit.BasisPoints, Value = 30000 });
        upgrades.AbilityOverrides.Add(new StudioAbilityOverride { SkillId = skill.Id, VariantId = "variant.ex", ValueParameterId = "param.ex-coefficient",
            Definition = new AbilityValueOverride { Id = "override.ex", Field = AbilityValueField.CoefficientBp, MinimumLevel = 2, MinimumRank = 2, MinimumTier = 1 } });
        var upgraded = Compile(upgrades);
        Check(upgraded.Character.Skills.First(s => s.Id == skill.Id).Variants.Single().Overrides.Single().Value == 30000, "Variant override compiles its typed parameter.");
        var duplicate = StudioDocumentService.Duplicate(upgrades);
        Check(duplicate.AbilityOverrides[0].VariantId == duplicate.AbilityVariants[0].Definition.Id &&
            duplicate.AbilityOverrides[0].ValueParameterId == duplicate.Parameters.Last().Id && Compile(duplicate).Success, "Variant and override duplication remaps local references.");
        var run = StudioSimulation.Run(upgrades, options: new StudioSimulationOptions { Level = 2, Rank = 2, Tier = 1 });
        Check(run.OpeningBattle.Player.Fighters[0].Level == 2 && run.Options.Level == 2 && run.Options.Rank == 2 && run.Options.Tier == 1,
            "Scenario captures independent level/rank/tier configuration.");
        Check(run.OpeningBattle.Player.Hand.Where(c => c.OwnerFighterId == run.OpeningBattle.Player.Fighters[0].Id && c.Kind == CardKind.Skill).All(c => c.Rank == 2),
            "Opening card rank is part of the frozen authoritative input.");
        Check(StudioSimulation.Run(upgrades, seed: run.Seed, options: run.Options).StateHash == run.StateHash, "Configured variant scenario reproduces deterministically.");
        var retained = run.Options; retained.Level = 999;
        Check(run.Options.Level == 2, "Run configuration is immutable through copied getters.");
        Check(SnapshotJson.Serialize(baseline) == sourceJson, "All M3 compiler/simulation work preserves the imported source.");
        Console.WriteLine("PASS " + checks + " M3 Studio compiler, migration and authoring assertions.");
        return checks;
    }

    private static StudioDocument ActionDocument(StudioDocument original)
    {
        var doc = StudioDocumentService.Clone(original);
        doc.Rules[0].ActionRootId = "action.root";
        doc.Actions.Add(new StudioActionNode { Id = "action.root", Kind = StudioActionKind.Sequence, Children = new List<string> { "action.gauge" } });
        doc.Actions.Add(new StudioActionNode { Id = "action.gauge", Kind = StudioActionKind.Command,
            AmountParameterId = "param.opening-gauge", Command = new PassiveCommandDefinition { Kind = PassiveCommandKind.ExecuteEffect,
                Effect = new CardEffectOperationDefinition { Kind = CardEffectOperationKind.ChangePowerGauge, Target = EffectTargetScope.AllAllies, Magnitude = 1 } } });
        return doc;
    }
}
