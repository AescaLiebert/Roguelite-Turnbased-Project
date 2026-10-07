using System;
using System.IO;
using System.Linq;
using FightingAllstar.Core.Run;
using FightingAllstar.Presentation.Content;
using FightingAllstar.Presentation.Route;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools
{
    [InitializeOnLoad]
    public static class DungeonFlowValidation
    {
        private const string Request = "Temp/validate-dungeon-flow.request";
        static DungeonFlowValidation()
        {
            EditorApplication.update += ProcessValidationRequest;
        }
        private static void ProcessValidationRequest()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Request)) return;
            File.Delete(Request);
            Validate();
        }
        [MenuItem("Fighting Allstar/Validate Dungeon Flow")]
        public static void Validate()
        {
            try
            {
                CharacterRegistrySync.Sync();
                var catalog = CharacterObjectCatalogBuilder.Load(null);
                var errors = FightingAllstar.Core.Content.ContentValidator.Validate(catalog);
                if (errors.Count > 0) throw new Exception("Combat content is invalid: " + string.Join("; ", errors));
                var profile = DungeonProfile.Filtered("series.kof", 1, 0);
                var nodes = RouteGenerator.Generate(profile, catalog.Characters, 0, 831);
                if (nodes.Where(n => n.EnemyTeamSnapshot.Count > 0).Any(n => n.EnemyTeamSnapshot.Any(f => !profile.Accepts(f.Definition))))
                    throw new Exception("Enemy category mismatch.");
                CheckTree("MainMenu", "battle", "summon", "hero-art");
                CheckTree("GachaBanner", "banner-screen", "result-screen", "result-cards", "summon-one", "summon-ten");
                CheckTree("DungeonEntry", "series-cards", "configuration-overlay", "phase-filter", "subphase-filter", "start-run-button");
                CheckTree("DungeonRouteMap", "route-rows", "node-actions", "abandon-button");
                CheckStaleScrollTarget(nodes);
                CheckRunSnapshot(nodes);
                var report = "PASS: Authored CharacterObjects loaded: " + catalog.Characters.Count +
                    "; combat validation passed; KOF 10XX candidates: " + catalog.Characters.Count(profile.Accepts) +
                    "; seeded route nodes: " + nodes.Count + "; all four UI trees contain required controls; stale map scroll targets safely rejected; authored enemy snapshots survive save/reload.";
                File.WriteAllText("Temp/dungeon-flow-validation.txt", report);
                Debug.Log(report);
            }
            catch (Exception ex)
            {
                File.WriteAllText("Temp/dungeon-flow-validation.txt", "FAIL: " + ex);
                Debug.LogException(ex);
            }
        }
        private static void CheckTree(string name, params string[] controls)
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Project/UI/" + name + ".uxml");
            if (tree == null) throw new Exception(name + " UXML failed to import.");
            var root = tree.CloneTree();
            foreach (var control in controls)
                if (root.Q(control) == null) throw new Exception(name + " is missing " + control);
        }
        private static void CheckRunSnapshot(System.Collections.Generic.List<RouteNodeState> nodes)
        {
            var run = new RunState { RunId = "validation", Seed = 831, Nodes = nodes,
                Formation = new System.Collections.Generic.List<string> { "owned-a", "owned-b", "owned-c", "owned-d" } };
            var json = FightingAllstar.Core.Content.SnapshotJson.Serialize(run);
            var restored = FightingAllstar.Core.Content.SnapshotJson.Deserialize<RunState>(json);
            if (FightingAllstar.Core.Content.SnapshotJson.Serialize(restored) != json)
                throw new Exception("Authored run snapshot changed after JSON round-trip.");
        }
        private static void CheckStaleScrollTarget(System.Collections.Generic.List<RouteNodeState> nodes)
        {
            var run = new RunState { Nodes = nodes, CurrentNodeId = "r0n0" };
            var scroll = new ScrollView();
            var oldBoard = new LabyrinthBoard(run, _ => { });
            scroll.Add(oldBoard);
            if (!scroll.contentContainer.Contains(oldBoard.CurrentTile)) throw new Exception("Current tile is not inside map content.");
            // Reproduce OnEnable followed by SetRun in the same frame.
            scroll.Clear();
            var replacement = new LabyrinthBoard(run, _ => { });
            scroll.Add(replacement);
            if (DungeonRouteMapController.TryScrollToCurrentTile(scroll, oldBoard))
                throw new Exception("Removed map accepted as a scroll target.");
            scroll.Clear();
            if (DungeonRouteMapController.TryScrollToCurrentTile(scroll, replacement))
                throw new Exception("Disabled map accepted as a scroll target.");
        }
    }
}
