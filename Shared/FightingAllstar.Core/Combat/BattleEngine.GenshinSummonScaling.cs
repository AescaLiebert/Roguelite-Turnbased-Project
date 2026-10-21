using System;
using System.Numerics;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    public static partial class BattleEngine
    {
        /// <summary>Generic owner-layer damage scaling; no character identity is required by the engine.</summary>
        private static int SourceStackCoefficient(FighterState actor, EffectDefinition effect, ActionResolutionContext resolution)
        {
            if (string.IsNullOrEmpty(effect.SourceStackRecipeId) || effect.SourceStackDamageBp <= 0)
                return effect.CoefficientBp;
            var cacheKey = "source-stack-coefficient:" + effect.Id;
            if (resolution?.Elemental.ConsumedStackCounts.TryGetValue(cacheKey, out var cached) == true) return cached;
            // First hit occurs after BeforeAction grants. Further targets/hits use the same receipt,
            // so Geo hits that grant new layers cannot increase this action's coefficient midway.
            var stacks = StatusSystem.Count(actor, recipeId: effect.SourceStackRecipeId);
            var bonus = new BigInteger(Math.Max(0, stacks)) * effect.SourceStackDamageBp;
            if (effect.SourceStackDotScaleBp > 0)
                bonus = bonus * Math.Max(0, StatusSystem.GetExplicitFamilyDamageDealtBp(actor, DamageFamily.DamageOverTime)) *
                    effect.SourceStackDotScaleBp / 100_000_000;
            var coefficient = (int)BigInteger.Max(0, BigInteger.Min(int.MaxValue, effect.CoefficientBp + bonus));
            if (resolution != null) resolution.Elemental.ConsumedStackCounts[cacheKey] = coefficient;
            return coefficient;
        }

        private static void ConsumeSourceStackLayers(BattleState state, FighterState actor, EffectDefinition effect, string cardId)
        {
            if (!effect.ConsumeSourceStacks || string.IsNullOrEmpty(effect.SourceStackRecipeId)) return;
            // Source wording removes ALL layers after Skill 2, including its own new Geo-hit layer.
            var removed = actor.Statuses.ConsumeLayers(effect.SourceStackRecipeId, int.MaxValue,
                requireFullAmount: false, reason: StatusRemovalReason.Consumed);
            if (removed.Count == 0) return;
            var count = 0;
            foreach (var snapshot in removed) count += snapshot.RemovedLayers;
            AddEvent(state, BattleEventKind.StatusRemoved, actor.Id, actor.Id, cardId, count,
                "Source status layers consumed: " + effect.SourceStackRecipeId);
            state.Events[state.Events.Count - 1].StatusesAfter = actor.Statuses.Instances.ConvertAll(status => status.Clone());
            CharacterPassiveRuntime.NotifyStatusRemoved(state, actor, actor, removed, cardId, true);
            CharacterPassiveRuntime.Refresh(state);
        }
    }
}
