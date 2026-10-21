#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using FightingAllstar.Presentation.Combat;
using FightingAllstar.Presentation.Content;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    [InitializeOnLoad]
    public static class StudioNativeSessionReloadCheck
    {
        private const string Key = "CharacterStudio.NativeSessionReload";
        [Serializable] private sealed class Trial { public bool Running, Dirty; public int Stage; public string Error, Scene; public double Deadline; public List<string> Checks = new List<string>(); }
        private static Trial trial;
        static StudioNativeSessionReloadCheck() { trial = JsonConvert.DeserializeObject<Trial>(SessionState.GetString(Key, "null")); if (trial?.Running == true) EditorApplication.update += Tick; }
        public static object Status => trial == null ? (object)new { Running = false } : new { trial.Running, trial.Stage, trial.Error, Checks = trial.Checks.Count };
        private static void Store() => SessionState.SetString(Key, JsonConvert.SerializeObject(trial));
        private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); trial.Checks.Add(message); Store(); }
        public static object Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Start from stable Edit Mode.");
            var joe = CharacterObjectRegistrySO.LoadAll().Single(c => c.DefinitionId == "fighter.joe96");
            var session = StudioProductionBridge.OpenSource(joe); var window = CharacterCreatorStudioWindow.Open(); window.LoadDraft(session);
            StudioDraftRepository.TryRead(session, out var doc, out var library, out _); StudioDocumentService.BeginKitEditing(doc).BaseStats.Attack = 251;
            StudioDraftRepository.Edit(session, doc, library, "Unsaved reload verification"); window.LoadDraft(session);
            trial = new Trial { Running = true, Stage = 0, Scene = SceneManager.GetActiveScene().path, Dirty = SceneManager.GetActiveScene().isDirty, Deadline = EditorApplication.timeSinceStartup + 180 };
            Store(); EditorApplication.update -= Tick; EditorApplication.update += Tick;
            var target = CharacterObjectRegistrySO.LoadAll().Single(c => c.DefinitionId == "fighter.kyo98");
            StudioBattleLabBridge.Launch(new BattleLabConfiguration { Mode = BattleLabMode.TrainingBattle, FirstTurn = BattleLabFirstTurn.Player,
                Actors = new List<BattleLabFighter> { new BattleLabFighter { Definition = CharacterObjectCatalogBuilder.CreateDefinition(joe), Tier = 5, Gauge = 5 } },
                Targets = new List<BattleLabFighter> { new BattleLabFighter { Definition = CharacterObjectCatalogBuilder.CreateDefinition(target) } } });
            return Status;
        }
        private static void Tick()
        {
            if (trial?.Running != true || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (EditorApplication.timeSinceStartup > trial.Deadline) throw new TimeoutException("Native session reload stage " + trial.Stage);
                var controller = StudioBattleLabBridge.Controller;
                if (trial.Stage == 0)
                {
                    if (controller?.LabCanAct != true) return;
                    var state = controller.LabSnapshot; var joe = state.Player.Fighters[0];
                    Check(controller.LabFighterViewCount == 2 && joe.Definition.Id == "fighter.joe96", "Actual 3D battle spawns native Joe96 and target across domain reload.");
                    Check(joe.Definition.BaseStats.Attack == 250, "Native battle uses the saved SOs rather than unsaved Studio preview edits.");
                    Check(StatusSystem.GetEffectiveStats(joe).Defense == 320, "Blue-ally passive raises native opening Defense from 200 to 320.");
                    var card = state.Player.Hand.Single(c => c.SkillId == "fighter.joe96.skill.2" && c.Rank == 3);
                    trial.Stage = 1; Store();
                    if (!controller.LabPlayCard(card.Id, state.Opponent.Fighters[0].Id, out var error)) throw new InvalidOperationException(error);
                }
                else if (trial.Stage == 1)
                {
                    if (controller == null) return;
                    var state = controller.LabSnapshot; var damage = state.Events.Where(e => e.Kind == BattleEventKind.DamageApplied && e.SourceId.Contains("fighter.joe96")).ToList();
                    if (damage.Count < 4) return;
                    var stats = StatusSystem.GetEffectiveStats(state.Player.Fighters[0]);
                    Check(damage.Count(e => e.HitCount == 4) == 4, "Native rank-three Skill 2 resolves four authoritative damage contacts.");
                    Check(stats.Defense == 440 && stats.BlockChanceBp == 9000, "Native team protection raises Defense to 440 and Block Chance to 90 percent.");
                    trial.Stage = 2; Store(); StudioBattleLabBridge.Stop();
                }
                else
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                    if (BattleLabContext.IsReady || SceneManager.GetActiveScene().path != trial.Scene) return;
                    var window = CharacterCreatorStudioWindow.Open(); if (window.Draft == null || window.Compilation?.Success != true) return;
                    Check(window.Draft.IsDirectSession && window.Draft.SourceCharacter.DefinitionId == "fighter.joe96", "Native editing session restores after leaving Play Mode without a draft asset.");
                    Check(window.Compilation.Character.BaseStats.Attack == 251, "Unsaved authoring edits survive the Play Mode round trip.");
                    Check(CharacterObjectCatalogBuilder.CreateDefinition(window.Draft.SourceCharacter).BaseStats.Attack == 250, "Domain reload recovery never writes pending edits into native assets.");
                    StudioDraftRepository.TryRead(window.Draft, out var doc, out var library, out _); StudioDocumentService.BeginKitEditing(doc).BaseStats.Attack = 250;
                    StudioDraftRepository.Edit(window.Draft, doc, library, "Clear reload verification edit"); window.LoadDraft(window.Draft); window.ShowWorkspace("overview");
                    Check(SceneManager.GetActiveScene().path == trial.Scene && SceneManager.GetActiveScene().isDirty == trial.Dirty && !BattleLabContext.IsReady, "Original scene and volatile lab context restore after the check.");
                    Finish(true);
                }
            }
            catch (Exception e) { trial.Error = e.ToString(); if (EditorApplication.isPlaying) StudioBattleLabBridge.Stop(); Finish(false); }
        }
        private static void Finish(bool success)
        {
            trial.Running = false; Store(); EditorApplication.update -= Tick;
            Directory.CreateDirectory("Artifacts/CharacterCreatorStudio/KitAuthoring/Joe96");
            File.WriteAllText("Artifacts/CharacterCreatorStudio/KitAuthoring/Joe96/live-evidence.json", JsonConvert.SerializeObject(new { Success = success, trial.Error, trial.Checks, OriginalScene = trial.Scene, OriginalSceneDirty = trial.Dirty, CapturedUtc = DateTime.UtcNow,
                Scope = "Actual 3D actors and committed native damage/status results, plus unsaved editor session recovery; no complete animation-cycle or FCT assertion." }, Formatting.Indented));
        }
    }
}
#endif
