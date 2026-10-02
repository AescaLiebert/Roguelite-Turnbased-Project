using System;
using System.Collections.Generic;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Run
{
    public static class RouteGenerator
    {
        public const int CurrentGeneratorVersion = 1;
        private const int RequiredRows = 9;

        public static List<RouteNodeState> Generate(DungeonProfile profile, IReadOnlyList<CharacterDefinition> catalog,
            int difficultyBonusPercent, ulong seed)
        {
            ValidateProfile(profile, catalog, difficultyBonusPercent);
            var rng = new DeterministicRandom(seed);
            var nodes = new List<RouteNodeState>();
            foreach (var row in profile.Rows)
            {
                for (var column = 0; column < row.Choices.Length; column++)
                {
                    var node = new RouteNodeState { Id = "r" + row.Row + "n" + column, Row = row.Row, Column = column,
                        Type = row.Choices[column], Progress = RouteNodeProgress.Locked };
                    if (node.Type == RouteNodeType.Battle || node.Type == RouteNodeType.Elite || node.Type == RouteNodeType.Boss)
                        node.EnemyTeamSnapshot = GenerateEnemyTeam(node, profile, catalog, difficultyBonusPercent, rng);
                    if (node.Type == RouteNodeType.Boon || node.Type == RouteNodeType.Elite)
                        node.BoonOfferIds = DrawBoonOffers(profile, rng, profile.BoonIds.Count);
                    nodes.Add(node);
                }
            }

            foreach (var node in nodes)
                if (node.Row + 1 < RequiredRows)
                    foreach (var successor in nodes)
                        if (successor.Row == node.Row + 1) node.OutgoingNodeIds.Add(successor.Id);

            if (!ValidateGraph(nodes, out var graphError)) throw new InvalidOperationException(graphError);
            var start = nodes.Find(node => node.Row == 0 && node.Type == RouteNodeType.Start);
            start.Progress = RouteNodeProgress.Completed;
            foreach (var id in start.OutgoingNodeIds) nodes.Find(node => node.Id == id).Progress = RouteNodeProgress.Reachable;
            return nodes;
        }

        public static bool ValidateGraph(IReadOnlyList<RouteNodeState> nodes, out string error)
        {
            error = null;
            if (nodes == null || nodes.Count == 0) { error = "Route has no nodes."; return false; }
            var byId = new Dictionary<string, RouteNodeState>(StringComparer.Ordinal);
            var rowCounts = new int[RequiredRows];
            foreach (var node in nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.Id) || byId.ContainsKey(node.Id)) { error = "Route contains a missing or duplicate node id."; return false; }
                if (node.Row < 0 || node.Row >= RequiredRows) { error = "Route node is outside the nine-row floor."; return false; }
                byId.Add(node.Id, node);
                rowCounts[node.Row]++;
            }
            for (var row = 0; row < rowCounts.Length; row++)
                if (rowCounts[row] == 0) { error = "Route has an empty row " + row + "."; return false; }
            var starts = 0;
            var bosses = 0;
            var rests = 0;
            foreach (var node in nodes)
            {
                if (node.Row == 0 && node.Type == RouteNodeType.Start) starts++;
                if (node.Row == RequiredRows - 1 && node.Type == RouteNodeType.Boss) bosses++;
                if (node.Type == RouteNodeType.Rest) rests++;
                foreach (var id in node.OutgoingNodeIds)
                {
                    if (!byId.TryGetValue(id, out var next) || next.Row != node.Row + 1)
                    { error = "Route edges must point to an existing node in the next row."; return false; }
                }
                if (node.Row < RequiredRows - 1 && node.OutgoingNodeIds.Count == 0)
                { error = "Every non-final route node must lead to the next row."; return false; }
                if (new HashSet<string>(node.OutgoingNodeIds, StringComparer.Ordinal).Count != node.OutgoingNodeIds.Count)
                { error = "Route node contains a duplicate outgoing edge."; return false; }
            }
            if (starts != 1 || bosses < 1 || rests < 1) { error = "Route needs one start, a boss, and at least one Rest node."; return false; }
            if (!CanReach(nodes, byId, startsWithRest: true)) { error = "Route has no Start→Rest→Boss path."; return false; }
            if (!CanReach(nodes, byId, startsWithRest: false)) { error = "Route has no Start→Boss path."; return false; }
            return true;
        }

        private static void ValidateProfile(DungeonProfile profile, IReadOnlyList<CharacterDefinition> catalog, int difficulty)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.Id)) throw new ArgumentException("A stable dungeon profile id is required.");
            if (profile.GeneratorVersion != CurrentGeneratorVersion) throw new InvalidOperationException("Unsupported route generator version.");
            if (difficulty < 0 || difficulty > 100 || difficulty % 5 != 0) throw new ArgumentOutOfRangeException(nameof(difficulty), "Difficulty must be 0-100 in five-point steps.");
            if (profile.Rows == null || profile.Rows.Count != RequiredRows) throw new InvalidOperationException("A floor must declare exactly nine rows.");
            for (var row = 0; row < RequiredRows; row++)
                if (profile.Rows[row] == null || profile.Rows[row].Row != row || profile.Rows[row].Choices == null || profile.Rows[row].Choices.Length == 0)
                    throw new InvalidOperationException("Floor rows must be ordered, nonempty, and numbered 0-8.");
            if (profile.Rows[0].Choices.Length != 1 || profile.Rows[0].Choices[0] != RouteNodeType.Start)
                throw new InvalidOperationException("First route row must contain one Start node.");
            if (profile.Rows[8].Choices.Length == 0) throw new InvalidOperationException("Final route row must contain a Boss.");
            foreach (var type in profile.Rows[8].Choices) if (type != RouteNodeType.Boss) throw new InvalidOperationException("Final route row only accepts Boss nodes.");
            if (profile.EnemyConstellationTier < 0 || profile.EnemyConstellationTier > 6) throw new InvalidOperationException("Enemy constellation tier must be 0-6.");
            if (profile.BoonIds == null || profile.BoonIds.Count < 3 || profile.Boons == null || profile.Boons.Count < 3)
                throw new InvalidOperationException("Boon rows require at least three compatible authored boon definitions.");
            var boonIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var boon in profile.Boons)
                if (boon == null || string.IsNullOrWhiteSpace(boon.Id) || !boonIds.Add(boon.Id) || !profile.BoonIds.Contains(boon.Id))
                    throw new InvalidOperationException("Boon definitions need unique stable ids present in the profile offer pool.");
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var catalogErrors = ContentValidator.Validate(new ContentCatalog { Characters = new List<CharacterDefinition>(catalog) });
            if (catalogErrors.Count > 0)
                throw new InvalidOperationException("Dungeon catalog is not runtime-ready: " + string.Join("; ", catalogErrors));
            var candidates = EligibleCandidates(profile, catalog);
            if (candidates.Count < 4) throw new InvalidOperationException("Dungeon profile needs at least four eligible distinct enemy definitions.");
            if (profile.Restrictions == null) throw new InvalidOperationException("Dungeon restrictions must be explicit, even when empty.");
            foreach (var restriction in profile.Restrictions)
                if (CountMatching(candidates, restriction) < restriction.MinimumCount)
                    throw new InvalidOperationException("Enemy catalog cannot satisfy profile restriction: " + restriction.Description);
        }

        private static List<CharacterDefinition> EligibleCandidates(DungeonProfile profile, IReadOnlyList<CharacterDefinition> catalog)
        {
            var candidates = new List<CharacterDefinition>();
            foreach (var character in catalog)
            {
                if (character == null || !character.RuntimeReady || character.BaseStats == null) continue;
                if (profile.EligibleCharacterIds != null && profile.EligibleCharacterIds.Count > 0 && !profile.EligibleCharacterIds.Contains(character.Id)) continue;
                candidates.Add(character);
            }
            return candidates;
        }

        private static List<EncounterFighterSnapshot> GenerateEnemyTeam(RouteNodeState node, DungeonProfile profile,
            IReadOnlyList<CharacterDefinition> catalog, int difficulty, DeterministicRandom rng)
        {
            var pool = EligibleCandidates(profile, catalog);
            var selected = new List<CharacterDefinition>();
            foreach (var restriction in profile.Restrictions)
            {
                var matching = new List<CharacterDefinition>();
                foreach (var candidate in pool) if (Matches(candidate, restriction) && !selected.Contains(candidate)) matching.Add(candidate);
                while (selected.FindAll(character => Matches(character, restriction)).Count < restriction.MinimumCount)
                {
                    var eligible = new List<CharacterDefinition>();
                    foreach (var candidate in matching) if (!selected.Contains(candidate)) eligible.Add(candidate);
                    if (eligible.Count == 0) throw new InvalidOperationException("Could not construct a restricted enemy team: " + restriction.Description);
                    selected.Add(eligible[rng.Next(eligible.Count)]);
                }
            }
            while (selected.Count < 4)
            {
                var available = new List<CharacterDefinition>();
                foreach (var candidate in pool) if (!selected.Contains(candidate)) available.Add(candidate);
                if (available.Count == 0) throw new InvalidOperationException("Enemy team cannot contain four distinct definitions.");
                selected.Add(available[rng.Next(available.Count)]);
            }
            for (var i = selected.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                var swap = selected[i]; selected[i] = selected[j]; selected[j] = swap;
            }

            var result = new List<EncounterFighterSnapshot>(4);
            for (var i = 0; i < 4; i++)
            {
                var definition = selected[i].Clone();
                var stats = definition.BaseStats.Clone();
                stats.Attack = ScaleBasicStat(stats.Attack, difficulty);
                stats.Defense = ScaleBasicStat(stats.Defense, difficulty);
                stats.MaxHealth = ScaleBasicStat(stats.MaxHealth, difficulty);
                stats.CombatClass = ScaleBasicStat(stats.CombatClass, difficulty);
                result.Add(new EncounterFighterSnapshot { FighterId = node.Id + ":enemy:" + i + ":" + definition.Id,
                    DefinitionId = definition.Id, Definition = definition, Stats = stats, CurrentHealth = stats.MaxHealth,
                    ConstellationTier = profile.EnemyConstellationTier, FormationSlot = Math.Min(i, 2), IsReserve = i == 3 });
            }
            return result;
        }

        private static List<string> DrawBoonOffers(DungeonProfile profile, DeterministicRandom rng, int count)
        {
            var pool = new List<string>(profile.BoonIds);
            var offers = new List<string>(count);
            while (offers.Count < count)
            {
                var index = rng.Next(pool.Count);
                offers.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return offers;
        }

        private static bool CanReach(IReadOnlyList<RouteNodeState> nodes, Dictionary<string, RouteNodeState> byId, bool startsWithRest)
        {
            var paths = new Queue<Tuple<string, bool>>();
            foreach (var node in nodes) if (node.Row == 0 && node.Type == RouteNodeType.Start) paths.Enqueue(Tuple.Create(node.Id, false));
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (paths.Count > 0)
            {
                var path = paths.Dequeue();
                if (!visited.Add(path.Item1 + ":" + path.Item2)) continue;
                var node = byId[path.Item1];
                var hasRest = path.Item2 || node.Type == RouteNodeType.Rest;
                if (node.Type == RouteNodeType.Boss && (!startsWithRest || hasRest)) return true;
                foreach (var id in node.OutgoingNodeIds) paths.Enqueue(Tuple.Create(id, hasRest));
            }
            return false;
        }

        private static int CountMatching(List<CharacterDefinition> characters, RosterRestriction restriction)
        {
            var count = 0;
            foreach (var character in characters) if (Matches(character, restriction)) count++;
            return count;
        }

        private static bool Matches(CharacterDefinition character, RosterRestriction restriction)
        {
            if (restriction == null) return false;
            if (!string.IsNullOrEmpty(restriction.AttributeId) && character.AttributeId != restriction.AttributeId) return false;
            if (!string.IsNullOrEmpty(restriction.TraitId) && (character.TraitIds == null || !character.TraitIds.Contains(restriction.TraitId))) return false;
            return true;
        }

        private static int ScaleBasicStat(int value, int difficulty) => (int)((long)Math.Max(0, value) * (100 + difficulty) / 100);
    }
}
