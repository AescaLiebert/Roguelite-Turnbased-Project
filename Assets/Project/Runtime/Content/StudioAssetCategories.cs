using FightingAllstar.Core.Content;

/// <summary>Shared authoring adapter; runtime definitions retain their full category metadata.</summary>
public static class StudioAssetCategories
{
    public static SkillType Native(CardCategory category) => category switch {
        CardCategory.Buff => SkillType.Buff, CardCategory.Debuff => SkillType.Debuff,
        CardCategory.Stance => SkillType.Stance, CardCategory.Recovery => SkillType.Heal,
        CardCategory.AttackDebuff => SkillType.DebuffAtk, _ => SkillType.Attack };
}
