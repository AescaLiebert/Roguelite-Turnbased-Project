#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>Builds disposable scenes in the isolated motion project, then exercises real Play mode.</summary>
public static class ScreenMotionRuntimeChecks
{
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory("Assets/MotionFixture");
            Directory.CreateDirectory("Artifacts/ScreenMotion");
            var source = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Project/Scenes/Fixed1920PanelSettings.asset");
            var settings = UnityEngine.Object.Instantiate(source);
            settings.name = "Motion fixture panel"; settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.referenceResolution = new Vector2Int(960, 540);
            AssetDatabase.DeleteAsset("Assets/MotionFixture/Panel.asset");
            AssetDatabase.CreateAsset(settings, "Assets/MotionFixture/Panel.asset"); AssetDatabase.SaveAssets();
            settings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/MotionFixture/Panel.asset");
            foreach (var name in new[] { "MotionSceneA", "MotionSceneB" })
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var documentObject = new GameObject(name + " UI");
                var document = documentObject.AddComponent<UIDocument>(); document.panelSettings = settings;
                var serializedDocument = new SerializedObject(document);
                serializedDocument.FindProperty("m_PanelSettings").objectReferenceValue = settings;
                serializedDocument.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(document);
                if (name == "MotionSceneA") new GameObject("Runtime motion verification").AddComponent<ScreenMotionRuntimeProbe>();
                EditorSceneManager.SaveScene(scene, "Assets/MotionFixture/" + name + ".unity");
            }
            EditorBuildSettings.scenes = new[] {
                new EditorBuildSettingsScene("Assets/MotionFixture/MotionSceneA.unity", true),
                new EditorBuildSettingsScene("Assets/MotionFixture/MotionSceneB.unity", true)
            };
            EditorSceneManager.OpenScene("Assets/MotionFixture/MotionSceneA.unity", OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            EditorApplication.EnterPlaymode();
        }
        catch (Exception error)
        {
            File.WriteAllText("Artifacts/ScreenMotion/Verification.txt", "FAIL during runtime fixture setup: " + error);
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
}
#endif
