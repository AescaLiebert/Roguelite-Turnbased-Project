using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

internal static partial class BattlePlaybackChecks
{
    private static BattleState PlayOwned(BattleState state, TeamSide side, int slot, int rank, string targetId = null, bool ultimate = false)
    {
        state.ActingSide = side; state.Phase = BattlePhase.Planning;
        var owner = state.Team(side).Fighters[0];
        var skill = owner.Definition.Skills.Find(s => s.Slot == slot);
        var card = new CardState { Id = "ownership:" + state.Events.Count, OwnerFighterId = owner.Id,
            SkillId = ultimate ? null : skill.Id, Rank = rank, Category = skill.Category, TargetScope = skill.TargetScope,
            Kind = ultimate ? CardKind.Ultimate : CardKind.Skill, UltimateTier = ultimate ? rank : 0 };
        owner.PowerGauge = 5;
        state.Team(side).Hand.Clear(); state.Team(side).Hand.Add(card);
        var plan = new PlanDraft(state);
        Check(plan.QueuePlay(card.Id, targetId ?? state.OtherTeam(side).Fighters[0].Id, out var error), "Ownership queue: " + error);
        Check(BattleEngine.TryResolvePlan(state, plan.BuildPlan("ownership-plan:" + state.Events.Count), out var next, out error), "Ownership resolve: " + error);
        return next;
    }

    private static EffectDefinition OwnershipAttack(int damage, bool area = false) => new EffectDefinition {
        Kind = EffectKind.Damage, Scaling = StatScaling.Fixed, Magnitude = damage,
        Target = area ? EffectTargetScope.AllEnemies : EffectTargetScope.SelectedEnemy,
        Attack = new DamageAttackDefinition { HitCount = 3 },
        Sequence = new List<CardEffectStep> { new CardEffectStep { Timing = CardEffectTiming.AfterDamage,
            Effect = new EffectStepDefinition { Id = "bonus", Kind = EffectKind.Damage,
                Target = area ? EffectTargetScope.AllEnemies : EffectTargetScope.SelectedEnemy,
                DamageRecipe = new DamageEffectRecipe { Family = DamageFamily.Additional, StatSource = DamageStatSource.Target,
                    Scaling = StatScaling.MaxHealth, CoefficientBp = 2000, CannotCrit = true, CannotBlock = true } } } } };

    private static void SkillEffectOwnershipChecks()
    {
        var start = _assertions;
        foreach (var scenario in new[] { "normal", "overkill", "shield", "evade", "immune" })
        {
            var a = Fighter("packet-owner", 20000, 10); var b = Fighter("packet-target", 1000, 10);
            a.Skills[1].Ranks[0].Effect = OwnershipAttack(scenario == "overkill" ? 3000 : 90);
            var state = BattleEngine.Create("packet-" + scenario, new[] { a }, null, new[] { b }, null, 421);
            if (scenario == "shield") state.Opponent.Fighters[0].Shield = 5000;
            if (scenario == "evade" || scenario == "immune") StatusSystem.Apply(state.Opponent.Fighters[0], "fixture", TeamSide.Opponent,
                new StatusRecipeDefinition { Id = "test." + scenario, DefaultDuration = 5, EvadeAttacks = scenario == "evade",
                    AdditionalDamageImmunity = scenario == "immune" }, "fixture", "fixture", 1);
            var next = PlayOwned(state, TeamSide.Player, 2, 1);
            var packets = next.Events.Where(e => e.Kind == BattleEventKind.DamageApplied && e.EffectId == "bonus").ToArray();
            Check(scenario == "evade" ? packets.Length == 0 : packets.Length == 1, "Packet frequency / evade: " + scenario);
            if (packets.Length > 0) Check(packets[0].Amount == (scenario == "immune" ? 0 : 200), "Target Max HP and immunity: " + scenario + " got " + packets[0].Amount);
            Check(!next.Events.Any(e => e.Kind == BattleEventKind.PassiveTriggered && e.Message != "BeforeDefeat"), "Skill packet never announces a passive: " + string.Join(";", next.Events.Where(e => e.Kind == BattleEventKind.PassiveTriggered).Select(e => e.Message)));
            if (scenario == "overkill") Check(!next.Opponent.Fighters[0].IsAlive, "Overkill finalizes defeat at the action boundary.");
        }
        var aoe = Fighter("area-owner", 20000); var first = Fighter("area-a", 1000); var second = Fighter("area-b", 2000);
        first.TraitIds.Add("trait.women");
        aoe.Skills[1].TargetScope = EffectTargetScope.AllEnemies;
        aoe.Skills[1].Ranks[1].Effect = OwnershipAttack(30, true);
        aoe.Skills[1].Ranks[1].Effect.Sequence[0].Effect.Conditions.Add(new EffectConditionDefinition {
            Kind = EffectConditionKind.TargetTraitIs, StringValue = "trait.women", Subject = ConditionSubject.OperationTarget });
        aoe.Skills[1].Ranks[1].Effect.Sequence[0].Effect.Conditions.Add(new EffectConditionDefinition { Kind = EffectConditionKind.CardRankAtLeast, Threshold = 2 });
        var areaState = BattleEngine.Create("area", new[] { aoe }, null, new[] { first, second }, null, 421);
        var areaResult = PlayOwned(areaState, TeamSide.Player, 2, 2);
        Check(areaResult.Events.Count(e => e.EffectId == "bonus") == 1 && areaResult.Events.Single(e => e.EffectId == "bonus").TargetId == areaResult.Opponent.Fighters[0].Id,
            "Area conditions and rank context are recipient-specific.");

        var rider = new EffectStepDefinition { Id = "rank-rider", Kind = EffectKind.ApplyStatus, Target = EffectTargetScope.Self,
            StatusRecipe = new StatusRecipeDefinition { Id = "rank-qualified", DefaultDuration = 4 },
            Conditions = new List<EffectConditionDefinition> { new EffectConditionDefinition { Kind = EffectConditionKind.CardRankAtLeast, Threshold = 2 } } };
        aoe.Skills[1].Ranks[1].Effect.Sequence.Add(new CardEffectStep { Timing = CardEffectTiming.AfterAction, Effect = rider });
        areaResult = PlayOwned(BattleEngine.Create("utility-rank", new[] { aoe }, null, new[] { first, second }, null, 421), TeamSide.Player, 2, 2);
        Check(StatusSystem.Count(areaResult.Player.Fighters[0], recipeId: "rank-qualified") > 0, "Utility sequence receives the actual card rank.");

        var broken = aoe.Clone(); broken.Skills[1].Ranks[1].Effect.Sequence[0].Timing = CardEffectTiming.BeforeAction;
        broken.Skills[1].Ranks[1].Effect.Sequence[0].Effect.Conditions.Add(new EffectConditionDefinition { Kind = EffectConditionKind.WasCritical });
        Check(SkillEffectValidator.Validate(broken).Any(e => e.Contains("hit outcomes")), "Unresolved hit predicates fail validation.");

        var criticalOwner = Fighter("critical-owner", 20000); criticalOwner.BaseStats.CritChanceBp = 10000;
        var resistant = second.Clone(); resistant.BaseStats.CritResistanceBp = 10000;
        criticalOwner.Skills[1].TargetScope = EffectTargetScope.AllEnemies;
        var criticalAttack = OwnershipAttack(30, true);
        criticalAttack.Sequence.Add(new CardEffectStep { Timing = CardEffectTiming.AfterDamage, Effect = new EffectStepDefinition {
            Id = "critical-rider", Kind = EffectKind.ApplyStatus, Target = EffectTargetScope.AllEnemies,
            StatusRecipe = new StatusRecipeDefinition { Id = "critical-qualified", Polarity = StatusPolarity.Debuff, DefaultDuration = 3 },
            Conditions = new List<EffectConditionDefinition> { new EffectConditionDefinition { Kind = EffectConditionKind.WasCritical } } } });
        criticalOwner.Skills[1].Ranks[0].Effect = criticalAttack;
        var criticalState = BattleEngine.Create("crit-snapshots", new[] { criticalOwner }, null, new[] { first, resistant }, null, 421);
        criticalState.CapturePassiveTrace = true;
        var criticalResult = PlayOwned(criticalState, TeamSide.Player, 2, 1);
        Check(StatusSystem.Count(criticalResult.Opponent.Fighters[0], recipeId: "critical-qualified") > 0 &&
            StatusSystem.Count(criticalResult.Opponent.Fighters[1], recipeId: "critical-qualified") == 0,
            "Critical rider uses each original recipient's primary hits, after noncritical bonus packets.");
        Check(criticalResult.PassiveTrace.Any(t => t.RuleId == "critical-rider" && !t.ConditionsPassed) &&
            criticalResult.PassiveTrace.Any(t => t.RuleId == "critical-rider" && t.Fired), "Battle Lab records executed and skipped skill conditions.");

        foreach (var name in new[] { "scii-taki", "billy97", "benimaru99" })
        {
            var path = "Artifacts/SkillEffectOwnership/" + name + "-after.json";
            Check(File.Exists(path), "Native migration fixture exists: " + name);
            var native = SnapshotJson.Deserialize<CharacterDefinition>(File.ReadAllText(path));
            Check(!SkillEffectOwnershipMigration.Apply(native), "Migration is idempotent: " + name);
            Check(SkillEffectValidator.Validate(native).Count == 0, "Native rank payloads validate: " + name);
            Check(!native.Passive.Reactions.Any(r => r.Id.Contains("guard-burn") || r.Id == "apply-debuff-women-defense-down" || r.Id == "skill-2-target-max-health-damage"), "No duplicated skill rules in passive: " + name);
            if (name == "billy97")
            {
                var invalidContext = native.Clone();
                var invalidRule = invalidContext.Skills[1].Ranks[0].Effect.StatusRecipe.StanceChildren.Single(c => c.Id == "status.stance.apply").Reactions[0];
                invalidRule.Conditions.Add(new EffectConditionDefinition { Kind = EffectConditionKind.ResourceEnteredAtLeast, Threshold = 1 });
                Check(SkillEffectValidator.Validate(invalidContext).Any(e => e.Contains("matching event")), "Status reaction rejects a predicate without its required event.");
                invalidRule.Trigger = PassiveEventKind.ResourceChanged;
                Check(SkillEffectValidator.Validate(invalidContext).Count == 0, "A matching status-event context permits its predicate.");
                var holder = native.Clone();
                var enemy = Fighter("snapshot-attacker", 100000, 1);
                enemy.Skills[0].Ranks[0].Effect = new EffectDefinition { Family = DamageFamily.True, Scaling = StatScaling.Fixed, Magnitude = 10 };
                var fixture = BattleEngine.Create("grant-snapshot", new[] { holder }, null, new[] { enemy }, null, 421);
                var guard = PlayOwned(fixture, TeamSide.Player, 2, 3);
                var refreshed = PlayOwned(guard, TeamSide.Player, 2, 1);
                Check(refreshed.Player.Fighters[0].Statuses.Instances.Find(s => s.Recipe.Reactions.Count > 0).Snapshot.GrantingRank == 1,
                    "Successful refresh replaces the reaction's granting-rank payload.");
                var expiring = guard.Clone();
                expiring.Player.Fighters[0].Statuses.Instances.ForEach(s => s.SkipNextDurationClock = false);
                for (var tick = 0; tick < 4; tick++) expiring.Player.Fighters[0].Statuses.Advance(StatusDurationClock.TargetTurnEnd);
                var oldCount = expiring.Events.Count;
                var expiredAttack = PlayOwned(expiring, TeamSide.Opponent, 1, 1);
                Check(!expiredAttack.Events.Skip(oldCount).Any(e => e.StatusRecipeId == "status.debuff.ignite"), "Natural expiry also ends granted reactions.");
                holder.UltimateTiers[4].Effect = holder.Skills[1].Ranks[2].Effect.Clone(); holder.UltimateTiers[4].Category = CardCategory.Stance;
                var ultimateFixture = BattleEngine.Create("ultimate-grant", new[] { holder }, null, new[] { enemy }, null, 421);
                var ultimateGuard = PlayOwned(ultimateFixture, TeamSide.Player, 2, 4, ultimate: true);
                ultimateGuard.Player.Fighters[0].ConstellationTier = 0;
                oldCount = ultimateGuard.Events.Count;
                var ultimateResponse = PlayOwned(ultimateGuard, TeamSide.Opponent, 1, 1);
                Check(ultimateResponse.Events.Skip(oldCount).Any(e => e.StatusRecipeId == "status.debuff.ignite" && e.GrantingTier == 4),
                    "Ultimate reaction keeps its casting tier after owner progression changes.");
            }
            for (var rank = 1; rank <= 3; rank++)
            {
                var state = BattleEngine.Create("native-" + name + rank, new[] { native }, null,
                    new[] { Fighter("native-enemy", 100000, 1) }, null, 421);
                state.Opponent.Fighters[0].Definition.TraitIds.Add("trait.women");
                state.Opponent.Fighters[0].Definition.Skills[0].Ranks[0].Effect = new EffectDefinition { Kind = EffectKind.Damage, Family = DamageFamily.True, Scaling = StatScaling.Fixed, Magnitude = 10 };
                var cast = PlayOwned(state, TeamSide.Player, 2, rank);
                if (name == "scii-taki")
                {
                    Check(cast.Events.Count(e => e.EffectOrigin == EffectOrigin.Skill && e.Kind == BattleEventKind.DamageApplied) == 1, "Native Taki bonus at rank " + rank);
                    foreach (var slot in new[] { 1, 2 })
                    {
                        var unrelated = PlayOwned(state.Clone(), TeamSide.Player, slot, rank, ultimate: slot == 2);
                        Check(!unrelated.Events.Any(e => e.EffectOrigin == EffectOrigin.Skill && e.Kind == BattleEventKind.DamageApplied), "No Taki bonus on unrelated skill / ultimate.");
                    }
                    continue;
                }
                if (name == "billy97")
                {
                    var root = cast.Player.Fighters[0].Statuses.Instances.Single(s => s.RecipeId == native.Skills[1].Ranks[rank - 1].Effect.StatusRecipe.Id);
                    var child = cast.Player.Fighters[0].Statuses.Instances.Single(s => s.RecipeId == "status.stance.apply");
                    Check(root.Recipe.Reactions.Count == 0 && child.Recipe.Reactions.Count == 1 && child.ParentInstanceId == root.InstanceId,
                        "Billy reaction belongs to the actual stance-child instance at rank " + rank);
                    var withoutChild = cast.Clone();
                    withoutChild.Player.Fighters[0].Statuses.RemoveInstance(child.InstanceId);
                    var beforeChildAttack = withoutChild.Events.Count;
                    var childAttack = PlayOwned(withoutChild, TeamSide.Opponent, 1, 1);
                    Check(childAttack.Player.Fighters[0].Statuses.Instances.Any(s => s.InstanceId == root.InstanceId) &&
                        !childAttack.Events.Skip(beforeChildAttack).Any(e => e.Kind == BattleEventKind.StatusApplied && e.StatusRecipeId == "status.debuff.ignite"),
                        "Removing just the Apply Ignite child stops its reaction while the parent guard remains.");
                }
                var count = cast.Events.Count;
                var attack = PlayOwned(cast, TeamSide.Opponent, 1, 1);
                var recipe = name == "billy97" ? "status.debuff.ignite" : "status.decrease.defense";
                var applied = attack.Events.Skip(count).Where(e => e.Kind == BattleEventKind.StatusApplied && e.StatusRecipeId == recipe).ToArray();
                if (applied.Length != (name == "benimaru99" && rank == 1 ? 0 : 1)) File.WriteAllText("Artifacts/SkillEffectOwnership/reaction-debug.json", SnapshotJson.Serialize(new { Cast = cast, Attack = attack }));
                Check(applied.Length == (name == "benimaru99" && rank == 1 ? 0 : 1), "Native reaction rank eligibility: " + name + rank);
                if (applied.Length > 0)
                {
                    Check(applied[0].TargetId == attack.Opponent.Fighters[0].Id && applied[0].EffectOrigin == EffectOrigin.SkillGrantedStatus && applied[0].GrantingRank == rank,
                        "Status reaction targets attacker and retains granting rank: " + name + rank + " target=" + applied[0].TargetId + " origin=" + applied[0].EffectOrigin + " rank=" + applied[0].GrantingRank + " snapshots=" + string.Join(",", cast.Player.Fighters[0].Statuses.Instances.Select(s => s.RecipeId + ":" + s.Snapshot.GrantingRank)));
                    if (name == "benimaru99") Check(attack.Opponent.Fighters[0].Statuses.Instances.Find(s => s.RecipeId == recipe).PotencyBp == (rank == 2 ? 2500 : 5000), "Benimaru rank-specific potency.");
                }
                var activeGuard = cast.Clone();
                cast.Player.Fighters[0].Statuses.Instances.Clear();
                var removed = PlayOwned(cast, TeamSide.Opponent, 1, 1);
                Check(!removed.Events.Skip(count).Any(e => e.Kind == BattleEventKind.StatusApplied && e.StatusRecipeId == recipe), "Removed guard cannot react: " + name + rank);
                if (name == "benimaru99")
                {
                    activeGuard.Opponent.Fighters[0].Definition.TraitIds.Clear();
                    var unqualified = PlayOwned(activeGuard, TeamSide.Opponent, 1, 1);
                    Check(!unqualified.Events.Skip(count).Any(e => e.Kind == BattleEventKind.StatusApplied && e.StatusRecipeId == recipe), "Non-Women attacker cannot qualify.");
                }
            }
        }
        Console.WriteLine("PASS: " + (_assertions - start) + " skill effect ownership assertions.");
    }
}
