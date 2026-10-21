using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Combat.AI;
using FightingAllstar.Core.Content;

internal static partial class Program
{
    private static int assertions;
    private static void Check(bool condition, string reason)
    { assertions++; if (!condition) throw new InvalidOperationException(reason); }
    private static string Json<T>(T value) => SnapshotJson.Serialize(value);
    private static T Copy<T>(T value) => SnapshotJson.Deserialize<T>(Json(value));

    public static void Main()
    {
        try
        {
            LifecycleAndCopies();
            ReplayAndProjection();
            SharedAbsorption();
            AutonomousActions();
            KitChecks();
            IntegrationChecks();
            AuditChecks();
            Console.WriteLine("PASS: " + assertions + " summon assertions: structural lifetime, clone/JSON/AI isolation, replay/public projection, autonomous actions, shared absorption, overflow, bypass, and Guoba/Oz/Jade Screen kits.");
        }
        catch (Exception error)
        { Console.Error.WriteLine("FAIL after " + assertions + " assertions: " + error); Environment.ExitCode = 1; }
    }

    private static CharacterDefinition Character(string id, int health = 10000)
    {
        var character = new CharacterDefinition { Id = id, SourceId = id, DisplayName = id,
            RuntimeReady = true, AttributeId = "attribute.speed", SeriesId = "series.genshin-impact",
            TraitIds = new List<string> { ElementalRuleSet.GenshinTrait },
            BaseStats = new StatBlock { MaxHealth = health, Attack = 100, Defense = 0, CritChanceBp = 0,
                CritDamageBp = 0, BlockChanceBp = 0 }, PassiveId = id + ".passive",
            Passive = new PassiveDefinition { Id = id + ".passive" } };
        for (var slot = 1; slot <= 2; slot++)
        {
            var skill = new SkillDefinition { Id = id + ".skill" + slot, Slot = slot,
                Category = CardCategory.Attack, TargetScope = EffectTargetScope.SelectedEnemy };
            for (var rank = 1; rank <= 3; rank++) skill.Ranks.Add(new SkillRankDefinition {
                Rank = rank, Effect = new EffectDefinition { Kind = EffectKind.Damage,
                    Scaling = StatScaling.Attack, CoefficientBp = 10000 * rank } });
            character.Skills.Add(skill);
        }
        for (var tier = 0; tier <= 5; tier++) character.UltimateTiers.Add(new UltimateTierDefinition {
            Tier = tier, Category = CardCategory.Attack,
            Effect = new EffectDefinition { Kind = EffectKind.Damage, Scaling = StatScaling.Attack, CoefficientBp = 10000 } });
        return character;
    }

    private static BattleState Start(CharacterDefinition owner = null, int enemies = 2, int allies = 1)
    {
        var team = new List<CharacterDefinition> { owner ?? Character("owner") };
        for (var i = 1; i < allies; i++) team.Add(Character("ally-" + i));
        return BattleEngine.Create("summon-fixture", team, null,
            Enumerable.Range(0, enemies).Select(i => Character("enemy-" + i, 100000)).ToArray(), null,
            917, TeamSide.Player);
    }

    private static BattleState Pass(BattleState state)
    {
        Check(BattleEngine.TryResolvePlan(state, new TurnPlan { RequestId = "pass-" + state.Revision,
            ExpectedRevision = state.Revision }, out var next, out var error), error ?? "Pass must resolve.");
        return next;
    }

    private static BattleState Play(BattleState state, int slot, int rank = 1, string targetId = null)
    {
        var actor = state.Team(state.ActingSide).LivingActive()[0];
        var skill = actor.Definition.Skills.Single(s => s.Slot == slot);
        var card = new CardState { Id = "fixture-card-" + state.Revision, OwnerFighterId = actor.Id,
            SkillId = skill.Id, Kind = CardKind.Skill, Rank = rank, Category = skill.Category,
            EffectCategory = skill.Category, TargetScope = skill.TargetScope };
        state.Team(state.ActingSide).Hand.Clear();
        state.Team(state.ActingSide).Hand.Add(card);
        Check(BattleEngine.TryResolvePlan(state, new TurnPlan { RequestId = "play-" + state.Revision,
            ExpectedRevision = state.Revision, Actions = new List<PlannedAction> {
                new PlannedAction { CardId = card.Id, TargetFighterId = targetId ?? state.OtherTeam(state.ActingSide).LivingActive()[0].Id } } },
            out var next, out var error), error ?? "Card must resolve.");
        return next;
    }

    private static BattleEntityDefinition Definition(string id = "fixture.summon", bool protection = false)
        => new BattleEntityDefinition { Id = id, NameKey = id, PresentationKey = id + ".visual", Duration = 2,
            Category = protection ? BattleEntityCategory.GeoConstruct : BattleEntityCategory.Summon,
            CapacityCoefficientBp = protection ? 2000 : 0,
            TurnEndAttack = protection ? null : new TriggeredElementalAttackDefinition {
                Element = ElementId.Pyro, AttackCoefficientBp = 10000 },
            ActionTarget = EffectTargetScope.AllEnemies };

    private static void LifecycleAndCopies()
    {
        var state = Start(); var owner = state.Player.Fighters[0];
        var definition = Definition(); var rng = state.RngState; var draws = state.RngDrawCount;
        var first = BattleEntitySystem.CreateOrReplace(state, owner, definition);
        var marker = owner.Statuses.Instances.Single(s => s.InstanceId == first.MarkerStatusInstanceId);
        Check(first != null && marker.EntityInstanceId == first.Id && marker.Recipe.StructuralEntityMarker,
            "A created entity must have exactly linked structural owner status.");
        Check(marker.Recipe.Polarity == StatusPolarity.Buff && marker.Recipe.Color == StatusColor.Grey,
            "Owner marker must be a grey buff.");
        Check(StatusSystem.Remove(owner, StatusPolarity.Buff, true) == 0 &&
            StatusSystem.Remove(owner, StatusPolarity.Buff, true, marker.RecipeId) == 0,
            "Generic and recipe-specific buff removal must preserve the structural marker.");
        Check(state.RngState == rng && state.RngDrawCount == draws, "Identity/lifetime work must not draw combat RNG.");
        definition.TurnEndAttack.AttackCoefficientBp = 9;
        Check(first.Definition.TurnEndAttack.AttackCoefficientBp == 10000, "Entity must freeze the definition at creation.");
        var clone = state.Clone();
        clone.Entities[0].Definition.TurnEndAttack.AttackCoefficientBp = 7;
        clone.Entities[0].Capacity = 42;
        clone.Player.Fighters[0].Statuses.Instances[0].RemainingDuration = 8;
        Check(first.Definition.TurnEndAttack.AttackCoefficientBp == 10000 && first.Capacity == 0 && marker.RemainingDuration == 2,
            "Battle clone must deep-copy entity definition and status lifetime.");
        var snapshot = Copy(state);
        Check(Json(snapshot.Entities) == Json(state.Entities) && snapshot.EntitySequence == state.EntitySequence &&
            snapshot.EntityTurnOccurrence == state.EntityTurnOccurrence, "JSON must retain all entity identity/scheduling fields.");
        var eventCopy = state.Events.Last(e => e.Kind == BattleEventKind.EntityCreated).Clone();
        eventCopy.Entity.Definition.NameKey = "mutated";
        Check(first.Definition.NameKey != "mutated" && state.Events.Last().Entity.Definition.NameKey != "mutated",
            "Event and event clone snapshots must not alias authoritative content.");
        var observation = AiPlanningObservation.Create(state);
        Check(observation.Entities.Count == 1 && observation.EntitySequence == state.EntitySequence &&
            observation.Opponent.Hand.Count == 0 && observation.Events.Count == 0, "AI observes public entities without opponent hand/history.");
        observation.Entities[0].Definition.NameKey = "ai mutation";
        Check(first.Definition.NameKey != "ai mutation", "AI entity definitions must be detached.");
        var replacement = BattleEntitySystem.CreateOrReplace(state, owner, Definition());
        Check(replacement.Id != first.Id && replacement.SpawnOrder > first.SpawnOrder && state.Entities.Count == 1 &&
            owner.Statuses.Instances.Count(s => s.Recipe.StructuralEntityMarker) == 1,
            "Same-owner same-definition recast replaces without duplicate marker or reused identity.");
        Check(state.Events.Any(e => e.Kind == BattleEventKind.EntityRemoved && e.EntityId == first.Id &&
            e.EntityRemovalCause == BattleEntityRemovalCause.Replaced), "Replacement must carry explicit cause.");
        var second = BattleEntitySystem.CreateOrReplace(state, owner, Definition("fixture.other"));
        Check(state.Entities.Count == 2 && second.Id != replacement.Id, "Different summon definitions coexist.");
        owner.IsAlive = false; owner.Health = 0;
        Check(!BattleEntitySystem.IsAvailable(state, replacement), "An owner death immediately makes entities unavailable.");
        BattleEntitySystem.Reconcile(state);
        Check(state.Entities.Count == 0 && owner.Statuses.Instances.All(s => !s.Recipe.StructuralEntityMarker) &&
            state.Events.Count(e => e.Kind == BattleEventKind.EntityRemoved && e.EntityRemovalCause == BattleEntityRemovalCause.OwnerDefeated) == 2,
            "Owner death removes all owned entities and markers with explicit facts.");
        var invalid = Definition(); invalid.Duration = 0;
        Check(invalid.Validate().Count > 0, "Zero lifetime must fail validation.");
        Check(BattleEntitySystem.CreateOrReplace(state, owner, Definition()) == null, "A defeated owner cannot summon.");
    }

    private static void ReplayAndProjection()
    {
        var state = Start(); var owner = state.Player.Fighters[0];
        var before = state.Clone(); var after = state.Events.Count;
        var entity = BattleEntitySystem.CreateOrReplace(state, owner, Definition("fixture.screen", true));
        var created = state.Events.Last(e => e.Kind == BattleEventKind.EntityCreated);
        var view = BattleProjectionBuilder.Build(state, TeamSide.Player, "verified-subject");
        Check(view.Entities.Count == 1 && view.Entities[0].RemainingDuration == 2 &&
            view.Entities[0].OwnerFighterId == owner.Id && view.Entities[0].Category == "GeoConstruct",
            "Public entity summary must expose identity, category, owner and remaining duration.");
        var marker = view.OwnTeam.Fighters[0].Statuses.Single(s => s.LinkedEntityId == entity.Id);
        Check(!marker.IsRemovable && marker.Color == "Grey" && marker.Polarity == "Buff", "Public owner marker communicates locked grey buff.");
        Check(view.OpponentTeam.Hand.Count == 0 && !Json(view).Contains("RngState") && !Json(view).Contains("TurnEndAttack"),
            "Projection must omit private RNG, opposing cards and full entity behavior payload.");
        view.Entities[0].Capacity = -5;
        Check(entity.Capacity > 0, "Public summaries must be detached.");
        foreach (var fact in state.Events.Skip(after)) BattlePlaybackState.Apply(before, fact);
        Check(Json(before.Entities) == Json(state.Entities), "Lifecycle creation facts must reconstruct entity snapshots.");
        var opening = BattlePlaybackState.BeforeOpeningDeal(state);
        Check(opening.Entities.Count == 0 && opening.EntitySequence == 0 && opening.EntityTurnOccurrence == 0 &&
            opening.Player.Fighters[0].Statuses.Instances.All(s => s.EntityInstanceId != entity.Id),
            "Opening replay must hide already-created entities and their linked markers until creation facts play.");
        var opponent = BattleProjectionBuilder.Build(state, TeamSide.Opponent, "other-subject");
        Check(opponent.Entities.Count == 1 && opponent.OpponentTeam.Hand.Count == 0,
            "Both recipients see battlefield entities without seeing each other's cards.");
        var start = state.Events.Count;
        BattleEntitySystem.Absorb(state, state.Opponent.Fighters[0], owner, 100);
        foreach (var fact in state.Events.Skip(start)) BattlePlaybackState.Apply(before, fact);
        Check(before.Entities[0].Capacity == state.Entities[0].Capacity, "Absorption facts update display capacity.");
        BattleEntitySystem.Remove(state, entity.Id, BattleEntityRemovalCause.Expired);
        var historical = BattleProjectionBuilder.Build(state, TeamSide.Player, "verified-subject").Events.Single(e => e.EventId == created.Id);
        Check(historical.Entity.RemainingDuration == 2, "Historical create projection must retain frozen duration after removal.");
        foreach (var fact in state.Events.Skip(start)) BattlePlaybackState.Apply(before, fact);
        Check(before.Entities.Count == 0 && before.Player.Fighters[0].Statuses.Instances.All(s => s.EntityInstanceId != entity.Id),
            "Replay removal must delete the entity and linked marker, including repeated facts.");
    }

    private static DamageResult Contact(BattleState state, FighterState source, FighterState target, int amount,
        DamagePolicy policy = null)
    {
        policy ??= new DamagePolicy { Family = DamageFamily.Normal, CannotCrit = true, CannotBlock = true };
        var result = DamageResolver.Resolve(new DamagePacket { BaseAmount = amount, Policy = policy },
            new StatBlock(), new StatBlock(), target.Health, target.Shield, 9999, 9999);
        var receipt = CombatPolicySystem.PrepareIncomingDamage(state, source, target, result, policy, "fixture-contact");
        target.Health = result.RemainingHealth; target.Shield = result.RemainingShield;
        CombatPolicySystem.NotifyDamageCommitted(state, source, target, result, receipt, "fixture-contact");
        return result;
    }

    private static void SharedAbsorption()
    {
        foreach (var incoming in new[] { 100, 2000, 2500 })
        {
            var state = Start(allies: 2); var owner = state.Player.Fighters[0];
            var screen = BattleEntitySystem.CreateOrReplace(state, owner, Definition("fixture.screen", true));
            var ally = state.Player.Fighters[1]; ally.Shield = 200;
            var health = ally.Health; var damage = Contact(state, state.Opponent.Fighters[0], ally, incoming);
            var absorbed = Math.Min(incoming, 2000); var overflow = incoming - absorbed;
            Check(damage.FormulaDamage == incoming && damage.ConstructAbsorbed == absorbed &&
                damage.ShieldLost == Math.Min(200, overflow) && damage.HealthLost == Math.Max(0, overflow - 200),
                "Shared capacity must precede personal shield; exact/overflow contact " + incoming + ".");
            Check(health - ally.Health == damage.HealthLost && owner.Health == owner.Stats.MaxHealth,
                "Screen protects any ally without charging the owner's HP.");
            Check(state.Entities.Count == (incoming >= 2000 ? 0 : 1), "Exact depletion removes pool immediately.");
        }
        {
            var state = Start(allies: 2); var owner = state.Player.Fighters[0];
            var screen = BattleEntitySystem.CreateOrReplace(state, owner, Definition("fixture.screen", true));
            Contact(state, state.Opponent.Fighters[0], owner, 1500);
            var later = Contact(state, state.Opponent.Fighters[0], state.Player.Fighters[1], 1500);
            Check(later.ConstructAbsorbed == 500 && later.HealthLost == 1000 && state.Entities.Count == 0,
                "AoE/multiple contacts share one remaining capacity rather than copying the pool per ally.");
            Check(state.Events.Count(e => e.Kind == BattleEventKind.EntityRemoved &&
                e.EntityRemovalCause == BattleEntityRemovalCause.CapacityDepleted) == 1,
                "A shared pool breaks exactly once.");
        }
        {
            var state = Start(); var owner = state.Player.Fighters[0];
            var a = BattleEntitySystem.CreateOrReplace(state, owner, Definition("screen.a", true));
            var bDefinition = Definition("screen.b", true); bDefinition.ProtectionPriority = -1;
            var b = BattleEntitySystem.CreateOrReplace(state, owner, bDefinition);
            var damage = Contact(state, state.Opponent.Fighters[0], owner, 2500);
            Check(damage.ConstructAbsorbed == 2500 && state.Entities.Count == 1 && state.Entities[0].Id == a.Id && a.Capacity == 1500,
                "Multiple pools allocate by explicit priority then spawn order.");
        }
        foreach (var family in new[] { DamageFamily.Normal, DamageFamily.True, DamageFamily.Additional,
            DamageFamily.DamageOverTime, DamageFamily.Destructive, DamageFamily.ElementalReaction })
        {
            var state = Start(); var owner = state.Player.Fighters[0];
            BattleEntitySystem.CreateOrReplace(state, owner, Definition("screen", true));
            var result = Contact(state, state.Opponent.Fighters[0], owner, 100,
                new DamagePolicy { Family = family, CannotCrit = true, CannotBlock = true });
            Check(result.FormulaDamage > 0 && result.ConstructAbsorbed == result.FormulaDamage && result.HealthLost == 0,
                "Incoming family uses shared interception after its formula: " + family);
        }
        {
            var state = Start(); var owner = state.Player.Fighters[0];
            var screen = BattleEntitySystem.CreateOrReplace(state, owner, Definition("screen", true));
            var result = Contact(state, state.Opponent.Fighters[0], owner, 100,
                new DamagePolicy { Family = DamageFamily.Normal, BypassConstruct = true });
            Check(result.ConstructAbsorbed == 0 && result.HealthLost == 100 && screen.Capacity == 2000,
                "An explicit construct bypass preserves capacity and applies the contact normally.");
            var nonAbsorber = Definition("non-absorbing-geo"); nonAbsorber.Category = BattleEntityCategory.GeoConstruct;
            BattleEntitySystem.CreateOrReplace(state, owner, nonAbsorber);
            BattleEntitySystem.Remove(state, screen.Id, BattleEntityRemovalCause.ScriptedLifecycle);
            Check(Contact(state, state.Opponent.Fighters[0], owner, 100).ConstructAbsorbed == 0,
                "GeoConstruct category alone grants no absorption capability.");
            Check(BattleEntitySystem.Absorb(state, owner, owner, 100) == 0,
                "Same-side/self damage cannot consume a protection pool.");
        }
    }

    private static void AutonomousActions()
    {
        var state = Start(); var owner = state.Player.Fighters[0];
        var entity = BattleEntitySystem.CreateOrReplace(state, owner, Definition());
        Check(BattleEntitySystem.CollectTurnEndJobs(state, TeamSide.Player, state.EntityTurnOccurrence).Count == 0,
            "A new entity cannot enter an already closed turn-end occurrence.");
        var original = Json(state);
        var first = Pass(state);
        Check(Json(state) == original, "Turn transaction must leave its input snapshot unchanged.");
        Check(first.Events.Count(e => e.Kind == BattleEventKind.EntityActionStarted) == 1 &&
            first.Events.Count(e => e.Kind == BattleEventKind.EntityActionEnded) == 1,
            "Owner turn end executes one entity action envelope.");
        Check(first.Opponent.Fighters.All(f => f.Health < f.Stats.MaxHealth), "Entity AoE attacks all living opposing fighters.");
        Check(first.Entities.Count == 1 && first.Player.Fighters[0].Statuses.Instances.Single(s =>
            s.EntityInstanceId == entity.Id).RemainingDuration == 1, "Entity action happens before owner-duration expiry.");
        var afterEnemy = Pass(first);
        Check(afterEnemy.Events.Count(e => e.Kind == BattleEventKind.EntityActionStarted) == 1,
            "Owner-only summon does not attack at opposing turn end.");
        var expired = Pass(afterEnemy);
        Check(expired.Entities.Count == 0 && expired.Events.Count(e => e.Kind == BattleEventKind.EntityActionStarted) == 2,
            "A two-turn summon gets its final attack before being removed.");
        Check(expired.Events.Last(e => e.Kind == BattleEventKind.EntityRemoved).EntityRemovalCause == BattleEntityRemovalCause.Expired,
            "Natural expiry has an explicit removal cause.");
        var replay = Pass(Copy(state));
        Check(Json(replay) == Json(first), "Saved snapshot reproduces entity actions and RNG exactly.");
        var once = first.Entities[0];
        Check(BattleEntitySystem.CollectTurnEndJobs(first, TeamSide.Player, once.LastActivationOccurrence).Count == 0,
            "The same occurrence never schedules the entity twice.");
        var both = Start(); var definition = Definition("both"); definition.ActOnBothSidesTurnEnd = true;
        BattleEntitySystem.CreateOrReplace(both, both.Player.Fighters[0], definition);
        both = Pass(Pass(both));
        Check(both.Events.Count(e => e.Kind == BattleEventKind.EntityActionStarted) == 2,
            "Explicit both-sides capability acts on each side without duplicating owner-duration clocks.");
    }
}
