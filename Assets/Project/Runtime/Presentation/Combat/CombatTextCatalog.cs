using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>
    /// Player-facing vocabulary from source-catalog.json and the combat source contract.
    /// IDs, spelling aliases, stat names and explanations are separate from icon assets.
    /// </summary>
    public static class CombatTextCatalog
    {
        public sealed class Term
        {
            public readonly string Name;
            public readonly string Explanation;
            public readonly string[] Aliases;
            public Term(string name, string explanation, params string[] aliases)
            { Name = name; Explanation = explanation; Aliases = aliases; }
        }

        public static readonly Term[] Terms =
        {
            new Term("Charge", "Ignores the target's Defense."),
            new Term("Shatter", "As an attack keyword, ignores the target's Resistance. As a reaction, attacking a Frozen target breaks the ice and deals one non-elemental hit at 3x level-based reaction damage, with DOT Damage Dealt bonuses."),
            new Term("Rupture", "Deals 2 times damage to targets with Buffs."),
            new Term("Detonate", "Increases damage dealt by 20% per Power Gauge orb on the target."),
            new Term("Blaze", "Increases damage dealt by 25% per Ignite stack on the target."),
            new Term("Depletes", "Decreases the target's Power Gauge."),
            new Term("Fills", "Increases the hero's Power Gauge."),
            new Term("Clash", "Increases damage dealt by 20% per Power Gauge orb on the hero. At 4 or more orbs, removes 1 orb from the target."),
            new Term("Co-Destruction", "Increases damage dealt by 20% per Debuff stack on the target.", "CoDestruction"),
            new Term("Sever", "Multiplies the hero's Crit Chance by 3."),
            new Term("Spike", "Multiplies the hero's Crit Damage by 2."),
            new Term("Breakthrough", "Ignores the target's Defense-related stats."),
            new Term("Pierce", "Multiplies the hero's Pierce Rate by 3."),
            new Term("Weakpoint", "Deals 3 times damage to targets with Debuffs.", "Weak Point"),
            new Term("Flood", "Increases damage dealt by 0.8% for every 1% of the hero's remaining HP, up to 80%."),
            new Term("Power Strike", "Increases damage dealt based on the target's Resistance.", "PowerStrike"),
            new Term("Cleave", "Increases damage dealt by 75% of the hero's Crit Damage above 100%. This attack cannot critically hit."),
            new Term("Quell", "Increases damage dealt by 50% per Stance on the hero."),
            new Term("Amplify", "Increases damage dealt by 30% per Normal-color Buff on the hero."),
            new Term("Secret Technique", "Increases damage dealt by 20% per card belonging to the hero in hand.", "SecretTechnique"),
            new Term("Absorb Energy", "Removes 3 Power Gauge orbs from the target, deals additional damage equal to 30% of their Max HP, and heals the hero by the additional damage dealt.", "AbsorbEnergy"),
            new Term("DOT Burst", "Deals additional damage per damage-over-time Debuff on the target.", "DotBurst"),
            new Term("DOT Explosion", "Deals additional damage based on the target's damage-over-time Debuffs, then removes those Debuffs.", "DotExplosion"),
            new Term("Remove Buff", "Removes Buffs from the target.", "Removes Buff", "Remove Buffs", "Removes Buffs", "RemoveBuff"),
            new Term("Cancel Stance", "Removes Stances from the target.", "CancelStance"),
            new Term("Abyss", "Deals damage over time at the start of the affected enemy's turn."),
            new Term("Despair", "Heals a portion of the hero's missing HP on a critical hit."),
            new Term("Wave", "Reduces damage dealt for each enemy on the field."),
            new Term("Ignite", "Increases damage taken by 10% per stack.", "Ignites"),
            new Term("Poison", "Deals damage over time equal to " + StandardDotRules.PoisonBp / 100 + "% of damage dealt at the end of the affected target's turn."),
            new Term("Bleed", "Deals damage over time equal to " + StandardDotRules.BleedBp / 100 + "% of damage dealt at the end of the affected target's turn.", "Bleeding"),
            new Term("Shock", "Deals damage over time equal to " + StandardDotRules.ShockBp / 100 + "% of damage dealt at the end of the affected target's turn."),
            new Term("Explosion", "Deals delayed damage based on damage dealt.", "Explodes"),
            new Term("Stun", "Removes stance statuses and their dependent effects. Prevents all actions for the duration.", "Stuns"),
            new Term("Paralyze", "Removes stance statuses and their dependent effects. Prevents all actions for the duration.", "Paralysis"),
            new Term("Freeze", "Applies after the triggering action finishes. Removes and blocks non-grey statuses, including neutral Stance and healing effects. Prevents card use and all HP recovery. An attack triggers Shatter and breaks the ice; otherwise expires after the next target turn. Has a hidden Freeze recovery cooldown.", "Frozen"),
            new Term("Frost Bite", "Pierce -15 percentage points and Power Gauge generation -50% while active. Deals 64% of triggering HP damage at target turn end."),
            new Term("Dissolve", "Removes the displayed number of Power Gauge orbs at the affected enemy's turn end."),
            new Term("Conductive", "Defense -20% per stack; up to three stacks. Lisa can consume these stacks for bonus damage."),
            new Term("Baron Bunny", "Explodes with Pyro damage only on natural expiry. Cleansing safely cancels it; the explosion can react and applies ordinary Ignite independently of its neutral Pyro aura."),
            new Term("Glacial Waltz", "Once per incoming attacking card, retaliates with Cryo damage equal to 15% of HP lost and heals 30% of retaliation HP damage. Only Kaeya receives its 30% damage reduction."),
            new Term("Melody Loop", "Attack +7%. Heals 6% of Barbara's snapshotted Max HP at the start of each protected ally's turn."),
            new Term("Swirl", "Preserves source setup and spreads one fresh elemental contact to occupied adjacent enemy slots. That contact can react."),
            new Term("Crystallize", "Grants a two-turn barrier equal to 30% of actual Geo HP and shield damage, capped at 100% of the Geo actor's Max HP."),
            new Term("Superconduct", "Deals 1.5x level-based Cryo reaction damage with DOT Damage Dealt bonuses to the target and adjacent enemies. Increases non-elemental damage taken by 40% for 2 target turns; cannot refresh."),
            new Term("Electro-Charged", "Deals 1.5x level-based Electro reaction damage with DOT Damage Dealt bonuses at target turn end for 2 turns. Preserves auras and spreads to adjacent Hydro enemies. Cannot refresh.", "Charged"),
            new Term("Overload", "Deals 2x level-based Pyro reaction damage with DOT Damage Dealt bonuses to the target and adjacent enemies. Their shields absorb this damage at 80% efficiency.", "Overloaded"),
            new Term("Melt", "Pyro on Cryo doubles the action damage against that target; Cryo on Pyro multiplies it by 1.5. Consumes the auras.", "Reverse Melt"),
            new Term("Vaporize", "Hydro on Pyro doubles the action damage against that target; Pyro on Hydro multiplies it by 1.5. Consumes the auras.", "Reverse Vaporize"),
            new Term("Burning", "Deals Pyro reaction damage equal to 3x the trigger actor's snapshotted Attack at target turn end for two turns; up to three independent stacks."),
            new Term("Card Seal", "Prevents using skill cards for the duration."),
            new Term("Disable Attack", "Prevents using Attack cards.", "Disable Attack Card", "Disable Attack Skills"),
            new Term("Disable Debuff", "Prevents using Debuff cards.", "Disable Debuff Card", "Disable Debuff Skills"),
            new Term("Disable Buff", "Prevents using Buff cards.", "Disable Buff Card", "Disable Buff Skills"),
            new Term("Disable Recovery", "Prevents all HP recovery.", "Infect"),
            new Term("Disable Healing Card", "Prevents using Recovery cards. Other healing effects still work.", "Disable Healing Skills"),
            new Term("Disable Stance", "Prevents using or receiving Stances.", "Disable Stance Card", "Disable Stance Skills"),
            new Term("Disable Ultimate", "Prevents using Ultimate cards."),
            new Term("Mark of Concentration", "Decreases Crit Chance by 100%."),
            new Term("Cleanse", "Removes eligible Debuffs."),
            new Term("Rejuvenation", "At turn start, heals an additional 60% of HP recovered per stack.", "Rejuvenates"),
            new Term("Debuff Immunity", "Prevents receiving Debuffs for the duration."),
            new Term("Barrier", "Absorbs incoming damage with a protective shield."),
            new Term("Taunt", "Forces enemies to target the hero with single-target attacks."),
            new Term("Evade", "Evades attacks for the duration."),
            new Term("Extort", "Decreases the target's stats and increases the hero's stats by the stolen amount.")
        };

        private static readonly Dictionary<string, Term> Lookup = BuildLookup();
        // Longest match wins: Recovery Rate must never produce a Recovery status;
        // Disable Attack must never contain a separately highlighted Attack stat.
        private static readonly Dictionary<string, string> StatAliases = BuildStats();
        private static readonly Regex TokenPattern = new Regex(@"(?<![\w])(?:" + string.Join("|",
            Lookup.Keys.Concat(StatAliases.Keys).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(s => s.Length).ThenBy(s => s, StringComparer.Ordinal)
                .Select(Regex.Escape)) + @")(?![\w])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex ConditionalClausePattern = new Regex(
            @"(^|[.!?,;]\s+|(?:then|and|but)\s+|\r?\n)(If\b(?:[^.!?\r\n]|\.(?=\d))*(?:[!?]|\.(?!\d)|$))",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);

        private static Dictionary<string, Term> BuildLookup()
        {
            var lookup = new Dictionary<string, Term>(StringComparer.OrdinalIgnoreCase);
            foreach (var term in Terms)
            { lookup[term.Name] = term; foreach (var alias in term.Aliases) lookup[alias] = term; }
            return lookup;
        }

        private static Dictionary<string, string> BuildStats()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (StatId stat in Enum.GetValues(typeof(StatId))) result[StatName(stat)] = StatName(stat);
            result["ATK"] = "Attack"; result["DEF"] = "Defense";
            result["MaxHealth"] = result["MaxHP"] = result["Maximum HP"] = "Max HP";
            result["Critical Chance"] = "Crit Chance"; result["Critical Damage"] = "Crit Damage";
            result["Critical Resistance"] = "Crit Resistance"; result["Critical Defense"] = "Crit Defense";
            result["Recovery"] = "Recovery Rate"; result["LifeSteal"] = "Lifesteal";
            foreach (var group in new[] { "Basic Stats", "Attack-related stats", "Defense-related stats", "HP-related stats", "All Stats" })
                result[group] = group;
            result["Attack Related"] = "Attack-related stats"; result["Defense Related"] = "Defense-related stats";
            result["HP Related"] = "HP-related stats";
            result["HP"] = "HP";
            return result;
        }

        public static string StatName(StatId stat) => stat switch
        {
            StatId.MaxHealth => "Max HP", StatId.Pierce => "Pierce Rate", StatId.Recovery => "Recovery Rate",
            StatId.LifeSteal => "Lifesteal", StatId.DamageOverTimeDealt => "DOT Dealt",
            StatId.DamageDealt => "Damage Dealt", StatId.AdditionalDamageDealt => "Additional Damage Dealt",
            _ => Regex.Replace(stat.ToString(), "([a-z])([A-Z])", "$1 $2")
        };

        public static string CanonicalKeyword(string id)
        {
            var value = (id ?? string.Empty).Trim();
            if (value.StartsWith("attack.", StringComparison.OrdinalIgnoreCase)) value = value.Substring(7);
            if (Lookup.TryGetValue(value, out var term)) return term.Name;
            var compact = Regex.Replace(value, @"[\s._-]", "");
            return Terms.FirstOrDefault(t => string.Equals(Regex.Replace(t.Name, @"[\s._-]", ""), compact,
                StringComparison.OrdinalIgnoreCase))?.Name ?? value;
        }

        public static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            // Do not rewrite markup or inject terms into an existing rich-text tag.
            return Regex.Replace(text, @"<[^>]+>|[^<]+", part => part.Value.StartsWith("<", StringComparison.Ordinal)
                ? part.Value : TokenPattern.Replace(part.Value, match => StatAliases.TryGetValue(match.Value, out var stat)
                    ? stat : match.Value.Equals("Remove Buffs", StringComparison.OrdinalIgnoreCase) || match.Value.Equals("Removes Buffs", StringComparison.OrdinalIgnoreCase)
                        ? "Remove Buffs" : Lookup[match.Value].Name));
        }

        public static string ColorConditionalClauses(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
            return ConditionalClausePattern.Replace(text, match => match.Groups[1].Value +
                "<color=" + CombatColors.ConditionalGray + ">" + match.Groups[2].Value + "</color>");
        }

        public static string Highlight(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            // One token pass avoids nested partial matches in compound names.
            var colorStack = new Stack<bool>();
            return Regex.Replace(Normalize(text), @"<[^>]+>|[^<]+", part =>
            {
                if (part.Value.StartsWith("<", StringComparison.Ordinal))
                {
                    if (part.Value.StartsWith("<color=", StringComparison.OrdinalIgnoreCase))
                        colorStack.Push(part.Value.StartsWith("<color=" + CombatColors.ConditionalGray + ">", StringComparison.OrdinalIgnoreCase));
                    else if (part.Value.Equals("</color>", StringComparison.OrdinalIgnoreCase) && colorStack.Count > 0)
                        colorStack.Pop();
                    return part.Value;
                }
                // Grey is a base style: keywords, stats and values keep their own
                // colors inside a conditional clause. Preserve any more specific
                // rich-text color already applied to a span.
                if (colorStack.Any(isConditional => !isConditional)) return part.Value;
                return Regex.Replace(TokenPattern.Replace(part.Value, match =>
                {
                    var isStat = StatAliases.ContainsKey(match.Value);
                    return "<color=" + (isStat ? CombatColors.StatBlue : CombatColors.KeywordCyan) + "><b>" + match.Value + "</b></color>";
                }), @"\b\d+(?:\.\d+)?(?:%|\s+turns?\b)", "<color=" + CombatColors.ValueAmber + "><b>$0</b></color>");
            });
        }

        public static IEnumerable<Term> FindTerms(string text)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in TokenPattern.Matches(Regex.Replace(text ?? string.Empty, @"<[^>]+>", "")))
                if (!StatAliases.ContainsKey(match.Value) && Lookup.TryGetValue(match.Value, out var term) && seen.Add(term.Name)) yield return term;
        }
    }
}
