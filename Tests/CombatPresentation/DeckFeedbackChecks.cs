using System;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

internal static partial class BattlePlaybackChecks
{
    private static CardState QueueCardFixture(string id, FighterState owner, int slot, int rank = 1) =>
        new CardState { Id = id, OwnerFighterId = owner.Id, SkillId = owner.Definition.Skills[slot - 1].Id,
            Kind = CardKind.Skill, Rank = rank, Category = CardCategory.Attack, EffectCategory = CardCategory.Attack };

    private static void DeckFeedbackQueueChecks()
    {
        DisabledCardCheckpointChecks();
        foreach (var side in new[] { TeamSide.Player, TeamSide.Opponent })
        {
            var state = Create(3101, side);
            var team = state.Team(side);
            var expectedOwners = team.LivingActive().AsEnumerable().Reverse()
                .SelectMany(fighter => new[] { fighter.Id, fighter.Id }).ToArray();
            var openingDraws = state.Events.Where(item => item.Kind == BattleEventKind.CardDrawn &&
                team.FindFighter(item.SourceId) != null).Take(expectedOwners.Length).ToList();
            Check(openingDraws.Select(item => item.SourceId).SequenceEqual(expectedOwners),
                "Opening hand must deal Pos3, then Pos2, then Pos1 on both sides.");
            var openingDisplay = BattlePlaybackState.BeforeOpeningDeal(state);
            foreach (var item in state.Events) BattlePlaybackState.Apply(openingDisplay, item);
            Check(openingDisplay.Team(side).Hand.Select(card => card.Id).SequenceEqual(team.Hand.Select(card => card.Id)),
                "Inverted opening order must survive event playback and intervening merges.");
            var owner = team.Fighters[0];
            var other = team.Fighters[1];
            var target = state.OtherTeam(side).Fighters[0];
            team.Hand.Clear();
            team.Hand.Add(QueueCardFixture("queue-first", owner, 2));
            team.Hand.Add(QueueCardFixture("queue-blocked", owner, 1));
            team.Hand.Add(QueueCardFixture("queue-continues", other, 2));
            state.ActionBudget = 3;
            owner.Definition.Skills[1].Ranks[0].Effect.Sequence.Add(new CardEffectStep {
                Timing = CardEffectTiming.AfterAction,
                Effect = new EffectStepDefinition { Kind = EffectKind.ApplyStatus, Target = EffectTargetScope.Self,
                    StatusRecipe = new StatusRecipeDefinition { Id = "queue.stun", Behavior = StatusBehavior.Disable,
                        Polarity = StatusPolarity.Debuff, DisableMask = CardCategoryMask.AllCards, DefaultDuration = 2 } } });
            var draft = new PlanDraft(state);
            foreach (var card in team.Hand)
                Check(draft.QueuePlay(card.Id, target.Id, out var draftError), draftError);
            var plan = draft.BuildPlan("queue-stun:" + side);
            Check(TurnExecutionState.TryCreate(state, plan, out var frozen, out var freezeError), freezeError);
            plan.Actions[0].CardId = "mutated-input";
            Check(frozen.Actions[0].Action.CardId == "queue-first", "Frozen queue aliases caller actions.");
            plan = draft.BuildPlan("queue-stun:" + side);
            var firstEventId = state.Events.Count;
            Check(BattleEngine.TryResolvePlan(state, plan, out var next, out var error), error);
            var events = next.Events.Skip(firstEventId).ToList();
            Check(events[0].Kind == BattleEventKind.TurnPlanCommitted && events[0].Plan.Actions.Count == 3,
                "Both sides must emit the entire frozen plan before resolving the first action.");
            Check(events[0].Plan.Actions.All(action => action.State == PlannedActionState.Pending),
                "Committed plan event changed while its actions executed.");
            Check(next.Execution.Actions.Select(action => action.State).SequenceEqual(new[] {
                PlannedActionState.Completed, PlannedActionState.Fizzled, PlannedActionState.Completed }),
                "Stun must fizzle the blocked action and allow another fighter's next action.");
            Check(events.Any(item => item.Kind == BattleEventKind.CardRemoved && item.CardId == "queue-blocked") &&
                !events.Any(item => item.Kind == BattleEventKind.CardPlayed && item.CardId == "queue-blocked"),
                "A mid-turn disabled card must be discarded instead of executing.");
            var replay = state.Clone();
            foreach (var item in events) BattlePlaybackState.Apply(replay, item);
            Check(replay.Execution.Actions.Select(action => action.State).SequenceEqual(next.Execution.Actions.Select(action => action.State)),
                "Display queue progress disagrees with authority after stun.");
            var eventCopy = events[0].Clone();
            eventCopy.Plan.Actions[0].Card.Rank = 99;
            eventCopy.Plan.Actions[0].Action.CardId = "changed-copy";
            Check(events[0].Plan.Actions[0].Card.Rank == 1 && events[0].Plan.Actions[0].Action.CardId == "queue-first",
                "Cloned plan event aliases cards or actions.");

            // A rank effect can merge away a committed card. This exercises the same missing-card
            // boundary needed by future direct card deletion effects, without inventing a new effect.
            state = Create(3102, side);
            team = state.Team(side); owner = team.Fighters[0]; target = state.OtherTeam(side).Fighters[0];
            team.Hand.Clear();
            team.Hand.Add(QueueCardFixture("delete-trigger", owner, 2));
            team.Hand.Add(QueueCardFixture("merge-survivor", owner, 1, 1));
            team.Hand.Add(QueueCardFixture("deleted-queued", owner, 1, 2));
            state.ActionBudget = 2;
            owner.Definition.Skills[1].Ranks[0].Effect.Sequence.Add(new CardEffectStep {
                Timing = CardEffectTiming.Damaging, Effect = new EffectStepDefinition {
                    Kind = EffectKind.ModifyCardRank, Target = EffectTargetScope.Self, Magnitude = -1 } });
            draft = new PlanDraft(state);
            Check(draft.QueuePlay("delete-trigger", target.Id, out error), error);
            Check(draft.QueuePlay("deleted-queued", target.Id, out error), error);
            firstEventId = state.Events.Count;
            Check(BattleEngine.TryResolvePlan(state, draft.BuildPlan("queue-delete:" + side), out next, out error), error);
            Check(next.Execution.Actions[1].State == PlannedActionState.Fizzled &&
                next.Events.Skip(firstEventId).Any(item => item.Kind == BattleEventKind.ActionFizzled && item.CardId == "deleted-queued"),
                "A committed card removed by a preceding effect must fizzle without rejecting the whole turn.");
            var committed = next.Events[firstEventId];
            Check(committed.Plan.Actions[1].Card.Rank == 2, "A queued card's draft rank must survive its later deletion.");
            Check(state.Team(side).Hand.Count == 3 && state.Execution == null,
                "Execution mutated the captured planning snapshot.");

            var victor = Fighter("queue-victor", 1000, 2000);
            victor.Skills[0].Ranks[0].Effect.Attack = new DamageAttackDefinition { HitCount = 10 };
            var victim = Fighter("queue-victim", 100, 1);
            state = BattleEngine.Create("queue-victory:" + side,
                new[] { side == TeamSide.Player ? victor : victim }, null,
                new[] { side == TeamSide.Opponent ? victor : victim }, null, 3103, side, cardDrawSeed: 3103);
            team = state.Team(side); owner = team.Fighters[0]; target = state.OtherTeam(side).Fighters[0];
            team.Hand.Clear();
            team.Hand.Add(QueueCardFixture("victory-first", owner, 1));
            team.Hand.Add(QueueCardFixture("victory-pending", owner, 2));
            state.ActionBudget = 2;
            draft = new PlanDraft(state);
            Check(draft.QueuePlay("victory-first", target.Id, out error), error);
            Check(draft.QueuePlay("victory-pending", target.Id, out error), error);
            firstEventId = state.Events.Count;
            Check(BattleEngine.TryResolvePlan(state, draft.BuildPlan("queue-victory:" + side), out next, out error), error);
            Check(next.Winner == side && next.Execution.Actions[1].State == PlannedActionState.Cancelled,
                "Victory must cancel remaining committed actions.");
            var victoryEvents = next.Events.Skip(firstEventId).ToList();
            Check(victoryEvents.Count(item => item.Kind == BattleEventKind.DamageApplied && item.CardId == "victory-first") == 10 &&
                victoryEvents.FindIndex(item => item.Kind == BattleEventKind.BattleCompleted) >
                    victoryEvents.FindLastIndex(item => item.Kind == BattleEventKind.DamageApplied),
                "Defeating the final enemy on the first hit must finish all ten hits before declaring victory.");
            replay = state.Clone();
            foreach (var item in next.Events.Skip(firstEventId)) BattlePlaybackState.Apply(replay, item);
            Check(replay.Execution.Actions[1].State == PlannedActionState.Cancelled,
                "Playback must cancel the pending queue when battle completes.");
        }
        Console.WriteLine("PASS: shared player/enemy frozen queue, mid-turn stun, removed queued cards, event isolation and queue replay.");
    }

    private static void DisabledCardCheckpointChecks()
    {
        foreach (var side in new[] { TeamSide.Player, TeamSide.Opponent })
        {
            foreach (var ultimate in new[] { false, true })
            {
                var state = Create(3110, side);
                var team = state.Team(side);
                var owner = team.Fighters[0];
                var other = team.Fighters[1];
                var target = state.OtherTeam(side).Fighters[0];
                owner.PowerGauge = ultimate ? 2 : 0;
                var initialGauge = owner.PowerGauge;
                var blocked = ultimate
                    ? new CardState { Id = "checkpoint-blocked", OwnerFighterId = owner.Id,
                        Kind = CardKind.Ultimate, Category = CardCategory.Ultimate, EffectCategory = CardCategory.Attack }
                    : QueueCardFixture("checkpoint-blocked", owner, 1);
                team.Hand.Clear();
                team.Hand.Add(blocked);
                team.Hand.Add(QueueCardFixture("checkpoint-following", other, 2));
                state.ActionBudget = 2;
                var disable = new StatusRecipeDefinition { Id = "checkpoint.disable", Polarity = StatusPolarity.Debuff,
                    Behavior = StatusBehavior.Disable, DisableMask = ultimate ? CardCategoryMask.Ultimate : CardCategoryMask.Attack,
                    DefaultDuration = 3 };
                Check(StatusSystem.Apply(owner, target.Id, target.Side, disable, "checkpoint-status", null, 1).Accepted,
                    "Checkpoint disable fixture did not apply.");

                var draft = new PlanDraft(state);
                Check(draft.QueuePlay(blocked.Id, target.Id, out var error), error);
                Check(draft.RemainingActions == 1 && draft.Preview.FindFighter(owner.Id).PowerGauge == initialGauge + 1,
                    "Disabled skill/ultimate must consume a draft slot and forecast +1 PG without paying ultimate cost.");
                Check(draft.QueuePlay("checkpoint-following", target.Id, out error), error);
                Check(draft.UndoLast() && draft.Preview.FindFighter(owner.Id).PowerGauge == initialGauge + 1,
                    "Undo/rebuild lost a disabled card's PG forecast.");
                draft.Reset();
                Check(draft.Actions.Count == 0 && draft.Preview.FindFighter(owner.Id).PowerGauge == initialGauge,
                    "Reset did not restore gauge before the disabled card was queued.");
                Check(draft.QueuePlay(blocked.Id, target.Id, out error), error);
                Check(draft.QueuePlay("checkpoint-following", target.Id, out error), error);
                var start = state.Events.Count;
                Check(BattleEngine.TryResolvePlan(state, draft.BuildPlan("checkpoint-discard:" + side + ultimate),
                    out var next, out error), error);
                var events = next.Events.Skip(start).ToList();
                Check(next.Execution.Actions.Select(action => action.State).SequenceEqual(new[] {
                    PlannedActionState.Fizzled, PlannedActionState.Completed }),
                    "A card disabled at planning must waste only its own slot and let the following action execute.");
                Check(events.Any(item => item.Kind == BattleEventKind.CardRemoved && item.CardId == blocked.Id) &&
                    !events.Any(item => item.Kind == BattleEventKind.CardPlayed && item.CardId == blocked.Id),
                    "Disabled card must be consumed without executing its effects.");
                Check(events.Any(item => item.Kind == BattleEventKind.PowerGaugeChanged && item.TargetId == owner.Id &&
                    item.Amount == 1 && item.PowerGaugeAfter == initialGauge + 1) &&
                    next.Team(side).FindFighter(owner.Id).PowerGauge == initialGauge + 1,
                    "Disabled card checkpoint must grant exactly +1 PG, including an unready disabled ultimate.");
                var replay = state.Clone();
                foreach (var item in events) BattlePlaybackState.Apply(replay, item);
                EqualDisplay(replay, next);
                Check(owner.PowerGauge == initialGauge && team.Hand.Count == 2,
                    "Draft/execution modified the captured planning state.");
            }

            foreach (var cleanseFirst in new[] { true, false })
            {
                var state = Create(3111, side);
                var team = state.Team(side);
                var owner = team.Fighters[0];
                var cleanser = team.Fighters[1];
                var target = state.OtherTeam(side).Fighters[0];
                owner.PowerGauge = 0;
                Check(StatusSystem.Apply(owner, target.Id, target.Side,
                    new StatusRecipeDefinition { Id = "checkpoint.disable-attack", Polarity = StatusPolarity.Debuff,
                        Behavior = StatusBehavior.Disable, DisableMask = CardCategoryMask.Attack, DefaultDuration = 3 },
                    "checkpoint-cleanse-status", null, 1).Accepted, "Cleanse checkpoint fixture did not apply.");
                cleanser.Definition.Skills[1].Ranks[0].Effect = new EffectDefinition {
                    Kind = EffectKind.Cleanse, Target = EffectTargetScope.SelectedAlly };
                var cleanse = QueueCardFixture("checkpoint-cleanse", cleanser, 2);
                cleanse.Category = cleanse.EffectCategory = CardCategory.Buff;
                cleanse.TargetScope = EffectTargetScope.SelectedAlly;
                var attack = QueueCardFixture("checkpoint-attack", owner, 1);
                team.Hand.Clear(); team.Hand.Add(cleanse); team.Hand.Add(attack);
                state.ActionBudget = 2;
                var draft = new PlanDraft(state);
                foreach (var card in cleanseFirst ? new[] { cleanse, attack } : new[] { attack, cleanse })
                    Check(draft.QueuePlay(card.Id, card == cleanse ? owner.Id : target.Id, out var reason), reason);
                var start = state.Events.Count;
                Check(BattleEngine.TryResolvePlan(state, draft.BuildPlan("checkpoint-cleanse:" + side + cleanseFirst),
                    out var next, out var error), error);
                var events = next.Events.Skip(start).ToList();
                var queuedAttack = next.Execution.Actions.First(action => action.Card.Id == attack.Id);
                Check(queuedAttack.State == (cleanseFirst ? PlannedActionState.Completed : PlannedActionState.Fizzled),
                    "Card availability must be checked at its execution checkpoint using prior cleanse effects.");
                Check(events.Any(item => item.Kind == BattleEventKind.DamageApplied && item.CardId == attack.Id) == cleanseFirst,
                    "Cleanse-before must unlock the queued card; cleanse-after must not replay a wasted slot.");
                Check(next.Team(side).FindFighter(owner.Id).PowerGauge == 1,
                    "Both an unlocked skill and a discarded disabled skill must grant +1 PG.");
                if (cleanseFirst)
                    Check(events.FindIndex(item => item.Kind == BattleEventKind.StatusRemoved && item.TargetId == owner.Id) <
                        events.FindIndex(item => item.Kind == BattleEventKind.CardPlayed && item.CardId == attack.Id),
                        "Cleanse removal must precede the unlocked card's execution.");
                var replay = state.Clone();
                foreach (var item in events) BattlePlaybackState.Apply(replay, item);
                EqualDisplay(replay, next);
            }
        }
    }
}
