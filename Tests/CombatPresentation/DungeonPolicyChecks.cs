using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;

internal static partial class BattlePlaybackChecks
{
    private static void DungeonPolicyChecks()
    {
        for (var phase = 1; phase <= 9; phase++)
            for (var sub = 0; sub <= 4; sub++)
            {
                var profile = DungeonProfile.Filtered("series.kof", phase, sub);
                var fighter = new CharacterDefinition { CategoryId = phase * 1000 + sub * 100 + 99, SeriesId = "series.kof" };
                Check(profile.Accepts(fighter), "Phase category should match.");
                Check(profile.Clone().Accepts(fighter.Clone()), "Clones lost category filters.");
                fighter.CategoryId += 100;
                Check(!profile.Accepts(fighter), "Other sub-phase leaked into category.");
                fighter.CategoryId -= 100; fighter.SeriesId = "series.other";
                Check(!profile.Accepts(fighter), "Other series leaked into category.");
            }
        var catalog = new List<CharacterDefinition>();
        for (var i = 0; i < 8; i++)
        {
            var fighter = Fighter("filter-" + i);
            fighter.RuntimeReady = true; fighter.SourceId = "source." + i;
            fighter.PassiveId = "passive." + i; fighter.SeriesId = "series.kof";
            fighter.CategoryId = 1001 + i;
            for (var tier = 1; tier < 6; tier++)
                fighter.UltimateTiers.Add(new UltimateTierDefinition { Tier = tier, Effect = fighter.UltimateTiers[0].Effect.Clone() });
            catalog.Add(fighter);
        }
        var other = catalog[0].Clone(); other.Id = "other"; other.CategoryId = 1100; catalog.Add(other);
        var policy = DungeonProfile.Filtered("series.kof", 1, 0);
        string Fingerprint(List<RouteNodeState> nodes) => string.Join("|", nodes.Select(n => n.Id + n.Type +
            string.Join(",", n.EnemyTeamSnapshot.Select(e => e.DefinitionId + ":" + e.FormationSlot + ":" + e.IsReserve))));
        var original = RouteGenerator.Generate(policy, catalog, 0, 7);
        Check(Fingerprint(original) == Fingerprint(RouteGenerator.Generate(policy, catalog, 0, 7)), "Same seed changed route or formation.");
        Check(Fingerprint(original) != Fingerprint(RouteGenerator.Generate(policy, catalog, 0, 8)), "New seed did not change formations.");
        foreach (var node in original.Where(n => n.EnemyTeamSnapshot.Count > 0))
        {
            Check(node.EnemyTeamSnapshot.Count == 4, "Enemy team must have four fighters.");
            Check(node.EnemyTeamSnapshot.Select(e => e.DefinitionId).Distinct().Count() == 4, "Enemy definitions repeat.");
            Check(node.EnemyTeamSnapshot.Count(e => e.IsReserve) == 1, "Enemy reserve missing.");
            Check(node.EnemyTeamSnapshot.All(e => policy.Accepts(e.Definition)), "Enemy category leak.");
        }
        var run = DungeonRunEngine.CreateRun("filter-run", "test", "start", policy, 0, 7, "v1", "hash",
            new List<RunFighterSeed>(), catalog, deferFormation: true);
        Check(run.Roster.Count == 0 && run.Status == RunStatus.InProgress && run.CurrentNodeId == "r0n0", "Map must open before formation or battle.");
        run.Formation[0] = "owned-test";
        var saved = SnapshotJson.Serialize(run);
        var loaded = SnapshotJson.Deserialize<RunState>(saved);
        Check(loaded.Seed == run.Seed && loaded.Formation[0] == "owned-test", "Run seed or formation lost on save.");
        Check(Fingerprint(loaded.Nodes) == Fingerprint(run.Nodes), "Saved enemy formations changed on load.");
        Check(SnapshotJson.Serialize(loaded) == saved, "Snapshot serialization is not stable.");
        var nested = new EffectConditionDefinition { Tag = "root" };
        var cursor = nested;
        for (var depth = 0; depth < 20; depth++)
        {
            var child = new EffectConditionDefinition { Tag = "depth-" + depth };
            cursor.Children.Add(child); cursor = child;
        }
        var loadedCondition = SnapshotJson.Deserialize<EffectConditionDefinition>(SnapshotJson.Serialize(nested));
        for (var depth = 0; depth < 20; depth++)
        {
            Check(loadedCondition.Children.Count == 1, "Nested condition truncated at " + depth);
            loadedCondition = loadedCondition.Children[0];
            Check(loadedCondition.Tag == "depth-" + depth, "Nested condition data changed.");
        }
        var legacy = SnapshotJson.Deserialize<RunState>("{\"RunId\":\"legacy\",\"Seed\":7,\"Status\":0,\"Roster\":[],\"Nodes\":[]}");
        Check(legacy.RunId == "legacy" && legacy.Seed == 7, "Legacy Unity field JSON cannot be loaded.");
        var clone = run.Clone(); clone.Formation[0] = "other";
        Check(run.Formation[0] == "owned-test", "Run formation clone aliases the save.");
        var rejected = false;
        try { RouteGenerator.Generate(DungeonProfile.Filtered("series.kof", 9, 4), catalog, 0, 1); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Empty category must not fall back to the full catalog.");
    }
}
