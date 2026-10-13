using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using UnityEngine;

namespace FightingAllstar.Presentation.Navigation
{
    /// <summary>Authored card frames, card badges, tooltip glyphs and passive condition artwork.</summary>
    public sealed class CharacterKitArtSO : ScriptableObject
    {
        public Texture2D rankOne, rankTwo, rankThree;
        public Sprite attack, buff, debuff, attackDebuff, recovery, stance;
        public Sprite skillSingleTarget, skillAoe, skillBuff, skillRecovery, skillStance, skillUltimate;
        public Sprite passiveOnField, passiveSub, passiveAll, passivePvp, passivePve;
        public Texture2D Rank(int rank) => rank <= 1 ? rankOne : rank == 2 ? rankTwo : rankThree;
        public Sprite Type(SkillType type) => type switch
        {
            SkillType.Buff => buff, SkillType.Debuff => debuff, SkillType.DebuffAtk => attackDebuff,
            SkillType.Heal => recovery, SkillType.Stance => stance, SkillType.Ultimate => null, _ => attack
        };

        public Sprite SkillIcon(CardState card, FighterState owner)
        {
            if (card?.Kind == CardKind.Ultimate) return skillUltimate;
            var skill = owner?.Definition?.Skills?.FirstOrDefault(s => s.Id == card?.SkillId);
            var rank = skill?.Ranks?.FirstOrDefault(r => r.Rank == card?.Rank);
            var category = rank?.HasCardKind == true ? rank.Category : skill != null ? CardRules.ResolveCategory(skill) : CardRules.GetEffectCategory(card);
            var target = rank?.HasCardKind == true ? rank.TargetScope : skill != null ? CardRules.ResolveTargetScope(skill) : card?.TargetScope ?? EffectTargetScope.SelectedEnemy;
            return category switch
            {
                CardCategory.Buff => skillBuff,
                CardCategory.Recovery => skillRecovery,
                CardCategory.Stance => skillStance,
                _ => target == EffectTargetScope.AllEnemies || target == EffectTargetScope.AllAllies ? skillAoe : skillSingleTarget
            };
        }

        public IEnumerable<(Sprite icon, string label)> PassiveConditions(string sourceType)
        {
            var source = sourceType ?? string.Empty;
            if (source.IndexOf("SUB", StringComparison.OrdinalIgnoreCase) >= 0) yield return (passiveSub, "Active from reserve");
            else if (source.IndexOf("On-Field", StringComparison.OrdinalIgnoreCase) >= 0) yield return (passiveOnField, "Active on the field");
            if (source.IndexOf("PVP", StringComparison.OrdinalIgnoreCase) >= 0) yield return (passivePvp, "PVP only");
            else if (source.IndexOf("PVE", StringComparison.OrdinalIgnoreCase) >= 0) yield return (passivePve, "PVE only");
            else yield return (passiveAll, "All battle modes");
        }
    }
}
