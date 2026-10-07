using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat.AI
{
    /// <summary>
    /// Phase 2: Generates candidate multi-action turn plans.
    /// Combines tactical templates (Merge-then-play, Finisher combo, Stance break, Heal rescue, Ultimate rush)
    /// with iterative beam search to populate full action budgets.
    /// </summary>
    public static class CandidateBuilder
    {
        public static List<CandidatePlan> Generate(BattleSituation situation, BattleState state, AiPersonalityProfile profile)
        {
            var candidates = new List<CandidatePlan>();
            if (situation == null || state == null || situation.ActionBudget <= 0) return candidates;

            int budget = situation.ActionBudget;

            // 1. Template: Stance Cancel / Counter Evasion
            GenerateStanceCounterBreak(situation, state, candidates, budget);

            // 2. Template: Ultimate Priority
            if (situation.AnyUltimateReady)
            {
                GenerateUltimateRushes(situation, state, candidates, budget);
            }

            // 3. Template: Finisher / Focus Fire
            GenerateFinisherCombos(situation, state, candidates, budget);

            // 4. Template: Strategic Merge Combos
            if (situation.MergeCandidates.Count > 0 && budget >= 2)
            {
                GenerateMergeCombos(situation, state, candidates, budget);
            }

            // 5. Template: Heal / Rescue
            if (situation.AnyOwnFighterLowHp || situation.OwnTeamHealthRatio < 0.5f)
            {
                GenerateHealRescue(situation, state, candidates, budget);
            }

            // 6. Template: Debuff / Control First
            GenerateDebuffSetups(situation, state, candidates, budget);

            // 7. Template: Buff Setup / Carry Empowerment
            GenerateBuffSetups(situation, state, candidates, budget);

            // 8. General Beam Search (fills any gaps and explores unexpected synergies)
            GenerateBeamSearchPlans(situation, state, candidates, budget, profile.BeamWidth);

            // Filter out empty plans or plans exceeding budget
            var validCandidates = new List<CandidatePlan>();
            var seenFingerprints = new HashSet<string>(StringComparer.Ordinal);

            foreach (var cand in candidates)
            {
                if (cand.Actions.Count == 0 || cand.Actions.Count > budget) continue;

                // Pad plan to full action budget if it has fewer actions than budget
                PadPlanToBudget(cand, state, situation);

                string fp = FingerprintPlan(cand);
                if (seenFingerprints.Add(fp))
                {
                    validCandidates.Add(cand);
                }
            }

            // Cap total candidates to profile.MaxCandidates
            if (validCandidates.Count > profile.MaxCandidates)
            {
                validCandidates = validCandidates.Take(profile.MaxCandidates).ToList();
            }

            return validCandidates;
        }

        // ──────────────────────────────────────────────────────────
        //  Tactical Templates
        // ──────────────────────────────────────────────────────────

        private static void GenerateStanceCounterBreak(BattleSituation situation, BattleState state, List<CandidatePlan> candidates, int budget)
        {
            var stanceThreat = situation.PlayerFighters.FirstOrDefault(p => p.HasStance);
            if (stanceThreat == null) return;

            // Search for a card in hand with RemoveStance or CancelStance
            var cancelCard = situation.OwnHand.FirstOrDefault(c =>
                c.IsPlayable && (c.Category == CardCategory.Attack || c.Category == CardCategory.Debuff || c.Category == CardCategory.AttackDebuff) &&
                HasStanceCancelEffect(c.Card, c.Owner?.Definition));

            if (cancelCard == null) return;

            var draft = new PlanDraft(state);
            var plan = new CandidatePlan { StrategyName = "StanceBreak", Description = $"Dispel stance on {stanceThreat.FighterId}" };

            string targetId = ResolveTargetForCard(cancelCard.Card, cancelCard.Owner, stanceThreat.FighterId, situation);
            if (draft.QueuePlay(cancelCard.CardId, targetId, out _))
            {
                plan.Actions.Add(new PlannedAction { CardId = cancelCard.CardId, TargetFighterId = targetId });
                FillRemainingSlotsGreedy(draft, plan, situation, budget);
                candidates.Add(plan);
            }
        }

        private static void GenerateUltimateRushes(BattleSituation situation, BattleState state, List<CandidatePlan> candidates, int budget)
        {
            var ultCards = situation.OwnHand.Where(c => c.IsPlayable && c.IsUltimate).ToList();
            foreach (var ult in ultCards)
            {
                var targetThreat = SelectOptimalTargetForAttack(situation);
                if (targetThreat == null) continue;

                var draft = new PlanDraft(state);
                var plan = new CandidatePlan { StrategyName = "UltimateRush", Description = $"Lead with ultimate from {ult.Owner?.Id}" };

                string targetId = ResolveTargetForCard(ult.Card, ult.Owner, targetThreat.FighterId, situation);
                if (draft.QueuePlay(ult.CardId, targetId, out _))
                {
                    plan.Actions.Add(new PlannedAction { CardId = ult.CardId, TargetFighterId = targetId });
                    plan.UsedUltimate = true;
                    FillRemainingSlotsGreedy(draft, plan, situation, budget);
                    candidates.Add(plan);
                }
            }
        }

        private static void GenerateFinisherCombos(BattleSituation situation, BattleState state, List<CandidatePlan> candidates, int budget)
        {
            // Focus on weakest or killable target
            var primeTarget = situation.Weakest ?? situation.MostDangerous;
            if (primeTarget == null) return;

            var draft = new PlanDraft(state);
            var plan = new CandidatePlan { StrategyName = "FinisherFocus", Description = $"Focus down {primeTarget.FighterId}" };

            // Pick highest damage/rank attacks targeting this enemy
            FillTargetFocusedSlots(draft, plan, situation, primeTarget.FighterId, budget);
            if (plan.Actions.Count > 0)
            {
                candidates.Add(plan);
            }
        }

        private static void GenerateMergeCombos(BattleSituation situation, BattleState state, List<CandidatePlan> candidates, int budget)
        {
            foreach (var merge in situation.MergeCandidates.Take(3))
            {
                var draft = new PlanDraft(state);
                var plan = new CandidatePlan
                {
                    StrategyName = "StrategicMerge",
                    Description = $"Merge rank {merge.CardA.Rank}->{merge.ResultingRank} ({merge.CardA.Card.SkillId})"
                };

                // Action 1: Move card to trigger merge
                if (draft.QueueMove(merge.CardA.CardId, merge.MoveToIndex, out _))
                {
                    plan.Actions.Add(new PlannedAction
                    {
                        IsMove = true,
                        CardId = merge.CardA.CardId,
                        DestinationIndex = merge.MoveToIndex
                    });
                    plan.MergesTriggered++;

                    // Action 2+: Follow up by playing the newly ranked up card or capitalising on the new hand!
                    var upgradedCard = draft.Preview.Hand.FirstOrDefault(c =>
                        c.OwnerFighterId == merge.CardA.Card.OwnerFighterId &&
                        c.SkillId == merge.CardA.Card.SkillId &&
                        c.Rank == merge.ResultingRank);

                    if (upgradedCard != null && plan.Actions.Count < budget)
                    {
                        var target = SelectOptimalTargetForAttack(situation);
                        string targetId = ResolveTargetForCard(upgradedCard, draft.Preview.FindFighter(upgradedCard.OwnerFighterId), target?.FighterId, situation);
                        if (draft.QueuePlay(upgradedCard.Id, targetId, out _))
                        {
                            plan.Actions.Add(new PlannedAction { CardId = upgradedCard.Id, TargetFighterId = targetId });
                        }
                    }

                    FillRemainingSlotsGreedy(draft, plan, situation, budget);
                    candidates.Add(plan);
                }
            }
        }

        private static void GenerateHealRescue(BattleSituation situation, BattleState state, List<CandidatePlan> candidates, int budget)
        {
            var healCards = situation.OwnHand.Where(c => c.IsPlayable && c.Category == CardCategory.Recovery).ToList();
            if (healCards.Count == 0) return;

            foreach (var heal in healCards)
            {
                var draft = new PlanDraft(state);
                var plan = new CandidatePlan { StrategyName = "HealRescue", Description = "Lead with recovery card" };

                // Select tactical ally target
                string targetId = AllyTargetEvaluator.SelectBestAllyForCard(heal.Card, situation, heal.Owner);
                if (string.IsNullOrEmpty(targetId))
                {
                    var lowestAlly = situation.OwnFighters.OrderBy(f => f.HealthRatio).FirstOrDefault();
                    targetId = lowestAlly?.FighterId ?? heal.Owner?.Id;
                }

                if (draft.QueuePlay(heal.CardId, targetId, out _))
                {
                    plan.Actions.Add(new PlannedAction { CardId = heal.CardId, TargetFighterId = targetId });
                    FillRemainingSlotsGreedy(draft, plan, situation, budget);
                    candidates.Add(plan);
                }
            }
        }

        private static void GenerateDebuffSetups(BattleSituation situation, BattleState state, List<CandidatePlan> candidates, int budget)
        {
            var debuffCards = situation.OwnHand.Where(c => c.IsPlayable && (c.Category == CardCategory.Debuff || c.Category == CardCategory.AttackDebuff)).ToList();
            if (debuffCards.Count == 0) return;

            var primaryEnemy = situation.MostDangerous ?? situation.Weakest;
            if (primaryEnemy == null) return;

            foreach (var debuff in debuffCards)
            {
                var draft = new PlanDraft(state);
                var plan = new CandidatePlan { StrategyName = "DebuffSetup", Description = "Apply debuff before striking" };

                string targetId = ResolveTargetForCard(debuff.Card, debuff.Owner, primaryEnemy.FighterId, situation);
                if (draft.QueuePlay(debuff.CardId, targetId, out _))
                {
                    plan.Actions.Add(new PlannedAction { CardId = debuff.CardId, TargetFighterId = targetId });
                    FillRemainingSlotsGreedy(draft, plan, situation, budget);
                    candidates.Add(plan);
                }
            }
        }

        private static void GenerateBuffSetups(BattleSituation situation, BattleState state, List<CandidatePlan> candidates, int budget)
        {
            var buffCards = situation.OwnHand.Where(c => c.IsPlayable && (c.Category == CardCategory.Buff || c.Category == CardCategory.Stance)).ToList();
            if (buffCards.Count == 0) return;

            foreach (var buff in buffCards)
            {
                var draft = new PlanDraft(state);
                var plan = new CandidatePlan { StrategyName = "BuffSetup", Description = "Empower ally before striking" };

                string targetId = AllyTargetEvaluator.SelectBestAllyForCard(buff.Card, situation, buff.Owner);
                if (string.IsNullOrEmpty(targetId)) targetId = buff.Owner?.Id;

                if (draft.QueuePlay(buff.CardId, targetId, out _))
                {
                    plan.Actions.Add(new PlannedAction { CardId = buff.CardId, TargetFighterId = targetId });

                    // Follow up with attacks from the buffed ally if available
                    var allyAttacks = draft.Preview.Hand.Where(c =>
                        c.OwnerFighterId == targetId &&
                        (c.Category == CardCategory.Attack || c.Category == CardCategory.AttackDebuff || c.Kind == CardKind.Ultimate))
                        .OrderByDescending(c => (c.Kind == CardKind.Ultimate ? 10 : 0) + c.Rank)
                        .ToList();

                    var attackTarget = SelectOptimalTargetForAttack(situation);
                    foreach (var atk in allyAttacks)
                    {
                        if (plan.Actions.Count >= budget) break;
                        string atkTargetId = ResolveTargetForCard(atk, draft.Preview.FindFighter(atk.OwnerFighterId), attackTarget?.FighterId, situation);
                        if (draft.QueuePlay(atk.Id, atkTargetId, out _))
                        {
                            plan.Actions.Add(new PlannedAction { CardId = atk.Id, TargetFighterId = atkTargetId });
                        }
                    }

                    FillRemainingSlotsGreedy(draft, plan, situation, budget);
                    candidates.Add(plan);
                }
            }
        }

        // ──────────────────────────────────────────────────────────
        //  Iterative Beam Search
        // ──────────────────────────────────────────────────────────

        private sealed class BeamNode
        {
            public PlanDraft Draft;
            public CandidatePlan Plan;
            public float HeuristicScore;
        }

        private static void GenerateBeamSearchPlans(BattleSituation situation, BattleState state, List<CandidatePlan> candidates, int budget, int beamWidth)
        {
            var currentLevel = new List<BeamNode>
            {
                new BeamNode
                {
                    Draft = new PlanDraft(state),
                    Plan = new CandidatePlan { StrategyName = "BeamSearch", Description = "Combinatorial plan" },
                    HeuristicScore = 0f
                }
            };

            for (int step = 0; step < budget; step++)
            {
                var nextLevel = new List<BeamNode>();

                foreach (var node in currentLevel)
                {
                    if (node.Draft.RemainingActions == 0)
                    {
                        nextLevel.Add(node);
                        continue;
                    }

                    var hand = node.Draft.Preview.Hand;
                    var livingAllies = node.Draft.Preview.LivingActive();

                    // Explore card plays
                    for (int i = 0; i < hand.Count; i++)
                    {
                        var card = hand[i];
                        var owner = node.Draft.Preview.FindFighter(card.OwnerFighterId);
                        if (owner == null || !owner.IsAlive || owner.IsReserve) continue;
                        var disabled = IsDisabledByStatus(card, owner);
                        if (card.Kind == CardKind.Ultimate && owner.PowerGauge < CardRules.UltimateGaugeCost && !disabled) continue;

                        // Identify target candidates based on TargetScope
                        var targetIds = GetPossibleTargetsForCard(card, node.Draft.Preview, situation);
                        foreach (var targetId in targetIds)
                        {
                            var childDraft = CloneDraftWithPlan(state, node.Plan);
                            if (childDraft.QueuePlay(card.Id, targetId, out _))
                            {
                                var childPlan = node.Plan.Clone();
                                childPlan.Actions.Add(new PlannedAction { CardId = card.Id, TargetFighterId = targetId });

                                float stepScore = disabled ? .25f : EvaluateQuickActionScore(card, targetId, situation);
                                nextLevel.Add(new BeamNode
                                {
                                    Draft = childDraft,
                                    Plan = childPlan,
                                    HeuristicScore = node.HeuristicScore + stepScore
                                });
                            }
                        }
                    }

                    // Explore strategic moves (only if remaining actions allow play after move)
                    if (node.Draft.RemainingActions >= 2)
                    {
                        var merges = MergeScanner.FindMerges(node.Draft.Preview, ConvertToCardOptions(node.Draft.Preview));
                        foreach (var merge in merges.Take(2))
                        {
                            var childDraft = CloneDraftWithPlan(state, node.Plan);
                            if (childDraft.QueueMove(merge.CardA.CardId, merge.MoveToIndex, out _))
                            {
                                var childPlan = node.Plan.Clone();
                                childPlan.Actions.Add(new PlannedAction
                                {
                                    IsMove = true,
                                    CardId = merge.CardA.CardId,
                                    DestinationIndex = merge.MoveToIndex
                                });
                                childPlan.MergesTriggered++;

                                nextLevel.Add(new BeamNode
                                {
                                    Draft = childDraft,
                                    Plan = childPlan,
                                    HeuristicScore = node.HeuristicScore + merge.MergeValue * 2f
                                });
                            }
                        }
                    }
                }

                if (nextLevel.Count == 0) break;

                // Sort by heuristic score descending and prune to beamWidth
                nextLevel.Sort((a, b) => b.HeuristicScore.CompareTo(a.HeuristicScore));
                currentLevel = nextLevel.Take(beamWidth).ToList();
            }

            foreach (var node in currentLevel)
            {
                if (node.Plan.Actions.Count > 0)
                {
                    candidates.Add(node.Plan);
                }
            }
        }

        // ──────────────────────────────────────────────────────────
        //  Helper & Fill Methods
        // ──────────────────────────────────────────────────────────

        private static void FillRemainingSlotsGreedy(PlanDraft draft, CandidatePlan plan, BattleSituation situation, int budget)
        {
            while (draft.RemainingActions > 0 && plan.Actions.Count < budget)
            {
                var availableCards = draft.Preview.Hand.ToList();
                if (availableCards.Count == 0) break;

                var scoredOptions = new List<(CardState card, string targetId, float score)>();

                foreach (var card in availableCards)
                {
                    var owner = draft.Preview.FindFighter(card.OwnerFighterId);
                    if (owner == null || !owner.IsAlive || owner.IsReserve) continue;
                    var disabled = IsDisabledByStatus(card, owner);
                    if (card.Kind == CardKind.Ultimate && owner.PowerGauge < CardRules.UltimateGaugeCost && !disabled) continue;

                    var possibleTargets = GetPossibleTargetsForCard(card, draft.Preview, situation);
                    foreach (var targetId in possibleTargets)
                    {
                        float val = disabled ? .25f : EvaluateQuickActionScore(card, targetId, situation);
                        scoredOptions.Add((card, targetId, val));
                    }
                }

                if (scoredOptions.Count == 0) break;

                scoredOptions.Sort((a, b) => b.score.CompareTo(a.score));

                bool played = false;
                foreach (var opt in scoredOptions)
                {
                    if (draft.QueuePlay(opt.card.Id, opt.targetId, out _))
                    {
                        plan.Actions.Add(new PlannedAction { CardId = opt.card.Id, TargetFighterId = opt.targetId });
                        played = true;
                        break;
                    }
                }

                if (!played) break;
            }
        }

        private static void FillTargetFocusedSlots(PlanDraft draft, CandidatePlan plan, BattleSituation situation, string focusEnemyId, int budget)
        {
            while (draft.RemainingActions > 0 && plan.Actions.Count < budget)
            {
                var hand = draft.Preview.Hand;
                // Prefer attacks from active owners
                var availableCards = hand
                    .Where(c => {
                        var owner = draft.Preview.FindFighter(c.OwnerFighterId);
                        if (owner == null || !owner.IsAlive || owner.IsReserve) return false;
                        if (c.Kind == CardKind.Ultimate && owner.PowerGauge < CardRules.UltimateGaugeCost && !IsDisabledByStatus(c, owner)) return false;
                        return true;
                    })
                    .OrderByDescending(c => (c.Kind == CardKind.Ultimate ? 10 : 0) + c.Rank * 3 + (c.Category == CardCategory.Attack ? 2 : 0))
                    .ToList();

                bool played = false;
                foreach (var card in availableCards)
                {
                    var owner = draft.Preview.FindFighter(card.OwnerFighterId);
                    string targetId = ResolveTargetForCard(card, owner, focusEnemyId, situation);

                    if (draft.QueuePlay(card.Id, targetId, out _))
                    {
                        plan.Actions.Add(new PlannedAction { CardId = card.Id, TargetFighterId = targetId });
                        played = true;
                        break;
                    }
                }

                if (!played) break;
            }
        }

        private static void PadPlanToBudget(CandidatePlan plan, BattleState state, BattleSituation situation)
        {
            if (plan.Actions.Count >= situation.ActionBudget) return;

            var draft = new PlanDraft(state);
            foreach (var act in plan.Actions)
            {
                if (act.IsMove) draft.QueueMove(act.CardId, act.DestinationIndex, out _);
                else draft.QueuePlay(act.CardId, act.TargetFighterId, out _);
            }

            FillRemainingSlotsGreedy(draft, plan, situation, situation.ActionBudget);
        }

        private static float EvaluateQuickActionScore(CardState card, string targetId, BattleSituation situation)
        {
            var owner = situation.OwnFighters.Find(fighter => fighter.FighterId == card.OwnerFighterId)?.State;
            if (IsDisabledByStatus(card, owner)) return .25f;
            var category = CardRules.GetEffectCategory(card);
            float score = card.Rank * 1.5f;
            if (card.Kind == CardKind.Ultimate) score += 5f;

            if (category == CardCategory.Attack || category == CardCategory.AttackDebuff)
            {
                score += 2f;
                if (targetId == situation.Weakest?.FighterId) score += 3f;
                else if (targetId == situation.MostDangerous?.FighterId) score += 2f;
            }
            else if (category == CardCategory.Recovery || category == CardCategory.Buff ||
                     card.TargetScope == EffectTargetScope.SelectedAlly || card.TargetScope == EffectTargetScope.Self)
            {
                var targetAlly = situation.OwnFighters.Find(f => f.FighterId == targetId);
                if (targetAlly != null)
                {
                    score += AllyTargetEvaluator.EvaluateAllyFitness(card, targetAlly, situation);
                }
                else
                {
                    if (category == CardCategory.Recovery)
                        score += situation.AnyOwnFighterLowHp ? 4f : 1f;
                    else
                        score += 2f;
                }
            }
            else if (category == CardCategory.Debuff)
            {
                score += 2f;
            }

            return score;
        }

        private static bool IsDisabledByStatus(CardState card, FighterState owner)
        {
            if (card == null || owner == null) return false;
            EffectDefinition effect;
            var hasEffect = card.Kind == CardKind.Skill
                ? CardRules.TryGetSkill(owner.Definition, card.SkillId, card.Rank, out effect)
                : CardRules.TryGetUltimate(owner.Definition, card.UltimateTier, out effect);
            return hasEffect && StatusSystem.IsCardUseBlocked(owner, CardRules.GetEffectCategory(card), card.Rank,
                card.Kind == CardKind.Ultimate, effect.Sequence != null && effect.Sequence.Count > 0);
        }

        private static List<string> GetPossibleTargetsForCard(CardState card, BattleTeamState friendlyTeam, BattleSituation situation)
        {
            var targets = new List<string>();
            switch (card.TargetScope)
            {
                case EffectTargetScope.Self:
                    targets.Add(card.OwnerFighterId);
                    break;
                case EffectTargetScope.SelectedAlly:
                    foreach (var ally in friendlyTeam.LivingActive()) targets.Add(ally.Id);
                    break;
                case EffectTargetScope.AllAllies:
                    var firstAlly = friendlyTeam.LivingActive().FirstOrDefault();
                    if (firstAlly != null) targets.Add(firstAlly.Id);
                    break;
                case EffectTargetScope.AllEnemies:
                    var firstEnemy = situation.PlayerFighters.FirstOrDefault();
                    if (firstEnemy != null) targets.Add(firstEnemy.FighterId);
                    break;
                default:
                    // Check taunt first
                    var taunter = situation.PlayerFighters.FirstOrDefault(f => f.HasTaunt);
                    if (taunter != null)
                    {
                        targets.Add(taunter.FighterId);
                    }
                    else
                    {
                        foreach (var threat in situation.PlayerFighters) targets.Add(threat.FighterId);
                    }
                    break;
            }
            return targets;
        }

        private static string ResolveTargetForCard(CardState card, FighterState owner, string preferredTargetId, BattleSituation situation)
        {
            if (owner == null) return preferredTargetId;
            switch (card.TargetScope)
            {
                case EffectTargetScope.Self:
                    return owner.Id;
                case EffectTargetScope.SelectedAlly:
                    var ally = situation.OwnFighters.FirstOrDefault(f => f.FighterId == preferredTargetId && f.State.IsAlive && !f.State.IsReserve);
                    return ally?.FighterId ?? AllyTargetEvaluator.SelectBestAllyForCard(card, situation, owner) ?? owner.Id;
                case EffectTargetScope.AllAllies:
                    return owner.Id;
                case EffectTargetScope.AllEnemies:
                    return situation.PlayerFighters.FirstOrDefault()?.FighterId ?? preferredTargetId;
                default:
                    var taunter = situation.PlayerFighters.FirstOrDefault(f => f.HasTaunt);
                    if (taunter != null) return taunter.FighterId;
                    return preferredTargetId ?? situation.Weakest?.FighterId ?? situation.PlayerFighters.FirstOrDefault()?.FighterId;
            }
        }

        private static ThreatProfile SelectOptimalTargetForAttack(BattleSituation situation)
        {
            // If someone has taunt, must target taunter
            var taunter = situation.PlayerFighters.FirstOrDefault(f => f.HasTaunt);
            if (taunter != null) return taunter;

            // Otherwise prioritize killable > weakest > most dangerous
            return situation.PlayerFighters.FirstOrDefault(f => f.EstimatedKillable)
                ?? situation.Weakest
                ?? situation.MostDangerous
                ?? situation.PlayerFighters.FirstOrDefault();
        }

        private static bool HasStanceCancelEffect(CardState card, CharacterDefinition def)
        {
            if (def == null) return false;
            if (CardRules.TryGetSkill(def, card.SkillId, card.Rank, out var effect))
            {
                if (effect.Kind == EffectKind.RemoveStance) return true;
                if (!string.IsNullOrEmpty(effect.KeywordId) &&
                    (effect.KeywordId.IndexOf("stance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     effect.KeywordId.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0))
                    return true;
            }
            return false;
        }

        private static PlanDraft CloneDraftWithPlan(BattleState baseState, CandidatePlan plan)
        {
            var draft = new PlanDraft(baseState);
            foreach (var act in plan.Actions)
            {
                if (act.IsMove) draft.QueueMove(act.CardId, act.DestinationIndex, out _);
                else draft.QueuePlay(act.CardId, act.TargetFighterId, out _);
            }
            return draft;
        }

        private static List<CardOption> ConvertToCardOptions(BattleTeamState team)
        {
            var list = new List<CardOption>();
            for (int i = 0; i < team.Hand.Count; i++)
            {
                var c = team.Hand[i];
                var owner = team.FindFighter(c.OwnerFighterId);
                list.Add(new CardOption
                {
                    CardId = c.Id,
                    Card = c,
                    Owner = owner,
                    HandIndex = i,
                    Rank = c.Rank,
                    Category = c.Category,
                    IsUltimate = c.Kind == CardKind.Ultimate,
                    IsPlayable = owner != null && owner.IsAlive && !owner.IsReserve
                });
            }
            return list;
        }

        private static string FingerprintPlan(CandidatePlan plan)
        {
            return string.Join(";", plan.Actions.Select(a => a.IsMove ? $"M:{a.CardId}:{a.DestinationIndex}" : $"P:{a.CardId}:{a.TargetFighterId}"));
        }
    }
}
