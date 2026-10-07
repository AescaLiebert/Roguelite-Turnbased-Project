using System;
using System.Collections.Generic;
using System.Linq;

namespace FightingAllstar.Core.Combat.AI
{
    /// <summary>
    /// Phase 5: Selects the winning TurnPlan from validated scored plans,
    /// adding optional slight noise to prevent robotic predictability.
    /// </summary>
    public static class PlanSelector
    {
        public static TurnPlan Select(List<ScoredPlan> scoredPlans, BattleState state, AiPersonalityProfile profile, DeterministicRandom rng = null)
        {
            if (scoredPlans == null || scoredPlans.Count == 0) return null;

            // Filter to only simulation-valid plans
            var validPlans = scoredPlans.Where(p => p.SimulationValid).ToList();
            if (validPlans.Count == 0)
            {
                // Fallback: take the highest scoring plan even if simulation threw a warning,
                // or return null to fall back to LegalAi
                validPlans = scoredPlans;
            }

            // Apply slight noise for natural feel
            float noiseFactor = profile?.NoisePercent ?? 0.02f;

            ScoredPlan best = null;
            float bestAdjustedScore = float.MinValue;

            foreach (var plan in validPlans)
            {
                float randomOffset = 0f;
                if (rng != null && noiseFactor > 0f)
                {
                    // Deterministic roll between -noiseFactor and +noiseFactor
                    float roll = (rng.NextBasisPoints() / 10000f) * 2f - 1f;
                    randomOffset = plan.TotalScore * (roll * noiseFactor);
                }

                float finalScore = plan.TotalScore + randomOffset;
                if (finalScore > bestAdjustedScore)
                {
                    bestAdjustedScore = finalScore;
                    best = plan;
                }
            }

            if (best == null) best = validPlans[0];

            var turnPlan = new TurnPlan
            {
                RequestId = $"enemy-ai:{state.MatchId}:{state.Revision}:{Guid.NewGuid().ToString("N").Substring(0, 8)}",
                ExpectedRevision = state.Revision,
                Actions = best.Candidate.Actions.Select(a => a.Clone()).ToList()
            };

            return turnPlan;
        }
    }
}
