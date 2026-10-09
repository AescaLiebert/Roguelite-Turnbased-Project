using System;
using System.Collections.Generic;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Presentation.Combat
{
    public enum ActorVisualEffectKind
    {
        Automatic, None, AttackIncrease, AttackDecrease, DefenseIncrease, DefenseDecrease,
        HealthIncrease, HealthDecrease, Stun, Paralyze, Bleed, Poison, Shock, Ignite
    }

    /// <summary>Presentation-only mapping. Uses recipe semantics, including bundles and potency.</summary>
    public static class ActorVisualEffectRules
    {
        public static bool IsPersistentDebuff(ActorVisualEffectKind kind) =>
            kind >= ActorVisualEffectKind.Paralyze && kind <= ActorVisualEffectKind.Ignite;

        public static ActorVisualEffectKind PersistentDebuff(StatusRecipeDefinition recipe)
        {
            if (recipe == null) return ActorVisualEffectKind.None;
            var authored = StatusVisualData.Get(recipe.Id, recipe.Polarity);
            if (authored != null && authored.ActorVisualEffect != ActorVisualEffectKind.Automatic)
                return IsPersistentDebuff(authored.ActorVisualEffect) ? authored.ActorVisualEffect : ActorVisualEffectKind.None;
            if (Matches(recipe, CombatTags.Paralyze, "paralyze")) return ActorVisualEffectKind.Paralyze;
            if (Matches(recipe, CombatTags.Bleed, "bleed")) return ActorVisualEffectKind.Bleed;
            // Preserve the historical 'posion' alias used by the authored Poison visual.
            if (Matches(recipe, CombatTags.Poison, "poison") || Matches(recipe, "status.posion", "posion")) return ActorVisualEffectKind.Poison;
            if (Matches(recipe, CombatTags.Shock, "shock")) return ActorVisualEffectKind.Shock;
            if (Matches(recipe, CombatTags.Ignite, "ignite")) return ActorVisualEffectKind.Ignite;
            return ActorVisualEffectKind.None;
        }

        private static bool Matches(StatusRecipeDefinition recipe, string tag, string token) =>
            recipe.Tags?.Contains(tag) == true || string.Equals(recipe.Id, token, StringComparison.OrdinalIgnoreCase) ||
            recipe.Id?.EndsWith("." + token, StringComparison.OrdinalIgnoreCase) == true;

        public static List<ActorVisualEffectKind> PersistentDebuffs(FighterState fighter)
        {
            var result = new List<ActorVisualEffectKind>();
            if (fighter?.Statuses?.Instances == null || !fighter.IsAlive || fighter.IsReserve) return result;
            foreach (var status in fighter.Statuses.Instances)
            {
                var kind = PersistentDebuff(status?.Recipe);
                if (IsPersistentDebuff(kind) && !result.Contains(kind)) result.Add(kind);
            }
            return result;
        }

        public static bool IsUnableToAct(FighterState fighter) => IsStunned(fighter) ||
            PersistentDebuffs(fighter).Contains(ActorVisualEffectKind.Paralyze);

        public static bool IsStun(StatusRecipeDefinition recipe)
        {
            if (recipe == null) return false;
            var authored = StatusVisualData.Get(recipe.Id, recipe.Polarity);
            if (authored != null && authored.ActorVisualEffect != ActorVisualEffectKind.Automatic)
                return authored.ActorVisualEffect == ActorVisualEffectKind.Stun;
            return recipe.Tags?.Contains(CombatTags.Stun) == true ||
                string.Equals(recipe.Id, "stun", StringComparison.OrdinalIgnoreCase) ||
                recipe.Id?.EndsWith(".stun", StringComparison.OrdinalIgnoreCase) == true;
        }

        public static bool IsStunned(FighterState fighter)
        {
            if (fighter?.Statuses?.Instances == null || !fighter.IsAlive || fighter.IsReserve) return false;
            foreach (var status in fighter.Statuses.Instances)
                if (IsStun(status?.Recipe)) return true;
            return false;
        }

        public static List<ActorVisualEffectKind> GrantedEffects(StatusInstance granted,
            IReadOnlyList<StatusInstance> statusesAfter = null)
        {
            var result = new List<ActorVisualEffectKind>();
            if (granted?.Recipe == null) return result;
            var authored = StatusVisualData.Get(granted.RecipeId, granted.Recipe.Polarity);
            if (authored != null && authored.ActorVisualEffect != ActorVisualEffectKind.Automatic)
            {
                var kind = authored.ActorVisualEffect;
                if (kind >= ActorVisualEffectKind.AttackIncrease && kind <= ActorVisualEffectKind.HealthDecrease) result.Add(kind);
                return result;
            }
            // Isolate this grant: unrelated active buffs must not mask its direction.
            // Core resolves multipliers around 10000, bundles, signed potency and stacking.
            var probe = new FighterState { Definition = new CharacterDefinition {
                BaseStats = new StatBlock { Attack = 10000, Defense = 10000, MaxHealth = 10000 } },
                Statuses = new StatusContainer() };
            probe.Statuses.Instances.Add(granted);
            if (statusesAfter != null)
                foreach (var child in statusesAfter)
                    if (child != null && !string.IsNullOrEmpty(child.ParentInstanceId) &&
                        child.ParentInstanceId == granted.InstanceId) probe.Statuses.Instances.Add(child);
            var effective = StatusSystem.GetEffectiveStats(probe);
            AddDirection(result, effective.Attack, ActorVisualEffectKind.AttackIncrease, ActorVisualEffectKind.AttackDecrease);
            AddDirection(result, effective.Defense, ActorVisualEffectKind.DefenseIncrease, ActorVisualEffectKind.DefenseDecrease);
            AddDirection(result, effective.MaxHealth, ActorVisualEffectKind.HealthIncrease, ActorVisualEffectKind.HealthDecrease);
            return result;
        }

        private static void AddDirection(List<ActorVisualEffectKind> result, int value,
            ActorVisualEffectKind up, ActorVisualEffectKind down)
        {
            if (value != 10000) result.Add(value > 10000 ? up : down);
        }
    }
}
