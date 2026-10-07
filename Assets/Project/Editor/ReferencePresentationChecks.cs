using System;
using System.Collections;
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
    /// <summary>Disposable visual fixtures. Run in a separate project copy; never uses a saved run.</summary>
    [InitializeOnLoad]
    public static class ReferencePresentationChecks
    {
        private const string Pending = "ReferencePresentationChecks.Pending";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static double _deadline;
        private static int _errors;
        private static int _indexingErrors;

        static ReferencePresentationChecks()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (!SessionState.GetBool(Pending, false) || (type != LogType.Error && type != LogType.Exception)) return;
                // Known Unity Search cold-import failure; keep it reported separately from runtime failures.
                if (stack != null && stack.Contains("UnityEditor.Search.SearchDatabase")) _indexingErrors++;
                else _errors++;
            };
        }

        public static void RunAutomated()
        {
            SessionState.SetBool(Pending, true);
            BattlePresentationPreview.PlayerFirst();
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (_deadline == 0) _deadline = EditorApplication.timeSinceStartup + 150;
            if (EditorApplication.timeSinceStartup > _deadline) { Finish(false, "Timed out"); return; }
            if (!EditorApplication.isPlaying) return;
            var controller = UnityEngine.Object.FindFirstObjectByType<CoreBattleSceneController>();
            if (controller == null || !(bool)Get(controller, "CanPlan", true)) return;
            EditorApplication.update -= Tick;
            controller.StartCoroutine(Guard(Check(controller)));
        }

        private static object Get(object obj, string field, bool property = false) => property
            ? obj.GetType().GetProperty(field, Private).GetValue(obj) : obj.GetType().GetField(field, Private).GetValue(obj);
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
        private static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Private).Invoke(obj, args);
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

        // Unity otherwise logs a coroutine exception but leaves a batch process running forever.
        private static IEnumerator Guard(IEnumerator routine)
        {
            while (true)
            {
                object next;
                try { if (!routine.MoveNext()) break; next = routine.Current; }
                catch (Exception e) { Finish(false, e.ToString()); yield break; }
                if (next is IEnumerator child) yield return Guard(child);
                else yield return next;
            }
        }

        private static IEnumerator Check(CoreBattleSceneController controller)
        {
            var state = ((BattleState)Get(controller, "_snapshot")).Clone();
            var session = (IBattleSession)Get(controller, "_session");
            var authority = JsonUtility.ToJson(session.GetSnapshot());
            var document = (UIDocument)Get(controller, "battleDocument");
            var stage = (BattleStagePresenter)Get(controller, "_stage");
            CheckEnemyHand(controller, document, state);
            Capture(document, "00-enemy-hand");
            yield return CheckCardKinds(controller, document, state);
            var source = state.Player.LivingActive()[0];
            var target = state.Opponent.LivingActive()[0];
            var card = state.Player.Hand.First(c => c.OwnerFighterId == source.Id).Clone();
            Call(controller, "QueueMoveTo", state.Player.Hand[0].Id, state.Player.Hand.Count - 1);
            var moveDeadline = Time.realtimeSinceStartup + 5;
            while ((bool)Get(controller, "_isAnimatingCards") && Time.realtimeSinceStartup < moveDeadline) yield return null;
            Require(!(bool)Get(controller, "_isAnimatingCards"), "Hand move did not settle");
            var draft = (PlanDraft)Get(controller, "_draft");
            Require(draft != null && draft.Actions.Count == 1 && draft.Actions[0].IsMove, "Move must spend exactly one draft action");
            Capture(document, "00-hand-move");
            Call(controller, "ResetDraft");
            Require(authority == JsonUtility.ToJson(session.GetSnapshot()), "Draft move/reset changed authority");
            Set(controller, "_displayState", state);
            Set(controller, "_isPlayingEvents", true);
            source.PowerGauge = CardRules.UltimateGaugeCost;
            state.Player.Hand.RemoveAll(c => c.Kind == CardKind.Ultimate);
            Call(controller, "SyncUltimateReady");
            Require(document.rootVisualElement.Query(className: "ultimate-ready-tag").ToList().Count == 0, "5 PG alone must not show ready");
            var ultimate = card.Clone(); ultimate.Id = "fixture:ultimate"; ultimate.Kind = CardKind.Ultimate;
            state.Player.Hand.Add(ultimate);
            Call(controller, "SyncUltimateReady");
            yield return new WaitForSecondsRealtime(.35f);
            Require(document.rootVisualElement.Query(className: "ultimate-ready-tag").ToList().Count == 1, "Ready needs sidebar");
            Capture(document, "01-ready");
            source.PowerGauge = 4;
            Call(controller, "SyncUltimateReady");
            Require(document.rootVisualElement.Query(className: "ultimate-ready-tag").ToList().Count == 0, "PG drain clears ready");
            source.PowerGauge = 5;
            source.IsAlive = false;
            Call(controller, "SyncUltimateReady");
            Require(document.rootVisualElement.Query(className: "ultimate-ready-tag").ToList().Count == 0, "Death clears ready");
            source.IsAlive = true;
            for (var rank = 1; rank <= 3; rank++)
            {
                card.Rank = rank;
                var played = new BattleEvent { Kind = BattleEventKind.CardPlayed, SourceId = source.Id, TargetId = target.Id,
                    CardId = card.Id, Card = card.Clone(), PowerGaugeAfter = 4 };
                yield return (IEnumerator)Call(controller, "PresentEvent", played);
                Capture(document, "02-rank" + rank);
                Call(controller, "FloatText", target.Id, "CRITICAL\n36,042", "critical");
                yield return new WaitForSecondsRealtime(.2f);
                Capture(document, "03-impact-rank" + rank);
                yield return stage.ReturnToPlanning(TeamSide.Player);
            }
            var cutIn = new BattleEvent { Kind = BattleEventKind.CardPlayed, SourceId = source.Id, TargetId = target.Id,
                CardId = ultimate.Id, Card = ultimate, PowerGaugeAfter = 0 };
            var cutRoutine = controller.StartCoroutine((IEnumerator)Call(controller, "UltimateCutIn", cutIn));
            yield return new WaitForSecondsRealtime(.35f);
            Require(document.rootVisualElement.Q("ultimate-cut-in") != null, "Ultimate cut-in visible");
            Capture(document, "04-ultimate-cut-in");
            yield return cutRoutine;
            yield return (IEnumerator)Call(controller, "PresentEvent", cutIn);
            Require(document.rootVisualElement.Query(className: "ultimate-ready-tag").ToList().Count == 0, "Ultimate consumption clears ready");
            yield return stage.ReturnToPlanning(TeamSide.Player);
            Call(controller, "FloatText", source.Id, "Active Unique", "passive");
            Call(controller, "FloatText", target.Id, "Attack Increase", "status");
            yield return new WaitForSecondsRealtime(.2f);
            Capture(document, "05-status");
            controller.StartCoroutine((IEnumerator)Call(controller, "UltimateCutIn", cutIn));
            yield return new WaitForSecondsRealtime(.1f);
            Call(controller, "SkipAnimations");
            Require(document.rootVisualElement.Q("ultimate-cut-in") == null, "Skip clears cut-in");
            Require(authority == JsonUtility.ToJson(session.GetSnapshot()), "Presentation changed authority");
            Finish(_errors == 0, "Move/reset, ready gating, PG drain, death, ranks 1/2/3, ultimate cut-in/consumption, FCT, mid-cut-in skip; runtime errors=" + _errors + "; Unity Search startup errors=" + _indexingErrors);
        }

        private static IEnumerator CheckCardKinds(CoreBattleSceneController controller, UIDocument document, BattleState state)
        {
            var source = state.Player.LivingActive()[0];
            var card = state.Player.Hand.First(c => c.OwnerFighterId == source.Id);
            var originalCategory = card.Category;
            var originalScope = card.TargetScope;
            card.Category = CardCategory.Recovery; card.TargetScope = EffectTargetScope.SelectedAlly;
            Set(controller, "_snapshot", state.Clone());
            Set(controller, "_draft", new PlanDraft(state));
            Call(controller, "QueueCard", card.Id);
            yield return null;
            var picker = document.rootVisualElement.Q("ally-target-picker");
            Require(picker != null && picker.Query<Button>().ToList().Count == 4, "Ally picker must show three active allies and cancel");
            Capture(document, "10-ally-picker");
            picker.RemoveFromHierarchy(); Set(controller, "_allyPicker", null);
            Call(controller, "ResetDraft");
            Set(controller, "_isPlayingEvents", true);
            Set(controller, "_displayState", state.Clone());
            var allies = state.Player.LivingActive().Select(f => f.Id).ToList();
            var stage = (BattleStagePresenter)Get(controller, "_stage");
            yield return stage.SupportAction(source.Id, allies, CardCategory.Recovery);
            var feedback = new System.Collections.Generic.List<BattleEvent>();
            foreach (var ally in state.Player.LivingActive())
                feedback.Add(new BattleEvent { Kind = BattleEventKind.HealApplied, SourceId = source.Id,
                    TargetId = ally.Id, Amount = 80, HealthAfter = ally.Health });
            var batch = controller.StartCoroutine((IEnumerator)Call(controller, "PresentFeedbackBatch", feedback));
            yield return new WaitForSecondsRealtime(.1f);
            Capture(document, "11-team-heal");
            yield return batch;
            var stance = new StatusRecipeDefinition { Id = "preview.stance", Polarity = StatusPolarity.Buff,
                Behavior = StatusBehavior.Stance, DefaultDuration = 2,
                StanceChildren = new System.Collections.Generic.List<StanceChildDefinition> {
                    new StanceChildDefinition { Id = "preview.taunt", Taunt = true },
                    new StanceChildDefinition { Id = "preview.counter" } } };
            StatusSystem.Apply(source, source.Id, source.Side, stance, "preview-stance", "preview", 1);
            yield return (IEnumerator)Call(controller, "PresentEvent", new BattleEvent {
                Kind = BattleEventKind.StatusApplied, SourceId = source.Id, TargetId = source.Id,
                StatusRecipeId = stance.Id, StatusesAfter = source.Statuses.Instances.ConvertAll(s => s.Clone()) });
            yield return stage.SupportAction(source.Id, new[] { source.Id }, CardCategory.Stance);
            Capture(document, "12-stance");
            var attacker = state.Opponent.LivingActive()[0];
            var counter = new BattleEvent { Kind = BattleEventKind.CounterStarted, SourceId = source.Id,
                TargetId = attacker.Id, TargetIds = new System.Collections.Generic.List<string> { attacker.Id },
                Card = new CardState { Category = CardCategory.Debuff, Rank = 1 } };
            var routine = controller.StartCoroutine((IEnumerator)Call(controller, "PresentEvent", counter));
            yield return new WaitForSecondsRealtime(.15f);
            Capture(document, "13-counter");
            yield return routine;
            yield return (IEnumerator)Call(controller, "PresentEvent", new BattleEvent {
                Kind = BattleEventKind.CounterEnded, SourceId = source.Id, TargetId = attacker.Id });
            card.Category = originalCategory; card.TargetScope = originalScope;
            source.Statuses.Instances.Clear();
            Set(controller, "_snapshot", state.Clone());
            Set(controller, "_displayState", state.Clone());
            Set(controller, "_isPlayingEvents", false);
            Set(controller, "_draft", new PlanDraft(state));
            Call(controller, "RefreshView");
            yield return stage.ReturnToPlanning(TeamSide.Player);
        }

        private static void CheckEnemyHand(CoreBattleSceneController controller, UIDocument document, BattleState state)
        {
            var row = document.rootVisualElement.Q<VisualElement>("enemy-deck-row");
            Require(row != null, "Enemy deck row is missing");
            Require(row.childCount == state.Opponent.Hand.Count,
                "Enemy deck row must match opponent hand size");
            for (var visualIndex = 0; visualIndex < row.childCount; visualIndex++)
            {
                var view = row[visualIndex];
                var card = state.Opponent.Hand[state.Opponent.Hand.Count - 1 - visualIndex];
                var rank = Mathf.Clamp(card.Rank, 1, 3);
                Require(view.ClassListContains("enemy-hand-card"), "Enemy card must use its concealed presentation");
                Require(view.Q<Image>("artwork") == null && view.Q<Label>("card-skill-slot") == null &&
                    view.Q<Image>("card-skill-type") == null,
                    "Enemy card field must not expose owner-linked artwork or skill identity");
                Require(view.Q<VisualElement>("enemy-card-disabled-overlay") != null,
                    "Enemy card must include the same disabled black-overlay feedback as the player hand");
                var badge = view.Q<Label>("enemy-card-rank-badge");
                Require(badge != null && badge.text == (rank == 3 ? "III" : rank == 2 ? "II" : "I"),
                    "Enemy card rank badge is incorrect");
                Require(view.ClassListContains(rank == 3 ? "rank-three" : rank == 2 ? "rank-two" : "rank-one"),
                    "Enemy card rank visual class is incorrect");
            }

            if (state.Opponent.Hand.Count == 0) return;
            var displayedState = (BattleState)Get(controller, "_snapshot");
            var disabledCard = displayedState.Opponent.Hand[0];
            var disabledOwner = displayedState.Opponent.FindFighter(disabledCard.OwnerFighterId);
            Require(disabledOwner != null, "Enemy card owner is missing from authority state");
            var wasAlive = disabledOwner.IsAlive;
            disabledOwner.IsAlive = false;
            Call(controller, "RenderEnemyHand");
            var disabledVisualIndex = displayedState.Opponent.Hand.Count - 1;
            Require(!row[disabledVisualIndex].ClassListContains("card-unavailable"),
                "General unavailable state must not black out an enemy card");
            disabledOwner.IsAlive = wasAlive;

            var disable = new StatusInstance
            {
                InstanceId = "preview:disable-card",
                RecipeId = "preview:disable-card",
                SourceFighterId = displayedState.Player.Fighters[0].Id,
                TargetFighterId = disabledOwner.Id,
                StackCount = 1,
                RemainingDuration = 1,
                Recipe = new StatusRecipeDefinition
                {
                    Id = "preview:disable-card",
                    Behavior = StatusBehavior.Disable,
                    Polarity = StatusPolarity.Debuff,
                    DisableMask = CardCategoryMask.AllCards
                }
            };
            disabledOwner.Statuses.Instances.Add(disable);
            Call(controller, "RenderEnemyHand");
            Require(row[disabledVisualIndex].ClassListContains("card-disabled-by-status"),
                "Disable-card debuff must activate the enemy card black overlay");
            disabledOwner.Statuses.Instances.Remove(disable);
            Call(controller, "RenderEnemyHand");
        }

        private static void Capture(UIDocument document, string name)
        {
            var directory = Path.GetFullPath("Logs/ReferencePresentationEvidence");
            Directory.CreateDirectory(directory);
            BattlePreviewCapture.Save(Path.Combine(directory, name + ".png"), document);
        }

        private static void Finish(bool pass, string message)
        {
            SessionState.SetBool(Pending, false);
            Debug.Log("REFERENCE PRESENTATION " + (pass ? "PASS " : "FAIL ") + message);
            EditorApplication.Exit(pass ? 0 : 1);
        }
    }
}
