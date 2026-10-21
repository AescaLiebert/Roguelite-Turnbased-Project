using System;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;

namespace CharacterCreatorStudio.Checks
{
    internal static class SharedCardSettingsChecks
    {
        public static int Run(CharacterDefinition baseline)
        {
            var assertions = 0;
            void Check(bool condition, string message)
            {
                if (!condition) throw new InvalidOperationException(message);
                assertions++;
            }

            var document = StudioDocumentService.ImportOpeningGauge(baseline);
            document.AuthoredCharacter = document.LegacyCharacter.Clone();
            var authoredSkill = document.AuthoredCharacter.Skills.First();
            authoredSkill.TargetScope = EffectTargetScope.AllEnemies;
            authoredSkill.DamageAnimation = new DamageAttackDefinition
            {
                HitCount = 4,
                Range = AttackRange.Long,
                Reaction = HitReaction.KnockUp,
                ReactionTiming = HitReactionTiming.FirstHit
            };
            foreach (var rank in authoredSkill.Ranks)
            {
                rank.TargetScope = EffectTargetScope.SelectedEnemy;
                rank.Effect = new EffectDefinition
                {
                    Kind = EffectKind.Damage,
                    Target = EffectTargetScope.SelectedEnemy,
                    Attack = new DamageAttackDefinition { HitCount = rank.Rank, Range = AttackRange.Close }
                };
            }

            var sourceCategories = authoredSkill.Ranks.Select(rank => rank.Category).ToArray();
            var compiled = StudioCompiler.Compile(document);
            Check(compiled.Success, "Root/rank mismatch fixture compiles: " + string.Join("; ", compiled.Diagnostics));
            var compiledSkill = compiled.Character.Skills.Single(skill => skill.Id == authoredSkill.Id);
            for (var i = 0; i < compiledSkill.Ranks.Count; i++)
            {
                var rank = compiledSkill.Ranks[i];
                Check(rank.TargetScope == EffectTargetScope.AllEnemies && rank.Effect.Target == EffectTargetScope.AllEnemies,
                    "Every compiled rank uses the skill-root target.");
                Check(rank.Effect.Attack.HitCount == 4 && rank.Effect.Attack.Range == AttackRange.Long &&
                    rank.Effect.Attack.Reaction == HitReaction.KnockUp && rank.Effect.Attack.ReactionTiming == HitReactionTiming.FirstHit,
                    "Every compiled rank uses the complete skill-root damage animation.");
                Check(rank.Category == sourceCategories[i], "Root canonicalization preserves rank category overrides.");
            }

            Check(document.AuthoredCharacter.Skills.First().Ranks[0].Effect.Attack.HitCount == 1 &&
                document.AuthoredCharacter.Skills.First().Ranks[0].TargetScope == EffectTargetScope.SelectedEnemy,
                "Compilation leaves authored rank data untouched for recovery and Undo.");

            var staleVariant = StudioDocumentService.Clone(document);
            foreach (var rank in staleVariant.AuthoredCharacter.Skills.First().Ranks)
            {
                rank.TargetScope = EffectTargetScope.SelectedAlly;
                rank.Effect.Target = EffectTargetScope.SelectedAlly;
                rank.Effect.Attack.HitCount = 10 - rank.Rank;
            }
            var staleCompiled = StudioCompiler.Compile(staleVariant);
            Check(staleCompiled.Success && staleCompiled.Hashes.Gameplay == compiled.Hashes.Gameplay,
                "Non-authoritative per-rank targeting and animation cannot change compiled gameplay or its hash.");

            Check(CardRules.TryGetSkill(compiled.Character, compiledSkill.Id, 1, out var runtimeEffect) &&
                runtimeEffect.Target == EffectTargetScope.AllEnemies && runtimeEffect.Attack.HitCount == 4 &&
                runtimeEffect.Attack.Range == AttackRange.Long,
                "Runtime skill resolution consumes the same canonical settings as Studio compilation.");
            return assertions;
        }
    }
}
