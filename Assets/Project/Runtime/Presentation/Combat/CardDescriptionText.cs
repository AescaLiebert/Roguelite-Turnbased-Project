using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>
    /// Damage is described from executable data. Authored supplementary rules remain
    /// intact (including passive-driven card effects not stored in the root effect).
    /// This is a presentation adapter; it never changes combat data or source text.
    /// </summary>
    public static class CardDescriptionText
    {
        private static readonly Regex DamageClause = new Regex(
            @"\b(?:(?:inflicts?|deals?)\s+(?:[\w-]+\s+){0,7}?|slashes?\s+one enemy\s+for\s+)" +
            @"\d+(?:\.\d+)?%\s+(?:of\s+)?(?:the hero's\s+|own\s+)?(?:Max HP|Attack|Defense|HP)" +
            @"(?:\s+(?:(?:[\w-]+\s+){0,2})?damage)?" +
            @"(?:\s+(?:on|to)\s+(?:one enemy|one ally|all enemies|all allies|all targets|single target|single enemy|the enemy))?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static string NormalizeProse(string text)
        {
            text = CombatTextCatalog.Normalize(text ?? string.Empty).Trim();
            text = Regex.Replace(text, @"\s+in\s+\d+\s+hits\b", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            text = Regex.Replace(text, @"\ballies['’]s\b", "allies'", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\balies['’]s\b", "allies'", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bselected (ally|enemy)\b", "one $1", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bult(?:imate)? gauges?\b", "Power Gauge", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\b1 (turns|gauges|orbs|stacks)\b", m => "1 " + m.Groups[1].Value.TrimEnd('s'), RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\b(\d+) turn\(s\)", m => m.Groups[1].Value + (m.Groups[1].Value == "1" ? " turn" : " turns"));
            text = Regex.Replace(text, @"\bself\b", "the hero", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bown (?=(?:Attack|Defense|Max HP|Crit|Lifesteal|damage|stats|Power Gauge)\b)", "the hero's ", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bIncrease allies'", "Increases allies'", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bDecrease allies'", "Decreases allies'", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\battack power\b", "Attack", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bdefense power\b", "Defense", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bDamage Received\b", "damage taken", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bPG gauge\b", "Power Gauge", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bControl Rate\b", "Control", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\b(?:a |an )?Bleed enemy\b", "an enemy affected by Bleed", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bthe hero not damaged\b", "the hero takes no damage", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bgains 1 point Power Gauge to the hero\b", "increase the hero's Power Gauge by 1 orb", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bWhen own attacks\b", "When the hero attacks", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bWhen attacking will Increase\b", "When attacking, increase", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\b(\d+) turn\b", match => match.Groups[1].Value + (match.Groups[1].Value == "1" ? " turn" : " turns"));
            text = Regex.Replace(text, @"[ \t]{2,}", " ");
            return text.Length == 0 ? string.Empty : char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        public static string Format(string authoredText, EffectDefinition effect)
        {
            var text = NormalizeProse(authoredText);
            if (effect == null) return Finish(text);
            if (CardEffectText.TryFormat(effect, text, out var generated)) return Finish(generated);
            var damage = effect.Kind == EffectKind.Damage ? effect : effect.Sequence?
                .Select(s => s?.Effect).FirstOrDefault(e => e?.Kind == EffectKind.Damage);
            if (damage == null) return Finish(text); // Healing, Buff, Debuff and Stance cards retain their actual action.

            var sentence = DamageSentence(damage);
            var hasPreEffects = effect.Sequence != null && effect.Sequence.Any(s => s?.Timing == CardEffectTiming.BeforeAction);
            var hasTimingNote = Regex.IsMatch(text, @"\bBefore dealing damage,\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            // Catalog definitions already contain canonical presentation text while
            // SourceDescription retains the original. Reuse it only when no pre-action
            // timing needs to be moved ahead of the damage clause.
            if (text.StartsWith(sentence, StringComparison.Ordinal) && !hasPreEffects && !hasTimingNote) return Finish(text);
            // A colored elemental sentence already came from this formatter. Its markup
            // must not be parsed as words or move an already ordered pre-action clause again.
            if (damage.DamageElement != ElementId.None && !hasTimingNote && text.IndexOf(sentence, StringComparison.OrdinalIgnoreCase) >= 0)
                return Finish(text);
            var match = DamageClause.Match(text);
            if (!match.Success) return Finish(text.Length == 0 ? sentence : text);

            // Keep special rules such as Taki's passive-driven additional damage,
            // Ivy's stat transfer and conditional heals rather than regenerating only
            // the subset of effects present in the card's root/sequence.
            var before = TrimConnector(text.Substring(0, match.Index));
            var after = TrimConnector(text.Substring(match.Index + match.Length));
            string preSentence = null;
            var timingNote = Regex.Match(after, @"(?:^|[.,]\s*)Before dealing damage,\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (timingNote.Success)
            {
                var noteStart = timingNote.Index + timingNote.Length;
                preSentence = FollowUp(after.Substring(noteStart));
                after = TrimConnector(after.Substring(0, timingNote.Index));
            }
            if (!string.IsNullOrEmpty(before))
            {
                // A condition in front of the damage clause may qualify damage too.
                // Preserve its scope in place rather than making it unconditional.
                if (before.StartsWith("If ", StringComparison.OrdinalIgnoreCase) && damage.Conditions?.Count > 0)
                    return Finish(before + ", " + LowerFirst(sentence) + (after.Length == 0 ? "" : ", then " + LowerFirst(after)));
                var authoredPre = FollowUp(before);
                preSentence = preSentence == null ? authoredPre : authoredPre + ", then " + preSentence;
            }
            else if (preSentence == null && hasPreEffects)
            {
                // Explicit authored timing remains in the prose; damage wording must
                // not imply that pre-action effects are applied after the hit.
                var preSteps = effect.Sequence.Where(s => s?.Timing == CardEffectTiming.BeforeAction && s.Effect != null)
                    .Select(s => DescribePreEffect(s.Effect)).Where(s => !string.IsNullOrEmpty(s)).ToArray();
                if (preSteps.Length > 0) preSentence = string.Join(", then ", preSteps);
            }

            if (after.Length > 0)
            {
                // Additional damage is part of the same attack resolution. Joining
                // it with "then" incorrectly presents it as a later action.
                if (Regex.IsMatch(after, @"^plus\s+additional damage\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    sentence += ", " + LowerFirst(after);
                else
                    sentence += ", then " + FollowUp(after);
            }
            if (preSentence != null) sentence = UpperFirst(preSentence) + ", then " + LowerFirst(sentence);
            return Finish(sentence);
        }

        public static string DamageSentence(EffectOperationDefinition effect)
        {
            var keyword = CombatTextCatalog.CanonicalKeyword(effect.KeywordId);
            // These IDs describe an application/PG operation, not a damage formula.
            if (keyword.Equals("ultimate", StringComparison.OrdinalIgnoreCase) || keyword.Equals("Depletes", StringComparison.OrdinalIgnoreCase) ||
                keyword.Equals("Fills", StringComparison.OrdinalIgnoreCase) || keyword.Equals("Ignite", StringComparison.OrdinalIgnoreCase) ||
                keyword.Equals("Poison", StringComparison.OrdinalIgnoreCase) || keyword.Equals("Bleed", StringComparison.OrdinalIgnoreCase) ||
                keyword.Equals("Shock", StringComparison.OrdinalIgnoreCase) || keyword.Equals("Explosion", StringComparison.OrdinalIgnoreCase)) keyword = string.Empty;
            var damageType = keyword.Length > 0 ? keyword + " damage" : "damage";
            if (effect.DamageElement != ElementId.None)
            {
                var elementName = effect.DamageElement.ToString();
                var color = UnityEngine.ColorUtility.ToHtmlStringRGB(GenshinCombatTextStyle.ElementColor(effect.DamageElement));
                damageType = "<color=#" + color + ">" + elementName + "</color> " + damageType;
            }
            var target = TargetName(effect.Target);
            if (effect.Scaling == StatScaling.Fixed)
                return "Inflict " + damageType + " equal to " + effect.Magnitude.ToString(CultureInfo.InvariantCulture) + " on " + target;
            var stat = effect.Scaling == StatScaling.Defense ? StatId.Defense : effect.Scaling == StatScaling.MaxHealth ? StatId.MaxHealth :
                effect.Scaling == StatScaling.SpecificStat ? effect.ScalingStat : StatId.Attack;
            return "Inflict " + damageType + " equal to " + (effect.CoefficientBp / 100d).ToString("0.##", CultureInfo.InvariantCulture) +
                "% of the hero's " + CombatTextCatalog.StatName(stat) + " on " + target;
        }

        private static string DescribePreEffect(EffectOperationDefinition effect) => effect.Kind switch
        {
            EffectKind.RemoveBuffs => "remove Buffs from " + TargetName(effect.Target),
            EffectKind.RemoveStance => "cancel Stances on " + TargetName(effect.Target),
            EffectKind.Cleanse => "remove Debuffs from " + TargetName(effect.Target),
            _ => string.Empty
        };

        public static string TargetName(EffectTargetScope target) => target switch
        {
            EffectTargetScope.SelectedEnemy => "one enemy", EffectTargetScope.SelectedAlly => "one ally",
            EffectTargetScope.AllEnemies => "all enemies", EffectTargetScope.AllAllies => "all allies",
            EffectTargetScope.Self => "the hero", EffectTargetScope.TriggerActor => "the triggering fighter",
            EffectTargetScope.TriggerTarget => "the triggering target", EffectTargetScope.StatusOwner => "the affected fighter",
            _ => "the target"
        };

        private static string TrimConnector(string text)
        {
            text = text.Trim(' ', '.', ',');
            text = Regex.Replace(text, @"^(?:(?:and|then)\s+)+|(?:,?\s+(?:and|then))+$", "", RegexOptions.IgnoreCase).Trim(' ', '.', ',');
            return text;
        }
        private static string LowerFirst(string text) => text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text.Substring(1);
        private static string UpperFirst(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
        private static string FollowUp(string text)
        {
            text = LowerFirst(text);
            text = Regex.Replace(text, @"^(applies|increases|decreases|reduces|inflicts|disables|causes|steals|removes|cancels|grants|restores|heals)\b",
                match => match.Value == "applies" ? "apply" : match.Value.Substring(0, match.Value.Length - 1));
            text = Regex.Replace(text, @"\band (increases|decreases|reduces|disables|cancels|grants)\b", match => "and " + match.Groups[1].Value.Substring(0, match.Groups[1].Value.Length - 1));
            text = Regex.Replace(text, @"^there (?:is |a )?(\d+%) chance to Stun", "has a $1 chance to apply Stun", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\b(\d+%) (\d+ turns?)\b", "$1 for $2");
            return text;
        }
        private static string Finish(string text) => string.IsNullOrWhiteSpace(text) ? string.Empty :
            CombatTextCatalog.ColorConditionalClauses(text.TrimEnd(' ', '.') + ".");
    }
}
