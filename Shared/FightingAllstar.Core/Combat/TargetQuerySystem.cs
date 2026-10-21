using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    public static class TargetQuerySystem
    {
        public static List<FighterState> Resolve(TargetQueryDefinition query, CardEffectContext context,
            DeterministicRandom rng = null)
        {
            var result = new List<FighterState>();
            if (query == null || context?.Battle == null || context.Actor == null) return result;
            var owner = context.EffectOwner ?? context.Actor;
            var anchor = query.Anchor == TargetQueryAnchor.Owner ? owner :
                query.Anchor == TargetQueryAnchor.EventTarget ? CardEffectSystem.EventTarget(context) : context.Actor;
            if (anchor == null) return result;
            var bindings = context.TargetBindings;
            if (!string.IsNullOrEmpty(query.ReuseBinding) && (bindings == null || !bindings.ContainsKey(query.ReuseBinding)))
                throw new PassiveExecutionException("Missing required target binding: " + query.ReuseBinding);
            if (!string.IsNullOrEmpty(query.ExcludeBinding) && (bindings == null || !bindings.ContainsKey(query.ExcludeBinding)))
                throw new PassiveExecutionException("Missing required exclusion binding: " + query.ExcludeBinding);
            if (!string.IsNullOrEmpty(query.ReuseBinding))
            {
                if (bindings != null && bindings.TryGetValue(query.ReuseBinding, out var ids))
                {
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var id in ids)
                    {
                        var fighter = Find(context.Battle, id);
                        if (seen.Add(id) && Eligible(fighter, query, context, owner, bindings)) result.Add(fighter);
                    }
                }
            }
            else
            {
                foreach (var team in new[] { context.Battle.Player, context.Battle.Opponent })
                    foreach (var fighter in team.Fighters)
                        if (InScope(fighter, query.BaseScope, context, anchor) &&
                            Eligible(fighter, query, context, owner, bindings)) result.Add(fighter);
                result.Sort((a, b) => Compare(a, b, query));
                if (query.SampleCount > 0 && result.Count > 1)
                {
                    var random = rng ?? context.Battle.ActiveRandom;
                    var standalone = random == null;
                    if (standalone) random = DeterministicRandom.Restore(context.Battle.RngState, context.Battle.RngDrawCount);
                    // Partial Fisher-Yates; each chosen ID is removed from the remaining candidate set.
                    var count = Math.Min(Math.Min(32, query.SampleCount), result.Count);
                    for (var i = 0; i < count; i++)
                    {
                        var selected = i + random.Next(result.Count - i);
                        var swap = result[i]; result[i] = result[selected]; result[selected] = swap;
                    }
                    result.RemoveRange(count, result.Count - count);
                    if (standalone)
                    {
                        context.Battle.RngState = random.State;
                        context.Battle.RngDrawCount = random.DrawCount;
                    }
                }
            }
            var limit = Math.Max(0, Math.Min(32, query.Limit));
            if (result.Count > limit) result.RemoveRange(limit, result.Count - limit);
            if (!string.IsNullOrEmpty(query.StoreBinding) && bindings != null)
                bindings[query.StoreBinding] = result.ConvertAll(f => f.Id);
            return result;
        }

        private static bool Eligible(FighterState fighter, TargetQueryDefinition query, CardEffectContext context,
            FighterState owner, Dictionary<string, List<string>> bindings)
        {
            if (fighter == null || !fighter.IsAlive || fighter.Health <= 0) return false;
            var filter = query.Filter;
            var eventTarget = CardEffectSystem.EventTarget(context);
            if (fighter.IsReserve && filter?.IncludeReserve != true ||
                query.ExcludeActor && fighter.Id == context.Actor.Id ||
                query.ExcludeOwner && fighter.Id == owner.Id ||
                query.ExcludeEventTarget && fighter.Id == eventTarget?.Id) return false;
            if (query.AdjacentToEventTarget && (eventTarget == null || fighter.IsReserve ||
                eventTarget.IsReserve || fighter.Side != eventTarget.Side ||
                Math.Abs(fighter.FormationSlot - eventTarget.FormationSlot) != 1)) return false;
            if (!string.IsNullOrEmpty(query.ExcludeBinding) && bindings != null &&
                bindings.TryGetValue(query.ExcludeBinding, out var excluded) && excluded.Contains(fighter.Id)) return false;
            if (filter == null) return true;
            if (!filter.IncludeOwner && fighter.Id == owner.Id) return false;
            if (filter.Relation == PassiveRelation.Self && fighter.Id != owner.Id ||
                filter.Relation == PassiveRelation.Allies && fighter.Side != owner.Side ||
                filter.Relation == PassiveRelation.Enemies && fighter.Side == owner.Side) return false;
            return (string.IsNullOrEmpty(filter.AttributeId) || fighter.Definition?.AttributeId == filter.AttributeId) &&
                (string.IsNullOrEmpty(filter.SeriesId) || fighter.Definition?.SeriesId == filter.SeriesId) &&
                (string.IsNullOrEmpty(filter.TraitId) || fighter.Definition?.TraitIds?.Contains(filter.TraitId) == true);
        }

        private static bool InScope(FighterState fighter, EffectTargetScope scope, CardEffectContext context, FighterState anchor)
        {
            if (fighter == null) return false;
            switch (scope)
            {
                case EffectTargetScope.Self: return fighter.Id == anchor.Id;
                case EffectTargetScope.SelectedEnemy: return fighter.Id == context.SelectedTarget?.Id && fighter.Side != anchor.Side;
                case EffectTargetScope.SelectedAlly: return fighter.Id == context.SelectedTarget?.Id && fighter.Side == anchor.Side;
                case EffectTargetScope.AllEnemies: return fighter.Side != anchor.Side;
                case EffectTargetScope.AllAllies: return fighter.Side == anchor.Side;
                case EffectTargetScope.TriggerActor: return fighter.Id == context.Actor.Id;
                case EffectTargetScope.TriggerTarget: return fighter.Id == CardEffectSystem.EventTarget(context)?.Id;
                case EffectTargetScope.OperationTarget: return fighter.Id == context.SelectedTarget?.Id;
                case EffectTargetScope.StatusOwner: return fighter.Id == (context.EffectOwner ?? context.Actor).Id;
                default: return false;
            }
        }

        private static int Compare(FighterState a, FighterState b, TargetQueryDefinition query)
        {
            var primary = query.Sort == TargetQuerySort.LowestHealth ? a.Health.CompareTo(b.Health) :
                query.Sort == TargetQuerySort.HighestHealth ? b.Health.CompareTo(a.Health) :
                query.Sort == TargetQuerySort.LowestRecipeLayers ? Layers(a, query.RecipeId).CompareTo(Layers(b, query.RecipeId)) : 0;
            if (primary != 0) return primary;
            primary = a.Side.CompareTo(b.Side);
            if (primary != 0) return primary;
            primary = a.FormationSlot.CompareTo(b.FormationSlot);
            if (primary != 0) return primary;
            primary = a.TeamIndex.CompareTo(b.TeamIndex);
            return primary != 0 ? primary : StringComparer.Ordinal.Compare(a.Id, b.Id);
        }

        private static int Layers(FighterState fighter, string recipeId) =>
            string.IsNullOrEmpty(recipeId) ? 0 : StatusSystem.Count(fighter, recipeId: recipeId);
        private static FighterState Find(BattleState state, string id) => state.Player.FindFighter(id) ?? state.Opponent.FindFighter(id);
    }
}
