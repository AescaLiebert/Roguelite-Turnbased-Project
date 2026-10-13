using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FightingAllstar.Core.Run;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation;
using FightingAllstar.Presentation.Content;
using FightingAllstar.Presentation.Loadout;
using FightingAllstar.Presentation.Navigation;
using FightingAllstar.Presentation.Route;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools
{
    internal static class UiPreviewSession
    {
        public static bool IsBusy { get; private set; }
        public static void Acquire() { IsBusy = true; }
        public static void Release() { IsBusy = false; }
    }
    /// <summary>Real UI Toolkit rendering with isolated ownership; never persists account, formation, or run data.</summary>
    [InitializeOnLoad]
    public static class ScreenLayoutPreview
    {
        private const string Request = "Temp/preview-screen-layouts.request";
        private const string DungeonRequest = "Temp/preview-dungeon-rewards.request";
        private const string InventoryRequest = "Temp/preview-character-inventory.request";
        private const string HubRequest = "Temp/preview-character-hub.request";
        private const string MenuRequest = "Temp/preview-mainmenu-refactor.request";
        private static string Folder = "Artifacts/ScreenLayouts";
        private static readonly string[] DefaultScreens = { "MainMenu", "GachaBanner", "DungeonEntry", "DungeonRouteMap", "CharacterLoadout" };
        private static string[] Screens = DefaultScreens;
        private static readonly Vector2Int[] Sizes = { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(768, 1024) };
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Scene _scene;
        private static PlayerInventoryService _original, _inventory;
        private static UIDocument _document;
        private static GameObject _screen;
        private static PanelSettings _settings;
        private static RenderTexture _target;
        private static int _index;
        private static double _next;
        private static bool _running;
        private static readonly List<string> Results = new List<string>();
        private static bool _dungeonOnly;
        private static bool _hubOnly;
        private static bool _menuRefactor;

        static ScreenLayoutPreview()
        {
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += () => { if (_running) Cleanup(); };
        }

        [MenuItem("Fighting Allstar/Validate Shared Screen Layouts")]
        public static void Run() => Start(false);

        [MenuItem("Fighting Allstar/Validate Character Inventory")]
        public static void RunInventory() => Start(true);

        [MenuItem("Fighting Allstar/Validate Character Hub")]
        public static void RunHub()
        {
            if (_running || UiPreviewSession.IsBusy || EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            _hubOnly = true; Start(false);
        }

        [MenuItem("Fighting Allstar/Validate Dungeon Inspection and Rewards")]
        public static void RunDungeonRewards() { _dungeonOnly = true; Start(false); }

        [MenuItem("Fighting Allstar/Validate Main Menu and Backpack")]
        public static void RunMenuRefactor() { _menuRefactor = true; Start(false); }

        private static void Start(bool inventoryOnly)
        {
            if (_running || UiPreviewSession.IsBusy || EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            Folder = _menuRefactor ? "Artifacts/MainMenuRefactor" : _hubOnly ? "Artifacts/CharacterHub" : _dungeonOnly ? "Artifacts/DungeonRewards" : inventoryOnly ? "Artifacts/CharacterInventory" : "Artifacts/ScreenLayouts";
            Screens = _menuRefactor ? new[] { "MainMenu", "DungeonEntry", "DungeonRouteMap" } : _hubOnly ? new[] { "CharacterHubKit", "CharacterHubUpgrade", "CharacterHubConstellation", "CharacterHubArtifacts" } : _dungeonOnly ? new[] { "DungeonEntry", "DungeonRouteMap", "CharacterLoadout" } : inventoryOnly ? new[] { "MainMenu", "CharacterInventory" } : DefaultScreens;
            Directory.CreateDirectory(Folder); Results.Clear(); _index = 0; _running = true;
            UiPreviewSession.Acquire();
            _original = PlayerInventoryService.Instance;
            try
            {
                _scene = EditorSceneManager.NewPreviewScene();
                var inventoryObject = new GameObject("Screen layout fixture inventory");
                SceneManager.MoveGameObjectToScene(inventoryObject, _scene); inventoryObject.SetActive(false);
                _inventory = inventoryObject.AddComponent<PlayerInventoryService>();
                var assets = CharacterObjectRegistrySO.LoadAll();
                var snapshot = new PlayerInventorySnapshot { subjectId = "guest:__screen-layout-preview__", diamonds = 1600 };
                if (_menuRefactor)
                {
                    snapshot.items.Coins = 123456;
                    snapshot.characterDupeTokens.Add(new CharacterDupeTokenStack { characterDefinitionId = "fighter.kyo94", amount = 12 });
                    foreach (var item in ItemCatalog.UpgradeMaterials())
                        snapshot.items.Materials.Add(new ItemAmount { ItemId = item.Id, Amount = 24 });
                    snapshot.items.Materials.Add(new ItemAmount { ItemId = ItemCatalog.SeriesId("series.kof"), Amount = 38 });
                    snapshot.items.Materials.Add(new ItemAmount { ItemId = ItemCatalog.SeriesId("series.kof", true), Amount = 4 });
                    foreach (ArtifactRarity rarity in Enum.GetValues(typeof(ArtifactRarity)))
                        snapshot.items.Artifacts.Add(new ArtifactInstance { InstanceId = "fixture-" + rarity, ItemId = ItemCatalog.ArtifactId(rarity), Rarity = rarity });
                }
                foreach (var asset in assets.Where(c => c != null && c.RuntimeReady))
                    snapshot.characters.Add(new OwnedCharacterRecord { definitionId = asset.DefinitionId, instanceId = "fixture-" + asset.DefinitionId });
                Set(_inventory, "snapshot", snapshot); Set(_inventory, "_catalog", assets); Set(_inventory, "_loadedSubjectId", snapshot.subjectId);
                InventoryInstance(_inventory); Build();
            }
            catch (Exception ex) { Fail(ex); }
        }

        private static void Build()
        {
            DisposeScreen();
            var size = Sizes[_index / Screens.Length]; var name = Screens[_index % Screens.Length];
            _settings = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Project/Scenes/Fixed1920PanelSettings.asset"));
            _target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32); _target.Create(); _settings.targetTexture = _target;
            _screen = new GameObject("Preview " + name); SceneManager.MoveGameObjectToScene(_screen, _scene);
            _document = _screen.AddComponent<UIDocument>(); _document.panelSettings = _settings;
            _document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Project/UI/" + (name == "CharacterInventory" || _hubOnly ? "MainMenu" : name) + ".uxml");
            if (name == "MainMenu" || name == "GachaBanner" || name == "CharacterInventory" || _hubOnly)
            {
                var controller = _screen.AddComponent<FrontEndController>();
                Set(controller, "bannerConfiguration", AssetDatabase.LoadAssetAtPath<SummonBannerConfigurationSO>("Assets/Project/Data/Economy/KofBannerConfiguration.asset"));
                Call(controller, "OnEnable"); Call(controller, "InitializeUI");
                if (name == "CharacterInventory")
                {
                    CharacterInventoryView.Show(_document.rootVisualElement);
                    _document.rootVisualElement.Q("character-inventory").RemoveFromClassList("animated-panel");
                    _document.rootVisualElement.Q("character-inventory").RemoveFromClassList("panel-enter");
                }
                if (_hubOnly)
                {
                    CharacterInventoryView.Show(_document.rootVisualElement);
                    var root = _document.rootVisualElement;
                    root.Q<TextField>("inventory-search").value = _inventory.GetOwnedDefinitions().First(c => c.DefinitionId == "fighter.kyo94").FighterName;
                    Call(root.Q("character-inventory").userData, "Select", _inventory.GetOwnedDefinitions().First(c => c.DefinitionId == "fighter.kyo94"));
                    Call(root.Q("character-hub").userData, "SelectTab", name.Substring("CharacterHub".Length).ToLowerInvariant());
                    SettleHub(root);
                }
            }
            else if (name == "DungeonEntry")
            {
                var catalog = CharacterObjectCatalogBuilder.Load(null);
                var controller = _screen.AddComponent<DungeonEntryController>(); Call(controller, "OnEnable");
                controller.Bind(_inventory.SubjectId, catalog.ContentVersion, catalog.ContentHash, 831, catalog.Characters, null);
                Call(controller, "InitializeUI");
            }
            else if (name == "DungeonRouteMap")
            {
                var catalog = CharacterObjectCatalogBuilder.Load(null);
                var run = DungeonRunEngine.CreateRun("screen-preview", _inventory.SubjectId, "preview-start",
                    DungeonFlowContext.CreateEntryProfile("series.kof", 1, 0, false), 25, 831,
                    catalog.ContentVersion, catalog.ContentHash, new RunFighterSeed[0], catalog.Characters, 320, true);
                var first = run.Nodes.First(n => n.Progress == RouteNodeProgress.Reachable && n.Type == RouteNodeType.Battle);
                if (!DungeonRunEngine.TryChooseNode(run, first.Id, run.Revision, "preview-node", out var selected, out var error)) throw new Exception(error);
                var controller = _screen.AddComponent<DungeonRouteMapController>();
                Set(controller, "_document", _document); Set(controller, "_run", run); Call(controller, "OnEnable");
                controller.InspectNode(first.Id);
            }
            else
            {
                var manager = _screen.AddComponent<CharacterSelectionManager>();
                Set(manager, "_isInitialized", true);
                var owned = _inventory.GetOwnedDefinitions().Take(4).ToList();
                var slots = new List<CharacterSlot>();
                for (var i = 0; i < 4; i++)
                {
                    var go = new GameObject("Fixture formation " + i); go.transform.SetParent(_screen.transform); go.SetActive(false);
                    slots.Add(new CharacterSlot { SlotName = "Slot " + i, LoadOutPosition = go.AddComponent<CharacterLoadOut>(), AssignedCharacter = owned[i] });
                }
                Set(manager, "characterSlots", slots);
                var controller = _screen.AddComponent<CharacterLoadoutView>(); Call(controller, "OnEnable"); Call(controller, "InitializeUI");
            }
            _next = EditorApplication.timeSinceStartup + .5;
        }

        private static void Tick()
        {
            if (!_running && !UiPreviewSession.IsBusy && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlaying && File.Exists(DungeonRequest))
            { File.Delete(DungeonRequest); RunDungeonRewards(); return; }
            if (!_running)
            {
                if (!UiPreviewSession.IsBusy && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlaying && File.Exists(MenuRequest))
                { File.Delete(MenuRequest); RunMenuRefactor(); return; }
                if (!UiPreviewSession.IsBusy && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlaying && File.Exists(HubRequest))
                { File.Delete(HubRequest); RunHub(); return; }
                if (!UiPreviewSession.IsBusy && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlaying && File.Exists(InventoryRequest))
                { File.Delete(InventoryRequest); RunInventory(); return; }
                if (!UiPreviewSession.IsBusy && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlaying && File.Exists(Request))
                { File.Delete(Request); Run(); }
                return;
            }
            if (EditorApplication.timeSinceStartup < _next) return;
            try
            {
                var size = Sizes[_index / Screens.Length]; var name = Screens[_index % Screens.Length];
                Render(size, name);
                var root = _document.rootVisualElement;
                var header = root.Q("account-header");
                Check(header != null && Math.Abs(header.worldBound.height - 84) < 1, "Header height changed across screens.");
                Check(Math.Abs(header.worldBound.width - size.x) < 1 && Math.Abs(header.worldBound.y) < 1, "Header is not docked to the viewport.");
                Check(root.Q<Label>("account-wallet").text == "1,600", "Header balance differs from the account.");
                _inventory.Snapshot.diamonds = 1234567;
                var signal = (Action)typeof(PlayerInventoryService).GetField("InventoryChanged", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                signal?.Invoke();
                Check(root.Q<Label>("account-wallet").text == "1,234,567", "Shared header did not refresh on inventory change.");
                _inventory.Snapshot.diamonds = 1600; signal?.Invoke();
                var body = _hubOnly ? root.Q(className: "hub-page") : name == "CharacterInventory" ? root.Q(className: "inventory-page") : root.Q(className: "screen-body");
                Check(body != null && body.worldBound.height > 0 && body.worldBound.yMax <= size.y + 1, "Screen body exceeds the viewport.");
                foreach (var scroll in root.Query<ScrollView>().ToList())
                    Check(scroll.horizontalScrollerVisibility == ScrollerVisibility.Hidden && scroll.verticalScrollerVisibility == ScrollerVisibility.Hidden,
                        "A ScrollView still shows a scrollbar.");
                if (name == "MainMenu")
                {
                    var open = root.Q<Button>("character-inventory-open");
                    Check(open != null && open.worldBound.xMax <= size.x && open.worldBound.yMax <= size.y, "Character inventory entry exceeds the viewport.");
                    if (_menuRefactor) CheckMenu(root, size);
                }
                if (name == "CharacterInventory") CheckInventory(root, size);
                if (_hubOnly) CheckHub(root, size, name);
                if (name == "DungeonEntry")
                {
                    if (_menuRefactor) CheckDifficulty(root, size);
                    Check(root.Q<ScrollView>("dungeon-stage-scroll") != null && root.Q<ScrollView>("dungeon-detail-scroll") == null, "Dungeon preview still scrolls.");
                    Check(root.Q("stage-filter").Contains(root.Q("phase-filter")) && root.Q("stage-filter").Contains(root.Q("subphase-filter")), "Phase controls are outside the selected dungeon stage filter.");
                    Check(root.Q("series-filter") == null, "Global dungeon series dropdown still exists.");
                    Check(root.Q("series-cards").Children().OfType<Button>().Select(c => c.name).SequenceEqual(
                        new[] { "open-circuit", "kof", "tekken", "soul-calibur", "genshin-impact" }), "Dungeon series cards are missing or out of order.");
                    Check(root.Q("dungeon-stage-preview").Q("character-icon") == null && root.Q("enemy-preview-grid") == null, "Full character catalog leaked into Stage Info.");
                    Check(root.Q("start-run-button").worldBound.yMax <= size.y, "Enter dungeon is outside the viewport.");
                    var entry = _screen.GetComponent<DungeonEntryController>();
                    Call(entry, "OpenPreview");
                    root.Q("configuration-overlay").RemoveFromClassList("animated-panel");
                    root.Q("configuration-overlay").RemoveFromClassList("panel-enter");
                    Render(size, "DungeonPreview");
                    CheckGridBounds(root.Q("dungeon-preview-grid"));
                    Check(root.Q("dungeon-preview-grid").Q("character-icon").worldBound.width >= 32, "Dungeon Preview icons became too small to read.");
                    Check(root.Q("dungeon-reward-grid").Query(className: "item-premium-frame").ToList().Count == 2, "Both currencies must have gold reward frames at Easy difficulty.");
                    Check(root.Q("dungeon-reward-grid").Query(className: "item-reward-amount").ToList().Count == 0, "Dungeon preview leaks reward quantities.");
                    Call(entry, "CloseConfiguration");
                }
                if (name == "GachaBanner") Check(root.Q("gacha-preview-grid").Q("character-icon") != null, "Banner preview does not use CharacterIcon.");
                if (name == "DungeonRouteMap")
                {
                    Check(root.Q("enemy-info-grid")?.Q("character-icon") != null, "Stage detail does not use CharacterIcon.");
                    Check(root.Q<ScrollView>("stage-detail-scroll") != null, "Full stage details must be scrollable.");
                    CheckGridBounds(root.Q("enemy-info-grid"));
                    Check(root.Q(className: "prepare-team-button").worldBound.yMax <= size.y, "Prepare team is outside the viewport.");
                    var controller = _screen.GetComponent<DungeonRouteMapController>();
                    var original = controller.GetRunSnapshot(); var serialized = SnapshotJson.Serialize(original);
                    foreach (var node in original.Nodes) controller.InspectNode(node.Id);
                    Check(SnapshotJson.Serialize(controller.GetRunSnapshot()) == serialized, "Inspecting all platforms changed the run.");
                    controller.InspectNode(original.Nodes.First(n => n.Type == RouteNodeType.Boss).Id);
                    Check(!root.Q<Button>("enter-stage-button").enabledSelf, "Locked boss inspection enabled entry.");
                    controller.InspectNode(original.Nodes.First(n => n.Progress == RouteNodeProgress.Reachable).Id);
                    Check(root.Q<Button>("enter-stage-button").enabledSelf, "Reachable stage cannot be entered.");
                    Check(root.Q("stage-reward-grid").Query(className: "item-reward-amount").ToList().Count == 0, "Stage preview leaks reward quantities.");
                    ItemRewardView.Show(root, ItemCatalog.Find(ItemCatalog.PhaseId(1)));
                    Check(root.Q<Label>("item-tooltip-name").text == ItemCatalog.Find(ItemCatalog.PhaseId(1)).Name && root.Q("item-tooltip-amount") != null && root.Q("item-tooltip-description") != null,
                        "Item tooltip lacks name, amount or description.");
                    Check(SnapshotJson.Serialize(controller.GetRunSnapshot()) == serialized, "Item tooltip committed the route.");
                    Render(size, "ItemTooltip");
                    var itemDialog = root.Q("item-tooltip-dialog").worldBound;
                    Check(itemDialog.xMin >= 0 && itemDialog.xMax <= size.x && itemDialog.yMin >= 0 && itemDialog.yMax <= size.y,
                        "Item tooltip exceeds the viewport.");
                    root.Q("item-tooltip-overlay").RemoveFromHierarchy();
                    if (_dungeonOnly) CheckDungeonSupport(root, size, controller, original);
                }
                if (name == "CharacterLoadout")
                {
                    Check(root.Q("collection-grid").Q("character-icon") != null && root.Q("formation-position-3") != null, "Formation or collection is missing.");
                    Check(root.Q<ScrollView>("formation-scroll") == null, "Formation still scrolls instead of staying in one row.");
                    var positions = Enumerable.Range(0, 4).Select(i => root.Q("formation-position-" + i)).ToList();
                    for (var i = 0; i < positions.Count; i++)
                    {
                        Check(Math.Abs(positions[i].worldBound.y - positions[0].worldBound.y) < 1, "Formation positions wrap into different rows.");
                        Check(positions[i].worldBound.xMin >= 0 && positions[i].worldBound.xMax <= size.x, "Formation exceeds the viewport.");
                        if (i > 0) Check(positions[i].worldBound.xMin >= positions[i - 1].worldBound.xMax, "Formation order overlaps.");
                        var icon = positions[i].Q("character-icon").worldBound;
                        Check(icon.height >= 70 && icon.yMax <= positions[i].worldBound.yMax, "Formation icon is clipped or too small.");
                    }
                    Check(root.Q<ScrollView>("collection-scroll").contentViewport.worldBound.height >= 172, "Owned fighter grid cannot show a complete row.");
                    Check(root.Q("loadout-play").worldBound.yMax <= size.y, "Enter battle is outside the viewport.");
                    if (_dungeonOnly) CheckDefeatedCollection(root, size);
                }
                Results.Add("PASS " + name + " " + size.x + "x" + size.y + ": viewport layout, shared live account header, character grid/scroll checks.");
                _index++;
                if (_index < Screens.Length * Sizes.Length) Build();
                else
                {
                    File.WriteAllLines(Folder + "/Verification.txt", Results.Concat(new[] { "Isolated fixture. No account, formation, run or scene saved." })); Cleanup();
                }
            }
            catch (Exception ex) { Fail(ex); }
        }

        private static void CheckMenu(VisualElement root, Vector2Int size)
        {
            var before = JsonUtility.ToJson(_inventory.Snapshot);
            var card = root.Q("route-card"); var battle = root.Q<Button>("battle");
            Check(card.style.display == DisplayStyle.None && !card.enabledSelf && battle.text.StartsWith("ENTER"), "No-run dungeon info must be hidden and disabled.");
            Check(battle.resolvedStyle.backgroundColor.r > .7f && battle.resolvedStyle.backgroundColor.g < .2f,
                "Shared primary button stylesheet failed to load.");
            Check(root.Q(className: "arena-background").resolvedStyle.backgroundImage.texture != null, "Main Menu background reference is missing.");
            Check(root.Query<Button>("training").ToList().Count == 1 && root.Q<Button>("backpack") != null, "Practice and Backpack need separate bindings.");
            Check(root.Q<Label>("account-coins").text == "123,456" && root.Q("account-items-button") == null, "Coin channel is missing or old header Items remains.");
            Check(root.Q("account-header").resolvedStyle.backgroundColor.a < .5f, "Header is not translucent.");
            var catalog = CharacterObjectCatalogBuilder.Load(null);
            var run = DungeonRunEngine.CreateRun("menu-preview", _inventory.SubjectId, "menu-start",
                DungeonFlowContext.CreateEntryProfile("series.kof", 1, 0, false), 75, 831,
                catalog.ContentVersion, catalog.ContentHash, new RunFighterSeed[0], catalog.Characters, 320, true);
            run.CurrentNodeId = run.Nodes.First(n => n.Type == RouteNodeType.Battle).Id;
            MainMenuView.RefreshDungeon(root, run); Render(size, "MainMenuActive");
            Check(card.enabledSelf && card.style.display == DisplayStyle.Flex && battle.text.StartsWith("CONTINUE") &&
                root.Q<Label>("current-dungeon-name").text == DungeonFlowContext.GetProfileForId(run.ProfileId).DisplayName &&
                root.Q<Label>("current-dungeon-stage").text.Contains("STAGE 1") && root.Q<Label>("current-dungeon-difficulty").text.Contains("Extreme"),
                "Dungeon card did not bind the current run, stage and difficulty.");
            Check(root.Q("route-art").resolvedStyle.backgroundImage.texture != null, "Dungeon card picture is missing.");
            Check(card.worldBound.y >= 84 && root.Q<Button>("backpack").worldBound.yMax <= size.y, "Active menu exceeds the viewport.");
            foreach (var status in new[] { RunStatus.Completed, RunStatus.Defeated, RunStatus.Abandoned })
            { run.Status = status; MainMenuView.RefreshDungeon(root, run); Check(!card.enabledSelf && card.style.display == DisplayStyle.None, "Terminal dungeon remained visible."); }
            MainMenuView.RefreshDungeon(root, null);
            var open = root.Q<Button>("backpack"); open.Focus(); BackpackView.Show(root);
            var overlay = root.Q("item-inventory-overlay"); var binding = overlay.userData;
            Render(size, "BackpackArtifact");
            Check(overlay.Q("backpack-grid").Query<Button>().ToList().Count == 5 && overlay.Q("backpack-grid").childCount >= 24, "Artifacts must be individual instances in grid boxes.");
            foreach (var tier in Enumerable.Range(1, 5)) Check(overlay.Q(className: "item-frame-" + tier) != null, "An artifact rarity frame is missing.");
            var dialog = overlay.Q("backpack-dialog").worldBound;
            Check(dialog.yMin >= 84 && dialog.xMin >= 0 && dialog.xMax <= size.x && dialog.yMax <= size.y, "Backpack exceeds the viewport.");
            Check(overlay.Q("item-inventory-preview") == null, "Backpack includes a permanent preview pane.");
            var artifact = overlay.Q<Button>("backpack-artifact-fixture-SSR");
            ItemRewardView.Show(root, ItemCatalog.Find(ItemCatalog.ArtifactId(ArtifactRarity.SSR)), anchor: artifact);
            Render(size, "BackpackTooltip"); CheckTooltipBounds(root, size);
            using (var escape = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape }))
            { escape.target = root.Q("item-tooltip-overlay"); root.Q("item-tooltip-overlay").SendEvent(escape); }
            Check(root.Q("item-tooltip-overlay") == null && root.Q("item-inventory-overlay") != null, "Tooltip Escape closed the whole Backpack.");
            Call(binding, "Select", 1); Render(size, "BackpackMaterial");
            Check(overlay.Q("backpack-grid").Query<Button>().ToList().Count == 16 && overlay.Q(className: "item-frame-3") != null && overlay.Q(className: "item-frame-4") != null,
                "Material tab is missing owned phase or series materials.");
            Call(binding, "Select", 2); Render(size, "BackpackDupe");
            Check(overlay.Q<Label>(className: "item-reward-amount").text == "12", "Dupe tab does not show the selected fighter's bound duplicate tokens.");
            var signal = (Action)typeof(PlayerInventoryService).GetField("InventoryChanged", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            _inventory.Snapshot.characterDupeTokens[0].amount = 13; signal?.Invoke();
            Check(overlay.Q<Label>(className: "item-reward-amount").text == "13", "Backpack did not refresh live balance changes.");
            _inventory.Snapshot.characterDupeTokens[0].amount = 12; signal?.Invoke();
            using (var escape = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape }))
            { escape.target = overlay.Q<Button>("backpack-tab-dupe"); overlay.Q<Button>("backpack-tab-dupe").SendEvent(escape); }
            Check(root.Q("item-inventory-overlay") == null, "Escape did not close Backpack.");
            for (var i = 0; i < 3; i++) { BackpackView.Show(root); Call(root.Q("item-inventory-overlay").userData, "Close"); }
            Check(JsonUtility.ToJson(_inventory.Snapshot) == before, "Backpack inspection changed the account.");
        }

        private static void CheckDifficulty(VisualElement root, Vector2Int size)
        {
            var slider = root.Q<SliderInt>("difficulty-slider");
            Check(slider.lowValue == 1 && slider.highValue == 5, "Difficulty must be a five-level slider.");
            var grid = root.Q("dungeon-reward-grid");
            Check(root.Q("dungeon-stage-preview").Contains(grid) && root.Q("configuration-overlay").style.display == DisplayStyle.None,
                "Rewards need to appear on the front page without opening Preview.");
            for (var level = 1; level <= 5; level++)
            {
                slider.value = level;
                var artifact = grid.Children().Where(c => c.ClassListContains("item-kind-artifact")).ToList();
                Check(artifact.Count == 1 && artifact[0].name == "item-" + ItemCatalog.ArtifactId((ArtifactRarity)level), "Preview must show only the highest possible artifact tier.");
                var ranks = grid.Children().Select(c => ItemCatalog.Find(c.name.Substring(5)).ValueRank).ToList();
                Check(ranks.SequenceEqual(ranks.OrderByDescending(v => v)), "Reward preview is not ordered by value.");
                Check(root.Q<Label>("difficulty-value").text == DungeonDifficulty.Display(level), "Difficulty name is incorrect.");
            }
            Render(size, "DungeonNightmare");
            var anchor = grid.Children().First();
            ItemRewardView.Show(root, ItemCatalog.Find(ItemCatalog.ArtifactId(ArtifactRarity.SSR)), anchor: anchor,
                rule: DungeonRewards.PreviewRule(ItemCatalog.Find(ItemCatalog.ArtifactId(ArtifactRarity.SSR)), 100));
            Render(size, "DungeonRewardTooltip"); CheckTooltipBounds(root, size); ItemRewardView.CloseTooltip(root);
            slider.value = 1; Render(size, "DungeonEasy");
            var ordered = new[] { root.Q("configuration-title"), root.Q("stage-filter"), root.Q(className: "difficulty-control"),
                root.Q("dungeon-mode"), root.Q("stage-route-info"), root.Q("reward-preview"), grid, root.Q("preview-details") };
            for (var i = 1; i < ordered.Length; i++)
                Check(ordered[i].worldBound.yMin >= ordered[i - 1].worldBound.yMax - 1, "Dungeon controls or reward labels overlap.");
            Check(grid.worldBound.yMax <= root.Q("dungeon-stage-preview").worldBound.yMax, "Front-page rewards are clipped.");
            Check(root.Q("preview-details").worldBound.yMax <= root.Q("dungeon-stage-preview").worldBound.yMax - 4, "Fighter Preview is clipped by the stage panel.");
        }

        private static void CheckTooltipBounds(VisualElement root, Vector2Int size)
        {
            var bounds = root.Q("item-tooltip-dialog").worldBound;
            Check(bounds.width <= 400 && bounds.xMin >= 0 && bounds.yMin >= 0 && bounds.xMax <= size.x && bounds.yMax <= size.y,
                "Quick tooltip is clipped or became a large preview panel.");
        }

        private static void Render(Vector2Int size, string name, bool capture = true)
        {
            var active = RenderTexture.active; Texture2D pixels = null;
            try
            {
                RenderTexture.active = _target; GL.Clear(true, true, Color.clear);
                typeof(PanelSettings).GetMethod("ApplyPanelSettings", Private)?.Invoke(_settings, null);
                var panel = _document.rootVisualElement.panel;
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                panel.GetType().GetMethod("Update", flags, null, Type.EmptyTypes, null)?.Invoke(panel, null);
                panel.GetType().GetMethod("Update", flags, null, Type.EmptyTypes, null)?.Invoke(panel, null);
                panel.GetType().GetMethod("Repaint", flags, null, new[] { typeof(Event) }, null)?.Invoke(panel, new object[] { new Event { type = EventType.Repaint } });
                panel.GetType().GetMethod("Render", flags, null, Type.EmptyTypes, null)?.Invoke(panel, null);
                if (!capture) return;
                RenderTexture.active = _target; pixels = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); pixels.Apply();
                File.WriteAllBytes(Folder + "/" + name + "-" + size.x + "x" + size.y + ".png", pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = active; if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels); }
        }
        private static void Check(bool ok, string message) { if (!ok) throw new Exception(Screens[_index % Screens.Length] + ": " + message); }
        private static void CheckDungeonSupport(VisualElement root, Vector2Int size, DungeonRouteMapController controller, RunState original)
        {
            var catalog = CharacterObjectCatalogBuilder.Load(null);
            var assets = _inventory.GetOwnedDefinitions().Where(c => c.RuntimeReady).Take(4).ToList();
            var preview = original.Clone();
            foreach (var asset in assets)
            {
                var definition = catalog.Characters.First(c => c.Id == asset.DefinitionId);
                preview.Roster.Add(new RunFighterState { RunFighterId = "fixture-" + asset.DefinitionId, DefinitionId = asset.DefinitionId,
                    Stats = definition.BaseStats.Clone(), CurrentHealth = definition.BaseStats.MaxHealth / 2 });
            }
            preview.Roster[0].CurrentHealth = 0; preview.Roster[0].IsDefeated = true;
            Set(controller, "_run", preview);
            controller.InspectNode(preview.Nodes.First(n => n.Type == RouteNodeType.Rest).Id);
            Render(size, "RestPreview");
            Check(root.Q("rest-preview") != null && root.Q("rest-confirm") != null, "Rest lacks its recovery preview/action.");
            Check(root.Q("rest-confirm").worldBound.yMax <= size.y && root.Q("node-commit-actions").Contains(root.Q("rest-confirm")), "Rest action is not pinned inside the viewport.");
            Check(!root.Query<Button>().ToList().Any(b => b.text?.Contains("Revive") == true), "Rest still offers revival.");
            controller.InspectNode(preview.Nodes.First(n => n.Type == RouteNodeType.Boon).Id);
            Render(size, "BoonPreview");
            Check(root.Q("boon-preview") != null && root.Q("boon-confirm") != null, "Boon lacks its stat preview/action.");
            Check(root.Q("boon-confirm").worldBound.xMax <= size.x && root.Q("boon-confirm").worldBound.yMax <= size.y, "Boon action exceeds the viewport.");
            var receipt = DungeonRewards.Victory(preview, preview.Nodes.First(n => n.Type == RouteNodeType.Battle));
            preview.Rewards.Add(receipt);
            _inventory.Snapshot.items.ClaimedReceiptIds.Add(receipt.Id);
            preview.Status = RunStatus.Completed;
            Check(DungeonFlowController.CanResumeRun(preview), "Completed run hides unseen victory rewards on resume.");
            Call(controller, "ShowPendingRewards"); SettleHub(root); Render(size, "VictoryRewards");
            var dialog = root.Q("victory-reward-dialog").worldBound;
            Check(dialog.xMin >= 0 && dialog.xMax <= size.x && dialog.yMin >= 0 && dialog.yMax <= size.y, "Victory dialog is clipped.");
            Check(root.Q("victory-reward-items").Query(className: "item-reward-amount").ToList().Count > 0, "Victory popup lacks actual quantities.");
            var back = root.Q("continue-after-reward").worldBound;
            Check(back.height >= 44 && back.yMax <= size.y, "Victory continue action is clipped.");
            // Remove presentation only; no account/run writes in this fixture.
            root.Q("victory-reward-overlay").RemoveFromHierarchy(); Set(controller, "_rewardOverlay", null);
            preview.PresentedRewardIds.Add(receipt.Id); Call(controller, "ShowPendingRewards");
            Check(root.Q("victory-reward-overlay") == null, "Acknowledged rewards reopen on resume.");
            Check(!DungeonFlowController.CanResumeRun(preview), "Completed, acknowledged run stays resumable.");
            Set(controller, "_run", original);
            controller.InspectNode(original.Nodes.First(n => n.Progress == RouteNodeProgress.Reachable).Id);
        }

        private static void CheckDefeatedCollection(VisualElement root, Vector2Int size)
        {
            var assets = _inventory.GetOwnedDefinitions().Where(c => c.RuntimeReady).Take(4).ToList();
            var run = new RunState { RunId = "defeated-ui-preview", ProfileId = "dungeon.open-circuit" };
            run.Roster.Add(new RunFighterState { RunFighterId = "fixture-dead", DefinitionId = assets[0].DefinitionId, IsDefeated = true });
            for (var i = 1; i < 4; i++) run.Formation[i] = _inventory.FindOwnedByDefinition(assets[i].DefinitionId).instanceId;
            DungeonFlowContext.BeginDungeonFlowForEncounter(DungeonProfile.OpenCircuit(), run, null);
            var manager = _screen.GetComponent<CharacterSelectionManager>(); Call(manager, "RestoreSavedFormation");
            var view = _screen.GetComponent<CharacterLoadoutView>(); Call(view, "Refresh");
            Render(size, "DefeatedCollection");
            var grid = root.Q("collection-grid"); var dead = grid.Q("character-" + assets[0].DefinitionId);
            Check(manager.GetSlot(0).AssignedCharacter == null, "Defeated character remained equipped after restore.");
            Check(!manager.TryAssignToSlot(0, assets[0], out _), "Loadout accepted a defeated character.");
            Check(dead?.Q("defeated-cross") != null && !dead.enabledSelf, "Defeated fighter lacks a cross or remains selectable.");
            Check(grid.Children().Last().name == dead.name, "Defeated fighter is not sorted last.");
            var tint = dead.Q("unavailable-overlay").resolvedStyle.backgroundColor;
            Check(tint.r == 0 && tint.g == 0 && tint.b == 0 && tint.a > .75f, "Defeated portrait does not have a black overlay.");
            root.Q<ScrollView>("collection-scroll").ScrollTo(dead); Render(size, "DefeatedCollection");
            DungeonFlowContext.Clear();
        }
        private static void SettleHub(VisualElement root)
        {
            foreach (var animated in root.Query(className: "animated-panel").ToList())
            { animated.RemoveFromClassList("animated-panel"); animated.RemoveFromClassList("panel-enter"); }
        }
        private static void CheckHub(VisualElement root, Vector2Int size, string name)
        {
            var hub = root.Q("character-hub");
            Check(hub.worldBound.y >= 84 && hub.worldBound.yMax <= size.y + 1, "Hub overlaps the header or viewport.");
            Check(root.Q("inventory-detail-icon") == null, "Inventory preview still exists.");
            var selected = name.Substring("CharacterHub".Length).ToLowerInvariant();
            foreach (var tab in new[] { "kit", "upgrade", "constellation", "artifacts" })
                Check(root.Q("hub-page-" + tab).style.display == (tab == selected ? DisplayStyle.Flex : DisplayStyle.None), "Incorrect hub tab visibility.");
            Check(root.Q("hub-stats").childCount == 4 && root.Q("hub-kit-icons").childCount == 4, "Kit needs basic stats, three cards and passive icon.");
            Check(root.Q("hub-level-materials").childCount == 1 && root.Q("hub-break-materials").childCount == 4, "Upgrade costs must show Coins and three matching materials.");
            Check(!root.Q<Button>("hub-limit-break").enabledSelf, "Limit break enabled below the cap.");
            var close = root.Q<Button>("hub-close").worldBound;
            Check(close.height >= 44 && close.xMin >= 0 && close.xMax <= size.x && close.yMax <= size.y, "Hub back action is clipped.");
            var before = JsonUtility.ToJson(_inventory.Snapshot);
            Call(hub.userData, "Close");
            Check(root.Q("character-inventory").style.display == DisplayStyle.Flex, "Back did not return to the grid.");
            Call(root.Q("character-inventory").userData, "Select", _inventory.GetOwnedDefinitions().First(c => c.DefinitionId == "fighter.kyo94"));
            Call(hub.userData, "SelectTab", selected); SettleHub(root);
            Check(JsonUtility.ToJson(_inventory.Snapshot) == before, "Inspection changed the account.");
        }
        private static void CheckInventory(VisualElement root, Vector2Int size)
        {
            var panel = root.Q("character-inventory");
            var grid = root.Q("inventory-grid");
            var expected = _inventory.GetOwnedDefinitions().OrderByDescending(c => c.FighterRarity)
                .ThenBy(c => c.FighterName, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.DefinitionId, StringComparer.Ordinal).ToList();
            Check(grid.Children().Select(c => c.name).SequenceEqual(expected.Select(c => "character-" + c.DefinitionId)), "Owned inventory membership or rarity order is wrong.");
            Check(root.Q("inventory-detail-icon") == null && grid.Q("character-icon") != null, "Inventory must be a grid without a preview.");
            Check(root.Q<ScrollView>("inventory-scroll").contentViewport.worldBound.height >= 172, "Collection cannot show a complete row.");
            Check(panel.worldBound.y >= 84 && panel.worldBound.yMax <= size.y + 1, "Inventory overlaps the header or viewport.");
            Check(root.Q("inventory-inspect") == null, "Obsolete inspect button still exists.");
            var search = root.Q<TextField>("inventory-search");
            search.value = "__no_matching_fighter__";
            Check(grid.Q("character-icon") == null && root.Q("inventory-detail-icon") == null, "No-match search leaves stale characters.");
            search.value = expected[0].FighterName;
            Check(grid.Q("character-" + expected[0].DefinitionId) != null, "Name search lost an owned fighter.");
            search.value = string.Empty;
            root.Q<DropdownField>("inventory-rarity").index = 1;
            Check(grid.Children().Where(c => c.ClassListContains("character-grid-card")).All(card =>
                _inventory.FindDefinition(card.name.Substring("character-".Length)).FighterRarity.ToString() == root.Q<DropdownField>("inventory-rarity").value), "Rarity filtering leaked fighters.");
            root.Q<DropdownField>("inventory-rarity").index = 0;
            var attribute = root.Q<DropdownField>("inventory-attribute");
            attribute.index = attribute.choices.IndexOf(expected[0].FighterAttribute.ToString());
            Check(grid.Children().Count(c => c.ClassListContains("character-grid-card")) == expected.Count(c => c.FighterAttribute == expected[0].FighterAttribute),
                "Attribute filter returned the wrong number of fighters.");
            attribute.index = 0;
            var sort = root.Q<DropdownField>("inventory-sort");
            sort.index = 1;
            Check(grid.Children().Select(c => c.name).SequenceEqual(expected.OrderBy(c => c.FighterName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.DefinitionId, StringComparer.Ordinal).Select(c => "character-" + c.DefinitionId)), "Name sorting is incorrect.");
            var sample = expected.Last(); var record = _inventory.FindOwnedByDefinition(sample.DefinitionId);
            var previousLevel = record.level; var previousTier = record.constellationTier; var previousCap = record.levelCap;
            try
            {
                record.level = 99; record.constellationTier = 3;
                sort.index = 2;
                Check(grid.Children().First().name == "character-" + sample.DefinitionId, "Level sorting ignores account progress.");
                sort.index = 3;
                Check(grid.Children().First().name == "character-" + sample.DefinitionId, "Constellation sorting ignores account progress.");
                panel.userData.GetType().GetMethod("Select", Private).Invoke(panel.userData, new object[] { sample });
                Check(root.Q<Label>("hub-name").text == sample.FighterName && root.Q<Label>("hub-progression").text.Contains("LEVEL 90") &&
                    root.Q<Label>("hub-progression").text.Contains("C3"), "Grid click did not open the owned fighter inspector.");
                Call(root.Q("character-hub").userData, "Close");
            }
            finally { record.level = previousLevel; record.constellationTier = previousTier; record.levelCap = previousCap; }
            panel.userData.GetType().GetMethod("Reset", Private).Invoke(panel.userData, null);
            Check(sort.index == 0 && attribute.index == 0 && search.value == string.Empty, "Reset did not restore default filters.");
            var records = _inventory.Snapshot.characters;
            try
            {
                _inventory.Snapshot.characters = records.Take(1).ToList();
                var signal = (Action)typeof(PlayerInventoryService).GetField("InventoryChanged", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                signal?.Invoke();
                Check(grid.Children().Count() == 1, "Grid does not reflect removed ownership.");
                _inventory.Snapshot.characters = records; signal?.Invoke();
                Check(grid.Q("character-icon") != null, "Inventory does not refresh after ownership changes.");
            }
            finally { _inventory.Snapshot.characters = records; }
            // Exercise the same close callback as the Home button without navigating scenes.
            panel.userData.GetType().GetMethod("Close", Private).Invoke(panel.userData, null);
            Check(panel.style.display.value == DisplayStyle.None && root.Q("home-screen").style.display.value == DisplayStyle.Flex,
                "Closing inventory did not restore Main Menu.");
            Check(root.Q<Label>("section-title").text == "HOME", "Closing inventory did not restore the account header title.");
            CharacterInventoryView.Show(root);
            panel.RemoveFromClassList("animated-panel"); panel.RemoveFromClassList("panel-enter");
            Render(size, "CharacterInventory", false);
            using (var escape = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape }))
            {
                escape.target = search; search.SendEvent(escape);
            }
            Check(panel.style.display.value == DisplayStyle.None, "Escape did not close inventory after reopening.");
        }
        private static void CheckGridBounds(VisualElement grid)
        {
            Check(grid.worldBound.height > 30, "Character preview has no available height.");
            foreach (var card in grid.Children().Where(c => c.ClassListContains("character-grid-card")))
                Check(card.worldBound.xMin >= grid.worldBound.xMin - 1 && card.worldBound.yMin >= grid.worldBound.yMin - 1 &&
                    card.worldBound.xMax <= grid.worldBound.xMax + 1 && card.worldBound.yMax <= grid.worldBound.yMax + 1,
                    "A character preview card exceeds its fitted panel.");
        }
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
        private static void Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Private).Invoke(obj, args);
        private static void InventoryInstance(PlayerInventoryService service) => typeof(PlayerInventoryService).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, service);
        private static void Fail(Exception ex) { File.WriteAllLines(Folder + "/Verification.txt", Results.Concat(new[] { "FAIL " + ex })); Debug.LogException(ex); Cleanup(); }
        private static void DisposeScreen()
        {
            if (_screen != null) UnityEngine.Object.DestroyImmediate(_screen);
            if (_target != null) { _target.Release(); UnityEngine.Object.DestroyImmediate(_target); }
            if (_settings != null) UnityEngine.Object.DestroyImmediate(_settings);
        }
        private static void Cleanup()
        {
            DisposeScreen(); if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            InventoryInstance(_original); _running = false; _dungeonOnly = false; _hubOnly = false; _menuRefactor = false; UiPreviewSession.Release();
        }
    }
}
