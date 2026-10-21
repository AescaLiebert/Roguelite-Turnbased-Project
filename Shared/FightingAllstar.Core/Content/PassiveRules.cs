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
        BeforeAction, BeforeDamage, DamageResolved, AfterAction, FighterDefeated, StatusApplied, StatusRemoved, Healed, ReserveEntered,
        ResourceChanged, BeforeDefeat, DamageCapReached }
    public enum PassiveCommandKind
    {
        IncrementCounter, ChangePowerGauge, ExecuteEffect, SetCounter,
        IncreaseCurrentAttackPercent, IncreaseCurrentDamageDealtPercent,
        IgnoreCurrentTargetCritDefensePercent, IncreaseCurrentDamageDealtByBaseStatPercent, SetAbilityVariant,
        IncreaseCurrentCritChancePercent
    }
    public enum PassiveValueSource { Fixed, ActualGaugeLost, RemovedStatusLayers, EventDamage, EventHealing, Binding, OwnerMissingHealthPercent }
    public enum PassiveEffectBindingField { None, Magnitude, DamageCoefficientBp, DamageFixedAmount, HealFixedAmount, HealCoefficientBp }
    public enum PassiveLimitScope { None, Battle, OwnerTurn, RootAction, RootActionTarget, Event }
    public enum PassiveDamageTiming { EachHit, FinalHit }

    [Serializable]
    public sealed class PassiveGate
    {
        public int MinimumTier;
        public int MaximumTier = 5;
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
        // Which fighters contribute stacks when Scaling is FieldStatusStacks.
        public PassiveRelation ScalingRelation = PassiveRelation.Any;
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
        public string ValueBinding;
        public PassiveEffectBindingField EffectBinding;
        public int Amount = 1;
        public StatId ScalingStat;
        public int ScalingCoefficientBp = 10000;
        public CardEffectOperationDefinition Effect;
        public string SkillId;
        public string VariantId;
        public bool VariantUnlocked;
        public PassiveCommandDefinition Clone()
        {
            var copy = (PassiveCommandDefinition)MemberwiseClone();
            copy.Effect = Effect?.Clone();
            return copy;
        }
    }

    [Serializable]
    public sealed partial class PassiveReactionDefinition
    {
        public string Id;
        public PassiveGate Gate = new PassiveGate();
        public PassiveEventKind Trigger;
        public PassiveDamageTiming DamageTiming;
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
        public ElementId RequiredDamageElement;
        public DamageFamily DamageFamily;
        public bool FilterCategory;
        public CardCategory Category;
        public int MinimumRank;
        public PassiveLimitScope LimitScope;
        public int MaximumActivations = 1;
        public int CooldownOwnerTurns;
        public List<EffectConditionDefinition> Conditions = new List<EffectConditionDefinition>();
        public List<PassiveCommandDefinition> Commands = new List<PassiveCommandDefinition>();
        public List<PassiveCommandDefinition> ElseCommands = new List<PassiveCommandDefinition>();
        public PassiveReactionDefinition Clone()
        {
            var copy = (PassiveReactionDefinition)MemberwiseClone();
            copy.Gate = Gate?.Clone(); copy.Commands = new List<PassiveCommandDefinition>();
            if (Commands != null) foreach (var command in Commands) copy.Commands.Add(command?.Clone());
            copy.ElseCommands = new List<PassiveCommandDefinition>();
            if (ElseCommands != null) foreach (var command in ElseCommands) copy.ElseCommands.Add(command?.Clone());
            copy.Conditions = new List<EffectConditionDefinition>();
            if (Conditions != null) foreach (var condition in Conditions) copy.Conditions.Add(condition?.Clone());
            copy.Actions = Actions?.ConvertAll(a => a?.Clone()) ?? new List<PassiveActionDefinition>();
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
            var support = passive.Support;
            if (support?.IsConfigured == true && (!Enum.IsDefined(typeof(PassivePresence), support.Presence) ||
                !Enum.IsDefined(typeof(PassivePresence), support.SwirlPresence) || support.ExplosionMultiplierBp < 0 ||
                support.SwirlStrengthBp < 0 || support.OpeningTeamGauge < 0 || support.OpeningTeamGauge > 5 ||
                support.DamageHealLowestAllyBp < 0 || support.DamageHealLowestAllyBp > 10000 ||
                support.BonusCritChanceBp < 0 || support.BonusDamageBp < 0 || support.AttackerGaugeGainBp < 0 ||
                support.AttackCadence < 0 || support.AttackCadence > 100 || support.CadenceDamageBp < 0 || support.CadenceDotBp < 0))
                errors.Add(passive.Id + ": invalid combat support capability.");
            if (passive.Triggers != null && passive.Triggers.Count > 0)
                errors.Add(passive.Id + ": legacy collected triggers are not executable; migrate to supported rules before publishing.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var counters = new HashSet<string>(StringComparer.Ordinal);
            if (passive.Reactions != null) foreach (var rule in passive.Reactions)
            {
                if (rule == null) { errors.Add("Null passive reaction."); continue; }
                ValidateRule(rule.Id, rule.Gate, ids, errors);
                if (!Enum.IsDefined(typeof(ElementId), rule.RequiredDamageElement) ||
                    rule.RequiredDamageElement != ElementId.None && rule.Trigger != PassiveEventKind.DamageResolved)
                    errors.Add(rule.Id + ": element filters require a damage-resolved trigger.");
                if (!Enum.IsDefined(typeof(PassiveEventKind), rule.Trigger) ||
                    !Enum.IsDefined(typeof(PassiveDamageTiming), rule.DamageTiming) ||
                    !Enum.IsDefined(typeof(PassiveRelation), rule.ActorRelation) ||
                    !Enum.IsDefined(typeof(PassiveRelation), rule.TargetRelation)) errors.Add(rule.Id + ": invalid trigger/filter.");
                if ((rule.Commands == null || rule.Commands.Count == 0) &&
                    (rule.ElseCommands == null || rule.ElseCommands.Count == 0) &&
                    (rule.Actions == null || rule.Actions.Count == 0)) errors.Add(rule.Id + ": commands or composed actions required.");
                if (rule.ExecutionVersion < 0 || rule.ExecutionVersion > 1)
                    errors.Add(rule.Id + ": unsupported execution version.");
                if (rule.Actions?.Count > 0 && rule.ExecutionVersion != 1)
                    errors.Add(rule.Id + ": action composition requires execution version one.");
                if (rule.ExecutionVersion == 1)
                {
                    if (!Enum.IsDefined(typeof(PassiveExecutionPhase), rule.Phase) ||
                        !Enum.IsDefined(typeof(PassiveLimitScope), rule.LimitScope) ||
                        rule.LimitScope != PassiveLimitScope.None && rule.MaximumActivations < 1 ||
                        rule.CooldownOwnerTurns < 0)
                        errors.Add(rule.Id + ": invalid execution phase, activation limit, or cooldown.");
                    if (rule.FilterRemovalReason && (rule.Trigger != PassiveEventKind.StatusRemoved ||
                        !Enum.IsDefined(typeof(StatusRemovalReason), rule.RemovalReason)))
                        errors.Add(rule.Id + ": removal reason requires a status-removal trigger.");
                    if (!string.IsNullOrEmpty(rule.EventResourceKey) && rule.Trigger != PassiveEventKind.ResourceChanged)
                        errors.Add(rule.Id + ": resource filter requires a resource-change trigger.");
                    if (!string.IsNullOrEmpty(rule.EventStatusRecipeId) && rule.Trigger != PassiveEventKind.StatusApplied &&
                        rule.Trigger != PassiveEventKind.StatusRemoved)
                        errors.Add(rule.Id + ": status filter requires a status lifecycle trigger.");
                    if (rule.Trigger == PassiveEventKind.BeforeDamage && rule.Actions?.Count > 0)
                        errors.Add(rule.Id + ": BeforeDamage accepts synchronous fixed local modifiers, not composed side effects.");
                    if (rule.Trigger == PassiveEventKind.BeforeDamage && (rule.RequireCritical || rule.RequireBlocked || rule.ElseCommands?.Count > 0))
                        errors.Add(rule.Id + ": BeforeDamage cannot inspect unresolved critical/block outcomes or execute an else program.");
                }
                if (rule.Commands != null)
                    foreach (var command in rule.Commands) ValidateCommand(rule, command, counters, errors);
                if (rule.ElseCommands != null)
                    foreach (var command in rule.ElseCommands) ValidateCommand(rule, command, counters, errors);
                var actionCount = 0;
                ValidateActions(rule, rule.Actions, counters, errors, new HashSet<PassiveActionDefinition>(), 0, ref actionCount);
            }
            if (passive.Auras != null) foreach (var rule in passive.Auras)
            {
                if (rule == null) { errors.Add("Null passive aura."); continue; }
                ValidateRule(rule.Id, rule.Gate, ids, errors);
                if (rule.Targets == null || !Enum.IsDefined(typeof(PassiveRelation), rule.Targets.Relation))
                    errors.Add(rule.Id + ": target filter required.");
                if (!Enum.IsDefined(typeof(PassiveScaling), rule.Scaling) ||
                    rule.Scaling != PassiveScaling.Constant && rule.MaximumUnits <= 0)
                    errors.Add(rule.Id + ": invalid scaling.");
                if (rule.Scaling == PassiveScaling.OwnerCounter && !counters.Contains(rule.ScalingKey ?? ""))
                    errors.Add(rule.Id + ": counter has no writer.");
                if (rule.Scaling == PassiveScaling.FieldStatusStacks && string.IsNullOrWhiteSpace(rule.ScalingKey))
                    errors.Add(rule.Id + ": status tag required.");
                if (rule.Scaling == PassiveScaling.FieldStatusStacks && !Enum.IsDefined(typeof(PassiveRelation), rule.ScalingRelation))
                    errors.Add(rule.Id + ": invalid status-stack source relation.");
                if (rule.Scaling == PassiveScaling.OwnerStat && !Enum.IsDefined(typeof(StatId), rule.SourceStat))
                    errors.Add(rule.Id + ": invalid source stat.");
                if (rule.Modifiers == null || rule.Modifiers.Count == 0)
                    errors.Add(rule.Id + ": at least one modifier is required.");
                else foreach (var modifier in rule.Modifiers)
                {
                    var statModifier = modifier != null && modifier.ResolvedTarget == ModifierTarget.Stat &&
                        Enum.IsDefined(typeof(StatId), modifier.ResolvedStat) &&
                        (modifier.ResolvedOperation == ModifierOperation.PercentOfBase || modifier.ResolvedOperation == ModifierOperation.PercentagePoints ||
                         modifier.ResolvedOperation == ModifierOperation.Flat);
                    var bundleModifier = modifier != null && modifier.ResolvedTarget == ModifierTarget.StatBundle &&
                        Enum.IsDefined(typeof(StatBundleKind), modifier.ResolvedBundle) && modifier.ResolvedBundle != StatBundleKind.None;
                    var damageModifier = modifier != null &&
                        (modifier.ResolvedTarget == ModifierTarget.AnyDamageDealt || modifier.ResolvedTarget == ModifierTarget.UltimateDamageDealt ||
                         modifier.ResolvedTarget == ModifierTarget.AnyDamageReceived ||
                         modifier.ResolvedTarget == ModifierTarget.FamilyDamageDealt || modifier.ResolvedTarget == ModifierTarget.FamilyDamageReceived) &&
                        modifier.ResolvedOperation == ModifierOperation.PercentagePoints;
                    var finalReduction = modifier != null && modifier.ResolvedTarget == ModifierTarget.FinalDamageReduction &&
                        modifier.ResolvedOperation == ModifierOperation.PercentagePoints && modifier.Amount >= 0 && modifier.Amount <= 10000;
                    if (modifier == null || modifier.ScaleByStatusPotency ||
                        (!statModifier && !bundleModifier && !damageModifier && !finalReduction))
                        errors.Add(rule.Id + ": aura modifier target or operation is unsupported.");
                }
            }
            return errors;
        }

        private static void ValidateCommand(PassiveReactionDefinition rule, PassiveCommandDefinition command,
            HashSet<string> counters, List<string> errors)
        {
            if (command == null || !Enum.IsDefined(typeof(PassiveCommandKind), command.Kind) ||
                !Enum.IsDefined(typeof(PassiveValueSource), command.ValueSource) || command.Amount < 0 ||
                !Enum.IsDefined(typeof(PassiveEffectBindingField), command.EffectBinding))
            { errors.Add(rule.Id + ": invalid command."); return; }
            if (rule.ExecutionVersion == 1 && command.ScalingCoefficientBp < 0)
                errors.Add(rule.Id + ": computed value scale cannot be negative.");
            if (command.ValueSource == PassiveValueSource.Binding &&
                (rule.ExecutionVersion != 1 || string.IsNullOrWhiteSpace(command.ValueBinding)))
                errors.Add(rule.Id + ": a typed value source requires execution version one and a binding key.");
            if (command.EffectBinding != PassiveEffectBindingField.None &&
                (rule.ExecutionVersion != 1 || command.Kind != PassiveCommandKind.ExecuteEffect))
                errors.Add(rule.Id + ": computed effect fields require a version-one effect command.");
            if ((command.EffectBinding == PassiveEffectBindingField.DamageCoefficientBp ||
                command.EffectBinding == PassiveEffectBindingField.DamageFixedAmount) &&
                (command.Effect?.Kind != CardEffectOperationKind.Damage || command.Effect.Damage == null))
                errors.Add(rule.Id + ": damage value binding requires a damage recipe.");
            if ((command.EffectBinding == PassiveEffectBindingField.HealCoefficientBp ||
                command.EffectBinding == PassiveEffectBindingField.HealFixedAmount) && command.Effect?.Kind != CardEffectOperationKind.Heal)
                errors.Add(rule.Id + ": healing value binding requires a heal operation.");
            if (rule.ExecutionVersion == 1 && rule.Trigger == PassiveEventKind.BeforeDamage &&
                command.Kind != PassiveCommandKind.IncreaseCurrentAttackPercent &&
                command.Kind != PassiveCommandKind.IncreaseCurrentDamageDealtPercent &&
                command.Kind != PassiveCommandKind.IgnoreCurrentTargetCritDefensePercent &&
                command.Kind != PassiveCommandKind.IncreaseCurrentDamageDealtByBaseStatPercent && command.Kind != PassiveCommandKind.IncreaseCurrentCritChancePercent)
                errors.Add(rule.Id + ": BeforeDamage command cannot mutate authoritative state while calculating a hit.");
            if (command.ValueSource == PassiveValueSource.ActualGaugeLost &&
                (rule.Trigger != PassiveEventKind.GaugeChanged || !rule.RequireGaugeLoss))
                errors.Add(rule.Id + ": gauge-loss values require a gauge-loss trigger.");
            var critChanceFromMissingHealth = command.Kind == PassiveCommandKind.IncreaseCurrentCritChancePercent &&
                command.ValueSource == PassiveValueSource.OwnerMissingHealthPercent;
            if (command.ValueSource == PassiveValueSource.OwnerMissingHealthPercent &&
                (!critChanceFromMissingHealth || rule.Trigger != PassiveEventKind.BeforeDamage ||
                 rule.ActorRelation != PassiveRelation.Self || command.Amount > 10000))
                errors.Add(rule.Id + ": owner missing-health values require a self-owned Before Damage crit chance bonus capped at 100%.");
            if ((command.Kind == PassiveCommandKind.IncreaseCurrentAttackPercent ||
                 command.Kind == PassiveCommandKind.IncreaseCurrentDamageDealtPercent ||
                 command.Kind == PassiveCommandKind.IgnoreCurrentTargetCritDefensePercent ||
                 command.Kind == PassiveCommandKind.IncreaseCurrentDamageDealtByBaseStatPercent || command.Kind == PassiveCommandKind.IncreaseCurrentCritChancePercent) &&
                (rule.Trigger != PassiveEventKind.BeforeDamage ||
                 command.ValueSource != PassiveValueSource.Fixed && !critChanceFromMissingHealth))
                errors.Add(rule.Id + ": current damage modifiers require a fixed-value Before Damage reaction.");
            if (command.Kind == PassiveCommandKind.IncreaseCurrentDamageDealtByBaseStatPercent &&
                !Enum.IsDefined(typeof(StatId), command.ScalingStat))
                errors.Add(rule.Id + ": stat-scaled damage bonus requires a valid source stat.");
            if (command.Kind == PassiveCommandKind.IncreaseCurrentDamageDealtByBaseStatPercent &&
                command.ScalingCoefficientBp < 0)
                errors.Add(rule.Id + ": stat-scaled damage bonus modifier cannot be negative.");
            if (command.Kind == PassiveCommandKind.IgnoreCurrentTargetCritDefensePercent && command.Amount > 10000)
                errors.Add(rule.Id + ": crit-defense ignore cannot exceed 100%.");
            if (command.Kind == PassiveCommandKind.ExecuteEffect && command.Effect == null)
                errors.Add(rule.Id + ": effect command requires an operation.");
            if (command.Kind == PassiveCommandKind.ExecuteEffect && command.Effect?.Kind == CardEffectOperationKind.Summon)
            {
                if (command.Effect.Summon == null) errors.Add(rule.Id + ": Summon requires an entity definition.");
                else errors.AddRange(command.Effect.Summon.Validate());
                if (command.Effect.Target != EffectTargetScope.Self)
                    errors.Add(rule.Id + ": Summon must target its effect owner.");
            }
            if (command.Kind == PassiveCommandKind.SetAbilityVariant &&
                (string.IsNullOrWhiteSpace(command.SkillId) || string.IsNullOrWhiteSpace(command.VariantId)))
                errors.Add(rule.Id + ": variant command requires stable skill and variant ids.");
            if (command.Kind == PassiveCommandKind.ExecuteEffect && command.Effect?.Kind == CardEffectOperationKind.TransferStats)
                foreach (var error in EffectRecipeValidator.ValidateStatTransfer(command.Effect.StatTransfer))
                    errors.Add(rule.Id + ": " + error);
            if (command.Kind == PassiveCommandKind.IncrementCounter || command.Kind == PassiveCommandKind.SetCounter)
            {
                if (string.IsNullOrWhiteSpace(command.CounterKey) || command.CounterCap <= 0)
                    errors.Add(rule.Id + ": invalid counter key/cap.");
                else counters.Add(command.CounterKey);
            }
        }

        private static void ValidateActions(PassiveReactionDefinition rule, List<PassiveActionDefinition> actions,
            HashSet<string> counters, List<string> errors, HashSet<PassiveActionDefinition> path,
            int depth, ref int count)
        {
            if (actions == null) return;
            if (depth > 64) { errors.Add(rule.Id + ": composed action depth exceeds 64."); return; }
            foreach (var action in actions)
            {
                if (++count > 4096) { errors.Add(rule.Id + ": composed action expansion exceeds 4096."); return; }
                if (action == null || string.IsNullOrWhiteSpace(action.Id) || !Enum.IsDefined(typeof(PassiveActionKind), action.Kind))
                { errors.Add(rule.Id + ": composed action requires an id and supported kind."); continue; }
                if (!path.Add(action)) { errors.Add(rule.Id + ": composed action cycle at " + action.Id); continue; }
                if (action.Kind == PassiveActionKind.ForEachTarget && (action.MaximumTargets < 1 || action.MaximumTargets > 32))
                    errors.Add(rule.Id + "/" + action.Id + ": target loop bound must be between 1 and 32.");
                var query = action.Query;
                if (query != null && (!Enum.IsDefined(typeof(EffectTargetScope), query.BaseScope) ||
                    !Enum.IsDefined(typeof(TargetQuerySort), query.Sort) || query.Limit < 0 || query.Limit > 32 ||
                    query.SampleCount < 0 || query.SampleCount > 32 || query.Filter != null &&
                    !Enum.IsDefined(typeof(PassiveRelation), query.Filter.Relation)))
                    errors.Add(rule.Id + "/" + action.Id + ": invalid bounded target query.");
                if ((action.Kind == PassiveActionKind.SpendResource || action.Kind == PassiveActionKind.TransferResource) &&
                    (string.IsNullOrWhiteSpace(action.ResourceKey) || action.Amount < 1 || action.ResourceCap < 0))
                    errors.Add(rule.Id + "/" + action.Id + ": resource operation requires key, positive amount, and nonnegative cap.");
                if (action.Kind == PassiveActionKind.ConsumeStatusLayers &&
                    (string.IsNullOrWhiteSpace(action.StatusRecipeId) || action.Amount < 1 ||
                    !Enum.IsDefined(typeof(StatusRemovalReason), action.RemovalReason)))
                    errors.Add(rule.Id + "/" + action.Id + ": layer consumption requires recipe, positive amount, and removal reason.");
                if (action.Kind == PassiveActionKind.Command)
                    foreach (var command in action.Commands ?? new List<PassiveCommandDefinition>())
                    {
                        ValidateCommand(rule, command, counters, errors);
                        if (command != null && (command.Kind == PassiveCommandKind.IncreaseCurrentAttackPercent ||
                            command.Kind == PassiveCommandKind.IncreaseCurrentDamageDealtPercent ||
                            command.Kind == PassiveCommandKind.IgnoreCurrentTargetCritDefensePercent ||
                            command.Kind == PassiveCommandKind.IncreaseCurrentDamageDealtByBaseStatPercent || command.Kind == PassiveCommandKind.IncreaseCurrentCritChancePercent))
                            errors.Add(rule.Id + "/" + action.Id + ": local hit modifiers belong to synchronous BeforeDamage commands.");
                    }
                ValidateActions(rule, action.Children, counters, errors, path, depth + 1, ref count);
                ValidateActions(rule, action.ElseChildren, counters, errors, path, depth + 1, ref count);
                path.Remove(action);
            }
        }

        private static void ValidateRule(string id, PassiveGate gate, HashSet<string> ids, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) errors.Add("Missing/duplicate passive rule id: " + id);
            // Unity's Flags enum field serializes its Everything selection as -1.
            var modes = gate == null ? (BattleModeMask)0 : gate.Modes;
            if (modes == (BattleModeMask)(-1)) modes = BattleModeMask.All;
            if (gate == null || gate.MinimumTier < 0 || gate.MaximumTier > 5 || gate.MinimumTier > gate.MaximumTier ||
                modes == 0 || (modes & ~BattleModeMask.All) != 0 ||
                !Enum.IsDefined(typeof(PassivePresence), gate.Presence))
                errors.Add(id + ": invalid availability gate.");
        }
    }
}
