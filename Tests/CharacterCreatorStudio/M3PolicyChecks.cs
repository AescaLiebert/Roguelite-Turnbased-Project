using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;

namespace CharacterCreatorStudio.Checks
{
    /// <summary>Synthetic shared-rule fixtures. No case-study fighter or production asset is installed.</summary>
    public static class M3PolicyChecks
    {
        private static int checks;
        private static void Check(bool value, string reason)
        { if (!value) throw new InvalidOperationException("M3 policies: " + reason); checks++; }

        public static int Run()
        {
            checks = 0;
            DamageProfiles();
            DirectCapAndFatalSaves();
            GeneratedContacts();
            DamageRemovalFacts();
            GaugeGuard();
            QueriesAndHistory();
            Console.WriteLine("PASS: " + checks + " M3 query/history/interception checks.");
            return checks;
        }

        private static CharacterDefinition Fighter(string id, int health = 1000, int attack = 1000)
        {
            var result = new CharacterDefinition { Id = id, BaseStats = new StatBlock { MaxHealth = health,
                Attack = attack, RecoveryBp = 10000, CritDamageBp = 15000 }, Passive = new PassiveDefinition { Id = id + ".passive" } };
            for (var slot = 1; slot <= 2; slot++)
            {
                var skill = new SkillDefinition { Id = id + ".skill." + slot, Slot = slot, Category = CardCategory.Attack };
                for (var rank = 1; rank <= 3; rank++) skill.Ranks.Add(new SkillRankDefinition { Rank = rank,
                    Effect = new EffectDefinition { Scaling = StatScaling.Attack, CoefficientBp = 10000 } });
                result.Skills.Add(skill);
            }
            result.UltimateTiers.Add(new UltimateTierDefinition { Tier = 0,
                Effect = new EffectDefinition { Scaling = StatScaling.Attack, CoefficientBp = 10000 } });
            return result;
        }

        private static CombatPolicyDefinition Cap(int bp = 4900) => new CombatPolicyDefinition {
            Id = "policy.contact-cap", Kind = CombatPolicyKind.IncomingDamageCap, MaximumHealthDamageBp = bp };
        private static CombatPolicyDefinition Save(bool bypassable = true) => new CombatPolicyDefinition {
            Id = "policy.fatal-save", Kind = CombatPolicyKind.BeforeDefeat, ResourceKey = "save", ResourceCost = 3,
            HealMaxHealthBp = 5000, CleanseDebuffs = true, Bypassable = bypassable };

        private static BattleState Create(CharacterDefinition actor, CharacterDefinition target, ulong seed = 91)
            => BattleEngine.Create("studio.m3.synthetic-policies", new[] { actor }, null,
                new[] { target }, null, seed, cardDrawSeed: seed, capturePassiveTrace: true);

        private static BattleState Play(BattleState state, int targetIndex = 0)
        {
            var card = state.Player.Hand.First(c => c.Kind == CardKind.Skill && c.OwnerFighterId == state.Player.Fighters[0].Id);
            var plan = new TurnPlan { RequestId = "policies.play." + state.Revision, ExpectedRevision = state.Revision,
                Actions = new List<PlannedAction> { new PlannedAction { CardId = card.Id,
                    TargetFighterId = state.Opponent.Fighters[targetIndex].Id } } };
            Check(BattleEngine.TryResolvePlan(state, plan, out var next, out var error), "legal card input: " + error);
            return next;
        }

        private static BattleState Pass(BattleState state)
        {
            Check(BattleEngine.TryResolvePlan(state, new TurnPlan { RequestId = "policies.pass." + state.Revision,
                ExpectedRevision = state.Revision }, out var next, out var error), "legal empty turn: " + error);
            return next;
        }

        private static void DamageProfiles()
        {
            var attacker = new StatBlock { Attack = 100, CritChanceBp = 0 };
            var defender = new StatBlock { MaxHealth = 10000, Defense = 10, ResistanceBp = 1000 };
            DamageResult Resolve(DamagePolicy policy) => DamageResolver.Resolve(new DamagePacket {
                BaseAmount = 100, Policy = policy }, attacker, defender, 10000, 1000, -1, -1);
            var legacy = new DamagePolicy { Family = DamageFamily.True, BypassShield = true,
                IncomingDecreaseBp = 5000, FinalReductionBp = 5000, FlatReduction = 10 };
            Check(Resolve(legacy).CalculatedDamage == 130 && Resolve(legacy).ShieldLost == 0,
                "null profiles preserve legacy True +30%, defenses/final reduction bypass and shield behavior");
            var explicitPolicy = new DamagePolicy { Family = DamageFamily.True,
                IncomingDecreaseBp = 5000, FinalReductionBp = 5000, FlatReduction = 10 };
            var full = new DamageProfileDefinition { Id = "profile.true.explicit", ExplicitTrueDamage = true,
                TrueDamageBonusBp = 2000, BypassDefense = true, BypassResistance = true,
                BypassGenericReduction = true, BypassFinalReduction = true, BypassFlatReduction = true,
                BypassShield = true, BypassDamageCap = true };
            CombatPolicySystem.ApplyProfile(explicitPolicy, full);
            Check(Resolve(explicitPolicy).CalculatedDamage == 120 && Resolve(explicitPolicy).ShieldLost == 0,
                "explicit True profile uses authored +20% and explicit bypass stages");
            var finite = new DamagePolicy { Family = DamageFamily.True,
                BypassShield = true, BypassDefense = true, BypassResistance = true, BypassGenericReduction = true,
                IncomingDecreaseBp = 5000, FinalReductionBp = 5000, FlatReduction = 10 };
            CombatPolicySystem.ApplyProfile(finite, new DamageProfileDefinition { ExplicitTrueDamage = true,
                TrueDamageBonusBp = 0 });
            Check(Resolve(finite).CalculatedDamage == 12 && Resolve(finite).HealthLost == 0 && Resolve(finite).ShieldLost == 12,
                "explicit profile can opt into defense, generic/final/flat reduction and shields");
            var copy = explicitPolicy.Clone(); copy.AuthoredProfile.TrueDamageBonusBp = 9900;
            Check(explicitPolicy.AuthoredProfile.TrueDamageBonusBp == 2000, "runtime damage profile clone owns its authored payload");
        }

        private static void DirectCapAndFatalSaves()
        {
            var actor = Fighter("synthetic.actor");
            var target = Fighter("synthetic.target"); target.Passive.Policies.Add(Cap());
            var state = Create(actor, target);
            var next = Play(state); var victim = next.Opponent.Fighters[0];
            var contact = next.Events.First(e => e.Kind == BattleEventKind.DamageApplied);
            Check(victim.Health == 510 && contact.HealthLost == 490, "actual card contact caps HP loss at authored max-HP fraction");
            Check(next.PassiveTrace.Any(t => t.RuleId == "policy.contact-cap" && t.Fired), "cap interception produces readable runtime trace");
            Check(state.Opponent.Fighters[0].Health == 1000, "resolved plan leaves input snapshot untouched");
            state = Create(actor, target); state.Opponent.Fighters[0].Shield = 200;
            next = Play(state); contact = next.Events.First(e => e.Kind == BattleEventKind.DamageApplied);
            Check(contact.ShieldLost == 200 && contact.HealthLost == 490 && contact.Amount == 690,
                "incoming cap limits HP-directed contact after shield absorption");

            target = Fighter("synthetic.save-target"); target.Passive.Policies.Add(Save());
            state = Create(actor, target); state.Opponent.Fighters[0].PassiveCounters.Add(new PassiveCounterState { Key = "save", Value = 3 });
            StatusSystem.Apply(state.Opponent.Fighters[0], state.Player.Fighters[0].Id, TeamSide.Player,
                new StatusRecipeDefinition { Id = "synthetic.recovery-block", Polarity = StatusPolarity.Debuff,
                    Behavior = StatusBehavior.PreventsRecovery, DurationClock = StatusDurationClock.Permanent }, "block", "setup", 1);
            next = Play(state); victim = next.Opponent.Fighters[0];
            Check(victim.IsAlive && victim.Health == 500 && CharacterPassiveRuntime.Counter(victim, "save") == 0,
                "fatal policy atomically spends three resources and restores HP before defeat");
            Check(victim.Statuses.Instances.Count == 0 && !next.Events.Any(e => e.Kind == BattleEventKind.FighterDefeated),
                "fatal cleanse removes recovery prevention and saves without defeat side effects");
            Check(next.Events.Any(e => e.Kind == BattleEventKind.HealApplied && e.HealthAfter == 500), "saved HP is represented in committed playback facts");

            foreach (var skill in actor.Skills) foreach (var rank in skill.Ranks) rank.Effect.Family = DamageFamily.Destructive;
            state = Create(actor, target); state.Opponent.Fighters[0].PassiveCounters.Add(new PassiveCounterState { Key = "save", Value = 3 });
            next = Play(state);
            Check(next.Opponent.Fighters[0].IsAlive && next.Opponent.Fighters[0].Health == 500 &&
                !next.Events.Any(e => e.Kind == BattleEventKind.FighterDefeated),
                "fatal interception clears a resolved Destructive execution before pending defeat commits");
            foreach (var skill in actor.Skills) foreach (var rank in skill.Ranks) rank.Effect.Family = DamageFamily.Normal;

            state = Create(actor, target); state.Opponent.Fighters[0].PassiveCounters.Add(new PassiveCounterState { Key = "save", Value = 3 });
            StatusSystem.Apply(state.Opponent.Fighters[0], state.Player.Fighters[0].Id, TeamSide.Player,
                new StatusRecipeDefinition { Id = "synthetic.grey-recovery-block", Polarity = StatusPolarity.Debuff,
                    Color = StatusColor.Grey, Behavior = StatusBehavior.PreventsRecovery,
                    DurationClock = StatusDurationClock.Permanent }, "grey-block", "setup", 1);
            next = Play(state);
            Check(!next.Opponent.Fighters[0].IsAlive && CharacterPassiveRuntime.Counter(next.Opponent.Fighters[0], "save") == 3,
                "an unremovable recovery block fails the complete save preflight without spending");

            state = Create(actor, target); state.Opponent.Fighters[0].PassiveCounters.Add(new PassiveCounterState { Key = "save", Value = 2 });
            next = Play(state); victim = next.Opponent.Fighters[0];
            Check(!victim.IsAlive && CharacterPassiveRuntime.Counter(victim, "save") == 2,
                "insufficient fatal-save resource causes no partial cost or heal");
            Check(next.PassiveTrace.Any(t => t.RuleId == "policy.fatal-save" && !t.Fired && t.Reason.Contains("Insufficient")),
                "failed save reports its failed atomic guard");

            foreach (var skill in actor.Skills) foreach (var rank in skill.Ranks)
                rank.Effect.DamageProfile = new DamageProfileDefinition { BypassFatalSave = true };
            state = Create(actor, target); state.Opponent.Fighters[0].PassiveCounters.Add(new PassiveCounterState { Key = "save", Value = 3 });
            next = Play(state);
            Check(!next.Opponent.Fighters[0].IsAlive && CharacterPassiveRuntime.Counter(next.Opponent.Fighters[0], "save") == 3,
                "fatal bypass wins over bypassable save without paying its resource");
            target.Passive.Policies[0].Bypassable = false;
            state = Create(actor, target); state.Opponent.Fighters[0].PassiveCounters.Add(new PassiveCounterState { Key = "save", Value = 3 });
            next = Play(state);
            Check(next.Opponent.Fighters[0].IsAlive && next.Opponent.Fighters[0].Health == 500,
                "explicit unbypassable save policy wins over damage bypass");
            target.Passive.Policies.Clear(); target.Passive.Policies.Add(Cap());
            foreach (var skill in actor.Skills) foreach (var rank in skill.Ranks)
                rank.Effect.DamageProfile = new DamageProfileDefinition { BypassDamageCap = true };
            Check(!Play(Create(actor, target)).Opponent.Fighters[0].IsAlive, "explicit incoming-cap bypass is honored by actual card packets");
            target.Passive.Policies[0].Bypassable = false;
            Check(Play(Create(actor, target)).Opponent.Fighters[0].Health == 510, "unbypassable cap precedence is explicit");
        }

        private static void GeneratedContacts()
        {
            var actor = Fighter("synthetic.generator", 1000, 1000);
            var target = Fighter("synthetic.protected", 1000, 0); target.Passive.Policies.Add(Cap());
            actor.Passive.Reactions.Add(new PassiveReactionDefinition { Id = "synthetic.additional", Trigger = PassiveEventKind.TeamTurnStarted,
                ActorRelation = PassiveRelation.Any, OwnTeamTurnOnly = true, Commands = new List<PassiveCommandDefinition> {
                    new PassiveCommandDefinition { Kind = PassiveCommandKind.ExecuteEffect, Effect = new CardEffectOperationDefinition {
                        Id = "additional", Kind = CardEffectOperationKind.Damage, Target = EffectTargetScope.AllEnemies,
                        Damage = new DamageEffectRecipe { Family = DamageFamily.Additional, Scaling = StatScaling.Fixed, FixedAmount = 1000 } } } } });
            var state = Create(actor, target);
            Check(state.Opponent.Fighters[0].Health == 510 && state.Events.Any(e => e.Kind == BattleEventKind.DamageApplied && e.HealthLost == 490),
                "passive additional contacts pass through incoming cap despite legacy family global-cap bypass");
            actor.Passive.Reactions.Clear();
            state = Create(actor, target); var owner = state.Player.Fighters[0]; var enemy = state.Opponent.Fighters[0];
            owner.Definition.Passive.Policies.Add(Cap());
            StatusSystem.Apply(owner, enemy.Id, enemy.Side, new StatusRecipeDefinition { Id = "synthetic.dot", Polarity = StatusPolarity.Debuff,
                Behavior = StatusBehavior.DamageOverTime, DurationClock = StatusDurationClock.Permanent,
                PeriodicDamage = new PeriodicDamageDefinition { Timing = StatusTickTiming.TargetTurnEnd, Scaling = StatusSnapshotScaling.Fixed,
                    FixedAmount = 1000, Family = DamageFamily.DamageOverTime } }, "dot", "setup", 1);
            var next = Pass(state);
            Check(next.Player.Fighters[0].Health == 510 && next.Events.Any(e => e.DamageOrigin == ElementalDamageOrigin.DamageOverTime && e.HealthLost == 490),
                "periodic DOT contacts share incoming cap lifecycle");

            actor.Passive.Policies.Add(Cap()); target.BaseStats.MaxHealth = 100000; target.BaseStats.ReflectDamageBp = 10000;
            next = Play(Create(actor, target));
            Check(next.Player.Fighters[0].Health == 510 && next.Events.Any(e => e.DamageOrigin == ElementalDamageOrigin.Reflection && e.HealthLost == 490),
                "reflection contacts share incoming cap lifecycle");

            target.BaseStats.ReflectDamageBp = 0;
            state = Create(actor, target); owner = state.Player.Fighters[0]; enemy = state.Opponent.Fighters[0];
            StatusSystem.Apply(owner, enemy.Id, enemy.Side, new StatusRecipeDefinition { Id = "synthetic.expiry", Polarity = StatusPolarity.Debuff,
                DurationClock = StatusDurationClock.TargetTurnEnd, DefaultDuration = 1,
                NaturalExpiryAttack = new TriggeredElementalAttackDefinition { Element = ElementId.Pyro, AttackCoefficientBp = 10000 } },
                "expiry", "setup", 1, new StatusSnapshot { SourceAttack = 1000 });
            next = Pass(state);
            Check(next.Player.Fighters[0].Health == 510 && next.Events.Any(e => e.DamageOrigin == ElementalDamageOrigin.TriggeredAttack && e.HealthLost == 490),
                "generated natural-expiry attacks share incoming cap lifecycle");
            ElementalContact(ElementId.Pyro, ElementId.Electro, "Overload");
            ElementalContact(ElementId.Anemo, ElementId.Pyro, "Swirl");
            ElementalContact(ElementId.Electro, ElementId.Cryo, "Superconduct");
        }

        private static void ElementalContact(ElementId attackElement, ElementId fuel, string expected)
        {
            var actor = Fighter("synthetic.elemental"); actor.TraitIds.Add(ElementalRuleSet.GenshinTrait);
            foreach (var skill in actor.Skills) foreach (var rank in skill.Ranks) rank.Effect.DamageElement = attackElement;
            var targets = Enumerable.Range(0, 3).Select(i => Fighter("synthetic.recipient." + i)).ToArray();
            foreach (var target in targets) { target.TraitIds.Add(ElementalRuleSet.GenshinTrait); target.Passive.Policies.Add(Cap(100)); }
            var state = BattleEngine.Create("studio.m3.synthetic-reactions", new[] { actor }, null, targets, null, 91,
                cardDrawSeed: 91, capturePassiveTrace: true);
            state.Opponent.Fighters[1].ElementStatuses.Apply(fuel, state.Player.Fighters[0].Id, "fuel");
            var next = Play(state, 1);
            var contacts = next.Events.Where(e => e.DamageOrigin == ElementalDamageOrigin.Reaction && e.Kind == BattleEventKind.DamageApplied).ToList();
            Check(contacts.Count > 0 && contacts.Any(e => e.Message == expected), expected + " produces real engine generated contacts");
            Check(contacts.All(e => e.HealthLost <= 10), expected + " terminal contacts cannot bypass authored incoming cap");
        }

        private static void GaugeGuard()
        {
            var target = Fighter("synthetic.guarded"); target.Passive.Policies.Add(new CombatPolicyDefinition {
                Id = "policy.full-gauge", Kind = CombatPolicyKind.GaugeLossGuard,
                Conditions = new List<EffectConditionDefinition> { new EffectConditionDefinition { Kind = EffectConditionKind.TargetGaugeAtLeast,
                    Subject = ConditionSubject.Owner, Threshold = 5 } } });
            var state = Create(Fighter("synthetic.drainer"), target); var victim = state.Opponent.Fighters[0];
            victim.PowerGauge = 5; victim.PowerGaugeRemainderBp = 0;
            var drawCount = state.RngDrawCount;
            Check(CharacterPassiveRuntime.ChangePowerGauge(state, state.Player.Fighters[0], victim, -5) == 0 && victim.PowerGauge == 5,
                "conditional gauge guard prevents the requested loss before mutation");
            Check(state.PassiveTrace.Any(t => t.RuleId == "policy.full-gauge" && t.Fired), "gauge interception traces its prevented loss");
            victim.PowerGauge = 4;
            Check(CharacterPassiveRuntime.ChangePowerGauge(state, state.Player.Fighters[0], victim, -2) == -2 && victim.PowerGauge == 2,
                "gauge guard stops applying when its authored condition becomes false");
            Check(CharacterPassiveRuntime.ChangePowerGauge(state, victim, victim, 1) == 1 && victim.PowerGauge == 3,
                "gauge guards do not block positive gains");
            Check(state.RngDrawCount == drawCount, "policy gates and guards consume no random draws");
        }

        private static void DamageRemovalFacts()
        {
            foreach (var recipeId in new[] { "synthetic.on-damage", "synthetic.barrier", StatusSystem.FreezeRecipeId })
            {
                var actor = Fighter("synthetic.breaker", 100000, 100);
                var target = Fighter("synthetic.status-owner", 100000, 0);
                target.Passive.Reactions.Add(new PassiveReactionDefinition { Id = "observe-broken", ExecutionVersion = 1,
                    Trigger = PassiveEventKind.StatusRemoved, ActorRelation = PassiveRelation.Any, TargetRelation = PassiveRelation.Self,
                    FilterRemovalReason = true, RemovalReason = StatusRemovalReason.Broken, EventStatusRecipeId = recipeId,
                    AllowReactionOrigin = true, Commands = new List<PassiveCommandDefinition> { new PassiveCommandDefinition {
                        Kind = PassiveCommandKind.IncrementCounter, CounterKey = "broken", Amount = 1 } } });
                var state = Create(actor, target); var victim = state.Opponent.Fighters[0];
                var recipe = new StatusRecipeDefinition { Id = recipeId, DurationClock = StatusDurationClock.Permanent,
                    Behavior = recipeId == "synthetic.barrier" ? StatusBehavior.Barrier : StatusBehavior.Stat,
                    BreakRule = recipeId == "synthetic.on-damage" ? StatusBreakRule.OnDamageTaken : StatusBreakRule.None };
                var applied = StatusSystem.Apply(victim, state.Player.Fighters[0].Id, TeamSide.Player, recipe,
                    "damage-removal", "setup", 1);
                Check(applied.Accepted, "damage-break fixture status applies: " + recipeId);
                if (recipeId == "synthetic.barrier") { applied.Instance.ShieldRemaining = 20; victim.Shield = 20; }
                var next = Play(state); victim = next.Opponent.Fighters[0];
                Check(!victim.Statuses.Instances.Any(s => s.RecipeId == recipeId) && CharacterPassiveRuntime.Counter(victim, "broken") == 1,
                    "damage removal emits exactly one Broken snapshot to matching rules: " + recipeId);
            }

            var dotOwner = Fighter("synthetic.dot-observer", 100000, 0);
            dotOwner.Passive.Reactions.Add(new PassiveReactionDefinition { Id = "observe-consumed", ExecutionVersion = 1,
                Trigger = PassiveEventKind.StatusRemoved, ActorRelation = PassiveRelation.Any, TargetRelation = PassiveRelation.Self,
                FilterRemovalReason = true, RemovalReason = StatusRemovalReason.Consumed, EventStatusRecipeId = "synthetic.single-tick",
                AllowReactionOrigin = true, Commands = new List<PassiveCommandDefinition> { new PassiveCommandDefinition {
                    Kind = PassiveCommandKind.IncrementCounter, CounterKey = "consumed", Amount = 1 } } });
            var dotState = Create(dotOwner, Fighter("synthetic.dot-source", 100000, 0));
            StatusSystem.Apply(dotState.Player.Fighters[0], dotState.Opponent.Fighters[0].Id, TeamSide.Opponent,
                new StatusRecipeDefinition { Id = "synthetic.single-tick", Behavior = StatusBehavior.DamageOverTime,
                    DurationClock = StatusDurationClock.Permanent, PeriodicDamage = new PeriodicDamageDefinition {
                        Scaling = StatusSnapshotScaling.Fixed, FixedAmount = 10, Timing = StatusTickTiming.TargetTurnEnd,
                        ConsumeOnTrigger = true } }, "single-tick", "setup", 1);
            var consumed = Pass(dotState);
            Check(CharacterPassiveRuntime.Counter(consumed.Player.Fighters[0], "consumed") == 1,
                "consume-on-trigger periodic status emits a Consumed snapshot exactly once");
        }

        private static void QueriesAndHistory()
        {
            var definitions = Enumerable.Range(0, 4).Select(i => Fighter("synthetic.query." + i)).ToArray();
            var state = BattleEngine.Create("studio.m3.synthetic-query", new[] { Fighter("synthetic.query-owner") }, null,
                definitions, null, 107, capturePassiveTrace: true);
            var owner = state.Player.Fighters[0]; var eventTarget = state.Opponent.Fighters[1];
            var context = new CardEffectContext { Battle = state, Actor = owner, EffectOwner = owner, SelectedTarget = eventTarget };
            var enemyContext = new CardEffectContext { Battle = state, Actor = eventTarget, EffectOwner = owner, SelectedTarget = owner };
            var ownerEnemies = TargetQuerySystem.Resolve(new TargetQueryDefinition { Anchor = TargetQueryAnchor.Owner }, enemyContext);
            Check(ownerEnemies.Count == 3 && ownerEnemies.All(f => f.Side != owner.Side),
                "owner anchor selects the owner's enemies when a hostile actor triggered the rule");
            Check(TargetQuerySystem.Resolve(new TargetQueryDefinition { BaseScope = EffectTargetScope.Self,
                Anchor = TargetQueryAnchor.EventTarget }, enemyContext).Single().Id == owner.Id,
                "event-target anchor provides an explicit selection coordinate");
            var query = new TargetQueryDefinition { BaseScope = EffectTargetScope.AllEnemies, ExcludeEventTarget = true,
                AdjacentToEventTarget = true, StoreBinding = "neighbors" };
            var before = state.RngDrawCount;
            var neighbors = TargetQuerySystem.Resolve(query, context);
            Check(neighbors.Count == 2 && neighbors[0].FormationSlot == 0 && neighbors[1].FormationSlot == 2,
                "adjacency uses stable active formation slots and excludes reserve");
            Check(state.RngDrawCount == before, "filter/sort selection consumes no random draws");
            eventTarget.FormationSlot = 99;
            var reused = TargetQuerySystem.Resolve(new TargetQueryDefinition { ReuseBinding = "neighbors" }, context);
            Check(reused.Select(f => f.Id).SequenceEqual(neighbors.Select(f => f.Id)), "stored target IDs reuse the frozen selection without adjacency recomputation");
            neighbors[0].Health = 0;
            Check(TargetQuerySystem.Resolve(new TargetQueryDefinition { ReuseBinding = "neighbors" }, context).Count == 1,
                "target reuse skips a now-defeated recipient and never invents a replacement");
            neighbors[0].Health = 1000; eventTarget.FormationSlot = 1;
            context.TargetBindings["neighbors"].Add(neighbors[0].Id);
            Check(TargetQuerySystem.Resolve(new TargetQueryDefinition { ReuseBinding = "neighbors" }, context).Count == 2,
                "a reused set cannot apply twice to a duplicate stored ID");

            state.Opponent.Fighters[0].Health = 200; state.Opponent.Fighters[2].Health = 100;
            var sorted = TargetQuerySystem.Resolve(new TargetQueryDefinition { Sort = TargetQuerySort.LowestHealth, Limit = 1 }, context);
            Check(sorted.Single().Id == state.Opponent.Fighters[2].Id, "lowest-health sorting and limit are applied after filters");
            var layered = new StatusRecipeDefinition { Id = "synthetic.layers", Stacking = StatusStackingPolicy.AddStacks,
                MaxStacks = 6, DurationClock = StatusDurationClock.Permanent };
            StatusSystem.Apply(state.Opponent.Fighters[0], owner.Id, owner.Side, layered, "layered", "setup", 1, stackCount: 3);
            var least = TargetQuerySystem.Resolve(new TargetQueryDefinition { Sort = TargetQuerySort.LowestRecipeLayers,
                RecipeId = layered.Id, Limit = 1 }, context);
            Check(least.Single().Id == eventTarget.Id, "lowest-layer overflow uses stable formation tie breaking");
            var clone = state.Clone();
            var sampled = TargetQuerySystem.Resolve(new TargetQueryDefinition { SampleCount = 2, ExcludeEventTarget = true, StoreBinding = "splash" }, context);
            var sampledAgain = TargetQuerySystem.Resolve(new TargetQueryDefinition { SampleCount = 2, ExcludeEventTarget = true },
                new CardEffectContext { Battle = clone, Actor = clone.Player.Fighters[0], SelectedTarget = clone.Opponent.Fighters[1] });
            Check(sampled.Count == 2 && sampled.Select(f => f.Id).Distinct().Count() == 2 && sampled.All(f => f.Id != eventTarget.Id),
                "random-other sampling has no duplicates and excludes the primary target");
            Check(sampled.Select(f => f.Id).SequenceEqual(sampledAgain.Select(f => f.Id)) && state.RngState == clone.RngState && state.RngDrawCount == clone.RngDrawCount,
                "saved seed/state reproduces random target IDs and exact draw count");
            before = state.RngDrawCount;
            var exclusion = TargetQuerySystem.Resolve(new TargetQueryDefinition { ExcludeBinding = "splash" }, context);
            Check(exclusion.All(f => sampled.All(s => f.Id != s.Id)) && state.RngDrawCount == before,
                "binding exclusions filter frozen IDs without rerolling");
            Check(TurnLedger.CrossedAtLeast(4, 5, 5) && !TurnLedger.CrossedAtLeast(5, 6, 5) &&
                TurnLedger.CrossedBelow(5, 4, 5) && !TurnLedger.CrossedBelow(4, 3, 5), "entry/exit helpers detect crossings instead of repeated level conditions");

            state = Create(Fighter("synthetic.ledger-owner", 100000, 1), Fighter("synthetic.ledger-target", 100000, 1));
            owner = state.Player.Fighters[0];
            Check(TurnLedger.CardsPlayedDuringCurrentOwnerTurn(state, owner) == 0 && TurnLedger.CardsPlayedDuringPreviousOwnerTurn(state, owner) == 0,
                "initial owner turn has no previous card history");
            state.Events.Add(new BattleEvent { Kind = BattleEventKind.CardMoved, SourceId = owner.Id });
            state.Events.Add(new BattleEvent { Kind = BattleEventKind.ActionFizzled, SourceId = owner.Id });
            Check(TurnLedger.CardsPlayedDuringCurrentOwnerTurn(state, owner) == 0, "moves and fizzles are not card use");
            var next = Play(state); owner = next.Player.Fighters[0];
            Check(TurnLedger.CardsPlayedDuringCurrentOwnerTurn(next, owner) == 0 && TurnLedger.CardsPlayedDuringPreviousOwnerTurn(next, owner) == 1,
                "enemy turn exposes the completed owner turn as previous history");
            next = Pass(next); owner = next.Player.Fighters[0];
            Check(TurnLedger.CardsPlayedDuringCurrentOwnerTurn(next, owner) == 0 && TurnLedger.CardsPlayedDuringPreviousOwnerTurn(next, owner) == 1,
                "new owner turn preserves previous committed-card history");
        }
    }
}
