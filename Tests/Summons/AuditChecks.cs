using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

internal static partial class Program
{
    private static void AuditChecks()
    {
        GeneratedStatusOrigin();
        OpeningEntityAuras();
        EntityContentValidation();
        EntitySnapshotValidation();
        EntityHealingReceipts();
        EntityDefeatCredit();
        EntityAoeContacts();
        EntityContactRiders();
        TurnEndCreationBoundary();
        OpposingEntityReplay();
    }

    private static void EntityContactRiders()
    {
        var state = Start(Kit("xiangling"));
        var owner = state.Player.Fighters[0];
        foreach (var target in state.Opponent.Fighters)
            target.Stats.Defense = 1000000;
        BattleEntitySystem.CreateOrReplace(state, owner, GenshinSummonKits.CreateGuoba());
        var result = Pass(state);
        var contacts = result.Events.Where(e => e.Kind == BattleEventKind.DamageApplied && e.DamageOrigin == ElementalDamageOrigin.Summon).ToList();
        Check(contacts.Count == 2 && contacts.All(e => e.FormulaDamage == 0),
            "Zero-contact fixture must fully endure both Guoba contacts before allocation.");
        Check(result.Opponent.Fighters.All(f => StatusSystem.Count(f, recipeId: "status.debuff.ignite") == 0 &&
            StatusSystem.Count(f, recipeId: "fighter.pre-release.xiangling.status.guoba-pyro-resistance") == 0),
            "A fully endured Guoba attack must not deliver its on-hit Ignite or resistance rider.");

        state = Start(Kit("xiangling"));
        owner = state.Player.Fighters[0];
        BattleEntitySystem.CreateOrReplace(state, owner, GenshinSummonKits.CreateGuoba());
        var screen = BattleEntitySystem.CreateOrReplace(state, state.Opponent.Fighters[0], Definition("audit.contact-screen", true));
        screen.Capacity = screen.MaximumCapacity = 100000;
        result = Pass(state);
        Check(result.Events.Any(e => e.Kind == BattleEventKind.DamageApplied && e.DamageOrigin == ElementalDamageOrigin.Summon &&
            e.ConstructAbsorbed > 0 && e.HealthLost == 0), "Shield-contact fixture must absorb Guoba's actual contact.");
        Check(result.Opponent.Fighters.All(f => StatusSystem.Count(f, recipeId: "status.debuff.ignite") == 2 &&
            StatusSystem.Count(f, recipeId: "fighter.pre-release.xiangling.status.guoba-pyro-resistance") == 1),
            "Positive contacts retain both riders when a construct absorbs all HP damage.");
    }

    private static void TurnEndCreationBoundary()
    {
        var character = Character("audit-turn-end-creation");
        character.Passive.Reactions.Add(new PassiveReactionDefinition { Id = "audit.turn-end-spawn", ExecutionVersion = 1,
            Trigger = PassiveEventKind.TeamTurnEnded, LimitScope = PassiveLimitScope.Battle,
            Commands = new List<PassiveCommandDefinition> { new PassiveCommandDefinition {
                Kind = PassiveCommandKind.ExecuteEffect, Effect = new CardEffectOperationDefinition {
                    Kind = CardEffectOperationKind.Summon, Target = EffectTargetScope.Self, Summon = Definition() } } } });
        var state = Pass(Start(character));
        Check(state.Entities.Count == 1 && !state.Events.Any(e => e.Kind == BattleEventKind.EntityActionStarted),
            "A turn-end-created entity must wait until the next eligible boundary.");
        var owner = state.Player.Fighters[0];
        Check(owner.Statuses.Instances.Single(s => s.Recipe.StructuralEntityMarker).RemainingDuration == 2,
            "Creation during turn end must preserve the full advertised lifetime.");
        state = Pass(Pass(state));
        Check(state.Events.Count(e => e.Kind == BattleEventKind.EntityActionStarted) == 1 &&
            state.Player.Fighters[0].Statuses.Instances.Single(s => s.Recipe.StructuralEntityMarker).RemainingDuration == 1,
            "The next owner turn activates the entity once before decrementing its lifetime.");
        state = Pass(Pass(state));
        Check(state.Entities.Count == 0 && state.Events.Count(e => e.Kind == BattleEventKind.EntityActionStarted) == 2,
            "A delayed summon still receives its final activation before expiry.");
    }

    private static void OpposingEntityReplay()
    {
        var state = Start(allies: 2);
        BattleEntitySystem.CreateOrReplace(state, state.Player.Fighters[0], Definition("audit.player"));
        BattleEntitySystem.CreateOrReplace(state, state.Opponent.Fighters[0], Definition("audit.opponent"));
        BattleEntitySystem.CreateOrReplace(state, state.Opponent.Fighters[1], Definition("audit.screen", true));
        for (var turn = 0; turn < 4; turn++)
        {
            var display = state.Clone(); var firstEvent = state.Events.Count;
            state = Pass(state);
            foreach (var fact in state.Events.Skip(firstEvent)) BattlePlaybackState.Apply(display, fact);
            Check(Json(display.Entities) == Json(state.Entities), "Both teams' entity snapshots replay exactly after turn " + turn + ".");
            foreach (var fighter in state.Player.Fighters.Concat(state.Opponent.Fighters))
            {
                var observed = display.Team(fighter.Side).FindFighter(fighter.Id);
                Check(Json(observed.Statuses.Instances.Where(s => s.Recipe.StructuralEntityMarker).ToList()) ==
                    Json(fighter.Statuses.Instances.Where(s => s.Recipe.StructuralEntityMarker).ToList()),
                    "Both teams' entity marker clocks replay exactly after turn " + turn + ".");
            }
        }
        Check(state.Entities.Count == 0, "Both sides' finite entities expire without leaving orphan links.");
    }

    private static void GeneratedStatusOrigin()
    {
        var character = Kit("xiangling");
        character.Passive.Reactions.Add(new PassiveReactionDefinition { Id = "audit.card-status-only",
            ExecutionVersion = 1, Trigger = PassiveEventKind.StatusApplied, ActorRelation = PassiveRelation.Self,
            TargetRelation = PassiveRelation.Enemies, CardOriginOnly = true, EventStatusRecipeId = "status.debuff.ignite",
            Commands = new List<PassiveCommandDefinition> { new PassiveCommandDefinition {
                Kind = PassiveCommandKind.IncrementCounter, CounterKey = "card-origin-status" } } });
        var state = Start(character);
        BattleEntitySystem.CreateOrReplace(state, state.Player.Fighters[0], GenshinSummonKits.CreateGuoba());
        var result = Pass(state);
        Check(result.Opponent.Fighters.All(f => StatusSystem.Count(f, recipeId: "status.debuff.ignite") == 2),
            "Audit fixture must deliver real Guoba Ignite layers.");
        Check(CharacterPassiveRuntime.Counter(result.Player.Fighters[0], "card-origin-status") == 0,
            "Generated summon status riders must not activate card-only status passives.");
    }

    private static BattleEntityDefinition AuraEntity() => new BattleEntityDefinition {
        Id = "audit.aura", NameKey = "Audit aura", Duration = 2,
        Auras = new List<PassiveAuraDefinition> { new PassiveAuraDefinition { Id = "owner-attack",
            Targets = new PassiveTargetFilter { Relation = PassiveRelation.Self },
            Modifiers = new List<StatModifierDefinition> { new StatModifierDefinition {
                Target = ModifierTarget.Stat, Stat = StatId.Attack, Operation = ModifierOperation.PercentOfBase, Amount = 2000 } } } } };

    private static void OpeningEntityAuras()
    {
        var character = Character("audit-opening-aura");
        character.Passive.Reactions.Add(new PassiveReactionDefinition { Id = "audit.opening", ExecutionVersion = 1,
            Trigger = PassiveEventKind.BattleStarted, Commands = new List<PassiveCommandDefinition> {
                new PassiveCommandDefinition { Kind = PassiveCommandKind.ExecuteEffect,
                    Effect = new CardEffectOperationDefinition { Kind = CardEffectOperationKind.Summon,
                        Target = EffectTargetScope.Self, Summon = AuraEntity() } } } });
        var state = Start(character); var owner = state.Player.Fighters[0];
        Check(StatusSystem.GetEffectiveStats(owner).Attack == 120, "Opening entity aura is active in the authoritative state.");
        var display = BattlePlaybackState.BeforeOpeningDeal(state);
        Check(display.Entities.Count == 0 && StatusSystem.GetEffectiveStats(display.Player.Fighters[0]).Attack == 100,
            "Pre-opening display must not retain an aura from an entity whose creation has not played.");
        foreach (var fact in state.Events) BattlePlaybackState.Apply(display, fact);
        Check(display.Entities.Count == 1 && StatusSystem.GetEffectiveStats(display.Player.Fighters[0]).Attack == 120,
            "Opening replay restores entity aura exactly when its authority facts play.");
    }

    private static void EntityContentValidation()
    {
        var character = Character("audit-validation");
        character.Skills[0].Ranks[0].Effect = new EffectDefinition { Kind = EffectKind.Summon,
            Target = EffectTargetScope.Self, Summon = Definition() };
        character.Skills[0].Ranks[0].Effect.Summon.Duration = 0;
        Check(AbilityVariantValidator.Validate(character).Count > 0,
            "Ordinary skill ranks must validate their summon definitions, not only optional variants.");
        character = Character("audit-ultimate-validation");
        character.UltimateTiers[0].Effect.Sequence.Add(new CardEffectStep {
            Timing = CardEffectTiming.AfterDamage, Effect = new EffectOperationDefinition {
                Kind = EffectKind.Summon, Target = EffectTargetScope.Self, Summon = new BattleEntityDefinition { Id = "bad", SchemaVersion = 99 } } });
        Check(AbilityVariantValidator.Validate(character).Count > 0,
            "Ordinary Ultimate sequence summons must reject unsupported definition schemas.");
        character = Character("audit-source-stack-validation");
        character.Skills[0].Ranks[0].Effect.SourceStackDamageBp = -1;
        Check(AbilityVariantValidator.Validate(character).Count > 0, "Negative source-layer scaling must be rejected at authoring/load boundaries.");
    }

    private static void RejectSnapshot(BattleState state, string reason)
    {
        var rejected = false;
        try { Copy(state); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, reason);
    }

    private static void EntitySnapshotValidation()
    {
        var state = Start(); var owner = state.Player.Fighters[0];
        var entity = BattleEntitySystem.CreateOrReplace(state, owner, Definition());
        var marker = owner.Statuses.Instances.Single(s => s.EntityInstanceId == entity.Id);
        marker.Recipe.DurationClock = StatusDurationClock.Permanent;
        RejectSnapshot(state, "Persisted structural markers cannot switch to a permanent duration clock.");
        var original = Json(state);
        Check(!BattleEngine.TryResolvePlan(state, new TurnPlan { RequestId = "invalid-entity", ExpectedRevision = state.Revision },
            out var rejectedState, out var error) && rejectedState == null && error.Contains("Invalid entity snapshot") && Json(state) == original,
            "The authoritative plan boundary rejects malformed entity state atomically, even without JSON loading.");
        marker.Recipe.DurationClock = StatusDurationClock.TargetTurnEnd;
        marker.Recipe.BreakRule = StatusBreakRule.None;
        RejectSnapshot(state, "Persisted structural markers cannot disable owner-death cleanup.");
        marker.Recipe.BreakRule = StatusBreakRule.OnSourceDefeated;
        entity.EligibleFromOccurrence = -1;
        RejectSnapshot(state, "Persisted entity scheduler occurrences cannot be negative.");
        var detached = owner.Clone(); var before = state.Entities.Count;
        Check(BattleEntitySystem.CreateOrReplace(state, detached, Definition("detached")) == null && state.Entities.Count == before,
            "Creation must reject a detached fighter instead of inserting an orphan marker link.");
    }

    private static void EntityHealingReceipts()
    {
        var character = Character("audit-support");
        character.Passive.Support = new CombatSupportDefinition { DamageHealLowestAllyBp = 5000 };
        var state = Start(character, allies: 2); var recipient = state.Player.Fighters[1]; recipient.Health = 1;
        BattleEntitySystem.CreateOrReplace(state, state.Player.Fighters[0], Definition());
        var result = Pass(state);
        var damage = result.Events.Where(e => e.Kind == BattleEventKind.DamageApplied && e.EntityId != null).Sum(e => e.HealthLost);
        var healed = result.Events.Where(e => e.Kind == BattleEventKind.HealApplied && e.TargetId == recipient.Id).Sum(e => e.Amount);
        Check(healed == damage / 2, "Entity AoE healing uses each committed damage receipt once, without recounting earlier targets.");
    }

    private static void AddKillCounter(CharacterDefinition character)
    {
        character.Passive.Reactions.Add(new PassiveReactionDefinition { Id = character.Id + ".audit-kill",
            ExecutionVersion = 1, Trigger = PassiveEventKind.FighterDefeated, ActorRelation = PassiveRelation.Self,
            TargetRelation = PassiveRelation.Enemies, AllowReactionOrigin = true,
            Commands = new List<PassiveCommandDefinition> { new PassiveCommandDefinition {
                Kind = PassiveCommandKind.IncrementCounter, CounterKey = "audit-kills" } } });
    }

    private static void EntityDefeatCredit()
    {
        var ally = Character("audit-hydro"); ally.Skills[0].Ranks[0].Effect.DamageElement = ElementId.Hydro;
        var fischl = Kit("fischl"); AddKillCounter(ally); AddKillCounter(fischl);
        var state = BattleEngine.Create("audit-entity-kill", new[] { ally, fischl }, null,
            new[] { Character("audit-victim", 100000), Character("audit-bystander", 100000) }, null, 917);
        var target = state.Opponent.Fighters[0]; target.Health = 150;
        target.ElementStatuses.Apply(ElementId.Electro, state.Player.Fighters[1].Id, "audit-electro");
        BattleEntitySystem.CreateOrReplace(state, state.Player.Fighters[1], GenshinSummonKits.CreateOz());
        var result = Play(state, 1);
        Check(result.Events.Any(e => e.Kind == BattleEventKind.DamageApplied && e.SourceId == state.Player.Fighters[1].Id &&
            e.TargetId == target.Id && e.HealthAfter == 0 && e.HealthLost > 0), "Oz follow-up must deliver the lethal contact in the audit fixture.");
        Check(CharacterPassiveRuntime.Counter(result.Player.Fighters[1], "audit-kills") == 1 &&
            CharacterPassiveRuntime.Counter(result.Player.Fighters[0], "audit-kills") == 0,
            "An allied Oz follow-up must credit Fischl's defeat passives, not the ally who played the triggering card.");
    }

    private static void EntityAoeContacts()
    {
        var state = Start(Kit("xiangling"));
        foreach (var target in state.Opponent.Fighters) target.Health = 150;
        state.Opponent.Fighters[0].ElementStatuses.Apply(ElementId.Electro, state.Player.Fighters[0].Id, "audit-aoe-electro");
        var entity = BattleEntitySystem.CreateOrReplace(state, state.Player.Fighters[0], GenshinSummonKits.CreateGuoba());
        var result = Pass(state);
        var roots = result.Events.Where(e => e.Kind == BattleEventKind.DamageApplied &&
            e.EntityId == entity.Id && e.DamageOrigin == ElementalDamageOrigin.Summon).ToList();
        Check(roots.Select(e => e.TargetId).Distinct().Count() == 2,
            "Guoba AoE must commit a root contact to every captured living target before reaction collateral can defeat its neighbours.");
        var firstChild = result.Events.FindIndex(e => e.Kind == BattleEventKind.DamageApplied && e.DamageOrigin == ElementalDamageOrigin.Reaction);
        Check(firstChild < 0 || roots.All(e => e.Id < result.Events[firstChild].Id),
            "Entity AoE reaction children resolve after all root contacts, matching the shared hit-batch contract.");
    }
}
