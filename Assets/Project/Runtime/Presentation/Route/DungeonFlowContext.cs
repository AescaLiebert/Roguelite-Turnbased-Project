using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;
using UnityEngine;

namespace FightingAllstar.Presentation.Route
{
    /// <summary>
    /// Holds the session context when navigating from Dungeon Entry/Stage selection
    /// into CharacterLoadOut (Filtered), and then into the Battle scene.
    /// </summary>
    public static class DungeonFlowContext
    {
        public static DungeonProfile ActiveProfile { get; private set; }
        public static RunState ActiveRun { get; private set; }
        public static EncounterProjection PendingEncounter { get; private set; }
        public static int SelectedDifficulty { get; private set; }
        public static string UserId { get; private set; }
        public static ulong Seed { get; private set; }
        public static string ContentVersion { get; private set; }
        public static string ContentHash { get; private set; }
        public static List<CharacterDefinition> Catalog { get; private set; }
        public static int BaseCompletionDiamonds { get; private set; } = 320;
        public static bool IsDungeonLoadoutMode => ActiveProfile != null;

        public static void BeginDungeonFlow(DungeonProfile profile, int difficulty, string userId, ulong seed,
            string contentVersion, string contentHash, IReadOnlyList<CharacterDefinition> catalog, int baseDiamonds)
        {
            ActiveProfile = profile == null ? null : EnemyTemplateLibrarySO.ApplyResourcesTo(profile.Clone());
            ActiveRun = null;
            PendingEncounter = null;
            SelectedDifficulty = Mathf.Clamp(difficulty, 0, 100);
            UserId = userId;
            Seed = seed;
            ContentVersion = contentVersion;
            ContentHash = contentHash;
            BaseCompletionDiamonds = baseDiamonds;

            SetCatalog(catalog);
        }

        public static void SetCatalog(IReadOnlyList<CharacterDefinition> catalog)
        {
            Catalog = new List<CharacterDefinition>();
            if (catalog == null) return;
            foreach (var character in catalog)
                if (character != null) Catalog.Add(character.Clone());
        }

        public static void BeginDungeonFlowForEncounter(DungeonProfile profile, RunState run, EncounterProjection encounter)
        {
            ActiveProfile = profile == null ? (run != null ? GetProfileForId(run.ProfileId) : null) :
                EnemyTemplateLibrarySO.ApplyResourcesTo(profile.Clone());
            ActiveRun = run == null ? null : run.Clone();
            PendingEncounter = encounter;
            if (run != null)
            {
                SelectedDifficulty = run.DifficultyBonusPercent;
                UserId = run.UserId;
                Seed = run.Seed;
                ContentVersion = run.ContentVersion;
                ContentHash = run.ContentHash;
                BaseCompletionDiamonds = run.BaseCompletionDiamonds;
            }
        }

        public static DungeonProfile GetProfileForId(string profileId)
        {
            if (profileId != null && profileId.StartsWith("dungeon.filter:", StringComparison.Ordinal))
            {
                var parts = profileId.Split(':');
                if (parts.Length == 4 && int.TryParse(parts[2], out var phase) && int.TryParse(parts[3], out var subPhase))
                    return EnemyTemplateLibrarySO.ApplyResourcesTo(DungeonProfile.Filtered(parts[1], phase, subPhase));
            }
            if (string.Equals(profileId, "dungeon.green-accord", StringComparison.OrdinalIgnoreCase))
                return EnemyTemplateLibrarySO.ApplyResourcesTo(DungeonProfile.GreenAccord());
            if (string.Equals(profileId, "dungeon.women-exhibition", StringComparison.OrdinalIgnoreCase))
                return EnemyTemplateLibrarySO.ApplyResourcesTo(DungeonProfile.WomenExhibition());
            return EnemyTemplateLibrarySO.ApplyResourcesTo(DungeonProfile.OpenCircuit());
        }

        public static void Clear()
        {
            ActiveProfile = null;
            ActiveRun = null;
            PendingEncounter = null;
            SelectedDifficulty = 0;
            UserId = null;
            Seed = 0;
            ContentVersion = null;
            ContentHash = null;
            Catalog = null;
        }

        /// <summary>
        /// Evaluates whether a character is eligible under the active dungeon profile's policy.
        /// </summary>
        public static bool IsCharacterEligible(DungeonProfile profile, CharacterObject character)
        {
            if (profile == null || character == null) return true;
            if (!profile.Accepts(new CharacterDefinition { Id = character.DefinitionId, CategoryId = character.ID, SeriesId = character.SeriesId })) return false;

            // 1. Explicit ID whitelist filter
            if (profile.EligibleCharacterIds != null && profile.EligibleCharacterIds.Count > 0)
            {
                if (!profile.EligibleCharacterIds.Contains(character.DefinitionId))
                    return false;
            }

            // 2. Profile restrictions (Attributes, Traits)
            if (profile.Restrictions != null && profile.Restrictions.Count > 0)
            {
                foreach (var restriction in profile.Restrictions)
                {
                    if (restriction == null) continue;

                    // Attribute filter (e.g. Green Accord -> "attribute.green")
                    if (!string.IsNullOrEmpty(restriction.AttributeId))
                    {
                        var charAttrId = "attribute." + character.FighterAttribute.ToString().ToLowerInvariant();
                        if (charAttrId != restriction.AttributeId.ToLowerInvariant())
                            return false;
                    }

                    // Trait filter (e.g. Women Exhibition -> "trait.women")
                    if (!string.IsNullOrEmpty(restriction.TraitId))
                    {
                        var hasTrait = false;
                        if (character.TraitIds != null)
                        {
                            foreach (var trait in character.TraitIds)
                            {
                                if (string.Equals(trait, restriction.TraitId, StringComparison.OrdinalIgnoreCase))
                                {
                                    hasTrait = true;
                                    break;
                                }
                            }
                        }
                        if (!hasTrait) return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Checks whether the entire formation satisfies the dungeon policy requirements.
        /// </summary>
        public static bool ValidateFormation(DungeonProfile profile, IReadOnlyList<CharacterObject> formation, out string errorMessage)
        {
            errorMessage = null;
            if (profile == null) return true;

            var livingCount = 0;
            for (var i = 0; i < Math.Min(3, formation.Count); i++)
            {
                if (formation[i] != null) livingCount++;
            }

            if (livingCount == 0)
            {
                errorMessage = "Assign at least one fighter to an active position before entering Battle.";
                return false;
            }

            foreach (var character in formation)
            {
                if (character == null) continue;
                if (!IsCharacterEligible(profile, character))
                {
                    errorMessage = $"{character.FighterName} does not meet the policy for {profile.DisplayName}.";
                    return false;
                }
            }

            if (profile.Restrictions != null)
            {
                foreach (var restriction in profile.Restrictions)
                {
                    if (restriction == null || restriction.MinimumCount <= 0) continue;
                    var matching = 0;
                    foreach (var character in formation)
                    {
                        if (character == null) continue;
                        var matches = true;
                        if (!string.IsNullOrEmpty(restriction.AttributeId))
                        {
                            var charAttrId = "attribute." + character.FighterAttribute.ToString().ToLowerInvariant();
                            if (charAttrId != restriction.AttributeId.ToLowerInvariant()) matches = false;
                        }
                        if (!string.IsNullOrEmpty(restriction.TraitId))
                        {
                            var hasTrait = false;
                            if (character.TraitIds != null)
                            {
                                foreach (var trait in character.TraitIds)
                                {
                                    if (string.Equals(trait, restriction.TraitId, StringComparison.OrdinalIgnoreCase))
                                    {
                                        hasTrait = true;
                                        break;
                                    }
                                }
                            }
                            if (!hasTrait) matches = false;
                        }
                        if (matches) matching++;
                    }

                    if (matching < restriction.MinimumCount)
                    {
                        errorMessage = $"{profile.DisplayName} requires at least {restriction.MinimumCount} eligible fighters ({restriction.Description}).";
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
