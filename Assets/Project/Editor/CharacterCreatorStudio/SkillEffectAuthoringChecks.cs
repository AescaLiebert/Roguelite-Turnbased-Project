#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using FightingAllstar.Presentation.Content;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public static class SkillEffectAuthoringChecks
    {
        public static object Run()
        {
            var checks = new List<string>();
            void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks.Add(message); }
            var source = CharacterObjectRegistrySO.LoadAll().Single(c => c.DefinitionId == "fighter.pre-release.billy97");
            var original = new UnityEngine.Object[] { source, source.Skill1, source.Skill2, source.Ultimate, source.PassiveDefinition }
                .ToDictionary(AssetDatabase.GetAssetPath, o => File.ReadAllBytes(AssetDatabase.GetAssetPath(o)));
            var window = CharacterCreatorStudioWindow.Open(); var previous = window.Draft;
            var rankField = typeof(CharacterCreatorStudioWindow).GetField("_kitRank", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var previousRank = (int)rankField.GetValue(window);
            var publisher = StudioProductionBridge.OpenSource(source); StudioDraftAsset session = null;
            var id = "fighter.skill-effect-check-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            try
            {
                var numeric = 940001; while (CharacterObjectRegistrySO.LoadAll().Any(c => c.ID == numeric)) numeric++;
                var character = StudioProductionBridge.Publish(publisher, StudioProductionBridge.Review(publisher, id, numeric, "Skill effect authoring verification"));
                session = StudioProductionBridge.OpenSource(character); window.LoadDraft(session); rankField.SetValue(window, 1); window.ShowWorkspace("skill2");
                var reactions = window.rootVisualElement.Query<VisualElement>().ToList().FirstOrDefault(v => v.name.EndsWith(".child.status.stance.apply.reactions"));
                Check(reactions != null, "Child status exposes its own WHEN / WHO / IF / DO.");
                Check(reactions.Query<Image>().ToList().Any(i => i.sprite != null), "Owning child-status condition displays its status icon.");
                Check(!window.rootVisualElement.Query<Label>().ToList().Any(l => l.text != null && l.text.Contains("Owned by this skill rank")), "Removed the misleading header description.");
                var duration = window.rootVisualElement.Query<IntegerField>().ToList().First(f => f.label == "Turns (0 = status default)");
                duration.value = 4;
                Check(window.Compilation.Success && window.Compilation.Character.Skills[1].Ranks[0].Effect.StatusDurationOverride == 4, "Actual skill duration widget updates canonical rank data.");
                var add = reactions.Query<Button>().ToList().First(b => b.text == "+ Reaction while active");
                using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = add; add.SendEvent(evt); }
                // Work on the authored graph after the widget transaction; no second data copy owns its conditions.
                var doc = SnapshotJson.Deserialize<StudioDocument>(session.DocumentJson);
                var recipe = StudioDocumentService.BeginKitEditing(doc).Skills[1].Ranks[0].Effect.StatusRecipe.StanceChildren.Single(c => c.Id == "status.stance.apply");
                var rule = recipe.Reactions.Last();
                Check(rule.ExecutionVersion == 1 && recipe.Reactions.Count == 2, "Add-reaction widget creates a status-owned runtime rule.");
                rule.Conditions.Add(new EffectConditionDefinition { Logic = ConditionLogic.Any, Children = new List<EffectConditionDefinition> {
                    new EffectConditionDefinition { Kind = EffectConditionKind.TargetTraitIs, Subject = ConditionSubject.Actor, StringValue = "trait.women" },
                    new EffectConditionDefinition { Logic = ConditionLogic.Not, Children = new List<EffectConditionDefinition> {
                        new EffectConditionDefinition { Kind = EffectConditionKind.TargetHasBuff, Subject = ConditionSubject.Actor } } } } });
                rule.Commands.Add(new PassiveCommandDefinition { Kind = PassiveCommandKind.ExecuteEffect, Effect = new CardEffectOperationDefinition {
                    Id = rule.Id + ".ignite", Kind = CardEffectOperationKind.ApplyStatus, Target = EffectTargetScope.TriggerActor,
                    Status = new StatusApplicationRecipe { InlineRecipe = StandardEffectDatabase.CreateStatusRecipes().Find(s => s.Id == "status.debuff.ignite").Clone(), DurationOverride = 2 } } });
                rule.Conditions[0].Children.Add(new EffectConditionDefinition { Kind = EffectConditionKind.TargetHasRecipe, Subject = ConditionSubject.Owner, RecipeId = "status.stance.apply" });
                session.DocumentJson = SnapshotJson.Serialize(doc);
                window.LoadDraft(session); window.ShowWorkspace("skill2");
                Check(window.rootVisualElement.Query<Image>("studio-condition-status-icon").ToList().Any(i => i.sprite != null),
                    "Status predicate picker renders the referenced status icon.");
                StudioProductionBridge.SaveSource(session);
                var saved = CharacterObjectCatalogBuilder.CreateDefinition(character);
                Check(saved.Skills[1].Ranks[0].Effect.StatusRecipe.StanceChildren.Single(c => c.Id == "status.stance.apply").Reactions.Last().Conditions[0].Children[1].Logic == ConditionLogic.Not,
                    "Native save retains nested Any / Not conditions through canonical JSON.");
                Check(saved.Passive.Reactions.All(r => r.Id != rule.Id), "Granted rules never compile into PassiveSO.");
                Undo.PerformUndo();
                Check(CharacterObjectCatalogBuilder.CreateDefinition(character).Skills[1].Ranks[0].Effect.StatusRecipe.StanceChildren.Single(c => c.Id == "status.stance.apply").Reactions.Count == 1, "Native Undo removes the newly authored reaction.");
                Undo.PerformRedo();
                Check(CharacterObjectCatalogBuilder.CreateDefinition(character).Skills[1].Ranks[0].Effect.StatusRecipe.StanceChildren.Single(c => c.Id == "status.stance.apply").Reactions.Count == 2, "Native Redo restores the grouped granted program.");
                var reopened = StudioProductionBridge.OpenSource(character);
                try { StudioProductionBridge.SaveSource(reopened); }
                finally { UnityEngine.Object.DestroyImmediate(reopened); }
                Check(SnapshotJson.Serialize(saved) == SnapshotJson.Serialize(CharacterObjectCatalogBuilder.CreateDefinition(character)), "Save / reopen / repeat-save does not duplicate or relocate granted rules.");
                var beforeFailure = new UnityEngine.Object[] { character, character.Skill1, character.Skill2, character.Ultimate, character.PassiveDefinition }
                    .ToDictionary(AssetDatabase.GetAssetPath, o => File.ReadAllBytes(AssetDatabase.GetAssetPath(o)));
                var failureSession = StudioProductionBridge.OpenSource(character);
                var failureDoc = SnapshotJson.Deserialize<StudioDocument>(failureSession.DocumentJson);
                StudioDocumentService.BeginKitEditing(failureDoc).Skills[1].Ranks[0].Description += " rollback fixture";
                failureSession.DocumentJson = SnapshotJson.Serialize(failureDoc);
                var injected = false;
                StudioProductionBridge.VerificationCheckpoint = _ => { injected = true; throw new InvalidOperationException("Injected skill-effect save failure"); };
                var rejected = false;
                try { StudioProductionBridge.SaveSource(failureSession); } catch (InvalidOperationException) { rejected = true; }
                finally { StudioProductionBridge.VerificationCheckpoint = null; UnityEngine.Object.DestroyImmediate(failureSession); }
                Check(rejected && injected && beforeFailure.All(p => File.ReadAllBytes(p.Key).SequenceEqual(p.Value)), "Failed native save leaves all five assets intact.");
            }
            finally
            {
                StudioProductionBridge.VerificationCheckpoint = null;
                var folder = StudioProductionBridge.RuntimeRoot + "/" + id.Substring(8);
                if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
                FightingAllstar.EditorTools.CharacterRegistrySync.Sync();
                if (session != null) UnityEngine.Object.DestroyImmediate(session);
                UnityEngine.Object.DestroyImmediate(publisher);
                rankField.SetValue(window, previousRank);
                if (previous != null) window.LoadDraft(previous);
            }
            Check(original.All(p => File.ReadAllBytes(p.Key).SequenceEqual(p.Value)), "Verification preserves all five original Billy assets.");
            var evidence = new { Success = true, Checks = checks };
            File.WriteAllText("Artifacts/SkillEffectOwnership/native-authoring-evidence.json", SnapshotJson.Serialize(evidence));
            return evidence;
        }
    }
}
#endif
