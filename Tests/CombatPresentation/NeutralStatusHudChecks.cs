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
            File.WriteAllText(resultPath, "PASS: " + assertions + " Neutral HUD ordering, stance children, gap closure and catalog assertions.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText(resultPath, "FAIL: " + error);
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}
#endif
