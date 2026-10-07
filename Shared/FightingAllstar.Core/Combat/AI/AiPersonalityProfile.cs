using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat.AI
{
    public enum AiPersonality { Reckless, Defensive, Tactical, Genius }

    /// <summary>
    /// Utility weights that shape how the AI scores candidate plans.
    /// Derived per-character from the team's kit composition.
    /// </summary>
    public sealed class AiPersonalityProfile
    {
        public AiPersonality Personality;

        // --- Scoring weights (all positive = desirable, negative = penalty) ---
        public float DamageDealt     = 1.0f;
        public float KillPotential   = 2.5f;
        public float HealValue       = 1.2f;
        public float DebuffValue     = 0.8f;
        public float BuffValue       = 0.6f;
        public float MergeValue      = 1.5f;
        public float UltimateUsed    = 1.8f;
        public float GaugeEfficiency = 0.4f;
        public float ThreatReduction = 1.3f;
        public float SurvivePenalty  = -3.0f;
        public float WastedSlots     = -0.5f;

        // --- Search parameters ---
        public int MaxCandidates   = 30;
        public int BeamWidth       = 6;
        public int SimulationDepth = 5;     // Top N candidates to validate
        public float NoisePercent  = 0.03f; // ±3% randomness to avoid robotic play

        /// <summary>
        /// Derive an AI personality profile from the team's character kits.
        /// Analyzes each fighter's skills, role, and base stats to produce
        /// a blended set of weights that reflect the team's natural playstyle.
        /// </summary>
        public static AiPersonalityProfile DeriveFromTeam(BattleTeamState team)
        {
            var profile = new AiPersonalityProfile();
            if (team == null) return profile;

            var living = team.LivingActive();
            if (living.Count == 0) return profile;

            int totalAttackCards = 0, totalRecoveryCards = 0, totalDebuffCards = 0;
            float totalAttackStat = 0f, totalDefenseStat = 0f, totalHealthStat = 0f;

            foreach (var fighter in living)
            {
                var def = fighter.Definition;
                if (def == null) continue;

                var stats = def.BaseStats ?? new StatBlock();
                totalAttackStat += stats.Attack;
                totalDefenseStat += stats.Defense;
                totalHealthStat += stats.MaxHealth;

                if (def.Skills == null) continue;
                foreach (var skill in def.Skills)
                {
                    var category = CardRules.ResolveCategory(skill);
                    switch (category)
                    {
                        case CardCategory.Attack:
                            totalAttackCards++;
                            break;
                        case CardCategory.AttackDebuff:
                            totalAttackCards++;
                            totalDebuffCards++;
                            break;
                        case CardCategory.Recovery:
                            totalRecoveryCards++;
                            break;
                        case CardCategory.Buff:
                            totalRecoveryCards++; // Buff and heal share defensive intent
                            break;
                        case CardCategory.Debuff:
                            totalDebuffCards++;
                            break;
                    }
                }
            }

            int totalCards = totalAttackCards + totalRecoveryCards + totalDebuffCards;
            if (totalCards == 0) totalCards = 1;

            float attackRatio   = (float)totalAttackCards / totalCards;
            float recoveryRatio = (float)totalRecoveryCards / totalCards;
            float debuffRatio   = (float)totalDebuffCards / totalCards;

            // Classify the dominant personality
            if (debuffRatio > 0.3f)
                profile.Personality = AiPersonality.Tactical;
            else if (recoveryRatio > 0.3f)
                profile.Personality = AiPersonality.Defensive;
            else if (attackRatio > 0.6f)
                profile.Personality = AiPersonality.Reckless;
            else
                profile.Personality = AiPersonality.Genius;

            // Apply personality-specific weight adjustments
            switch (profile.Personality)
            {
                case AiPersonality.Reckless:
                    profile.DamageDealt     = 1.6f;
                    profile.KillPotential   = 3.0f;
                    profile.HealValue       = 0.5f;
                    profile.DebuffValue     = 0.4f;
                    profile.MergeValue      = 1.8f;  // Merge for bigger hits
                    profile.UltimateUsed    = 2.2f;
                    profile.ThreatReduction = 0.8f;
                    profile.SurvivePenalty  = -1.5f;  // Doesn't care much about survival
                    profile.NoisePercent    = 0.05f;
                    profile.BeamWidth       = 4;
                    break;

                case AiPersonality.Defensive:
                    profile.DamageDealt     = 0.8f;
                    profile.KillPotential   = 1.5f;
                    profile.HealValue       = 2.0f;
                    profile.DebuffValue     = 0.6f;
                    profile.BuffValue       = 1.2f;
                    profile.MergeValue      = 1.2f;
                    profile.UltimateUsed    = 1.4f;
                    profile.ThreatReduction = 1.0f;
                    profile.SurvivePenalty  = -4.0f;  // Strongly avoids letting allies die
                    profile.NoisePercent    = 0.02f;
                    profile.BeamWidth       = 5;
                    break;

                case AiPersonality.Tactical:
                    profile.DamageDealt     = 1.0f;
                    profile.KillPotential   = 2.0f;
                    profile.HealValue       = 1.0f;
                    profile.DebuffValue     = 1.8f;
                    profile.BuffValue       = 0.8f;
                    profile.MergeValue      = 1.6f;
                    profile.UltimateUsed    = 1.6f;
                    profile.ThreatReduction = 1.8f;  // Prioritizes shutting down threats
                    profile.SurvivePenalty  = -2.5f;
                    profile.NoisePercent    = 0.02f;
                    profile.BeamWidth       = 6;
                    break;

                case AiPersonality.Genius:
                    // Balanced with slightly boosted merge and search depth
                    profile.DamageDealt     = 1.2f;
                    profile.KillPotential   = 2.5f;
                    profile.HealValue       = 1.2f;
                    profile.DebuffValue     = 1.0f;
                    profile.BuffValue       = 0.8f;
                    profile.MergeValue      = 2.0f;
                    profile.UltimateUsed    = 1.8f;
                    profile.GaugeEfficiency = 0.6f;
                    profile.ThreatReduction = 1.5f;
                    profile.SurvivePenalty  = -3.0f;
                    profile.NoisePercent    = 0.01f;  // Very consistent
                    profile.BeamWidth       = 8;
                    profile.SimulationDepth = 8;
                    break;
            }

            // Stat-based fine tuning: if the team has high attack, boost damage weights further
            float avgAttack = totalAttackStat / living.Count;
            float avgDefense = totalDefenseStat / living.Count;
            if (avgAttack > avgDefense * 1.5f)
            {
                profile.DamageDealt *= 1.15f;
                profile.KillPotential *= 1.1f;
            }
            else if (avgDefense > avgAttack * 1.2f)
            {
                profile.HealValue *= 1.15f;
                profile.SurvivePenalty *= 1.1f;
            }

            return profile;
        }
    }
}
