using UnityEngine;

namespace FightingAllstar.Presentation.Navigation
{
    /// <summary>References the same frames and card type icons used in the battle HUD.</summary>
    public sealed class CharacterKitArtSO : ScriptableObject
    {
        public Texture2D rankOne, rankThree;
        public Sprite attack, buff, debuff, attackDebuff, recovery, stance;
        public Sprite Type(SkillType type) => type switch
        {
            SkillType.Buff => buff, SkillType.Debuff => debuff, SkillType.DebuffAtk => attackDebuff,
            SkillType.Heal => recovery, SkillType.Stance => stance, SkillType.Ultimate => null, _ => attack
        };
    }
}
