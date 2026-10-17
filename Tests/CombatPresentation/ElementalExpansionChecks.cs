using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

internal static partial class BattlePlaybackChecks
{
    private static void DotBonus(FighterState source, int amount) => StatusSystem.Apply(source, source.Id, source.Side,
        new StatusRecipeDefinition { Id = "fixture.dot-bonus", Polarity = StatusPolarity.Buff, DefaultDuration = 10,
            Modifiers = new List<StatModifierDefinition> { new StatModifierDefinition { Target = ModifierTarget.FamilyDamageDealt,
                Family = DamageFamily.DamageOverTime, Amount = amount } } }, "dot-bonus", "fixture", 1);

    private static void ElementalExpansionChecks()
    {
        var start = _assertions;
        Check(ElementalContentValidator.Validate(ElementalRuleSet.CreateDefault(), StandardEffectDatabase.CreateStatusRecipes()).Count == 0, "Default rules invalid.");
        foreach (var level in new[] { 1, 10, 90 })
        foreach (var attack in new[] { 10, 1000 })
        {
            var state = ElementFixture(); var source = state.Player.Fighters[0]; source.Level = level; source.Stats.Attack = attack;
            DotBonus(source, 5000);
            state.Opponent.Fighters[0].ElementStatuses.Apply(ElementId.Pyro, "setup", "overload");
            var effect = ElementAttack(ElementId.Electro); effect.CoefficientBp = 0;
            var next = PlayKind(state, CardCategory.Attack, EffectTargetScope.SelectedEnemy, effect);
            var damage = next.Events.Skip(state.Events.Count).Where(e => e.DamageOrigin == ElementalDamageOrigin.Reaction && e.Kind == BattleEventKind.DamageApplied).ToList();
            Check(damage.Count == 2 && damage.All(e => e.Amount == level * 500 * 20000L * 15000L / 100000000 && e.DamageElement == ElementId.Pyro), "Overload is not level/DOT based at 2x or spreads beyond adjacent slots.");
        }
        var shield = ElementFixture(); shield.Player.Fighters[0].Level = 10;
        shield.Opponent.Fighters[0].Shield = 100;
        shield.Opponent.Fighters[0].ElementStatuses.Apply(ElementId.Pyro, "setup", "shield");
        var zeroElectro = ElementAttack(ElementId.Electro); zeroElectro.CoefficientBp = 0;
        var shieldNext = PlayKind(shield, CardCategory.Attack, EffectTargetScope.SelectedEnemy, zeroElectro);
        var overload = shieldNext.Events.Skip(shield.Events.Count).Single(e => e.DamageOrigin == ElementalDamageOrigin.Reaction && e.TargetId == shield.Opponent.Fighters[0].Id);
        Check(overload.Amount == 2000 && overload.ShieldLost == 100 && overload.HealthLost == 1920, "Overload did not use 2x damage and 80% shield efficiency.");
        var charged = ElementFixture(); charged.Player.Fighters[0].Level = 10; charged.Player.Fighters[0].Stats.Attack = 12345; DotBonus(charged.Player.Fighters[0], 5000);
        foreach (var target in charged.Opponent.LivingActive()) target.ElementStatuses.Apply(ElementId.Hydro, "setup", "hydro:" + target.Id);
        charged.Opponent.Fighters[0].ElementStatuses.Instances[0].RemainingDuration = 1;
        var chargedNext = PlayKind(charged, CardCategory.Attack, EffectTargetScope.SelectedEnemy, zeroElectro);
        foreach (var target in chargedNext.Opponent.LivingActive())
        {
            var status = target.Statuses.Instances.Single(s => s.RecipeId == "status.reaction.charged");
            Check(status.RemainingDuration == 2 && status.Snapshot.FixedAmount == 1000, "Charged spread did not use a two-turn level snapshot.");
            Check(target.ElementStatuses.Has(ElementId.Hydro), "Charged consumed Hydro.");
        }
        Check(chargedNext.Opponent.Fighters[0].ElementStatuses.Has(ElementId.Electro) && chargedNext.Opponent.Fighters[0].ElementStatuses.Instances.First(s => s.Element == ElementId.Hydro).RemainingDuration == 1,
            "Charged consumed incoming Electro or refreshed existing Hydro.");
        var spread = chargedNext.Events.Skip(charged.Events.Count).Where(e => e.Kind == BattleEventKind.ElementalReactionTriggered && e.TargetIds.Count > 0).ToList();
        Check(spread.Count == 2 && spread.All(e => e.ElementalReaction == ElementalReactionId.ElectroCharged && e.ParentEventId > 0), "Electrify arcs lost their public origin/lineage.");
        var instance = chargedNext.Opponent.Fighters[0].Statuses.Instances.Single(); instance.RemainingDuration = 1;
        Check(!StatusSystem.Apply(chargedNext.Opponent.Fighters[0], "stronger", TeamSide.Player, instance.Recipe.Clone(), "stronger", "fixture", 1,
            snapshot: new StatusSnapshot { SourceAttack = 9999 }, duration: 9).Accepted, "Charged refreshed/replaced while active.");
        Check(instance.RemainingDuration == 1 && instance.Snapshot.FixedAmount == 1000, "Existing Charged changed strength/duration.");
        Check(BattleEngine.TryResolvePlan(chargedNext, new PlanDraft(chargedNext).BuildPlan("charged-tick"), out var ticked, out var error), error);
        var ticks = ticked.Events.Skip(chargedNext.Events.Count).Where(e => e.Kind == BattleEventKind.DamageApplied && e.StatusRecipeId == "status.reaction.charged").ToList();
        Check(ticks.Count == 3 && ticks.All(e => e.Amount == 2250 && e.DamageElement == ElementId.Electro), "Charged ticks did not use 1.5x damage, DOT bonus/level or recursed.");
        Check(!ticked.Opponent.Fighters[0].ElementStatuses.Has(ElementId.Hydro) && ticked.Opponent.Fighters[0].ElementStatuses.Instances.Single().RemainingDuration == 2, "Charged altered aura expiry clocks.");
        var lateSpread = ElementFixture(); lateSpread.Player.Fighters[0].Level = 10;
        lateSpread.Opponent.Fighters[0].ElementStatuses.Apply(ElementId.Hydro, "setup", "late-hydro0");
        var lateStart = PlayKind(lateSpread, CardCategory.Attack, EffectTargetScope.SelectedEnemy, zeroElectro);
        lateStart.Opponent.Fighters[1].ElementStatuses.Apply(ElementId.Hydro, "setup", "late-hydro1");
        Check(BattleEngine.TryResolvePlan(lateStart, new PlanDraft(lateStart).BuildPlan("late-spread"), out var lateTick, out error), error);
        Check(lateTick.Opponent.Fighters[1].Statuses.Instances.Any(s => s.RecipeId == "status.reaction.charged" && s.RemainingDuration == 2), "Charged tick did not spread to newly Hydro neighbor with full duration.");
        Check(lateTick.Events.Skip(lateStart.Events.Count).Count(e => e.Kind == BattleEventKind.DamageApplied && e.StatusRecipeId == "status.reaction.charged") == 1, "New spread ticked in the same batch.");
        var superconduct = ElementFixture(); superconduct.Player.Fighters[0].Level = 10; DotBonus(superconduct.Player.Fighters[0], 5000);
        superconduct.Opponent.Fighters[0].ElementStatuses.Apply(ElementId.Cryo, "setup", "cryo");
        var superNext = PlayKind(superconduct, CardCategory.Attack, EffectTargetScope.SelectedEnemy, zeroElectro);
        var superDamage = superNext.Events.Skip(superconduct.Events.Count).Where(e => e.Kind == BattleEventKind.DamageApplied && e.DamageOrigin == ElementalDamageOrigin.Reaction).ToList();
        Check(superDamage.Count == 2 && superDamage.All(e => e.Amount == 2250 && e.DamageElement == ElementId.Cryo), "Superconduct 1.5x level/DOT spread is wrong.");
        foreach (var recipient in superNext.Opponent.LivingActive().Take(2))
        {
            var status = recipient.Statuses.Instances.Single(s => s.RecipeId == "status.reaction.superconduct");
            Check(status.RemainingDuration == 2, "Superconduct must last two turns.");
            foreach (var family in new[] { DamageFamily.Normal, DamageFamily.True, DamageFamily.Additional, DamageFamily.DamageOverTime })
            {
                var policy = StatusSystem.BuildDamagePolicy(superNext.Player.Fighters[0], recipient, family);
                var baseline = policy.Clone(); baseline.PhysicalReceivedIncreaseBp = 0;
                var before = DamageResolver.Resolve(new DamagePacket { BaseAmount = 100, Policy = baseline }, new StatBlock(), new StatBlock(), 10000, 0, -1, -1).CalculatedDamage;
                var after = DamageResolver.Resolve(new DamagePacket { BaseAmount = 100, Policy = policy }, new StatBlock(), new StatBlock(), 10000, 0, -1, -1).CalculatedDamage;
                Check(after == before * 14 / 10, "Superconduct did not increase non-elemental family: " + family);
                policy.Element = ElementId.Pyro;
                Check(DamageResolver.Resolve(new DamagePacket { BaseAmount = 100, Policy = policy }, new StatBlock(), new StatBlock(), 10000, 0, -1, -1).CalculatedDamage == before, "Superconduct increased elemental damage.");
            }
            status.RemainingDuration = 1;
            Check(!StatusSystem.Apply(recipient, "again", TeamSide.Player, status.Recipe.Clone(), "again", "fixture", 1).Accepted && status.RemainingDuration == 1, "Superconduct refreshed.");
        }
        var swirl = ElementFixture(); DotBonus(swirl.Player.Fighters[0], 9000);
        swirl.Opponent.Fighters[0].ElementStatuses.Apply(ElementId.Pyro, "setup", "swirl");
        var swirlNext = PlayKind(swirl, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Anemo));
        Check(swirlNext.Opponent.Fighters[0].ElementStatuses.Has(ElementId.Pyro) && swirlNext.Opponent.Fighters[1].ElementStatuses.Has(ElementId.Pyro), "Swirl lost source/spread aura.");
        Check(swirlNext.Opponent.Fighters[1].Statuses.Instances.Count == 0 && swirlNext.Events.Skip(swirl.Events.Count).Single(e => e.DamageOrigin == ElementalDamageOrigin.Reaction).Amount == 100, "Swirl still synchronizes a DOT or DOT bonus.");
        var crystal = ElementFixture(); crystal.Opponent.Fighters[0].ElementStatuses.Apply(ElementId.Hydro, "setup", "crystal");
        var crystalNext = PlayKind(crystal, CardCategory.Attack, EffectTargetScope.SelectedEnemy, ElementAttack(ElementId.Geo));
        Check(crystalNext.Player.Fighters[0].Shield == 30 && crystalNext.Opponent.Fighters[0].ElementStatuses.Has(ElementId.Hydro), "Crystallize changed shield basis or consumed aura.");
        var oldBalance = ElementFixture();oldBalance.ElementalRules.ContentVersion="elemental-auras-v2";oldBalance.ElementalRules.ReactionDamagePerLevel=10;
        Check(BattleEngine.TryResolvePlan(oldBalance,new PlanDraft(oldBalance).BuildPlan("balance-upgrade"),out var newBalance,out error),error);
        Check(newBalance.ElementalRules.ReactionDamagePerLevel==500 && oldBalance.ElementalRules.ReactionDamagePerLevel==10,
            "Saved default reaction formula did not upgrade on a detached copy.");
        var oldSnapshot = ElementFixture(); oldSnapshot.ElementalSchemaVersion = 1; oldSnapshot.ElementalRules.SchemaVersion = 1;
        StatusSystem.Apply(oldSnapshot.Opponent.Fighters[0], oldSnapshot.Player.Fighters[0].Id, TeamSide.Player,
            StandardEffectDatabase.CreateStatusRecipes().Find(r => r.Id == "status.debuff.ignite"), "legacy-ignite", "fixture", 1);
        Check(BattleEngine.TryResolvePlan(oldSnapshot, new PlanDraft(oldSnapshot).BuildPlan("legacy-upgrade"), out var upgraded, out error), error);
        Check(upgraded.ElementalSchemaVersion == 2 && upgraded.Opponent.Fighters[0].ElementStatuses.Instances.Count == 0 &&
            upgraded.Opponent.Fighters[0].Statuses.Instances.Any(s => s.InstanceId == "legacy-ignite") && oldSnapshot.ElementalSchemaVersion == 1,
            "Legacy snapshot migration inferred auras from debuffs or mutated its input.");
        var legacyEffect = ElementAttack(ElementId.None);
        legacyEffect.ElementalApplication = new ElementalApplicationDefinition { Element = ElementId.Electro,
            HitIndex = 3, StatusEffectId = "ordinary-shock", RequiresCritical = true };
        legacyEffect.Sequence.Add(new CardEffectStep { Timing = CardEffectTiming.AfterDamage, Effect = new EffectDefinition {
            Id = "ordinary-shock", Kind = EffectKind.ApplyStatus, StatusRecipe = StandardEffectDatabase.CreateStatusRecipes().Find(r => r.Id == "status.debuff.shock") } });
        var migratedEffect = legacyEffect.Clone();
        Check(migratedEffect.DamageElement == ElementId.Electro && migratedEffect.ElementalApplication == null &&
            migratedEffect.Sequence[0].Effect.Conditions.Single().Kind == EffectConditionKind.WasCritical && legacyEffect.DamageElement == ElementId.None,
            "Legacy card migration did not detach ordinary critical debuff delivery from elemental damage.");
        var encounter = new FightingAllstar.Core.Run.EncounterProjection { BattleId = "level-snapshot" };
        encounter.PlayerTeam.Add(new FightingAllstar.Core.Run.EncounterFighterSnapshot { FighterId = "level-source", DefinitionId = "level-source",
            Definition = Fighter("level-source", 1000), Stats = new StatBlock { MaxHealth = 1000 }, CurrentHealth = 1000, Level = 77 });
        encounter.EnemyTeam.Add(new FightingAllstar.Core.Run.EncounterFighterSnapshot { FighterId = "level-target", DefinitionId = "level-target",
            Definition = Fighter("level-target", 1000), Stats = new StatBlock { MaxHealth = 1000 }, CurrentHealth = 1000, Level = 31 });
        var leveled = FightingAllstar.Core.Run.RunBattleBridge.CreateLocalBattle(encounter, 90);
        Check(leveled.Player.Fighters[0].Level == 77 && leveled.Opponent.Fighters[0].Level == 31 &&
            ElementalReactionRules.LevelDamage(leveled.Player.Fighters[0], leveled.ElementalRules) == 38500,
            "Encounter levels were dropped before reaction calculation.");
        ElementalForecastChecks();
        FreezeBehaviorChecks();
        ReactionBalanceChecks();
        Console.WriteLine("PASS: " + (_assertions - start) + " reaction output, spread, scaling and forecast assertions.");
    }

    private static void ReactionBalanceChecks()
    {
        foreach(var existing in new[] { ElementId.Pyro,ElementId.Dendro })
        foreach(var level in new[] { 1,10,90 })
        foreach(var attack in new[] { 10,9999 })
        {
            var state=ElementFixture(); var source=state.Player.Fighters[0]; var target=state.Opponent.Fighters[0];
            source.Level=level; source.Stats.Attack=attack; DotBonus(source,5000);
            source.Stats.ElementalModifiers.Add(new ElementalModifierDefinition { Element=ElementId.Pyro,DamageDealtBp=2000 });
            target.Stats.ElementalModifiers.Add(new ElementalModifierDefinition { Element=ElementId.Pyro,ResistanceBp=2000 });
            target.ElementStatuses.Apply(existing,"setup","burning");
            var hit=ElementAttack(existing==ElementId.Pyro?ElementId.Dendro:ElementId.Pyro,3); hit.CoefficientBp=0;
            var next=PlayKind(state,CardCategory.Attack,EffectTargetScope.SelectedEnemy,hit);
            var damage=next.Events.Skip(state.Events.Count).Where(e=>e.Kind==BattleEventKind.DamageApplied &&
                e.ElementalReaction==ElementalReactionId.Burning && e.DamageOrigin==ElementalDamageOrigin.Reaction).ToList();
            Check(damage.Count==0 && next.Opponent.Fighters[0].Statuses.Instances.Single(s=>s.RecipeId=="status.reaction.burning").RemainingDuration==2,
                "Burning must retain a two-turn status without an immediate reaction packet.");
            Check(next.Opponent.Fighters[0].Statuses.Instances.Single(s=>s.RecipeId=="status.reaction.burning").Snapshot.SourceAttack==attack &&
                next.Opponent.Fighters[1].Health==state.Opponent.Fighters[1].Health,
                "Burning lost its existing ATK snapshot or spread beyond its target.");
            var ticking=next;
            for(var turn=0;turn<3;turn++)
            {
                Check(BattleEngine.TryResolvePlan(ticking,new PlanDraft(ticking).BuildPlan("burning-tick:"+turn),out var passed,out var error),error);
                ticking=passed;
            }
            var ticks=ticking.Events.Skip(next.Events.Count).Where(e=>e.Kind==BattleEventKind.DamageApplied && e.StatusRecipeId=="status.reaction.burning").ToList();
            Check(ticks.Count==2 && ticks.All(e=>e.Amount==(long)attack*3*12000*8000/100000000 && e.DamageElement==ElementId.Pyro) &&
                ticking.Opponent.Fighters[0].Statuses.Instances.All(s=>s.RecipeId!="status.reaction.burning"),
                "Burning must tick twice at 3x snapshotted ATK, retaining its Pyro modifiers and natural expiry.");
        }
        var legacy=ElementFixture(); legacy.ElementalRules.ContentVersion="elemental-auras-v3";
        legacy.ElementalRules.ReactionDamageCap=1234;
        legacy.ElementalRules.Reactions.RemoveAll(r=>r.Id==ElementalReactionId.Shatter);
        foreach(var reaction in legacy.ElementalRules.Reactions)
        {
            if(reaction.Id==ElementalReactionId.Overload) reaction.AttackCoefficientBp=15000;
            if(reaction.Id==ElementalReactionId.Superconduct) reaction.AttackCoefficientBp=10000;
            if(reaction.Id==ElementalReactionId.Burning) reaction.AttackCoefficientBp=0;
        }
        legacy.ElementalRules.Reactions.First(r=>r.Id==ElementalReactionId.Overload).AttackCoefficientBp=17777;
        var charged=GenshinStatusRecipes.CreateOutputs().Single(r=>r.Id=="status.reaction.charged");
        charged.PeriodicDamage.CoefficientBp=10000;
        StatusSystem.Apply(legacy.Opponent.Fighters[0],legacy.Player.Fighters[0].Id,TeamSide.Player,charged,"legacy-charged","fixture",1,
            snapshot:new StatusSnapshot { FixedAmount=1000 });
        var burning=GenshinStatusRecipes.CreateOutputs().Single(r=>r.Id=="status.reaction.burning");
        burning.PeriodicDamage.CoefficientBp=10000;
        StatusSystem.Apply(legacy.Opponent.Fighters[0],legacy.Player.Fighters[0].Id,TeamSide.Player,burning,"legacy-burning","fixture",2,
            snapshot:new StatusSnapshot { SourceAttack=123 });
        var before=SnapshotJson.Serialize(legacy);
        Check(BattleEngine.TryResolvePlan(legacy,new PlanDraft(legacy).BuildPlan("multiplier-migration"),out var updated,out var migrationError),migrationError);
        Check(updated.ElementalRules.ContentVersion=="elemental-auras-v6" && updated.ElementalRules.ReactionDamageCap==1234 &&
            updated.ElementalRules.Reactions.Any(r=>r.Id==ElementalReactionId.Shatter && r.AttackCoefficientBp==30000) &&
            updated.ElementalRules.Reactions.Where(r=>r.Id==ElementalReactionId.Overload).Select(r=>r.AttackCoefficientBp).OrderBy(n=>n).SequenceEqual(new[] { 17777,20000 }) &&
            updated.ElementalRules.Reactions.Where(r=>r.Id==ElementalReactionId.Superconduct).All(r=>r.AttackCoefficientBp==15000) &&
            updated.ElementalRules.Reactions.Where(r=>r.Id==ElementalReactionId.Burning).All(r=>r.AttackCoefficientBp==0),
            "Legacy reaction migration lost custom coefficients/caps or failed to upgrade defaults.");
        Check(updated.Opponent.Fighters[0].Statuses.Instances.Single(s=>s.RecipeId==charged.Id).Recipe.PeriodicDamage.CoefficientBp==15000 &&
            updated.Opponent.Fighters[0].Statuses.Instances.Single(s=>s.RecipeId==burning.Id).Recipe.PeriodicDamage.CoefficientBp==30000 &&
            SnapshotJson.Serialize(legacy)==before,"Legacy Charged/Burning did not upgrade on a detached snapshot.");
    }

    private static void ElementalForecastChecks()
    {
        var state = ElementFixture(); state.ActionBudget = 3;
        var target = state.Opponent.Fighters[0];
        var actions = new[] {
            new ElementalForecastAction { CardId = "cryo", TargetId = target.Id, Effect = ElementAttack(ElementId.Cryo) },
            new ElementalForecastAction { CardId = "pyro", TargetId = target.Id, Effect = ElementAttack(ElementId.Pyro) },
            new ElementalForecastAction { CardId = "pyro2", TargetId = target.Id, Effect = ElementAttack(ElementId.Pyro) } };
        var before = SnapshotJson.Serialize(state);
        var view = BattleProjectionBuilder.Build(state, TeamSide.Player, "fixture", long.MaxValue);
        var forecast = ElementalForecast.Build(view, state.ElementalRules, actions);
        Check(forecast.Lines.Count == 1 && forecast.Lines[0].Reaction == ElementalReactionId.Melt && forecast.Lines[0].ActionIndex == 1 && !forecast.Lines[0].Conditional, "Forecast did not use automatic aura setup/consumption.");
        Check(SnapshotJson.Serialize(state) == before, "Forecast mutated authority.");
        target.ElementStatuses.Apply(ElementId.Hydro, "setup", "public-hydro");
        var restored = SnapshotJson.Deserialize<BattleState>(SnapshotJson.Serialize(state));
        restored.Opponent.Fighters[0].ElementStatuses.Instances[0].RemainingDuration = 1;
        Check(target.ElementStatuses.Instances[0].RemainingDuration == 3 && state.Clone().Opponent.Fighters[0].ElementStatuses.Has(ElementId.Hydro), "Aura state not detached on clone/save.");
        view = BattleProjectionBuilder.Build(state, TeamSide.Player, "fixture", long.MaxValue);
        Check(view.OpponentTeam.Fighters[0].ElementStatuses.Single().Polarity == "Neutral" && view.OpponentTeam.Fighters[0].Statuses.Count == 0 && view.OpponentTeam.Hand.Count == 0, "Public projection mixed aura/debuff/private card state.");
    }
}
