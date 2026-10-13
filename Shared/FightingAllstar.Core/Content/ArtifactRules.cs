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
    public sealed class ArtifactEnhancementPreview
    {
        public ArtifactInstance TargetBefore, TargetAfter;
        public long ExperienceGained, CoinCost, DustAmount, ExcessExperience;
        public List<string> FodderIds = new List<string>();
        public List<int> Milestones = new List<int>();
    }

    /// <summary>
    /// Artifact identity, deterministic rolls, equipment and guaranteed enhancement.
    /// Economy and roll ranges are starting values; the authored thresholds and set effects are fixed.
    /// </summary>
    public static class ArtifactRules
    {
        public const int MaxLevel = 20, CurrentSchema = 1, DustXp = 100;
        private static readonly string[] SetIds = { "striker", "bulwark", "lifebloom" };
        private static readonly string[] SlotIds = { "flower", "feather", "sandclock", "goblet", "crown" };
        private static readonly string[] SlotNames = { "Flower", "Feather", "Sand Clock", "Goblet", "Crown" };
        private static readonly int[] FodderBase = { 0, 200, 400, 800, 1400, 2200 };

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

        /// <summary>All six basic main forms are initially allowed on each slot.</summary>
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
            RepairLines(artifact);
            artifact.MainStat.Value = MainValue(artifact);
            artifact.SchemaVersion = CurrentSchema;
            if (artifact.Level == MaxLevel) artifact.Experience = 0;
            else artifact.Experience = Math.Min(artifact.Experience, RequiredXp(artifact, artifact.Level) - 1);
        }

        public static int MainValue(ArtifactInstance artifact)
        {
            if (artifact?.MainStat == null) return 0;
            int minimum, maximum;
            if (artifact.MainStat.Percent) { minimum = 600; maximum = 3600; }
            else if (artifact.MainStat.Stat == StatId.Attack) { minimum = 60; maximum = 500; }
            else if (artifact.MainStat.Stat == StatId.Defense) { minimum = 50; maximum = 400; }
            else { minimum = 500; maximum = 3500; }
            var rarity = (int)ClampRarity(artifact.Rarity);
            var numerator = minimum * (long)rarity * MaxLevel + (maximum - minimum) * (long)rarity * Math.Max(0, Math.Min(MaxLevel, artifact.Level));
            return (int)(numerator / (5 * MaxLevel));
        }

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
                    { ApplyMilestone(after, after.Level); preview.Milestones.Add(after.Level); }
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
            var costs = new List<ItemAmount> { new ItemAmount { ItemId = ItemCatalog.Coin, Amount = preview.CoinCost } };
            if (dustAmount > 0) costs.Add(new ItemAmount { ItemId = ItemCatalog.ArtifactDust, Amount = dustAmount });
            if (!inventory.TrySpend(costs, UpgradeResourceUse.ArtifactUpgrade, out var copy, out error)) { preview = null; return false; }
            var consumedIds = preview.FodderIds;
            copy.Artifacts.RemoveAll(a => consumedIds.Contains(a.InstanceId));
            var targetIndex = copy.Artifacts.FindIndex(a => a.InstanceId == targetId);
            copy.Artifacts[targetIndex] = preview.TargetAfter.Clone();
            next = copy;
            return true;
        }

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
            var mainForm = Roll(artifact.Seed, "main", 6);
            artifact.MainStat = new ArtifactStatLine { Stat = (StatId)(mainForm / 2), Percent = mainForm % 2 == 1 };
            artifact.MainStat.Value = MainValue(artifact);
            var pool = new List<ArtifactStatLine>();
            foreach (StatId stat in Enum.GetValues(typeof(StatId)))
            {
                pool.Add(new ArtifactStatLine { Stat = stat, Percent = !IsBasic(stat) && stat != StatId.CombatClass });
                if (IsBasic(stat)) pool.Add(new ArtifactStatLine { Stat = stat, Percent = true });
            }
            artifact.Substats = new List<ArtifactStatLine>();
            for (var i = 0; i < (int)artifact.Rarity - 1; i++)
            {
                var index = Roll(artifact.Seed, "sub-identity:" + i, pool.Count);
                var line = pool[index]; pool.RemoveAt(index);
                line.Value = QualityValue(artifact, line, "initial:" + i);
                artifact.Substats.Add(line);
            }
        }
        private static void QualityRange(ArtifactStatLine line, out int low, out int high)
        {
            if (line.Percent) { low = 100; high = 400; }
            else if (line.Stat == StatId.Attack) { low = 8; high = 25; }
            else if (line.Stat == StatId.Defense) { low = 6; high = 20; }
            else if (line.Stat == StatId.MaxHealth) { low = 75; high = 200; }
            else { low = 20; high = 80; }
        }
        private static int QualityValue(ArtifactInstance artifact, ArtifactStatLine line, string purpose)
        {
            QualityRange(line, out var low, out var high);
            var value = low + Roll(artifact.Seed, "quality:" + purpose, high - low + 1);
            return Math.Max(1, value * (int)artifact.Rarity / 5);
        }

        /// <summary>Repairs damaged imported data without rerolling the valid lines or their upgrades.</summary>
        private static void RepairLines(ArtifactInstance artifact)
        {
            if (artifact.MainStat == null || !IsBasic(artifact.MainStat.Stat))
            {
                var form = Roll(artifact.Seed, "main", 6);
                artifact.MainStat = new ArtifactStatLine { Stat = (StatId)(form / 2), Percent = form % 2 == 1 };
            }
            var count = (int)artifact.Rarity - 1;
            var existing = artifact.Substats ?? new List<ArtifactStatLine>();
            var repaired = new HashSet<int>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var compact = new List<ArtifactStatLine>();
            for (var index = 0; index < count; index++)
            {
                var line = index < existing.Count ? existing[index] : null;
                if (line == null || !Enum.IsDefined(typeof(StatId), line.Stat) ||
                    !IsBasic(line.Stat) && line.Percent != (line.Stat != StatId.CombatClass) ||
                    !seen.Add(Identity(line)))
                { compact.Add(null); repaired.Add(index); }
                else compact.Add(line);
            }
            var pool = new List<ArtifactStatLine>();
            foreach (StatId stat in Enum.GetValues(typeof(StatId)))
            {
                pool.Add(new ArtifactStatLine { Stat = stat, Percent = !IsBasic(stat) && stat != StatId.CombatClass });
                if (IsBasic(stat)) pool.Add(new ArtifactStatLine { Stat = stat, Percent = true });
            }
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
                if (!repaired.Contains(index) && line.Value <= 0)
                { line.Value = QualityValue(artifact, line, "initial:" + index); repaired.Add(index); }
                for (var milestone = 6; milestone <= artifact.Level; milestone += 6)
                    if (Roll(artifact.Seed, "milestone-line:" + milestone, count) == index)
                    {
                        rolls++;
                        if (repaired.Contains(index)) line.Value += QualityValue(artifact, line, "milestone:" + milestone);
                    }
                QualityRange(line, out var minimum, out var maximum);
                minimum = Math.Max(1, minimum * (int)artifact.Rarity / 5) * rolls;
                maximum = Math.Max(1, maximum * (int)artifact.Rarity / 5) * rolls;
                line.Value = Math.Max(minimum, Math.Min(maximum, line.Value));
            }
        }
        private static void ApplyMilestone(ArtifactInstance artifact, int level)
        {
            if (artifact.Substats.Count == 0) return;
            var index = Roll(artifact.Seed, "milestone-line:" + level, artifact.Substats.Count);
            var line = artifact.Substats[index];
            line.Value = (int)Math.Min(int.MaxValue, (long)line.Value + QualityValue(artifact, line, "milestone:" + level));
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
