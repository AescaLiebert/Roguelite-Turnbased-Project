using System;
using System.Collections.Generic;

namespace FightingAllstar.Core.Content
{
    [Flags]
    public enum BattleModeMask { PvE = 1, PvP = 2, All = PvE | PvP }
    public enum PassivePresence { LivingRoster, LivingActive, LivingReserve }
    public enum PassiveRelation { Any, Self, Allies, Enemies }
    public enum PassiveScaling { Constant, FieldStatusStacks, OwnerCounter, OwnerStat }
    public enum PassiveEventKind { BattleStarted, TeamTurnStarted, TeamTurnEnded, GaugeChanged,
        BeforeAction, BeforeDamage, DamageResolved, AfterAction, FighterDefeated, StatusApplied, StatusRemoved, Healed, ReserveEntered }
    public enum PassiveCommandKind { IncrementCounter, ChangePowerGauge, ExecuteEffect, SetCounter }
    public enum PassiveValueSource { Fixed, ActualGaugeLost }
    public enum PassiveLimitScope { None, Battle, OwnerTurn, RootAction, RootActionTarget, Event }

    [Serializable]
    public sealed class PassiveGate
    {
        public int MinimumTier;
        public int MaximumTier = 6;
        public BattleModeMask Modes = BattleModeMask.All;
        public PassivePresence Presence = PassivePresence.LivingRoster;
        public bool AllowDefeatedOwner;
        public PassiveGate Clone() => (PassiveGate)MemberwiseClone();
    }

    [Serializable]
    public sealed class PassiveTargetFilter
    {
        public PassiveRelation Relation = PassiveRelation.Allies;
        public bool IncludeOwner = true;
        public bool IncludeReserve = true;
        public string AttributeId;
        public string TraitId;
        public string SeriesId;
        public PassiveTargetFilter Clone() => (PassiveTargetFilter)MemberwiseClone();
    }

    [Serializable]
    public sealed class PassiveAuraDefinition
    {
        public string Id;
        public PassiveGate Gate = new PassiveGate();
        public PassiveTargetFilter Targets = new PassiveTargetFilter();
        public PassiveScaling Scaling;
        // Status tag for FieldStatusStacks; counter key for OwnerCounter.
        public string ScalingKey;
        public StatId SourceStat;
        public int MaximumUnits = int.MaxValue;
        public List<StatModifierDefinition> Modifiers = new List<StatModifierDefinition>();
        public PassiveAuraDefinition Clone()
        {
            var copy = (PassiveAuraDefinition)MemberwiseClone();
            copy.Gate = Gate?.Clone(); copy.Targets = Targets?.Clone();
            copy.Modifiers = new List<StatModifierDefinition>();
            if (Modifiers != null) foreach (var modifier in Modifiers) copy.Modifiers.Add(modifier?.Clone());
            return copy;
        }
    }

    [Serializable]
    public sealed class PassiveCommandDefinition
    {
        public PassiveCommandKind Kind;
        public string CounterKey;
        public int CounterCap = int.MaxValue;
        public PassiveValueSource ValueSource;
        public int Amount = 1;
        public CardEffectOperationDefinition Effect;
        public int DelayOwnerTurns;
        public PassiveCommandDefinition Clone()
        {
            var copy = (PassiveCommandDefinition)MemberwiseClone();
            copy.Effect = Effect?.Clone();
            return copy;
        }
    }

    [Serializable]
    public sealed class PassiveReactionDefinition
    {
        public string Id;
        public PassiveGate Gate = new PassiveGate();
        public PassiveEventKind Trigger;
        public bool OwnTeamTurnOnly;
        public PassiveRelation ActorRelation;
        public PassiveRelation TargetRelation;
        public bool CardOriginOnly;
        public bool ExcludeUltimate;
        public bool RequireGaugeLoss;
        public bool RequireCritical;
        public bool RequireBlocked;
        public bool AllowReactionOrigin;
        public bool FilterDamageFamily;
        public DamageFamily DamageFamily;
        public bool FilterCategory;
        public CardCategory Category;
        public int MinimumRank;
        public PassiveLimitScope LimitScope;
        public int MaximumActivations = 1;
        public int CooldownOwnerTurns;
        public List<EffectConditionDefinition> Conditions = new List<EffectConditionDefinition>();
        public List<PassiveCommandDefinition> Commands = new List<PassiveCommandDefinition>();
        public PassiveReactionDefinition Clone()
        {
            var copy = (PassiveReactionDefinition)MemberwiseClone();
            copy.Gate = Gate?.Clone(); copy.Commands = new List<PassiveCommandDefinition>();
            if (Commands != null) foreach (var command in Commands) copy.Commands.Add(command?.Clone());
            copy.Conditions = new List<EffectConditionDefinition>();
            if (Conditions != null) foreach (var condition in Conditions) copy.Conditions.Add(condition?.Clone());
            return copy;
        }
    }

    public static class PassiveRuleValidator
    {
        public static List<string> Validate(PassiveDefinition passive)
        {
            var errors = new List<string>();
            if (passive == null) return errors;
            if (string.IsNullOrWhiteSpace(passive.Id)) errors.Add("Passive requires a stable id.");
            if (passive.Triggers != null && passive.Triggers.Count > 0)
                errors.Add(passive.Id + ": legacy collected triggers are not executable; migrate to supported rules before publishing.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var counters = new HashSet<string>(StringComparer.Ordinal);
            if (passive.Reactions != null) foreach (var rule in passive.Reactions)
            {
                if (rule == null) { errors.Add("Null passive reaction."); continue; }
                ValidateRule(rule.Id, rule.Gate, ids, errors);
                if (!Enum.IsDefined(typeof(PassiveEventKind), rule.Trigger) ||
                    !Enum.IsDefined(typeof(PassiveRelation), rule.ActorRelation) ||
                    !Enum.IsDefined(typeof(PassiveRelation), rule.TargetRelation)) errors.Add(rule.Id + ": invalid trigger/filter.");
                if (rule.Commands == null || rule.Commands.Count == 0) errors.Add(rule.Id + ": commands required.");
                else foreach (var command in rule.Commands)
                {
                    if (command == null || !Enum.IsDefined(typeof(PassiveCommandKind), command.Kind) ||
                        !Enum.IsDefined(typeof(PassiveValueSource), command.ValueSource) || command.Amount < 0 || command.DelayOwnerTurns < 0)
                    { errors.Add(rule.Id + ": invalid command."); continue; }
                    if (command.ValueSource == PassiveValueSource.ActualGaugeLost &&
                        (rule.Trigger != PassiveEventKind.GaugeChanged || !rule.RequireGaugeLoss))
                        errors.Add(rule.Id + ": gauge-loss values require a gauge-loss trigger.");
                    if (command.Kind == PassiveCommandKind.ExecuteEffect && command.Effect == null)
                        errors.Add(rule.Id + ": effect command requires an operation.");
                    if (command.Kind == PassiveCommandKind.IncrementCounter || command.Kind == PassiveCommandKind.SetCounter)
                    {
                        if (string.IsNullOrWhiteSpace(command.CounterKey) || command.CounterCap <= 0)
                            errors.Add(rule.Id + ": invalid counter key/cap.");
                        else counters.Add(command.CounterKey);
                    }
                }
            }
            if (passive.Auras != null) foreach (var rule in passive.Auras)
            {
                if (rule == null) { errors.Add("Null passive aura."); continue; }
                ValidateRule(rule.Id, rule.Gate, ids, errors);
                if (rule.Targets == null || !Enum.IsDefined(typeof(PassiveRelation), rule.Targets.Relation))
                    errors.Add(rule.Id + ": target filter required.");
                if (!Enum.IsDefined(typeof(PassiveScaling), rule.Scaling) || rule.MaximumUnits <= 0)
                    errors.Add(rule.Id + ": invalid scaling.");
                if (rule.Scaling == PassiveScaling.OwnerCounter && !counters.Contains(rule.ScalingKey ?? ""))
                    errors.Add(rule.Id + ": counter has no writer.");
                if (rule.Scaling == PassiveScaling.FieldStatusStacks && string.IsNullOrWhiteSpace(rule.ScalingKey))
                    errors.Add(rule.Id + ": status tag required.");
                if (rule.Scaling == PassiveScaling.OwnerStat && !Enum.IsDefined(typeof(StatId), rule.SourceStat))
                    errors.Add(rule.Id + ": invalid source stat.");
                if (rule.Modifiers == null || rule.Modifiers.Count == 0) errors.Add(rule.Id + ": modifiers required.");
                else foreach (var modifier in rule.Modifiers)
                    if (modifier == null || modifier.Target != ModifierTarget.Stat || modifier.ScaleByStatusPotency ||
                        !Enum.IsDefined(typeof(StatId), modifier.Stat) ||
                        (modifier.Operation != ModifierOperation.PercentOfBase && modifier.Operation != ModifierOperation.PercentagePoints &&
                         modifier.Operation != ModifierOperation.Flat))
                        errors.Add(rule.Id + ": aura supports explicit additive stat modifiers only.");
            }
            return errors;
        }

        private static void ValidateRule(string id, PassiveGate gate, HashSet<string> ids, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) errors.Add("Missing/duplicate passive rule id: " + id);
            if (gate == null || gate.MinimumTier < 0 || gate.MaximumTier > 6 || gate.MinimumTier > gate.MaximumTier ||
                gate.Modes == 0 || (gate.Modes & ~BattleModeMask.All) != 0 || !Enum.IsDefined(typeof(PassivePresence), gate.Presence))
                errors.Add(id + ": invalid availability gate.");
        }
    }
}
