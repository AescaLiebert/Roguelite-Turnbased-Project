#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Run;
using FightingAllstar.Presentation.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class GenshinPracticeChecks
{
    static GenshinPracticeChecks() { EditorApplication.update += Tick; }
    public static void Run()
    {
        // This runner is copied into a disposable project; isolate its automatic inventory bootstrap too.
        PlayerSettings.companyName = "FightingAllstarTests"; PlayerSettings.productName = "GenshinPracticeChecks";
        SessionState.SetBool("GenshinPracticeChecks", true); EditorApplication.isPlaying = true;
    }
    private static void Tick()
    {
        if (!SessionState.GetBool("GenshinPracticeChecks", false) || !EditorApplication.isPlaying) return;
        SessionState.SetBool("GenshinPracticeChecks", false);
        new GameObject("Practice checks").AddComponent<GenshinPracticeRunner>();
    }
}

public sealed class GenshinPracticeRunner : MonoBehaviour
{
    private int checks;
    private void Require(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    private static void Click(Button button) => typeof(Clickable).GetMethod("SimulateSingleClick", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
        .Invoke(button.clickable, new object[] { null, 0 });
    private IEnumerator Start()
    {
        var run = Run();
        while (true)
        {
            object next;
            try { if (!run.MoveNext()) break; next = run.Current; }
            catch (Exception error) { Debug.LogError(error); EditorApplication.Exit(1); yield break; }
            yield return next;
        }
    }
    private IEnumerator Run()
    {
        var output = Path.Combine(Directory.GetCurrentDirectory(), "Artifacts/GenshinPractice"); Directory.CreateDirectory(output);
        var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>(); camera.tag = "MainCamera";
        camera.backgroundColor = new Color(.03f, .05f, .09f); camera.clearFlags = CameraClearFlags.SolidColor;
        new GameObject("Key Light", typeof(Light)).GetComponent<Light>().type = LightType.Directional;
        var documentObject = new GameObject("Practice UI"); documentObject.SetActive(false);
        var doc = documentObject.AddComponent<UIDocument>();
        doc.panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        doc.panelSettings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss");
        Require(doc.panelSettings.themeStyleSheet != null, "Practice fixture did not load the real runtime theme.");
        doc.panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
        doc.panelSettings.targetTexture = new RenderTexture(1280, 720, 24);
        documentObject.SetActive(true);
        var root = doc.rootVisualElement;
        root.style.position = Position.Absolute; root.style.left = root.style.right = root.style.top = root.style.bottom = 0;
        root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Project/UI/MainMenu.uss"));
        TrainingCharacterPicker.Show(root);
        yield return null; yield return null;
        Require(root.Query<Button>(className: "training-character").ToList().Count == CharacterObjectRegistrySO.LoadAll().Length,
            "Practice picker excluded authored characters.");
        Require(root.Q<Button>("training-genshin") == null, "Removed Genshin shortcut is still visible.");
        root.Q<DropdownField>("training-series").value = "Genshin Impact";
        yield return null; yield return null;
        var buttons = root.Query<Button>(className: "training-character").ToList();
        Require(buttons.Count == 5 && buttons.All(b => b.enabledSelf && b.name.Contains("fighter.")), "Series selection did not show five ready Genshin fighters.");
        Require(root.Q<DropdownField>("training-series").value == "Genshin Impact", "Series filter leaked its internal ID.");
        Require(root.Q<DropdownField>("training-tier").choices.Last() == "C5", "Practice offered an unauthored C6 tier.");
        Require(root.Q<ScrollView>("training-scroll").verticalScrollerVisibility == ScrollerVisibility.Auto, "Practice hid its scrolling affordance.");
        Require(root.Q<Label>("training-title").resolvedStyle.height > 0 && root.Q<DropdownField>("training-series").resolvedStyle.height > 0,
            "Practice title or filters had no visible layout.");
        FightingAllstar.EditorTools.BattlePreviewCapture.Save(Path.Combine(output, "01-genshin-picker.png"), doc);
        var inventoryBefore = JsonUtility.ToJson(PlayerInventoryService.Instance.Snapshot);
        var fullCatalog = FightingAllstar.Presentation.Content.CharacterObjectCatalogBuilder.Load(null);
        var phase = DungeonAvailability.Phases("series.kof", fullCatalog.Characters).First();
        var subPhase = DungeonAvailability.SubPhases("series.kof", phase, fullCatalog.Characters).First();
        var profile = DungeonProfile.Filtered("series.kof", phase, subPhase);
        Require(DungeonEnemyFactory.RecommendedCombatClass(profile, fullCatalog.Characters, 0) > 0,
            "Dungeon entry preview rejected the full saved character catalog.");
        Require(RouteGenerator.Generate(profile, fullCatalog.Characters, 0, 777).Any(n => n.Type == RouteNodeType.Boss),
            "Dungeon generation rejected the full saved character catalog.");
        foreach (var character in CharacterObjectRegistrySO.LoadAll().Where(c => c.SeriesId == "series.genshin-impact"))
        {
            Require(buttons.Any(b => b.name == "training-fighter:" + character.DefinitionId), "Practice omitted " + character.DefinitionId);
            foreach (var tier in new[] { 0, 5, 6 })
            {
                TrainingContext.Prepare(character, tier);
                Require(TrainingContext.TryCreateEncounter(out var encounter, out var seed), "Practice context failed for " + character.DefinitionId);
                Require(encounter.PlayerTeam[0].ConstellationTier == Math.Min(5, tier), "Practice did not clamp to authored ultimate tiers.");
                var battle = RunBattleBridge.CreateLocalBattle(encounter, seed, training: true);
                Require(battle.IsTraining && battle.Player.Hand.Count == 7 && battle.ActionBudget == 1,
                    "Practice failed to deal every rank/ultimate for " + character.DefinitionId);
                foreach (var card in battle.Player.Hand)
                {
                    var session = new LocalBattleSession(battle);
                    var draft = new PlanDraft(session.GetSnapshot());
                    Require(draft.QueuePlay(card.Id, battle.Opponent.Fighters[0].Id, out var error), "Practice card failed: " + error);
                    Require(session.Submit(draft.BuildPlan("practice:" + character.DefinitionId + ":" + tier + ":" + card.Id), out error), "Practice turn failed: " + error);
                    Require(session.GetSnapshot().Player.Fighters[0].Health == battle.Player.Fighters[0].Health,
                        "Practice lost its infinite-health rule.");
                }
                TrainingContext.Clear(); Require(!TrainingContext.IsReady, "Practice exit retained its disposable context.");
            }
        }
        Require(JsonUtility.ToJson(PlayerInventoryService.Instance.Snapshot) == inventoryBefore, "Practice changed inventory or currency.");
        Click(root.Q<Button>("training-all")); yield return null;
        Require(root.Query<Button>(className: "training-character").ToList().Count == CharacterObjectRegistrySO.LoadAll().Length, "All Fighters did not restore the full roster.");
        root.Q<TextField>("training-search").value = "Barbara"; yield return null;
        Require(root.Query<Button>(className: "training-character").ToList().Count == 1, "Practice search did not find Barbara.");
        FightingAllstar.EditorTools.BattlePreviewCapture.Save(Path.Combine(output, "02-barbara-search.png"), doc);
        File.WriteAllText(Path.Combine(output, "result.txt"), "PASS: " + checks + " Practice picker/context/card/isolation checks; all five Genshin fighters, C0/C5 and C6-clamp, 105 authored card plays.");
        EditorApplication.Exit(0);
    }
}
#endif
