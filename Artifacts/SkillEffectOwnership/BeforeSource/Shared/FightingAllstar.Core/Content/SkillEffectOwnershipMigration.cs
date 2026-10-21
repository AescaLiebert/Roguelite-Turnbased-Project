using System;
using System.Linq;

namespace FightingAllstar.Core.Content
{
    /// <summary>Explicit authoring migration. Never called by battle loading.</summary>
    public static class SkillEffectOwnershipMigration
    {
        public static bool Apply(CharacterDefinition character)
        {
            var skill = character.Skills.Find(s => s.Slot == 2);
            if (skill == null || character.Passive == null) return false;
            var taki = character.Passive.Reactions.Find(r => r.Id == "skill-2-target-max-health-damage");
            var billy = character.Passive.Reactions.Find(r => r.Id.EndsWith(".guard-burn", StringComparison.Ordinal));
            var benimaru = character.Passive.Reactions.Find(r => r.Id == "apply-debuff-women-defense-down");
            if (taki == null && billy == null && benimaru == null) return false;
            foreach (var rank in skill.Ranks)
            {
                if (taki != null)
                {
                    var id = skill.Id + ".rank" + rank.Rank + ".additional";
                    rank.Effect.Sequence.RemoveAll(s => s?.Effect?.Id == id);
                    rank.Effect.Sequence.Add(new CardEffectStep { Timing = CardEffectTiming.AfterDamage,
                        Effect = new EffectStepDefinition { Id = id, Kind = EffectKind.Damage, Target = EffectTargetScope.SelectedEnemy,
                            DamageRecipe = taki.Commands.First(c => c.Effect?.Damage != null).Effect.Damage.Clone() } });
                }
                if (billy != null)
                {
                    var reaction = billy.Clone();
                    reaction.Id = skill.Id + ".rank" + rank.Rank + ".guard-burn";
                    reaction.CardOriginOnly = true;
                    rank.Effect.StatusRecipe.Reactions.Add(reaction);
                }
                if (benimaru != null)
                {
                    var grant = rank.Effect.Sequence.Select(s => s.Effect).FirstOrDefault(e => e?.StatusRecipe?.Id == "status.buff.apply-debuff");
                    if (grant == null) continue; // Rank one grants no Defense-reduction response.
                    var reaction = benimaru.Clone();
                    reaction.Id = skill.Id + ".rank" + rank.Rank + ".women-defense-down";
                    reaction.CardOriginOnly = true;
                    reaction.Conditions.RemoveAll(c => c.Kind == EffectConditionKind.TargetHasRecipe && c.RecipeId == "status.buff.apply-debuff");
                    var application = reaction.Commands.First(c => c.Effect?.Status != null).Effect.Status;
                    application.PotencySource = StatusPotencySource.Authored;
                    application.PotencyBp = grant.StatusPotencyBp;
                    application.PotencySourceRecipeId = null;
                    grant.StatusRecipe.Reactions.Add(reaction);
                }
            }
            character.Passive.Reactions.RemoveAll(r => ReferenceEquals(r, taki) || ReferenceEquals(r, billy) || ReferenceEquals(r, benimaru));
            return true;
        }
    }
}
