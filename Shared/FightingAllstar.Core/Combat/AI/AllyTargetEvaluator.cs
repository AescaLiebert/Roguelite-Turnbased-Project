using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat.AI
{
    /// <summary>
    /// Specialized tactical decision evaluator for single-target ally buffs and heals.
    /// Determines who is the best ally to receive a heal, shield, buff, or cleanse card.
    /// </summary>
    public static class AllyTargetEvaluator
    {
        /// <summary>
        /// Selects the best ally ID for a given card based on tactical priorities.
        /// </summary>
        public static string SelectBestAllyForCard(CardState card, BattleSituation situation, FighterState owner, List<PlannedAction> currentPlan = null)
        {
            if (situation == null || situation.OwnFighters == null || situation.OwnFighters.Count == 0)
                return owner?.Id;

            FighterProfile bestAlly = null;
            float bestScore = float.MinValue;

            foreach (var ally in situation.OwnFighters)
            {
                if (ally == null || !ally.State.IsAlive || ally.State.IsReserve) continue;

                float score = EvaluateAllyFitness(card, ally, situation, currentPlan);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestAlly = ally;
                }
            }

            return bestAlly?.FighterId ?? owner?.Id;
        }

        /// <summary>
        /// Scores how suitable a specific ally is to receive this card.
        /// Higher score = much better recipient.
        /// </summary>
        public static float EvaluateAllyFitness(CardState card, FighterProfile ally, BattleSituation situation, List<PlannedAction> currentPlan = null)
        {
            if (card == null || ally == null) return 0f;

            var category = CardRules.GetEffectCategory(card);
            var owner = situation.OwnFighters.Find(f => f.FighterId == card.OwnerFighterId)?.State;

            EffectDefinition effect = null;
            if (owner?.Definition != null)
            {
                if (card.Kind == CardKind.Skill)
                    CardRules.TryGetSkill(owner.Definition, card.SkillId, card.Rank, out effect);
                else
                    CardRules.TryGetUltimate(owner.Definition, card.UltimateTier, out effect);
            }

            if (category == CardCategory.Recovery || (effect != null && (effect.Kind == EffectKind.Heal || effect.Kind == EffectKind.Cleanse || effect.Kind == EffectKind.RemoveDebuffs)))
            {
                return EvaluateHealFitness(ally, situation, effect);
            }

            if (category == CardCategory.Buff || (effect != null && effect.Kind == EffectKind.ApplyStatus && effect.StatusRecipe?.Polarity == StatusPolarity.Buff))
            {
                return EvaluateBuffFitness(card, ally, situation, effect, currentPlan);
            }

            // Fallback for general ally-targeted skills
            float baseScore = 2.0f;
            if (ally.Role == FighterRole.Dps) baseScore += 1.0f;
            return baseScore;
        }

        // ──────────────────────────────────────────────────────────
        //  Heal & Recovery Fitness
        // ──────────────────────────────────────────────────────────

        private static float EvaluateHealFitness(FighterProfile ally, BattleSituation situation, EffectDefinition effect)
        {
            float score = 0f;
            float hpRatio = ally.HealthRatio;
            float missingHpRatio = 1.0f - hpRatio;

            // 1. Missing Health Factor & Overheal Avoidance
            if (hpRatio >= 0.98f)
            {
                // Severe penalty for healing an ally who is already at 100% health
                score -= 6.0f;
            }
            else if (hpRatio <= 0.35f)
            {
                // Critical danger! Top priority to save ally from elimination
                score += 7.0f + (0.35f - hpRatio) * 12.0f;
            }
            else if (hpRatio <= 0.70f)
            {
                // Solid missing health recovery
                score += 3.5f + missingHpRatio * 4.0f;
            }
            else
            {
                // Light chip damage (70% - 98%)
                score += missingHpRatio * 2.5f;
            }

            // 2. Debuff Cleanse Synergy
            int debuffCount = CountDebuffs(ally.State);
            bool hasHardCc = HasHardControl(ally.State);

            bool cleanses = effect != null && (effect.Kind == EffectKind.Cleanse || effect.Kind == EffectKind.RemoveDebuffs ||
                (effect.Sequence != null && effect.Sequence.Any(s => s.Effect?.Kind == EffectKind.Cleanse || s.Effect?.Kind == EffectKind.RemoveDebuffs)));

            if (cleanses)
            {
                if (hasHardCc)
                {
                    // Un-stunning/un-freezing an ally is huge tactical value
                    score += 5.5f;
                }
                else if (debuffCount > 0)
                {
                    score += debuffCount * 1.5f;
                }
            }

            // 3. Strategic Importance of the Ally
            if (ally.UltimateReady)
            {
                // Protect the fighter holding an Ultimate ready to win
                score += 3.0f;
            }
            else if (ally.PowerGauge >= 4)
            {
                score += 1.5f;
            }

            if (ally.Role == FighterRole.Dps)
            {
                score += 1.5f; // Protect main attacker
            }

            // If ally is currently Taunting, they are taking all enemy single-target fire
            if (ally.State.Statuses.Instances.Any(s => s.Recipe != null && s.Recipe.HasTaunt))
            {
                score += 2.5f;
            }

            return score;
        }

        // ──────────────────────────────────────────────────────────
        //  Buff Fitness
        // ──────────────────────────────────────────────────────────

        private static float EvaluateBuffFitness(CardState card, FighterProfile ally, BattleSituation situation, EffectDefinition effect, List<PlannedAction> currentPlan)
        {
            float score = 2.0f;
            var recipe = effect?.StatusRecipe;

            bool isOffensive = IsOffensiveBuff(effect, recipe);
            bool isDefensive = IsDefensiveBuff(effect, recipe);

            // 1. Offensive Buff Logic (Attack Up, Pierce, Crit Up, Damage Up)
            if (isOffensive)
            {
                // Best targets: DPS / Carries
                if (ally.Role == FighterRole.Dps)
                {
                    score += 3.5f;
                }
                else if (ally.AttackCardCount > 0)
                {
                    score += 1.8f;
                }
                else if (ally.Role == FighterRole.Tank)
                {
                    score -= 2.0f; // Wasteful to put offensive buffs on pure tanks with no attacks
                }

                // Combo Multiplier: Is this ally attacking this turn?
                bool isAttackingThisTurn = false;
                if (currentPlan != null)
                {
                    isAttackingThisTurn = currentPlan.Any(a => !a.IsMove &&
                        situation.OwnHand.Any(c => c.CardId == a.CardId && c.Card.OwnerFighterId == ally.FighterId &&
                        (c.Category == CardCategory.Attack || c.Category == CardCategory.AttackDebuff)));
                }

                if (isAttackingThisTurn)
                {
                    score += 4.0f; // Signature 7DSGC synergy: buffing right before strike!
                }

                if (ally.UltimateReady)
                {
                    score += 3.0f; // Empowering incoming ultimate
                }
            }

            // 2. Defensive Buff Logic (Defense Up, Shield, Damage Reduction, Evade)
            if (isDefensive)
            {
                // Best targets: critically injured allies or taunters
                if (ally.HealthRatio <= 0.4f)
                {
                    score += 4.5f; // Lifesaver shield/defense
                }
                else if (ally.HealthRatio <= 0.7f)
                {
                    score += (1.0f - ally.HealthRatio) * 3.5f;
                }

                if (ally.State.Statuses.Instances.Any(s => s.Recipe != null && s.Recipe.HasTaunt))
                {
                    score += 4.0f; // Taunter absorbs incoming damage, so defense/shield value is maximized!
                }
            }

            // 3. Debuff Immunity Buff
            if (recipe != null && recipe.DebuffImmunity)
            {
                if (ally.Role == FighterRole.Dps || ally.UltimateReady)
                {
                    score += 3.0f; // Keep carry immune to CC
                }
            }

            // 4. Duplicate Buff Penalty (avoid refreshing if already active with multiple turns)
            if (recipe != null && HasActiveDuplicateBuff(ally.State, recipe.Id))
            {
                score -= 3.5f;
            }

            return score;
        }

        // ──────────────────────────────────────────────────────────
        //  Helper Queries
        // ──────────────────────────────────────────────────────────

        private static bool IsOffensiveBuff(EffectDefinition effect, StatusRecipeDefinition recipe)
        {
            if (recipe != null)
            {
                if (recipe.Modifiers != null && recipe.Modifiers.Any(m =>
                    m.Stat == StatId.Attack || m.Stat == StatId.Pierce || m.Stat == StatId.CritChance ||
                    m.Stat == StatId.CritDamage || m.Target == ModifierTarget.AnyDamageDealt ||
                    m.Target == ModifierTarget.UltimateDamageDealt ||
                    m.Target == ModifierTarget.FamilyDamageDealt))
                {
                    return true;
                }

                var lowerId = recipe.Id?.ToLowerInvariant() ?? string.Empty;
                if (lowerId.Contains("attack") || lowerId.Contains("atk") || lowerId.Contains("damage") || lowerId.Contains("crit"))
                    return true;
            }

            return false;
        }

        private static bool IsDefensiveBuff(EffectDefinition effect, StatusRecipeDefinition recipe)
        {
            if (recipe != null)
            {
                if (recipe.PeriodicHealing != null || recipe.EvadeAttacks || recipe.SurviveLethalCharges > 0)
                    return true;

                if (recipe.Modifiers != null && recipe.Modifiers.Any(m =>
                    m.Stat == StatId.Defense || m.Stat == StatId.Resistance || m.Stat == StatId.CritDefense ||
                    m.Stat == StatId.CritResistance || m.Stat == StatId.MaxHealth ||
                    m.Target == ModifierTarget.AnyDamageReceived || m.Target == ModifierTarget.FamilyDamageReceived ||
                    m.Target == ModifierTarget.FinalDamageReduction || m.Target == ModifierTarget.FlatDamageReduction))
                {
                    return true;
                }

                var lowerId = recipe.Id?.ToLowerInvariant() ?? string.Empty;
                if (lowerId.Contains("defense") || lowerId.Contains("def") || lowerId.Contains("shield") || lowerId.Contains("heal") || lowerId.Contains("rejuv"))
                    return true;
            }

            return false;
        }

        private static int CountDebuffs(FighterState fighter)
        {
            if (fighter?.Statuses?.Instances == null) return 0;
            return fighter.Statuses.Instances.Count(s => s.Recipe != null && s.Recipe.Polarity == StatusPolarity.Debuff);
        }

        private static bool HasHardControl(FighterState fighter)
        {
            if (fighter?.Statuses?.Instances == null) return false;
            return fighter.Statuses.Instances.Any(s => s.Recipe != null &&
                ((s.Recipe.Behavior & StatusBehavior.Disable) != 0 ||
                 (s.Recipe.DisableMask != CardCategoryMask.None) ||
                 s.Recipe.Tags.Contains(CombatTags.Stun) ||
                 s.Recipe.Tags.Contains(CombatTags.Paralyze) ||
                 s.Recipe.Tags.Contains(CombatTags.Seal)));
        }

        private static bool HasActiveDuplicateBuff(FighterState fighter, string recipeId)
        {
            if (fighter?.Statuses?.Instances == null || string.IsNullOrEmpty(recipeId)) return false;
            return fighter.Statuses.Instances.Any(s => s.Recipe != null && s.Recipe.Id == recipeId && s.RemainingDuration > 1);
        }
    }
}
