using System;
using System.Collections.Generic;

namespace FightingAllstar.Core.Content
{
    public enum DamageFamily { Normal, True, Additional, DamageOverTime, Destructive }
    public enum StatScaling { Attack, Defense, MaxHealth, Fixed, SpecificStat }
    public enum SourceProvenance { Supplied, Derived, Proposal, GeneratedStartingValue }
    public enum StatId
    {
        Attack, Defense, MaxHealth, CombatClass, Pierce, Resistance, Regeneration,
        CritChance, CritDamage, CritResistance, CritDefense, Recovery, BlockChance,
        BlockPower, LifeSteal, Evade, Perception, Control, Avoidance
    }
    public enum EffectKind { Damage, ApplyStatus, Heal, Cleanse, RemoveBuffs, RemoveDebuffs, ChangePowerGauge, ModifyCardRank }
    public enum StatusPolarity { Buff, Debuff, Neutral }
    public enum StatusColor { Blue, Grey }
    public enum ModifierOperation { Flat, PercentOfBase, PercentagePoints, Multiplier }
    public enum ModifierTarget { Stat, AnyDamageDealt, AnyDamageReceived, FamilyDamageDealt, FamilyDamageReceived, FinalDamageReduction, FlatDamageReduction }
    public enum EffectTrigger
    {
        OnBattleStart, OnTurnStart, OnTurnEnd, BeforeCard, AfterCard, AfterDamageDealt,
        AfterDamageTaken, AfterStatusApplied, OnFighterDefeated
    }
    public enum CardEffectTiming { BeforeAction, BeforeDamage, AfterDamage, AfterAction }

    /// <summary>Stable vocabulary for status tags and effect keywords; values are content IDs, not display text.</summary>
    public static class CombatTags
    {
        public const string Ignite = "status.ignite";
        public const string Bleed = "status.bleed";
        public const string Shock = "status.shock";
        public const string Poison = "status.poison";
        public const string Stun = "control.stun";
        public const string Paralyze = "control.paralyze";
        public const string Seal = "control.seal";
        public const string Stance = "status.stance";
    }

    /// <summary>All ratios are stored as basis points. 10,000 basis points equals 100%.</summary>
    [Serializable]
    public sealed class StatBlock
    {
        public int Attack;
        public int Defense;
        public int MaxHealth;
        public int CombatClass;
        public int PierceBp;
        public int ResistanceBp;
        public int RegenerationBp;
        public int CritChanceBp;
        public int CritDamageBp;
        public int CritResistanceBp;
        public int CritDefenseBp;
        public int RecoveryBp = 10000;
        public int BlockChanceBp;
        public int BlockPowerBp;
        public int LifeStealBp;
        public int EvadeBp;
        public int PerceptionBp = 10000;
        public int ControlBp = 10000;
        public int AvoidanceBp;

        public StatBlock Clone() => (StatBlock)MemberwiseClone();

        public int Get(StatId stat)
        {
            switch (stat)
            {
                case StatId.Attack: return Attack;
                case StatId.Defense: return Defense;
                case StatId.MaxHealth: return MaxHealth;
                case StatId.CombatClass: return CombatClass;
                case StatId.Pierce: return PierceBp;
                case StatId.Resistance: return ResistanceBp;
                case StatId.Regeneration: return RegenerationBp;
                case StatId.CritChance: return CritChanceBp;
                case StatId.CritDamage: return CritDamageBp;
                case StatId.CritResistance: return CritResistanceBp;
                case StatId.CritDefense: return CritDefenseBp;
                case StatId.Recovery: return RecoveryBp;
                case StatId.BlockChance: return BlockChanceBp;
                case StatId.BlockPower: return BlockPowerBp;
                case StatId.LifeSteal: return LifeStealBp;
                case StatId.Evade: return EvadeBp;
                case StatId.Perception: return PerceptionBp;
                case StatId.Control: return ControlBp;
                case StatId.Avoidance: return AvoidanceBp;
                default: throw new ArgumentOutOfRangeException(nameof(stat), stat, "Unknown combat stat.");
            }
        }

        public void Set(StatId stat, int value)
        {
            switch (stat)
            {
                case StatId.Attack: Attack = value; break;
                case StatId.Defense: Defense = value; break;
                case StatId.MaxHealth: MaxHealth = value; break;
                case StatId.CombatClass: CombatClass = value; break;
                case StatId.Pierce: PierceBp = value; break;
                case StatId.Resistance: ResistanceBp = value; break;
                case StatId.Regeneration: RegenerationBp = value; break;
                case StatId.CritChance: CritChanceBp = value; break;
                case StatId.CritDamage: CritDamageBp = value; break;
                case StatId.CritResistance: CritResistanceBp = value; break;
                case StatId.CritDefense: CritDefenseBp = value; break;
                case StatId.Recovery: RecoveryBp = value; break;
                case StatId.BlockChance: BlockChanceBp = value; break;
                case StatId.BlockPower: BlockPowerBp = value; break;
                case StatId.LifeSteal: LifeStealBp = value; break;
                case StatId.Evade: EvadeBp = value; break;
                case StatId.Perception: PerceptionBp = value; break;
                case StatId.Control: ControlBp = value; break;
                case StatId.Avoidance: AvoidanceBp = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(stat), stat, "Unknown combat stat.");
            }
        }
    }

    [Serializable]
    public sealed class DamagePolicy
    {
        public DamageFamily Family;
        public int OutgoingIncreaseBp;
        public int OutgoingDecreaseBp;
        public int IncomingIncreaseBp;
        public int IncomingDecreaseBp;
        public int FamilyDealtIncreaseBp;
        public int FamilyDealtDecreaseBp;
        public int FamilyReceivedIncreaseBp;
        public int FamilyReceivedDecreaseBp;
        public int AttributeFactorBp = 10000;
        public int FlatReduction;
        public int FinalReductionBp;
        public int DamageCap = int.MaxValue;
        public int FamilySpecificReductionBp;
        public int Shield;
        public bool BypassDefense;
        public bool BypassResistance;
        public bool BypassGenericReduction;
        public bool BypassDamageCap;
        public bool BypassShield;
        public bool BypassSurviveAtOne;
        public bool CannotCrit;
        public bool CannotBlock;
        public DamagePolicy Clone() => (DamagePolicy)MemberwiseClone();
    }

    [Serializable]
    public sealed class DamagePacket
    {
        public int BaseAmount;
        public int CoefficientBp = 10000;
        public int KeywordFactorBp = 10000;
        public int VarianceBp = 10000;
        public DamagePolicy Policy = new DamagePolicy();
        public DamagePacket Clone() => new DamagePacket { BaseAmount = BaseAmount, CoefficientBp = CoefficientBp,
            KeywordFactorBp = KeywordFactorBp, VarianceBp = VarianceBp, Policy = Policy == null ? null : Policy.Clone() };
    }

    [Serializable]
    public sealed class DamageResult
    {
        public int CalculatedDamage;
        public int ShieldLost;
        public int HealthLost;
        public int RemainingHealth;
        public int RemainingShield;
        public bool WasCritical;
        public bool WasBlocked;
        public bool WasEndured;
        public bool Executed;
        public bool WasCapped;
    }

    [Serializable]
    public sealed class EffectDefinition
    {
        public string Id;
        public EffectKind Kind = EffectKind.Damage;
        public DamageFamily Family;
        public StatScaling Scaling;
        public StatId ScalingStat = StatId.Attack;
        public int Magnitude;
        public int CoefficientBp = 10000;
        public int KeywordFactorBp = 10000;
        public string KeywordId;
        public List<string> Tags = new List<string>();
        public StatusDefinition Status;
        public StatusRecipeDefinition StatusRecipe;
        public int StatusDurationOverride;
        public int StatusStackCount = 1;
        public int StatusPotencyBp;
        public EffectValueDefinition HealValue;
        public StatId HealScalingStat = StatId.Attack;
        public int HealCoefficientBp;
        public int PowerGaugeAmount;
        public string Target = "SelectedEnemy";
        public string TriggerWindow = "PreAction";
        public List<CardEffectStep> Sequence = new List<CardEffectStep>();
        public SourceProvenance Provenance;
        public EffectDefinition Clone()
        {
            var copy = (EffectDefinition)MemberwiseClone();
            copy.Tags = Tags == null ? new List<string>() : new List<string>(Tags);
            copy.Status = Status == null ? null : Status.Clone();
            copy.StatusRecipe = StatusRecipe == null ? null : StatusRecipe.Clone();
            copy.HealValue = HealValue == null ? null : HealValue.Clone();
            copy.Sequence = new List<CardEffectStep>();
            if (Sequence != null) foreach (var step in Sequence) copy.Sequence.Add(step == null ? null : step.Clone());
            return copy;
        }
    }

    [Serializable]
    public sealed class CardEffectStep
    {
        public CardEffectTiming Timing = CardEffectTiming.BeforeDamage;
        public EffectDefinition Effect;
        public CardEffectStep Clone() => new CardEffectStep { Timing = Timing, Effect = Effect == null ? null : Effect.Clone() };
    }

    [Serializable]
    public sealed class StatModifierDefinition
    {
        public ModifierTarget Target = ModifierTarget.Stat;
        public StatId Stat;
        public DamageFamily Family;
        public ModifierOperation Operation;
        public int Amount;
        public bool ScaleByStatusPotency;
        public int PotencyCoefficientBp = 10000;
        public StatModifierDefinition Clone() => (StatModifierDefinition)MemberwiseClone();
    }

    [Serializable]
    public sealed class StatusDefinition
    {
        public string Id;
        public string NameKey;
        public StatusPolarity Polarity;
        public StatusColor Color = StatusColor.Blue;
        public List<string> Tags = new List<string>();
        public List<StatModifierDefinition> Modifiers = new List<StatModifierDefinition>();
        public int DurationOwnerTurns = 2;
        public int MaxStacks = 1;
        public bool Dispellable = true;
        public bool BypassDebuffImmunity;
        public bool IsPeriodicDamage;
        public DamageFamily PeriodicDamageFamily = DamageFamily.DamageOverTime;
        public int SnapshotDamage;
        public bool TickAtOwnerTurnStart;
        public bool CloneOnRefresh = true;
        public StatusDefinition Clone()
        {
            var copy = (StatusDefinition)MemberwiseClone();
            copy.Tags = Tags == null ? new List<string>() : new List<string>(Tags);
            copy.Modifiers = new List<StatModifierDefinition>();
            if (Modifiers != null) foreach (var modifier in Modifiers) copy.Modifiers.Add(modifier == null ? null : modifier.Clone());
            return copy;
        }
    }

    [Serializable]
    public sealed class PassiveTriggerDefinition
    {
        public EffectTrigger Trigger;
        public string RequiredTag;
        public int RequiredStackCount;
        public List<EffectConditionDefinition> Conditions = new List<EffectConditionDefinition>();
        public List<CardEffectOperationDefinition> Operations = new List<CardEffectOperationDefinition>();
        public List<EffectDefinition> Effects = new List<EffectDefinition>();
        public PassiveTriggerDefinition Clone()
        {
            var copy = new PassiveTriggerDefinition { Trigger = Trigger, RequiredTag = RequiredTag, RequiredStackCount = RequiredStackCount };
            if (Conditions != null) foreach (var condition in Conditions) copy.Conditions.Add(condition?.Clone());
            if (Operations != null) foreach (var operation in Operations) copy.Operations.Add(operation?.Clone());
            if (Effects != null) foreach (var effect in Effects) copy.Effects.Add(effect == null ? null : effect.Clone());
            return copy;
        }
    }

    [Serializable]
    public sealed class PassiveDefinition
    {
        public string Id;
        public List<PassiveAuraDefinition> Auras = new List<PassiveAuraDefinition>();
        public List<PassiveReactionDefinition> Reactions = new List<PassiveReactionDefinition>();
        public List<PassiveTriggerDefinition> Triggers = new List<PassiveTriggerDefinition>();
        public PassiveDefinition Clone()
        {
            var copy = new PassiveDefinition { Id = Id };
            if (Auras != null) foreach (var aura in Auras) copy.Auras.Add(aura?.Clone());
            if (Reactions != null) foreach (var reaction in Reactions) copy.Reactions.Add(reaction?.Clone());
            if (Triggers != null) foreach (var trigger in Triggers) copy.Triggers.Add(trigger == null ? null : trigger.Clone());
            return copy;
        }
    }

    [Serializable]
    public sealed class SkillRankDefinition
    {
        public int Rank;
        public EffectDefinition Effect;
        public string DescriptionKey;
        public string SourceDescription;
        public SourceProvenance Provenance;
        public SkillRankDefinition Clone() => new SkillRankDefinition { Rank = Rank, Effect = Effect == null ? null : Effect.Clone(),
            DescriptionKey = DescriptionKey, SourceDescription = SourceDescription, Provenance = Provenance };
    }

    [Serializable]
    public sealed class SkillDefinition
    {
        public string Id;
        public int Slot;
        public CardCategory Category;
        public EffectTargetScope TargetScope = EffectTargetScope.SelectedEnemy;
        public string SourceTarget;
        public string SourceType;
        public string SourceEffectTags;
        public List<SkillRankDefinition> Ranks = new List<SkillRankDefinition>();
        public SkillDefinition Clone()
        {
            var copy = new SkillDefinition { Id = Id, Slot = Slot, Category = Category, TargetScope = TargetScope,
                SourceTarget = SourceTarget, SourceType = SourceType,
                SourceEffectTags = SourceEffectTags };
            if (Ranks != null) foreach (var rank in Ranks) copy.Ranks.Add(rank == null ? null : rank.Clone());
            return copy;
        }
    }

    [Serializable]
    public sealed class UltimateTierDefinition
    {
        public int Tier;
        public string SourceLabel;
        public string SourceDescription;
        public EffectDefinition Effect;
        public SourceProvenance Provenance;
        public UltimateTierDefinition Clone() => new UltimateTierDefinition { Tier = Tier, SourceLabel = SourceLabel,
            SourceDescription = SourceDescription, Effect = Effect == null ? null : Effect.Clone(), Provenance = Provenance };
    }

    [Serializable]
    public sealed class SourceStatField
    {
        public string StatId;
        public string SourceField;
        public float SourceValue;
        public string Unit;
        public string Provenance;
        public float Value;
        public SourceStatField Clone() => (SourceStatField)MemberwiseClone();
    }

    [Serializable]
    public sealed class PassiveSourceDefinition
    {
        public string SourceType;
        public string Description;
        public string Restriction;
        public string Provenance;
        public PassiveSourceDefinition Clone() => (PassiveSourceDefinition)MemberwiseClone();
    }

    [Serializable]
    public sealed class HolyRelicSourceDefinition
    {
        public string Description;
        public bool EnabledInPrototype;
        public string Provenance;
        public HolyRelicSourceDefinition Clone() => (HolyRelicSourceDefinition)MemberwiseClone();
    }

    [Serializable]
    public sealed class CharacterDefinition
    {
        public int SchemaVersion = 1;
        public bool RuntimeReady;
        public string Id;
        public string SourceId;
        public int SourceRecord;
        public string DisplayName;
        public string FamilyId;
        public string Role;
        public string SeriesId;
        public string AttributeId;
        public string RarityId;
        public List<string> TraitIds = new List<string>();
        public StatBlock BaseStats = new StatBlock();
        public List<SkillDefinition> Skills = new List<SkillDefinition>();
        public string PassiveId;
        public PassiveSourceDefinition PassiveSource;
        public PassiveDefinition Passive;
        public HolyRelicSourceDefinition HolyRelicSource;
        public List<string> ReviewNotes = new List<string>();
        public List<string> SourceTraits = new List<string>();
        public List<SourceStatField> SourceStats = new List<SourceStatField>();
        public List<UltimateTierDefinition> UltimateTiers = new List<UltimateTierDefinition>();
        public SourceProvenance StatsProvenance;
        public CharacterDefinition Clone()
        {
            var copy = new CharacterDefinition { SchemaVersion = SchemaVersion, RuntimeReady = RuntimeReady, Id = Id, SourceId = SourceId,
                SourceRecord = SourceRecord, DisplayName = DisplayName, FamilyId = FamilyId, Role = Role,
                SeriesId = SeriesId, AttributeId = AttributeId, RarityId = RarityId, BaseStats = BaseStats == null ? null : BaseStats.Clone(),
                PassiveId = PassiveId, StatsProvenance = StatsProvenance };
            if (TraitIds != null) copy.TraitIds.AddRange(TraitIds);
            if (SourceTraits != null) copy.SourceTraits.AddRange(SourceTraits);
            if (ReviewNotes != null) copy.ReviewNotes.AddRange(ReviewNotes);
            copy.PassiveSource = PassiveSource == null ? null : PassiveSource.Clone();
            copy.Passive = Passive == null ? null : Passive.Clone();
            copy.HolyRelicSource = HolyRelicSource == null ? null : HolyRelicSource.Clone();
            if (SourceStats != null) foreach (var stat in SourceStats) copy.SourceStats.Add(stat == null ? null : stat.Clone());
            if (Skills != null) foreach (var skill in Skills) copy.Skills.Add(skill == null ? null : skill.Clone());
            if (UltimateTiers != null) foreach (var tier in UltimateTiers) copy.UltimateTiers.Add(tier == null ? null : tier.Clone());
            return copy;
        }
    }

    [Serializable]
    public sealed class ContentCatalog
    {
        public int SchemaVersion = 1;
        public string ContentVersion;
        public string ContentHash;
        public List<CharacterDefinition> Characters = new List<CharacterDefinition>();
        public List<StatusRecipeDefinition> StatusRecipes = new List<StatusRecipeDefinition>();
        public List<AttackEffectRecipeDefinition> AttackEffectRecipes = new List<AttackEffectRecipeDefinition>();
        public List<CardEffectRecipeDefinition> CardEffectRecipes = new List<CardEffectRecipeDefinition>();
    }

    public static class ContentValidator
    {
        public static List<string> Validate(ContentCatalog catalog, bool requireRuntimeReady = true)
        {
            var errors = new List<string>();
            if (catalog == null) { errors.Add("Catalog is missing."); return errors; }
            if (catalog.SchemaVersion != 1) errors.Add("Unsupported catalog schema version.");
            errors.AddRange(EffectRecipeValidator.Validate(catalog.StatusRecipes, catalog.CardEffectRecipes,
                catalog.AttackEffectRecipes));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var draftCount = 0;
            foreach (var fighter in catalog.Characters)
            {
                if (fighter == null) { errors.Add("Catalog contains a null character."); continue; }
                errors.AddRange(PassiveRuleValidator.Validate(fighter.Passive));
                if (string.IsNullOrWhiteSpace(fighter.Id)) errors.Add("Character has no stable definition id.");
                else if (!ids.Add(fighter.Id)) errors.Add("Duplicate character id: " + fighter.Id);
                if (string.IsNullOrWhiteSpace(fighter.SourceId)) errors.Add(fighter.Id + " has no source id.");
                if (fighter.BaseStats == null || fighter.BaseStats.Attack < 0 || fighter.BaseStats.Defense < 0 || fighter.BaseStats.MaxHealth <= 0)
                    errors.Add(fighter.Id + " has invalid base stats.");
                if (fighter.Skills == null || fighter.Skills.Count != 2) errors.Add(fighter.Id + " must define exactly two skills.");
                else
                {
                    var slots = new HashSet<int>();
                    foreach (var skill in fighter.Skills)
                    {
                        if (skill == null || string.IsNullOrWhiteSpace(skill.Id)) { errors.Add(fighter.Id + " has a skill without a stable id."); continue; }
                        if (skill.Slot < 1 || skill.Slot > 2 || !slots.Add(skill.Slot)) errors.Add(skill.Id + " has an invalid or duplicate slot.");
                        ValidateRanks(fighter.Id, skill.Ranks, errors);
                    }
                }
                if (string.IsNullOrWhiteSpace(fighter.PassiveId)) errors.Add(fighter.Id + " has no passive definition id.");
                if (fighter.UltimateTiers == null || fighter.UltimateTiers.Count != 7) errors.Add(fighter.Id + " must define all seven C0-C6 ultimate tiers.");
                else for (var i = 0; i < fighter.UltimateTiers.Count; i++)
                    if (fighter.UltimateTiers[i] == null || fighter.UltimateTiers[i].Tier != i || fighter.UltimateTiers[i].Effect == null)
                        errors.Add(fighter.Id + " has an incomplete or out-of-order ultimate tier at " + i + ".");
                if (requireRuntimeReady && !fighter.RuntimeReady) draftCount++;
            }
            if (draftCount > 0)
                errors.Add(draftCount + " character" + (draftCount == 1 ? " is" : "s are") +
                    " still marked as draft and cannot be loaded at runtime.");
            return errors;
        }

        private static void ValidateRanks(string fighterId, List<SkillRankDefinition> ranks, List<string> errors)
        {
            if (ranks == null || ranks.Count != 3) { errors.Add(fighterId + " skill must define ranks 1-3."); return; }
            for (var i = 0; i < 3; i++)
                if (ranks[i] == null || ranks[i].Rank != i + 1 || ranks[i].Effect == null)
                    errors.Add(fighterId + " skill has an incomplete or out-of-order rank at " + (i + 1) + ".");
        }
    }

    /// <summary>Checks WIP authoring completeness without pretending that source prose is executable gameplay.</summary>
    public static class ContentAuthoringValidator
    {
        public static List<string> ValidateDrafts(ContentCatalog catalog)
        {
            var errors = new List<string>();
            if (catalog == null) { errors.Add("Catalog is missing."); return errors; }
            if (catalog.Characters == null) { errors.Add("Catalog character list is missing."); return errors; }
            if (catalog.Characters.Count != 29) errors.Add("WIP roster catalog must contain all 29 source characters.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var sourceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var fighter in catalog.Characters)
            {
                if (fighter == null) { errors.Add("Catalog contains a null character."); continue; }
                if (string.IsNullOrWhiteSpace(fighter.Id) || !ids.Add(fighter.Id)) errors.Add("Character has a missing or duplicate stable id: " + fighter.Id);
                if (string.IsNullOrWhiteSpace(fighter.SourceId) || !sourceIds.Add(fighter.SourceId)) errors.Add(fighter.Id + " has a missing or duplicate source id.");
                if (fighter.RuntimeReady) errors.Add(fighter.Id + " must remain a draft until its effects are executable.");
                if (fighter.BaseStats == null || fighter.BaseStats.Attack < 0 || fighter.BaseStats.Defense < 0 || fighter.BaseStats.MaxHealth <= 0)
                    errors.Add(fighter.Id + " has invalid normalized stats.");
                if (fighter.SourceStats == null || fighter.SourceStats.Count != 19) errors.Add(fighter.Id + " must retain all 19 source stat records.");
                if (fighter.PassiveSource == null || string.IsNullOrWhiteSpace(fighter.PassiveSource.Description)) errors.Add(fighter.Id + " is missing its source passive text.");
                if (fighter.Skills == null || fighter.Skills.Count != 2) errors.Add(fighter.Id + " must retain both source cards.");
                else foreach (var skill in fighter.Skills)
                {
                    if (skill == null || skill.Ranks == null || skill.Ranks.Count != 3) { errors.Add(fighter.Id + " has an incomplete source card."); continue; }
                    for (var i = 0; i < 3; i++)
                        if (skill.Ranks[i] == null || skill.Ranks[i].Rank != i + 1 || string.IsNullOrWhiteSpace(skill.Ranks[i].SourceDescription))
                            errors.Add(fighter.Id + " has an incomplete source card rank.");
                }
                if (fighter.UltimateTiers == null || fighter.UltimateTiers.Count != 7) errors.Add(fighter.Id + " must retain all seven source constellation tiers.");
                else for (var i = 0; i < 7; i++)
                    if (fighter.UltimateTiers[i] == null || fighter.UltimateTiers[i].Tier != i || string.IsNullOrWhiteSpace(fighter.UltimateTiers[i].SourceDescription))
                        errors.Add(fighter.Id + " has an incomplete source constellation tier.");
            }
            return errors;
        }
    }

    public static class ContentAliases
    {
        public static string NormalizeStat(string value)
        {
            if (value == null) return string.Empty;
            switch (value.Trim().Replace("_", "").Replace(" ", "").ToLowerInvariant())
            {
                case "evasion": return "Evade";
                case "penetration": return "Perception";
                case "crowdcontrol": return "Control";
                case "regenerate": return "Regeneration";
                case "resistence": return "Resistance";
                default: return value.Trim();
            }
        }
    }
}
