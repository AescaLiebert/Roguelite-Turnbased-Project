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
    public static class StudioPassiveCentralizationChecks
    {
        public static object RunRosterParity()
        {
            var rows = new List<object>(); var failures = new List<string>(); var imports = 0; var preserved = 0;
            foreach (var character in CharacterObjectRegistrySO.LoadAll())
            {
                var session = StudioProductionBridge.OpenSource(character);
                try
                {
                    StudioDraftRepository.TryRead(session, out var doc, out var library, out _);
                    var compiled = StudioCompiler.Compile(doc, library);
                    if (!compiled.Success) throw new InvalidOperationException(character.DefinitionId + ": " + string.Join("; ", compiled.Diagnostics));
                    imports += doc.Rules.Count(r => r.Imported); preserved += doc.CompatibilityReasons.Count;
                    foreach (var seed in new ulong[] { 12345, 98765 })
                    foreach (var config in new[] { 0, 1, 2 })
                    {
                        var tier = config == 1 ? 5 : 0; var rank = config == 1 ? 3 : 1; var reserve = config == 2;
                        var before = StudioSimulation.CreateFixture(doc.LegacyCharacter, seed, false, reserve, tier: tier, rank: rank);
                        var after = StudioSimulation.CreateFixture(compiled.Character, seed, false, reserve, tier: tier, rank: rank);
                        if (!FightingAllstar.Core.Combat.BattleEngine.TryResolvePlan(before, StudioSimulation.CreateInputPlan(before), out var nativeResult, out var nativeError) ||
                            !FightingAllstar.Core.Combat.BattleEngine.TryResolvePlan(after, StudioSimulation.CreateInputPlan(after), out var authoredResult, out var authoredError))
                            throw new InvalidOperationException("Parity fixture plan was rejected for " + character.DefinitionId);
                        var parity = StudioSimulation.StateHash(nativeResult) == StudioSimulation.StateHash(authoredResult);
                        rows.Add(new { Id = character.DefinitionId, Seed = seed, Tier = tier, Rank = rank, Reserve = reserve, Parity = parity });
                        if (!parity) failures.Add(character.DefinitionId + "/" + seed + "/" + config);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(session); }
            }
            var evidence = new { Success = failures.Count == 0, ImportedReactions = imports, CompatibilityReactions = preserved, Cases = rows.Count, Failures = failures, Rows = rows };
            Directory.CreateDirectory("Artifacts/CharacterCreatorStudio/PassiveCentralization");
            File.WriteAllText("Artifacts/CharacterCreatorStudio/PassiveCentralization/roster-parity.json", JsonConvert.SerializeObject(evidence, Formatting.Indented));
            if (failures.Count > 0) throw new InvalidOperationException("Import changed native outcomes: " + string.Join(", ", failures));
            return new { evidence.Success, evidence.ImportedReactions, evidence.CompatibilityReactions, evidence.Cases };
        }

        public static object Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stable Edit Mode is required.");
            var checks = new List<string>();
            void Check(bool ok, string text) { if (!ok) throw new InvalidOperationException(text); checks.Add(text); }
            var originals = CharacterObjectRegistrySO.LoadAll().SelectMany(c => new UnityEngine.Object[] { c, c.Skill1, c.Skill2, c.Ultimate, c.PassiveDefinition })
                .Where(o => o != null).Select(AssetDatabase.GetAssetPath).Distinct().ToDictionary(p => p, File.ReadAllBytes);
            var scene = SceneManager.GetActiveScene(); var sceneDirty = scene.isDirty;
            var preferenceKey = "FightingAllstar.CharacterStudio." + StudioDraftRepository.Revision(Application.dataPath, "");
            var prefs = new[] { ".character", ".draft", ".page", ".locale" }.ToDictionary(s => preferenceKey + s, s => EditorPrefs.GetString(preferenceKey + s, ""));
            var publisher = StudioProductionBridge.OpenSource(CharacterObjectRegistrySO.LoadAll().Single(c => c.DefinitionId == "fighter.kaeya"));
            var id = "fighter.passive-check-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var number = 940001; while (CharacterObjectRegistrySO.LoadAll().Any(c => c.ID == number)) number++;
            StudioDraftAsset session = null; CharacterCreatorStudioWindow window = null;
            try
            {
                StudioDraftRepository.TryRead(publisher, out var doc, out var library, out _);
                var native = StudioDocumentService.BeginKitEditing(doc);
                native.Skills[0].Variants.Add(new AbilityVariantDefinition { Id = doc.Rules[0].Id, Name = "Unrelated native variant", InitiallyUnlocked = true,
                    Ranks = native.Skills[0].Ranks.Select(r => r.Clone()).ToList() });
                native.Passive.Reactions.Add(new PassiveReactionDefinition { Id = "native.check.opening", Trigger = PassiveEventKind.BattleStarted,
                    Commands = { new PassiveCommandDefinition { Kind = PassiveCommandKind.ChangePowerGauge, Amount = 1 } } });
                native.Passive.Auras.Add(new PassiveAuraDefinition { Id = "native.check.aura", Modifiers = { new StatModifierDefinition { Amount = 100 } } });
                StudioDraftRepository.Edit(publisher, doc, library, "Prepare isolated native fixture");
                var plan = StudioProductionBridge.Review(publisher, id, number, "Passive centralization verification");
                var character = StudioProductionBridge.Publish(publisher, plan);
                session = StudioProductionBridge.OpenSource(character);
                window = ScriptableObject.CreateInstance<CharacterCreatorStudioWindow>(); window.ShowUtility(); window.CreateGUI(); window.LoadDraft(session); window.ShowWorkspace("passive");
                Check(window.rootVisualElement.panel != null, "Widget checks use an attached Unity panel.");
                var root = window.rootVisualElement;
                Click(window, "studio-passive-preview-collapse");
                Check(root.Q<Button>("studio-passive-preview-collapse").text == "+", "Preview close button collapses the preview and leaves an expand control.");
                Click(window, "studio-passive-preview-collapse");
                Check(root.Q<Button>("studio-passive-preview-collapse").text == "×", "Preview can be reopened without navigating away.");
                Check(root.Q<VisualElement>("studio-passive-inventory") != null && root.Q<Foldout>("passive.open.native.check.opening") != null && root.Q<Foldout>("passive.open.native.check.aura") != null,
                    "Overview includes imported event and continuous mechanics in one inventory.");
                Check(root.Q<Button>("studio-passive-remove-support") == null, "Compatibility controls appear only for the selected mechanic.");
                root.Q<Foldout>("passive.open.native.check.opening").value = true; Click(window, "passive.edit.native.check.opening");
                Check(!Field<IntegerField>(root, "Cooldown (owner turns)").enabledInHierarchy, "Imported version-zero frequency fields are disabled with an explanation.");
                Click(window, "studio-rules-passive-overview"); root.Q<Foldout>("passive.open.studio.opening-team-gauge").value = true; Click(window, "passive.edit.studio.opening-team-gauge");
                Check(root.Q<Button>("card.studio.opening-team-gauge.when") != root.Q<Button>("card.studio.opening-team-gauge.who"), "WHEN and WHO have unique selection identities.");
                Field<PopupField<string>>(root, "Trigger").value = "BeforeAction";
                Check(root.Q<Label>("summary.studio.opening-team-gauge.when").text.Contains("Before Action"), "WHEN refreshes immediately when the trigger changes.");
                Field<PopupField<string>>(root, "Trigger").value = "BattleStarted";
                Click(window, "card.studio.opening-team-gauge.who");
                Check(root.Query<PopupField<string>>().ToList().All(f => f.label != "Trigger"), "WHO exposes event filters in a distinct contextual inspector.");
                Field<EnumField>(root, "Actor relation").value = PassiveRelation.Enemies;
                Field<EnumField>(root, "Event target relation").value = PassiveRelation.Self;
                Field<EnumField>(root, "Activation scope").value = PassiveLimitScope.Battle;
                Field<IntegerField>(root, "Cooldown (owner turns)").value = 3;
                var reaction = window.Compilation.Character.Passive.Reactions.Single(r => r.Id == "studio.opening-team-gauge");
                Check(reaction.ActorRelation == PassiveRelation.Enemies && reaction.TargetRelation == PassiveRelation.Self && reaction.LimitScope == PassiveLimitScope.Battle && reaction.CooldownOwnerTurns == 3,
                    "Actual widgets change the executable opening-rule filters and frequency.");
                Check(root.Q<Label>("summary.studio.opening-team-gauge.who").text.Contains("Enemies") && root.Q<Label>("summary.studio.opening-team-gauge.who").text.Contains("Self") &&
                    root.Q<Label>("studio-rule-execution-summary").text.Contains("3 owner turns"), "WHO and execution summaries refresh without navigation or rebuilding the window.");
                Click(window, "card.studio.opening-team-gauge.who");
                Check(root.Query<Label>().ToList().Any(l => l.text == "WHO · EVENT FILTERS"), "WHO opens its contextual inspector.");
                Click(window, "studio-save");
                session = window.Draft;
                var reopened = StudioProductionBridge.OpenSource(character);
                try
                {
                    StudioDraftRepository.TryRead(reopened, out doc, out library, out _);
                    var compiled = StudioCompiler.Compile(doc, library);
                    Check(compiled.Success && compiled.Character.Skills[0].Variants.Any(v => v.Id == "studio.opening-team-gauge"), "Native variant with colliding passive ID survives save and reopen.");
                    reaction = compiled.Character.Passive.Reactions.Single(r => r.Id == "studio.opening-team-gauge");
                    Check(reaction.CooldownOwnerTurns == 3 && reaction.ActorRelation == PassiveRelation.Enemies, "Opening controls survive native persistence.");
                    Check(doc.Rules.Count(r => r.Id == "native.check.opening") == 1, "Imported native event uses the shared rule representation after reload.");
                    doc.Rules.RemoveAll(r => r.Id == "native.check.opening");
                    StudioDraftRepository.Edit(reopened, doc, library, "Remove imported event"); StudioProductionBridge.SaveSource(reopened); StudioProductionBridge.SaveSource(reopened);
                    Check(character.PassiveDefinition.CreateDefinition().Reactions.All(r => r.Id != "native.check.opening"), "Removing imported event remains removed after repeated save/reopen.");
                }
                finally { UnityEngine.Object.DestroyImmediate(reopened); }
                window.LoadDraft(StudioProductionBridge.OpenSource(character)); session = window.Draft; window.ShowWorkspace("passive");
                root.Q<Foldout>("passive.open.native.check.aura").value = true;
                Check(root.Q<VisualElement>("studio-kit-aura") != null, "Continuous mechanic expands inside its own card.");
                root.Q<Foldout>("passive.open.native.check.aura").value = false;
                Check(root.Q<VisualElement>("studio-kit-aura") == null, "Expanded mechanic can be collapsed without leaving Passive Overview.");
                root.Q<Foldout>("passive.open.native.check.aura").value = true;
                Field<PopupField<string>>(root.Q<VisualElement>("studio-kit-aura"), "Active position").value = "Off-Field";
                Check(root.Q<PopupField<string>>("studio-passive-position").value.Contains("Mixed"), "Overview availability refreshes when an individual mechanic diverges.");
                Check(root.Q<Label>("passive.availability.native.check.aura").text.Contains("Off-Field"), "Continuous-rule inventory availability refreshes immediately.");
                Check(root.Q<VisualElement>("studio-native-passive-badges").Query<Image>().ToList().Any(i => i.tooltip == "Off-Field"), "Preview badges include the executable off-field composition.");
                var auraEditor = root.Q<VisualElement>("studio-kit-aura");
                Field<EnumField>(auraEditor, "Modifier target").value = ModifierTarget.StatBundle;
                Check(auraEditor.Q<EnumField>("studio-modifier-bundle") != null && auraEditor.Q<EnumField>("studio-modifier-family") == null &&
                    auraEditor.Query<EnumField>().ToList().All(f => f.label != "Stat" && f.label != "Operation"), "Bundle modifiers show only applicable target fields.");
                Field<EnumField>(auraEditor, "Modifier target").value = ModifierTarget.FamilyDamageDealt;
                Field<EnumField>(auraEditor, "Damage family").value = DamageFamily.DamageOverTime;
                Check(auraEditor.Q<EnumField>("studio-modifier-bundle") == null && auraEditor.Q<EnumField>("studio-modifier-family") != null &&
                    root.Q<Label>("passive.summary.native.check.aura").text.Contains("Damage Over Time damage dealt"), "Changing target hides bundle fields and refreshes the family-damage preview.");
            }
            finally
            {
                if (window != null) window.Close();
                if (session != null) UnityEngine.Object.DestroyImmediate(session);
                UnityEngine.Object.DestroyImmediate(publisher);
                var folder = StudioProductionBridge.RuntimeRoot + "/" + id.Substring(8);
                if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
                FightingAllstar.EditorTools.CharacterRegistrySync.Sync();
                foreach (var p in prefs) EditorPrefs.SetString(p.Key, p.Value);
            }
            Check(originals.All(p => File.ReadAllBytes(p.Key).SequenceEqual(p.Value)), "All original character and native data assets remain byte-for-byte unchanged.");
            Check(SceneManager.GetActiveScene().path == scene.path && SceneManager.GetActiveScene().isDirty == sceneDirty && !EditorApplication.isPlaying,
                "The active scene and Play Mode remain unchanged.");
            var evidence = new { Success = true, Checks = checks, NativeFilesPreserved = originals.Count, CapturedUtc = DateTime.UtcNow };
            Directory.CreateDirectory("Artifacts/CharacterCreatorStudio/PassiveCentralization");
            File.WriteAllText("Artifacts/CharacterCreatorStudio/PassiveCentralization/native-ui-evidence.json", JsonConvert.SerializeObject(evidence, Formatting.Indented));
            return evidence;
        }
        private static T Field<T>(VisualElement root, string label) where T : VisualElement => root.Query<T>().ToList().Single(f => (string)f.GetType().GetProperty("label").GetValue(f) == label);
        private static void Click(CharacterCreatorStudioWindow window, string name)
        {
            var button = window.rootVisualElement.Q<Button>(name); if (button == null) throw new InvalidOperationException("Missing " + name);
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = button; button.SendEvent(e); }
        }
    }
}
#endif
