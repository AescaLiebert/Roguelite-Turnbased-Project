using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CharacterRarityIconArt
{
    public FighterRarity rarity;
    public Sprite background;
    public Sprite frame;
    public Sprite label;
}

[Serializable]
public sealed class CharacterAttributeIconArt
{
    public FighterAttribute attribute;
    public Sprite icon;
}

[Serializable]
public sealed class CharacterSeriesIconArt
{
    public string seriesId;
    public Sprite icon;
}

[CreateAssetMenu(fileName = "CharacterIconArt", menuName = "Fighting Allstar/Character Icon Art")]
public sealed class CharacterIconArtSO : ScriptableObject
{
    private static CharacterIconArtSO _cached;
    [SerializeField] private CharacterRarityIconArt[] rarities;
    [SerializeField] private CharacterAttributeIconArt[] attributes;
    [SerializeField] private CharacterSeriesIconArt[] series;

    public static CharacterIconArtSO Load()
    {
        if (_cached == null) _cached = Resources.Load<CharacterIconArtSO>("CharacterIconArt");
        return _cached;
    }

    public CharacterRarityIconArt Rarity(FighterRarity rarity)
    {
        if (rarities == null) return null;
        foreach (var item in rarities) if (item != null && item.rarity == rarity) return item;
        return null;
    }

    public Sprite Attribute(FighterAttribute attribute)
    {
        if (attributes == null) return null;
        foreach (var item in attributes) if (item != null && item.attribute == attribute) return item.icon;
        return null;
    }

    public Sprite Series(string seriesId)
    {
        if (series == null) return null;
        foreach (var item in series)
            if (item != null && string.Equals(item.seriesId, seriesId, StringComparison.OrdinalIgnoreCase)) return item.icon;
        return null;
    }
}
