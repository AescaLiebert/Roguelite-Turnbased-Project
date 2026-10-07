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
            _view.Team.Hand.Remove(card);
            if (disabled) owner.PowerGauge = Math.Min(CardRules.UltimateGaugeCost, owner.PowerGauge + 1);
            else if (card.Kind == CardKind.Ultimate) owner.PowerGauge = 0;
            else owner.PowerGauge = Math.Min(5, owner.PowerGauge + 1);
            if (owner.PowerGaugeDisabled) owner.PowerGauge = 0;
            _lastEvents.Add(new BattleEvent { Kind = BattleEventKind.CardPlayed, SourceId = owner.Id,
                TargetId = target.Id, CardId = card.Id, Card = card.Clone(), PowerGaugeAfter = owner.PowerGauge,
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
            _view.Team = _baseSnapshot.Team(_baseSnapshot.ActingSide).Clone();
            foreach (var action in _actions)
            {
                var from = _view.Team.Hand.FindIndex(c => c.Id == action.CardId);
                if (action.IsMove) CardRules.TryMove(_view.Team, from, action.DestinationIndex, true, out _);
                else if (from >= 0)
                {
                    var card = _view.Team.Hand[from];
                    _view.Team.Hand.RemoveAt(from);
                    var owner = _view.Team.FindFighter(card.OwnerFighterId);
                    if (card.Kind == CardKind.Ultimate) owner.PowerGauge = 0;
                    else owner.PowerGauge = Math.Min(5, owner.PowerGauge + 1);
                    if (owner.PowerGaugeDisabled) owner.PowerGauge = 0;
                    CardRules.MergeAdjacent(_view.Team, null);
                }
            }
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
