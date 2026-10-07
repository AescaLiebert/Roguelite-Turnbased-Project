using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    public sealed class AttackEffectCalculation
    {
        public StatBlock Attacker;
        public StatBlock Defender;
        public DamagePolicy Policy;
        public List<string> TriggeredPassiveReactionIds = new List<string>();
        public int KeywordFactorBp = 10000;
        public bool RemoveTargetBuffsBeforeDamage;
        public bool RemoveTargetStancesBeforeDamage;
    }

    /// <summary>Builds a local damage-calculation view for source-backed attack keywords.</summary>
    public static class AttackEffectSystem
    {
        public static AttackEffectCalculation Prepare(AttackEffectRecipeDefinition recipe, BattleState battle,
            FighterState source, FighterState target, DamageFamily family, CardState card = null)
        {
            var result = new AttackEffectCalculation { Attacker = StatusSystem.GetEffectiveStats(source),
                Defender = StatusSystem.GetEffectiveStats(target), Policy = StatusSystem.BuildDamagePolicy(source, target, family,
                    card?.Kind == CardKind.Ultimate) };
            CharacterPassiveRuntime.ApplyBeforeDamageReactions(battle, source, target, family, card, result);
            if (recipe == null) return result;
            switch (recipe.Kind)
            {
                case AttackEffectKind.Charge: result.Defender.Defense = 0; break;
                case AttackEffectKind.Shatter: result.Defender.ResistanceBp = 0; break;
                case AttackEffectKind.Rupture:
                    if (StatusSystem.Count(target, StatusPolarity.Buff) > 0) result.KeywordFactorBp = recipe.PrimaryValueBp; break;
                case AttackEffectKind.Detonate:
                    result.KeywordFactorBp = AddScaled(result.KeywordFactorBp, Math.Max(0, target?.PowerGauge ?? 0), recipe.PrimaryValueBp); break;
                case AttackEffectKind.Blaze:
                    result.KeywordFactorBp = AddScaled(result.KeywordFactorBp, StatusSystem.Count(target, requiredTag: CombatTags.Ignite), recipe.PrimaryValueBp); break;
                case AttackEffectKind.Clash:
                    result.KeywordFactorBp = AddScaled(result.KeywordFactorBp, Math.Max(0, source?.PowerGauge ?? 0), recipe.PrimaryValueBp); break;
                case AttackEffectKind.CoDestruction:
                    result.KeywordFactorBp = AddScaled(result.KeywordFactorBp, StatusSystem.Count(target, StatusPolarity.Debuff), recipe.PrimaryValueBp); break;
                case AttackEffectKind.Sever:
                    result.Attacker.CritChanceBp = MultiplyBp(result.Attacker.CritChanceBp, recipe.PrimaryValueBp); break;
                case AttackEffectKind.Spike:
                    result.Attacker.CritDamageBp = MultiplyBp(result.Attacker.CritDamageBp, recipe.PrimaryValueBp); break;
                case AttackEffectKind.Breakthrough:
                    result.Defender.Defense = result.Defender.ResistanceBp = result.Defender.CritResistanceBp = result.Defender.CritDefenseBp = 0;
                    break;
                case AttackEffectKind.Pierce:
                    result.Attacker.PierceBp = MultiplyBp(result.Attacker.PierceBp, recipe.PrimaryValueBp); break;
                case AttackEffectKind.Weakpoint:
                    if (StatusSystem.Count(target, StatusPolarity.Debuff) > 0) result.KeywordFactorBp = recipe.PrimaryValueBp; break;
                case AttackEffectKind.Flood:
                    result.KeywordFactorBp += ScaleByHealthRatio(source, recipe.PrimaryValueBp); break;
                case AttackEffectKind.PowerStrike:
                    result.KeywordFactorBp += Math.Max(0, result.Defender.ResistanceBp); break;
                case AttackEffectKind.Cleave:
                    result.KeywordFactorBp = (int)Math.Min(int.MaxValue, (long)result.KeywordFactorBp +
                        (long)Math.Max(0, result.Attacker.CritDamageBp - 10000) * recipe.PrimaryValueBp / 10000);
                    result.Policy.CannotCrit = true;
                    break;
                case AttackEffectKind.Quell:
                    result.KeywordFactorBp = AddScaled(result.KeywordFactorBp, CountTag(source, CombatTags.Stance), recipe.PrimaryValueBp); break;
                case AttackEffectKind.Amplify:
                    result.KeywordFactorBp = AddScaled(result.KeywordFactorBp, CountNormalBuffs(source), recipe.PrimaryValueBp); break;
                case AttackEffectKind.SecretTechnique:
                    result.KeywordFactorBp = AddScaled(result.KeywordFactorBp, CountSourceCards(battle, source), recipe.PrimaryValueBp); break;
                case AttackEffectKind.RemoveBuff: result.RemoveTargetBuffsBeforeDamage = true; break;
                case AttackEffectKind.CancelStance: result.RemoveTargetStancesBeforeDamage = true; break;
            }
            return result;
        }

        private static int CountTag(FighterState fighter, string tag) => StatusSystem.Count(fighter, requiredTag: tag);

        private static int CountNormalBuffs(FighterState fighter)
        {
            var count = 0;
            var statuses = fighter?.Statuses?.Instances;
            if (statuses == null) return count;
            foreach (var status in statuses)
                if (status?.Recipe != null && status.Recipe.Polarity == StatusPolarity.Buff &&
                    status.Recipe.Color == StatusColor.Normal) count++;
            return count;
        }

        private static int CountSourceCards(BattleState battle, FighterState source)
        {
            if (battle == null || source == null) return 0;
            var count = 0;
            foreach (var card in battle.Team(source.Side).Hand)
                if (card != null && card.OwnerFighterId == source.Id) count++;
            return count;
        }

        private static int ScaleByHealthRatio(FighterState fighter, int valueBp)
        {
            if (fighter == null) return 0;
            var maxHealth = Math.Max(1, StatusSystem.GetEffectiveStats(fighter).MaxHealth);
            return (int)Math.Min(int.MaxValue, (long)Math.Max(0, fighter.Health) * valueBp / maxHealth);
        }

        private static int MultiplyBp(int value, int multiplierBp) =>
            (int)Math.Min(int.MaxValue, (long)Math.Max(0, value) * Math.Max(0, multiplierBp) / 10000);

        private static int AddScaled(int current, int count, int amount) =>
            (int)Math.Min(int.MaxValue, Math.Max(0L, (long)current + (long)Math.Max(0, count) * amount));
    }
}
