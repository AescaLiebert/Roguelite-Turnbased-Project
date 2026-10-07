using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat.AI
{
    /// <summary>
    /// Phase 3: Evaluates candidate turn plans across 11 utility dimensions using
    /// the team's personality profile weights.
    /// </summary>
    public static class UtilityScorer
    {
        public const int DimensionCount = 11;

        public static ScoredPlan Score(CandidatePlan candidate, BattleSituation situation, BattleState state, AiPersonalityProfile profile)
        {
            var scored = new ScoredPlan
            {
                Candidate = candidate,
                DimensionScores = new float[DimensionCount]
            };

            if (candidate == null || situation == null) return scored;

            // Compute dimension values
            float damageScore     = EstimatePlanDamage(candidate, situation, state);
            float killScore       = EstimateKillBonuses(candidate, situation, state);
            float healScore       = EstimatePlanHealing(candidate, situation, state);
            float debuffScore     = EstimateDebuffImpact(candidate, situation);
            float buffScore       = EstimateBuffImpact(candidate, situation);
            float mergeScore      = candidate.MergesTriggered * 2.5f;
            float ultScore        = candidate.UsedUltimate ? 4.0f : 0f;
            float gaugeScore      = EstimateGaugeGains(candidate, situation);
            float threatScore     = EstimateThreatFocus(candidate, situation);
            float survivePenalty  = EstimateSurviveRisk(candidate, situation);
            float wastedSlots     = (situation.ActionBudget - candidate.Actions.Count) * -2.0f;

            // Apply personality weights
            scored.DimensionScores[0]  = damageScore * profile.DamageDealt;
            scored.DimensionScores[1]  = killScore * profile.KillPotential;
            scored.DimensionScores[2]  = healScore * profile.HealValue;
            scored.DimensionScores[3]  = debuffScore * profile.DebuffValue;
            scored.DimensionScores[4]  = buffScore * profile.BuffValue;
            scored.DimensionScores[5]  = mergeScore * profile.MergeValue;
            scored.DimensionScores[6]  = ultScore * profile.UltimateUsed;
            scored.DimensionScores[7]  = gaugeScore * profile.GaugeEfficiency;
            scored.DimensionScores[8]  = threatScore * profile.ThreatReduction;
            scored.DimensionScores[9]  = survivePenalty * Math.Abs(profile.SurvivePenalty);
            scored.DimensionScores[10] = wastedSlots * Math.Abs(profile.WastedSlots);

            // Total sum
            scored.TotalScore = scored.DimensionScores.Sum();
            candidate.EstimatedDamage = damageScore;
            candidate.EstimatedHealing = healScore;

            return scored;
        }

        private static float EstimatePlanDamage(CandidatePlan candidate, BattleSituation situation, BattleState state)
        {
            float totalDamage = 0f;
            var aiTeam = state.Team(state.ActingSide);

            foreach (var action in candidate.Actions)
            {
                if (action.IsMove) continue;

                var card = aiTeam.Hand.FirstOrDefault(c => c.Id == action.CardId);
                if (card == null) continue;
                if (situation.OwnHand.Any(option => option.CardId == card.Id && option.IsDisabled)) continue;

                var owner = aiTeam.FindFighter(card.OwnerFighterId);
                if (owner == null) continue;

                var stats = StatusSystem.GetEffectiveStats(owner);

                if (card.Kind == CardKind.Ultimate)
                {
                    totalDamage += stats.Attack * 3.5f;
                }
                else if (CardRules.GetEffectCategory(card) == CardCategory.Attack || CardRules.GetEffectCategory(card) == CardCategory.AttackDebuff)
                {
                    float rankMultiplier = card.Rank == 1 ? 1.0f : card.Rank == 2 ? 1.6f : 2.5f;
                    totalDamage += stats.Attack * rankMultiplier;
                }
            }

            return totalDamage / 1000f; // Scale to manageable numbers
        }

        private static float EstimateKillBonuses(CandidatePlan candidate, BattleSituation situation, BattleState state)
        {
            float killBonus = 0f;
            var damageByTarget = new Dictionary<string, float>(StringComparer.Ordinal);
            var aiTeam = state.Team(state.ActingSide);

            foreach (var action in candidate.Actions)
            {
                if (action.IsMove || string.IsNullOrEmpty(action.TargetFighterId)) continue;

                var card = aiTeam.Hand.FirstOrDefault(c => c.Id == action.CardId);
                if (card == null) continue;
                if (situation.OwnHand.Any(option => option.CardId == card.Id && option.IsDisabled)) continue;

                var owner = aiTeam.FindFighter(card.OwnerFighterId);
                if (owner == null) continue;

                var stats = StatusSystem.GetEffectiveStats(owner);
                float estDmg = stats.Attack * (card.Kind == CardKind.Ultimate ? 3.5f : card.Rank * 1.2f);

                if (!damageByTarget.ContainsKey(action.TargetFighterId))
                    damageByTarget[action.TargetFighterId] = 0;
                damageByTarget[action.TargetFighterId] += estDmg;
            }

            foreach (var kvp in damageByTarget)
            {
                var threat = situation.PlayerFighters.FirstOrDefault(p => p.FighterId == kvp.Key);
                if (threat != null)
                {
                    int totalTargetEhp = threat.State.Health + threat.State.Shield;
                    if (kvp.Value >= totalTargetEhp * 0.85f)
                    {
                        // Highly likely kill! Eliminating an enemy removes their turn actions entirely
                        killBonus += 5.0f;
                        if (threat == situation.MostDangerous) killBonus += 2.5f; // Eliminating boss/carry
                    }
                }
            }

            return killBonus;
        }

        private static float EstimatePlanHealing(CandidatePlan candidate, BattleSituation situation, BattleState state)
        {
            float healScore = 0f;
            var aiTeam = state.Team(state.ActingSide);

            foreach (var action in candidate.Actions)
            {
                if (action.IsMove) continue;

                var card = aiTeam.Hand.FirstOrDefault(c => c.Id == action.CardId);
                if (card == null || CardRules.GetEffectCategory(card) != CardCategory.Recovery) continue;
                if (situation.OwnHand.Any(option => option.CardId == card.Id && option.IsDisabled)) continue;

                var owner = aiTeam.FindFighter(card.OwnerFighterId);
                if (owner == null) continue;

                var targetAlly = situation.OwnFighters.Find(f => f.FighterId == action.TargetFighterId);
                if (targetAlly != null)
                {
                    float fitness = AllyTargetEvaluator.EvaluateAllyFitness(card, targetAlly, situation, candidate.Actions);
                    healScore += fitness;
                }
                else
                {
                    // Value is higher if allies are injured
                    if (situation.AnyOwnFighterLowHp) healScore += 4.0f;
                    else if (situation.OwnTeamHealthRatio < 0.7f) healScore += 2.0f;
                    else healScore += 0.5f; // Overhealing has low value
                }
            }

            return healScore;
        }

        private static float EstimateDebuffImpact(CandidatePlan candidate, BattleSituation situation)
        {
            float score = 0f;
            foreach (var action in candidate.Actions)
            {
                if (action.IsMove) continue;
                var cardOpt = situation.OwnHand.FirstOrDefault(c => c.CardId == action.CardId);
                if (cardOpt == null) continue;
                if (cardOpt.IsDisabled) continue;

                if (cardOpt.Category == CardCategory.Debuff || cardOpt.Category == CardCategory.AttackDebuff)
                {
                    score += 1.5f * cardOpt.Rank;
                    if (action.TargetFighterId == situation.MostDangerous?.FighterId)
                        score += 1.5f; // Debuffing player's carry
                }
            }
            return score;
        }

        private static float EstimateBuffImpact(CandidatePlan candidate, BattleSituation situation)
        {
            float score = 0f;
            foreach (var action in candidate.Actions)
            {
                if (action.IsMove) continue;
                var cardOpt = situation.OwnHand.FirstOrDefault(c => c.CardId == action.CardId);
                if (cardOpt != null && !cardOpt.IsDisabled && (cardOpt.Category == CardCategory.Buff || cardOpt.Category == CardCategory.Stance))
                {
                    float baseVal = 1.2f * cardOpt.Rank;
                    var targetAlly = situation.OwnFighters.Find(f => f.FighterId == action.TargetFighterId);
                    if (targetAlly != null)
                    {
                        float fitness = AllyTargetEvaluator.EvaluateAllyFitness(cardOpt.Card, targetAlly, situation, candidate.Actions);
                        score += baseVal + fitness;
                    }
                    else
                    {
                        score += baseVal;
                    }
                }
            }
            return score;
        }

        private static float EstimateGaugeGains(CandidatePlan candidate, BattleSituation situation)
        {
            float gaugeScore = 0f;
            // Every action (move or play) gives +1 PG to the card owner
            // Merges give additional +1 PG
            gaugeScore += candidate.Actions.Count * 0.5f;
            gaugeScore += candidate.MergesTriggered * 1.2f;

            // Check if any fighter reaches 5 PG after this plan
            foreach (var own in situation.OwnFighters)
            {
                int actionsByFighter = candidate.Actions.Count(a => !a.IsMove && situation.OwnHand.Any(c => c.CardId == a.CardId && c.Card.OwnerFighterId == own.FighterId));
                if (own.PowerGauge + actionsByFighter >= CardRules.UltimateGaugeCost && own.PowerGauge < CardRules.UltimateGaugeCost)
                {
                    gaugeScore += 2.0f; // Securing Ultimate readiness for next turn!
                }
            }

            return gaugeScore;
        }

        private static float EstimateThreatFocus(CandidatePlan candidate, BattleSituation situation)
        {
            float score = 0f;
            if (situation.MostDangerous == null) return score;

            foreach (var action in candidate.Actions)
            {
                if (!action.IsMove && action.TargetFighterId == situation.MostDangerous.FighterId)
                {
                    if (situation.OwnHand.Any(card => card.CardId == action.CardId && card.IsDisabled)) continue;
                    score += 1.2f;
                }
                // Avoid attacking into stance without stance break
                var targetThreat = situation.PlayerFighters.FirstOrDefault(p => p.FighterId == action.TargetFighterId);
                if (targetThreat != null && targetThreat.HasStance)
                {
                    score -= 1.0f; // Discourage hitting counter stance
                }
            }
            return score;
        }

        private static float EstimateSurviveRisk(CandidatePlan candidate, BattleSituation situation)
        {
            // If any ally is critically low and the candidate contains no heals, apply penalty
            if (situation.AnyOwnFighterLowHp)
            {
                bool hasHeal = candidate.Actions.Any(a => !a.IsMove && situation.OwnHand.Any(c => c.CardId == a.CardId &&
                    !c.IsDisabled && c.Category == CardCategory.Recovery));
                if (!hasHeal) return -2.5f;
            }
            return 0f;
        }
    }
}
