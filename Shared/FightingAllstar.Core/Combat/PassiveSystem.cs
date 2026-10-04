using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    /// <summary>A passive effect queued from an explicit trigger; callers resolve it in returned order.</summary>
    public sealed class TriggeredPassiveEffect
    {
        public FighterState Owner;
        public FighterState TriggerTarget;
        public EffectDefinition Effect;
    }

    public sealed class TriggeredPassiveOperation
    {
        public FighterState Owner;
        public ResolvedCardEffect Resolved;
    }

    /// <summary>Deterministic trigger matching shared by cards, statuses, turn windows, and passives.</summary>
    public static class PassiveSystem
    {
        public static List<TriggeredPassiveOperation> CollectOperations(BattleState state, EffectTrigger trigger,
            CardEffectWindow window, CardEffectContext triggerContext)
        {
            var result = new List<TriggeredPassiveOperation>();
            if (state == null || triggerContext == null) return result;
            CollectOperationTeam(state.Player, trigger, window, triggerContext, result);
            CollectOperationTeam(state.Opponent, trigger, window, triggerContext, result);
            return result;
        }

        public static List<TriggeredPassiveEffect> Collect(BattleState state, EffectTrigger trigger,
            string triggerTargetId = null, string requiredTag = null)
        {
            var result = new List<TriggeredPassiveEffect>();
            if (state == null) return result;
            var triggerTarget = FindFighter(state, triggerTargetId);
            CollectTeam(state.Player, trigger, triggerTarget, requiredTag, result);
            CollectTeam(state.Opponent, trigger, triggerTarget, requiredTag, result);
            return result;
        }

        private static void CollectTeam(BattleTeamState team, EffectTrigger trigger, FighterState triggerTarget,
            string requiredTag, List<TriggeredPassiveEffect> result)
        {
            if (team?.Fighters == null) return;
            foreach (var fighter in team.Fighters)
            {
                var passive = fighter?.Definition?.Passive;
                if (fighter == null || !fighter.IsAlive || passive?.Triggers == null) continue;
                foreach (var rule in passive.Triggers)
                {
                    if (rule == null || rule.Trigger != trigger ||
                        (!string.IsNullOrEmpty(requiredTag) && !string.IsNullOrEmpty(rule.RequiredTag) && rule.RequiredTag != requiredTag)) continue;
                    var statusSubject = triggerTarget ?? fighter;
                    var statusCount = StatusSystem.Count(statusSubject, requiredTag: rule.RequiredTag);
                    if (statusCount < rule.RequiredStackCount || rule.Effects == null) continue;
                    foreach (var effect in rule.Effects)
                        if (effect != null) result.Add(new TriggeredPassiveEffect { Owner = fighter,
                            TriggerTarget = triggerTarget, Effect = effect.Clone() });
                }
            }
        }

        private static void CollectOperationTeam(BattleTeamState team, EffectTrigger trigger, CardEffectWindow window,
            CardEffectContext triggerContext, List<TriggeredPassiveOperation> result)
        {
            if (team?.Fighters == null) return;
            foreach (var fighter in team.Fighters)
            {
                var passive = fighter?.Definition?.Passive;
                if (fighter == null || !fighter.IsAlive || passive?.Triggers == null) continue;
                foreach (var rule in passive.Triggers)
                {
                    if (rule == null || rule.Trigger != trigger || rule.Operations == null) continue;
                    var context = CopyContext(triggerContext, fighter);
                    var subject = context.SelectedTarget ?? fighter;
                    if (StatusSystem.Count(subject, requiredTag: rule.RequiredTag) < rule.RequiredStackCount) continue;
                    if (!CardEffectSystem.ConditionsPass(rule.Conditions, context, subject)) continue;
                    var recipe = new CardEffectRecipeDefinition { Id = passive.Id + ":" + trigger, Operations = rule.Operations };
                    foreach (var resolved in CardEffectSystem.ResolveWindow(recipe, window, context))
                        result.Add(new TriggeredPassiveOperation { Owner = fighter, Resolved = resolved });
                }
            }
        }

        private static CardEffectContext CopyContext(CardEffectContext source, FighterState effectOwner) =>
            new CardEffectContext { Battle = source.Battle, EffectOwner = effectOwner, Actor = source.Actor,
                SelectedTarget = source.SelectedTarget, RootActionId = source.RootActionId,
                CardCategory = source.CardCategory, CardRank = source.CardRank, IsUltimate = source.IsUltimate,
                WasCritical = source.WasCritical, WasBlocked = source.WasBlocked,
                ActualHealthDamage = source.ActualHealthDamage, ActualShieldDamage = source.ActualShieldDamage,
                DamageFamily = source.DamageFamily };

        private static FighterState FindFighter(BattleState state, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return state.Player.FindFighter(id) ?? state.Opponent.FindFighter(id);
        }
    }
}
