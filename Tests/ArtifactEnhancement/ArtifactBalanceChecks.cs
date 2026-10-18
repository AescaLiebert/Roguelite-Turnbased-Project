using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;

internal static partial class Program
{
    private static readonly (StatId Stat, int Start, int End)[] ExpectedMains =
    {
        (StatId.MaxHealth, 700, 4660), (StatId.Defense, 870, 5830), (StatId.Attack, 700, 4660),
        (StatId.Pierce, 870, 5830), (StatId.Resistance, 470, 3110), (StatId.Regeneration, 470, 3110),
        (StatId.CritChance, 470, 3110), (StatId.CritDamage, 930, 6220), (StatId.CritResistance, 470, 3110),
        (StatId.CritDefense, 930, 6220), (StatId.Recovery, 470, 3110), (StatId.LifeSteal, 470, 3110),
        (StatId.BlockChance, 470, 3110), (StatId.BlockPower, 930, 6220),
        (StatId.DamageOverTimeDealt, 700, 4660), (StatId.DamageDealt, 700, 4660)
    };
    private static readonly (StatId Stat, bool Percent, int Base)[] ExpectedSubs =
    {
        (StatId.MaxHealth, false, 298), (StatId.Attack, false, 20), (StatId.Defense, false, 23),
        (StatId.MaxHealth, true, 583), (StatId.Attack, true, 583), (StatId.Defense, true, 729),
        (StatId.Pierce, true, 512), (StatId.Regeneration, true, 389), (StatId.CritChance, true, 389),
        (StatId.CritDamage, true, 777), (StatId.CritResistance, true, 389), (StatId.CritDefense, true, 777),
        (StatId.Recovery, true, 389), (StatId.LifeSteal, true, 389), (StatId.BlockChance, true, 389),
        (StatId.BlockPower, true, 777), (StatId.DamageOverTimeDealt, true, 512),
        (StatId.DamageDealt, true, 512), (StatId.AdditionalDamageDealt, true, 512)
    };
    private static string Key(ArtifactStatLine line) => line.Stat + ":" + line.Percent;
    private static void AuthoredBalanceChecks()
    {
        MainTableEndpoints(); SubstatTableAndQuality(); GeneratedPoolAndRarity();
        BalanceMigration(); AppliedArtifactStats(); DamageRateFamilies();
    }
    private static void MainTableEndpoints()
    {
        foreach (ArtifactSlot slot in Enum.GetValues(typeof(ArtifactSlot)))
        foreach (StatId stat in Enum.GetValues(typeof(StatId)))
        {
            var eligible = ExpectedMains.Take(slot == ArtifactSlot.Flower || slot == ArtifactSlot.Feather ? 3 : 16).ToArray();
            var rule = eligible.FirstOrDefault(r => r.Stat == stat);
            var found = ArtifactRules.TryMainStatRange(slot, stat, out var min, out var max);
            Check(found == eligible.Any(r => r.Stat == stat), "Main pool must exactly match authored eligibility for " + slot + "/" + stat);
            if (!found) continue;
            Check(min == rule.Start && max == rule.End, "Main endpoints must match the authored table for " + stat);
            foreach (ArtifactRarity rarity in Enum.GetValues(typeof(ArtifactRarity)))
            {
                var artifact = new ArtifactInstance { Slot = slot, Rarity = rarity, MainStat = new ArtifactStatLine { Stat = stat, Percent = true } };
                for (var level = 0; level <= 20; level++)
                {
                    artifact.Level = level;
                    Check(ArtifactRules.MainValue(artifact) == rule.Start + (rule.End - rule.Start) * level / 20,
                        "Main progression must interpolate +0/+20 exactly without quality or rarity scaling.");
                }
                artifact.MainStat.Percent = false;
                Check(ArtifactRules.MainValue(artifact) == 0, "Flat main forms must not be admitted by the new table.");
            }
        }
        Check(!ArtifactRules.TryMainStatRange((ArtifactSlot)99, StatId.Attack, out _, out _), "Invalid slots cannot have a main pool.");
    }
    private static void SubstatTableAndQuality()
    {
        foreach (StatId stat in Enum.GetValues(typeof(StatId)))
        foreach (var percent in new[] { false, true })
        {
            var line = new ArtifactStatLine { Stat = stat, Percent = percent };
            var expected = ExpectedSubs.FirstOrDefault(s => s.Stat == stat && s.Percent == percent).Base;
            Check(ArtifactRules.SubstatBaseValue(line) == expected, "Substat pool/base must match the authored table for " + Key(line));
            foreach (ArtifactRarity rarity in Enum.GetValues(typeof(ArtifactRarity)))
            foreach (var quality in new[] { 7000, 7001, 8500, 9999, 10000 })
                Check(ArtifactRules.SubstatRollValue(line, rarity, quality) == (long)expected * quality * (int)rarity / 50000,
                    "Roll value must floor base x quality x rarity once for " + Key(line));
        }
        Check(ArtifactRules.SubstatBaseValue(new ArtifactStatLine { Stat = StatId.Resistance, Percent = true }) == 0,
            "Resistance is allowed as a main only.");
        Check(ArtifactRules.SubstatRollValue(new ArtifactStatLine { Stat = StatId.Attack }, ArtifactRarity.Common, 7000) == 2,
            "Flat rolls must not round before applying quality and rarity together.");
    }
    private static void GeneratedPoolAndRarity()
    {
        var seenMains = new Dictionary<ArtifactSlot, HashSet<StatId>>();
        var seenSubs = new HashSet<string>(); var minimumQuality = 10001; var maximumQuality = 0;
        for (var seed = 1; seed <= 768; seed++)
        foreach (ArtifactRarity rarity in Enum.GetValues(typeof(ArtifactRarity)))
        {
            var artifact = ArtifactRules.Generate("table-" + seed, rarity, seed);
            if (!seenMains.TryGetValue(artifact.Slot, out var mains)) seenMains.Add(artifact.Slot, mains = new HashSet<StatId>());
            mains.Add(artifact.MainStat.Stat);
            Check(artifact.MainStat.Percent && ArtifactRules.TryMainStatRange(artifact.Slot, artifact.MainStat.Stat, out _, out _), "Generation produced a forbidden main.");
            Check(artifact.Substats.Count == (int)rarity - 1, "Rarity must retain its authored substat count.");
            Check(artifact.Substats.All(s => Key(s) != Key(artifact.MainStat)) && artifact.Substats.Select(Key).Distinct().Count() == artifact.Substats.Count,
                "An exact main identity or repeated substat may not appear in the compact.");
            for (var i = 0; i < artifact.Substats.Count; i++)
            {
                var line = artifact.Substats[i]; seenSubs.Add(Key(line));
                var low = ArtifactRules.SubstatRollValue(line, rarity, 7000);
                var high = ArtifactRules.SubstatRollValue(line, rarity, 10000);
                Check(ArtifactRules.SubstatBaseValue(line) > 0 && line.Value >= low && line.Value <= high, "Generated quality/rarity scaling is outside the authored range.");
                if (high > low)
                {
                    minimumQuality = Math.Min(minimumQuality, line.Value * 10000 / Math.Max(1, high));
                    maximumQuality = Math.Max(maximumQuality, line.Value * 10000 / Math.Max(1, high));
                }
            }
            var json = Json(artifact); ArtifactRules.Normalize(artifact);
            Check(Json(artifact) == json, "Current-schema normalization must not reroll generated stats.");
        }
        Check(seenMains.Count == 5 && seenMains.All(kv => kv.Value.Count == (kv.Key == ArtifactSlot.Flower || kv.Key == ArtifactSlot.Feather ? 3 : 16)),
            "Seed sample must exercise every allowed main on every slot.");
        Check(seenSubs.Count == 19 && minimumQuality < 8000 && maximumQuality > 9500, "Generation must exercise the whole substat pool and varied quality.");
    }
    private static void BalanceMigration()
    {
        var legacy = ArtifactRules.Generate("legacy-table", ArtifactRarity.SSR, 491);
        legacy.SchemaVersion = 1; legacy.Level = 18; legacy.Experience = 12;
        legacy.Favourite = true; legacy.EquippedCharacterId = "fighter";
        legacy.SourceRunId = "run"; legacy.SourceNodeId = "node";
        legacy.MainStat = new ArtifactStatLine { Stat = StatId.Attack, Percent = false, Value = 456 };
        legacy.Substats = new List<ArtifactStatLine>
        {
            new ArtifactStatLine { Stat = StatId.Attack, Percent = true, Value = 200 },
            new ArtifactStatLine { Stat = StatId.Resistance, Percent = true, Value = 300 },
            new ArtifactStatLine { Stat = StatId.CombatClass, Value = 60 },
            new ArtifactStatLine { Stat = StatId.ReflectDamage, Percent = true, Value = 250 }
        };
        var another = legacy.Clone(); ArtifactRules.Normalize(legacy); ArtifactRules.Normalize(another);
        Check(Json(legacy) == Json(another) && legacy.SchemaVersion == 2 && legacy.Level == 18 && legacy.Experience == 12 &&
            legacy.Favourite && legacy.EquippedCharacterId == "fighter" && legacy.SourceRunId == "run" && legacy.SourceNodeId == "node",
            "Balance migration must be deterministic and preserve identity, equipment, protection and invested progression.");
        Check(legacy.MainStat.Stat == StatId.Attack && legacy.MainStat.Percent && legacy.MainStat.Value == 4264,
            "Old flat basic mains must convert to the corresponding authored percentage track.");
        Check(legacy.Substats.All(s => ArtifactRules.SubstatBaseValue(s) > 0 && Key(s) != Key(legacy.MainStat)) &&
            legacy.Substats.Select(Key).Distinct().Count() == 4, "Migration must replace removed affixes and exclude the main identity.");
        var migrated = Json(legacy); ArtifactRules.Normalize(legacy);
        Check(Json(legacy) == migrated, "Migration must happen only once.");
        var reconstructed = legacy.Clone(); reconstructed.Substats.Clear(); ArtifactRules.Normalize(reconstructed);
        Check(Json(reconstructed) == migrated, "Repaired migrated lines must recover all earned random milestone gains.");
    }
    private static void AppliedArtifactStats()
    {
        var baseline = new StatBlock { Attack = 10000, Defense = 20000, MaxHealth = 50000, RecoveryBp = 10000 };
        foreach (var rule in ExpectedMains)
        {
            var artifact = new ArtifactInstance { Slot = ArtifactSlot.Crown, Rarity = ArtifactRarity.SSR, Level = 20,
                MainStat = new ArtifactStatLine { Stat = rule.Stat, Percent = true, Value = rule.End } };
            var applied = ArtifactRules.ApplyStats(baseline, new[] { artifact });
            var expected = baseline.Get(rule.Stat) + (ArtifactRules.IsBasic(rule.Stat) ? baseline.Get(rule.Stat) * rule.End / 10000 : rule.End);
            Check(applied.Get(rule.Stat) == expected, "Main stat must affect its correct base or rate: " + rule.Stat);
        }
        foreach (var rule in ExpectedSubs)
        {
            var artifact = new ArtifactInstance { Slot = ArtifactSlot.Crown, MainStat = new ArtifactStatLine { Stat = StatId.MaxHealth, Percent = true },
                Substats = new List<ArtifactStatLine> { new ArtifactStatLine { Stat = rule.Stat, Percent = rule.Percent, Value = rule.Base } } };
            var applied = ArtifactRules.ApplyStats(baseline, new[] { artifact });
            var expected = baseline.Get(rule.Stat) + (ArtifactRules.IsBasic(rule.Stat) && rule.Percent ? baseline.Get(rule.Stat) * rule.Base / 10000 : rule.Base);
            Check(applied.Get(rule.Stat) == expected, "Substat must affect its correct flat, basic percent or secondary rate: " + rule.Stat);
            Check(Json(Copy(applied)) == Json(applied), "New damage rate fields must survive serialization.");
        }
    }
    private static FighterState RateFighter(int global, int dot, int additional) => new FighterState
    {
        Id = "rate-source", Health = 100000,
        Definition = new CharacterDefinition { BaseStats = new StatBlock { Attack = 10000, MaxHealth = 100000,
            DamageDealtBp = global, DamageOverTimeDealtBp = dot, AdditionalDamageDealtBp = additional } }
    };
    private static void DamageRateFamilies()
    {
        var defender = RateFighter(0, 0, 0); var source = RateFighter(1000, 2000, 3000);
        foreach (DamageFamily family in Enum.GetValues(typeof(DamageFamily)))
        {
            var policy = StatusSystem.BuildDamagePolicy(source, defender, family);
            var normal = family == DamageFamily.Normal || family == DamageFamily.True || family == DamageFamily.Destructive;
            Check(normal ? policy.OutgoingIncreaseBp == 1000 && policy.FamilyDealtIncreaseBp == 0 :
                policy.FamilyDealtIncreaseBp == 1000 + (family == DamageFamily.DamageOverTime ? 2000 : family == DamageFamily.Additional ? 3000 : 0),
                "Global, DOT and Additional affixes must affect only their intended damage policy branch: " + family);
            var damage = DamageResolver.Resolve(new DamagePacket { BaseAmount = 10000, Policy = policy },
                source.Stats, defender.Stats, 100000, 0, -1, -1);
            var expected = family == DamageFamily.True ? 14000 : family == DamageFamily.DamageOverTime ? 13000 :
                family == DamageFamily.Additional ? 14000 : 11000;
            Check(damage.CalculatedDamage == expected, "Authored damage affixes must affect actual resolved damage exactly once: " + family);
        }
        Check(StatusSystem.GetExplicitFamilyDamageDealtBp(source, DamageFamily.DamageOverTime) == 2000 &&
            StatusSystem.GetExplicitFamilyDamageDealtBp(source, DamageFamily.Additional) == 3000,
            "DOT and Additional family lookup must remain independent and exclude global Damage Dealt.");
        var reaction = StatusSystem.BuildDamagePolicy(source, defender, DamageFamily.ElementalReaction);
        ElementalReactionRules.ApplyOwnReactionBonus(reaction, source);
        Check(reaction.DotBridgeFactorBp == 12000 && reaction.FamilyDealtIncreaseBp == 1000,
            "Artifact DOT must participate in the existing reaction bridge without duplicating global damage.");
        Check(CombatClassRules.Score(source.Stats) - CombatClassRules.Score(new StatBlock { Attack = 10000, MaxHealth = 100000 }) == 6000L * CombatClassRules.SecondaryBasisPointWeight,
            "Artifact damage rates must contribute to the same secondary stat CC weights.");
    }
}
