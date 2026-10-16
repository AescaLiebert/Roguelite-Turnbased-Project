using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;

internal static partial class BattlePlaybackChecks
{
    private static ElementalRuleSet ElementRules()
    {
        var rules = ElementalRuleSet.CreateSliceAC();
        rules.Fuel.Add(new ElementalFuelDefinition { RecipeId = "fixture.cryo", Element = ElementId.Cryo });
        rules.Fuel.Add(new ElementalFuelDefinition { RecipeId = "fixture.wet", Element = ElementId.Hydro });
        rules.Fuel.Add(new ElementalFuelDefinition { RecipeId = "fixture.flame-alias", Element = ElementId.Pyro });
        return rules;
    }

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

    private static void ElementFuel(FighterState target, string recipe, string instance, string source = "fixture-source")
    {
        var definition = StandardEffectDatabase.CreateStatusRecipes().Find(r => r.Id == recipe) ??
            new StatusRecipeDefinition { Id = recipe, Polarity = StatusPolarity.Debuff,
                Stacking = StatusStackingPolicy.IndependentStacks, MaxStacks = 10 };
        Check(StatusSystem.Apply(target, source, TeamSide.Player, definition, instance, "fixture", 1,
            duration: 10, snapshot: new StatusSnapshot { TriggeringHealthDamage = 50 }).Accepted, "Elemental fuel fixture rejected.");
    }

    private static EffectDefinition ElementAttack(ElementId element, int hits = 1) => new EffectDefinition {
        Scaling = StatScaling.Attack, DamageElement = element, CoefficientBp = 10000,
        Attack = new DamageAttackDefinition { HitCount = hits } };

    private static void ElementalSliceChecks()
    {
        var initialCount = _assertions;
        var neutral = ElementFixture();
        Check(neutral.Player.ElementalReactionsEnabled && neutral.Opponent.ElementalReactionsEnabled,
            "Full starting rosters should enable both sides without a provider passive.");
        var mixed = ElementFixture(mixedReserve: true);
        Check(!mixed.Player.ElementalReactionsEnabled, "Non-Genshin SUB must disable reactions.");
        ElementFuel(mixed.Opponent.Fighters[0], "status.debuff.shock", "mixed-fuel");
        var mixedNext = PlayKind(mixed, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Pyro));
        Check(!mixedNext.Events.Skip(mixed.Events.Count).Any(e => e.Kind == BattleEventKind.ElementalReactionTriggered) &&
            mixedNext.Opponent.Fighters[0].Statuses.Instances.Any(s => s.InstanceId == "mixed-fuel"), "Ineligible source reacted.");
        mixed.Player.Fighters[3].IsAlive = false;
        Check(!mixed.Clone().Player.ElementalReactionsEnabled, "Death unlocked a mixed initial roster.");
        mixed.Player.Fighters[0].Definition.TraitIds.Clear();
        Check(!mixed.Player.ElementalReactionsEnabled, "Starting eligibility was reevaluated.");
        var cloned = SnapshotJson.Deserialize<BattleState>(SnapshotJson.Serialize(neutral));
        Check(cloned.ElementalSchemaVersion == 1 && cloned.Player.ElementalReactionsEnabled &&
            cloned.ElementalRules.Fuel.Count == neutral.ElementalRules.Fuel.Count, "Snapshot lost elemental rules/eligibility.");
        cloned.ElementalRules.Fuel.Clear();
        Check(neutral.ElementalRules.Fuel.Count > 0, "Snapshot rule mappings share mutable state.");
        var deep = neutral.Clone();
        deep.ElementalRules.Reactions[0].AmplificationBp = 30000;
        Check(neutral.ElementalRules.Reactions[0].AmplificationBp == 20000, "Clone shares reaction definitions.");

        foreach (var row in new[] {
            ("fixture.cryo", ElementId.Pyro, ElementalReactionId.Melt, 20000),
            ("status.debuff.ignite", ElementId.Cryo, ElementalReactionId.ReverseMelt, 15000),
            ("status.debuff.ignite", ElementId.Hydro, ElementalReactionId.Vaporize, 20000),
            ("fixture.wet", ElementId.Pyro, ElementalReactionId.ReverseVaporize, 15000) })
        foreach (var side in new[] { TeamSide.Player, TeamSide.Opponent })
        {
            var state = ElementFixture(side: side); var target = state.OtherTeam(side).LivingActive()[0];
            ElementFuel(target, row.Item1, "amplify-fuel");
            var before = state.Events.Count;
            var next = PlayKind(state, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(row.Item2));
            var events = next.Events.Skip(before).ToList();
            var reaction = events.Single(e => e.Kind == BattleEventKind.ElementalReactionTriggered);
            Check(reaction.ElementalReaction == row.Item3 && reaction.Amount == row.Item4, "Wrong amplification direction.");
            var hit = events.Single(e => e.Kind == BattleEventKind.DamageApplied && e.DamageOrigin == ElementalDamageOrigin.Card);
            var baseHit = row.Item1 == "status.debuff.ignite" ? 110 : 100;
            Check(hit.Amount == baseHit * row.Item4 / 10000, "Amplification duplicated/preceded the wrong formula stage.");
            Check(reaction.ParentEventId == hit.Id && !string.IsNullOrWhiteSpace(reaction.RootActionId), "Reaction lost contact lineage.");
            Check(!next.OtherTeam(side).Fighters[0].Statuses.Instances.Any(s => s.InstanceId == "amplify-fuel"), "Amplification left fuel behind.");
            var display = state.Clone(); foreach (var evt in events) BattlePlaybackState.Apply(display, evt);
            Check(display.OtherTeam(side).Fighters[0].Health == next.OtherTeam(side).Fighters[0].Health &&
                display.OtherTeam(side).Fighters[0].Statuses.Instances.Count == next.OtherTeam(side).Fighters[0].Statuses.Instances.Count,
                "Event playback lost elemental damage/consumption.");
            var repeat = PlayKind(state.Clone(), CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(row.Item2));
            Check(SnapshotJson.Serialize(next) == SnapshotJson.Serialize(repeat), "Elemental resolution is not deterministic.");
        }

        var stacks = ElementFixture(); var stackTarget = stacks.Opponent.Fighters[0];
        ElementFuel(stackTarget, "status.debuff.ignite", "ignite-1");
        ElementFuel(stackTarget, "fixture.flame-alias", "ignite-alias", "other-source");
        ElementFuel(stackTarget, "status.debuff.shock", "shock-1");
        ElementFuel(stackTarget, "status.debuff.shock", "shock-2", "other-source");
        ElementFuel(stackTarget, "status.debuff.poison", "poison");
        var stacksNext = PlayKind(stacks, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Electro));
        var consumed = stacksNext.Events.Last(e => e.Kind == BattleEventKind.ElementalReactionTriggered).ConsumedStatusInstanceIds;
        Check(consumed.Count == 4 && consumed.Contains("ignite-alias"), "Reaction failed to remove all participating aliases/owners.");
        Check(stacksNext.Opponent.Fighters[0].Statuses.Instances.All(s => s.InstanceId == "poison"), "Reaction consumed unrelated DOT.");
        var view = BattleProjectionBuilder.Build(stacksNext, TeamSide.Player, "element-test");
        Check(view.OwnTeam.ElementalReactionsEnabled && view.OpponentTeam.Hand.Count == 0 &&
            view.Events.Any(e => e.ElementalReaction == "Overload" && e.ConsumedStatusInstanceIds.Count == 4), "Projection lost public reaction facts or exposed hand.");

        var priority = ElementFixture(); var priorityTarget = priority.Opponent.Fighters[0];
        ElementFuel(priorityTarget, "status.debuff.shock", "priority-electro");
        ElementFuel(priorityTarget, "fixture.wet", "priority-hydro");
        ElementFuel(priorityTarget, "fixture.cryo", "priority-cryo");
        var selected = ElementalReactionRules.Evaluate(priority.ElementalRules, priorityTarget, ElementId.Pyro);
        priorityTarget.Statuses.Instances.Reverse();
        Check(selected.Reaction.Id == ElementalReactionId.Melt && ElementalReactionRules.Evaluate(priority.ElementalRules,
            priorityTarget, ElementId.Pyro).Reaction.Id == selected.Reaction.Id, "Selection depends on status insertion order.");

        foreach (var count in new[] { 1, 3, 10 })
        {
            var state = ElementFixture(); ElementFuel(state.Opponent.Fighters[1], "status.debuff.shock", "overload-source");
            ElementFuel(state.Opponent.Fighters[2], "fixture.cryo", "terminal-cryo");
            var next = PlayKind(state, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Pyro, count),
                state.Opponent.Fighters[1].Id);
            var events = next.Events.Skip(state.Events.Count).ToList();
            Check(events.Count(e => e.Kind == BattleEventKind.ElementalReactionTriggered) == 1, "Visual hit count added reactions.");
            Check(events.Count(e => e.Kind == BattleEventKind.DamageApplied && e.DamageOrigin == ElementalDamageOrigin.Reaction) == 3,
                "Overload adjacency changed with visual hits.");
            Check(events.Where(e => e.DamageOrigin == ElementalDamageOrigin.Reaction).All(e => !e.WasCritical && !e.WasBlocked), "Overload crit/block leaked.");
            Check(next.Opponent.Fighters[2].Statuses.Instances.Any(s => s.InstanceId == "terminal-cryo"), "Overload chained into Melt.");
        }
        var overlap = ElementFixture();
        foreach (var target in overlap.Opponent.LivingActive()) ElementFuel(target, "status.debuff.shock", "overlap:" + target.Id);
        var overlapNext = PlayKind(overlap, CardCategory.Attack, EffectTargetScope.AllEnemies, ElementAttack(ElementId.Pyro));
        var overlapEvents = overlapNext.Events.Skip(overlap.Events.Count).ToList();
        Check(overlapEvents.Count(e => e.Kind == BattleEventKind.ElementalReactionTriggered) == 3 &&
            overlapEvents.Count(e => e.DamageOrigin == ElementalDamageOrigin.Reaction) == 3, "Overlap damage budget cancelled valid local reactions.");
        Check(overlapNext.Opponent.LivingActive().All(f => f.Statuses.Instances.Count == 0), "Overlapping reactions left suppressed fuel.");
        Check(overlapEvents.Where(e => e.Kind == BattleEventKind.DamageApplied && e.DamageOrigin == ElementalDamageOrigin.Card).All(e => e.Amount == 100),
            "Earlier reaction changed simultaneous root formula inputs.");
        var gap = ElementFixture(); gap.Opponent.Fighters[1].IsAlive = false; gap.Opponent.Fighters[1].Health = 0;
        ElementFuel(gap.Opponent.Fighters[0], "status.debuff.shock", "gap-fuel");
        var gapNext = PlayKind(gap, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Pyro));
        Check(!gapNext.Events.Skip(gap.Events.Count).Any(e => e.DamageOrigin == ElementalDamageOrigin.Reaction && e.TargetId == gap.Opponent.Fighters[2].Id),
            "Splash compacted empty formation slots.");

        ElementalDeliveryChecks();
        ElementalPolicyAndValidationChecks();
        Console.WriteLine("PASS: " + (_assertions - initialCount) + " elemental A-C assertions.");
    }

    private static EffectDefinition ElementDelivery(bool direct = false, int chance = 10000, bool crit = false) => new EffectDefinition {
        Scaling = StatScaling.Attack, DamageElement = direct ? ElementId.Electro : ElementId.None,
        ElementalApplication = new ElementalApplicationDefinition { Element = ElementId.Electro, StatusEffectId = "contact-shock", RequiresCritical = crit },
        Sequence = { new CardEffectStep { Timing = CardEffectTiming.AfterDamage, Effect = new EffectDefinition {
            Id = "contact-shock", Kind = EffectKind.ApplyStatus, Target = EffectTargetScope.SelectedEnemy,
            StatusApplyChanceBp = chance, StatusDurationOverride = 4,
            StatusRecipe = StandardEffectDatabase.CreateStatusRecipes().Find(r => r.Id == "status.debuff.shock") } } } };

    private static void ElementalDeliveryChecks()
    {
        foreach (var direct in new[] { false, true })
        {
            var state = ElementFixture(); ElementFuel(state.Opponent.Fighters[0], "status.debuff.ignite", "contact-ignite");
            var next = PlayKind(state, CardCategory.AttackDebuff, EffectTargetScope.SelectedEnemy, ElementDelivery(direct));
            Check(next.Events.Skip(state.Events.Count).Count(e => e.Kind == BattleEventKind.ElementalReactionTriggered) == 1 &&
                next.Opponent.Fighters[0].Statuses.Instances.Count == 0, "Direct/rider contact doubled or reapplied consumed status.");
        }
        foreach (var direct in new[] { false, true })
        {
            var state = ElementFixture(); var target = state.Opponent.Fighters[0];
            ElementFuel(target, "status.debuff.ignite", "immune-ignite");
            Check(StatusSystem.Apply(target, target.Id, target.Side, new StatusRecipeDefinition { Id = "immunity", DebuffImmunity = true,
                DefaultDuration = 10 }, "immunity", "fixture", 2).Accepted, "Immunity fixture failed.");
            var next = PlayKind(state, CardCategory.AttackDebuff, EffectTargetScope.SelectedEnemy, ElementDelivery(direct));
            Check(next.Events.Skip(state.Events.Count).Any(e => e.Kind == BattleEventKind.ElementalReactionTriggered) == direct,
                "Rejected rider wrongly prevented direct elemental hit or triggered as an accepted status.");
        }
        var chanceState = ElementFixture(); ElementFuel(chanceState.Opponent.Fighters[0], "status.debuff.ignite", "chance-fuel");
        var rejected = PlayKind(chanceState, CardCategory.AttackDebuff, EffectTargetScope.SelectedEnemy, ElementDelivery(chance: 0));
        Check(!rejected.Events.Skip(chanceState.Events.Count).Any(e => e.Kind == BattleEventKind.ElementalReactionTriggered) &&
            rejected.Opponent.Fighters[0].Statuses.Instances.Any(s => s.InstanceId == "chance-fuel"), "Zero-chance delivery consumed fuel.");
        var noSetup = ElementFixture();
        var applied = PlayKind(noSetup, CardCategory.AttackDebuff, EffectTargetScope.SelectedEnemy, ElementDelivery());
        Check(applied.Opponent.Fighters[0].Statuses.Instances.Single().Snapshot.TriggeringHealthDamage == 100 &&
            applied.Events.Skip(noSetup.Events.Count).Count(e => e.Kind == BattleEventKind.StatusApplied) == 1, "Ordinary contact rider was lost or snapshotted twice.");
        var noDebuff = ElementFixture();
        var bare = PlayKind(noDebuff, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Hydro));
        Check(bare.Opponent.Fighters[0].Statuses.Instances.Count == 0, "Damage-only element invented setup.");
        var evaded = ElementFixture(); var evadeTarget = evaded.Opponent.Fighters[0];
        ElementFuel(evadeTarget, "status.debuff.shock", "evade-fuel");
        StatusSystem.Apply(evadeTarget, evadeTarget.Id, evadeTarget.Side, new StatusRecipeDefinition { Id = "evade",
            EvadeAttacks = true, DefaultDuration = 10 }, "evade", "fixture", 2);
        var evadeNext = PlayKind(evaded, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Pyro));
        Check(!evadeNext.Events.Skip(evaded.Events.Count).Any(e => e.Kind == BattleEventKind.ElementalReactionTriggered) &&
            evadeNext.Opponent.Fighters[0].Statuses.Instances.Any(s => s.InstanceId == "evade-fuel"), "Evaded attack consumed setup.");
        var shield = ElementFixture(); shield.Opponent.Fighters[0].Shield = 1000;
        ElementFuel(shield.Opponent.Fighters[0], "status.debuff.shock", "shield-fuel");
        var shieldNext = PlayKind(shield, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Pyro));
        Check(shieldNext.Events.Skip(shield.Events.Count).Any(e => e.Kind == BattleEventKind.ElementalReactionTriggered) &&
            shieldNext.Opponent.Fighters[0].Health == 100000 && shieldNext.Opponent.Fighters[0].Shield == 750,
            "Shield contact failed or Overload bypassed shields.");
        var crit = ElementFixture(); crit.Player.Fighters[0].Stats.CritChanceBp = 10000; crit.Player.Fighters[0].Stats.CritDamageBp = 20000;
        ElementFuel(crit.Opponent.Fighters[0], "status.debuff.ignite", "crit-fuel");
        var critNext = PlayKind(crit, CardCategory.AttackDebuff, EffectTargetScope.SelectedEnemy, ElementDelivery(crit: true));
        Check(critNext.Events.Skip(crit.Events.Count).Count(e => e.Kind == BattleEventKind.ElementalReactionTriggered) == 1, "Critical delivery did not use its cached contact outcome.");
        var nonCrit = ElementFixture(); ElementFuel(nonCrit.Opponent.Fighters[0], "status.debuff.ignite", "noncrit-fuel");
        var directNonCrit = PlayKind(nonCrit, CardCategory.AttackDebuff, EffectTargetScope.SelectedEnemy, ElementDelivery(direct: true, crit: true));
        Check(directNonCrit.Events.Skip(nonCrit.Events.Count).Any(e => e.Kind == BattleEventKind.ElementalReactionTriggered),
            "Failed optional critical rider prevented direct elemental reaction.");
        var conditionalNonCrit = PlayKind(nonCrit.Clone(), CardCategory.AttackDebuff, EffectTargetScope.SelectedEnemy, ElementDelivery(crit: true));
        Check(!conditionalNonCrit.Events.Skip(nonCrit.Events.Count).Any(e => e.Kind == BattleEventKind.ElementalReactionTriggered),
            "Noncritical status-dependent application consumed setup.");

        var weaker = ElementFixture(); var weakerSource = weaker.Player.Fighters[0]; var weakerTarget = weaker.Opponent.Fighters[0];
        var weakerDelivery = ElementDelivery(); weakerDelivery.Sequence[0].Effect.StatusRecipe.Stacking = StatusStackingPolicy.RefreshStronger;
        StatusSystem.Apply(weakerTarget, weakerSource.Id, weakerSource.Side, weakerDelivery.Sequence[0].Effect.StatusRecipe.Clone(),
            "strong-shock", "fixture", 1, snapshot: new StatusSnapshot { TriggeringHealthDamage = 10000 }, duration: 10);
        ElementFuel(weakerTarget, "status.debuff.ignite", "weaker-ignite");
        var weakerNext = PlayKind(weaker, CardCategory.AttackDebuff, EffectTargetScope.SelectedEnemy, weakerDelivery);
        Check(!weakerNext.Events.Skip(weaker.Events.Count).Any(e => e.Kind == BattleEventKind.ElementalReactionTriggered) &&
            weakerNext.Opponent.Fighters[0].Statuses.Instances.Count == 2, "Ignored-weaker incoming status consumed setup.");

        var managed = ElementFixture(); var managedEffect = ElementDelivery();
        managedEffect.Attack.HitCount = 3; managedEffect.ElementalApplication.HitIndex = 2;
        var managedNext = PlayKind(managed, CardCategory.AttackDebuff, EffectTargetScope.SelectedEnemy, managedEffect);
        Check(managedNext.Opponent.Fighters[0].Statuses.Instances.Single().Snapshot.TriggeringHealthDamage == 33,
            "Contact status snapshot inherited prior visual-hit totals.");

        var roll = ElementFixture(); ElementFuel(roll.Opponent.Fighters[0], "status.debuff.ignite", "roll-fuel");
        var probabilistic = PlayKind(roll, CardCategory.AttackDebuff, EffectTargetScope.SelectedEnemy, ElementDelivery(chance: 5000));
        Check(probabilistic.RngDrawCount - roll.RngDrawCount == 3, "Status contact probability was rerolled during commit.");

        var utility = ElementFixture(); ElementFuel(utility.Opponent.Fighters[0], "status.debuff.ignite", "utility-ignite");
        var pure = PlayKind(utility, CardCategory.Debuff, EffectTargetScope.SelectedEnemy, new EffectDefinition {
            Kind = EffectKind.ApplyStatus, StatusRecipe = StandardEffectDatabase.CreateStatusRecipes().Find(r => r.Id == "status.debuff.shock"),
            StatusDurationOverride = 4 });
        Check(!pure.Events.Skip(utility.Events.Count).Any(e => e.Kind == BattleEventKind.ElementalReactionTriggered) &&
            pure.Opponent.Fighters[0].Statuses.Instances.Count == 2, "Coexistence or utility application triggered a reaction.");
    }

    private static void ElementalPolicyAndValidationChecks()
    {
        var state = ElementFixture(); var source = state.Player.Fighters[0]; var target = state.Opponent.Fighters[0];
        StatusSystem.Apply(source, source.Id, source.Side, new StatusRecipeDefinition { Id = "dot-bonus", DefaultDuration = 10,
            Modifiers = { new StatModifierDefinition { Target = ModifierTarget.FamilyDamageDealt,
                Family = DamageFamily.DamageOverTime, Amount = 5000 } } }, "dot-bonus", "fixture", 1);
        var withBridge = PlayKind(state, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Hydro));
        Check(withBridge.Events.Skip(state.Events.Count).First(e => e.Kind == BattleEventKind.DamageApplied).Amount == 150, "DOT bridge missing or applied twice.");
        var without = PlayKind(state.Clone(), CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.None));
        Check(without.Events.Skip(state.Events.Count).First(e => e.Kind == BattleEventKind.DamageApplied).Amount == 100, "DOT bridge leaked to non-elemental attack.");
        var artifact = ElementFixture(); artifact.Player.Fighters[0].Health = 1000;
        for (var slot = 0; slot < 4; slot++) artifact.Player.Fighters[0].Definition.ArtifactLoadout.Add(
            new ArtifactInstance { Set = ArtifactSet.Striker, Slot = (ArtifactSlot)slot });
        Check(ArtifactRules.ActiveSets(artifact.Player.Fighters[0]).Contains(ArtifactSet.Striker), "Global artifact bonus fixture is inactive.");
        var artifactNext = PlayKind(artifact, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Hydro));
        Check(artifactNext.Events.Skip(artifact.Events.Count).First(e => e.Kind == BattleEventKind.DamageApplied).Amount == 105,
            "A global artifact damage bonus crossed into the DOT bridge a second time.");
        Check(StatusSystem.GetExplicitFamilyDamageDealtBp(artifact.Player.Fighters[0], DamageFamily.DamageOverTime) == 0,
            "Global family policy contribution was misclassified as an explicit DOT bonus.");
        ElementFuel(artifact.Opponent.Fighters[0], "status.debuff.shock", "artifact-overload");
        var artifactReaction = PlayKind(artifact.Clone(), CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Pyro));
        Check(artifactReaction.Events.Skip(artifact.Events.Count).Where(e => e.DamageOrigin == ElementalDamageOrigin.Reaction).All(e => e.Amount == 157),
            "Global artifact damage bonus must affect the new reaction family exactly once.");
        Check(artifactReaction.Player.Fighters[0].Health == 1005, "Terminal reaction inherited ordinary artifact lifesteal.");
        source.Stats.ElementalModifiers.Add(new ElementalModifierDefinition { Element = ElementId.Hydro, DamageDealtBp = 2000 });
        StatusSystem.Apply(target, target.Id, target.Side, new StatusRecipeDefinition { Id = "element-resistance", DefaultDuration = 10,
            ElementalModifiers = { new ElementalModifierDefinition { Element = ElementId.Hydro, ResistanceBp = 2500 } },
            Modifiers = { new StatModifierDefinition { Target = ModifierTarget.FamilyDamageReceived,
                Family = DamageFamily.DamageOverTime, Amount = 10000 } } }, "element-resistance", "fixture", 2);
        var elementNext = PlayKind(state.Clone(), CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Hydro));
        Check(elementNext.Events.Skip(state.Events.Count).First(e => e.Kind == BattleEventKind.DamageApplied).Amount == 135,
            "Element bonus/resistance or DOT-received exclusion is incorrect.");
        var modifiersClone = state.Clone(); modifiersClone.Player.Fighters[0].Stats.ElementalModifiers[0].DamageDealtBp = 9999;
        modifiersClone.Opponent.Fighters[0].Statuses.Instances[0].Recipe.ElementalModifiers[0].ResistanceBp = -10000;
        Check(source.Stats.ElementalModifiers[0].DamageDealtBp == 2000 && target.Statuses.Instances[0].Recipe.ElementalModifiers[0].ResistanceBp == 2500,
            "Element modifiers share mutable state across snapshots.");
        var policy = new DamagePolicy { Family = DamageFamily.Normal, AmplificationBp = 20000, DotBridgeFactorBp = 15000,
            ElementFactorBp = 12000, ElementResistanceFactorBp = 7500, DamageCap = 100 };
        var damage = DamageResolver.Resolve(new DamagePacket { BaseAmount = 101, Policy = policy }, new StatBlock(),
            new StatBlock { Defense = 17 }, 1000, 30, -1, -1);
        Check(damage.WasCapped && damage.CalculatedDamage == 100 && damage.ShieldLost == 30 && damage.HealthLost == 70,
            "Elemental formula stages violated cap/shield order.");
        var capRules = ElementRules(); capRules.ReactionDamageCap = 50;
        var capState = ElementFixture(capRules); capState.Opponent.Fighters[0].Stats.Defense = 999999;
        ElementFuel(capState.Opponent.Fighters[0], "status.debuff.shock", "cap-fuel");
        var capNext = PlayKind(capState, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Pyro));
        Check(capNext.Events.Skip(capState.Events.Count).Count(e => e.DamageOrigin == ElementalDamageOrigin.Reaction) == 2 &&
            capNext.Events.Skip(capState.Events.Count).Where(e => e.DamageOrigin == ElementalDamageOrigin.Reaction).All(e => e.Amount == 50),
            "Overload failed DEF bypass or explicit reaction cap.");
        var isolated = ElementFixture(); ElementFuel(isolated.Opponent.Fighters[0], "status.debuff.shock", "snapshot-fuel");
        var isolatedAttack = ElementAttack(ElementId.Pyro);
        isolatedAttack.Sequence.Add(new CardEffectStep { Timing = CardEffectTiming.AfterDamage, Effect = new EffectDefinition {
            Kind = EffectKind.ApplyStatus, StatusDurationOverride = 4,
            StatusRecipe = StandardEffectDatabase.CreateStatusRecipes().Find(r => r.Id == "status.debuff.poison") } });
        var isolatedNext = PlayKind(isolated, CardCategory.AttackDebuff, EffectTargetScope.SelectedEnemy, isolatedAttack);
        Check(isolatedNext.Opponent.Fighters[0].Statuses.Instances.Single().Snapshot.TriggeringHealthDamage == 100,
            "Terminal reaction damage leaked into an ordinary card DOT snapshot.");
        var death = ElementFixture(); death.Opponent.Fighters[0].Health = 10; death.Opponent.Fighters[1].Health = 10;
        ElementFuel(death.Opponent.Fighters[0], "status.debuff.shock", "death-fuel");
        var deathNext = PlayKind(death, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Pyro));
        Check(!deathNext.Opponent.Fighters[0].IsAlive && !deathNext.Opponent.Fighters[1].IsAlive &&
            deathNext.Events.Skip(death.Events.Count).Count(e => e.Kind == BattleEventKind.FighterDefeated) == 2,
            "Secondary reaction death escaped the pending-defeat batch.");

        var invalid = ElementRules(); invalid.Reactions.Add(invalid.Reactions[0].Clone());
        Check(ElementalContentValidator.Validate(invalid).Count > 0, "Duplicate reaction pair accepted.");
        Check(ElementalContentValidator.Validate(ElementalRuleSet.CreateSliceAC(), StandardEffectDatabase.CreateStatusRecipes()).Count == 0,
            "Default production elemental registry is invalid.");
        Check(StandardEffectDatabase.CreateStatusRecipes().Single(r => r.Id == "status.debuff.shock").PeriodicDamage.CoefficientBp == StandardDotRules.ShockBp,
            "Standard Shock diverged from the database-owned coefficient.");
        invalid = ElementRules(); invalid.SchemaVersion = 99;
        Check(ElementalContentValidator.Validate(invalid).Count > 0, "Future rule schema accepted.");
        var missingFuel = ElementRules();
        Check(ElementalContentValidator.Validate(missingFuel, StandardEffectDatabase.CreateStatusRecipes()).Count > 0,
            "Unmapped production fuel recipe accepted.");
        var malformed = ElementDelivery(); malformed.ElementalApplication.StatusEffectId = "missing";
        var errors = new List<string>(); ElementalContentValidator.Validate(malformed, ElementRules(), errors);
        Check(errors.Count > 0, "Missing contact rider accepted.");
        var spread = ElementDelivery(); spread.Sequence[0].Effect.Target = EffectTargetScope.AllEnemies;
        errors.Clear(); ElementalContentValidator.Validate(spread, ElementRules(), errors);
        Check(errors.Count > 0, "Unsupported unhit-recipient contact delivery accepted.");
        var legacy = ElementFixture(); legacy.ElementalSchemaVersion = 0; legacy.ElementalRules = null;
        legacy.Player.ElementalReactionsEnabled = false;
        var legacyNext = PlayKind(legacy, CardCategory.Attack, EffectTargetScope.SelectedEnemy, new EffectDefinition { Scaling = StatScaling.Attack });
        Check(!legacyNext.Events.Skip(legacy.Events.Count).Any(e => e.Kind == BattleEventKind.ElementalReactionTriggered), "Legacy snapshot gained inferred elemental rules.");
        var future = ElementFixture(); future.ElementalSchemaVersion = 99;
        Check(!BattleEngine.TryResolvePlan(future, new TurnPlan { RequestId = "future", ExpectedRevision = future.Revision }, out _, out _), "Future snapshot schema accepted.");
        future.ElementalSchemaVersion = 0;
        Check(!BattleEngine.TryResolvePlan(future, new TurnPlan { RequestId = "mixed-schema", ExpectedRevision = future.Revision }, out _, out _),
            "Mixed legacy/elemental schema accepted.");

        var reflection = ElementFixture(); reflection.Opponent.Fighters[0].Stats.ReflectDamageBp = 10000;
        ElementFuel(reflection.Player.Fighters[0], "status.debuff.ignite", "reflect-source-fuel");
        var reflected = PlayKind(reflection, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Electro));
        Check(reflected.Events.Skip(reflection.Events.Count).Any(e => e.DamageOrigin == ElementalDamageOrigin.Reflection) &&
            !reflected.Events.Skip(reflection.Events.Count).Any(e => e.Kind == BattleEventKind.ElementalReactionTriggered),
            "Generic reflection lost its terminal origin.");

        var retry = ElementFixture(); var retryOwner = retry.Player.Fighters[0];
        retryOwner.Definition.Skills[0].Ranks[0].Effect = ElementAttack(ElementId.Pyro);
        ElementFuel(retry.Opponent.Fighters[0], "status.debuff.shock", "retry-fuel");
        retry.Player.Hand.Clear(); retry.Player.Hand.Add(new CardState { Id = "elemental-retry-card", OwnerFighterId = retryOwner.Id,
            SkillId = retryOwner.Definition.Skills[0].Id, Rank = 1, Category = CardCategory.Attack });
        var draft = new PlanDraft(retry);
        Check(draft.QueuePlay("elemental-retry-card", retry.Opponent.Fighters[0].Id, out _), "Retry fixture could not queue.");
        var retryPlan = draft.BuildPlan("elemental-retry"); var session = new LocalBattleSession(retry);
        Check(session.Submit(retryPlan, out var error), "Elemental session failed: " + error);
        var receipt = SnapshotJson.Serialize(session.GetSnapshot());
        Check(session.Submit(retryPlan, out error) && receipt == SnapshotJson.Serialize(session.GetSnapshot()), "Retry duplicated reaction consumption/damage.");
    }
}
