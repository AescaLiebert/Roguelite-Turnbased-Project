using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    /// <summary>A disposable preview. Reset rebuilds from the captured authority snapshot and never advances RNG.</summary>
    public sealed class PlanDraft
    {
        private readonly BattleState _baseSnapshot;
        private readonly TeamPlanDraft _view;
        private readonly List<PlannedAction> _actions = new List<PlannedAction>();
        private readonly List<ElementalForecastAction> _forecastActions = new List<ElementalForecastAction>();
        public IReadOnlyList<PlannedAction> Actions => _actions;
        private readonly List<BattleEvent> _lastEvents = new List<BattleEvent>();
        public IReadOnlyList<BattleEvent> LastEvents => _lastEvents;
        public BattleTeamState Preview => _view.Team;
        public int RemainingActions => Math.Max(0, _baseSnapshot.ActionBudget - _actions.Count);

        public PlanDraft(BattleState snapshot)
        {
            _baseSnapshot = snapshot.Clone();
            _view = new TeamPlanDraft(_baseSnapshot.Team(_baseSnapshot.ActingSide).Clone());
        }

        public bool QueuePlay(string cardId, string targetId, out string reason)
        {
            _lastEvents.Clear();
            reason = null;
            if (RemainingActions == 0) { reason = "No actions remain this turn."; return false; }
            var card = _view.Team.Hand.Find(c => c.Id == cardId);
            if (card == null) { reason = "Card is not in the draft hand."; return false; }
            var owner = _view.Team.FindFighter(card.OwnerFighterId);
            if (owner == null || !owner.IsAlive || owner.IsReserve) { reason = "Card owner is not an active fighter."; return false; }
            EffectDefinition effect;
            var hasEffect = card.Kind == CardKind.Skill
                ? CardRules.TryGetSkill(owner.Definition, card.SkillId, card.Rank, out effect)
                : CardRules.TryGetUltimate(owner.Definition, card.UltimateTier, out effect);
            // A disabled card can be committed: earlier actions may cleanse its owner.
            // The live status check in BattleEngine decides whether it executes or is discarded.
            var disabled = hasEffect && StatusSystem.IsCardUseBlocked(owner,
                CardRules.GetEffectCategory(card), card.Rank,
                card.Kind == CardKind.Ultimate, effect.Sequence != null && effect.Sequence.Count > 0);
            if (card.Kind == CardKind.Ultimate && owner.PowerGauge < CardRules.UltimateGaugeCost && !disabled && !_view.Team.TrainingDeck)
            {
                reason = "Ultimate requires five PG.";
                return false;
            }
            var target = ResolveDraftTarget(card, owner, targetId);
            if (target == null) { reason = card.TargetScope == EffectTargetScope.SelectedAlly
                ? "Choose a living active ally." : "Choose a living active opponent."; return false; }
            _actions.Add(new PlannedAction { CardId = cardId, TargetFighterId = target.Id });
            _forecastActions.Add(new ElementalForecastAction { CardId = cardId, OwnerId = owner.Id, TargetId = target.Id, Effect = effect?.Clone() });
            _view.Team.Hand.Remove(card);
            PreviewCardGauge(owner, card, effect, target, disabled);
            _lastEvents.Add(new BattleEvent { Kind = BattleEventKind.CardPlayed, SourceId = owner.Id,
                TargetId = target.Id, CardId = card.Id, Card = card.Clone(), PowerGaugeAfter = owner.PowerGauge,
                PowerGaugeRemainderAfter = owner.PowerGaugeRemainderBp,
                TargetIds = ResolveDraftTargets(card, owner, target).ConvertAll(item => item.Id) });
            CardRules.MergeAdjacent(_view.Team, null, _lastEvents);
            return true;
        }

        public bool QueueMove(string cardId, int destination, out string reason)
        {
            _lastEvents.Clear();
            reason = null;
            if (RemainingActions == 0) { reason = "No actions remain this turn."; return false; }
            var from = _view.Team.Hand.FindIndex(c => c.Id == cardId);
            if (!CardRules.TryMove(_view.Team, from, destination, true, out reason, null, _lastEvents)) return false;
            _actions.Add(new PlannedAction { IsMove = true, CardId = cardId, DestinationIndex = destination });
            _forecastActions.Add(null);
            return true;
        }

        private FighterState ResolveDraftTarget(CardState card, FighterState owner, string requestedTargetId)
        {
            var friendly = _baseSnapshot.Team(owner.Side);
            var enemy = _baseSnapshot.OtherTeam(owner.Side);
            switch (card.TargetScope)
            {
                case EffectTargetScope.Self: return owner;
                case EffectTargetScope.SelectedAlly:
                    var ally = friendly.FindFighter(requestedTargetId);
                    return ally != null && ally.IsAlive && !ally.IsReserve ? ally : null;
                case EffectTargetScope.AllAllies: return friendly.LivingActive().Count > 0 ? friendly.LivingActive()[0] : null;
                case EffectTargetScope.AllEnemies: return enemy.LivingActive().Count > 0 ? enemy.LivingActive()[0] : null;
                default:
                    var target = enemy.FindFighter(requestedTargetId);
                    var taunters = enemy.LivingActive().FindAll(f => f.Statuses.Instances.Exists(s => s.Recipe?.HasTaunt == true));
                    if (taunters.Count > 0 && !taunters.Contains(target)) return null;
                    return target != null && target.IsAlive && !target.IsReserve ? target : null;
            }
        }

        private List<FighterState> ResolveDraftTargets(CardState card, FighterState owner, FighterState anchor)
        {
            switch (card.TargetScope)
            {
                case EffectTargetScope.AllAllies: return _baseSnapshot.Team(owner.Side).LivingActive();
                case EffectTargetScope.AllEnemies: return _baseSnapshot.OtherTeam(owner.Side).LivingActive();
                default: return new List<FighterState> { anchor };
            }
        }

        public bool UndoLast()
        {
            if (_actions.Count == 0) return false;
            _actions.RemoveAt(_actions.Count - 1);
            RebuildPreview();
            return true;
        }

        public void Reset()
        {
            _lastEvents.Clear();
            _actions.Clear();
            RebuildPreview();
        }

        public TurnPlan BuildPlan(string requestId) => new TurnPlan { RequestId = requestId,
            ExpectedRevision = _baseSnapshot.Revision, Actions = CloneActions(_actions) };

        private void RebuildPreview()
        {
            _forecastActions.Clear();
            _view.Team = _baseSnapshot.Team(_baseSnapshot.ActingSide).Clone();
            foreach (var action in _actions)
            {
                var from = _view.Team.Hand.FindIndex(c => c.Id == action.CardId);
                if (action.IsMove)
                {
                    CardRules.TryMove(_view.Team, from, action.DestinationIndex, true, out _);
                    _forecastActions.Add(null);
                }
                else if (from >= 0)
                {
                    var card = _view.Team.Hand[from];
                    _view.Team.Hand.RemoveAt(from);
                    var owner = _view.Team.FindFighter(card.OwnerFighterId);
                    EffectDefinition effect;
                    var hasEffect = card.Kind == CardKind.Skill
                        ? CardRules.TryGetSkill(owner.Definition, card.SkillId, card.Rank, out effect)
                        : CardRules.TryGetUltimate(owner.Definition, card.UltimateTier, out effect);
                    _forecastActions.Add(new ElementalForecastAction { CardId = card.Id, OwnerId = owner.Id,
                        TargetId = action.TargetFighterId, Effect = effect?.Clone() });
                    var target = ResolveDraftTarget(card, owner, action.TargetFighterId);
                    var disabled = hasEffect && StatusSystem.IsCardUseBlocked(owner,
                        CardRules.GetEffectCategory(card), card.Rank,
                        card.Kind == CardKind.Ultimate, effect.Sequence != null && effect.Sequence.Count > 0);
                    PreviewCardGauge(owner, card, effect, target, disabled);
                    CardRules.MergeAdjacent(_view.Team, null);
                }
            }
        }

        private void PreviewCardGauge(FighterState owner, CardState card, EffectDefinition effect,
            FighterState target, bool disabled)
        {
            // Forecast the current checkpoint outcome; execution rechecks after earlier effects.
            if (!disabled && effect?.Kind == EffectKind.Damage &&
                (CardRules.GetEffectCategory(card) == CardCategory.Attack ||
                 CardRules.GetEffectCategory(card) == CardCategory.AttackDebuff))
                PreviewAttackerGaugeSupport(owner, card, target);

            if (card.Kind == CardKind.Ultimate && !disabled) PowerGaugeRules.SpendUltimate(owner);
            else PowerGaugeRules.GainForAction(owner);
            if (owner.PowerGaugeDisabled) owner.PowerGauge = 0;
        }

        private void PreviewAttackerGaugeSupport(FighterState attacker, CardState card, FighterState target)
        {
            if (attacker == null || target == null) return;
            var targets = ResolveDraftTargets(card, attacker, target);
            foreach (var provider in _view.Team.Fighters)
            {
                var support = provider?.Definition?.Passive?.Support;
                var providerAvailable = provider != null && provider.IsAlive && provider.Health > 0 &&
                    (support?.Presence == PassivePresence.LivingRoster ||
                     support?.Presence == PassivePresence.LivingActive && !provider.IsReserve ||
                     support?.Presence == PassivePresence.LivingReserve && provider.IsReserve);
                if (!providerAvailable || string.IsNullOrEmpty(support.AttackerGaugeAgainstRecipeId) ||
                    support.AttackerGaugeGainBp <= 0 || !targets.Exists(t =>
                        StatusSystem.Count(t, recipeId: support.AttackerGaugeAgainstRecipeId) > 0)) continue;

                var recipe = new StatusRecipeDefinition {
                    Id = "status.buff.gauge-acceleration", NameKey = "Gauge Acceleration",
                    Polarity = StatusPolarity.Buff, DefaultDuration = 1,
                    PowerGaugeGainBp = support.AttackerGaugeGainBp,
                    Stacking = StatusStackingPolicy.RefreshDuration
                };
                if (recipe.DurationClock == StatusDurationClock.TargetTurnStart ||
                    recipe.DurationClock == StatusDurationClock.TargetTurnEnd)
                    recipe.DurationClock = attacker.Side == provider.Side
                        ? StatusDurationClock.TargetTurnStart : StatusDurationClock.TargetTurnEnd;
                StatusSystem.Apply(attacker, provider.Id, provider.Side, recipe,
                    "preview:gauge-acceleration:" + attacker.Id, card.Id, _forecastActions.Count);
            }
        }

        public ElementalForecastResult Forecast(string candidateCardId = null, string candidateTargetId = null)
        {
            var actions = new List<ElementalForecastAction>(_forecastActions);
            var card = _view.Team.Hand.Find(c => c.Id == candidateCardId);
            if (card != null)
            {
                var owner = _view.Team.FindFighter(card.OwnerFighterId);
                EffectDefinition effect;
                if (card.Kind == CardKind.Skill) CardRules.TryGetSkill(owner.Definition, card.SkillId, card.Rank, out effect);
                else CardRules.TryGetUltimate(owner.Definition, card.UltimateTier, out effect);
                actions.Add(new ElementalForecastAction { CardId = card.Id, OwnerId = owner.Id,
                    TargetId = candidateTargetId, Effect = effect?.Clone() });
            }
            return ElementalForecast.Build(BattleProjectionBuilder.Build(_baseSnapshot, _baseSnapshot.ActingSide,
                "local-forecast", long.MaxValue), _baseSnapshot.ElementalRules, actions);
        }

        private static List<PlannedAction> CloneActions(List<PlannedAction> source)
        {
            var result = new List<PlannedAction>();
            foreach (var action in source) result.Add(action.Clone());
            return result;
        }

        private sealed class TeamPlanDraft
        {
            public BattleTeamState Team;
            public TeamPlanDraft(BattleTeamState team) { Team = team; }
        }
    }
}
