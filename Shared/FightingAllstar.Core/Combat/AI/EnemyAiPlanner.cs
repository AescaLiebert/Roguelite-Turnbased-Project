using System;
using System.Collections.Generic;
using System.Linq;

namespace FightingAllstar.Core.Combat.AI
{
    /// <summary>
    /// Master Enemy AI Planner.
    /// Orchestrates situation assessment, strategic candidate generation, utility scoring,
    /// simulation validation, and optimal plan selection.
    /// Replaces the naive 1-card LegalAi with full tactical team play.
    /// </summary>
    public static class EnemyAiPlanner
    {
        public static Action<string> LogCallback;

        public static TurnPlan CreatePlan(BattleState state, AiPersonalityProfile profileOverride = null)
        {
            if (state == null || state.Phase != BattlePhase.Planning || state.ActionBudget <= 0)
                return null;

            var aiTeam = state.Team(state.ActingSide);
            if (aiTeam == null || aiTeam.Hand.Count == 0 || aiTeam.LivingActive().Count == 0)
                return null;

            try
            {
                state = AiPlanningObservation.Create(state);
                aiTeam = state.Team(state.ActingSide);
                // Determine Personality Profile based on character kits
                var profile = profileOverride ?? AiPersonalityProfile.DeriveFromTeam(aiTeam);

                // Phase 1: Situation Assessment
                var situation = BoardAnalyzer.Analyze(state);

                // Phase 2: Candidate Generation
                var candidates = CandidateBuilder.Generate(situation, state, profile);
                if (candidates.Count == 0)
                {
                    LogCallback?.Invoke("[EnemyAiPlanner] 0 candidates generated, falling back to LegalAi.");
                    return LegalAi.CreatePlan(state); // Fallback
                }

                // Phase 3: Utility Scoring
                var scoredPlans = new List<ScoredPlan>();
                foreach (var candidate in candidates)
                {
                    var scored = UtilityScorer.Score(candidate, situation, state, profile, evaluateElemental: false);
                    scoredPlans.Add(scored);
                }

                // Sort descending by total score
                scoredPlans.Sort((a, b) => b.TotalScore.CompareTo(a.TotalScore));

                if (aiTeam.ElementalReactionsEnabled)
                {
                    // Keep conversion and ordinary choices in the bounded outcome shortlist.
                    int limit = Math.Max(2, Math.Min(16, profile.OutcomeEvaluationDepth));
                    var shortlist = scoredPlans.Where(p => p.Candidate.StrategyName == "ElementalOrder").Take(limit / 2)
                        .Concat(scoredPlans.Where(p => p.Candidate.StrategyName != "ElementalOrder").Take(limit / 2)).ToList();
                    foreach (var entry in scoredPlans)
                        if (shortlist.Count < limit && !shortlist.Contains(entry)) shortlist.Add(entry);
                    scoredPlans = shortlist.Select(p => UtilityScorer.Score(p.Candidate, situation, state, profile)).ToList();
                    scoredPlans.Sort((a, b) => b.TotalScore.CompareTo(a.TotalScore));
                }

                // Phase 4: Simulation Validation on Top Candidates
                int depth = Math.Min(profile.SimulationDepth, scoredPlans.Count);
                for (int i = 0; i < depth; i++)
                {
                    var plan = scoredPlans[i];
                    var simResult = PlanSimulator.Validate(plan.Candidate, state);
                    plan.SimulationValid = simResult.IsValid;

                    // If simulation triggered additional merges, reward with bonus score
                    if (simResult.IsValid && simResult.MergesOccurred > plan.Candidate.MergesTriggered)
                    {
                        plan.TotalScore += (simResult.MergesOccurred - plan.Candidate.MergesTriggered) * 2.0f;
                    }
                }

                // Phase 5: Plan Selection
                // Planning variation is independent of both authority random streams.
                var rng = new DeterministicRandom(unchecked((ulong)state.Revision * 0x9E3779B97F4A7C15UL + (ulong)state.TurnNumber + 1));
                var selectedTurnPlan = PlanSelector.Select(scoredPlans, state, profile, rng);

                if (selectedTurnPlan != null && selectedTurnPlan.Actions.Count > 0)
                {
                    LogCallback?.Invoke($"[EnemyAiPlanner] Selected tactical plan with {selectedTurnPlan.Actions.Count} actions (budget: {state.ActionBudget}, personality: {profile.Personality}).");
                    var reason = scoredPlans.FirstOrDefault(p => p.Candidate.Actions.Count == selectedTurnPlan.Actions.Count &&
                        p.Candidate.Actions.Zip(selectedTurnPlan.Actions, (a, b) => a.CardId == b.CardId && a.TargetFighterId == b.TargetFighterId &&
                            a.IsMove == b.IsMove && a.DestinationIndex == b.DestinationIndex).All(same => same));
                    if (!string.IsNullOrEmpty(reason?.Explanation)) LogCallback?.Invoke("[EnemyAiPlanner] " + reason.Explanation);
                    return selectedTurnPlan;
                }

                // Fallback to basic legal AI if all candidates failed
                LogCallback?.Invoke("[EnemyAiPlanner] Selected plan was null/empty, falling back to LegalAi.");
                return LegalAi.CreatePlan(state);
            }
            catch (Exception ex)
            {
                // Safety net: in case of any runtime edge-case error, log and fallback to safe LegalAi
                LogCallback?.Invoke($"[EnemyAiPlanner] Exception during planning: {ex}");
                return LegalAi.CreatePlan(state);
            }
        }
    }
}
