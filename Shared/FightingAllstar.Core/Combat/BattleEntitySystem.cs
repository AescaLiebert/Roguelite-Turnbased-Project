using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    [Serializable]
    public sealed class BattleEntityInstance
    {
        public string Id;
        public string OwnerFighterId;
        public string MarkerStatusInstanceId;
        public BattleEntityDefinition Definition;
        public TeamSide Side;
        public long SpawnOrder;
        public int Capacity;
        public int MaximumCapacity;
        public long EligibleFromOccurrence;
        public long LastActivationOccurrence;
        public BattleEntityInstance Clone()
        {
            var copy = (BattleEntityInstance)MemberwiseClone();
            copy.Definition = Definition?.Clone();
            return copy;
        }
    }

    /// <summary>Owns entity identity and lifetime links; damage allocation is a separate capability.</summary>
    public static partial class BattleEntitySystem
    {
        public static FighterState FindOwner(BattleState state, BattleEntityInstance entity) =>
            entity == null ? null : state?.Player.FindFighter(entity.OwnerFighterId) ?? state?.Opponent.FindFighter(entity.OwnerFighterId);

        public static bool IsAvailable(BattleState state, BattleEntityInstance entity)
        {
            var owner = FindOwner(state, entity);
            return entity != null && state != null && !state.Winner.HasValue && !state.IsDraw &&
                state.Phase != BattlePhase.Complete && owner != null && owner.IsAlive && owner.Health > 0 &&
                owner.Statuses?.Instances?.Exists(s => s.InstanceId == entity.MarkerStatusInstanceId &&
                    s.EntityInstanceId == entity.Id && s.Recipe?.StructuralEntityMarker == true) == true;
        }

        public static BattleEntityInstance CreateOrReplace(BattleState state, FighterState owner,
            BattleEntityDefinition definition, string rootActionId = null)
        {
            if (state == null || owner == null || !owner.IsAlive || owner.Health <= 0 || definition == null ||
                state.Winner.HasValue || state.IsDraw || state.Phase == BattlePhase.Complete) return null;
            if (!ReferenceEquals(state.Team(owner.Side).FindFighter(owner.Id), owner)) return null;
            var errors = definition.Validate();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors));
            if (state.Entities == null) state.Entities = new List<BattleEntityInstance>();
            var marker = new StatusRecipeDefinition { Id = definition.Id + ".marker",
                NameKey = definition.MarkerRecipe?.NameKey,
                Tags = new List<string>(definition.MarkerRecipe?.Tags ?? new List<string>()) };
            // Structural identity is scoped to the definition, never a shared authored recipe.
            marker.Id = definition.Id + ".marker";
            marker.NameKey = definition.NameKey ?? marker.NameKey ?? definition.Id;
            marker.Polarity = StatusPolarity.Buff;
            marker.Color = StatusColor.Grey;
            marker.Behavior = StatusBehavior.None;
            marker.StructuralEntityMarker = true;
            marker.BreakRule = StatusBreakRule.OnSourceDefeated;
            marker.Identity = StatusIdentityScope.RecipeAndSource;
            marker.Stacking = StatusStackingPolicy.ReplaceAlways;
            marker.DurationClock = StatusDurationClock.TargetTurnEnd;
            marker.DefaultDuration = definition.Duration;
            marker.Modifiers = new List<StatModifierDefinition>();
            marker.StanceChildren = new List<StanceChildDefinition>();
            marker.PeriodicDamage = null;
            marker.PeriodicHealing = null;
            marker.NaturalExpiryAttack = null;
            marker.Retaliation = null;
            foreach (var previous in new List<BattleEntityInstance>(state.Entities))
                if (previous.OwnerFighterId == owner.Id && previous.Definition?.Id == definition.Id)
                    Remove(state, previous.Id, BattleEntityRemovalCause.Replaced, rootActionId);
            var sequence = ++state.EntitySequence;
            var id = state.MatchId + ":entity:" + sequence;
            var markerId = id + ":marker";
            var applied = StatusSystem.Apply(owner, owner.Id, owner.Side, marker, markerId, rootActionId,
                sequence, duration: definition.Duration,
                skipNextDurationClock: state.Phase == BattlePhase.TurnEnd && owner.Side == state.ActingSide);
            if (!applied.Accepted) throw new InvalidOperationException("Structural entity marker was rejected: " + applied.Reason);
            applied.Instance.EntityInstanceId = id;
            var capacity = (int)Math.Min(int.MaxValue, Math.Max(0L,
                (long)StatusSystem.GetEffectiveStats(owner).Get(definition.CapacitySourceStat) * definition.CapacityCoefficientBp / 10000));
            var entity = new BattleEntityInstance { Id = id, OwnerFighterId = owner.Id, Side = owner.Side,
                MarkerStatusInstanceId = markerId, Definition = definition.Clone(), SpawnOrder = sequence,
                Capacity = capacity, MaximumCapacity = capacity, EligibleFromOccurrence = state.EntityTurnOccurrence + 1 };
            state.Entities.Add(entity);
            var statusEvent = Emit(state, BattleEventKind.StatusApplied, entity, rootActionId);
            statusEvent.TargetId = owner.Id;
            statusEvent.StatusInstanceId = markerId;
            statusEvent.StatusRecipeId = marker.Id;
            statusEvent.StatusOutcome = applied.Outcome;
            statusEvent.StatusesAfter = owner.Statuses.Instances.ConvertAll(s => s.Clone());
            Emit(state, BattleEventKind.EntityCreated, entity, rootActionId);
            CharacterPassiveRuntime.Refresh(state);
            return entity;
        }

        public static bool Remove(BattleState state, string id, BattleEntityRemovalCause cause, string rootActionId = null)
        {
            var entity = state?.Entities?.Find(e => e.Id == id);
            if (entity == null) return false;
            state.Entities.Remove(entity);
            var owner = FindOwner(state, entity);
            if (owner?.Statuses?.RemoveInstance(entity.MarkerStatusInstanceId) == true)
            {
                var statusEvent = Emit(state, BattleEventKind.StatusRemoved, entity, rootActionId);
                statusEvent.TargetId = owner.Id;
                statusEvent.StatusInstanceId = entity.MarkerStatusInstanceId;
                statusEvent.StatusesAfter = owner.Statuses.Instances.ConvertAll(s => s.Clone());
            }
            var evt = Emit(state, BattleEventKind.EntityRemoved, entity, rootActionId);
            evt.EntityRemovalCause = cause;
            if (cause == BattleEntityRemovalCause.CapacityDepleted && owner?.IsAlive == true && owner.Health > 0 &&
                entity.Definition.DepletedStatus != null)
                BattleEngine.ApplyEntityDepletionReward(state, entity, rootActionId);
            CharacterPassiveRuntime.Refresh(state);
            return true;
        }

        public static void RemoveOwned(BattleState state, string ownerId, string rootActionId = null)
        {
            if (state?.Entities == null) return;
            foreach (var entity in new List<BattleEntityInstance>(state.Entities))
                if (entity.OwnerFighterId == ownerId) Remove(state, entity.Id, BattleEntityRemovalCause.OwnerDefeated, rootActionId);
        }

        public static void RemoveAll(BattleState state, BattleEntityRemovalCause cause = BattleEntityRemovalCause.BattleEnded)
        {
            if (state?.Entities == null) return;
            foreach (var entity in new List<BattleEntityInstance>(state.Entities)) Remove(state, entity.Id, cause);
        }

        public static void Reconcile(BattleState state)
        {
            if (state?.Entities == null) return;
            foreach (var entity in new List<BattleEntityInstance>(state.Entities))
            {
                var owner = FindOwner(state, entity);
                if (owner == null || !owner.IsAlive) Remove(state, entity.Id, BattleEntityRemovalCause.OwnerDefeated);
                else if (owner.Statuses?.Instances?.Exists(s => s.InstanceId == entity.MarkerStatusInstanceId && s.EntityInstanceId == entity.Id) != true)
                    Remove(state, entity.Id, BattleEntityRemovalCause.Expired);
            }
        }

        /// <summary>Reject malformed persisted links without inventing objects from historical status names.</summary>
        public static void ValidateSnapshot(BattleState state)
        {
            if (state == null) return;
            if (state.Entities == null) state.Entities = new List<BattleEntityInstance>();
            if (state.EntitySequence < 0 || state.EntityTurnOccurrence < 0 ||
                state.Entities.Count > 0 && (state.Winner.HasValue || state.IsDraw || state.Phase == BattlePhase.Complete))
                throw new InvalidOperationException("Invalid persisted entity clock or completed battle entities.");
            var entityIds = new HashSet<string>(StringComparer.Ordinal);
            var markerIds = new HashSet<string>(StringComparer.Ordinal);
            var ownerDefinitions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entity in state.Entities)
            {
                if (entity == null || string.IsNullOrWhiteSpace(entity.Id) || !entityIds.Add(entity.Id) ||
                    string.IsNullOrWhiteSpace(entity.MarkerStatusInstanceId) || !markerIds.Add(entity.MarkerStatusInstanceId) ||
                    entity.Definition == null || entity.Definition.Validate().Count != 0 ||
                    !ownerDefinitions.Add(entity.OwnerFighterId + "::" + entity.Definition.Id))
                    throw new InvalidOperationException("Invalid or duplicate persisted battle entity.");
                var owner = FindOwner(state, entity);
                var marker = owner?.Statuses?.Instances?.Find(s => s.InstanceId == entity.MarkerStatusInstanceId);
                if (owner == null || !owner.IsAlive || owner.Health <= 0 || owner.Side != entity.Side || marker == null || marker.EntityInstanceId != entity.Id ||
                    marker.SourceFighterId != owner.Id || marker.TargetFighterId != owner.Id || marker.Recipe?.StructuralEntityMarker != true ||
                    marker.Recipe.Color != StatusColor.Grey || marker.Recipe.Polarity != StatusPolarity.Buff || marker.RemainingDuration <= 0 ||
                    marker.Recipe.DurationClock != StatusDurationClock.TargetTurnEnd || marker.Recipe.BreakRule != StatusBreakRule.OnSourceDefeated ||
                    marker.Recipe.Stacking != StatusStackingPolicy.ReplaceAlways || marker.Recipe.Identity != StatusIdentityScope.RecipeAndSource ||
                    marker.SourceSide != entity.Side || marker.RecipeId != entity.Definition.Id + ".marker" ||
                    marker.Recipe.Id != marker.RecipeId || marker.RemainingDuration > entity.Definition.Duration ||
                    owner.Statuses.Instances.FindAll(s => s?.InstanceId == entity.MarkerStatusInstanceId).Count != 1 ||
                    entity.Capacity < 0 || entity.MaximumCapacity < entity.Capacity || entity.SpawnOrder <= 0 ||
                    entity.SpawnOrder > state.EntitySequence || entity.Id != state.MatchId + ":entity:" + entity.SpawnOrder ||
                    entity.MarkerStatusInstanceId != entity.Id + ":marker" || entity.LastActivationOccurrence < 0 ||
                    entity.LastActivationOccurrence > state.EntityTurnOccurrence || entity.EligibleFromOccurrence < 1 ||
                    entity.EligibleFromOccurrence > state.EntityTurnOccurrence + 1)
                    throw new InvalidOperationException("Persisted entity and owner marker do not form a valid reciprocal link.");
            }
            foreach (var team in new[] { state.Player, state.Opponent })
                foreach (var fighter in team.Fighters)
                    foreach (var marker in fighter.Statuses?.Instances ?? new List<StatusInstance>())
                        if (marker?.Recipe?.StructuralEntityMarker == true &&
                            !state.Entities.Exists(e => e.OwnerFighterId == fighter.Id && e.MarkerStatusInstanceId == marker.InstanceId && e.Id == marker.EntityInstanceId))
                            throw new InvalidOperationException("Persisted structural entity marker has no owning entity.");
        }

        /// <summary>Pending zero-HP owners stop supplying entity auras without finalizing their defeat.</summary>
        public static void SuppressUnavailableAuras(BattleState state)
        {
            if (state?.Entities == null) return;
            var entityPrefix = state.MatchId + ":entity:";
            foreach (var team in new[] { state.Player, state.Opponent })
                foreach (var fighter in team.Fighters)
                {
                    if (fighter.PassiveContributions == null) continue;
                    var oldMaximum = Math.Max(1, StatusSystem.GetEffectiveStats(fighter).MaxHealth);
                    var removed = fighter.PassiveContributions.RemoveAll(contribution => {
                        if (contribution?.RuleId == null || !contribution.RuleId.StartsWith(entityPrefix, StringComparison.Ordinal)) return false;
                        var entity = state.Entities.Find(e => contribution.RuleId.StartsWith(e.Id + ":", StringComparison.Ordinal));
                        return !IsAvailable(state, entity);
                    });
                    if (removed == 0) continue;
                    var maximum = Math.Max(1, StatusSystem.GetEffectiveStats(fighter).MaxHealth);
                    if (fighter.IsAlive && fighter.Health > 0)
                        fighter.Health = oldMaximum == maximum ? Math.Min(maximum, fighter.Health)
                            : (int)Math.Min(maximum, (long)fighter.Health * maximum / oldMaximum);
                    var evt = new BattleEvent { Id = state.Events.Count + 1, Kind = BattleEventKind.PassiveStatsChanged,
                        SourceId = fighter.Id, TargetId = fighter.Id, HealthAfter = fighter.Health, EffectiveMaxHealth = maximum,
                        Message = "Unavailable entity aura suppressed." };
                    evt.PassiveContributions = fighter.PassiveContributions.ConvertAll(c => c?.Clone());
                    state.Events.Add(evt);
                }
            state.ActivePassiveAuras.RemoveAll(key => {
                var separator = key.IndexOf("::", StringComparison.Ordinal);
                if (separator < 0) return false;
                var rule = key.Substring(separator + 2);
                if (!rule.StartsWith(entityPrefix, StringComparison.Ordinal)) return false;
                return !state.Entities.Exists(e => rule.StartsWith(e.Id + ":", StringComparison.Ordinal) && IsAvailable(state, e));
            });
        }

        public static List<string> CollectTurnEndJobs(BattleState state, TeamSide side, long occurrence)
        {
            var entities = new List<BattleEntityInstance>();
            if (state?.Entities != null) foreach (var entity in state.Entities)
                if (IsAvailable(state, entity) && (entity.Side == side || entity.Definition.ActOnBothSidesTurnEnd) &&
                    entity.EligibleFromOccurrence <= occurrence && entity.LastActivationOccurrence != occurrence &&
                    entity.Definition.TurnEndAttack != null) entities.Add(entity);
            entities.Sort((a, b) => {
                var aOwner = FindOwner(state, a); var bOwner = FindOwner(state, b);
                var compare = a.Side.CompareTo(b.Side);
                if (compare == 0) compare = aOwner.FormationSlot.CompareTo(bOwner.FormationSlot);
                if (compare == 0) compare = a.SpawnOrder.CompareTo(b.SpawnOrder);
                return compare != 0 ? compare : StringComparer.Ordinal.Compare(a.Id, b.Id);
            });
            return entities.ConvertAll(e => e.Id);
        }

        public static BattleEvent Emit(BattleState state, BattleEventKind kind, BattleEntityInstance entity, string rootActionId = null)
        {
            var evt = new BattleEvent { Id = state.Events.Count + 1, Kind = kind, SourceId = entity.OwnerFighterId,
                RootActionId = rootActionId, EntityId = entity.Id, EntityOwnerFighterId = entity.OwnerFighterId,
                EntityDefinitionId = entity.Definition.Id, Entity = entity.Clone(), EntityOccurrence = state.EntityTurnOccurrence,
                EntityCapacityAfter = entity.Capacity, EntityMaximumCapacity = entity.MaximumCapacity,
                EntityRemainingDuration = FindOwner(state, entity)?.Statuses?.Instances?.Find(s => s.InstanceId == entity.MarkerStatusInstanceId)?.RemainingDuration ?? 0 };
            state.Events.Add(evt);
            return evt;
        }
    }
}
