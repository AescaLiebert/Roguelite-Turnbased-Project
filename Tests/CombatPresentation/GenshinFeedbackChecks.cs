#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class GenshinFeedbackChecks
{
    static GenshinFeedbackChecks() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool("GenshinFeedbackChecks", true); EditorApplication.isPlaying = true; }
    private static void Tick()
    {
        if (!SessionState.GetBool("GenshinFeedbackChecks", false) || !EditorApplication.isPlaying) return;
        SessionState.SetBool("GenshinFeedbackChecks", false);
        new GameObject("Genshin feedback checks").AddComponent<GenshinFeedbackRunner>();
    }
}

public sealed class GenshinFeedbackRunner : MonoBehaviour
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private CoreBattleSceneController controller;
    private UIDocument document;
    private string output;
    private int checks;
    private void Require(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    private object Call(string name, params object[] args) => typeof(CoreBattleSceneController).GetMethod(name, Private).Invoke(controller, args);
    private void Set(string name, object value) => typeof(CoreBattleSceneController).GetField(name, Private).SetValue(controller, value);
    private void Capture(string name) => FightingAllstar.EditorTools.BattlePreviewCapture.Save(Path.Combine(output, name + ".png"), document, 1280, 720);
    private IEnumerator Start() { yield return Guard(Run()); }
    private IEnumerator Guard(IEnumerator routine)
    {
        while (true)
        {
            object next;
            try { if (!routine.MoveNext()) break; next = routine.Current; }
            catch (Exception error) { Debug.LogError(error); EditorApplication.Exit(1); yield break; }
            if (next is IEnumerator nested) yield return Guard(nested); else yield return next;
        }
    }
    private IEnumerator Run()
    {
        output = Path.Combine(Directory.GetCurrentDirectory(), "Evidence"); Directory.CreateDirectory(output);
        var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>(); camera.tag = "MainCamera";
        camera.orthographic = true; camera.orthographicSize = 4.5f; camera.transform.position = new Vector3(0, 0, -10);
        camera.aspect = 1280f / 720f;
        camera.backgroundColor = new Color(.045f, .055f, .08f); camera.clearFlags = CameraClearFlags.SolidColor;
        var light = new GameObject("Key Light", typeof(Light)).GetComponent<Light>(); light.type = LightType.Directional;
        var documentObject = new GameObject("UI"); documentObject.SetActive(false);
        document = documentObject.AddComponent<UIDocument>();
        document.panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        document.panelSettings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UnityDefaultRuntimeTheme.tss");
        document.panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
        document.panelSettings.targetTexture = new RenderTexture(1280, 720, 24);
        documentObject.SetActive(true);
        var root = document.rootVisualElement;
        root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Project/UI/BattleCardHud.uss"));
        root.style.position = Position.Absolute; root.style.left = root.style.right = root.style.top = root.style.bottom = 0;
        var title = new Label("ELEMENTAL COMBAT FEEDBACK  ·  HYWenHei / zh-cn");
        title.style.unityFontDefinition = FontDefinition.FromFont(GenshinCombatTextStyle.Font);
        title.style.color = Color.white; title.style.fontSize = 22; title.style.marginLeft = 40; title.style.marginTop = 30; root.Add(title);
        var layer = new VisualElement(); layer.style.position = Position.Absolute;
        layer.style.left = layer.style.right = layer.style.top = layer.style.bottom = 0; root.Add(layer);
        controller = new GameObject("Presentation only").AddComponent<CoreBattleSceneController>(); controller.enabled = false;
        Set("_effectsLayer", layer);
        var feedbackRoster = GenshinStarterRoster.Create().Take(4).ToArray();
        var feedbackState = BattleEngine.Create("exclusive-fct", feedbackRoster, null, feedbackRoster, null, 7654);
        var genshinSource = feedbackState.Player.Fighters[0].Id;
        var legacySource = feedbackState.Opponent.Fighters[3].Id;
        feedbackState.Opponent.Fighters[3].Definition.TraitIds.Clear();
        feedbackState.Player.ElementalReactionsEnabled = false; // Style belongs to the fighter, even in a mixed roster.
        Set("_snapshot", feedbackState);
        var views = (Dictionary<string, GameObject>)typeof(CoreBattleSceneController).GetField("_fighterViews", Private).GetValue(controller);
        var elements = new[] { ElementId.Pyro, ElementId.Hydro, ElementId.Electro, ElementId.Cryo, ElementId.Dendro, ElementId.Anemo, ElementId.Geo };
        for (var i = 0; i < elements.Length; i++)
        {
            var actor = new GameObject("target" + i); actor.transform.position = new Vector3(-6 + i * 2, -1.25f, 0); views.Add(actor.name, actor);
            var label = new Label(elements[i].ToString()); label.style.position = Position.Absolute;
            label.style.left = 55 + i * 176; label.style.top = 390; label.style.fontSize = 18;
            label.style.color = GenshinCombatTextStyle.ElementColor(elements[i]); root.Add(label);
        }
        var stage = new GameObject("Stage").AddComponent<BattleStagePresenter>(); stage.Initialize(views); Set("_stage", stage);
        yield return null; yield return null;
        Require(GenshinCombatTextStyle.Font != null, "The requested local font did not load.");
        Require(GenshinCombatTextStyle.Font.fontNames.Contains("HYWenHei-85W"), "Font importer selected a family absent from the bundled typeface.");
        var pool = (FloatingCombatTextPool)typeof(CoreBattleSceneController).GetField("_fctPool", Private).GetValue(controller);
        var settings = FloatingCombatTextSettingsSO.GetDefault();
        // The isolated precompiled-runtime fixture has no source MonoScript GUID for the settings asset.
        var settingsData = new SerializedObject(settings);
        settingsData.FindProperty("fontAsset").objectReferenceValue = Resources.Load<Font>("UI/ArchivoBlack-Regular");
        settingsData.FindProperty("fontStyle").intValue = (int)FontStyle.BoldAndItalic;
        settingsData.FindProperty("overrideColors").boolValue = true;
        settingsData.ApplyModifiedPropertiesWithoutUndo();
        Require(settings.FontAsset != null && settings.FontAsset != GenshinCombatTextStyle.Font, "Legacy font fixture did not load its own font.");
        pool.Initialize(layer, 24, settings);
        foreach (var element in elements)
        {
            var item = pool.Acquire("12345", "critical", element: element, genshin: true);
            Require(item.Label.style.color.value == GenshinCombatTextStyle.ElementColor(element), "Critical lost element color.");
            Require(item.Label.style.unityFontStyleAndWeight.value == FontStyle.Normal, "FCT retained synthetic bold/italic.");
            pool.Release(item);
        }
        Require((bool)Call("UsesGenshinCombatText", genshinSource) && !(bool)Call("UsesGenshinCombatText", legacySource), "Style selection ignored the source fighter's trait.");
        var critical = pool.Acquire("CRITICAL\n12345", "critical", genshin: true);
        Require(critical.Kicker.style.display.value == DisplayStyle.None && critical.Label.text == "12345", "Genshin critical label was not removed."); pool.Release(critical);
        var block = pool.Acquire("BLOCK\n12345", "blocked", genshin: true);
        Require(block.Kicker.text == "BLOCK" && block.Kicker.style.display.value == DisplayStyle.Flex, "BLOCK lost its explicit cue."); pool.Release(block);
        var number = pool.Acquire(284552.ToString("N0"), "critical", element: ElementId.Pyro, genshin: true);
        Require(number.Label.text == "284552" && number.Label.style.unityFontDefinition.value.font == GenshinCombatTextStyle.Font,
            "Reference numerals retained grouping or the serif font.");
        var caption = pool.Acquire("Melt", "reaction", reaction: ElementalReactionId.Melt, genshin: true);
        Require(caption.Label.style.fontSize.value.value < number.Label.style.fontSize.value.value / 2,
            "Global FCT settings overrode reaction/critical size hierarchy.");
        pool.Release(number); pool.Release(caption);
        var overloaded = pool.Acquire("3633", "damage", element: ElementId.Pyro, reaction: ElementalReactionId.Overload, genshin: true);
        Require(overloaded.Label.style.color.value == GenshinCombatTextStyle.ReactionColor(ElementalReactionId.Overload),
            "Overloaded damage lost its red reaction identity."); pool.Release(overloaded);
        var legacy = pool.Acquire("12345", "critical", element: ElementId.Electro);
        Require(legacy.Kicker.text == "CRITICAL" && legacy.Label.style.unityFontStyleAndWeight.value == pool.Settings.FontStyle,
            "A reused Genshin item changed legacy critical typography.");
        Require(legacy.Label.style.unityFontDefinition.value.font == pool.Settings.FontAsset &&
            legacy.Label.style.color.value == pool.Settings.CriticalColor, "Genshin font/color leaked onto a non-Genshin caption."); pool.Release(legacy);
        var reused = pool.Acquire("4567", "damage");
        Require(reused.Group.style.width.keyword == StyleKeyword.Null, "Reference group width leaked onto a legacy hit."); pool.Release(reused);
        Require(GenshinCombatTextStyle.Scale(0, true, false) > GenshinCombatTextStyle.Scale(0, false, false) &&
            GenshinCombatTextStyle.Scale(0, false, false) > GenshinCombatTextStyle.Scale(.2f, false, false), "Critical entrance or shrinking scale is incorrect.");
        Require(GenshinCombatTextStyle.Opacity(0, 1.45f) == 0 && GenshinCombatTextStyle.Opacity(.1f, 1.45f) > .99f &&
            GenshinCombatTextStyle.Opacity(1.45f, 1.45f) == 0, "Fade-in/hold/fade-out envelope is incorrect.");
        Require(Mathf.Abs(GenshinCombatTextStyle.Offset(.2f, 1.45f, 0, false).y - GenshinCombatTextStyle.Offset(.1f, 1.45f, 0, false).y) >
            Mathf.Abs(GenshinCombatTextStyle.Offset(1.2f, 1.45f, 0, false).y - GenshinCombatTextStyle.Offset(1.1f, 1.45f, 0, false).y), "Upward slide does not slow down.");
        for (var i = 0; i < elements.Length; i++)
            Call("FloatText", "target" + i, (1248 + i * 113).ToString(), i % 2 == 0 ? "critical" : "damage", 1, 1f,
                AttributeAffinity.Neutral, elements[i], ElementalReactionId.None, genshinSource);
        foreach (var row in new[] { (0, ElementalReactionId.Melt, ElementId.Pyro, ElementId.Cryo),
            (3, ElementalReactionId.Freeze, ElementId.Cryo, ElementId.Hydro), (6, ElementalReactionId.Swirl, ElementId.Anemo, ElementId.Pyro) })
            controller.StartCoroutine((IEnumerator)Call("PresentReactionFeedback", new BattleEvent { SourceId = genshinSource, TargetId = "target" + row.Item1,
                ElementalReaction = row.Item2, TriggerElement = row.Item3, DamageElement = row.Item4 }));
        yield return new WaitForSecondsRealtime(.035f); Capture("01-elemental-contact");
        Require(layer.Query<VisualElement>("elemental-contact").ToList().Count == 3, "Contact convergence was not visible.");
        yield return new WaitForSecondsRealtime(.18f); Capture("02-reaction-impact");
        Require(layer.Query<Label>().ToList().Any(l => l.text == "Melt"), "Reaction caption never appeared.");
        var reactionCues = (Dictionary<int, AudioClip>)typeof(BattleStagePresenter).GetField("_cues", Private).GetValue(stage);
        Require(reactionCues.Keys.Count(k => k >= 10) == 3 && reactionCues.Values.All(c => c.samples > 0), "Distinct reaction audio cues were not generated at impact.");
        stage.ClearAuras();
        Require(!stage.GetComponent<AudioSource>().isPlaying, "Reaction audio survived presentation cleanup.");
        yield return new WaitForSecondsRealtime(.28f); Capture("03-float-settle");
        yield return new WaitForSecondsRealtime(.5f); Capture("04-late-fade");
        yield return new WaitForSecondsRealtime(.9f);
        Require(!layer.Query<VisualElement>("elemental-contact").ToList().Any(), "Contact effect leaked after playback.");
        Require(layer.Query<Label>().ToList().All(l => string.IsNullOrEmpty(l.text)), "Pooled captions did not clear after playback.");
        var tooltip = new VisualElement(); root.Add(tooltip); Set("_tooltipContainer", tooltip);
        Set("_tooltipCardId", feedbackState.Player.Hand[0].Id); Call("RenderTooltip");
        Require(root.Q("reaction-forecast") == null && !tooltip.Query<Label>().ToList().Any(l => l.text.Contains("Reaction forecast")), "Removed prediction UI was rendered.");
        tooltip.RemoveFromHierarchy();
        var comparison = new Label("GENSHIN: critical / BLOCK                       OTHER SERIES: critical / BLOCK");
        comparison.style.position = Position.Absolute; comparison.style.left = 150; comparison.style.top = 140; comparison.style.color = Color.white; comparison.style.fontSize = 20; root.Add(comparison);
        Call("FloatText", "target1", "4567", "critical", 1, 1f, AttributeAffinity.Neutral, ElementId.Pyro, ElementalReactionId.None, genshinSource);
        Call("FloatText", "target2", "BLOCK\n1234", "blocked", 1, 1f, AttributeAffinity.Neutral, ElementId.Hydro, ElementalReactionId.None, genshinSource);
        Call("FloatText", "target4", "4567", "critical", 1, 1f, AttributeAffinity.Neutral, ElementId.Pyro, ElementalReactionId.None, legacySource);
        Call("FloatText", "target5", "BLOCK\n1234", "blocked", 1, 1f, AttributeAffinity.Neutral, ElementId.Hydro, ElementalReactionId.None, legacySource);
        yield return new WaitForSecondsRealtime(.15f); Capture("05-exclusive-style-comparison");
        yield return new WaitForSecondsRealtime(1.5f); comparison.RemoveFromHierarchy();
        var gaugeRoot = new GameObject("Fractional gauge", typeof(RectTransform), typeof(Canvas));
        gaugeRoot.SetActive(false);
        var canvas = gaugeRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
        var gaugeRect = gaugeRoot.GetComponent<RectTransform>(); gaugeRect.sizeDelta = new Vector2(500, 40);
        gaugeRect.position = new Vector3(0, -2.5f, 0); gaugeRect.localScale = Vector3.one * .01f;
        var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        fillObject.transform.SetParent(gaugeRoot.transform, false);
        var fill = fillObject.GetComponent<UnityEngine.UI.Image>();
        fill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f);
        fill.color = new Color(1, .59f, 0);
        var hud = gaugeRoot.AddComponent<CoreFighterHud>(); hud.enabled = false;
        var gaugeSlider = gaugeRoot.AddComponent<UnityEngine.UI.Slider>(); gaugeSlider.fillRect = fill.rectTransform;
        typeof(CoreFighterHud).GetField("powerGaugeSlider", Private).SetValue(hud, gaugeSlider);
        gaugeRoot.SetActive(true);
        hud.SetPowerGauge(1); hud.SetPowerGaugeRemainder(5000);
        var partial = gaugeRoot.transform.Find("PGSegment_1").GetComponent<UnityEngine.UI.Image>();
        Require(partial.gameObject.activeSelf && Mathf.Approximately(partial.fillAmount, .5f), "Fractional orb was not half filled.");
        var gaugeLabel = new Label("Power Gauge  1.5 / 5"); gaugeLabel.style.position = Position.Absolute;
        gaugeLabel.style.left = 470; gaugeLabel.style.top = 510; gaugeLabel.style.fontSize = 22; gaugeLabel.style.color = Color.white; root.Add(gaugeLabel);
        Canvas.ForceUpdateCanvases(); yield return null; yield return null;
        Canvas.ForceUpdateCanvases(); Capture("06-fractional-gauge");
        hud.SetPowerGauge(2); hud.SetPowerGaugeRemainder(0);
        Require(Mathf.Approximately(partial.fillAmount, 1), "A completed orb retained its partial fill.");
        controller.StartCoroutine((IEnumerator)Call("PresentReactionFeedback", new BattleEvent { TargetId = "target0", ElementalReaction = ElementalReactionId.Melt,
            TriggerElement = ElementId.Pyro, DamageElement = ElementId.Cryo }));
        yield return null;
        Require(layer.Q<VisualElement>("elemental-contact") != null, "Cleanup regression did not enter contact playback.");
        Call("OnDisable");
        Require(!layer.Query<VisualElement>("elemental-contact").ToList().Any() && !stage.GetComponent<AudioSource>().isPlaying,
            "Interrupted presentation leaked contact effects or audio.");
        // Sample the production envelope at fixed frame times so screenshot encoding cannot distort the preview's timing.
        gaugeRoot.SetActive(false); root.Clear(); root.Add(layer);
        var specimenTitle = new Label("GENSHIN FIGHTERS  ·  reference study");
        specimenTitle.style.position = Position.Absolute; specimenTitle.style.left = 40; specimenTitle.style.top = 35;
        specimenTitle.style.fontSize = 24; specimenTitle.style.color = Color.white; root.Add(specimenTitle);
        var specimens = new List<(FloatingCombatTextPool.Item item, float x, float y, int lane, float start, string kind)>();
        void Sample(string text, string kind, ElementId element, ElementalReactionId reaction, float x, float y, int lane = 0, float start = 0)
        {
            specimens.Add((pool.Acquire(text, kind, element: element, reaction: reaction, genshin: true), x, y, lane, start, kind));
        }
        Sample("284552", "critical", ElementId.Pyro, ElementalReactionId.None, 250, 240);
        Sample("Melt", "reaction", ElementId.None, ElementalReactionId.Melt, 250, 240);
        Sample("+3492", "heal", ElementId.None, ElementalReactionId.None, 440, 285, 0, .08f);
        Sample("1284", "damage", ElementId.Electro, ElementalReactionId.None, 710, 240);
        Sample("3633", "damage", ElementId.Pyro, ElementalReactionId.Overload, 710, 282, 0, .08f);
        Sample("Overloaded", "reaction", ElementId.None, ElementalReactionId.Overload, 710, 240);
        Sample("302", "damage", ElementId.Cryo, ElementalReactionId.None, 1050, 240);
        Sample("1054", "damage", ElementId.Electro, ElementalReactionId.None, 1050, 282, 0, .08f);
        Sample("Superconduct", "reaction", ElementId.None, ElementalReactionId.Superconduct, 1050, 240);
        Sample("250", "critical", ElementId.None, ElementalReactionId.None, 620, 450);
        Sample("5072", "critical", ElementId.Hydro, ElementalReactionId.None, 560, 460, 0, .08f);
        Sample("454", "damage", ElementId.None, ElementalReactionId.None, 600, 510, 1, .16f);
        Sample("3489", "critical", ElementId.Hydro, ElementalReactionId.None, 540, 565, 2, .24f);
        Sample("3489", "critical", ElementId.Hydro, ElementalReactionId.None, 690, 595, 3, .32f);
        Directory.CreateDirectory(Path.Combine(output, "Motion"));
        for (var frame = 0; frame < 48; frame++)
        {
            foreach (var sample in specimens)
            {
                var seconds = frame / 30f - sample.start;
                var duration = sample.kind == "reaction" ? 1.2f : 1.15f;
                var placement = GenshinCombatTextStyle.Placement(sample.lane, sample.kind == "reaction");
                var offset = GenshinCombatTextStyle.Offset(seconds, duration, sample.lane, sample.kind == "reaction");
                sample.item.Group.style.left = sample.x - GenshinCombatTextStyle.Width / 2 + placement.x + offset.x;
                sample.item.Group.style.top = sample.y + placement.y + offset.y;
                sample.item.Group.style.scale = new Scale(Vector3.one * GenshinCombatTextStyle.Scale(seconds, sample.kind == "critical", sample.kind == "reaction"));
                sample.item.Group.style.opacity = seconds < 0 ? 0 : GenshinCombatTextStyle.Opacity(seconds, duration);
            }
            yield return null;
            Capture("Motion/frame-" + frame.ToString("D3"));
            if (frame == 14) Capture("07-reference-specimens");
        }
        pool.Clear();
        File.WriteAllText(Path.Combine(output, "result.txt"), "PASS " + checks + " Genshin feedback assertions; source isolation, reference typography, animation, prediction removal, fractional gauge, reaction audio and interrupted cleanup checked.");
        EditorApplication.Exit(0);
    }
}
#endif
