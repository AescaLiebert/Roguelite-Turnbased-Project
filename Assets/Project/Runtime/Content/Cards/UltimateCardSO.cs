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
    [SerializeField, HideInInspector] private int studioSchema;
    [SerializeField, HideInInspector] private string studioDefinitionJson;
    [SerializeField, HideInInspector] private bool hasStudioCardSettings;
    [SerializeField, HideInInspector] private CardCategory studioCategory = CardCategory.Attack;
    [SerializeField, HideInInspector] private EffectTargetScope studioTargetScope = EffectTargetScope.SelectedEnemy;
    [SerializeField, HideInInspector] private DamageAttackDefinition studioDamageAnimation;
    public bool StudioOwned => studioSchema != 0;
    public bool HasStudioCardSettings => hasStudioCardSettings;
    public CardCategory StudioCategory => studioCategory;
    public EffectTargetScope StudioTargetScope => studioTargetScope;
    public DamageAttackDefinition StudioDamageAnimation => studioDamageAnimation;

    public void SetStudioCardSettings(CardCategory category, EffectTargetScope target, DamageAttackDefinition damageAnimation)
    {
        studioCategory = category;
        studioTargetScope = target;
        studioDamageAnimation = damageAnimation?.Clone();
        hasStudioCardSettings = true;
    }

    public void ClearStudioCardSettings() => hasStudioCardSettings = false;

    public List<UltimateTierDefinition> CreateStudioDefinition()
    {
        if (!StudioOwned) return null;
        if (studioSchema != 1) throw new InvalidOperationException("Unsupported Studio ultimate schema: " + studioSchema);
        return SnapshotJson.Deserialize<List<UltimateTierDefinition>>(studioDefinitionJson) ?? throw new InvalidOperationException("Studio ultimate payload is missing.");
    }
    public void SetStudioDefinition(List<UltimateTierDefinition> value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        studioDefinitionJson = SnapshotJson.Serialize(value); studioSchema = 1;
        levels = new List<UltimateLevelData>();
        foreach (var tier in value) levels.Add(new UltimateLevelData { level = tier.Tier, description = tier.Description, Wording = tier.Wording?.Clone(),
            skillType = StudioAssetCategories.Native(hasStudioCardSettings ? studioCategory : tier.Category), runtimeEffect = null });
    }

    public UltimateLevelData GetLevelData(int constellationTier)
    {
        if (StudioOwned)
        {
            var tier = CreateStudioDefinition().Find(t => t.Tier == Mathf.Clamp(constellationTier, 0, 5));
            return tier == null ? null : new UltimateLevelData { level = tier.Tier, description = tier.Description, Wording = tier.Wording?.Clone(),
                skillType = StudioAssetCategories.Native(hasStudioCardSettings ? studioCategory : tier.Category), runtimeEffect = tier.Effect?.Clone() };
        }
        // Ultimate progression is C0-C5, so tier is the list index.
        if (levels == null || levels.Count == 0) return null;

        int index = Mathf.Clamp(constellationTier, 0, levels.Count - 1);
        return levels[index];
    }
}

[Serializable]
public class UltimateLevelData
{
    [SerializeReference] public AuthoredDescription Wording;
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
