#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    /// <summary>A synthetic rule sandbox, never a playable case-study character or registry migration.</summary>
    public static class StudioM3Checks
    {
        public static StudioDraftAsset LastDraft { get; private set; }

        public static StudioDraftAsset CreateExampleDraft()
        {
            var source = Array.Find(CharacterObjectRegistrySO.LoadAll(), c => c != null && c.DefinitionId == "fighter.kaeya");
            if (source == null) throw new InvalidOperationException("The registered source fixture is missing.");
            var draft = StudioDraftRepository.Create(source);
            StudioDraftRepository.TryRead(draft, out var document, out var library, out var error);
            if (error != null) throw new InvalidOperationException(error);
            AddCaseStudyMechanics(document);
            StudioDraftRepository.Edit(draft, document, library, "Create synthetic M3 rule sandbox");
            StudioDraftRepository.Save(draft);
            LastDraft = draft;
            return draft;
        }

        public static void AddCaseStudyMechanics(StudioDocument document)
        {
            document.Name = "M3 reusable rule case study";
            document.Parameters.Add(new StudioParameter { Id = "param.resource-cost", Name = "Guard resource cost", Unit = StudioParameterUnit.Integer, Value = 3 });
            document.Description.Locales[0].Blocks[0].Source += "\nSynthetic guard costs {{value:param.resource-cost|integer}} charges; consume three authored layers.";
            void Command(string id, PassiveCommandDefinition command)
            { document.Actions.Add(new StudioActionNode { Id = id, Name = id, Kind = StudioActionKind.Command, Command = command }); }
            void Rule(string id, string name, PassiveEventKind trigger, string action, string condition = null, string eventResource = null, string eventStatus = null)
            {
                document.Rules.Add(new StudioRule { Id = id, Name = name, Trigger = trigger, ActionRootId = action, ConditionRootId = condition ?? "condition.always",
                    Gate = new PassiveGate { Presence = PassivePresence.LivingActive }, Settings = new PassiveReactionDefinition {
                        ExecutionVersion = 1, AllowReactionOrigin = true, ActorRelation = trigger == PassiveEventKind.BattleStarted ? PassiveRelation.Any : PassiveRelation.Self,
                        LimitScope = PassiveLimitScope.None, EventResourceKey = eventResource, EventStatusRecipeId = eventStatus } });
            }
            Command("action.seed", new PassiveCommandDefinition { Kind = PassiveCommandKind.SetCounter, CounterKey = "resource.charge", Amount = 5, CounterCap = 6 });
            Rule("rule.seed", "Seed a bounded resource", PassiveEventKind.BattleStarted, "action.seed");
            document.Conditions.Add(new StudioConditionNode { Id = "condition.enter", Kind = EffectConditionKind.ResourceEnteredAtLeast, Threshold = 5, StringValue = "resource.charge" });
            document.Conditions.Add(new StudioConditionNode { Id = "condition.exit", Kind = EffectConditionKind.ResourceExitedBelow, Threshold = 3, StringValue = "resource.charge" });
            Command("action.enter", new PassiveCommandDefinition { Kind = PassiveCommandKind.IncrementCounter, CounterKey = "receipt.entry", Amount = 1 });
            Command("action.exit", new PassiveCommandDefinition { Kind = PassiveCommandKind.IncrementCounter, CounterKey = "receipt.exit", Amount = 1 });
            Rule("rule.enter", "Cross into a resource threshold", PassiveEventKind.ResourceChanged, "action.enter", "condition.enter", "resource.charge");
            Rule("rule.exit", "Cross out of a resource threshold", PassiveEventKind.ResourceChanged, "action.exit", "condition.exit", "resource.charge");
            var mark = new StatusRecipeDefinition { Id = "fixture.layered-mark", NameKey = "Synthetic layered mark", Polarity = StatusPolarity.Debuff,
                Stacking = StatusStackingPolicy.AddStacks, MaxStacks = 3, Identity = StatusIdentityScope.RecipeAndSource, DurationClock = StatusDurationClock.Permanent };
            Command("action.mark", new PassiveCommandDefinition { Kind = PassiveCommandKind.ExecuteEffect,
                Effect = new CardEffectOperationDefinition { Id = "operation.mark", Kind = CardEffectOperationKind.ApplyStatus,
                    Target = EffectTargetScope.TriggerTarget, Status = new StatusApplicationRecipe { InlineRecipe = mark, StackCount = 3 } } });
            Command("action.burst", new PassiveCommandDefinition { Kind = PassiveCommandKind.ExecuteEffect,
                ValueSource = PassiveValueSource.Binding, ValueBinding = "consumed.layers", Amount = 10000,
                EffectBinding = PassiveEffectBindingField.DamageCoefficientBp,
                Effect = new CardEffectOperationDefinition { Id = "operation.burst", Kind = CardEffectOperationKind.Damage,
                    Target = EffectTargetScope.TriggerTarget, Damage = new DamageEffectRecipe { Family = DamageFamily.Additional, Scaling = StatScaling.Attack } } });
            Command("action.blocked", new PassiveCommandDefinition { Kind = PassiveCommandKind.IncrementCounter, CounterKey = "receipt.blocked", Amount = 1 });
            document.Actions.Add(new StudioActionNode { Id = "action.consume", Name = "Absorb owned layers", Kind = StudioActionKind.ConsumeStatusLayers,
                StatusRecipeId = mark.Id, Amount = 3, StatusOwnerOnly = true, RemovalReason = StatusRemovalReason.Absorbed,
                ResultBinding = "consumed.layers", Children = new List<string> { "action.burst" } });
            document.Actions.Add(new StudioActionNode { Id = "action.guard", Name = "Atomic cost before rewards", Kind = StudioActionKind.SpendResource,
                ResourceKey = "resource.charge", AmountParameterId = "param.resource-cost", Amount = 3,
                Children = new List<string> { "action.mark", "action.consume" }, ElseChildren = new List<string> { "action.blocked" } });
            document.Modules.Add(new StudioModule { Id = "module.guard", Name = "Reusable resource and layers", RootActionId = "action.guard" });
            document.Actions.Add(new StudioActionNode { Id = "action.call", Name = "Call the reusable module", Kind = StudioActionKind.Module, ModuleId = "module.guard" });
            Rule("rule.guard", "Resource guard and layer absorption", PassiveEventKind.BeforeAction, "action.call");
            Command("action.removal", new PassiveCommandDefinition { Kind = PassiveCommandKind.IncrementCounter, CounterKey = "receipt.removed-layers",
                ValueSource = PassiveValueSource.RemovedStatusLayers, Amount = 1 });
            document.Conditions.Add(new StudioConditionNode { Id = "condition.owned-removal", Kind = EffectConditionKind.RemovedStatusFromEffectOwner });
            Rule("rule.removal", "Observe the immutable removal snapshot", PassiveEventKind.StatusRemoved, "action.removal", "condition.owned-removal", eventStatus: mark.Id);
        }

        public static object Run()
        {
            var passed = new List<string>();
            var failed = new List<string>();
            void Check(bool condition, string message) { (condition ? passed : failed).Add(message); }
            var scene = SceneManager.GetActiveScene(); var sceneDirty = scene.isDirty;
            var draft = CreateExampleDraft();
            var sourcePath = AssetDatabase.GetAssetPath(draft.SourceCharacter);
            string SourceHash() { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(sourcePath))); }
            var beforeSource = SourceHash();
            var assetPath = AssetDatabase.GetAssetPath(draft);
            StudioDraftRepository.TryRead(draft, out var document, out var library, out _);
            var compiled = StudioCompiler.Compile(document, library);
            Check(compiled.Success, "Synthetic flat action/module program compiles: " + string.Join("; ", compiled.Diagnostics));
            if (!compiled.Success) throw new InvalidOperationException(string.Join("; ", compiled.Diagnostics));
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            draft = AssetDatabase.LoadAssetAtPath<StudioDraftAsset>(assetPath); LastDraft = draft;
            StudioDraftRepository.TryRead(draft, out document, out library, out _);
            Check(StudioCompiler.Compile(document, library).Hashes.Bundle == compiled.Hashes.Bundle, "Schema2 actions, modules, conditions and bindings survive save/reimport.");
            var run = StudioSimulation.Run(document, library);
            var owner = run.Battle.Player.Fighters[0];
            Check(CharacterPassiveRuntime.Counter(owner, "resource.charge") == 2, "Real BeforeAction spends exactly three seeded charges.");
            Check(CharacterPassiveRuntime.Counter(owner, "receipt.entry") == 1 && CharacterPassiveRuntime.Counter(owner, "receipt.exit") == 1,
                "Resource entry and exit transitions fire once from committed previous/current values.");
            Check(CharacterPassiveRuntime.Counter(owner, "receipt.removed-layers") == 3, "Absorption removal reaction receives three owned layers.");
            Check(run.Battle.Opponent.Fighters[0].Statuses.Instances.All(s => s.RecipeId != "fixture.layered-mark"), "Fully absorbed mark is absent from resulting state.");
            Check(run.Trace.Any(t => t.ActionNodeId != null && t.Fired), "Production trace records executable action node identities.");
            var original = draft.DocumentJson;
            Undo.IncrementCurrentGroup();
            document.Parameters.Single(p => p.Id == "param.resource-cost").Value = 4;
            StudioDraftRepository.Edit(draft, document, library, "Change M3 bound resource cost"); Undo.FlushUndoRecordObjects();
            var changed = StudioSimulation.Run(document, library);
            Check(CharacterPassiveRuntime.Counter(changed.Battle.Player.Fighters[0], "resource.charge") == 1 &&
                StudioCompiler.Compile(document, library).Description.PlainText.Contains("costs 4"), "One bound cost changes actual resource use and description wording.");
            Undo.PerformUndo();
            Check(draft.DocumentJson == original, "Native Unity Undo restores the complete flat M3 program and bindings.");
            StudioDraftRepository.TryRead(draft, out document, out library, out _);
            var copy = StudioDraftRepository.Duplicate(draft);
            StudioDraftRepository.TryRead(copy, out var duplicate, out var duplicateLibrary, out _);
            Check(StudioCompiler.Compile(duplicate, duplicateLibrary).Success && !duplicate.Actions.Select(a => a.Id).Intersect(document.Actions.Select(a => a.Id)).Any(),
                "Duplication remaps actions/modules/references and retains external status/keyword identities.");
            var scenario = StudioScenarioAsset.Save(draft, run);
            Check(scenario.Replay(out var replay, out _) && StudioSimulation.StateHash(replay) == run.StateHash, "Saved M3 scenario replays exact compiled program, state and input.");
            var bundle = StudioDraftRepository.ApplyTestBundle(draft, compiled);
            Check(bundle.UseStudioDefinition && bundle.ResolveDefinition().Passive.Reactions.Any(r => r.ExecutionVersion == 1), "Reversible test bundle contains actual M3 reactions.");
            StudioDraftRepository.RevertTestBundle(bundle);
            Check(!bundle.UseStudioDefinition, "Test content returns to its frozen legacy source.");
            Check(beforeSource == SourceHash(), "Production character bytes remain unchanged; no case-study roster entry is created.");
            Check(SceneManager.GetActiveScene() == scene && scene.isDirty == sceneDirty && !EditorApplication.isPlaying,
                "Authoring and simulation preserve the active scene and remain outside Play Mode.");
            StudioDraftRepository.Save(draft);
            var report = new { Success = failed.Count == 0, SchemaVersion = document.SchemaVersion, DraftPath = assetPath,
                Passed = passed, Failed = failed, compiled.Hashes, StateHash = run.StateHash,
                Trace = run.Trace, CapturedUtc = DateTime.UtcNow.ToString("O") };
            var path = Path.GetFullPath("Artifacts/CharacterCreatorStudio/M3/editor-evidence.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonConvert.SerializeObject(report, Formatting.Indented));
            return report;
        }
    }
}
#endif
