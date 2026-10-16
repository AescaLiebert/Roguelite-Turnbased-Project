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
            var previousMaxHealth = id == ElementalReactionId.Freeze
                ? new Dictionary<string, int> { [contact.Target.Id] = StatusSystem.GetEffectiveStats(contact.Target).MaxHealth }
                : null;
            switch (id)
            {
                case ElementalReactionId.Overload:
                    ResolveOverload(state, source, card, contact, context, pendingDefeats);
                    break;
                case ElementalReactionId.Freeze:
                    ApplyReactionStatus(state, source, contact.Target, card, contact, StatusSystem.FreezeRecipeId);
                    break;
                case ElementalReactionId.ElectroCharged:
                    ApplyReactionStatus(state, source, contact.Target, card, contact, "status.reaction.charged");
                    break;
                case ElementalReactionId.Burning:
                    ApplyReactionStatus(state, source, contact.Target, card, contact, "status.reaction.burning");
                    break;
                case ElementalReactionId.Crystallize:
                    ApplyReactionStatus(state, source, source, card, contact, "status.reaction.crystallize",
                        (int)Math.Min(int.MaxValue, (long)contact.HealthDamage + contact.ShieldDamage));
                    break;
                case ElementalReactionId.Superconduct:
                    foreach (var recipient in state.Team(contact.Target.Side).LivingActive())
                    {
                        if (recipient.Health <= 0 || !context.ReactionDamageRecipients.Add(recipient.Id + ":" + id)) continue;
                        var policy = StatusSystem.BuildDamagePolicy(source, recipient, DamageFamily.ElementalReaction);
                        policy.CannotCrit = policy.CannotBlock = true;
                        policy.DamageCap = state.ElementalRules.ReactionDamageCap;
                        ElementalReactionRules.ApplyElementPolicy(policy, ElementId.Cryo, source, recipient, state.ElementalRules);
                        var damage = DamageResolver.Resolve(new DamagePacket { BaseAmount = contact.SourceAttack,
                            CoefficientBp = contact.Decision.Reaction.AttackCoefficientBp, Policy = policy },
                            StatusSystem.GetEffectiveStats(source), StatusSystem.GetEffectiveStats(recipient),
                            recipient.Health, recipient.Shield, -1, -1);
                        if (!recipient.CannotDie) recipient.Health = damage.RemainingHealth;
                        recipient.Shield = damage.RemainingShield;
                        ConsumeBarrierShield(recipient, damage.ShieldLost);
                        StatusSystem.BreakFreezeOnAttack(recipient);
                        AddEvent(state, BattleEventKind.DamageApplied, source.Id, recipient.Id, card.Id, damage.CalculatedDamage, "Superconduct");
                        var evt = state.Events[state.Events.Count - 1];
                        SetElementalFacts(evt, contact, card.Id);
                        evt.DamageOrigin = ElementalDamageOrigin.Reaction;
                        evt.DamageElement = ElementId.Cryo;
                        evt.HealthAfter = recipient.Health;
                        evt.ShieldAfter = recipient.Shield;
                        evt.HealthLost = recipient.CannotDie ? 0 : damage.HealthLost;
                        evt.ShieldLost = damage.ShieldLost;
                        evt.StatusesAfter = recipient.Statuses.Instances.ConvertAll(s => s.Clone());
                        ApplyReactionStatus(state, source, recipient, card, contact, "status.reaction.superconduct");
                        if (recipient.Health <= 0 && !recipient.CannotDie && !pendingDefeats.Contains(recipient)) pendingDefeats.Add(recipient);
                    }
                    break;
            }
            CharacterPassiveRuntime.Refresh(state, previousMaxHealthOverrides: previousMaxHealth);
        }

        private static bool ApplyReactionStatus(BattleState state, FighterState source, FighterState target,
            CardState card, ElementalContactCommit contact, string recipeId, int? barrierDamageBasis = null)
        {
            if (target.Health <= 0) return false;
            var recipe = GenshinStatusRecipes.CreateOutputs().Find(r => r.Id == recipeId);
            var order = state.Events.Count + 1;
            var shieldBefore = target.Shield;
            var result = StatusSystem.Apply(target, source.Id, source.Side, recipe, "reaction:" + order,
                card.Id, order, new StatusSnapshot { SourceAttack = barrierDamageBasis ?? contact.SourceAttack,
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
    }
}
