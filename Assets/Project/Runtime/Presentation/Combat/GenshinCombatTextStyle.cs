using FightingAllstar.Core.Content;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Typography, elemental identity and animation for captions owned by a Genshin fighter.</summary>
    public static class GenshinCombatTextStyle
    {
        private static Font _font;
        public static Font Font => _font != null ? _font : (_font = Resources.Load<Font>("UI/zh-cn"));
        public const float Width = 340f;
        public static float FontSize(string kind) => kind switch {
            "critical" => 64f, "damage" => 36f, "blocked" => 34f, "heal" => 28f,
            "reaction" => 24f, "status" or "passive" => 21f, _ => 30f };
        public static bool IsNumeric(string kind) => kind == "damage" || kind == "critical" || kind == "blocked" || kind == "heal";
        public static string FormatNumber(string text)
        {
            var positive = text.StartsWith("+", System.StringComparison.Ordinal);
            return decimal.TryParse(text, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.CurrentCulture, out var value)
                ? (positive ? "+" : "") + value.ToString("0", System.Globalization.CultureInfo.InvariantCulture) : text;
        }
        public static Color Outline(Color color) => new Color(color.r * .48f, color.g * .48f, color.b * .48f, .85f);
        // A small deterministic fan keeps separate hit packets readable without building a staircase.
        public static Vector2 Placement(int lane, bool reaction) => reaction ? new Vector2(0, -38f) : (lane % 4) switch {
            0 => new Vector2(-16, 8), 1 => new Vector2(22, -34),
            2 => new Vector2(-30, 32), _ => new Vector2(28, -54) };
        public static Color ElementColor(ElementId element) => element switch {
            ElementId.Pyro => new Color32(255, 157, 0, 255),
            ElementId.Hydro => new Color32(46, 198, 242, 255),
            ElementId.Electro => new Color32(222, 151, 244, 255),
            ElementId.Cryo => new Color32(156, 235, 239, 255),
            ElementId.Dendro => new Color32(165, 216, 67, 255),
            ElementId.Anemo => new Color32(116, 235, 194, 255),
            ElementId.Geo => new Color32(242, 205, 112, 255),
            _ => new Color32(249, 247, 238, 255) };

        public static Color ReactionColor(ElementalReactionId reaction) => reaction switch {
            ElementalReactionId.Melt or ElementalReactionId.ReverseMelt => new Color32(255, 177, 44, 255),
            ElementalReactionId.Vaporize or ElementalReactionId.ReverseVaporize => new Color32(248, 205, 138, 255),
            ElementalReactionId.Overload => new Color32(255, 117, 93, 255),
            ElementalReactionId.Freeze => ElementColor(ElementId.Cryo),
            ElementalReactionId.Shatter => ElementColor(ElementId.None),
            ElementalReactionId.Superconduct => new Color32(173, 191, 255, 255),
            ElementalReactionId.ElectroCharged => ElementColor(ElementId.Electro),
            ElementalReactionId.Burning => ElementColor(ElementId.Pyro),
            ElementalReactionId.Crystallize => ElementColor(ElementId.Geo),
            _ => ElementColor(ElementId.Anemo) };

        public static string ReactionName(ElementalReactionId reaction) => reaction switch {
            ElementalReactionId.ReverseMelt => "Melt",
            ElementalReactionId.ReverseVaporize => "Vaporize",
            ElementalReactionId.ElectroCharged => "Electro-Charged",
            ElementalReactionId.Freeze => "Frozen",
            ElementalReactionId.Overload => "Overloaded",
            _ => reaction.ToString() };

        // Starting envelope: fade in while shrinking, decelerating upward drift, then fade away.
        public static Vector2 Offset(float seconds, float duration, int lane, bool reaction)
        {
            var t = Mathf.Clamp01(seconds / Mathf.Max(.01f, duration));
            var progress = (1f - Mathf.Exp(-3f * t)) / (1f - Mathf.Exp(-3f));
            var direction = lane % 2 == 0 ? -1f : 1f;
            return new Vector2(reaction ? 0 : direction * 6f * progress, -48f * progress);
        }

        public static float Scale(float seconds, bool critical, bool reaction)
        {
            var start = critical ? 1.7f : reaction ? 1.08f : 1.18f;
            var settle = 1f - Mathf.Pow(1f - Mathf.Clamp01(seconds / .18f), 3f);
            return Mathf.Lerp(start, 1f, settle);
        }

        public static float Opacity(float seconds, float duration)
        {
            var entrance = Mathf.SmoothStep(0, 1, seconds / .05f);
            var fade = Mathf.SmoothStep(0, 1, (seconds / Mathf.Max(.01f, duration) - .7f) / .3f);
            return entrance * (1f - fade);
        }
    }
}
