using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FightingAllstar.Core.Run;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Content;
using FightingAllstar.Presentation.Route;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools
{
    /// <summary>Renders the real dungeon UXML and exercises its controls in an isolated preview scene.</summary>
    [InitializeOnLoad]
    public static class DungeonSelectionPreview
    {
        private const string Request = "Temp/preview-dungeon-selection.request";
        private const string Folder = "Artifacts/DungeonEntry";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Scene _scene;
        private static UIDocument _document;
        private static DungeonEntryController _controller;
        private static PanelSettings _settings;
        private static RenderTexture _target;
        private static PlayerInventoryService _original, _preview;
        private static double _next;
        private static int _step;
        private static bool _running;

        static DungeonSelectionPreview()
        {
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += () => { if (_running) Cleanup(); };
        }

        [MenuItem("Fighting Allstar/Validate Dungeon Selection Layout")]
        public static void Run()
        {
            if (_running || UiPreviewSession.IsBusy || EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            Directory.CreateDirectory(Folder);
            UiPreviewSession.Acquire();
            _running = true; _step = 0; _next = EditorApplication.timeSinceStartup + .5;
            try
            {
                AssetDatabase.Refresh();
                _scene = EditorSceneManager.NewPreviewScene();
                var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Project/UI/DungeonEntry.uxml");
                var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Project/Scenes/Fixed1920PanelSettings.asset");
                if (tree == null || panel == null) throw new Exception("Dungeon selection assets did not import.");
                _settings = UnityEngine.Object.Instantiate(panel);
                _target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32); _target.Create();
                _settings.targetTexture = _target;
                var go = new GameObject("Dungeon selection preview");
                SceneManager.MoveGameObjectToScene(go, _scene);
                _document = go.AddComponent<UIDocument>();
                _document.panelSettings = _settings; _document.visualTreeAsset = tree;
                _controller = go.AddComponent<DungeonEntryController>();
                var catalog = CharacterObjectCatalogBuilder.Load(null);
                var assets = CharacterObjectRegistrySO.LoadAll();
                var inventory = new GameObject("Isolated preview inventory");
                SceneManager.MoveGameObjectToScene(inventory, _scene);
                inventory.SetActive(false);
                _original = PlayerInventoryService.Instance;
                _preview = inventory.AddComponent<PlayerInventoryService>();
                SetStaticInventory(_preview);
                var snapshot = new PlayerInventorySnapshot { subjectId = "__dungeon-ui-preview__", diamonds = 1600 };
                foreach (var asset in assets.Where(c => c != null && c.RuntimeReady && c.ID / 1000 == 1 && c.ID / 100 % 10 == 0))
                    snapshot.characters.Add(new OwnedCharacterRecord { definitionId = asset.DefinitionId, instanceId = "preview-" + asset.DefinitionId });
                Set(_preview, "snapshot", snapshot); Set(_preview, "_catalog", assets);
                Set(_preview, "_loadedSubjectId", snapshot.subjectId);
                Call(_controller, "OnEnable");
                _controller.Bind(snapshot.subjectId, catalog.ContentVersion, catalog.ContentHash, 831, catalog.Characters, null);
                Call(_controller, "InitializeUI");
            }
            catch (Exception ex) { Fail(ex); }
        }

        private static void Tick()
        {
            if (!_running)
            {
                if (!UiPreviewSession.IsBusy && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlaying && File.Exists(Request))
                { File.Delete(Request); Run(); }
                return;
            }
            if (EditorApplication.timeSinceStartup < _next) return;
            try
            {
                var root = _document.rootVisualElement;
                Check(root.panel != null, "Runtime UI panel did not attach.");
                if (_step == 0)
                {
                    Capture("OpenCircuit-1920x1080.png");
                    CheckStageCatalog();
                    root.Q<SliderInt>("difficulty-slider").value = 2;
                    Check(root.Q<Label>("difficulty-value").text == DungeonDifficulty.Display(2), "Normal difficulty preview is incorrect.");
                    root.Q<SliderInt>("difficulty-slider").value = 5;
                    Check(root.Q("dungeon-reward-grid").Q("item-artifact.ssr") != null, "Nightmare preview is missing the highest artifact.");
                    root.Q<SliderInt>("difficulty-slider").value = 1;
                    root.Q<DropdownField>("phase-filter").index = 8;
                    Check(root.Q<Button>("start-run-button").enabledSelf, "EndGame excluded past runtime-ready fighters.");
                    Check(root.Q<Button>("open-circuit").ClassListContains("selected") && root.Q<Button>("kof").resolvedStyle.display == DisplayStyle.Flex,
                        "A stage filter changed or hid the selected dungeon state.");
                    CheckStageCatalog();
                    root.Q<DropdownField>("phase-filter").index = 0;
                    root.Q<DropdownField>("subphase-filter").index = 1; CheckStageCatalog();
                    root.Q<DropdownField>("subphase-filter").index = 0;
                    var initialOwned = _preview.Snapshot.characters;
                    var unownedId = initialOwned[0].definitionId;
                    _preview.Snapshot.characters = initialOwned.Where(c => c.definitionId != unownedId).ToList();
                    Call(_controller, "Refresh");
                    CheckBlackPortrait(unownedId);
                    _preview.Snapshot.characters = initialOwned; Call(_controller, "Refresh");
                    // Simulate a draft using an in-memory clone, preserving all real catalog assets.
                    var originalAssets = _preview.GetCatalog().ToArray();
                    var originalAsset = originalAssets.First(c => c != null && c.DefinitionId == unownedId);
                    var draft = UnityEngine.Object.Instantiate(originalAsset);
                    try
                    {
                        Set(draft, "runtimeReady", false);
                        Set(_preview, "_catalog", originalAssets.Select(c => c == originalAsset ? draft : c).ToArray());
                        Call(_controller, "Refresh"); CheckBlackPortrait(draft.DefinitionId); CheckStageCatalog();
                    }
                    finally
                    {
                        Set(_preview, "_catalog", originalAssets); UnityEngine.Object.DestroyImmediate(draft); Call(_controller, "Refresh");
                    }
                    Check(root.Q("women-exhibition") == null && root.Q(className: "card-illustration") == null,
                        "Legacy Women's or placeholder fighter artwork remains.");
                    foreach (var stage in new[] { "open-circuit", "kof", "tekken", "soul-calibur", "genshin-impact" })
                    {
                        _controller.SelectDungeonStage(stage);
                        Check(root.Q<Button>(stage).ClassListContains("selected"), "Series card did not select: " + stage);
                        CheckStageCatalog();
                        var selected = CurrentProfile();
                        var restored = DungeonFlowContext.GetProfileForId(selected.Id);
                        Check(restored.Id == selected.Id && restored.SeriesId == selected.SeriesId &&
                            restored.Phase == selected.Phase && restored.SubPhase == selected.SubPhase,
                            "Series or cutoff lost when reconstructing a saved profile.");
                        Check(root.Q<Label>("configuration-title").text == selected.DisplayName, "Selected dungeon title differs from the series.");
                        if (!_preview.GetCatalog().Any(c => c != null && DungeonFlowContext.IsCharacterEligible(selected, c)))
                            Check(!root.Q<Button>("start-run-button").enabledSelf && root.Q("dungeon-preview-grid").Q("unavailable-overlay") == null,
                                "An empty series permitted entry or overlaid excluded characters.");
                    }
                    _controller.SelectDungeonStage("kof");
                    root.Q<DropdownField>("phase-filter").index = 3;
                    root.Q<DropdownField>("subphase-filter").index = 1;
                    CheckStageCatalog();
                    Check(root.Q("dungeon-preview-grid").Q("character-" + unownedId) != null && root.Q<Button>("start-run-button").enabledSelf,
                        "Season 2 Begin did not retain past WIP fighters.");
                    CheckSeriesAndCutoffExclusion(originalAssets, originalAsset);
                    var owned = _preview.Snapshot.characters;
                    _preview.Snapshot.characters = owned.Take(0).ToList();
                    Call(_controller, "Refresh");
                    Check(!root.Q<Button>("start-run-button").enabledSelf, "Zero eligible ownership allowed entry.");
                    _preview.Snapshot.characters = owned;
                    Call(_controller, "Refresh");
                    var catalog = CharacterObjectCatalogBuilder.Load(null);
                    foreach (var series in new[] { null, "series.kof" })
                    {
                        var run = DungeonRunEngine.CreateRun("preview-run", "__dungeon-ui-preview__", "preview-request",
                            DungeonFlowContext.CreateEntryProfile(series, 4, 1), 25, 831,
                            catalog.ContentVersion, catalog.ContentHash, new RunFighterSeed[0], catalog.Characters, 320, true);
                        Check(run.Nodes.Select(n => n.Row).Distinct().Count() == 9 && run.RewardQuoteDiamonds == 0,
                            "Dungeon run generation or absence of a completion bonus disagrees with the UI.");
                    }
                    _step = 1;
                }
                else if (_step == 1)
                {
                    Capture("KOF-1920x1080.png");
                    var notOwned = _preview.GetOwnedDefinitions().First(c => DungeonFlowContext.IsCharacterEligible(CurrentProfile(), c));
                    _preview.Snapshot.characters.RemoveAll(c => c.definitionId == notOwned.DefinitionId);
                    Call(_controller, "Refresh"); CheckBlackPortrait(notOwned.DefinitionId);
                    Call(_controller, "OpenPreview");
                    Check(root.Q("configuration-overlay").style.display.value == DisplayStyle.Flex, "Preview did not open.");
                    // Offscreen panels update only when rendered; capture the settled appearance.
                    root.Q("configuration-overlay").RemoveFromClassList("animated-panel");
                    root.Q("configuration-overlay").RemoveFromClassList("panel-enter");
                    _step = 2;
                }
                else
                {
                    Capture("DungeonPreview-1920x1080.png");
                    CheckStageCatalog();
                    var overlay = root.Q("dungeon-preview-grid").Q("unavailable-overlay");
                    Check(overlay != null && overlay.resolvedStyle.backgroundColor.a > 0 && overlay.resolvedStyle.backgroundColor.a < 1,
                        "Unavailable character overlay is not translucent.");
                    Check(root.Q("series-filter") == null && root.Q("dungeon-stage-preview").Q("character-icon") == null,
                        "Series dropdown or full catalog is still shown in Stage Info.");
                    Call(_controller, "CloseConfiguration");
                    Check(root.Q("configuration-overlay").style.display.value == DisplayStyle.None &&
                        root.Q("series-browser").enabledSelf, "Preview close did not restore selection controls.");
                    File.WriteAllText(Folder + "/Verification.txt", "PASS: five series cards (Open Circuit, KOF, Tekken, Soul Calibur, Genshin Impact); no Women's/placeholder cards; cumulative ID cutoff including Season 2 Begin retaining WIP; series/future-stage exclusion before rarity sorting and overlays; unowned/draft artwork preserved; empty series and zero ownership disable entry; saved all-series/series cutoffs and nine-row runs; reward and preview open/close. No account, formation, run or scene saved.");
                    Cleanup();
                }
                _next = EditorApplication.timeSinceStartup + .5;
            }
            catch (Exception ex) { Fail(ex); }
        }

        private static void Capture(string name)
        {
            var panel = _document.rootVisualElement.panel;
            var oldActive = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                RenderTexture.active = _target; GL.Clear(true, true, Color.clear);
                typeof(PanelSettings).GetMethod("ApplyPanelSettings", Private)?.Invoke(_settings, null);
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                panel.GetType().GetMethod("Update", flags, null, Type.EmptyTypes, null)?.Invoke(panel, null);
                panel.GetType().GetMethod("Update", flags, null, Type.EmptyTypes, null)?.Invoke(panel, null);
                panel.GetType().GetMethod("Repaint", flags, null, new[] { typeof(Event) }, null)?.Invoke(panel, new object[] { new Event { type = EventType.Repaint } });
                panel.GetType().GetMethod("Render", flags, null, Type.EmptyTypes, null)?.Invoke(panel, null);
                RenderTexture.active = _target;
                pixels = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); pixels.Apply();
                File.WriteAllBytes(Folder + "/" + name, pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = oldActive; if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels); }
        }
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void CheckStageCatalog()
        {
            var root = _document.rootVisualElement;
            var filter = CurrentProfile();
            var expected = _preview.GetCatalog().Where(c => c != null && DungeonFlowContext.IsCharacterEligible(filter, c))
                .OrderByDescending(c => c.FighterRarity).ThenBy(c => c.FighterName, StringComparer.Ordinal).ThenBy(c => c.DefinitionId, StringComparer.Ordinal).ToList();
            var grid = root.Q("dungeon-preview-grid");
            Check(grid.Children().Where(c => c.ClassListContains("character-grid-card")).Select(c => c.name)
                .SequenceEqual(expected.Select(c => "character-" + c.DefinitionId)), "Dungeon Preview membership or rarity order differs from its policy filter.");
            foreach (var draft in expected.Where(c => !c.RuntimeReady)) CheckBlackPortrait(draft.DefinitionId);
        }
        private static DungeonProfile CurrentProfile() => (DungeonProfile)typeof(DungeonEntryController)
            .GetProperty("Profile", Private).GetValue(_controller);

        private static void CheckSeriesAndCutoffExclusion(CharacterObject[] assets, CharacterObject original)
        {
            var sample = UnityEngine.Object.Instantiate(original);
            try
            {
                Set(sample, "runtimeReady", false);
                Set(sample, "seriesId", "series.tekken"); Set(sample, "id", 4100);
                Set(_preview, "_catalog", assets.Select(c => c == original ? sample : c).ToArray());
                Call(_controller, "Refresh");
                Check(_document.rootVisualElement.Q("character-" + sample.DefinitionId) == null,
                    "Other series should be filtered out before any overlay.");
                _controller.SelectDungeonStage("open-circuit"); CheckBlackPortrait(sample.DefinitionId); CheckStageCatalog();
                Set(sample, "id", 4200); Call(_controller, "Refresh");
                Check(_document.rootVisualElement.Q("character-" + sample.DefinitionId) == null,
                    "A future character should be filtered out before any overlay.");
                Set(sample, "id", 4099); Call(_controller, "Refresh");
                CheckBlackPortrait(sample.DefinitionId); CheckStageCatalog();
            }
            finally
            {
                Set(_preview, "_catalog", assets); UnityEngine.Object.DestroyImmediate(sample);
                _controller.SelectDungeonStage("kof");
            }
        }
        private static void CheckBlackPortrait(string definitionId)
        {
            var card = _document.rootVisualElement.Q("dungeon-preview-grid").Q("character-" + definitionId);
            Check(card != null && card.ClassListContains("character-unavailable") && card.Q("unavailable-overlay") != null && card.Q<Image>("char-icon").tintColor == Color.white,
                "Unavailable fighter does not retain its original portrait with an overlay.");
            Check(card.Q<Image>("rarity-border").sprite != null && card.Q<Image>("attribute").sprite != null,
                "Blacking out a fighter removed its shared rarity/attribute artwork.");
        }
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
        private static void Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Private).Invoke(obj, args);
        private static void SetStaticInventory(PlayerInventoryService inventory) => typeof(PlayerInventoryService)
            .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, inventory);
        private static void Fail(Exception ex)
        {
            File.WriteAllText(Folder + "/Verification.txt", "FAIL: " + ex);
            Debug.LogException(ex); Cleanup();
        }
        private static void Cleanup()
        {
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            SetStaticInventory(_original);
            if (_target != null) { _target.Release(); UnityEngine.Object.DestroyImmediate(_target); }
            if (_settings != null) UnityEngine.Object.DestroyImmediate(_settings);
            _running = false;
            UiPreviewSession.Release();
        }
    }
}

