using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Run
{
    public enum DungeonMode { Route, SeriesChallengeBoss }
    public enum ArtifactDropPattern { AttackDefense, HealthAttack, DefenseHealth }
    public enum UpgradeResourceUse { CharacterUpgrade, HolyRelic, ArtifactUpgrade, External }
    [Serializable]
    public sealed class ItemAmount
    {
        public string ItemId;
        public long Amount;
        public ItemAmount Clone() => (ItemAmount)MemberwiseClone();
    }
    [Serializable]
    public sealed class ArtifactInstance
    {
        public string InstanceId, ItemId, SourceRunId, SourceNodeId;
        public ArtifactRarity Rarity;
        public int SchemaVersion, Seed, Level;
        public long Experience;
        public ArtifactSet Set;
        public ArtifactSlot Slot;
        public bool Favourite;
        public string EquippedCharacterId;
        public ArtifactStatLine MainStat;
        public List<ArtifactStatLine> Substats = new List<ArtifactStatLine>();
        public ArtifactInstance Clone()
        {
            var copy = (ArtifactInstance)MemberwiseClone();
            copy.MainStat = MainStat?.Clone();
            copy.Substats = Substats?.ConvertAll(s => s?.Clone()) ?? new List<ArtifactStatLine>();
            return copy;
        }
    }
    [Serializable]
    public sealed class DungeonRewardReceipt
    {
        public string Id, NodeId;
        public DungeonMode Mode;
        public bool CompletionBonus;
        public List<ItemAmount> Items = new List<ItemAmount>();
        public List<ArtifactInstance> Artifacts = new List<ArtifactInstance>();
        public long Amount(string itemId) => Items.Where(i => i.ItemId == itemId).Sum(i => i.Amount);
        public DungeonRewardReceipt Clone() => new DungeonRewardReceipt { Id = Id, NodeId = NodeId, Mode = Mode,
            CompletionBonus = CompletionBonus, Items = Items.ConvertAll(i => i.Clone()), Artifacts = Artifacts.ConvertAll(a => a.Clone()) };
    }

    /// <summary>Five player-facing levels; percentages remain an internal, legacy-compatible enemy scaling value.</summary>
    public static class DungeonDifficulty
    {
        public static readonly string[] Names = { "Easy", "Normal", "Hard", "Extreme", "Nightmare" };
        public static int Clamp(int level) => Math.Max(1, Math.Min(5, level));
        public static int BonusPercent(int level) => (Clamp(level) - 1) * 25;
        public static int Level(int bonusPercent) => Clamp(1 + (Math.Max(0, Math.Min(100, bonusPercent)) + 12) / 25);
        public static string Display(int level) => Clamp(level) + " · " + Names[Clamp(level) - 1];
    }

    /// <summary>Starting reward values. Kept separate from route and combat RNG; inspections never roll rewards.</summary>
    public static class DungeonRewards
    {
        public static bool IsBattle(RouteNodeType type) => type == RouteNodeType.Battle || type == RouteNodeType.Elite || type == RouteNodeType.Boss;
        public static int ArtifactChancePercent(int difficulty) => 25 + DungeonDifficulty.BonusPercent(DungeonDifficulty.Level(difficulty)) / 2;
        public static ArtifactRarity HighestArtifact(int difficulty) => (ArtifactRarity)DungeonDifficulty.Level(difficulty);
        public static int MaterialMaximum(int difficulty) => DungeonDifficulty.Level(difficulty) + 1;

        // Each release stage has one two-set basket. Series and difficulty do not change it.
        public static ArtifactDropPattern ArtifactPattern(int phase, int subPhase) =>
            (ArtifactDropPattern)((Math.Max(1, Math.Min(9, phase)) - 1 + Math.Max(0, Math.Min(4, subPhase))) % 3);
        public static ArtifactSet[] ArtifactBasket(int phase, int subPhase)
        {
            switch (ArtifactPattern(phase, subPhase))
            {
                case ArtifactDropPattern.HealthAttack: return new[] { ArtifactSet.Lifebloom, ArtifactSet.Striker };
                case ArtifactDropPattern.DefenseHealth: return new[] { ArtifactSet.Bulwark, ArtifactSet.Lifebloom };
                default: return new[] { ArtifactSet.Striker, ArtifactSet.Bulwark };
            }
        }
        public static string ArtifactBasketLabel(int phase, int subPhase)
        {
            switch (ArtifactPattern(phase, subPhase))
            {
                case ArtifactDropPattern.HealthAttack: return "HP / ATK";
                case ArtifactDropPattern.DefenseHealth: return "DEF / HP";
                default: return "ATK / DEF";
            }
        }
        public static string ArtifactBasketRule(int phase, int subPhase, int difficulty) =>
            ArtifactChancePercent(difficulty) + "% artifact chance per victory. Basket: " +
            string.Join(" + ", ArtifactBasket(phase, subPhase).Select(ArtifactRules.SetName)) +
            ". Either set can drop any of the five slots. The Flower is a preview icon. Highest possible rarity: " +
            new string('★', (int)HighestArtifact(difficulty)) + "; lower rarities can also drop.";

        // Quality is the primary order. Equal-quality types keep the reward overview order.
        public static int RewardOrder(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Diamond: return 0;
                case ItemKind.Coin: return 1;
                case ItemKind.SeriesMaterial: return 2;
                case ItemKind.SeriesPremiumMaterial: return 3;
                case ItemKind.PhaseMaterial: return 4;
                case ItemKind.SubPhaseMaterial: return 5;
                case ItemKind.Artifact: return 6;
                case ItemKind.ArtifactMaterial: return 7;
                default: return 8;
            }
        }

        public static long CurrencyAmount(string itemId, int difficulty, RouteNodeType stage = RouteNodeType.Battle) =>
            (itemId == ItemCatalog.Diamond ? 20L : 600L) * Multiplier(stage) *
            (100 + DungeonDifficulty.BonusPercent(DungeonDifficulty.Level(difficulty))) / 100;
        private static int Multiplier(RouteNodeType stage) => stage == RouteNodeType.Boss ? 4 : stage == RouteNodeType.Elite ? 2 : 1;

        public static string PreviewRule(ItemDefinition item, int difficulty, RouteNodeType stage = RouteNodeType.Battle, DungeonProfile profile = null)
        {
            if (item.Kind == ItemKind.Diamond || item.Kind == ItemKind.Coin)
                return "Guaranteed per victory: " + CurrencyAmount(item.Id, difficulty, stage).ToString("N0");
            if (item.Kind == ItemKind.Artifact)
                return ArtifactBasketRule(profile?.Phase ?? 1, profile?.SubPhase ?? 0, difficulty) +
                    " Artifact Dust is also guaranteed: " + Multiplier(stage) + "–" + MaterialMaximum(difficulty) * Multiplier(stage) + " per victory.";
            if (item.Kind == ItemKind.SeriesMaterial)
                return "One enemy series guarantees " + Multiplier(stage) + "–" + MaterialMaximum(difficulty) * Multiplier(stage) + " badges per victory." +
                    (profile?.Mode == DungeonMode.SeriesChallengeBoss ? " Series Boss victory also grants its exclusive Relic Core." : "");
            if (item.Kind == ItemKind.SeriesPremiumMaterial)
                return "Guaranteed " + Multiplier(stage) + "–" + MaterialMaximum(difficulty) * Multiplier(stage) + " per victory from one enemy series.";
            return "Guaranteed " + Multiplier(stage) + "–" + MaterialMaximum(difficulty) * Multiplier(stage) + " per victory.";
        }

        public static List<ItemDefinition> Preview(DungeonProfile profile, IEnumerable<string> seriesIds, int difficulty = 0)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var items = new List<ItemDefinition> { ItemCatalog.Find(ItemCatalog.Diamond), ItemCatalog.Find(ItemCatalog.Coin),
                ItemCatalog.Find(ItemCatalog.PhaseId(Math.Max(1, Math.Min(9, profile.Phase)))),
                ItemCatalog.Find(ItemCatalog.SubPhaseId(Math.Max(0, Math.Min(4, profile.SubPhase)))) };
            var series = !string.IsNullOrEmpty(profile.SeriesId) ? new[] { profile.SeriesId } :
                (seriesIds ?? Enumerable.Empty<string>()).Where(id => !string.IsNullOrEmpty(id)).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToArray();
            var badge = series.Length == 1 ? ItemCatalog.Find(ItemCatalog.SeriesId(series[0])) : null;
            items.Add(badge ?? new ItemDefinition { Id = "preview.series-badge", Name = "Series Badge", Kind = ItemKind.SeriesMaterial,
                Description = "One badge from a defeated enemy series is awarded per victory." +
                    (series.Length > 0 ? " Available series: " + string.Join(", ", series.Select(DungeonProfile.SeriesDisplayName)) + "." : "") });
            var basket = ArtifactBasket(profile.Phase, profile.SubPhase);
            foreach (var set in basket)
                items.Add(new ItemDefinition { Id = ArtifactRules.ItemId(set, ArtifactSlot.Flower), Name = ArtifactRules.SetName(set),
                    Kind = ItemKind.Artifact, Tier = (int)HighestArtifact(difficulty),
                    Description = "Any of this set's five slots can drop. Each obtained artifact has its own stat rolls; inspect it after victory for exact stats." });
            return items.Where(item => item != null).OrderByDescending(item => item.FrameTier)
                .ThenBy(item => RewardOrder(item.Kind)).ThenBy(item => item.Name, StringComparer.Ordinal).ToList();
        }

        public static List<ItemDefinition> Preview(RunState run, RouteNodeState node)
        {
            if (node == null || !IsBattle(node.Type)) return new List<ItemDefinition>();
            return Preview(new DungeonProfile { Phase = run.RewardPhase, SubPhase = run.RewardSubPhase,
                Mode = node.Type == RouteNodeType.Boss ? run.Mode : DungeonMode.Route },
                node.EnemyTeamSnapshot.Select(e => e.Definition?.SeriesId), run.DifficultyBonusPercent);
        }

        public static DungeonRewardReceipt Victory(RunState run, RouteNodeState node)
        {
            if (!IsBattle(node.Type)) throw new ArgumentException("Victory rewards require a battle stage.");
            var reward = new DungeonRewardReceipt { Id = run.RunId + ":reward:" + node.Id, NodeId = node.Id, Mode = run.Mode };
            var multiplier = Multiplier(node.Type);
            void Add(string id, long amount) => reward.Items.Add(new ItemAmount { ItemId = id, Amount = amount });
            int Material(string purpose) => (1 + Roll(run, node, purpose, MaterialMaximum(run.DifficultyBonusPercent))) * multiplier;
            Add(ItemCatalog.Diamond, CurrencyAmount(ItemCatalog.Diamond, run.DifficultyBonusPercent, node.Type));
            Add(ItemCatalog.Coin, CurrencyAmount(ItemCatalog.Coin, run.DifficultyBonusPercent, node.Type));
            Add(ItemCatalog.PhaseId(Math.Max(1, run.RewardPhase)), Material("phase-amount"));
            Add(ItemCatalog.SubPhaseId(run.RewardSubPhase), Material("subphase-amount"));
            Add(ItemCatalog.ArtifactDust, Material("dust-amount"));
            var series = node.EnemyTeamSnapshot.Select(e => e.Definition?.SeriesId).Where(s => !string.IsNullOrEmpty(s))
                .Distinct().OrderBy(s => s, StringComparer.Ordinal).ToList();
            if (series.Count > 0)
            {
                var selected = series[Roll(run, node, "series", series.Count)];
                Add(ItemCatalog.SeriesId(selected), Material("series-amount"));
                if (run.Mode == DungeonMode.SeriesChallengeBoss && node.Type == RouteNodeType.Boss)
                    Add(ItemCatalog.SeriesId(selected, true), Material("series-premium-amount"));
            }
            if (Roll(run, node, "artifact-drop", 100) < ArtifactChancePercent(run.DifficultyBonusPercent))
            {
                var rarity = (ArtifactRarity)(1 + Roll(run, node, "artifact-rarity", 100) * (int)HighestArtifact(run.DifficultyBonusPercent) / 100);
                var artifact = ArtifactRules.Generate(reward.Id + ":artifact:0", rarity);
                var basket = ArtifactBasket(run.RewardPhase, run.RewardSubPhase);
                artifact.Set = basket[Roll(run, node, "artifact-set", basket.Length)];
                artifact.ItemId = ArtifactRules.ItemId(artifact.Set, artifact.Slot);
                artifact.SourceRunId = run.RunId; artifact.SourceNodeId = node.Id;
                reward.Artifacts.Add(artifact);
            }
            return reward;
        }

        private static int Roll(RunState run, RouteNodeState node, string purpose, int limit)
        {
            using (var hash = SHA256.Create())
            {
                var bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(run.Seed + ":" + node.Id + ":" + purpose));
                var value = (uint)bytes[0] | (uint)bytes[1] << 8 | (uint)bytes[2] << 16 | (uint)bytes[3] << 24;
                return (int)(value % (uint)limit);
            }
        }
    }
    [Serializable]
    public sealed class ItemInventory
    {
        public long Coins;
        public List<ItemAmount> Materials = new List<ItemAmount>();
        public List<ArtifactInstance> Artifacts = new List<ArtifactInstance>();
        public List<string> ClaimedReceiptIds = new List<string>();
        public long Amount(string id) => id == ItemCatalog.Coin ? Coins :
            id.StartsWith("artifact.") ? Artifacts.LongCount(a => a.ItemId == id) : Materials.Where(i => i.ItemId == id).Sum(i => i.Amount);
        public ItemInventory Clone() => new ItemInventory { Coins = Coins, Materials = Materials.ConvertAll(i => i.Clone()),
            Artifacts = Artifacts.ConvertAll(a => a.Clone()), ClaimedReceiptIds = new List<string>(ClaimedReceiptIds) };

        public bool TrySpend(IEnumerable<ItemAmount> costs, UpgradeResourceUse use, out ItemInventory next, out string error)
        {
            next = null; error = null;
            var totals = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var cost in costs ?? Enumerable.Empty<ItemAmount>())
            {
                var definition = ItemCatalog.Find(cost?.ItemId);
                if (cost == null || cost.Amount <= 0 || definition == null || definition.Kind == ItemKind.Diamond || definition.Kind == ItemKind.Artifact ||
                    definition.Kind == ItemKind.SeriesPremiumMaterial && use != UpgradeResourceUse.HolyRelic && use != UpgradeResourceUse.External ||
                    (definition.Kind == ItemKind.PhaseMaterial || definition.Kind == ItemKind.SubPhaseMaterial || definition.Kind == ItemKind.SeriesMaterial) && use != UpgradeResourceUse.CharacterUpgrade ||
                    definition.Kind == ItemKind.ArtifactMaterial && use != UpgradeResourceUse.ArtifactUpgrade)
                { error = "This resource cannot be used for that upgrade."; return false; }
                totals.TryGetValue(cost.ItemId, out var total); totals[cost.ItemId] = checked(total + cost.Amount);
            }
            if (totals.Count == 0) { error = "Upgrade costs are required."; return false; }
            foreach (var cost in totals)
                if (Amount(cost.Key) < cost.Value) { error = "Not enough " + ItemCatalog.Find(cost.Key).Name + "."; return false; }
            var copy = Clone();
            foreach (var cost in totals)
                if (cost.Key == ItemCatalog.Coin) copy.Coins -= cost.Value;
                else
                {
                    // Imported/editor profiles may contain multiple stacks of the same item.
                    var remaining = cost.Value;
                    foreach (var stack in copy.Materials.Where(i => i.ItemId == cost.Key))
                    {
                        var deducted = Math.Min(Math.Max(0, stack.Amount), remaining);
                        stack.Amount -= deducted; remaining -= deducted;
                        if (remaining == 0) break;
                    }
                }
            next = copy; return true;
        }

        public bool TryGrant(DungeonRewardReceipt reward, out ItemInventory next)
        {
            next = null;
            if (reward == null || string.IsNullOrWhiteSpace(reward.Id)) throw new ArgumentException("A reward receipt is required.");
            if (reward.CompletionBonus) return false;
            if (ClaimedReceiptIds.Contains(reward.Id)) return false;
            var copy = Clone();
            foreach (var item in reward.Items)
            {
                var definition = ItemCatalog.Find(item.ItemId);
                if (item.Amount < 0 || definition == null || definition.Kind == ItemKind.Artifact ||
                    definition.Kind == ItemKind.SeriesPremiumMaterial && reward.Mode != DungeonMode.SeriesChallengeBoss)
                    throw new ArgumentException("Invalid item reward: " + item.ItemId);
                if (item.ItemId == ItemCatalog.Diamond) continue; // Existing Gacha wallet owns Diamonds.
                if (item.ItemId == ItemCatalog.Coin) copy.Coins = checked(copy.Coins + item.Amount);
                else
                {
                    var stack = copy.Materials.Find(i => i.ItemId == item.ItemId);
                    if (stack == null) copy.Materials.Add(new ItemAmount { ItemId = item.ItemId, Amount = item.Amount });
                    else stack.Amount = checked(stack.Amount + item.Amount);
                }
            }
            foreach (var artifact in reward.Artifacts)
            {
                if (artifact == null || string.IsNullOrEmpty(artifact.InstanceId) ||
                    ItemCatalog.Find(artifact.ItemId)?.Kind != ItemKind.Artifact || copy.Artifacts.Exists(a => a.InstanceId == artifact.InstanceId))
                    throw new ArgumentException("Invalid or duplicate artifact instance.");
                var added = artifact.Clone(); ArtifactRules.Normalize(added);
                added.EquippedCharacterId = null;
                copy.Artifacts.Add(added);
            }
            copy.ClaimedReceiptIds.Add(reward.Id); next = copy; return true;
        }
    }
}
