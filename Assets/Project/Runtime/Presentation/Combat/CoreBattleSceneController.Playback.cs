using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Combat
{
    public enum BattlePresentationPhase { ComparingCC, OpeningDeal, Planning, CardExecution, TurnSwitch, Complete }

    public sealed partial class CoreBattleSceneController
    {
        public BattlePresentationPhase PresentationPhase { get; private set; }
        private BattleState _displayState;
        private BattleStagePresenter _stage;
        private bool _hudRevealed;
        private bool _isAnimatingCards;
        private bool _commitPending;
        private bool _suppressDraftCardPlayback;
        private readonly List<CardState> _draftCards = new List<CardState>();
        private readonly List<BattleEvent> _executionEvents = new List<BattleEvent>();
        private int _executionIndex = -1;
        private VisualElement _tray;
        private VisualElement _effectsLayer;
        private VisualElement _turnBanner;
        private Label _turnBannerTitle;
        private Label _turnBannerSub;
        private Label _phaseLabel;
        private Label _phaseTurnLabel;
        private Label _executionLabel;
        private VisualElement _resultPanel;
        private Label _resultTitle;
        private Button _commitButton;
        private VisualElement _ccComparison;
        private Button _skipButton;
        private readonly Dictionary<string, int> _textLanes = new Dictionary<string, int>();
        private bool CanPlan => _openingSequenceComplete && !_isPlayingEvents && !_isAnimatingCards && !_commitPending &&
            _snapshot != null && _snapshot.Phase == BattlePhase.Planning && _snapshot.ActingSide == TeamSide.Player &&
            PresentationPhase == BattlePresentationPhase.Planning;

        private void BindPresentationHud(VisualElement root)
        {
            _tray = root.Q<VisualElement>("battle-card-tray");
            _effectsLayer = root.Q<VisualElement>("battle-effects");
            _turnBanner = root.Q<VisualElement>("turn-banner");
            _turnBannerTitle = root.Q<Label>("turn-banner-title");
            _turnBannerSub = root.Q<Label>("turn-banner-sub");
            _phaseLabel = root.Q<Label>("battle-phase");
            _phaseTurnLabel = root.Q<Label>("battle-phase-turn");
            _executionLabel = root.Q<Label>("execution-label");
            _resultPanel = root.Q<VisualElement>("battle-result");
            _resultTitle = root.Q<Label>("result-title");
            _commitButton = root.Q<Button>("commit-button");
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
                BattlePresentationPhase.CardExecution => "CARD EXECUTION",
                BattlePresentationPhase.TurnSwitch => "TURN CHANGE",
                _ => "BATTLE COMPLETE"
            };
        }

        private void SetBattleHudVisible(bool visible)
        {
            _hudRevealed = visible;
            if (_tray != null) _tray.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
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
            var owner = _displayState.Player.FindFighter(item.SourceId);
            if (owner == null)
            {
                BattlePlaybackState.Apply(_displayState, item);
                yield break;
            }
            _handCardViews.TryGetValue(item.CardId ?? string.Empty, out var view);
            if (item.Kind == BattleEventKind.CardsMerged)
            {
                _handCardViews.TryGetValue(item.ConsumedCardId ?? string.Empty, out var consumed);
                if (view != null && consumed != null)
                {
                    var delta = view.worldBound.center - consumed.worldBound.center;
                    yield return BattleStagePresenter.Tween(.22f, t =>
                    {
                        consumed.style.translate = new Translate(delta.x * t, delta.y * t);
                        consumed.style.scale = new Scale(Vector3.one * (1 - .4f * t));
                        consumed.style.opacity = 1 - t;
                    });
                }
                BattlePlaybackState.Apply(_displayState, item);
                RenderHand();
                if (_handCardViews.TryGetValue(item.CardId, out view))
                {
                    view.AddToClassList("rank-up");
                    StartCoroutine(CardCaption(view, "RANK " + item.Amount));
                    yield return BattleStagePresenter.Tween(.32f, t =>
                        view.style.scale = new Scale(Vector3.one * (1 + Mathf.Sin(t * Mathf.PI) * .22f)));
                    view.RemoveFromClassList("rank-up");
                    view.style.scale = new Scale(Vector3.one);
                }
                UpdateDisplayedHud(item.SourceId);
                yield return new WaitForSecondsRealtime(.08f);
                yield break;
            }
            if (item.Kind == BattleEventKind.CardPlayed && drafting && view != null)
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
            RenderHand();
            if (item.Kind == BattleEventKind.CardDrawn && _handCardViews.TryGetValue(item.CardId, out view))
            {
                view.style.opacity = 1;
                yield return BattleStagePresenter.Tween(.24f, t =>
                {
                    view.style.translate = new Translate(-100 * (1 - t), 150 * (1 - t));
                    view.style.rotate = new Rotate(new Angle(-16 * (1 - t), AngleUnit.Degree));
                    view.style.scale = new Scale(Vector3.one * Mathf.Lerp(.55f, 1, t));
                });
                view.style.translate = new Translate(0, 0);
                view.style.rotate = new Rotate(new Angle(0, AngleUnit.Degree));
                view.style.scale = new Scale(Vector3.one);
                if (item.Card?.Kind == CardKind.Ultimate) StartCoroutine(CardCaption(view, "ULTIMATE READY"));
            }
            else if (item.Kind == BattleEventKind.CardMoved)
            {
                if (view != null)
                    yield return BattleStagePresenter.Tween(.24f, t =>
                        view.style.translate = new Translate(0, -22 * Mathf.Sin(t * Mathf.PI)));
                else yield return new WaitForSecondsRealtime(.24f);
            }
            else yield return new WaitForSecondsRealtime(.12f);
            UpdateDisplayedHud(item.SourceId);
        }

        private IEnumerator PresentEvent(BattleEvent item)
        {
            switch (item.Kind)
            {
                case BattleEventKind.BattleStarted: yield break;
                case BattleEventKind.PassiveStatsChanged:
                case BattleEventKind.PowerGaugeChanged:
                    BattlePlaybackState.Apply(_displayState, item);
                    UpdateDisplayedHud(item.TargetId);
                    yield break;
                case BattleEventKind.CardRemoved:
                    BattlePlaybackState.Apply(_displayState, item);
                    RenderHand();
                    yield break;
                case BattleEventKind.PassiveTriggered:
                    FloatText(item.SourceId, "Passive Trigger", "passive");
                    yield return new WaitForSecondsRealtime(.35f);
                    yield break;
                case BattleEventKind.TurnStarted:
                    BattlePlaybackState.Apply(_displayState, item);
                    _executionIndex = -1;
                    ClearExecution();
                    SetPhase(BattlePresentationPhase.TurnSwitch);
                    yield return _stage.Turn(_displayState.ActingSide, .65f);
                    yield return Banner(_displayState.ActingSide == TeamSide.Player ? "YOUR TURN" : "ENEMY TURN",
                        _displayState.ActingSide == TeamSide.Opponent, .7f);
                    yield break;
                case BattleEventKind.TurnEnded:
                    _suppressDraftCardPlayback = false;
                    _executionIndex = -1;
                    ClearExecution();
                    if (_targetReticle != null) _targetReticle.SetVisible(false);
                    yield return _stage.ReturnToPlanning(_displayState.ActingSide, .35f);
                    yield return new WaitForSecondsRealtime(.25f);
                    yield break;
                case BattleEventKind.CardDrawn:
                case BattleEventKind.CardsMerged:
                case BattleEventKind.CardMoved:
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
                case BattleEventKind.CardPlayed:
                    SetPhase(BattlePresentationPhase.CardExecution);
                    AdvanceExecution(item);
                    BattlePlaybackState.Apply(_displayState, item);
                    RenderHand();
                    UpdateDisplayedHud(item.SourceId);
                    if (item.PowerGaugeAfter == CardRules.UltimateGaugeCost) FloatText(item.SourceId, "ULTIMATE READY", "status");
                    var targetIds = item.TargetIds != null && item.TargetIds.Count > 0
                        ? item.TargetIds : new List<string> { item.TargetId };
                    if (targetIds.Count == 1 && _fighterViews.TryGetValue(item.TargetId, out var target) && _targetReticle != null)
                    { _targetReticle.AttachTo(target.transform); _targetReticle.HighlightAttack(); }
                    else if (_targetReticle != null) _targetReticle.SetVisible(false);
                    if (item.Card != null && (item.Card.Category == CardCategory.Recovery ||
                        item.Card.Category == CardCategory.Debuff || item.Card.Category == CardCategory.Buff ||
                        item.Card.Category == CardCategory.Stance))
                        yield return _stage.SupportAction(item.SourceId, targetIds, item.Card.Category);
                    else if (targetIds.Count > 1) yield return _stage.AttackArea(item.SourceId, targetIds,
                        item.Card != null && item.Card.Category == CardCategory.AttackDebuff);
                    else yield return _stage.Attack(item.SourceId, item.TargetId,
                        item.Card != null && item.Card.Category == CardCategory.AttackDebuff);
                    yield break;
                case BattleEventKind.DamageApplied:
                    BattlePlaybackState.Apply(_displayState, item);
                    UpdateDisplayedHud(item.TargetId);
                    if (item.WasEndured)
                    {
                        FloatText(item.TargetId, "Endurance", "status");
                    }
                    else
                    {
                        var caption = (item.WasCritical ? "CRITICAL\n" : item.WasBlocked ? "BLOCK\n" : "") + item.Amount.ToString("N0");
                        FloatText(item.TargetId, caption, item.WasCritical ? "critical" : item.WasBlocked ? "blocked" : "damage");
                        UpdateDamageTotal(item.SourceId, (long)item.Amount + item.ShieldLost);
                    }
                    if (item.ShieldLost > 0) FloatText(item.TargetId, "SHIELD −" + item.ShieldLost.ToString("N0"), "status");
                    yield return _stage.Impact(item.TargetId, item.WasCritical);
                    if (eventDelay > 0) yield return new WaitForSecondsRealtime(eventDelay);
                    if (_executionEvents.Count > 0 && _executionIndex >= _executionEvents.Count)
                    {
                        if (_targetReticle != null) _targetReticle.SetVisible(false);
                        yield return _stage.ReturnToPlanning(_displayState.ActingSide, .4f);
                    }
                    yield break;
                case BattleEventKind.HealApplied:
                    BattlePlaybackState.Apply(_displayState, item);
                    UpdateDisplayedHud(item.TargetId);
                    FloatText(item.TargetId, "+" + item.Amount.ToString("N0"), "heal");
                    if (eventDelay > 0) yield return new WaitForSecondsRealtime(eventDelay);
                    yield break;
                case BattleEventKind.CardRankChanged:
                    BattlePlaybackState.Apply(_displayState, item);
                    RenderHand();
                    FloatText(item.TargetId, "RANK UP", "status");
                    yield break;
                case BattleEventKind.FighterDefeated:
                    if (_fighterBillboards.TryGetValue(item.SourceId, out var hud) && hud != null) hud.gameObject.SetActive(false);
                    if (_targetReticle != null) _targetReticle.SetVisible(false);
                    FloatText(item.SourceId, "DEFEATED", "status");
                    yield return _stage.Defeat(item.SourceId);
                    BattlePlaybackState.Apply(_displayState, item);
                    if (_preferredTargetId == item.SourceId) _preferredTargetId = null;
                    RenderHand();
                    yield break;
                case BattleEventKind.ReserveEntered:
                    BattlePlaybackState.Apply(_displayState, item);
                    RefreshWorldViews();
                    _stage.RememberPose(item.SourceId);
                    FloatText(item.SourceId, "RESERVE IN", "status");
                    yield return new WaitForSecondsRealtime(.4f);
                    yield break;
                case BattleEventKind.ActionFizzled:
                    AdvanceExecution(item);
                    if (_executionLabel != null) _executionLabel.text = "SKIPPED · " + item.Message;
                    FloatText(item.SourceId, "SKIPPED", "status");
                    yield return new WaitForSecondsRealtime(.45f);
                    if (_executionEvents.Count > 0 && _executionIndex >= _executionEvents.Count)
                    {
                        if (_targetReticle != null) _targetReticle.SetVisible(false);
                        yield return _stage.ReturnToPlanning(_displayState.ActingSide, .4f);
                    }
                    yield break;
                case BattleEventKind.BattleCompleted:
                    yield return _stage.RecoverAttacker();
                    yield return new WaitForSecondsRealtime(.45f);
                    yield break;
            }
        }

        private void UpdateDamageTotal(string ownerId, long appliedDamage)
        {
            if (string.IsNullOrEmpty(ownerId) || appliedDamage <= 0 || damageTotalPanel == null || damageTotalValue == null) return;
            if (_damageTotalOwnerId != ownerId)
            {
                _damageTotalOwnerId = ownerId;
                _damageTotalAmount = 0;
            }

            _damageTotalAmount = System.Math.Min(long.MaxValue - appliedDamage, _damageTotalAmount) + appliedDamage;
            damageTotalValue.text = _damageTotalAmount.ToString("N0");
            damageTotalPanel.SetActive(true);
            if (_damageTotalHideCoroutine != null) StopCoroutine(_damageTotalHideCoroutine);
            _damageTotalHideCoroutine = StartCoroutine(HideDamageTotalAfterDelay());
        }

        private IEnumerator HideDamageTotalAfterDelay()
        {
            yield return new WaitForSecondsRealtime(1f);
            if (damageTotalPanel != null) damageTotalPanel.SetActive(false);
            _damageTotalHideCoroutine = null;
            _damageTotalOwnerId = null;
            _damageTotalAmount = 0;
        }

        private void UpdateDisplayedGauge(BattleEvent item)
        {
            var fighter = _displayState.Player.FindFighter(item.SourceId) ?? _displayState.Opponent.FindFighter(item.SourceId);
            if (fighter != null && item.PowerGaugeAfter >= 0) fighter.PowerGauge = item.PowerGaugeAfter;
            UpdateDisplayedHud(item.SourceId);
        }

        private void UpdateDisplayedHud(string id)
        {
            var fighter = _displayState.Player.FindFighter(id) ?? _displayState.Opponent.FindFighter(id);
            if (fighter == null || !_fighterBillboards.TryGetValue(id, out var hud) || hud == null) return;
            var maximumHealth = StatusSystem.GetEffectiveStats(fighter).MaxHealth;
            hud.SetCoreHealth(fighter.Health, maximumHealth);
            hud.SetShield(fighter.Shield, maximumHealth);
            hud.SetStatuses(fighter.Statuses?.Instances);

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
            for (var i = 0; i < _executionEvents.Count && i < _actionSlots.Count; i++)
            {
                var slot = _actionSlots.Count - 1 - i;
                var item = _executionEvents[i];
                _actionSlots[slot].style.display = DisplayStyle.Flex;
                _actionMarkers[slot].text = item.Kind == BattleEventKind.CardMoved ? "MOVE" : (i + 1).ToString();
                if (item.Card != null && item.Kind == BattleEventKind.CardPlayed)
                {
                    var owner = _displayState.Player.FindFighter(item.SourceId) ?? _displayState.Opponent.FindFighter(item.SourceId);
                    AddCardButton(_actionContents[slot], item.Card, owner, null, null, null);
                    _actionMarkers[slot].style.display = DisplayStyle.None;
                }
            }
        }

        private void AdvanceExecution(BattleEvent item)
        {
            for (var i = 0; i < _actionSlots.Count; i++)
            {
                var order = _actionSlots.Count - 1 - i;
                _actionSlots[i].EnableInClassList("executing", order == _executionIndex);
                _actionSlots[i].style.opacity = order < _executionIndex ? .25f : 1;
            }
            _executionIndex++;
            if (_executionLabel != null)
            {
                var owner = _displayState.Player.FindFighter(item.SourceId) ?? _displayState.Opponent.FindFighter(item.SourceId);
                var target = _displayState.Player.FindFighter(item.TargetId) ?? _displayState.Opponent.FindFighter(item.TargetId);
                var name = item.Kind == BattleEventKind.CardMoved ? "MOVE" : GetTooltip(item.Card, owner);
                _executionLabel.text = _executionIndex + " / " + _executionEvents.Count + "   " + name +
                    (target == null ? "" : "  →  " + ShortId(target.Definition.Id));
                _executionLabel.style.display = DisplayStyle.Flex;
            }
        }

        private void ClearExecution()
        {
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
            var mainTitle = lines[0];
            var subTitle = lines.Length > 1 ? lines[1] : (enemy ? "ENEMY PHASE" : "PLAYER PHASE");

            if (_turnBannerTitle != null) _turnBannerTitle.text = mainTitle;
            if (_turnBannerSub != null) _turnBannerSub.text = subTitle;

            _turnBanner.EnableInClassList("enemy", enemy);
            _turnBanner.style.display = DisplayStyle.Flex;
            _turnBanner.style.opacity = 0f;

            var startX = enemy ? 250f : -250f;
            var exitX = enemy ? -350f : 350f;

            // Fast cinematic speed-slash in
            yield return BattleStagePresenter.Tween(.22f, t =>
            {
                var ease = Mathf.Sin(t * Mathf.PI * 0.5f);
                _turnBanner.style.opacity = Mathf.Clamp01(t * 2.5f);
                _turnBanner.style.translate = new Translate(Mathf.Lerp(startX, 0f, ease), 0);
            });

            yield return new WaitForSecondsRealtime(hold);

            // Speed-slash exit to the opposite side
            yield return BattleStagePresenter.Tween(.18f, t =>
            {
                var ease = t * t;
                _turnBanner.style.opacity = 1f - t;
                _turnBanner.style.translate = new Translate(Mathf.Lerp(0f, exitX, ease), 0);
            });

            _turnBanner.style.display = DisplayStyle.None;
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

        private void FloatText(string fighterId, string text, string kind)
        {
            if (string.IsNullOrEmpty(fighterId) || _effectsLayer == null || !_fighterViews.TryGetValue(fighterId, out var view) || view == null || !view.activeInHierarchy) return;
            _textLanes.TryGetValue(fighterId, out var lane);
            _textLanes[fighterId] = (lane + 1) % 3;
            StartCoroutine(AnimateCombatText(view.transform.position + Vector3.up * 2.25f, text, kind, lane));
        }

        private IEnumerator AnimateCombatText(Vector3 world, string text, string kind, int lane)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("combat-text");
            label.AddToClassList(kind);
            label.style.fontSize = CombatTextFontSize(text, kind);
            _effectsLayer.Add(label);

            var isCrit = kind == "critical";
            var maxScale = isCrit ? 1.85f : 1.55f;
            var settleScale = isCrit ? 1.15f : 1.0f;

            yield return BattleStagePresenter.Tween(1.15f, t =>
            {
                var camera = Camera.main;
                if (camera == null || _effectsLayer.panel == null) return;
                var screen = camera.WorldToScreenPoint(world);
                label.style.visibility = screen.z > 0 ? Visibility.Visible : Visibility.Hidden;
                var panel = RuntimePanelUtils.ScreenToPanel(_effectsLayer.panel, new Vector2(screen.x, Screen.height - screen.y));
                var point = _effectsLayer.WorldToLocal(panel);

                // Staggered upward arc drift: fast rise then slow
                var riseEase = 1f - Mathf.Pow(1f - t, 2.5f);
                var laneOffset = (lane - 1) * 32f;
                label.style.left = point.x - 160f + laneOffset;
                label.style.top = point.y - lane * 30f - riseEase * 105f;

                // 7DSGC-style Juicy bounce curve: Explosive surge -> Elastic bounce recoil -> Settle -> Float
                float pop;
                if (t < 0.12f)
                {
                    var popT = t / 0.12f;
                    pop = Mathf.Lerp(0.35f, maxScale, Mathf.Sin(popT * Mathf.PI * 0.5f));
                }
                else if (t < 0.28f)
                {
                    var bounceT = (t - 0.12f) / 0.16f;
                    pop = Mathf.Lerp(maxScale, settleScale * 0.92f, bounceT);
                }
                else if (t < 0.42f)
                {
                    var settleT = (t - 0.28f) / 0.14f;
                    pop = Mathf.Lerp(settleScale * 0.92f, settleScale, settleT);
                }
                else
                {
                    pop = settleScale;
                }
                label.style.scale = new Scale(Vector3.one * pop);

                // High readability hold, then accelerated fade-out
                var alpha = t < 0.68f ? 1f : Mathf.Clamp01(1f - (t - 0.68f) / 0.32f);
                label.style.opacity = alpha;
            });
            label.RemoveFromHierarchy();
        }

        private static float CombatTextFontSize(string text, string kind)
        {
            var baseSize = kind == "critical" ? 54f : kind == "blocked" ? 34f : kind == "heal" ? 40f : kind == "passive" ? 28f : kind == "status" ? 26f : 42f;
            var longestLine = 1;
            foreach (var line in (text ?? string.Empty).Split('\n'))
                longestLine = Mathf.Max(longestLine, line.Length);
            return Mathf.Clamp(Mathf.Min(baseSize, 320f / (longestLine * .60f)), 16f, baseSize);
        }

        private void ShowBattleResult()
        {
            SetPhase(BattlePresentationPhase.Complete);
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
            if (_session == null || _isAnimatingCards || PresentationPhase == BattlePresentationPhase.Complete || CanPlan) return;
            StopAllCoroutines();
            _stage?.SnapToPlanning();
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
            StopAllCoroutines();
            if (_stage != null) _stage.Restore();
            _effectsLayer?.Clear();
        }
    }
}
