using UnityEngine;
using System.Collections.Generic;
using System;

[CreateAssetMenu(fileName = "New Ultimate Card", menuName = "Card System/Ultimate Card")]
public class UltimateCardSO : ScriptableObject
{
    [Header("Ultimate Information")]
    public string ultimateName;
    public Sprite icon;
    
    [Header("Progression")]
    public List<UltimateLevelData> levels = new List<UltimateLevelData>();

    public UltimateLevelData GetLevelData(int constellationTier)
    {
        // Source progression is C0-C6, so tier is the list index.
        if (levels == null || levels.Count == 0) return null;

        int index = Mathf.Clamp(constellationTier, 0, levels.Count - 1);
        return levels[index];
    }
}

[Serializable]
public class UltimateLevelData
{
    public int level; // Constellation tier C0 to C6
    [TextArea]
    public string description;
    public SkillTargetType targetType;
    public DamageScalingType damageScalingType;
    public float damageMultiplier;
    public GameObject ultimatePrefab;
    [TextArea] public string sourceDescription;
    public string provenance;
    public string damageKeyword;
    public System.Collections.Generic.List<CharacterCardEffect> effects = new System.Collections.Generic.List<CharacterCardEffect>();
}
