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
            if (taki == null && billy == null && benimaru == null)
            {
                var repaired = character.Id.EndsWith(".billy97", StringComparison.Ordinal) && MoveBillyGuardToChild(skill);
                if (character.Id == "fighter.benimaru99")
                    foreach (var rank in skill.Ranks)
                        foreach (var step in rank.Effect.Sequence)
                            foreach (var rule in step.Effect?.StatusRecipe?.Reactions ?? new System.Collections.Generic.List<PassiveReactionDefinition>())
                                if (rule.Id.EndsWith(".women-defense-down", StringComparison.Ordinal))
                                    foreach (var command in rule.Commands)
                                        if (command.Effect?.TargetQuery != null) { command.Effect.TargetQuery = null; repaired = true; }
                return repaired;
            }
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
                    foreach (var command in reaction.Commands) if (command.Effect != null) command.Effect.TargetQuery = null;
                    reaction.Conditions.RemoveAll(c => c.Kind == EffectConditionKind.TargetHasRecipe && c.RecipeId == "status.buff.apply-debuff");
                    var application = reaction.Commands.First(c => c.Effect?.Status != null).Effect.Status;
                    application.PotencySource = StatusPotencySource.Authored;
                    application.PotencyBp = grant.StatusPotencyBp;
                    application.PotencySourceRecipeId = null;
                    grant.StatusRecipe.Reactions.Add(reaction);
                }
            }
            if (billy != null) MoveBillyGuardToChild(skill);
            character.Passive.Reactions.RemoveAll(r => ReferenceEquals(r, taki) || ReferenceEquals(r, billy) || ReferenceEquals(r, benimaru));
            return true;
        }

        private static bool MoveBillyGuardToChild(SkillDefinition skill)
        {
            var changed = false;
            foreach (var rank in skill.Ranks)
            {
                var root = rank.Effect?.StatusRecipe;
                if (root == null) continue;
                var rules = root.Reactions.Where(r => r.Id.EndsWith(".guard-burn", StringComparison.Ordinal)).ToArray();
                if (rules.Length == 0) continue;
                var child = root.StanceChildren.Find(c => c?.Id == "status.stance.apply");
                if (child == null)
                {
                    child = new StanceChildDefinition { Id = "status.stance.apply", NameKey = "Apply Ignite" };
                    root.StanceChildren.Add(child);
                }
                foreach (var rule in rules)
                {
                    var id = child.Id + ".guard-burn";
                    if (!child.Reactions.Any(r => r.Id == id)) { var moved = rule.Clone(); moved.Id = id; child.Reactions.Add(moved); }
                    root.Reactions.Remove(rule); changed = true;
                }
            }
            return changed;
        }
    }
}
