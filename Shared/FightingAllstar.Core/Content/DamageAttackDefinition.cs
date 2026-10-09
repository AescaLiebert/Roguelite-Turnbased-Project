using System;
using System.Collections.Generic;

namespace FightingAllstar.Core.Content
{
    public enum AttackRange { Close, Long }
    public enum HitReaction { Hit, None, KnockBack, KnockDown, KnockUp }
    public enum HitReactionTiming { LastHit, FirstHit, EveryHit }

    [Serializable]
    public sealed class DamageAttackDefinition
    {
        public int HitCount = 1;
        public AttackRange Range;
        public HitReaction Reaction = HitReaction.Hit;
        public HitReactionTiming ReactionTiming = HitReactionTiming.LastHit;
        public int ResolvedHitCount => Math.Max(1, Math.Min(10, HitCount));
        public DamageAttackDefinition Clone() => (DamageAttackDefinition)MemberwiseClone();
    }

    /// <summary>Reusable choreography presets. Hit count remains editable from 1 through 10.</summary>
    public sealed class DamageAnimationTemplate
    {
        public string Name { get; }
        public AttackRange Range { get; }
        public bool Area { get; }
        public int Hits { get; }
        public DamageAnimationTemplate(string name, AttackRange range, bool area, int hits)
        { Name = name; Range = range; Area = area; Hits = hits; }
    }

    public static class DamageAnimationTemplates
    {
        public static IReadOnlyList<DamageAnimationTemplate> All { get; } = Array.AsReadOnly(new[] {
            new DamageAnimationTemplate("Vault Cleaver", AttackRange.Close, false, 1),
            new DamageAnimationTemplate("Seismic Landing", AttackRange.Close, true, 1),
            new DamageAnimationTemplate("Comet Lance", AttackRange.Long, false, 1),
            new DamageAnimationTemplate("Thunderstorm", AttackRange.Long, true, 1),
            new DamageAnimationTemplate("Cross Cut", AttackRange.Close, false, 2),
            new DamageAnimationTemplate("Twin Crescent", AttackRange.Close, true, 2),
            new DamageAnimationTemplate("Twin Fang", AttackRange.Long, false, 2),
            new DamageAnimationTemplate("Crosswind", AttackRange.Long, true, 2),
            new DamageAnimationTemplate("Rising Dragon", AttackRange.Close, false, 3),
            new DamageAnimationTemplate("Cyclone Breaker", AttackRange.Close, true, 3),
            new DamageAnimationTemplate("Trinity Volley", AttackRange.Long, false, 3),
            new DamageAnimationTemplate("Stormfront", AttackRange.Long, true, 3),
            new DamageAnimationTemplate("Phantom Rush", AttackRange.Close, false, 6),
            new DamageAnimationTemplate("Blade Tempest", AttackRange.Close, true, 5),
            new DamageAnimationTemplate("Meteor Barrage", AttackRange.Long, false, 8),
            new DamageAnimationTemplate("Starfall Rain", AttackRange.Long, true, 10)
        });

        public static int Index(int hits, AttackRange range, bool area) =>
            (Math.Max(1, Math.Min(4, hits)) - 1) * 4 + (range == AttackRange.Long ? 2 : 0) + (area ? 1 : 0);
        public static DamageAnimationTemplate Resolve(int hits, AttackRange range, bool area) => All[Index(hits, range, area)];
    }
}
