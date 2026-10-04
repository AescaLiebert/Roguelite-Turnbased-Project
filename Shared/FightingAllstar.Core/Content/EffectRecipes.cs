using System;
using System.Collections.Generic;

namespace FightingAllstar.Core.Content
{
    [Serializable]
    public sealed class EffectDatabaseDefinition
    {
        public int SchemaVersion = 1;
        public string ContentVersion;
        public List<StatusRecipeDefinition> StatusRecipes = new List<StatusRecipeDefinition>();
        public List<AttackEffectRecipeDefinition> AttackEffectRecipes = new List<AttackEffectRecipeDefinition>();
        public List<CardEffectRecipeDefinition> CardEffectRecipes = new List<CardEffectRecipeDefinition>();
        public EffectDatabaseDefinition Clone()
        {
            var copy = new EffectDatabaseDefinition { SchemaVersion = SchemaVersion, ContentVersion = ContentVersion };
            foreach (var item in StatusRecipes) copy.StatusRecipes.Add(item.Clone());
            foreach (var item in AttackEffectRecipes) copy.AttackEffectRecipes.Add(item.Clone());
            foreach (var item in CardEffectRecipes) copy.CardEffectRecipes.Add(item.Clone());
            return copy;
        }
    }

    [Flags]
    public enum StatusBehavior
    {
        None = 0,
        Stat = 1 << 0,
        DamageOverTime = 1 << 1,
        Disable = 1 << 2,
        Stance = 1 << 3,
        Triggered = 1 << 4
    }

    public enum StatusStackingPolicy
    {
        RefreshStronger,
        RefreshDuration,
        IndependentStacks,
        AddStacks,
        ReplaceAlways
    }

    public enum StatusIdentityScope { Recipe, RecipeAndSource }
    public enum StatusDurationClock { TargetTurnStart, TargetTurnEnd, SourceTurnEnd, ActionEnd, Permanent }
    public enum StatusTickTiming { None, TargetTurnStart, TargetTurnEnd, WhenTargetTakesDamage }
    public enum StatusSnapshotScaling { Fixed, TriggeringHealthDamage, SourceAttack, TargetMaxHealth }
    public enum StatusBreakRule { None, OnDamageTaken, OnCardUsed, OnSourceDefeated }

    [Flags]
    public enum CardCategoryMask
    {
        None = 0,
        Attack = 1 << 0,
        Debuff = 1 << 1,
        Buff = 1 << 2,
        Recovery = 1 << 3,
        Stance = 1 << 4,
        Ultimate = 1 << 5,
        RankTwoOrThree = 1 << 6,
        CardEffects = 1 << 7,
        ReceiveBlueBuffs = 1 << 8,
        ReceiveStances = 1 << 9,
        GainPowerGauge = 1 << 10,
        GainDebuffImmunity = 1 << 11,
        GainAdditionalDamageImmunity = 1 << 12,
        GainFatalDamageProtection = 1 << 13,
        AllCards = Attack | Debuff | Buff | Recovery | Stance | Ultimate
    }

    public enum CardCategory { Attack, Debuff, Buff, Recovery, Stance, AttackDebuff }
    public enum EffectTargetScope { Self, SelectedEnemy, SelectedAlly, AllEnemies, AllAllies, TriggerActor, TriggerTarget, StatusOwner }
    public enum CardEffectOperationKind { Damage, ApplyStatus, RemoveStatus, Cleanse, Heal, ChangePowerGauge, RemoveCard, ModifyCardRank }
    public enum CardEffectWindow { BeforeAction, BeforeDamage, Damage, AfterDamage, AfterAction }
    public enum EffectConditionKind
    {
        Always,
        SourceHasStatusTag,
        TargetHasStatusTag,
        TargetHasBuff,
        TargetHasDebuff,
        TargetHasRecipe,
        TargetHasRecipeFromEffectOwner,
        ActorIsEffectOwner,
        ActorIsAllyOfEffectOwner,
        ActorIsEnemyOfEffectOwner,
        TargetAttributeIs,
        WasCritical,
        WasBlocked,
        SourceGaugeAtLeast,
        TargetGaugeAtLeast,
        SourceHealthAtMost,
        TargetHealthAtMost,
        CounterAtLeast, TargetTraitIs, TargetSeriesIs, RosterCountAtLeast, TargetIsAlive
    }
    public enum ConditionLogic { Leaf, All, Any, Not }
    public enum ConditionSubject { OperationTarget, Owner, Actor, EventTarget }
    public enum EffectValueSource { Fixed, ActualHealthDamage, ActualShieldDamage, SourceAttack, TargetMaxHealth, TargetMissingHealth }

    public enum AttackEffectKind
    {
        Charge, Shatter, Rupture, Detonate, Blaze, Depletes, Fills, Clash, CoDestruction,
        Sever, Spike, Breakthrough, Despair, Pierce, Weakpoint, Flood, PowerStrike, Cleave,
        Quell, Amplify, SecretTechnique, RemoveBuff, CancelStance, Wave, AbsorbEnergy,
        DotBurst, DotExplosion, Abyss
    }

    [Serializable]
    public sealed class AttackEffectRecipeDefinition
    {
        public string Id;
        public AttackEffectKind Kind;
        public DamageFamily OutputFamily;
        public CardEffectWindow Window = CardEffectWindow.BeforeDamage;
        public int PrimaryValueBp;
        public int SecondaryValue;
        public bool RuntimeReady = true;
        public AttackEffectRecipeDefinition Clone() => (AttackEffectRecipeDefinition)MemberwiseClone();
    }

    [Serializable]
    public sealed class PeriodicDamageDefinition
    {
        public DamageFamily Family = DamageFamily.DamageOverTime;
        public StatusTickTiming Timing = StatusTickTiming.TargetTurnEnd;
        public StatusSnapshotScaling Scaling = StatusSnapshotScaling.TriggeringHealthDamage;
        public int CoefficientBp = 10000;
        public int FixedAmount;
        public bool ConsumeOnTrigger;
        public PeriodicDamageDefinition Clone() => (PeriodicDamageDefinition)MemberwiseClone();
    }

    [Serializable]
    public sealed class StatusRecipeDefinition
    {
        public string Id;
        public string NameKey;
        public StatusPolarity Polarity;
        public StatusColor Color = StatusColor.Blue;
        public StatusBehavior Behavior;
        public StatusStackingPolicy Stacking = StatusStackingPolicy.RefreshStronger;
        public StatusIdentityScope Identity = StatusIdentityScope.Recipe;
        public StatusDurationClock DurationClock = StatusDurationClock.TargetTurnEnd;
        public int DefaultDuration = 2;
        public int MaxStacks = 1;
        public int DefaultPotencyBp;
        public bool Dispellable = true;
        public bool BypassDebuffImmunity;
        public StatusBreakRule BreakRule;
        public CardCategoryMask DisableMask;
        public List<string> Tags = new List<string>();
        public List<StatModifierDefinition> Modifiers = new List<StatModifierDefinition>();
        public PeriodicDamageDefinition PeriodicDamage;
        public bool DebuffImmunity;
        public bool AdditionalDamageImmunity;
        public bool Taunt;
        public int SurviveLethalCharges;
        public int IgnoreCritResistanceBp;
        public int IgnoreCritDefenseBp;
        public List<string> ImmuneStatusTags = new List<string>();
        public List<PassiveReactionDefinition> Reactions = new List<PassiveReactionDefinition>();

        public StatusRecipeDefinition Clone()
        {
            var copy = (StatusRecipeDefinition)MemberwiseClone();
            copy.Tags = Tags == null ? new List<string>() : new List<string>(Tags);
            copy.Modifiers = new List<StatModifierDefinition>();
            if (Modifiers != null) foreach (var modifier in Modifiers) copy.Modifiers.Add(modifier?.Clone());
            copy.PeriodicDamage = PeriodicDamage?.Clone();
            copy.ImmuneStatusTags = new List<string>(ImmuneStatusTags ?? new List<string>());
            copy.Reactions = new List<PassiveReactionDefinition>();
            if (Reactions != null) foreach (var reaction in Reactions) copy.Reactions.Add(reaction.Clone());
            return copy;
        }
    }

    [Serializable]
    public sealed class EffectConditionDefinition
    {
        public EffectConditionKind Kind;
        public string RecipeId;
        public string Tag;
        public string StringValue;
        public int Threshold;
        public bool Negate;
        public ConditionLogic Logic;
        public ConditionSubject Subject;
        public List<EffectConditionDefinition> Children = new List<EffectConditionDefinition>();
        public PassiveTargetFilter RosterFilter;
        public bool InitialRoster;
        public EffectConditionDefinition Clone()
        {
            var copy = (EffectConditionDefinition)MemberwiseClone();
            copy.Children = new List<EffectConditionDefinition>();
            if (Children != null) foreach (var child in Children) copy.Children.Add(child?.Clone());
            copy.RosterFilter = RosterFilter?.Clone();
            return copy;
        }
    }

    [Serializable]
    public sealed class EffectValueDefinition
    {
        public EffectValueSource Source;
        public int FixedAmount;
        public int CoefficientBp = 10000;
        public int MaximumAmount = int.MaxValue;
        public EffectValueDefinition Clone() => (EffectValueDefinition)MemberwiseClone();
    }

    [Serializable]
    public sealed class DamageEffectRecipe
    {
        public DamageFamily Family;
        public StatScaling Scaling = StatScaling.Attack;
        public StatId ScalingStat = StatId.Attack;
        public int FixedAmount;
        public int CoefficientBp = 10000;
        public int KeywordFactorBp = 10000;
        public string AttackEffectId;
        public bool CannotCrit;
        public bool CannotBlock;
        public bool ScaleFromTargetMaxHealth;
        public DamageEffectRecipe Clone() => (DamageEffectRecipe)MemberwiseClone();
    }

    [Serializable]
    public sealed class StatusApplicationRecipe
    {
        public string StatusRecipeId;
        public int DurationOverride;
        public int StackCount = 1;
        public int PotencyBp;
        public int ProcChanceBp = 10000;
        public StatusRecipeDefinition InlineRecipe;
        public StatusApplicationRecipe Clone()
        {
            var copy = (StatusApplicationRecipe)MemberwiseClone();
            copy.InlineRecipe = InlineRecipe?.Clone();
            return copy;
        }
    }

    [Serializable]
    public sealed class CardEffectOperationDefinition
    {
        public string Id;
        public int Order;
        public CardEffectWindow Window;
        public CardEffectOperationKind Kind;
        public EffectTargetScope Target;
        public List<EffectConditionDefinition> Conditions = new List<EffectConditionDefinition>();
        public DamageEffectRecipe Damage;
        public StatusApplicationRecipe Status;
        public StatusPolarity RemovePolarity;
        public string RemoveRecipeId;
        public bool RemoveAll = true;
        public int Magnitude;
        public EffectValueDefinition Value;

        public CardEffectOperationDefinition Clone()
        {
            var copy = (CardEffectOperationDefinition)MemberwiseClone();
            copy.Conditions = new List<EffectConditionDefinition>();
            if (Conditions != null) foreach (var condition in Conditions) copy.Conditions.Add(condition?.Clone());
            copy.Damage = Damage?.Clone();
            copy.Status = Status?.Clone();
            copy.Value = Value?.Clone();
            return copy;
        }
    }

    [Serializable]
    public sealed class CardEffectRecipeDefinition
    {
        public string Id;
        public CardCategory Category;
        public List<CardEffectOperationDefinition> Operations = new List<CardEffectOperationDefinition>();

        public CardEffectRecipeDefinition Clone()
        {
            var copy = new CardEffectRecipeDefinition { Id = Id, Category = Category };
            if (Operations != null) foreach (var operation in Operations) copy.Operations.Add(operation?.Clone());
            return copy;
        }
    }

    public static class EffectRecipeValidator
    {
        public static List<string> Validate(EffectDatabaseDefinition database)
        {
            if (database == null) return new List<string> { "Effect database is missing." };
            var errors = Validate(database.StatusRecipes, database.CardEffectRecipes, database.AttackEffectRecipes);
            if (database.SchemaVersion != 1) errors.Insert(0, "Unsupported effect database schema version.");
            if (string.IsNullOrWhiteSpace(database.ContentVersion)) errors.Insert(0, "Effect database requires a content version.");
            return errors;
        }

        public static List<string> Validate(IReadOnlyList<StatusRecipeDefinition> statuses,
            IReadOnlyList<CardEffectRecipeDefinition> cardEffects,
            IReadOnlyList<AttackEffectRecipeDefinition> attackEffects = null)
        {
            var errors = new List<string>();
            var statusIds = new HashSet<string>(StringComparer.Ordinal);
            if (statuses != null)
                foreach (var status in statuses)
                {
                    if (status == null || string.IsNullOrWhiteSpace(status.Id)) { errors.Add("Status recipe has no stable id."); continue; }
                    if (!statusIds.Add(status.Id)) errors.Add("Duplicate status recipe id: " + status.Id);
                    if (status.DurationClock != StatusDurationClock.Permanent && status.DefaultDuration <= 0)
                        errors.Add(status.Id + " requires a positive default duration.");
                    if (status.MaxStacks <= 0) errors.Add(status.Id + " requires a positive max stack count.");
                    if ((status.Behavior & StatusBehavior.DamageOverTime) != 0 && status.PeriodicDamage == null)
                        errors.Add(status.Id + " is DOT but has no periodic damage recipe.");
                    if ((status.Behavior & StatusBehavior.Disable) != 0 && status.DisableMask == CardCategoryMask.None)
                        errors.Add(status.Id + " is Disable but has no disable mask.");
                }

            var attackIds = new HashSet<string>(StringComparer.Ordinal);
            if (attackEffects != null)
                foreach (var attack in attackEffects)
                {
                    if (attack == null || string.IsNullOrWhiteSpace(attack.Id)) { errors.Add("Attack-effect recipe has no stable id."); continue; }
                    if (!attackIds.Add(attack.Id)) errors.Add("Duplicate attack-effect recipe id: " + attack.Id);
                }

            var cardIds = new HashSet<string>(StringComparer.Ordinal);
            if (cardEffects != null)
                foreach (var card in cardEffects)
                {
                    if (card == null || string.IsNullOrWhiteSpace(card.Id)) { errors.Add("Card-effect recipe has no stable id."); continue; }
                    if (!cardIds.Add(card.Id)) errors.Add("Duplicate card-effect recipe id: " + card.Id);
                    var operationIds = new HashSet<string>(StringComparer.Ordinal);
                    var orders = new HashSet<int>();
                    if (card.Operations == null) continue;
                    foreach (var operation in card.Operations)
                    {
                        if (operation == null || string.IsNullOrWhiteSpace(operation.Id)) { errors.Add(card.Id + " has an operation without an id."); continue; }
                        if (!operationIds.Add(operation.Id)) errors.Add(card.Id + " has duplicate operation id: " + operation.Id);
                        if (!orders.Add(operation.Order)) errors.Add(card.Id + " has duplicate operation order: " + operation.Order);
                        if (operation.Kind == CardEffectOperationKind.Damage && operation.Damage == null)
                            errors.Add(card.Id + "/" + operation.Id + " is Damage but has no damage recipe.");
                        if (operation.Damage != null && !string.IsNullOrEmpty(operation.Damage.AttackEffectId) &&
                            !attackIds.Contains(operation.Damage.AttackEffectId))
                            errors.Add(card.Id + "/" + operation.Id + " references an unknown attack-effect recipe.");
                        if (operation.Kind == CardEffectOperationKind.ApplyStatus &&
                            (operation.Status == null || !statusIds.Contains(operation.Status.StatusRecipeId)))
                            errors.Add(card.Id + "/" + operation.Id + " references an unknown status recipe.");
                    }
                }
            return errors;
        }
    }
}
