using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;
using FightingAllstar.Presentation.Loadout;
using FightingAllstar.Presentation.Route;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools
{
    /// <summary>Isolated real UI rendering. Does not save accounts, runs or scenes.</summary>
    [InitializeOnLoad]
    public static class DungeonEnemyPreviewChecks
    {
        private static readonly Type Preview = typeof(ScreenLayoutPreview);
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string Folder = "Artifacts/DungeonEnemyRefactor";
        private static readonly Vector2Int[] Sizes = { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(768, 1024) };
        private static int _index, _assertions;
        private static double _next;
        private static bool _running;
        private static DungeonProfile _originalProfile;
        private static RunState _originalRun;
        private static EncounterProjection _originalEncounter;
        private static readonly List<string> Results = new List<string>();
        static DungeonEnemyPreviewChecks() { EditorApplication.update += Tick; }
        private static object Field(string name) => Preview.GetField(name, StaticPrivate).GetValue(null);
        private static void SetField(string name, object value) => Preview.GetField(name, StaticPrivate).SetValue(null, value);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        private static object PreviewCall(string name, params object[] args) => Preview.GetMethod(name, StaticPrivate).Invoke(null, args);
        private static void Check(bool condition, string message) { _assertions++; if (!condition) throw new Exception(message); }

        [MenuItem("Fighting Allstar/Validation/Dungeon Enemies and Loadout")]
        public static void Run()
        {
            if (UiPreviewSession.IsBusy || EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            _originalProfile = DungeonFlowContext.ActiveProfile; _originalRun = DungeonFlowContext.ActiveRun;
            _originalEncounter = DungeonFlowContext.PendingEncounter;
            Directory.CreateDirectory(Folder); Results.Clear(); _index = _assertions = 0;
            try
            {
                PreviewCall("Start", false);
                SetField("_running", false); // This fixture owns validation instead of the broader legacy checks.
                SetField("Screens", new[] { "DungeonEntry", "CharacterLoadout" });
                SetField("Folder", Folder); _running = true; Build();
            }
            catch (Exception ex) { Finish(ex); }
        }
        private static void Build()
        {
            DungeonFlowContext.Clear(); SetField("_index", _index); PreviewCall("Build");
            _next = EditorApplication.timeSinceStartup + .6;
        }
        private static void Capture(Vector2Int size, string name) => PreviewCall("Render", size, name, true);
        private static void Tick()
        {
            if (!_running || EditorApplication.timeSinceStartup < _next) return;
            try
            {
                var size = Sizes[_index / 2];
                var screen = (GameObject)Field("_screen");
                var root = ((UIDocument)Field("_document")).rootVisualElement;
                var inventory = (PlayerInventoryService)Field("_inventory");
                if (_index % 2 == 0)
                {
                    var entry = screen.GetComponent<DungeonEntryController>();
                    entry.SelectDungeonStage("soul-calibur");
                    var phase = root.Q<DropdownField>("phase-filter"); var sub = root.Q<DropdownField>("subphase-filter");
                    Check(!phase.choices.Contains("WIP-Phase") && phase.value == "Pre-Release", "Soul Calibur retained unavailable phases.");
                    Check(sub.choices.SequenceEqual(new[] { "Late", "META" }) && sub.value == "Late", "Soul Calibur retained unavailable sub-phases.");
                    var before = JsonUtility.ToJson(inventory.Snapshot);
                    for (var level = 1; level <= 5; level++)
                    {
                        root.Q<SliderInt>("difficulty-slider").value = level;
                        Check(root.Q<Label>("recommended-combat-class").text.StartsWith("RECOMMENDED TEAM CC  ") &&
                            !root.Q<Label>("recommended-combat-class").text.EndsWith("—"), "Missing CC recommendation at difficulty " + level);
                    }
                    Capture(size, "SoulCaliburNightmare");
                    root.Q<SliderInt>("difficulty-slider").value = 1; Capture(size, "SoulCaliburEasy");
                    var recommendation = root.Q("recommended-combat-class").worldBound;
                    Check(recommendation.height > 0 && recommendation.xMax <= size.x + 1 && recommendation.yMax <= size.y + 1, "Recommended CC is clipped.");
                    Check(root.Q("start-run-button").worldBound.yMax <= size.y + 1, "Enter Dungeon is clipped.");
                    Check(JsonUtility.ToJson(inventory.Snapshot) == before, "Filter/difficulty inspection changed the account.");
                    Results.Add("PASS: Soul Calibur release cutoff, all five CC recommendations, entry bounds and read-only inspection at " + size);
                }
                else
                {
                    var manager = screen.GetComponent<CharacterSelectionManager>();
                    var slots = (List<CharacterSlot>)typeof(CharacterSelectionManager).GetField("characterSlots", Private).GetValue(manager);
                    var first = slots[0].AssignedCharacter;
                    inventory.FindOwnedByDefinition(first.DefinitionId).level = 50;
                    inventory.FindOwnedByDefinition(first.DefinitionId).levelCap = 50;
                    var upgraded = inventory.GetCharacterStats(first);
                    var artifact = ArtifactRules.Generate("loadout-fixture-artifact", ArtifactRarity.SSR, 4);
                    artifact.Level = 20; ArtifactRules.Normalize(artifact);
                    var frozenStats = ArtifactRules.ApplyStats(upgraded, new[] { artifact });
                    var run = new RunState { RunId = "loadout-preview", CurrentNodeId = "encounter" };
                    run.Roster.Add(new RunFighterState { DefinitionId = first.DefinitionId, RunFighterId = "fixture-" + first.DefinitionId,
                        Stats = frozenStats, CurrentHealth = frozenStats.MaxHealth * 3 / 10, ArtifactLoadout = new List<ArtifactInstance> { artifact } });
                    var node = new RouteNodeState { Id = "encounter", Type = RouteNodeType.Battle };
                    node.EnemyTeamSnapshot.Add(new EncounterFighterSnapshot { Stats = new StatBlock() }); run.Nodes.Add(node);
                    DungeonFlowContext.BeginDungeonFlowForEncounter(DungeonProfile.OpenCircuit(), run, null);
                    var team = LoadoutStats.TeamCombatClass(slots.Select(s => s.AssignedCharacter));
                    DungeonFlowContext.ActiveRun.FindNode("encounter").EnemyTeamSnapshot[0].Stats.CombatClass = checked((int)team + 1);
                    var view = screen.GetComponent<CharacterLoadoutView>(); Call(view, "Refresh");
                    var cc = root.Q<Label>("formation-stats");
                    Check(cc.ClassListContains("cc-second") && cc.text == LoadoutStats.Comparison(team, team + 1), "Lower CC must be red and compare the frozen enemy team.");
                    var slot = root.Q("formation-position-0");
                    var text = slot.Q<Label>(className: "formation-character-stats").text;
                    Check(text.Contains("HP " + frozenStats.MaxHealth.ToString("N0")) && !text.Contains(" / ") &&
                        text.Contains("ATK " + frozenStats.Attack.ToString("N0")) && text.Contains("DEF " + frozenStats.Defense.ToString("N0")), "Loadout uses base stats or current/max HP text.");
                    var fill = slot.Q(className: "formation-health-fill");
                    Check(Math.Abs(fill.style.width.value.value - 30) < .1f, "Run injury is not represented by the health fill.");
                    Capture(size, "LoadoutEnemyFirst");
                    DungeonFlowContext.ActiveRun.FindNode("encounter").EnemyTeamSnapshot[0].Stats.CombatClass = checked((int)team);
                    Call(view, "Refresh");
                    Check(cc.ClassListContains("cc-first") && !cc.ClassListContains("cc-second"), "Equal CC must be green, matching battle tie behavior.");
                    Capture(size, "LoadoutPlayerFirst");
                    var playBounds = root.Q("loadout-play").worldBound;
                    Check(root.Q("formation-health-0").worldBound.height > 0 && playBounds.yMax <= size.y + 1 &&
                        playBounds.xMin >= 0 && playBounds.xMax <= size.x + 1, "Health bar or battle entry is clipped.");
                    // Verify the serialized uGUI component's automatic filled Image fallback as well.
                    var legacy = slots[0].LoadOutPosition;
                    var hp = new GameObject("Fixture HP", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI)); hp.transform.SetParent(legacy.transform, false);
                    typeof(CharacterLoadOut).GetField("hpText", Private).SetValue(legacy, hp.GetComponent<TMPro.TextMeshProUGUI>());
                    legacy.DisplayCharacterInfo(first);
                    var image = (UnityEngine.UI.Image)typeof(CharacterLoadOut).GetField("healthBarFill", Private).GetValue(legacy);
                    Check(image != null && image.type == UnityEngine.UI.Image.Type.Filled && Math.Abs(image.fillAmount - .3f) < .001f &&
                        hp.GetComponent<TMPro.TextMeshProUGUI>().text == frozenStats.MaxHealth.ToString("N0"), "Legacy loadout does not use max HP and a filled image.");
                    Results.Add("PASS: upgraded/frozen stats, injury fill, red/green CC including ties, uGUI fill fallback and layout at " + size);
                }
                _index++; if (_index < Sizes.Length * 2) Build(); else Finish(null);
            }
            catch (Exception ex) { Finish(ex); }
        }
        private static void Finish(Exception failure)
        {
            _running = false;
            Results.Add(failure == null ? "PASS: " + _assertions + " assertions. No account, run or scene saved." : "FAIL: " + failure);
            File.WriteAllLines(Folder + "/Verification.txt", Results);
            PreviewCall("Cleanup");
            if (_originalProfile != null) DungeonFlowContext.BeginDungeonFlowForEncounter(_originalProfile, _originalRun, _originalEncounter);
            else DungeonFlowContext.Clear();
            if (failure != null) Debug.LogException(failure); else Debug.Log("Dungeon enemy/loadout UI verification passed: " + _assertions);
        }
    }
}
