using System;
using System.Collections.Generic;

namespace FightingAllstar.Presentation.Navigation
{
    /// <summary>Immutable display metadata for one already committed draw; never grants inventory.</summary>
    public sealed class SummonDrawPresentation
    {
        public CharacterObject Character { get; }
        public bool IsNew { get; }
        public bool IsDuplicate => !IsNew;
        /// <summary>Actual post-C5 token count when progression is known; zero also covers unknown payout.</summary>
        public int FragmentCount { get; }
        /// <summary>The resulting constellation tier, or -1 when the caller supplied no progression context.</summary>
        public int ConstellationTier { get; }

        public SummonDrawPresentation(CharacterObject character, bool isNew, int fragmentCount = 1, int constellationTier = -1)
        {
            Character = character != null ? character : throw new ArgumentNullException(nameof(character));
            IsNew = isNew;
            FragmentCount = isNew ? 0 : Math.Max(0, fragmentCount);
            ConstellationTier = constellationTier < 0 ? -1 : Math.Min(5, constellationTier);
        }

        /// <summary>Classifies identities only. Duplicate payout is left unknown rather than inventing tokens.</summary>
        public static List<SummonDrawPresentation> Classify(IReadOnlyList<CharacterObject> draws, IEnumerable<string> ownedDefinitionIds)
            => Classify(draws, ownedDefinitionIds, null);

        /// <summary>Replays display-only progression in receipt order using a detached pre-purchase snapshot.</summary>
        public static List<SummonDrawPresentation> Classify(IReadOnlyList<CharacterObject> draws,
            IEnumerable<string> ownedDefinitionIds, IReadOnlyDictionary<string, int> ownedConstellationTiers)
        {
            var owned = new HashSet<string>(StringComparer.Ordinal);
            if (ownedDefinitionIds != null)
                foreach (var definitionId in ownedDefinitionIds)
                    if (!string.IsNullOrWhiteSpace(definitionId)) owned.Add(definitionId);
            var tiers = new Dictionary<string, int>(StringComparer.Ordinal);
            if (ownedConstellationTiers != null)
                foreach (var entry in ownedConstellationTiers)
                    if (!string.IsNullOrWhiteSpace(entry.Key)) tiers[entry.Key] = Math.Max(0, Math.Min(5, entry.Value));

            var result = new List<SummonDrawPresentation>();
            if (draws == null) return result;
            foreach (var character in draws)
            {
                if (character == null) continue;
                var id = character.DefinitionId;
                var isNew = string.IsNullOrWhiteSpace(id) || owned.Add(id);
                if (isNew)
                {
                    if (ownedConstellationTiers != null && !string.IsNullOrWhiteSpace(id)) tiers[id] = 0;
                    result.Add(new SummonDrawPresentation(character, true, 0, 0));
                }
                else if (ownedConstellationTiers != null && tiers.TryGetValue(id, out var tier))
                {
                    var tokens = tier >= 5 ? 1 : 0;
                    tier = Math.Min(5, tier + 1);
                    tiers[id] = tier;
                    result.Add(new SummonDrawPresentation(character, false, tokens, tier));
                }
                else result.Add(new SummonDrawPresentation(character, false, 0));
            }
            return result;
        }
    }
}
