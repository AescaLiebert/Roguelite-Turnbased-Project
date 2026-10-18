using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Run;

namespace FightingAllstar.Core.Content
{
    public enum ArtifactSet { Striker, Bulwark, Lifebloom }
    public enum ArtifactSlot { Flower, Feather, SandClock, Goblet, Crown }

    [Serializable]
    public sealed class ArtifactStatLine
    {
        public StatId Stat;
        public bool Percent;
        public int Value;
        public ArtifactStatLine Clone() => (ArtifactStatLine)MemberwiseClone();
    }

    [Serializable]
    public sealed class ArtifactAffixImprovement
    {
        public int Level, SubstatIndex, BeforeValue, AfterValue;
    }

    [Serializable]
    public sealed class ArtifactEnhancementPreview
    {
        public ArtifactInstance TargetBefore, TargetAfter;
        public long ExperienceGained, CoinCost, DustAmount, ExcessExperience;
        public List<string> FodderIds = new List<string>();
        public List<int> Milestones = new List<int>();
        public List<ArtifactAffixImprovement> AffixImprovements = new List<ArtifactAffixImprovement>();
    }

    /// <summary>
    /// Artifact identity, deterministic rolls, equipment and guaranteed enhancement.
    /// Main and substat tables follow the authored artifact balance table; economy values remain starting values.
    /// </summary>
    public static class ArtifactRules
    {
        public const int MaxLevel = 20, CurrentSchema = 2, DustXp = 100;
        public const int MinimumQualityBp = 7000, MaximumQualityBp = 10000;
        private static readonly string[] SetIds = { "striker", "bulwark", "lifebloom" };
        private static readonly string[] SlotIds = { "flower", "feather", "sandclock", "goblet", "crown" };
        private static readonly string[] SlotNames = { "Flower", "Feather", "Sand Clock", "Goblet", "Crown" };
        private static readonly int[] FodderBase = { 0, 200, 400, 800, 1400, 2200 };
        private static readonly (StatId Stat, int Minimum, int Maximum)[] MainPool =
        {
            (StatId.MaxHealth, 700, 4660), (StatId.Defense, 870, 5830), (StatId.Attack, 700, 4660),
            (StatId.Pierce, 870, 5830), (StatId.Resistance, 470, 3110), (StatId.Regeneration, 470, 3110),
            (StatId.CritChance, 470, 3110), (StatId.CritDamage, 930, 6220), (StatId.CritResistance, 470, 3110),
            (StatId.CritDefense, 930, 6220), (StatId.Recovery, 470, 3110), (StatId.LifeSteal, 470, 3110),
            (StatId.BlockChance, 470, 3110), (StatId.BlockPower, 930, 6220),
            (StatId.DamageOverTimeDealt, 700, 4660), (StatId.DamageDealt, 700, 4660)
        };
        private static readonly (StatId Stat, bool Percent, int Maximum)[] SubstatPool =
        {
            (StatId.MaxHealth, false, 298), (StatId.Attack, false, 20), (StatId.Defense, false, 23),
            (StatId.MaxHealth, true, 583), (StatId.Attack, true, 583), (StatId.Defense, true, 729),
            (StatId.Pierce, true, 512), (StatId.Regeneration, true, 389), (StatId.CritChance, true, 389),
            (StatId.CritDamage, true, 777), (StatId.CritResistance, true, 389), (StatId.CritDefense, true, 777),
            (StatId.Recovery, true, 389), (StatId.LifeSteal, true, 389), (StatId.BlockChance, true, 389),
            (StatId.BlockPower, true, 777), (StatId.DamageOverTimeDealt, true, 512),
            (StatId.DamageDealt, true, 512), (StatId.AdditionalDamageDealt, true, 512)
        };

        public static bool IsBasic(StatId stat) => stat == StatId.Attack || stat == StatId.Defense || stat == StatId.MaxHealth;
        public static string ItemId(ArtifactSet set, ArtifactSlot slot) => "artifact." + SetIds[(int)set] + "." + SlotIds[(int)slot];
        public static string SetName(ArtifactSet set) => set == ArtifactSet.Striker ? "Striker’s Oath" : set == ArtifactSet.Bulwark ? "Bulwark’s Oath" : "Lifebloom’s Oath";
        public static string SlotName(ArtifactSlot slot) => SlotNames[(int)slot];
        public static string Name(ArtifactInstance artifact) => artifact == null ? "Artifact" :
            (artifact.Set == ArtifactSet.Striker ? "Striker’s " : artifact.Set == ArtifactSet.Bulwark ? "Bulwark’s " : "Lifebloom’s ") +
            (artifact.Slot == ArtifactSlot.Flower ? "Bloom" : SlotName(artifact.Slot));
        public static string Description(ArtifactSet set) => set == ArtifactSet.Striker ?
            "2 pieces: ATK +20%.\n4 pieces: ATK +20%. Below 20% HP: Life Steal +5% and Damage Dealt +5%." : set == ArtifactSet.Bulwark ?
            "2 pieces: DEF +20%.\n4 pieces: DEF +20%. Below 20% HP: Critical Resistance +15% and Critical Defense +15%." :
            "2 pieces: Max HP +20%.\n4 pieces: Max HP +20%. Below 20% HP: Regeneration +10% and Recovery +10%.";

        public static string StatName(ArtifactStatLine line)
        {
            if (line == null) return "—";
            var name = line.Stat switch
            {
                StatId.Attack => "ATK", StatId.Defense => "DEF", StatId.MaxHealth => "Max HP",
                StatId.CombatClass => "Combat Class", StatId.CritChance => "Crit Chance", StatId.CritDamage => "Crit Damage",
                StatId.CritResistance => "Crit Resistance", StatId.CritDefense => "Crit Defense", StatId.BlockChance => "Block Chance",
                StatId.BlockPower => "Block Power", StatId.LifeSteal => "Life Steal", StatId.ReflectDamage => "Reflect Damage",
                StatId.Pierce => "Pierce", StatId.Resistance => "Resistance", StatId.Regeneration => "Regeneration",
                StatId.Recovery => "Recovery Rate", StatId.DamageOverTimeDealt => "DOT Dealt", StatId.DamageDealt => "Damage Dealt",
                StatId.AdditionalDamageDealt => "Additional Damage",
                _ => line.Stat.ToString()
            };
            return name + (IsBasic(line.Stat) && line.Percent ? "%" : "");
        }
        public static string Format(ArtifactStatLine line) => line == null ? "—" : line.Percent ?
            (line.Value / 100.0).ToString("0.0#", CultureInfo.InvariantCulture) + "%" : line.Value.ToString("N0", CultureInfo.InvariantCulture);

        public static bool TryIdentity(string id, out ArtifactSet set, out ArtifactSlot slot)
        {
            for (var s = 0; s < 3; s++) for (var p = 0; p < 5; p++)
                if (id == ItemId((ArtifactSet)s, (ArtifactSlot)p)) { set = (ArtifactSet)s; slot = (ArtifactSlot)p; return true; }
            set = default; slot = default; return false;
        }

        /// <summary>Flower/Feather draw basic percentages; Sand Clock/Goblet/Crown also draw the authored secondary rates.</summary>
        public static ArtifactInstance Generate(string instanceId, ArtifactRarity rarity, int seed = 0)
        {
            var artifact = new ArtifactInstance { InstanceId = instanceId, Rarity = ClampRarity(rarity), Seed = seed == 0 ? Hash(instanceId ?? "artifact") : seed };
            artifact.Set = (ArtifactSet)Roll(artifact.Seed, "set", 3);
            artifact.Slot = (ArtifactSlot)Roll(artifact.Seed, "slot", 5);
            artifact.ItemId = ItemId(artifact.Set, artifact.Slot);
            GenerateLines(artifact);
            artifact.SchemaVersion = CurrentSchema;
            return artifact;
        }

        /// <summary>Migrates old rarity-only instances without changing their unique receipt identity.</summary>
        public static void Normalize(ArtifactInstance artifact)
        {
            if (artifact == null) return;
            artifact.Rarity = ClampRarity(artifact.Rarity);
            artifact.Level = Math.Max(0, Math.Min(MaxLevel, artifact.Level));
            artifact.Experience = Math.Max(0, artifact.Experience);
            if (artifact.Seed == 0) artifact.Seed = Hash(artifact.InstanceId ?? artifact.ItemId ?? "artifact");
            if (TryIdentity(artifact.ItemId, out var set, out var slot)) { artifact.Set = set; artifact.Slot = slot; }
            else if (artifact.SchemaVersion < CurrentSchema)
            {
                artifact.Set = (ArtifactSet)Roll(artifact.Seed, "set", 3);
                artifact.Slot = (ArtifactSlot)Roll(artifact.Seed, "slot", 5);
            }
            artifact.Set = (ArtifactSet)Math.Max(0, Math.Min(2, (int)artifact.Set));
            artifact.Slot = (ArtifactSlot)Math.Max(0, Math.Min(4, (int)artifact.Slot));
            artifact.ItemId = ItemId(artifact.Set, artifact.Slot);
            RepairLines(artifact, artifact.SchemaVersion < CurrentSchema);
            artifact.MainStat.Value = MainValue(artifact);
            artifact.SchemaVersion = CurrentSchema;
            if (artifact.Level == MaxLevel) artifact.Experience = 0;
            else artifact.Experience = Math.Min(artifact.Experience, RequiredXp(artifact, artifact.Level) - 1);
        }

        public static int MainValue(ArtifactInstance artifact)
        {
            if (artifact?.MainStat == null || !artifact.MainStat.Percent ||
                !TryMainStatRange(artifact.Slot, artifact.MainStat.Stat, out var minimum, out var maximum)) return 0;
            return minimum + (maximum - minimum) * Math.Max(0, Math.Min(MaxLevel, artifact.Level)) / MaxLevel;
        }

        /// <summary>Authored percentage endpoints in basis points; main stats have no quality or rarity multiplier.</summary>
        public static bool TryMainStatRange(ArtifactSlot slot, StatId stat, out int minimum, out int maximum)
        {
            minimum = maximum = 0;
            if (!Enum.IsDefined(typeof(ArtifactSlot), slot)) return false;
            foreach (var rule in MainPool.Take(slot == ArtifactSlot.Flower || slot == ArtifactSlot.Feather ? 3 : MainPool.Length))
                if (rule.Stat == stat) { minimum = rule.Minimum; maximum = rule.Maximum; return true; }
            return false;
        }

        /// <summary>Authored SSR base for one substat roll; zero means the exact identity is outside the pool.</summary>
        public static int SubstatBaseValue(ArtifactStatLine line)
        {
            if (line == null) return 0;
            foreach (var rule in SubstatPool)
                if (rule.Stat == line.Stat && rule.Percent == line.Percent) return rule.Maximum;
            return 0;
        }

        /// <summary>Applies the supplied quality (70% to 100%) and rarity (one to five stars) to one authored roll.</summary>
        public static int SubstatRollValue(ArtifactStatLine line, ArtifactRarity rarity, int qualityBp) =>
            (int)((long)SubstatBaseValue(line) * Math.Max(MinimumQualityBp, Math.Min(MaximumQualityBp, qualityBp)) *
                (int)ClampRarity(rarity) / (MaximumQualityBp * 5));

        public static long RequiredXp(ArtifactInstance artifact, int level) => RequiredXp(artifact?.Rarity ?? ArtifactRarity.Common, level);
        public static long RequiredXp(ArtifactRarity rarity, int level) => Math.Max(1, (int)ClampRarity(rarity)) * (100L + 30L * Math.Max(0, Math.Min(MaxLevel - 1, level)));
        public static long InvestedXp(ArtifactInstance artifact)
        {
            if (artifact == null) return 0;
            var level = Math.Max(0, Math.Min(MaxLevel, artifact.Level));
            long total = level == MaxLevel ? 0 : Math.Min(Math.Max(0, artifact.Experience), RequiredXp(artifact, level) - 1);
            for (var i = 0; i < level; i++) total += RequiredXp(artifact, i);
            return total;
        }
        public static long FodderXp(ArtifactInstance artifact) => artifact == null ? 0 : FodderBase[(int)ClampRarity(artifact.Rarity)] + InvestedXp(artifact) * 4 / 5;
        public static long RemainingXp(ArtifactInstance artifact)
        {
            if (artifact == null || artifact.Level >= MaxLevel) return 0;
            var level = Math.Max(0, artifact.Level);
            long amount = -Math.Min(Math.Max(0, artifact.Experience), RequiredXp(artifact, level) - 1);
            for (var i = level; i < MaxLevel; i++) amount += RequiredXp(artifact, i);
            return Math.Max(0, amount);
        }

        public static bool CanUseAsFodder(ArtifactInstance artifact, string targetId = null) => artifact != null &&
            artifact.InstanceId != targetId && !artifact.Favourite && string.IsNullOrEmpty(artifact.EquippedCharacterId);

        public static bool PreviewEnhance(ItemInventory inventory, string targetId, long dustAmount, IEnumerable<string> fodderIds,
            out ArtifactEnhancementPreview preview, out string error)
        {
            preview = null; error = null;
            var target = inventory?.Artifacts?.Find(a => a != null && a.InstanceId == targetId);
            if (target == null) { error = "The target artifact is no longer in your backpack."; return false; }
            var before = target.Clone(); Normalize(before);
            if (before.Level >= MaxLevel) { error = "This artifact is already at +20."; return false; }
            if (dustAmount < 0) { error = "Artifact Dust amount cannot be negative."; return false; }
            if (dustAmount > inventory.Amount(ItemCatalog.ArtifactDust)) { error = "Not enough Artifact Dust."; return false; }
            var ids = (fodderIds ?? Enumerable.Empty<string>()).ToList();
            if (ids.Any(string.IsNullOrEmpty) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
            { error = "Each source artifact can only be selected once."; return false; }
            long xp;
            try
            {
                xp = checked(dustAmount * DustXp);
                foreach (var id in ids)
                {
                    var source = inventory.Artifacts.Find(a => a != null && a.InstanceId == id);
                    if (source == null) { error = "A selected source artifact is no longer available."; return false; }
                    if (!CanUseAsFodder(source, targetId))
                    { error = source.InstanceId == targetId ? "An artifact cannot consume itself." : source.Favourite ? Name(source) + " is a Favourite. Unfavourite it before using it as a material." : Name(source) + " is equipped. Unequip it before using it as a material."; return false; }
                    var normalized = source.Clone(); Normalize(normalized);
                    xp = checked(xp + FodderXp(normalized));
                }
                if (xp <= 0) { error = "Select Artifact Dust or an eligible artifact."; return false; }
                var usedXp = Math.Min(xp, RemainingXp(before));
                var coinCost = checked(usedXp * 2);
                var after = before.Clone();
                var pending = checked(after.Experience + xp);
                preview = new ArtifactEnhancementPreview { TargetBefore = before, TargetAfter = after,
                    ExperienceGained = xp, CoinCost = coinCost, DustAmount = dustAmount, FodderIds = ids };
                while (after.Level < MaxLevel && pending >= RequiredXp(after, after.Level))
                {
                    pending -= RequiredXp(after, after.Level); after.Level++;
                    if (after.Level % 6 == 0 && after.Substats.Count > 0)
                    {
                        preview.AffixImprovements.Add(ApplyMilestone(after, after.Level));
                        preview.Milestones.Add(after.Level);
                    }
                }
                after.Experience = after.Level == MaxLevel ? 0 : pending;
                preview.ExcessExperience = after.Level == MaxLevel ? pending : 0;
                after.MainStat.Value = MainValue(after);
                if (inventory.Coins < coinCost) { error = "Not enough Coins. Required: " + coinCost.ToString("N0") + "."; return false; }
                return true;
            }
            catch (OverflowException) { preview = null; error = "The selected material amount is too large."; return false; }
        }

        public static bool TryEnhance(ItemInventory inventory, string targetId, long dustAmount, IEnumerable<string> fodderIds,
            out ItemInventory next, out ArtifactEnhancementPreview preview, out string error)
        {
            next = null;
            if (!PreviewEnhance(inventory, targetId, dustAmount, fodderIds, out preview, out error)) return false;
            if (SpendEnhancement(inventory, preview, out next, out error)) return true;
            preview = null;
            return false;
        }

        /// <summary>Revalidates the reviewed roll and costs before consuming anything; previews cannot reroll an artifact.</summary>
        public static bool TryEnhance(ItemInventory inventory, ArtifactEnhancementPreview reviewed,
            out ItemInventory next, out ArtifactEnhancementPreview preview, out string error)
        {
            next = null; preview = null;
            if (reviewed?.TargetBefore == null)
            { error = "Review an artifact enhancement before confirming."; return false; }
            if (!PreviewEnhance(inventory, reviewed.TargetBefore.InstanceId, reviewed.DustAmount,
                reviewed.FodderIds, out var current, out error)) return false;
            if (!SamePreview(reviewed, current))
            { error = "The artifact or selected materials changed. Review enhancement again."; return false; }
            if (!SpendEnhancement(inventory, current, out next, out error)) return false;
            preview = current;
            return true;
        }

        private static bool SpendEnhancement(ItemInventory inventory, ArtifactEnhancementPreview preview,
            out ItemInventory next, out string error)
        {
            next = null;
            var costs = new List<ItemAmount> { new ItemAmount { ItemId = ItemCatalog.Coin, Amount = preview.CoinCost } };
            if (preview.DustAmount > 0) costs.Add(new ItemAmount { ItemId = ItemCatalog.ArtifactDust, Amount = preview.DustAmount });
            if (!inventory.TrySpend(costs, UpgradeResourceUse.ArtifactUpgrade, out var copy, out error)) return false;
            var consumedIds = preview.FodderIds;
            copy.Artifacts.RemoveAll(a => consumedIds.Contains(a.InstanceId));
            var targetIndex = copy.Artifacts.FindIndex(a => a.InstanceId == preview.TargetBefore.InstanceId);
            copy.Artifacts[targetIndex] = preview.TargetAfter.Clone();
            next = copy;
            return true;
        }

        private static bool SamePreview(ArtifactEnhancementPreview first, ArtifactEnhancementPreview second) =>
            SameArtifact(first.TargetBefore, second.TargetBefore) && SameArtifact(first.TargetAfter, second.TargetAfter) &&
            first.ExperienceGained == second.ExperienceGained && first.CoinCost == second.CoinCost &&
            first.DustAmount == second.DustAmount && first.ExcessExperience == second.ExcessExperience &&
            first.FodderIds != null && first.FodderIds.SequenceEqual(second.FodderIds) &&
            first.Milestones != null && first.Milestones.SequenceEqual(second.Milestones) &&
            first.AffixImprovements != null && first.AffixImprovements.Count == second.AffixImprovements.Count &&
            first.AffixImprovements.Zip(second.AffixImprovements, (a, b) => a != null && a.Level == b.Level &&
                a.SubstatIndex == b.SubstatIndex && a.BeforeValue == b.BeforeValue && a.AfterValue == b.AfterValue).All(equal => equal);

        private static bool SameArtifact(ArtifactInstance first, ArtifactInstance second) =>
            first != null && second != null && first.InstanceId == second.InstanceId && first.ItemId == second.ItemId &&
            first.SourceRunId == second.SourceRunId && first.SourceNodeId == second.SourceNodeId &&
            first.Rarity == second.Rarity && first.SchemaVersion == second.SchemaVersion && first.Seed == second.Seed &&
            first.Level == second.Level && first.Experience == second.Experience && first.Set == second.Set && first.Slot == second.Slot &&
            first.Favourite == second.Favourite && first.EquippedCharacterId == second.EquippedCharacterId &&
            SameLine(first.MainStat, second.MainStat) && first.Substats != null && second.Substats != null &&
            first.Substats.Count == second.Substats.Count && first.Substats.Zip(second.Substats, SameLine).All(equal => equal);

        private static bool SameLine(ArtifactStatLine first, ArtifactStatLine second) => first != null && second != null &&
            first.Stat == second.Stat && first.Percent == second.Percent && first.Value == second.Value;

        /// <summary>Percentage basic lines and both set thresholds add against the unequipped base stat.</summary>
        public static StatBlock ApplyStats(StatBlock baseStats, IEnumerable<ArtifactInstance> equipped)
        {
            var baseline = baseStats ?? new StatBlock();
            var result = baseline.Clone();
            var artifacts = ValidLoadout(equipped);
            var flat = new Dictionary<StatId, long>(); var percent = new Dictionary<StatId, long>();
            foreach (var artifact in artifacts)
            {
                foreach (var line in new[] { artifact.MainStat }.Concat(artifact.Substats))
                {
                    if (line == null) continue;
                    var dict = IsBasic(line.Stat) && line.Percent ? percent : flat;
                    dict.TryGetValue(line.Stat, out var value); dict[line.Stat] = value + line.Value;
                }
            }
            foreach (ArtifactSet set in Enum.GetValues(typeof(ArtifactSet)))
            {
                var count = SetCount(artifacts, set);
                var stat = set == ArtifactSet.Striker ? StatId.Attack : set == ArtifactSet.Bulwark ? StatId.Defense : StatId.MaxHealth;
                percent.TryGetValue(stat, out var value); percent[stat] = value + (count >= 2 ? 2000 : 0) + (count >= 4 ? 2000 : 0);
            }
            foreach (StatId stat in Enum.GetValues(typeof(StatId)))
            {
                flat.TryGetValue(stat, out var additional); percent.TryGetValue(stat, out var percentValue);
                var value = new BigInteger(baseline.Get(stat)) + additional + new BigInteger(baseline.Get(stat)) * percentValue / 10000;
                result.Set(stat, (int)BigInteger.Max(BigInteger.Zero, BigInteger.Min(int.MaxValue, value)));
            }
            if (result.HasDerivedCombatClass)
            {
                flat.TryGetValue(StatId.CombatClass, out var explicitBonus);
                result.CombatClassFlatBonus = (int)Math.Min(int.MaxValue, (long)baseline.CombatClassFlatBonus + explicitBonus);
                CombatClassRules.Recalculate(result);
            }
            return result;
        }

        public static int SetCount(IEnumerable<ArtifactInstance> equipped, ArtifactSet set) =>
            ValidLoadout(equipped).Count(a => a.Set == set);

        public static List<ArtifactSet> ActiveSets(FighterState fighter)
        {
            if (fighter == null) return new List<ArtifactSet>();
            return ActiveSets(fighter.Definition?.ArtifactLoadout, fighter.Health, StatusSystem.GetEffectiveStats(fighter).MaxHealth, fighter.IsAlive);
        }
        public static List<ArtifactSet> ActiveSets(IEnumerable<ArtifactInstance> equipped, int health, int maxHealth, bool alive = true)
        {
            var sets = new List<ArtifactSet>();
            if (!alive || health <= 0 || maxHealth <= 0 || (long)health * 5 >= maxHealth) return sets;
            foreach (ArtifactSet set in Enum.GetValues(typeof(ArtifactSet))) if (SetCount(equipped, set) >= 4) sets.Add(set);
            return sets;
        }

        /// <summary>Evaluated after ordinary status modifiers so the threshold follows live max HP.</summary>
        public static void ApplyConditionalStats(FighterState fighter, StatBlock effective)
        {
            foreach (var set in ActiveSets(fighter?.Definition?.ArtifactLoadout, fighter?.Health ?? 0, effective.MaxHealth, fighter?.IsAlive ?? false))
            {
                if (set == ArtifactSet.Striker) Add(StatId.LifeSteal, 500);
                else if (set == ArtifactSet.Bulwark) { Add(StatId.CritResistance, 1500); Add(StatId.CritDefense, 1500); }
                else { Add(StatId.Regeneration, 1000); Add(StatId.Recovery, 1000); }
            }
            void Add(StatId stat, int amount) => effective.Set(stat, (int)Math.Min(int.MaxValue, (long)effective.Get(stat) + amount));
        }

        private static List<ArtifactInstance> ValidLoadout(IEnumerable<ArtifactInstance> equipped) =>
            (equipped ?? Enumerable.Empty<ArtifactInstance>()).Where(a => a != null).GroupBy(a => a.Slot).Select(g => g.First()).ToList();
        private static ArtifactRarity ClampRarity(ArtifactRarity rarity) => (ArtifactRarity)Math.Max(1, Math.Min(5, (int)rarity));
        private static string Identity(ArtifactStatLine line) => line.Stat + ":" + line.Percent;
        private static void GenerateLines(ArtifactInstance artifact)
        {
            artifact.MainStat = GenerateMainLine(artifact);
            artifact.MainStat.Value = MainValue(artifact);
            artifact.Substats = new List<ArtifactStatLine>();
            RepairLines(artifact, true);
        }
        private static ArtifactStatLine GenerateMainLine(ArtifactInstance artifact)
        {
            var count = artifact.Slot == ArtifactSlot.Flower || artifact.Slot == ArtifactSlot.Feather ? 3 : MainPool.Length;
            return new ArtifactStatLine { Stat = MainPool[Roll(artifact.Seed, "main", count)].Stat, Percent = true };
        }
        private static int QualityValue(ArtifactInstance artifact, ArtifactStatLine line, string purpose)
        {
            // A quality roll is uniform from 70.00% through 100.00%, inclusive.
            // Apply quality and rarity together before the single integer floor, including flat stats.
            var quality = MinimumQualityBp + Roll(artifact.Seed, "quality:" + purpose, MaximumQualityBp - MinimumQualityBp + 1);
            return SubstatRollValue(line, artifact.Rarity, quality);
        }

        /// <summary>Migrates authored balance once, then repairs imported data without rerolling valid current-schema lines.</summary>
        private static void RepairLines(ArtifactInstance artifact, bool migrate)
        {
            // Old flat basic mains keep their stat identity and adopt the percentage form in the new table.
            if (artifact.MainStat != null && IsBasic(artifact.MainStat.Stat)) artifact.MainStat.Percent = true;
            if (artifact.MainStat == null || !artifact.MainStat.Percent ||
                !TryMainStatRange(artifact.Slot, artifact.MainStat.Stat, out _, out _)) artifact.MainStat = GenerateMainLine(artifact);
            var count = (int)artifact.Rarity - 1;
            var existing = artifact.Substats ?? new List<ArtifactStatLine>();
            var repaired = new HashSet<int>();
            var seen = new HashSet<string>(StringComparer.Ordinal) { Identity(artifact.MainStat) };
            var compact = new List<ArtifactStatLine>();
            for (var index = 0; index < count; index++)
            {
                var line = index < existing.Count ? existing[index] : null;
                if (SubstatBaseValue(line) == 0 || !seen.Add(Identity(line)))
                { compact.Add(null); repaired.Add(index); }
                else compact.Add(line);
            }
            var pool = SubstatPool.Select(rule => new ArtifactStatLine { Stat = rule.Stat, Percent = rule.Percent }).ToList();
            pool.RemoveAll(line => seen.Contains(Identity(line)));
            foreach (var index in repaired.OrderBy(i => i))
            {
                var selected = Roll(artifact.Seed, "sub-identity:" + index, pool.Count);
                var line = pool[selected]; pool.RemoveAt(selected);
                line.Value = QualityValue(artifact, line, "initial:" + index);
                compact[index] = line;
            }
            artifact.Substats = compact;
            for (var index = 0; index < count; index++)
            {
                var line = compact[index];
                var rolls = 1;
                if (migrate || !repaired.Contains(index) && line.Value <= 0)
                { line.Value = QualityValue(artifact, line, "initial:" + index); repaired.Add(index); }
                for (var milestone = 6; milestone <= artifact.Level; milestone += 6)
                    if (Roll(artifact.Seed, "milestone-line:" + milestone, count) == index)
                    {
                        rolls++;
                        if (repaired.Contains(index)) line.Value += QualityValue(artifact, line, "milestone:" + milestone);
                    }
                var minimum = SubstatRollValue(line, artifact.Rarity, MinimumQualityBp) * rolls;
                var maximum = SubstatRollValue(line, artifact.Rarity, MaximumQualityBp) * rolls;
                line.Value = Math.Max(minimum, Math.Min(maximum, line.Value));
            }
        }
        private static ArtifactAffixImprovement ApplyMilestone(ArtifactInstance artifact, int level)
        {
            // Both the selected line and its gain depend only on this artifact and milestone.
            // Changing draft materials, reopening the workshop or splitting upgrades cannot fish for a better roll.
            var index = Roll(artifact.Seed, "milestone-line:" + level, artifact.Substats.Count);
            var line = artifact.Substats[index];
            var before = line.Value;
            line.Value = (int)Math.Min(int.MaxValue, (long)line.Value + QualityValue(artifact, line, "milestone:" + level));
            return new ArtifactAffixImprovement { Level = level, SubstatIndex = index, BeforeValue = before, AfterValue = line.Value };
        }
        private static int Hash(string value)
        {
            using (var algorithm = SHA256.Create())
            {
                var bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(value));
                var result = (int)((uint)bytes[0] | (uint)bytes[1] << 8 | (uint)bytes[2] << 16 | (uint)bytes[3] << 24);
                return result == 0 ? 1 : result;
            }
        }
        private static int Roll(int seed, string purpose, int maximum) => (int)((uint)Hash(seed.ToString(CultureInfo.InvariantCulture) + ":" + purpose) % (uint)maximum);
    }
}
