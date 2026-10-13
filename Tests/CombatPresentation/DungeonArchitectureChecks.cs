using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;

internal static partial class BattlePlaybackChecks
{
    private static void DungeonArchitectureChecks()
    {
        var catalog = new List<CharacterDefinition>();
        for (var i = 0; i < 8; i++)
        {
            var fighter = Fighter("architecture-" + i, 1000, 100);
            fighter.BaseStats.Defense = 100; fighter.RuntimeReady = true;
            fighter.SourceId = "source." + i; fighter.PassiveId = "passive." + i;
            fighter.SeriesId = "series.kof"; fighter.CategoryId = 1001 + i;
            for (var tier = 1; tier < 6; tier++) fighter.UltimateTiers.Add(new UltimateTierDefinition { Tier = tier, Effect = fighter.UltimateTiers[0].Effect.Clone() });
            catalog.Add(fighter);
        }
        RunFighterSeed Seed(int i, int slot) => new RunFighterSeed { OwnedFighterId = "owned-" + i,
            Definition = catalog[i], ResolvedStats = catalog[i].BaseStats.Clone(), FormationSlot = slot, IsReserve = slot == 3 };
        var profile = DungeonProfile.Filtered("series.kof", 1, 0);
        var run = DungeonRunEngine.CreateRun("architecture", "test", "start", profile, 25, 123, "v", "hash",
            new[] { Seed(0, 0), Seed(1, 1) }, catalog);
        var generated = SnapshotJson.Serialize(run.Generation);
        var otherRoster = DungeonRunEngine.CreateRun("other", "other-user", "start", profile, 25, 123, "v", "hash",
            new[] { Seed(5, 0) }, catalog);
        Check(generated == SnapshotJson.Serialize(otherRoster.Generation), "Player ownership or formation changed generated dungeon content.");
        Check(!generated.Contains("Roster") && !generated.Contains("Formation\"") && !generated.Contains("RngState"), "Generation includes player or battle state.");
        var first = run.Nodes.First(n => n.Progress == RouteNodeProgress.Reachable);
        Check(DungeonRunEngine.TryChooseNode(run, first.Id, run.Revision, "enter", out var pending, out var error), error);
        Check(DungeonRunEngine.TryBuildEncounter(pending, catalog, out var encounter, out error), error);
        var combatStreams = new HashSet<ulong>(); var cardStreams = new HashSet<ulong>(); var hands = new HashSet<string>();
        for (var i = 0; i < 24; i++)
        {
            var battle = RunBattleBridge.CreateLocalBattle(encounter);
            combatStreams.Add(battle.RngState); cardStreams.Add(battle.CardRngState);
            hands.Add(string.Join(",", battle.Player.Hand.Select(c => c.OwnerFighterId + ":" + c.SkillId)));
            Check(battle.RngState != battle.CardRngState, "Combat and card randomness share a stream.");
        }
        Check(combatStreams.Count == 24 && cardStreams.Count == 24 && hands.Count > 1, "A fixed dungeon generated fixed battle randomness or draws.");
        var explicitBattle = RunBattleBridge.CreateLocalBattle(encounter, 88UL, cardDrawSeed: 99UL);
        Check(SnapshotJson.Serialize(explicitBattle) == SnapshotJson.Serialize(RunBattleBridge.CreateLocalBattle(encounter, 88UL, cardDrawSeed: 99UL)),
            "Explicit battle entropy must remain replayable for tests.");

        Check(DungeonRunEngine.TrySetFormation(pending, new[] { Seed(0, 0), Seed(2, 1) }, catalog, profile,
            pending.Revision, "swap", out var swapped, out error), error);
        Check(swapped.Roster.Count == 3 && !swapped.Formation.Contains("owned-1"), "Changing teams erased fighter history.");
        Check(DungeonRunEngine.TryBuildEncounter(swapped, catalog, out encounter, out error), error);
        Check(encounter.PlayerTeam.Count == 2 && !encounter.PlayerTeam.Exists(f => f.FighterId == "owned-1"), "Benched fighter entered the battle.");
        var outcome = RunBattleBridge.CreateLocalBattle(encounter, 1UL);
        outcome.Player.FindFighter("owned-0").Health = 0;
        outcome.Player.FindFighter("owned-2").Health = 0;
        var results = RunBattleBridge.BuildRunResults(swapped, outcome);
        Check(results.Find(f => f.RunFighterId == "owned-1").CurrentHealth == 1000, "Benched fighter died without participating.");
        Check(DungeonRunEngine.TryApplyBattleResult(swapped, swapped.PendingBattleId, "loss-receipt", swapped.Revision,
            "loss", false, results, out var lost, out error), error);
        Check(lost.Status == RunStatus.InProgress && lost.Rewards.Count == 0 && lost.Formation.All(string.IsNullOrEmpty),
            "Defeat ended the run, paid rewards, or left dead fighters equipped.");
        Check(lost.FindNode(lost.CurrentNodeId).Progress == RouteNodeProgress.Selected, "Loss advanced or completed the stage.");
        Check(!DungeonRunEngine.TryChooseNode(lost, first.OutgoingNodeIds[0], lost.Revision, "skip-loss", out _, out _), "Loss can skip its stage.");
        Check(DungeonRunEngine.TryRetryBattle(lost, lost.Revision, "retry", out var retry, out error), error);
        Check(retry.PendingBattleId != swapped.PendingBattleId, "Retry reuses the old battle identity.");
        Check(!DungeonRunEngine.TrySetFormation(retry, new[] { Seed(0, 0) }, catalog, profile, retry.Revision, "dead-team", out _, out _),
            "Defeated character can be equipped again.");
        Check(DungeonRunEngine.TrySetFormation(retry, new[] { Seed(1, 0), Seed(3, 1) }, catalog, profile,
            retry.Revision, "replacement-team", out var prepared, out error), error);
        Check(prepared.Roster.Count == 4 && prepared.Roster.Count(f => f.IsDefeated) == 2, "Replacements erase defeated history.");
        Check(!DungeonRunEngine.TryApplyBattleResult(prepared, swapped.PendingBattleId, "stale", prepared.Revision,
            "old-result", false, results, out _, out _), "Old attempt can overwrite the current encounter.");
        results = prepared.Roster.Select(f => new BattleFighterResult { RunFighterId = f.RunFighterId, CurrentHealth = f.CurrentHealth, IsDefeated = f.IsDefeated }).ToList();
        Check(DungeonRunEngine.TryApplyBattleResult(prepared, prepared.PendingBattleId, "replacement-win", prepared.Revision,
            "win", true, results, out var won, out error), error);
        Check(won.Rewards.Count == 1 && won.Status == RunStatus.InProgress && won.Roster.Count(f => f.IsDefeated) == 2, "Victory reward or character history is wrong.");
        var beforeReplay = SnapshotJson.Serialize(won);
        Check(DungeonRunEngine.TryApplyBattleResult(won, prepared.PendingBattleId, "replacement-win", prepared.Revision,
            "win", true, results, out var replay, out error) && beforeReplay == SnapshotJson.Serialize(replay), "Victory replay changed rewards.");
        Check(DungeonRunEngine.TryAcknowledgeReward(won, won.Rewards[0].Id, won.Revision, "ack", out var acknowledged, out error), error);
        Check(acknowledged.PresentedRewardIds.Count == 1 && acknowledged.Rewards.Count == won.Rewards.Count, "Acknowledgment changed the payout.");
        Check(DungeonRunEngine.TryAcknowledgeReward(acknowledged, won.Rewards[0].Id, won.Revision, "ack", out replay, out error) &&
            SnapshotJson.Serialize(replay) == SnapshotJson.Serialize(acknowledged), "Acknowledgment replay changed state.");
        Check(!DungeonRunEngine.TryAcknowledgeReward(won, "unknown", won.Revision, "bad-ack", out _, out _), "Unknown reward acknowledgment accepted.");
        var wipeResults = swapped.Roster.Select(f => new BattleFighterResult { RunFighterId = f.RunFighterId,
            CurrentHealth = f.RunFighterId == "owned-1" ? f.CurrentHealth : 0, IsDefeated = f.RunFighterId != "owned-1" }).ToList();
        Check(!DungeonRunEngine.TryApplyBattleResult(swapped, swapped.PendingBattleId, "invalid-win", swapped.Revision,
            "win-with-only-bench", true, wipeResults, out _, out _), "Benched survivors permit a false battle victory.");
        var single = pending.Clone(); single.Roster.RemoveAll(f => f.RunFighterId != "owned-0"); single.Formation[1] = "";
        Check(DungeonRunEngine.TryApplyBattleResult(single, single.PendingBattleId, "full-wipe", single.Revision,
            "wipe", false, new[] { new BattleFighterResult { RunFighterId = "owned-0", CurrentHealth = 0, IsDefeated = true } }, out var wiped, out error), error);
        Check(wiped.Status == RunStatus.InProgress && wiped.Roster.All(f => f.IsDefeated) && wiped.Formation.All(string.IsNullOrEmpty), "Full wipe automatically ended the run.");
        Check(DungeonRunEngine.TryRetryBattle(wiped, wiped.Revision, "wipe-retry", out var wipeRetry, out error), error);
        Check(DungeonRunEngine.TrySetFormation(wipeRetry, new[] { Seed(5, 0) }, catalog, profile, wipeRetry.Revision,
            "wipe-replacement", out _, out error), error);

        // Place at support nodes to test their state transitions independently of route topology.
        var rest = won.Clone(); rest.CurrentNodeId = rest.Nodes.First(n => n.Type == RouteNodeType.Rest).Id;
        rest.FindNode(rest.CurrentNodeId).Progress = RouteNodeProgress.Selected;
        rest.FindFighter("owned-1").CurrentHealth = 100;
        Check(!DungeonRunEngine.TryApplyRest(rest, RestChoice.ReviveOne, "owned-0", rest.Revision, "revive", out _, out _), "Rest revived a defeated fighter.");
        Check(DungeonRunEngine.TryApplyRest(rest, RestChoice.HealLiving, null, rest.Revision, "heal", out var rested, out error), error);
        Check(rested.FindFighter("owned-1").CurrentHealth == 500 && rested.FindFighter("owned-0").IsDefeated, "Rest recovery changed permanent defeat.");
        var fullRest = won.Clone(); fullRest.CurrentNodeId = rest.CurrentNodeId; fullRest.FindNode(rest.CurrentNodeId).Progress = RouteNodeProgress.Selected;
        Check(DungeonRunEngine.TryApplyRest(fullRest, RestChoice.Continue, null, fullRest.Revision, "rest-skip", out _, out error), error);
        var boon = won.Clone(); boon.CurrentNodeId = boon.Nodes.First(n => n.Type == RouteNodeType.Boon).Id;
        boon.FindNode(boon.CurrentNodeId).Progress = RouteNodeProgress.Selected;
        var offer = DungeonRunEngine.GetAvailableBoonOffers(boon, boon.CurrentNodeId).Single();
        var elite = lost.Clone(); elite.CurrentNodeId = elite.Nodes.First(n => n.Type == RouteNodeType.Elite).Id;
        elite.FindNode(elite.CurrentNodeId).Progress = RouteNodeProgress.Selected;
        Check(!DungeonRunEngine.TryChooseBoon(elite, "stat-boost:" + elite.CurrentNodeId, elite.Revision,
            "unearned-elite", out _, out _), "Elite stat reward can be claimed without a victory.");
        var boost = boon.FindNode(boon.CurrentNodeId).StatBoost;
        Check(boost.AmountBp >= 300 && boost.AmountBp <= 800, "Stat boost outside starting range.");
        var previous = boon.FindFighter("owned-1").Stats.Get(boost.Stat);
        Check(DungeonRunEngine.TryChooseBoon(boon, offer, boon.Revision, "blessing", out var blessed, out error), error);
        Check(blessed.StatBoosts.Count == 1 && blessed.FindFighter("owned-1").Stats.Get(boost.Stat) > previous &&
            blessed.FindFighter("owned-0").Stats.Get(boost.Stat) == boon.FindFighter("owned-0").Stats.Get(boost.Stat), "Blessing should only boost living fighters.");
        Check(DungeonRunEngine.TryChooseBoon(blessed, offer, boon.Revision, "blessing", out replay, out error) &&
            SnapshotJson.Serialize(replay) == SnapshotJson.Serialize(blessed), "Blessing replay stacks stats.");
        Check(!DungeonRunEngine.TryChooseBoon(blessed, offer, blessed.Revision, "second-blessing", out _, out _), "Completed boon can be claimed twice.");
        blessed.Status = RunStatus.InBattle; blessed.PendingBattleId = "new-encounter";
        Check(DungeonRunEngine.TrySetFormation(blessed, new[] { Seed(4, 0) }, catalog, profile, blessed.Revision,
            "later-recruit", out var recruited, out error), error);
        Check(recruited.FindFighter("owned-4").Stats.Get(boost.Stat) > catalog[4].BaseStats.Get(boost.Stat), "Later recruit missed the run blessing.");
        var restored = SnapshotJson.Deserialize<RunState>(SnapshotJson.Serialize(recruited));
        Check(restored.Roster.Count == 5 && restored.Roster.Count(f => f.IsDefeated) == 2 && restored.StatBoosts.Count == 1,
            "Save/resume lost roster history or blessings.");
        Check(!SnapshotJson.Serialize(restored).Contains("\"Seed\":123,\"Revision\""), "New save uses the old flat architecture.");
        var legacy = SnapshotJson.Deserialize<RunState>("{\"RunId\":\"old\",\"Seed\":77,\"GeneratorVersion\":2,\"Nodes\":[],\"Roster\":[]}");
        Check(legacy.Generation.Seed == 77 && legacy.Generation.GeneratorVersion == 2 && legacy.Nodes.Count == 0, "Legacy generation fields failed to migrate.");
        var deepCopy = restored.Clone(); deepCopy.Generation.Seed = 999; deepCopy.StatBoosts[0].AmountBp = 1;
        Check(restored.Seed == 123 && restored.StatBoosts[0].AmountBp >= 300, "Clone aliases generation or buffs.");
        var allDead = lost.Clone(); foreach (var fighter in allDead.Roster) { fighter.CurrentHealth = 0; fighter.IsDefeated = true; }
        Check(DungeonRunEngine.TryAbandon(allDead, allDead.Revision, "abandon-wipe", out var abandoned, out error) && abandoned.Status == RunStatus.Abandoned, error);
        Console.WriteLine("PASS: independent dungeon/battle entropy, generation data, permanent defeat, bench history, replacements, retry, rewards, stat blessings and save compatibility.");
    }
}
