using System;
using System.Collections.Generic;

namespace FightingAllstar.Core.Combat.AI
{
    /// <summary>
    /// Phase 4: Validates candidate plans using PlanDraft to ensure no actions fizzle,
    /// taunts/statuses are respected, and hand updates match simulation.
    /// </summary>
    public static class PlanSimulator
    {
        public static SimulationResult Validate(CandidatePlan candidate, BattleState state)
        {
            var result = new SimulationResult { IsValid = true };
            if (candidate == null || state == null)
            {
                result.IsValid = false;
                result.FailureReason = "Null candidate or battle state.";
                return result;
            }

            var draft = new PlanDraft(state);
            int merges = 0;

            for (int i = 0; i < candidate.Actions.Count; i++)
            {
                var action = candidate.Actions[i];
                if (action == null)
                {
                    result.IsValid = false;
                    result.FailureReason = $"Action at index {i} is null.";
                    return result;
                }

                if (action.IsMove)
                {
                    if (!draft.QueueMove(action.CardId, action.DestinationIndex, out var moveError))
                    {
                        result.IsValid = false;
                        result.FailureReason = $"Move failed at slot {i}: {moveError}";
                        return result;
                    }

                    // Count merges from last events
                    foreach (var evt in draft.LastEvents)
                    {
                        if (evt.Kind == BattleEventKind.CardsMerged) merges++;
                    }
                }
                else
                {
                    if (!draft.QueuePlay(action.CardId, action.TargetFighterId, out var playError))
                    {
                        result.IsValid = false;
                        result.FailureReason = $"Play failed at slot {i} (card {action.CardId}): {playError}";
                        return result;
                    }

                    foreach (var evt in draft.LastEvents)
                    {
                        if (evt.Kind == BattleEventKind.CardsMerged) merges++;
                    }
                }

                result.ActionsCompleted++;
            }

            result.MergesOccurred = merges;
            result.HandSizeAfterPlan = draft.Preview.Hand.Count;

            int totalPg = 0;
            foreach (var f in draft.Preview.LivingActive()) totalPg += f.PowerGauge;
            result.PgAfterPlan = totalPg;

            return result;
        }
    }
}
