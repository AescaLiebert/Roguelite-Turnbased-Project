using System;
using System.Collections.Generic;
using System.Numerics;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    [Serializable]
    public sealed class StatusSnapshot
    {
        public int TriggeringHealthDamage;
        public int SourceAttack;
        public int TargetMaxHealth;
        public int FixedAmount;
        public StatusSnapshot Clone() => (StatusSnapshot)MemberwiseClone();
    }

    [Serializable]
    public sealed class StatusInstance
    {
        public string InstanceId;
        public string RecipeId;
        public string SourceFighterId;
        public TeamSide SourceSide;
        public string TargetFighterId;
        public string RootActionId;
        public StatusRecipeDefinition Recipe;
        public StatusSnapshot Snapshot = new StatusSnapshot();
        public int PotencyBp;
        public int StackCount = 1;
        public int RemainingDuration;
        public bool SkipNextDurationClock;
        public long AppliedOrder;

        public StatusInstance Clone() => new StatusInstance { InstanceId = InstanceId, RecipeId = RecipeId,
            SourceFighterId = SourceFighterId, SourceSide = SourceSide, TargetFighterId = TargetFighterId,
            RootActionId = RootActionId, Recipe = Recipe?.Clone(), Snapshot = Snapshot?.Clone(), PotencyBp = PotencyBp,
            StackCount = StackCount, RemainingDuration = RemainingDuration,
            SkipNextDurationClock = SkipNextDurationClock, AppliedOrder = AppliedOrder };
    }

    public enum StatusApplyOutcome { Rejected, Added, Refreshed, Replaced, Stacked }

    public sealed class StatusApplyRequest
    {
        public StatusRecipeDefinition Recipe;
        public string InstanceId;
        public string SourceFighterId;
        public TeamSide SourceSide;
        public string TargetFighterId;
        public string RootActionId;
        public StatusSnapshot Snapshot;
        public int PotencyBp;
        public int StackCount = 1;
        public int Duration;
        public bool SkipNextDurationClock;
        public long AppliedOrder;
    }

    public sealed class StatusApplyResult
    {
        public StatusApplyOutcome Outcome;
        public StatusInstance Instance;
        public string Reason;
        public bool Accepted => Outcome != StatusApplyOutcome.Rejected;
    }

    public sealed class StatusTick
    {
        public string StatusInstanceId;
        public string RecipeId;
        public string SourceFighterId;
        public string TargetFighterId;
        public DamageFamily Family;
        public int Amount;
        public bool ConsumeOnTrigger;
    }

    /// <summary>Owns all mutable status instances for one fighter.</summary>
    [Serializable]
    public sealed class StatusContainer
    {
        public List<StatusInstance> Instances = new List<StatusInstance>();

        public StatusContainer Clone()
        {
            var copy = new StatusContainer();
            if (Instances != null) foreach (var instance in Instances) copy.Instances.Add(instance?.Clone());
            return copy;
        }

        public StatusApplyResult Apply(StatusApplyRequest request)
        {
            var recipe = request?.Recipe;
            if (recipe == null || string.IsNullOrWhiteSpace(recipe.Id))
                return Rejected("A status recipe with a stable id is required.");
            if (string.IsNullOrWhiteSpace(request.TargetFighterId)) return Rejected("A target fighter id is required.");
            if (recipe.DurationClock != StatusDurationClock.Permanent && ResolveDuration(request) <= 0)
                return Rejected("A temporary status requires a positive duration.");
            if (Instances == null) Instances = new List<StatusInstance>();

            var matches = FindIdentityMatches(recipe, request.SourceFighterId);
            if (recipe.Stacking == StatusStackingPolicy.IndependentStacks)
                return AddIndependent(request, matches);
            if (matches.Count == 0) return Added(CreateInstance(request));

            var current = matches[matches.Count - 1];
            var incoming = CreateInstance(request);
            switch (recipe.Stacking)
            {
                case StatusStackingPolicy.RefreshDuration:
                    RefreshDuration(current, incoming);
                    return Result(StatusApplyOutcome.Refreshed, current);
                case StatusStackingPolicy.AddStacks:
                    var combinedStacks = Math.Min(Math.Max(1, recipe.MaxStacks),
                        current.StackCount + Math.Max(1, incoming.StackCount));
                    if (CompareStrength(incoming, current) > 0) ReplacePayload(current, incoming);
                    current.StackCount = combinedStacks;
                    RefreshDuration(current, incoming);
                    return Result(StatusApplyOutcome.Stacked, current);
                case StatusStackingPolicy.ReplaceAlways:
                    ReplacePayload(current, incoming);
                    current.RemainingDuration = incoming.RemainingDuration;
                    current.SkipNextDurationClock = incoming.SkipNextDurationClock;
                    return Result(StatusApplyOutcome.Replaced, current);
                default:
                    var stronger = CompareStrength(incoming, current) > 0;
                    if (stronger) ReplacePayload(current, incoming);
                    RefreshDuration(current, incoming);
                    return Result(stronger ? StatusApplyOutcome.Replaced : StatusApplyOutcome.Refreshed, current);
            }
        }

        public int Remove(StatusPolarity polarity, bool removeAll, string recipeId = null)
        {
            if (Instances == null) return 0;
            var removed = 0;
            for (var i = Instances.Count - 1; i >= 0; i--)
            {
                var instance = Instances[i];
                if (instance?.Recipe == null || !instance.Recipe.Dispellable || instance.Recipe.Polarity != polarity) continue;
                if (!string.IsNullOrEmpty(recipeId) && instance.RecipeId != recipeId) continue;
                Instances.RemoveAt(i);
                removed++;
                if (!removeAll) break;
            }
            return removed;
        }

        public bool RemoveInstance(string instanceId)
        {
            if (Instances == null || string.IsNullOrEmpty(instanceId)) return false;
            var index = Instances.FindIndex(instance => instance?.InstanceId == instanceId);
            if (index < 0) return false;
            Instances.RemoveAt(index);
            return true;
        }

        public int Count(StatusPolarity? polarity = null, string requiredTag = null, string recipeId = null,
            string sourceFighterId = null)
        {
            if (Instances == null) return 0;
            var count = 0;
            foreach (var instance in Instances)
            {
                if (instance?.Recipe == null || (polarity.HasValue && instance.Recipe.Polarity != polarity.Value)) continue;
                if (!string.IsNullOrEmpty(recipeId) && instance.RecipeId != recipeId) continue;
                if (!string.IsNullOrEmpty(sourceFighterId) && instance.SourceFighterId != sourceFighterId) continue;
                if (!string.IsNullOrEmpty(requiredTag) &&
                    (instance.Recipe.Tags == null || !instance.Recipe.Tags.Contains(requiredTag))) continue;
                count += instance.Recipe.Stacking == StatusStackingPolicy.AddStacks ? Math.Max(1, instance.StackCount) : 1;
            }
            return count;
        }

        public List<StatusInstance> Advance(StatusDurationClock clock, string sourceFighterId = null)
        {
            var expired = new List<StatusInstance>();
            if (Instances == null) return expired;
            for (var i = Instances.Count - 1; i >= 0; i--)
            {
                var instance = Instances[i];
                if (instance?.Recipe == null || instance.Recipe.DurationClock != clock ||
                    (clock == StatusDurationClock.SourceTurnEnd && instance.SourceFighterId != sourceFighterId)) continue;
                if (instance.SkipNextDurationClock) { instance.SkipNextDurationClock = false; continue; }
                if (--instance.RemainingDuration > 0) continue;
                expired.Add(instance.Clone());
                Instances.RemoveAt(i);
            }
            expired.Reverse();
            return expired;
        }

        public List<StatusTick> CollectTicks(StatusTickTiming timing)
        {
            var ticks = new List<StatusTick>();
            if (Instances == null) return ticks;
            foreach (var instance in Instances)
            {
                var periodic = instance?.Recipe?.PeriodicDamage;
                if (periodic == null || periodic.Timing != timing) continue;
                var basis = periodic.Scaling switch
                {
                    StatusSnapshotScaling.TriggeringHealthDamage => instance.Snapshot?.TriggeringHealthDamage ?? 0,
                    StatusSnapshotScaling.SourceAttack => instance.Snapshot?.SourceAttack ?? 0,
                    StatusSnapshotScaling.TargetMaxHealth => instance.Snapshot?.TargetMaxHealth ?? 0,
                    _ => periodic.FixedAmount > 0 ? periodic.FixedAmount : instance.Snapshot?.FixedAmount ?? 0
                };
                var amount = (int)BigInteger.Min(int.MaxValue, BigInteger.Divide(
                    new BigInteger(Math.Max(0, basis)) * Math.Max(0, periodic.CoefficientBp), 10000));
                ticks.Add(new StatusTick { StatusInstanceId = instance.InstanceId, RecipeId = instance.RecipeId,
                    SourceFighterId = instance.SourceFighterId, TargetFighterId = instance.TargetFighterId,
                    Family = periodic.Family, Amount = amount * Math.Max(1, instance.StackCount),
                    ConsumeOnTrigger = periodic.ConsumeOnTrigger });
            }
            return ticks;
        }

        private List<StatusInstance> FindIdentityMatches(StatusRecipeDefinition recipe, string sourceFighterId)
        {
            var matches = new List<StatusInstance>();
            foreach (var instance in Instances)
                if (instance != null && instance.RecipeId == recipe.Id &&
                    (recipe.Identity == StatusIdentityScope.Recipe || instance.SourceFighterId == sourceFighterId))
                    matches.Add(instance);
            matches.Sort((a, b) => a.AppliedOrder.CompareTo(b.AppliedOrder));
            return matches;
        }

        private StatusApplyResult AddIndependent(StatusApplyRequest request, List<StatusInstance> matches)
        {
            var cap = Math.Max(1, request.Recipe.MaxStacks);
            while (matches.Count >= cap)
            {
                var oldest = matches[0];
                Instances.Remove(oldest);
                matches.RemoveAt(0);
            }
            return Added(CreateInstance(request));
        }

        private StatusInstance CreateInstance(StatusApplyRequest request) => new StatusInstance {
            InstanceId = request.InstanceId, RecipeId = request.Recipe.Id, SourceFighterId = request.SourceFighterId,
            SourceSide = request.SourceSide, TargetFighterId = request.TargetFighterId, RootActionId = request.RootActionId,
            Recipe = request.Recipe.Clone(), Snapshot = request.Snapshot?.Clone() ?? new StatusSnapshot(),
            PotencyBp = request.PotencyBp != 0 ? request.PotencyBp : request.Recipe.DefaultPotencyBp,
            StackCount = Math.Min(Math.Max(1, request.Recipe.MaxStacks), Math.Max(1, request.StackCount)),
            RemainingDuration = request.Recipe.DurationClock == StatusDurationClock.Permanent ? int.MaxValue : ResolveDuration(request),
            SkipNextDurationClock = request.SkipNextDurationClock, AppliedOrder = request.AppliedOrder };

        private static int ResolveDuration(StatusApplyRequest request) => request.Duration > 0
            ? request.Duration : request.Recipe.DefaultDuration;

        private static int CompareStrength(StatusInstance left, StatusInstance right)
        {
            var result = left.PotencyBp.CompareTo(right.PotencyBp);
            if (result != 0) return result;
            result = SnapshotStrength(left.Snapshot).CompareTo(SnapshotStrength(right.Snapshot));
            if (result != 0) return result;
            return ModifierStrength(left.Recipe).CompareTo(ModifierStrength(right.Recipe));
        }

        private static long SnapshotStrength(StatusSnapshot snapshot) => snapshot == null ? 0L :
            Math.Max(Math.Max(snapshot.TriggeringHealthDamage, snapshot.SourceAttack),
                Math.Max(snapshot.TargetMaxHealth, snapshot.FixedAmount));

        private static long ModifierStrength(StatusRecipeDefinition recipe)
        {
            long value = 0;
            if (recipe?.Modifiers != null) foreach (var modifier in recipe.Modifiers)
                if (modifier != null) value += Math.Abs((long)modifier.Amount);
            return value;
        }

        private static void ReplacePayload(StatusInstance current, StatusInstance incoming)
        {
            current.SourceFighterId = incoming.SourceFighterId;
            current.SourceSide = incoming.SourceSide;
            current.RootActionId = incoming.RootActionId;
            current.Recipe = incoming.Recipe.Clone();
            current.Snapshot = incoming.Snapshot.Clone();
            current.PotencyBp = incoming.PotencyBp;
            current.StackCount = incoming.StackCount;
        }

        private static void RefreshDuration(StatusInstance current, StatusInstance incoming)
        {
            current.RemainingDuration = Math.Max(current.RemainingDuration, incoming.RemainingDuration);
            current.SkipNextDurationClock |= incoming.SkipNextDurationClock;
        }

        private StatusApplyResult Added(StatusInstance instance)
        {
            Instances.Add(instance);
            return Result(StatusApplyOutcome.Added, instance);
        }

        private static StatusApplyResult Result(StatusApplyOutcome outcome, StatusInstance instance) =>
            new StatusApplyResult { Outcome = outcome, Instance = instance };

        private static StatusApplyResult Rejected(string reason) =>
            new StatusApplyResult { Outcome = StatusApplyOutcome.Rejected, Reason = reason };
    }

    public static class StatusSystem
    {
        public static StatusApplyResult Apply(FighterState target, string sourceFighterId, TeamSide sourceSide,
            StatusRecipeDefinition recipe, string instanceId, string rootActionId, long appliedOrder,
            StatusSnapshot snapshot = null, int potencyBp = 0, int stackCount = 1, int duration = 0,
            bool skipNextDurationClock = false)
        {
            if (target == null || !target.IsAlive)
                return new StatusApplyResult { Outcome = StatusApplyOutcome.Rejected, Reason = "Target is not alive." };
            if (target.Statuses == null) target.Statuses = new StatusContainer();
            return target.Statuses.Apply(new StatusApplyRequest { Recipe = recipe, InstanceId = instanceId,
                SourceFighterId = sourceFighterId, SourceSide = sourceSide, TargetFighterId = target.Id,
                RootActionId = rootActionId, Snapshot = snapshot, PotencyBp = potencyBp, StackCount = stackCount,
                Duration = duration, SkipNextDurationClock = skipNextDurationClock, AppliedOrder = appliedOrder });
        }

        /// <summary>Compatibility adapter for prototype-authored status definitions.</summary>
        public static bool Apply(FighterState target, string sourceFighterId, StatusDefinition definition,
            string instanceId, long appliedOrder, int appliedOwnerTurnCount, TeamSide? sourceSide = null)
        {
            if (definition == null) return false;
            var behavior = definition.Modifiers != null && definition.Modifiers.Count > 0 ? StatusBehavior.Stat : StatusBehavior.None;
            if (definition.IsPeriodicDamage) behavior |= StatusBehavior.DamageOverTime;
            var recipe = new StatusRecipeDefinition { Id = definition.Id, NameKey = definition.NameKey,
                Polarity = definition.Polarity, Color = definition.Color, Behavior = behavior,
                Stacking = definition.MaxStacks > 1 ? StatusStackingPolicy.IndependentStacks : StatusStackingPolicy.RefreshStronger,
                DefaultDuration = Math.Max(1, definition.DurationOwnerTurns), MaxStacks = Math.Max(1, definition.MaxStacks),
                Dispellable = definition.Dispellable, BypassDebuffImmunity = definition.BypassDebuffImmunity,
                Tags = definition.Tags == null ? new List<string>() : new List<string>(definition.Tags),
                PeriodicDamage = definition.IsPeriodicDamage ? new PeriodicDamageDefinition { Family = definition.PeriodicDamageFamily,
                    Timing = definition.TickAtOwnerTurnStart ? StatusTickTiming.TargetTurnStart : StatusTickTiming.TargetTurnEnd,
                    Scaling = StatusSnapshotScaling.Fixed, FixedAmount = definition.SnapshotDamage } : null };
            if (definition.Modifiers != null) foreach (var modifier in definition.Modifiers) recipe.Modifiers.Add(modifier?.Clone());
            return Apply(target, sourceFighterId, sourceSide ?? target.Side, recipe, instanceId, null, appliedOrder,
                new StatusSnapshot { FixedAmount = definition.SnapshotDamage }, skipNextDurationClock:
                appliedOwnerTurnCount > target.OwnerTurnsCompleted).Accepted;
        }

        public static int Remove(FighterState target, StatusPolarity polarity, bool removeAll, string recipeId = null) =>
            target?.Statuses?.Remove(polarity, removeAll, recipeId) ?? 0;

        public static int Count(FighterState fighter, StatusPolarity? polarity = null, string requiredTag = null,
            string recipeId = null, string sourceFighterId = null) =>
            fighter?.Statuses?.Count(polarity, requiredTag, recipeId, sourceFighterId) ?? 0;

        public static bool HasRecipeFromSource(FighterState fighter, string recipeId, string sourceFighterId) =>
            Count(fighter, recipeId: recipeId, sourceFighterId: sourceFighterId) > 0;

        public static void AdvanceOwnerTurn(FighterState fighter)
        {
            if (fighter == null) return;
            fighter.OwnerTurnsCompleted++;
            fighter.Statuses?.Advance(StatusDurationClock.TargetTurnEnd);
        }

        public static bool IsCardUseBlocked(FighterState fighter, CardCategory category, int rank, bool isUltimate,
            bool includesCardEffect = false)
        {
            var statuses = fighter?.Statuses?.Instances;
            if (statuses == null) return false;
            var requested = category switch
            {
                CardCategory.Attack => CardCategoryMask.Attack,
                CardCategory.Debuff => CardCategoryMask.Debuff,
                CardCategory.Buff => CardCategoryMask.Buff,
                CardCategory.Recovery => CardCategoryMask.Recovery,
                CardCategory.Stance => CardCategoryMask.Stance,
                CardCategory.AttackDebuff => CardCategoryMask.Attack | CardCategoryMask.Debuff,
                _ => CardCategoryMask.None
            };
            if (isUltimate) requested |= CardCategoryMask.Ultimate;
            if (rank >= 2) requested |= CardCategoryMask.RankTwoOrThree;
            if (includesCardEffect) requested |= CardCategoryMask.CardEffects;
            foreach (var status in statuses)
                if (status?.Recipe != null && (status.Recipe.DisableMask & requested) != 0) return true;
            return false;
        }

        public static StatBlock GetEffectiveStats(FighterState fighter)
        {
            var baseStats = fighter?.Stats;
            if (baseStats == null) return new StatBlock();
            var effective = baseStats.Clone();
            var statuses = new List<StatusInstance>();
            if (fighter.Statuses?.Instances != null) statuses.AddRange(fighter.Statuses.Instances);
            if (fighter.PassiveContributions != null && fighter.PassiveContributions.Count > 0)
            {
                var passiveStats = new StatusRecipeDefinition();
                foreach (var contribution in fighter.PassiveContributions) passiveStats.Modifiers.Add(contribution.Modifier);
                statuses.Add(new StatusInstance { Recipe = passiveStats });
            }
            if (statuses.Count == 0) return effective;
            foreach (StatId stat in Enum.GetValues(typeof(StatId)))
            {
                var baseValue = baseStats.Get(stat);
                long flat = 0, percentOfBase = 0, points = 0, multiplier = 10000;
                foreach (var status in statuses)
                {
                    if (status?.Recipe?.Modifiers == null) continue;
                    foreach (var modifier in status.Recipe.Modifiers)
                    {
                        if (modifier == null || modifier.Target != ModifierTarget.Stat || modifier.Stat != stat) continue;
                        var stacks = status.Recipe.Stacking == StatusStackingPolicy.AddStacks ? Math.Max(1, status.StackCount) : 1;
                        var modifierAmount = ResolveModifierAmount(modifier, status);
                        switch (modifier.Operation)
                        {
                            case ModifierOperation.Flat: flat += (long)modifierAmount * stacks; break;
                            case ModifierOperation.PercentOfBase: percentOfBase += (long)modifierAmount * stacks; break;
                            case ModifierOperation.PercentagePoints: points += (long)modifierAmount * stacks; break;
                            case ModifierOperation.Multiplier:
                                for (var i = 0; i < stacks; i++) multiplier = (long)BigInteger.Min(int.MaxValue,
                                    BigInteger.Divide(new BigInteger(multiplier) * Math.Max(0, modifierAmount), 10000));
                                break;
                        }
                    }
                }
                var value = new BigInteger(baseValue) + flat + BigInteger.Divide(new BigInteger(baseValue) * percentOfBase, 10000);
                value = BigInteger.Divide(value * multiplier, 10000) + points;
                effective.Set(stat, (int)BigInteger.Max(BigInteger.Zero, BigInteger.Min(int.MaxValue, value)));
            }
            return effective;
        }

        public static DamagePolicy BuildDamagePolicy(FighterState attacker, FighterState defender, DamageFamily family)
        {
            var policy = new DamagePolicy { Family = family,
                BypassDefense = family == DamageFamily.True, BypassResistance = family == DamageFamily.True,
                BypassGenericReduction = family == DamageFamily.True, BypassShield = family == DamageFamily.True,
                BypassDamageCap = family == DamageFamily.Additional || family == DamageFamily.DamageOverTime,
                BypassSurviveAtOne = family == DamageFamily.Destructive };
            Accumulate(attacker, family, true, policy);
            Accumulate(defender, family, false, policy);
            return policy;
        }

        private static void Accumulate(FighterState fighter, DamageFamily family, bool dealt, DamagePolicy policy)
        {
            var statuses = fighter?.Statuses?.Instances;
            if (statuses == null) return;
            foreach (var status in statuses)
            {
                var modifiers = status?.Recipe?.Modifiers;
                if (modifiers == null) continue;
                var stacks = status.Recipe.Stacking == StatusStackingPolicy.AddStacks ? Math.Max(1, status.StackCount) : 1;
                foreach (var modifier in modifiers)
                {
                    if (modifier == null) continue;
                    var amount = ResolveModifierAmount(modifier, status) * stacks;
                    switch (modifier.Target)
                    {
                        case ModifierTarget.AnyDamageDealt when dealt:
                            if (amount >= 0) policy.OutgoingIncreaseBp += amount; else policy.OutgoingDecreaseBp += -amount; break;
                        case ModifierTarget.AnyDamageReceived when !dealt:
                            if (amount >= 0) policy.IncomingIncreaseBp += amount; else policy.IncomingDecreaseBp += -amount; break;
                        case ModifierTarget.FamilyDamageDealt when dealt && modifier.Family == family:
                            if (family == DamageFamily.Normal || family == DamageFamily.True || family == DamageFamily.Destructive)
                            { if (amount >= 0) policy.OutgoingIncreaseBp += amount; else policy.OutgoingDecreaseBp += -amount; }
                            else if (amount >= 0) policy.FamilyDealtIncreaseBp += amount; else policy.FamilyDealtDecreaseBp += -amount;
                            break;
                        case ModifierTarget.FamilyDamageReceived when !dealt && modifier.Family == family:
                            if (family == DamageFamily.Normal || family == DamageFamily.True || family == DamageFamily.Destructive)
                            { if (amount >= 0) policy.IncomingIncreaseBp += amount; else policy.IncomingDecreaseBp += -amount; }
                            else if (amount >= 0) policy.FamilyReceivedIncreaseBp += amount; else policy.FamilyReceivedDecreaseBp += -amount;
                            break;
                        case ModifierTarget.FinalDamageReduction when !dealt: policy.FinalReductionBp += amount; break;
                        case ModifierTarget.FlatDamageReduction when !dealt: policy.FlatReduction += amount; break;
                    }
                }
            }
        }

        private static int ResolveModifierAmount(StatModifierDefinition modifier, StatusInstance status)
        {
            if (!modifier.ScaleByStatusPotency) return modifier.Amount;
            var value = BigInteger.Divide(new BigInteger(status.PotencyBp) * modifier.PotencyCoefficientBp, 10000);
            return (int)BigInteger.Max(int.MinValue, BigInteger.Min(int.MaxValue, value));
        }
    }
}
