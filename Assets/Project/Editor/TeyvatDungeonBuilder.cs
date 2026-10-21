using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.EnvironmentArt;
using FightingAllstar.Presentation.Combat;
using FightingAllstar.Presentation.Route;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FightingAllstar.EditorTools
{
    [InitializeOnLoad]
    public static class TeyvatDungeonBuilder
    {
        public const string ScenePath = "Assets/Project/Scenes/BattleTeyvat.unity";
        private const string Root = "Assets/Project/Art/Environment/TeyvatGrassland/Lighting";
        public const string Evidence = "Artifacts/TeyvatGrassland/PlayableDungeon";
        static TeyvatDungeonBuilder() { EditorApplication.update += Requests; }
        static void Requests()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!File.Exists("Temp/build-teyvat-dungeon.request")) return;
            File.Delete("Temp/build-teyvat-dungeon.request");
            try { Build(); } catch (Exception e) { Directory.CreateDirectory(Evidence); File.WriteAllText(Evidence + "/BuildFailed.txt", e.ToString()); Debug.LogException(e); }
        }

        [MenuItem("Fighting Allstar/Stage/Build Playable Teyvat Dungeon")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before rebuilding.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save open scene edits before building the dedicated dungeon scene.");
            Directory.CreateDirectory(Evidence);
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene("Assets/Project/Scenes/Battle.unity");
                EditorSceneManager.SaveScene(scene, ScenePath);
                foreach (var context in Object.FindObjectsByType<FightingAllstar.Rendering.MapRenderContext>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    Object.DestroyImmediate(context);
                foreach (var root in scene.GetRootGameObjects().Where(g => g.name == "Osaka Night • Walking Mall" || g.name == "Directional Light"))
                    Object.DestroyImmediate(root);
                foreach (var glow in Object.FindObjectsByType<OsakaNightAtmosphere>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(glow);
                var stage = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TeyvatSurroundingsBuilder.Stage), scene);
                stage.name = "Teyvat Arena";
                var binding = stage.GetComponent<TeyvatStageBinding>();
                var player = GameObject.Find("CharHeroPosition2").transform.position;
                var enemy = GameObject.Find("EnemyHeroPosition2").transform.position;
                stage.transform.SetPositionAndRotation((player + enemy) * .5f, Quaternion.identity);
                stage.transform.localScale = Vector3.one;
                binding.ApplyBinding();

                var review = EditorSceneManager.OpenScene("Assets/Project/Scenes/EnvironmentReview/TeyvatGrassland/TG_LightingReview_r2.unity", OpenSceneMode.Additive);
                SceneManager.SetActiveScene(review);
                var sourceRig = review.GetRootGameObjects().Single(g => g.name == "TG WP08 lighting rig");
                var rigRoot = Object.Instantiate(sourceRig);
                rigRoot.name = "Teyvat Dungeon Lighting";
                SceneManager.MoveGameObjectToScene(rigRoot, scene);
                var rig = rigRoot.GetComponent<TeyvatLightingRig>();
                rig.globals = stage.GetComponentInChildren<TeyvatMaterialGlobals>();
                var data = Lightmapping.lightingDataAsset;
                if (data == null) throw new InvalidOperationException("The accepted lighting review has no baked probe data.");
                EditorSceneManager.CloseScene(review, true);
                SceneManager.SetActiveScene(scene);
                // Positions match the accepted review frame. The scene's disabled
                // GI proxies and probe group are copied together with the rig.
                Lightmapping.lightingDataAsset = data;
                rig.Apply();
                var runtime = new GameObject("Teyvat Dungeon Rendering").AddComponent<TeyvatDungeonArena>();
                runtime.pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root + "/Settings/TG_LightingURP.asset");
                runtime.actorShader = Shader.Find("Universal Render Pipeline/Lit");
                runtime.lightingRig = rig;
                var camera = Camera.main;
                var additional = camera.GetUniversalAdditionalCameraData();
                additional.renderPostProcessing = true; additional.requiresDepthTexture = true; additional.requiresColorTexture = false;
                camera.allowHDR = true; camera.allowMSAA = true; camera.farClipPlane = 1600;
                binding.meadow.data = binding.canonicalMeadowData;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                binding.ApplyBinding();
                var buildScenes = EditorBuildSettings.scenes.ToList();
                if (!buildScenes.Any(s => s.path == ScenePath)) buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                else buildScenes.First(s => s.path == ScenePath).enabled = true;
                EditorBuildSettings.scenes = buildScenes.ToArray();
                RegisterPipelineForBuild(runtime.pipeline);
                MapOptimizationLab.ApplyStandardToOpenMap();
                AssetDatabase.SaveAssets();
                ValidateRouting();
                File.WriteAllText(Evidence + "/BuildPassed.txt", "Dedicated BattleTeyvat saved; original Battle retained; route policies and build registration passed.");
                if (File.Exists(Evidence + "/BuildFailed.txt")) File.Delete(Evidence + "/BuildFailed.txt");
                Debug.Log("TEYVAT DUNGEON: dedicated playable scene built and Genshin policy routing verified.");
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }

        static void RegisterPipelineForBuild(UniversalRenderPipelineAsset pipeline)
        {
            // URP's build preprocessor discovers pipelines through Quality settings.
            // Add an opt-in tier; retain every existing tier and the active/default indices.
            var defaultLines = File.ReadAllLines("ProjectSettings/QualitySettings.asset");
            var defaults = new Dictionary<string, int>(); var readingDefaults = false;
            foreach (var line in defaultLines)
            {
                if (line.StartsWith("  m_PerPlatformDefaultQuality:")) { readingDefaults = true; continue; }
                if (!readingDefaults) continue;
                if (!line.StartsWith("    ")) break;
                var split = line.Trim().Split(':');
                if (split.Length == 2 && int.TryParse(split[1].Trim(), out var value)) defaults[split[0]] = value;
            }
            var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = quality.FindProperty("m_QualitySettings");
            var index = -1;
            for (var i = 0; i < levels.arraySize; i++) if (levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == "Teyvat Arena") index = i;
            if (index < 0) { index = levels.arraySize; levels.InsertArrayElementAtIndex(index); }
            var level = levels.GetArrayElementAtIndex(index);
            level.FindPropertyRelative("name").stringValue = "Teyvat Arena";
            level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
            level.FindPropertyRelative("excludedTargetPlatforms").ClearArray();
            // Explicitly preserve the native map: array edits in a generic
            // SerializedObject can otherwise drop its platform-default pairs.
            if (defaults.Count > 0)
            {
                var platformDefaults = quality.FindProperty("m_PerPlatformDefaultQuality"); platformDefaults.arraySize = defaults.Count;
                var i = 0;
                foreach (var pair in defaults)
                {
                    var item = platformDefaults.GetArrayElementAtIndex(i++);
                    item.FindPropertyRelative("first").stringValue = pair.Key;
                    item.FindPropertyRelative("second").intValue = pair.Value;
                }
            }
            quality.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ValidateRouting()
        {
            foreach (var phase in new[] { 1, 2, 3 }) foreach (var sub in new[] { 0, 1 }) foreach (var boss in new[] { false, true })
            {
                var profile = DungeonFlowContext.CreateEntryProfile("series.genshin-impact", phase, sub, seriesBoss: boss);
                if (DungeonBattleScene.ForProfile(profile) != DungeonBattleScene.Teyvat ||
                    DungeonBattleScene.ForProfile(DungeonFlowContext.GetProfileForId(profile.Id)) != DungeonBattleScene.Teyvat)
                    throw new Exception("Genshin fresh/resumed policy routed incorrectly: " + profile.Id);
            }
            foreach (var series in new[] { "series.kof", "series.tekken", "series.soulcalibur" })
                if (DungeonBattleScene.ForProfile(DungeonFlowContext.CreateEntryProfile(series, 1, 0)) != DungeonBattleScene.Default)
                    throw new Exception("An unrelated dungeon changed arena.");
            if (DungeonBattleScene.ForProfile(null) != DungeonBattleScene.Default) throw new Exception("Default routing changed.");
        }
    }
}
