using System;
using System.Linq;
using System.Collections.Generic;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

internal static partial class BattlePlaybackChecks
{
    private static BattleState PlayKind(BattleState state, CardCategory kind, EffectTargetScope scope,
        EffectDefinition effect, string targetId = null)
    {
        var owner = state.Team(state.ActingSide).LivingActive()[0];
        var skill = owner.Definition.Skills[0];
        skill.Category = kind; skill.TargetScope = scope;
        skill.Ranks[0].Effect = effect;
        var card = new CardState { Id = "test-kind:" + state.Revision, OwnerFighterId = owner.Id,
            SkillId = skill.Id, Rank = 1, Category = kind, TargetScope = scope };
        state.Team(state.ActingSide).Hand.Clear(); state.Team(state.ActingSide).Hand.Add(card);
        var draft = new PlanDraft(state);
        Check(draft.QueuePlay(card.Id, targetId ?? state.OtherTeam(state.ActingSide).LivingActive()[0].Id, out var error), "Kind plan rejected: " + error);
        Check(BattleEngine.TryResolvePlan(state, draft.BuildPlan("kind:" + state.Revision), out var next, out error), "Kind resolution failed: " + error);
        return next;
    }

    private static StatusRecipeDefinition CounterStance(bool evade = false) => new StatusRecipeDefinition {
        Id = "test.stance", Polarity = StatusPolarity.Buff, Behavior = StatusBehavior.Stance,
        DurationClock = StatusDurationClock.TargetTurnEnd, DefaultDuration = 2,
        StanceChildren = new List<StanceChildDefinition> {
            new StanceChildDefinition { Id = "test.taunt", Taunt = true },
            new StanceChildDefinition { Id = "test.evade", EvadeAttacks = evade },
            new StanceChildDefinition { Id = "test.counter", CounterEnabled = true, CounterCategory = CardCategory.Debuff,
                CounterEffect = new CounterEffectDefinition { Kind = EffectKind.ApplyStatus,
                    StatusRecipeId = "test.ignite" } }
        }
    };

    private static void ActionTimingChecks()
    {
        var state = Create(824);
        var effect = new EffectDefinition { CoefficientBp = 15000 };
        foreach (CardEffectTiming timing in Enum.GetValues(typeof(CardEffectTiming)))
            effect.Sequence.Add(new CardEffectStep { Timing = timing, Effect = new EffectDefinition {
                Kind = EffectKind.ApplyStatus, Target = EffectTargetScope.AllEnemies, StatusRecipe = new StatusRecipeDefinition {
                    Id = "timing." + timing, Polarity = StatusPolarity.Debuff, DefaultDuration = 5 } } });
        var before = state.Events.Count;
        var next = PlayKind(state, CardCategory.AttackDebuff, EffectTargetScope.AllEnemies, effect);
        var events = next.Events.Skip(before).ToList();
        var played = events.FindIndex(e => e.Kind == BattleEventKind.CardPlayed);
        var firstHit = events.FindIndex(e => e.Kind == BattleEventKind.DamageApplied);
        var lastHit = events.FindLastIndex(e => e.Kind == BattleEventKind.DamageApplied && e.CardId != null);
        var afterAction = events.FindIndex(e => e.Kind == BattleEventKind.ActionTiming && e.Timing == CardEffectTiming.AfterAction);
        var completed = events.FindIndex(e => e.Kind == BattleEventKind.ActionCompleted);
        Check(events.Count(e => e.Kind == BattleEventKind.DamageApplied && e.Amount > 0) == 3,
            "Attack+Debuff must damage all three targets.");
        Check(events.FindLastIndex(e => e.StatusRecipeId == "timing.BeforeAction") < played,
            "BeforeAction must precede the action animation.");
        Check(events.FindIndex(e => e.StatusRecipeId == "timing.Damaging") > played &&
            events.FindLastIndex(e => e.StatusRecipeId == "timing.Damaging") < firstHit,
            "Damaging effects must resolve at action impact before the damage packet.");
        Check(events.FindIndex(e => e.StatusRecipeId == "timing.AfterDamage") > lastHit,
            "AfterDamage must follow the last AOE target hit, not run between targets.");
        Check(events.FindIndex(e => e.StatusRecipeId == "timing.AfterAction") > afterAction &&
            events.FindLastIndex(e => e.StatusRecipeId == "timing.AfterAction") < completed,
            "AfterAction feedback must be enclosed by return/completion barriers.");
        var resolutionStart = events.FindIndex(e => e.Kind == BattleEventKind.StatusResolutionStarted);
        var resolutionEnd = events.FindIndex(e => e.Kind == BattleEventKind.StatusResolutionEnded);
        var turnEnd = events.FindIndex(e => e.Kind == BattleEventKind.TurnEnded);
        var incomingTurn = events.FindIndex(e => e.Kind == BattleEventKind.TurnStarted);
        var incomingResolve = events.FindLastIndex(e => e.Kind == BattleEventKind.StatusResolutionStarted);
        var incomingResolved = events.FindLastIndex(e => e.Kind == BattleEventKind.StatusResolutionEnded);
        Check(resolutionStart > completed && resolutionEnd > resolutionStart && turnEnd > resolutionEnd,
            "Outgoing team status resolution must finish before camera/turn change.");
        Check(incomingTurn > turnEnd && incomingResolve > incomingTurn && incomingResolved > incomingResolve,
            "Incoming team start effects must resolve after its turn-change event.");
        Check(events.Where(e => e.Kind == BattleEventKind.DamageApplied && e.Message.StartsWith("Status tick:"))
            .All(e => (next.Player.FindFighter(e.TargetId) ?? next.Opponent.FindFighter(e.TargetId)).Side ==
                (e.SourceId.StartsWith("Player:") ? TeamSide.Player : TeamSide.Opponent)),
            "Each team's turn-ending tick must resolve inside that team's own outgoing phase.");
        var display = state.Clone(); foreach (var item in events) BattlePlaybackState.Apply(display, item);
        EqualDisplay(display, next);
    }

    private static void ConditionalEffectAndCounterChecks()
    {
        EffectDefinition ConditionalParalyze()
        {
            var paralyze = StandardEffectDatabase.CreateStatusRecipes()
                .Find(recipe => recipe.Id == "status.debuff.paralyze");
            var effect = new EffectDefinition { Scaling = StatScaling.Attack, CoefficientBp = 35000 };
            effect.Sequence.Add(new CardEffectStep
            {
                Timing = CardEffectTiming.AfterDamage,
                Effect = new EffectDefinition
                {
                    Kind = EffectKind.ApplyStatus,
                    Target = EffectTargetScope.SelectedEnemy,
                    StatusRecipe = paralyze,
                    StatusDurationOverride = 1,
                    Conditions = new List<EffectConditionDefinition>
                    {
                        new EffectConditionDefinition
                        {
                            Kind = EffectConditionKind.TargetTraitIs,
                            Subject = ConditionSubject.OperationTarget,
                            StringValue = "women"
                        }
                    }
                }
            });
            return effect;
        }

        var state = Create(825);
        var woman = state.Opponent.LivingActive()[0];
        woman.Definition.TraitIds.Add("trait.women");
        var before = state.Events.Count;
        var next = PlayKind(state, CardCategory.AttackDebuff, EffectTargetScope.SelectedEnemy,
            ConditionalParalyze(), woman.Id);
        Check(next.Events.Skip(before).Any(e => e.Kind == BattleEventKind.StatusApplied &&
            e.TargetId == woman.Id && e.StatusRecipeId == "status.debuff.paralyze"),
            "A trait-gated ultimate step must apply Paralyze to a Women target.");

        state = Create(826);
        var man = state.Opponent.LivingActive()[0];
        before = state.Events.Count;
        next = PlayKind(state, CardCategory.AttackDebuff, EffectTargetScope.SelectedEnemy,
            ConditionalParalyze(), man.Id);
        Check(!next.Events.Skip(before).Any(e => e.StatusRecipeId == "status.debuff.paralyze"),
            "A trait-gated ultimate step must not apply Paralyze to a non-Women target.");

        StatusRecipeDefinition WomenDefenseCounter() => new StatusRecipeDefinition
        {
            Id = "test.benimaru99.guard",
            Polarity = StatusPolarity.Buff,
            Behavior = StatusBehavior.Stance | StatusBehavior.Taunt,
            DefaultDuration = 3,
            CounterEnabled = true,
            CounterCategory = CardCategory.Debuff,
            CounterTarget = EffectTargetScope.SelectedEnemy,
            CounterConditions = new List<EffectConditionDefinition>
            {
                new EffectConditionDefinition
                {
                    Kind = EffectConditionKind.TargetTraitIs,
                    Subject = ConditionSubject.OperationTarget,
                    StringValue = "women"
                }
            },
            CounterEffect = new CounterEffectDefinition
            {
                Kind = EffectKind.ApplyStatus,
                Target = EffectTargetScope.SelectedEnemy,
                StatusRecipeId = "status.debuff.stat.defense",
                StatusPotencyBp = 2500,
                StatusDurationOverride = 2
            }
        };

        state = Create(827);
        var attacker = state.Player.LivingActive()[0];
        attacker.Definition.TraitIds.Add("trait.women");
        attacker.Definition.BaseStats.Defense = 100;
        var defender = state.Opponent.LivingActive()[0];
        StatusSystem.Apply(defender, defender.Id, defender.Side, WomenDefenseCounter(), "women-counter", "setup", 1);
        before = state.Events.Count;
        next = PlayKind(state, CardCategory.Attack, EffectTargetScope.SelectedEnemy,
            new EffectDefinition { Scaling = StatScaling.Attack }, defender.Id);
        var resolvedAttacker = next.Player.FindFighter(attacker.Id);
        Check(next.Events.Skip(before).Any(e => e.Kind == BattleEventKind.CounterStarted),
            "A Women attacker must trigger the conditional status counter.");
        Check(StatusSystem.GetEffectiveStats(resolvedAttacker).Defense == 75,
            "The conditional counter must reduce the Women attacker's Defense by 25%.");

        state = Create(828);
        attacker = state.Player.LivingActive()[0];
        defender = state.Opponent.LivingActive()[0];
        StatusSystem.Apply(defender, defender.Id, defender.Side, WomenDefenseCounter(), "women-counter", "setup", 1);
        before = state.Events.Count;
        next = PlayKind(state, CardCategory.Attack, EffectTargetScope.SelectedEnemy,
            new EffectDefinition { Scaling = StatScaling.Attack }, defender.Id);
        Check(!next.Events.Skip(before).Any(e => e.Kind == BattleEventKind.CounterStarted),
            "A non-Women attacker must not trigger the conditional status counter.");
    }

    private static void CardKindAndStanceChecks()
    {
        var state = Create(812);
        var defender = state.Opponent.LivingActive()[0];
        StatusSystem.Apply(defender, defender.Id, defender.Side, CounterStance(), "stance", "action", 1);
        Check(defender.Statuses.Instances.Count == 4, "Stance must create independently visible child buffs.");
        var before = state.Events.Count;
        var next = PlayKind(state, CardCategory.Debuff, EffectTargetScope.AllEnemies,
            new EffectDefinition { CoefficientBp = int.MaxValue, Sequence = new List<CardEffectStep> {
                new CardEffectStep { Timing = CardEffectTiming.AfterDamage, Effect = new EffectDefinition {
                    Kind = EffectKind.ApplyStatus, Target = EffectTargetScope.AllEnemies, StatusRecipe = new StatusRecipeDefinition {
                        Id = "test.pure-debuff", Polarity = StatusPolarity.Debuff,
                        Stacking = StatusStackingPolicy.AddStacks, MaxStacks = 20 } } } } });
        var events = next.Events.Skip(before).ToList();
        Check(!events.Any(e => e.Kind == BattleEventKind.DamageApplied || e.Kind == BattleEventKind.CounterStarted),
            "Pure debuff must bypass damage packets and counters despite an authored multiplier.");
        Check(events.Count(e => e.Kind == BattleEventKind.StatusApplied && e.StatusRecipeId == "test.pure-debuff") == 3,
            "AOE pure debuff must apply exactly once per target.");

        state = Create(813); defender = state.Opponent.LivingActive()[0];
        StatusSystem.Apply(defender, defender.Id, defender.Side, CounterStance(), "stance", "action", 1);
        before = state.Events.Count;
        next = PlayKind(state, CardCategory.Attack, EffectTargetScope.AllEnemies, new EffectDefinition());
        events = next.Events.Skip(before).ToList();
        var counterIndex = events.FindIndex(e => e.Kind == BattleEventKind.CounterStarted);
        Check(counterIndex > events.FindLastIndex(e => e.Kind == BattleEventKind.DamageApplied), "Counter must follow all AOE damage.");
        Check(events.Count(e => e.Kind == BattleEventKind.CounterStarted) == 1 && events.Any(e => e.Kind == BattleEventKind.CounterEnded),
            "One counter session per stance per attacking card.");
        Check(events.Any(e => e.StatusRecipeId == "test.ignite" && e.TargetId == state.Player.LivingActive()[0].Id),
            "Debuff counter must apply to the original attacker.");
        Check(!events.Any(e => e.Kind == BattleEventKind.DamageApplied && e.SourceId == defender.Id), "Debuff counter must not deal damage.");
        var display = state.Clone(); foreach (var item in events) BattlePlaybackState.Apply(display, item);
        EqualDisplay(display, next);

        state = Create(814); defender = state.Opponent.LivingActive()[0];
        StatusSystem.Apply(defender, defender.Id, defender.Side, CounterStance(true), "stance", "action", 1);
        var card = state.Player.Hand[0];
        var draft = new PlanDraft(state);
        Check(!draft.QueuePlay(card.Id, state.Opponent.LivingActive()[1].Id, out _), "Taunt must block other single enemy targets.");
        before = state.Events.Count;
        next = PlayKind(state, CardCategory.Attack, EffectTargetScope.SelectedEnemy,
            new EffectDefinition { KeywordId = "attack.cancel-stance" }, defender.Id);
        events = next.Events.Skip(before).ToList();
        Check(events.Any(e => e.Kind == BattleEventKind.StatusRemoved && e.Message == "Stance removed."), "Cancel stance must emit removal.");
        Check(events.Any(e => e.Kind == BattleEventKind.DamageApplied) && !events.Any(e => e.Kind == BattleEventKind.CounterStarted || e.Kind == BattleEventKind.AttackEvaded),
            "Stance removal must cancel evade and counter before damage.");
        Check(next.Opponent.FindFighter(defender.Id).Statuses.Instances.Count == 0, "No orphan stance effects may survive.");

        state = Create(815); defender = state.Opponent.LivingActive()[0];
        var immunity = CounterStance(true); immunity.StanceChildren.Add(new StanceChildDefinition { Id = "test.immunity", DebuffImmunity = true });
        StatusSystem.Apply(defender, defender.Id, defender.Side, immunity, "stance", "action", 1);
        Check(!StatusSystem.Apply(defender, "enemy", TeamSide.Player,
            new StatusRecipeDefinition { Id = "blocked", Polarity = StatusPolarity.Debuff }, "debuff", null, 2).Accepted,
            "Stance immunity must reject incoming debuffs.");
        before = state.Events.Count;
        next = PlayKind(state, CardCategory.Attack, EffectTargetScope.SelectedEnemy, new EffectDefinition(), defender.Id);
        Check(next.Events.Skip(before).Any(e => e.Kind == BattleEventKind.AttackEvaded), "Evade stance must skip the attack packet.");
        defender.Statuses.Advance(StatusDurationClock.TargetTurnEnd); defender.Statuses.Advance(StatusDurationClock.TargetTurnEnd);
        Check(defender.Statuses.Instances.Count == 0, "Expiry must remove every stance child.");

        state = Create(816);
        foreach (var f in state.Player.LivingActive()) f.Health = 100;
        before = state.Events.Count;
        next = PlayKind(state, CardCategory.Recovery, EffectTargetScope.AllAllies,
            new EffectDefinition { Kind = EffectKind.Heal, Target = EffectTargetScope.AllAllies,
                HealValue = new EffectValueDefinition { Source = EffectValueSource.Fixed, FixedAmount = 50 } });
        Check(next.Events.Skip(before).Count(e => e.Kind == BattleEventKind.HealApplied) == 3, "Team recovery must heal once per active ally.");
        Check(next.Player.LivingActive().All(f => f.Health == 150), "All allies must receive the same heal.");
        Check(next.Opponent.LivingActive().All(f => f.Health == 450), "Friendly recovery must never touch enemies.");

        state = Create(818);
        var owner = state.Player.LivingActive()[0];
        var stanceEffect = new EffectDefinition { Kind = EffectKind.ApplyStatus, Target = EffectTargetScope.Self, StatusRecipe = CounterStance() };
        stanceEffect.StatusRecipe.DefaultDuration = 1;
        before = state.Events.Count;
        next = PlayKind(state, CardCategory.Stance, EffectTargetScope.Self, stanceEffect, owner.Id);
        Check(next.Player.FindFighter(owner.Id).Statuses.Instances.Count == 4, "One-turn stance must survive activation turn.");
        display = state.Clone();
        foreach (var item in next.Events.Skip(before)) BattlePlaybackState.Apply(display, item);
        Check(display.Player.FindFighter(owner.Id).Statuses.Instances.Count == 4, "Playback must not prematurely expire stance children.");
        Check(display.Player.FindFighter(owner.Id).Statuses.Instances[0].RemainingDuration == 1, "Playback parent duration must match authority.");
        var frozen = next.Events.First(e => e.Kind == BattleEventKind.StatusApplied && e.StatusesAfter != null).Clone();
        next.Player.FindFighter(owner.Id).Statuses.Instances.Clear();
        Check(frozen.StatusesAfter.Count == 4, "Status event snapshots must be isolated from future mutations.");

        state = Create(819); owner = state.Player.LivingActive()[0];
        owner.Definition.Skills[0].Ranks[1].HasCardKind = true;
        owner.Definition.Skills[0].Ranks[1].Category = CardCategory.Recovery;
        owner.Definition.Skills[0].Ranks[1].TargetScope = EffectTargetScope.AllAllies;
        state.Player.Hand.Clear();
        for (var i = 0; i < 2; i++) state.Player.Hand.Add(new CardState { Id = "rank:" + i,
            OwnerFighterId = owner.Id, SkillId = owner.Definition.Skills[0].Id, Rank = 1 });
        CardRules.MergeAdjacent(state.Player);
        Check(state.Player.Hand[0].Category == CardCategory.Recovery && state.Player.Hand[0].TargetScope == EffectTargetScope.AllAllies,
            "Rank merge must update gameplay category and target scope.");

        state = Create(817); defender = state.Opponent.LivingActive()[0];
        var recovery = new StatusRecipeDefinition { Id = "test.recovery-stance", Polarity = StatusPolarity.Buff,
            Behavior = StatusBehavior.Stance, DefaultDuration = 1, DurationClock = StatusDurationClock.TargetTurnStart,
            StanceChildren = new List<StanceChildDefinition> { new StanceChildDefinition { Id = "test.recover", RecoverDamageTakenBp = 8000 } } };
        StatusSystem.Apply(defender, defender.Id, defender.Side, recovery, "stance", "action", 1);
        before = state.Events.Count;
        next = PlayKind(state, CardCategory.Attack, EffectTargetScope.SelectedEnemy,
            new EffectDefinition { Scaling = StatScaling.Fixed, Magnitude = 100, Family = DamageFamily.True }, defender.Id);
        events = next.Events.Skip(before).ToList();
        var damage = events.First(e => e.Kind == BattleEventKind.DamageApplied).Amount;
        Check(events.Single(e => e.Message == "Stance recovery.").Amount == damage * 8000 / 10000,
            "Stance recovery must use actual damage received and occur before own-turn expiry.");
        Check(next.Opponent.FindFighter(defender.Id).Statuses.Instances.Count == 0, "Recovery stance must expire after its final trigger.");
    }
}
