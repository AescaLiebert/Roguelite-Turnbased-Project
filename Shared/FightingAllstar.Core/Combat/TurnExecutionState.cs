using System;
using System.Collections.Generic;

namespace FightingAllstar.Core.Combat
{
    public enum PlannedActionState { Pending, Executing, Completed, Fizzled, Cancelled }

    /// <summary>A frozen turn queue shared by both sides. Card availability is checked at execution.</summary>
    [Serializable]
    public sealed class TurnExecutionState
    {
        public TeamSide Side;
        public long Revision;
        public int CurrentIndex = -1;
        public List<QueuedBattleAction> Actions = new List<QueuedBattleAction>();

        public TurnExecutionState Clone() => new TurnExecutionState { Side = Side, Revision = Revision,
            CurrentIndex = CurrentIndex, Actions = Actions.ConvertAll(action => action.Clone()) };

        public static bool TryCreate(BattleState snapshot, TurnPlan plan, out TurnExecutionState execution, out string error)
        {
            execution = new TurnExecutionState { Side = snapshot.ActingSide, Revision = snapshot.Revision };
            var draft = new PlanDraft(snapshot);
            error = null;
            foreach (var action in plan.Actions)
            {
                if (action == null) { error = "Plan contains an empty action."; return false; }
                var card = draft.Preview.Hand.Find(item => item.Id == action.CardId)?.Clone();
                var valid = action.IsMove
                    ? draft.QueueMove(action.CardId, action.DestinationIndex, out error)
                    : draft.QueuePlay(action.CardId, action.TargetFighterId, out error);
                if (!valid) return false;
                execution.Actions.Add(new QueuedBattleAction { Action = action.Clone(), Card = card });
            }
            return true;
        }
    }

    [Serializable]
    public sealed class QueuedBattleAction
    {
        public PlannedAction Action;
        public CardState Card;
        public PlannedActionState State;
        public QueuedBattleAction Clone() => new QueuedBattleAction { Action = Action?.Clone(), Card = Card?.Clone(), State = State };
    }
}
