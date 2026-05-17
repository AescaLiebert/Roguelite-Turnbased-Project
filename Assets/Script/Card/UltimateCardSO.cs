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

    public UltimateLevelData GetLevelData(int level)
    {
        // Levels 1-6. Index = level - 1
        if (levels == null || levels.Count == 0) return null;

        int index = Mathf.Clamp(level - 1, 0, levels.Count - 1);
        return levels[index];
    }
}

[Serializable]
public class UltimateLevelData
{
    public int level; // 1 to 6
    [TextArea]
    public string description;
    public SkillTargetType targetType;
    public DamageScalingType damageScalingType;
    public float damageMultiplier;
    public GameObject ultimatePrefab;
    
    // Special effects that scale with level
}
