using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    /// <summary>A bounded root failed. The caller must discard its working snapshot.</summary>
    public sealed class PassiveExecutionException : InvalidOperationException
    {
        public PassiveExecutionException(string message) : base(message) { }
    }

    [Serializable]
    public sealed class PassiveActivationReceipt
    {
        public string Key;
        public int Count;
        public int NextEligibleOwnerTurn;
        public PassiveActivationReceipt Clone() => (PassiveActivationReceipt)MemberwiseClone();
    }

    [Serializable]
    public sealed class StatusRemovalSnapshot
    {
        public StatusRemovalReason Reason;
        public StatusInstance Status;
        public int RemovedLayers;
        public int RemainingLayers;
        public StatusRemovalSnapshot Clone() => new StatusRemovalSnapshot {
            Reason = Reason, Status = Status?.Clone(), RemovedLayers = RemovedLayers, RemainingLayers = RemainingLayers };
    }

    public sealed partial class BattleState
    {
        public List<PassiveActivationReceipt> PassiveActivations = new List<PassiveActivationReceipt>();
        [NonSerialized, Newtonsoft.Json.JsonIgnore]
        internal PassiveExecutionSession PassiveSession;
    }

    public sealed partial class CardEffectContext
    {
        public int ExecutionVersion;
        public bool ReactionOrigin;
        public Dictionary<string, int> Values = new Dictionary<string, int>(StringComparer.Ordinal);
        public Dictionary<string, List<string>> TargetBindings = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        public StatusRemovalSnapshot RemovedStatus;
        public int PreviousResourceValue;
        public int ResourceValue;
        public string ResourceKey;
        public int HealingAmount;
        public PassiveFact EventFact;
        internal string ExecutingRuleId;
    }

    internal sealed class PassiveExecutionSession
    {
        internal readonly Queue<PassiveDispatchEntry> Queue = new Queue<PassiveDispatchEntry>();
        internal string RootId;
        internal bool IsDraining;
        internal int ReactionDepth;
        internal int Facts;
        internal int Commands;
        internal void Fact()
        {
            if (++Facts > 256) throw new PassiveExecutionException("Root " + RootId + " exceeds the deterministic budget of 256 passive facts.");
        }
        internal void Command()
        {
            if (++Commands > 1024) throw new PassiveExecutionException("Root " + RootId + " exceeds the deterministic budget of 1024 rule steps.");
        }
    }

    internal sealed class PassiveDispatchEntry
    {
        internal PassiveFact Fact;
        internal PassiveDamageTiming? Timing;
    }

    public sealed partial class StatusContainer
    {
        /// <summary>Consume oldest matching layers. Insufficient exact requests never mutate any instance.</summary>
        public List<StatusRemovalSnapshot> ConsumeLayers(string recipeId, int amount, string sourceFighterId = null,
            bool requireFullAmount = true, StatusRemovalReason reason = StatusRemovalReason.Consumed)
        {
            var result = new List<StatusRemovalSnapshot>();
            if (string.IsNullOrWhiteSpace(recipeId) || amount <= 0 || Instances == null) return result;
            var matches = Instances.FindAll(s => s?.Recipe != null && !s.Recipe.StructuralEntityMarker && s.RecipeId == recipeId &&
                (string.IsNullOrEmpty(sourceFighterId) || s.SourceFighterId == sourceFighterId));
            matches.Sort((a, b) => a.AppliedOrder != b.AppliedOrder ? a.AppliedOrder.CompareTo(b.AppliedOrder) :
                StringComparer.Ordinal.Compare(a.InstanceId, b.InstanceId));
            long available = 0;
            foreach (var status in matches) available += LayerCount(status);
            if (requireFullAmount && available < amount) return result;
            var remaining = amount;
            foreach (var status in matches)
            {
                if (remaining <= 0) break;
                var before = LayerCount(status);
                var taken = Math.Min(before, remaining);
                var snapshot = new StatusRemovalSnapshot { Status = status.Clone(), RemovedLayers = taken,
                    RemainingLayers = before - taken, Reason = reason };
                result.Add(snapshot);
                if (taken == before) Instances.Remove(status); else status.StackCount = before - taken;
                remaining -= taken;
            }
            var orphans = new List<StatusInstance>();
            RemoveOrphans(orphans);
            foreach (var orphan in orphans) result.Add(new StatusRemovalSnapshot {
                Status = orphan, RemovedLayers = LayerCount(orphan), RemainingLayers = 0, Reason = reason });
            return result;
        }

        internal static int LayerCount(StatusInstance status) =>
            status?.Recipe?.Stacking == StatusStackingPolicy.AddStacks ? Math.Max(1, status.StackCount) : 1;
    }

    public static partial class CharacterPassiveRuntime
    {
        private sealed class ScheduledReaction
        {
            internal FighterState Owner;
            internal ReactionBinding Binding;
            internal int OriginalOrder;
        }

        private static List<ScheduledReaction> ScheduledBindings(BattleState state, PassiveFact fact)
        {
            var result = new List<ScheduledReaction>();
            foreach (var owner in Fighters(state))
            {
                foreach (var binding in ReactionBindings(owner)) result.Add(new ScheduledReaction { Owner = owner,
                    Binding = binding, OriginalOrder = result.Count });
                // A removed status may own its own removal reaction; its frozen recipe is retained in the fact.
                var removed = fact.RemovedStatus;
                if (fact.Kind == PassiveEventKind.StatusRemoved && fact.TargetId == owner.Id &&
                    removed?.RemainingLayers == 0 && removed.Status?.Recipe?.Reactions != null)
                    foreach (var rule in removed.Status.Recipe.Reactions)
                        if (rule != null) result.Add(new ScheduledReaction { Owner = owner, OriginalOrder = result.Count,
                            Binding = new ReactionBinding { Rule = rule, StatusOwned = true, Status = removed.Status,
                                SourceId = StatusBindingSource(removed.Status, rule) } });
            }
            result.Sort((a, b) =>
            {
                var ar = a.Binding.Rule; var br = b.Binding.Rule;
                var phase = (ar?.ExecutionVersion == 1 ? ar.Phase : PassiveExecutionPhase.Normal)
                    .CompareTo(br?.ExecutionVersion == 1 ? br.Phase : PassiveExecutionPhase.Normal);
                if (phase != 0) return phase;
                var priority = (br?.ExecutionVersion == 1 ? br.Priority : 0).CompareTo(ar?.ExecutionVersion == 1 ? ar.Priority : 0);
                if (priority != 0) return priority;
                var side = a.Owner.Side.CompareTo(b.Owner.Side); if (side != 0) return side;
                var index = a.Owner.TeamIndex.CompareTo(b.Owner.TeamIndex); if (index != 0) return index;
                // A shipped version-zero kit retains its exact character/status command order.
                if (ar?.ExecutionVersion == 0 && br?.ExecutionVersion == 0) return a.OriginalOrder.CompareTo(b.OriginalOrder);
                if (ar?.ExecutionVersion != br?.ExecutionVersion) return (ar?.ExecutionVersion ?? 0).CompareTo(br?.ExecutionVersion ?? 0);
                var source = StringComparer.Ordinal.Compare(a.Binding.SourceId, b.Binding.SourceId);
                if (source != 0) return source;
                var identity = StringComparer.Ordinal.Compare(ar?.Id, br?.Id);
                return identity != 0 ? identity : a.OriginalOrder.CompareTo(b.OriginalOrder);
            });
            return result;
        }

        private static bool GateAvailableAtPendingDefeat(BattleState state, FighterState owner, PassiveGate gate) =>
            gate != null && owner.ConstellationTier >= gate.MinimumTier && owner.ConstellationTier <= gate.MaximumTier &&
            (gate.Modes & state.Mode) != 0 && (gate.Presence == PassivePresence.LivingRoster ||
                gate.Presence == PassivePresence.LivingActive && !owner.IsReserve ||
                gate.Presence == PassivePresence.LivingReserve && owner.IsReserve);

        private static string StatusBindingSource(StatusInstance status, PassiveReactionDefinition rule)
        {
            var recipe = status.RecipeId ?? status.Recipe?.Id ?? "status";
            return rule.ExecutionVersion == 1 ? recipe + ":" + status.InstanceId : recipe;
        }

        private sealed class RootLease : IDisposable
        {
            private BattleState _state;
            private readonly bool _owns;
            internal RootLease(BattleState state, bool owns) { _state = state; _owns = owns; }
            public void Dispose()
            {
                if (_state != null && _owns) _state.PassiveSession = null;
                _state = null;
            }
        }

        /// <summary>All effects and nested facts inside the lease share one deterministic budget.</summary>
        public static IDisposable BeginRootResolution(BattleState state, string rootActionId)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var owns = state.PassiveSession == null;
            if (owns) state.PassiveSession = new PassiveExecutionSession {
                RootId = rootActionId ?? "lifecycle:" + (state.PassiveEventSequence + 1) };
            return new RootLease(state, owns);
        }

        public static void EmitFact(BattleState state, PassiveFact fact)
        {
            if (state == null || fact == null) return;
            Dispatch(state, fact.Clone());
        }

        public static void NotifyAction(BattleState state, PassiveEventKind kind, FighterState actor, FighterState target,
            string rootActionId, bool cardOrigin = true, bool isUltimate = false,
            CardCategory category = CardCategory.Attack, int rank = 1)
        {
            if (kind != PassiveEventKind.BeforeAction && kind != PassiveEventKind.AfterAction)
                throw new ArgumentOutOfRangeException(nameof(kind));
            EmitFact(state, new PassiveFact { Kind = kind, TurnSide = state.ActingSide, ActorId = actor?.Id,
                TargetId = target?.Id, RootActionId = rootActionId, CardOrigin = cardOrigin,
                IsUltimate = isUltimate, CardCategory = category, CardRank = rank, VersionedLifecycleOnly = true });
        }

        public static void NotifyStatusApplied(BattleState state, FighterState source, FighterState target,
            StatusInstance status, string rootActionId = null, bool cardOrigin = false)
        {
            if (state == null || target == null || status == null) return;
            EmitFact(state, new PassiveFact { Kind = PassiveEventKind.StatusApplied, TurnSide = state.ActingSide,
                ActorId = source?.Id, TargetId = target.Id, RootActionId = rootActionId, CardOrigin = cardOrigin,
                AppliedStatus = status.Clone(), VersionedLifecycleOnly = true });
        }

        public static void NotifyStatusRemoved(BattleState state, FighterState source, FighterState target,
            IReadOnlyList<StatusRemovalSnapshot> removals, string rootActionId = null, bool cardOrigin = false)
        {
            if (state == null || target == null || removals == null) return;
            foreach (var removal in removals)
                if (removal?.Status != null && removal.RemovedLayers > 0)
                    EmitFact(state, new PassiveFact { Kind = PassiveEventKind.StatusRemoved, TurnSide = state.ActingSide,
                        ActorId = source?.Id, TargetId = target.Id, RootActionId = rootActionId, CardOrigin = cardOrigin,
                        RemovedStatus = removal.Clone(), VersionedLifecycleOnly = true });
        }

        public static void RecordStatusRemovals(BattleState state, FighterState source, FighterState target,
            IReadOnlyList<StatusInstance> previous, StatusRemovalReason reason, string rootActionId = null,
            bool cardOrigin = false)
        {
            if (target == null || previous == null) return;
            var removals = new List<StatusRemovalSnapshot>();
            foreach (var before in previous)
            {
                if (before?.Recipe == null) continue;
                var after = target.Statuses.Instances.Find(s => s?.InstanceId == before.InstanceId);
                var remaining = after == null ? 0 : StatusContainer.LayerCount(after);
                var removed = StatusContainer.LayerCount(before) - remaining;
                if (removed > 0) removals.Add(new StatusRemovalSnapshot { Status = before.Clone(),
                    Reason = reason, RemovedLayers = removed, RemainingLayers = remaining });
            }
            NotifyStatusRemoved(state, source, target, removals, rootActionId, cardOrigin);
        }

        public static void NotifyHealed(BattleState state, FighterState source, FighterState target, int amount,
            string rootActionId = null, bool cardOrigin = false)
        {
            if (state == null || target == null || amount <= 0) return;
            EmitFact(state, new PassiveFact { Kind = PassiveEventKind.Healed, TurnSide = state.ActingSide,
                ActorId = source?.Id, TargetId = target.Id, RootActionId = rootActionId, CardOrigin = cardOrigin,
                HealingAmount = amount, VersionedLifecycleOnly = true });
        }

        public static void NotifyReserveEntered(BattleState state, FighterState target)
        {
            if (state == null || target == null) return;
            EmitFact(state, new PassiveFact { Kind = PassiveEventKind.ReserveEntered, TurnSide = state.ActingSide,
                ActorId = target.Id, TargetId = target.Id, VersionedLifecycleOnly = true });
        }

        public static int ChangeResource(BattleState state, FighterState owner, string key, int delta,
            int cap = int.MaxValue, string rootActionId = null)
        {
            if (state == null || owner == null || !owner.IsAlive || string.IsNullOrWhiteSpace(key) || cap < 0) return 0;
            var before = Counter(owner, key);
            var after = (int)Math.Max(0L, Math.Min(cap, (long)before + delta));
            if (after == before) return 0;
            SetResource(owner, key, after);
            EmitResource(state, owner, key, before, after, rootActionId);
            return after - before;
        }

        public static bool TrySpendResource(BattleState state, FighterState owner, string key, int amount,
            string rootActionId = null)
        {
            if (state == null || owner == null || !owner.IsAlive || amount <= 0 || string.IsNullOrWhiteSpace(key) ||
                Counter(owner, key) < amount) return false;
            ChangeResource(state, owner, key, -amount, int.MaxValue, rootActionId);
            return true;
        }

        public static bool TryTransferResource(BattleState state, FighterState source, FighterState target,
            string key, int amount, int cap = int.MaxValue, string rootActionId = null)
        {
            if (state == null || source == null || target == null || source == target || !source.IsAlive ||
                !target.IsAlive || amount <= 0 || cap < 0 || string.IsNullOrWhiteSpace(key)) return false;
            var sourceBefore = Counter(source, key); var targetBefore = Counter(target, key);
            if (sourceBefore < amount || (long)targetBefore + amount > cap) return false;
            // Both values commit before either observer runs.
            SetResource(source, key, sourceBefore - amount); SetResource(target, key, targetBefore + amount);
            using (BeginRootResolution(state, rootActionId))
            {
                EmitResource(state, source, key, sourceBefore, sourceBefore - amount, rootActionId);
                EmitResource(state, target, key, targetBefore, targetBefore + amount, rootActionId);
            }
            return true;
        }

        private static void SetResource(FighterState owner, string key, int value)
        {
            var item = owner.PassiveCounters.Find(c => c.Key == key);
            if (item == null) { item = new PassiveCounterState { Key = key }; owner.PassiveCounters.Add(item); }
            item.Value = value;
        }

        private static void EmitResource(BattleState state, FighterState owner, string key, int before, int after, string root)
        {
            Emit(state, BattleEventKind.PassiveTriggered, owner.Id, owner.Id, root, after - before,
                "Resource " + key + ": " + before + " → " + after);
            EmitFact(state, new PassiveFact { Kind = PassiveEventKind.ResourceChanged, TurnSide = state.ActingSide,
                ActorId = owner.Id, TargetId = owner.Id, RootActionId = root, ResourceKey = key,
                PreviousResourceValue = before, ResourceValue = after });
        }

        private static string ActivationRuleKey(FighterState owner, ReactionBinding binding) =>
            owner.Id + "\0" + binding.SourceId + "\0" + binding.Rule.Id;

        private static string ActivationScopeKey(BattleState state, FighterState owner, PassiveFact fact, PassiveLimitScope scope)
        {
            switch (scope)
            {
                case PassiveLimitScope.Battle: return "battle";
                case PassiveLimitScope.OwnerTurn: return "turn:" + owner.OwnerTurnsCompleted;
                case PassiveLimitScope.RootAction: return "root:" + (fact.RootActionId ?? state.PassiveSession?.RootId ?? fact.Sequence.ToString());
                case PassiveLimitScope.RootActionTarget: return "root:" + (fact.RootActionId ?? state.PassiveSession?.RootId ?? fact.Sequence.ToString()) + ":" + fact.TargetId;
                default: return "event:" + fact.Sequence;
            }
        }

        private static bool CanActivate(BattleState state, FighterState owner, ReactionBinding binding, PassiveFact fact, out string reason)
        {
            reason = null;
            var rule = binding.Rule;
            if (rule.ExecutionVersion == 0)
            {
                if (!fact.VersionedLifecycleOnly) return true;
                reason = "Production lifecycle hook requires execution version one; legacy declaration remains inactive.";
                return false;
            }
            if (rule.ExecutionVersion != 1) throw new PassiveExecutionException("Unsupported execution version for " + rule.Id);
            if (fact.ReactionOrigin && !rule.AllowReactionOrigin) { reason = "Reaction-origin facts are excluded."; return false; }
            if (!string.IsNullOrEmpty(rule.EventResourceKey) && rule.EventResourceKey != fact.ResourceKey ||
                !string.IsNullOrEmpty(rule.EventStatusRecipeId) && rule.EventStatusRecipeId !=
                    (fact.RemovedStatus?.Status?.RecipeId ?? fact.AppliedStatus?.RecipeId) ||
                rule.FilterRemovalReason && rule.RemovalReason != fact.RemovedStatus?.Reason)
            { reason = "Resource/status lifecycle filter did not match."; return false; }
            var prefix = ActivationRuleKey(owner, binding);
            var cooldown = state.PassiveActivations.Find(r => r.Key == prefix + "\0cooldown");
            if (cooldown != null && owner.OwnerTurnsCompleted < cooldown.NextEligibleOwnerTurn)
            { reason = "Cooldown: eligible at owner turn " + cooldown.NextEligibleOwnerTurn + "."; return false; }
            if (rule.LimitScope != PassiveLimitScope.None)
            {
                var key = prefix + "\0" + ActivationScopeKey(state, owner, fact, rule.LimitScope);
                var receipt = state.PassiveActivations.Find(r => r.Key == key);
                if (rule.MaximumActivations <= 0 || receipt != null && receipt.Count >= rule.MaximumActivations)
                { reason = "Activation limit reached for " + rule.LimitScope + "."; return false; }
            }
            return true;
        }

        private static void RecordActivation(BattleState state, FighterState owner, ReactionBinding binding, PassiveFact fact)
        {
            var rule = binding.Rule;
            if (rule.ExecutionVersion != 1) return;
            var prefix = ActivationRuleKey(owner, binding);
            if (rule.LimitScope != PassiveLimitScope.None)
            {
                var key = prefix + "\0" + ActivationScopeKey(state, owner, fact, rule.LimitScope);
                var receipt = state.PassiveActivations.Find(r => r.Key == key);
                if (receipt == null) { receipt = new PassiveActivationReceipt { Key = key }; state.PassiveActivations.Add(receipt); }
                receipt.Count++;
            }
            if (rule.CooldownOwnerTurns > 0)
            {
                var key = prefix + "\0cooldown";
                var receipt = state.PassiveActivations.Find(r => r.Key == key);
                if (receipt == null) { receipt = new PassiveActivationReceipt { Key = key }; state.PassiveActivations.Add(receipt); }
                receipt.NextEligibleOwnerTurn = (int)Math.Min(int.MaxValue, (long)owner.OwnerTurnsCompleted + rule.CooldownOwnerTurns);
            }
        }

        private static bool ExecuteActions(BattleState state, FighterState owner, CardEffectContext context,
            List<PassiveActionDefinition> actions, int depth = 0)
        {
            if (actions == null) return false;
            if (depth > 64) throw new PassiveExecutionException("Rule action depth exceeds 64.");
            var any = false;
            foreach (var node in actions)
            {
                if (node == null) continue;
                state.PassiveSession.Command();
                var beforeEvents = state.Events.Count;
                var conditionStart = context.ConditionTrace?.Count ?? 0;
                var passed = CardEffectSystem.ConditionsPass(node.Conditions, context, context.SelectedTarget);
                var nodeConditions = context.ConditionTrace?.Skip(conditionStart).Select(c => c.Clone()).ToList() ?? new List<PassiveConditionTrace>();
                var selectedTargets = new List<string>();
                var operationTarget = context.SelectedTarget?.Id;
                var resourceKey = node.Kind == PassiveActionKind.SpendResource || node.Kind == PassiveActionKind.TransferResource ? node.ResourceKey :
                    node.Kind == PassiveActionKind.Command && (node.Commands?.FirstOrDefault()?.Kind == PassiveCommandKind.SetCounter || node.Commands?.FirstOrDefault()?.Kind == PassiveCommandKind.IncrementCounter)
                        ? node.Commands[0].CounterKey : null;
                var resourceBefore = string.IsNullOrEmpty(resourceKey) ? 0 : Counter(owner, resourceKey);
                var resourceAfter = resourceBefore;
                var outcome = passed ? "Completed" : "Condition failed";
                var branch = passed ? "Success" : "Else";
                var success = passed;
                if (!passed)
                    any |= ExecuteActions(state, owner, context, node.ElseChildren, depth + 1);
                else switch (node.Kind)
                {
                    case PassiveActionKind.Sequence:
                    case PassiveActionKind.Branch:
                        any |= ExecuteActions(state, owner, context, node.Children, depth + 1);
                        break;
                    case PassiveActionKind.Command:
                        foreach (var command in node.Commands ?? new List<PassiveCommandDefinition>())
                            any |= ExecuteActionCommand(state, owner, context, command);
                        resourceAfter = string.IsNullOrEmpty(resourceKey) ? 0 : Counter(owner, resourceKey);
                        any |= ExecuteActions(state, owner, context, node.Children, depth + 1);
                        break;
                    case PassiveActionKind.ForEachTarget:
                        if (node.MaximumTargets < 1 || node.MaximumTargets > 32)
                            throw new PassiveExecutionException("Target loop bound must be between 1 and 32.");
                        var query = node.Query?.Clone() ?? new TargetQueryDefinition { BaseScope = node.Target };
                        var targets = TargetQuerySystem.Resolve(query, context);
                        selectedTargets = targets.ConvertAll(t => t.Id);
                        outcome = targets.Count == 0 ? "No targets" : "Completed";
                        if (targets.Count > node.MaximumTargets)
                            throw new PassiveExecutionException("Target loop exceeds its declared maximum of " + node.MaximumTargets + ".");
                        var selected = context.SelectedTarget;
                        try
                        {
                            foreach (var target in targets)
                            {
                                context.SelectedTarget = target;
                                any |= ExecuteActions(state, owner, context, node.Children, depth + 1);
                            }
                        }
                        finally { context.SelectedTarget = selected; }
                        break;
                    case PassiveActionKind.SpendResource:
                        success = TrySpendResource(state, owner, node.ResourceKey, node.Amount, context.RootActionId);
                        resourceAfter = Counter(owner, node.ResourceKey);
                        outcome = success ? "Paid" : "Insufficient resource"; branch = success ? "Success" : "Failure";
                        any |= ExecuteActions(state, owner, context, success ? node.Children : node.ElseChildren, depth + 1);
                        break;
                    case PassiveActionKind.TransferResource:
                        success = TryTransferResource(state, owner, context.SelectedTarget, node.ResourceKey,
                            node.Amount, node.ResourceCap, context.RootActionId);
                        resourceAfter = Counter(owner, node.ResourceKey);
                        outcome = success ? "Transferred" : "Transfer rejected"; branch = success ? "Success" : "Failure";
                        any |= ExecuteActions(state, owner, context, success ? node.Children : node.ElseChildren, depth + 1);
                        break;
                    case PassiveActionKind.ConsumeStatusLayers:
                        var targetState = context.SelectedTarget ?? owner;
                        var snapshots = targetState.Statuses.ConsumeLayers(node.StatusRecipeId, node.Amount,
                            node.StatusOwnerOnly ? owner.Id : null, node.RequireFullAmount, node.RemovalReason);
                        var consumed = 0;
                        foreach (var snapshot in snapshots) if (snapshot.Status.RecipeId == node.StatusRecipeId) consumed += snapshot.RemovedLayers;
                        success = consumed > 0;
                        outcome = success ? "Consumed " + consumed + " layers" : "No matching layers"; branch = success ? "Success" : "Failure";
                        if (!string.IsNullOrEmpty(node.ResultBinding)) context.Values[node.ResultBinding] = consumed;
                        var savedSnapshot = context.RemovedStatus;
                        context.RemovedStatus = snapshots.Count > 0 ? snapshots[0].Clone() : null;
                        if (snapshots.Count > 0)
                        {
                            var item = Emit(state, BattleEventKind.StatusRemoved, owner.Id, targetState.Id,
                                context.RootActionId, consumed, "Status layers consumed: " + node.StatusRecipeId);
                            item.StatusesAfter = targetState.Statuses.Instances.ConvertAll(s => s.Clone());
                            NotifyStatusRemoved(state, owner, targetState, snapshots, context.RootActionId);
                        }
                        any |= ExecuteActions(state, owner, context, success ? node.Children : node.ElseChildren, depth + 1);
                        context.RemovedStatus = savedSnapshot;
                        break;
                    default: throw new PassiveExecutionException("Unsupported action kind: " + node.Kind);
                }
                any |= state.Events.Count != beforeEvents;
                if (state.CapturePassiveTrace)
                    state.PassiveTrace.Add(new PassiveRuleTrace { FactSequence = context.EventFact?.Sequence ?? 0,
                        Trigger = context.EventFact?.Kind.ToString(), OwnerId = owner.Id,
                        RuleId = context.ExecutingRuleId, ActionNodeId = node.Id, RootActionId = context.RootActionId,
                        ConditionsPassed = passed, Fired = success && state.Events.Count != beforeEvents,
                        Reason = node.Kind + ": " + (success ? "completed" : "guard/atomic requirement failed") + ". " + outcome,
                        Outcome = outcome, Branch = branch, ActorId = context.EventFact?.ActorId,
                        EventTargetId = context.EventFact?.TargetId, OperationTargetId = operationTarget,
                        ResourceKey = resourceKey, ResourceBefore = resourceBefore, ResourceAfter = resourceAfter,
                        SelectedTargets = selectedTargets, Values = new Dictionary<string, int>(context.Values), Conditions = nodeConditions,
                        FirstEventIndex = beforeEvents, EventCount = state.Events.Count - beforeEvents });
            }
            return any;
        }

        private static bool ExecuteActionCommand(BattleState state, FighterState owner, CardEffectContext context,
            PassiveCommandDefinition command)
        {
            if (command == null) return false;
            state.PassiveSession.Command();
            var before = state.Events.Count;
            var amount = ResolveCommandAmount(command, context);
            switch (command.Kind)
            {
                case PassiveCommandKind.IncrementCounter:
                    ChangeResource(state, owner, command.CounterKey, amount, command.CounterCap, context.RootActionId);
                    break;
                case PassiveCommandKind.SetCounter:
                    ChangeResource(state, owner, command.CounterKey, amount - Counter(owner, command.CounterKey), command.CounterCap, context.RootActionId);
                    break;
                case PassiveCommandKind.ChangePowerGauge:
                    var exactRefund = command.ValueSource == PassiveValueSource.ActualGaugeLost;
                    var gauge = CommitGauge(state, owner, owner, exactRefund ? 0 : amount,
                        context.RootActionId, false, false, exactRefund ? amount : (int?)null);
                    if (gauge != null) EmitFact(state, gauge);
                    break;
                case PassiveCommandKind.ExecuteEffect:
                    if (command.Effect != null) BattleEngine.ExecutePassiveOperation(state, owner, context,
                        BindEffect(command, amount), context.RootActionId, context.ExecutingRuleId);
                    break;
                case PassiveCommandKind.SetAbilityVariant:
                    AbilityVariantSystem.SetUnlocked(state, owner, command.SkillId, command.VariantId, command.VariantUnlocked);
                    break;
                default: throw new PassiveExecutionException("Command " + command.Kind + " is unavailable in an action composition.");
            }
            return state.Events.Count != before;
        }

        private static int ResolveCommandAmount(PassiveCommandDefinition command, CardEffectContext context)
        {
            long basis = 1;
            switch (command.ValueSource)
            {
                case PassiveValueSource.Fixed: break;
                case PassiveValueSource.ActualGaugeLost:
                    basis = command.Kind == PassiveCommandKind.ChangePowerGauge
                        ? Math.Max(0, -GaugeDeltaInBp(context.EventFact))
                        : Math.Max(0, -context.EventFact.GaugeDelta);
                    break;
                case PassiveValueSource.RemovedStatusLayers: basis = context.RemovedStatus?.RemovedLayers ?? 0; break;
                case PassiveValueSource.EventDamage: basis = context.EventFact?.DamageAmount ?? 0; break;
                case PassiveValueSource.EventHealing: basis = context.HealingAmount; break;
                case PassiveValueSource.Binding:
                    if (string.IsNullOrEmpty(command.ValueBinding) || !context.Values.TryGetValue(command.ValueBinding, out var value))
                        throw new PassiveExecutionException("Missing typed value binding: " + command.ValueBinding);
                    basis = value;
                    break;
                default: throw new PassiveExecutionException("Unknown command value source.");
            }
            // BigInteger keeps designer values saturating deterministically instead of overflowing long before division.
            var resolved = new System.Numerics.BigInteger(Math.Max(0, basis)) * Math.Max(0, command.Amount) *
                Math.Max(0, command.ScalingCoefficientBp) / 10000;
            return (int)System.Numerics.BigInteger.Min(int.MaxValue, resolved);
        }

        private static CardEffectOperationDefinition BindEffect(PassiveCommandDefinition command, int amount)
        {
            var effect = command.Effect.Clone();
            switch (command.EffectBinding)
            {
                case PassiveEffectBindingField.None: break;
                case PassiveEffectBindingField.Magnitude: effect.Magnitude = amount; break;
                case PassiveEffectBindingField.DamageCoefficientBp:
                    if (effect.Damage == null) throw new PassiveExecutionException("Damage coefficient binding requires a damage recipe.");
                    effect.Damage.CoefficientBp = amount; break;
                case PassiveEffectBindingField.DamageFixedAmount:
                    if (effect.Damage == null) throw new PassiveExecutionException("Damage amount binding requires a damage recipe.");
                    effect.Damage.FixedAmount = amount; break;
                case PassiveEffectBindingField.HealFixedAmount:
                    if (effect.Value == null) effect.Value = new EffectValueDefinition();
                    effect.Value.Source = EffectValueSource.Fixed; effect.Value.FixedAmount = amount; break;
                case PassiveEffectBindingField.HealCoefficientBp:
                    if (effect.Value == null) effect.Value = new EffectValueDefinition();
                    effect.Value.CoefficientBp = amount; break;
                default: throw new PassiveExecutionException("Unknown computed effect field.");
            }
            return effect;
        }
    }
}
