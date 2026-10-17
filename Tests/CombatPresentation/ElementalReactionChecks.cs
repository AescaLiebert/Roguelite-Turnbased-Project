using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;

internal static partial class BattlePlaybackChecks
{
    private static ElementalRuleSet ElementRules() => ElementalRuleSet.CreateDefault();

    private static BattleState ElementFixture(ElementalRuleSet rules = null, TeamSide side = TeamSide.Player, bool mixedReserve = false)
    {
        CharacterDefinition[] Team(string prefix) => Enumerable.Range(0, 4).Select(i => {
            var fighter = Fighter(prefix + i, 100000, 100);
            fighter.TraitIds.Add(ElementalRuleSet.GenshinTrait);
            return fighter;
        }).ToArray();
        var player = Team("element-player-");
        if (mixedReserve) player[3].TraitIds.Clear();
        return BattleEngine.Create("elemental-fixture", player, null, Team("element-enemy-"), null,
            7391, firstSide: side, elementalRules: rules ?? ElementRules());
    }

    private static void ElementAura(FighterState target, ElementId element, string instance, string source = "fixture-source")
    {
        target.ElementStatuses.Apply(element, source, instance);
    }

    private static EffectDefinition ElementAttack(ElementId element, int hits = 1) => new EffectDefinition {
        Scaling = StatScaling.Attack, DamageElement = element, CoefficientBp = 10000,
        Attack = new DamageAttackDefinition { HitCount = hits } };

    private static void ElementalSliceChecks()
    {
        var start = _assertions;
        foreach (var element in new[] { ElementId.Pyro, ElementId.Hydro, ElementId.Electro, ElementId.Cryo, ElementId.Dendro })
        {
            var state = ElementFixture();
            var target = state.Opponent.Fighters[0];
            var next = PlayKind(state, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(element, 3));
            var after = next.Opponent.Fighters[0];
            Check(after.ElementStatuses.Instances.Count == 1 && after.ElementStatuses.Has(element), "Elemental hit did not apply unique aura.");
            Check(after.ElementStatuses.Instances[0].RemainingDuration == 3 && after.Statuses.Instances.Count == 0, "Aura is not neutral/independent/three turns.");
            Check(next.Events.Skip(state.Events.Count).Count(e => e.Kind == BattleEventKind.ElementStatusApplied) == 1, "Multi-hit aura label duplicated.");
            after.ElementStatuses.Instances[0].RemainingDuration = 1;
            Check(!after.ElementStatuses.Apply(element, "new-source", "new-id") && after.ElementStatuses.Instances[0].RemainingDuration == 1,
                "Same-element aura refreshed or stacked.");
            Check(StatusSystem.Remove(after, StatusPolarity.Debuff, true) == 0 && after.ElementStatuses.Has(element), "Cleanse removed a neutral aura.");
            Check(!CardEffectSystem.ConditionsPass(new List<EffectConditionDefinition> { new EffectConditionDefinition { Kind = EffectConditionKind.TargetHasDebuff } },
                new CardEffectContext { Battle = next, Actor = next.Player.Fighters[0], EffectOwner = next.Player.Fighters[0] }, after), "Aura counts toward debuff conditions.");
        }
        foreach (var element in new[] { ElementId.Anemo, ElementId.Geo })
        {
            var state = ElementFixture();
            var next = PlayKind(state, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(element));
            Check(next.Opponent.Fighters[0].ElementStatuses.Instances.Count == 0, "Driver created an aura.");
        }
        var immune = ElementFixture();
        StatusSystem.Apply(immune.Opponent.Fighters[0], "immune", TeamSide.Opponent,
            new StatusRecipeDefinition { Id = "immune", Polarity = StatusPolarity.Buff, DebuffImmunity = true }, "immune", "fixture", 1);
        var immuneNext = PlayKind(immune, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Pyro));
        Check(immuneNext.Opponent.Fighters[0].ElementStatuses.Has(ElementId.Pyro), "Debuff immunity blocked neutral aura.");
        var evade = ElementFixture();
        StatusSystem.Apply(evade.Opponent.Fighters[0], "evade", TeamSide.Opponent,
            new StatusRecipeDefinition { Id = "evade", Polarity = StatusPolarity.Buff, EvadeAttacks = true }, "evade", "fixture", 1);
        var evadeNext = PlayKind(evade, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Pyro));
        Check(evadeNext.Opponent.Fighters[0].ElementStatuses.Instances.Count == 0, "Evaded hit applied aura.");
        foreach (var recipe in new[] { "status.debuff.ignite", "status.debuff.shock", "status.debuff.frost-bite" })
        {
            var state = ElementFixture();
            StatusSystem.Apply(state.Opponent.Fighters[0], state.Player.Fighters[0].Id, TeamSide.Player,
                StandardEffectDatabase.CreateStatusRecipes().Find(r => r.Id == recipe), "legacy-dot", "fixture", 1);
            Check(ElementalReactionRules.Evaluate(state.ElementalRules, state.Opponent.Fighters[0], ElementId.Pyro) == null &&
                ElementalReactionRules.Evaluate(state.ElementalRules, state.Opponent.Fighters[0], ElementId.Cryo) == null, "Old DOT is still reaction fuel.");
            var next = PlayKind(state, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Hydro));
            Check(next.Opponent.Fighters[0].Statuses.Instances.Any(s => s.InstanceId == "legacy-dot"), "Elemental reaction removed an ordinary debuff.");
        }
        var lifetime = ElementFixture(); lifetime.Opponent.Fighters[0].ElementStatuses.Apply(ElementId.Dendro, "clock", "clock");
        for (var turn = 0; turn < 6; turn++)
        {
            Check(BattleEngine.TryResolvePlan(lifetime, new PlanDraft(lifetime).BuildPlan("clock:" + turn), out var next, out var error), error);
            lifetime = next;
            var aura = lifetime.Opponent.Fighters[0].ElementStatuses.Instances.SingleOrDefault();
            var ends = (turn + 1) / 2;
            Check(ends == 3 ? aura == null : aura?.RemainingDuration == 3 - ends, "Aura advanced on wrong side/clock.");
        }
        foreach (var row in new[] { (ElementId.Cryo, ElementId.Pyro, ElementalReactionId.Melt, 200),
            (ElementId.Pyro, ElementId.Cryo, ElementalReactionId.ReverseMelt, 150),
            (ElementId.Pyro, ElementId.Hydro, ElementalReactionId.Vaporize, 200),
            (ElementId.Hydro, ElementId.Pyro, ElementalReactionId.ReverseVaporize, 150) })
        foreach (var side in new[] { TeamSide.Player, TeamSide.Opponent })
        foreach (var hits in new[] { 1, 3, 7, 10 })
        {
            var state = ElementFixture(side: side);
            var target = state.OtherTeam(side).Fighters[0]; target.ElementStatuses.Apply(row.Item1, "setup", "amplify");
            var next = PlayKind(state, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(row.Item2, hits));
            var events = next.Events.Skip(state.Events.Count).ToList();
            Check(events.Where(e => e.Kind == BattleEventKind.DamageApplied && e.DamageOrigin == ElementalDamageOrigin.Card).Sum(e => e.Amount) == row.Item4,
                "Amplification did not cover the whole final action: " + row.Item3 + "/" + hits);
            Check(events.Count(e => e.Kind == BattleEventKind.ElementalReactionTriggered && e.ElementalReaction == row.Item3) == 1, "Amplifying reaction triggered more than once.");
            Check(next.OtherTeam(side).Fighters[0].ElementStatuses.Instances.Count == 0, "Consumed aura reapplied inside the same action.");
            var playback = state.Clone(); foreach (var evt in events) BattlePlaybackState.Apply(playback, evt);
            Check(SnapshotJson.Serialize(playback.OtherTeam(side).Fighters[0].ElementStatuses) == SnapshotJson.Serialize(next.OtherTeam(side).Fighters[0].ElementStatuses), "Replay lost aura consumption.");
        }
        var aoe = ElementFixture();
        aoe.Opponent.Fighters[0].ElementStatuses.Apply(ElementId.Cryo, "setup", "aoe-cryo");
        aoe.Opponent.Fighters[1].ElementStatuses.Apply(ElementId.Hydro, "setup", "aoe-hydro");
        var aoeEffect = ElementAttack(ElementId.Pyro, 7); aoeEffect.Target = EffectTargetScope.AllEnemies;
        var aoeNext = PlayKind(aoe, CardCategory.Attack, EffectTargetScope.AllEnemies, aoeEffect);
        foreach (var index in new[] { 0, 1, 2 })
        {
            var total = aoeNext.Events.Skip(aoe.Events.Count).Where(e => e.Kind == BattleEventKind.DamageApplied &&
                e.DamageOrigin == ElementalDamageOrigin.Card && e.TargetId == aoe.Opponent.Fighters[index].Id).Sum(e => e.Amount);
            Check(total == new[] { 200, 150, 100 }[index], "AOE amplification leaked between recipients or stopped after one hit.");
        }
        var riderState = ElementFixture(); var riderEffect = ElementAttack(ElementId.Electro, 3);
        riderEffect.Sequence.Add(new CardEffectStep { Timing = CardEffectTiming.AfterDamage, Effect = new EffectDefinition {
            Id = "independent-shock", Kind = EffectKind.ApplyStatus, StatusRecipe = StandardEffectDatabase.CreateStatusRecipes().Find(r => r.Id == "status.debuff.shock") } });
        var riderNext = PlayKind(riderState, CardCategory.AttackDebuff, EffectTargetScope.SelectedEnemy, riderEffect);
        var riderEvents = riderNext.Events.Skip(riderState.Events.Count).ToList();
        Check(riderEvents.FindIndex(e => e.Kind == BattleEventKind.StatusApplied) > riderEvents.FindLastIndex(e => e.Kind == BattleEventKind.DamageApplied) &&
            riderNext.Opponent.Fighters[0].ElementStatuses.Has(ElementId.Electro) &&
            riderNext.Opponent.Fighters[0].Statuses.Instances.Single(s => s.RecipeId == "status.debuff.shock").Snapshot.TriggeringHealthDamage == 100,
            "Ordinary status recipe was forced onto the first hit or lost its whole-action damage snapshot.");
        var mixed = ElementFixture(mixedReserve: true);
        mixed.Opponent.Fighters[0].ElementStatuses.Apply(ElementId.Cryo, "mixed", "mixed");
        var mixedNext = PlayKind(mixed, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Pyro));
        Check(!mixedNext.Events.Skip(mixed.Events.Count).Any(e => e.Kind == BattleEventKind.ElementalReactionTriggered) &&
            mixedNext.Opponent.Fighters[0].ElementStatuses.Has(ElementId.Cryo) && !mixedNext.Opponent.Fighters[0].ElementStatuses.Has(ElementId.Pyro), "Mixed-series roster applied an elemental aura.");
        Console.WriteLine("PASS: " + (_assertions - start) + " neutral aura and whole-action amplification assertions.");
    }
}
