using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    public sealed class CombatPolicyReceipt
    {
        public string CapPolicyId;
        public bool CapReached;
        public int CapAmount;
        public bool BypassFatalSave;
    }

    /// <summary>One interception pipeline for card, generated, periodic and reflected contacts.</summary>
    public static class CombatPolicySystem
    {
        public static void ApplyProfile(DamagePolicy policy, DamageProfileDefinition profile)
        {
            if (policy == null || profile == null) return;
            policy.AuthoredProfile = profile.Clone();
            // Explicit True semantics replace only the legacy family's implicit bypass defaults.
            // Authored bypasses then combine monotonically; a defender cannot re-enable a bypassed stage.
            if (profile.ExplicitTrueDamage && policy.Family == DamageFamily.True)
                policy.BypassDefense = policy.BypassResistance = policy.BypassGenericReduction = policy.BypassShield = false;
            policy.BypassDefense |= profile.BypassDefense;
            policy.BypassResistance |= profile.BypassResistance;
            policy.BypassGenericReduction |= profile.BypassGenericReduction;
            policy.BypassShield |= profile.BypassShield;
            policy.BypassConstruct |= profile.BypassConstruct;
            policy.BypassDamageCap |= profile.BypassDamageCap;
            policy.BypassIncomingCap |= profile.BypassDamageCap;
            policy.BypassFatalSave |= profile.BypassFatalSave;
        }

        public static CombatPolicyReceipt PrepareIncomingDamage(BattleState state, FighterState source,
            FighterState target, DamageResult damage, DamagePolicy damagePolicy, string rootActionId = null)
        {
            var receipt = new CombatPolicyReceipt { BypassFatalSave = damagePolicy?.BypassFatalSave == true };
            if (state == null || target == null || damage == null || damage.CalculatedDamage <= 0) return receipt;
            if (damage.FormulaDamage == 0)
            {
                damage.FormulaDamage = damage.CalculatedDamage;
                DamageResolver.Allocate(damage, damagePolicy ?? new DamagePolicy(), target.Health, target.Shield);
            }
            if (damage.ConstructAbsorbed == 0 && damagePolicy?.BypassConstruct != true)
            {
                damage.ConstructAbsorbed = BattleEntitySystem.Absorb(state, source, target, damage.FormulaDamage, rootActionId);
                if (damage.ConstructAbsorbed > 0)
                    DamageResolver.Allocate(damage, damagePolicy ?? new DamagePolicy(), target.Health, target.Shield);
            }
            if (target.CannotDie) return receipt;
            foreach (var policy in Policies(target))
            {
                if (policy.Kind != CombatPolicyKind.IncomingDamageCap) continue;
                if (policy.Bypassable && damagePolicy?.BypassIncomingCap == true)
                { Trace(state, target, policy.Id, "DamageCapReached", false, "Damage profile bypasses incoming caps.", rootActionId); continue; }
                if (!Eligible(state, target, source, policy))
                { Trace(state, target, policy.Id, "DamageCapReached", false, "Presence, mode, tier or condition guard did not match.", rootActionId); continue; }
                var maximum = Math.Max(1, StatusSystem.GetEffectiveStats(target).MaxHealth);
                var cap = (int)Math.Min(int.MaxValue, (long)maximum * Math.Max(0, Math.Min(10000, policy.MaximumHealthDamageBp)) / 10000);
                var absorbed = damage.ShieldAbsorbed;
                var directed = damage.HpDirectedDamage;
                if (directed <= 0 || directed < cap)
                { Trace(state, target, policy.Id, "DamageCapReached", false, "HP-directed contact " + directed + " is below cap " + cap + ".", rootActionId); continue; }
                if (receipt.CapReached && cap >= receipt.CapAmount) continue;
                receipt.CapReached = true; receipt.CapPolicyId = policy.Id; receipt.CapAmount = cap;
                // Interception may only reduce a previously resolved loss. It must not undo an
                // earlier survival rule or charge HP that a shield already absorbed.
                damage.HealthLost = Math.Min(damage.HealthLost, cap);
                damage.RemainingHealth = Math.Max(0, target.Health - damage.HealthLost);
                damage.CalculatedDamage = (int)Math.Min(int.MaxValue, (long)damage.ConstructAbsorbed + absorbed + cap);
                damage.WasCapped = true;
                damage.Executed = false;
            }
            return receipt;
        }

        public static bool NotifyDamageCommitted(BattleState state, FighterState source, FighterState target,
            DamageResult damage, CombatPolicyReceipt receipt, string rootActionId = null)
        {
            if (state == null || target == null || damage == null) return false;
            if (damage.CalculatedDamage > 0)
            {
                var previous = target.Statuses.Instances.ConvertAll(s => s.Clone());
                foreach (var status in previous)
                    if (status.Recipe?.BreakRule == StatusBreakRule.OnDamageTaken)
                        target.Statuses.RemoveInstance(status.InstanceId);
                long oldShield = 0, newShield = 0;
                foreach (var status in previous) oldShield += Math.Max(0, status.ShieldRemaining);
                foreach (var status in target.Statuses.Instances) newShield += Math.Max(0, status.ShieldRemaining);
                target.Shield = (int)Math.Max(0L, target.Shield - Math.Max(0L, oldShield - newShield));
                if (previous.Count != target.Statuses.Instances.Count)
                {
                    var removal = Emit(state, BattleEventKind.StatusRemoved, source, target, rootActionId,
                        previous.Count - target.Statuses.Instances.Count, "Status broken by damage.");
                    removal.StatusesAfter = target.Statuses.Instances.ConvertAll(s => s.Clone());
                    removal.ShieldAfter = target.Shield;
                    removal.ShieldChanged = oldShield != newShield;
                    CharacterPassiveRuntime.RecordStatusRemovals(state, source, target, previous,
                        StatusRemovalReason.Broken, rootActionId);
                }
            }
            if (target.CannotDie) return false;
            if (receipt?.CapReached == true)
            {
                Emit(state, BattleEventKind.PassiveTriggered, target, target, rootActionId, receipt.CapAmount,
                    "CapReached: " + receipt.CapPolicyId);
                Trace(state, target, receipt.CapPolicyId, "CapReached", true,
                    "Incoming HP-directed contact capped at " + receipt.CapAmount + ".", rootActionId);
                CharacterPassiveRuntime.EmitFact(state, new PassiveFact { Kind = PassiveEventKind.DamageCapReached,
                    ActorId = source?.Id, TargetId = target.Id, RootActionId = rootActionId,
                    DamageAmount = damage.HealthLost, TurnSide = state.ActingSide });
            }
            if (target.Health > 0 || !target.IsAlive) return false;
            Emit(state, BattleEventKind.PassiveTriggered, target, target, rootActionId, 0, "BeforeDefeat");
            // BeforeDefeat compositions are bypassable save hooks. An explicitly unbypassable
            // authored policy below may still save; ordinary callbacks cannot evade this flag.
            if (receipt?.BypassFatalSave != true)
                CharacterPassiveRuntime.EmitInterceptFact(state, new PassiveFact { Kind = PassiveEventKind.BeforeDefeat,
                    ActorId = source?.Id, TargetId = target.Id, RootActionId = rootActionId,
                    DamageAmount = damage.HealthLost, TurnSide = state.ActingSide });
            if (target.Health > 0) { damage.Executed = false; return true; }
            foreach (var policy in Policies(target))
            {
                if (policy.Kind != CombatPolicyKind.BeforeDefeat) continue;
                if (!Eligible(state, target, source, policy, true))
                { Trace(state, target, policy.Id, "BeforeDefeat", false, "Presence, mode, tier or condition guard did not match.", rootActionId); continue; }
                if (policy.Bypassable && receipt?.BypassFatalSave == true)
                { Trace(state, target, policy.Id, "BeforeDefeat", false, "Damage profile bypasses fatal saves.", rootActionId); continue; }
                var recoveredState = target;
                if (policy.CleanseDebuffs)
                {
                    recoveredState = target.Clone();
                    recoveredState.Statuses.Remove(StatusPolarity.Debuff, true);
                }
                var amount = (int)Math.Min(int.MaxValue, (long)Math.Max(1, StatusSystem.GetEffectiveStats(recoveredState).MaxHealth) *
                    Math.Max(0, Math.Min(10000, policy.HealMaxHealthBp)) / 10000);
                if (amount <= 0 || StatusSystem.IsRecoveryBlocked(recoveredState))
                { Trace(state, target, policy.Id, "BeforeDefeat", false, "No positive permitted recovery.", rootActionId); continue; }
                if (policy.ResourceCost > 0 && (string.IsNullOrWhiteSpace(policy.ResourceKey) ||
                    CharacterPassiveRuntime.Counter(target, policy.ResourceKey) < policy.ResourceCost))
                { Trace(state, target, policy.Id, "BeforeDefeat", false, "Insufficient resource; no partial cost or recovery committed.", rootActionId); continue; }
                var statusesBefore = target.Statuses.Instances.ConvertAll(s => s.Clone());
                if (policy.CleanseDebuffs)
                {
                    var removed = target.Statuses.Remove(StatusPolarity.Debuff, true);
                    if (removed > 0)
                    {
                        long shieldBefore = 0, shieldAfter = 0;
                        foreach (var status in statusesBefore) shieldBefore += Math.Max(0, status.ShieldRemaining);
                        foreach (var status in target.Statuses.Instances) shieldAfter += Math.Max(0, status.ShieldRemaining);
                        target.Shield = (int)Math.Max(0L, target.Shield - Math.Max(0L, shieldBefore - shieldAfter));
                        var removal = Emit(state, BattleEventKind.StatusRemoved, target, target, rootActionId, removed,
                            "BeforeDefeat cleanse: " + policy.Id);
                        removal.StatusesAfter = target.Statuses.Instances.ConvertAll(s => s.Clone());
                        removal.ShieldAfter = target.Shield;
                        removal.ShieldChanged = shieldBefore != shieldAfter;
                    }
                }
                // The complete save state is committed before resource observers run. A
                // ResourceChanged reaction sees both the new HP and the paid cost.
                target.Health = amount;
                damage.Executed = false;
                if (policy.ResourceCost > 0)
                    CharacterPassiveRuntime.TrySpendResource(state, target, policy.ResourceKey, policy.ResourceCost, rootActionId);
                var heal = Emit(state, BattleEventKind.HealApplied, target, target, rootActionId, amount,
                    "BeforeDefeat save: " + policy.Id);
                heal.HealthAfter = target.Health;
                CharacterPassiveRuntime.RecordStatusRemovals(state, target, target, statusesBefore,
                    StatusRemovalReason.Cleansed, rootActionId);
                CharacterPassiveRuntime.NotifyHealed(state, target, target, amount, rootActionId);
                Trace(state, target, policy.Id, "BeforeDefeat", true,
                    "Atomic cost " + policy.ResourceCost + " " + (policy.ResourceKey ?? "") + "; restored " + amount + " HP before defeat.", rootActionId);
                return true;
            }
            if (target.Health <= 0) BattleEntitySystem.SuppressUnavailableAuras(state);
            return false;
        }

        public static bool BlocksGaugeLoss(BattleState state, FighterState target, int delta, string rootActionId = null)
        {
            if (delta >= 0 || state == null || target == null) return false;
            foreach (var policy in Policies(target))
                if (policy.Kind == CombatPolicyKind.GaugeLossGuard)
                {
                    if (!Eligible(state, target, target, policy))
                    { Trace(state, target, policy.Id, "GaugeLossGuard", false, "Presence, mode, tier or condition guard did not match.", rootActionId); continue; }
                    Emit(state, BattleEventKind.PassiveTriggered, target, target, rootActionId, (int)Math.Min(int.MaxValue, -(long)delta),
                        "Gauge loss prevented: " + policy.Id);
                    Trace(state, target, policy.Id, "GaugeLossGuard", true,
                        "Prevented requested gauge loss " + -(long)delta + " before any mutation.", rootActionId);
                    return true;
                }
            return false;
        }

        private static List<CombatPolicyDefinition> Policies(FighterState target)
        {
            var result = new List<CombatPolicyDefinition>(target?.Definition?.Passive?.Policies ?? new List<CombatPolicyDefinition>());
            result.RemoveAll(p => p == null);
            result.Sort((a, b) => a.Priority != b.Priority ? b.Priority.CompareTo(a.Priority) : StringComparer.Ordinal.Compare(a.Id, b.Id));
            return result;
        }

        private static bool Eligible(BattleState state, FighterState owner, FighterState actor,
            CombatPolicyDefinition policy, bool pendingDefeat = false)
        {
            var gate = policy.Gate;
            if (gate == null || !owner.IsAlive || !pendingDefeat && owner.Health <= 0 ||
                owner.ConstellationTier < gate.MinimumTier || owner.ConstellationTier > gate.MaximumTier ||
                (gate.Modes & state.Mode) == 0 ||
                gate.Presence == PassivePresence.LivingActive && owner.IsReserve ||
                gate.Presence == PassivePresence.LivingReserve && !owner.IsReserve) return false;
            return CardEffectSystem.ConditionsPass(policy.Conditions, new CardEffectContext {
                Battle = state, EffectOwner = owner, Actor = actor ?? owner, SelectedTarget = owner }, owner);
        }

        private static BattleEvent Emit(BattleState state, BattleEventKind kind, FighterState source,
            FighterState target, string root, int amount, string message)
        {
            var result = new BattleEvent { Id = state.Events.Count + 1, Kind = kind, SourceId = source?.Id,
                TargetId = target?.Id, RootActionId = root, Amount = amount, Message = message };
            state.Events.Add(result); return result;
        }

        private static void Trace(BattleState state, FighterState owner, string policyId, string trigger,
            bool fired, string reason, string root)
        {
            if (!state.CapturePassiveTrace) return;
            state.PassiveTrace.Add(new PassiveRuleTrace { OwnerId = owner.Id, RuleId = policyId, Trigger = trigger,
                Fired = fired, ConditionsPassed = fired, Reason = reason, RootActionId = root,
                FirstEventIndex = fired ? Math.Max(0, state.Events.Count - 1) : state.Events.Count,
                EventCount = fired ? 1 : 0 });
        }
    }
}
