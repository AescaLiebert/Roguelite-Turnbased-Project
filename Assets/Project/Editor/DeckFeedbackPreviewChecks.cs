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

namespace FightingAllstar.EditorTools
{
    /// <summary>Disposable live HUD fixture. Uses a separate local session and never writes a run save.</summary>
    [InitializeOnLoad]
    public static class DeckFeedbackPreviewChecks
    {
        private const string Running = "DeckFeedbackChecks.Running";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int _step;
        private static double _at;
        private static double _deadline;
        private static Button _held;
        private static Vector2 _destination;
        private static int _pointer;
        private static float _initialSpacing;
        private static int _drawSamples;
        private static bool _bothRushed;
        private static bool _mergeFlashed;
        private static string _impactSamples;
        private static BattleState _fixture;
        private static BattleEvent _draw;
        private static List<BattleEvent> _enemyEvents;
        private static bool _background;
        private static bool _backgroundCaptured;
        private static int _assertions;

        static DeckFeedbackPreviewChecks() { EditorApplication.update += Tick; }

        [MenuItem("Fighting Allstar/Validation/Deck Feedback")]
        public static void Run()
        {
            SessionState.SetBool(Running, true);
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            _step = 0; _assertions = 0; _backgroundCaptured = false; _deadline = EditorApplication.timeSinceStartup + 90;
        }

        private static object Get(CoreBattleSceneController c, string name) => c.GetType().GetField(name, Private).GetValue(c);
        private static void Set(CoreBattleSceneController c, string name, object value) => c.GetType().GetField(name, Private).SetValue(c, value);
        private static object Call(CoreBattleSceneController c, string name, params object[] args) => c.GetType().GetMethod(name, Private).Invoke(c, args);
        private static void Check(bool passed, string message) { _assertions++; if (!passed) throw new InvalidOperationException(message); }
        private static void Next(int step, double delay = .15) { _step = step; _at = EditorApplication.timeSinceStartup + delay; }
        private static CardState Card(string id, FighterState owner, int slot) => new CardState { Id = id,
            OwnerFighterId = owner.Id, SkillId = owner.Definition.Skills[slot - 1].Id, Rank = 1, Kind = CardKind.Skill,
            Category = CardCategory.Attack, EffectCategory = CardCategory.Attack };

        private static void Capture(string name)
        {
            var folder = Path.GetFullPath("Logs/DeckFeedbackEvidence");
            Directory.CreateDirectory(folder);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
        }

        private static void Drag(VisualElement hand)
        {
            _held = hand.Q<VisualElement>("Card fixture-a").Q<Button>("card-button");
            var start = _held.worldBound.center;
            _destination = hand.Q<VisualElement>("Card fixture-b").Q<Button>("card-button").worldBound.center;
            using (var down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, mousePosition = start, button = 0 }))
            { _pointer = down.pointerId; down.target = _held; _held.SendEvent(down); }
            using (var move = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseDrag, mousePosition = _destination, button = 0 }))
            { move.target = _held; _held.SendEvent(move); }
        }

        private static IEnumerator ObserveMerge(CoreBattleSceneController c)
        {
            _bothRushed = _mergeFlashed = false;
            _impactSamples = "";
            var views = (Dictionary<string, VisualElement>)Get(c, "_handCardViews");
            var root = ((UIDocument)Get(c, "battleDocument")).rootVisualElement;
            while ((bool)Get(c, "_isAnimatingCards"))
            {
                if (views.TryGetValue("fixture-a", out var a) && views.TryGetValue("fixture-b", out var b) &&
                    a.ClassListContains("merge-colliding") && b.ClassListContains("merge-colliding"))
                {
                    var ax = a.resolvedStyle.translate.x;
                    var bx = b.resolvedStyle.translate.x;
                    if (Mathf.Abs(ax) > 2 && Mathf.Abs(bx) > 2 && Mathf.Sign(ax) != Mathf.Sign(bx)) _bothRushed = true;
                }
                var impact = root.Q("merge-impact");
                if (impact != null) _impactSamples += impact.resolvedStyle.backgroundColor + "/" + impact.resolvedStyle.opacity + "; ";
                if (!_mergeFlashed && impact != null && impact.resolvedStyle.opacity > .5f &&
                    impact.resolvedStyle.backgroundColor.r > .99f && impact.resolvedStyle.backgroundColor.g > .99f &&
                    impact.resolvedStyle.backgroundColor.b > .99f)
                {
                    _mergeFlashed = true;
                    Capture("02-merge-white-impact");
                }
                yield return null;
            }
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Running, false)) return;
            if (!EditorApplication.isPlaying)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode) BattlePresentationPreview.PlayerFirst();
                return;
            }
            var c = UnityEngine.Object.FindFirstObjectByType<CoreBattleSceneController>();
            if (c == null) return;
            if (_deadline == 0) _deadline = EditorApplication.timeSinceStartup + 90;
            if (EditorApplication.timeSinceStartup < _at) return;
            try
            {
                if (EditorApplication.timeSinceStartup > _deadline) throw new TimeoutException("Deck feedback fixture timed out.");
                var root = ((UIDocument)Get(c, "battleDocument")).rootVisualElement;
                var hand = root.Q<VisualElement>("deck-row");
                var views = (Dictionary<string, VisualElement>)Get(c, "_handCardViews");
                switch (_step)
                {
                    case 0:
                        if (!_backgroundCaptured) { _background = Application.runInBackground; _backgroundCaptured = true; }
                        Application.runInBackground = true;
                        if (c.PresentationPhase != BattlePresentationPhase.Planning) return;
                        _fixture = ((IBattleSession)Get(c, "_session")).GetSnapshot();
                        var owner = _fixture.Player.Fighters[0];
                        _fixture.Player.Hand = new List<CardState> { Card("fixture-a", owner, 1),
                            Card("fixture-spacer", owner, 2), Card("fixture-b", owner, 1) };
                        _fixture.Player.HandCapacity = 7; _fixture.ActionBudget = 3;
                        Set(c, "_session", new LocalBattleSession(_fixture));
                        Set(c, "_draft", null);
                        Call(c, "RefreshView");
                        var hud = ((Dictionary<string, CoreFighterHud>)Get(c, "_fighterBillboards")).Values.First();
                        var active = hud.gameObject.activeSelf;
                        var inactiveErrors = 0;
                        Application.LogCallback monitor = (message, trace, type) =>
                        { if (type == LogType.Error && message.Contains("Coroutine couldn't be started")) inactiveErrors++; };
                        Application.logMessageReceived += monitor;
                        try
                        {
                            hud.gameObject.SetActive(false);
                            hud.SetPowerGauge(5); hud.SetPowerGauge(0);
                        }
                        finally { Application.logMessageReceived -= monitor; hud.gameObject.SetActive(active); }
                        Check(inactiveErrors == 0, "Hidden fighter gauge tried to start an animation coroutine.");
                        Next(1); break;
                    case 1:
                        Check(views["fixture-a"].Q<CardEnergyElement>("card-merge-hint").Merge &&
                            views["fixture-b"].Q<CardEnergyElement>("card-merge-hint").Merge,
                            "Matching cards do not advertise merge availability before dragging.");
                        Check(!views["fixture-spacer"].Q<CardEnergyElement>("card-merge-hint").Merge,
                            "An unrelated skill advertised a false merge.");
                        var cappedHand = _fixture.Player.Clone();
                        cappedHand.Hand.Find(card => card.Id == "fixture-a").Rank = 3;
                        cappedHand.Hand.Find(card => card.Id == "fixture-b").Rank = 3;
                        Call(c, "RefreshMergeAvailability", cappedHand);
                        Check(views.Values.All(view => !view.Q<CardEnergyElement>("card-merge-hint").Merge),
                            "Rank-three cards advertised a false merge.");
                        foreach (var card in cappedHand.Hand) card.Kind = CardKind.Ultimate;
                        Call(c, "RefreshMergeAvailability", cappedHand);
                        Check(views.Values.All(view => !view.Q<CardEnergyElement>("card-merge-hint").Merge),
                            "Ultimate cards advertised a false merge.");
                        Call(c, "RefreshMergeAvailability", _fixture.Player);
                        Capture("00-merge-available");
                        Next(17); break;
                    case 17:
                        _initialSpacing = views["fixture-a"].resolvedStyle.left - views["fixture-spacer"].resolvedStyle.left;
                        Check(_initialSpacing <= views["fixture-a"].Q<Button>().resolvedStyle.width + .1f,
                            "Hand wrappers leave an empty gap between visible card faces.");
                        Drag(hand);
                        Check((int)Get(c, "_dragDestination") == 2, "Drag preview chose the wrong logical slot after BringToFront.");
                        Check(Get(c, "_draft") == null, "Drag preview spent an action before release.");
                        Check(views["fixture-a"].Q<CardEnergyElement>().Merge && views["fixture-b"].Q<CardEnergyElement>().Merge,
                            "Merge-compatible cards did not glow.");
                        Check(root.Q("drag-landing") != null, "Drag landing preview is missing.");
                        Next(2, .2); break;
                    case 2:
                        Check(views["fixture-b"].Q<CardEnergyElement>("card-merge-hint").MergeReady &&
                            views["fixture-b"].Q<Label>("card-merge-badge").text == "RANK II",
                            "Actual merge destination does not show the predicted upgraded rank.");
                        Check(views["fixture-a"].resolvedStyle.scale.value.x > 1.15f, "Held card did not enlarge.");
                        Check(Mathf.Abs(views["fixture-spacer"].resolvedStyle.translate.x) > 10, "Passed cards did not slide out of the landing slot.");
                        Capture("01-drag-merge-preview");
                        Next(3); break;
                    case 3:
                        using (var up = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, mousePosition = _destination, button = 0 }))
                        { up.target = _held; _held.SendEvent(up); }
                        var draft = (PlanDraft)Get(c, "_draft");
                        Check(draft != null && draft.Actions.Count == 1 && draft.Actions[0].DestinationIndex == 2,
                            "Drop did not commit exactly one move to the shown landing slot.");
                        Check(draft.Preview.Hand.Count == 2, "Shown merge prediction did not match the actual drop.");
                        c.StartCoroutine(ObserveMerge(c));
                        Next(4, 1); break;
                    case 4:
                        if (!(bool)c.GetType().GetProperty("CanPlan", Private).GetValue(c)) return;
                        Check(_bothRushed, "Merge did not move both cards toward their shared midpoint.");
                        Check(_mergeFlashed, "Merge collision did not create a bright white impact: " + _impactSamples);
                        Check(Mathf.Abs(Mathf.Abs(views["fixture-spacer"].resolvedStyle.left - views["fixture-b"].resolvedStyle.left) - _initialSpacing) < .1f,
                            "Hand spacing changed when the card count decreased.");
                        Capture("03-compact-hand-after-merge"); Next(14); break;
                    case 14:
                        Call(c, "ResetDraft"); Next(5); break;
                    case 5:
                        Drag(hand);
                        _held.ReleasePointer(_pointer);
                        Next(6, .2); break;
                    case 6:
                        Check(((PlanDraft)Get(c, "_draft")).Actions.Count == 0 && Get(c, "_dragCardId") == null,
                            "Capture loss committed a move or left a stale preview.");
                        Check(root.Q("drag-landing") == null, "Cancelled drag left its landing marker behind.");
                        Check(views.Values.All(view => !view.Q<CardEnergyElement>().Merge), "Cancelled drag left merge glow behind.");
                        Check(views.Values.All(view => !view.Q<CardEnergyElement>("card-merge-hint").MergeReady),
                            "Cancelled drag left a ready-to-merge cue behind.");
                        Check(views.Values.All(view => view.resolvedStyle.scale == Vector3.one), "Cancelled drag left an enlarged card.");
                        Set(c, "_displayState", _fixture.Clone()); Set(c, "_isAnimatingCards", true);
                        _draw = new BattleEvent { Kind = BattleEventKind.CardDrawn, SourceId = _fixture.Player.Fighters[0].Id,
                            CardId = "fixture-draw", Card = Card("fixture-draw", _fixture.Player.Fighters[0], 2) };
                        _drawSamples = 0;
                        c.StartCoroutine((IEnumerator)Call(c, "AnimateCardEvent", _draw, false));
                        Next(7, 0); break;
                    case 7:
                        if (views.TryGetValue(_draw.CardId, out var drawn))
                        {
                            Check(drawn.resolvedStyle.scale == Vector3.one, "Normal draw scaled the card.");
                            Check(Mathf.Abs(drawn.resolvedStyle.translate.y) < .1f && drawn.resolvedStyle.translate.x <= 0,
                                "Draw did not travel horizontally from the left.");
                            _drawSamples++;
                        }
                        if (_drawSamples < 4) return;
                        Next(8, .15); break;
                    case 8:
                        Check(views[_draw.CardId].resolvedStyle.translate.sqrMagnitude < .1f, "Normal draw did not settle in its slot.");
                        _draw = new BattleEvent { Kind = BattleEventKind.CardDrawn, SourceId = _fixture.Player.Fighters[0].Id,
                            CardId = "fixture-ultimate", Card = new CardState { Id = "fixture-ultimate", OwnerFighterId = _fixture.Player.Fighters[0].Id,
                                Kind = CardKind.Ultimate, Rank = 1, Category = CardCategory.Ultimate } };
                        c.StartCoroutine((IEnumerator)Call(c, "AnimateCardEvent", _draw, false));
                        Next(9, .05); break;
                    case 9:
                        Check(views[_draw.CardId].Q<CardEnergyElement>().Ultimate, "Ultimate energy background was not enabled.");
                        Check(views[_draw.CardId].resolvedStyle.scale == Vector3.one, "Ultimate draw changed card size.");
                        Check(Mathf.Abs(views[_draw.CardId].resolvedStyle.translate.y) < .1f, "Ultimate draw left the horizontal path.");
                        Capture("04-ultimate-left-draw"); Next(10, .2); break;
                    case 10:
                        Check(views[_draw.CardId].resolvedStyle.translate.sqrMagnitude < .1f, "Ultimate did not settle after its slide.");
                        Capture("05-ultimate-energy"); Next(15); break;
                    case 15:
                        Set(c, "_isAnimatingCards", false); Call(c, "RefreshView");
                        var enemy = _fixture.Clone(); enemy.ActingSide = TeamSide.Opponent;
                        enemy.ActionBudget = 3; enemy.Opponent.Hand.Clear();
                        for (var i = 0; i < 3; i++) enemy.Opponent.Hand.Add(Card("enemy-fixture-" + i, enemy.Opponent.Fighters[i], 1));
                        var enemyDraft = new PlanDraft(enemy);
                        foreach (var card in enemy.Opponent.Hand)
                            Check(enemyDraft.QueuePlay(card.Id, enemy.Player.Fighters[0].Id, out var reason), reason);
                        Check(BattleEngine.TryResolvePlan(enemy, enemyDraft.BuildPlan("visual-enemy-plan"), out var resolved, out var error), error);
                        _enemyEvents = resolved.Events.Skip(enemy.Events.Count).ToList();
                        Set(c, "_snapshot", enemy); Set(c, "_displayState", enemy.Clone()); Set(c, "_isPlayingEvents", true);
                        Call(c, "RenderEnemyHand");
                        c.StartCoroutine((IEnumerator)Call(c, "PresentCommittedPlan", _enemyEvents[0]));
                        Next(11, 1.6); break;
                    case 11:
                        Check(c.PresentationPhase == BattlePresentationPhase.EnemyPlanning, "Enemy did not enter a draft presentation state.");
                        Check(root.Q("enemy-plan-row").childCount == 3, "Enemy draft did not display the full committed queue.");
                        Check(((BattleState)Get(c, "_displayState")).Opponent.Hand.Count == 3, "Draft visuals consumed live enemy cards early.");
                        var enemyViews = (Dictionary<string, VisualElement>)Get(c, "_enemyHandCardViews");
                        Check(root.Q("enemy-deck-field").worldBound.xMax < root.worldBound.width * .4f &&
                            root.Q("enemy-plan-field").worldBound.xMax < root.worldBound.width * .4f,
                            "Enemy hand and action queue did not move to the left side.");
                        Check(enemyViews["enemy-fixture-0"].resolvedStyle.width <= 52.1f &&
                            enemyViews["enemy-fixture-0"].resolvedStyle.height <= 80.1f,
                            "Enemy deck cards did not shrink.");
                        Check(Mathf.Abs(enemyViews["enemy-fixture-0"].resolvedStyle.left - enemyViews["enemy-fixture-1"].resolvedStyle.left) <=
                            enemyViews["enemy-fixture-0"].resolvedStyle.width + .1f, "Enemy cards leave empty gaps.");
                        Capture("06-enemy-plan-locked"); Next(16); break;
                    case 16:
                        var played = _enemyEvents.First(item => item.Kind == BattleEventKind.CardPlayed);
                        Call(c, "AdvanceExecution", played);
                        c.StartCoroutine((IEnumerator)Call(c, "AnimateEnemyCardUse", played));
                        Next(12, .6); break;
                    case 12:
                        Check(((BattleState)Get(c, "_displayState")).Opponent.Hand.Count == 2, "Enemy card use failed to consume its selected live card.");
                        var skipped = new BattleEvent { Kind = BattleEventKind.ActionFizzled, CardId = "enemy-fixture-1",
                            SourceId = _fixture.Opponent.Fighters[1].Id, Message = "Fixture interruption" };
                        Call(c, "AdvanceExecution", skipped);
                        Check(root.Q("enemy-plan-row")[1].ClassListContains("fizzled"), "Interrupted enemy action did not mark its queue slot as skipped.");
                        Capture("07-enemy-plan-skipped"); Next(13); break;
                    case 13:
                        var authority = SnapshotJson.Serialize(((IBattleSession)Get(c, "_session")).GetSnapshot());
                        Call(c, "SkipAnimations");
                        Check(authority == SnapshotJson.Serialize(((IBattleSession)Get(c, "_session")).GetSnapshot()), "Skip changed authority state.");
                        Check(root.Q("enemy-plan-field").resolvedStyle.display == DisplayStyle.None, "Skip left the enemy queue visible.");
                        SessionState.SetBool(Running, false);
                        Application.runInBackground = _background;
                        Debug.Log("DECK FEEDBACK VISUAL PASS: " + _assertions + " assertions; evidence in Logs/DeckFeedbackEvidence.");
                        break;
                }
            }
            catch (Exception error)
            {
                SessionState.SetBool(Running, false);
                Application.runInBackground = _background;
                Debug.LogError("DECK FEEDBACK VISUAL FAIL: step " + _step + " " + error);
            }
        }
    }
}
