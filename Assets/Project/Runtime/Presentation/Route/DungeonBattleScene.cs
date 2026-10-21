using System;
using FightingAllstar.Core.Run;

namespace FightingAllstar.Presentation.Route
{
    /// <summary>Scene selection follows the dungeon policy, never the selected fighter.</summary>
    public static class DungeonBattleScene
    {
        public const string Default = "Battle";
        public const string Teyvat = "BattleTeyvat";

        public static string ForProfile(DungeonProfile profile) =>
            profile != null && string.Equals(profile.SeriesId, "series.genshin-impact", StringComparison.OrdinalIgnoreCase)
                ? Teyvat : Default;
    }
}
