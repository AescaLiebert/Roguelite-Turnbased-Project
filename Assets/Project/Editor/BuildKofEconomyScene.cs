using System;
using System.IO;
using FightingAllstar.Presentation.Economy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools
{
    public static class BuildKofEconomyScene
    {
        private const string Root = "Assets/Project";
        private const string ScenePath = Root + "/Scenes/Scene-KofEconomyPrototype.unity";
        private const string PanelPath = Root + "/Scenes/DungeonPanelSettings.asset";
        private const string UxmlPath = Root + "/UI/KofBanner.uxml";
        private const string CatalogPath = Root + "/Content/WipCharacterCatalog.json";

        [MenuItem("Fighting Allstar/Build KOF Economy Prototype Scene")]
        public static void Build()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            var catalog = AssetDatabase.LoadAssetAtPath<TextAsset>(CatalogPath);
            if (panel == null || tree == null || catalog == null)
                throw new InvalidOperationException("Economy scene needs PanelSettings, KofBanner.uxml, and WipCharacterCatalog.json.");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var screen = new GameObject("KOF Economy Prototype");
            var document = screen.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = tree;
            var serializedDocument = new SerializedObject(document);
            var panelProperty = serializedDocument.FindProperty("m_PanelSettings");
            if (panelProperty == null) throw new MissingFieldException(typeof(UIDocument).Name, "m_PanelSettings");
            panelProperty.objectReferenceValue = panel;
            serializedDocument.ApplyModifiedPropertiesWithoutUndo();
            var controller = screen.AddComponent<LocalEconomyController>();
            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("catalogJson").objectReferenceValue = catalog;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(document);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            PatchPanelReference();
            Debug.Log("Built the local KOF economy prototype scene with the pinned WIP character catalog.");
        }

        private static void PatchPanelReference()
        {
            var path = Path.GetFullPath(ScenePath);
            var yaml = File.ReadAllText(path);
            const string missing = "m_PanelSettings: {fileID: 0}";
            var count = yaml.Split(new[] { missing }, StringSplitOptions.None).Length - 1;
            if (count != 1) throw new InvalidDataException("Expected one UI document in the KOF economy scene; found " + count + " unresolved panel references.");
            var guid = AssetDatabase.AssetPathToGUID(PanelPath);
            if (string.IsNullOrWhiteSpace(guid)) throw new InvalidDataException("Dungeon PanelSettings asset has no GUID.");
            File.WriteAllText(path, yaml.Replace(missing, "m_PanelSettings: {fileID: 11400000, guid: " + guid + ", type: 2}"));
        }
    }
}
