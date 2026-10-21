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
        public const string ConditionalGray = "#A0A0A0"; // Conditional clauses
    }

    /// <summary>
    /// Status badge model for skill cards, passives, and character inspections.
    /// </summary>
    public struct CombatStatusBadgeData
    {
        public string Name;
        public Sprite Icon;
        public bool IsBuff;
        public StatusPolarity Polarity;

        public CombatStatusBadgeData(string name, Sprite icon, bool isBuff)
        {
            Name = name;
            Icon = icon;
            IsBuff = isBuff;
            Polarity = isBuff ? StatusPolarity.Buff : StatusPolarity.Debuff;
        }
        public CombatStatusBadgeData(string name, Sprite icon, StatusPolarity polarity)
        {
            Name = name; Icon = icon; Polarity = polarity; IsBuff = polarity != StatusPolarity.Debuff;
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
        public List<PassiveGate> Availability = new List<PassiveGate>();
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
                case ModifierTarget.Stat: label = CombatTextCatalog.StatName(modifier.ResolvedStat); break;
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
        public static readonly Dictionary<string, string> KeywordExplanations = CombatTextCatalog.Terms
            .ToDictionary(term => term.Name, term => term.Explanation, StringComparer.OrdinalIgnoreCase);

        public static string HighlightKeywords(string text) => CombatTextCatalog.Highlight(text);

        public static string BuildKeywordsExplanation(string desc, string damageKeyword = null, IEnumerable<CombatStatusBadgeData> statuses = null)
        {
            var text = (desc ?? string.Empty) + " " + CombatTextCatalog.CanonicalKeyword(damageKeyword);
            if (statuses != null) text += " " + string.Join(" ", statuses.Select(s => s.Name));
            var terms = CombatTextCatalog.FindTerms(text).ToArray();
            return terms.Length == 0 ? string.Empty : "<color=" + CombatColors.KeywordCyan + ">" +
                ColorElementNames(string.Join("\n", terms.Select(term => "\u203b" + term.Name + ": " + term.Explanation))) + "</color>";
        }

        private static string ColorElementNames(string text)
        {
            foreach (ElementId element in Enum.GetValues(typeof(ElementId)))
            {
                if (element == ElementId.None) continue;
                var color = ColorUtility.ToHtmlStringRGB(GenshinCombatTextStyle.ElementColor(element));
                text = Regex.Replace(text, @"\b" + Regex.Escape(element.ToString()) + @"\b",
                    match => "<color=#" + color + ">" + match.Value + "</color>",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
            return text;
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
            var rawDesc = ResolveCardDescription(card, owner, character, out var runtimeEffect, out var wording);
            rawDesc = CardDescriptionText.Format(rawDesc, runtimeEffect, wording, card?.Kind == CardKind.Ultimate ? card.UltimateTier : owner?.ConstellationTier ?? 0);
            var damageKeyword = runtimeEffect?.KeywordId;

            var statuses = new List<CombatStatusBadgeData>();
            var seenStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddBadge(string name, StatusPolarity polarity, string iconKey = null)
            {
                if (string.IsNullOrWhiteSpace(name)) return;
                var isBuff = polarity != StatusPolarity.Debuff;
                var visual = (string.IsNullOrWhiteSpace(iconKey) ? null : StatusVisualData.Get(iconKey, polarity)) ?? StatusVisualData.Get(name, polarity);
                // Art can be reused by several mechanics; retain the supplied effect name.
                var cleanName = name.StartsWith("status.", StringComparison.OrdinalIgnoreCase) ? StatusVisualData.GetDisplayName(name, polarity) : name;
                cleanName = CombatTextCatalog.Normalize(cleanName);

                var badgeKey = $"{cleanName}:{(isBuff ? "b" : "d")}";
                if (seenStatuses.Add(badgeKey))
                {
                    var sprite = visual?.Icon ?? (statusSpriteResolver != null ? statusSpriteResolver(iconKey ?? name, polarity) : StatusVisualData.GetSprite(iconKey ?? name, polarity));
                    statuses.Add(new CombatStatusBadgeData(cleanName, sprite, polarity));
                }
            }

            AddEffectBadges(runtimeEffect, AddBadge);
            AddCardStatusVisualBadges(card, owner, character, AddBadge);


            var highlightedDesc = wording == null ? HighlightKeywords(rawDesc) : rawDesc;
            var keywordsExplanation = wording?.AutomaticKeywords == false ? wording.KeywordNotes ?? "" : BuildKeywordsExplanation(rawDesc, damageKeyword, statuses);

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
            if (!string.IsNullOrWhiteSpace(owner?.Definition?.PassiveSource?.SourceType))
                sourceType = owner.Definition.PassiveSource.SourceType;
            else if (character != null && character.PassiveSourceType != PassiveSourceType.Unknown)
                sourceType = CharacterPassiveMetadata.SourceTypeLabel(character.PassiveSourceType);

            string restriction = string.Empty;
            if (!string.IsNullOrWhiteSpace(owner?.Definition?.PassiveSource?.Restriction))
                restriction = owner.Definition.PassiveSource.Restriction;
            else if (character != null && character.PassiveRestriction != PassiveRestriction.Unknown)
                restriction = CharacterPassiveMetadata.RestrictionLabel(character.PassiveRestriction);

            var statuses = new List<CombatStatusBadgeData>();
            var seenStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddBadge(string name, StatusPolarity polarity, string iconKey = null)
            {
                if (string.IsNullOrWhiteSpace(name)) return;
                var isBuff = polarity != StatusPolarity.Debuff;
                var visual = (string.IsNullOrWhiteSpace(iconKey) ? null : StatusVisualData.Get(iconKey, polarity)) ?? StatusVisualData.Get(name, polarity);
                // Art can be reused by several mechanics; retain the supplied effect name.
                var cleanName = name.StartsWith("status.", StringComparison.OrdinalIgnoreCase) ? StatusVisualData.GetDisplayName(name, polarity) : name;
                cleanName = CombatTextCatalog.Normalize(cleanName);

                var badgeKey = $"{cleanName}:{(isBuff ? "b" : "d")}";
                if (seenStatuses.Add(badgeKey))
                {
                    var sprite = visual?.Icon ?? (statusSpriteResolver != null ? statusSpriteResolver(iconKey ?? name, polarity) : StatusVisualData.GetSprite(iconKey ?? name, polarity));
                    statuses.Add(new CombatStatusBadgeData(cleanName, sprite, polarity));
                }
            }

            // Extract badges from passive reactions and commands
            if (owner?.Definition?.Passive?.Reactions != null)
            {
                foreach (var reaction in owner.Definition.Passive.Reactions)
                {
                    if (reaction == null) continue;
                    AddPassiveCommandsBadges(reaction.Commands, AddBadge, hideStatChanges: true);
                    AddPassiveCommandsBadges(reaction.ElseCommands, AddBadge, hideStatChanges: true);
                }
            }

            // Aura contributions are passive rules, not applied status effects.
            // Their description, passive portrait and restriction icons carry this information.
            var wording = owner?.Definition?.PassiveSource?.Wording;
            rawDesc = PassiveDescriptionText.Format(rawDesc, owner?.Definition?.Passive, wording, owner?.ConstellationTier ?? 0);

            var highlightedDesc = wording == null ? HighlightKeywords(rawDesc) : rawDesc;
            var keywordsExplanation = wording?.AutomaticKeywords == false ? wording.KeywordNotes ?? "" : BuildKeywordsExplanation(rawDesc, null, statuses);

            Sprite passiveIcon = character?.FighterPassive;
            var passive = owner?.Definition?.Passive;
            var availability = new List<PassiveGate>();
            if (passive != null)
            {
                availability.AddRange(passive.Auras.Select(a => a.Gate).Concat(passive.Reactions.Select(r => r.Gate)).Concat(passive.Policies.Select(p => p.Gate)).Where(g => g != null).Select(g => g.Clone()));
                if (passive.Support?.IsConfigured == true)
                {
                    availability.Add(new PassiveGate { Presence = passive.Support.Presence });
                    if (passive.Support.SwirlStrengthBp != 0) availability.Add(new PassiveGate { Presence = passive.Support.SwirlPresence });
                }
            }

            return new PassiveDetailData
            {
                Title = title,
                SourceType = sourceType,
                Restriction = restriction,
                RawDescription = rawDesc,
                FormattedDescription = highlightedDesc,
                Keywords = keywordsExplanation,
                Statuses = statuses,
                Icon = passiveIcon,
                Availability = availability
            };
        }

        private static string ResolveCardDescription(CardState card, FighterState owner, CharacterObject character, out EffectDefinition runtimeEffect, out AuthoredDescription wording)
        {
            runtimeEffect = null;
            wording = null;
            if (card == null || owner?.Definition == null) return "No effect description available.";

            if (card.Kind == CardKind.Ultimate)
            {
                var ultTier = owner.Definition.UltimateTiers?.FirstOrDefault(t => t.Tier == card.UltimateTier)
                           ?? owner.Definition.UltimateTiers?.FirstOrDefault();
                runtimeEffect = ultTier?.Effect;
                wording = ultTier?.Wording;
                return ultTier?.Description ?? string.Empty;
            }

            var skill = owner.Definition.Skills?.FirstOrDefault(s => s.Id == card.SkillId || (s.Slot == 1 ? card.SkillId == null : false))
                     ?? owner.Definition.Skills?.FirstOrDefault();
            var skillRank = skill?.Ranks?.FirstOrDefault(r => r.Rank == card.Rank)
                         ?? skill?.Ranks?.FirstOrDefault();
            runtimeEffect = skillRank?.Effect;
            if (skill != null && skillRank != null && CardRules.TryGetSkill(owner.Definition, skill.Id, skillRank.Rank, out var sharedEffect))
                runtimeEffect = sharedEffect;
            wording = skillRank?.Wording;
            return skillRank?.Description ?? string.Empty;
        }

        private static void AddEffectBadges(EffectDefinition root, Action<string, StatusPolarity, string> addBadge)
        {
            if (root == null || addBadge == null) return;
            AddEffectBadge(root, addBadge);
            if (root.Sequence == null) return;
            foreach (var step in root.Sequence)
                if (step?.Effect != null) AddEffectBadge(step.Effect, addBadge);
        }

        private static void AddCardStatusVisualBadges(CardState card, FighterState owner, CharacterObject character,
            Action<string, StatusPolarity, string> addBadge)
        {
            if (card == null || character == null || addBadge == null) return;

            // Some effects are executed by a passive reaction tied to a card (for
            // example Ivy's Ultimate stat transfer), so they are not represented in
            // the card's root/sequence EffectDefinition. The card's own visual list is
            // the authored source of truth for those status badges.
            List<StatusVisualData> visuals = null;
            if (card.Kind == CardKind.Ultimate)
            {
                visuals = character.Ultimate?.statusVisuals;
            }
            else
            {
                var slot = owner?.Definition?.Skills?.FirstOrDefault(skill => skill != null && skill.Id == card.SkillId)?.Slot;
                var skillCard = slot == 2 ? character.Skill2 : character.Skill1;
                visuals = skillCard?.statusVisuals;
            }

            if (visuals == null) return;
            foreach (var visual in visuals)
                if (visual != null)
                    addBadge(visual.DisplayName, visual.Polarity, visual.Id);
        }

        private static void AddEffectBadge(EffectOperationDefinition effect, Action<string, StatusPolarity, string> addBadge)
        {
            if (effect == null || effect.Kind != EffectKind.ApplyStatus || effect.StatusRecipe == null) return;
            AddStatusRecipeBadges(effect.StatusRecipe, addBadge, effect.StatusPotencyBp != 0 ? (int?)effect.StatusPotencyBp : null);
        }

        private static void AddPassiveCommandsBadges(List<PassiveCommandDefinition> commands, Action<string, StatusPolarity, string> addBadge, bool hideStatChanges = false)
        {
            if (commands == null) return;
            foreach (var command in commands)
            {
                if (command?.Kind != PassiveCommandKind.ExecuteEffect) continue;
                var operation = command.Effect;
                if (!hideStatChanges && operation?.Kind == CardEffectOperationKind.TransferStats)
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
                    if (!hideStatChanges || !IsPlainStatRecipe(recipe)) AddStatusRecipeBadges(recipe, addBadge);
                }
            }
        }

        public static void AddStatusRecipeBadges(StatusRecipeDefinition recipe, Action<string, StatusPolarity, string> addBadge, int? potencyBp = null)
        {
            if (string.IsNullOrWhiteSpace(recipe?.Id)) return;
            AddStatusRecipeBadge(recipe, addBadge, potencyBp);
            if (recipe.StanceChildren == null) return;
            foreach (var child in recipe.StanceChildren)
                if (child != null) AddStatusRecipeBadge(child.ToRecipe(), addBadge);
        }

        private static void AddStatusRecipeBadge(StatusRecipeDefinition recipe, Action<string, StatusPolarity, string> addBadge, int? potencyBp = null)
        {
            if (recipe == null || addBadge == null) return;
            var polarity = recipe.Polarity;
            var isBuff = polarity == StatusPolarity.Buff;
            var recipeId = recipe.Id ?? "";
            var lowerId = recipeId.ToLowerInvariant();

            // Display actual stat changes, even when authors reuse another preset's ID.
            // Select art from the stat and direction, never from an unrelated recipe name.
            if (IsPlainStatRecipe(recipe))
            {
                foreach (var modifier in recipe.Modifiers.Where(m => m != null))
                {
                    var amount = modifier.ScaleByStatusPotency ? (long)modifier.PotencyCoefficientBp *
                        (potencyBp ?? recipe.DefaultPotencyBp) / 10000 : modifier.Amount;
                    if (modifier.ResolvedOperation == ModifierOperation.Multiplier) amount -= 10000;
                    var label = modifier.ResolvedTarget == ModifierTarget.Stat ? CombatTextCatalog.StatName(modifier.ResolvedStat) :
                        modifier.ResolvedTarget == ModifierTarget.StatBundle ? BundleName(modifier.ResolvedBundle) :
                        DescribeModifier(modifier, 0).Replace(" 0%", "").Replace(" 0", "");
                    var direction = amount >= 0 ? "Increase " : "Decrease ";
                    var name = direction + label;
                    if (modifier.ResolvedTarget == ModifierTarget.FlatDamageReduction || modifier.ResolvedTarget == ModifierTarget.FinalDamageReduction)
                        name = amount >= 0 ? "Reduce Damage Taken" : "Increase Damage Taken";
                    var iconId = ModifierIconId(modifier, amount) ??
                        (polarity == StatusPolarity.Buff ? "stat_up" : polarity == StatusPolarity.Debuff ? "stat_down" : "neutral_default");
                    addBadge(name, polarity, iconId);
                }
                return;
            }

            var visual = StatusVisualData.Get(recipeId, polarity);
            if (visual != null)
            {
                addBadge(visual.DisplayName, polarity, recipeId);
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

            addBadge(displayName, polarity, iconKey);
        }

        private static bool IsPlainStatRecipe(StatusRecipeDefinition recipe) => recipe != null &&
            recipe.Behavior == StatusBehavior.Stat && recipe.Modifiers != null && recipe.Modifiers.Count > 0 &&
            recipe.Tags?.Contains(CombatTags.Ignite) != true &&
            !(recipe.Id ?? "").Equals("status.debuff.ignite", StringComparison.OrdinalIgnoreCase);

        // Artwork IDs are stable asset identities. Player-facing stat names can be
        // normalized or reworded without changing which authored sprite is selected.
        private static string ModifierIconId(StatModifierDefinition modifier, long amount)
        {
            var increase = amount >= 0;
            var prefix = increase ? "status.increase." : "status.decrease.";
            switch (modifier.ResolvedTarget)
            {
                case ModifierTarget.Stat:
                    switch (modifier.ResolvedStat)
                    {
                        case StatId.Attack: return prefix + (increase ? "attack" : "atk");
                        case StatId.Defense: return prefix + (increase ? "DEF" : "defense");
                        case StatId.MaxHealth: return prefix + "hp";
                        case StatId.CritChance: return prefix + "crit-chance";
                        case StatId.CritDamage: return increase ? prefix + "crit-damage" : null;
                        case StatId.BlockChance: return increase ? prefix + "block-chance" : null;
                        case StatId.Pierce: return increase ? prefix + "pierce" : null;
                        case StatId.Resistance: return increase ? null : prefix + "resistence";
                        case StatId.Recovery: return increase ? null : prefix + "recovery";
                        default: return null;
                    }
                case ModifierTarget.StatBundle:
                    switch (modifier.ResolvedBundle)
                    {
                        case StatBundleKind.AttackRelated: return prefix + (increase ? "attack-related" : "atk-related");
                        case StatBundleKind.DefenseRelated: return increase ? null : prefix + "defense-related";
                        case StatBundleKind.HpRelated: return prefix + (increase ? "hp.related" : "hp-related");
                        default: return null;
                    }
                case ModifierTarget.AnyDamageDealt: return increase ? "status.increase.damage-dealt" : null;
                case ModifierTarget.AnyDamageReceived: return increase ? "status.increase.damage-taken" : "status.reduce.damage-taken";
                case ModifierTarget.FlatDamageReduction:
                case ModifierTarget.FinalDamageReduction: return increase ? "status.reduce.damage-taken" : "status.increase.damage-taken";
                default: return null;
            }
        }

        private static string BundleName(StatBundleKind bundle) => bundle switch
        {
            StatBundleKind.BasicStats => "Basic Stats", StatBundleKind.AttackRelated => "Attack-related stats",
            StatBundleKind.DefenseRelated => "Defense-related stats", StatBundleKind.HpRelated => "HP-related stats",
            StatBundleKind.AllStats => "All Stats", _ => "Stats"
        };

    }
}
