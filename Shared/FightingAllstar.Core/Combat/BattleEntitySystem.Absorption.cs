using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    public static partial class BattleEntitySystem
    {
        /// <summary>Consumes shared pools once, after formula resolution and before personal barriers.</summary>
        public static int Absorb(BattleState state, FighterState source, FighterState target,
            int incomingDamage, string rootActionId = null)
        {
            if (state?.Entities == null || target == null || incomingDamage <= 0 || !target.IsAlive ||
                target.Health <= 0 || target.IsReserve || source?.Side == target.Side) return 0;
            var pools = new List<BattleEntityInstance>();
            foreach (var entity in state.Entities)
                if (entity.Side == target.Side && entity.Capacity > 0 &&
                    entity.Definition?.CapacityCoefficientBp > 0 && IsAvailable(state, entity)) pools.Add(entity);
            pools.Sort((a, b) =>
            {
                var priority = a.Definition.ProtectionPriority.CompareTo(b.Definition.ProtectionPriority);
                if (priority != 0) return priority;
                var order = a.SpawnOrder.CompareTo(b.SpawnOrder);
                return order != 0 ? order : StringComparer.Ordinal.Compare(a.Id, b.Id);
            });
            var remainder = incomingDamage;
            foreach (var entity in pools)
            {
                if (remainder <= 0) break;
                if (!IsAvailable(state, entity) || entity.Capacity <= 0) continue;
                var absorbed = Math.Min(entity.Capacity, remainder);
                entity.Capacity -= absorbed;
                remainder -= absorbed;
                var fact = Emit(state, BattleEventKind.EntityAbsorbedDamage, entity, rootActionId);
                fact.SourceId = source?.Id;
                fact.TargetId = target.Id;
                fact.CardId = rootActionId;
                fact.Amount = fact.EntityAbsorbedAmount = absorbed;
                fact.Message = entity.Definition.NameKey + " absorbed damage.";
                if (entity.Capacity == 0) Remove(state, entity.Id, BattleEntityRemovalCause.CapacityDepleted, rootActionId);
            }
            return incomingDamage - remainder;
        }
    }
}
