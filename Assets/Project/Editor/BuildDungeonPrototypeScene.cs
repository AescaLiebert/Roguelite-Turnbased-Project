using System;
using System.IO;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Combat;
using FightingAllstar.Presentation.Route;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools
{
    /// <summary>Rebuilds the isolated local route-flow scene without touching legacy gameplay scenes.</summary>
    public static class BuildDungeonPrototypeScene
    {
        private const string Root = "Assets/Project";
        private const string PanelPath = Root + "/Scenes/DungeonPanelSettings.asset";
        private const string ScenePath = Root + "/Scenes/Scene-DungeonPrototype.unity";

        [MenuItem("Fighting Allstar/Build Dungeon Prototype Scene")]
        public static void Build()
        {
            EnsureFolder(Root + "/Scenes");
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panelSettings, PanelPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(PanelPath, ImportAssetOptions.ForceUpdate);
                panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            }
            if (panelSettings == null) throw new InvalidOperationException("Dungeon PanelSettings asset could not be loaded after import.");

            var entryTree = LoadTree("DungeonEntry.uxml");
            var routeTree = LoadTree("DungeonRouteMap.uxml");
            var combatTree = LoadTree("CombatHud.uxml");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var flowObject = new GameObject("Dungeon Flow");
            var flow = flowObject.AddComponent<DungeonFlowController>();
            var localComposition = flowObject.AddComponent<LocalDungeonComposition>();
            var entryObject = CreateScreen("Dungeon Entry", entryTree, panelSettings, true);
            var routeObject = CreateScreen("Dungeon Route Map", routeTree, panelSettings, false);
            var combatObject = CreateScreen("Local Combat", combatTree, panelSettings, false);

            var entry = entryObject.AddComponent<DungeonEntryController>();
            var routeMap = routeObject.AddComponent<DungeonRouteMapController>();
            var combat = combatObject.AddComponent<CombatHudController>();
            SetReference(flow, "entry", entry);
            SetReference(flow, "routeMap", routeMap);
            SetReference(flow, "combat", combat);
            ConfigureLocalComposition(localComposition, flow);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            PatchDocumentPanelReferences();
            Debug.Log("Built isolated dungeon prototype scene at " + ScenePath + ". Assign a runtime-ready content catalog and roster to enable entry.");
        }

        private static void ConfigureLocalComposition(LocalDungeonComposition composition, DungeonFlowController flow)
        {
            var serialized = new SerializedObject(composition);
            serialized.FindProperty("flow").objectReferenceValue = flow;
            serialized.FindProperty("contentSource").objectReferenceValue = null;
            serialized.FindProperty("userId").stringValue = "local-development-user";
            var roster = serialized.FindProperty("roster");
            roster.arraySize = 4;
            for (var i = 0; i < roster.arraySize; i++)
            {
                var fighter = roster.GetArrayElementAtIndex(i);
                fighter.FindPropertyRelative("OwnedFighterId").stringValue = string.Empty;
                fighter.FindPropertyRelative("DefinitionId").stringValue = string.Empty;
                fighter.FindPropertyRelative("ConstellationTier").intValue = 0;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(composition);
        }

        private static GameObject CreateScreen(string name, VisualTreeAsset tree, PanelSettings panel, bool active)
        {
            var screen = new GameObject(name);
            screen.SetActive(active);
            var document = screen.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = tree;
            var serializedDocument = new SerializedObject(document);
            var panelProperty = serializedDocument.FindProperty("m_PanelSettings");
            if (panelProperty == null) throw new MissingFieldException(typeof(UIDocument).Name, "m_PanelSettings");
            panelProperty.objectReferenceValue = panel;
            serializedDocument.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(document);
            if (document.panelSettings != panel)
                throw new InvalidOperationException("UIDocument rejected its PanelSettings asset on " + name + ".");
            return screen;
        }

        private static VisualTreeAsset LoadTree(string name)
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root + "/UI/" + name);
            if (tree == null) throw new FileNotFoundException("Missing UI Toolkit tree: " + name);
            return tree;
        }

        private static void SetReference(UnityEngine.Object target, string propertyName, UnityEngine.Object reference)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null) throw new MissingFieldException(target.GetType().Name, propertyName);
            property.objectReferenceValue = reference;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void PatchDocumentPanelReferences()
        {
            var path = Path.GetFullPath(ScenePath);
            var yaml = File.ReadAllText(path);
            const string missingReference = "m_PanelSettings: {fileID: 0}";
            var count = yaml.Split(new[] { missingReference }, System.StringSplitOptions.None).Length - 1;
            if (count != 3) throw new InvalidDataException("Expected three UI documents in the dungeon scene; found " + count + " unresolved panel references.");
            var panelGuid = AssetDatabase.AssetPathToGUID(PanelPath);
            if (string.IsNullOrWhiteSpace(panelGuid)) throw new InvalidDataException("Dungeon PanelSettings asset has no GUID.");
            var reference = "m_PanelSettings: {fileID: 11400000, guid: " + panelGuid + ", type: 2}";
            File.WriteAllText(path, yaml.Replace(missingReference, reference));
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || !AssetDatabase.IsValidFolder(parent))
                throw new DirectoryNotFoundException("Unity asset folder parent is missing: " + parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
