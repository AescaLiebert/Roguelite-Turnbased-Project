using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Presentation.Combat
{
    public static class AuthoredCombatText
    {
        public static string Render(AuthoredDescription wording, EffectDefinition effect = null, int tier = 0, PassiveDefinition passive = null)
        {
            if (wording == null) return string.Empty;
            var text = ResolveNumbers(wording.Text ?? "", effect, tier, passive);
            var highlights = (wording.Highlights ?? new List<DescriptionHighlight>()).Where(h => h != null && !string.IsNullOrEmpty(h.Text))
                .GroupBy(h => h.Text, StringComparer.OrdinalIgnoreCase).Select(g => g.Last()).OrderByDescending(h => h.Text.Length).ToList();
            if (highlights.Count > 0)
            {
                var lookup = highlights.ToDictionary(h => h.Text, StringComparer.OrdinalIgnoreCase);
                var pattern = string.Join("|", highlights.Select(h => Regex.Escape(h.Text)));
                var colorDepth = 0;
                text = Regex.Replace(text, @"<[^>]+>|[^<]+", chunk => {
                    if (chunk.Value.StartsWith("<", StringComparison.Ordinal))
                    {
                        if (chunk.Value.StartsWith("<color=", StringComparison.OrdinalIgnoreCase)) colorDepth++;
                        else if (chunk.Value.Equals("</color>", StringComparison.OrdinalIgnoreCase)) colorDepth = Math.Max(0, colorDepth - 1);
                        return chunk.Value;
                    }
                    if (colorDepth > 0) return chunk.Value;
                    return Regex.Replace(chunk.Value, pattern, match => {
                        var style = lookup[match.Value];
                        var color = Regex.IsMatch(style.Color ?? "", @"^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$") ? style.Color : "#60A5FA";
                        return "<color=" + color + ">" + (style.Bold ? "<b>" : "") + match.Value + (style.Bold ? "</b>" : "") + "</color>";
                    }, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                });
            }
            return wording.AutoHighlight ? CombatDetailFormatter.HighlightKeywords(text) : text;
        }

        public static string ResolveNumbers(string text, EffectDefinition effect, int tier, PassiveDefinition passive = null) =>
            Regex.Replace(text ?? "", @"\{\{([\w.:-]+)\}\}", match => {
                var key = match.Groups[1].Value.ToLowerInvariant();
                string value = null;
                if (key == "tier") value = tier.ToString(CultureInfo.InvariantCulture);
                else if (key.StartsWith("number:", StringComparison.Ordinal) && decimal.TryParse(key.Substring(7), NumberStyles.Number, CultureInfo.InvariantCulture, out var literal)) value = literal.ToString("0.##", CultureInfo.InvariantCulture);
                else
                {
                    var operation = (EffectOperationDefinition)effect;
                    var parts = key.Split('.');
                    if (parts.Length == 3 && parts[0] == "step" && int.TryParse(parts[1], out var index))
                    {
                        operation = index > 0 && index <= (effect?.Sequence?.Count ?? 0) ? effect.Sequence[index - 1]?.Effect : null;
                        key = parts[2];
                    }
                    if (parts.Length == 4 && parts[0] == "aura" && parts[2] == "modifier" && int.TryParse(parts[1], out var auraIndex) && int.TryParse(parts[3], out var modifierIndex))
                    {
                        var aura = auraIndex > 0 && auraIndex <= (passive?.Auras?.Count ?? 0) ? passive.Auras[auraIndex - 1] : null;
                        var modifier = modifierIndex > 0 && modifierIndex <= (aura?.Modifiers?.Count ?? 0) ? aura.Modifiers[modifierIndex - 1] : null;
                        if (modifier != null) value = modifier.ResolvedOperation == ModifierOperation.Flat && !(modifier.ResolvedTarget == ModifierTarget.Stat && modifier.ResolvedStat >= StatId.Pierce) ? modifier.Amount.ToString(CultureInfo.InvariantCulture) : Percent(modifier.Amount);
                    }
                    else if (operation != null)
                    {
                        switch (key)
                        {
                            case "damage": value = operation.Scaling == StatScaling.Fixed ? operation.Magnitude.ToString(CultureInfo.InvariantCulture) : Percent(operation.CoefficientBp); break;
                            case "duration": value = (operation.StatusDurationOverride != 0 ? operation.StatusDurationOverride : operation.StatusRecipe?.DefaultDuration ?? 0).ToString(CultureInfo.InvariantCulture); break;
                            case "chance": value = Percent(operation.StatusApplyChanceBp); break;
                            case "hits": value = (operation.Attack?.ResolvedHitCount ?? 1).ToString(CultureInfo.InvariantCulture); break;
                            case "recovery": value = Percent(operation.HealValue?.CoefficientBp ?? operation.HealCoefficientBp); break;
                            case "gauge": value = operation.PowerGaugeAmount.ToString(CultureInfo.InvariantCulture); break;
                            case "stacks": value = operation.StatusStackCount.ToString(CultureInfo.InvariantCulture); break;
                        }
                    }
                }
                if (value == null) return match.Value;
                var prefix = (text ?? "").Substring(0, match.Index);
                var inColor = Regex.Matches(prefix, @"<color=[^>]+>", RegexOptions.IgnoreCase).Count > Regex.Matches(prefix, "</color>", RegexOptions.IgnoreCase).Count;
                return inColor ? value : "<color=" + CombatColors.ValueAmber + "><b>" + value + "</b></color>";
            });

        private static string Percent(int bp) => (bp / 100d).ToString("0.##", CultureInfo.InvariantCulture) + "%";
    }
}
