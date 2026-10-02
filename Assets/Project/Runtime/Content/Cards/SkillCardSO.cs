using UnityEngine;
using System.Collections.Generic;
using System;

[CreateAssetMenu(fileName = "New Skill Card", menuName = "Card System/Skill Card")]
public class SkillCardSO : ScriptableObject
{
    [Header("Card Information")]
    public string cardName;
    public Sprite cardIcon;

    [Header("Rank Data")]
    public List<CardRankData> ranks = new List<CardRankData>();

    public CardRankData GetRankData(int rank)
    {
        // Ranks are usually 1, 2, 3. Adjust index accordingly (rank-1).
        if (ranks == null || ranks.Count == 0) return null;
        
        int index = Mathf.Clamp(rank - 1, 0, ranks.Count - 1);
        return ranks[index];
    }
}

public enum SkillType
{
    Attack,
    Buff,
    Debuff,
    Stance,
    Heal,
    DebuffAtk
}

public enum SkillTargetType
{
    Single,
    AOE,
    Self,
    AllAllies
}

public enum DamageScalingType
{
    ATK,
    DEF,
    HP
}

[Serializable]
public class CardRankData
{
    public int rankLevel; // 1, 2, 3
    [TextArea]
    public string description;
    public SkillTargetType targetType;
    public SkillType skillType;
    public DamageScalingType damageScalingType;
    public float damageMultiplier; // e.g., 1.2 for 120%
    public GameObject skillPrefab; // The ActionScript prefab
    [TextArea] public string sourceDescription;
    public string provenance;
    public string damageKeyword;
    public List<CharacterCardEffect> effects = new List<CharacterCardEffect>();
}

public enum CharacterCardEffectKind
{
    ApplyStatus,
    HealAttackMultiplier,
    HealMissingHealthPercent,
    HealMaxHealthPercent,
    CleanseDebuffs,
    RemoveBuffs,
    DisableCardType,
    DrainPowerGauge,
    IncreaseCardRank,
    ModifyStat
}

[Serializable]
public class CharacterCardEffect
{
    public CharacterCardEffectKind kind;
    public string statusId;
    public string statId;
    public SkillType disabledCardType;
    public SkillTargetType targetType;
    public float magnitude;
    public int durationTurns;
    public int stackCount = 1;
    public int stackCap;
}
