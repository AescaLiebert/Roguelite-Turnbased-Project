#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class CinematicHudChecks
{
    private static double deadline;
    static CinematicHudChecks() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool("CinematicHudChecks", true); deadline = EditorApplication.timeSinceStartup + 180; EditorApplication.isPlaying = true; }
    static void Tick()
    {
        if (!SessionState.GetBool("CinematicHudChecks", false)) return;
        if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 180;
        if (EditorApplication.timeSinceStartup > deadline) { Debug.LogError("CINEMATIC HUD FAIL timeout"); EditorApplication.Exit(1); return; }
        if (!EditorApplication.isPlaying || UnityEngine.Object.FindFirstObjectByType<CinematicHudRunner>() != null) return;
        new GameObject("CinematicHudChecks").AddComponent<CinematicHudRunner>();
    }
}
public sealed class CinematicHudRunner : MonoBehaviour
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    CoreBattleSceneController controller;
    BattleStagePresenter stage;
    UIDocument document;
    Camera viewCamera;
    string output;
    int checks;
    int width, height;
    object Get(string name) => typeof(CoreBattleSceneController).GetField(name, Private).GetValue(controller);
    void Set(string name, object value) => typeof(CoreBattleSceneController).GetField(name, Private).SetValue(controller, value);
    object Call(string name, params object[] values) => typeof(CoreBattleSceneController).GetMethod(name, Private).Invoke(controller, values);
    void Require(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    void Capture(string name) => FightingAllstar.EditorTools.BattlePreviewCapture.Save(Path.Combine(output, name + ".png"), document, width, height);
    IEnumerator Start() { yield return Guard(Check()); }
    IEnumerator Guard(IEnumerator routine)
    {
        while (true)
        {
            object next;
            try { if (!routine.MoveNext()) break; next = routine.Current; }
            catch (Exception e) { Debug.LogError("CINEMATIC HUD FAIL " + e); EditorApplication.Exit(1); yield break; }
            if (next is IEnumerator nested) yield return Guard(nested); else yield return next;
        }
    }
    IEnumerator Check()
    {
        var reactionSnapshot = typeof(CoreBattleSceneController).GetMethod("SnapshotActionReaction", BindingFlags.Static | BindingFlags.NonPublic);
        foreach (var begin in new[] { BattleEventKind.CardPlayed, BattleEventKind.CounterStarted })
        {
            var facts = new[] { new BattleEvent { Kind = begin, SourceId = "a" },
                new BattleEvent { Kind = BattleEventKind.DamageApplied, SourceId = "a", HasHitReaction = true, Reaction = HitReaction.Hit },
                new BattleEvent { Kind = BattleEventKind.DamageApplied, SourceId = "b", HasHitReaction = true, Reaction = HitReaction.KnockUp },
                new BattleEvent { Kind = BattleEventKind.DamageApplied, SourceId = "a", HasHitReaction = true, Reaction = HitReaction.KnockBack },
                new BattleEvent { Kind = BattleEventKind.ActionCompleted },
                new BattleEvent { Kind = BattleEventKind.DamageApplied, SourceId = "a", HasHitReaction = true, Reaction = HitReaction.KnockUp } };
            Require((HitReaction)reactionSnapshot.Invoke(null, new object[] { facts, 0 }) == HitReaction.KnockBack,
                "Framing snapshot included another source or next action.");
            facts[3].Reaction = HitReaction.KnockUp;
            Require((HitReaction)reactionSnapshot.Invoke(null, new object[] { facts, 0 }) == HitReaction.KnockUp,
                "Framing snapshot missed recorded card/counter knockup.");
        }
        var queued = typeof(CoreBattleSceneController).GetMethod("HasQueuedExecution", BindingFlags.Static | BindingFlags.NonPublic);
        foreach (var boundary in new[] { BattleEventKind.TurnEnded, BattleEventKind.TurnStarted,
            BattleEventKind.StatusResolutionStarted, BattleEventKind.BattleCompleted })
            Require(!(bool)queued.Invoke(null, new object[] { new[] { new BattleEvent { Kind = BattleEventKind.ActionCompleted },
                new BattleEvent { Kind = boundary }, new BattleEvent { Kind = BattleEventKind.CardPlayed } }, 0 }),
                "Next turn kept the previous execution camera active: " + boundary);
        foreach (var next in new[] { BattleEventKind.CardPlayed, BattleEventKind.CounterStarted })
            Require((bool)queued.Invoke(null, new object[] { new[] { new BattleEvent { Kind = BattleEventKind.ActionCompleted },
                new BattleEvent { Kind = next }, new BattleEvent { Kind = BattleEventKind.TurnEnded } }, 0 }),
                "Same-turn queue lost its rear camera: " + next);
        output = Path.GetFullPath("Evidence"); Directory.CreateDirectory(output);
        viewCamera = new GameObject("Main Camera").AddComponent<Camera>(); viewCamera.tag = "MainCamera";
        viewCamera.clearFlags = CameraClearFlags.SolidColor; viewCamera.backgroundColor = new Color(.1f, .15f, .22f);
        viewCamera.transform.SetPositionAndRotation(new Vector3(0, 13.5f, -17), Quaternion.Euler(35,0,0));
        viewCamera.fieldOfView = 55;
        var light = new GameObject("Light").AddComponent<Light>(); light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50, 25, 0); RenderSettings.ambientLight = Color.gray;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.transform.localScale = Vector3.one * 6;
        floor.GetComponent<Renderer>().material.color = new Color(.24f, .27f, .31f);
        new GameObject("CharHeroPosition2").transform.position = new Vector3(0, 0, -4);
        new GameObject("EnemyHeroPosition2").transform.position = new Vector3(0, 0, 4);
        var state = new BattleState { Phase = BattlePhase.Resolving, ActingSide = TeamSide.Player };
        var actors = new Dictionary<string, GameObject>();
        foreach (var side in new[] { TeamSide.Player, TeamSide.Opponent })
            for (var i = 0; i < 3; i++)
            {
                var id = (side == TeamSide.Player ? "p" : "e") + i;
                var definition = new CharacterDefinition { Id = "fixture", DisplayName = "Mai Shiranui" };
                (side == TeamSide.Player ? state.Player : state.Opponent).Fighters.Add(new FighterState { Id = id, Definition = definition, Side = side, FormationSlot = i, Health = 100 });
                var actor = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                actor.name = id; actor.transform.position = new Vector3((i - 1) * 3.2f, 1, side == TeamSide.Player ? -4 : 4);
                actor.transform.rotation = BattleFighterView.FormationRotation(side);
                actor.GetComponent<Renderer>().material.color = side == TeamSide.Player ? new Color(.12f,.5f,.9f) : new Color(.9f,.25f,.12f);
                actors.Add(id, actor);
            }
        stage = new GameObject("Stage").AddComponent<BattleStagePresenter>(); stage.Initialize(actors, state);
        var docObject = new GameObject("Document"); document = docObject.AddComponent<UIDocument>();
        document.panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        document.panelSettings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UnityDefaultRuntimeTheme.tss");
        document.panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
        document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Project/UI/BattleCardHud.uxml");
        controller = new GameObject("Disabled authority controller").AddComponent<CoreBattleSceneController>();
        controller.enabled = false; // Fixture exercises presentation only; never opens a session.
        Set("battleDocument", document); Set("_stage", stage); Set("_displayState", state);
        Call("BindPresentationHud", document.rootVisualElement);
        document.rootVisualElement.Q<VisualElement>("card-tooltip-container").style.display = DisplayStyle.None;
        var character = ScriptableObject.CreateInstance<CharacterObject>();
        typeof(CharacterObject).GetField("fighterFull", Private).SetValue(character, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Cut_17_Mai94.png"));
        typeof(CharacterObject).GetField("fighterIcon", Private).SetValue(character, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Cut_17_Mai94.png"));
        typeof(CharacterObject).GetField("fighterName", Private).SetValue(character, "Mai Shiranui");
        typeof(CoreBattleSceneController).GetField("_characterObjectByDefinitionId", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, new Dictionary<string, CharacterObject> { { "fixture", character } });
        var root = document.rootVisualElement;
        root.style.position = Position.Absolute;
        root.style.left = root.style.right = root.style.top = root.style.bottom = 0;
        root.style.unityFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/LiberationSans.ttf");
        foreach (var portrait in new[] { false, true })
        {
            width = portrait ? 720 : 1280; height = portrait ? 1280 : 720;
            viewCamera.aspect = width / (float)height;
            // Persistent offscreen target sets the real layout size before coroutine captures.
            var layoutTarget = new RenderTexture(width, height, 24); layoutTarget.Create();
            document.panelSettings.targetTexture = layoutTarget;
            yield return null; yield return null;
            Require(root.worldBound.height > height * .9f, "HUD root did not fill its render target");
            var prefix = portrait ? "portrait" : "landscape";
            Call("SetUltimateHud", true);
            // Sample the actual production timeline at fixed times, independent of batch frame rate.
            var cutView = new UltimateCutInView(character.FighterFull, "Butterfly Flurry", "Mai Shiranui", new Color(.65f, .35f, 1f));
            root.Q<VisualElement>("battle-effects").Add(cutView);
            cutView.Apply(0, width, height); yield return null;
            Require(cutView.Q<Image>("ultimate-art").resolvedStyle.opacity < .01f &&
                cutView.Q<Label>("ultimate-title").resolvedStyle.opacity < .01f, "Cut-in flashed content before entrance");
            cutView.Apply(.04f, width, height); yield return null;
            var arrivingArt = cutView.Q<Image>("ultimate-art");
            Require(arrivingArt.resolvedStyle.opacity > .01f && arrivingArt.resolvedStyle.opacity < .99f, "Art did not ease into the opening");
            Require(cutView.Q<Label>("ultimate-title").resolvedStyle.opacity < .01f, "Title ignored entrance stagger");
            Capture(prefix + "-ultimate-opening");
            cutView.Apply(.8f, width, height); yield return null;
            Require(arrivingArt.parent == cutView && arrivingArt.scaleMode == ScaleMode.ScaleToFit, "Art was cropped inside a strip");
            Require(arrivingArt.worldBound.yMin >= 0 && arrivingArt.worldBound.yMax <= height, "Full-art frame clipped vertically");
            var titleBounds = cutView.Q<Label>("ultimate-title").worldBound;
            Require(titleBounds.xMin >= 0 && titleBounds.xMax <= width && titleBounds.yMax <= height, "Ultimate title left the viewport");
            Require(cutView.resolvedStyle.backgroundColor.a < .01f, "Opaque black backdrop returned");
            Capture(prefix + "-ultimate-full-art");
            cutView.Apply(UltimateCutInView.OpeningDuration + UltimateCutInView.HoldDuration + .21f, width, height);
            yield return null;
            Require(arrivingArt.resolvedStyle.opacity > .01f && arrivingArt.resolvedStyle.opacity < .99f, "Exit abruptly hid art");
            Require(cutView.Q<Label>("ultimate-title").resolvedStyle.opacity < arrivingArt.resolvedStyle.opacity, "Text did not lead the exit");
            Capture(prefix + "-ultimate-exit");
            cutView.Apply(UltimateCutInView.Duration, width, height); yield return null;
            Require(arrivingArt.resolvedStyle.opacity < .01f && cutView.Q("ultimate-wash").resolvedStyle.opacity < .01f, "Cut-in layers survived the exit");
            cutView.RemoveFromHierarchy();
            Call("SetUltimateHud", false);
            foreach (var side in new[] { TeamSide.Player, TeamSide.Opponent })
            {
                var transition = controller.StartCoroutine(Guard((IEnumerator)Call("PresentTurnChange", side)));
                yield return new WaitForSecondsRealtime(.3f);
                var banner = root.Q<VisualElement>("turn-banner");
                Require(banner.resolvedStyle.display == DisplayStyle.Flex, "Turn announcement missing during orbit");
                Require(root.Q<Label>("turn-banner-title").text == (side == TeamSide.Player ? "YOUR TURN" : "ENEMY TURN"), "Wrong turn text");
                Require(banner.worldBound.yMin >= 0 && banner.worldBound.yMax < height, "Turn announcement outside screen");
                Capture(prefix + "-turn-" + side);
                yield return transition;
                Require(banner.resolvedStyle.display == DisplayStyle.None, "Turn announcement did not exit");
            }
            Call("ResetDamageTotal", true);
            Call("UpdateDamageTotal", "p0", 1329L); yield return new WaitForSecondsRealtime(.25f);
            Call("UpdateDamageTotal", "p0", 1900L); yield return new WaitForSecondsRealtime(.25f);
            Require((long)Get("_damageTotalAmount") == 3229L, "Multi-hit total inaccurate");
            Require(root.Q<Label>("damage-total-value").text == "3,229", "Total formatting inaccurate");
            Capture(prefix + "-total-damage");
            Require(root.Q<Label>("damage-total-value").worldBound.yMin > height * .05f, "Total damage clipped at screen edge");
            yield return new WaitForSecondsRealtime(1.5f);
            Require(root.Q<VisualElement>("damage-total").resolvedStyle.display == DisplayStyle.Flex, "Long action hid total before completion");
            Call("ResetDamageTotal", true); Call("UpdateDamageTotal", "p0", 250L);
            Require((long)Get("_damageTotalAmount") == 250L, "Consecutive cards from the same owner carried damage");
            Set("_damageTotalActionOpen", false); yield return new WaitForSecondsRealtime(1.7f);
            Require(root.Q<VisualElement>("damage-total").resolvedStyle.display == DisplayStyle.None, "Total did not expire");
            Call("ResetDamageTotal", true); Call("UpdateDamageTotal", "p0", long.MaxValue - 2); Call("UpdateDamageTotal", "p0", 200L);
            Require((long)Get("_damageTotalAmount") == long.MaxValue, "Total overflowed");
            Call("ClearCinematicHud");
            var card = new BattleEvent { SourceId = "p0", Card = new CardState { Kind = CardKind.Ultimate } };
            Coroutine cut;
            foreach (var side in new[] { TeamSide.Player, TeamSide.Opponent })
            {
                yield return stage.ReturnToPlanning(side, .01f);
                var rest = new Pose(viewCamera.transform.position, viewCamera.transform.rotation);
                var restFov = viewCamera.fieldOfView;
                var source = side == TeamSide.Player ? "p0" : "e0";
                // Queue the ultimate after a self buff's rear view rather than from rest.
                yield return stage.BeginExecution(source, source, 1, false, CardCategory.Buff, new[] { source }, false, true);
                yield return stage.SupportAction(source, new[] { source }, CardCategory.Buff);
                yield return stage.RecoverAttacker();
                Require(Vector3.Distance(viewCamera.transform.position, rest.position) > .1f, "Ultimate fixture did not start from prior card");
                var previousShot = viewCamera.transform.position;
                card.SourceId = source;
                var travelStarted = Time.realtimeSinceStartup;
                cut = controller.StartCoroutine(Guard((IEnumerator)Call("UltimateCutIn", card)));
                var startDeadline = Time.realtimeSinceStartup + .5f;
                while (!stage.CameraTransitionActive && Time.realtimeSinceStartup < startDeadline) yield return null;
                Require(stage.CameraTransitionActive, "Ultimate return snapped instead of starting a camera move");
                Require(stage.UltimateLeadInActive && stage.ExecutionPhase == ActorCardPhase.Rest,
                    "Ultimate did not enter overhead lead-in before the cut-in");
                var intermediate = false;
                var slowFrame = false;
                while (stage.CameraTransitionActive)
                {
                    Require(root.Q("ultimate-cut-in") == null, "Ultimate portrait started before Rest camera arrival");
                    intermediate |= Vector3.Distance(viewCamera.transform.position, previousShot) > .01f &&
                        Vector3.Distance(viewCamera.transform.position, rest.position) > .01f;
                    slowFrame |= Time.unscaledDeltaTime >= .3f;
                    yield return null;
                }
                Require(intermediate || slowFrame, "Ultimate return had no intermediate camera poses");
                Require(Time.realtimeSinceStartup - travelStarted >= .18f, "Ultimate camera return snapped");
                Require(Vector3.Distance(viewCamera.transform.position, rest.position) < .001f &&
                    Quaternion.Angle(viewCamera.transform.rotation, rest.rotation) < .01f && Mathf.Abs(viewCamera.fieldOfView - restFov) < .001f,
                    "Ultimate did not arrive at the acting side's drafting camera");
                // The cut-in now starts on arrival, without the previous 350 ms idle pause.
                var arrivalDeadline = Time.realtimeSinceStartup + .1f;
                while (root.Q("ultimate-cut-in") == null && Time.realtimeSinceStartup < arrivalDeadline) yield return null;
                Require(root.Q("ultimate-cut-in") != null, "Ultimate added a pause after camera arrival");
                Require(root.Q<VisualElement>("battle-card-tray").resolvedStyle.opacity < .01f, "Ultimate lead-in did not hide hand");
                Capture(prefix + "-ultimate-lead-in-" + side);
                yield return new WaitForSecondsRealtime(.55f);
                Require(!stage.UltimateLeadInActive && root.Q("ultimate-cut-in") != null, "Ultimate did not progress to portrait");
                Require(root.Q("ultimate-cut-in").worldBound.height > height * .9f, "Ultimate did not fill screen");
                Capture(prefix + "-ultimate-portrait-" + side); yield return cut;
                Require(root.Q("ultimate-cut-in") == null, "Ultimate portrait leaked");
                Require(Vector3.Distance(viewCamera.transform.position, rest.position) < .001f, "Cut-in lost the resting camera");
            }
            card.SourceId = "p0";
            var reveal = stage.StartCoroutine(stage.BeginExecution("p0", "e1", 3, true));
            while (stage.ExecutionPhase != ActorCardPhase.WindUp) yield return null;
            yield return stage.WaitForCameraTransition();
            var windUp = new Pose(viewCamera.transform.position, viewCamera.transform.rotation);
            var windUpFov = viewCamera.fieldOfView;
            // The ranked face cut is settled before the world energy reveal.
            var revealDeadline = Time.realtimeSinceStartup + 1.85f;
            while (Time.realtimeSinceStartup < revealDeadline)
            {
                Require(Vector3.Distance(viewCamera.transform.position, windUp.position) < .001f &&
                    Quaternion.Angle(viewCamera.transform.rotation, windUp.rotation) < .01f &&
                    Mathf.Abs(viewCamera.fieldOfView - windUpFov) < .001f,
                    "Ultimate added a camera state or tracked its actor during Wind Up.");
                yield return null;
            }
            Capture(prefix + "-ultimate-windup");
            Capture(prefix + "-ultimate-energy");
            yield return reveal;
            Require(!stage.UltimateRevealActive, "Ultimate energy leaked");
            yield return stage.BeginDamageAttack("p0", new[] { "e1" }, 1, AttackRange.Long, false);
            Capture(prefix + "-ultimate-action"); yield return stage.CompleteExecution(false, TeamSide.Player);
            Call("SetUltimateHud", false); yield return null;
            Require(root.Q<VisualElement>("battle-card-tray").resolvedStyle.opacity > .99f, "Ultimate did not restore hand");
            // Interrupt the reveal and cut-in at their active peaks, as Skip does.
            cut = controller.StartCoroutine(Guard((IEnumerator)Call("UltimateCutIn", card)));
            yield return new WaitForSecondsRealtime(.1f); controller.StopCoroutine(cut); Call("ClearCinematicHud"); stage.SnapToPlanning();
            Require(!stage.UltimateLeadInActive && stage.ExecutionPhase == ActorCardPhase.Rest, "Interrupted overhead anticipation leaked");
            Require(root.Q<VisualElement>("battle-card-tray").resolvedStyle.opacity > .99f, "Interrupted anticipation hid hand");
            cut = controller.StartCoroutine(Guard((IEnumerator)Call("UltimateCutIn", card)));
            var portraitDeadline = Time.realtimeSinceStartup + 2f;
            while (root.Q("ultimate-cut-in") == null && Time.realtimeSinceStartup < portraitDeadline) yield return null;
            Require(root.Q("ultimate-cut-in") != null, "Portrait interruption never reached the overlay");
            yield return new WaitForSecondsRealtime(.3f); controller.StopCoroutine(cut); Call("ClearCinematicHud"); stage.SnapToPlanning();
            Require(root.Q("ultimate-cut-in") == null, "Interrupted portrait leaked");
            Require(root.Q("card-execution-overlay").resolvedStyle.visibility == Visibility.Visible, "Interrupted portrait left execution panel hidden");
            reveal = stage.StartCoroutine(stage.BeginExecution("p0", "e1", 3, true));
            yield return new WaitForSecondsRealtime(1.4f); stage.SnapToPlanning();
            Require(!stage.UltimateRevealActive && stage.ExecutionPhase == ActorCardPhase.Rest, "Interrupted world reveal leaked");
            document.panelSettings.targetTexture = null; layoutTarget.Release(); Destroy(layoutTarget);
        }
        File.WriteAllText(Path.Combine(output, "result.txt"), "PASS " + checks + " cinematic HUD assertions");
        Debug.Log("CINEMATIC HUD PASS " + checks); EditorApplication.Exit(0);
    }
}
#endif
