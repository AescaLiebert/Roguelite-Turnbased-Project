using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;

public static class M3ClosureChecks
{
    private static int count;
    private static void Check(bool pass, string message) { if (!pass) throw new InvalidOperationException("M3 closure: " + message); count++; }
    private static StudioDocument Document() => StudioDocumentService.ImportOpeningGauge(SnapshotJson.Deserialize<CharacterDefinition>(File.ReadAllText("Artifacts/CharacterCreatorStudio/M0/KaeyaLegacyDefinition.json")));
    private static StudioActionNode Node(string id, StudioActionKind kind) => new StudioActionNode { Id = id, Kind = kind };
    private static StudioDocument Program(params StudioActionNode[] nodes)
    { var d = Document(); d.Rules[0].ActionRootId = nodes[0].Id; d.Actions.AddRange(nodes); return d; }
    private static bool Has(StudioDocument d, string code) => StudioCompiler.Compile(d).Diagnostics.Any(e => e.Code == code);
    private static StudioActionNode Counter(string id, string key, int amount) => new StudioActionNode { Id = id, Kind = StudioActionKind.Command,
        Command = new PassiveCommandDefinition { Kind = PassiveCommandKind.SetCounter, CounterKey = key, Amount = amount } };
    private static TargetQueryDefinition Query(string reuse = null, string store = null, string exclude = null) => new TargetQueryDefinition {
        BaseScope = EffectTargetScope.AllEnemies, Filter = new PassiveTargetFilter { Relation = PassiveRelation.Any }, Limit = 3,
        ReuseBinding = reuse, StoreBinding = store, ExcludeBinding = exclude };
    public static int Run()
    {
        count = 0;
        var d = Document(); var skill = d.LegacyCharacter.Skills[0];
        var variant = new AbilityVariantDefinition { Id = "variant.audit", InitiallyUnlocked = true, Ranks = skill.Ranks.ConvertAll(r => r.Clone()) };
        d.AbilityVariants.Add(new StudioAbilityVariant { SkillId = skill.Id, Definition = variant });
        foreach (var rank in variant.Ranks) { rank.Effect.Kind = EffectKind.ApplyStatus; rank.Effect.StatusRecipe = null; }
        Check(Has(d, "ability.validation"), "Null status payload reproducer is rejected.");
        foreach (var rank in variant.Ranks) { rank.Effect.Kind = EffectKind.Damage; rank.Effect.CoefficientBp = -10000; }
        Check(Has(d, "ability.validation"), "Negative coefficient reproducer is rejected.");
        foreach (var rank in variant.Ranks)
        {
            rank.Effect.CoefficientBp = 10000;
            rank.Effect.Sequence.Add(new CardEffectStep { Timing = CardEffectTiming.AfterAction,
                Effect = new EffectOperationDefinition { Id = "variant.recovery", Kind = EffectKind.Heal, Target = EffectTargetScope.Self,
                    HealValue = new EffectValueDefinition { Source = EffectValueSource.TargetMaxHealth, CoefficientBp = 2000 } } });
        }
        Check(StudioCompiler.Compile(d).Success, "Complete composed alternate ranks compile.");
        variant.Ranks[0].Effect.Sequence.Last().Effect.HealValue.CoefficientBp = -1;
        Check(Has(d, "ability.validation"), "Sequence payload receives the same validation as a primary effect.");

        var root = Node("root", StudioActionKind.Sequence); root.Children.AddRange(new[] { "choose", "reuse" });
        var choose = Node("choose", StudioActionKind.ForEachTarget); choose.Query = Query(store: "splash");
        var reuse = Node("reuse", StudioActionKind.ForEachTarget); reuse.Query = Query(reuse: "missing");
        d = Program(root, choose, reuse);
        Check(Has(d, "binding.undefined-target"), "Undefined target reuse is rejected at source.");
        reuse.Query = Query(exclude: "missing");
        Check(Has(d, "binding.undefined-target"), "Undefined target exclusion cannot silently broaden selection.");
        reuse.Query = Query(reuse: "splash");
        Check(StudioCompiler.Compile(d).Success, "An earlier stored target set is available to a later sibling.");
        Check(StudioBindingAnalysis.Analyze(d).Targets["reuse"].Contains("splash"), "Inspector inventory uses the compiler's definite scope.");
        root.Children.Reverse();
        Check(Has(d, "binding.undefined-target"), "Moving a consumer before its producer blocks activation.");
        root.Children.Reverse();
        var guard = Node("guard", StudioActionKind.Branch); guard.ConditionRootId = "condition.always"; guard.Children.Add("choose");
        d.Actions.Add(guard); root.Children[0] = "guard";
        Check(Has(d, "binding.undefined-target"), "A selection produced on only one branch is not definitely assigned.");
        guard.ElseChildren.Add("choose");
        Check(StudioCompiler.Compile(d).Success, "A producer on both branches is definitely assigned.");
        var loop = Node("outer", StudioActionKind.ForEachTarget); loop.Query = Query(); loop.Children.Add("choose"); d.Actions.Add(loop); root.Children[0] = "outer";
        Check(Has(d, "binding.undefined-target"), "Zero loop iterations cannot export a child-produced binding.");
        root.Children[0] = "choose";
        var fixture = StudioSimulation.CreateFixture(StudioCompiler.Compile(d).Character, 7);
        var context = new CardEffectContext { Battle = fixture, Actor = fixture.Player.Fighters[0], EffectOwner = fixture.Player.Fighters[0] };
        bool threw = false; try { TargetQuerySystem.Resolve(Query(reuse: "absent"), context); } catch (PassiveExecutionException) { threw = true; }
        Check(threw, "Direct runtime callers receive explicit missing-binding errors.");
        context.TargetBindings["empty"] = new List<string>();
        Check(TargetQuerySystem.Resolve(Query(reuse: "empty"), context).Count == 0, "A valid empty set remains a legal no-target outcome.");

        var seed = Counter("seed", "resource.central", 100);
        var spend = Node("spend", StudioActionKind.SpendResource); spend.ResourceKey = "resource.central"; spend.Amount = 2;
        var reward = Counter("reward", "receipt.reward", 1); spend.Children.Add(reward.Id);
        root = Node("root", StudioActionKind.Sequence); root.Children.AddRange(new[] { seed.Id, spend.Id });
        d = Program(root, seed, spend, reward); d.Resources.Add(new StudioResource { Id = "resource.central", Name = "Central charges", Cap = 6 });
        var before = SnapshotJson.Serialize(d); var compiled = StudioCompiler.Compile(d);
        Check(compiled.Success, string.Join("; ", compiled.Diagnostics));
        var state = StudioSimulation.CreateFixture(compiled.Character, 12);
        Check(CharacterPassiveRuntime.Counter(state.Player.Fighters[0], "resource.central") == 4, "Shared cap overrides repeated operation caps before exact payment.");
        Check(SnapshotJson.Serialize(d) == before, "Library expansion leaves authoring/source payloads immutable.");
        var payment = state.PassiveTrace.Single(t => t.ActionNodeId == "spend");
        Check(payment.ResourceBefore == 6 && payment.ResourceAfter == 4 && payment.Branch == "Success", "Trace records payment values and selected branch.");
        Check(payment.ActorId == null && payment.OwnerId == state.Player.Fighters[0].Id, "Battle-start trace distinguishes absent event actor from owner.");
        var clone = payment.Clone(); clone.Values["bad"] = 4; clone.SelectedTargets.Add("bad");
        Check(!payment.Values.ContainsKey("bad") && !payment.SelectedTargets.Contains("bad"), "Observed contexts clone independently.");
        var copy = StudioDocumentService.Duplicate(d);
        Check(copy.Resources[0].Id != d.Resources[0].Id && copy.Actions.Find(a => a.Kind == StudioActionKind.SpendResource).ResourceKey == copy.Resources[0].Id && StudioCompiler.Compile(copy).Success,
            "Document duplication remaps central resource references.");
        d.Resources[0].Cap = 0;
        Check(Has(d, "resource.cap"), "An invalid shared cap blocks compilation.");

        var apply = Node("apply", StudioActionKind.Command);
        apply.Command.Effect = new CardEffectOperationDefinition { Kind = CardEffectOperationKind.ApplyStatus, Target = EffectTargetScope.Self,
            Status = new StatusApplicationRecipe { StatusRecipeId = "status.central" } };
        d = Program(apply); var status = new StudioStatus { Id = "status.central", Name = "Shared mark", Definition = new StatusRecipeDefinition {
            Id = "status.central", NameKey = "Shared mark", MaxStacks = 3, BreakRule = StatusBreakRule.OnSourceDefeated, Identity = StatusIdentityScope.RecipeAndSource } };
        d.Statuses.Add(status);
        var local = new StudioRule { Id = "local", Name = "Observe removal", StatusId = status.Id, Trigger = PassiveEventKind.StatusRemoved,
            ConditionRootId = "condition.always", ActionRootId = "receipt" };
        local.Settings.EventStatusRecipeId = status.Id; local.Settings.AllowReactionOrigin = true; d.Rules.Add(local);
        d.Actions.Add(Counter("receipt", "receipt.removal", 1));
        compiled = StudioCompiler.Compile(d);
        Check(compiled.Success, string.Join("; ", compiled.Diagnostics));
        Check(compiled.Character.Passive.Reactions.All(r => r.Id != "local"), "Status-local rule does not leak into global character reactions.");
        state = StudioSimulation.CreateFixture(compiled.Character, 12);
        var instance = state.Player.Fighters[0].Statuses.Instances.Single(s => s.RecipeId == status.Id);
        Check(instance.Recipe.BreakRule == StatusBreakRule.OnSourceDefeated && instance.Recipe.Reactions.Any(r => r.Id == "local"), "Applied instance carries centralized lifetime and local program.");
        var removals = state.Player.Fighters[0].Statuses.ConsumeLayers(status.Id, 1, state.Player.Fighters[0].Id, true, StatusRemovalReason.Absorbed);
        CharacterPassiveRuntime.NotifyStatusRemoved(state, state.Player.Fighters[0], state.Player.Fighters[0], removals, "closure.removal");
        Check(CharacterPassiveRuntime.Counter(state.Player.Fighters[0], "receipt.removal") == 1, "A fully removed shared status executes its compiled local rule from the frozen snapshot.");
        copy = StudioDocumentService.Duplicate(d);
        Check(copy.Rules.Find(r => r.Name == "Observe removal").StatusId == copy.Statuses[0].Id && StudioCompiler.Compile(copy).Success, "Status ownership and application references remap on duplication.");
        var legacy = StudioDocumentService.Clone(d); legacy.SchemaVersion = 2; legacy.Resources = null; legacy.Statuses = null;
        var oldJson = SnapshotJson.Serialize(legacy);
        Check(StudioDocumentService.TryMigrate(legacy, out var migrated, out var notices) && migrated.SchemaVersion == StudioDocument.CurrentSchemaVersion && notices.Any(n => n.Code == "migration.v2-v3"), "Schema2 migrates through shared definitions to the current schema.");
        Check(SnapshotJson.Serialize(legacy) == oldJson, "Migration preserves original source bytes in memory.");
        d = Program(Counter("seed", "resource.save", 5));
        d.Resources.Add(new StudioResource { Id = "resource.save", Name = "Save charges", Cap = 6 });
        d.Policies.Add(new StudioPolicy { Id = "cap", Definition = new CombatPolicyDefinition { Kind = CombatPolicyKind.IncomingDamageCap, MaximumHealthDamageBp = 4900 } });
        d.Policies.Add(new StudioPolicy { Id = "save", Definition = new CombatPolicyDefinition { Kind = CombatPolicyKind.BeforeDefeat,
            ResourceKey = "resource.save", ResourceCost = 3, HealMaxHealthBp = 5000 } });
        var capRun = StudioSimulation.Run(d, options: new StudioSimulationOptions { Preset = StudioScenarioPreset.IncomingCap });
        var hp = capRun.OpeningBattle.Player.Fighters[0].Health;
        Check(capRun.Battle.Events.First(e => e.Kind == BattleEventKind.DamageApplied && e.TargetId == capRun.OpeningBattle.Player.Fighters[0].Id).HealthLost == hp * 4900 / 10000,
            "Incoming-contact acceptance preset exercises the authored cap before ordinary end-turn recovery.");
        var fatalRun = StudioSimulation.Run(d, options: new StudioSimulationOptions { Preset = StudioScenarioPreset.FatalSave });
        Check(fatalRun.Battle.Player.Fighters[0].IsAlive && CharacterPassiveRuntime.Counter(fatalRun.Battle.Player.Fighters[0], "resource.save") == 2,
            "Fatal-save acceptance preset exercises recovery and exact authored cost.");
        Check(BattleEngine.TryResolvePlan(fatalRun.OpeningBattle, fatalRun.InputPlan, out var replay, out _) && StudioSimulation.StateHash(replay) == fatalRun.StateHash,
            "Incoming preset freezes its actual opponent input for exact replay.");
        Console.WriteLine("PASS: " + count + " M3 closure validation, bindings, libraries, context and migration assertions."); return count;
    }
}
