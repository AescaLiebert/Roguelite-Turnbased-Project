using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Combat
{
    public enum BattlePresentationPhase { ComparingCC, OpeningDeal, Planning, CardExecution, TurnSwitch, Complete, ResolvingStatus, EnemyPlanning }

    public sealed partial class CoreBattleSceneController
    {
        public BattlePresentationPhase PresentationPhase { get; private set; }
        [System.NonSerialized] private BattleState _displayState;
        private BattleStagePresenter _stage;
        private bool _hudRevealed;
        private bool _isAnimatingCards;
        private bool _commitPending;
        private bool _suppressDraftCardPlayback;
        private readonly List<CardState> _draftCards = new List<CardState>();
        private readonly List<BattleEvent> _executionEvents = new List<BattleEvent>();
        private HitReaction _actionFramingReaction;

        private static HitReaction SnapshotActionReaction(IReadOnlyList<BattleEvent> events, int start)
        {
            var reaction = HitReaction.Hit;
            var action = events[start];
            for (var i = start + 1; i < events.Count; i++)
            {
                var item = events[i];
                if (item.Kind == BattleEventKind.CardPlayed || item.Kind == BattleEventKind.CounterStarted ||
                    item.Kind == BattleEventKind.ActionCompleted || item.Kind == BattleEventKind.CounterEnded) break;
                if (item.SourceId != action.SourceId || !item.HasHitReaction) continue;
                if (item.Reaction == HitReaction.KnockUp) return HitReaction.KnockUp;
                if (item.Reaction == HitReaction.KnockBack) reaction = HitReaction.KnockBack;
            }
            return reaction;
        }
        private int _executionIndex = -1;
        private VisualElement _tray;
        private VisualElement _effectsLayer;
        private VisualElement _turnBanner;
        private Label _turnBannerTitle;
        private Label _turnBannerSub;
        private Label _phaseLabel;
        private Label _phaseTurnLabel;
        private Label _executionLabel;
        private VisualElement _executionOverlay;
        private VisualElement _execCardContainer;
        private VisualElement _execCardFace;
        private Image _execCardArtwork;
        private VisualElement _execCardHoloGlow;
        private Image _execCardRankFrame;
        private Label _execCardSkillSlot;
        private Image _execCardSkillType;
        private readonly List<Label> _execCardStars = new List<Label>();
        private Image _execTypeIcon;
        private Label _execCardTitle;
        private VisualElement _execStatusGrid;
        private Label _execCardDesc;
        private Label _execKeywordsDesc;
        private bool _isExecutionOverlayVisible;
        private VisualElement _resultPanel;
        private Label _resultTitle;
        private Button _commitButton;
        private VisualElement _topRightControls;
        private VisualElement _ccComparison;
        private Button _skipButton;
        private readonly Dictionary<string, int> _textLanes = new Dictionary<string, int>();
        private readonly FloatingCombatTextPool _fctPool = new FloatingCombatTextPool();
        private bool CanPlan => _inspector == null && _openingSequenceComplete && !_isPlayingEvents && !_isAnimatingCards && !_commitPending &&
            _snapshot != null && _snapshot.Phase == BattlePhase.Planning && _snapshot.ActingSide == TeamSide.Player &&
            PresentationPhase == BattlePresentationPhase.Planning;

        private void BindPresentationHud(VisualElement root)
        {
            _tray = root.Q<VisualElement>("battle-card-tray");
            _effectsLayer = root.Q<VisualElement>("battle-effects");
            _fctPool.Initialize(_effectsLayer, 24, FctSettings);
            _turnBanner = root.Q<VisualElement>("turn-banner");
            _turnBannerTitle = root.Q<Label>("turn-banner-title");
            _turnBannerSub = root.Q<Label>("turn-banner-sub");
            _phaseLabel = root.Q<Label>("battle-phase");
            _phaseTurnLabel = root.Q<Label>("battle-phase-turn");
            _executionLabel = root.Q<Label>("execution-label");
            _executionOverlay = root.Q<VisualElement>("card-execution-overlay");
            _execCardContainer = root.Q<VisualElement>("exec-card-container");
            _execCardFace = root.Q<VisualElement>("exec-card-face");
            _execCardArtwork = root.Q<Image>("exec-card-artwork");
            _execCardHoloGlow = root.Q<VisualElement>("exec-card-holo-glow");
            _execCardRankFrame = root.Q<Image>("exec-card-rank-frame");
            _execCardSkillSlot = root.Q<Label>("exec-card-skill-slot");
            _execCardSkillType = root.Q<Image>("exec-card-skill-type");
            _execCardStars.Clear();
            for (var i = 0; i < 3; i++)
            {
                var star = root.Q<Label>("exec-card-star-" + i);
                if (star != null) _execCardStars.Add(star);
            }
            _execTypeIcon = root.Q<Image>("exec-type-icon");
            _execCardTitle = root.Q<Label>("exec-card-title");
            _execStatusGrid = root.Q<VisualElement>("exec-status-grid");
            _execCardDesc = root.Q<Label>("exec-card-desc");
            _execKeywordsDesc = root.Q<Label>("exec-keywords-desc");
            if (_executionOverlay != null) _executionOverlay.style.display = DisplayStyle.None;
            _resultPanel = root.Q<VisualElement>("battle-result");
            _resultTitle = root.Q<Label>("result-title");
            _commitButton = root.Q<Button>("commit-button");
            _topRightControls = root.Q<VisualElement>("top-right-controls");
            _ccComparison = root.Q<VisualElement>("cc-comparison");
            _skipButton = root.Q<Button>("skip-animation");
            if (_skipButton != null) _skipButton.clicked += SkipAnimations;
            if (_commitButton != null) _commitButton.clicked += () =>
            {
                if (!CanPlan) return;
                _commitPending = true;
                CommitDraft();
            };
            var continueButton = root.Q<Button>("result-continue");
            if (continueButton != null) continueButton.clicked += FinishBattle;
            if (_effectsLayer != null) _effectsLayer.pickingMode = PickingMode.Ignore;
            if (_turnBanner != null) _turnBanner.pickingMode = PickingMode.Ignore;
            if (_phaseLabel != null) _phaseLabel.pickingMode = PickingMode.Ignore;
            SetPhase(BattlePresentationPhase.ComparingCC);
        }

        private void SetPhase(BattlePresentationPhase phase)
        {
            PresentationPhase = phase;
            if (_skipButton != null) _skipButton.style.display = phase == BattlePresentationPhase.Planning ||
                phase == BattlePresentationPhase.Complete ? DisplayStyle.None : DisplayStyle.Flex;
            if (_phaseTurnLabel != null && _snapshot != null)
            {
                _phaseTurnLabel.text = "TURN " + Mathf.Max(1, _snapshot.TurnNumber);
            }
            if (_phaseLabel == null) return;
            _phaseLabel.text = phase switch
            {
                BattlePresentationPhase.ComparingCC => "COMBAT CLASS",
                BattlePresentationPhase.OpeningDeal => "DECK INITIATE",
                BattlePresentationPhase.Planning => "YOUR TURN  ·  SELECT CARDS IN ORDER",
                BattlePresentationPhase.EnemyPlanning => "ENEMY TURN  ·  PLANNING ACTIONS",
                BattlePresentationPhase.CardExecution => "CARD EXECUTION",
                BattlePresentationPhase.TurnSwitch => "TURN CHANGE",
                BattlePresentationPhase.ResolvingStatus => "RESOLVING STATUS",
                _ => "BATTLE COMPLETE"
            };
        }

        private void SetBattleHudVisible(bool visible)
        {
            _hudRevealed = visible;
            if (_tray != null) _tray.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
            SetEnemyHandVisible(visible);
            if (_topRightControls != null) _topRightControls.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
            foreach (var hud in _fighterBillboards.Values)
                if (hud != null) hud.gameObject.SetActive(visible);
            // The authored uGUI tray is superseded by the UI Toolkit card tray.
            foreach (var transform in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform.name == "UnitUICanvas") transform.gameObject.SetActive(visible);
                if (transform.name == "WholeCardSystemUI") transform.gameObject.SetActive(false);
            }
        }

        private IEnumerator CompareCombatClass(int playerCC, int enemyCC, bool playerFirst, Sprite playerIcon, Sprite enemyIcon)
        {
            if (_ccComparison == null) { yield return FallbackOpening(playerCC, enemyCC, playerFirst); yield break; }
            _ccComparison.style.display = DisplayStyle.Flex;
            var player = _ccComparison.Q<VisualElement>("cc-player");
            var enemy = _ccComparison.Q<VisualElement>("cc-enemy");
            var playerValue = _ccComparison.Q<Label>("cc-player-value");
            var enemyValue = _ccComparison.Q<Label>("cc-enemy-value");
            _ccComparison.Q<Image>("cc-player-icon").sprite = playerIcon;
            _ccComparison.Q<Image>("cc-enemy-icon").sprite = enemyIcon;
            var decision = _ccComparison.Q<Label>("cc-decision");
            decision.text = "";
            playerValue.text = enemyValue.text = "0";
            yield return BattleStagePresenter.Tween(.4f, t =>
            {
                player.style.translate = new Translate(-450 * (1 - t), 0);
                enemy.style.translate = new Translate(450 * (1 - t), 0);
                _ccComparison.style.opacity = t;
            });
            yield return BattleStagePresenter.Tween(1f, t =>
            {
                playerValue.text = Mathf.RoundToInt(playerCC * t).ToString("N0");
                enemyValue.text = Mathf.RoundToInt(enemyCC * t).ToString("N0");
            });
            var winner = playerFirst ? player : enemy;
            winner.AddToClassList("initiative-winner");
            decision.text = playerFirst ? "YOU GO FIRST" : "ENEMY GOES FIRST";
            yield return new WaitForSecondsRealtime(.85f);
            yield return BattleStagePresenter.Tween(.3f, t =>
            {
                player.style.translate = new Translate(-450 * t, 0);
                enemy.style.translate = new Translate(450 * t, 0);
                _ccComparison.style.opacity = 1 - t;
            });
            winner.RemoveFromClassList("initiative-winner");
            _ccComparison.style.display = DisplayStyle.None;
            yield return OpeningDeal();
        }

        private IEnumerator FallbackOpening(int playerCC, int enemyCC, bool playerFirst)
        {
            yield return Banner("COMBAT CLASS\n" + playerCC.ToString("N0") + "   VS   " + enemyCC.ToString("N0"), false, 1.5f);
            yield return Banner(playerFirst ? "YOU GO FIRST" : "ENEMY GOES FIRST", !playerFirst, .8f);
            yield return OpeningDeal();
        }

        private IEnumerator OpeningDeal()
        {
            _isPlayingEvents = true;
            SetPhase(BattlePresentationPhase.OpeningDeal);
            SetBattleHudVisible(true);
            RefreshWorldViews();
            SetControls(false);
            yield return _stage.Turn(_snapshot.ActingSide);
            yield return Banner("DECK INITIATE", false, .55f);
            yield return PlayEvents(_session.GetEventsAfter(0));
        }

        private IEnumerator AnimateDraft(BattleTeamState before, IReadOnlyList<BattleEvent> events)
        {
            _isAnimatingCards = true;
            _displayState = _snapshot.Clone();
            _displayState.Player = before;
            SetControls(false);
            HideTooltip();
            RenderActionField();
            foreach (var item in events) yield return AnimateCardEvent(item, true);
            _isAnimatingCards = false;
            RefreshView();
            if (_draft != null && _draft.Actions.Count >= _snapshot.ActionBudget)
            {
                _commitPending = true;
                yield return CommitFilledActionField();
            }
        }

        private IEnumerator AnimateCardEvent(BattleEvent item, bool drafting = false)
        {
            var owner = _displayState.Player.FindFighter(item.SourceId) ?? _displayState.Opponent.FindFighter(item.SourceId);
            if (owner == null)
            {
                BattlePlaybackState.Apply(_displayState, item);
                yield break;
            }
            var side = owner.Side;
            var handViews = HandCardViews(side);
            var handPositions = CaptureHandPositions(side);
            handViews.TryGetValue(item.CardId ?? string.Empty, out var view);
            if (item.Kind == BattleEventKind.CardsMerged)
            {
                handViews.TryGetValue(item.ConsumedCardId ?? string.Empty, out var consumed);
                var impactPosition = Vector2.zero;
                var impactWidth = side == TeamSide.Player ? CardWidth : EnemyCardWidth;
                var impactHeight = side == TeamSide.Player ? CardHeight : EnemyCardHeight;
                var collided = view != null && consumed != null;
                if (collided)
                {
                    var halfDistance = (consumed.worldBound.center - view.worldBound.center) * .5f;
                    impactPosition = _effectsLayer.WorldToLocal((view.worldBound.center + consumed.worldBound.center) * .5f);
                    var survivorStart = view.resolvedStyle.translate;
                    var consumedStart = consumed.resolvedStyle.translate;
                    view.AddToClassList("merge-colliding");
                    consumed.AddToClassList("merge-colliding");
                    view.BringToFront();
                    consumed.BringToFront();
                    yield return BattleStagePresenter.Tween(.14f, t =>
                    {
                        // Accelerate both cards into their shared midpoint, at full opacity.
                        var rush = t * t;
                        view.style.translate = new Translate(survivorStart.x + halfDistance.x * rush, survivorStart.y + halfDistance.y * rush);
                        consumed.style.translate = new Translate(consumedStart.x - halfDistance.x * rush, consumedStart.y - halfDistance.y * rush);
                        view.style.scale = consumed.style.scale = new Scale(Vector3.one);
                    });
                    view.RemoveFromClassList("merge-colliding");
                    consumed.RemoveFromClassList("merge-colliding");
                    handPositions = CaptureHandPositions(side);
                }
                BattlePlaybackState.Apply(_displayState, item);
                RenderHand(side);
                if (collided) StartCoroutine(MergeImpact(impactPosition, impactWidth, impactHeight));
                yield return ReflowHand(handPositions, item.CardId, side);
                if (handViews.TryGetValue(item.CardId, out view))
                {
                    if (collided) StartCoroutine(CardCaption(view, "RANK " + (item.Amount >= 3 ? "III" : "II")));
                    if (!collided) yield return MergeFlash(view, item.Amount);
                }
                UpdateDisplayedHud(item.SourceId);
                yield return new WaitForSecondsRealtime(.06f);
                yield break;
            }
            if (side == TeamSide.Player && item.Kind == BattleEventKind.CardPlayed && drafting && view != null)
            {
                var slot = _actionSlots.Count - _draft.Actions.Count;
                if (slot >= 0 && slot < _actionSlots.Count)
                {
                    var destination = _actionSlots[slot].worldBound.center - view.worldBound.center;
                    _actionContents[slot].style.opacity = 0;
                    yield return BattleStagePresenter.Tween(.24f, t =>
                    {
                        view.style.translate = new Translate(destination.x * t, destination.y * t - Mathf.Sin(t * Mathf.PI) * 24);
                        view.style.scale = new Scale(Vector3.one * (1 - .12f * t));
                    });
                    _actionContents[slot].style.opacity = 1;
                }
            }
            BattlePlaybackState.Apply(_displayState, item);
            RenderHand(side);
            if (item.Kind == BattleEventKind.CardDrawn && handViews.TryGetValue(item.CardId, out view))
            {
                view.style.opacity = 1;
                view.style.scale = new Scale(Vector3.one);
                var ultimate = item.Card?.Kind == CardKind.Ultimate;
                _stage?.PlayCue(ultimate ? 4 : 0);
                // Enter from the left edge of the hand, including when only a few cards remain.
                var startX = -view.style.left.value.value - (side == TeamSide.Player ? CardWidth : EnemyCardWidth);
                view.style.translate = new Translate(startX, 0);
                view.style.rotate = new Rotate(new Angle(0, AngleUnit.Degree));
                var energy = view.Q<CardEnergyElement>("card-energy");
                yield return BattleStagePresenter.Tween(.12f, t =>
                {
                    view.style.translate = new Translate(startX * (1 - t), 0);
                    view.style.scale = new Scale(Vector3.one);
                    if (energy != null) { energy.Burst = ultimate ? Mathf.Sin(t * Mathf.PI) : 0; energy.MarkDirtyRepaint(); }
                });
                view.style.translate = new Translate(0, 0);
                view.style.rotate = new Rotate(new Angle(0, AngleUnit.Degree));
                view.style.scale = new Scale(Vector3.one);
                if (energy != null) energy.Burst = 0;
                if (item.Card?.Kind == CardKind.Ultimate) SyncUltimateReady();
            }
            else if (item.Kind == BattleEventKind.CardMoved || item.Kind == BattleEventKind.CardPlayed)
                yield return ReflowHand(handPositions, item.Kind == BattleEventKind.CardMoved ? item.CardId : null, side);
            else yield return new WaitForSecondsRealtime(.12f);
            UpdateDisplayedHud(item.SourceId);
        }

        private VisualElement _counterCue;
        private float _feedbackSettlesAt;
        private bool _resolvingTeamStatuses;
        private bool _hitWindow;
        private bool _hasQueuedCardExecution;
        private bool _turnChangePresented;
        private readonly HashSet<string> _statusAnimationActors = new HashSet<string>();
        private readonly HashSet<string> _passiveActivationActionsPresented = new HashSet<string>(System.StringComparer.Ordinal);
        private string _executionHiddenActorId;

        private IEnumerator WaitForFeedback()
        {
            while (Time.unscaledTime < _feedbackSettlesAt) yield return null;
            foreach (var id in _statusAnimationActors) yield return _stage.WaitForActionEnd(id);
            _statusAnimationActors.Clear();
        }

        private void PlayStatusAnimation(BattleEvent item)
        {
            if (string.IsNullOrEmpty(item.TargetId)) return;
            var duration = _stage.ActorStatusFeedback(item);
            _feedbackSettlesAt = Mathf.Max(_feedbackSettlesAt, Time.unscaledTime + duration);
            _statusAnimationActors.Add(item.TargetId);
        }

        private void PlayStatusRefreshFeedback(BattleEvent item, bool succeeded)
        {
            if (item == null || string.IsNullOrEmpty(item.TargetId) || string.IsNullOrEmpty(item.StatusRecipeId)) return;
            if (_fighterBillboards.TryGetValue(item.TargetId, out var hud) && hud != null)
                hud.PlayStatusRefreshFeedback(item.StatusRecipeId, succeeded);
        }

        private IEnumerator FinishActionVisual()
        {
            _damageTotalActionOpen = false;
            if (_stage != null) yield return _stage.WaitForTargetReactions();
            yield return RecoverExecutionActorVisuals();
            yield return WaitForFeedback();
            SetUltimateHud(false);
        }

        private IEnumerator RecoverExecutionActorVisuals()
        {
            if (_stage != null) yield return _stage.RecoverAttacker();
            RestoreExecutionActorVisuals();
        }

        private IEnumerator ReturnToPlanningAndRestore(TeamSide side, float duration)
        {
            if (_stage != null) yield return _stage.ReturnToPlanning(side, duration);
            RestoreExecutionActorVisuals();
        }

        private void HideExecutionActorVisuals(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (_executionHiddenActorId == id) return;
            if (!string.IsNullOrEmpty(_executionHiddenActorId) && _executionHiddenActorId != id)
                RestoreExecutionActorVisuals();
            _executionHiddenActorId = id;
            _stage?.SetPersistentEffectsHidden(id, true);
        }

        private void RestoreExecutionActorVisuals()
        {
            if (string.IsNullOrEmpty(_executionHiddenActorId)) return;
            var id = _executionHiddenActorId;
            _stage?.SetPersistentEffectsHidden(id, false);
            _executionHiddenActorId = null;
        }

        private bool ShouldPresentPassiveActivation(BattleEvent item)
        {
            if (item == null) return false;
            var actionId = string.IsNullOrEmpty(item.RootActionId) ? item.CardId : item.RootActionId;
            if (string.IsNullOrEmpty(actionId)) return true;
            return _passiveActivationActionsPresented.Add(actionId);
        }


        private static bool IsExpiredStatusMessage(string message) =>
            !string.IsNullOrEmpty(message) && message.IndexOf("expired", System.StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool IsSimultaneousFeedback(BattleEventKind kind) =>
            kind == BattleEventKind.DamageApplied || kind == BattleEventKind.HealApplied ||
            kind == BattleEventKind.StatusApplied || kind == BattleEventKind.StatusRemoved ||
            kind == BattleEventKind.PassiveTriggered || kind == BattleEventKind.PassiveStatsChanged ||
            kind == BattleEventKind.PowerGaugeChanged || kind == BattleEventKind.AttackEvaded ||
            kind == BattleEventKind.StatusImmuned || kind == BattleEventKind.StatusesChanged ||
            kind == BattleEventKind.RecoveryBlocked || kind == BattleEventKind.StatusWeaker;

        private IEnumerator PresentFeedbackBatch(IReadOnlyList<BattleEvent> batch)
        {
            var impacts = new List<BattleEvent>();
            var hitCount = 1;
            var visible = false;
            foreach (var item in batch)
            {
                BattlePlaybackState.Apply(_displayState, item);
                UpdateDisplayedHud(item.TargetId);
                if (item.Kind == BattleEventKind.StatusApplied || item.Kind == BattleEventKind.StatusRemoved ||
                    item.Kind == BattleEventKind.StatusesChanged)
                    RefreshCardAvailability(item.TargetId);
                switch (item.Kind)
                {
                    case BattleEventKind.DamageApplied:
                        hitCount = Mathf.Max(hitCount, item.HitCount);
                        visible = true;
                        FloatText(item.TargetId, item.WasEndured ? "Patience" :
                            (item.WasCritical ? "CRITICAL\n" : item.WasBlocked ? "BLOCK\n" : "") + item.Amount.ToString("N0"),
                            item.WasEndured ? "status" : item.WasCritical ? "critical" : item.WasBlocked ? "blocked" : "damage",
                            hitCount, 1f, ResolveEventAffinity(item));
                        PresentShieldDamage(item, hitCount);
                        UpdateDamageTotal(item.SourceId, (long)item.Amount);
                        impacts.Add(item);
                        break;
                    case BattleEventKind.HealApplied:
                        visible = true;
                        FloatText(item.TargetId, "+" + item.Amount.ToString("N0"), "heal");
                        _stage.Pulse(item.TargetId, new Color(.3f, 1f, .65f));
                        break;
                    case BattleEventKind.RecoveryBlocked:
                        visible = true;
                        FloatText(item.TargetId, RecoveryBlockedMessage, "status");
                        break;
                    case BattleEventKind.PassiveTriggered:
                        if (ShouldPresentPassiveActivation(item))
                        {
                            visible = true;
                            FloatText(item.SourceId, "Active Unique", "passive");
                            _stage.Pulse(item.SourceId, new Color(1f, .85f, .25f));
                        }
                        break;
                    case BattleEventKind.StatusApplied:
                        visible = true;
                        PlayStatusAnimation(item);
                        if (item.StatusOutcome == StatusApplyOutcome.Refreshed)
                            PlayStatusRefreshFeedback(item, true);
                        FloatText(item.TargetId, FeedbackName(item), "status");
                        _stage.Pulse(item.TargetId, new Color(.85f, .4f, 1f));
                        break;
                    case BattleEventKind.StatusesChanged:
                        break;
                    case BattleEventKind.StatusRemoved:
                        visible = true;
                        if (item.WasStanceCancelled) impacts.Add(item);
                        else PlayStatusAnimation(item);
                        if (!string.IsNullOrEmpty(item.Message) && !IsExpiredStatusMessage(item.Message))
                        {
                            FloatText(item.TargetId, item.Message, "status");
                        }
                        break;
                    case BattleEventKind.AttackEvaded:
                        visible = true;
                        FloatText(item.TargetId, "Evade", "evade");
                        break;
                    case BattleEventKind.StatusImmuned:
                        visible = true;
                        FloatText(item.TargetId, "Immunity", "immunity");
                        _stage.Pulse(item.TargetId, new Color(1f, 0.85f, 0.4f));
                        break;
                    case BattleEventKind.StatusWeaker:
                        visible = true;
                        PlayStatusRefreshFeedback(item, false);
                        break;
                }
            }
            if (impacts.Count > 0) yield return _stage.ReactMultiple(impacts, HitImpactScale(hitCount));
            else if (visible) _stage.PlayCue(3);
            if (visible && !_hitWindow && eventDelay > 0) yield return new WaitForSecondsRealtime(eventDelay);
        }

        private static float HitImpactScale(int hitCount) => hitCount > 3
            ? Mathf.Clamp(.72f / hitCount / .28f, .1f, 1f) : 1f;

        private IEnumerator PresentEvent(BattleEvent item)
        {
            switch (item.Kind)
            {
                case BattleEventKind.BattleStarted: yield break;
                case BattleEventKind.HitStarted:
                    _hitWindow = true;
                    yield return _stage.DamageHit(item.HitIndex, item.TargetIds);
                    yield break;
                case BattleEventKind.ActionTiming:
                    _hitWindow = item.Timing == CardEffectTiming.Damaging;
                    if (item.Timing == CardEffectTiming.BeforeAction)
                        yield return FinishActionVisual();
                    else if (item.Timing == CardEffectTiming.AfterDamage)
                    {
                        yield return _stage.WaitForLastHit();
                        yield return _stage.WaitForTargetReactions();
                        yield return _stage.WaitForDefeatAnimations();
                    }
                    else if (item.Timing == CardEffectTiming.AfterAction)
                        yield return FinishActionVisual();
                    // Damaging/AfterDamage are barriers: do not batch across a hit boundary.
                    yield break;
                case BattleEventKind.ActionCompleted:
                    yield return FinishActionVisual();
                    if (_isExecutionOverlayVisible) yield return AnimateExecutionExit();
                    if (!_hasQueuedCardExecution && _targetReticle != null) _targetReticle.SetVisible(false);
                    yield return _stage.CompleteExecution(_hasQueuedCardExecution, _displayState.ActingSide);
                    yield break;
                case BattleEventKind.StatusResolutionStarted:
                    yield return FinishActionVisual();
                    if (_isExecutionOverlayVisible) yield return AnimateExecutionExit();
                    _resolvingTeamStatuses = true;
                    SetPhase(BattlePresentationPhase.ResolvingStatus);
                    if (_targetReticle != null) _targetReticle.SetVisible(false);
                    // End-turn effects settle with their owner still on screen. The incoming
                    // side's camera/banner already switched at TurnEnded/TurnStarted.
                    if (item.Message != null && item.Message.StartsWith("Turn-end"))
                        yield return ReturnToPlanningAndRestore(_displayState.ActingSide, .35f);
                    yield break;
                case BattleEventKind.StatusResolutionEnded:
                    yield return WaitForFeedback();
                    _resolvingTeamStatuses = false;
                    yield break;
                case BattleEventKind.PassiveStatsChanged:
                case BattleEventKind.PowerGaugeChanged:
                case BattleEventKind.StatusesChanged:
                    BattlePlaybackState.Apply(_displayState, item);
                    UpdateDisplayedHud(item.TargetId);
                    RefreshCardAvailability(item.TargetId);
                    yield break;
                case BattleEventKind.StatusApplied:
                    BattlePlaybackState.Apply(_displayState, item);
                    UpdateDisplayedHud(item.TargetId);
                    RefreshCardAvailability(item.TargetId);
                    var statusName = FeedbackName(item);
                    if (item.ShieldChanged && item.ShieldAfter > 0) _stage.ShieldActivated(item.TargetId);
                    else _stage.Pulse(item.TargetId, new Color(.85f, .4f, 1f));
                    if (item.StatusOutcome == StatusApplyOutcome.Refreshed)
                        PlayStatusRefreshFeedback(item, true);
                    FloatText(item.TargetId, statusName, "status");
                    yield break;
                case BattleEventKind.StatusRemoved:
                    BattlePlaybackState.Apply(_displayState, item);
                    UpdateDisplayedHud(item.TargetId);
                    RefreshCardAvailability(item.TargetId);
                    if (!string.IsNullOrEmpty(item.Message) && !IsExpiredStatusMessage(item.Message))
                    {
                        FloatText(item.TargetId, item.Message, "status");
                    }
                    if (item.WasStanceCancelled) yield return _stage.React(item);
                    yield break;
                case BattleEventKind.AttackEvaded:
                    FloatText(item.TargetId, "Evade", "evade");
                    yield break;
                case BattleEventKind.StatusImmuned:
                    FloatText(item.TargetId, "Immunity", "immunity");
                    _stage.Pulse(item.TargetId, new Color(1f, 0.85f, 0.4f));
                    yield break;
                case BattleEventKind.StatusWeaker:
                    PlayStatusRefreshFeedback(item, false);
                    yield break;
                case BattleEventKind.CardRemoved:
                    var removedOwner = _displayState.Player.FindFighter(item.SourceId) ?? _displayState.Opponent.FindFighter(item.SourceId);
                    var removedSide = removedOwner?.Side ?? TeamSide.Player;
                    var removedViews = HandCardViews(removedSide);
                    var handPositions = CaptureHandPositions(removedSide);
                    if (removedViews.TryGetValue(item.CardId ?? string.Empty, out var removedView) && removedView != null)
                    {
                        removedView.BringToFront();
                        _stage.PlayCue(2);
                        yield return BattleStagePresenter.Tween(.2f, t =>
                        {
                            removedView.style.translate = new Translate(0, -26 * t);
                            removedView.style.scale = new Scale(Vector3.one * (1 - .28f * t));
                            removedView.style.opacity = 1 - t;
                        });
                    }
                    BattlePlaybackState.Apply(_displayState, item);
                    RenderHand(removedSide);
                    yield return ReflowHand(handPositions, null, removedSide);
                    SyncUltimateReady();
                    yield break;
                case BattleEventKind.PassiveTriggered:
                    if (ShouldPresentPassiveActivation(item))
                    {
                        FloatText(item.SourceId, "Active Unique", "passive");
                        _stage.Pulse(item.SourceId, new Color(1f, .85f, .25f));
                        _stage.PlayCue(3);
                        yield return new WaitForSecondsRealtime(.35f);
                    }
                    yield break;
                case BattleEventKind.TurnPlanCommitted:
                    yield return PresentCommittedPlan(item);
                    yield break;
                case BattleEventKind.TurnStarted:
                    yield return FinishActionVisual();
                    BattlePlaybackState.Apply(_displayState, item);
                    _executionIndex = -1;
                    ClearExecution();
                    ClearEnemyPlan();
                    if (_turnChangePresented) _turnChangePresented = false;
                    else
                    {
                        SetPhase(BattlePresentationPhase.TurnSwitch);
                        yield return PresentTurnChange(_displayState.ActingSide);
                        if (_displayState.ActingSide == TeamSide.Opponent)
                            yield return new WaitForSecondsRealtime(0.75f);
                    }
                    yield return _stage.PresentDisabledTurn(_displayState, _displayState.ActingSide);
                    yield break;
                case BattleEventKind.TurnEnded:
                    _suppressDraftCardPlayback = false;
                    _executionIndex = -1;
                    ClearExecution();
                    ClearEnemyPlan();
                    var incomingSide = _displayState.ActingSide == TeamSide.Player ? TeamSide.Opponent : TeamSide.Player;
                    _turnChangePresented = true;
                    SetPhase(BattlePresentationPhase.TurnSwitch);
                    yield return RecoverExecutionActorVisuals();
                    yield return PresentTurnChange(incomingSide);
                    if (incomingSide == TeamSide.Opponent)
                        yield return new WaitForSecondsRealtime(0.75f);
                    yield break;
                case BattleEventKind.CardDrawn:
                case BattleEventKind.CardsMerged:
                case BattleEventKind.CardMoved:
                    if (_hitWindow && item.Kind == BattleEventKind.CardsMerged)
                    {
                        var mergeOwner = _displayState.Player.FindFighter(item.SourceId) ?? _displayState.Opponent.FindFighter(item.SourceId);
                        BattlePlaybackState.Apply(_displayState, item);
                        if (mergeOwner != null) RenderHand(mergeOwner.Side);
                        yield break;
                    }
                    if (item.Kind == BattleEventKind.CardMoved) AdvanceExecution(item);
                    if (_suppressDraftCardPlayback && item.Kind != BattleEventKind.CardDrawn &&
                        _displayState.Player.FindFighter(item.SourceId) != null)
                    {
                        UpdateDisplayedGauge(item);
                        if (item.Kind == BattleEventKind.CardMoved) yield return new WaitForSecondsRealtime(.25f);
                        yield break;
                    }
                    yield return AnimateCardEvent(item);
                    yield break;
                case BattleEventKind.CounterStarted:
                    ResetDamageTotal(true);
                    yield return RecoverExecutionActorVisuals();
                    if (_isExecutionOverlayVisible) yield return AnimateExecutionExit();
                    var counterUser = _displayState.Player.FindFighter(item.SourceId) ?? _displayState.Opponent.FindFighter(item.SourceId);
                    _counterCue?.RemoveFromHierarchy();
                    _counterCue = new VisualElement();
                    _counterCue.AddToClassList("counter-cue");
                    _counterCue.Add(new Image { sprite = LoadCharacter(counterUser.Definition.Id)?.FighterIcon });
                    _counterCue.Add(new Label("COUNTER · " + counterUser.Definition.DisplayName));
                    _effectsLayer.Add(_counterCue);
                    _stage.PlayCue(3);
                    FloatText(item.SourceId, "COUNTER", "passive");
                    yield return new WaitForSecondsRealtime(.35f);
                    HideExecutionActorVisuals(item.SourceId);
                    var counterCategory = CardRules.GetEffectCategory(item.Card);
                    yield return _stage.BeginExecution(item.SourceId, item.TargetId, 1, false, counterCategory,
                        item.TargetIds, item.Card?.TargetScope == EffectTargetScope.AllEnemies || item.TargetIds.Count > 1,
                        item.Card?.TargetScope == EffectTargetScope.Self);
                    if (item.HitCount > 0)
                        yield return _stage.BeginDamageAttack(item.SourceId, item.TargetIds, item.HitCount, item.AttackRange,
                            item.Card?.TargetScope == EffectTargetScope.AllEnemies || item.TargetIds.Count > 1, _actionFramingReaction);
                    else if (counterCategory == CardCategory.Attack || counterCategory == CardCategory.AttackDebuff)
                    {
                        if (item.Card.TargetScope == EffectTargetScope.AllEnemies || item.TargetIds.Count > 1)
                            yield return _stage.AttackArea(item.SourceId, item.TargetIds, counterCategory == CardCategory.AttackDebuff);
                        else yield return _stage.Attack(item.SourceId, item.TargetId, counterCategory == CardCategory.AttackDebuff);
                    }
                    else yield return _stage.SupportAction(item.SourceId, item.TargetIds, counterCategory);
                    yield break;
                case BattleEventKind.CounterEnded:
                    _damageTotalActionOpen = false;
                    yield return WaitForFeedback();
                    yield return _stage.WaitForActionEnd(item.SourceId);
                    yield return RecoverExecutionActorVisuals();
                    yield return _stage.WaitForActionEnd(item.TargetId);
                    _counterCue?.RemoveFromHierarchy(); _counterCue = null;
                    yield return _stage.CompleteExecution(_hasQueuedCardExecution, _displayState.ActingSide);
                    yield break;
                case BattleEventKind.CardPlayed:
                    ResetDamageTotal(true);
                    yield return WaitForFeedback();
                    yield return RecoverExecutionActorVisuals();
                    SetPhase(BattlePresentationPhase.CardExecution);
                    if (_isExecutionOverlayVisible) yield return AnimateExecutionExit();
                    AdvanceExecution(item);
                    var playedOwner = _displayState.Player.FindFighter(item.SourceId) ?? _displayState.Opponent.FindFighter(item.SourceId);
                    if (playedOwner?.Side == TeamSide.Opponent)
                        yield return AnimateEnemyCardUse(item);
                    else
                    {
                        BattlePlaybackState.Apply(_displayState, item);
                        RenderHand();
                    }
                    UpdateDisplayedHud(item.SourceId);
                    if (_executionOverlay != null)
                    {
                        yield return AnimateExecutionEnter();
                    }
                    if (item.Card?.Kind == CardKind.Ultimate) yield return UltimateCutIn(item);
                    HideExecutionActorVisuals(item.SourceId);
                    var targetIds = item.TargetIds != null && item.TargetIds.Count > 0
                        ? item.TargetIds : new List<string> { item.TargetId };
                    var effectCategory = CardRules.GetEffectCategory(item.Card);
                    yield return _stage.BeginExecution(item.SourceId, item.TargetId, item.Card?.Rank ?? 1,
                        item.Card?.Kind == CardKind.Ultimate, effectCategory, targetIds,
                        item.Card?.TargetScope == EffectTargetScope.AllEnemies || targetIds.Count > 1,
                        item.Card?.TargetScope == EffectTargetScope.Self);
                    if (targetIds.Count == 1 && _fighterViews.TryGetValue(item.TargetId, out var target) && _targetReticle != null)
                    { _targetReticle.AttachTo(target.transform); _targetReticle.HighlightAttack(); }
                    else if (_targetReticle != null) _targetReticle.SetVisible(false);
                    if (item.HitCount > 0)
                        yield return _stage.BeginDamageAttack(item.SourceId, targetIds, item.HitCount, item.AttackRange,
                            item.Card?.TargetScope == EffectTargetScope.AllEnemies || targetIds.Count > 1, _actionFramingReaction);
                    else if (item.Card != null && (effectCategory == CardCategory.Recovery ||
                        effectCategory == CardCategory.Debuff || effectCategory == CardCategory.Buff ||
                        effectCategory == CardCategory.Stance))
                    {
                        yield return _stage.SupportAction(item.SourceId, targetIds, effectCategory);

                    }
                    else if (item.Card?.TargetScope == EffectTargetScope.AllEnemies || targetIds.Count > 1) yield return _stage.AttackArea(item.SourceId, targetIds,
                        item.Card != null && effectCategory == CardCategory.AttackDebuff);
                    else yield return _stage.Attack(item.SourceId, item.TargetId,
                        item.Card != null && effectCategory == CardCategory.AttackDebuff);
                    yield break;
                case BattleEventKind.DamageApplied:
                    BattlePlaybackState.Apply(_displayState, item);
                    UpdateDisplayedHud(item.TargetId);
                    var textLifetimeScale = item.HitCount > 3
                        ? Mathf.Clamp((1.2f / Mathf.Sqrt(item.HitCount)) / 1.45f, .65f, 1f) : 1f;
                    if (item.WasEndured)
                    {
                        FloatText(item.TargetId, "Patience", "status", item.HitCount, textLifetimeScale);
                    }
                    else
                    {
                        var caption = (item.WasCritical ? "CRITICAL\n" : item.WasBlocked ? "BLOCK\n" : "") + item.Amount.ToString("N0");
                        FloatText(item.TargetId, caption, item.WasCritical ? "critical" : item.WasBlocked ? "blocked" : "damage", item.HitCount, textLifetimeScale, ResolveEventAffinity(item));
                        UpdateDamageTotal(item.SourceId, (long)item.Amount);
                    }
                    PresentShieldDamage(item, item.HitCount, textLifetimeScale);
                    yield return _stage.React(item, HitImpactScale(item.HitCount));
                    if (eventDelay > 0) yield return new WaitForSecondsRealtime(eventDelay);
                    if (_isExecutionOverlayVisible)
                    {
                        yield return new WaitForSecondsRealtime(0.12f);
                        yield return AnimateExecutionExit();
                    }
                    yield break;
                case BattleEventKind.HealApplied:
                    BattlePlaybackState.Apply(_displayState, item);
                    UpdateDisplayedHud(item.TargetId);
                    FloatText(item.TargetId, "+" + item.Amount.ToString("N0"), "heal");
                    if (eventDelay > 0) yield return new WaitForSecondsRealtime(eventDelay);
                    yield break;
                case BattleEventKind.RecoveryBlocked:
                    FloatText(item.TargetId, RecoveryBlockedMessage, "status");
                    yield break;
                case BattleEventKind.CardRankChanged:
                    var rankOwner = _displayState.Player.FindFighter(item.SourceId) ?? _displayState.Opponent.FindFighter(item.SourceId);
                    var rankSide = rankOwner?.Side ?? TeamSide.Player;
                    var rankViews = HandCardViews(rankSide);
                    var rankPositions = CaptureHandPositions(rankSide);
                    rankViews.TryGetValue(item.CardId ?? string.Empty, out var rankedView);
                    BattlePlaybackState.Apply(_displayState, item);
                    RenderHand(rankSide);
                    var rankedUp = string.IsNullOrEmpty(item.Message) || !item.Message.Contains("decreased");
                    if (rankedView != null)
                    {
                        rankedView.AddToClassList(rankedUp ? "rank-up" : "rank-down");
                        yield return MergeFlash(rankedView, item.Amount, rankedUp);
                        rankedView.RemoveFromClassList(rankedUp ? "rank-up" : "rank-down");
                    }
                    yield return ReflowHand(rankPositions, null, rankSide);
                    FloatText(item.TargetId, rankedUp ? "RANK UP" : "RANK DOWN", "status");
                    yield break;
                case BattleEventKind.FighterDefeated:
                    if (_fighterBillboards.TryGetValue(item.SourceId, out var hud) && hud != null) hud.gameObject.SetActive(false);
                    if (_targetReticle != null) _targetReticle.SetVisible(false);
                    FloatText(item.SourceId, "DEFEATED", "status");
                    _stage.BeginDefeat(item.SourceId);
                    BattlePlaybackState.Apply(_displayState, item);
                    SyncUltimateReady();
                    if (_preferredTargetId == item.SourceId) _preferredTargetId = null;
                    RenderHand();
                    RenderEnemyHand();
                    yield break;
                case BattleEventKind.ReserveEntered:
                    BattlePlaybackState.Apply(_displayState, item);
                    RefreshWorldViews();
                    yield return _stage.EnterFromSky(item.SourceId);
                    _stage.RememberPose(item.SourceId);
                    FloatText(item.SourceId, "SUB ENTER", "status");
                    yield return new WaitForSecondsRealtime(.2f);
                    yield break;
                case BattleEventKind.ActionFizzled:
                    AdvanceExecution(item);
                    if (_isExecutionOverlayVisible) yield return AnimateExecutionExit();
                    if (_executionLabel != null) _executionLabel.text = "SKIPPED · " + item.Message;
                    if (item.Message == "Disabled card discarded; gained 1 PG.")
                        FloatText(item.SourceId, "DISABLED · +1 PG", "status");
                    else if (item.Message == HealingCardBlockedMessage)
                        FloatText(item.SourceId, HealingCardBlockedMessage, "status");
                    else if (item.Message != RecoveryBlockedMessage)
                        FloatText(item.SourceId, "SKIPPED", "status");
                    _stage.FlinchIfDisabled(item.SourceId);
                    yield return new WaitForSecondsRealtime(.45f);
                    if (_executionEvents.Count > 0 && _executionIndex >= _executionEvents.Count)
                    {
                        if (_targetReticle != null) _targetReticle.SetVisible(false);
                        yield return ReturnToPlanningAndRestore(_displayState.ActingSide, .4f);
                    }
                    yield break;
                case BattleEventKind.BattleCompleted:
                    yield return FinishActionVisual();
                    yield return RecoverExecutionActorVisuals();
                    yield return new WaitForSecondsRealtime(.45f);
                    yield break;
            }
        }

        private void UpdateDamageTotal(string ownerId, long appliedDamage)
        {
            if (string.IsNullOrEmpty(ownerId) || appliedDamage <= 0) return;
            EnsureDamageTotal();
            if (_damageTotalOwnerId != ownerId) { _damageTotalOwnerId = ownerId; _damageTotalAmount = 0; }
            _damageTotalAmount = System.Math.Min(long.MaxValue - appliedDamage, _damageTotalAmount) + appliedDamage;
            _damageTotalLastImpact = Time.realtimeSinceStartup;
            if (_damageTotalLabel != null)
            {
                _damageTotalLabel.text = _damageTotalAmount.ToString("N0");
                // Preserve every digit for large totals; no ellipsis or numeric rounding.
                var width = Mathf.Clamp(_effectsLayer.resolvedStyle.width * .36f, 160f, 292f);
                _damageTotalElement.style.width = width;
                _damageTotalLabel.style.fontSize = Mathf.Min(64f, width / Mathf.Max(1f, _damageTotalLabel.text.Length * .62f));
                _damageTotalElement.style.display = DisplayStyle.Flex;
                _damageTotalElement.style.opacity = 1f;
            }
            if (damageTotalPanel != null) damageTotalPanel.SetActive(false);
            if (_damageTotalHideCoroutine != null) StopCoroutine(_damageTotalHideCoroutine);
            _damageTotalHideCoroutine = StartCoroutine(HideDamageTotalAfterDelay());
        }

        private IEnumerator HideDamageTotalAfterDelay()
        {
            yield return BattleStagePresenter.Tween(.2f, t =>
            {
                if (_damageTotalElement == null) return;
                _damageTotalElement.style.scale = new Scale(Vector3.one * (1f + .16f * Mathf.Sin(t * Mathf.PI)));
                _damageTotalElement.style.translate = new Translate(0, -5f * Mathf.Sin(t * Mathf.PI));
            });
            while (_damageTotalActionOpen || Time.realtimeSinceStartup < _damageTotalLastImpact + 1.35f) yield return null;
            yield return BattleStagePresenter.Tween(.22f, t =>
            {
                if (_damageTotalElement != null) _damageTotalElement.style.opacity = 1f - t;
            });
            if (_damageTotalElement != null) _damageTotalElement.style.display = DisplayStyle.None;
            _damageTotalHideCoroutine = null;
        }

        private IEnumerator PresentDamageBatch(IReadOnlyList<BattleEvent> batch)
        {
            var impacts = new List<BattleEvent>();
            foreach (var item in batch)
            {
                BattlePlaybackState.Apply(_displayState, item);
                UpdateDisplayedHud(item.TargetId);
                if (item.WasEndured)
                {
                    FloatText(item.TargetId, "Patience", "status");
                }
                else
                {
                    var caption = (item.WasCritical ? "CRITICAL\n" : item.WasBlocked ? "BLOCK\n" : "") + item.Amount.ToString("N0");
                    FloatText(item.TargetId, caption, item.WasCritical ? "critical" : item.WasBlocked ? "blocked" : "damage", 1, 1f, ResolveEventAffinity(item));
                    UpdateDamageTotal(item.SourceId, (long)item.Amount);
                }
                PresentShieldDamage(item);
                impacts.Add(item);
            }

            yield return _stage.ReactMultiple(impacts);
            if (eventDelay > 0) yield return new WaitForSecondsRealtime(eventDelay);
            if (_isExecutionOverlayVisible)
            {
                yield return new WaitForSecondsRealtime(0.12f);
                yield return AnimateExecutionExit();
            }
        }

        private IEnumerator PresentHealBatch(IReadOnlyList<BattleEvent> batch)
        {
            foreach (var item in batch)
            {
                BattlePlaybackState.Apply(_displayState, item);
                UpdateDisplayedHud(item.TargetId);
                FloatText(item.TargetId, "+" + item.Amount.ToString("N0"), "heal");
            }
            if (eventDelay > 0) yield return new WaitForSecondsRealtime(eventDelay);
        }

        private IEnumerator PresentPassiveBatch(IReadOnlyList<BattleEvent> batch)
        {
            foreach (var item in batch)
            {
                if (!ShouldPresentPassiveActivation(item)) continue;
                FloatText(item.SourceId, "Active Unique", "passive");
                _stage.Pulse(item.SourceId, new Color(1f, .85f, .25f));
            }
            _stage.PlayCue(3);
            yield return new WaitForSecondsRealtime(.35f);
        }

        private void UpdateDisplayedGauge(BattleEvent item)
        {
            var fighter = _displayState.Player.FindFighter(item.SourceId) ?? _displayState.Opponent.FindFighter(item.SourceId);
            if (fighter != null && item.PowerGaugeAfter >= 0) fighter.PowerGauge = item.PowerGaugeAfter;
            UpdateDisplayedHud(item.SourceId);
        }

        private void UpdateDisplayedHud(string id)
        {
            SyncUltimateReady();
            var fighter = _displayState.Player.FindFighter(id) ?? _displayState.Opponent.FindFighter(id);
            if (fighter == null) return;
            _stage.SyncActorVisualEffects(fighter);
            if (!_fighterBillboards.TryGetValue(id, out var hud) || hud == null) return;
            var maximumHealth = StatusSystem.GetEffectiveStats(fighter).MaxHealth;
            hud.SetCoreHealth(fighter.Health, maximumHealth);
            hud.SetShield(fighter.Shield, maximumHealth);
            var shownStatuses = fighter.Statuses.Instances.ConvertAll(status => status.Clone());
            foreach (var status in shownStatuses)
            {
                var parent = shownStatuses.Find(s => s.InstanceId == status.ParentInstanceId);
                if (parent == null) continue;
                status.RemainingDuration = parent.RemainingDuration;
                status.Recipe.DurationClock = parent.Recipe.DurationClock;
                status.Recipe.DefaultDuration = parent.Recipe.DefaultDuration;
            }
            hud.SetStatuses(shownStatuses);

            var isPlayerDraft = fighter.Side == TeamSide.Player && _draft != null && !_isPlayingEvents;
            if (isPlayerDraft)
            {
                var truePG = _snapshot.Player.FindFighter(id)?.PowerGauge ?? fighter.PowerGauge;
                var draftPG = fighter.PowerGauge;
                hud.SetPowerGauge(truePG, draftPG);
            }
            else
            {
                hud.SetPowerGauge(fighter.PowerGauge);
            }
        }

        private void PresentShieldDamage(BattleEvent item, int hitCount = 1, float durationScale = 1f)
        {
            if (item == null || item.ShieldLost <= 0) return;
            var broken = item.ShieldAfter <= 0;
            var message = broken
                ? "SHIELD BREAK\n−" + item.ShieldLost.ToString("N0")
                : "SHIELD −" + item.ShieldLost.ToString("N0");
            FloatText(item.TargetId, message, "status", hitCount, durationScale);
            _stage?.ShieldImpact(item.TargetId, broken);
        }

        private static bool IsExecutionEvent(BattleEvent item) => item.Kind == BattleEventKind.CardPlayed ||
            item.Kind == BattleEventKind.CardMoved || item.Kind == BattleEventKind.ActionFizzled;

        private void PrepareExecution(IReadOnlyList<BattleEvent> events, int start)
        {
            _executionEvents.Clear();
            for (var i = start; i < events.Count; i++)
            {
                if (events[i].Kind == BattleEventKind.TurnEnded || events[i].Kind == BattleEventKind.TurnStarted) break;
                if (IsExecutionEvent(events[i])) _executionEvents.Add(events[i]);
            }
            _executionIndex = 0;
            ClearExecution();
            var firstOwner = _executionEvents.Count == 0 ? null :
                _displayState.Player.FindFighter(_executionEvents[0].SourceId);
            if (firstOwner != null) PopulatePlayerExecutionSlots();
        }

        private void AdvanceExecution(BattleEvent item)
        {
            AdvanceEnemyPlan(item);
            for (var i = 0; i < _actionSlots.Count; i++)
            {
                var order = _actionSlots.Count - 1 - i;
                _actionSlots[i].EnableInClassList("executing", order == _executionIndex);
                _actionSlots[i].style.opacity = order < _executionIndex ? .25f : 1;
            }
            _executionIndex++;

            if (item.Kind == BattleEventKind.CardPlayed && item.Card != null)
            {
                var owner = _displayState.Player.FindFighter(item.SourceId) ?? _displayState.Opponent.FindFighter(item.SourceId);
                DisplayCardExecution(item.Card, owner);
            }
            else if (item.Kind == BattleEventKind.CardMoved)
            {
                DismissExecutionOverlayImmediate();
            }

            if (_executionLabel != null)
            {
                var owner = _displayState.Player.FindFighter(item.SourceId) ?? _displayState.Opponent.FindFighter(item.SourceId);
                var target = _displayState.Player.FindFighter(item.TargetId) ?? _displayState.Opponent.FindFighter(item.TargetId);
                var name = item.Kind == BattleEventKind.CardMoved ? "MOVE" : GetTooltip(item.Card, owner);
                _executionLabel.text = _executionIndex + " / " + _executionEvents.Count + "   " + name +
                    (target == null ? "" : "  →  " + ShortId(target.Definition.Id));
            }
        }

        private void DisplayCardExecution(CardState card, FighterState owner)
        {
            if (card == null || owner == null || _executionOverlay == null) return;

            var character = LoadCharacter(owner.Definition?.Id);
            var icon = GetCardIcon(card, owner);
            if (_execCardArtwork != null)
            {
                _execCardArtwork.sprite = icon;
                _execCardArtwork.tintColor = Color.white;
                _execCardArtwork.scaleMode = ScaleMode.ScaleAndCrop;
            }

            if (_execCardFace != null)
            {
                _execCardFace.style.backgroundColor = icon == null ? SlotColor(owner) : new Color(0.09f, 0.10f, 0.12f, 1f);
            }

            if (_execCardRankFrame != null)
            {
                _execCardRankFrame.image = card.Kind == CardKind.Ultimate ? rankThreeFrameTexture : RankFrameTexture(card.Rank);
                _execCardRankFrame.scaleMode = ScaleMode.StretchToFill;
                _execCardRankFrame.style.display = _execCardRankFrame.image == null ? DisplayStyle.None : DisplayStyle.Flex;
            }

            var skillType = GetCardSkillType(card, owner);
            var skillTypeSprite = LoadSkillTypeSprite(skillType);
            if (_execCardSkillType != null)
            {
                _execCardSkillType.sprite = skillTypeSprite;
                _execCardSkillType.style.display = skillTypeSprite != null ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_execTypeIcon != null)
            {
                _execTypeIcon.sprite = skillTypeSprite;
                _execTypeIcon.style.display = skillTypeSprite != null ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (card.Kind == CardKind.Ultimate)
            {
                if (_execCardSkillSlot != null) _execCardSkillSlot.text = "ULT";
                if (_execCardHoloGlow != null) _execCardHoloGlow.style.display = DisplayStyle.Flex;
                for (var i = 0; i < _execCardStars.Count; i++)
                {
                    _execCardStars[i].EnableInClassList("active-star", true);
                }
            }
            else
            {
                if (_execCardHoloGlow != null) _execCardHoloGlow.style.display = DisplayStyle.None;
                var skillDef = owner.Definition?.Skills?.Find(item => item != null && item.Id == card.SkillId);
                if (_execCardSkillSlot != null) _execCardSkillSlot.text = skillDef != null && skillDef.Slot == 2 ? "2" : "1";
                var rank = Mathf.Clamp(card.Rank, 1, 3);
                for (var i = 0; i < _execCardStars.Count; i++)
                {
                    _execCardStars[i].EnableInClassList("active-star", i < rank);
                }
            }

            var title = GetCardTitle(card, owner, character);
            if (_execCardTitle != null) _execCardTitle.text = title;

            var (desc, keywords, statuses) = ExtractCardDetails(card, owner, character);
            if (_execCardDesc != null) _execCardDesc.text = desc;
            if (_execKeywordsDesc != null)
            {
                _execKeywordsDesc.text = keywords;
                _execKeywordsDesc.style.display = string.IsNullOrEmpty(keywords) ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (_execStatusGrid != null)
            {
                _execStatusGrid.Clear();
                foreach (var st in statuses)
                {
                    _execStatusGrid.Add(CreateStatusBadge(st));
                }
            }
        }

        private IEnumerator AnimateExecutionEnter()
        {
            if (_executionOverlay == null) yield break;
            _isExecutionOverlayVisible = true;
            _executionOverlay.style.display = DisplayStyle.Flex;
            _executionOverlay.style.opacity = 0f;
            _executionOverlay.style.translate = new Translate(-36f, 0f);
            if (_execCardContainer != null)
                _execCardContainer.style.scale = new Scale(Vector3.one * 0.9f);

            yield return BattleStagePresenter.Tween(0.22f, t =>
            {
                if (_executionOverlay == null) return;
                var ease = Mathf.Sin(t * Mathf.PI * 0.5f);
                _executionOverlay.style.opacity = Mathf.Clamp01(t * 1.5f);
                _executionOverlay.style.translate = new Translate(Mathf.Lerp(-36f, 0f, ease), 0f);
                if (_execCardContainer != null)
                    _execCardContainer.style.scale = new Scale(Vector3.one * Mathf.Lerp(0.9f, 1f, ease));
            });
        }

        private IEnumerator AnimateExecutionExit()
        {
            if (_executionOverlay == null || !_isExecutionOverlayVisible) yield break;
            _isExecutionOverlayVisible = false;

            yield return BattleStagePresenter.Tween(0.18f, t =>
            {
                if (_executionOverlay == null) return;
                var ease = t * t;
                _executionOverlay.style.opacity = 1f - t;
                _executionOverlay.style.translate = new Translate(Mathf.Lerp(0f, 30f, ease), 0f);
            });

            if (_executionOverlay != null)
            {
                _executionOverlay.style.display = DisplayStyle.None;
                _executionOverlay.style.translate = new Translate(0f, 0f);
            }
        }

        private void DismissExecutionOverlayImmediate()
        {
            _isExecutionOverlayVisible = false;
            if (_executionOverlay != null)
            {
                _executionOverlay.style.display = DisplayStyle.None;
                _executionOverlay.style.opacity = 0f;
                _executionOverlay.style.translate = new Translate(0f, 0f);
            }
        }

        private void ClearExecution()
        {
            DismissExecutionOverlayImmediate();
            foreach (var content in _actionContents) { content.Clear(); content.style.opacity = 1; }
            foreach (var slot in _actionSlots)
            { slot.RemoveFromClassList("executing"); slot.style.opacity = 1; slot.style.display = DisplayStyle.None; }
            foreach (var marker in _actionMarkers) marker.style.display = DisplayStyle.Flex;
            foreach (var badge in _actionTargetBadges) badge.style.display = DisplayStyle.None;
            if (_executionLabel != null) _executionLabel.style.display = DisplayStyle.None;
        }

        private IEnumerator Banner(string text, bool enemy, float hold)
        {
            if (_turnBanner == null) yield break;
            var lines = (text ?? string.Empty).Split('\n');
            var turn = lines[0] == "YOUR TURN" || lines[0] == "ENEMY TURN";
            _turnBanner.EnableInClassList("turn-announcement", turn);
            _turnBanner.EnableInClassList("enemy", enemy);
            if (_turnBannerTitle != null) _turnBannerTitle.text = lines[0];
            if (_turnBannerSub != null)
            {
                _turnBannerSub.text = lines.Length > 1 ? lines[1] : "";
                _turnBannerSub.style.display = turn || lines.Length < 2 ? DisplayStyle.None : DisplayStyle.Flex;
            }
            _turnBanner.style.display = DisplayStyle.Flex;
            _turnBanner.BringToFront();
            var sign = enemy ? 1f : -1f;
            yield return BattleStagePresenter.Tween(.24f, t =>
            {
                _turnBanner.style.opacity = Mathf.Clamp01(t * 3f);
                _turnBanner.style.translate = new Translate(sign * 90f * (1 - t), 8f * (1 - t));
                _turnBanner.style.scale = new Scale(Vector3.one * Mathf.Lerp(1.15f, 1f, t));
            });
            yield return new WaitForSecondsRealtime(hold);
            yield return BattleStagePresenter.Tween(.2f, t =>
            {
                _turnBanner.style.opacity = 1f - t;
                _turnBanner.style.translate = new Translate(-sign * 45f * t, -4f * t);
            });
            _turnBanner.style.display = DisplayStyle.None;
            _turnBanner.style.scale = new Scale(Vector3.one);
            _turnBanner.style.translate = new Translate(0, 0);
        }

        private IEnumerator CardCaption(VisualElement card, string message)
        {
            if (_effectsLayer == null) yield break;
            var label = new Label(message) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("card-caption");
            _effectsLayer.Add(label);
            var position = _effectsLayer.WorldToLocal(card.worldBound.center);
            yield return BattleStagePresenter.Tween(.7f, t =>
            {
                label.style.left = position.x - 85;
                label.style.top = position.y - 85 - t * 35;
                label.style.opacity = 1 - t * t;
            });
            label.RemoveFromHierarchy();
        }

        private AttributeAffinity ResolveEventAffinity(BattleEvent item)
        {
            if (item == null) return AttributeAffinity.Neutral;
            if (item.Affinity != AttributeAffinity.Neutral) return item.Affinity;
            if (!string.IsNullOrEmpty(item.SourceId) && !string.IsNullOrEmpty(item.TargetId))
            {
                var src = _displayState?.Player?.FindFighter(item.SourceId) ?? _displayState?.Opponent?.FindFighter(item.SourceId);
                var tgt = _displayState?.Player?.FindFighter(item.TargetId) ?? _displayState?.Opponent?.FindFighter(item.TargetId);
                var srcAttr = src?.Definition?.AttributeId;
                if (string.IsNullOrEmpty(srcAttr) && src?.Definition != null)
                {
                    var co = LoadCharacter(src.Definition.Id);
                    if (co != null) srcAttr = "attribute." + co.FighterAttribute.ToString().ToLowerInvariant();
                }
                var tgtAttr = tgt?.Definition?.AttributeId;
                if (string.IsNullOrEmpty(tgtAttr) && tgt?.Definition != null)
                {
                    var co = LoadCharacter(tgt.Definition.Id);
                    if (co != null) tgtAttr = "attribute." + co.FighterAttribute.ToString().ToLowerInvariant();
                }
                return AttributeRules.GetAffinity(srcAttr, tgtAttr);
            }
            return AttributeAffinity.Neutral;
        }

        private void FloatText(string fighterId, string text, string kind, int hitCount = 1, float durationScale = 1f, AttributeAffinity affinity = AttributeAffinity.Neutral)
        {
            if (string.IsNullOrEmpty(fighterId) || string.IsNullOrEmpty(text)) return;
            EmitFloatText(fighterId, text, kind, hitCount, durationScale, affinity);
        }

        private void EmitFloatText(string fighterId, string text, string kind, int hitCount = 1, float durationScale = 1f, AttributeAffinity affinity = AttributeAffinity.Neutral)
        {
            if (string.IsNullOrEmpty(fighterId) || _effectsLayer == null || !_fighterViews.TryGetValue(fighterId, out var view) || view == null || !view.activeInHierarchy) return;
            _textLanes.TryGetValue(fighterId, out var lane);
            _textLanes[fighterId] = (lane + 1) % 4;
            if (hitCount > 3 && durationScale >= 1f)
                durationScale = Mathf.Clamp((1.2f / Mathf.Sqrt(hitCount)) / FctSettings.BaseDurationNumeric, FctSettings.MinMultiHitDurationScale, 1f);

            var isStatus = kind == "status" || kind == "passive";
            var world = view.transform.position + Vector3.up * (isStatus ? 2.6f : 2.25f);
            StartCoroutine(AnimateCombatText(world, text, kind, lane, durationScale, affinity));
        }

        private IEnumerator AnimateCombatText(Vector3 world, string text, string kind, int lane, float durationScale = 1f, AttributeAffinity affinity = AttributeAffinity.Neutral)
        {
            if (_fctPool.TotalCount == 0 && _effectsLayer != null)
                _fctPool.Initialize(_effectsLayer, 24, FctSettings);
            var item = _fctPool.Acquire(text, kind, affinity);
            var group = item.Group;
            var numeric = kind == "damage" || kind == "critical" || kind == "blocked" || kind == "heal";
            var settings = FctSettings;
            var duration = (numeric ? settings.BaseDurationNumeric : settings.BaseDurationStatus) * Mathf.Clamp(durationScale, settings.MinMultiHitDurationScale, 1f);
            _feedbackSettlesAt = Mathf.Max(_feedbackSettlesAt, Time.unscaledTime + Mathf.Min(duration, 0.75f));

            // Project once at impact: camera shake must not drag glyphs around the screen.
            var camera = Camera.main;
            if (camera == null || _effectsLayer.panel == null)
            {
                _fctPool.Release(item);
                yield break;
            }
            var viewport = camera.WorldToViewportPoint(world);
            if (viewport.z <= 0)
            {
                _fctPool.Release(item);
                yield break;
            }
            var width = _effectsLayer.resolvedStyle.width;
            var height = _effectsLayer.resolvedStyle.height;
            // The effects layer fills the camera viewport. Normalized coordinates also work
            // with DPI scaling and offscreen panels, unlike mixing Game View pixel sizes.
            var point = new Vector2(viewport.x * width, (1 - viewport.y) * height);
            var x = Mathf.Clamp(point.x - 140 + (lane - 1.5f) * 24f, 4, Mathf.Max(4, width - 284));
            var y = Mathf.Clamp(point.y - lane * 28f, 35, Mathf.Max(35, height - 210));
            yield return BattleStagePresenter.Tween(duration, t =>
            {
                var seconds = t * duration;
                var rise = numeric
                    ? (seconds < .12f ? settings.InitialRisePixels * (seconds / .12f) : settings.InitialRisePixels + settings.DriftRisePixels * Mathf.Pow(t, 0.7f))
                    : (settings.InitialRisePixels + settings.DriftRisePixels) * t;
                group.style.left = x;
                group.style.top = y - rise;
                float pop = 1f;
                if (numeric || kind == "evade" || kind == "immunity")
                {
                    var targetPop = kind == "critical" ? settings.PopScaleCritical : settings.PopScaleNormal;
                    if (seconds < .06f) pop = Mathf.Lerp(.75f, targetPop, seconds / .06f);
                    else if (seconds < .18f) pop = Mathf.Lerp(targetPop, 1f, (seconds - .06f) / .12f);
                }
                group.style.scale = new Scale(Vector3.one * pop);

                // Smooth graceful fade: solid for first 45%, then smoothly eases out to 0
                float opacity;
                var fadeStart = settings.FadeStartPercent;
                if (t < fadeStart)
                {
                    opacity = 1f;
                }
                else
                {
                    var fadeProgress = (t - fadeStart) / Mathf.Max(0.01f, 1f - fadeStart);
                    opacity = 1f - Mathf.SmoothStep(0f, 1f, fadeProgress);
                }
                group.style.opacity = opacity;
            }, false);
            _fctPool.Release(item);
        }

        private float CombatTextFontSize(string text, string kind) =>
            FloatingCombatTextPool.CalculateFontSize(text, kind, FctSettings);

        private void ShowBattleResult()
        {
            SetPhase(BattlePresentationPhase.Complete);
            _stage?.ClearAuras();
            _readySidebar?.Clear();
            _readyTags.Clear();
            SetControls(false);
            ClearExecution();
            if (_targetReticle != null) _targetReticle.SetVisible(false);
            if (_resultPanel != null) _resultPanel.style.display = DisplayStyle.Flex;
            if (_resultTitle != null) _resultTitle.text = _snapshot.IsDraw ? "DRAW" :
                _snapshot.Winner == TeamSide.Player ? "VICTORY" : "DEFEAT";
            if (_resultPanel == null) FinishBattle();
        }

        private void SkipAnimations()
        {
            if (_inspector != null || _session == null || _isAnimatingCards || PresentationPhase == BattlePresentationPhase.Complete || CanPlan) return;
            StopAllCoroutines();
            ClearHandDrag();
            ClearEnemyPlan();
            ClearCinematicHud();
            _stage?.SnapToPlanning();
            _stage?.ClearAuras();
            RestoreExecutionActorVisuals();
            _textLanes.Clear();
            _fctPool?.Clear();
            _statusAnimationActors.Clear();
            _feedbackSettlesAt = 0f;
            _resolvingTeamStatuses = false;
            _hitWindow = false;
            _turnChangePresented = false;
            _effectsLayer?.Clear();
            if (_ccComparison != null) _ccComparison.style.display = DisplayStyle.None;
            if (_turnBanner != null) _turnBanner.style.display = DisplayStyle.None;
            ClearExecution();
            _openingSequenceComplete = true;
            _isPlayingEvents = _commitPending = _suppressDraftCardPlayback = false;
            _executionIndex = -1;
            SetBattleHudVisible(true);
            SetPhase(BattlePresentationPhase.Planning);
            RefreshView();
            if (_snapshot.Phase == BattlePhase.Complete) ShowBattleResult();
        }

        private static GameObject CreatePlaceholder(FighterState fighter, Vector3 position, Quaternion rotation)
        {
            var root = new GameObject("Placeholder");
            root.transform.SetPositionAndRotation(position, rotation);
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Model";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.up;
            var renderer = body.GetComponent<Renderer>();
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", fighter.Side == TeamSide.Player ? new Color(.18f, .6f, .95f) : new Color(.9f, .25f, .22f));
            renderer.SetPropertyBlock(properties);
            return root;
        }

        private void OnDisable()
        {
            if (_inspector != null) Destroy(_inspector.gameObject);
            _inspector = null;
            _inspectHeldId = null;
            StopAllCoroutines();
            ClearHandDrag();
            ClearEnemyPlan();
            ClearCinematicHud();
            _fctPool?.Clear();
            _feedbackSettlesAt = 0f;
            _turnChangePresented = false;
            _hitWindow = _resolvingTeamStatuses = false;
            _statusAnimationActors.Clear();
            if (_stage != null) { _stage.Restore(); _stage.ClearAuras(); }
            _effectsLayer?.Clear();
        }
    }
}
