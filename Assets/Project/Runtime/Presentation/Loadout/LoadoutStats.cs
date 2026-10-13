using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;
using FightingAllstar.Presentation.Route;

namespace FightingAllstar.Presentation.Loadout
{
    public static class LoadoutStats
    {
        public static StatBlock Resolve(CharacterObject character)
        {
            if (character == null) return null;
            var frozen = DungeonFlowContext.ActiveRun?.Roster.Find(f => f.DefinitionId == character.DefinitionId);
            if (frozen?.Stats != null) return frozen.Stats;
            if (PlayerInventoryService.Instance != null) return PlayerInventoryService.Instance.GetCharacterStats(character);
            var definition = Content.CharacterObjectCatalogBuilder.CreateDefinition(character);
            return CharacterProgression.StatsAtLevel(definition.BaseStats, definition.StatGrowth, 1);
        }

        public static long EnemyCombatClass => DungeonEnemyFactory.TeamCombatClass(
            DungeonFlowContext.ActiveRun?.FindNode(DungeonFlowContext.ActiveRun.CurrentNodeId)?.EnemyTeamSnapshot ??
            DungeonFlowContext.PendingEncounter?.EnemyTeam);

        public static long TeamCombatClass(IEnumerable<CharacterObject> characters)
        { long total = 0; foreach (var character in characters) total += Math.Max(0, Resolve(character)?.CombatClass ?? 0); return total; }

        public static string Comparison(long player, long enemy) => "CC  " + player.ToString("N0") + " / " + enemy.ToString("N0");
    }
}
