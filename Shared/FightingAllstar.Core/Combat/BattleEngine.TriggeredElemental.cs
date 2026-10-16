using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    public static partial class BattleEngine
    {
        private static void ResolveNaturalExpiryAttacks(BattleState state, List<StatusInstance> expired, DeterministicRandom rng)
        {
            var pending = new List<FighterState>();
            foreach (var status in expired)
            {
                var source = FindFighter(state, status.SourceFighterId);
                var target = FindFighter(state, status.TargetFighterId);
                if (source == null || target == null || !target.IsAlive || target.Health <= 0 || target.IsReserve) continue;
                var card = new CardState { Id = "expiry:" + status.InstanceId, OwnerFighterId = source.Id };
                ResolveTriggeredElementalAttack(state, source, target, status.Recipe.NaturalExpiryAttack, card,
                    status.Snapshot.SourceAttack, 0, 0, rng, pending);
            }
            ApplyPendingDefeats(state, pending, "natural-expiry", null, null);
        }

        private static void ResolveAuthoredRetaliations(BattleState state, FighterState attacker,
            List<FighterState> originalTargets, CardState hostileCard, DeterministicRandom rng)
        {
            var pending = new List<FighterState>();
            foreach (var victim in originalTargets)
            {
                if (victim.Health <= 0 || !victim.IsAlive || attacker.Health <= 0) continue;
                var statuses = new List<StatusInstance>(victim.Statuses.Instances);
                foreach (var status in statuses)
                {
                    if (status.Recipe?.Retaliation == null) continue;
                    long damageTaken = 0;
                    long parent = 0;
                    foreach (var evt in state.Events)
                        if (evt.Kind == BattleEventKind.DamageApplied && evt.CardId == hostileCard.Id &&
                            evt.TargetId == victim.Id && evt.SourceId == attacker.Id &&
                            (evt.DamageOrigin == ElementalDamageOrigin.Card || evt.DamageOrigin == ElementalDamageOrigin.Reaction))
                        { damageTaken += evt.HealthLost; parent = evt.Id; }
                    if (damageTaken <= 0) continue;
                    var card = new CardState { Id = hostileCard.Id + ":retaliation:" + status.InstanceId,
                        OwnerFighterId = victim.Id };
                    ResolveTriggeredElementalAttack(state, victim, attacker, status.Recipe.Retaliation, card,
                        StatusSystem.GetEffectiveStats(victim).Attack, (int)Math.Min(int.MaxValue, damageTaken), parent, rng, pending);
                }
            }
            ApplyPendingDefeats(state, pending, hostileCard.Id, null, null);
        }

        private static void ResolveTriggeredElementalAttack(BattleState state, FighterState source, FighterState target,
            TriggeredElementalAttackDefinition attack, CardState card, int sourceAttack, int receivedDamage,
            long parent, DeterministicRandom rng, List<FighterState> pendingDefeats)
        {
            if (target.Statuses.Instances.Exists(s => s.Recipe?.EvadeAttacks == true))
            {
                AddEvent(state, BattleEventKind.AttackEvaded, source.Id, target.Id, card.Id, 0, "Evade");
                return;
            }
            StatusSystem.BreakFreezeOnAttack(target);
            var decision = state.Team(source.Side).ElementalReactionsEnabled
                ? ElementalReactionRules.Evaluate(state.ElementalRules, target, attack.Element) : null;
            var family = attack.ReceivedDamageCoefficientBp > 0 ? DamageFamily.ElementalReaction : DamageFamily.Normal;
            var policy = StatusSystem.BuildDamagePolicy(source, target, family);
            policy.CannotCrit = policy.CannotBlock = true;
            policy.DamageCap = state.ElementalRules?.ReactionDamageCap ?? int.MaxValue;
            policy.AmplificationBp = decision?.Reaction.AmplificationBp ?? 10000;
            if (attack.Explosion) policy.ExplosionFactorBp = TeamExplosionFactor(state, source);
            ElementalReactionRules.ApplyElementPolicy(policy, attack.Element, source, target, state.ElementalRules);
            var damage = DamageResolver.Resolve(new DamagePacket { BaseAmount = family == DamageFamily.Normal ? sourceAttack : receivedDamage,
                CoefficientBp = family == DamageFamily.Normal ? attack.AttackCoefficientBp : attack.ReceivedDamageCoefficientBp, Policy = policy },
                StatusSystem.GetEffectiveStats(source), StatusSystem.GetEffectiveStats(target), target.Health, target.Shield, -1, -1);
            if (!target.CannotDie) target.Health = damage.RemainingHealth;
            target.Shield = damage.RemainingShield;
            ConsumeBarrierShield(target, damage.ShieldLost);
            var contact = new ElementalContactCommit { Target = target, Decision = decision, SourceAttack = sourceAttack,
                HealthDamage = target.CannotDie ? 0 : damage.HealthLost, ShieldDamage = damage.ShieldLost,
                HitIndex = 1, HitCount = 1, ParentEventId = parent };
            AddEvent(state, BattleEventKind.DamageApplied, source.Id, target.Id, card.Id, damage.CalculatedDamage, "Triggered elemental attack.");
            var evt = state.Events[state.Events.Count - 1];
            SetElementalFacts(evt, contact, card.Id);
            evt.DamageOrigin = ElementalDamageOrigin.TriggeredAttack;
            evt.DamageElement = attack.Element;
            evt.HealthAfter = target.Health;
            evt.ShieldAfter = target.Shield;
            evt.HealthLost = contact.HealthDamage;
            evt.ShieldLost = damage.ShieldLost;
            evt.StatusesAfter = target.Statuses.Instances.ConvertAll(s => s.Clone());
            contact.ParentEventId = evt.Id;
            if (decision != null)
            {
                var context = new ElementalActionContext();
                if (decision.Reaction.Id == ElementalReactionId.Swirl)
                    ResolveSwirl(state, source, card, contact, context, rng, pendingDefeats);
                else
                {
                    CommitReactionFacts(state, source, card, contact, context);
                    ResolveReactionOutput(state, source, card, contact, context, pendingDefeats);
                }
            }
            else if (target.Health > 0 && !string.IsNullOrEmpty(attack.StatusRecipeId))
                ApplyCardUtilityEffectSingle(state, rng, source, target, new EffectDefinition {
                    Kind = EffectKind.ApplyStatus, StatusRecipe = StandardEffectDatabase.CreateStatusRecipes().Find(r => r.Id == attack.StatusRecipeId),
                    StatusDurationOverride = attack.StatusDuration, StatusStackCount = Math.Max(1, attack.StatusStackCount) }, card.Id, false,
                    contact.HealthDamage, contact.HealthDamage, contactSnapshotDamage: contact.HealthDamage);
            if (attack.SelfHealDamageBp > 0 && source.Health > 0)
                TryApplyRecovery(state, source, source, (int)((long)contact.HealthDamage * attack.SelfHealDamageBp / 10000),
                    StatusSystem.GetEffectiveStats(source).MaxHealth, card.Id, "Retaliation recovery.");
            long ownedHealthDamage = 0;
            foreach (var item in state.Events)
                if (item.Kind == BattleEventKind.DamageApplied && item.CardId == card.Id && item.SourceId == source.Id)
                    ownedHealthDamage += item.HealthLost;
            HealLowestAllyFromDamage(state, source, ownedHealthDamage, card.Id);
            if (target.Health <= 0 && !target.CannotDie && !pendingDefeats.Contains(target)) pendingDefeats.Add(target);
            // Generated attacks stop here: no card PG, generic reflect, stance counter or retaliation callback.
        }
    }
}
