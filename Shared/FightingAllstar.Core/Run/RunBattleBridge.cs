using System;
using System.Collections.Generic;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Run
{
    /// <summary>Maps a frozen route encounter to the disposable shared battle engine and back to run HP.</summary>
    public static class RunBattleBridge
    {
        public static BattleState CreateLocalBattle(EncounterProjection encounter, ulong seed, TeamSide firstSide = TeamSide.Player)
        {
            if (encounter == null || string.IsNullOrWhiteSpace(encounter.BattleId)) throw new ArgumentException("Encounter projection is missing.");
            if (encounter.PlayerTeam == null || encounter.PlayerTeam.Count < 1 || encounter.PlayerTeam.Count > 4)
                throw new ArgumentException("Encounter needs one to four living player fighters.");
            if (encounter.EnemyTeam == null || encounter.EnemyTeam.Count < 1 || encounter.EnemyTeam.Count > 4)
                throw new ArgumentException("Encounter needs one to four frozen opposing fighters.");

            var playerDefinitions = new List<CharacterDefinition>();
            var playerTiers = new List<int>();
            foreach (var fighter in encounter.PlayerTeam)
            {
                playerDefinitions.Add(ResolvedDefinition(fighter.Definition, fighter.Stats));
                playerTiers.Add(fighter.ConstellationTier);
            }
            var enemyDefinitions = new List<CharacterDefinition>();
            var enemyTiers = new List<int>();
            foreach (var fighter in encounter.EnemyTeam)
            {
                enemyDefinitions.Add(ResolvedDefinition(fighter.Definition, fighter.Stats));
                enemyTiers.Add(fighter.ConstellationTier);
            }

            var battle = BattleEngine.Create(encounter.BattleId, playerDefinitions, playerTiers,
                enemyDefinitions, enemyTiers, seed, firstSide, encounter.ChosenBoons,
                CurrentHealth(encounter.PlayerTeam), CurrentHealth(encounter.EnemyTeam));
            MapFighterIds(battle, battle.Player, encounter.PlayerTeam);
            MapFighterIds(battle, battle.Opponent, encounter.EnemyTeam);
            CharacterPassiveRuntime.Refresh(battle);
            return battle;
        }

        private static List<int> CurrentHealth(List<EncounterFighterSnapshot> fighters)
        {
            var result = new List<int>(fighters.Count);
            foreach (var fighter in fighters) result.Add(fighter.CurrentHealth);
            return result;
        }

        public static List<BattleFighterResult> BuildRunResults(RunState run, BattleState battle)
        {
            if (run == null || battle == null || battle.MatchId != run.PendingBattleId)
                throw new ArgumentException("Battle state does not belong to the pending run encounter.");
            var results = new List<BattleFighterResult>(run.Roster.Count);
            foreach (var fighter in run.Roster)
            {
                var battleFighter = battle.Player.FindFighter(fighter.RunFighterId);
                // Battle-only MaxHP contributions are not permanent run-stat upgrades.
                var hp = fighter.IsDefeated || battleFighter == null ? 0 : Math.Max(0, Math.Min(fighter.Stats.MaxHealth, battleFighter.Health));
                results.Add(new BattleFighterResult { RunFighterId = fighter.RunFighterId,
                    CurrentHealth = hp, IsDefeated = hp == 0 });
            }
            return results;
        }

        private static CharacterDefinition ResolvedDefinition(CharacterDefinition source, StatBlock stats)
        {
            if (source == null || stats == null) throw new ArgumentException("Encounter fighter definition and stats are required.");
            var resolved = source.Clone();
            resolved.BaseStats = stats.Clone();
            if (resolved.Passive == null || string.IsNullOrWhiteSpace(resolved.Passive.Id))
                resolved.Passive = StandardCharacterPassives.Create(resolved.Id) ?? new PassiveDefinition { Id = "passive.none" };
            return resolved;
        }

        private static void MapFighterIds(BattleState battle, BattleTeamState team, List<EncounterFighterSnapshot> snapshots)
        {
            if (team.Fighters.Count != snapshots.Count) throw new InvalidOperationException("Encounter and battle formation sizes diverged.");
            var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < snapshots.Count; i++)
            {
                var fighter = team.Fighters[i];
                replacements.Add(fighter.Id, snapshots[i].FighterId);
                fighter.Id = snapshots[i].FighterId;
                fighter.FormationSlot = snapshots[i].FormationSlot;
                fighter.IsReserve = snapshots[i].IsReserve;
            }
            foreach (var card in team.Hand)
                if (replacements.TryGetValue(card.OwnerFighterId, out var ownerId)) card.OwnerFighterId = ownerId;
            RemapSources(battle.Player, replacements);
            RemapSources(battle.Opponent, replacements);
            for (var i = 0; i < battle.ActivePassiveAuras.Count; i++)
            {
                var aura = battle.ActivePassiveAuras[i];
                var sep = aura.IndexOf("::", StringComparison.Ordinal);
                if (sep > 0)
                {
                    var oldOwner = aura.Substring(0, sep);
                    var suffix = aura.Substring(sep);
                    if (replacements.TryGetValue(oldOwner, out var newOwner))
                        battle.ActivePassiveAuras[i] = newOwner + suffix;
                }
            }
            foreach (var item in battle.Events)
            {
                if (replacements.TryGetValue(item.SourceId ?? string.Empty, out var sourceId)) item.SourceId = sourceId;
                if (replacements.TryGetValue(item.TargetId ?? string.Empty, out var targetId)) item.TargetId = targetId;
                if (item.Card != null && replacements.TryGetValue(item.Card.OwnerFighterId, out var cardOwnerId))
                    item.Card.OwnerFighterId = cardOwnerId;
                foreach (var contribution in item.PassiveContributions)
                    if (replacements.TryGetValue(contribution.OwnerId, out var passiveOwner)) contribution.OwnerId = passiveOwner;
                if (item.Kind == BattleEventKind.PassiveTriggered && item.Message != null && item.Message.StartsWith("Passive Trigger: "))
                {
                    var key = item.Message.Substring("Passive Trigger: ".Length);
                    var sep = key.IndexOf("::", StringComparison.Ordinal);
                    if (sep > 0)
                    {
                        var oldOwner = key.Substring(0, sep);
                        var suffix = key.Substring(sep);
                        if (replacements.TryGetValue(oldOwner, out var newOwner))
                            item.Message = "Passive Trigger: " + newOwner + suffix;
                    }
                }
            }
        }

        private static void RemapSources(BattleTeamState team, Dictionary<string, string> replacements)
        {
            foreach (var fighter in team.Fighters)
            {
                foreach (var contribution in fighter.PassiveContributions)
                    if (replacements.TryGetValue(contribution.OwnerId, out var owner)) contribution.OwnerId = owner;
                foreach (var status in fighter.Statuses.Instances)
                {
                    if (replacements.TryGetValue(status.SourceFighterId ?? string.Empty, out var source)) status.SourceFighterId = source;
                    if (replacements.TryGetValue(status.TargetFighterId ?? string.Empty, out var target)) status.TargetFighterId = target;
                }
            }
        }
    }
}
