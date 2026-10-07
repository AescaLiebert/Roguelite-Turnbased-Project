using System;
using System.Collections.Generic;

namespace FightingAllstar.Core.Content
{
    public static class StatusLibrary
    {
        private static readonly Dictionary<string, string> DisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // DoTs
            ["status.debuff.poison"] = "Poison",
            ["status.debuff.ignite"] = "Ignite",
            ["status.debuff.bleed"] = "Bleed",
            ["status.debuff.shock"] = "Shock",

            // Disables
            ["status.debuff.seal"] = "Card Seal",
            ["status.debuff.paralyze"] = "Paralyze",
            ["status.debuff.stun"] = "Stun",
            ["status.debuff.freeze"] = "Freeze",
            ["status.debuff.disable-attack"] = "Disable Attack",
            ["status.debuff.disable-debuff"] = "Disable Debuff",
            ["status.debuff.disable-buff"] = "Disable Buff",
            ["status.debuff.disable-recovery"] = "Disable Recovery",
            ["status.debuff.disable.recovery"] = "Disable Recovery",
            ["disable.recovery"] = "Disable Recovery",
            ["status.debuff.disable-healing-card"] = "Disable Healing Card",
            ["status.debuff.disable-heal"] = "Disable Healing Card",
            ["disable.healing-card"] = "Disable Healing Card",
            ["disable.heal"] = "Disable Healing Card",
            ["status.debuff.disable-stance"] = "Disable Stance",
            ["status.debuff.disable.stance"] = "Disable Stance",
            ["disable.stance"] = "Disable Stance",
            ["status.debuff.disable-ultimate"] = "Disable Ultimate",
            ["status.debuff.disable-rank-2-3"] = "Disable Rank 2/3",
            ["status.debuff.disable-card-effect"] = "Disable Card Effect",

            // Special buffs
            ["status.buff.debuffimmunity"] = "Debuff Immunity",
            ["status.buff.rejuvenation"] = "Rejuvenation",

            // Stat groups
            ["status.buff.stat.basic"] = "Increase Basic Stats",
            ["status.debuff.stat.basic"] = "Decrease Basic Stats",
            ["status.buff.stat.attack-related"] = "Increase Attack Stats",
            ["status.debuff.stat.attack-related"] = "Decrease Attack Stats",
            ["status.buff.stat.defense-related"] = "Increase Defense Stats",
            ["status.debuff.stat.defense-related"] = "Decrease Defense Stats",
            ["status.buff.stat.hp-related"] = "Increase HP Stats",
            ["status.debuff.stat.hp-related"] = "Decrease HP Stats",
            ["status.buff.stat.special"] = "Increase Special Stats",
            ["status.debuff.stat.special"] = "Decrease Special Stats",
            ["status.buff.stat.all"] = "Increase All Stats",
            ["status.debuff.stat.all"] = "Decrease All Stats",

            // Individual stats - Buffs
            ["status.buff.stat.attack"] = "Increase Attack",
            ["status.buff.stat.defense"] = "Increase Defense",
            ["status.buff.stat.maxhealth"] = "Increase Max HP",
            ["status.buff.stat.pierce"] = "Increase Pierce",
            ["status.buff.stat.resistance"] = "Increase Resistance",
            ["status.buff.stat.regeneration"] = "Increase Regeneration",
            ["status.buff.stat.critchance"] = "Increase Crit Chance",
            ["status.buff.stat.critdamage"] = "Increase Crit Damage",
            ["status.buff.stat.critresistance"] = "Increase Crit Resistance",
            ["status.buff.stat.critdefense"] = "Increase Crit Defense",
            ["status.buff.stat.recovery"] = "Increase Recovery",
            ["status.buff.stat.lifesteal"] = "Increase Lifesteal",

            // Individual stats - Debuffs
            ["status.debuff.stat.attack"] = "Decrease Attack",
            ["status.debuff.stat.defense"] = "Decrease Defense",
            ["status.debuff.stat.maxhealth"] = "Decrease Max HP",
            ["status.debuff.stat.pierce"] = "Decrease Pierce",
            ["status.debuff.stat.resistance"] = "Decrease Resistance",
            ["status.debuff.stat.regeneration"] = "Decrease Regeneration",
            ["status.debuff.stat.critchance"] = "Decrease Crit Chance",
            ["status.debuff.stat.critdamage"] = "Decrease Crit Damage",
            ["status.debuff.stat.critresistance"] = "Decrease Crit Resistance",
            ["status.debuff.stat.critdefense"] = "Decrease Crit Defense",
            ["status.debuff.stat.recovery"] = "Decrease Recovery",
            ["status.buff.stat.recovery"] = "Increase Recovery",
            ["status.debuff.recoveryrate"] = "Decrease Recovery",
            ["status.buff.recoveryrate"] = "Increase Recovery",
            ["status.debuff.recovery"] = "Decrease Recovery",
            ["status.buff.recovery"] = "Increase Recovery",
            ["status.debuff.stat.lifesteal"] = "Decrease Lifesteal"
        };

        public static string GetDisplayName(string recipeId, StatusPolarity polarity = StatusPolarity.Debuff)
        {
            if (string.IsNullOrWhiteSpace(recipeId))
                return polarity == StatusPolarity.Buff ? "Buff Applied" : "Debuff Applied";

            var trimmed = recipeId.Trim();
            if (DisplayNames.TryGetValue(trimmed, out var displayName))
                return displayName;

            // Normalize and parse components
            var lower = trimmed.ToLowerInvariant();
            var isBuff = lower.Contains(".buff.") || polarity == StatusPolarity.Buff;
            var isDebuff = lower.Contains(".debuff.") || polarity == StatusPolarity.Debuff;

            // Strip prefix hierarchy
            var lastDot = trimmed.LastIndexOf('.');
            var leaf = lastDot >= 0 ? trimmed.Substring(lastDot + 1) : trimmed;
            leaf = leaf.Replace('_', ' ').Replace('-', ' ');
            var formatted = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(leaf);

            if (isBuff && !formatted.StartsWith("Increase", StringComparison.OrdinalIgnoreCase) && !formatted.StartsWith("Buff", StringComparison.OrdinalIgnoreCase))
                return "Increase " + formatted;
            if (isDebuff && !formatted.StartsWith("Decrease", StringComparison.OrdinalIgnoreCase) && !formatted.StartsWith("Debuff", StringComparison.OrdinalIgnoreCase) && !formatted.StartsWith("Disable", StringComparison.OrdinalIgnoreCase))
                return "Decrease " + formatted;

            return formatted;
        }

        public static string GetIconKey(string recipeId, StatusPolarity polarity)
        {
            var lower = (recipeId ?? string.Empty).ToLowerInvariant();

            // DoTs
            if (lower.Contains("ignite") || lower.Contains("bleed") || lower.Contains("poison") || lower.Contains("shock") || lower.Contains("dot"))
                return "Cardtype_Debuffatk";

            // Specific disables & unique statuses
            if (lower.Contains("disable-heal") || lower.Contains("disable.healing-card") || lower.Contains("disable.healingcard") || lower.Contains("disable-recovery") || lower.Contains("disable.recovery"))
                return "icon_buff_cc_dis_heal_skill";
            if (lower.Contains("disable-stance") || lower.Contains("disable.stance") || (lower.Contains("disable") && lower.Contains("stance")))
                return "icon_buff_cc_dis_pose_skill";
            if (lower.Contains("rejuvenation"))
                return "icon_buff_heal_dot_heal";
            if (lower.Contains("debuffimmunity") || lower.Contains("immunity"))
                return "icon_buff_number_immune_debuff";
            if (lower.Contains("recovery") && polarity == StatusPolarity.Debuff)
                return "icon_buff_explosion_debuff_add_per";

            // Disables
            if (lower.Contains("stun") || lower.Contains("paralyze") || lower.Contains("seal") || lower.Contains("freeze") || lower.Contains("disable"))
                return "Cardtype_Debuff";

            // Specific stat icons
            if (lower.Contains("attack") && polarity == StatusPolarity.Buff)
                return "sword";
            if (lower.Contains("defense") && polarity == StatusPolarity.Buff)
                return "shield";
            if ((lower.Contains("hp") || lower.Contains("heal") || lower.Contains("recovery") || lower.Contains("regen")) && polarity == StatusPolarity.Buff)
                return "heart";

            // Fallback by polarity
            if (polarity == StatusPolarity.Buff)
                return "Cardtype_buff";

            return "Cardtype_Debuff";
        }
    }
}
