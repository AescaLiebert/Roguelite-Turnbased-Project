#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using FightingAllstar.Core.Studio;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    [InitializeOnLoad]
    public static class StudioBattleLabBridge
    {
        public const string ScenePath = "Assets/Project/Scenes/Training.unity";
        private const string Pending = "CharacterStudio.BattleLab.Pending", Payload = "CharacterStudio.BattleLab.Memory", Previous = "CharacterStudio.BattleLab.PreviousStart", Visuals = "CharacterStudio.BattleLab.Visuals";
        [Serializable] public sealed class VisualSelection { public string FighterId, AssetPath, ProfilePath; public float Scale = 1, Yaw; }
        private static CoreBattleSceneController controller;
        public static CoreBattleSceneController Controller
        {
            get
            {
                if (!EditorApplication.isPlaying) return null;
                if (controller == null) controller = UnityEngine.Object.FindFirstObjectByType<CoreBattleSceneController>();
                return controller != null && controller.IsBattleLab ? controller : null;
            }
        }
        static StudioBattleLabBridge()
        {
            EditorApplication.playModeStateChanged += OnMode;
            if (EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetBool(Pending, false))
            {
                Restore();
                EditorApplication.delayCall += () => {
                    // A code reload cannot retain the managed authority session/coroutines.
                    // Recreate the disposable lab instead of exposing a stranded half-session.
                    if (EditorApplication.isPlaying && SceneManager.GetActiveScene().path == ScenePath && Controller != null && Controller.LabSnapshot == null)
                        BattleLabContext.Restart();
                };
            }
        }
        public static void Launch(BattleLabConfiguration settings, List<VisualSelection> visuals = null)
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying) throw new InvalidOperationException("Wait for the editor transition to finish.");
            BattleLabSimulation.Create(settings, out _); // Validate the frozen setup before entering Play Mode.
            foreach(var visual in visuals ?? new List<VisualSelection>())
            {
                var profile=AssetDatabase.LoadAssetAtPath<ActorPresentationProfile>(visual.ProfilePath ?? "");
                if(profile!=null && !profile.HasValidTransform)throw new InvalidOperationException("Repair the actor profile's scale, yaw and approach before launching.");
            }
            var visualJson = SnapshotJson.Serialize(visuals ?? new List<VisualSelection>());
            if (EditorApplication.isPlaying)
            {
                if (Controller == null) throw new InvalidOperationException("Stop the unrelated Play Mode session before launching Battle Lab.");
                SessionState.SetString(Payload, SnapshotJson.Serialize(settings)); SessionState.SetString(Visuals, visualJson); BattleLabContext.Prepare(settings); RestoreVisuals();
                EditorApplication.isPaused = false; BattleLabContext.Restart(); controller = null; return;
            }
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (scene == null) throw new InvalidOperationException("The existing 3D Training scene is missing.");
            SessionState.SetString(Previous, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetString(Payload, SnapshotJson.Serialize(settings)); SessionState.SetBool(Pending, true);
            SessionState.SetString(Visuals, visualJson); BattleLabContext.Prepare(settings); RestoreVisuals(); EditorSceneManager.playModeStartScene = scene;
            EditorApplication.EnterPlaymode();
        }
        private static void Restore()
        {
            var json = SessionState.GetString(Payload, "");
            if (!string.IsNullOrEmpty(json)) { BattleLabContext.Prepare(SnapshotJson.Deserialize<BattleLabConfiguration>(json)); RestoreVisuals(); }
        }
        private static void RestoreVisuals()
        {
            BattleLabContext.ClearVisuals();
            foreach (var visual in SnapshotJson.Deserialize<List<VisualSelection>>(SessionState.GetString(Visuals, "[]")))
                BattleLabContext.SetVisual(visual.FighterId, AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(visual.AssetPath), visual.Scale, visual.Yaw, AssetDatabase.LoadAssetAtPath<ActorPresentationProfile>(visual.ProfilePath ?? ""));
        }
        private static void OnMode(PlayModeStateChange state)
        {
            controller = null;
            if (state == PlayModeStateChange.ExitingEditMode && SessionState.GetBool(Pending, false)) Restore();
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Pending, false)) return;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Previous, ""));
            SessionState.EraseBool(Pending); SessionState.EraseString(Payload); SessionState.EraseString(Previous); SessionState.EraseString(Visuals);
            BattleLabContext.Clear();
        }
        public static void Stop()
        { if (EditorApplication.isPlaying && SessionState.GetBool(Pending, false)) { EditorApplication.isPaused = false; EditorApplication.ExitPlaymode(); } }
    }
}
#endif

