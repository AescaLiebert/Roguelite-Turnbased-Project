#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class NeutralStatusHudChecks
{
    private static int assertions;
    private static double deadline;
    static NeutralStatusHudChecks() { EditorApplication.update += Tick; }
    public static void Run()
    {
        SessionState.SetBool("NeutralStatusHudChecks.Running", true);
        EditorApplication.EnterPlaymode();
    }

    private static void Check(bool value, string message)
    {
        assertions++;
        if (!value) throw new Exception(message);
    }

    private static void Tick()
    {
        if (!SessionState.GetBool("NeutralStatusHudChecks.Running", false)) return;
        if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 120;
        if (EditorApplication.timeSinceStartup > deadline)
        {
            SessionState.SetBool("NeutralStatusHudChecks.Running", false);
            Debug.LogError("Neutral HUD fixture timed out.");
            EditorApplication.Exit(1);
            return;
        }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        SessionState.SetBool("NeutralStatusHudChecks.Running", false);
        Verify();
    }

    private static void Verify()
    {
        var resultPath = Path.GetFullPath("Evidence/neutral-status-hud.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
        try
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var root = new GameObject("Fighter HUD", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            root.SetActive(false);
            var panel = new GameObject("StatusContainer", typeof(RectTransform), typeof(GridLayoutGroup));
            panel.transform.SetParent(root.transform, false);
            var grid = panel.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(25, 25);
            grid.spacing = Vector2.zero;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 12;
            var hud = root.AddComponent<CoreFighterHud>();
            typeof(CoreFighterHud).GetField("_statusPanel", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(hud, panel);
            var gauge = new GameObject("Power Gauge", typeof(RectTransform), typeof(Slider));
            gauge.transform.SetParent(root.transform, false);
            var gaugeFill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            gaugeFill.transform.SetParent(gauge.transform, false);
            gauge.GetComponent<Slider>().fillRect = gaugeFill.GetComponent<RectTransform>();
            typeof(CoreFighterHud).GetField("powerGaugeSlider", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(hud, gauge.GetComponent<Slider>());
            root.SetActive(true);

            StatusInstance Status(string id, StatusPolarity polarity, StatusColor color) => new StatusInstance {
                InstanceId = id, RecipeId = id, RemainingDuration = 2,
                Recipe = new StatusRecipeDefinition { Id = id, Polarity = polarity, Color = color } };
            var statuses = new List<StatusInstance> {
                Status("normal-debuff", StatusPolarity.Debuff, StatusColor.Normal),
                Status("grey-neutral", StatusPolarity.Neutral, StatusColor.Grey),
                Status("normal-neutral-a", StatusPolarity.Neutral, StatusColor.Normal),
                Status("grey-debuff", StatusPolarity.Debuff, StatusColor.Grey),
                Status("normal-buff", StatusPolarity.Buff, StatusColor.Normal),
                Status("grey-buff", StatusPolarity.Buff, StatusColor.Grey),
                Status("normal-neutral-b", StatusPolarity.Neutral, StatusColor.Normal) };
            hud.SetStatuses(statuses);
            string[] Order() => Enumerable.Range(0, panel.transform.childCount)
                .Select(i => panel.transform.GetChild(i).name).ToArray();
            var expected = new[] { "grey-neutral", "grey-buff", "grey-debuff", "normal-neutral-a",
                "normal-buff", "normal-neutral-b", "normal-debuff" }.Select(id => "Status_" + id + "_0").ToArray();
            Check(Order().SequenceEqual(expected), "HUD must sort Grey before Normal, keep Buff and Neutral together in source order, then show Debuffs.");
            hud.SetStatuses(statuses);
            Check(Order().SequenceEqual(expected), "Refreshing the HUD must preserve sibling order and reuse icons.");

            var fighter = new FighterState { Id = "stance-owner", Health = 100,
                Definition = new CharacterDefinition { BaseStats = new StatBlock { MaxHealth = 100 } } };
            var stance = new StatusRecipeDefinition { Id = "test.stance", Polarity = StatusPolarity.Buff,
                Behavior = StatusBehavior.Stance, StanceChildren = new List<StanceChildDefinition> {
                    new StanceChildDefinition { Id = "test.stance.taunt", Taunt = true },
                    new StanceChildDefinition { Id = "test.stance.immunity", DebuffImmunity = true } } };
            StatusSystem.Apply(fighter, fighter.Id, fighter.Side, stance, "stance", null, 1);
            statuses.AddRange(fighter.Statuses.Instances);
            hud.SetStatuses(statuses);
            var stanceIndex = Array.IndexOf(Order(), "Status_test.stance_0");
            Check(stanceIndex == 6 && Order().Skip(stanceIndex).Take(3).SequenceEqual(new[] { "Status_test.stance_0",
                "Status_test.stance.taunt_0", "Status_test.stance.immunity_0" }) &&
                Order()[9] == "Status_normal-debuff_0",
                "Stance parents and children must share the Buff/Neutral floor before the Debuff floor.");
            fighter.Statuses.RemoveInstance("stance");
            statuses.RemoveAll(s => s.InstanceId == "stance" || s.ParentInstanceId == "stance");
            statuses.RemoveAt(2); // Remove a Neutral icon while its exit animation runs.
            hud.SetStatuses(statuses);
            Check(Order().SequenceEqual(expected.Where(n => n != "Status_normal-neutral-a_0")),
                "Removed parents, children and Neutral icons must immediately vacate grid cells.");
            for (var i = 1; i < panel.transform.childCount; i++)
            {
                var previous = ((RectTransform)panel.transform.GetChild(i - 1)).anchoredPosition;
                var current = ((RectTransform)panel.transform.GetChild(i)).anchoredPosition;
                Check(Mathf.Approximately(current.x - previous.x, 25) && Mathf.Approximately(current.y, previous.y),
                    "Surviving icons must have no empty grid cell.");
            }

            var ordinaryOrder = Order();
            var ordinaryPosition = ((RectTransform)panel.transform).anchoredPosition;
            foreach (var element in new[] { ElementId.Pyro, ElementId.Hydro, ElementId.Electro, ElementId.Cryo, ElementId.Dendro })
                fighter.ElementStatuses.Apply(element, fighter.Id, "aura-" + element);
            Check(CoreFighterHud.DisplayStatuses(fighter).Count == fighter.Statuses.Instances.Count,
                "Ordinary HUD display includes elemental auras.");
            hud.SetElementStatuses(fighter.ElementStatuses.Instances);
            var elementPanel = (GameObject)typeof(CoreFighterHud).GetField("_elementStatusPanel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hud);
            Check(elementPanel != null && elementPanel != panel && elementPanel.transform.parent == panel.transform.parent,
                "Auras must have their own sibling visual container.");
            Check(elementPanel.transform.childCount == 5 && Order().SequenceEqual(ordinaryOrder),
                "Aura icons entered the ordinary status grid or displaced ordinary icons.");
            var elementIds = Enumerable.Range(0, 5).Select(i => elementPanel.transform.GetChild(i).GetInstanceID()).ToArray();
            var ordinaryTop = ((RectTransform)panel.transform).anchoredPosition.y + LayoutUtility.GetPreferredHeight((RectTransform)panel.transform);
            Check(((RectTransform)elementPanel.transform).anchoredPosition.y >= ordinaryTop,
                "The elemental row must sit above the ordinary status grid.");
            fighter.ElementStatuses.AdvanceTargetTurnEnd();
            Check(!fighter.ElementStatuses.Apply(ElementId.Pyro, fighter.Id, "duplicate-pyro"), "Repeated aura refreshed its clock.");
            hud.SetElementStatuses(fighter.ElementStatuses.Instances);
            Check(Enumerable.Range(0, 5).Select(i => elementPanel.transform.GetChild(i).GetInstanceID()).SequenceEqual(elementIds),
                "Aura countdown rebuilt or duplicated the icons.");
            Check(elementPanel.GetComponentsInChildren<Image>().Where(image => image.name == "Cooldown")
                .All(image => Mathf.Approximately(image.fillAmount, 1f / 3f)), "Aura cooldown is not based on three target turns.");
            hud.SetStatuses(new List<StatusInstance>());
            Check(!panel.activeSelf && elementPanel.activeSelf && elementPanel.transform.childCount == 5,
                "Clearing ordinary statuses hid or removed elemental icons.");
            hud.SetStatuses(statuses);
            hud.SetElementStatuses(null);
            Check(!elementPanel.activeSelf && Order().SequenceEqual(ordinaryOrder) &&
                ((RectTransform)panel.transform).anchoredPosition == ordinaryPosition,
                "Clearing elemental icons changed ordinary statuses or retained the elemental row gap.");
            fighter.ElementStatuses.AdvanceTargetTurnEnd();
            hud.SetElementStatuses(fighter.ElementStatuses.Instances);
            Check(elementPanel.transform.childCount == 5 && elementPanel.GetComponentsInChildren<Image>()
                .Where(image => image.name == "Cooldown").All(image => Mathf.Approximately(image.fillAmount, 2f / 3f)),
                "Restoring a saved aura snapshot lost its existing duration.");
            var inspectorIcon = hud.CreateInspectorStatusIcon(fighter.ElementStatuses.Instances[0].ToDisplayStatus(), root.transform);
            Check(Mathf.Approximately(inspectorIcon.Find("Cooldown").GetComponent<Image>().fillAmount, 2f / 3f),
                "Inspector aura icon lost the separate row's duration.");
            fighter.ElementStatuses.AdvanceTargetTurnEnd();
            hud.SetElementStatuses(fighter.ElementStatuses.Instances);
            Check(!elementPanel.activeSelf && Order().SequenceEqual(ordinaryOrder), "Expired auras remain visible or alter ordinary statuses.");
            Check(fighter.ElementStatuses.ApplyReaction(ElementalReactionId.Superconduct, fighter.Id, "reaction-superconduct", 20, 0),
                "Elemental reaction status fixture could not be applied.");
            hud.SetElementStatuses(fighter.ElementStatuses.Instances, fighter.ElementStatuses.ReactionStatuses);
            hud.SetStatuses(statuses);
            Check(elementPanel.activeSelf && elementPanel.transform.childCount == 1 &&
                elementPanel.transform.GetChild(0).name.Contains("Superconduct") && Order().SequenceEqual(ordinaryOrder),
                "Elemental reaction status appeared in the buff/debuff row or was hidden from the elemental row.");
            hud.SetElementStatuses(fighter.ElementStatuses.Instances);
            Check(!elementPanel.activeSelf && Order().SequenceEqual(ordinaryOrder),
                "Clearing elemental conditions changed ordinary statuses.");

            var sanitize = typeof(FightingAllstar.Presentation.Content.CharacterObjectCatalogBuilder).GetMethod("SanitizeRecipe", BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var id in new[] { "status.buff.neutral", "status.debuff.neutral", "status.increase.neutral" })
            {
                var neutral = new StatusRecipeDefinition { Id = id, Polarity = StatusPolarity.Neutral };
                sanitize.Invoke(null, new object[] { neutral });
                Check(neutral.Polarity == StatusPolarity.Neutral, "Catalog loading overwrote explicit Neutral: " + id);
            }
            StatusVisualData.ResetRegistry();
            var defaultDebuff = ScriptableObject.CreateInstance<StatusVisualData>();
            var visual = new SerializedObject(defaultDebuff);
            visual.FindProperty("id").stringValue = "default_debuff";
            visual.FindProperty("icon").objectReferenceValue = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            visual.ApplyModifiedPropertiesWithoutUndo();
            StatusVisualData.Register(defaultDebuff);
            Check(StatusVisualData.GetSprite(null, StatusPolarity.Debuff) != null &&
                StatusVisualData.GetSprite(null, StatusPolarity.Neutral) == null,
                "Neutral fallback must never select the default Debuff icon.");
            CaptureSeparateRows(canvas, root, hud, panel, elementPanel, () => {
                File.WriteAllText(resultPath, "PASS: " + assertions + " Neutral HUD ordering, separate elemental row, countdown, cleanup, stance children and catalog assertions.");
                EditorApplication.Exit(0);
            });
        }
        catch (Exception error)
        {
            File.WriteAllText(resultPath, "FAIL: " + error);
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    private static void CaptureSeparateRows(GameObject canvasRoot, GameObject root, CoreFighterHud hud, GameObject ordinary, GameObject elemental, Action completed)
    {
        // Use production icon rendering and the authored CharacterStatUI row dimensions.
        foreach (Transform child in root.transform)
            if (child.gameObject != ordinary && child.gameObject != elemental) child.gameObject.SetActive(false);
        var camera = new GameObject("HUD capture camera", typeof(Camera)).GetComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.035f, .045f, .07f);
        camera.orthographic = true;
        camera.orthographicSize = 270;
        var target = new RenderTexture(960, 540, 24);
        camera.targetTexture = target;
        var canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 5;
        var rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = rootRect.anchorMax = Vector2.one * .5f;
        rootRect.pivot = Vector2.zero;
        rootRect.anchoredPosition = new Vector2(-300, -45);
        rootRect.sizeDelta = new Vector2(193.18f, 54.16f);
        rootRect.localScale = Vector3.one * 2;
        foreach (var panel in new[] { ordinary, elemental })
        {
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(193.18f, 26);
            rect.anchoredPosition = Vector2.zero;
            var grid = panel.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(25, 25);
            grid.spacing = new Vector2(1.3f, 3.84f);
            grid.childAlignment = TextAnchor.LowerLeft;
            grid.startCorner = GridLayoutGroup.Corner.LowerLeft;
            grid.constraintCount = panel == ordinary ? 7 : 5;
            grid.padding = new RectOffset(5, 5, 0, 1);
        }
        hud.SetStatuses(new List<StatusInstance> {
            new StatusInstance { InstanceId = "capture-atk", RecipeId = "capture.atk", RemainingDuration = 2, Recipe = new StatusRecipeDefinition() },
            new StatusInstance { InstanceId = "capture-shield", RecipeId = "capture.shield", RemainingDuration = 2, Recipe = new StatusRecipeDefinition() },
            new StatusInstance { InstanceId = "capture-ignite", RecipeId = "capture.ignite", RemainingDuration = 2, Recipe = new StatusRecipeDefinition { Polarity = StatusPolarity.Debuff } }
        });
        var auras = new ElementStatusContainer();
        foreach (var element in new[] { ElementId.Pyro, ElementId.Hydro, ElementId.Electro, ElementId.Cryo, ElementId.Dendro })
            auras.Apply(element, "capture", "capture-" + element);
        hud.SetElementStatuses(auras.Instances);
        hud.enabled = false; // Settle the entrance animation for the still capture.
        Sprite Icon(string file)
        {
            var texture = new Texture2D(2, 2);
            if (!texture.LoadImage(File.ReadAllBytes(Path.Combine("FixtureIcons", file + ".png"))))
                throw new Exception("Could not load icon: " + file);
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f);
        }
        var ordinaryIcons = new[] { "icon_buff_atk_add", "icon_buff_heal_barrier", "icon_buff_cc_atk_fire" };
        for (var i = 0; i < ordinary.transform.childCount; i++)
            ordinary.transform.GetChild(i).Find("Icon").GetComponent<Image>().sprite = Icon(ordinaryIcons[i]);
        foreach (Transform child in elemental.transform)
            child.Find("Icon").GetComponent<Image>().sprite = Icon(child.name.Replace("ElementStatus_", "Element_"));
        // The generated fixture icon needs the sprite used by the production prefab's radial overlay.
        var overlay = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), Vector2.one * .5f);
        foreach (var panel in new[] { ordinary, elemental })
            foreach (var overlayImage in panel.GetComponentsInChildren<Image>())
                if (overlayImage.name == "Cooldown") overlayImage.sprite = overlay;
        void Caption(string value, Vector2 position, int size)
        {
            var label = new GameObject("Caption", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)label.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(850, 50);
            var text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = Color.white;
            text.text = value;
        }
        Caption("CHARACTER HUD / SEPARATE STATUS ROWS", new Vector2(60, -55), 27);
        Caption("Elemental auras", new Vector2(590, -205), 23);
        Caption("Buffs & debuffs", new Vector2(590, -265), 23);
        Caption("Each row updates and clears independently.", new Vector2(60, -440), 21);
        var captureFrame = Time.frameCount + 2;
        void CaptureOnFrame()
        {
            if (Time.frameCount < captureFrame) return;
            EditorApplication.update -= CaptureOnFrame;
            try
            {
                Canvas.ForceUpdateCanvases();
                camera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes("Evidence/element-status-row.png", image.EncodeToPNG());
                Check(image.GetPixels32().Count(pixel => Math.Max(pixel.r, Math.Max(pixel.g, pixel.b)) -
                    Math.Min(pixel.r, Math.Min(pixel.g, pixel.b)) > 60) > 500, "HUD layout capture did not render the colored status icons.");
                UnityEngine.Object.Destroy(image);
                camera.targetTexture = null;
                target.Release();
                completed();
            }
            catch (Exception error)
            {
                File.WriteAllText("Evidence/neutral-status-hud.txt", "FAIL: " + error);
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }
        EditorApplication.update += CaptureOnFrame;
    }
}
#endif
