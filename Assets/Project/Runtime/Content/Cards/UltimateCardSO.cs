using UnityEngine;
using System.Collections.Generic;
using System;
using FightingAllstar.Core.Content;

[CreateAssetMenu(fileName = "New Ultimate Card", menuName = "Card System/Ultimate Card")]
public class UltimateCardSO : ScriptableObject
{
    [Header("Ultimate Information")]
    public string ultimateName;
    public Sprite icon;
    
    [Header("Progression")]
    public List<FightingAllstar.Presentation.Combat.StatusVisualData> statusVisuals = new List<FightingAllstar.Presentation.Combat.StatusVisualData>();
    private void OnEnable() { foreach (var visual in statusVisuals) FightingAllstar.Presentation.Combat.StatusVisualData.Register(visual); }
    public List<UltimateLevelData> levels = new List<UltimateLevelData> { new UltimateLevelData() };

    public UltimateLevelData GetLevelData(int constellationTier)
    {
        // Ultimate progression is C0-C5, so tier is the list index.
        if (levels == null || levels.Count == 0) return null;

        int index = Mathf.Clamp(constellationTier, 0, levels.Count - 1);
        return levels[index];
    }
}

[Serializable]
public class UltimateLevelData
{
    public int level; // Constellation tier C0 to C5
    [TextArea]
    public string description;
    public SkillType skillType = SkillType.Ultimate;
    [Header("Runtime Effect")]
    [Tooltip("The combat definition used by battle. Put all card effects, including follow-up effects, in this definition's Sequence.")]
    public EffectDefinition runtimeEffect = new EffectDefinition {
        Kind = EffectKind.Damage,
        Attack = new DamageAttackDefinition {
            Reaction = HitReaction.KnockUp,
            ReactionTiming = HitReactionTiming.LastHit
        }
    };
}
