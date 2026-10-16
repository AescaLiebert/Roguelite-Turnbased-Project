using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    public static partial class BattleEngine
    {
        private static CombatSupportDefinition ActiveSupport(FighterState fighter)
        {
            var support = fighter?.Definition?.Passive?.Support;
            return support != null && fighter.IsAlive && fighter.Health > 0 &&
                (support.Presence == PassivePresence.LivingRoster ||
                 support.Presence == PassivePresence.LivingActive && !fighter.IsReserve ||
                 support.Presence == PassivePresence.LivingReserve && fighter.IsReserve) ? support : null;
        }

        private static int TeamExplosionFactor(BattleState state, FighterState actor)
        {
            var factor = 10000;
            foreach (var provider in state.Team(actor.Side).Fighters)
                factor = Math.Max(factor, ActiveSupport(provider)?.ExplosionMultiplierBp ?? 10000);
            return factor;
        }

        private static int TeamSwirlFactor(BattleState state, FighterState actor)
        {
            long bonus = 10000;
            foreach (var provider in state.Team(actor.Side).Fighters)
            {
                var support = provider.Definition?.Passive?.Support;
                if (support != null && CharacterPassiveRuntime.IsAvailable(state, provider,
                    new PassiveGate { Presence = support.SwirlPresence })) bonus += support.SwirlStrengthBp;
            }
            return (int)Math.Min(int.MaxValue, bonus);
        }

        private static void ApplySupportOpening(BattleState state)
        {
            foreach (var team in new[] { state.Player, state.Opponent })
                foreach (var provider in team.Fighters)
                    if (ActiveSupport(provider)?.OpeningTeamGauge > 0)
                        foreach (var ally in team.LivingActive())
                            CharacterPassiveRuntime.ChangePowerGauge(state, provider, ally, ActiveSupport(provider).OpeningTeamGauge, "support-opening");
        }

        private static void PrepareSupportAction(BattleState state, FighterState actor, CardState card,
            EffectDefinition effect, List<FighterState> targets, ActionResolutionContext resolution, DeterministicRandom rng)
        {
            if (effect.Kind != EffectKind.Damage || !IsAttack(CardRules.GetEffectCategory(card))) return;
            var support = ActiveSupport(actor);
            foreach (var target in targets)
            {
                if (!string.IsNullOrEmpty(support?.BonusAgainstRecipeId) && StatusSystem.Count(target, recipeId: support.BonusAgainstRecipeId) > 0)
                    resolution.Elemental.BonusTargets.Add(target.Id);
                if (!string.IsNullOrEmpty(effect.ConsumeTargetRecipeId))
                    resolution.Elemental.ConsumedStackCounts[target.Id] = StatusSystem.Count(target, recipeId: effect.ConsumeTargetRecipeId);
            }
            if (support?.AttackCadence > 0)
            {
                var counter = actor.PassiveCounters.Find(c => c.Key == "support.attack-cadence");
                if (counter == null) { counter = new PassiveCounterState { Key = "support.attack-cadence" }; actor.PassiveCounters.Add(counter); }
                counter.Value++;
                if (counter.Value >= support.AttackCadence)
                {
                    counter.Value = 0;
                    resolution.Elemental.CadenceDamageBp = support.CadenceDamageBp;
                    resolution.Elemental.CadenceDotBp = support.CadenceDotBp;
                }
            }
            foreach (var provider in state.Team(actor.Side).Fighters)
            {
                var providerSupport = ActiveSupport(provider);
                if (string.IsNullOrEmpty(providerSupport?.AttackerGaugeAgainstRecipeId) || providerSupport.AttackerGaugeGainBp <= 0 || !targets.Exists(t =>
                    StatusSystem.Count(t, recipeId: providerSupport.AttackerGaugeAgainstRecipeId) > 0)) continue;
                ApplyCardUtilityEffectSingle(state, rng, provider, actor, new EffectDefinition {
                    Kind = EffectKind.ApplyStatus, StatusRecipe = new StatusRecipeDefinition {
                        Id = "status.buff.gauge-acceleration", NameKey = "Gauge Acceleration", Polarity = StatusPolarity.Buff,
                        DefaultDuration = 1, PowerGaugeGainBp = providerSupport.AttackerGaugeGainBp,
                        Stacking = StatusStackingPolicy.RefreshDuration } }, card.Id, false);
            }
        }

        private static void ApplySupportHit(BattleState state, FighterState actor, FighterState target,
            EffectDefinition effect, ActionResolutionContext resolution, AttackEffectCalculation calculation)
        {
            var support = ActiveSupport(actor);
            if (resolution?.Elemental.BonusTargets.Contains(target.Id) == true && support != null)
            {
                calculation.Attacker.CritChanceBp += support.BonusCritChanceBp;
                calculation.Policy.OutgoingIncreaseBp += support.BonusDamageBp;
            }
            calculation.Policy.OutgoingIncreaseBp += resolution?.Elemental.CadenceDamageBp ?? 0;
            if (resolution?.Elemental.ConsumedStackCounts.TryGetValue(target.Id, out var stacks) == true)
                calculation.Policy.OutgoingIncreaseBp = (int)Math.Min(int.MaxValue,
                    (long)calculation.Policy.OutgoingIncreaseBp + (long)stacks * effect.DamagePerConsumedStackBp);
            if (effect.Explosion) calculation.Policy.ExplosionFactorBp = TeamExplosionFactor(state, actor);
        }

        private static void FinishSupportAction(BattleState state, FighterState actor, CardState card,
            EffectDefinition effect, List<FighterState> targets, DeterministicRandom rng, ActionResolutionContext resolution)
        {
            var support = ActiveSupport(actor);
            var contactedTargets = new HashSet<string>(StringComparer.Ordinal);
            long actualDamage = 0;
            var killed = false;
            for (var index = resolution.EventStart; index < state.Events.Count; index++)
            {
                var evt = state.Events[index];
                if (evt.Kind != BattleEventKind.DamageApplied || evt.SourceId != actor.Id || evt.CardId != card.Id) continue;
                if (evt.DamageOrigin == ElementalDamageOrigin.Card) contactedTargets.Add(evt.TargetId);
                actualDamage += evt.HealthLost;
                killed |= evt.HealthLost > 0 && evt.HealthAfter == 0;
            }
            foreach (var target in targets)
            {
                if (!contactedTargets.Contains(target.Id)) continue;
                if (!string.IsNullOrEmpty(effect.ConsumeTargetRecipeId))
                    EmitStatusRemoval(state, actor, target, StatusPolarity.Debuff, true, card.Id, effect.ConsumeTargetRecipeId);
                if (effect.Kind != EffectKind.Damage || string.IsNullOrEmpty(support?.AfterAttackStatusRecipeId) ||
                    support.RequiresElementalTeam && !state.Team(actor.Side).ElementalReactionsEnabled || target.Health <= 0) continue;
                var recipe = StandardEffectDatabase.CreateStatusRecipes().Find(r => r.Id == support.AfterAttackStatusRecipeId);
                ApplyCardUtilityEffectSingle(state, rng, actor, target, new EffectDefinition {
                    Kind = EffectKind.ApplyStatus, StatusRecipe = recipe }, card.Id, false);
            }
            HealLowestAllyFromDamage(state, actor, actualDamage, card.Id);
            if (effect.OnKillSelfHealMaxHealthBp > 0 && killed && actor.Health > 0)
            {
                var stats = StatusSystem.GetEffectiveStats(actor);
                TryApplyRecovery(state, actor, actor, (int)Math.Min(int.MaxValue,
                    (long)stats.MaxHealth * effect.OnKillSelfHealMaxHealthBp / 10000), stats.MaxHealth, card.Id, "Defeat recovery.");
            }
        }

        private static void HealLowestAllyFromDamage(BattleState state, FighterState actor, long damage, string rootActionId)
        {
            var support = ActiveSupport(actor);
            if (support == null || support.DamageHealLowestAllyBp <= 0 || damage <= 0) return;
            FighterState lowest = null;
            foreach (var ally in state.Team(actor.Side).LivingActive())
            {
                if (ally.Id == actor.Id || ally.Health <= 0) continue;
                if (lowest == null || (long)ally.Health * StatusSystem.GetEffectiveStats(lowest).MaxHealth <
                    (long)lowest.Health * StatusSystem.GetEffectiveStats(ally).MaxHealth) lowest = ally;
            }
            if (lowest == null) return;
            TryApplyRecovery(state, actor, lowest, (int)Math.Min(int.MaxValue,
                damage * support.DamageHealLowestAllyBp / 10000), StatusSystem.GetEffectiveStats(lowest).MaxHealth,
                rootActionId, "Damage recovery.");
        }
    }
}
