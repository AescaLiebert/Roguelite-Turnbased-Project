using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SummonBannerRarityPool
{
    [SerializeField] private FighterRarity rarity = FighterRarity.R;
    [SerializeField, Min(1), Tooltip("Chance for this rarity in basis points. 100 basis points equals 1%.")]
    private int rateBasisPoints = 1;
    [SerializeField] private List<CharacterObject> characters = new List<CharacterObject>();

    public FighterRarity Rarity => rarity;
    public int RateBasisPoints => rateBasisPoints;
    public IReadOnlyList<CharacterObject> Characters => characters;
}

[CreateAssetMenu(fileName = "SummonBanner", menuName = "Fighting Allstar/Summon Banner Configuration")]
public sealed class SummonBannerConfigurationSO : ScriptableObject
{
    [SerializeField] private string bannerName = "KOF Fighters";
    [SerializeField] private string seriesId = "series.kof";
    [SerializeField, Min(1), Tooltip("The sum of all rarity rates. 10000 basis points equals 100%.")]
    private int totalRateBasisPoints = 10000;
    [SerializeField] private List<SummonBannerRarityPool> rarityPools = new List<SummonBannerRarityPool>();

    public string BannerName => bannerName;
    public string SeriesId => seriesId;
    public int TotalRateBasisPoints => totalRateBasisPoints;
    public IReadOnlyList<SummonBannerRarityPool> RarityPools => rarityPools;

    public int GetRarityRateBasisPoints(FighterRarity rarity)
    {
        if (rarityPools == null) return 0;
        foreach (var pool in rarityPools)
            if (pool != null && pool.Rarity == rarity)
                return pool.RateBasisPoints;
        return 0;
    }

    public bool Validate(out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(bannerName)) { error = "Banner name is required."; return false; }
        if (string.IsNullOrWhiteSpace(seriesId)) { error = "Banner series ID is required."; return false; }
        if (totalRateBasisPoints != 10000) { error = "Total banner rate must be 10000 basis points (100%)."; return false; }
        if (rarityPools == null || rarityPools.Count == 0) { error = "Banner has no rarity pools."; return false; }

        var seenRarities = new HashSet<FighterRarity>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var total = 0;
        foreach (var pool in rarityPools)
        {
            if (pool == null) { error = "Every rarity pool needs a configuration."; return false; }
            if (pool.Rarity != FighterRarity.R && pool.Rarity != FighterRarity.SR && pool.Rarity != FighterRarity.SSR)
            { error = "Rarity " + pool.Rarity + " is unsupported by this banner."; return false; }
            if (!seenRarities.Add(pool.Rarity)) { error = "Banner has more than one pool for rarity " + pool.Rarity + "."; return false; }
            if (pool.RateBasisPoints <= 0) { error = pool.Rarity + " needs a positive rarity rate."; return false; }
            if (pool.Characters == null || pool.Characters.Count == 0) { error = pool.Rarity + " pool is empty."; return false; }
            total += pool.RateBasisPoints;

            foreach (var character in pool.Characters)
            {
                if (character == null) { error = "Every banner pool entry needs a Character asset."; return false; }
                if (string.IsNullOrWhiteSpace(character.DefinitionId)) { error = character.name + " has no Definition ID."; return false; }
                if (character.SeriesId != seriesId) { error = character.DefinitionId + " is outside banner series " + seriesId + "."; return false; }
                if (pool.Rarity != character.FighterRarity) { error = character.DefinitionId + " rarity does not match its rarity pool."; return false; }
                if (!ids.Add(character.DefinitionId)) { error = "Banner contains duplicate fighter " + character.DefinitionId + "."; return false; }
            }
        }

        if (total != totalRateBasisPoints)
        {
            error = "Rarity rates total " + total + " basis points; expected " + totalRateBasisPoints + ".";
            return false;
        }
        return true;
    }
}
