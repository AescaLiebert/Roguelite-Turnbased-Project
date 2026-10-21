using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FightingAllstar.Adapters;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;
using FightingAllstar.EnvironmentArt;
using FightingAllstar.Presentation.Combat;
using FightingAllstar.Presentation.Content;
using FightingAllstar.Presentation.Route;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools
{
    /// <summary>Disposable run and real scene/controller; never writes inventory/run saves.</summary>
    [InitializeOnLoad]
    public static class TeyvatDungeonValidation
    {
        const string Key = "TeyvatDungeon.Validation";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static double began, next;
        static bool planning, execution, returning;
        static bool initialColorTemperature, initialLinearIntensity;
        static RunState run;
        static readonly List<string> errors = new List<string>();
        static CoreBattleSceneController controller;
        static TeyvatDungeonValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += Mode;
            Application.logMessageReceived += Log;
        }
        static void Log(string message, string stack, LogType type)
        {
            if (SessionState.GetInt(Key, 0) == 2 && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                && !stack.Contains("UnityEditor.Search.")) errors.Add(message);
        }
        static void Mode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetInt(Key, 0) == 1)
            {
                try { Seed(); SessionState.SetInt(Key, 2); began = EditorApplication.timeSinceStartup; next = began + 1; SceneManager.LoadScene(DungeonBattleScene.Teyvat); }
                catch (Exception e) { Finish(false, e.ToString()); }
            }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetInt(Key, 0) > 0)
            {
                LocalEncounterContext.Clear(); DungeonFlowContext.Clear();
                PlayerSettings.runInBackground = SessionState.GetBool(Key + ".RunInBackground", false);
                var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0];
                EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
                SessionState.SetInt(Key, 0);
                EditorSceneManager.OpenScene(TeyvatDungeonBuilder.ScenePath);
            }
        }
        static void Seed()
        {
            Application.runInBackground = true;
            errors.Clear(); planning = execution = returning = false;
            initialColorTemperature = GraphicsSettings.lightsUseColorTemperature;
            initialLinearIntensity = GraphicsSettings.lightsUseLinearIntensity;
            var catalog = CharacterObjectCatalogBuilder.Load(null);
            // The authored Genshin definitions are 1411..1415 (WIP / META).
            var profile = DungeonFlowContext.CreateEntryProfile("series.genshin-impact", 1, 4);
            var definitions = catalog.Characters.Where(c => c.RuntimeReady && profile.Accepts(c)).Take(4).ToList();
            if (definitions.Count != 4) throw new Exception("Four playable Genshin definitions are required.");
            // Disposable test strength shortens the playthrough. It never edits
            // a character asset, inventory, dungeon balance or a persisted run.
            var roster = definitions.Select((c, i) => new RunFighterSeed { OwnedFighterId = "teyvat-check:" + i,
                Definition = c, ResolvedStats = c.BaseStats.Clone(), FormationSlot = i, IsReserve = i == 3, Level = 1 }).ToList();
            foreach (var fighter in roster) { fighter.ResolvedStats.MaxHealth = 50000; fighter.ResolvedStats.Attack = 20000; fighter.ResolvedStats.CombatClass = 999999; }
            run = DungeonRunEngine.CreateRun("teyvat-arena-validation", "disposable-validation", "start", profile, 0, 831,
                catalog.ContentVersion, catalog.ContentHash, roster, catalog.Characters);
            var node = run.Nodes.First(n => run.FindNode(run.CurrentNodeId).OutgoingNodeIds.Contains(n.Id) &&
                (n.Type == RouteNodeType.Battle || n.Type == RouteNodeType.Elite || n.Type == RouteNodeType.Boss));
            if (!DungeonRunEngine.TryChooseNode(run, node.Id, run.Revision, "choose", out run, out var error)) throw new Exception(error);
            if (!DungeonRunEngine.TryBuildEncounter(run, catalog.Characters, out var encounter, out error)) throw new Exception(error);
            DungeonFlowContext.SetCatalog(catalog.Characters);
            DungeonFlowContext.BeginDungeonFlowForEncounter(DungeonFlowContext.GetProfileForId(run.ProfileId), run, encounter);
            if (DungeonBattleScene.ForProfile(DungeonFlowContext.ActiveProfile) != DungeonBattleScene.Teyvat) throw new Exception("Resumed encounter routing failed.");
            LocalEncounterContext.Begin(encounter, 8, previewOnly: true);
        }
        static object Field(string name) => typeof(CoreBattleSceneController).GetField(name, Private).GetValue(controller);
        static void Call(string name, params object[] args) => typeof(CoreBattleSceneController).GetMethod(name, Private).Invoke(controller, args);
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Capture(string name)
        {
            var doc = (UIDocument)Field("battleDocument");
            BattlePreviewCapture.Save(TeyvatDungeonBuilder.Evidence + "/" + name + ".png", doc);
        }
        static void Tick()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
                File.Exists("Temp/check-teyvat-dungeon.request"))
            {
                File.Delete("Temp/check-teyvat-dungeon.request"); Directory.CreateDirectory(TeyvatDungeonBuilder.Evidence);
                SessionState.SetBool(Key + ".RunInBackground", PlayerSettings.runInBackground);
                SessionState.SetInt(Key, 1); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene); EditorApplication.EnterPlaymode(); return;
            }
            if (!EditorApplication.isPlaying || SessionState.GetInt(Key, 0) != 2) return;
            // Keep real player frames advancing when the MCP-driven Editor is
            // unfocused or its Game view is behind another tab.
            EditorApplication.QueuePlayerLoopUpdate();
            try
            {
                if (EditorApplication.timeSinceStartup - began > 180) throw new Exception("Playthrough timed out.");
                if (returning)
                {
                    if (SceneManager.GetActiveScene().name != "Battle") return;
                    var street=FightingAllstar.Rendering.MapRenderContext.Active;
                    Require(TeyvatDungeonArena.Active == null && street != null && GraphicsSettings.currentRenderPipeline == street.pipeline, "Shared Osaka rendering context did not take over.");
                    Require(GraphicsSettings.lightsUseLinearIntensity == initialLinearIntensity,
                        "Shared map transition changed the project color-space interpretation.");
                    Require(GameObject.Find("Osaka Night • Walking Mall") != null, "Default Osaka stage missing after return.");
                    Require(errors.Count == 0, string.Join("; ", errors));
                    Finish(true, "PASS: real Genshin generated route/encounter and resumed policy, dedicated scene, real combat opening/card execution/result, in-memory dungeon result application, shared URP Osaka rendering context took over. No account saves written."); return;
                }
                controller = UnityEngine.Object.FindFirstObjectByType<CoreBattleSceneController>();
                if (controller == null || EditorApplication.timeSinceStartup < next) return;
                var phase = controller.PresentationPhase;
                if (phase == BattlePresentationPhase.CardExecution && !execution) { execution = true; Capture("02-card-execution"); }
                if (phase == BattlePresentationPhase.Complete)
                {
                    Require(planning && execution, "Opening/planning/execution was not exercised.");
                    Capture("03-result");
                    var session = (IBattleSession)Field("_session"); var battle = session.GetSnapshot();
                    var results = RunBattleBridge.BuildRunResults(run, battle);
                    Require(DungeonRunEngine.TryApplyBattleResult(run, battle.MatchId, "arena-result", run.Revision, "apply", true, results, out var completed, out var error), error);
                    Require(completed.CompletedBattleReceiptIds.Contains("arena-result"), "Dungeon result receipt was not applied.");
                    returning = true; SceneManager.LoadScene("Battle"); return;
                }
                if (phase != BattlePresentationPhase.Planning || !(bool)typeof(CoreBattleSceneController).GetProperty("CanPlan", Private).GetValue(controller)) return;
                if (!planning)
                {
                    var arena = TeyvatDungeonArena.Active;
                    Require(arena != null && GraphicsSettings.currentRenderPipeline == arena.pipeline, "Dedicated URP renderer inactive.");
                    Require(LightmapSettings.lightProbes != null && LightmapSettings.lightProbes.count == 45, "Accepted baked lighting probes missing.");
                    Require(GameObject.Find("Osaka Night • Walking Mall") == null, "Osaka is visible in Genshin battle.");
                    var binding = UnityEngine.Object.FindFirstObjectByType<TeyvatStageBinding>();
                    Require(binding != null && binding.TransformSupported && binding.boundPlacementCount == 27737, "Arena/grass binding failed.");
                    var views = (Dictionary<string, GameObject>)Field("_fighterViews");
                    Require(views.Count >= 7 && views.Values.Count(v => v.activeSelf) == 6, "Active/reserve fighter spawn failed.");
                    Require(views.Values.SelectMany(v => v.GetComponentsInChildren<Renderer>(true)).All(r => r.sharedMaterials.All(m => m == null || m.shader.name != "Standard")), "An actor kept an incompatible Standard material.");
                    Require(views.Values.SelectMany(v => v.GetComponentsInChildren<Renderer>(true)).All(r => r.sharedMaterials.All(m => m == null || m.shader.isSupported)), "Unsupported actor shader.");
                    var doc = (UIDocument)Field("battleDocument");
                    Require(doc.rootVisualElement.Q<VisualElement>("deck-row")?.childCount > 0, "Playable card hand missing.");
                    planning = true; Capture("01-planning");
                }
                var snapshot = (BattleState)Field("_snapshot"); var draft = (PlanDraft)Field("_draft");
                var hand = draft?.Preview.Hand ?? snapshot.Player.Hand;
                if (hand.Count > 0) Call("QueueCard", hand[0].Id); else Call("CommitDraft");
                next = EditorApplication.timeSinceStartup + .65;
            }
            catch (Exception e) { Finish(false, e.ToString()); }
        }
        static void Finish(bool pass, string message)
        {
            Directory.CreateDirectory(TeyvatDungeonBuilder.Evidence);
            File.WriteAllText(TeyvatDungeonBuilder.Evidence + "/" + (pass ? "PlayPassed.txt" : "PlayFailed.txt"), message + "\n" + string.Join("\n", errors));
            if (pass && File.Exists(TeyvatDungeonBuilder.Evidence + "/PlayFailed.txt")) File.Delete(TeyvatDungeonBuilder.Evidence + "/PlayFailed.txt");
            SessionState.SetInt(Key, 3); EditorApplication.ExitPlaymode();
        }
    }
}
