using System;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

internal static partial class BattlePlaybackChecks
{
    private static BattleState GenshinFixture(string first, int tier = 0)
    {
        var roster = GenshinStarterRoster.Create();
        var team = new[] { roster.Single(f => f.Id == "fighter." + first) }.Concat(roster.Where(f => f.Id != "fighter." + first)).Take(4).ToArray();
        var enemies = Enumerable.Range(0, 4).Select(i => Fighter("genshin-dummy-" + i, 100000, 1000)).ToArray();
        foreach (var enemy in enemies) { enemy.TraitIds.Add(ElementalRuleSet.GenshinTrait); enemy.BaseStats.CritResistanceBp = 100000; }
        return BattleEngine.Create("genshin-kit", team, new[] { tier, 0, 0, 0 }, enemies, null, 92763);
    }

    private static BattleState PlayAuthored(BattleState state, int slot, int rank = 3, bool ultimate = false, TeamSide side = TeamSide.Player, string targetId = null)
    {
        state.ActingSide = side;
        var team = state.Team(side);
        var actor = team.LivingActive()[0];
        var skill = actor.Definition.Skills[slot - 1];
        var tier = actor.Definition.UltimateTiers[actor.ConstellationTier];
        var effect = ultimate ? tier.Effect : skill.Ranks[rank - 1].Effect;
        if (ultimate) actor.PowerGauge = 5;
        var card = new CardState { Id = "kit-card:" + state.Revision, OwnerFighterId = actor.Id, Kind = ultimate ? CardKind.Ultimate : CardKind.Skill,
            SkillId = skill.Id, Rank = rank, UltimateTier = actor.ConstellationTier,
            Category = ultimate ? tier.Category : skill.Category, TargetScope = effect.Target };
        team.Hand.Clear(); team.Hand.Add(card);
        var draft = new PlanDraft(state);
        Check(draft.QueuePlay(card.Id, targetId ?? state.OtherTeam(side).LivingActive()[0].Id, out var error), "Authored kit queue failed: " + error);
        Check(BattleEngine.TryResolvePlan(state, draft.BuildPlan(card.Id), out var next, out error), "Authored kit resolution failed: " + error);
        return next;
    }

    private static void GenshinKitChecks()
    {
        var start = _assertions;
        foreach (var fighter in GenshinStarterRoster.Create())
        {
            Check(ElementalContentValidator.Validate(fighter, ElementalRuleSet.CreateDefault()).Count == 0, "Invalid elemental kit: " + fighter.Id);
            Check(PassiveRuleValidator.Validate(fighter.Passive).Count == 0, "Invalid passive: " + fighter.Id);
            Check(fighter.Skills.Count == 2 && fighter.Skills.All(s => s.Ranks.Count == 3) && fighter.UltimateTiers.Count == 6,
                "Incomplete authored kit: " + fighter.Id);
        }
        var amber = GenshinFixture("amber");
        var marked = PlayAuthored(amber, 1);
        Check(marked.Opponent.LivingActive().All(f => f.Statuses.Instances.Any(s => s.RecipeId == "status.debuff.baron-bunny")), "Bunny marker was not applied.");
        var wait = new PlanDraft(marked).BuildPlan("bunny-expiry");
        Check(BattleEngine.TryResolvePlan(marked, wait, out var exploded, out var error), error);
        var explosions = exploded.Events.Skip(marked.Events.Count).Where(e => e.DamageOrigin == ElementalDamageOrigin.TriggeredAttack).ToList();
        Check(explosions.Count == 3 && explosions.All(e => e.Amount == 952 && e.DamageElement == ElementId.Pyro),
            "Bunny must use the Normal elemental formula (150% ATK plus Pierce) with explosion doubling exactly once: " + string.Join(",", explosions.Select(e => e.Amount)));
        Check(exploded.Opponent.LivingActive().All(f => f.Statuses.Instances.Any(s => s.RecipeId == "status.debuff.ignite" && s.RemainingDuration == 4)),
            "Bunny must apply ordinary four-turn Ignite.");
        var cleansed = marked.Clone();
        foreach (var target in cleansed.Opponent.LivingActive()) StatusSystem.Remove(target, StatusPolarity.Debuff, true);
        Check(BattleEngine.TryResolvePlan(cleansed, new PlanDraft(cleansed).BuildPlan("bunny-cleanse"), out var safe, out error), error);
        Check(!safe.Events.Skip(cleansed.Events.Count).Any(e => e.DamageOrigin == ElementalDamageOrigin.TriggeredAttack), "Cleansed Bunny exploded.");
        var lethalMarker = marked.Clone(); lethalMarker.Opponent.Fighters[0].Health = 10;
        Check(BattleEngine.TryResolvePlan(lethalMarker, new PlanDraft(lethalMarker).BuildPlan("bunny-lethal"), out var lethal, out error), error);
        Check(!lethal.Opponent.Fighters[0].IsAlive && lethal.Events.Skip(lethalMarker.Events.Count).Any(e => e.Kind == BattleEventKind.FighterDefeated),
            "Delayed lethal damage did not finalize defeat.");

        var kaeya = GenshinFixture("kaeya");
        Check(kaeya.Player.LivingActive().All(f => f.PowerGauge == 1), "Kaeya opening gauge was not granted once.");
        var waltz = PlayAuthored(kaeya, 1, ultimate: true);
        Check(waltz.Player.LivingActive().Count(f => f.Statuses.Instances.Any(s => s.RecipeId == "status.buff.glacial-waltz")) == 1,
            "C0 Waltz must protect only Kaeya.");
        var hit = PlayKind(waltz, CardCategory.Attack, EffectTargetScope.SelectedEnemy, new EffectDefinition { CoefficientBp = 10000 });
        var retaliation = hit.Events.Skip(waltz.Events.Count).Single(e => e.DamageOrigin == ElementalDamageOrigin.TriggeredAttack);
        var incoming = hit.Events.Skip(waltz.Events.Count).First(e => e.Kind == BattleEventKind.DamageApplied);
        Check(retaliation.Amount == incoming.HealthLost * 1500 / 10000 && retaliation.DamageElement == ElementId.Cryo,
            "Waltz retaliation did not use actual HP damage received.");
        Check(hit.Events.Skip(waltz.Events.Count).Count(e => e.DamageOrigin == ElementalDamageOrigin.TriggeredAttack) == 1,
            "Waltz recursively retaliated.");
        var c5 = PlayAuthored(GenshinFixture("kaeya", 5), 1, ultimate: true);
        Check(c5.Player.LivingActive().All(f => f.Statuses.Instances.Any(s => s.RecipeId == "status.buff.glacial-waltz")), "C5 Waltz did not protect all active allies.");
        Check(StatusSystem.BuildDamagePolicy(c5.Opponent.Fighters[0], c5.Player.Fighters[1], DamageFamily.Normal).FinalReductionBp == 0,
            "Kaeya's personal damage reduction leaked to allies.");

        var lisa = GenshinFixture("lisa");
        var enemy0 = lisa.Opponent.Fighters[0];
        var conductive = GenshinStatusRecipes.CreateKitStatuses().Single(r => r.Id == "status.debuff.conductive");
        StatusSystem.Apply(enemy0, lisa.Player.Fighters[0].Id, TeamSide.Player, conductive, "conductive-old", "fixture", 1, stackCount: 3);
        var cashout = PlayAuthored(lisa, 2);
        Check(cashout.Opponent.Fighters[0].Statuses.Instances.Single(s => s.RecipeId == "status.debuff.conductive").StackCount == 1,
            "Lisa must consume old Conductive before adding her new passive stack.");
        Check(!cashout.Opponent.Fighters[0].Statuses.Instances.Any(s => s.RecipeId == "status.debuff.shock"), "Lisa's noncritical skill 2 applied Shock.");
        var evadingLisa = lisa.Clone();
        StatusSystem.Apply(evadingLisa.Opponent.Fighters[0], enemy0.Id, TeamSide.Opponent,
            new StatusRecipeDefinition { Id = "fixture.evade", Polarity = StatusPolarity.Buff, DefaultDuration = 2, EvadeAttacks = true },
            "evade-lisa", "fixture", 1);
        // A past contact with a reused card ID must not qualify this evaded action.
        var pastContact = cashout.Events.Skip(lisa.Events.Count).First(e => e.Kind == BattleEventKind.DamageApplied && e.TargetId == enemy0.Id).Clone();
        evadingLisa.Events.Add(pastContact);
        var missedCashout = PlayAuthored(evadingLisa, 2);
        Check(missedCashout.Events.Skip(evadingLisa.Events.Count).Any(e => e.Kind == BattleEventKind.AttackEvaded && e.TargetId == enemy0.Id),
            "Conductive regression did not exercise an evaded target.");
        Check(missedCashout.Opponent.Fighters[0].Statuses.Instances.Single(s => s.RecipeId == "status.debuff.conductive").StackCount == 3,
            "An evaded Lisa attack consumed old Conductive or added a new passive stack.");
        var criticalLisa = GenshinFixture("lisa"); criticalLisa.Player.Fighters[0].Stats.CritChanceBp = 100000;
        foreach (var target in criticalLisa.Opponent.Fighters) target.Stats.CritResistanceBp = 0;
        var criticalCashout = PlayAuthored(criticalLisa, 2);
        Check(criticalCashout.Opponent.LivingActive().All(f => f.Statuses.Instances.Any(s => s.RecipeId == "status.debuff.shock")),
            "Lisa's critical skill 2 failed to apply Shock.");
        Check(cashout.Events.Skip(lisa.Events.Count).Where(e => e.Kind == BattleEventKind.DamageApplied && e.DamageOrigin == ElementalDamageOrigin.Card)
            .OrderBy(e => e.TargetId).First().Amount > 0, "Lisa cashout failed to damage.");

        var barbara = GenshinFixture("barbara");
        barbara.Player.Fighters[1].Health = 100;
        var hydro = PlayAuthored(barbara, 1);
        Check(!hydro.Opponent.LivingActive().Any(f => f.Statuses.Instances.Any(s => s.RecipeId == "status.debuff.wet")), "Barbara invented Wet setup.");
        Check(hydro.Opponent.LivingActive().All(f => f.Statuses.Instances.Any(s => s.RecipeId == "status.debuff.dissolve" && s.Recipe.EndTurnPowerGaugeLoss == 2 && s.RemainingDuration == 2)),
            "Barbara R3 Dissolve does not match the owner rule.");
        Check(hydro.Player.Fighters[1].Health > 100, "Barbara did not heal the lowest-ratio teammate.");
        foreach (var rank in new[] { 1, 2, 3 })
        {
            var dissolve = GenshinFixture("barbara");
            foreach (var target in dissolve.Opponent.LivingActive()) target.PowerGauge = 5;
            var applied = PlayAuthored(dissolve, 1, rank);
            Check(applied.Opponent.Fighters[0].PowerGauge == 5, "Dissolve drained before enemy turn end.");
            Check(BattleEngine.TryResolvePlan(applied, new PlanDraft(applied).BuildPlan("dissolve-end:" + rank), out var drained, out error), error);
            Check(drained.Opponent.Fighters[0].PowerGauge == (rank == 1 ? 4 : 3), "Dissolve's target-end drain does not match rank.");
            Check(drained.Opponent.Fighters[0].Statuses.Instances.Any(s => s.RecipeId == "status.debuff.dissolve") == (rank == 3),
                "Dissolve's target-turn duration does not match rank.");
        }
        var melody = PlayAuthored(GenshinFixture("barbara"), 2);
        Check(melody.Player.LivingActive().All(f => f.Statuses.Instances.Any(s => s.RecipeId == "status.buff.melody-loop" && s.RemainingDuration == 2)),
            "Melody Loop did not retain two future target turns.");

        var lumine = GenshinFixture("lumine-anemo");
        lumine.Player.Fighters[0].Stats.CritChanceBp = 0;
        var first = PlayAuthored(lumine, 1);
        var second = PlayAuthored(first, 1);
        var third = PlayAuthored(second, 1);
        var firstHit = first.Events.Skip(lumine.Events.Count).First(e => e.Kind == BattleEventKind.DamageApplied && e.DamageOrigin == ElementalDamageOrigin.Card);
        var thirdHit = third.Events.Skip(second.Events.Count).First(e => e.Kind == BattleEventKind.DamageApplied && e.DamageOrigin == ElementalDamageOrigin.Card);
        Check(thirdHit.Amount > firstHit.Amount && CharacterPassiveRuntime.Counter(third.Player.Fighters[0], "support.attack-cadence") == 0,
            "Lumine's cadence did not boost/reset on the third attacking card.");
        Check(GenshinStarterRoster.Create().Last().UltimateTiers.All(u => u.Effect.KeywordId == "attack.shatter"), "Lumine ultimate is not Shatter.");
        var reserve = ElementFixture(ElementalRuleSet.CreateDefault());
        reserve.Player.Fighters[3].Definition.Passive = GenshinStarterRoster.Create().Last().Passive.Clone();
        ElementFuel(reserve.Opponent.Fighters[0], "status.debuff.ignite", "reserve-swirl");
        var supported = PlayKind(reserve, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Anemo));
        Check(supported.Events.Skip(reserve.Events.Count).Single(e => e.DamageOrigin == ElementalDamageOrigin.Reaction).Amount == 140,
            "Lumine's SUB Swirl bonus failed or entered twice.");
        Console.WriteLine("PASS: " + (_assertions - start) + " Genshin starter kit assertions.");
    }
}
