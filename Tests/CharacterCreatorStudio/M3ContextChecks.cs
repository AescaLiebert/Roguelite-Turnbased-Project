using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;

namespace CharacterCreatorStudio.Checks
{
    public static class M3ContextChecks
    {
        public static int Run()
        {
            var checks = 0;
            void Check(bool value, string why) { if (!value) throw new InvalidOperationException("M3 context: " + why); checks++; }
            var source = SnapshotJson.Deserialize<CharacterDefinition>(File.ReadAllText("Artifacts/CharacterCreatorStudio/M0/KaeyaLegacyDefinition.json"));
            source.Passive = new PassiveDefinition { Id = "fixture.context" };
            var state = StudioSimulation.CreateFixture(source, 12);
            var actor = state.Player.Fighters[0]; var original = state.Opponent.Fighters[0]; var selected = state.Opponent.Fighters[1];
            var context = new CardEffectContext { Battle = state, Actor = actor, EffectOwner = actor, SelectedTarget = selected,
                EventFact = new PassiveFact { Kind = PassiveEventKind.BeforeAction, ActorId = actor.Id, TargetId = original.Id } };
            Check(CardEffectSystem.EventTarget(context) == original, "loop selection does not replace immutable fact target.");
            var query = new TargetQueryDefinition { BaseScope = EffectTargetScope.Self, Anchor = TargetQueryAnchor.EventTarget };
            Check(TargetQuerySystem.Resolve(query, context).Single() == original, "event-target anchor remains the original target.");
            query = new TargetQueryDefinition { BaseScope = EffectTargetScope.AllEnemies, Anchor = TargetQueryAnchor.Owner, ExcludeEventTarget = true };
            var candidates = TargetQuerySystem.Resolve(query, context);
            Check(!candidates.Contains(original) && candidates.Contains(selected), "random-other exclusions use original event target.");
            context.SelectedTarget = state.Opponent.Fighters[2];
            query.ExcludeEventTarget = false; query.AdjacentToEventTarget = true;
            Check(TargetQuerySystem.Resolve(query, context).Single() == selected, "adjacency uses immutable logical event slot.");
            var operations = new CardEffectRecipeDefinition { Operations = new List<CardEffectOperationDefinition> {
                new CardEffectOperationDefinition { Id = "original", Target = EffectTargetScope.TriggerTarget },
                new CardEffectOperationDefinition { Id = "loop", Order = 1, Target = EffectTargetScope.OperationTarget } } };
            var resolved = CardEffectSystem.ResolveWindow(operations, CardEffectWindow.BeforeAction, context);
            Check(resolved[0].Target == original && resolved[1].Target == context.SelectedTarget, "explicit scopes distinguish original and loop target.");
            var condition = new EffectConditionDefinition { Kind = EffectConditionKind.TargetGaugeAtLeast,
                Subject = ConditionSubject.EventTarget, Threshold = 4 };
            original.PowerGauge = 4; selected.PowerGauge = 0;
            Check(CardEffectSystem.ConditionsPass(new List<EffectConditionDefinition> { condition }, context, selected), "event-target condition ignores loop target gauge.");
            condition.Subject = ConditionSubject.OperationTarget;
            Check(!CardEffectSystem.ConditionsPass(new List<EffectConditionDefinition> { condition }, context, selected), "operation-target condition evaluates loop selection.");

            PassiveActionDefinition Mark(string id, EffectTargetScope scope, string recipe) => new PassiveActionDefinition { Id = id, Kind = PassiveActionKind.Command,
                Commands = new List<PassiveCommandDefinition> { new PassiveCommandDefinition { Kind = PassiveCommandKind.ExecuteEffect,
                    Effect = new CardEffectOperationDefinition { Id = id + ".operation", Kind = CardEffectOperationKind.ApplyStatus, Target = scope,
                        Status = new StatusApplicationRecipe { InlineRecipe = new StatusRecipeDefinition { Id = recipe, Polarity = StatusPolarity.Debuff,
                            Stacking = StatusStackingPolicy.AddStacks, MaxStacks = 3, DurationClock = StatusDurationClock.Permanent } } } } } };
            actor.Definition.Passive.Reactions.Add(new PassiveReactionDefinition { Id = "rule.loop", ExecutionVersion = 1,
                Trigger = PassiveEventKind.BeforeAction, Actions = new List<PassiveActionDefinition> { new PassiveActionDefinition {
                    Id = "action.loop", Kind = PassiveActionKind.ForEachTarget, MaximumTargets = 3,
                    Query = new TargetQueryDefinition { BaseScope = EffectTargetScope.AllEnemies, Anchor = TargetQueryAnchor.Owner, Limit = 3 },
                    Children = new List<PassiveActionDefinition> { Mark("action.original", EffectTargetScope.TriggerTarget, "fixture.original"),
                        Mark("action.current", EffectTargetScope.OperationTarget, "fixture.current") } } } });
            CharacterPassiveRuntime.EmitFact(state, new PassiveFact { Kind = PassiveEventKind.BeforeAction, ActorId = actor.Id,
                TargetId = original.Id, TurnSide = actor.Side, RootActionId = "fixture.loop" });
            Check(StatusSystem.Count(original, recipeId: "fixture.original") == 3 && StatusSystem.Count(selected, recipeId: "fixture.original") == 0,
                "nested runtime loop retains one original target for all three trigger-target effects.");
            Check(state.Opponent.LivingActive().All(f => StatusSystem.Count(f, recipeId: "fixture.current") == 1),
                "operation-target effects apply once to each selected loop member.");
            Console.WriteLine("PASS: " + checks + " M3 immutable event/operation context checks.");
            return checks;
        }
    }
}
