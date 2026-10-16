using System;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

internal static partial class BattlePlaybackChecks
{
    private static void ElementalExpansionChecks()
    {
        var start = _assertions;
        FreezeBehaviorChecks();
        foreach (var row in new[] { ("shock", 4500), ("poison", 4500), ("bleed", 7000) })
        {
            var state = ElementFixture();
            var recipe = new StatusRecipeDefinition { Id = "status.debuff." + row.Item1,
                Polarity = StatusPolarity.Debuff, Behavior = StatusBehavior.DamageOverTime,
                PeriodicDamage = new PeriodicDamageDefinition { CoefficientBp = 3300 } };
            Check(recipe.Clone().PeriodicDamage.CoefficientBp == row.Item2 && recipe.PeriodicDamage.CoefficientBp == 3300,
                "Legacy DOT cloning did not normalize independently.");
            StatusSystem.Apply(state.Opponent.Fighters[0], state.Player.Fighters[0].Id, TeamSide.Player,
                recipe, "central-dot", "central-dot", 1, snapshot: new StatusSnapshot { TriggeringHealthDamage = 1000, SourceAttack = 17 });
            var live = state.Opponent.Fighters[0].Statuses.Instances.Single();
            live.Recipe.PeriodicDamage.CoefficientBp = 12; // Emulate an older embedded save.
            Check(state.Opponent.Fighters[0].Statuses.CollectTicks(StatusTickTiming.TargetTurnEnd).Single().Amount == row.Item2 / 10,
                "Saved DOT did not use the central damage-dealt coefficient.");
        }
        var custom = new StatusRecipeDefinition { Id = "custom.dot", PeriodicDamage = new PeriodicDamageDefinition { CoefficientBp = 1234 } };
        Check(custom.Clone().PeriodicDamage.CoefficientBp == 1234, "Unrelated custom DOT was changed.");
        Check(ElementalContentValidator.Validate(ElementalRuleSet.CreateDefault(), StandardEffectDatabase.CreateStatusRecipes()).Count == 0,
            "Expanded production fuel registry is invalid.");

        var swirl = ElementFixture(ElementalRuleSet.CreateDefault());
        ElementFuel(swirl.Opponent.Fighters[0], "status.debuff.ignite", "source-pyro");
        ElementFuel(swirl.Opponent.Fighters[1], "status.debuff.shock", "source-electro");
        ElementFuel(swirl.Opponent.Fighters[2], "status.debuff.frost-bite", "source-cryo");
        var resolved = PlayKind(swirl, CardCategory.Attack, EffectTargetScope.AllEnemies, ElementAttack(ElementId.Anemo));
        var events = resolved.Events.Skip(swirl.Events.Count).ToList();
        Check(events.Count(e => e.Kind == BattleEventKind.ElementalReactionTriggered && e.ElementalReaction == ElementalReactionId.Swirl) == 2,
            "A consumed Swirl source emitted later, or a valid captured source failed.");
        Check(events.Count(e => e.Kind == BattleEventKind.ElementalReactionTriggered && e.ElementalReaction == ElementalReactionId.Overload) == 1,
            "Swirl did not cause destination Overload exactly once.");
        Check(!events.Any(e => e.Kind == BattleEventKind.ElementalReactionTriggered && e.ElementalReaction == ElementalReactionId.Melt),
            "Terminal Overload chained into Melt.");
        Check(events.FindLastIndex(e => e.Kind == BattleEventKind.DamageApplied && e.DamageOrigin == ElementalDamageOrigin.Card) <
            events.FindIndex(e => e.Kind == BattleEventKind.DamageApplied && e.DamageOrigin == ElementalDamageOrigin.Reaction),
            "Child spread changed the simultaneous root batch.");
        Check(resolved.Opponent.Fighters[1].Statuses.Instances.Any(s => s.RecipeId == "status.debuff.frost-bite" &&
            s.Snapshot.TriggeringHealthDamage != 50 && s.RemainingDuration == 3), "Spread copied an old source snapshot/duration.");

        var fresh = ElementFixture(ElementalRuleSet.CreateDefault());
        ElementFuel(fresh.Opponent.Fighters[0], "status.debuff.ignite", "fresh-source");
        var freshNext = PlayKind(fresh, CardCategory.Attack, EffectTargetScope.AllEnemies, ElementAttack(ElementId.Anemo));
        Check(freshNext.Events.Skip(fresh.Events.Count).Count(e => e.Kind == BattleEventKind.ElementalReactionTriggered &&
            e.ElementalReaction == ElementalReactionId.Swirl) == 1, "Newly spread fuel became a root Swirl source.");
        Check(freshNext.Opponent.Fighters[0].Statuses.Instances.Any(s => s.InstanceId == "fresh-source"), "Swirl consumed source fuel.");
        Check(freshNext.Opponent.Fighters[1].Statuses.Instances.Count(s => s.RecipeId == "status.debuff.ignite") == 1,
            "Spread did not create exactly one fresh setup stack.");

        var gap = ElementFixture(ElementalRuleSet.CreateDefault());
        ElementFuel(gap.Opponent.Fighters[0], "status.debuff.ignite", "gap-source");
        gap.Opponent.Fighters[1].FormationSlot = 4;
        var gapNext = PlayKind(gap, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Anemo));
        Check(!gapNext.Events.Skip(gap.Events.Count).Any(e => e.Kind == BattleEventKind.DamageApplied &&
            e.DamageOrigin == ElementalDamageOrigin.Reaction), "Swirl compressed a formation gap or reached reserve.");
        ElementalOutputChecks();
        ElementalForecastChecks();
        Console.WriteLine("PASS: " + (_assertions - start) + " expanded elemental and centralized DOT assertions.");
    }

    private static void ElementalForecastChecks()
    {
        var state = ElementFixture(ElementalRuleSet.CreateDefault());
        ElementFuel(state.Opponent.Fighters[0], "status.debuff.frost-bite", "forecast-frost");
        var before = SnapshotJson.Serialize(state);
        var view = BattleProjectionBuilder.Build(state, TeamSide.Player, "forecast-test");
        var actions = new[] { new ElementalForecastAction { TargetId = state.Opponent.Fighters[0].Id, Effect = ElementAttack(ElementId.Pyro) },
            new ElementalForecastAction { TargetId = state.Opponent.Fighters[0].Id, Effect = ElementAttack(ElementId.Hydro) } };
        var forecast = ElementalForecast.Build(view, state.ElementalRules, actions);
        Check(forecast.Lines.Count == 1 && forecast.Lines[0].Reaction == ElementalReactionId.Melt && forecast.Lines[0].ConsumedStacks == 1,
            "Queue forecast invented a persistent aura after an elemental hit or failed ordered consumption.");
        Check(before == SnapshotJson.Serialize(state), "Forecast mutated authority or RNG.");
        Check(view.OpponentTeam.Hand.Count == 0 && view.OpponentTeam.Fighters[0].Statuses.Any(s => s.InstanceId == "forecast-frost"),
            "Public forecast projection omitted fuel or exposed the opponent hand.");
        view.OpponentHandSize = 999;
        view.Events.Clear();
        Check(ElementalForecast.Build(view, state.ElementalRules, actions).Lines.Single().Reaction == ElementalReactionId.Melt,
            "Forecast depended on private hand/event content.");

        var conditional = ElementFixture(ElementalRuleSet.CreateDefault());
        var delivery = ElementDelivery();
        delivery.ElementalApplication.RequiresCritical = true;
        var conditionalActions = new[] { new ElementalForecastAction { TargetId = conditional.Opponent.Fighters[0].Id, Effect = delivery },
            new ElementalForecastAction { TargetId = conditional.Opponent.Fighters[0].Id, Effect = ElementAttack(ElementId.Pyro) } };
        var conditionalForecast = ElementalForecast.Build(BattleProjectionBuilder.Build(conditional, TeamSide.Player, "conditional"), conditional.ElementalRules, conditionalActions);
        Check(conditionalForecast.Lines.Any(l => l.ActionIndex == 1 && l.Reaction == ElementalReactionId.Overload && l.Conditional),
            "Critical delivery follow-up was omitted or presented as certain.");

        var swirl = ElementFixture(ElementalRuleSet.CreateDefault());
        ElementFuel(swirl.Opponent.Fighters[0], "status.debuff.ignite", "forecast-pyro");
        ElementFuel(swirl.Opponent.Fighters[1], "status.debuff.shock", "forecast-electro");
        var spread = ElementalForecast.Build(BattleProjectionBuilder.Build(swirl, TeamSide.Player, "spread"), swirl.ElementalRules,
            new[] { new ElementalForecastAction { TargetId = swirl.Opponent.Fighters[0].Id, Effect = ElementAttack(ElementId.Anemo) } });
        Check(spread.Lines.Any(l => l.Reaction == ElementalReactionId.Swirl && l.AdjacentTargetIds.Single() == swirl.Opponent.Fighters[1].Id) &&
            spread.Lines.Any(l => l.Reaction == ElementalReactionId.Overload), "Forecast omitted occupied-neighbor spread or destination reaction.");
    }

    private static void ElementalOutputChecks()
    {
        var freeze = ElementFixture(ElementalRuleSet.CreateDefault());
        ElementFuel(freeze.Opponent.Fighters[0], "status.debuff.wet", "freeze-wet");
        var frozen = PlayKind(freeze, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Cryo));
        var victim = frozen.Opponent.Fighters[0];
        Check(StatusSystem.IsCardUseBlocked(victim, CardCategory.Attack, 1, false), "Frozen did not disable cards.");
        Check(!victim.Statuses.Instances.Any(s => s.InstanceId == "freeze-wet"), "Freeze did not consume setup.");
        StatusSystem.AdvanceOwnerTurn(victim);
        Check(!StatusSystem.IsCardUseBlocked(victim, CardCategory.Attack, 1, false), "Frozen did not expire after target turn.");
        var frozenRecipe = GenshinStatusRecipes.CreateOutputs().Single(r => r.Id == "status.debuff.freeze");
        Check(!StatusSystem.Apply(victim, "source", TeamSide.Player, frozenRecipe, "repeat", "repeat", 1).Accepted,
            "Freeze bypassed the recovery turn.");
        StatusSystem.AdvanceOwnerTurn(victim);
        Check(StatusSystem.Apply(victim, "source", TeamSide.Player, frozenRecipe, "repeat", "repeat", 1).Accepted,
            "Freeze recovery never ended.");

        var immune = ElementFixture(ElementalRuleSet.CreateDefault());
        ElementFuel(immune.Opponent.Fighters[0], "status.debuff.wet", "immune-wet");
        StatusSystem.Apply(immune.Opponent.Fighters[0], "self", TeamSide.Opponent,
            new StatusRecipeDefinition { Id = "fixture.cc-immune", ImmuneStatusTags = new System.Collections.Generic.List<string> { "status.freeze" } },
            "cc-immune", "fixture", 1);
        var immuneNext = PlayKind(immune, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Cryo));
        Check(!immuneNext.Opponent.Fighters[0].Statuses.Instances.Any(s => s.RecipeId == "status.debuff.freeze") &&
            immuneNext.Events.Skip(immune.Events.Count).Any(e => e.Kind == BattleEventKind.DamageApplied), "CC immunity erased the incoming hit.");

        var geo = ElementFixture(ElementalRuleSet.CreateDefault());
        geo.Player.Fighters[0].Stats.MaxHealth = geo.Player.Fighters[0].Health = 1000;
        geo.Player.Fighters[0].Stats.Attack = 10000;
        foreach (var enemy in geo.Opponent.LivingActive()) ElementFuel(enemy, "status.debuff.wet", "geo-" + enemy.Id);
        var shielded = PlayKind(geo, CardCategory.Attack, EffectTargetScope.AllEnemies, ElementAttack(ElementId.Geo));
        Check(shielded.Player.Fighters[0].Shield == 1000, "Crystallize did not cap at 100% Geo actor Max HP.");
        Check(shielded.Opponent.LivingActive().All(f => f.Statuses.Instances.Any(s => s.RecipeId == "status.debuff.wet")), "Crystallize consumed source setup.");
        var barrier = shielded.Player.Fighters[0].Statuses.Instances.Single(s => s.RecipeId == "status.reaction.crystallize");
        Check(barrier.RemainingDuration == 2 && barrier.ShieldRemaining == 1000, "Crystallize ownership/duration mismatch.");
        var overkill = ElementFixture(ElementalRuleSet.CreateDefault());
        overkill.Player.Fighters[0].Stats.Attack = 10000;
        overkill.Opponent.Fighters[0].Health = 10; overkill.Opponent.Fighters[0].Shield = 40;
        ElementFuel(overkill.Opponent.Fighters[0], "status.debuff.wet", "overkill-wet");
        var overkillNext = PlayKind(overkill, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Geo));
        Check(overkillNext.Player.Fighters[0].Shield == 15, "Crystallize included overkill or excluded shield damage.");

        foreach (var row in new[] { (ElementId.Hydro, "status.debuff.shock", "status.reaction.charged"),
            (ElementId.Dendro, "status.debuff.ignite", "status.reaction.burning") })
        {
            var periodic = ElementFixture(ElementalRuleSet.CreateDefault());
            ElementFuel(periodic.Opponent.Fighters[0], row.Item2, "periodic-fuel");
            var next = PlayKind(periodic, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(row.Item1));
            var status = next.Opponent.Fighters[0].Statuses.Instances.Single(s => s.RecipeId == row.Item3);
            Check(status.RemainingDuration == 2 && status.Snapshot.SourceAttack == 100, "Reaction DOT duration or actor snapshot is incorrect.");
            var tick = next.Opponent.Fighters[0].Statuses.CollectTicks(StatusTickTiming.TargetTurnEnd).Single();
            Check(tick.Amount == 100 && tick.Family == DamageFamily.ElementalReaction && tick.Element != ElementId.None,
                "Reaction DOT lost its element or reaction damage policy.");
            Check(ElementalReactionRules.Evaluate(next.ElementalRules, next.Opponent.Fighters[0], ElementId.Cryo) == null,
                "Periodic reaction output became ordinary fuel.");
        }
        var superconduct = ElementFixture(ElementalRuleSet.CreateDefault());
        ElementFuel(superconduct.Opponent.Fighters[0], "status.debuff.shock", "sc-fuel");
        var sc = PlayKind(superconduct, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Cryo));
        Check(sc.Events.Skip(superconduct.Events.Count).Count(e => e.DamageOrigin == ElementalDamageOrigin.Reaction) == 3,
            "Superconduct did not damage all active enemies.");
        sc.ActingSide = TeamSide.Player; // Compare the same attacker against the vulnerability before its target-turn expiry.
        var physical = PlayKind(sc, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.None));
        var elemental = PlayKind(sc, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Pyro));
        var physicalHit = physical.Events.Skip(sc.Events.Count).First(e => e.Kind == BattleEventKind.DamageApplied).Amount;
        var elementalHit = elemental.Events.Skip(sc.Events.Count).First(e => e.Kind == BattleEventKind.DamageApplied).Amount;
        Check(physicalHit == 140 && elementalHit == 100,
            $"Superconduct vulnerability must affect only physical Normal damage: physical {physicalHit}, elemental {elementalHit}.");

        var gauge = ElementFixture().Player.Fighters[0];
        gauge.PowerGauge = 0;
        ElementFuel(gauge, "status.debuff.frost-bite", "slow-gauge");
        Check(PowerGaugeRules.GainForAction(gauge) == 0 && gauge.PowerGaugeRemainderBp == 5000,
            "Frost Bite did not produce half an orb.");
        Check(PowerGaugeRules.GainForAction(gauge) == 1 && gauge.PowerGauge == 1 && gauge.PowerGaugeRemainderBp == 0,
            "Fractional gauge did not carry between actions.");
        gauge.Statuses.Instances.Clear();
        StatusSystem.Apply(gauge, gauge.Id, gauge.Side, new StatusRecipeDefinition { Id = "fixture.gauge-haste", PowerGaugeGainBp = 5000 },
            "haste", "fixture", 1);
        Check(PowerGaugeRules.GainForAction(gauge) == 1 && gauge.PowerGaugeRemainderBp == 5000, "Gauge haste did not gain 1.5 orbs.");
        var gaugeClone = gauge.Clone();
        Check(PowerGaugeRules.GainForAction(gaugeClone) == 2 && gauge.PowerGaugeRemainderBp == 5000, "Gauge remainder was lost or shared by clone.");
        var drain = ElementFixture(); var refunder = drain.Player.Fighters[0]; var drained = drain.Opponent.Fighters[0];
        refunder.Definition.Passive = StandardCharacterPassives.Create("fighter.shingo97");
        refunder.PowerGauge = drained.PowerGauge = 0;
        drained.PowerGaugeRemainderBp = 5000;
        var playback = drain.Clone(); var eventStart = drain.Events.Count;
        CharacterPassiveRuntime.ChangePowerGauge(drain, refunder, drained, -1, "fractional-drain", cardOrigin: true);
        Check(drained.PowerGaugeRemainderBp == 0 && refunder.PowerGauge == 0 && refunder.PowerGaugeRemainderBp == 5000,
            "A clamped whole-orb drain/refund created or preserved excess fractional gauge.");
        foreach (var evt in drain.Events.Skip(eventStart)) BattlePlaybackState.Apply(playback, evt);
        Check(playback.Opponent.Fighters[0].PowerGaugeRemainderBp == 0 && playback.Player.Fighters[0].PowerGaugeRemainderBp == 5000,
            "Fractional drain/refund replay lost the exact gauge amounts.");
    }
}
