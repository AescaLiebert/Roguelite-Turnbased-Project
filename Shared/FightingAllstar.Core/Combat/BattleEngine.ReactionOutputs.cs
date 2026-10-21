using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    public static partial class BattleEngine
    {
        private static void ResolveReactionOutput(BattleState state, FighterState source, CardState card,
            ElementalContactCommit contact, ElementalActionContext context, List<FighterState> pendingDefeats)
        {
            var id = contact.Decision?.Reaction.Id ?? ElementalReactionId.None;
            switch (id)
            {
                case ElementalReactionId.Overload:
                    ResolveOverload(state, source, card, contact, context, pendingDefeats);
                    break;
                case ElementalReactionId.Shatter:
                    ResolveSingleReactionHit(state, source, card, contact, context, pendingDefeats);
                    break;
                case ElementalReactionId.Freeze:
                    if (!context.DeferredFreezes.Exists(item => item.Target.Id == contact.Target.Id))
                        context.DeferredFreezes.Add(contact);
                    break;
                case ElementalReactionId.ElectroCharged:
                    ApplyElementalReactionStatus(state, source, contact.Target, card, contact, ElementalReactionId.ElectroCharged);
                    SpreadElectroCharged(state, source, contact.Target, card, contact);
                    break;
                case ElementalReactionId.Burning:
                    ApplyElementalReactionStatus(state, source, contact.Target, card, contact, ElementalReactionId.Burning);
                    break;
                case ElementalReactionId.Crystallize:
                    ApplyReactionStatus(state, source, source, card, contact, "status.reaction.crystallize",
                        (int)Math.Min(int.MaxValue, (long)contact.HealthDamage + contact.ShieldDamage));
                    break;
                case ElementalReactionId.Superconduct:
                    foreach (var recipient in state.Team(contact.Target.Side).LivingActive())
                    {
                        if (recipient.Health <= 0 || Math.Abs(recipient.FormationSlot - contact.Target.FormationSlot) > 1 ||
                            !context.ReactionDamageRecipients.Add(recipient.Id + ":" + id)) continue;
                        var policy = StatusSystem.BuildDamagePolicy(source, recipient, DamageFamily.ElementalReaction);
                        ElementalReactionRules.ConfigureReactionDamagePolicy(policy, ElementalReactionId.Superconduct);
                        policy.CannotCrit = policy.CannotBlock = true;
                        policy.DamageCap = state.ElementalRules.ReactionDamageCap;
                        ElementalReactionRules.ApplyElementPolicy(policy, ElementId.Cryo, source, recipient, state.ElementalRules);
                        ElementalReactionRules.ApplyOwnReactionBonus(policy, source, context.CadenceDotBp);
                        var damage = DamageResolver.Resolve(new DamagePacket { BaseAmount = ElementalReactionRules.LevelDamage(source, state.ElementalRules),
                            CoefficientBp = contact.Decision.Reaction.AttackCoefficientBp, Policy = policy },
                            StatusSystem.GetEffectiveStats(source), StatusSystem.GetEffectiveStats(recipient),
                            recipient.Health, recipient.Shield, -1, -1);
                        var policyReceipt = CombatPolicySystem.PrepareIncomingDamage(state, source, recipient, damage, policy, card.Id);
                        if (!recipient.CannotDie) recipient.Health = damage.RemainingHealth;
                        recipient.Shield = damage.RemainingShield;
                        var damageStatusesBefore = recipient.Statuses.Instances.ConvertAll(s => s.Clone());
                        ConsumeBarrierShield(recipient, damage.ShieldLost);
                        var freezeBroken = StatusSystem.BreakFreezeOnAttack(recipient);
                        AddEvent(state, BattleEventKind.DamageApplied, source.Id, recipient.Id, card.Id, damage.CalculatedDamage, "Superconduct");
                        var evt = state.Events[state.Events.Count - 1];
                        SetElementalFacts(evt, contact, card.Id);
                        evt.DamageOrigin = ElementalDamageOrigin.Reaction;
                        evt.DamageElement = ElementId.Cryo;
                        evt.HealthAfter = recipient.Health;
                        evt.ShieldAfter = recipient.Shield;
                        evt.HealthLost = recipient.CannotDie ? 0 : damage.HealthLost;
                        evt.ShieldLost = damage.ShieldLost;
                        evt.FormulaDamage = damage.FormulaDamage;
                        evt.ConstructAbsorbed = damage.ConstructAbsorbed;
                        evt.StatusesAfter = recipient.Statuses.Instances.ConvertAll(s => s.Clone());
                        CharacterPassiveRuntime.RecordStatusRemovals(state, source, recipient, damageStatusesBefore,
                            StatusRemovalReason.Broken, card.Id);
                        CombatPolicySystem.NotifyDamageCommitted(state, source, recipient, damage, policyReceipt, card.Id);
                        if (freezeBroken) QueueShatter(state, recipient, evt, context);
                        ApplyElementalReactionStatus(state, source, recipient, card, contact, ElementalReactionId.Superconduct);
                        if (recipient.Health <= 0 && !recipient.CannotDie && !pendingDefeats.Contains(recipient)) pendingDefeats.Add(recipient);
                    }
                    break;
            }
            CharacterPassiveRuntime.Refresh(state);
        }

        private static void ResolveSingleReactionHit(BattleState state, FighterState source, CardState card,
            ElementalContactCommit contact, ElementalActionContext context, List<FighterState> pendingDefeats)
        {
            var target = contact.Target;
            if (target.Health <= 0 || !target.IsAlive) return;
            var reaction = contact.Decision.Reaction;
            var policy = StatusSystem.BuildDamagePolicy(source, target, DamageFamily.ElementalReaction);
            ElementalReactionRules.ConfigureReactionDamagePolicy(policy, reaction.Id);
            policy.CannotCrit = policy.CannotBlock = true;
            policy.DamageCap = state.ElementalRules.ReactionDamageCap;
            ElementalReactionRules.ApplyElementPolicy(policy, reaction.OutputElement, source, target, state.ElementalRules);
            ElementalReactionRules.ApplyOwnReactionBonus(policy, source, context.CadenceDotBp);
            var damage = DamageResolver.Resolve(new DamagePacket {
                BaseAmount = ElementalReactionRules.LevelDamage(source, state.ElementalRules),
                CoefficientBp = reaction.AttackCoefficientBp, Policy = policy },
                StatusSystem.GetEffectiveStats(source), StatusSystem.GetEffectiveStats(target),
                target.Health, target.Shield, -1, -1);
            var policyReceipt = CombatPolicySystem.PrepareIncomingDamage(state, source, target, damage, policy, card.Id);
            if (!target.CannotDie) target.Health = damage.RemainingHealth;
            target.Shield = damage.RemainingShield;
            var damageStatusesBefore = target.Statuses.Instances.ConvertAll(s => s.Clone());
            ConsumeBarrierShield(target, damage.ShieldLost);
            AddEvent(state, BattleEventKind.DamageApplied, source.Id, target.Id, card.Id, damage.CalculatedDamage, reaction.Id.ToString());
            var evt = state.Events[state.Events.Count - 1];
            SetElementalFacts(evt, contact, card.Id);
            evt.DamageOrigin = ElementalDamageOrigin.Reaction;
            evt.DamageElement = reaction.OutputElement;
            evt.HealthAfter = target.Health;
            evt.ShieldAfter = target.Shield;
            evt.HealthLost = target.CannotDie ? 0 : damage.HealthLost;
            evt.ShieldLost = damage.ShieldLost;
            evt.FormulaDamage = damage.FormulaDamage;
            evt.ConstructAbsorbed = damage.ConstructAbsorbed;
            evt.StatusesAfter = target.Statuses.Instances.ConvertAll(s => s.Clone());
            CharacterPassiveRuntime.RecordStatusRemovals(state, source, target, damageStatusesBefore,
                StatusRemovalReason.Broken, card.Id);
            CombatPolicySystem.NotifyDamageCommitted(state, source, target, damage, policyReceipt, card.Id);
            if (target.Health <= 0 && !target.CannotDie && !pendingDefeats.Contains(target)) pendingDefeats.Add(target);
            // Terminal damage does not trigger lifesteal, reflect or another attack/reaction chain.
        }

        // Freeze settles only after every damage packet and authored/passive follow-up of this action.
        // Otherwise later hits in the same action immediately shatter the Freeze that they just triggered.
        private static void ResolveDeferredFreezes(BattleState state, FighterState source, CardState card, ElementalActionContext context)
        {
            if (context.DeferredFreezes.Count == 0) return;
            var previousMaxHealth = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var contact in context.DeferredFreezes)
            {
                if (!contact.Target.IsAlive || contact.Target.Health <= 0) continue;
                previousMaxHealth[contact.Target.Id] = StatusSystem.GetEffectiveStats(contact.Target).MaxHealth;
                ApplyReactionStatus(state, source, contact.Target, card, contact, StatusSystem.FreezeRecipeId);
            }
            context.DeferredFreezes.Clear();
            CharacterPassiveRuntime.Refresh(state, previousMaxHealthOverrides: previousMaxHealth);
        }

        private static void SpreadElectroCharged(BattleState state, FighterState source, FighterState start,
            CardState card, ElementalContactCommit contact)
        {
            var visited = new HashSet<string> { start.Id };
            var queue = new Queue<FighterState>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var origin = queue.Dequeue();
                foreach (var neighbor in state.Team(start.Side).LivingActive())
                {
                    if (neighbor.Health <= 0 || Math.Abs(neighbor.FormationSlot - origin.FormationSlot) != 1 ||
                        !neighbor.ElementStatuses.Has(ElementId.Hydro) || !visited.Add(neighbor.Id)) continue;
                    queue.Enqueue(neighbor);
                    var spread = new ElementalContactCommit { Target = neighbor, SourceAttack = contact.SourceAttack,
                        ParentEventId = contact.ParentEventId, HitIndex = contact.HitIndex, HitCount = contact.HitCount,
                        Decision = new ElementalReactionDecision { IncomingElement = ElementId.Electro,
                            Reaction = new ElementalReactionDefinition { Id = ElementalReactionId.ElectroCharged,
                                ExistingElement = ElementId.Hydro, IncomingElement = ElementId.Electro, PreserveFuel = true } } };
                    AddEvent(state, BattleEventKind.ElementalReactionTriggered, source.Id, neighbor.Id, card.Id, 10000, "Electro-Charged spread");
                    var evt = state.Events[state.Events.Count - 1];
                    spread.ReactionEventId = evt.Id;
                    evt.ElementalReactionStatusAlreadyActive = neighbor.ElementStatuses.HasReaction(ElementalReactionId.ElectroCharged);
                    SetElementalFacts(evt, spread, card.Id);
                    evt.SourceId = source.Id;
                    evt.TargetIds.Add(origin.Id); // Public animation origin for the electrify arc.
                    evt.DamageElement = ElementId.Hydro;
                    evt.TriggerElement = ElementId.Electro;
                    ApplyElementalReactionStatus(state, source, neighbor, card, spread, ElementalReactionId.ElectroCharged);
                }
            }
        }

        private static bool ApplyReactionStatus(BattleState state, FighterState source, FighterState target,
            CardState card, ElementalContactCommit contact, string recipeId, int? barrierDamageBasis = null)
        {
            if (target.Health <= 0) return false;
            var recipe = GenshinStatusRecipes.CreateOutputs().Find(r => r.Id == recipeId);
            if (recipe == null) return false;
            var order = state.Events.Count + 1;
            var shieldBefore = target.Shield;
            var result = StatusSystem.Apply(target, source.Id, source.Side, recipe, "reaction:" + order,
                card.Id, order, new StatusSnapshot { SourceAttack = barrierDamageBasis ?? contact.SourceAttack,
                    FixedAmount = recipeId == "status.reaction.charged" ? ElementalReactionRules.LevelDamage(source, state.ElementalRules) : 0,
                    SourceMaxHealth = StatusSystem.GetEffectiveStats(source).MaxHealth,
                    TriggeringHealthDamage = contact.HealthDamage },
                skipNextDurationClock: target.Side == state.ActingSide);
            if (!result.Accepted)
            {
                if (recipeId == "status.debuff.freeze")
                {
                    var reactionEvent = state.Events.FindLast(e => e.Kind == BattleEventKind.ElementalReactionTriggered &&
                        e.CardId == card.Id && e.TargetId == target.Id && e.ElementalReaction == ElementalReactionId.Freeze);
                    if (reactionEvent != null) reactionEvent.ReactionOutputBlocked = true;
                }
                AddEvent(state, BattleEventKind.StatusImmuned, source.Id, target.Id, card.Id, 0, result.Reason);
                return false;
            }
            var barrier = GrantBarrierShield(target, recipe, result.Instance);
            AddEvent(state, BattleEventKind.StatusApplied, source.Id, target.Id, card.Id, 1, recipeId);
            var evt = state.Events[state.Events.Count - 1];
            SetElementalFacts(evt, contact, card.Id);
            evt.StatusInstanceId = result.Instance.InstanceId;
            evt.StatusRecipeId = recipeId;
            evt.StatusOutcome = result.Outcome;
            evt.StatusesAfter = target.Statuses.Instances.ConvertAll(s => s.Clone());
            if (barrier != 0 || shieldBefore != target.Shield) { evt.ShieldAfter = target.Shield; evt.ShieldChanged = true; }
            return true;
        }

        private static bool ApplyElementalReactionStatus(BattleState state, FighterState source, FighterState target,
            CardState card, ElementalContactCommit contact, ElementalReactionId reaction)
        {
            if (target == null || target.Health <= 0 || source == null) return false;
            var order = state.Events.Count + 1;
            var instanceId = "reaction:" + order;
            var fixedDamage = reaction == ElementalReactionId.ElectroCharged
                ? ElementalReactionRules.LevelDamage(source, state.ElementalRules) : 0;
            if (!target.ElementStatuses.ApplyReaction(reaction, source.Id, instanceId,
                contact?.SourceAttack ?? StatusSystem.GetEffectiveStats(source).Attack, fixedDamage,
                skipNextDurationClock: target.Side == state.ActingSide)) return false;

            if (contact?.ReactionEventId > 0)
            {
                var reactionEvent = state.Events.Find(candidate => candidate.Id == contact.ReactionEventId);
                if (reactionEvent != null) reactionEvent.ElementalReactionStatusApplied = true;
            }

            AddEvent(state, BattleEventKind.ElementStatusesChanged, source.Id, target.Id, card?.Id, 0,
                "Elemental reaction status applied.");
            var evt = state.Events[state.Events.Count - 1];
            if (contact != null) SetElementalFacts(evt, contact, card?.Id);
            evt.StatusInstanceId = instanceId;
            evt.StatusRecipeId = target.ElementStatuses.ReactionStatuses.Find(status => status.InstanceId == instanceId)?.RecipeId;
            evt.ElementStatusesAfter = target.ElementStatuses.Instances.ConvertAll(s => s.Clone());
            evt.ElementalReactionStatusesAfter = target.ElementStatuses.ReactionStatuses.ConvertAll(s => s.Clone());
            return true;
        }
    }
}
