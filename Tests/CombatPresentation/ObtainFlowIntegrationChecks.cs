#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FightingAllstar.Core.Run;
using FightingAllstar.Presentation.Navigation;
using FightingAllstar.Presentation.Route;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Copied only to the isolated batch project. Account saving is disabled on the in-memory fixture.
public static class ObtainFlowIntegrationChecks
{
    private const BindingFlags IP = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags SP = BindingFlags.Static | BindingFlags.NonPublic;
    private static UIDocument _document;
    private static PanelSettings _settings;

    public static IEnumerable<object> Run()
    {
        var original = PlayerInventoryService.Instance;
        var scene = EditorSceneManager.NewPreviewScene();
        var fixture = new GameObject("Obtain flow fixture inventory"); fixture.SetActive(false);
        SceneManager.MoveGameObjectToScene(fixture, scene);
        var inventory = fixture.AddComponent<PlayerInventoryService>();
        var snapshot = new PlayerInventorySnapshot { subjectId = "__obtain_memory_fixture__", diamonds = 1600, featuredGuaranteeProgress = 299 };
        var catalog = CharacterObjectRegistrySO.LoadAll();
        snapshot.characters.Add(new OwnedCharacterRecord { instanceId = "obtain-existing-starter", definitionId = catalog.First(c => c != null && c.FighterRarity == FighterRarity.R).DefinitionId });
        Set(inventory, "snapshot", snapshot); Set(inventory, "_catalog", catalog);
        Set(inventory, "_loadedSubjectId", null); // SaveCurrent returns before touching the account filesystem.
        typeof(PlayerInventoryService).GetField("_instance", SP).SetValue(null, inventory);
        var screen = new GameObject("Obtain flow fixture UI"); SceneManager.MoveGameObjectToScene(screen, scene);
        _document = screen.AddComponent<UIDocument>();
        _settings = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Project/Scenes/Fixed1920PanelSettings.asset"));
        var texture = new RenderTexture(1280, 720, 24); texture.Create(); _settings.targetTexture = texture;
        _document.panelSettings = _settings;
        _document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Project/UI/GachaBanner.uxml");
        FrontEndController front = null; DungeonRouteMapController route = null;
        var runPath = Path.GetFullPath("Temp/obtain-flow-fixture-run.json");
        var notes = new List<string>();
        try
        {
            Pump(); yield return null;
            front = screen.AddComponent<FrontEndController>();
            Set(front, "_root", _document.rootVisualElement); Set(front, "_uiInitialized", true);
            Set(front, "_banner", _document.rootVisualElement.Q("banner-screen"));
            Set(front, "_notice", _document.rootVisualElement.Q<Label>("notice"));
            var fighter = catalog.First(c => c != null && c.FighterRarity == FighterRarity.SSR);
            for (var scenario = 0; scenario < 3; scenario++)
            {
                if (scenario == 1) snapshot.FindDefinition(fighter.DefinitionId).constellationTier = 4;
                snapshot.selectorEntitlements = 1;
                Call(front, "ClaimSelector", fighter);
                var sequence = (SummonSequenceView)Get(front, "_summonSequence");
                Check(sequence != null && sequence.IsPlaying && (bool)Get(front, "_busy"), "Pity claim bypassed obtain presentation/input lock.");
                Call(front, "ClaimSelector", fighter);
                Check(snapshot.selectorEntitlements == 0 && snapshot.characters.Count == 2 && snapshot.diamonds == 1600 && snapshot.featuredGuaranteeProgress == 299,
                    "Pity presentation spent currency, changed paid progress or repeated the claim.");
                Call(sequence, "AdvancePlayback", 120f);
                Check(sequence.IsAwaitingRevealInput, "Pity SSR does not stop for input after its reveal.");
                var receipt = _document.rootVisualElement.Q<SummonRewardItemView>().Receipt;
                Check(receipt.IsNew == (scenario == 0), "Pity claim classified ownership after granting the fighter.");
                if (scenario == 1) Check(receipt.ConstellationTier == 5 && receipt.FragmentCount == 0 && snapshot.FindDefinition(fighter.DefinitionId).constellationTier == 5,
                    "Pity duplicate did not display its actual C5 advancement.");
                if (scenario == 2) Check(receipt.FragmentCount == 1 && inventory.GetCharacterDupeAmount(fighter.DefinitionId) == 1,
                    "Post-C5 pity duplicate did not display the single actual token grant.");
                Call(sequence, "AdvancePremium"); Call(sequence, "AdvancePlayback", 120f);
                Check(sequence.IsAwaitingClose && (bool)Get(front, "_busy"), "Pity claim did not retain its final obtain grid.");
                sequence.Close();
                var deadline = EditorApplication.timeSinceStartup + 3;
                while ((bool)Get(front, "_busy") && EditorApplication.timeSinceStartup < deadline) { Pump(); yield return null; }
                Check(!(bool)Get(front, "_busy"), "Pity close did not restore summon input.");
            }
            notes.Add("PASS Pity claim uses X/reveal/grid for NEW, C5 and token duplicates; repeated claim is blocked; no currency/progress change; tap holds and close restores input.");
            ((SummonSequenceView)Get(front, "_summonSequence"))?.Dispose();
            UnityEngine.Object.DestroyImmediate(front); front = null;

            route = screen.AddComponent<DungeonRouteMapController>();
            Set(route, "_document", _document); Set(route, "_store", new LocalRunStateStore(runPath));
            var run = new RunState { RunId = "obtain-flow-fixture", ProfileId = "dungeon.open-circuit" };
            for (var i = 0; i < 2; i++)
            {
                var reward = FightingAllstar.EditorTools.SummonSequencePreview.CreateItemReceipt(false);
                reward.Id = "obtain-flow-receipt-" + i;
                run.Rewards.Add(reward); snapshot.items.ClaimedReceiptIds.Add(reward.Id);
            }
            Set(route, "_run", run);
            var accountBefore = JsonUtility.ToJson(snapshot);
            Call(route, "ShowPendingRewards");
            for (var i = 0; i < 2; i++)
            {
                var sequence = (SummonSequenceView)Get(route, "_rewardSequence");
                Check(sequence != null && sequence.IsPlaying, "Dungeon reward bypasses the shared animated obtain board.");
                sequence.Skip(); Pump();
                Check(sequence.IsAwaitingClose && !sequence.IsAwaitingRevealInput, "Dungeon item receipt does not wait on its white obtain panel.");
                var artifact = _document.rootVisualElement.Q<Button>(className: "artifact-item-tile");
                typeof(Clickable).GetMethod("Invoke", IP).Invoke(artifact.clickable, new object[] { null }); Pump();
                Check(_document.rootVisualElement.Q("item-tooltip-overlay") != null && sequence.IsAwaitingClose,
                    "Dungeon artifact cannot be inspected without dismissing obtain.");
                var tooltip = _document.rootVisualElement.Q("item-tooltip-overlay");
                FightingAllstar.Presentation.ItemRewardView.CloseTooltip(_document.rootVisualElement);
                var tooltipDeadline = EditorApplication.timeSinceStartup + 3;
                while (tooltip?.panel != null && EditorApplication.timeSinceStartup < tooltipDeadline) { Pump(); yield return null; }
                if (i == 0)
                {
                    var blank = _document.rootVisualElement.panel.Pick(new Vector2(12, 360));
                    var obtain = _document.rootVisualElement.Q("summon-sequence");
                    Check(blank != null && (blank == obtain || obtain.Contains(blank)), "Obtain background does not block underlying dungeon controls: " + blank?.name);
                    using (var tap = ClickEvent.GetPooled()) { tap.target = blank; blank.SendEvent(tap); }
                }
                else sequence.Close();
                var deadline = EditorApplication.timeSinceStartup + 3;
                while (route.GetRunSnapshot().PresentedRewardIds.Count <= i && EditorApplication.timeSinceStartup < deadline) { Pump(); yield return null; }
                Check(route.GetRunSnapshot().PresentedRewardIds.Count == i + 1, "Dungeon close did not acknowledge exactly its displayed receipt.");
            }
            Call(route, "ShowPendingRewards");
            Check(Get(route, "_rewardSequence") == null, "Acknowledged dungeon rewards reopen on resume.");
            Check(JsonUtility.ToJson(snapshot) == accountBefore, "Dungeon presentation or acknowledgment granted rewards again.");
            notes.Add("PASS Dungeon grants use shared white obtain, artifact inspection works, close acknowledges each saved receipt once, no inventory grant/replay.");
            File.WriteAllLines("Artifacts/SummonSequence/ObtainFlows.txt", notes.Concat(new[] { "PASS" }));
        }
        finally
        {
            if (front != null) { ((SummonSequenceView)Get(front, "_summonSequence"))?.Dispose(); UnityEngine.Object.DestroyImmediate(front); }
            if (route != null) { ((SummonSequenceView)Get(route, "_rewardSequence"))?.Dispose(); UnityEngine.Object.DestroyImmediate(route); }
            typeof(PlayerInventoryService).GetField("_instance", SP).SetValue(null, original);
            DungeonFlowContext.Clear();
            UnityEngine.Object.DestroyImmediate(screen); UnityEngine.Object.DestroyImmediate(fixture);
            texture.Release(); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(_settings);
            EditorSceneManager.ClosePreviewScene(scene);
            foreach (var path in new[] { runPath, runPath + ".tmp", runPath + ".bak" }) if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void Pump()
    {
        typeof(PanelSettings).GetMethod("ApplyPanelSettings", IP)?.Invoke(_settings, null);
        var panel = _document.rootVisualElement.panel;
        panel?.GetType().GetMethod("Update", IP, null, Type.EmptyTypes, null)?.Invoke(panel, null);
    }
    private static object Get(object target, string field) => target.GetType().GetField(field, IP).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, IP).SetValue(target, value);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, IP).Invoke(target, args);
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
#endif
