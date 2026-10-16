using System;
using System.Collections.Generic;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Run
{
    public enum DungeonEnemyKind { Fighter, Minion, EliteMinion }

    /// <summary>Starting balance values. Enemy equipment and levels are frozen with the route.</summary>
    public static class DungeonEnemyFactory
    {
        private static readonly int[] StartingLevels = { 1, 15, 35, 60, 90 };
        private static readonly int[] EquipmentCounts = { 0, 2, 4, 5, 5 };
        private static readonly int[] EquipmentLevels = { 0, 0, 6, 12, 20 };

        public static EncounterFighterSnapshot Create(CharacterDefinition source, RouteNodeState node,
            DungeonProfile profile, int difficulty, int slot, int constellation, DeterministicRandom rng)
        {
            var tier = DungeonDifficulty.Level(difficulty);
            var kind = DungeonEnemyKind.Fighter;
            var progress = Math.Min(100, node.Row * profile.DifficultyIncreasePerRowPercent +
                (node.Type == RouteNodeType.Elite ? profile.EliteDifficultyBonusPercent :
                 node.Type == RouteNodeType.Boss ? profile.BossDifficultyBonusPercent : 0));
            var level = tier == 1 ? 1 : Math.Min(CharacterProgression.MaxLevel, StartingLevels[tier - 1] + progress / 5);
            var definition = source.Clone();
            definition.ArtifactLoadout.Clear();
            var id = node.Id + ":enemy:" + slot + ":" + source.Id;
            var equipmentCount = EquipmentCounts[tier - 1];
            // Easy non-boss enemies used to skip this roll as minions. Preserve route RNG
            // sequence for those nodes while using normal level-one fighter data.
            if (equipmentCount > 0 || node.Type == RouteNodeType.Boss)
            {
                var set = (ArtifactSet)rng.Next(3);
                for (var i = 0; i < equipmentCount; i++)
                {
                    var artifact = ArtifactRules.Generate(id + ":artifact:" + i, (ArtifactRarity)tier, rng.Next(int.MaxValue));
                    artifact.Set = set; artifact.Slot = (ArtifactSlot)i;
                    artifact.ItemId = ArtifactRules.ItemId(set, artifact.Slot);
                    artifact.Level = Math.Min(ArtifactRules.MaxLevel, EquipmentLevels[tier - 1] + progress / 10);
                    artifact.Substats.Clear(); // Normalize recreates all earned milestone rolls at this level.
                    ArtifactRules.Normalize(artifact);
                    definition.ArtifactLoadout.Add(artifact);
                }
            }
            var stats = CharacterProgression.StatsAtLevel(definition.BaseStats, definition.StatGrowth, level);
            stats = ArtifactRules.ApplyStats(stats, definition.ArtifactLoadout);
            return new EncounterFighterSnapshot { FighterId = id, DefinitionId = source.Id, Definition = definition,
                Stats = stats, CurrentHealth = stats.MaxHealth, Level = level, EnemyKind = kind,
                ConstellationTier = constellation, FormationSlot = Math.Min(slot, 2), IsReserve = slot == 3 };
        }

        public static long TeamCombatClass(IEnumerable<EncounterFighterSnapshot> team)
        { long total = 0; if (team != null) foreach (var fighter in team) total += Math.Max(0, fighter?.Stats?.CombatClass ?? 0); return total; }

        /// <summary>Conservative recommendation across several deterministic route samples, including bosses.</summary>
        public static long RecommendedCombatClass(DungeonProfile profile, IReadOnlyList<CharacterDefinition> catalog, int difficulty)
        {
            long maximum = 0;
            for (ulong seed = 1; seed <= 3; seed++)
                foreach (var node in RouteGenerator.Generate(profile, catalog, difficulty, seed))
                    maximum = Math.Max(maximum, TeamCombatClass(node.EnemyTeamSnapshot));
            return maximum;
        }
    }
}
