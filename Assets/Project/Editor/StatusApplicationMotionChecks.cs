using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FightingAllstar.EditorTools
{
    /// <summary>Samples the production status feedback without combat state, coroutines, or a wallet.</summary>
    public static class StatusApplicationMotionChecks
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.NonPublic;
        private static int _checks;

        [MenuItem("Fighting Allstar/Validate Status Application Motion")]
        public static void Run()
        {
            _checks = 0;
            var results = new List<string>();
            var directory = Path.GetFullPath("Artifacts/SummonSequence/StatusMotion");
            Directory.CreateDirectory(directory);
            var root = new GameObject("Status Motion Fixture", typeof(RectTransform), typeof(Canvas))
            { hideFlags = HideFlags.HideAndDontSave };
            root.SetActive(false);
            try
            {
                var hud = root.AddComponent<CoreFighterHud>();
                var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefabs/StatusIcon.prefab");
                Check(template != null, "Authored status icon template must exist.");
                var panel = new GameObject("Status Layout", typeof(RectTransform), typeof(GridLayoutGroup));
                panel.transform.SetParent(root.transform, false);
                var grid = panel.GetComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(25f, 25f);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 4;
                hud.enabled = false;
                root.SetActive(true);
                foreach (var kind in new[] { "Buff", "Debuff", "Neutral", "Element", "Fallback" })
                {
                    typeof(CoreFighterHud).GetField("_statusTemplate", Members).SetValue(hud, kind == "Fallback" ? null : template);
                    var status = new StatusInstance
                    {
                        InstanceId = "status-motion-" + kind, RecipeId = "status.motion." + kind,
                        RemainingDuration = 2, StackCount = 1,
                        Recipe = new StatusRecipeDefinition
                        {
                            Id = "status.motion." + kind, DefaultDuration = 3,
                            Polarity = kind == "Debuff" ? StatusPolarity.Debuff :
                                kind == "Buff" ? StatusPolarity.Buff : StatusPolarity.Neutral
                        }
                    };
                    if (kind == "Element")
                    {
                        var elements = new ElementStatusContainer();
                        elements.Apply(ElementId.Pyro, "fixture", "status-motion-element");
                        status = elements.Instances[0].ToDisplayStatus();
                    }
                    var entry = typeof(CoreFighterHud).GetMethod("CreateStatusView", Members)
                        .Invoke(hud, new object[] { status, 0, panel.transform });
                    var rect = Field<RectTransform>(entry, "Rect");
                    var opacity = Field<CanvasGroup>(entry, "Opacity");
                    var flash = Field<Image>(entry, "ApplicationFlash");
                    var parts = Field<RectTransform[]>(entry, "FeedbackParts");
                    var positions = Field<Vector2[]>(entry, "FeedbackPartPositions");
                    var cooldown = Field<Image>(entry, "CooldownOverlay");
                    var durationFill = cooldown != null ? cooldown.fillAmount : -1f;
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)panel.transform);
                    var cellPosition = rect.anchoredPosition;
                    Check(rect.Find("Icon") != null && rect.Find("Cooldown") != null,
                        kind + ": authored direct-child icon/cooldown structure is preserved.");
                    foreach (var entering in new[] { true, false })
                    {
                        Reset(entry);
                        var duration = entering ? .26f : .22f;
                        var rise = entering ? 5f : 2f;
                        var previousY = float.NegativeInfinity;
                        var previousAlpha = -1f;
                        for (var i = 0; i <= 26; i++)
                        {
                            var age = duration * i / 26f;
                            Sample(entry, age, entering);
                            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)panel.transform);
                            Check(rect.localScale == Vector3.one && rect.localRotation == Quaternion.identity,
                                kind + ": status motion must not scale or rotate at sample " + i);
                            Check(rect.anchoredPosition == cellPosition, kind + ": slide must not move its grid cell.");
                            var offset = parts[0].anchoredPosition.y - positions[0].y;
                            Check(offset >= previousY - .0001f && offset >= -rise - .0001f && offset <= .0001f,
                                kind + ": rise must approach the resting position without bounce or overshoot.");
                            Check(opacity.alpha >= previousAlpha - .0001f && opacity.alpha <= 1f,
                                kind + ": entry opacity must rise monotonically.");
                            previousY = offset;
                            previousAlpha = opacity.alpha;
                            if (i == 0)
                                Check(Mathf.Approximately(opacity.alpha, entering ? 0f : .65f) &&
                                    flash.enabled && flash.sprite == null && flash.color.a > .6f,
                                    kind + ": entry starts with the brief white flash before its rise.");
                        }
                        Sample(entry, duration - .045f, entering);
                        Check(flash.enabled && flash.sprite != null && flash.color.a > .5f &&
                            Mathf.Approximately(parts[0].anchoredPosition.y, positions[0].y),
                            kind + ": settling light starts only after the rise has reached its position.");
                        Sample(entry, .03f, entering);
                        Sample(entry, .03f, entering);
                        Check(Mathf.Approximately(parts[0].anchoredPosition.y - positions[0].y,
                            -rise * Mathf.Pow(1f - .03f / (duration - .09f), 3f)),
                            kind + ": repeated samples and interrupted refreshes cannot accumulate offsets.");
                        Reset(entry);
                        Check(opacity.alpha == 1f && !flash.enabled && flash.color.a == 0f &&
                            parts[0].anchoredPosition == positions[0] && rect.localScale == Vector3.one,
                            kind + ": interruption/disable cleanup returns the icon to its resting state.");
                    }
                    for (var i = 0; i <= 10; i++)
                    {
                        typeof(CoreFighterHud).GetMethod("ApplyStatusRejectedFrame", StaticMembers)
                            .Invoke(null, new object[] { entry, .02f * i });
                        Check(rect.localScale == Vector3.one && rect.localRotation == Quaternion.identity &&
                            !flash.enabled && opacity.alpha >= .87f && parts[0].anchoredPosition == positions[0],
                            kind + ": rejected refresh is restrained tint/fade without bounce or success glint.");
                    }
                    Reset(entry);
                    Check(cooldown == null || cooldown.fillAmount == durationFill,
                        kind + ": animation must preserve the authored duration fill.");
                    var overlayCount = 0;
                    foreach (var image in rect.GetComponentsInChildren<Image>(true))
                        if (image.name == "ApplicationFlash") overlayCount++;
                    Check(overlayCount == 1 && rect.Find("ApplicationFlash").GetComponent<Image>() == flash && !flash.raycastTarget,
                        kind + ": each icon reuses one non-interactive bounded light overlay.");
                    results.Add("PASS " + kind + ": entrance, refresh, rejection, layout stability and reset.");
                    UnityEngine.Object.DestroyImmediate(rect.gameObject);
                }
                results.Insert(0, "PASS: " + _checks + " production status application motion assertions.");
                File.WriteAllLines(Path.Combine(directory, "Verification.txt"), results);
                Debug.Log(results[0]);
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(directory, "Verification.txt"), "FAIL: " + error);
                Debug.LogException(error);
                throw;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, Members).GetValue(target);
        private static void Sample(object entry, float age, bool entering) =>
            typeof(CoreFighterHud).GetMethod("ApplyStatusApplicationFrame", StaticMembers)
                .Invoke(null, new object[] { entry, age, entering });
        private static void Reset(object entry) =>
            typeof(CoreFighterHud).GetMethod("ResetStatusFeedbackVisual", StaticMembers)
                .Invoke(null, new[] { entry });
        private static void Check(bool condition, string message)
        {
            _checks++;
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
