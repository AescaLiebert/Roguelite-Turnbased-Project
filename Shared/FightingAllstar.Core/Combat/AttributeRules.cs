using System;

namespace FightingAllstar.Core.Combat
{
    public enum AttributeAffinity
    {
        Neutral = 0,
        Advantage = 1,
        Disadvantage = 2
    }

    public static class AttributeRules
    {
        public const int AdvantageFactorBp = 12000;   // +20% damage
        public const int DisadvantageFactorBp = 8000; // -20% damage
        public const int NeutralFactorBp = 10000;     // Baseline 1.0x

        public static AttributeAffinity GetAffinity(string attackerAttributeId, string defenderAttributeId)
        {
            if (string.IsNullOrEmpty(attackerAttributeId) || string.IsNullOrEmpty(defenderAttributeId))
                return AttributeAffinity.Neutral;

            var atk = Normalize(attackerAttributeId);
            var def = Normalize(defenderAttributeId);

            if (atk == def || string.IsNullOrEmpty(atk) || string.IsNullOrEmpty(def))
                return AttributeAffinity.Neutral;

            // 5-way cycle: Red -> Green -> Yellow -> Purple -> Blue -> Red
            // Standalone attributes (Darkness, Light, Infinity) do not participate in the cycle and are always Neutral.
            switch (atk)
            {
                case "red":
                    if (def == "green") return AttributeAffinity.Advantage;
                    if (def == "blue") return AttributeAffinity.Disadvantage;
                    return AttributeAffinity.Neutral;

                case "green":
                    if (def == "yellow") return AttributeAffinity.Advantage;
                    if (def == "red") return AttributeAffinity.Disadvantage;
                    return AttributeAffinity.Neutral;

                case "yellow":
                    if (def == "purple") return AttributeAffinity.Advantage;
                    if (def == "green") return AttributeAffinity.Disadvantage;
                    return AttributeAffinity.Neutral;

                case "purple":
                    if (def == "blue") return AttributeAffinity.Advantage;
                    if (def == "yellow") return AttributeAffinity.Disadvantage;
                    return AttributeAffinity.Neutral;

                case "blue":
                    if (def == "red") return AttributeAffinity.Advantage;
                    if (def == "purple") return AttributeAffinity.Disadvantage;
                    return AttributeAffinity.Neutral;

                default:
                    return AttributeAffinity.Neutral;
            }
        }

        public static int GetFactorBp(AttributeAffinity affinity) => affinity switch
        {
            AttributeAffinity.Advantage => AdvantageFactorBp,
            AttributeAffinity.Disadvantage => DisadvantageFactorBp,
            _ => NeutralFactorBp
        };

        public static string Normalize(string attr)
        {
            if (string.IsNullOrEmpty(attr)) return string.Empty;
            var lower = attr.Trim().ToLowerInvariant();
            if (lower.StartsWith("attribute."))
                lower = lower.Substring("attribute.".Length);
            return lower;
        }
    }
}
