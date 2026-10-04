using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;

internal static class BattlePlaybackChecks
{
    private static int _assertions;
    private static void Check(bool condition, string message)
    {
        _assertions++;
        if (!condition) throw new Exception(message);
    }

    public static void Main()
    {
        OpeningAndFullBattleReplay();
        DraftMergeAndReset();
        MoveAndMergePowerGaugeChecks();
        DeadTargetRetargetsDeterministically();
        EventPayloadIsolation();
        EncounterIdsReachHistoricalCards();
        EmptyPassiveSerializationCompatibility();
        PassiveTriggerAndLifestealAndRecoveryChecks();
        CardCategoryAndDisableChecks();
        AreaTargetDamageChecks();
        PierceAndWeakpointRecipeChecks();
        RejuvenationRuntimeChecks();
        CardRankIncreaseRuntimeChecks();
        Console.WriteLine("PASS: " + _assertions + " assertions: opening chronology, chained merges, full battle replay, enemy-first, death/reserve, seeded retargeting, draft reset, payload isolation, encounter IDs, move & merge PG, passive trigger, uncapped lifesteal, missing health recovery.");
    }

    private static CharacterDefinition Fighter(string id, int health = 450, int attack = 85)
    {
        var definition = new CharacterDefinition { Id = id, BaseStats = new StatBlock { MaxHealth = health, Attack = attack } };
        for (var slot = 1; slot <= 2; slot++)
        {
            var skill = new SkillDefinition { Id = id + ":skill:" + slot, Slot = slot };
            for (var rank = 1; rank <= 3; rank++) skill.Ranks.Add(new SkillRankDefinition { Rank = rank,
                Effect = new EffectDefinition { Scaling = StatScaling.Attack, CoefficientBp = rank * 10000 } });
            definition.Skills.Add(skill);
        }
        definition.UltimateTiers.Add(new UltimateTierDefinition { Tier = 0,
            Effect = new EffectDefinition { Scaling = StatScaling.Attack, CoefficientBp = 40000 } });
        return definition;
    }

    private static BattleState Create(ulong seed, TeamSide first = TeamSide.Player) => BattleEngine.Create("test:" + seed,
        new[] { Fighter("a"), Fighter("b"), Fighter("c"), Fighter("d") }, null,
        new[] { Fighter("e"), Fighter("f"), Fighter("g"), Fighter("h") }, null, seed, first);

    private static void EqualDisplay(BattleState display, BattleState authority)
    {
        foreach (var side in new[] { TeamSide.Player, TeamSide.Opponent })
        {
            var left = display.Team(side);
            var right = authority.Team(side);
            Check(string.Join("|", left.Hand.Select(c => c.Id + ":" + c.Rank)) ==
                string.Join("|", right.Hand.Select(c => c.Id + ":" + c.Rank)), "Event hand diverged: " + side);
            foreach (var fighter in right.Fighters)
            {
                var shown = left.FindFighter(fighter.Id);
                Check(shown.Health == fighter.Health && shown.Shield == fighter.Shield && shown.IsAlive == fighter.IsAlive &&
                    shown.IsReserve == fighter.IsReserve && shown.FormationSlot == fighter.FormationSlot && shown.PowerGauge == fighter.PowerGauge,
                    "Event fighter diverged: " + fighter.Id);
            }
        }
    }

    private static void OpeningAndFullBattleReplay()
    {
        var midDealMerge = false;
        var sawDeath = false;
        var sawReserve = false;
        var sawUltimate = false;
        for (ulong seed = 1; seed <= 64; seed++)
        {
            var initial = Create(seed, seed % 2 == 0 ? TeamSide.Opponent : TeamSide.Player);
            var display = BattlePlaybackState.BeforeOpeningDeal(initial);
            for (var i = 0; i < initial.Events.Count; i++)
            {
                var item = initial.Events[i];
                if (item.Kind == BattleEventKind.CardsMerged)
                {
                    var owner = display.Player.FindFighter(item.SourceId) ?? display.Opponent.FindFighter(item.SourceId);
                    var hand = display.Team(owner.Side).Hand;
                    Check(hand.Exists(c => c.Id == item.CardId) && hand.Exists(c => c.Id == item.ConsumedCardId), "Merge precedes its two draws.");
                    midDealMerge |= initial.Events.Skip(i + 1).Any(e => e.Kind == BattleEventKind.CardDrawn && e.SourceId == item.SourceId);
                }
                BattlePlaybackState.Apply(display, item);
            }
            EqualDisplay(display, initial);
            var session = new LocalBattleSession(initial);
            long cursor = initial.Events.Last().Id;
            for (var turn = 0; turn <= BattleEngine.MaximumCompletedTurns; turn++)
            {
                foreach (var item in session.GetEventsAfter(cursor))
                {
                    BattlePlaybackState.Apply(display, item);
                    cursor = item.Id;
                    sawDeath |= item.Kind == BattleEventKind.FighterDefeated;
                    sawReserve |= item.Kind == BattleEventKind.ReserveEntered;
                    sawUltimate |= item.Kind == BattleEventKind.CardPlayed && item.Card.Kind == CardKind.Ultimate;
                }
                var state = session.GetSnapshot();
                EqualDisplay(display, state);
                if (state.Phase == BattlePhase.Complete) break;
                Check(turn < BattleEngine.MaximumCompletedTurns, "Battle did not terminate.");
                var draft = new PlanDraft(state);
                for (var slot = 0; slot < state.ActionBudget; slot++)
                {
                    var card = draft.Preview.Hand.FirstOrDefault();
                    if (card == null) break;
                    if (!draft.QueuePlay(card.Id, state.Opponent.LivingActive()[0].Id, out _)) break;
                }
                Check(session.Submit(draft.BuildPlan("turn:" + turn), out var error), error);
            }
        }
        Check(midDealMerge, "Fixture must cover draw-merge-draw opening.");
        Check(sawDeath && sawReserve && sawUltimate, "Full sessions must cover death, reserve, and ultimate.");
    }

    private static void DraftMergeAndReset()
    {
        var state = Create(72);
        var owner = state.Player.Fighters[0];
        state.Player.Hand = new List<CardState>
        {
            Card("x", owner, 2), Card("y", owner, 1), Card("gap", owner, 1, 1), Card("z", owner, 1)
        };
        var draft = new PlanDraft(state);
        var display = state.Clone();
        Check(draft.QueuePlay("gap", state.Opponent.Fighters[0].Id, out var error), error);
        Check(draft.LastEvents.Count(e => e.Kind == BattleEventKind.CardsMerged) == 2, "Rank chain must emit both merges.");
        foreach (var item in draft.LastEvents) BattlePlaybackState.Apply(display, item);
        Check(display.Player.Hand.Count == 1 && display.Player.Hand[0].Rank == 3, "Chained rank must reach three.");
        Check(draft.Preview.Hand[0].Rank == display.Player.Hand[0].Rank, "Draft animation diverged.");
        Check(draft.QueuePlay("x", state.Opponent.Fighters[0].Id, out error), error);
        Check(draft.LastEvents[0].Card.Rank == 3, "Queued execution loses merged rank.");
        Check(draft.UndoLast(), "Undo failed.");
        draft.Reset();
        Check(draft.Preview.Hand.Count == 4 && draft.Preview.Hand[0].Rank == 2 && state.RngDrawCount == display.RngDrawCount,
            "Reset must restore hand and leave RNG untouched.");
        Check(draft.QueueMove("z", 2, out error), error);
        Check(draft.LastEvents[0].Kind == BattleEventKind.CardMoved, "Move must precede merge.");
    }

    private static void MoveAndMergePowerGaugeChecks()
    {
        var state = Create(99);
        var f1 = state.Player.Fighters[0];
        var f2 = state.Player.Fighters[1];
        f1.PowerGauge = 0;
        f2.PowerGauge = 0;

        // 1. Move without merge: gains exactly 1 PG
        state.Player.Hand = new List<CardState>
        {
            Card("a1", f1, 1, 0),
            Card("b1", f2, 1, 0),
            Card("a2", f1, 1, 1),
        };
        var draft = new PlanDraft(state);
        Check(draft.QueueMove("a2", 0, out var error), error);
        Check(draft.Preview.FindFighter(f1.Id).PowerGauge == 1, "Move without merge must grant exactly 1 PG.");
        Check(draft.LastEvents.Count == 1 && draft.LastEvents[0].Kind == BattleEventKind.CardMoved && draft.LastEvents[0].PowerGaugeAfter == 1, "CardMoved event must report PowerGaugeAfter = 1.");

        // 2. Move with merge: gains 2 PG (1 from move + 1 from merge)
        state.Player.Hand = new List<CardState>
        {
            Card("a1", f1, 1, 0),
            Card("b1", f2, 1, 0),
            Card("a2", f1, 1, 0),
        };
        f1.PowerGauge = 0;
        f2.PowerGauge = 0;
        draft = new PlanDraft(state);
        Check(draft.QueueMove("a2", 0, out error), error);
        Check(draft.Preview.FindFighter(f1.Id).PowerGauge == 2, "Move with merge must grant 2 PG (1 move + 1 merge).");
        Check(draft.LastEvents.Count == 2, "Must emit move followed by merge.");
        Check(draft.LastEvents[0].Kind == BattleEventKind.CardMoved && draft.LastEvents[0].PowerGaugeAfter == 1, "CardMoved event must report 1 PG.");
        Check(draft.LastEvents[1].Kind == BattleEventKind.CardsMerged && draft.LastEvents[1].PowerGaugeAfter == 2, "CardsMerged event must report 2 PG.");

        // 3. Move causing another fighter to merge: mover gets 1 move PG, other gets 1 merge PG
        state.Player.Hand = new List<CardState>
        {
            Card("a1", f1, 1, 0),
            Card("b1", f2, 1, 0),
            Card("a2", f1, 1, 0),
        };
        f1.PowerGauge = 0;
        f2.PowerGauge = 0;
        draft = new PlanDraft(state);
        Check(draft.QueueMove("b1", 2, out error), error);
        Check(draft.Preview.FindFighter(f2.Id).PowerGauge == 1, "Moving fighter gains 1 move PG.");
        Check(draft.Preview.FindFighter(f1.Id).PowerGauge == 1, "Merged fighter gains 1 merge PG.");

        // 4. Move causing a 2-step merge chain: mover gains 1 move PG + 2 merge PG = 3 PG
        state.Player.Hand = new List<CardState>
        {
            Card("a_r2", f1, 2, 0),
            Card("b1", f2, 1, 0),
            Card("a_r1_left", f1, 1, 0),
            Card("a_r1_right", f1, 1, 0),
        };
        f1.PowerGauge = 0;
        f2.PowerGauge = 0;
        draft = new PlanDraft(state);
        Check(draft.QueueMove("b1", 3, out error), error);
        Check(draft.Preview.FindFighter(f2.Id).PowerGauge == 1, "Mover gains 1 move PG.");
        Check(draft.Preview.FindFighter(f1.Id).PowerGauge == 2, "Chain merged fighter gains 2 merge PG (1 per merge).");

        // 5. PG capped at 5
        f1.PowerGauge = 4;
        state.Player.Hand = new List<CardState>
        {
            Card("a1", f1, 1, 0),
            Card("b1", f2, 1, 0),
            Card("a2", f1, 1, 0),
        };
        draft = new PlanDraft(state);
        Check(draft.QueueMove("a2", 0, out error), error);
        Check(draft.Preview.FindFighter(f1.Id).PowerGauge == 5, "PG must cap at 5 on move + merge.");
    }

    private static CardState Card(string id, FighterState owner, int rank, int skill = 0) => new CardState
    { Id = id, OwnerFighterId = owner.Id, SkillId = owner.Definition.Skills[skill].Id, Rank = rank };

    private static void DeadTargetRetargetsDeterministically()
    {
        var targets = new HashSet<string>();
        for (ulong seed = 1; seed <= 32; seed++)
        {
            var state = BattleEngine.Create("retarget", new[] { Fighter("a", 1000, 1000) }, null,
                new[] { Fighter("b", 10), Fighter("c", 10), Fighter("d", 10) }, null, seed);
            var owner = state.Player.Fighters[0];
            state.Player.Hand = new List<CardState> { Card("one", owner, 1), Card("two", owner, 1, 1) };
            var draft = new PlanDraft(state);
            foreach (var card in state.Player.Hand) Check(draft.QueuePlay(card.Id, state.Opponent.Fighters[0].Id, out _), "Plan target rejected.");
            var plan = draft.BuildPlan("retarget-plan");
            Check(BattleEngine.TryResolvePlan(state, plan, out var first, out var error), error);
            Check(BattleEngine.TryResolvePlan(state, plan, out var replay, out error), error);
            var plays = first.Events.Where(e => e.Kind == BattleEventKind.CardPlayed).ToList();
            Check(plays.Count == 2 && plays[0].TargetId != plays[1].TargetId, "Dead target was not replaced.");
            targets.Add(plays[1].TargetId);
            Check(plays[1].TargetId == replay.Events.Last(e => e.Kind == BattleEventKind.CardPlayed).TargetId &&
                first.RngState == replay.RngState, "Retarget replay is nondeterministic.");
        }
        Check(targets.Count == 2, "Retarget always chooses a fixed slot.");
    }

    private static void EventPayloadIsolation()
    {
        var state = Create(8);
        var draw = state.Events.First(e => e.Card != null);
        var copy = state.Clone();
        copy.Events.First(e => e.Card != null).Card.Rank = 99;
        Check(draw.Card.Rank == 1, "Cloned event payload mutated authority.");
    }

    private static void EncounterIdsReachHistoricalCards()
    {
        var encounter = new EncounterProjection { BattleId = "encounter-remap" };
        for (var side = 0; side < 2; side++)
        {
            var definition = Fighter("fixture:" + side);
            var fighter = new EncounterFighterSnapshot { FighterId = "run:" + side, DefinitionId = definition.Id,
                Definition = definition, Stats = definition.BaseStats.Clone(), CurrentHealth = 321 };
            (side == 0 ? encounter.PlayerTeam : encounter.EnemyTeam).Add(fighter);
        }
        var battle = RunBattleBridge.CreateLocalBattle(encounter, 10);
        foreach (var item in battle.Events.Where(e => e.Card != null))
            Check(item.Card.OwnerFighterId.StartsWith("run:"), "Historical card owner was not remapped.");
        Check(battle.Player.Fighters[0].Health == 321, "Opening heals persistent run HP.");
    }

    private static void EmptyPassiveSerializationCompatibility()
    {
        // Simulates Unity's JsonUtility creating empty PassiveDefinition instances (Id = "")
        var encounter = new EncounterProjection { BattleId = "encounter-empty-passive" };
        var playerFighter = Fighter("fighter.kyo94");
        playerFighter.Passive = new PassiveDefinition { Id = "" };
        var playerSnapshot = new EncounterFighterSnapshot { FighterId = "run:player:1", DefinitionId = playerFighter.Id,
            Definition = playerFighter, Stats = playerFighter.BaseStats.Clone(), CurrentHealth = 450 };
        encounter.PlayerTeam.Add(playerSnapshot);

        var enemyFighter = Fighter("fighter.mai94");
        enemyFighter.Passive = new PassiveDefinition { Id = "" };
        var enemySnapshot = new EncounterFighterSnapshot { FighterId = "run:enemy:1", DefinitionId = enemyFighter.Id,
            Definition = enemyFighter, Stats = enemyFighter.BaseStats.Clone(), CurrentHealth = 450 };
        encounter.EnemyTeam.Add(enemySnapshot);

        var battle = RunBattleBridge.CreateLocalBattle(encounter, 10);
        Check(battle != null, "Battle should initialize without ArgumentException when passives have empty IDs.");
        Check(battle.Player.Fighters[0].Definition.Passive != null, "Player passive should be attached from standard passives.");
        Check(battle.Player.Fighters[0].Definition.Passive.Id == "fighter.kyo94.passive.source", "Kyo's passive was attached.");
        Check(battle.Opponent.Fighters[0].Definition.Passive.Id == "fighter.mai94.passive.source", "Mai's passive was attached.");

        // Test ContentCatalog AttachMissing with empty passive Id
        var catalog = new ContentCatalog { Characters = new List<CharacterDefinition> { playerFighter, enemyFighter } };
        playerFighter.Passive = new PassiveDefinition { Id = "" };
        enemyFighter.Passive = new PassiveDefinition { Id = "" };
        StandardCharacterPassives.AttachMissing(catalog);
        Check(playerFighter.Passive.Id == "fighter.kyo94.passive.source", "AttachMissing repaired empty player passive.");
        Check(enemyFighter.Passive.Id == "fighter.mai94.passive.source", "AttachMissing repaired empty enemy passive.");
    }

    private static void PassiveTriggerAndLifestealAndRecoveryChecks()
    {
        // 1. Turn-start recovery scales with missing health
        var player = Fighter("p1", 1000, 100);
        player.BaseStats.RegenerationBp = 500; // 5%
        player.BaseStats.RecoveryBp = 10000;   // 100%
        var enemy = Fighter("e1", 1000, 10);
        var battle = BattleEngine.Create("recovery-test", new[] { player }, null, new[] { enemy }, null, 1, TeamSide.Player,
            playerHealth: new[] { 600 }); // 400 missing HP
        // Advance turn so turn-start recovery triggers on player's next turn
        var draft = new PlanDraft(battle);
        for (var slot = 0; slot < battle.ActionBudget; slot++)
        {
            var card = draft.Preview.Hand.FirstOrDefault();
            if (card == null) break;
            draft.QueuePlay(card.Id, battle.Opponent.LivingActive()[0].Id, out _);
        }
        var session = new LocalBattleSession(battle);
        Check(session.Submit(draft.BuildPlan("turn1"), out var recErr), recErr);
        // Opponent turn ends and Player turn starts -> TurnStart recovery fires
        var enemyDraft = new PlanDraft(session.GetSnapshot());
        for (var slot = 0; slot < battle.ActionBudget; slot++)
        {
            var card = enemyDraft.Preview.Hand.FirstOrDefault();
            if (card == null) break;
            enemyDraft.QueuePlay(card.Id, battle.Player.LivingActive()[0].Id, out _);
        }
        Check(session.Submit(enemyDraft.BuildPlan("turn2"), out var enemyErr), enemyErr);

        var healEvents = battle.Events.FindAll(e => e.Kind == BattleEventKind.HealApplied && e.Message == "Turn-start Recovery.");
        Check(healEvents.Count > 0, "Turn-start recovery should fire for damaged fighter.");
        // Expected heal: 400 missing HP * 5% * 1.0 = 20
        Check(healEvents[0].Amount == 20, "Turn-start recovery must heal based on missing health (expected 20, got " + healEvents[0].Amount + ")");

        // 2. Lifesteal FCT is uncapped by missing HP (even at full health)
        var lifestealPlayer = Fighter("p_ls", 1000, 200);
        lifestealPlayer.BaseStats.LifeStealBp = 2000; // 20%
        lifestealPlayer.BaseStats.RecoveryBp = 10000; // 100%
        var lifestealEnemy = Fighter("e_ls", 1000, 10);
        var lsBattle = BattleEngine.Create("lifesteal-test", new[] { lifestealPlayer }, null, new[] { lifestealEnemy }, null, 1);
        Check(lsBattle.Player.Fighters[0].Health == 1000, "Attacker starts at full health.");
        var lsDraft = new PlanDraft(lsBattle);
        for (var slot = 0; slot < lsBattle.ActionBudget; slot++)
        {
            var card = lsDraft.Preview.Hand.FirstOrDefault();
            if (card == null) break;
            lsDraft.QueuePlay(card.Id, lsBattle.Opponent.LivingActive()[0].Id, out _);
        }
        var lsSession = new LocalBattleSession(lsBattle);
        Check(lsSession.Submit(lsDraft.BuildPlan("turn1"), out var lsErr), lsErr);
        var lsState = lsSession.GetSnapshot();
        var lsHealEvents = lsState.Events.FindAll(e => e.Kind == BattleEventKind.HealApplied && e.Message == "Lifesteal.");
        Check(lsHealEvents.Count > 0, "Lifesteal event must be emitted even if attacker is at full health.");
        Check(lsHealEvents[0].Amount > 0, "Lifesteal event Amount must be > 0 for FCT display.");
        Check(lsHealEvents[0].HealthAfter == 1000, "Attacker HP remains clamped to MaxHealth.");

        // 3. Mai94 emits PassiveTriggered at Game Start
        var maiFighter = Fighter("fighter.mai94");
        maiFighter.Passive = StandardCharacterPassives.Create("fighter.mai94");
        maiFighter.BaseStats.RegenerationBp = 1000;
        var maiBattle = BattleEngine.Create("mai-test", new[] { maiFighter }, null, new[] { Fighter("enemy") }, null, 1);
        var maiFighterState = maiBattle.Player.Fighters[0];
        var maiPassiveEvents = maiBattle.Events.FindAll(e => e.Kind == BattleEventKind.PassiveTriggered && e.SourceId == maiFighterState.Id);
        Check(maiPassiveEvents.Count == 1, "Mai94 must emit PassiveTriggered at Game Start when her aura activates.");

        // 4. Shingo97 Depletes drain & Passive Trigger
        var shingoFighter = Fighter("fighter.shingo97");
        shingoFighter.Passive = StandardCharacterPassives.Create("fighter.shingo97");
        shingoFighter.Skills[0].Ranks[0].Effect.KeywordId = "Depletes";
        var shingoEnemy = Fighter("enemy_target", 2000, 10);
        var shingoBattle = BattleEngine.Create("shingo-test", new[] { shingoFighter }, null, new[] { shingoEnemy }, null, 1);
        var targetFighter = shingoBattle.Opponent.Fighters[0];
        targetFighter.PowerGauge = 3;
        var shingoFighterState = shingoBattle.Player.Fighters[0];
        var shingoDraft = new PlanDraft(shingoBattle);
        for (var slot = 0; slot < shingoBattle.ActionBudget; slot++)
        {
            var card = shingoDraft.Preview.Hand.FirstOrDefault(c => c.OwnerFighterId == shingoFighterState.Id) ?? shingoDraft.Preview.Hand.FirstOrDefault();
            if (card == null) break;
            shingoDraft.QueuePlay(card.Id, targetFighter.Id, out _);
        }
        var shingoSession = new LocalBattleSession(shingoBattle);
        Check(shingoSession.Submit(shingoDraft.BuildPlan("turn1"), out var shingoErr), shingoErr);
        var shingoState = shingoSession.GetSnapshot();
        var shingoPassiveEvents = shingoState.Events.FindAll(e => e.Kind == BattleEventKind.PassiveTriggered && e.SourceId == shingoFighterState.Id);
        var drainEvent = shingoState.Events.Find(e => e.Kind == BattleEventKind.PowerGaugeChanged && e.TargetId == targetFighter.Id && e.Amount < 0);
        Check(drainEvent != null, "Enemy power gauge must have decreased from Depletes card.");
        Check(shingoPassiveEvents.Count > 0, "Shingo must emit PassiveTriggered when enemy gauge is depleted.");
        Check(shingoState.Player.Fighters[0].PowerGauge > 0, "Shingo must receive refunded power gauge from passive.");
    }

    private static void CardCategoryAndDisableChecks()
    {
        var player = Fighter("category-player");
        player.Skills[0].SourceType = "Heal";
        var enemy = Fighter("category-enemy");
        var battle = BattleEngine.Create("category-test", new[] { player }, null, new[] { enemy }, null, 33,
            TeamSide.Player);
        var heal = battle.Player.Hand.Find(card => card.SkillId == player.Skills[0].Id);
        Check(heal != null && heal.Category == CardCategory.Recovery,
            "WIP Heal skill type should be carried into the runtime card category.");
        var view = BattleProjectionBuilder.Build(battle, TeamSide.Player, "player-subject");
        var projectedHeal = view.OwnTeam.Hand.Find(card => card.CardId == heal.Id);
        Check(projectedHeal != null && projectedHeal.Category == nameof(CardCategory.Recovery),
            "Recipient battle projection should preserve card category.");
        Check(CardRules.ResolveCategory(new SkillDefinition { SourceType = "DebuffAtk" }) == CardCategory.AttackDebuff,
            "DebuffAtk should map to the combined runtime category.");

        var owner = battle.Player.FindFighter(heal.OwnerFighterId);
        var disable = new StatusRecipeDefinition { Id = "test.disable-recovery", Polarity = StatusPolarity.Debuff,
            Behavior = StatusBehavior.Disable, DisableMask = CardCategoryMask.Recovery, DefaultDuration = 1 };
        var applied = StatusSystem.Apply(owner, battle.Opponent.Fighters[0].Id, TeamSide.Opponent, disable,
            "category-disable", null, 1);
        Check(applied.Accepted, "Recovery-disable status should apply to its target.");
        var draft = new PlanDraft(battle);
        Check(!draft.QueuePlay(heal.Id, battle.Opponent.Fighters[0].Id, out var blocked) &&
              blocked.Contains("prevents this card category"),
            "A category-disable status should block the matching WIP skill in plan drafting.");
    }

    private static void AreaTargetDamageChecks()
    {
        var player = Fighter("area-player", 1200, 100);
        player.Skills[0].SourceTarget = "AOE";
        foreach (var rank in player.Skills[0].Ranks) rank.Effect.CoefficientBp = 10000;
        var first = Fighter("area-enemy-1", 1200, 25);
        var second = Fighter("area-enemy-2", 1200, 25);
        var third = Fighter("area-enemy-3", 1200, 25);
        first.BaseStats.Defense = 0;
        second.BaseStats.Defense = 20;
        third.BaseStats.Defense = 45;
        var battle = BattleEngine.Create("area-test", new[] { player }, null, new[] { first, second, third }, null,
            71, TeamSide.Player);
        var card = battle.Player.Hand.Find(item => item.SkillId == player.Skills[0].Id);
        Check(card != null && card.TargetScope == EffectTargetScope.AllEnemies,
            "AOE source target should be carried as an all-enemies card target scope.");
        var draft = new PlanDraft(battle);
        Check(draft.QueuePlay(card.Id, battle.Opponent.LivingActive()[0].Id, out var reason), reason);
        Check(BattleEngine.TryResolvePlan(battle, draft.BuildPlan("area-commit"), out var resolved, out var error), error);
        var hits = resolved.Events.FindAll(item => item.Kind == BattleEventKind.DamageApplied && item.CardId == card.Id);
        Check(hits.Count == 3, "An all-enemies skill should resolve one damage packet per active target.");
        Check(hits.Select(item => item.TargetId).Distinct().Count() == 3,
            "An all-enemies skill should record each target independently.");
        Check(hits.Select(item => item.Amount).Distinct().Count() == 3,
            "AOE damage should recalculate against each target's own defense.");
        var played = resolved.Events.Find(item => item.Kind == BattleEventKind.CardPlayed && item.CardId == card.Id);
        Check(played != null && played.TargetIds.Count == 3,
            "Card-play event should carry the full AOE target group for presentation.");
    }

    private static void PierceAndWeakpointRecipeChecks()
    {
        var state = Create(7741);
        var attacker = state.Player.Fighters[0];
        var target = state.Opponent.Fighters[0];
        attacker.Definition.BaseStats.PierceBp = 2000;
        var recipes = StandardEffectDatabase.CreateAttackEffects();
        var pierce = AttackEffectSystem.Prepare(recipes.Find(x => x.Id == "attack.pierce"), state,
            attacker, target, DamageFamily.Normal);
        Check(pierce.Attacker.PierceBp == 6000, "Pierce recipe was not applied to runtime attacker stats.");
        var weakpointRecipe = recipes.Find(x => x.Id == "attack.weakpoint");
        var inactive = AttackEffectSystem.Prepare(weakpointRecipe, state, attacker, target, DamageFamily.Normal);
        Check(inactive.KeywordFactorBp == 10000, "Weakpoint should be conditional before the target has a debuff.");
        StatusSystem.Apply(target, attacker.Id, TeamSide.Player, new StatusRecipeDefinition
        {
            Id = "test.debuff", Polarity = StatusPolarity.Debuff, DefaultDuration = 2,
            Tags = new List<string> { "test.debuff" }
        }, "test.debuff", "test.action", 1);
        var active = AttackEffectSystem.Prepare(weakpointRecipe, state, attacker, target, DamageFamily.Normal);
        Check(active.KeywordFactorBp == 30000, "Weakpoint recipe did not amplify damage against a debuffed target.");
    }

    private static void RejuvenationRuntimeChecks()
    {
        var state = Create(7742);
        var target = state.Player.Fighters[0];
        target.Health -= 100;
        var recipe = new StatusRecipeDefinition { Id = "status.buff.rejuvenation", Polarity = StatusPolarity.Buff,
            Behavior = StatusBehavior.Stat, Stacking = StatusStackingPolicy.AddStacks, MaxStacks = 3,
            DefaultDuration = 2, Modifiers = { new StatModifierDefinition { Target = ModifierTarget.Stat,
                Stat = StatId.Regeneration, Operation = ModifierOperation.PercentagePoints, Amount = 1000 } } };
        Check(StatusSystem.Apply(target, target.Id, target.Side, recipe, "rejuvenation", "test", 1,
            stackCount: 2, duration: 2).Accepted, "Rejuvenation status was not accepted.");
        Check(StatusSystem.GetEffectiveStats(target).RegenerationBp == 2000,
            "Rejuvenation stacks did not raise turn-start regeneration.");
        Check(BattleEngine.TryResolvePlan(state, new TurnPlan { RequestId = "rejuvenation-player-pass",
            ExpectedRevision = state.Revision }, out var opponentTurn, out var error), error);
        Check(BattleEngine.TryResolvePlan(opponentTurn, new TurnPlan { RequestId = "rejuvenation-opponent-pass",
            ExpectedRevision = opponentTurn.Revision }, out var advanced, out error), error);
        Check(advanced.Player.Fighters[0].Health > target.Health, "Rejuvenation did not produce runtime healing on the owner's turn.");
    }

    private static void CardRankIncreaseRuntimeChecks()
    {
        var state = BattleEngine.Create("rank-up", new[] { Fighter("mai") }, null,
            new[] { Fighter("enemy") }, null, 7743);
        var owner = state.Player.Fighters[0];
        owner.PowerGauge = CardRules.UltimateGaugeCost;
        owner.Definition.UltimateTiers[0].Effect.Sequence.Add(new CardEffectStep { Timing = CardEffectTiming.AfterDamage,
            Effect = new EffectDefinition { Kind = EffectKind.ModifyCardRank, Magnitude = 1, Target = "Self" } });
        var skillCard = Card("mai-skill", owner, 1);
        var ultimate = new CardState { Id = "mai-ultimate", OwnerFighterId = owner.Id, Kind = CardKind.Ultimate,
            UltimateTier = 0, TargetScope = EffectTargetScope.SelectedEnemy };
        state.Player.Hand = new List<CardState> { skillCard, ultimate };
        var draft = new PlanDraft(state);
        Check(draft.QueuePlay(ultimate.Id, state.Opponent.Fighters[0].Id, out var reason), reason);
        Check(BattleEngine.TryResolvePlan(state, draft.BuildPlan("rank-up-plan"), out var resolved, out var error), error);
        var ranked = resolved.Player.Hand.Find(card => card.Id == skillCard.Id);
        Check(ranked != null && ranked.Rank == 2, "Mai's rank-up effect did not update her remaining skill card.");
        var rankEvent = resolved.Events.Find(item => item.Kind == BattleEventKind.CardRankChanged && item.CardId == skillCard.Id);
        Check(rankEvent != null && rankEvent.Amount == 2, "Card rank increase did not emit a replayable event.");
        var display = state.Clone();
        BattlePlaybackState.Apply(display, rankEvent);
        Check(display.Player.Hand.Find(card => card.Id == skillCard.Id).Rank == 2,
            "Rank-up event did not update projected hand state.");
    }
}
