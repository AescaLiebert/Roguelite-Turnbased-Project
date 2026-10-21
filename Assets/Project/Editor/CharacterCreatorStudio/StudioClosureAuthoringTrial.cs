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
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    /// <summary>Acceptance trial using public widgets, pointer events and normal edit callbacks.
    /// It reads saved documents to locate identities; it never constructs or edits their graph directly.</summary>
    public static class StudioClosureAuthoringTrial
    {
        private static CharacterCreatorStudioWindow window;
        private static readonly List<KeyValuePair<string, Action>> steps = new List<KeyValuePair<string, Action>>();
        private static readonly List<string> completed = new List<string>();
        private static readonly List<string> passed = new List<string>();
        private static int index;
        private static double next;
        private static string error, resource, status, seedRule, guardRule, guard, consume, queryRule, choose, reuse, variant, sourceHash, sourcePath, scenePath;
        private static bool sceneDirty;
        private static StudioScenarioAsset savedScenario;
        private static string beforeUndo;
        private static string module, thresholdRule;
        private static StudioDraftAsset previousDraft;
        private static bool pendingClick;
        public static object Status => new { Running = index < steps.Count && error == null, Step = index, Total = steps.Count,
            Current = index < steps.Count ? steps[index].Key : "Finished", Error = error, Assertions = passed.Count,
            Draft = window == null ? null : AssetDatabase.GetAssetPath(window.Draft) };
        private static StudioDocument Read() { StudioDraftRepository.TryRead(window.Draft, out var d, out _, out var e); if (e != null) throw new InvalidOperationException(e); return d; }
        private static void Step(string title, Action action) => steps.Add(new KeyValuePair<string, Action>(title, action));
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); passed.Add(message); }
        private static string Hash(string path) { using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))); }
        public static object Start()
        {
            EditorApplication.update -= Tick;
            window = CharacterCreatorStudioWindow.Open(); steps.Clear(); completed.Clear(); passed.Clear(); error = null; index = 0;
            sourcePath = AssetDatabase.GetAssetPath(window.Draft.SourceCharacter); sourceHash = Hash(sourcePath);
            scenePath = SceneManager.GetActiveScene().path; sceneDirty = SceneManager.GetActiveScene().isDirty;
            pendingClick = false;
            Step("Import a separate draft through the import button", () => { previousDraft = window.Draft; Click("Import opening gauge"); });
            Step("Open shared definitions", () => { Check(previousDraft != window.Draft, "Draft imported through real pointer events."); window.ShowWorkspace("resources"); });
            Step("Create shared charges", () => Click("+ Resource"));
            Step("Name shared charges", () => { Text("Name", "Closure charges"); Number("Maximum amount", 6); resource = Read().Resources.Last().Id; });
            Step("Create shared mark", () => Click("+ Status"));
            Step("Author status lifetime and ownership", () => { Text("Name", "Closure mark"); Number("Maximum layers", 3); Enum("Identity scope", StatusIdentityScope.RecipeAndSource); Enum("Duration clock", StatusDurationClock.Permanent); Enum("Break / source lifetime", StatusBreakRule.OnSourceDefeated); status = Read().Statuses.Last().Id; });
            Step("Create incoming cap", () => Click("+ Interception policy"));
            Step("Configure contact cap", () => { Text("Policy name", "49% contact cap"); Number("Max HP loss / contact (bp)", 4900); });
            Step("Create fatal save", () => Click("+ Interception policy"));
            Step("Choose fatal interception", () => Enum("Kind", CombatPolicyKind.BeforeDefeat));
            Step("Configure atomic fatal save", () => { Text("Policy name", "Spend charges to survive"); Popup("Resource", resource); Number("Atomic resource cost", 3); Number("Restore max HP (bp)", 5000); });
            Step("Open reusable modules", () => window.ShowWorkspace("modules"));
            Step("Create reusable module", () => Click("+ Module"));
            Step("Name reusable module", () => { Text("Module name", "Closure helper"); module = Read().Modules.Last().Id; });
            Step("Add reusable action", () => AddChild(Read().Modules.Last().RootActionId, StudioActionKind.Command));
            Step("Describe reusable action", () => Text("Action name", "Give owner a gauge point"));
            Step("Create seed rule", () => { Click("+ Add rule"); seedRule = Read().Rules.Last().Id; });
            Step("Name seed rule", () => Text("Rule name", "Seed closure charges"));
            Step("Add seed operation", () => { var root = Read().Rules.Last().ActionRootId; AddChild(root, StudioActionKind.Command); });
            Step("Choose counter command", () => Popup("Command", PassiveCommandKind.SetCounter.ToString()));
            Step("Choose shared charge resource", () => Popup("Resource", resource));
            Step("Set opening charge amount", () => Number("Amount", 5));
            Step("Create resource-transition rule", () => Click("+ Add rule"));
            Step("Choose resource event", () => { Text("Rule name", "Enter full charges"); Popup("Trigger", PassiveEventKind.ResourceChanged.ToString()); thresholdRule = Read().Rules.Last().Id; });
            Step("Choose observed resource", () => { Popup("Event resource", resource); Toggle("Allow reaction-origin facts", true); });
            Step("Select threshold condition", () => SelectCard(Read().Rules.Last().ConditionRootId));
            Step("Choose entry predicate", () => Popup("Condition", EffectConditionKind.ResourceEnteredAtLeast.ToString()));
            Step("Configure entry threshold", () => { Number("Threshold (count)", 5); Popup("Resource", resource); });
            Step("Add threshold reward", () => AddChild(Read().Rules.Last().ActionRootId, StudioActionKind.Command));
            Step("Create protection rule", () => { Click("+ Add rule"); guardRule = Read().Rules.Last().Id; });
            Step("Configure protection trigger", () => { Text("Rule name", "Pay, apply and absorb layers"); Popup("Trigger", PassiveEventKind.BeforeAction.ToString()); guardRule = Read().Rules.Last().Id; });
            Step("Limit source actor", () => { Enum("Actor relation", PassiveRelation.Self); Enum("Activation scope", PassiveLimitScope.RootActionTarget); Number("Priority (higher first)", 5); });
            Step("Add exact resource guard", () => { var root = Read().Rules.Last().ActionRootId; AddChild(root, StudioActionKind.SpendResource); });
            Step("Select cost resource", () => { guard = Read().Actions.Last().Id; Popup("Resource", resource); });
            Step("Set exact cost", () => Number("Amount", 2));
            Step("Add status application reward", () => AddChild(guard, StudioActionKind.Command));
            Step("Choose ApplyStatus operation", () => Popup("Operation", CardEffectOperationKind.ApplyStatus.ToString()));
            Step("Apply the shared mark", () => { Popup("Shared / standard status", status); Number("Layers", 3); });
            Step("Add layer absorption", () => AddChild(guard, StudioActionKind.ConsumeStatusLayers));
            Step("Configure owned absorption snapshot", () => { consume = Read().Actions.Last().Id; Popup("Status", status); Number("Layer count", 3); Enum("Removal reason", StatusRemovalReason.Absorbed); Text("Consumed count binding", "absorbed"); });
            Step("Add computed damage reward", () => AddChild(consume, StudioActionKind.Command));
            Step("Choose burst damage operation", () => Popup("Operation", CardEffectOperationKind.Damage.ToString()));
            Step("Choose bound count value", () => { Enum("Damage family", DamageFamily.Additional); Popup("Value basis", PassiveValueSource.Binding.ToString()); });
            Step("Bind burst to consumed layers", () => { Popup("Value binding", "absorbed"); Popup("Computed field", PassiveEffectBindingField.DamageCoefficientBp.ToString()); Number("Multiplier", 10000); });
            Step("Call shared helper after payment", () => AddChild(guard, StudioActionKind.Module));
            Step("Select helper definition", () => Popup("Module", module));
            Step("Create selector rule", () => { Click("+ Add rule"); queryRule = Read().Rules.Last().Id; });
            Step("Configure post-action trigger", () => { Text("Rule name", "Remember and reuse a target"); Popup("Trigger", PassiveEventKind.AfterAction.ToString()); });
            Step("Add target selection", () => AddChild(Read().Rules.Last().ActionRootId, StudioActionKind.ForEachTarget));
            Step("Name target set", () => { choose = Read().Actions.Last().Id; Number("Sample without replacement (0 = all)", 1); Text("Store target binding", "splash"); });
            Step("Add target reuse", () => AddChild(Read().Rules.Last().ActionRootId, StudioActionKind.ForEachTarget));
            Step("Select stored set", () => { reuse = Read().Actions.Last().Id; Popup("Reuse saved targets", "splash"); });
            Step("Select target producer", () => SelectCard(choose));
            Step("Introduce and locate binding error", () => { Text("Store target binding", "renamed splash"); Check(!window.Compilation.Success && window.Compilation.Diagnostics.Any(d => d.SourceId == reuse && d.Code == "binding.undefined-target"), "Renaming a producer identifies the exact broken target consumer."); });
            Step("Select target consumer", () => SelectCard(reuse));
            Step("Repair target reference", () => { Popup("Reuse saved targets", "renamed splash"); Check(window.Compilation.Success, "Target binding repaired through the scoped picker."); });
            Step("Open shared status", () => { window.ShowWorkspace("resources"); });
            Step("Select shared status", () => SelectCard(status));
            Step("Create status-local rule", () => Click("+ Status event rule"));
            Step("Add local recovery", () => AddChild(Read().Rules.Last().ActionRootId, StudioActionKind.Command));
            Step("Choose local Heal", () => Popup("Operation", CardEffectOperationKind.Heal.ToString()));
            Step("Set local recipient", () => { Enum("Operation target", EffectTargetScope.StatusOwner); Number("Heal coefficient (bp)", 1000); });
            Step("Open variant composer", () => window.ShowWorkspace("abilities"));
            Step("Create EX from source ranks", () => Click("+ Variant from base skill"));
            Step("Configure variant readiness", () => { Text("Variant name", "Closure EX"); Number("Minimum level", 2); Popup("Readiness resource", resource); });
            Step("Set one-use EX cost", () => { Number("Resource cost per use", 1); Toggle("Relock after successful use", true); variant = Read().AbilityVariants.Last().Definition.Id; });
            Step("Add alternate skill recovery step", () => {
                var rank = RankPanel(1); Enum("New effect", EffectKind.Heal, rank); Click("+ Sequence step", rank);
            });
            Step("Reorder alternate skill step", () => { var rank = RankPanel(1); var step = rank.Query<Foldout>().ToList().Last(f => f.text.StartsWith("Sequence ·")); Click("Move step earlier", step); });
            Step("Reject negative variant payload", () => { Number("Coefficient (bp)", -1, RankPanel(1)); Check(!window.Compilation.Success && window.Compilation.Diagnostics.Any(d => d.SourceId == variant), "Invalid EX damage points to its variant, not the whole document."); });
            Step("Repair variant coefficient", () => { Number("Coefficient (bp)", 20000, RankPanel(1)); Check(window.Compilation.Success, "Valid alternate sequence compiles after repair."); });
            Step("Author an independent level override", () => Click("+ Upgrade value override"));
            Step("Configure maximum-level branch", () => { Number("Minimum level", 50, window.rootVisualElement.Q<VisualElement>(Read().AbilityOverrides.Last().Definition.Id)); Number("Fixed override value", 30000, window.rootVisualElement.Q<VisualElement>(Read().AbilityOverrides.Last().Definition.Id)); });
            Step("Open bound parameters", () => window.ShowWorkspace("parameters"));
            Step("Edit one bound value in its own Undo group", () => {
                Undo.IncrementCurrentGroup(); beforeUndo = window.Draft.DocumentJson;
                Number("Value", 2); Undo.FlushUndoRecordObjects();
            });
            Step("Undo through the toolbar", () => Click("Undo"));
            Step("Redo through the toolbar", () => { Check(window.Draft.DocumentJson == beforeUndo, "Toolbar Undo restores the complete authored graph and references."); Click("Redo"); });
            Step("Restore the original bound value", () => { Check(Read().Parameters[0].Value == 2, "Toolbar Redo restores the bound edit."); Click("Undo"); });
            Step("Save authored document", () => { Check(window.Draft.DocumentJson == beforeUndo, "Undo restores the intended final parameter value before Save."); Click("Save draft"); Check(Read().SchemaVersion == 3, "Shared libraries and programs save as schema3."); });
            Step("Choose simulation context", () => { window.ShowWorkspace("scenario"); Number("Character level", 2); Number("Opening card rank (1–3)", 1); });
            Step("Run authored program", () => Click("Run current draft"));
            Step("Inspect production outcomes", () => {
                Check(window.LastRun != null, "UI-authored A–F document runs through the production engine.");
                Check(window.LastRun.Trace.Any(t => t.ActionNodeId == guard && t.ResourceBefore == 4 && t.ResourceAfter == 2), "EX cost precedes guarded payment and the trace records 4 → 2.");
                Check(window.LastRun.Trace.Any(t => t.ActionNodeId == consume && t.Values.ContainsKey("absorbed") && t.Values["absorbed"] == 3), "Absorption trace retains the three-layer bound value.");
                Check(window.LastRun.Battle.Events.Any(e => e.Kind == BattleEventKind.CardPlayed && e.Card?.VariantId == variant), "Committed card preserves the UI-authored EX choice.");
                Check(window.LastRun.Trace.Any(t => t.RuleId == thresholdRule && t.Fired), "Resource entry predicate fires from the authored threshold transition.");
            });
            Step("Save exact scenario", () => Click("Save this scenario and frozen input"));
            Step("Replay saved input", () => {
                savedScenario = AssetDatabase.LoadAssetAtPath<StudioScenarioAsset>(AssetDatabase.FindAssets("t:StudioScenarioAsset", new[] { StudioDraftRepository.Root + "/Drafts" })
                    .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.Contains(window.Draft.name + "Scenario")).OrderByDescending(File.GetLastWriteTimeUtc).First());
                Check(savedScenario.Replay(out var state, out _) && StudioSimulation.StateHash(state) == window.LastRun.StateHash, "Saved UI-authored input replays the exact engine state.");
            });
            Step("Choose incoming cap acceptance", () => Enum("Acceptance scenario", StudioScenarioPreset.IncomingCap));
            Step("Run incoming cap", () => Click("Run current draft"));
            Step("Verify authored cap outcome", () => {
                var hp = window.LastRun.OpeningBattle.Player.Fighters[0].Health;
                Check(window.LastRun.Battle.Events.First(e => e.Kind == BattleEventKind.DamageApplied && e.TargetId == window.LastRun.OpeningBattle.Player.Fighters[0].Id).HealthLost == hp * 4900 / 10000,
                    "UI-authored incoming cap limits the contact to 49% Max HP before ordinary end-turn recovery.");
            });
            Step("Choose fatal-save acceptance", () => Enum("Acceptance scenario", StudioScenarioPreset.FatalSave));
            Step("Run fatal-save", () => Click("Run current draft"));
            Step("Verify fatal-save outcome", () => {
                Check(window.LastRun.Battle.Player.Fighters[0].IsAlive && CharacterPassiveRuntime.Counter(window.LastRun.Battle.Player.Fighters[0], resource) == 2,
                    "UI-authored fatal policy saves the fighter and spends exactly three charges.");
            });
            Step("Save and replay fatal input", () => { Click("Save this scenario and frozen input"); });
            Step("Replay the actual enemy command", () => {
                var scenarios = AssetDatabase.FindAssets("t:StudioScenarioAsset", new[] { StudioDraftRepository.Root + "/Drafts" }).Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => p.Contains(window.Draft.name + "Scenario")).OrderByDescending(File.GetLastWriteTimeUtc).ToArray();
                var saved = AssetDatabase.LoadAssetAtPath<StudioScenarioAsset>(scenarios[0]);
                Check(saved.Replay(out var state, out _) && StudioSimulation.StateHash(state) == window.LastRun.StateHash, "Fatal scenario saves and reproduces the actual opponent command.");
            });
            Step("Reopen saved authoring document", () => {
                var draft = window.Draft; window.Close(); window = CharacterCreatorStudioWindow.Open();
                Check(window.Draft == draft && window.Compilation.Success, "Window reopen restores the saved document and valid compilation.");
            });
            Step("Verify scene and production source isolation", () => {
                Check(Hash(sourcePath) == sourceHash, "Production character bytes are unchanged.");
                Check(SceneManager.GetActiveScene().path == scenePath && SceneManager.GetActiveScene().isDirty == sceneDirty && !EditorApplication.isPlaying, "Authoring preserves the scene and never enters Play Mode.");
                window.ShowWorkspace("rules");
            });
            next = EditorApplication.timeSinceStartup + .25; EditorApplication.update += Tick; return Status;
        }
        private static void Tick()
        {
            if (pendingClick || EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling) return;
            if (index >= steps.Count) { EditorApplication.update -= Tick; Write(); return; }
            try { steps[index].Value(); completed.Add(steps[index].Key); index++; window.Repaint(); }
            catch (Exception exception) { error = steps[index].Key + ": " + exception.Message; EditorApplication.update -= Tick; Write(); return; }
            next = EditorApplication.timeSinceStartup + .22;
        }
        private static void Write()
        {
            Directory.CreateDirectory("Artifacts/CharacterCreatorStudio/M3Closure");
            File.WriteAllText("Artifacts/CharacterCreatorStudio/M3Closure/ui-author-trial.json", JsonConvert.SerializeObject(new {
                Success = error == null && index == steps.Count, Error = error, CompletedSteps = completed, Passed = passed,
                Draft = window == null ? null : AssetDatabase.GetAssetPath(window.Draft), Scenario = AssetDatabase.GetAssetPath(savedScenario),
                Interaction = "Actual UI Toolkit fields and PointerDown/PointerUp events; graph readback only, no direct graph authoring.",
                RuntimeAssemblyHash = Hash(typeof(BattleEngine).Assembly.Location), EditorAssemblyHash = Hash(typeof(CharacterCreatorStudioWindow).Assembly.Location),
                SavedRevision = window == null ? null : window.Draft.SavedRevision, Bundle = window?.Compilation?.Hashes?.Bundle,
                CapturedUtc = DateTime.UtcNow.ToString("O") }, Formatting.Indented));
        }
        private static void Reveal(VisualElement element)
        {
            for (var p = element.parent; p != null; p = p.parent) if (p is Foldout fold) fold.value = true;
            for (var p = element.parent; p != null; p = p.parent) if (p is ScrollView scroll) { scroll.ScrollTo(element); break; }
        }
        private static void Click(string text, VisualElement root = null)
        {
            var button = (root ?? window.rootVisualElement).Query<Button>().ToList().First(b => b.text == text);
            Click(button);
        }
        private static void Click(Button button)
        {
            Reveal(button); if (!button.enabledInHierarchy) throw new InvalidOperationException("Button is disabled: " + button.text);
            // Scroll/expanded groups need one layout before a pointer can hit the visible widget.
            pendingClick = true;
            button.schedule.Execute(() => {
                var acknowledged = false; Action observer = () => acknowledged = true; button.clicked += observer;
                button.Focus(); var position = button.worldBound.center;
                using (var e = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = position })) button.SendEvent(e);
                using (var e = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = position })) button.SendEvent(e);
                button.clicked -= observer; pendingClick = false;
                if (!acknowledged) { error = "Pointer did not activate visible button: " + button.text; EditorApplication.update -= Tick; Write(); }
            }).StartingIn(60);
        }
        private static void Text(string label, string value, VisualElement root = null)
        { var f = (root ?? window.rootVisualElement).Query<TextField>().ToList().First(x => x.label == label); Reveal(f); f.value = value; }
        private static void Number(string label, int value, VisualElement root = null)
        { var f = (root ?? window.rootVisualElement).Query<IntegerField>().ToList().First(x => x.label == label); Reveal(f); f.value = value; }
        private static void Enum(string label, System.Enum value, VisualElement root = null)
        { var f = (root ?? window.rootVisualElement).Query<EnumField>().ToList().First(x => x.label == label); Reveal(f); f.value = value; }
        private static void Toggle(string label, bool value)
        { var f = window.rootVisualElement.Query<Toggle>().ToList().First(x => x.label == label); Reveal(f); f.value = value; }
        private static void Popup(string label, string value)
        { var f = window.rootVisualElement.Query<PopupField<string>>().ToList().First(x => x.label == label); Reveal(f); f.value = value; }
        private static void SelectCard(string id) => Click(window.rootVisualElement.Q<Button>("card." + id));
        private static void AddChild(string id, StudioActionKind kind)
        {
            var card = window.rootVisualElement.Q<Button>("card." + id);
            var body = card.parent.ElementAt(card.parent.IndexOf(card) + 1);
            var addKind = body.Children().Last().Q<EnumField>(); Reveal(addKind); addKind.value = kind; Click("+ Add step", body.Children().Last());
        }
        private static Foldout RankPanel(int rank)
        { var fold = window.rootVisualElement.Query<Foldout>().ToList().First(f => f.text == "Variant card rank " + rank); fold.value = true; return fold; }
    }
}
#endif
