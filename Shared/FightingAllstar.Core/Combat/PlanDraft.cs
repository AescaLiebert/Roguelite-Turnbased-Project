using System;
using System.Collections.Generic;

namespace FightingAllstar.Core.Combat
{
    /// <summary>A disposable preview. Reset rebuilds from the captured authority snapshot and never advances RNG.</summary>
    public sealed class PlanDraft
    {
        private readonly BattleState _baseSnapshot;
        private readonly TeamPlanDraft _view;
        private readonly List<PlannedAction> _actions = new List<PlannedAction>();
        public IReadOnlyList<PlannedAction> Actions => _actions;
        public BattleTeamState Preview => _view.Team;
        public int RemainingActions => Math.Max(0, _baseSnapshot.ActionBudget - _actions.Count);

        public PlanDraft(BattleState snapshot)
        {
            _baseSnapshot = snapshot.Clone();
            _view = new TeamPlanDraft(_baseSnapshot.Team(_baseSnapshot.ActingSide).Clone());
        }

        public bool QueuePlay(string cardId, string targetId, out string reason)
        {
            reason = null;
            if (RemainingActions == 0) { reason = "No actions remain this turn."; return false; }
            var card = _view.Team.Hand.Find(c => c.Id == cardId);
            if (card == null) { reason = "Card is not in the draft hand."; return false; }
            var owner = _view.Team.FindFighter(card.OwnerFighterId);
            if (owner == null || !owner.IsAlive || owner.IsReserve) { reason = "Card owner is not an active fighter."; return false; }
            if (card.Kind == CardKind.Ultimate && owner.PowerGauge < CardRules.UltimateGaugeCost)
            { reason = "Ultimate requires five PG."; return false; }
            var target = _baseSnapshot.OtherTeam(_baseSnapshot.ActingSide).FindFighter(targetId);
            if (target == null || !target.IsAlive || target.IsReserve) { reason = "Choose a living active opponent."; return false; }
            _actions.Add(new PlannedAction { CardId = cardId, TargetFighterId = targetId });
            _view.Team.Hand.Remove(card);
            if (card.Kind == CardKind.Ultimate) owner.PowerGauge = 0;
            else owner.PowerGauge = Math.Min(5, owner.PowerGauge + 1);
            CardRules.MergeAdjacent(_view.Team, null, true);
            return true;
        }

        public bool QueueMove(string cardId, int destination, out string reason)
        {
            reason = null;
            if (RemainingActions == 0) { reason = "No actions remain this turn."; return false; }
            var from = _view.Team.Hand.FindIndex(c => c.Id == cardId);
            if (!CardRules.TryMove(_view.Team, from, destination, true, out reason)) return false;
            _actions.Add(new PlannedAction { IsMove = true, CardId = cardId, DestinationIndex = destination });
            return true;
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
                    CardRules.MergeAdjacent(_view.Team, null, true);
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
