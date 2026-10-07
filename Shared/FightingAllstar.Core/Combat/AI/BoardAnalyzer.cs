using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat.AI
{
    /// <summary>
    /// Phase 1: Reads BattleState and produces a <see cref="BattleSituation"/> snapshot.
    /// The AI never peeks at the player's hand — only visible board state.
    /// </summary>
    public static class BoardAnalyzer
    {
        private const float LowHpThreshold = 0.35f;

        public static BattleSituation Analyze(BattleState state)
        {
            var situation = new BattleSituation();
            if (state == null) return situation;

            var aiTeam     = state.Team(state.ActingSide);
            var playerTeam = state.OtherTeam(state.ActingSide);

            situation.ActionBudget = state.ActionBudget;
            situation.TurnNumber   = state.TurnNumber;

            // --- Analyze own fighters ---
            AnalyzeOwnTeam(aiTeam, situation);

            // --- Analyze own hand ---
            AnalyzeOwnHand(aiTeam, situation);

            // --- Analyze player fighters (no hand peeking) ---
            AnalyzePlayerTeam(playerTeam, aiTeam, situation);

            // --- Scan for merge opportunities ---
            situation.MergeCandidates = MergeScanner.FindMerges(aiTeam, situation.OwnHand);

            // --- Tactical context ---
            situation.IsLosingFight = situation.OwnTeamHealthRatio < situation.PlayerTeamHealthRatio - 0.15f;
            situation.AnyOwnFighterLowHp = situation.OwnFighters.Exists(f => f.HealthRatio < LowHpThreshold);

            int debuffs = 0;
            foreach (var threat in situation.PlayerFighters)
                debuffs += threat.DebuffCount;
            situation.ActiveDebuffsOnPlayer = debuffs;

            int buffs = 0;
            foreach (var own in situation.OwnFighters)
                foreach (var status in own.State.Statuses.Instances)
                    if (status.Recipe != null && status.Recipe.Polarity == StatusPolarity.Buff) buffs++;
            situation.ActiveBuffsOnSelf = buffs;

            return situation;
        }

        // ───── Own team analysis ─────

        private static void AnalyzeOwnTeam(BattleTeamState team, BattleSituation situation)
        {
            var living = team.LivingActive();
            float totalHpRatio = 0f;

            foreach (var fighter in living)
            {
                var stats = StatusSystem.GetEffectiveStats(fighter);
                float hpRatio = stats.MaxHealth > 0 ? (float)fighter.Health / stats.MaxHealth : 0f;
                totalHpRatio += hpRatio;

                var role = ClassifyRole(fighter.Definition);

                var profile = new FighterProfile
                {
                    FighterId = fighter.Id,
                    State = fighter,
                    EffectiveStats = stats,
                    HealthRatio = hpRatio,
                    PowerGauge = fighter.PowerGauge,
                    UltimateReady = fighter.PowerGauge >= CardRules.UltimateGaugeCost,
                    Role = role
                };

                if (profile.UltimateReady) situation.AnyUltimateReady = true;
                situation.OwnFighters.Add(profile);
            }

            situation.OwnTeamHealthRatio = living.Count > 0 ? totalHpRatio / living.Count : 0f;
        }

        // ───── Own hand analysis ─────

        private static void AnalyzeOwnHand(BattleTeamState team, BattleSituation situation)
        {
            for (int i = 0; i < team.Hand.Count; i++)
            {
                var card = team.Hand[i];
                var owner = team.FindFighter(card.OwnerFighterId);

                bool playable = owner != null && owner.IsAlive && !owner.IsReserve;
                bool disabled = false;
                if (playable)
                {
                    EffectDefinition effect;
                    var hasEffect = card.Kind == CardKind.Skill
                        ? CardRules.TryGetSkill(owner.Definition, card.SkillId, card.Rank, out effect)
                        : CardRules.TryGetUltimate(owner.Definition, card.UltimateTier, out effect);
                    disabled = hasEffect && StatusSystem.IsCardUseBlocked(owner, CardRules.GetEffectCategory(card), card.Rank,
                        card.Kind == CardKind.Ultimate, effect.Sequence != null && effect.Sequence.Count > 0);
                }
                if (card.Kind == CardKind.Ultimate && (owner == null || owner.PowerGauge < CardRules.UltimateGaugeCost) && !disabled)
                    playable = false;
                var option = new CardOption
                {
                    CardId = card.Id,
                    Card = card,
                    Owner = owner,
                    HandIndex = i,
                    Category = CardRules.GetEffectCategory(card),
                    TargetScope = card.TargetScope,
                    Rank = card.Rank,
                    IsUltimate = card.Kind == CardKind.Ultimate,
                    IsPlayable = playable,
                    IsDisabled = disabled,
                    BaseUtility = EstimateCardBaseUtility(card, owner)
                };

                // Update fighter profile card counts
                var fighterProfile = situation.OwnFighters.Find(f => f.FighterId == card.OwnerFighterId);
                if (fighterProfile != null)
                {
                    switch (CardRules.GetEffectCategory(card))
                    {
                        case CardCategory.Attack:
                        case CardCategory.AttackDebuff:
                            fighterProfile.AttackCardCount++;
                            break;
                        case CardCategory.Recovery:
                            fighterProfile.RecoveryCardCount++;
                            break;
                        case CardCategory.Debuff:
                            fighterProfile.DebuffCardCount++;
                            break;
                    }
                }

                situation.OwnHand.Add(option);
            }

            // Mark cards that can merge
            for (int i = 0; i < situation.OwnHand.Count; i++)
            {
                var a = situation.OwnHand[i];
                if (a.IsUltimate || a.Rank >= 3) continue;
                for (int j = i + 1; j < situation.OwnHand.Count; j++)
                {
                    var b = situation.OwnHand[j];
                    if (b.IsUltimate || b.Rank >= 3) continue;
                    if (a.Card.OwnerFighterId == b.Card.OwnerFighterId &&
                        a.Card.SkillId == b.Card.SkillId && a.Rank == b.Rank)
                    {
                        a.CanMerge = true;
                        b.CanMerge = true;
                    }
                }
            }
        }

        // ───── Player team analysis (no hand peeking!) ─────

        private static void AnalyzePlayerTeam(BattleTeamState playerTeam, BattleTeamState aiTeam, BattleSituation situation)
        {
            var living = playerTeam.LivingActive();
            float totalHpRatio = 0f;
            ThreatProfile weakest = null;
            ThreatProfile mostDangerous = null;
            float highestThreat = -1f;

            // Estimate total AI attack power for kill estimation
            int totalAiAttack = 0;
            foreach (var f in aiTeam.LivingActive())
            {
                var s = StatusSystem.GetEffectiveStats(f);
                totalAiAttack += s.Attack;
            }

            foreach (var fighter in living)
            {
                var stats = StatusSystem.GetEffectiveStats(fighter);
                float hpRatio = stats.MaxHealth > 0 ? (float)fighter.Health / stats.MaxHealth : 0f;
                totalHpRatio += hpRatio;

                bool hasStance = false, hasTaunt = false;
                int debuffCount = 0;
                foreach (var status in fighter.Statuses.Instances)
                {
                    if (status.Recipe == null) continue;
                    if ((status.Recipe.Behavior & StatusBehavior.Stance) != 0 ||
                        status.Recipe.Tags.Contains(CombatTags.Stance))
                        hasStance = true;
                    if (status.Recipe.HasTaunt) hasTaunt = true;
                    if (status.Recipe.Polarity == StatusPolarity.Debuff) debuffCount++;
                }

                // Threat estimate: based on attack stat and ultimate readiness
                float threatScore = stats.Attack;
                if (fighter.PowerGauge >= CardRules.UltimateGaugeCost)
                    threatScore *= 1.5f;   // Ultimate ready = very dangerous
                if (fighter.PowerGauge >= 3)
                    threatScore *= 1.15f;  // Close to ultimate
                var role = ClassifyRole(fighter.Definition);
                if (role == FighterRole.Support || role == FighterRole.Debuffer)
                    threatScore *= 1.2f;   // Support/debuffers are high-value targets

                // Kill estimation: rough check if AI can deal enough damage
                int estDamageToKill = fighter.Health + fighter.Shield;
                bool estimatedKillable = totalAiAttack * 2 >= estDamageToKill; // Very rough

                var threat = new ThreatProfile
                {
                    FighterId = fighter.Id,
                    State = fighter,
                    EffectiveStats = stats,
                    HealthRatio = hpRatio,
                    PowerGauge = fighter.PowerGauge,
                    UltimateReady = fighter.PowerGauge >= CardRules.UltimateGaugeCost,
                    EstimatedDps = threatScore,
                    HasStance = hasStance,
                    HasTaunt = hasTaunt,
                    DebuffCount = debuffCount,
                    EstimatedKillable = estimatedKillable,
                    EstimatedDamageToKill = estDamageToKill
                };

                if (threat.UltimateReady) situation.PlayerHasUltimateReady = true;
                if (weakest == null || threat.HealthRatio < weakest.HealthRatio) weakest = threat;
                if (threatScore > highestThreat) { highestThreat = threatScore; mostDangerous = threat; }

                situation.PlayerFighters.Add(threat);
            }

            situation.Weakest = weakest;
            situation.MostDangerous = mostDangerous;
            situation.PlayerTeamHealthRatio = living.Count > 0 ? totalHpRatio / living.Count : 0f;
            situation.CanKillAnyoneThisTurn = situation.PlayerFighters.Exists(t => t.EstimatedKillable);
        }

        // ───── Utilities ─────

        private static FighterRole ClassifyRole(CharacterDefinition def)
        {
            if (def == null || def.Skills == null) return FighterRole.Hybrid;

            int attack = 0, recovery = 0, debuff = 0;
            foreach (var skill in def.Skills)
            {
                var cat = CardRules.ResolveCategory(skill);
                switch (cat)
                {
                    case CardCategory.Attack: attack++; break;
                    case CardCategory.AttackDebuff: attack++; debuff++; break;
                    case CardCategory.Recovery:
                    case CardCategory.Buff: recovery++; break;
                    case CardCategory.Debuff: debuff++; break;
                }
            }

            // Check role string from definition
            if (!string.IsNullOrEmpty(def.Role))
            {
                var lower = def.Role.ToLowerInvariant();
                if (lower.Contains("tank") || lower.Contains("hp")) return FighterRole.Tank;
                if (lower.Contains("support") || lower.Contains("heal")) return FighterRole.Support;
                if (lower.Contains("debuff") || lower.Contains("control")) return FighterRole.Debuffer;
            }

            if (recovery >= 2) return FighterRole.Support;
            if (debuff >= 2) return FighterRole.Debuffer;
            if (attack >= 2) return FighterRole.Dps;

            // Stat-based fallback
            var stats = def.BaseStats;
            if (stats != null && stats.MaxHealth > stats.Attack * 2) return FighterRole.Tank;

            return FighterRole.Hybrid;
        }

        private static float EstimateCardBaseUtility(CardState card, FighterState owner)
        {
            float utility = 1f;

            // Rank multiplier
            utility *= (1f + (card.Rank - 1) * 0.5f); // R1=1, R2=1.5, R3=2.0

            // Ultimate is high value
            if (card.Kind == CardKind.Ultimate) utility *= 2.5f;

            // Category value
            switch (CardRules.GetEffectCategory(card))
            {
                case CardCategory.Attack: utility *= 1.0f; break;
                case CardCategory.AttackDebuff: utility *= 1.3f; break;
                case CardCategory.Recovery: utility *= 0.8f; break;
                case CardCategory.Buff: utility *= 0.7f; break;
                case CardCategory.Debuff: utility *= 0.9f; break;
            }

            return utility;
        }
    }
}
