using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>
    /// Standard formatting constants for combat presentation, tooltips, and inspectors.
    /// </summary>
    public static class CombatColors
    {
        public const string ValueAmber = "#fbbf24";    // Numeric values, percentages, turn counts
        public const string StatBlue = "#60a5fa";      // Core fighter stats (ATK, DEF, HP, etc.)
        public const string KeywordCyan = "#38bdf8";   // Skill mechanics, keywords, and glossaries
        public const string BuffGreen = "#79E5A2";     // Positive deltas / buff indicators
        public const string DebuffRed = "#FF8894";     // Negative deltas / debuff indicators
        public const string MutedSlate = "#94BACB";    // Secondary metadata labels
    }

    /// <summary>
    /// Status badge model for skill cards, passives, and character inspections.
    /// </summary>
    public struct CombatStatusBadgeData
    {
        public string Name;
        public Sprite Icon;
        public bool IsBuff;

        public CombatStatusBadgeData(string name, Sprite icon, bool isBuff)
        {
            Name = name;
            Icon = icon;
            IsBuff = isBuff;
        }
    }

    /// <summary>
    /// Formatted presentation payload for character passives.
    /// </summary>
    public sealed class PassiveDetailData
    {
        public string Title;
        public string SourceType;
        public string Restriction;
        public string RawDescription;
        public string FormattedDescription;
        public string Keywords;
        public List<CombatStatusBadgeData> Statuses;
        public Sprite Icon;
    }

    /// <summary>
    /// Central standard system for formatting combat descriptions, keyword glossaries,
    /// rich-text colors, and status badges across UI Toolkit cards and UGUI Inspector views.
    /// </summary>
    public static class CombatDetailFormatter
    {
        public static string DescribeModifier(StatModifierDefinition modifier, long amount)
        {
            if (modifier == null) return string.Empty;
            var target = modifier.ResolvedTarget;
            string label;
            switch (target)
            {
                case ModifierTarget.Stat: label = DisplayLabel(modifier.ResolvedStat.ToString()); break;
                case ModifierTarget.StatBundle: label = "Stats"; break;
                case ModifierTarget.AnyDamageDealt: label = "Damage dealt"; break;
                case ModifierTarget.AnyDamageReceived: label = "Damage taken"; break;
                case ModifierTarget.FamilyDamageDealt: label = DisplayLabel(modifier.ResolvedFamily.ToString()) + " damage dealt"; break;
                case ModifierTarget.FamilyDamageReceived: label = DisplayLabel(modifier.ResolvedFamily.ToString()) + " damage taken"; break;
                case ModifierTarget.UltimateDamageDealt: label = "Ultimate damage dealt"; break;
                case ModifierTarget.FinalDamageReduction: label = "Damage reduction"; break;
                case ModifierTarget.FlatDamageReduction: label = "Flat damage reduction"; break;
                default: label = DisplayLabel(target.ToString()); break;
            }

            var value = modifier.ResolvedOperation == ModifierOperation.Flat
                ? amount.ToString("+0;-0;0")
                : (amount / 100d).ToString("+0.##;-0.##;0") + "%";
            return label + " " + value;
        }

        public static bool IsModifierBeneficial(StatModifierDefinition modifier, long amount)
        {
            if (modifier == null) return false;
            var target = modifier.ResolvedTarget;
            bool positiveIsGood = target != ModifierTarget.AnyDamageReceived && target != ModifierTarget.FamilyDamageReceived;
            return amount == 0 || (amount > 0) == positiveIsGood;
        }

        private static string DisplayLabel(string value) => Regex.Replace(value ?? string.Empty, "([a-z])([A-Z])", "$1 $2").Replace('-', ' ');

        /// <summary>
        /// Canonical combat keyword glossary explaining card and passive mechanics.
        /// </summary>
        public static readonly Dictionary<string, string> KeywordExplanations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Pierce"] = "3x Pierce Rate Increase.",
            ["Ignite"] = "Increases damage taken by 10% for each stack.",
            ["Poison"] = "Deals additional damage equal to 45% of damage dealt at the end of each turn.",
            ["Bleed"] = "Deals additional damage equal to 33% of damage dealt at the end of each turn.",
            ["Shock"] = "Deals additional damage equal to 36% of damage dealt at the end of each turn.",
            ["Shatter"] = "Ignores enemy Resistance.",
            ["Charge"] = "Ignores enemy Defense.",
            ["Rupture"] = "Deals 2x damage against enemies with Buffs.",
            ["Detonate"] = "Deals 20% additional damage per Power Gauge orb on target.",
            ["Blaze"] = "Deals 25% additional damage per Ignite on target.",
            ["Sever"] = "3x Critical Chance Increase.",
            ["Spike"] = "2x Critical Damage Increase.",
            ["Weakpoint"] = "Deals 3x damage against enemies with Debuffs.",
            ["Weak Point"] = "Deals 3x damage against enemies with Debuffs.",
            ["Amplify"] = "Increases damage dealt by 30% per Buff on self.",
            ["Flood"] = "Increases damage dealt based on remaining HP (up to 80%).",
            ["Despair"] = "Recovers 30% of diminished HP on Critical Hit.",
            ["Cleave"] = "Ignores enemy Critical Defense.",
            ["Quell"] = "Decreases target's Power Gauge.",
            ["Stun"] = "Prevents all actions for the duration.",
            ["Freeze"] = "Prevents actions and takes additional damage when attacked.",
            ["Card Seal"] = "Disables skill card usage for the duration.",
            ["Disable Attack"] = "Prevents using Attack skills.",
            ["Disable Debuff"] = "Prevents using Debuff skills.",
            ["Disable Buff"] = "Prevents using Buff skills.",
            ["Disable Recovery"] = "Prevents all HP recovery.",
            ["Disable Healing Card"] = "Prevents using Recovery cards. Other healing effects still work.",
            ["Disable Stance"] = "Prevents using Stance skills.",
            ["Mark of Concentration"] = "-100% Crit Chance.",
            ["Remove Buff"] = "Removes all Buffs from the target.",
            ["Removes Buff"] = "Removes all Buffs from the target.",
            ["Remove Buffs"] = "Removes all Buffs from the target.",
            ["Cleanse"] = "Removes all Debuffs from allies.",
            ["Rejuvenation"] = "At turn start, heals an additional 60% of HP recovered per stack.",
            ["LifeSteal"] = "Restores HP equal to this percentage of damage dealt.",
            ["Recovery"] = "Decreases or increases HP recovery amount.",
            ["Debuff Immunity"] = "Immune to all debuffs for the duration.",
            ["Barrier"] = "Absorbs incoming damage with a protective shield.",
            ["Taunt"] = "Forces enemies to target this unit.",
            ["Evade"] = "Evades enemy attacks for the duration.",
            ["Extort"] = "Steals a portion of the target's stats and grants them to self."
        };

        /// <summary>
        /// Highlights numbers, stats, and keywords with standardized rich text tags.
        /// </summary>
        public static string HighlightKeywords(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            // 1. Multi-word keywords and phrases first
            text = Regex.Replace(text, @"\b(Disable Healing Card|Disable Recovery|Disable Attack|Disable Debuff|Disable Buff|Disable Stance|Removes Buff|Remove Buffs|Remove Buff|Mark of Concentration|Debuff Immunity|Weak Point)\b", "<color=" + CombatColors.KeywordCyan + "><b>$1</b></color>", RegexOptions.IgnoreCase);

            // 2. Multi-word stats
            text = Regex.Replace(text, @"\b(Pierce Rate|Crit Chance|Crit Damage|Critical Chance|Critical Damage|Critical Defense|Combat Class)\b", "<color=" + CombatColors.StatBlue + "><b>$1</b></color>", RegexOptions.IgnoreCase);

            // 3. Numbers, percentages and turn counts (Amber #fbbf24)
            text = Regex.Replace(text, @"(\b\d+(?:\.\d+)?%)", "<color=" + CombatColors.ValueAmber + "><b>$1</b></color>");
            text = Regex.Replace(text, @"(\b\d+\s*(?:turns?|turn\(s\))\b)", "<color=" + CombatColors.ValueAmber + "><b>$1</b></color>", RegexOptions.IgnoreCase);

            // 4. Single-word Core Stats (Blue #60a5fa)
            text = Regex.Replace(text, @"\b(?<!Disable\s+)(ATK|Attack|DEF|Defense|HP|Resistance)\b", "<color=" + CombatColors.StatBlue + "><b>$1</b></color>", RegexOptions.IgnoreCase);

            // 5. Single-word Keywords & Status Effects (Cyan #38bdf8)
            text = Regex.Replace(text, @"\b(Cleanse|Pierce(?!\s*Rate)|Ignite|Poison|Bleed|Shock|Stun|Freeze|Shatter|Charge|Rupture|Detonate|Blaze|Sever|Spike|Weakpoint|Amplify|Flood|Despair|Cleave|Quell|Card Seal|Rejuvenation|Recovery|Barrier|Taunt|Evade|Extort)\b", "<color=" + CombatColors.KeywordCyan + "><b>$1</b></color>", RegexOptions.IgnoreCase);

            return text;
        }

        /// <summary>
        /// Builds the explanation block (※Keyword: Explanation) for any detected keywords or status effects.
        /// </summary>
        public static string BuildKeywordsExplanation(string desc, string damageKeyword = null, IEnumerable<CombatStatusBadgeData> statuses = null)
        {
            var matched = new List<string>();
            var seenKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var combinedText = (desc ?? string.Empty) + " " + (damageKeyword ?? string.Empty);

            if (statuses != null)
            {
                foreach (var status in statuses)
                {
                    combinedText += " " + status.Name;
                }
            }

            foreach (var pair in KeywordExplanations)
            {
                if (Regex.IsMatch(combinedText, $@"\b{Regex.Escape(pair.Key)}\b", RegexOptions.IgnoreCase))
                {
                    var unifiedKey = pair.Key.Replace("s", "").Replace(" ", "").ToLowerInvariant();
                    if (seenKeywords.Add(unifiedKey))
                    {
                        matched.Add($"※{pair.Key}: {pair.Value}");
                    }
                }
            }

            if (matched.Count == 0) return string.Empty;
            return "<color=" + CombatColors.KeywordCyan + ">" + string.Join("\n", matched) + "</color>";
        }

        /// <summary>
        /// Extracts card details without damage animation lines (pure description, keyword glossary, status badges).
        /// </summary>
        public static (string desc, string keywords, List<CombatStatusBadgeData> statuses) ExtractCardDetails(
            CardState card,
            FighterState owner,
            CharacterObject character,
            Func<string, StatusPolarity, Sprite> statusSpriteResolver = null)
        {
            var rawDesc = ResolveCardDescription(card, owner, character, out var runtimeEffect);
            var damageKeyword = runtimeEffect?.KeywordId;

            var statuses = new List<CombatStatusBadgeData>();
            var seenStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddBadge(string name, bool isBuff, string iconKey = null)
            {
                if (string.IsNullOrWhiteSpace(name)) return;
                var polarity = isBuff ? StatusPolarity.Buff : StatusPolarity.Debuff;
                var visual = StatusVisualData.Get(iconKey, polarity) ?? StatusVisualData.Get(name, polarity);
                // Art can be reused by several mechanics; retain the supplied effect name.
                var cleanName = name.StartsWith("status.", StringComparison.OrdinalIgnoreCase) ? StatusVisualData.GetDisplayName(name, polarity) : name;
                if (cleanName.StartsWith("Increase ", StringComparison.OrdinalIgnoreCase)) cleanName = cleanName.Substring(9);
                if (cleanName.StartsWith("Decrease ", StringComparison.OrdinalIgnoreCase)) cleanName = cleanName.Substring(9);

                var badgeKey = $"{cleanName}:{(isBuff ? "b" : "d")}";
                if (seenStatuses.Add(badgeKey))
                {
                    var sprite = visual?.Icon ?? (statusSpriteResolver != null ? statusSpriteResolver(iconKey ?? name, polarity) : StatusVisualData.GetSprite(iconKey ?? name, polarity));
                    statuses.Add(new CombatStatusBadgeData(cleanName, sprite, isBuff));
                }
            }

            AddEffectBadges(runtimeEffect, AddBadge);

            if (!string.IsNullOrEmpty(damageKeyword))
            {
                if (damageKeyword.IndexOf("ignite", StringComparison.OrdinalIgnoreCase) >= 0)
                    AddBadge("Ignite", false, "Status_Ignite");
                else if (damageKeyword.IndexOf("poison", StringComparison.OrdinalIgnoreCase) >= 0)
                    AddBadge("Poison", false, "Status_Poison");
                else if (damageKeyword.IndexOf("bleed", StringComparison.OrdinalIgnoreCase) >= 0)
                    AddBadge("Bleed", false, "Status_Bleed");
                else if (damageKeyword.IndexOf("shock", StringComparison.OrdinalIgnoreCase) >= 0)
                    AddBadge("Shock", false, "Status_Shock");
            }

            if (rawDesc.IndexOf("ignite", StringComparison.OrdinalIgnoreCase) >= 0 && !seenStatuses.Contains("Ignite:d"))
                AddBadge("Ignite", false, "Status_Ignite");
            if (rawDesc.IndexOf("poison", StringComparison.OrdinalIgnoreCase) >= 0 && !seenStatuses.Contains("Poison:d"))
                AddBadge("Poison", false, "Status_Poison");
            if (rawDesc.IndexOf("bleed", StringComparison.OrdinalIgnoreCase) >= 0 && !seenStatuses.Contains("Bleed:d"))
                AddBadge("Bleed", false, "Status_Bleed");
            if (rawDesc.IndexOf("shock", StringComparison.OrdinalIgnoreCase) >= 0 && !seenStatuses.Contains("Shock:d"))
                AddBadge("Shock", false, "Status_Shock");

            var highlightedDesc = HighlightKeywords(rawDesc);
            var keywordsExplanation = BuildKeywordsExplanation(rawDesc, damageKeyword, statuses);

            return (highlightedDesc, keywordsExplanation, statuses);
        }

        /// <summary>
        /// Extracts formatted passive details, including rich text description, keyword glossary,
        /// and status badges.
        /// </summary>
        public static PassiveDetailData ExtractPassiveDetails(
            FighterState owner,
            CharacterObject character = null,
            Func<string, StatusPolarity, Sprite> statusSpriteResolver = null)
        {
            var rawDesc = owner?.Definition?.PassiveSource?.Description;
            if (string.IsNullOrWhiteSpace(rawDesc) && character != null)
                rawDesc = character.PassiveSourceDescription;
            if (string.IsNullOrWhiteSpace(rawDesc))
                rawDesc = string.Empty;

            var charName = character?.FighterName ?? owner?.Definition?.DisplayName ?? "Fighter";
            var title = charName + " Passive";

            string sourceType = "Character passive";
            if (character != null && character.PassiveSourceType != PassiveSourceType.Unknown)
                sourceType = CharacterPassiveMetadata.SourceTypeLabel(character.PassiveSourceType);
            else if (!string.IsNullOrWhiteSpace(owner?.Definition?.PassiveSource?.SourceType))
                sourceType = owner.Definition.PassiveSource.SourceType;

            string restriction = string.Empty;
            if (character != null && character.PassiveRestriction != PassiveRestriction.Unknown)
                restriction = CharacterPassiveMetadata.RestrictionLabel(character.PassiveRestriction);
            else if (!string.IsNullOrWhiteSpace(owner?.Definition?.PassiveSource?.Restriction))
                restriction = owner.Definition.PassiveSource.Restriction;

            var statuses = new List<CombatStatusBadgeData>();
            var seenStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddBadge(string name, bool isBuff, string iconKey = null)
            {
                if (string.IsNullOrWhiteSpace(name)) return;
                var polarity = isBuff ? StatusPolarity.Buff : StatusPolarity.Debuff;
                var visual = StatusVisualData.Get(iconKey, polarity) ?? StatusVisualData.Get(name, polarity);
                // Art can be reused by several mechanics; retain the supplied effect name.
                var cleanName = name.StartsWith("status.", StringComparison.OrdinalIgnoreCase) ? StatusVisualData.GetDisplayName(name, polarity) : name;
                if (cleanName.StartsWith("Increase ", StringComparison.OrdinalIgnoreCase)) cleanName = cleanName.Substring(9);
                if (cleanName.StartsWith("Decrease ", StringComparison.OrdinalIgnoreCase)) cleanName = cleanName.Substring(9);

                var badgeKey = $"{cleanName}:{(isBuff ? "b" : "d")}";
                if (seenStatuses.Add(badgeKey))
                {
                    var sprite = visual?.Icon ?? (statusSpriteResolver != null ? statusSpriteResolver(iconKey ?? name, polarity) : StatusVisualData.GetSprite(iconKey ?? name, polarity));
                    statuses.Add(new CombatStatusBadgeData(cleanName, sprite, isBuff));
                }
            }

            // Extract badges from passive reactions and commands
            if (owner?.Definition?.Passive?.Reactions != null)
            {
                foreach (var reaction in owner.Definition.Passive.Reactions)
                {
                    if (reaction == null) continue;
                    AddPassiveCommandsBadges(reaction.Commands, AddBadge);
                    AddPassiveCommandsBadges(reaction.ElseCommands, AddBadge);
                }
            }

            // Extract badges from passive auras (stat modifiers)
            if (owner?.Definition?.Passive?.Auras != null)
            {
                foreach (var aura in owner.Definition.Passive.Auras)
                {
                    if (aura?.Modifiers == null) continue;
                    foreach (var mod in aura.Modifiers)
                    {
                        if (mod == null) continue;
                        bool isBuff = IsModifierBeneficial(mod, mod.Amount);
                        var target = mod.ResolvedTarget;
                        string statName = target == ModifierTarget.Stat ? mod.ResolvedStat.ToString() : target switch
                        {
                            ModifierTarget.AnyDamageDealt => "Damage dealt",
                            ModifierTarget.AnyDamageReceived => "Damage taken",
                            ModifierTarget.FamilyDamageDealt => DisplayLabel(mod.ResolvedFamily.ToString()) + " damage dealt",
                            ModifierTarget.FamilyDamageReceived => DisplayLabel(mod.ResolvedFamily.ToString()) + " damage taken",
                            ModifierTarget.UltimateDamageDealt => "Ultimate damage dealt",
                            ModifierTarget.FinalDamageReduction => "Damage reduction",
                            ModifierTarget.FlatDamageReduction => "Flat damage reduction",
                            ModifierTarget.StatBundle => "Stats",
                            _ => DisplayLabel(target.ToString())
                        };
                        AddBadge(statName, isBuff, isBuff ? "Status_StatUp" : "Status_StatDown");
                    }
                }
            }

            // Extract badges from keywords in description
            ScanDescriptionForBadges(rawDesc, seenStatuses, AddBadge);

            var highlightedDesc = HighlightKeywords(rawDesc);
            var keywordsExplanation = BuildKeywordsExplanation(rawDesc, null, statuses);

            Sprite passiveIcon = character?.FighterPassive;

            return new PassiveDetailData
            {
                Title = title,
                SourceType = sourceType,
                Restriction = restriction,
                RawDescription = rawDesc,
                FormattedDescription = highlightedDesc,
                Keywords = keywordsExplanation,
                Statuses = statuses,
                Icon = passiveIcon
            };
        }

        private static string ResolveCardDescription(CardState card, FighterState owner, CharacterObject character, out EffectDefinition runtimeEffect)
        {
            runtimeEffect = null;
            if (card == null || owner?.Definition == null) return "Deals damage.";

            if (card.Kind == CardKind.Ultimate)
            {
                var ultTier = owner.Definition.UltimateTiers?.FirstOrDefault(t => t.Tier == card.UltimateTier)
                           ?? owner.Definition.UltimateTiers?.FirstOrDefault();
                runtimeEffect = ultTier?.Effect;
                return ultTier?.Description ?? "Deals massive damage.";
            }

            var skill = owner.Definition.Skills?.FirstOrDefault(s => s.Id == card.SkillId || (s.Slot == 1 ? card.SkillId == null : false))
                     ?? owner.Definition.Skills?.FirstOrDefault();
            var skillRank = skill?.Ranks?.FirstOrDefault(r => r.Rank == card.Rank)
                         ?? skill?.Ranks?.FirstOrDefault();
            runtimeEffect = skillRank?.Effect;
            return skillRank?.Description ?? "Deals damage.";
        }

        private static void AddEffectBadges(EffectDefinition root, Action<string, bool, string> addBadge)
        {
            if (root == null || addBadge == null) return;
            AddEffectBadge(root, addBadge);
            if (root.Sequence == null) return;
            foreach (var step in root.Sequence)
                if (step?.Effect != null) AddEffectBadge(step.Effect, addBadge);
        }

        private static void AddEffectBadge(EffectOperationDefinition effect, Action<string, bool, string> addBadge)
        {
            if (effect == null || effect.Kind != EffectKind.ApplyStatus || effect.StatusRecipe == null) return;
            AddStatusRecipeBadges(effect.StatusRecipe, addBadge);
        }

        private static void AddPassiveCommandsBadges(List<PassiveCommandDefinition> commands, Action<string, bool, string> addBadge)
        {
            if (commands == null) return;
            foreach (var command in commands)
            {
                if (command?.Kind != PassiveCommandKind.ExecuteEffect) continue;
                var operation = command.Effect;
                if (operation?.Kind == CardEffectOperationKind.TransferStats)
                {
                    AddStatusRecipeBadges(operation.StatTransfer?.SourceStatus, addBadge);
                    AddStatusRecipeBadges(operation.StatTransfer?.TargetStatus, addBadge);
                }
                else if (operation?.Kind == CardEffectOperationKind.ApplyStatus)
                {
                    var recipe = operation.Status?.InlineRecipe;
                    if (string.IsNullOrWhiteSpace(recipe?.Id))
                        recipe = StandardEffectDatabase.CreateStatusRecipes()
                            .Find(status => status.Id == operation.Status?.StatusRecipeId);
                    AddStatusRecipeBadges(recipe, addBadge);
                }
            }
        }

        public static void AddStatusRecipeBadges(StatusRecipeDefinition recipe, Action<string, bool, string> addBadge)
        {
            if (string.IsNullOrWhiteSpace(recipe?.Id)) return;
            AddStatusRecipeBadge(recipe, addBadge);
            if (recipe.StanceChildren == null) return;
            foreach (var child in recipe.StanceChildren)
                if (child != null) AddStatusRecipeBadge(child.ToRecipe(), addBadge);
        }

        private static void AddStatusRecipeBadge(StatusRecipeDefinition recipe, Action<string, bool, string> addBadge)
        {
            if (recipe == null || addBadge == null) return;
            var polarity = recipe.Polarity;
            var isBuff = polarity == StatusPolarity.Buff;
            var recipeId = recipe.Id ?? "";
            var lowerId = recipeId.ToLowerInvariant();

            // Some authored stat recipes reuse a status preset name/art. Display the
            // actual modifier instead of describing an unrelated preset mechanic.
            if (recipe.Behavior == StatusBehavior.Stat && recipe.Modifiers != null &&
                recipe.Modifiers.Any(m => m != null && m.ResolvedTarget == ModifierTarget.Stat))
            {
                foreach (var modifier in recipe.Modifiers.Where(m => m != null && m.ResolvedTarget == ModifierTarget.Stat))
                    addBadge((modifier.Amount >= 0 ? "Increase " : "Decrease ") + modifier.ResolvedStat, isBuff, recipeId);
                return;
            }

            var visual = StatusVisualData.Get(recipeId, polarity);
            if (visual != null)
            {
                addBadge(visual.DisplayName, isBuff, recipeId);
                return;
            }

            string iconKey;
            string displayName;

            if (lowerId.Contains("ignite")) { iconKey = "Status_Ignite"; displayName = "Ignite"; }
            else if (lowerId.Contains("poison")) { iconKey = "Status_Poison"; displayName = "Poison"; }
            else if (lowerId.Contains("bleed")) { iconKey = "Status_Bleed"; displayName = "Bleed"; }
            else if (lowerId.Contains("shock")) { iconKey = "Status_Shock"; displayName = "Shock"; }
            else if (lowerId.Contains("freeze")) { iconKey = "Status_Freeze"; displayName = "Freeze"; }
            else if (lowerId.Contains("disable-heal") || lowerId.Contains("disable.healing-card") || lowerId.Contains("disable.healingcard"))
            {
                iconKey = "icon_buff_cc_dis_heal_skill"; displayName = "Disable Healing Card";
            }
            else if (lowerId.Contains("disable-recovery") || lowerId.Contains("disable.recovery"))
            {
                iconKey = "icon_buff_explosion_debuff_add_per"; displayName = "Disable Recovery";
            }
            else if (lowerId.Contains("disable-stance") || lowerId.Contains("disable.stance") || (lowerId.Contains("disable") && lowerId.Contains("stance")))
            {
                iconKey = "icon_buff_cc_dis_pose_skill"; displayName = "Disable Stance";
            }
            else if (lowerId.Contains("stun") || lowerId.Contains("paralyze") || lowerId.Contains("seal") || (recipe.Behavior & StatusBehavior.Disable) != 0)
            {
                iconKey = "Status_Stun";
                displayName = !string.IsNullOrWhiteSpace(recipe.NameKey) ? recipe.NameKey : StatusLibrary.GetDisplayName(recipeId, polarity);
            }
            else if (lowerId.Contains("rejuvenation"))
            {
                iconKey = "icon_buff_heal_dot_heal"; displayName = "Rejuvenation";
            }
            else if (lowerId.Contains("debuffimmunity") || (recipe.DebuffImmunity && !lowerId.Contains("recovery") && !lowerId.Contains("rejuvenation")))
            {
                iconKey = "icon_buff_number_immune_debuff"; displayName = "Debuff Immunity";
            }
            else if (lowerId.Contains("recovery"))
            {
                iconKey = isBuff ? "icon_buff_heal_dot_heal" : "icon_buff_explosion_debuff_add_per";
                displayName = isBuff ? "Increase Recovery" : "Decrease Recovery";
            }
            else if (lowerId.Contains("barrier") || recipe.BarrierCoefficientBp > 0)
            {
                iconKey = "icon_buff_shield"; displayName = "Barrier";
            }
            else if (lowerId.Contains("taunt") || recipe.Taunt)
            {
                iconKey = "icon_buff_provocation"; displayName = "Taunt";
            }
            else if (lowerId.Contains("evade") || recipe.EvadeAttacks)
            {
                iconKey = "icon_buff_evade"; displayName = "Evade";
            }
            else if (lowerId.Contains("extort"))
            {
                iconKey = isBuff ? "icon_buff_extort_buff" : "icon_buff_extort_debuff"; displayName = "Extort";
            }
            else if ((recipe.Behavior & StatusBehavior.Stat) != 0)
            {
                iconKey = isBuff ? "Status_StatUp" : "Status_StatDown";
                displayName = !string.IsNullOrWhiteSpace(recipe.NameKey) ? recipe.NameKey : StatusLibrary.GetDisplayName(recipeId, polarity);
            }
            else
            {
                iconKey = recipeId;
                displayName = !string.IsNullOrWhiteSpace(recipe.NameKey) ? recipe.NameKey : StatusLibrary.GetDisplayName(recipeId, polarity);
            }

            addBadge(displayName, isBuff, iconKey);
        }

        private static void ScanDescriptionForBadges(string text, HashSet<string> seenStatuses, Action<string, bool, string> addBadge)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (text.IndexOf("ignite", StringComparison.OrdinalIgnoreCase) >= 0 && !seenStatuses.Contains("Ignite:d"))
                addBadge("Ignite", false, "Status_Ignite");
            if (text.IndexOf("poison", StringComparison.OrdinalIgnoreCase) >= 0 && !seenStatuses.Contains("Poison:d"))
                addBadge("Poison", false, "Status_Poison");
            if (text.IndexOf("bleed", StringComparison.OrdinalIgnoreCase) >= 0 && !seenStatuses.Contains("Bleed:d"))
                addBadge("Bleed", false, "Status_Bleed");
            if (text.IndexOf("shock", StringComparison.OrdinalIgnoreCase) >= 0 && !seenStatuses.Contains("Shock:d"))
                addBadge("Shock", false, "Status_Shock");
            if (text.IndexOf("barrier", StringComparison.OrdinalIgnoreCase) >= 0 && !seenStatuses.Contains("Barrier:b"))
                addBadge("Barrier", true, "icon_buff_shield");
            if (text.IndexOf("taunt", StringComparison.OrdinalIgnoreCase) >= 0 && !seenStatuses.Contains("Taunt:b"))
                addBadge("Taunt", true, "icon_buff_provocation");
            if (text.IndexOf("evade", StringComparison.OrdinalIgnoreCase) >= 0 && !seenStatuses.Contains("Evade:b"))
                addBadge("Evade", true, "icon_buff_evade");
            if (text.IndexOf("extort", StringComparison.OrdinalIgnoreCase) >= 0 && !seenStatuses.Contains("Extort:b"))
                addBadge("Extort", true, "icon_buff_extort_buff");
        }
    }
}
