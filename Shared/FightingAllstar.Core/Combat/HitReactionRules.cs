using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    /// <summary>Authoritative visual facts, independent of the presentation's current stance aura.</summary>
    public static class HitReactionRules
    {
        public static bool IsInStance(FighterState target) => target?.Statuses?.Instances != null &&
            target.Statuses.Instances.Exists(s => s?.Recipe != null &&
                ((s.Recipe.Behavior & StatusBehavior.Stance) != 0 || s.Recipe.Tags?.Contains(CombatTags.Stance) == true));

        public static HitReaction Resolve(DamageAttackDefinition attack, bool inStance,
            int hitIndex, int hitCount, bool endured, bool stanceCancelled)
        {
            // Cancellation owns one forced reaction on the removal event; later packets
            // from that action cannot replace it with the card's selected reaction.
            if (inStance || endured || stanceCancelled) return HitReaction.None;
            var reaction = attack?.Reaction ?? HitReaction.Hit;
            if (reaction == HitReaction.None) return reaction;
            var timing = attack?.ReactionTiming ?? HitReactionTiming.LastHit;
            var selectedHit = timing == HitReactionTiming.EveryHit ||
                timing == HitReactionTiming.FirstHit && hitIndex == 1 ||
                timing == HitReactionTiming.LastHit && hitIndex == hitCount;
            return selectedHit ? reaction : HitReaction.Hit;
        }
    }
}
