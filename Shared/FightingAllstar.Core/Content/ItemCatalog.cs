using System;
using System.Collections.Generic;
using FightingAllstar.Core.Run;

namespace FightingAllstar.Core.Content
{
    public enum ItemKind { Diamond, Coin, PhaseMaterial, SubPhaseMaterial, SeriesMaterial, SeriesPremiumMaterial, ArtifactMaterial, Artifact, CharacterDupe }
    public enum ArtifactRarity { Common = 1, Uncommon = 2, R = 3, SR = 4, SSR = 5 }

    [Serializable]
    public sealed class ItemDefinition
    {
        public string Id, Name, Description, SeriesId;
        public string Appearance, IconColor;
        public ItemKind Kind;
        public int Tier;
        public int FrameTier => Kind == ItemKind.Artifact ? Tier :
            Kind == ItemKind.Diamond || Kind == ItemKind.Coin ? 5 :
            Kind == ItemKind.SeriesMaterial || Kind == ItemKind.SeriesPremiumMaterial || Kind == ItemKind.CharacterDupe ? 4 : 3;
        public bool PremiumFrame => FrameTier == 5;
        public int ValueRank => Kind == ItemKind.Artifact ? 1000 + Tier * 100 : Kind == ItemKind.Diamond ? 900 :
            Kind == ItemKind.CharacterDupe ? 850 : Kind == ItemKind.SeriesPremiumMaterial ? 800 : Kind == ItemKind.SeriesMaterial ? 700 :
            Kind == ItemKind.PhaseMaterial ? 600 + Tier : Kind == ItemKind.SubPhaseMaterial ? 550 + Tier : Kind == ItemKind.Coin ? 500 : 400;
    }

    public static class ItemCatalog
    {
        public const string Diamond = "currency.diamond", Coin = "currency.coin", ArtifactDust = "material.artifact-dust";
        private const string CharacterDupeTokenPrefix = "character.dupe-token:";
        private static readonly string[] PhaseRelics = { "Prototype Crest", "Dawn Crest", "Release Crest", "Ember Crest", "Tempest Crest", "Prism Crest", "Vanguard Crest", "Eclipse Crest", "Apex Crest" };
        private static readonly string[] PhaseColors = { "#60B6EF", "#86D9CC", "#FFD17E", "#F4976D", "#98AFFF", "#D797F1", "#75D793", "#BFB5F6", "#FF929F" };
        private static readonly string[] SubRelics = { "Origin Sigil", "Spark Sigil", "Resonance Sigil", "Horizon Sigil", "Apex Sigil" };
        private static readonly string[] SubColors = { "#9CEBDB", "#FADE80", "#77CBF7", "#AFABF9", "#F3A3E6" };
        public static string PhaseId(int phase) => "material.phase." + phase;
        public static string SubPhaseId(int subPhase) => "material.subphase." + subPhase;
        public static string SeriesId(string series, bool premium = false) =>
            (premium ? "material.series-premium." : "material.series.") + series;
        public static string CharacterDupeTokenId(string characterDefinitionId) =>
            string.IsNullOrWhiteSpace(characterDefinitionId) ? string.Empty : CharacterDupeTokenPrefix + characterDefinitionId;
        public static bool TryGetCharacterDupeDefinitionId(string itemId, out string characterDefinitionId)
        {
            characterDefinitionId = null;
            if (string.IsNullOrWhiteSpace(itemId) || !itemId.StartsWith(CharacterDupeTokenPrefix, StringComparison.Ordinal)) return false;
            characterDefinitionId = itemId.Substring(CharacterDupeTokenPrefix.Length);
            return !string.IsNullOrWhiteSpace(characterDefinitionId);
        }
        public static ItemDefinition CreateCharacterDupeToken(string characterDefinitionId, string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterDefinitionId)) return null;
            var name = string.IsNullOrWhiteSpace(characterName) ? characterDefinitionId : characterName.Trim();
            var item = Item(CharacterDupeTokenId(characterDefinitionId), name + " Dupe Token", ItemKind.CharacterDupe,
                "A duplicate of " + name + ". This token can only be used for " + name + ".");
            item.IconColor = "#E9BB5C";
            return item;
        }
        public static string ArtifactId(ArtifactRarity rarity) => "artifact." + rarity.ToString().ToLowerInvariant();

        public static ItemDefinition Find(string id)
        {
            if (id == Diamond) return Item(id, "Diamond", ItemKind.Diamond, "The main currency used to summon fighters in Gacha.");
            if (id == Coin) return Item(id, "Coin", ItemKind.Coin, "Utility currency used for character upgrades and other upgrade systems.");
            if (TryGetCharacterDupeDefinitionId(id, out var duplicateDefinitionId))
                return CreateCharacterDupeToken(duplicateDefinitionId, duplicateDefinitionId);
            if (id == ArtifactDust) return Item(id, "Artifact Dust", ItemKind.ArtifactMaterial, "A dedicated enhancement material. Each Dust grants " + ArtifactRules.DustXp + " artifact EXP.");
            if (id == null) return null;
            if (ArtifactRules.TryIdentity(id, out var artifactSet, out var artifactSlot))
            {
                var artifact = new ArtifactInstance { Set = artifactSet, Slot = artifactSlot };
                var item = Item(id, ArtifactRules.Name(artifact), ItemKind.Artifact,
                    ArtifactRules.SetName(artifactSet) + " · " + ArtifactRules.SlotName(artifactSlot) + ". " + ArtifactRules.Description(artifactSet), 5);
                item.IconColor = artifactSet == ArtifactSet.Striker ? "#ED8D81" : artifactSet == ArtifactSet.Bulwark ? "#EBC063" : "#9FCAA8";
                return item;
            }
            if (id.StartsWith("material.phase.") && int.TryParse(id.Substring(15), out var phase) && phase >= 1 && phase <= 9)
            {
                var item = Item(id, DungeonProfile.PhaseNames[phase - 1] + " " + PhaseRelics[phase - 1], ItemKind.PhaseMaterial,
                    "A crystalline crest preserving the spirit of " + DungeonProfile.PhaseNames[phase - 1] + ". Used to limit break fighters released in this phase. Earn it from this phase's dungeon victories.", phase);
                item.IconColor = PhaseColors[phase - 1]; item.Appearance = "Faceted shield crest with a bright core and " + phase + " engraved release marks."; return item;
            }
            if (id.StartsWith("material.subphase.") && int.TryParse(id.Substring(18), out var sub) && sub >= 0 && sub < 5)
            {
                var item = Item(id, DungeonProfile.SubPhaseNames[sub] + " " + SubRelics[sub], ItemKind.SubPhaseMaterial,
                    "A resonant seal attuned to the " + DungeonProfile.SubPhaseNames[sub] + " sub-phase. Used to limit break matching fighters from any phase. Earn it in dungeons of this sub-phase.", sub);
                item.IconColor = SubColors[sub]; item.Appearance = "Floating ring around a diamond seal with " + (sub + 4) + " radial rays."; return item;
            }
            var premium = id.StartsWith("material.series-premium.", StringComparison.Ordinal);
            if (premium || id.StartsWith("material.series.", StringComparison.Ordinal))
            {
                var series = id.Substring(premium ? 24 : 16);
                if (!series.StartsWith("series.", StringComparison.Ordinal) || series.Length <= 7) return null;
                var relic = series == "series.kof" ? "Tournament Insignia" : series == "series.tekken" ? "Iron Fist Token" : series == "series.soulcalibur" ? "Soul Fragment" : "Legacy Insignia";
                var item = Item(id, DungeonProfile.SeriesDisplayName(series) + (premium ? " Relic Core" : " " + relic),
                    premium ? ItemKind.SeriesPremiumMaterial : ItemKind.SeriesMaterial,
                    premium ? "Series-exclusive premium material, obtainable only from Series Challenge Boss victories. Used for Holy Relics and future external systems." :
                        "A legacy sealed in crystal, carrying the fighting spirit of " + DungeonProfile.SeriesDisplayName(series) + ". Used to limit break fighters from this series. Earn it by defeating this series in dungeons.");
                item.SeriesId = series; item.IconColor = "#BF92F4";
                item.Appearance = series == "series.kof" && !premium
                    ? "Silver circular tournament badge with an oversized K, teal globe, red flame sweep, and gold accent."
                    : "Violet hexagonal medallion bearing a gold " + DungeonProfile.SeriesDisplayName(series) + " monogram.";
                return item;
            }
            foreach (ArtifactRarity rarity in Enum.GetValues(typeof(ArtifactRarity)))
                if (id == ArtifactId(rarity)) return Item(id, rarity + " Artifact", ItemKind.Artifact,
                    "A legacy artifact identity. Owned instances migrate to a named set and slot when loaded.", (int)rarity);
            return null;
        }

        public static List<ItemDefinition> UpgradeMaterials()
        {
            var items = new List<ItemDefinition>();
            for (var phase = 1; phase <= 9; phase++) items.Add(Find(PhaseId(phase)));
            for (var sub = 0; sub < 5; sub++) items.Add(Find(SubPhaseId(sub)));
            return items;
        }

        public static List<ItemDefinition> Artifacts()
        {
            var artifacts = new List<ItemDefinition>();
            foreach (ArtifactSet set in Enum.GetValues(typeof(ArtifactSet)))
                foreach (ArtifactSlot slot in Enum.GetValues(typeof(ArtifactSlot))) artifacts.Add(Find(ArtifactRules.ItemId(set, slot)));
            return artifacts;
        }

        private static ItemDefinition Item(string id, string name, ItemKind kind, string description, int tier = 0) =>
            new ItemDefinition { Id = id, Name = name, Kind = kind, Description = description, Tier = tier };
    }
}
