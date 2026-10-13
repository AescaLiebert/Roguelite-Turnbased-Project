using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;

internal static partial class BattlePlaybackChecks
{
    private static void DungeonRewardChecks()
    {
        var materials = ItemCatalog.UpgradeMaterials();
        Check(materials.Count == 14 && materials.Select(i => i.Id).Distinct().Count() == 14 &&
            materials.Count(i => i.Kind == ItemKind.PhaseMaterial) == 9 && materials.Count(i => i.Kind == ItemKind.SubPhaseMaterial) == 5,
            "Upgrade catalog must have nine Phase Crests and five Ascension Sigils.");
        Check(materials.Last().Name.Contains("META") && materials[8].Name.Contains("META"), "Both material progressions must end at META.");
        Check(ItemCatalog.Find(ItemCatalog.SeriesId("series.kof")).SeriesId == "series.kof" &&
            ItemCatalog.Find(ItemCatalog.SeriesId("series.tekken", true)).Kind == ItemKind.SeriesPremiumMaterial,
            "Series materials must preserve their series and premium identity.");
        var catalog = new List<CharacterDefinition>();
        for (var i = 0; i < 8; i++)
        {
            var fighter = Fighter("reward-fighter-" + i); fighter.RuntimeReady = true;
            fighter.SourceId = "source." + i; fighter.PassiveId = "passive." + i; fighter.SeriesId = "series.kof"; fighter.CategoryId = 1001 + i;
            for (var tier = 1; tier < 6; tier++) fighter.UltimateTiers.Add(new UltimateTierDefinition { Tier = tier, Effect = fighter.UltimateTiers[0].Effect.Clone() });
            catalog.Add(fighter);
        }
        var seeds = catalog.Take(4).Select((f, i) => new RunFighterSeed { OwnedFighterId = "owned-" + i,
            Definition = f, FormationSlot = i, IsReserve = i == 3 }).ToList();
        var profile = DungeonProfile.Filtered("series.kof", 1, 0);
        var run = DungeonRunEngine.CreateRun("reward-run", "test", "start", profile, 25, 17, "v", "hash", seeds, catalog);
        var before = SnapshotJson.Serialize(run);
        foreach (var node in run.Nodes)
        {
            var inspected = DungeonRunEngine.InspectNode(run, node.Id);
            Check(inspected != null && inspected.Id == node.Id && inspected.Progress == node.Progress, "All nodes must be inspectable.");
            inspected.OutgoingNodeIds.Clear();
            if (inspected.EnemyTeamSnapshot.Count > 0) inspected.EnemyTeamSnapshot[0].Stats.Attack = 0;
        }
        Check(SnapshotJson.Serialize(run) == before, "Inspection modified route, revision, enemy snapshots or reward state.");
        Check(!DungeonRunEngine.CanEnterNode(run, run.Nodes.First(n => n.Type == RouteNodeType.Boss).Id), "Inspection cannot make a locked boss enterable.");
        var first = run.Nodes.First(n => n.Progress == RouteNodeProgress.Reachable);
        Check(DungeonRunEngine.CanEnterNode(run, first.Id), "Reachable battle must be enterable.");
        Check(DungeonRunEngine.TryChooseNode(run, first.Id, run.Revision, "enter", out var selected, out var error), error);
        Check(selected.SelectedPath.Count == run.SelectedPath.Count + 1 && selected.Status == RunStatus.InBattle,
            "Explicit entry must commit the route and encounter.");
        var results = selected.Roster.Select(f => new BattleFighterResult { RunFighterId = f.RunFighterId, CurrentHealth = 100 }).ToList();
        Check(DungeonRunEngine.TryApplyBattleResult(selected, selected.PendingBattleId, "battle-receipt", selected.Revision,
            "win", true, results, out var won, out error), error);
        Check(won.Rewards.Count == 1 && won.Rewards[0].Amount(ItemCatalog.Diamond) == 25 && won.Rewards[0].Amount(ItemCatalog.Coin) == 750,
            "Every battle victory must grant difficulty-scaled currency and materials.");
        Check(won.Rewards[0].Items.Any(i => i.ItemId == ItemCatalog.PhaseId(1)) && won.Rewards[0].Items.Any(i => i.ItemId == ItemCatalog.SubPhaseId(0)),
            "Victory must grant the selected phase and sub-phase material.");
        Check(DungeonRunEngine.TryApplyBattleResult(won, selected.PendingBattleId, "battle-receipt", selected.Revision,
            "win", true, results, out var replayed, out error) && SnapshotJson.Serialize(replayed) == SnapshotJson.Serialize(won),
            "Retrying a battle result must not roll or append rewards again.");
        Check(!DungeonRunEngine.TryApplyBattleResult(won, selected.PendingBattleId, "battle-receipt", won.Revision,
            "another-win", true, results, out _, out _), "A reused result receipt must be rejected.");
        Check(DungeonRunEngine.TryApplyBattleResult(selected, selected.PendingBattleId, "loss-receipt", selected.Revision,
            "lose", false, results, out var lost, out error) && lost.Rewards.Count == 0, "Loss or draw must not grant victory rewards.");
        var clone = won.Clone(); clone.Rewards[0].Items[0].Amount = 0;
        Check(won.Rewards[0].Items[0].Amount > 0, "Run reward snapshots must not alias their original.");
        var loaded = SnapshotJson.Deserialize<RunState>(SnapshotJson.Serialize(won));
        Check(SnapshotJson.Serialize(loaded) == SnapshotJson.Serialize(won), "Reward receipts must survive save/resume.");

        var preview = DungeonRewards.Preview(profile, new[] { "series.kof", "series.kof", "series.tekken" });
        Check(preview.Count(i => i.Kind == ItemKind.SeriesMaterial) == 2 && !preview.Any(i => i.Kind == ItemKind.SeriesPremiumMaterial),
            "Regular preview must show possible series materials once and exclude premium materials.");
        Check(preview.Count(i => i.Kind == ItemKind.Artifact) == 1 && preview.Single(i => i.Kind == ItemKind.Artifact).Tier == 1 &&
            preview.Count(i => i.PremiumFrame) == 2 && ItemCatalog.Find(ItemCatalog.Coin).PremiumFrame,
            "Easy preview must show only its highest artifact tier and give both currencies a Gold frame.");
        Check(preview.Select(i => i.ValueRank).SequenceEqual(preview.Select(i => i.ValueRank).OrderByDescending(v => v)),
            "Preview rewards must be sorted from highest value to lowest.");
        foreach (var level in Enumerable.Range(1, 5))
        {
            var levelPreview = DungeonRewards.Preview(profile, new[] { "series.kof" }, DungeonDifficulty.BonusPercent(level));
            Check(levelPreview.Single(i => i.Kind == ItemKind.Artifact).Tier == level &&
                DungeonDifficulty.Level(DungeonDifficulty.BonusPercent(level)) == level,
                "Five difficulty levels must map to Common / Uncommon / R / SR / SSR.");
        }
        Check(materials.All(i => i.FrameTier == 3) && ItemCatalog.Find(ItemCatalog.SeriesId("series.kof")).FrameTier == 4,
            "Phase materials must be Blue and series materials Purple.");
        Check(Enum.GetValues(typeof(ArtifactRarity)).Cast<ArtifactRarity>().All(r => ItemCatalog.Find(ItemCatalog.ArtifactId(r)).FrameTier == (int)r),
            "All five artifact frames must match their rarity.");
        var bossProfile = DungeonProfile.SeriesChallengeBoss("series.kof", 1, 0);
        var bossRun = DungeonRunEngine.CreateRun("challenge-run", "test", "start", bossProfile, 100, 17, "v", "hash", seeds, catalog);
        Check(bossRun.Nodes.Count == 2 && RouteGenerator.ValidateGraph(bossRun.Nodes, out error, true), "Series Challenge must be a valid Start→Boss route: " + error);
        Check(!RouteGenerator.ValidateGraph(bossRun.Nodes, out _, false), "A Series Challenge route cannot pass regular nine-row validation.");
        var boss = bossRun.Nodes.Single(n => n.Type == RouteNodeType.Boss);
        Check(DungeonRunEngine.TryChooseNode(bossRun, boss.Id, bossRun.Revision, "boss-enter", out var pendingBoss, out error), error);
        Check(DungeonRunEngine.TryApplyBattleResult(pendingBoss, pendingBoss.PendingBattleId, "boss-receipt", pendingBoss.Revision,
            "boss-win", true, results, out var cleared, out error), error);
        Check(cleared.Status == RunStatus.Completed && cleared.Rewards.Count == 1 && !cleared.Rewards[0].CompletionBonus &&
            cleared.RewardQuoteDiamonds == 0 &&
            cleared.Rewards[0].Items.Any(i => ItemCatalog.Find(i.ItemId)?.Kind == ItemKind.SeriesPremiumMaterial),
            "Challenge victory must grant battle rewards and its Relic Core, with no dungeon-clear bonus.");
        var normalBoss = DungeonRewards.Victory(run, run.Nodes.First(n => n.Type == RouteNodeType.Boss));
        Check(!normalBoss.Items.Any(i => ItemCatalog.Find(i.ItemId)?.Kind == ItemKind.SeriesPremiumMaterial),
            "Regular bosses must never grant Series Challenge premium materials.");

        var inventory = new ItemInventory();
        Check(inventory.TryGrant(won.Rewards[0], out var wallet) && inventory.Coins == 0 && wallet.Coins == 750,
            "Reward grants must be atomic and clone the wallet.");
        Check(!wallet.TryGrant(won.Rewards[0], out _), "Reward receipt replay paid twice.");
        Check(wallet.TryGrant(cleared.Rewards[0], out var premiumWallet), "Challenge materials must be grantable.");
        var invalid = normalBoss.Clone(); invalid.Id = "invalid-premium"; invalid.Items.Add(new ItemAmount { ItemId = ItemCatalog.SeriesId("series.kof", true), Amount = 1 });
        var rejected = false;
        try { wallet.TryGrant(invalid, out _); } catch (ArgumentException) { rejected = true; }
        Check(rejected && wallet.Coins == 750, "Premium grant outside Series Challenge must fail without changing the wallet.");
        Check(wallet.TrySpend(new[] { new ItemAmount { ItemId = ItemCatalog.Coin, Amount = 100 },
            new ItemAmount { ItemId = ItemCatalog.PhaseId(1), Amount = 1 } }, UpgradeResourceUse.CharacterUpgrade, out var spent, out error) &&
            spent.Coins == 650 && spent.Amount(ItemCatalog.PhaseId(1)) == wallet.Amount(ItemCatalog.PhaseId(1)) - 1 && wallet.Amount(ItemCatalog.PhaseId(1)) >= 1,
            "Character upgrades must spend Coins and the exact material atomically.");
        Check(!wallet.TrySpend(new[] { new ItemAmount { ItemId = ItemCatalog.Coin, Amount = 400 }, new ItemAmount { ItemId = ItemCatalog.Coin, Amount = 400 } },
            UpgradeResourceUse.CharacterUpgrade, out _, out _), "Duplicate cost rows cannot overspend the balance.");
        Check(!premiumWallet.TrySpend(new[] { new ItemAmount { ItemId = ItemCatalog.SeriesId("series.kof", true), Amount = 1 } },
            UpgradeResourceUse.CharacterUpgrade, out _, out _), "Relic Cores cannot fund character upgrades.");
        Check(premiumWallet.TrySpend(new[] { new ItemAmount { ItemId = ItemCatalog.SeriesId("series.kof", true), Amount = 1 } },
            UpgradeResourceUse.HolyRelic, out _, out _), "Relic Cores must be spendable for Holy Relics.");
        Check(!wallet.TrySpend(new[] { new ItemAmount { ItemId = ItemCatalog.Diamond, Amount = 1 } },
            UpgradeResourceUse.CharacterUpgrade, out _, out _), "Upgrades must not consume Gacha Diamonds.");

        Check(!wallet.TryGrant(new DungeonRewardReceipt { Id = "legacy-clear", CompletionBonus = true,
            Items = new List<ItemAmount> { new ItemAmount { ItemId = ItemCatalog.Coin, Amount = 3000 } } }, out _),
            "Legacy completion receipts must not pay a new clear bonus.");
        var lowDrops = 0; var highDrops = 0;
        var phaseAmounts = new HashSet<long>(); var rarities = new HashSet<ArtifactRarity>();
        for (ulong seed = 0; seed < 300; seed++)
        {
            run.Seed = seed; run.DifficultyBonusPercent = 0;
            var low = DungeonRewards.Victory(run, first);
            run.DifficultyBonusPercent = 100;
            var high = DungeonRewards.Victory(run, first);
            Check(high.Amount(ItemCatalog.Diamond) == 40 && high.Amount(ItemCatalog.Coin) == 1200,
                "Currency must be a fixed payout at the selected difficulty, independent of reward rolls.");
            foreach (var id in new[] { ItemCatalog.PhaseId(1), ItemCatalog.SubPhaseId(0), ItemCatalog.SeriesId("series.kof") })
                Check(high.Amount(id) >= 1 && high.Amount(id) <= 6, "Victory must guarantee a material amount within the range.");
            phaseAmounts.Add(high.Amount(ItemCatalog.PhaseId(1)));
            Check(SnapshotJson.Serialize(high) == SnapshotJson.Serialize(DungeonRewards.Victory(run, first)), "Reward generation is not deterministic.");
            if (low.Artifacts.Count > 0)
            {
                lowDrops++; Check(high.Artifacts.Count > 0 && high.Artifacts[0].Rarity >= low.Artifacts[0].Rarity,
                    "Higher difficulty must retain a low-difficulty drop and cannot downgrade its rarity.");
            }
            if (high.Artifacts.Count > 0)
            {
                rarities.Add(high.Artifacts[0].Rarity);
                highDrops++; Check(high.Artifacts[0].InstanceId.StartsWith(run.RunId + ":reward:" + first.Id), "Artifact must have stable run/node instance identity.");
            }
        }
        Check(highDrops > lowDrops, "Higher difficulty did not increase artifact drop frequency.");
        Check(phaseAmounts.Count > 1 && rarities.Count == 5, "Materials must vary and all five artifact tiers must be obtainable at Nightmare.");
        var legacy = SnapshotJson.Deserialize<RunState>("{\"RunId\":\"legacy\",\"Roster\":[],\"Nodes\":[]}");
        Check(legacy.Rewards.Count == 0 && legacy.Mode == DungeonMode.Route && legacy.RewardPhase == 1,
            "Legacy runs must migrate with empty receipts and a regular reward context.");
    }
}
