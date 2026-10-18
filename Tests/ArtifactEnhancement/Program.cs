using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;

internal static partial class Program
{
    private static int assertions;
    private static void Check(bool condition, string reason)
    { assertions++; if (!condition) throw new Exception(reason); }
    private static string Json<T>(T value) => SnapshotJson.Serialize(value);
    private static T Copy<T>(T value) => SnapshotJson.Deserialize<T>(Json(value));
    private static ItemInventory Wallet(int seed = 491, ArtifactRarity rarity = ArtifactRarity.SSR)
    {
        var wallet = new ItemInventory { Coins = 10000000 };
        wallet.Materials.Add(new ItemAmount { ItemId = ItemCatalog.ArtifactDust, Amount = 10000 });
        wallet.Artifacts.Add(ArtifactRules.Generate("target", rarity, seed));
        wallet.Artifacts.Add(ArtifactRules.Generate("fodder-a", ArtifactRarity.Common, seed + 1));
        wallet.Artifacts.Add(ArtifactRules.Generate("fodder-b", ArtifactRarity.Common, seed + 2));
        return wallet;
    }
    private static ArtifactEnhancementPreview Preview(ItemInventory wallet, long dust, params string[] fodder)
    {
        Check(ArtifactRules.PreviewEnhance(wallet, "target", dust, fodder, out var preview, out var error), error);
        return preview;
    }
    private static void Rejected(ItemInventory wallet, ArtifactEnhancementPreview review, string reason)
    {
        var before = Json(wallet);
        Check(!ArtifactRules.TryEnhance(wallet, review, out var next, out var preview, out var error), reason);
        Check(next == null && preview == null && !string.IsNullOrEmpty(error) && Json(wallet) == before,
            reason + " must leave the inventory unchanged and explain the rejection.");
    }
    public static void Main()
    {
        AuthoredBalanceChecks();
        RandomRollsAndPersistence();
        MaterialChoiceCannotReroll();
        ReviewedTransactions();
        UpgradeBatching();
        RarityAndResourceEdges();
        Console.WriteLine("PASS: " + assertions + " artifact assertions: authored main/substat tables, quality/rarity scaling, migration, damage-family effects, stable random upgrades, reviewed transactions and resource protection.");
    }
    private static void RandomRollsAndPersistence()
    {
        var rolledIndices = new HashSet<int>(); var rolledGains = new HashSet<int>();
        for (var seed = 1; seed <= 128; seed++)
        {
            var wallet = Wallet(seed); var before = Json(wallet);
            var review = Preview(wallet, 385);
            Check(review.TargetAfter.Level == 20 && review.AffixImprovements.Count == 3 &&
                review.Milestones.SequenceEqual(new[] { 6, 12, 18 }), "Only earned milestones may improve an affix.");
            Check(Json(wallet) == before, "Drafting must not spend materials or mutate the target.");
            Check(Json(review) == Json(Preview(wallet, 385)), "Reopening a draft must preserve the random result.");
            var values = review.TargetBefore.Substats.Select(s => s.Value).ToArray();
            foreach (var gain in review.AffixImprovements)
            {
                Check(gain.SubstatIndex >= 0 && gain.SubstatIndex < 4 && gain.BeforeValue == values[gain.SubstatIndex] &&
                    gain.AfterValue > gain.BeforeValue, "Every milestone must identify its exact random line and positive gain.");
                values[gain.SubstatIndex] = gain.AfterValue;
                rolledIndices.Add(gain.SubstatIndex); rolledGains.Add(gain.AfterValue - gain.BeforeValue);
            }
            Check(values.SequenceEqual(review.TargetAfter.Substats.Select(s => s.Value)), "Roll details must reconstruct every displayed substat.");
            Check(ArtifactRules.TryEnhance(wallet, review, out var next, out var committed, out var error), error);
            Check(Json(review) == Json(committed) && Json(wallet) == before, "Commit must use exactly the reviewed roll without altering the source wallet.");
            var saved = Copy(next.Artifacts[0]); var savedJson = Json(saved); ArtifactRules.Normalize(saved);
            Check(savedJson == Json(saved), "Reload normalization must preserve all committed affix gains.");
        }
        Check(rolledIndices.Count == 4 && rolledGains.Count > 4, "Randomness must select across all four affixes with varied gains.");
    }
    private static void MaterialChoiceCannotReroll()
    {
        var wallet = Wallet();
        var allDust = Preview(wallet, 100);
        var firstFodder = Preview(wallet, 98, "fodder-a");
        var secondFodder = Preview(wallet, 98, "fodder-b");
        Check(Json(allDust.TargetAfter) == Json(firstFodder.TargetAfter) &&
            Json(firstFodder.TargetAfter) == Json(secondFodder.TargetAfter), "Equal EXP from different materials must keep the same random result.");
        var larger = Preview(wallet, 300);
        Check(Json(allDust.AffixImprovements[0]) == Json(larger.AffixImprovements[0]), "Changing the draft level must preserve already reached milestone rolls.");
        allDust.TargetAfter.Substats[0].Value++;
        Check(Json(wallet.Artifacts[0]) == Json(allDust.TargetBefore), "Preview outputs must not alias the saved artifact.");
    }
    private static void ReviewedTransactions()
    {
        var wallet = Wallet(); var review = Preview(wallet, 98, "fodder-a");
        var modified = Copy(review); modified.TargetAfter.Substats[0].Value++;
        Rejected(wallet, modified, "Modified outcome");
        modified = Copy(review); modified.CoinCost--;
        Rejected(wallet, modified, "Modified cost");
        modified = Copy(review); modified.AffixImprovements[0].SubstatIndex = (modified.AffixImprovements[0].SubstatIndex + 1) % 4;
        Rejected(wallet, modified, "Modified roll cue");
        modified = Copy(review); modified.Milestones.Clear();
        Rejected(wallet, modified, "Modified milestone list");
        modified = Copy(review); modified.TargetBefore = null;
        Rejected(wallet, modified, "Missing reviewed target");
        var stale = Copy(wallet); stale.Artifacts[0].Experience++;
        Rejected(stale, review, "Target changed during review");
        stale = Copy(wallet); stale.Artifacts[0].EquippedCharacterId = "another-fighter";
        Rejected(stale, review, "Target ownership changed during review");
        stale = Copy(wallet); stale.Artifacts[1].Favourite = true;
        Rejected(stale, review, "Material favourited during review");
        stale = Copy(wallet); stale.Artifacts[1].EquippedCharacterId = "fighter";
        Rejected(stale, review, "Material equipped during review");
        stale = Copy(wallet); stale.Artifacts.RemoveAt(1);
        Rejected(stale, review, "Material disappeared during review");
        stale = Copy(wallet); stale.Materials[0].Amount = 0;
        Rejected(stale, review, "Dust spent during review");
        stale = Copy(wallet); stale.Coins = 0;
        Rejected(stale, review, "Coins spent during review");
        var unrelated = Copy(wallet); unrelated.Coins++;
        Check(ArtifactRules.TryEnhance(unrelated, review, out _, out _, out _), "Unrelated wallet changes must not invalidate a still affordable enhancement.");
        Check(ArtifactRules.TryEnhance(wallet, review, out var committed, out var result, out var error), error);
        Check(committed.Coins == wallet.Coins - result.CoinCost &&
            committed.Amount(ItemCatalog.ArtifactDust) == wallet.Amount(ItemCatalog.ArtifactDust) - 98 &&
            committed.Artifacts.Count == 2 && committed.Artifacts.Any(a => a.InstanceId == "fodder-b") &&
            Json(committed.Artifacts[0]) == Json(result.TargetAfter), "Commit must consume only reviewed coins, dust and source artifacts.");
        Rejected(committed, review, "Repeated confirmation");
    }
    private static void UpgradeBatching()
    {
        for (var seed = 1; seed <= 8; seed++)
        {
            var initial = Wallet(seed); var review = Preview(initial, 385);
            Check(ArtifactRules.TryEnhance(initial, review, out var bulk, out _, out var error), error);
            var incremental = initial; var gains = new List<ArtifactAffixImprovement>();
            for (var i = 0; i < 385; i++)
            {
                Check(ArtifactRules.TryEnhance(incremental, "target", 1, null, out var next, out var piece, out error), error);
                gains.AddRange(piece.AffixImprovements); incremental = next;
            }
            Check(Json(bulk) == Json(incremental) && Json(gains) == Json(review.AffixImprovements), "Splitting an upgrade must not change cost, final stats or random milestone results.");
        }
    }
    private static void RarityAndResourceEdges()
    {
        foreach (ArtifactRarity rarity in Enum.GetValues(typeof(ArtifactRarity)))
        {
            var wallet = Wallet(rarity: rarity); var review = Preview(wallet, 1000);
            Check(review.TargetAfter.Level == 20 && review.AffixImprovements.Count == (rarity == ArtifactRarity.Common ? 0 : 3),
                "Common artifacts have no affixes; all other rarities improve one existing affix at each milestone.");
            Check(review.ExcessExperience > 0 && review.CoinCost == ArtifactRules.RemainingXp(wallet.Artifacts[0]) * 2,
                "Capped upgrades must disclose wasted EXP and charge coins only for usable EXP.");
        }
        var resources = Wallet(); var before = Json(resources);
        Check(!ArtifactRules.TryEnhance(resources, "target", 0, new[] { "target" }, out _, out _, out _), "Self consumption must be rejected.");
        Check(!ArtifactRules.TryEnhance(resources, "target", 0, new[] { "fodder-a", "fodder-a" }, out _, out _, out _), "Duplicate source consumption must be rejected.");
        Check(!ArtifactRules.TryEnhance(resources, "target", 0, null, out _, out _, out _), "Empty enhancement must be rejected.");
        Check(!ArtifactRules.TryEnhance(resources, "target", -1, null, out _, out _, out _), "Negative dust must be rejected.");
        Check(Json(resources) == before, "Rejected input must preserve every resource.");
    }
}
