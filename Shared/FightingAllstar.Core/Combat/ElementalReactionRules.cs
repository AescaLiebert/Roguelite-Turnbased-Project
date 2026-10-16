using System;
using System.Collections.Generic;
using System.Numerics;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    public sealed class ElementalReactionDecision
    {
        public ElementalReactionDefinition Reaction;
        public ElementId IncomingElement;
        public List<string> ConsumedInstanceIds = new List<string>();
        public List<string> SourceInstanceIds = new List<string>();
    }

    /// <summary>Pure selection shared with future public forecasts; never mutates statuses or consumes RNG.</summary>
    public static class ElementalReactionRules
    {
        public static bool Qualifies(BattleTeamState team)
        {
            if (team?.Fighters == null || team.Fighters.Count == 0) return false;
            foreach (var fighter in team.Fighters)
                if (fighter?.Definition?.TraitIds?.Contains(ElementalRuleSet.GenshinTrait) != true) return false;
            return true;
        }

        public static ElementalReactionDecision Evaluate(ElementalRuleSet rules, FighterState target, ElementId incoming)
        {
            if (rules == null || target?.Statuses?.Instances == null || incoming == ElementId.None) return null;
            ElementalReactionDefinition selected = null;
            foreach (var reaction in rules.Reactions)
            {
                if (reaction.IncomingElement != incoming || !target.Statuses.Instances.Exists(s =>
                    Eligible(s) && rules.ElementFor(s.RecipeId) == reaction.ExistingElement)) continue;
                if (selected == null || reaction.Priority < selected.Priority ||
                    reaction.Priority == selected.Priority && reaction.Id < selected.Id) selected = reaction;
            }
            if (selected == null) return null;
            var result = new ElementalReactionDecision { Reaction = selected.Clone(), IncomingElement = incoming };
            foreach (var status in target.Statuses.Instances)
            {
                if (!Eligible(status)) continue;
                var element = rules.ElementFor(status.RecipeId);
                if (element == selected.ExistingElement) result.SourceInstanceIds.Add(status.InstanceId);
                if (!selected.PreserveFuel && (element == selected.ExistingElement || element == incoming))
                    result.ConsumedInstanceIds.Add(status.InstanceId);
            }
            return result;
        }

        private static bool Eligible(StatusInstance status) => status?.Recipe != null &&
            status.Recipe.Polarity == StatusPolarity.Debuff && status.Recipe.Color != StatusColor.Grey &&
            string.IsNullOrEmpty(status.ParentInstanceId);

        public static void ApplyElementPolicy(DamagePolicy policy, ElementId element, FighterState source,
            FighterState target, ElementalRuleSet rules)
        {
            policy.Element = element;
            if (element == ElementId.None) return;
            var outgoing = Modifier(source, element, false);
            var resistance = Math.Max(-10000L, Math.Min(10000L, Modifier(target, element, true)));
            policy.ElementFactorBp = (int)Math.Max(0L, Math.Min(int.MaxValue, 10000L + outgoing));
            policy.ElementResistanceFactorBp = (int)(10000L - resistance);
            if (policy.Family == DamageFamily.Normal && rules?.BridgeDotDealt == true)
            {
                var dot = StatusSystem.GetExplicitFamilyDamageDealtBp(source, DamageFamily.DamageOverTime);
                policy.DotBridgeFactorBp = (int)Math.Max(0L, Math.Min(int.MaxValue,
                    10000L + dot));
            }
        }

        private static long Modifier(FighterState fighter, ElementId element, bool resistance)
        {
            var total = Sum(fighter?.Stats?.ElementalModifiers, element, resistance);
            if (fighter?.Statuses?.Instances != null) foreach (var status in fighter.Statuses.Instances)
            {
                if (status?.Recipe == null) continue;
                var stacks = status.Recipe.Stacking == StatusStackingPolicy.AddStacks ? Math.Max(1, status.StackCount) : 1;
                total += Sum(status.Recipe.ElementalModifiers, element, resistance) * stacks;
            }
            return (long)BigInteger.Max(-int.MaxValue, BigInteger.Min(int.MaxValue, total));
        }

        private static BigInteger Sum(List<ElementalModifierDefinition> modifiers, ElementId element, bool resistance)
        {
            var total = BigInteger.Zero;
            if (modifiers != null) foreach (var item in modifiers)
                if (item?.Element == element) total += resistance ? item.ResistanceBp : item.DamageDealtBp;
            return total;
        }
    }
}
