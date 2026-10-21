#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using FightingAllstar.Presentation.Content;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public static class StudioKitAuthoringChecks
    {
        public static object Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start from stable Edit Mode.");
            var checks = new List<string>(); void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks.Add(message); }
            var joe = CharacterObjectRegistrySO.LoadAll().Single(c => c.DefinitionId == "fighter.joe96");
            var original = new UnityEngine.Object[] { joe, joe.Skill1, joe.Skill2, joe.Ultimate, joe.PassiveDefinition }.ToDictionary(AssetDatabase.GetAssetPath, o => File.ReadAllBytes(AssetDatabase.GetAssetPath(o)));
            var scene = SceneManager.GetActiveScene(); var dirty = scene.isDirty;
            var draftAssets = AssetDatabase.FindAssets("t:StudioDraftAsset").Length;
            var w = CharacterCreatorStudioWindow.Open(); var previous = w.Draft;
            var publisher = StudioProductionBridge.OpenSource(joe); StudioDraftAsset editing = null;
            var id = "fighter.kit-check-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var numeric = 930001; while (CharacterObjectRegistrySO.LoadAll().Any(c => c.ID == numeric)) numeric++;
            try
            {
                var plan = StudioProductionBridge.Review(publisher, id, numeric, "Joe96 authoring verification");
                var character = StudioProductionBridge.Publish(publisher, plan);
                editing = StudioProductionBridge.OpenSource(character); w.LoadDraft(editing); w.ShowWorkspace("overview");
                Check(editing.IsDirectSession && AssetDatabase.GetAssetPath(editing) == "", "Normal character editing uses an in-memory buffer, not a draft asset.");
                Check(new[] { "identity", "skill1", "skill2", "ultimate", "passive" }.All(p => w.rootVisualElement.Q<Button>("studio-nav-" + p) != null), "Kit navigator exposes identity/stats, both skills, ultimate and passive.");
                Click(w, "studio-nav-identity");
                w.rootVisualElement.Q<IntegerField>("kit.stats.Attack").value = 267;
                Check(w.Compilation.Character.BaseStats.Attack == 267, "Identity/stat widget changes the compiled native kit.");
                Click(w, "studio-nav-skill1"); Click(w, "studio-kit-rank-1"); Field<FloatField>(w.rootVisualElement.Q<VisualElement>("studio-kit-primary"), "Damage (%)").value = 135;
                Click(w, "studio-kit-rank-2");
                Check(Field<FloatField>(w.rootVisualElement.Q<VisualElement>("studio-kit-primary"), "Damage (%)").value == 120, "Rank navigation preserves independent rank effects.");
                Click(w, "studio-nav-skill2"); Click(w, "studio-kit-rank-3");
                Field<FloatField>(w.rootVisualElement.Q<VisualElement>("studio-kit-primary"), "Damage (%)").value = 455;
                Click(w, "studio-nav-ultimate"); Click(w, "studio-kit-tier-5");
                Field<FloatField>(w.rootVisualElement.Q<VisualElement>("studio-kit-primary"), "Damage (%)").value = 660;
                Click(w, "studio-nav-passive");
                Click(w, "passive.open." + w.Compilation.Character.Passive.Auras[0].Id);
                var aura = w.rootVisualElement.Q<VisualElement>("studio-kit-aura");
                Field<IntegerField>(aura, "Amount (bp for ratios)").value = 6500;
                Field<EnumField>(aura, "Stat").value = StatId.Defense;
                Check(w.Compilation.Character.Passive.Auras[0].Modifiers[0].ResolvedStat == StatId.Defense && w.Compilation.Character.Passive.Auras[0].Modifiers[0].Amount == 6500, "Passive widgets edit effective imported modifier mirrors.");
                w.rootVisualElement.Q<TextField>("studio-description-source").value = "Blue allies gain 65% Defense.";
                Check(w.Compilation.Character.PassiveSource.Wording.Text.Contains("65%"), "Dedicated passive wording is part of the native definition.");
                var guids = plan.Paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
                Click(w, "studio-save");
                var native = CharacterObjectCatalogBuilder.CreateDefinition(character);
                Check(native.BaseStats.Attack == 267 && native.Skills[0].Ranks[0].Effect.CoefficientBp == 13500 && native.Skills[1].Ranks[2].Effect.CoefficientBp == 45500 && native.UltimateTiers[5].Effect.CoefficientBp == 66000 && native.Passive.Auras[0].Modifiers[0].Amount == 6500, "Save character writes all five native SOs through actual UI callbacks.");
                Check(plan.Paths.Select(AssetDatabase.AssetPathToGUID).SequenceEqual(guids), "Direct save preserves the five native GUIDs.");
                var reopened = StudioProductionBridge.OpenSource(character);
                Check(StudioDraftRepository.TryRead(reopened, out var doc, out var library, out _) && StudioCompiler.Compile(doc, library).Character.BaseStats.Attack == 267, "A new editing session reconstructs saved kit data from native assets.");
                var embedded = SnapshotJson.Deserialize<StudioDocument>(character.StudioAuthoringJson);
                Check(embedded.LegacyCharacter == null && embedded.AuthoredCharacter == null, "Embedded authoring metadata stores no duplicate character snapshots.");
                const string ruleId = "rule.kit-save-check";
                doc.Conditions.Add(new StudioConditionNode { Id = "condition.kit-save", Kind = EffectConditionKind.Always });
                doc.Actions.Add(new StudioActionNode { Id = "action.kit-save", Kind = StudioActionKind.Command, Command = new PassiveCommandDefinition { Kind = PassiveCommandKind.ChangePowerGauge, Amount = 1 } });
                doc.Rules.Add(new StudioRule { Id = ruleId, Name = "Save check", ConditionRootId = "condition.kit-save", ActionRootId = "action.kit-save", Trigger = PassiveEventKind.BattleStarted });
                StudioDraftRepository.Edit(reopened, doc, library, "Add verification rule");
                StudioProductionBridge.SaveSource(reopened); StudioProductionBridge.SaveSource(reopened);
                Check(character.PassiveDefinition.CreateDefinition().Reactions.Count(r => r.Id == ruleId) == 1, "Save/reopen/save never duplicates embedded Studio rules.");
                var priorBytes = plan.Paths.ToDictionary(p => p, File.ReadAllBytes);
                StudioDraftRepository.TryRead(reopened, out doc, out library, out _); StudioDocumentService.BeginKitEditing(doc).BaseStats.Attack = 268;
                StudioDraftRepository.Edit(reopened, doc, library, "Failure fixture");
                StudioProductionBridge.VerificationCheckpoint = p => { throw new InvalidOperationException("Injected direct-save failure"); };
                var rejected = false; try { StudioProductionBridge.SaveSource(reopened); } catch (InvalidOperationException) { rejected = true; }
                finally { StudioProductionBridge.VerificationCheckpoint = null; }
                Check(rejected && priorBytes.All(p => File.ReadAllBytes(p.Key).SequenceEqual(p.Value)), "Partial direct-save failure restores the original five files.");
                StudioProductionBridge.SaveSource(reopened);
                Check(CharacterObjectCatalogBuilder.CreateDefinition(character).BaseStats.Attack == 268, "A failed save can be retried without stale native state.");
                Undo.PerformUndo();
                Check(CharacterObjectCatalogBuilder.CreateDefinition(character).BaseStats.Attack == 267, "Native save participates in Unity Undo.");
                Undo.PerformRedo();
                Check(CharacterObjectCatalogBuilder.CreateDefinition(character).BaseStats.Attack == 268, "Native save participates in Unity Redo.");
                UnityEngine.Object.DestroyImmediate(reopened);
                Check(AssetDatabase.FindAssets("t:StudioDraftAsset").Length == draftAssets, "Opening, authoring and saving creates no StudioDraft asset.");
            }
            finally
            {
                StudioProductionBridge.VerificationCheckpoint = null;
                // This disposable verification package may have direct edits; discard only its exact five assets.
                var folder = StudioProductionBridge.RuntimeRoot + "/" + id.Substring(8);
                if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
                FightingAllstar.EditorTools.CharacterRegistrySync.Sync();
                if (editing != null) UnityEngine.Object.DestroyImmediate(editing);
                UnityEngine.Object.DestroyImmediate(publisher);
                if (previous != null) w.LoadDraft(previous); else w.LoadDraft(StudioProductionBridge.OpenSource(joe));
                w.ShowWorkspace("overview");
            }
            Check(original.All(p => File.ReadAllBytes(p.Key).SequenceEqual(p.Value)), "The original Joe96 assets remain unchanged by disposable verification.");
            Check(SceneManager.GetActiveScene().path == scene.path && SceneManager.GetActiveScene().isDirty == dirty && !EditorApplication.isPlaying, "Editor scene and Play Mode remain unchanged.");
            var evidence = new { Success = true, Checks = checks, DraftAssetsCreated = AssetDatabase.FindAssets("t:StudioDraftAsset").Length - draftAssets, CapturedUtc = DateTime.UtcNow };
            Directory.CreateDirectory("Artifacts/CharacterCreatorStudio/KitAuthoring"); File.WriteAllText("Artifacts/CharacterCreatorStudio/KitAuthoring/native-evidence.json", JsonConvert.SerializeObject(evidence, Formatting.Indented));
            return evidence;
        }
        private static T Field<T>(VisualElement root, string label) where T : VisualElement
            => root.Query<T>().ToList().Single(f => (string)f.GetType().GetProperty("label").GetValue(f) == label);
        private static void Click(CharacterCreatorStudioWindow window, string name)
        { var button = window.rootVisualElement.Q<Button>(name); if (button == null) throw new InvalidOperationException("Missing " + name); using (var e = NavigationSubmitEvent.GetPooled()) { e.target = button; button.SendEvent(e); } }
    }
}
#endif
