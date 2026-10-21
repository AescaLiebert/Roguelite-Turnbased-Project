using System;
using System.Collections.Generic;

namespace FightingAllstar.Core.Content
{
    public enum AbilityValueField { CoefficientBp, Magnitude, HealCoefficientBp, PowerGaugeAmount, StatusStackCount }

    [Serializable]
    public sealed class AbilityValueOverride
    {
        public string Id;
        public int MinimumLevel = 1;
        public int MaximumLevel = int.MaxValue;
        public int MinimumRank = 1;
        public int MaximumRank = 3;
        public int MinimumTier;
        public int MaximumTier = 5;
        public string EffectId;
        public AbilityValueField Field;
        public int Value;
        public AbilityValueOverride Clone() => (AbilityValueOverride)MemberwiseClone();
    }

    /// <summary>Alternate skill ranks selected from authoritative readiness at card resolution.</summary>
    [Serializable]
    public sealed class AbilityVariantDefinition
    {
        public string Id;
        public string Name;
        public int Priority;
        public bool InitiallyUnlocked;
        public int MinimumLevel = 1;
        public int MaximumLevel = int.MaxValue;
        public int MinimumTier;
        public int MaximumTier = 5;
        public string RequiredResourceKey;
        public int RequiredResourceAmount;
        public int ResourceCost;
        public bool RelockAfterUse;
        public List<SkillRankDefinition> Ranks = new List<SkillRankDefinition>();
        public List<AbilityValueOverride> Overrides = new List<AbilityValueOverride>();
        public AbilityVariantDefinition Clone()
        {
            var copy = (AbilityVariantDefinition)MemberwiseClone();
            copy.Ranks = Ranks?.ConvertAll(r => r?.Clone()) ?? new List<SkillRankDefinition>();
            copy.Overrides = Overrides?.ConvertAll(v => v?.Clone()) ?? new List<AbilityValueOverride>();
            return copy;
        }
    }

    public static class AbilityVariantValidator
    {
        public static List<string> Validate(CharacterDefinition character)
        {
            var errors = new List<string>();
            foreach (var skill in character?.Skills ?? new List<SkillDefinition>())
            {
                if (skill == null) continue;
                ValidateOverrides(skill.Id, skill.ValueOverrides, skill.Ranks, errors);
                foreach (var rank in skill.Ranks ?? new List<SkillRankDefinition>())
                    ValidateEntityCapabilities(skill.Id + "/rank" + rank?.Rank, rank?.Effect, errors);
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var variant in skill.Variants ?? new List<AbilityVariantDefinition>())
                {
                    if (variant == null) { errors.Add(skill.Id + ": null variant."); continue; }
                    if (string.IsNullOrWhiteSpace(variant.Id) || !ids.Add(variant.Id)) errors.Add(skill.Id + ": missing/duplicate variant ID.");
                    if (variant.MinimumLevel < 1 || variant.MaximumLevel < variant.MinimumLevel || variant.MinimumTier < 0 ||
                        variant.MaximumTier > 5 || variant.MinimumTier > variant.MaximumTier || variant.RequiredResourceAmount < 0 ||
                        variant.ResourceCost < 0 || (variant.ResourceCost > 0 || variant.RequiredResourceAmount > 0) &&
                        string.IsNullOrWhiteSpace(variant.RequiredResourceKey)) errors.Add(variant.Id + ": invalid readiness gate or resource cost.");
                    for (var rank = 1; rank <= 3; rank++)
                        if (variant.Ranks?.FindAll(r => r?.Rank == rank).Count != 1 || variant.Ranks?.Find(r => r?.Rank == rank)?.Effect == null)
                            errors.Add(variant.Id + ": exactly one effect for each rank 1–3 is required.");
                    if (variant.Ranks?.Count != 3) errors.Add(variant.Id + ": rank table must contain three entries.");
                    foreach (var rank in variant.Ranks ?? new List<SkillRankDefinition>())
                    {
                        if (rank?.Effect == null) continue;
                        ValidateEffect(variant.Id + "/rank" + rank.Rank, rank.Effect, errors);
                        foreach (var step in rank.Effect.Sequence ?? new List<CardEffectStep>())
                        {
                            if (step == null || !Enum.IsDefined(typeof(CardEffectTiming), step.Timing))
                                errors.Add(variant.Id + ": sequence step requires a valid timing.");
                            ValidateEffect(variant.Id + "/rank" + rank.Rank + "/" + step?.Effect?.Id, step?.Effect, errors);
                        }
                    }
                    ValidateOverrides(variant.Id, variant.Overrides, variant.Ranks, errors);
                }
            }
            foreach (var tier in character?.UltimateTiers ?? new List<UltimateTierDefinition>())
                ValidateEntityCapabilities(character.Id + "/ultimate" + tier?.Tier, tier?.Effect, errors);
            return errors;
        }

        private static void ValidateEntityCapabilities(string source, EffectDefinition effect, List<string> errors)
        {
            if (effect == null) return;
            ValidateAddedOperation(source, effect, errors);
            foreach (var step in effect.Sequence ?? new List<CardEffectStep>())
                if (step?.Effect != null) ValidateAddedOperation(source + "/" + step.Effect.Id, step.Effect, errors);
        }

        private static void ValidateAddedOperation(string source, EffectOperationDefinition effect, List<string> errors)
        {
            // Preserve existing legacy validation while applying the new contracts to ordinary cards too.
            if (effect.Kind == EffectKind.Summon || effect.Summon != null) ValidateEffect(source, effect, errors);
            else ValidateSourceLayers(source, effect, errors);
        }

        private static void ValidateSourceLayers(string source, EffectOperationDefinition effect, List<string> errors)
        {
            if (effect.SourceStackDamageBp < 0 || effect.SourceStackDotScaleBp < 0)
                errors.Add(source + ": source-layer scaling must be nonnegative.");
            if ((effect.SourceStackDamageBp != 0 || effect.SourceStackDotScaleBp != 0 || effect.ConsumeSourceStacks) &&
                (string.IsNullOrWhiteSpace(effect.SourceStackRecipeId) || effect.Kind != EffectKind.Damage))
                errors.Add(source + ": source-layer damage/consumption requires a damage effect and a stable status recipe id.");
        }

        public static void ValidateEffect(string source, EffectOperationDefinition effect, List<string> errors)
        {
            if (effect == null) { errors.Add(source + ": missing effect payload."); return; }
            ValidateSourceLayers(source, effect, errors);
            if (effect.Summon != null && effect.Kind != EffectKind.Summon)
                errors.Add(source + ": an entity payload requires the Summon operation kind.");
            if (!Enum.IsDefined(typeof(EffectKind), effect.Kind) || !Enum.IsDefined(typeof(EffectTargetScope), effect.Target) ||
                !Enum.IsDefined(typeof(StatScaling), effect.Scaling) || !Enum.IsDefined(typeof(DamageFamily), effect.Family) ||
                !Enum.IsDefined(typeof(StatId), effect.ScalingStat) ||
                !Enum.IsDefined(typeof(StatId), effect.StatusApplyChanceBonusStat)) errors.Add(source + ": unsupported effect kind, target or scaling.");
            if (effect.CoefficientBp < 0 || effect.HealCoefficientBp < 0 || effect.StatusApplyChanceBonusCoefficientBp < 0 ||
                effect.DamageProfile?.TrueDamageBonusBp < 0)
                errors.Add(source + ": damage/heal coefficients and true damage bonus must be nonnegative.");
            if (effect.Kind == EffectKind.ApplyStatus)
            {
                if (effect.StatusRecipe == null) errors.Add(source + ": ApplyStatus requires a status recipe.");
                else if (effect.StatusRecipe.StructuralEntityMarker) errors.Add(source + ": structural markers are created only by Summon operations.");
                else foreach (var error in EffectRecipeValidator.Validate(new[] { effect.StatusRecipe }, null))
                    errors.Add(source + ": " + error);
                if (effect.StatusStackCount < 1 || effect.StatusApplyChanceBp < 0 || effect.StatusApplyChanceBp > 10000 || effect.StatusDurationOverride < 0)
                    errors.Add(source + ": invalid status layers, chance or duration.");
            }
            if (effect.Kind == EffectKind.Summon)
            {
                if (effect.Summon == null) errors.Add(source + ": Summon requires an entity definition.");
                else foreach (var error in effect.Summon.Validate()) errors.Add(source + ": " + error);
                if (effect.Target != EffectTargetScope.Self) errors.Add(source + ": Summon is owned by its acting fighter and must target Self.");
            }
            if (effect.Kind == EffectKind.Damage && (effect.Magnitude < 0 || effect.Attack == null || effect.Attack.HitCount < 1))
                errors.Add(source + ": damage requires a nonnegative amount and positive hit count.");
            if (effect.Kind == EffectKind.Heal && (effect.Magnitude < 0 || effect.HealValue != null &&
                (effect.HealValue.FixedAmount < 0 || effect.HealValue.CoefficientBp < 0 || !Enum.IsDefined(typeof(EffectValueSource), effect.HealValue.Source))))
                errors.Add(source + ": invalid healing basis or amount.");
            if (effect.Kind == EffectKind.ChangePowerGauge && (effect.PowerGaugeAmount < -5 || effect.PowerGaugeAmount > 5))
                errors.Add(source + ": gauge change must be -5 through 5.");
            if (effect.Kind == EffectKind.ModifyCardRank && (effect.Magnitude == 0 || effect.Magnitude < -2 || effect.Magnitude > 2))
                errors.Add(source + ": rank change must be -2, -1, 1 or 2.");
        }

        private static void ValidateOverrides(string owner, List<AbilityValueOverride> values,
            List<SkillRankDefinition> ranks, List<string> errors)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values ?? new List<AbilityValueOverride>())
            {
                if (value == null) { errors.Add(owner + ": null override."); continue; }
                if (string.IsNullOrWhiteSpace(value.Id) || !ids.Add(value.Id)) errors.Add(owner + ": missing/duplicate override ID.");
                if (value.MinimumLevel < 1 || value.MaximumLevel < value.MinimumLevel || value.MinimumRank < 1 ||
                    value.MaximumRank > 3 || value.MinimumRank > value.MaximumRank || value.MinimumTier < 0 || value.MaximumTier > 5 ||
                    value.MinimumTier > value.MaximumTier || !Enum.IsDefined(typeof(AbilityValueField), value.Field) ||
                    value.Field != AbilityValueField.PowerGaugeAmount && value.Value < 0 ||
                    value.Field == AbilityValueField.StatusStackCount && value.Value < 1)
                    errors.Add(value.Id + ": invalid upgrade range, field or value.");
                if (!string.IsNullOrEmpty(value.EffectId))
                {
                    var found = false;
                    foreach (var rank in ranks ?? new List<SkillRankDefinition>())
                    {
                        if (rank?.Effect == null) continue;
                        if (rank.Effect.Id == value.EffectId) found = true;
                        foreach (var step in rank.Effect.Sequence ?? new List<CardEffectStep>())
                            if (step?.Effect?.Id == value.EffectId) found = true;
                    }
                    if (!found) errors.Add(value.Id + ": unknown effect override target.");
                }
            }
            if (values == null) return;
            for (var i = 0; i < values.Count; i++)
                for (var j = i + 1; j < values.Count; j++)
                {
                    var left = values[i]; var right = values[j];
                    if (left != null && right != null && left.Field == right.Field && (left.EffectId ?? "") == (right.EffectId ?? "") &&
                        left.MinimumLevel <= right.MaximumLevel && right.MinimumLevel <= left.MaximumLevel &&
                        left.MinimumRank <= right.MaximumRank && right.MinimumRank <= left.MaximumRank &&
                        left.MinimumTier <= right.MaximumTier && right.MinimumTier <= left.MaximumTier)
                        errors.Add(owner + ": overlapping overrides for the same effect field: " + left.Id + " and " + right.Id);
                }
        }
    }
}
