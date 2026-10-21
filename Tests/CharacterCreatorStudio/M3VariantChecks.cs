using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;

namespace CharacterCreatorStudio.Checks
{
    public static class M3VariantChecks
    {
        public static int Run()
        {
            var count = 0;
            void Check(bool value, string message) { if (!value) throw new InvalidOperationException("M3 variants: " + message); count++; }
            var original = SnapshotJson.Deserialize<CharacterDefinition>(File.ReadAllText("Artifacts/CharacterCreatorStudio/M0/KaeyaLegacyDefinition.json"));
            var character = original.Clone();
            character.Id = "fixture.m3.variant";
            character.Passive = new PassiveDefinition { Id = "fixture.variant.passive" };
            var skill = character.Skills.OrderBy(s => s.Slot).First();
            var originalCoefficient = skill.Ranks[0].Effect.CoefficientBp;
            var variant = new AbilityVariantDefinition { Id = "variant.empowered", Name = "Empowered",
                MinimumLevel = 10, MinimumTier = 2, RequiredResourceKey = "resource.charge",
                RequiredResourceAmount = 3, ResourceCost = 3, RelockAfterUse = true,
                Ranks = skill.Ranks.Select(r => r.Clone()).ToList() };
            foreach (var rank in variant.Ranks) rank.Effect.CoefficientBp *= 2;
            variant.Overrides.Add(new AbilityValueOverride { Id = "upgrade.empowered.level50", MinimumLevel = 50,
                MinimumRank = 2, MaximumRank = 2, Field = AbilityValueField.CoefficientBp, Value = 60000 });
            skill.ValueOverrides.Add(new AbilityValueOverride { Id = "upgrade.base.level50", MinimumLevel = 50,
                Field = AbilityValueField.CoefficientBp, Value = 30000 });
            skill.Variants.Add(variant);
            Check(AbilityVariantValidator.Validate(character).Count == 0, "valid complete variant rank table.");
            var state = StudioSimulation.CreateFixture(character, 912, tier: 2);
            var fighter = state.Player.Fighters[0];
            var card = state.Player.Hand.First(c => c.OwnerFighterId == fighter.Id && c.SkillId == skill.Id);
            card.Rank = 2;
            var stableId = card.Id;
            AbilityVariantSystem.SetUnlocked(state, fighter, skill.Id, variant.Id, true);
            CharacterPassiveRuntime.ChangeResource(state, fighter, "resource.charge", 6, 6);
            Check(AbilityVariantSystem.Select(fighter, skill.Id, 2) == null, "level readiness independent of tier and resource.");
            fighter.Level = 49;
            Check(AbilityVariantSystem.TryResolve(fighter, card, out var level49) && card.VariantId == variant.Id,
                "unlocked and ready EX resolves using the existing card.");
            Check(level49.CoefficientBp == variant.Ranks.Single(r => r.Rank == 2).Effect.CoefficientBp,
                "level49 retains its selected rank coefficient.");
            fighter.Level = 50;
            Check(AbilityVariantSystem.TryResolve(fighter, card, out var level50) && level50.CoefficientBp == 60000,
                "level50 rank2 override applies after base upgrade.");
            card.Rank = 1;
            AbilityVariantSystem.TryResolve(fighter, card, out var rank1);
            Check(rank1.CoefficientBp == 30000, "rank2-specific override does not affect rank1; base upgrade still applies.");
            card.Rank = 2;
            fighter.ConstellationTier = 1;
            AbilityVariantSystem.TryResolve(fighter, card, out var tier1);
            Check(card.VariantId == null && tier1.CoefficientBp == 30000, "tier gate relocks variant without undoing level upgrade.");
            fighter.ConstellationTier = 2;
            AbilityVariantSystem.RefreshCards(state);
            Check(card.Id == stableId && card.SkillId == skill.Id && card.Rank == 2,
                "readiness changes preserve card identity, base skill identity and rank.");
            var sourceBefore = SnapshotJson.Serialize(fighter.Definition);
            var stateBefore = SnapshotJson.Serialize(state);
            var plan = new TurnPlan { RequestId = "fixture.variant.play", ExpectedRevision = state.Revision,
                Actions = new List<PlannedAction> { new PlannedAction { CardId = card.Id, TargetFighterId = state.Opponent.Fighters[0].Id } } };
            Check(BattleEngine.TryResolvePlan(state, plan, out var next, out var error), error ?? "real plan rejected.");
            var played = next.Events.Last(e => e.Kind == BattleEventKind.CardPlayed && e.SourceId == fighter.Id);
            Check(played.Card.VariantId == variant.Id && played.Card.Rank == 2, "committed playback card freezes the chosen variant and rank.");
            var nextFighter = next.Player.Fighters[0];
            Check(CharacterPassiveRuntime.Counter(nextFighter, "resource.charge") == 3, "variant cost commits once.");
            Check(AbilityVariantSystem.Select(nextFighter, skill.Id, 2) == null, "one-use variant relocks despite enough remaining charge.");
            Check(SnapshotJson.Serialize(state) == stateBefore, "plan leaves original state immutable.");
            Check(SnapshotJson.Serialize(nextFighter.Definition) == sourceBefore && skill.Ranks[0].Effect.CoefficientBp == originalCoefficient,
                "overrides and costs never mutate the source definition.");
            Check(BattleEngine.TryResolvePlan(SnapshotJson.Deserialize<BattleState>(stateBefore), plan, out var replay, out _) &&
                StudioSimulation.StateHash(replay) == StudioSimulation.StateHash(next), "snapshot replay reproduces variant outcome and RNG.");
            var clone = next.Clone();
            clone.Player.Fighters[0].AbilityVariants[0].Unlocked = true;
            Check(!nextFighter.AbilityVariants[0].Unlocked, "cloned readiness is independent.");
            var broken = character.Clone(); broken.Skills[0].Variants[0].Ranks.RemoveAt(0);
            Check(AbilityVariantValidator.Validate(broken).Count > 0, "missing rank rejected.");
            broken = character.Clone(); broken.Skills[0].Variants[0].MinimumLevel = 0;
            Check(AbilityVariantValidator.Validate(broken).Count > 0, "invalid level rejected.");
            broken = character.Clone(); broken.Skills[0].Variants[0].Overrides[0].EffectId = "missing";
            Check(AbilityVariantValidator.Validate(broken).Count > 0, "dangling effect override rejected.");
            Console.WriteLine("PASS: " + count + " M3 variant checks.");
            return count;
        }
    }
}
