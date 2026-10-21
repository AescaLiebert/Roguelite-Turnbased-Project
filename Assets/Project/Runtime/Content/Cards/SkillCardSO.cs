using UnityEngine;
using System.Collections.Generic;
using System;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

[CreateAssetMenu(fileName = "New Skill Card", menuName = "Card System/Skill Card")]
public class SkillCardSO : ScriptableObject
{
    [Header("Card Information")]
    public string cardName;
    public Sprite cardIcon;

    [Header("Status Visuals")]
    public List<FightingAllstar.Presentation.Combat.StatusVisualData> statusVisuals = new List<FightingAllstar.Presentation.Combat.StatusVisualData>();
    private void OnEnable() { foreach (var visual in statusVisuals) FightingAllstar.Presentation.Combat.StatusVisualData.Register(visual); }
    [Header("Rank Effects")]
    [Tooltip("Card art is shared by every rank. This list stores only the description, category, and runtime effect that can change by rank.")]
    public List<CardRankData> ranks = new List<CardRankData>();
    [SerializeField, HideInInspector] private int studioSchema;
    [SerializeField, HideInInspector] private string studioDefinitionJson;
    public bool StudioOwned => studioSchema != 0;
    public SkillDefinition CreateStudioDefinition()
    {
        if (!StudioOwned) return null;
        if (studioSchema != 1) throw new InvalidOperationException("Unsupported Studio skill schema: " + studioSchema);
        var definition = SnapshotJson.Deserialize<SkillDefinition>(studioDefinitionJson) ?? throw new InvalidOperationException("Studio skill payload is missing.");
        CardRules.ApplySharedSkillSettings(definition);
        return definition;
    }
    public void SetStudioDefinition(SkillDefinition value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        var definition = value.Clone();
        CardRules.ApplySharedSkillSettings(definition);
        studioDefinitionJson = SnapshotJson.Serialize(definition); studioSchema = 1;
        ranks = new List<CardRankData>(); // Inspector mirrors are not a second gameplay authority.
        foreach (var rank in definition.Ranks) ranks.Add(new CardRankData { description = rank.Description, Wording = rank.Wording?.Clone(),
            skillType = StudioAssetCategories.Native(rank.HasCardKind ? rank.Category : definition.Category) });
    }

    [ContextMenu("Stance Examples/Immunity, Evade, 80% Recovery")]
    private void ConfigureRecoveryStance()
    {
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Configure recovery stance");
#endif
        foreach (var rank in ranks)
        {
            if (rank == null) continue;
            rank.skillType = SkillType.Stance;
            rank.runtimeEffect = new EffectDefinition { Kind = EffectKind.ApplyStatus, Target = EffectTargetScope.Self,
            StatusRecipe = new StatusRecipeDefinition {
                Id = name + ".stance", NameKey = "Defensive Stance", Polarity = StatusPolarity.Neutral,
                Behavior = StatusBehavior.Stance, DefaultDuration = 1, DurationClock = StatusDurationClock.TargetTurnStart,
                StanceChildren = new List<StanceChildDefinition> {
                    new StanceChildDefinition { Id = name + ".immunity", NameKey = "Debuff Immunity", DebuffImmunity = true },
                    new StanceChildDefinition { Id = name + ".evade", NameKey = "Evade", EvadeAttacks = true },
                    new StanceChildDefinition { Id = name + ".recovery", NameKey = "Damage Recovery", RecoverDamageTakenBp = 8000 }
                } } };
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Stance Examples/Taunt with Ignite Counter")]
    private void ConfigureIgniteCounterStance()
    {
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Configure counter stance");
#endif
        foreach (var rank in ranks)
        {
            if (rank == null) continue;
            rank.skillType = SkillType.Stance;
            rank.runtimeEffect = new EffectDefinition { Kind = EffectKind.ApplyStatus, Target = EffectTargetScope.Self,
            StatusRecipe = new StatusRecipeDefinition {
                Id = name + ".stance", NameKey = "Counter Stance", Polarity = StatusPolarity.Neutral,
                Behavior = StatusBehavior.Stance, DefaultDuration = 2,
                StanceChildren = new List<StanceChildDefinition> {
                    new StanceChildDefinition { Id = name + ".taunt", NameKey = "Taunt", Taunt = true },
                    new StanceChildDefinition { Id = name + ".counter", NameKey = "Ignite Counter",
                        CounterEnabled = true, CounterCategory = CardCategory.Debuff,
                        CounterEffect = new CounterEffectDefinition { Kind = EffectKind.ApplyStatus,
                            StatusRecipeId = "status.debuff.ignite", StatusDurationOverride = 2, StatusStackCount = 1 } }
                } } };
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    public CardRankData GetRankData(int rank)
    {
        if (StudioOwned)
        {
            var skill = CreateStudioDefinition(); var item = skill.Ranks.Find(r => r.Rank == Mathf.Clamp(rank, 1, 3));
            return item == null ? null : new CardRankData { description = item.Description, Wording = item.Wording?.Clone(),
                skillType = StudioAssetCategories.Native(item.HasCardKind ? item.Category : skill.Category), runtimeEffect = item.Effect?.Clone() };
        }
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
    DebuffAtk,
    Ultimate
}

public enum SkillTargetType
{
    Single,
    AOE,
    Self,
    AllAllies
}

[Serializable]
public class CardRankData
{
    [SerializeReference] public AuthoredDescription Wording;
    [TextArea]
    public string description;
    [Tooltip("The category may change by rank, for example when a higher rank adds a debuff.")]
    public SkillType skillType;
    [Header("Runtime Effect")]
    [Tooltip("The combat definition used by battle. Put all card effects, including follow-up effects, in this definition's Sequence.")]
    public EffectDefinition runtimeEffect;
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
    ModifyStat,
    RemoveStance
}

[Serializable]
public class CharacterCardEffect
{
    public CharacterCardEffectKind kind;
    public string statusId;
    [Range(0f, 100f)]
    public float applyChancePercent = 100f;
    public FightingAllstar.Presentation.Combat.StatusVisualData statusVisual;
    public string statId;
    public SkillType disabledCardType;
    public SkillTargetType targetType;
    public float magnitude;
    public int durationTurns;
    public int stackCount = 1;
    public int stackCap;
}
