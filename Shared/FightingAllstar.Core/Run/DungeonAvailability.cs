using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Run
{
    public static class DungeonAvailability
    {
        public static bool HasEnemyRoster(DungeonProfile profile, IReadOnlyList<CharacterDefinition> catalog)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (catalog != null) foreach (var character in catalog)
                if (character != null && character.RuntimeReady && character.BaseStats != null && profile.Accepts(character))
                    ids.Add(character.Id);
            return ids.Count >= 4;
        }

        public static List<int> Phases(string seriesId, IReadOnlyList<CharacterDefinition> catalog)
        {
            var values = new List<int>();
            for (var phase = 1; phase <= DungeonProfile.PhaseNames.Length; phase++)
                if (HasEnemyRoster(DungeonProfile.Filtered(seriesId, phase, 4), catalog)) values.Add(phase);
            return values;
        }

        public static List<int> SubPhases(string seriesId, int phase, IReadOnlyList<CharacterDefinition> catalog)
        {
            var values = new List<int>();
            for (var sub = 0; sub < DungeonProfile.SubPhaseNames.Length; sub++)
                if (HasEnemyRoster(DungeonProfile.Filtered(seriesId, phase, sub), catalog)) values.Add(sub);
            return values;
        }
    }
}
