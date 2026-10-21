using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    [Serializable]
    public sealed class AbilityVariantState
    {
        public string SkillId;
        public string VariantId;
        public bool Unlocked;
        public AbilityVariantState Clone() => (AbilityVariantState)MemberwiseClone();
    }

    /// <summary>Variant readiness changes presentation metadata, never a card's stable identity or rank.</summary>
    public static class AbilityVariantSystem
    {
        /// <summary>Pure authored-configuration preview. Readiness/resources are battle state and deliberately excluded.</summary>
        public static EffectDefinition ResolveAuthored(SkillDefinition skill, int level, int rank, int tier, string variantId = "")
        {
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            var variant = string.IsNullOrEmpty(variantId) ? null : skill.Variants?.Find(v => v.Id == variantId);
            if (!string.IsNullOrEmpty(variantId) && variant == null) throw new InvalidOperationException("Unknown authored variant " + variantId);
            if (variant != null && (level < variant.MinimumLevel || level > variant.MaximumLevel || tier < variant.MinimumTier || tier > variant.MaximumTier))
                throw new InvalidOperationException("Variant is outside its authored level/tier range.");
            var data = (variant?.Ranks ?? skill.Ranks)?.Find(r => r.Rank == rank);
            if (data?.Effect == null) throw new InvalidOperationException("Selected authored rank has no effect.");
            var effect = data.Effect.Clone();
            var fighter = new FighterState { Level = level, ConstellationTier = tier };
            ApplyOverrides(effect, skill.ValueOverrides, fighter, rank);
            if (variant != null) ApplyOverrides(effect, variant.Overrides, fighter, rank);
            CardRules.ApplySharedDamageSettings(effect, CardRules.ResolveTargetScope(skill), skill.DamageAnimation);
            return effect;
        }

        public static void SetUnlocked(BattleState state, FighterState fighter, string skillId, string variantId, bool unlocked)
        {
            var definition = fighter?.Definition?.Skills?.Find(s => s?.Id == skillId)?.Variants?.Find(v => v?.Id == variantId);
            if (definition == null) throw new InvalidOperationException("Unknown ability variant: " + skillId + "/" + variantId);
            var entry = fighter.AbilityVariants.Find(v => v.SkillId == skillId && v.VariantId == variantId);
            if (entry == null)
            {
                entry = new AbilityVariantState { SkillId = skillId, VariantId = variantId, Unlocked = definition.InitiallyUnlocked };
                fighter.AbilityVariants.Add(entry);
            }
            if (entry.Unlocked == unlocked) return;
            entry.Unlocked = unlocked;
            state?.Events.Add(new BattleEvent { Id = state.Events.Count + 1, Kind = BattleEventKind.AbilityVariantChanged,
                SourceId = fighter.Id, TargetId = fighter.Id, Amount = unlocked ? 1 : 0,
                Message = skillId + "/" + variantId + (unlocked ? " unlocked." : " relocked.") });
            if (state != null) RefreshCards(state);
        }

        public static AbilityVariantDefinition Select(FighterState fighter, string skillId, int rank)
        {
            var skill = fighter?.Definition?.Skills?.Find(s => s?.Id == skillId);
            AbilityVariantDefinition selected = null;
            foreach (var variant in skill?.Variants ?? new List<AbilityVariantDefinition>())
            {
                if (variant == null || fighter.Level < variant.MinimumLevel || fighter.Level > variant.MaximumLevel ||
                    fighter.ConstellationTier < variant.MinimumTier || fighter.ConstellationTier > variant.MaximumTier ||
                    variant.Ranks?.Find(r => r?.Rank == rank)?.Effect == null) continue;
                var entry = fighter.AbilityVariants.Find(v => v.SkillId == skillId && v.VariantId == variant.Id);
                if (!(entry?.Unlocked ?? variant.InitiallyUnlocked)) continue;
                if (!string.IsNullOrEmpty(variant.RequiredResourceKey) &&
                    CharacterPassiveRuntime.Counter(fighter, variant.RequiredResourceKey) < Math.Max(variant.RequiredResourceAmount, variant.ResourceCost)) continue;
                if (selected == null || variant.Priority > selected.Priority || variant.Priority == selected.Priority &&
                    string.CompareOrdinal(variant.Id, selected.Id) < 0) selected = variant;
            }
            return selected;
        }

        public static bool TryResolve(FighterState fighter, CardState card, out EffectDefinition effect)
        {
            effect = null;
            if (fighter == null || card == null) return false;
            if (card.Kind == CardKind.Ultimate)
                return CardRules.TryGetUltimate(fighter.Definition, card.UltimateTier, out effect);
            var skill = fighter.Definition?.Skills?.Find(s => s?.Id == card.SkillId);
            if (skill == null) return false;
            if ((skill.Variants == null || skill.Variants.Count == 0) && (skill.ValueOverrides == null || skill.ValueOverrides.Count == 0))
                return CardRules.TryGetSkill(fighter.Definition, card.SkillId, card.Rank, out effect);
            var variant = Select(fighter, card.SkillId, card.Rank);
            var data = (variant?.Ranks ?? skill?.Ranks)?.Find(r => r?.Rank == card.Rank);
            if (data?.Effect == null) return false;
            effect = data.Effect.Clone();
            ApplyOverrides(effect, skill.ValueOverrides, fighter, card.Rank);
            if (variant != null) ApplyOverrides(effect, variant.Overrides, fighter, card.Rank);
            CardRules.ApplySharedDamageSettings(effect, CardRules.ResolveTargetScope(skill), skill.DamageAnimation);
            card.VariantId = variant?.Id;
            CardRules.RefreshCardKind(card, fighter.Definition);
            if (variant != null)
            {
                card.EffectCategory = data.HasCardKind ? data.Category : skill.Category;
                card.Category = card.EffectCategory;
                card.TargetScope = CardRules.ResolveTargetScope(skill);
            }
            return true;
        }

        public static bool CommitUse(BattleState state, FighterState fighter, CardState card)
        {
            if (string.IsNullOrEmpty(card?.VariantId)) return true;
            var variant = fighter?.Definition?.Skills?.Find(s => s?.Id == card.SkillId)?.Variants?.Find(v => v?.Id == card.VariantId);
            if (variant == null) return false;
            var category = card.Category;
            var effectCategory = card.EffectCategory;
            var targetScope = card.TargetScope;
            var selectedId = card.VariantId;
            if (variant.ResourceCost > 0 && !CharacterPassiveRuntime.TrySpendResource(state, fighter,
                variant.RequiredResourceKey, variant.ResourceCost, card.Id)) return false;
            if (variant.RelockAfterUse) SetUnlocked(state, fighter, card.SkillId, variant.Id, false);
            card.VariantId = selectedId;
            card.Category = category;
            card.EffectCategory = effectCategory;
            card.TargetScope = targetScope;
            return true;
        }

        public static void RefreshCards(BattleState state)
        {
            if (state == null) return;
            foreach (var team in new[] { state.Player, state.Opponent })
                foreach (var card in team.Hand)
                {
                    var fighter = team.FindFighter(card.OwnerFighterId);
                    var skill = fighter?.Definition?.Skills?.Find(s => s?.Id == card.SkillId);
                    if (card.Kind == CardKind.Skill && (skill?.Variants?.Count > 0 || skill?.ValueOverrides?.Count > 0))
                        TryResolve(fighter, card, out _);
                }
        }

        private static void ApplyOverrides(EffectDefinition effect, List<AbilityValueOverride> values, FighterState fighter, int rank)
        {
            foreach (var value in values ?? new List<AbilityValueOverride>())
            {
                if (value == null || fighter.Level < value.MinimumLevel || fighter.Level > value.MaximumLevel ||
                    rank < value.MinimumRank || rank > value.MaximumRank || fighter.ConstellationTier < value.MinimumTier ||
                    fighter.ConstellationTier > value.MaximumTier) continue;
                if (string.IsNullOrEmpty(value.EffectId) || value.EffectId == effect.Id) Set(effect, value);
                else if (effect.Sequence != null)
                    foreach (var step in effect.Sequence) if (step?.Effect?.Id == value.EffectId) Set(step.Effect, value);
            }
        }

        private static void Set(EffectOperationDefinition effect, AbilityValueOverride value)
        {
            switch (value.Field)
            {
                case AbilityValueField.CoefficientBp: effect.CoefficientBp = value.Value; break;
                case AbilityValueField.Magnitude: effect.Magnitude = value.Value; break;
                case AbilityValueField.HealCoefficientBp: effect.HealCoefficientBp = value.Value; if (effect.HealValue != null) effect.HealValue.CoefficientBp = value.Value; break;
                case AbilityValueField.PowerGaugeAmount: effect.PowerGaugeAmount = value.Value; break;
                case AbilityValueField.StatusStackCount: effect.StatusStackCount = value.Value; break;
            }
        }
    }
}
