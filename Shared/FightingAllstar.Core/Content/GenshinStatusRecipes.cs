using System.Collections.Generic;

namespace FightingAllstar.Core.Content
{
    public static class GenshinStatusRecipes
    {
        public static List<StatusRecipeDefinition> CreateKitStatuses() => new List<StatusRecipeDefinition> {
            new StatusRecipeDefinition { Id = "status.debuff.conductive", NameKey = "Conductive", Polarity = StatusPolarity.Debuff,
                Color = StatusColor.Grey, Behavior = StatusBehavior.Stat, DurationClock = StatusDurationClock.Permanent,
                DefaultDuration = 0, Stacking = StatusStackingPolicy.AddStacks, MaxStacks = 3,
                Modifiers = new List<StatModifierDefinition> { new StatModifierDefinition {
                    Target = ModifierTarget.Stat, Stat = StatId.Defense, Operation = ModifierOperation.PercentOfBase, Amount = -2000 } } },
            new StatusRecipeDefinition { Id = "status.debuff.baron-bunny", NameKey = "Baron Bunny", Polarity = StatusPolarity.Debuff,
                Behavior = StatusBehavior.Triggered, DefaultDuration = 1, Stacking = StatusStackingPolicy.ReplaceAlways,
                NaturalExpiryAttack = new TriggeredElementalAttackDefinition { Element = ElementId.Pyro, AttackCoefficientBp = 15000,
                    Explosion = true, StatusRecipeId = "status.debuff.ignite", StatusDuration = 4 } },
            new StatusRecipeDefinition { Id = "status.buff.glacial-waltz", NameKey = "Glacial Waltz", Polarity = StatusPolarity.Buff,
                Behavior = StatusBehavior.Triggered, DefaultDuration = 2, Stacking = StatusStackingPolicy.RefreshDuration,
                SourceOnlyDamageReductionBp = 3000, SkipApplyingTurnEnd = true,
                Retaliation = new TriggeredElementalAttackDefinition { Element = ElementId.Cryo, ReceivedDamageCoefficientBp = 1500,
                    StatusRecipeId = "status.debuff.frost-bite", StatusDuration = 3, SelfHealDamageBp = 3000 } },
            new StatusRecipeDefinition { Id = "status.buff.melody-loop", NameKey = "Melody Loop", Polarity = StatusPolarity.Neutral,
                Behavior = StatusBehavior.Heal | StatusBehavior.Stat, DefaultDuration = 2,
                SkipApplyingTurnEnd = true,
                Stacking = StatusStackingPolicy.ReplaceAlways,
                PeriodicHealing = new PeriodicHealingDefinition { Timing = StatusTickTiming.TargetTurnStart,
                    Scaling = StatusHealScaling.SourceMaxHealth, CoefficientBp = 600 },
                Modifiers = new List<StatModifierDefinition> { new StatModifierDefinition {
                    Target = ModifierTarget.Stat, Stat = StatId.Attack, Operation = ModifierOperation.PercentOfBase, Amount = 700 } } }
        };

        public static List<StatusRecipeDefinition> CreateOutputs() => new List<StatusRecipeDefinition> {
            new StatusRecipeDefinition { Id = "status.debuff.freeze", NameKey = "Freeze", Polarity = StatusPolarity.Debuff,
                Behavior = StatusBehavior.Disable, DisableMask = CardCategoryMask.AllCards, DefaultDuration = 1,
                Stacking = StatusStackingPolicy.RefreshDuration, Tags = new List<string> { "status.freeze", "status.disable" } },
            new StatusRecipeDefinition { Id = "status.reaction.superconduct", NameKey = "Superconduct", Polarity = StatusPolarity.Debuff,
                Behavior = StatusBehavior.Stat, PhysicalDamageReceivedBp = 4000, DefaultDuration = 1,
                Stacking = StatusStackingPolicy.RefreshDuration },
            ReactionDot("status.reaction.charged", "Charged", ElementId.Electro, StatusStackingPolicy.RefreshStronger, 1),
            ReactionDot("status.reaction.burning", "Burning", ElementId.Pyro, StatusStackingPolicy.IndependentStacks, 3),
            new StatusRecipeDefinition { Id = "status.reaction.crystallize", NameKey = "Crystallize", Polarity = StatusPolarity.Buff,
                Behavior = StatusBehavior.Barrier, DefaultDuration = 2, Stacking = StatusStackingPolicy.RefreshDuration,
                BarrierCoefficientBp = 3000, BarrierSourceStat = BarrierSourceStat.Attack,
                BarrierMaxHealthCapBp = 10000, BarrierAmountPolicy = BarrierAmountPolicy.Accumulate,
                BarrierDurationPolicy = BarrierDurationPolicy.Refresh }
        };

        private static StatusRecipeDefinition ReactionDot(string id, string name, ElementId element,
            StatusStackingPolicy stacking, int cap) => new StatusRecipeDefinition {
                Id = id, NameKey = name, Polarity = StatusPolarity.Debuff, Behavior = StatusBehavior.DamageOverTime,
                DefaultDuration = 2, Stacking = stacking, MaxStacks = cap, Identity = StatusIdentityScope.Recipe,
                PeriodicDamage = new PeriodicDamageDefinition { Family = DamageFamily.ElementalReaction,
                    Element = element, CoefficientBp = 10000, Scaling = StatusSnapshotScaling.SourceAttack,
                    Timing = StatusTickTiming.TargetTurnEnd } };

        public static List<StatusRecipeDefinition> CreateFuel() => new List<StatusRecipeDefinition> {
            new StatusRecipeDefinition { Id = "status.debuff.frost-bite", NameKey = "Frost Bite",
                Polarity = StatusPolarity.Debuff, Behavior = StatusBehavior.DamageOverTime | StatusBehavior.Stat,
                Stacking = StatusStackingPolicy.IndependentStacks, Identity = StatusIdentityScope.RecipeAndSource,
                MaxStacks = 10, DefaultDuration = 3, PowerGaugeGainBp = -5000,
                Tags = new List<string> { "status.frost-bite", "status.dot" },
                Modifiers = new List<StatModifierDefinition> { Stat(StatId.Pierce, -1500) },
                PeriodicDamage = new PeriodicDamageDefinition { CoefficientBp = 6400,
                    Scaling = StatusSnapshotScaling.TriggeringHealthDamage, Timing = StatusTickTiming.TargetTurnEnd } },
            new StatusRecipeDefinition { Id = "status.debuff.wet", NameKey = "Wet",
                Polarity = StatusPolarity.Debuff, Behavior = StatusBehavior.Stat,
                Stacking = StatusStackingPolicy.IndependentStacks, MaxStacks = 10,
                Tags = new List<string> { "status.wet" },
                Modifiers = new List<StatModifierDefinition> { Stat(StatId.Avoidance, -1000) } },
            new StatusRecipeDefinition { Id = "status.debuff.nature", NameKey = "Nature",
                Polarity = StatusPolarity.Debuff, Behavior = StatusBehavior.Stat,
                Stacking = StatusStackingPolicy.IndependentStacks, MaxStacks = 10,
                Tags = new List<string> { "status.nature" },
                Modifiers = new List<StatModifierDefinition> { Stat(StatId.Control, -1000) } }
        };

        private static StatModifierDefinition Stat(StatId stat, int amount) => new StatModifierDefinition {
            Target = ModifierTarget.Stat, Stat = stat, Operation = ModifierOperation.PercentagePoints, Amount = amount };
    }
}
