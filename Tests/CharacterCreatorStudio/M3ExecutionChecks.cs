using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

namespace CharacterCreatorStudio.Checks
{
    /// <summary>Synthetic reusable-mechanic fixtures; no case-study character or production asset is introduced.</summary>
    public static class M3ExecutionChecks
    {
        private static int checks;
        private static void Check(bool passed, string message)
        { if (!passed) throw new InvalidOperationException("M3 execution: " + message); checks++; }

        public static int Run()
        {
            checks = 0;
            AtomicResourcesAndComposition();
            RealProtectionAndLayerBurst();
            ThresholdTransitionsAndBeforeDamage();
            OrderingLimitsAndOrigins();
            NewLifecycleHooksPreserveLegacyOptIn();
            StatusOwnershipSnapshotsAndBindings();
            SynchronousInterception();
            SharedBudgetAndFailedTransaction();
            ValidationAndSerialization();
            Console.WriteLine("PASS: " + checks + " M3 runtime composition, resource, lifecycle, ordering, receipt, origin, interception and transaction checks.");
            return checks;
        }

        private static BattleState Fixture()
        {
            var state = new BattleState { MatchId = "synthetic.m3", CapturePassiveTrace = true,
                RngState = 421, Phase = BattlePhase.Planning, ActingSide = TeamSide.Player };
            FighterState Fighter(string id, TeamSide side, int slot) => new FighterState { Id = id, Side = side,
                TeamIndex = slot, FormationSlot = slot, Health = 1000, Definition = Definition(id) };
            state.Player.Fighters.Add(Fighter("owner", TeamSide.Player, 0));
            state.Player.Fighters.Add(Fighter("ally", TeamSide.Player, 1));
            state.Opponent.Fighters.Add(Fighter("enemy.0", TeamSide.Opponent, 0));
            state.Opponent.Fighters.Add(Fighter("enemy.1", TeamSide.Opponent, 1));
            return state;
        }

        private static CharacterDefinition Definition(string id)
        {
            var definition = new CharacterDefinition { Id = id, DisplayName = id, RuntimeReady = true,
                BaseStats = new StatBlock { MaxHealth = 1000, Attack = 100, PerceptionBp = 10000 },
                Passive = new PassiveDefinition { Id = "passive." + id } };
            for (var slot = 1; slot <= 2; slot++)
            {
                var skill = new SkillDefinition { Id = "skill." + slot, Slot = slot, Category = CardCategory.Attack };
                for (var rank = 1; rank <= 3; rank++) skill.Ranks.Add(new SkillRankDefinition { Rank = rank,
                    Effect = new EffectDefinition { Kind = EffectKind.Damage, Target = EffectTargetScope.SelectedEnemy,
                        Scaling = StatScaling.Fixed, Magnitude = 10, CoefficientBp = 10000 } });
                definition.Skills.Add(skill);
            }
            return definition;
        }

        private static PassiveCommandDefinition Increment(string key, int amount = 1) =>
            new PassiveCommandDefinition { Kind = PassiveCommandKind.IncrementCounter, CounterKey = key, Amount = amount };
        private static PassiveActionDefinition Command(string id, PassiveCommandDefinition command) =>
            new PassiveActionDefinition { Id = id, Kind = PassiveActionKind.Command,
                Commands = new List<PassiveCommandDefinition> { command } };
        private static PassiveReactionDefinition Rule(string id, PassiveEventKind kind, params PassiveCommandDefinition[] commands) =>
            new PassiveReactionDefinition { Id = id, ExecutionVersion = 1, Trigger = kind,
                ActorRelation = PassiveRelation.Self, Commands = commands.ToList() };
        private static void Fire(BattleState state, PassiveEventKind kind, string root = "root", string target = "enemy.0",
            bool reaction = false) => CharacterPassiveRuntime.EmitFact(state, new PassiveFact { Kind = kind,
                ActorId = "owner", TargetId = target, RootActionId = root, TurnSide = TeamSide.Player,
                CardOrigin = !reaction, ReactionOrigin = reaction });
        private static int Counter(BattleState state, string key) => CharacterPassiveRuntime.Counter(state.Player.Fighters[0], key);

        private static void AtomicResourcesAndComposition()
        {
            var state = Fixture(); var owner = state.Player.Fighters[0]; var ally = state.Player.Fighters[1];
            CharacterPassiveRuntime.ChangeResource(state, owner, "resource", 4, 6);
            Check(!CharacterPassiveRuntime.TrySpendResource(state, owner, "resource", 5) && Counter(state, "resource") == 4,
                "an insufficient spend leaves its resource unchanged");
            CharacterPassiveRuntime.ChangeResource(state, ally, "resource", 5, 6);
            Check(!CharacterPassiveRuntime.TryTransferResource(state, owner, ally, "resource", 2, 6) &&
                Counter(state, "resource") == 4 && CharacterPassiveRuntime.Counter(ally, "resource") == 5,
                "an over-cap transfer never debits the source");
            Check(CharacterPassiveRuntime.TryTransferResource(state, owner, ally, "resource", 1, 6) &&
                Counter(state, "resource") == 3 && CharacterPassiveRuntime.Counter(ally, "resource") == 6,
                "an exact valid transfer commits both balances");
            Check(CharacterPassiveRuntime.ChangeResource(state, owner, "resource", int.MaxValue, 6) == 3 &&
                Counter(state, "resource") == 6, "resource gain saturates at the declared cap");
            Check(CharacterPassiveRuntime.ChangeResource(state, owner, "resource", int.MinValue, 6) == -6 &&
                Counter(state, "resource") == 0, "resource loss saturates at zero without overflow");

            var spend = new PassiveActionDefinition { Id = "spend", Kind = PassiveActionKind.SpendResource,
                ResourceKey = "resource", Amount = 2,
                Children = new List<PassiveActionDefinition> { Command("success", Increment("reward")) },
                ElseChildren = new List<PassiveActionDefinition> { Command("failure", Increment("fallback")) } };
            var rule = Rule("atomic-branch", PassiveEventKind.BeforeAction); rule.Actions.Add(spend);
            owner.Definition.Passive.Reactions.Add(rule);
            Fire(state, PassiveEventKind.BeforeAction);
            Check(Counter(state, "reward") == 0 && Counter(state, "fallback") == 1,
                "failed spend selects only the failure branch");
            CharacterPassiveRuntime.ChangeResource(state, owner, "resource", 3);
            Fire(state, PassiveEventKind.BeforeAction, "second");
            Check(Counter(state, "resource") == 1 && Counter(state, "reward") == 1 && Counter(state, "fallback") == 1,
                "successful spend precedes its ordered reward branch");
            Check(state.PassiveTrace.Any(t => t.ActionNodeId == "spend" && !t.Fired && t.Reason.Contains("failed")) &&
                state.PassiveTrace.Any(t => t.ActionNodeId == "success" && t.Fired), "action-node traces expose both branches");

            var loopRule = Rule("targets", PassiveEventKind.AfterAction);
            loopRule.Actions.Add(new PassiveActionDefinition { Id = "loop", Kind = PassiveActionKind.ForEachTarget,
                MaximumTargets = 2, Query = new TargetQueryDefinition { BaseScope = EffectTargetScope.AllEnemies,
                    ExcludeEventTarget = true, StoreBinding = "other-enemies" },
                Children = new List<PassiveActionDefinition> { Command("gauge", new PassiveCommandDefinition {
                    Kind = PassiveCommandKind.ExecuteEffect, Effect = new CardEffectOperationDefinition {
                        Id = "gauge-other", Kind = CardEffectOperationKind.ChangePowerGauge,
                        Target = EffectTargetScope.OperationTarget, Magnitude = 1 } }) } });
            owner.Definition.Passive.Reactions.Add(loopRule);
            Fire(state, PassiveEventKind.AfterAction);
            Check(state.Opponent.Fighters[0].PowerGauge == 0 && state.Opponent.Fighters[1].PowerGauge == 1,
                "bounded target iteration excludes the original target and executes against each selected target");
        }

        private static void OrderingLimitsAndOrigins()
        {
            var state = Fixture(); var passive = state.Player.Fighters[0].Definition.Passive;
            var after = Rule("after", PassiveEventKind.BeforeAction, Increment("ordered")); after.Phase = PassiveExecutionPhase.After;
            var normal = Rule("normal", PassiveEventKind.BeforeAction, Increment("ordered"));
            var high = Rule("high", PassiveEventKind.BeforeAction, Increment("ordered")); high.Priority = 9;
            var before = Rule("before", PassiveEventKind.BeforeAction, Increment("ordered")); before.Phase = PassiveExecutionPhase.Before;
            passive.Reactions.AddRange(new[] { after, normal, high, before });
            Fire(state, PassiveEventKind.BeforeAction);
            var order = string.Join(",", state.PassiveTrace.Where(t => t.Fired && t.ActionNodeId == null).Select(t => t.RuleId));
            Check(order == "before,high,normal,after", "phase, descending priority and stable rule identity define dispatch order: " + order);
            passive.Reactions.Clear();
            var perTarget = Rule("target-limit", PassiveEventKind.DamageResolved, Increment("targets"));
            perTarget.LimitScope = PassiveLimitScope.RootActionTarget;
            passive.Reactions.Add(perTarget);
            Fire(state, PassiveEventKind.DamageResolved, "attack", "enemy.0");
            Fire(state, PassiveEventKind.DamageResolved, "attack", "enemy.0");
            Fire(state, PassiveEventKind.DamageResolved, "attack", "enemy.1");
            Check(Counter(state, "targets") == 2 && state.PassiveTrace.Any(t => t.RuleId == "target-limit" &&
                !t.Fired && t.Reason.Contains("Activation limit")), "multi-hit facts obey once per root and target with a readable skip");
            var perRoot = Rule("root-limit", PassiveEventKind.AfterAction, Increment("roots")); perRoot.LimitScope = PassiveLimitScope.RootAction;
            passive.Reactions.Add(perRoot);
            Fire(state, PassiveEventKind.AfterAction, "same"); Fire(state, PassiveEventKind.AfterAction, "same");
            Fire(state, PassiveEventKind.AfterAction, "different");
            Check(Counter(state, "roots") == 2, "root receipts suppress repeats and permit a new root");
            CharacterPassiveRuntime.EmitFact(state, new PassiveFact { Kind = PassiveEventKind.AfterAction, ActorId = "owner" });
            CharacterPassiveRuntime.EmitFact(state, new PassiveFact { Kind = PassiveEventKind.AfterAction, ActorId = "owner" });
            Check(Counter(state, "roots") == 4, "independent rootless lifecycle events receive distinct deterministic root scopes");
            var cooldown = Rule("cooldown", PassiveEventKind.TeamTurnStarted, Increment("cooldown")); cooldown.CooldownOwnerTurns = 2;
            passive.Reactions.Add(cooldown);
            Fire(state, PassiveEventKind.TeamTurnStarted, "turn0");
            state.Player.Fighters[0].OwnerTurnsCompleted = 1; Fire(state, PassiveEventKind.TeamTurnStarted, "turn1");
            state.Player.Fighters[0].OwnerTurnsCompleted = 2; Fire(state, PassiveEventKind.TeamTurnStarted, "turn2");
            Check(Counter(state, "cooldown") == 2, "cooldowns count completed owner turns independently of root ids");
            var origin = Rule("origin", PassiveEventKind.Healed, Increment("origin")); passive.Reactions.Add(origin);
            Fire(state, PassiveEventKind.Healed, reaction: true);
            Check(Counter(state, "origin") == 0 && state.PassiveTrace.Last().Reason.Contains("Reaction-origin"),
                "version-one reactions exclude reaction origin by default");
            origin.AllowReactionOrigin = true; Fire(state, PassiveEventKind.Healed, reaction: true);
            Check(Counter(state, "origin") == 1, "reaction-origin opt-in is explicit and executable");
            var legacy = Rule("legacy", PassiveEventKind.Healed, Increment("legacy")); legacy.ExecutionVersion = 0;
            passive.Reactions.Add(legacy); Fire(state, PassiveEventKind.Healed, reaction: true);
            Check(Counter(state, "legacy") == 1, "legacy version-zero origin behavior is preserved");
            Check(state.Clone().PassiveActivations.Count == state.PassiveActivations.Count &&
                SnapshotJson.Deserialize<BattleState>(SnapshotJson.Serialize(state)).PassiveActivations.Count == state.PassiveActivations.Count,
                "activation receipts survive clone and saved-snapshot reload");
        }

        private static BattleState PlayOne(BattleState battle, string request)
        {
            var card = battle.Team(battle.ActingSide).Hand.First(c => c.Kind == CardKind.Skill);
            var target = battle.OtherTeam(battle.ActingSide).LivingActive()[0];
            var plan = new TurnPlan { RequestId = request, ExpectedRevision = battle.Revision,
                Actions = new List<PlannedAction> { new PlannedAction { CardId = card.Id, TargetFighterId = target.Id } } };
            if (!BattleEngine.TryResolvePlan(battle, plan, out var next, out var error))
                throw new InvalidOperationException("Synthetic production action failed: " + error);
            return next;
        }

        private static void RealProtectionAndLayerBurst()
        {
            var attacker = Definition("attacker"); attacker.Passive = null;
            var protector = Definition("protector");
            protector.Passive.Reactions.Add(Rule("opening-resource", PassiveEventKind.BattleStarted, Increment("protection")));
            // BattleStarted has no actor, so use the explicit any relation for the opening grant.
            protector.Passive.Reactions[0].ActorRelation = PassiveRelation.Any;
            var protect = Rule("protection", PassiveEventKind.DamageResolved);
            protect.ActorRelation = PassiveRelation.Enemies; protect.TargetRelation = PassiveRelation.Self;
            protect.CardOriginOnly = true; protect.LimitScope = PassiveLimitScope.RootActionTarget;
            protect.Actions.Add(new PassiveActionDefinition { Id = "protection-spend", Kind = PassiveActionKind.SpendResource,
                ResourceKey = "protection", Amount = 1, Children = new List<PassiveActionDefinition> {
                    Command("cleanse", new PassiveCommandDefinition { Kind = PassiveCommandKind.ExecuteEffect,
                        Effect = new CardEffectOperationDefinition { Id = "cleanse", Kind = CardEffectOperationKind.Cleanse,
                            Target = EffectTargetScope.StatusOwner } }),
                    Command("recover", new PassiveCommandDefinition { Kind = PassiveCommandKind.ExecuteEffect,
                        Effect = new CardEffectOperationDefinition { Id = "recover", Kind = CardEffectOperationKind.Heal,
                            Target = EffectTargetScope.StatusOwner, Value = new EffectValueDefinition { FixedAmount = 200 } } }),
                    Command("mark-attacker", new PassiveCommandDefinition { Kind = PassiveCommandKind.ExecuteEffect,
                        Effect = new CardEffectOperationDefinition { Id = "mark-attacker", Kind = CardEffectOperationKind.ApplyStatus,
                            Target = EffectTargetScope.TriggerActor, Status = new StatusApplicationRecipe {
                                StatusRecipeId = "status.protection-mark", InlineRecipe = new StatusRecipeDefinition {
                                    Id = "status.protection-mark", Polarity = StatusPolarity.Debuff,
                                    DurationClock = StatusDurationClock.Permanent } } } }) } });
            protector.Passive.Reactions.Add(protect);
            var battle = BattleEngine.Create("protection", new[] { attacker }, null, new[] { protector }, null, 712,
                capturePassiveTrace: true);
            var defender = battle.Opponent.Fighters[0]; defender.Health = 600;
            defender.Statuses.Apply(new StatusApplyRequest { InstanceId = "recovery-lock", TargetFighterId = defender.Id,
                SourceFighterId = battle.Player.Fighters[0].Id, Recipe = new StatusRecipeDefinition {
                    Id = "status.recovery-lock", Polarity = StatusPolarity.Debuff, Behavior = StatusBehavior.PreventsRecovery,
                    DurationClock = StatusDurationClock.Permanent } });
            var next = PlayOne(battle, "protection-hit"); var nextDefender = next.Opponent.Fighters[0];
            Check(nextDefender.Health == 790 && CharacterPassiveRuntime.Counter(nextDefender, "protection") == 0,
                "real incoming damage spends exactly one protection charge and heals after cleansing recovery lock");
            Check(nextDefender.Statuses.Count(recipeId: "status.recovery-lock") == 0 &&
                next.Player.Fighters[0].Statuses.Count(recipeId: "status.protection-mark") == 1,
                "the production damage fact cleanses its defender and marks its attacker");
            var cleanseIndex = next.Events.FindIndex(e => e.Kind == BattleEventKind.StatusRemoved && e.TargetId == nextDefender.Id);
            var healIndex = next.Events.FindIndex(e => e.Kind == BattleEventKind.HealApplied && e.TargetId == nextDefender.Id);
            var markIndex = next.Events.FindIndex(e => e.Kind == BattleEventKind.StatusApplied && e.TargetId == next.Player.Fighters[0].Id);
            Check(cleanseIndex >= 0 && healIndex > cleanseIndex && markIndex > healIndex,
                "committed engine events preserve cleanse then heal then mark ordering");
            Check(next.PassiveTrace.Any(t => t.RuleId == "protection" && t.Fired) &&
                next.PassiveTrace.Any(t => t.ActionNodeId == "recover" && t.Fired),
                "production dispatch exposes the protection rule and its executed action nodes");

            var absorber = Definition("absorber"); var dummy = Definition("marked-target"); dummy.Passive = null;
            var absorb = Rule("absorb-mark", PassiveEventKind.AfterAction);
            var damage = new CardEffectOperationDefinition { Id = "per-layer-burst", Kind = CardEffectOperationKind.Damage,
                Target = EffectTargetScope.TriggerTarget, Damage = new DamageEffectRecipe {
                    Family = DamageFamily.Additional, Scaling = StatScaling.Fixed, FixedAmount = 1,
                    CannotCrit = true, CannotBlock = true } };
            absorb.Actions.Add(new PassiveActionDefinition { Id = "absorb", Kind = PassiveActionKind.ConsumeStatusLayers,
                Amount = 2, StatusRecipeId = "status.mark", RemovalReason = StatusRemovalReason.Absorbed,
                Children = new List<PassiveActionDefinition> { Command("burst", new PassiveCommandDefinition {
                    Kind = PassiveCommandKind.ExecuteEffect, ValueSource = PassiveValueSource.RemovedStatusLayers,
                    Amount = 25, EffectBinding = PassiveEffectBindingField.DamageFixedAmount, Effect = damage }) } });
            absorber.Passive.Reactions.Add(absorb);
            var absorption = BattleEngine.Create("absorption", new[] { absorber }, null, new[] { dummy }, null, 713,
                capturePassiveTrace: true);
            ApplyLayers(absorption.Opponent.Fighters[0], absorption.Player.Fighters[0].Id, 3, 1);
            var absorbed = PlayOne(absorption, "absorb-hit");
            Check(absorbed.Opponent.Fighters[0].Health == 940 && absorbed.Opponent.Fighters[0].Statuses.Count(recipeId: "status.mark") == 1,
                "a real card action consumes owned layers then deals computed removal-snapshot damage");
            Check(damage.Damage.FixedAmount == 1 && absorbed.Events.Any(e => e.Kind == BattleEventKind.DamageApplied && e.Amount == 50),
                "computed burst value is committed as damage while authored damage remains unchanged");
        }

        private static void ThresholdTransitionsAndBeforeDamage()
        {
            var state = Fixture(); var owner = state.Player.Fighters[0]; var passive = owner.Definition.Passive;
            var entry = Rule("threshold-entry", PassiveEventKind.ResourceChanged, Increment("entered"));
            entry.EventResourceKey = "threshold"; entry.Conditions.Add(new EffectConditionDefinition {
                Kind = EffectConditionKind.ResourceEnteredAtLeast, StringValue = "threshold", Threshold = 3 });
            var exit = Rule("threshold-exit", PassiveEventKind.ResourceChanged, Increment("exited"));
            exit.EventResourceKey = "threshold"; exit.Conditions.Add(new EffectConditionDefinition {
                Kind = EffectConditionKind.ResourceExitedBelow, StringValue = "threshold", Threshold = 3 });
            passive.Reactions.AddRange(new[] { entry, exit });
            CharacterPassiveRuntime.ChangeResource(state, owner, "threshold", 3);
            CharacterPassiveRuntime.ChangeResource(state, owner, "threshold", 1);
            CharacterPassiveRuntime.ChangeResource(state, owner, "threshold", -2);
            CharacterPassiveRuntime.ChangeResource(state, owner, "threshold", 3);
            Check(Counter(state, "entered") == 2 && Counter(state, "exited") == 1,
                "threshold entry and exit react to committed before/after values without retriggering while inside");
            Check(state.PassiveTrace.Any(t => t.RuleId == "threshold-entry" && !t.Fired &&
                t.Conditions.Any(c => c.HasObservedValue && c.ObservedValue == 4)),
                "a held threshold exposes its actual value and skipped condition trace");
            passive.Reactions.Clear();
            var modifier = Rule("once-per-hit-root", PassiveEventKind.BeforeDamage, new PassiveCommandDefinition {
                Kind = PassiveCommandKind.IncreaseCurrentAttackPercent, Amount = 10000 });
            modifier.LimitScope = PassiveLimitScope.RootAction; passive.Reactions.Add(modifier);
            using (CharacterPassiveRuntime.BeginRootResolution(state, "multi-hit"))
            {
                var first = AttackEffectSystem.Prepare(null, state, owner, state.Opponent.Fighters[0], DamageFamily.Normal);
                var second = AttackEffectSystem.Prepare(null, state, owner, state.Opponent.Fighters[0], DamageFamily.Normal);
                Check(first.Attacker.Attack == 200 && second.Attacker.Attack == 100 && owner.Definition.BaseStats.Attack == 100,
                    "synchronous BeforeDamage honors v1 root receipts without changing source stats");
            }
            Check(state.PassiveTrace.Any(t => t.RuleId == modifier.Id && !t.Fired && t.Reason.Contains("Activation limit")),
                "local-hit modifier interception supplies the same readable execution-limit trace");
        }

        private static void NewLifecycleHooksPreserveLegacyOptIn()
        {
            var state = Fixture(); var owner = state.Player.Fighters[0];
            foreach (var kind in new[] { PassiveEventKind.BeforeAction, PassiveEventKind.Healed,
                PassiveEventKind.StatusApplied, PassiveEventKind.StatusRemoved, PassiveEventKind.ReserveEntered })
            {
                var legacy = Rule("legacy-hook." + kind, kind, Increment("legacy-hook." + kind));
                legacy.ExecutionVersion = 0;
                var current = Rule("current-hook." + kind, kind, Increment("current-hook." + kind));
                owner.Definition.Passive.Reactions.AddRange(new[] { legacy, current });
            }
            CharacterPassiveRuntime.NotifyAction(state, PassiveEventKind.BeforeAction, owner,
                state.Opponent.Fighters[0], "actual-action-hook");
            Check(Counter(state, "legacy-hook.BeforeAction") == 0 && Counter(state, "current-hook.BeforeAction") == 1,
                "new production action hook executes opted-in v1 rules and preserves dormant legacy declarations");
            CharacterPassiveRuntime.NotifyHealed(state, owner, state.Player.Fighters[1], 25, "actual-heal-hook");
            Check(Counter(state, "legacy-hook.Healed") == 0 && Counter(state, "current-hook.Healed") == 1,
                "new production healing hook executes opted-in v1 rules and preserves dormant legacy declarations");
            var status = ApplyLayers(owner, owner.Id, 1, 0);
            CharacterPassiveRuntime.NotifyStatusApplied(state, owner, owner, status);
            var removed = owner.Statuses.ConsumeLayers("status.mark", 1, owner.Id);
            CharacterPassiveRuntime.NotifyStatusRemoved(state, owner, owner, removed);
            CharacterPassiveRuntime.NotifyReserveEntered(state, owner);
            Check(new[] { PassiveEventKind.StatusApplied, PassiveEventKind.StatusRemoved, PassiveEventKind.ReserveEntered }
                .All(kind => Counter(state, "legacy-hook." + kind) == 0 && Counter(state, "current-hook." + kind) == 1),
                "status and reserve production hooks preserve the same explicit version opt-in contract");
            Check(state.PassiveTrace.Any(t => t.RuleId == "legacy-hook.Healed" && !t.Fired &&
                t.Reason.Contains("legacy declaration remains inactive")), "dormant lifecycle declaration has an explicit compatibility skip trace");
            Fire(state, PassiveEventKind.BeforeAction); Fire(state, PassiveEventKind.Healed);
            Check(Counter(state, "legacy-hook.BeforeAction") == 1 && Counter(state, "legacy-hook.Healed") == 1 &&
                Counter(state, "current-hook.BeforeAction") == 2 && Counter(state, "current-hook.Healed") == 2,
                "explicit direct fact emission keeps its existing manual legacy-compatible behavior");
            var fact = new PassiveFact { Kind = PassiveEventKind.Healed, VersionedLifecycleOnly = true };
            Check(fact.Clone().VersionedLifecycleOnly &&
                SnapshotJson.Deserialize<PassiveFact>(SnapshotJson.Serialize(fact)).VersionedLifecycleOnly,
                "lifecycle opt-in provenance survives fact cloning and snapshot serialization");
        }

        private static StatusInstance ApplyLayers(FighterState target, string source, int layers, long order,
            List<PassiveReactionDefinition> reactions = null)
        {
            return target.Statuses.Apply(new StatusApplyRequest { InstanceId = source + ".mark." + order,
                SourceFighterId = source, TargetFighterId = target.Id, AppliedOrder = order, StackCount = layers,
                Recipe = new StatusRecipeDefinition { Id = "status.mark", Polarity = StatusPolarity.Debuff,
                    Identity = StatusIdentityScope.RecipeAndSource, DurationClock = StatusDurationClock.Permanent,
                    Stacking = StatusStackingPolicy.AddStacks, MaxStacks = 9,
                    Reactions = reactions ?? new List<PassiveReactionDefinition>() } }).Instance;
        }

        private static void StatusOwnershipSnapshotsAndBindings()
        {
            var state = Fixture(); var owner = state.Player.Fighters[0]; var target = state.Opponent.Fighters[0];
            var own = ApplyLayers(target, owner.Id, 3, 2); var foreign = ApplyLayers(target, "ally", 2, 1);
            var original = SnapshotJson.Serialize(target.Statuses);
            Check(target.Statuses.ConsumeLayers("status.mark", 4, owner.Id).Count == 0 &&
                SnapshotJson.Serialize(target.Statuses) == original, "insufficient exact layers are rejected without partial mutation");
            var removed = target.Statuses.ConsumeLayers("status.mark", 2, owner.Id, reason: StatusRemovalReason.Absorbed);
            Check(removed.Count == 1 && removed[0].RemovedLayers == 2 && removed[0].RemainingLayers == 1 &&
                own.StackCount == 1 && foreign.StackCount == 2, "partial consumption respects source ownership and retains other layers");
            own.StackCount = 7;
            Check(removed[0].Status.StackCount == 3 && removed[0].Reason == StatusRemovalReason.Absorbed,
                "removal snapshot freezes pre-removal state and explicit reason");
            own.StackCount = 3;
            var capture = Rule("removed-snapshot", PassiveEventKind.StatusRemoved, new PassiveCommandDefinition {
                Kind = PassiveCommandKind.IncrementCounter, CounterKey = "detonation-layers", Amount = 10,
                ValueSource = PassiveValueSource.RemovedStatusLayers });
            capture.ActorRelation = PassiveRelation.Any; capture.AllowReactionOrigin = true;
            capture.FilterRemovalReason = true; capture.RemovalReason = StatusRemovalReason.Absorbed;
            owner.Definition.Passive.Reactions.Add(capture);
            CharacterPassiveRuntime.NotifyStatusRemoved(state, owner, target, removed);
            Check(Counter(state, "detonation-layers") == 20, "removal observers use the frozen removed-layer amount");
            var consume = new PassiveActionDefinition { Id = "consume", Kind = PassiveActionKind.ConsumeStatusLayers,
                StatusRecipeId = "status.mark", Amount = 2, ResultBinding = "consumed", RemovalReason = StatusRemovalReason.Absorbed,
                Children = new List<PassiveActionDefinition> {
                    Command("bound", new PassiveCommandDefinition { Kind = PassiveCommandKind.SetCounter,
                        CounterKey = "bound-layers", ValueSource = PassiveValueSource.Binding, ValueBinding = "consumed" }),
                    Command("heal", new PassiveCommandDefinition { Kind = PassiveCommandKind.ExecuteEffect,
                        ValueSource = PassiveValueSource.RemovedStatusLayers, Amount = 10,
                        EffectBinding = PassiveEffectBindingField.HealFixedAmount,
                        Effect = new CardEffectOperationDefinition { Id = "layer-reward", Kind = CardEffectOperationKind.Heal,
                            Window = CardEffectWindow.AfterAction, Target = EffectTargetScope.StatusOwner } }) } };
            var consumeRule = Rule("consume-action", PassiveEventKind.AfterAction); consumeRule.Actions.Add(consume);
            owner.Definition.Passive.Reactions.Add(consumeRule); owner.Health = 500;
            var authored = SnapshotJson.Serialize(consumeRule);
            Fire(state, PassiveEventKind.AfterAction);
            Check(Counter(state, "bound-layers") == 2 && owner.Health == 520,
                "typed result binding and computed healing execute inline inside the consumption snapshot");
            Check(SnapshotJson.Serialize(consumeRule) == authored,
                "computed effect fields never mutate their authored command or operation");
            Check(Counter(state, "detonation-layers") == 40 && target.Statuses.Instances.Any(s => s.SourceFighterId == "ally" && s.StackCount == 2),
                "nested removal fact fires once and leaves foreign ownership untouched");

            var statusObserver = Rule("status-owned", PassiveEventKind.StatusRemoved, Increment("own-removal"));
            statusObserver.ActorRelation = PassiveRelation.Any; statusObserver.AllowReactionOrigin = true;
            statusObserver.LimitScope = PassiveLimitScope.Battle;
            target.Statuses.Instances.Clear(); ApplyLayers(target, "source-a", 1, 1, new List<PassiveReactionDefinition> { statusObserver });
            var first = target.Statuses.ConsumeLayers("status.mark", 1, "source-a");
            CharacterPassiveRuntime.NotifyStatusRemoved(state, owner, target, first);
            ApplyLayers(target, "source-b", 1, 2, new List<PassiveReactionDefinition> { statusObserver.Clone() });
            var second = target.Statuses.ConsumeLayers("status.mark", 1, "source-b");
            CharacterPassiveRuntime.NotifyStatusRemoved(state, owner, target, second);
            Check(CharacterPassiveRuntime.Counter(target, "own-removal") == 2,
                "a removed status can react from its frozen recipe and different instances have independent receipts");
        }

        private static void SynchronousInterception()
        {
            var state = Fixture(); var defender = state.Opponent.Fighters[0];
            var ally = state.Player.Fighters[1]; ally.Shield = 100;
            ally.Statuses.Instances.Add(new StatusInstance { InstanceId = "source-dependent", RecipeId = "dependent",
                SourceFighterId = defender.Id, TargetFighterId = ally.Id, ShieldRemaining = 40,
                Recipe = new StatusRecipeDefinition { Id = "dependent", DurationClock = StatusDurationClock.Permanent,
                    BreakRule = StatusBreakRule.OnSourceDefeated } });
            ally.Statuses.Instances.Add(new StatusInstance { InstanceId = "dependent-child", ParentInstanceId = "source-dependent",
                RecipeId = "child", SourceFighterId = defender.Id, TargetFighterId = ally.Id, ShieldRemaining = 10,
                Recipe = new StatusRecipeDefinition { Id = "child", DurationClock = StatusDurationClock.Permanent } });
            var save = Rule("fatal-intercept", PassiveEventKind.BeforeDefeat, new PassiveCommandDefinition {
                Kind = PassiveCommandKind.ExecuteEffect, Effect = new CardEffectOperationDefinition { Id = "save-heal",
                    Kind = CardEffectOperationKind.Heal, Target = EffectTargetScope.StatusOwner,
                    Value = new EffectValueDefinition { FixedAmount = 250 } } });
            save.ActorRelation = PassiveRelation.Any; save.TargetRelation = PassiveRelation.Self; save.AllowReactionOrigin = true;
            save.LimitScope = PassiveLimitScope.Battle; defender.Definition.Passive.Reactions.Add(save);
            var attack = Rule("reaction-damage", PassiveEventKind.BeforeAction, new PassiveCommandDefinition {
                Kind = PassiveCommandKind.ExecuteEffect, Effect = new CardEffectOperationDefinition { Id = "lethal",
                    Kind = CardEffectOperationKind.Damage, Target = EffectTargetScope.TriggerTarget,
                    Damage = new DamageEffectRecipe { Family = DamageFamily.Additional, Scaling = StatScaling.Fixed,
                        FixedAmount = 2000, CannotCrit = true, CannotBlock = true } } });
            state.Player.Fighters[0].Definition.Passive.Reactions.Add(attack);
            Fire(state, PassiveEventKind.BeforeAction);
            Check(defender.IsAlive && defender.Health == 250 && state.PassiveTrace.Any(t => t.RuleId == "fatal-intercept" && t.Fired),
                "nested lethal reaction executes synchronous BeforeDefeat before the engine marks the target defeated");
            Check(ally.Statuses.Instances.Count == 2 && ally.Shield == 100,
                "successful fatal interception does not break source-dependent statuses");
            Fire(state, PassiveEventKind.BeforeAction, "second-lethal");
            Check(!defender.IsAlive && defender.Health == 0 && state.PassiveTrace.Any(t => t.RuleId == "fatal-intercept" &&
                !t.Fired && t.Reason.Contains("Activation limit")), "fatal save receipt is consumed once per battle");
            Check(ally.Statuses.Instances.Count == 0 && ally.Shield == 50 && state.Events.Any(e =>
                e.Kind == BattleEventKind.StatusRemoved && e.TargetId == ally.Id && e.StatusesAfter.Count == 0),
                "actual source defeat removes matching statuses and orphans and only their remaining barriers");
        }

        private static void SharedBudgetAndFailedTransaction()
        {
            var state = Fixture(); var failed = false;
            try
            {
                using (CharacterPassiveRuntime.BeginRootResolution(state, "shared"))
                    for (var i = 0; i < 257; i++) Fire(state, PassiveEventKind.AfterAction, "sub-action-" + i);
            }
            catch (PassiveExecutionException budgetError) { failed = budgetError.Message.Contains("256 passive facts"); }
            Check(failed, "all nested facts inside a root lease share one deterministic budget");
            Fire(state, PassiveEventKind.AfterAction, "fresh-root");
            Check(state.PassiveEventSequence == 257, "a disposed failed lease releases its session for a fresh resolution");

            var definition = Definition("budget-owner");
            definition.Passive.Reactions.Add(Rule("seed", PassiveEventKind.BeforeAction, Increment("feedback")));
            var loop = Rule("feedback-loop", PassiveEventKind.ResourceChanged, Increment("feedback"));
            loop.AllowReactionOrigin = true; loop.EventResourceKey = "feedback"; definition.Passive.Reactions.Add(loop);
            var enemy = Definition("budget-enemy"); enemy.Passive = null;
            var battle = BattleEngine.Create("transaction", new[] { definition }, null, new[] { enemy }, null, 998,
                capturePassiveTrace: true);
            var before = SnapshotJson.Serialize(battle);
            var card = battle.Player.Hand.First(c => c.Kind == CardKind.Skill);
            var plan = new TurnPlan { RequestId = "budget-action", ExpectedRevision = battle.Revision,
                Actions = new List<PlannedAction> { new PlannedAction { CardId = card.Id,
                    TargetFighterId = battle.Opponent.Fighters[0].Id } } };
            var success = BattleEngine.TryResolvePlan(battle, plan, out var next, out var error);
            Check(!success && next == null && error.Contains("deterministic budget"),
                "production transaction returns a readable bounded failure instead of a partial result");
            Check(SnapshotJson.Serialize(battle) == before, "failed production resolution preserves the authoritative source snapshot byte for byte");
        }

        private static void ValidationAndSerialization()
        {
            var rule = Rule("composed", PassiveEventKind.AfterAction); rule.Actions.Add(Command("gain", Increment("gain")));
            var passive = new PassiveDefinition { Id = "validation", Reactions = new List<PassiveReactionDefinition> { rule } };
            Check(PassiveRuleValidator.Validate(passive).Count == 0, "action-only version-one rule validates");
            var clone = SnapshotJson.Deserialize<PassiveDefinition>(SnapshotJson.Serialize(passive));
            Check(clone.Reactions[0].Actions[0].Commands[0].CounterKey == "gain" &&
                clone.Clone().Reactions[0].Actions[0].Id == "gain", "nested compiled composition survives JSON and definition cloning");
            rule.ExecutionVersion = 0;
            Check(PassiveRuleValidator.Validate(passive).Any(e => e.Contains("version one")), "legacy execution cannot silently accept new composition");
            rule.ExecutionVersion = 1; rule.Actions[0].Children.Add(rule.Actions[0]);
            Check(PassiveRuleValidator.Validate(passive).Any(e => e.Contains("cycle")), "runtime validation rejects authored action cycles");
            rule.Actions[0].Children.Clear(); rule.Actions[0].Kind = PassiveActionKind.ForEachTarget; rule.Actions[0].MaximumTargets = 33;
            Check(PassiveRuleValidator.Validate(passive).Any(e => e.Contains("bound")), "unbounded target iterations are rejected before execution");
            rule.Actions.Clear(); rule.Commands.Add(new PassiveCommandDefinition { Kind = PassiveCommandKind.ExecuteEffect,
                EffectBinding = PassiveEffectBindingField.DamageFixedAmount,
                Effect = new CardEffectOperationDefinition { Kind = CardEffectOperationKind.Heal } });
            Check(PassiveRuleValidator.Validate(passive).Any(e => e.Contains("damage value binding")),
                "computed fields validate their operation type");
        }
    }
}
