using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FightingAllstar.Presentation.Navigation;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools
{
    /// <summary>Renders the summon presentation in an isolated preview scene without an account or summon controller.</summary>
    [InitializeOnLoad]
    public static class SummonSequencePreview
    {
        public const string OutputFolder = "Artifacts/SummonSequence";
        public const string RequestPath = "Temp/preview-summon-sequence.request";
        private const string VerificationPath = OutputFolder + "/Verification.txt";
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Vector2Int[] Sizes = { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(768, 1024) };
        private static readonly Sample[] Samples =
        {
            new Sample("Low-Arrival", 0, .8f),
            new Sample("Low-Complete", 0, 2.5f),
            new Sample("SSR-Cross", 1, 1.4f),
            new Sample("SSR-Silhouette", 1, 2.15f),
            new Sample("SSR-Hero", 1, 3.1f),
            new Sample("SSR-Complete", 1, 6f),
            new Sample("Ten-Low-Complete", 2, 4f),
            new Sample("Ten-Mixed-Complete", 3, 12f),
            new Sample("Receipt-White-Flash", 4, .48f),
            new Sample("Receipt-Upward-Fade", 4, .56f),
            new Sample("Receipt-Settle-Glint", 4, .84f),
            new Sample("Receipt-Mixed-Complete", 5, 12f),
            new Sample("SSR-Red", 6, 2.7f),
            new Sample("SSR-Yellow", 7, 2.7f),
            new Sample("SSR-Green", 8, 2.7f),
            new Sample("SSR-Blue", 9, 2.7f),
            new Sample("SSR-Purple", 10, 2.7f),
            new Sample("SSR-Screen-Entry-Impulse", 6, .54f),
            new Sample("SSR-Rush-Impulse", 6, 1.54f),
            new Sample("Items-Arrival", 11, .56f),
            new Sample("Items-Complete", 11, 5f),
            new Sample("Items-Overflow", 12, 10f)
        };
        private static readonly List<string> Results = new List<string>();
        private static readonly List<CharacterObject> FixtureClones = new List<CharacterObject>();
        private static Scene _scene;
        private static GameObject _screen;
        private static UIDocument _document;
        private static PanelSettings _settings;
        private static RenderTexture _target;
        private static object _view;
        private static Type _viewType;
        private static CharacterObject[] _low, _mixed, _tenLow, _tenMixed, _catalog;
        private static SummonDrawPresentation[] _lowReceipts, _mixedReceipts;
        private static SummonDrawPresentation[][] _themeReceipts;
        private static DungeonRewardReceipt _itemReceipt, _overflowReceipt;
        private static int _index;
        private static double _next;
        private static bool _layoutSettled;
        private static float _flashTranslation;
        private static IEnumerator<object> _lifecycle;
        private static bool _captureMotion, _motionPrepared;
        private static int _motionFrame = -1, _motionLastFrame;
        private static float _heldMotionTime;

        public static bool IsRunning { get; private set; }
        public static string Status { get; private set; } = "Idle";

        static SummonSequencePreview()
        {
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        }

        [MenuItem("Fighting Allstar/Validate Summon Sequence")]
        public static void Run() => Start(false);

        [MenuItem("Fighting Allstar/Validate Summon Sequence With Motion Frames")]
        public static void RunMotion() => Start(true);

        private static void Start(bool captureMotion)
        {
            if (IsRunning || UiPreviewSession.IsBusy || EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Status = "Waiting for the editor and other UI previews to be idle.";
                return;
            }

            Directory.CreateDirectory(OutputFolder);
            Results.Clear();
            _index = 0;
            _captureMotion = captureMotion;
            _motionFrame = -1;
            IsRunning = true;
            UiPreviewSession.Acquire();
            try
            {
                _viewType = typeof(CharacterObject).Assembly.GetType("FightingAllstar.Presentation.Navigation.SummonSequenceView", true);
                _catalog = CharacterObjectRegistrySO.LoadAll().Where(c => c != null).ToArray();
                _low = new[] { Fighter(FighterRarity.R), Fighter(FighterRarity.SR) };
                _mixed = new[] { _low[0], _low[1], Fighter(FighterRarity.SSR) };
                _tenLow = Enumerable.Range(0, 10).Select(i => _low[i % 2]).ToArray();
                var second = _catalog.Where(c => c.FighterRarity == FighterRarity.SSR && c != _mixed[2])
                    .OrderByDescending(c => c.FighterFull != null).ThenBy(c => c.DefinitionId, StringComparer.Ordinal).FirstOrDefault() ?? _mixed[2];
                _tenMixed = new[] { _low[0], _low[1], _mixed[2], _low[0], second, _low[1], _low[0], _low[1], _low[0], _low[1] };
                _lowReceipts = new[] { new SummonDrawPresentation(_low[0], true), new SummonDrawPresentation(_low[1], false, 1, 5) };
                var receiptFighters = new[] { _low[0], _low[1], _mixed[2], _low[0], _mixed[2], _low[1], _low[0], _low[1], _low[0], _low[1] };
                _mixedReceipts = SummonDrawPresentation.Classify(receiptFighters, new[] { _low[1].DefinitionId },
                    new Dictionary<string, int> { { _low[1].DefinitionId, 5 } }).ToArray();
                _themeReceipts = new[] { FighterAttribute.Red, FighterAttribute.Yellow, FighterAttribute.Green, FighterAttribute.Blue, FighterAttribute.Purple }
                    .Select(attribute =>
                    {
                        var copy = UnityEngine.Object.Instantiate(_mixed[2]);
                        copy.FighterAttribute = attribute;
                        FixtureClones.Add(copy);
                        return new[] { new SummonDrawPresentation(copy, true) };
                    }).ToArray();
                _itemReceipt = CreateItemReceipt(false); _overflowReceipt = CreateItemReceipt(true);
                _scene = EditorSceneManager.NewPreviewScene();
                Results.Add("Summon presentation verification. Fixtures use authored character assets only.");
                Results.Add("No FrontEndController, ownership service, account fixture, wallet, or summon transaction is created or accessed.");
                foreach (var fighter in _mixed)
                    Results.Add("Fixture: " + fighter.FighterRarity + " / " + fighter.FighterName + " / " + fighter.DefinitionId);
                CheckPresentationIsolation();
                Build();
            }
            catch (Exception ex) { Fail(ex); }
        }

        private static CharacterObject Fighter(FighterRarity rarity)
        {
            var fighter = _catalog.Where(c => c.FighterRarity == rarity)
                .OrderByDescending(c => c.FighterFull != null)
                .ThenByDescending(c => c.FighterPic != null)
                .ThenBy(c => c.DefinitionId, StringComparer.Ordinal).FirstOrDefault();
            Check(fighter != null, "The authored registry has no " + rarity + " fixture.");
            Check(fighter.FighterFull != null || fighter.FighterPic != null || fighter.FighterIcon != null, rarity + " fixture has no authored artwork.");
            return fighter;
        }

        private static void Build()
        {
            var size = Sizes[_index / Samples.Length];
            var sample = Samples[_index % Samples.Length];
            BuildPanel(size);
            SamplePresentation(sample);
            _layoutSettled = false;
            SetStatus("RUNNING " + (_index + 1) + "/" + (Sizes.Length * Samples.Length) + " " + sample.Name + " " + size.x + "x" + size.y);
            _next = EditorApplication.timeSinceStartup + .2;
        }

        private static void BuildPanel(Vector2Int size)
        {
            DisposeScreen();
            var originalSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Project/Scenes/Fixed1920PanelSettings.asset");
            Check(originalSettings != null, "Missing fixed panel settings.");
            _settings = UnityEngine.Object.Instantiate(originalSettings);
            _target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
            _target.Create();
            _settings.targetTexture = _target;
            _screen = new GameObject("Summon sequence preview");
            SceneManager.MoveGameObjectToScene(_screen, _scene);
            _document = _screen.AddComponent<UIDocument>();
            _document.panelSettings = _settings;
            _document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Project/UI/GachaBanner.uxml");
            Check(_document.visualTreeAsset != null, "Missing GachaBanner.uxml.");
            _view = Activator.CreateInstance(_viewType, Members, null, new object[] { _document.rootVisualElement }, null);
        }

        private static void Tick()
        {
            if (!IsRunning)
            {
                if (!UiPreviewSession.IsBusy && !EditorApplication.isPlaying && !EditorApplication.isCompiling && !EditorApplication.isUpdating && File.Exists(RequestPath))
                {
                    File.Delete(RequestPath);
                    Run();
                }
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < _next) return;
            try
            {
                if (_lifecycle != null)
                {
                    Render(new Vector2Int(_target.width, _target.height), null);
                    if (_lifecycle.MoveNext()) { _next = EditorApplication.timeSinceStartup + .016; return; }
                    _lifecycle.Dispose(); _lifecycle = null;
                    Complete();
                    return;
                }
                if (_motionFrame >= 0)
                {
                    if (CaptureMotionStep()) return;
                    _lifecycle = ValidateLifecycle().GetEnumerator(); _next = 0;
                    return;
                }
                var size = Sizes[_index / Samples.Length];
                var sample = Samples[_index % Samples.Length];
                if (!_layoutSettled)
                {
                    Render(size, null);
                    // The first panel update establishes this target's real dimensions.
                    SamplePresentation(sample);
                    Render(size, null);
                    _layoutSettled = true;
                    _next = EditorApplication.timeSinceStartup + .1;
                    return;
                }
                Render(size, null);
                if (sample.Fixture >= 11) Render(size, sample.Name + "-Layout");
                ValidateSample(sample, size);
                Render(size, sample.Name);
                Results.Add("PASS " + sample.Name + " at " + sample.Elapsed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "s / " + size.x + "x" + size.y);
                _index++;
                if (_index < Sizes.Length * Samples.Length) { Build(); return; }
                ValidateFixtureCases();
                ValidateAudioSynthesis();
                ValidateScreenImpulse();
                if (_captureMotion) { BeginMotion(); return; }
                _lifecycle = ValidateLifecycle().GetEnumerator();
                _next = 0;
            }
            catch (Exception ex) { Fail(ex); }
        }

        private static void BeginMotion()
        {
            BuildPanel(Sizes[0]);
            Directory.CreateDirectory(OutputFolder + "/Motion");
            Invoke("SampleForPreview", _themeReceipts[1], 0f);
            _motionLastFrame = Mathf.CeilToInt(Duration * 24);
            _motionFrame = 0; _motionPrepared = false; _next = EditorApplication.timeSinceStartup + .1;
            SetStatus("RUNNING 24fps motion frames");
        }

        private static bool CaptureMotionStep()
        {
            var size = Sizes[0];
            if (_motionFrame <= _motionLastFrame)
            {
                if (!_motionPrepared)
                {
                    Invoke("SampleForPreview", _themeReceipts[1], _motionFrame / 24f);
                    Render(size, null); _motionPrepared = true; _next = EditorApplication.timeSinceStartup + .001;
                    return true;
                }
                Render(size, "Motion/frame-" + _motionFrame.ToString("D4"));
                _motionFrame++; _motionPrepared = false;
                return true;
            }
            if (_motionFrame == _motionLastFrame + 1)
            {
                Invoke("Play", _themeReceipts[1], (Action)(() => { }));
                Invoke("AdvancePlayback", 120f);
                Check(AwaitingReveal, "Motion evidence could not enter the settled SSR input hold.");
                _heldMotionTime = (float)_viewType.GetField("_elapsed", Members).GetValue(_view);
                Render(size, "Motion/held-loop-0"); _motionFrame++;
                return true;
            }
            Invoke("AdvancePlayback", 1.1f);
            Check(AwaitingReveal && Math.Abs((float)_viewType.GetField("_elapsed", Members).GetValue(_view) - _heldMotionTime) < .001f,
                "Held motion evidence advanced the reveal timeline without input.");
            Render(size, "Motion/held-loop-1");
            File.WriteAllText(OutputFolder + "/Motion/Frames.txt", "24fps;1920x1080;frames=" + (_motionLastFrame + 1) + "; first frame=0. Two held-loop images are 1.1 loop seconds apart with the reveal timeline frozen.");
            Results.Add("PASS Captured " + (_motionLastFrame + 1) + " finite timeline frames at24fps and two independently animated held-loop frames.");
            _motionFrame = -1;
            return false;
        }

        private static void Complete()
        {
            Results.Add("PASS All checks completed. " + (Sizes.Length * Samples.Length) + " deterministic preview images captured.");
            SetStatus("PASS");
            Debug.Log("Summon sequence verified. Images and report: " + Path.GetFullPath(OutputFolder));
            Cleanup();
        }

        private static void ValidateSample(Sample sample, Vector2Int size)
        {
            var root = _document.rootVisualElement;
            var overlay = root.Q("summon-sequence");
            Check(overlay != null && overlay.resolvedStyle.display != DisplayStyle.None, "Sample overlay is hidden.");
            Check(overlay.worldBound.width > 0 && overlay.worldBound.height > 0, "Sample overlay has no render area.");
            Check(overlay.worldBound.xMin >= -1 && overlay.worldBound.yMin >= -1 && overlay.worldBound.xMax <= size.x + 1 && overlay.worldBound.yMax <= size.y + 1,
                "Sequence overlay exceeds the viewport.");
            var skip = root.Q<Button>("summon-skip");
            Check(skip != null, "Skip button is missing.");
            var waiting = (bool)_viewType.GetProperty("IsAwaitingClose", Members).GetValue(_view);
            var action = waiting ? root.Q<Button>("summon-result-close") : skip;
            Check(action != null && Bounded(action.worldBound, size), "Visible sequence action is clipped.");
            Check(root.Q("result-screen") == null, "Legacy result screen remains in the gacha document.");
            if (waiting)
            {
                var prompt = root.Q<Label>("summon-result-close-label");
                Check(prompt?.text == "TAP TO CLOSE", "Final reward board lacks its close prompt.");
                Check(prompt.visible && Bounded(prompt.worldBound, size) && prompt.worldBound.height > 8 &&
                    prompt.resolvedStyle.opacity > .5f && prompt.resolvedStyle.color.a > .5f,
                    "Final close prompt is hidden or clipped: " + prompt.worldBound);
            }
            if (sample.Fixture >= 11) { ValidateItems(sample, size); return; }
            var draws = Draws(sample);
            var receipts = sample.Fixture >= 4 ? Receipts(sample) : SummonDrawPresentation.Classify(draws, Array.Empty<string>()).ToArray();
            Check(root.Query<VisualElement>().ToList().Count(e => e.name != null && e.name.StartsWith("summon-card-", StringComparison.Ordinal) && int.TryParse(e.name.Substring("summon-card-".Length), out _)) == draws.Length,
                "The number of reward cards differs from the draws.");
            for (var i = 0; i < draws.Length; i++)
            {
                var card = root.Q("summon-card-" + i);
                Check(card != null, "Reward card order is incomplete.");
                CheckReceiptItem(card, receipts[i]);
                Check(Vector3.Distance(card.resolvedStyle.scale.value, Vector3.one) < .001f, "Reward arrival changes the fixed item scale.");
            }
            Check(root.Q("summon-reward-board") != null, "Reward board is missing.");
            Check(Vector3.Distance(root.Q("summon-reward-board").resolvedStyle.scale.value, Vector3.one) < .001f, "Reward board changes scale during its entrance.");
            var heroScale = root.Q("summon-hero-stage").resolvedStyle.scale.value;
            Check(heroScale.x >= .999f && heroScale.x <= 1.251f && Math.Abs(heroScale.x - heroScale.y) < .001f, "SSR artwork scale is outside its1.25-to1 settle motion.");
            var cinematic = root.Q("summon-cinematic");
            Check(cinematic != null, "Cinematic element is missing.");
            if (sample.Name.StartsWith("Receipt-", StringComparison.Ordinal) && sample.Fixture == 4)
            {
                var card = root.Q("summon-card-0");
                var light = card.Q("summon-item-light");
                Check(light != null, "Reward arrival has no white-flash/glint graphic.");
                var translation = card.resolvedStyle.translate.y;
                if (sample.Name == "Receipt-White-Flash")
                {
                    Check(ReadGraphicProperty<float>(light, "WhiteFlash") > .01f, "Ordinary reward arrival is missing its white flash.");
                    Check(translation > 0 && card.style.opacity.value > 0 && card.style.opacity.value < .99f, "Ordinary reward should fade upward from below.");
                    _flashTranslation = translation;
                }
                else if (sample.Name == "Receipt-Upward-Fade")
                    Check(translation > 0 && translation < _flashTranslation, "Ordinary reward does not move upward toward its settled pose.");
                else
                {
                    Check(Math.Abs(translation) < .01f && card.style.opacity.value >= .99f, "Reward did not settle at full opacity.");
                    Check(ReadGraphicProperty<float>(light, "SettleGlint") > .01f, "Settled reward is missing its small glint.");
                }
            }
            if (!draws.Any(c => c.FighterRarity >= FighterRarity.SSR))
                Check(cinematic.resolvedStyle.display == DisplayStyle.None || cinematic.resolvedStyle.opacity <= .001f, "R/SR-only draws opened a rarity cinematic.");
            if (sample.Name == "SSR-Hero" || sample.Fixture >= 6 && !sample.Name.EndsWith("Impulse", StringComparison.Ordinal))
            {
                var name = root.Q<Label>("summon-name");
                var rarity = root.Q<Label>("summon-rarity");
                Check(name != null && name.text == draws.Last(c => c.FighterRarity >= FighterRarity.SSR).FighterName, "SSR reveal has the wrong fighter name.");
                Check(rarity != null && rarity.text.Contains("SSR"), "SSR reveal has the wrong rarity.");
                var hero = root.Q("summon-hero");
                Check(hero != null && hero.worldBound.width > 0 && hero.worldBound.height > 0, "SSR hero artwork has no render area.");
                var image = hero as Image;
                Check((image != null && (image.sprite != null || image.image != null || image.vectorImage != null)) ||
                    hero.resolvedStyle.backgroundImage.sprite != null || hero.resolvedStyle.backgroundImage.texture != null, "SSR hero has no authored artwork assigned.");
                Check(Bounded(name.worldBound, size) && Bounded(rarity.worldBound, size), "SSR reveal text is clipped.");
                var bar = root.Q("summon-name-bar");
                var textSize = name.MeasureTextSize(name.text, name.layout.width, VisualElement.MeasureMode.Exactly, 0, VisualElement.MeasureMode.Undefined);
                Check(textSize.y <= bar.layout.height + 2, "SSR fighter name wraps beyond the name band.");
                var nameBounds = name.worldBound;
                var bandBounds = bar.worldBound;
                Check(nameBounds.xMin >= bandBounds.xMin - 1 && nameBounds.xMax <= bandBounds.xMax + 1 && nameBounds.yMin >= bandBounds.yMin - 1 && nameBounds.yMax <= bandBounds.yMax + 1,
                    "SSR fighter name extends outside the red band.");
                if (sample.Fixture >= 6)
                {
                    var attribute = draws[0].FighterAttribute;
                    Check(overlay.ClassListContains("theme-" + attribute.ToString().ToLowerInvariant()), "SSR reveal has the wrong attribute theme class.");
                    var accent = ReadGraphicProperty<Color>(cinematic, "Accent");
                    var expected = SummonAttributeTheme.ColorFor(attribute);
                    Check(Math.Abs(accent.r - expected.r) < .001f && Math.Abs(accent.g - expected.g) < .001f && Math.Abs(accent.b - expected.b) < .001f,
                        "SSR particles/cross effect do not use the selected attribute accent.");
                }
            }
            if (sample.Name.EndsWith("Complete", StringComparison.Ordinal))
            {
                var duration = (float)_viewType.GetProperty("Duration", Members).GetValue(_view);
                Check(duration > 0 && sample.Elapsed >= duration, "Completed sample precedes the runtime timeline duration.");
                for (var i = 0; i < draws.Length; i++)
                {
                    var bounds = root.Q("summon-card-" + i).worldBound;
                    Check(Bounded(bounds, size), "A character obtain card is clipped.");
                }
                if (draws.Length == 10) CheckTenGrid(size);
            }
            Check(_screen.GetComponents<Component>().All(c => c is Transform || c is UIDocument), "Preview attached a runtime controller.");
        }

        internal static DungeonRewardReceipt CreateItemReceipt(bool overflow)
        {
            var receipt = new DungeonRewardReceipt { Id = "obtain-presentation-fixture" };
            receipt.Items.Add(new ItemAmount { ItemId = ItemCatalog.Diamond, Amount = 175 });
            receipt.Items.Add(new ItemAmount { ItemId = ItemCatalog.Coin, Amount = 2500 });
            receipt.Items.Add(new ItemAmount { ItemId = ItemCatalog.ArtifactDust, Amount = 20 });
            receipt.Items.Add(new ItemAmount { ItemId = "material.phase.1", Amount = 4 });
            for (var i = 0; i < (overflow ? 22 : 2); i++)
                receipt.Artifacts.Add(ArtifactRules.Generate("obtain-artifact-" + i, (ArtifactRarity)(i % 5 + 1), 121 + i));
            return receipt;
        }

        private static void SamplePresentation(Sample sample)
        {
            if (sample.Fixture >= 11) Invoke("SampleItemsForPreview", sample.Fixture == 11 ? _itemReceipt : _overflowReceipt, sample.Elapsed);
            else Invoke("SampleForPreview", Presentation(sample), sample.Elapsed);
        }

        private static void ValidateScreenImpulse()
        {
            var cinematic = _document.rootVisualElement.Q("summon-cinematic");
            var before = JsonUtility.ToJson(UnityEngine.Random.state);
            foreach (var t in new[] { .54f, 1.54f })
            {
                Invoke("SampleForPreview", _themeReceipts[0], t);
                Check(cinematic.style.translate.value.x.value * cinematic.style.translate.value.x.value +
                    cinematic.style.translate.value.y.value * cinematic.style.translate.value.y.value > 9,
                    "X screen-entry/rush has no visible screen impulse.");
            }
            Invoke("SampleForPreview", _themeReceipts[0], 3.1f);
            Check(Math.Abs(cinematic.style.translate.value.x.value) < .001f && Math.Abs(cinematic.style.translate.value.y.value) < .001f &&
                Vector3.Distance(cinematic.style.scale.value.value, Vector3.one) < .001f, "Screen impulse persists into the inspection hold.");
            Check(JsonUtility.ToJson(UnityEngine.Random.state) == before, "Screen shake advances the gameplay random stream.");
            Results.Add("PASS Visible X screen-entry/rush impulses settle to neutral before inspection and leave RNG unchanged.");
        }

        private static void ValidateItems(Sample sample, Vector2Int size)
        {
            var receipt = sample.Fixture == 11 ? _itemReceipt : _overflowReceipt;
            var root = _document.rootVisualElement;
            var cards = root.Query<ObtainRewardCell>().ToList();
            Check(cards.Count == receipt.Items.Count + receipt.Artifacts.Count, "Shared obtain board dropped an item or artifact instance.");
            Check(root.Q("summon-cinematic").style.display == DisplayStyle.None, "Ordinary items unexpectedly launch a fighter reveal.");
            Check(root.Q<ScrollView>("obtain-item-scroll") != null, "Obtain item grants lack overflow scrolling.");
            Check(root.Q<Label>(className: "summon-board-watermark").text == "ITEMS OBTAINED", "Item receipt retains summon-only copy.");
            Check(root.Query<Label>(className: "item-reward-amount").ToList().Any(l => l.text == "2,500"), "Item receipt lost its exact coin amount.");
            Check(root.Query<Button>(className: "artifact-item-tile").ToList().Count == receipt.Artifacts.Count, "Artifact instances were merged by item ID.");
            if (sample.Name != "Items-Arrival")
            {
                Check(AwaitingClose && !Playing, "Item obtain does not wait for Tap to Close.");
                Check(root.Q<Button>("summon-result-close").worldBound.yMin >= root.Q("summon-reward-board").worldBound.yMax, "Close control overlaps the item row.");
                var scroll = root.Q<ScrollView>("obtain-item-scroll");
                Check(Bounded(scroll.worldBound, size), "Item grant scrolling region exceeds the viewport.");
                var hit = root.panel.Pick(cards[0].worldBound.center);
                var pickedName = hit?.name + "/" + hit?.GetType().Name;
                while (hit != null && !(hit is ObtainRewardCell)) hit = hit.parent;
                Check(hit != null, "Reward tile is not pickable: " + pickedName + "; cell=" + cards[0].worldBound + "; grid=" + root.Q("summon-reward-grid").worldBound + "; viewport=" + scroll.contentViewport.worldBound);
                Check(scroll.mode == ScrollViewMode.Horizontal, "Item obtain scroll is not horizontal.");
                Check(cards.Select(c => Mathf.RoundToInt(c.worldBound.y)).Distinct().Count() == 1, "Item obtains wrap onto multiple rows.");
                if (sample.Name == "Items-Overflow") Check(scroll.contentContainer.layout.width > scroll.contentViewport.layout.width,
                    "Long item receipts do not create horizontal scrolling.");
                foreach (var card in cards)
                {
                    Check(card.worldBound.yMin >= 0 && card.worldBound.yMax <= size.y + 1, "An item obtain tile is vertically clipped.");
                    Check(Math.Abs(card.Q(className: "item-reward-frame").worldBound.width - SummonSequenceView.ObtainTileSize) < 1, "Item grid has not restored its larger obtain size.");
                }
            }
            Check(Vector3.Distance(cards[0].resolvedStyle.scale.value, Vector3.one) < .001f, "Item obtain uses a size-pop entrance.");
        }

        private static object Presentation(Sample sample) => sample.Fixture < 4 ? (object)Draws(sample) : Receipts(sample);
        private static SummonDrawPresentation[] Receipts(Sample sample) => sample.Fixture == 4 ? _lowReceipts : sample.Fixture == 5 ? _mixedReceipts : _themeReceipts[sample.Fixture - 6];
        private static CharacterObject[] Draws(Sample sample) => sample.Fixture == 0 ? _low : sample.Fixture == 1 ? _mixed : sample.Fixture == 2 ? _tenLow : sample.Fixture == 3 ? _tenMixed : Receipts(sample).Select(draw => draw.Character).ToArray();

        private static void CheckReceiptItem(VisualElement card, SummonDrawPresentation draw)
        {
            Check(card.ClassListContains(draw.IsNew ? "summon-item-new" : "summon-item-duplicate"), "Receipt item uses the wrong NEW/duplicate presentation.");
            var aura = card.Q("summon-new-aura");
            var wantsAura = draw.IsNew && draw.Character.FighterRarity >= FighterRarity.SSR;
            Check((aura != null) == wantsAura, "SSR backdrop aura must appear only on NEW premium items.");
            if (aura != null)
            {
                var theme = typeof(CharacterObject).Assembly.GetType("FightingAllstar.Presentation.Navigation.SummonAttributeTheme", true);
                var color = (Color)theme.GetMethod("ColorFor", BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[] { draw.Character.FighterAttribute });
                var accent = ReadGraphicProperty<Color>(aura, "Accent");
                Check(Math.Abs(accent.r - color.r) < .001f && Math.Abs(accent.g - color.g) < .001f && Math.Abs(accent.b - color.b) < .001f,
                    "NEW premium aura uses the wrong attribute color.");
            }
            if (draw.IsNew)
            {
                CheckSharedIcon(card, draw.Character);
                Check(card.Q("summon-new-label") != null, "NEW reward has no NEW badge.");
                Check(card.Q("summon-fragment") == null, "NEW reward was replaced with a memory fragment.");
                return;
            }

            Check(card.Q("character-icon") == null && card.Q("summon-new-label") == null, "Duplicate memory displays a full NEW character card or badge.");
            var fragment = card.Q("summon-fragment");
            Check(fragment != null, "Duplicate reward has no hexagonal memory fragment.");
            Check(ReadGraphicProperty<Sprite>(fragment, "PortraitSprite") == draw.Character.FighterIcon, "Memory glyph uses the wrong fighter portrait.");
            var art = CharacterIconArtSO.Load();
            var rarity = art.Rarity(draw.Character.FighterRarity) ?? art.Rarity(FighterRarity.SSR);
            Check(ReadGraphicProperty<Sprite>(fragment, "BackgroundSprite") == rarity.background, "Memory glyph uses the wrong shared rarity background.");
            Check(ReadGraphicProperty<FighterRarity>(fragment, "Rarity") == draw.Character.FighterRarity, "Memory glyph uses the wrong rarity geometry color.");
            CheckLayer(fragment, "fragment-attribute", art.Attribute(draw.Character.FighterAttribute));
            CheckLayer(fragment, "fragment-series", art.Series(draw.Character.SeriesId));
            Check(fragment.Q("fragment-series-fallback") == null, "Duplicate series must remain empty when no sprite is assigned.");
            CheckLayer(fragment, "fragment-rarity", rarity.label);
            Check(card.Q<Label>("fragment-kind")?.text == "MEMORY", "Duplicate reward is not identified as MEMORY.");
            var count = card.Q<Label>("fragment-count");
            Check(count != null, "Memory reward is missing its progression/count label.");
            var expected = draw.FragmentCount > 0 ? draw.FragmentCount.ToString() : draw.ConstellationTier >= 0 ? "C" + draw.ConstellationTier : string.Empty;
            Check(count.text.Replace(" ", string.Empty) == expected, "Memory reward shows an invented or incorrect payout/progression.");
            Check((count.style.display == DisplayStyle.None) == string.IsNullOrEmpty(expected), "Unknown memory payout should not display a count.");
        }

        private static T ReadGraphicProperty<T>(VisualElement root, string name)
        {
            foreach (var element in new[] { root }.Concat(root.Query<VisualElement>().ToList()))
            {
                var property = element.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (property != null && property.PropertyType == typeof(T)) return (T)property.GetValue(element);
                var field = element.GetType().GetField(name, Members) ?? element.GetType().GetField("_" + char.ToLowerInvariant(name[0]) + name.Substring(1), Members);
                if (field != null && field.FieldType == typeof(T)) return (T)field.GetValue(element);
            }
            throw new InvalidOperationException("Visual graphic is missing its " + name + " binding.");
        }

        private static void CheckSharedIcon(VisualElement card, CharacterObject fighter)
        {
            var icon = card.Q("character-icon");
            Check(icon != null && icon.Q("icon-root")?.ClassListContains("character-icon") == true, "Reward does not use the shared CharacterIcon template.");
            var art = CharacterIconArtSO.Load();
            Check(art != null, "Shared CharacterIcon artwork configuration is missing.");
            var grade = art.Rarity(fighter.FighterRarity) ?? art.Rarity(FighterRarity.SSR);
            Check(grade != null, "Shared rarity artwork is missing.");
            CheckLayer(icon, "char-icon", fighter.FighterIcon);
            CheckLayer(icon, "rarity-background", grade.background);
            CheckLayer(icon, "rarity-border", grade.frame);
            CheckLayer(icon, "attribute", art.Attribute(fighter.FighterAttribute));
            CheckLayer(icon, "rarity-label", grade.label);
            CheckLayer(icon, "series-label", art.Series(fighter.SeriesId));
        }

        private static void CheckLayer(VisualElement icon, string name, Sprite sprite)
        {
            var layer = icon.Q<Image>(name);
            Check(layer != null && layer.sprite == sprite, "Shared CharacterIcon layer has the wrong sprite: " + name);
            Check(layer.style.display.value == (sprite == null ? DisplayStyle.None : DisplayStyle.Flex), "Shared CharacterIcon layer has the wrong visibility: " + name);
        }

        private static void CheckTenGrid(Vector2Int size)
        {
            var root = _document.rootVisualElement;
            var cards = Enumerable.Range(0, 10).Select(i => root.Q("summon-card-" + i)).ToArray();
            var rows = cards.GroupBy(c => Mathf.RoundToInt(c.worldBound.y)).ToArray();
            Check(rows.Length == 2 && rows.All(row => row.Count() == 5), "Character gacha results must use two rows of five.");
            var board = root.Q("summon-reward-board").worldBound;
            foreach (var card in cards)
                Check(Bounded(card.worldBound, size) && card.worldBound.yMin >= board.yMin - 1 && card.worldBound.yMax <= board.yMax + 1,
                    "A character obtain card exceeds the board.");
        }

        private static void ValidateFixtureCases()
        {
            var size = Sizes[Sizes.Length - 1];
            var root = _document.rootVisualElement;
            foreach (var rarity in (FighterRarity[])Enum.GetValues(typeof(FighterRarity)))
            {
                var copy = UnityEngine.Object.Instantiate(_mixed[2]);
                try
                {
                    copy.FighterRarity = rarity;
                    var draws = new[] { copy };
                    Invoke("SampleForPreview", draws, .8f);
                    Render(size, null);
                    var premium = rarity >= FighterRarity.SSR;
                    Check((root.Q("summon-cinematic").style.display == DisplayStyle.Flex) == premium, rarity + " used the wrong presentation path.");
                    Check(Math.Abs(Duration - (premium ? 5.4f : 1.3f)) < .02f, rarity + " single draw has incorrect timing.");
                    if (premium)
                    {
                        Invoke("SampleForPreview", draws, 2.7f);
                        Check(root.Q<Label>("summon-rarity").text == rarity.ToString(), rarity + " reveal has the wrong grade.");
                        Check(root.Q<Image>("summon-hero").sprite == copy.FighterFull || root.Q<Image>("summon-hero").sprite == copy.FighterPic || root.Q<Image>("summon-hero").sprite == copy.FighterIcon,
                            rarity + " reveal did not preserve the cloned artwork.");
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(copy); }
            }
            Results.Add("PASS Single draws dispatch all seven rarity tiers correctly, using temporary asset clones only.");

            Invoke("SampleForPreview", new CharacterObject[] { null, _low[0], null, _mixed[2] }, 8f);
            Render(size, null);
            Check(root.Q("summon-card-0") != null && root.Q("summon-card-1") != null && root.Q("summon-card-2") == null,
                "Null entries created phantom cards or removed a valid draw.");
            Results.Add("PASS Null entries are ignored while preserving valid draw order.");

            Invoke("SampleForPreview", _tenMixed, 3.1f);
            Check(root.Q<Label>("summon-name").text == _tenMixed[2].FighterName, "First SSR reveal is out of draw order.");
            Check(root.Q("summon-card-1").style.opacity.value >= .99f && root.Q("summon-card-2").style.opacity.value < .01f,
                "First SSR arrived before its cinematic ended.");
            Invoke("SampleForPreview", _tenMixed, 7.6f);
            Check(root.Q<Label>("summon-name").text == _tenMixed[4].FighterName, "Second SSR reveal is out of draw order.");
            Check(root.Q("summon-card-2").style.opacity.value >= .99f && root.Q("summon-card-3").style.opacity.value >= .99f && root.Q("summon-card-4").style.opacity.value < .01f,
                "Repeated SSR timelines overlap or fail to resume earlier reward arrivals.");
            Check(Math.Abs(Duration - 11.3f) < .02f, "Repeated SSR duration does not include both reveals.");
            Results.Add("PASS Mixed ten-pull preserves draw order and runs both SSR reveals before their respective card arrivals.");
        }

        private static IEnumerable<object> ValidateLifecycle()
        {
            ValidateRevealHold(_tenMixed, _tenMixed[2], _tenMixed[4]);
            ValidateRevealHold(_mixedReceipts, _mixedReceipts[2].Character, _mixedReceipts[4].Character);
            Results.Add("PASS Each SSR holds after settling until input, including duplicate SSR; long frame deltas cannot pass either reveal.");

            var callbacks = 0;
            Invoke("Play", _mixed, (Action)(() => callbacks++));
            Check(Playing, "Play did not enter the playing state.");
            Invoke("Skip");
            Check(callbacks == 0 && !Playing && AwaitingClose, "Skip must show final rewards without closing or releasing the purchase guard.");
            Invoke("Skip");
            Check(callbacks == 0 && AwaitingClose, "Repeated skip must leave results open.");
            Invoke("Close"); Invoke("Close");
            foreach (var step in WaitForCallback(() => callbacks)) yield return step;
            Check(callbacks == 1 && !AwaitingClose, "Tap to close must finish exactly once.");
            Check(_document.rootVisualElement.Q("summon-sequence").style.display == DisplayStyle.None, "Close callback ran before the exit animation hid the sequence.");
            Results.Add("PASS Skip retains the same final board; asynchronous close completes once after the exit animation; repeated inputs are harmless.");

            callbacks = 0;
            Invoke("Play", _mixedReceipts, (Action)(() => callbacks++));
            Check(Playing, "Receipt overload did not enter the playing state.");
            Invoke("Skip");
            Invoke("Skip");
            Check(callbacks == 0 && AwaitingClose, "Receipt results must wait for tap to close.");
            Invoke("Close");
            foreach (var step in WaitForCallback(() => callbacks)) yield return step;
            Check(callbacks == 1 && !Playing, "Receipt overload did not complete exactly once on skip.");
            Results.Add("PASS Committed NEW/memory receipt overload follows the same completion lifecycle.");

            callbacks = 0;
            Invoke("Play", Array.Empty<CharacterObject>(), (Action)(() => callbacks++));
            Check(callbacks == 1 && !Playing, "Empty draws did not complete immediately once.");
            callbacks = 0;
            Invoke("Play", null, (Action)(() => callbacks++));
            Check(callbacks == 1 && !Playing, "Null draws did not complete immediately once.");
            Results.Add("PASS Empty and null draws complete immediately once.");

            callbacks = 0;
            Invoke("Play", _mixed, (Action)(() => callbacks++));
            Invoke("Dispose");
            Check(!Playing && callbacks == 0, "Dispose left playback running or invoked the reward callback.");
            Invoke("Dispose");
            var overlay = _document.rootVisualElement.Q("summon-sequence");
            Check(overlay == null || overlay.style.display == DisplayStyle.None, "Disposed overlay remains visible.");
            _view = null;
            Results.Add("PASS Dispose cancels playback, suppresses completion, removes or hides the overlay, and can be repeated.");
        }

        private static IEnumerable<object> WaitForCallback(Func<int> count)
        {
            var deadline = EditorApplication.timeSinceStartup + 3;
            while (count() == 0 && EditorApplication.timeSinceStartup < deadline) yield return null;
            Check(count() == 1, "Exit animation did not invoke completion exactly once within three seconds.");
        }

        private static void ValidateRevealHold(object draws, CharacterObject first, CharacterObject second)
        {
            var callbacks = 0;
            Invoke("Play", draws, (Action)(() => callbacks++));
            Check(Playing && !AwaitingReveal, "Playback entered the SSR input hold before the reveal settled.");
            var opening = (float)_viewType.GetField("_elapsed", Members).GetValue(_view);
            Invoke("AdvancePremium");
            Check(Math.Abs((float)_viewType.GetField("_elapsed", Members).GetValue(_view) - opening) < .001f,
                "Input skipped an SSR before the settled input hold.");
            Invoke("AdvancePlayback", 120f);
            var held = (float)_viewType.GetField("_elapsed", Members).GetValue(_view);
            Check(Playing && AwaitingReveal && !AwaitingClose && callbacks == 0, "A large frame delta passed the first SSR input hold.");
            Check(_document.rootVisualElement.Q<Label>("summon-name").text == first.FighterName, "The first SSR hold shows the wrong fighter.");
            Invoke("AdvancePlayback", 120f);
            Check(AwaitingReveal && Math.Abs((float)_viewType.GetField("_elapsed", Members).GetValue(_view) - held) < .001f,
                "A settled SSR reveal advanced without player input.");
            Invoke("AdvancePremium");
            Check(Playing && !AwaitingReveal, "Continuing the first SSR did not release its input hold.");
            Invoke("AdvancePlayback", 120f);
            Check(Playing && AwaitingReveal && !AwaitingClose && callbacks == 0, "Continuing the first SSR bypassed the second SSR input hold.");
            Check(_document.rootVisualElement.Q<Label>("summon-name").text == second.FighterName, "The second SSR hold shows the wrong fighter.");
            Invoke("AdvancePlayback", 120f);
            Check(Playing && AwaitingReveal && callbacks == 0, "The second SSR input hold advanced on its own.");
            Invoke("SampleForPreview", draws, 1000f);
            Check(!Playing && !AwaitingReveal && AwaitingClose && callbacks == 0, "Deterministic preview sampling became gated by live input or invoked completion.");
        }

        private static void ValidateAudioSynthesis()
        {
            var audioType = typeof(CharacterObject).Assembly.GetType("FightingAllstar.Presentation.Navigation.SummonSequenceAudio", true);
            var originalRandom = JsonUtility.ToJson(UnityEngine.Random.state);
            var preview = new List<float>();
            foreach (var methodName in new[] { "CreateArrival", "CreateCharge", "CreateImpact" })
            {
                var factory = audioType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
                Check(factory != null, "Audio synthesis factory is missing.");
                for (var variant = 0; variant < 3; variant++)
                {
                    AudioClip clip = null, repeated = null;
                    try
                    {
                        clip = (AudioClip)factory.Invoke(null, new object[] { variant });
                        repeated = (AudioClip)factory.Invoke(null, new object[] { variant });
                        var samples = new float[clip.samples * clip.channels];
                        var again = new float[repeated.samples * repeated.channels];
                        Check(clip.GetData(samples, 0) && repeated.GetData(again, 0), "Generated audio samples cannot be read.");
                        Check(samples.Length > 0 && samples.All(value => !float.IsNaN(value) && !float.IsInfinity(value) && Math.Abs(value) <= .3601f), "Generated cue contains an invalid sample or exceeds its peak limit.");
                        Check(samples.Any(value => Math.Abs(value) > .01f), "Generated cue is silent.");
                        Check(samples.SequenceEqual(again), "Repeated audio synthesis is not deterministic.");
                        Check(Math.Abs(samples[0]) < .001f && Math.Abs(samples[samples.Length - 1]) < .001f, "Cue envelope does not fade its boundaries.");
                        if ((methodName == "CreateArrival" && variant == 0) || variant == 2 && methodName != "CreateArrival")
                        {
                            preview.AddRange(samples);
                            preview.AddRange(new float[8820]);
                        }
                    }
                    finally
                    {
                        if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
                        if (repeated != null) UnityEngine.Object.DestroyImmediate(repeated);
                    }
                }
            }
            Check(JsonUtility.ToJson(UnityEngine.Random.state) == originalRandom, "Audio synthesis advanced Unity's random stream.");
            WriteWave(OutputFolder + "/Audio-Preview.wav", preview);
            Results.Add("PASS Nine original audio cues are deterministic, finite, peak limited, faded at their boundaries, and leave Unity RNG unchanged.");
        }

        private static void WriteWave(string path, IReadOnlyList<float> samples)
        {
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + samples.Count * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(44100); writer.Write(88200); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples.Count * 2);
                for (var i = 0; i < samples.Count; i++) writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(samples[i], -1, 1) * short.MaxValue));
            }
        }

        private static void CheckPresentationIsolation()
        {
            var path = "Assets/Project/Runtime/Presentation/Navigation/SummonSequenceView.cs";
            if (!File.Exists(path)) return;
            var source = File.ReadAllText(path);
            var prohibited = new[] { "PlayerInventoryService", "PlayerPrefs", "TrySummon(", "AddCharacter(", "AccountService", "Firebase" };
            foreach (var token in prohibited) Check(!source.Contains(token), "Presentation accesses persistence or summon transactions: " + token);
            Results.Add("PASS SummonSequenceView source has no inventory, wallet, account, or transaction dependency.");
        }

        private static bool Playing => (bool)_viewType.GetProperty("IsPlaying", Members).GetValue(_view);
        private static bool AwaitingClose => (bool)_viewType.GetProperty("IsAwaitingClose", Members).GetValue(_view);
        private static bool AwaitingReveal => (bool)_viewType.GetProperty("IsAwaitingRevealInput", Members).GetValue(_view);
        private static float Duration => (float)_viewType.GetProperty("Duration", Members).GetValue(_view);
        private static object Invoke(string name, params object[] arguments)
        {
            var method = _viewType.GetMethods(Members).Where(candidate => candidate.Name == name)
                .Where(candidate => candidate.GetParameters().Length == arguments.Length)
                .Where(candidate => candidate.GetParameters().Select((parameter, i) => arguments[i] == null || parameter.ParameterType.IsInstanceOfType(arguments[i])).All(match => match))
                // Keep untyped null calls on the original CharacterObject API.
                .OrderByDescending(candidate => candidate.GetParameters().Any(parameter => parameter.ParameterType == typeof(IReadOnlyList<CharacterObject>)))
                .FirstOrDefault();
            Check(method != null, "Runtime API is missing " + name + ".");
            return method.Invoke(_view, arguments);
        }

        private static bool Bounded(Rect rect, Vector2Int size) => rect.width > 0 && rect.height > 0 && rect.xMin >= -1 && rect.yMin >= -1 && rect.xMax <= size.x + 1 && rect.yMax <= size.y + 1;
        private static void Check(bool pass, string message) { if (!pass) throw new InvalidOperationException(message); }

        private static void Render(Vector2Int size, string imageName)
        {
            var active = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                RenderTexture.active = _target;
                GL.Clear(true, true, Color.clear);
                typeof(PanelSettings).GetMethod("ApplyPanelSettings", Members)?.Invoke(_settings, null);
                var panel = _document.rootVisualElement.panel;
                Check(panel != null, "Preview panel did not attach.");
                var type = panel.GetType();
                type.GetMethod("Update", Members, null, Type.EmptyTypes, null)?.Invoke(panel, null);
                type.GetMethod("Update", Members, null, Type.EmptyTypes, null)?.Invoke(panel, null);
                type.GetMethod("Repaint", Members, null, new[] { typeof(Event) }, null)?.Invoke(panel, new object[] { new Event { type = EventType.Repaint } });
                type.GetMethod("Render", Members, null, Type.EmptyTypes, null)?.Invoke(panel, null);
                if (imageName == null) return;
                RenderTexture.active = _target;
                pixels = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(OutputFolder + "/" + imageName + "-" + size.x + "x" + size.y + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = active;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        private static void SetStatus(string status)
        {
            Status = status;
            File.WriteAllLines(VerificationPath, Results.Concat(new[] { status }));
        }

        private static void Fail(Exception error)
        {
            var cause = error is TargetInvocationException && error.InnerException != null ? error.InnerException : error;
            Results.Add("FAIL " + cause);
            SetStatus("FAIL");
            Debug.LogException(cause);
            Cleanup();
        }

        private static void BeforeReload()
        {
            if (!IsRunning) return;
            Results.Add("INTERRUPTED by script reload; run the preview again after compilation.");
            SetStatus("INTERRUPTED");
            Cleanup();
        }

        private static void DisposeScreen()
        {
            var view = _view;
            _view = null;
            try { if (view != null) _viewType.GetMethod("Dispose", Members)?.Invoke(view, null); }
            finally
            {
                if (_screen != null) UnityEngine.Object.DestroyImmediate(_screen);
                if (_target != null) { _target.Release(); UnityEngine.Object.DestroyImmediate(_target); }
                if (_settings != null) UnityEngine.Object.DestroyImmediate(_settings);
                _screen = null;
                _document = null;
                _target = null;
                _settings = null;
            }
        }

        private static void Cleanup()
        {
            _lifecycle?.Dispose(); _lifecycle = null;
            DisposeScreen();
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            foreach (var clone in FixtureClones) if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
            FixtureClones.Clear();
            _scene = default;
            IsRunning = false;
            UiPreviewSession.Release();
        }

        private readonly struct Sample
        {
            public readonly string Name;
            public readonly int Fixture;
            public readonly float Elapsed;
            public Sample(string name, int fixture, float elapsed) { Name = name; Fixture = fixture; Elapsed = elapsed; }
        }
    }
}
