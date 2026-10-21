using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    public sealed partial class BattleState
    {
        [NonSerialized] internal List<EntityReactionJob> EntityReactionJobs = new List<EntityReactionJob>();
        [NonSerialized] internal bool ResolvingEntityReaction;
    }

    internal sealed class EntityReactionJob
    {
        public string EntityId;
        public string TargetId;
        public string RootActionId;
        public long ParentEventId;
    }

    public static partial class BattleEngine
    {
        private static void ResolveTurnEndEntities(BattleState state, List<string> jobs, DeterministicRandom rng)
        {
            foreach (var id in jobs)
            {
                var entity = state.Entities.Find(e => e.Id == id);
                if (!BattleEntitySystem.IsAvailable(state, entity) || entity.LastActivationOccurrence == state.EntityTurnOccurrence) continue;
                entity.LastActivationOccurrence = state.EntityTurnOccurrence;
                var actionId = entity.Id + ":turn:" + state.EntityTurnOccurrence;
                ResolveEntityAttack(state, entity, entity.Definition.TurnEndAttack, null, actionId, 0, rng);
                if (state.Winner.HasValue || state.IsDraw) break;
            }
        }

        private static void ResolveEntityAttack(BattleState state, BattleEntityInstance entity,
            TriggeredElementalAttackDefinition attack, FighterState selectedTarget, string actionId, long parentEventId,
            DeterministicRandom rng, List<FighterState> sharedPendingDefeats = null)
        {
            if (attack == null || !BattleEntitySystem.IsAvailable(state, entity)) return;
            var owner = BattleEntitySystem.FindOwner(state, entity);
            var payload = attack.Clone();
            if (entity.Definition.AttackCoefficientPerDotDealtBp > 0)
                payload.DamageBonusBp = (int)Math.Min(int.MaxValue, (long)payload.DamageBonusBp +
                    (long)Math.Max(0, StatusSystem.GetExplicitFamilyDamageDealtBp(owner, DamageFamily.DamageOverTime)) *
                    entity.Definition.AttackCoefficientPerDotDealtBp / 10000);
            var sourceAttack = StatusSystem.GetEffectiveStats(owner).Attack;
            var targets = selectedTarget != null ? new List<FighterState> { selectedTarget } : state.OtherTeam(owner.Side).LivingActive();
            if (selectedTarget == null && entity.Definition.ActionTarget == EffectTargetScope.SelectedEnemy && targets.Count > 1)
                targets.RemoveRange(1, targets.Count - 1);
            var started = BattleEntitySystem.Emit(state, BattleEventKind.EntityActionStarted, entity, actionId);
            started.ParentEventId = parentEventId;
            var pending = sharedPendingDefeats ?? new List<FighterState>();
            var contacts = new List<Action>();
            var freezes = new List<Action>();
            foreach (var target in targets)
            {
                if (!BattleEntitySystem.IsAvailable(state, entity)) break;
                if (!target.IsAlive || target.Health <= 0) continue;
                var start = state.Events.Count;
                var card = new CardState { Id = actionId, OwnerFighterId = owner.Id };
                var completionIndex = contacts.Count;
                ResolveTriggeredElementalAttack(state, owner, target, payload, card, sourceAttack, 0, parentEventId, rng, pending, contacts, freezes);
                var contacted = StampEntityFacts(state, entity, actionId, start, target.Id);
                if (contacts.Count > completionIndex)
                {
                    var complete = contacts[completionIndex];
                    contacts[completionIndex] = () =>
                    {
                        var childStart = state.Events.Count;
                        complete();
                        if (contacted && target.Health > 0 && BattleEntitySystem.IsAvailable(state, entity))
                            foreach (var status in entity.Definition.OnHitStatuses ?? new List<StatusRecipeDefinition>())
                                if (status != null) ApplyCardUtilityEffectSingle(state, rng, owner, target,
                                    new EffectDefinition { Kind = EffectKind.ApplyStatus, StatusRecipe = status.Clone() }, actionId, false, cardOrigin: false);
                        StampEntityFacts(state, entity, actionId, childStart);
                    };
                }
            }
            // Capture and commit the whole root hit batch before any reaction can alter a neighbour.
            foreach (var complete in contacts) complete();
            var freezeStart = state.Events.Count;
            foreach (var freeze in freezes) freeze();
            StampEntityFacts(state, entity, actionId, freezeStart);
            if (sharedPendingDefeats == null)
            {
                ApplyPendingDefeats(state, pending, actionId, owner);
                CheckOutcome(state);
            }
            var ended = BattleEntitySystem.Emit(state, BattleEventKind.EntityActionEnded, entity, actionId);
            ended.ParentEventId = parentEventId;
            if (!state.Entities.Exists(e => e.Id == entity.Id)) ended.Entity = null;
        }

        private static bool StampEntityFacts(BattleState state, BattleEntityInstance entity, string actionId, int start, string targetId = null)
        {
            var contacted = false;
            for (var index = start; index < state.Events.Count; index++)
            {
                var fact = state.Events[index];
                if (fact.RootActionId == null) fact.RootActionId = actionId;
                if (string.IsNullOrEmpty(fact.EntityId))
                {
                    fact.EntityId = entity.Id;
                    fact.EntityOwnerFighterId = entity.OwnerFighterId;
                    fact.EntityDefinitionId = entity.Definition.Id;
                }
                if (fact.Kind == BattleEventKind.DamageApplied && fact.SourceId == entity.OwnerFighterId &&
                    fact.CardId == actionId && fact.DamageOrigin == ElementalDamageOrigin.TriggeredAttack && fact.EntityId == entity.Id)
                {
                    fact.DamageOrigin = ElementalDamageOrigin.Summon;
                    if (fact.TargetId == targetId && fact.Amount > 0) contacted = true;
                }
            }
            return contacted;
        }

        internal static void ApplyEntityDepletionReward(BattleState state, BattleEntityInstance entity, string rootActionId)
        {
            var owner = BattleEntitySystem.FindOwner(state, entity);
            var recipe = entity.Definition.DepletedStatus;
            if (owner == null || recipe == null) return;
            foreach (var target in state.Team(owner.Side).Fighters)
            {
                if (!EntityTargetMatches(owner, target, entity.Definition.DepletedTargets)) continue;
                ApplyCardUtilityEffectSingle(state, state.ActiveRandom, owner, target,
                    new EffectDefinition { Kind = EffectKind.ApplyStatus, StatusRecipe = recipe.Clone() }, rootActionId, false, cardOrigin: false);
            }
        }

        internal static bool EntityTargetMatches(FighterState owner, FighterState target, PassiveTargetFilter filter)
        {
            if (target == null || owner == null || filter == null || !target.IsAlive || target.Health <= 0 ||
                target.IsReserve && !filter.IncludeReserve || !filter.IncludeOwner && owner.Id == target.Id) return false;
            if (filter.Relation == PassiveRelation.Self && owner.Id != target.Id ||
                filter.Relation == PassiveRelation.Allies && owner.Side != target.Side ||
                filter.Relation == PassiveRelation.Enemies && owner.Side == target.Side) return false;
            return (string.IsNullOrEmpty(filter.AttributeId) || target.Definition.AttributeId == filter.AttributeId) &&
                (string.IsNullOrEmpty(filter.TraitId) || target.Definition.TraitIds?.Contains(filter.TraitId) == true) &&
                (string.IsNullOrEmpty(filter.SeriesId) || target.Definition.SeriesId == filter.SeriesId);
        }

        internal static void QueueEntityReactionFollowUps(BattleState state, FighterState source, FighterState target,
            string rootActionId, long parentEventId)
        {
            if (state == null || source == null || target == null || state.ResolvingEntityReaction) return;
            if (state.EntityReactionJobs == null) state.EntityReactionJobs = new List<EntityReactionJob>();
            foreach (var entity in state.Entities)
                if (entity.Side == source.Side && entity.Definition.ReactionFollowUp != null && BattleEntitySystem.IsAvailable(state, entity) &&
                    !state.EntityReactionJobs.Exists(j => j.EntityId == entity.Id && j.ParentEventId == parentEventId))
                    state.EntityReactionJobs.Add(new EntityReactionJob { EntityId = entity.Id, TargetId = target.Id,
                        RootActionId = rootActionId, ParentEventId = parentEventId });
        }

        internal static void FlushEntityReactionFollowUps(BattleState state, DeterministicRandom rng, List<FighterState> pendingDefeats)
        {
            if (state == null || state.ResolvingEntityReaction || state.EntityReactionJobs == null || state.EntityReactionJobs.Count == 0) return;
            var jobs = new List<EntityReactionJob>(state.EntityReactionJobs);
            state.EntityReactionJobs.Clear();
            state.ResolvingEntityReaction = true;
            try
            {
                foreach (var job in jobs)
                {
                    var entity = state.Entities.Find(e => e.Id == job.EntityId);
                    var target = state.Player.FindFighter(job.TargetId) ?? state.Opponent.FindFighter(job.TargetId);
                    if (!BattleEntitySystem.IsAvailable(state, entity) || target == null || !target.IsAlive || target.Health <= 0) continue;
                    ResolveEntityAttack(state, entity, entity.Definition.ReactionFollowUp, target,
                        (job.RootActionId ?? entity.Id) + ":followup:" + job.ParentEventId + ":" + entity.Id, job.ParentEventId, rng, pendingDefeats);
                }
            }
            finally { state.ResolvingEntityReaction = false; }
        }
    }
}
