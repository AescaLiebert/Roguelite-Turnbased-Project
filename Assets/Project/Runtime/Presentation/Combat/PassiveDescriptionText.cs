using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Describes simple passive auras from their rules rather than maintaining a second set of numbers in prose.</summary>
    public static class PassiveDescriptionText
    {
        private const string GeneratedRosterPlaceholder =
            "Genshin starter kit; generated starting stats. See the GDD roster specification.";

        public static string Format(string authored, PassiveDefinition passive, AuthoredDescription wording = null, int tier = 0)
        {
            if (wording != null) return AuthoredCombatText.Render(wording, null, tier, passive);
            var authoredText = CardDescriptionText.NormalizeProse(authored);
            if (!string.IsNullOrWhiteSpace(authoredText) &&
                !string.Equals(authoredText, GeneratedRosterPlaceholder, StringComparison.OrdinalIgnoreCase))
                return Finish(authoredText);
            var fallbackAuthored = string.Equals(authoredText, GeneratedRosterPlaceholder, StringComparison.OrdinalIgnoreCase)
                ? string.Empty : authoredText;
            // Reaction descriptions contain trigger conditions and extra character
            // rules. Retain them until those rules have complete text templates.
            if (passive?.Auras == null || passive.Auras.Count == 0 || passive.Reactions?.Count > 0 || passive.Triggers?.Count > 0)
                return Finish(fallbackAuthored);
            var sentences = new List<string>();
            foreach (var aura in passive.Auras)
            {
                if (aura?.Modifiers == null || aura.Modifiers.Count == 0 || aura.Targets == null ||
                    aura.Scaling == PassiveScaling.OwnerCounter || aura.Gate == null ||
                    aura.Gate.MinimumTier != 0 || aura.Gate.MaximumTier != 5)
                    return Finish(fallbackAuthored);
                var target = Target(aura.Targets);
                foreach (var group in aura.Modifiers.Where(m => m != null).GroupBy(m => new { m.Amount, m.ResolvedTarget, m.ResolvedOperation, m.ResolvedFamily, m.ResolvedBundle }))
                {
                    var modifier = group.First();
                    // Stat bundles choose their operation per stat at runtime. Their
                    // serialized operation is ignored, so format the shared basis
                    // points as a percentage instead of exposing the raw 1000 value.
                    var delta = modifier.ResolvedTarget == ModifierTarget.Stat && modifier.ResolvedOperation == ModifierOperation.Multiplier
                        ? (long)modifier.Amount - 10000 : modifier.Amount;
                    var amount = Math.Abs(delta);
                    var verb = delta >= 0 ? "Increases " : "Decreases ";
                    var names = group.Select(m => m.ResolvedTarget == ModifierTarget.Stat ? CombatTextCatalog.StatName(m.ResolvedStat) :
                        m.ResolvedTarget == ModifierTarget.StatBundle ? Bundle(m.ResolvedBundle) : "").ToArray();
                    var bundle = FindBundle(group.ToArray());
                    var label = bundle ?? Join(names);
                    string sentence;
                    if (modifier.ResolvedTarget == ModifierTarget.Stat && modifier.ResolvedStat == StatId.ReflectDamage &&
                        aura.Targets.Relation == PassiveRelation.Self && aura.Scaling == PassiveScaling.Constant)
                        sentence = "Reflects " + Percent(amount) + " of damage taken";
                    else
                    {
                        var value = aura.Scaling == PassiveScaling.OwnerStat ? Percent(amount) + " of the hero's " + CombatTextCatalog.StatName(aura.SourceStat) :
                            (modifier.ResolvedTarget == ModifierTarget.Stat && modifier.ResolvedOperation == ModifierOperation.Flat || modifier.ResolvedTarget == ModifierTarget.FlatDamageReduction)
                                ? amount.ToString(CultureInfo.InvariantCulture) : Percent(amount);
                        switch (modifier.ResolvedTarget)
                        {
                            case ModifierTarget.Stat: case ModifierTarget.StatBundle:
                                sentence = verb + Possessive(target) + " " + label + " by " + value; break;
                            case ModifierTarget.AnyDamageDealt: sentence = verb + "damage dealt by " + target + " by " + value; break;
                            case ModifierTarget.AnyDamageReceived: sentence = verb + "damage taken by " + target + " by " + value; break;
                            case ModifierTarget.FinalDamageReduction: sentence = verb + "damage reduction for " + target + " by " + value; break;
                            case ModifierTarget.UltimateDamageDealt: sentence = verb + "Ultimate damage dealt by " + target + " by " + value; break;
                            case ModifierTarget.FamilyDamageDealt: sentence = verb + Family(modifier.ResolvedFamily) + " damage dealt by " + target + " by " + value; break;
                            case ModifierTarget.FamilyDamageReceived: sentence = verb + Family(modifier.ResolvedFamily) + " damage taken by " + target + " by " + value; break;
                            case ModifierTarget.FlatDamageReduction: sentence = verb + "flat damage reduction for " + target + " by " + value; break;
                            default: return Finish(fallbackAuthored);
                        }
                    }
                    if (aura.Scaling == PassiveScaling.FieldStatusStacks)
                    {
                        if (aura.ScalingKey != CombatTags.Ignite) return Finish(fallbackAuthored);
                        sentence += " per Ignite stack " + (aura.ScalingRelation == PassiveRelation.Enemies ? "on enemies" :
                            aura.ScalingRelation == PassiveRelation.Self ? "on the hero" : aura.ScalingRelation == PassiveRelation.Allies ? "on allies" : "on the field");
                        if (aura.MaximumUnits != int.MaxValue) sentence += " (up to " + Percent(amount * aura.MaximumUnits) + ")";
                    }
                    if (aura.Gate.Modes == BattleModeMask.PvP) sentence += " in PvP";
                    else if (aura.Gate.Modes == BattleModeMask.PvE) sentence += " in PvE";
                    sentences.Add(Finish(sentence));
                }
            }
            return string.Join("\n\n", sentences);
        }

        private static string Target(PassiveTargetFilter filter)
        {
            if (filter.Relation == PassiveRelation.Self) return "the hero";
            var qualifiers = new List<string>();
            foreach (var id in new[] { filter.AttributeId, filter.TraitId, filter.SeriesId })
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                var token = id.Substring(id.LastIndexOf('.') + 1).Replace('-', ' ');
                qualifiers.Add(token == "kof" ? "KOF" : token == "women" ? "Women" : char.ToUpperInvariant(token[0]) + token.Substring(1));
            }
            var relation = filter.Relation == PassiveRelation.Enemies ? "enemies" : filter.Relation == PassiveRelation.Any ? "fighters" : "allies";
            return (filter.IncludeOwner || filter.Relation == PassiveRelation.Enemies ? "all " : "other ") +
                (qualifiers.Count > 0 ? string.Join(" ", qualifiers) + " " : "") + relation;
        }

        private static string FindBundle(StatModifierDefinition[] modifiers)
        {
            if (modifiers.Any(m => m.ResolvedTarget != ModifierTarget.Stat)) return null;
            var stats = new HashSet<StatId>(modifiers.Select(m => m.ResolvedStat));
            foreach (var bundle in new[] { StatBundleKind.BasicStats, StatBundleKind.AttackRelated, StatBundleKind.DefenseRelated, StatBundleKind.HpRelated, StatBundleKind.AllStats })
                if (stats.SetEquals(Enum.GetValues(typeof(StatId)).Cast<StatId>().Where(s => StatBundleRules.Contains(bundle, s)))) return Bundle(bundle);
            return null;
        }
        private static string Bundle(StatBundleKind bundle) => bundle switch
        {
            StatBundleKind.BasicStats => "Basic Stats", StatBundleKind.AttackRelated => "Attack-related stats",
            StatBundleKind.DefenseRelated => "Defense-related stats", StatBundleKind.HpRelated => "HP-related stats",
            StatBundleKind.AllStats => "All Stats", _ => "Stats"
        };
        private static string Possessive(string target) => target == "the hero" ? "the hero's" : target.EndsWith("s", StringComparison.Ordinal) ? target + "'" : target + "'s";
        private static string Family(DamageFamily family) => family == DamageFamily.DamageOverTime ? "DOT" :
            family == DamageFamily.ElementalReaction ? "Elemental Reaction" : family.ToString();
        private static string Join(string[] names) => names.Length < 2 ? names.FirstOrDefault() ?? "" : string.Join(", ", names.Take(names.Length - 1)) + " and " + names.Last();
        private static string Percent(long value) => (value / 100d).ToString("0.##", CultureInfo.InvariantCulture) + "%";
        private static string Finish(string text) => string.IsNullOrWhiteSpace(text) ? "" :
            CombatTextCatalog.ColorConditionalClauses(text.TrimEnd(' ', '.') + ".");
    }
}
