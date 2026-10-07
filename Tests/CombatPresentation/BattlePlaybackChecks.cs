using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Combat.AI;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;

internal static partial class BattlePlaybackChecks
{
    private static int _assertions;
    private static void Check(bool condition, string message)
    {
        _assertions++;
        if (!condition) throw new Exception(message);
    }

    public static void Main(string[] args)
    {
        TrainingChecks();
        if (args.Contains("--training")) { Console.WriteLine("PASS: " + _assertions + " training assertions."); return; }
        MultiHitChecks();
        ReserveEntryRefillChecks();
        InspectorPassiveChecks();
        ConditionalEffectAndCounterChecks();
        EnemyAiPlannerTacticalChecks();
        CardKindAndStanceChecks();
        ActionTimingChecks();
        CardDrawRandomnessUsesItsOwnStream();
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
        AuraSingleTriggerInRunEncounterChecks();
        StatusPlaybackStateTrackingChecks();
        AoeAndStatusTickSimultaneousResolutionChecks();
        MultiUltimateDrawChecks();
        NonDotDebuffAndDotTickChecks();
        StatusLibraryAndDurationChecks();
        KingSkill2PoisonChecks();
        CardTooltipAndKeywordChecks();
        DungeonFormationIsolationAndAbandonChecks();
        DungeonPolicyChecks();
        CheckWeakpointRuptureAndKingPierce();
        EvadeAndImmunityFeedbackChecks();
        YuriSkill2RefreshStrongerChecks();
        EffectTargetScopeEnumChecks();
        YuriUltShockTriggeringDamageChecks();
        Console.WriteLine("PASS: " + _assertions + " assertions: opening chronology, chained merges, full battle replay, enemy-first, death/reserve refill, seeded retargeting, draft reset, payload isolation, encounter IDs, move & merge PG, passive trigger, uncapped lifesteal, uncapped heal & damage, missing health recovery, single aura trigger, status display tracking, AOE & tick batching, multi-ultimate draw, non-DOT tick filter, status library, king poison, card tooltip keywords, dungeon formation isolation & abandon, additive pierce, weakpoint, rupture keywords, and evade/immunity feedback.");
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
        new[] { Fighter("e"), Fighter("f"), Fighter("g"), Fighter("h") }, null, seed, first,
        cardDrawSeed: seed);

    private static void CardDrawRandomnessUsesItsOwnStream()
    {
        var hands = new HashSet<string>();
        BattleState first = null;
        for (ulong cardSeed = 1; cardSeed <= 16; cardSeed++)
        {
            var battle = BattleEngine.Create("draw-stream:" + cardSeed,
                new[] { Fighter("draw-a"), Fighter("draw-b"), Fighter("draw-c"), Fighter("draw-d") }, null,
                new[] { Fighter("draw-e"), Fighter("draw-f"), Fighter("draw-g"), Fighter("draw-h") }, null,
                777, cardDrawSeed: cardSeed);
            first ??= battle;
            hands.Add(string.Join("|", battle.Player.Hand.Concat(battle.Opponent.Hand)
                .Select(card => card.OwnerFighterId + ":" + card.SkillId + ":" + card.Rank)));
            Check(battle.RngState == 777 && battle.RngDrawCount == 0,
                "Opening card draws consumed the seeded combat RNG stream.");
            Check(battle.CardRngDrawCount > 0, "Opening card draws did not consume the card RNG stream.");
        }
        Check(hands.Count > 1, "Changing only the card seed did not change the opening draw.");

        var replay = BattleEngine.Create("draw-stream:replay",
            new[] { Fighter("draw-a"), Fighter("draw-b"), Fighter("draw-c"), Fighter("draw-d") }, null,
            new[] { Fighter("draw-e"), Fighter("draw-f"), Fighter("draw-g"), Fighter("draw-h") }, null,
            999, cardDrawSeed: 1);
        Check(string.Join("|", first.Player.Hand.Concat(first.Opponent.Hand).Select(card => card.OwnerFighterId + ":" + card.SkillId + ":" + card.Rank)) ==
              string.Join("|", replay.Player.Hand.Concat(replay.Opponent.Hand).Select(card => card.OwnerFighterId + ":" + card.SkillId + ":" + card.Rank)),
            "A recorded card seed does not replay the same draw independently of the combat seed.");
        var clone = first.Clone();
        Check(clone.CardRngState == first.CardRngState && clone.CardRngDrawCount == first.CardRngDrawCount,
            "Battle cloning lost the card RNG replay state.");
    }

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
            var firstSide = seed % 2 == 0 ? TeamSide.Opponent : TeamSide.Player;
            var secondSide = firstSide == TeamSide.Player ? TeamSide.Opponent : TeamSide.Player;
            var initial = Create(seed, firstSide);
            foreach (var side in new[] { TeamSide.Player, TeamSide.Opponent })
            {
                var baseline = side == secondSide ? 1 : 0;
                foreach (var fighter in initial.Team(side).Fighters)
                {
                    var openingMerges = initial.Events.Count(e =>
                        e.Kind == BattleEventKind.CardsMerged && e.SourceId == fighter.Id);
                    Check(fighter.PowerGauge == Math.Min(CardRules.UltimateGaugeCost, baseline + openingMerges),
                        "Opening draw merges must grant 1 PG each: " + fighter.Id);
                }
            }
            var display = BattlePlaybackState.BeforeOpeningDeal(initial);
            Check(display.Player.Fighters.Concat(display.Opponent.Fighters).All(f => f.PowerGauge == 0),
                "Opening playback must begin before starting and merge PG is awarded.");
            for (var i = 0; i < initial.Events.Count; i++)
            {
                var item = initial.Events[i];
                if (item.Kind == BattleEventKind.CardsMerged)
                {
                    var owner = display.Player.FindFighter(item.SourceId) ?? display.Opponent.FindFighter(item.SourceId);
                    var hand = display.Team(owner.Side).Hand;
                    Check(hand.Exists(c => c.Id == item.CardId) && hand.Exists(c => c.Id == item.ConsumedCardId), "Merge precedes its two draws.");
                    midDealMerge |= initial.Events.Skip(i + 1).Any(e => e.Kind == BattleEventKind.CardDrawn && e.SourceId == item.SourceId);
                    Check(item.PowerGaugeAfter > 0, "Opening draw merge must emit its gained PG.");
                }
                BattlePlaybackState.Apply(display, item);
                if (item.Kind == BattleEventKind.CardsMerged)
                {
                    var owner = display.Player.FindFighter(item.SourceId) ?? display.Opponent.FindFighter(item.SourceId);
                    Check(owner.PowerGauge == item.PowerGaugeAfter,
                        "Opening merge playback must update the owner PG.");
                }
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
        // Expected heal: 400 missing HP * 5% * 1.0 * 0.3 = 6
        Check(healEvents[0].Amount == 6, "Turn-start recovery must heal based on missing health (expected 6, got " + healEvents[0].Amount + ")");

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

        // 2b. Card Heal FCT is uncapped by missing HP (even at 100% HP)
        var healPlayer = Fighter("p_heal", 1000, 200);
        var healCardSkill = new SkillDefinition {
            Id = "skill_heal", Slot = 1, Category = CardCategory.Recovery, TargetScope = EffectTargetScope.SelectedAlly
        };
        healCardSkill.Ranks.Add(new SkillRankDefinition {
            Rank = 1,
            Effect = new EffectDefinition {
                Kind = EffectKind.Heal,
                HealCoefficientBp = 10000, // 100% of ATK = 200
                Target = EffectTargetScope.SelectedAlly
            }
        });
        healPlayer.Skills[0] = healCardSkill;
        var healBattle = BattleEngine.Create("heal-uncapped-test", new[] { healPlayer }, null, new[] { Fighter("e_dummy", 1000, 10) }, null, 1);
        Check(healBattle.Player.Fighters[0].Health == 1000, "Heal recipient starts at full health (100% HP).");
        var healDraft = new PlanDraft(healBattle);
        var healCard = healDraft.Preview.Hand.Find(c => c.Category == CardCategory.Recovery);
        Check(healCard != null, "Heal card present in hand.");
        healDraft.QueuePlay(healCard.Id, healBattle.Player.Fighters[0].Id, out _);
        var healSession = new LocalBattleSession(healBattle);
        Check(healSession.Submit(healDraft.BuildPlan("turn1"), out var hErr), hErr);
        var hState = healSession.GetSnapshot();
        var hEvents = hState.Events.FindAll(e => e.Kind == BattleEventKind.HealApplied && e.Message == "Card effect.");
        Check(hEvents.Count > 0, "Card heal event must be emitted even if recipient is at 100% health.");
        Check(hEvents[0].Amount == 200, "Card heal event Amount must show raw heal amount (expected 200, got " + hEvents[0].Amount + ").");
        Check(hEvents[0].HealthAfter == 1000, "Recipient HP cannot exceed MaxHealth.");

        // 2c. Damage FCT is uncapped by target HP = 0% / low HP
        var dmgPlayer = Fighter("p_dmg", 1000, 500); // 500 ATK
        var lowHpEnemy = Fighter("e_low", 1000, 10);
        lowHpEnemy.BaseStats.Defense = 0;
        lowHpEnemy.BaseStats.ResistanceBp = 0;
        var dmgBattle = BattleEngine.Create("dmg-uncapped-test", new[] { dmgPlayer }, null, new[] { lowHpEnemy }, null, 1);
        dmgBattle.Opponent.Fighters[0].Health = 50; // Target only has 50 HP remaining
        var dmgDraft = new PlanDraft(dmgBattle);
        var attackCard = dmgDraft.Preview.Hand.Find(c => c.Category == CardCategory.Attack);
        Check(attackCard != null, "Attack card present in hand.");
        dmgDraft.QueuePlay(attackCard.Id, dmgBattle.Opponent.Fighters[0].Id, out _);
        var dmgSession = new LocalBattleSession(dmgBattle);
        Check(dmgSession.Submit(dmgDraft.BuildPlan("turn1"), out var dErr), dErr);
        var dState = dmgSession.GetSnapshot();
        var dEvents = dState.Events.FindAll(e => e.Kind == BattleEventKind.DamageApplied && e.CardId == attackCard.Id);
        Check(dEvents.Count > 0, "Damage event emitted.");
        Check(dEvents[0].Amount > 50, "Damage event Amount must show raw calculated damage uncapped by 50 HP (got " + dEvents[0].Amount + ").");
        Check(dEvents[0].HealthAfter == 0, "Target HP safely clamps to 0.");

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
              (blocked.Contains("prevents this card category") || blocked == StatusSystem.RecoveryBlockedMessage),
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
            Effect = new EffectDefinition { Kind = EffectKind.ModifyCardRank, Magnitude = 1, Target = EffectTargetScope.Self } });
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

    private static void AuraSingleTriggerInRunEncounterChecks()
    {
        var maiFighter = Fighter("fighter.mai94");
        maiFighter.Passive = StandardCharacterPassives.Create("fighter.mai94");
        maiFighter.BaseStats.RegenerationBp = 1000;
        var enemy = Fighter("enemy");
        enemy.Passive = new PassiveDefinition { Id = "passive.none" };

        var encounter = new EncounterProjection
        {
            BattleId = "enc:aura-test",
            PlayerTeam = new List<EncounterFighterSnapshot>
            {
                new EncounterFighterSnapshot
                {
                    FighterId = "Encounter:Player:0:mai94",
                    DefinitionId = "fighter.mai94",
                    Definition = maiFighter,
                    Stats = maiFighter.BaseStats.Clone(),
                    CurrentHealth = maiFighter.BaseStats.MaxHealth,
                    FormationSlot = 0,
                    IsReserve = false
                }
            },
            EnemyTeam = new List<EncounterFighterSnapshot>
            {
                new EncounterFighterSnapshot
                {
                    FighterId = "Encounter:Enemy:0:enemy",
                    DefinitionId = "fighter.enemy",
                    Definition = enemy,
                    Stats = enemy.BaseStats.Clone(),
                    CurrentHealth = enemy.BaseStats.MaxHealth,
                    FormationSlot = 0,
                    IsReserve = false
                }
            }
        };

        var battle = RunBattleBridge.CreateLocalBattle(encounter, 12345);
        var maiId = "Encounter:Player:0:mai94";
        var maiFighterState = battle.Player.FindFighter(maiId);
        Check(maiFighterState != null, "Fighter ID was not mapped in Player team.");

        Check(battle.ActivePassiveAuras.Any(a => a.StartsWith(maiId + "::")),
            "ActivePassiveAuras was not remapped to encounter fighter ID.");

        var passiveEvents = battle.Events.FindAll(e => e.Kind == BattleEventKind.PassiveTriggered && e.SourceId == maiId);
        Check(passiveEvents.Count == 1,
            $"Expected exactly 1 PassiveTriggered event for aura fighter, but got {passiveEvents.Count}.");
    }

    private static void StatusPlaybackStateTrackingChecks()
    {
        var state = Create(9999);
        var display = BattlePlaybackState.BeforeOpeningDeal(state);
        var targetId = state.Player.Fighters[0].Id;

        var applyEvent = new BattleEvent
        {
            Id = 1,
            Kind = BattleEventKind.StatusApplied,
            SourceId = state.Opponent.Fighters[0].Id,
            TargetId = targetId,
            StatusInstanceId = "status_inst_1",
            StatusRecipeId = "status.debuff.ignite",
            Amount = 1,
            Message = "status.debuff.ignite"
        };
        BattlePlaybackState.Apply(display, applyEvent);
        var targetFighter = display.Player.FindFighter(targetId);
        Check(targetFighter.Statuses != null && targetFighter.Statuses.Instances.Count == 1,
            "StatusApplied did not add status instance to display state.");
        Check(targetFighter.Statuses.Instances[0].RecipeId == "status.debuff.ignite",
            "StatusApplied recipe ID does not match.");
        Check(targetFighter.Statuses.Instances[0].StackCount == 1,
            "StatusApplied stack count should be 1.");

        var applySecondStack = new BattleEvent
        {
            Id = 2,
            Kind = BattleEventKind.StatusApplied,
            SourceId = state.Opponent.Fighters[0].Id,
            TargetId = targetId,
            StatusInstanceId = "status_inst_1",
            StatusRecipeId = "status.debuff.ignite",
            Amount = 1,
            Message = "status.debuff.ignite"
        };
        BattlePlaybackState.Apply(display, applySecondStack);
        Check(targetFighter.Statuses.Instances.Count == 1,
            "Adding a stack should not create duplicate status instance.");
        Check(targetFighter.Statuses.Instances[0].StackCount == 2,
            "Adding a stack should increase StackCount to 2.");

        var removeEvent = new BattleEvent
        {
            Id = 3,
            Kind = BattleEventKind.StatusRemoved,
            TargetId = targetId,
            StatusInstanceId = "status_inst_1",
            StatusRecipeId = "status.debuff.ignite"
        };
        BattlePlaybackState.Apply(display, removeEvent);
        Check(targetFighter.Statuses.Instances.Count == 0,
            "StatusRemoved did not remove status instance from display state.");

        // Stacking capped at max stack limit
        var applyBleed1 = new BattleEvent
        {
            Id = 4,
            Kind = BattleEventKind.StatusApplied,
            SourceId = state.Opponent.Fighters[0].Id,
            TargetId = targetId,
            StatusInstanceId = "bleed_inst_1",
            StatusRecipeId = "status.debuff.bleed",
            Amount = 1
        };
        var applyBleed2 = new BattleEvent
        {
            Id = 5,
            Kind = BattleEventKind.StatusApplied,
            SourceId = state.Opponent.Fighters[0].Id,
            TargetId = targetId,
            StatusInstanceId = "bleed_inst_2",
            StatusRecipeId = "status.debuff.bleed",
            Amount = 1
        };
        BattlePlaybackState.Apply(display, applyBleed1);
        BattlePlaybackState.Apply(display, applyBleed2);
        Check(targetFighter.Statuses.Instances.Count == 2,
            "Independent status instances should create separate entries in display state.");
        Check(targetFighter.Statuses.Instances.TrueForAll(s => s.RecipeId == "status.debuff.bleed"),
            "Both independent instances should share the same status recipe ID.");
        Check(targetFighter.Statuses.Instances.TrueForAll(s => s.StackCount == 1),
            "Independent statuses must not stack like data stacking; each instance must retain StackCount = 1.");
    }

    private static void AoeAndStatusTickSimultaneousResolutionChecks()
    {
        var player = Fighter("aoe-hero", 1000, 500);
        var enemy1 = Fighter("enemy-1", 100, 10);
        var enemy2 = Fighter("enemy-2", 1000, 10);
        var enemy3 = Fighter("enemy-3", 100, 10);
        var enemyReserve = Fighter("enemy-reserve", 500, 10);

        var battle = BattleEngine.Create("aoe-test", new[] { player }, null,
            new[] { enemy1, enemy2, enemy3, enemyReserve }, null, 42, TeamSide.Player);

        var card = battle.Player.Hand[0];
        card.TargetScope = EffectTargetScope.AllEnemies;

        var draft = new PlanDraft(battle);
        draft.QueuePlay(card.Id, enemy2.Id, out _);
        Check(BattleEngine.TryResolvePlan(battle, draft.BuildPlan("aoe-plan"), out var resolved, out var err), err);

        var events = resolved.Events;
        var firstDamageIdx = events.FindIndex(e => e.Kind == BattleEventKind.DamageApplied && e.CardId == card.Id);
        Check(firstDamageIdx >= 0, "No damage events found for AOE card.");

        var cardDamages = events.FindAll(e => e.Kind == BattleEventKind.DamageApplied && e.CardId == card.Id);
        Check(cardDamages.Count == 3, "Expected 3 damage events for 3 active enemies.");

        for (var i = 0; i < 3; i++)
        {
            Check(events[firstDamageIdx + i].Kind == BattleEventKind.DamageApplied &&
                  events[firstDamageIdx + i].CardId == card.Id,
                  $"AOE DamageApplied events are not contiguous at offset {i}.");
        }

        var firstDefeatIdx = events.FindIndex(e => e.Kind == BattleEventKind.FighterDefeated);
        Check(firstDefeatIdx > firstDamageIdx + 2,
            "FighterDefeated occurred before all AOE damage events completed!");

        var p1 = Fighter("tick-p1", 50, 10);
        var p2 = Fighter("tick-p2", 50, 10);
        var e1 = Fighter("tick-e1", 500, 1000);
        var tickBattle = BattleEngine.Create("tick-test", new[] { p1, p2 }, null, new[] { e1 }, null, 43, TeamSide.Player);

        var tickRecipe = new StatusRecipeDefinition
        {
            Id = "status.debuff.bleed",
            Polarity = StatusPolarity.Debuff,
            Behavior = StatusBehavior.DamageOverTime,
            DefaultDuration = 1,
            PeriodicDamage = new PeriodicDamageDefinition
            {
                Timing = StatusTickTiming.TargetTurnEnd,
                Scaling = StatusSnapshotScaling.Fixed,
                FixedAmount = 100,
                CoefficientBp = 10000,
                Family = DamageFamily.DamageOverTime
            }
        };

        var enemyId = tickBattle.Opponent.Fighters[0].Id;
        StatusSystem.Apply(tickBattle.Player.Fighters[0], enemyId, TeamSide.Opponent, tickRecipe, "bleed-1", "test", 1,
            snapshot: new StatusSnapshot { FixedAmount = 100 });
        StatusSystem.Apply(tickBattle.Player.Fighters[1], enemyId, TeamSide.Opponent, tickRecipe, "bleed-2", "test", 1,
            snapshot: new StatusSnapshot { FixedAmount = 100 });

        Check(BattleEngine.TryResolvePlan(tickBattle, new TurnPlan { RequestId = "pass1", ExpectedRevision = tickBattle.Revision },
            out var tickResolved, out var tickErr), tickErr);

        var tickEvents = tickResolved.Events;
        var firstTickDamageIdx = tickEvents.FindIndex(e => e.Kind == BattleEventKind.DamageApplied && e.Message != null && e.Message.Contains("status.debuff.bleed"));
        Check(firstTickDamageIdx >= 0, "No tick damage events found.");

        var tickDamages = tickEvents.FindAll(e => e.Kind == BattleEventKind.DamageApplied && e.Message != null && e.Message.Contains("status.debuff.bleed"));
        Check(tickDamages.Count == 2, "Expected 2 tick damage events.");

        Check(tickEvents[firstTickDamageIdx + 1].Kind == BattleEventKind.DamageApplied &&
              tickEvents[firstTickDamageIdx + 1].Message != null &&
              tickEvents[firstTickDamageIdx + 1].Message.Contains("status.debuff.bleed"),
              "Status tick damage events across fighters must be contiguous!");

        var tickDefeatIdx = tickEvents.FindIndex(e => e.Kind == BattleEventKind.FighterDefeated);
        Check(tickDefeatIdx > firstTickDamageIdx + 1,
            "FighterDefeated occurred before all status tick damages completed!");
    }

    private static void MultiUltimateDrawChecks()
    {
        var f1 = Fighter("hero-1", 1000, 100);
        var f2 = Fighter("hero-2", 1000, 100);
        var f3 = Fighter("hero-3", 1000, 100);
        var e1 = Fighter("boss", 2000, 100);

        var battle = BattleEngine.Create("multi-ult-test", new[] { f1, f2, f3 }, null, new[] { e1 }, null, 50, TeamSide.Player);

        // Clear initial hand so capacity is free
        battle.Player.Hand.Clear();

        // Give 2 fighters 5PG
        battle.Player.Fighters[0].PowerGauge = 5;
        battle.Player.Fighters[1].PowerGauge = 5;
        battle.Player.Fighters[2].PowerGauge = 2;

        var drawn = new List<CardState>();
        var timeline = new List<BattleEvent>();
        CardRules.StartTurn(battle.Player, new DeterministicRandom(100), drawnCards: drawn, timeline: timeline);

        var ultCards = battle.Player.Hand.FindAll(c => c.Kind == CardKind.Ultimate);
        Check(ultCards.Count == 2, $"Expected exactly 2 ultimates drawn for 2 ready fighters, got {ultCards.Count}");
        Check(ultCards.Exists(c => c.OwnerFighterId == battle.Player.Fighters[0].Id), "Hero 1 ultimate was not drawn");
        Check(ultCards.Exists(c => c.OwnerFighterId == battle.Player.Fighters[1].Id), "Hero 2 ultimate was not drawn");
    }

    private static void NonDotDebuffAndDotTickChecks()
    {
        var fighter = Fighter("target-fighter", 1000, 100);
        var attacker = Fighter("attacker-fighter", 1000, 200);
        var battle = BattleEngine.Create("status-tick-filter-test", new[] { fighter }, null, new[] { attacker }, null, 51, TeamSide.Player);

        var target = battle.Player.Fighters[0];
        var source = battle.Opponent.Fighters[0];

        // 1. Non-DoT debuff: decrease defense
        var defDebuff = StandardEffectDatabase.CreateStatusRecipes().Find(r => r.Id == "status.debuff.stat.defense-related");
        Check(defDebuff != null, "status.debuff.stat.defense-related recipe missing");
        Check((defDebuff.Behavior & StatusBehavior.DamageOverTime) == 0, "Defense debuff must not have DamageOverTime behavior");

        StatusSystem.Apply(target, source.Id, TeamSide.Opponent, defDebuff, "def-down-inst", "test", 1);
        var ticks = target.Statuses.CollectTicks(StatusTickTiming.TargetTurnEnd);
        Check(ticks.Count == 0, $"Non-DoT debuff must NOT produce any ticks, got {ticks.Count}");

        // 2. DoT debuff: poison
        var poisonRecipe = StandardEffectDatabase.CreateStatusRecipes().Find(r => r.Id == "status.debuff.poison");
        Check(poisonRecipe != null, "status.debuff.poison recipe missing");
        Check((poisonRecipe.Behavior & StatusBehavior.DamageOverTime) != 0, "Poison must have DamageOverTime behavior");

        var snapshot = new StatusSnapshot { SourceAttack = 200, TriggeringHealthDamage = 200, TargetMaxHealth = 1000 };
        StatusSystem.Apply(target, source.Id, TeamSide.Opponent, poisonRecipe, "poison-inst", "test", 2, snapshot: snapshot);

        var dotTicks = target.Statuses.CollectTicks(StatusTickTiming.TargetTurnEnd);
        Check(dotTicks.Count == 1, $"Poison must produce 1 tick, got {dotTicks.Count}");
        Check(dotTicks[0].Amount > 0, $"Poison tick amount must be positive, got {dotTicks[0].Amount}");

        // Verify damage resolver does not endure on positive tick
        var policy = StatusSystem.BuildDamagePolicy(source, target, dotTicks[0].Family);
        var result = DamageResolver.Resolve(new DamagePacket { BaseAmount = dotTicks[0].Amount, Policy = policy },
            StatusSystem.GetEffectiveStats(source), StatusSystem.GetEffectiveStats(target), target.Health, target.Shield, -1, -1);
        Check(!result.WasEndured, "Positive DoT tick damage should NOT be endured");
        Check(result.HealthLost > 0, "Poison tick must inflict health damage");
    }

    private static void StatusLibraryAndDurationChecks()
    {
        // 1. Display Names
        Check(StatusLibrary.GetDisplayName("status.debuff.poison") == "Poison", "Poison display name mismatch");
        Check(StatusLibrary.GetDisplayName("status.debuff.ignite") == "Ignite", "Ignite display name mismatch");
        Check(StatusLibrary.GetDisplayName("status.debuff.stat.defense") == "Decrease Defense", "Decrease Defense display name mismatch");
        Check(StatusLibrary.GetDisplayName("status.buff.stat.attack") == "Increase Attack", "Increase Attack display name mismatch");
        Check(StatusLibrary.GetDisplayName("status.buff.debuffimmunity") == "Debuff Immunity", "Debuff Immunity display name mismatch");
        Check(StatusLibrary.GetDisplayName("status.debuff.recoveryrate") == "Decrease Recovery", "Decrease Recovery display name mismatch");
        Check(StatusLibrary.GetDisplayName("status.buff.rejuvenation") == "Rejuvenation", "Rejuvenation display name mismatch");
        Check(StatusLibrary.GetDisplayName("status.debuff.disable-recovery") == "Disable Recovery", "Disable Recovery display name mismatch");
        Check(StatusLibrary.GetDisplayName("status.debuff.disable-stance") == "Disable Stance", "Disable Stance display name mismatch");

        // 2. Icon Keys
        Check(StatusLibrary.GetIconKey("status.debuff.poison", StatusPolarity.Debuff) == "Cardtype_Debuffatk", "Poison icon key mismatch");
        Check(StatusLibrary.GetIconKey("status.debuff.stat.defense", StatusPolarity.Debuff) == "Cardtype_Debuff", "Defense debuff icon key mismatch");
        Check(StatusLibrary.GetIconKey("status.buff.stat.attack", StatusPolarity.Buff) == "sword", "Attack buff icon key mismatch");
        Check(StatusLibrary.GetIconKey("status.buff.stat.defense", StatusPolarity.Buff) == "shield", "Defense buff icon key mismatch");
        Check(StatusLibrary.GetIconKey("status.buff.rejuvenation", StatusPolarity.Buff) == "icon_buff_heal_dot_heal", "Rejuvenation icon key mismatch");
        Check(StatusLibrary.GetIconKey("status.debuff.recoveryrate", StatusPolarity.Debuff) == "icon_buff_explosion_debuff_add_per", "Recovery debuff icon key mismatch");
        Check(StatusLibrary.GetIconKey("status.debuff.disable-recovery", StatusPolarity.Debuff) == "icon_buff_cc_dis_heal_skill", "Disable Recovery icon key mismatch");
        Check(StatusLibrary.GetIconKey("status.debuff.disable-stance", StatusPolarity.Debuff) == "icon_buff_cc_dis_pose_skill", "Disable Stance icon key mismatch");

        // 3. Playback State Remaining Duration & Expiration on TurnEnded
        var state = Create(9998);
        var display = BattlePlaybackState.BeforeOpeningDeal(state);
        var targetId = state.Player.Fighters[0].Id;
        var sourceId = state.Opponent.Fighters[0].Id;

        var applyEvent = new BattleEvent
        {
            Id = 1,
            Kind = BattleEventKind.StatusApplied,
            SourceId = sourceId,
            TargetId = targetId,
            StatusInstanceId = "dur_status_1",
            StatusRecipeId = "status.debuff.poison",
            Amount = 1
        };
        BattlePlaybackState.Apply(display, applyEvent);
        var target = display.Player.FindFighter(targetId);
        Check(target.Statuses.Instances.Count == 1, "Poison not added to display state");
        Check(target.Statuses.Instances[0].RemainingDuration > 0, "RemainingDuration was not initialized");

        var initialDuration = target.Statuses.Instances[0].RemainingDuration;

        // Playback must use authority duration snapshots, rather than guessing from a turn event.
        display.ActingSide = TeamSide.Player;
        BattlePlaybackState.Apply(display, new BattleEvent { Kind = BattleEventKind.TurnEnded, SourceId = TeamSide.Player.ToString() });
        Check(target.Statuses.Instances[0].RemainingDuration == initialDuration, "Turn event must not independently advance clocks");
        var afterTick = target.Statuses.Instances.ConvertAll(status => status.Clone());
        afterTick[0].RemainingDuration--;
        BattlePlaybackState.Apply(display, new BattleEvent { Kind = BattleEventKind.StatusesChanged,
            TargetId = targetId, StatusesAfter = afterTick });
        Check(target.Statuses.Instances[0].RemainingDuration == initialDuration - 1, "Authority duration update was not applied");
        BattlePlaybackState.Apply(display, new BattleEvent { Kind = BattleEventKind.StatusRemoved,
            TargetId = targetId, StatusesAfter = new List<StatusInstance>() });
        Check(target.Statuses.Instances.Count == 0, "Expired status snapshot must clear the displayed status");

        // Verify status cooldown overlay fill ratio from 0 to 1 (0 = full duration, 1 = expired)
        static float CalcCooldownRatio(int remaining, int maxDuration, bool isPermanent)
        {
            if (isPermanent) return 0f;
            if (maxDuration <= 0) return 0f;
            return Math.Clamp((float)(maxDuration - remaining) / maxDuration, 0f, 1f);
        }

        Check(CalcCooldownRatio(1, 1, false) == 0f, "1-turn debuff on application must have 0 cooldown overlay ratio (full duration)");
        Check(CalcCooldownRatio(2, 2, false) == 0f, "Full duration status must have 0 cooldown overlay ratio");
        Check(Math.Abs(CalcCooldownRatio(1, 2, false) - 0.5f) < 0.001f, "Half elapsed status must have 0.5 cooldown overlay ratio");
        Check(CalcCooldownRatio(0, 2, false) == 1f, "Expired status must have 1.0 cooldown overlay ratio");
        Check(CalcCooldownRatio(100, 100, true) == 0f, "Permanent status must have 0 cooldown overlay ratio");
        Check(Math.Abs(CalcCooldownRatio(2, 3, false) - (1f / 3f)) < 0.001f, "1 turn elapsed of 3 must have 1/3 cooldown overlay ratio");
        Check(Math.Abs(CalcCooldownRatio(1, 3, false) - (2f / 3f)) < 0.001f, "2 turns elapsed of 3 must have 2/3 cooldown overlay ratio");

        // Verify status stack instantiation count
        static List<string> ExpandStackKeys(string instanceId, int stackCount)
        {
            var keys = new List<string>();
            var count = Math.Max(1, stackCount);
            for (var s = 0; s < count; s++) keys.Add($"{instanceId}_{s}");
            return keys;
        }

        var singleKeys = ExpandStackKeys("bleed", 1);
        Check(singleKeys.Count == 1 && singleKeys[0] == "bleed_0", "Single stack must instantiate 1 prefab key");
        var multiKeys = ExpandStackKeys("ignite", 3);
        Check(multiKeys.Count == 3 && multiKeys[2] == "ignite_2", "3-stack status must instantiate 3 separate prefab keys");
    }

    private static void KingSkill2PoisonChecks()
    {
        var poisonRecipe = StandardEffectDatabase.CreateStatusRecipes().Find(r => r.Id == "status.debuff.poison");
        Check(poisonRecipe != null, "Poison recipe missing");

        var kingSkill = new SkillDefinition
        {
            Id = "fighter.king94.skill.2",
            Slot = 2,
            Category = CardCategory.AttackDebuff,
            TargetScope = EffectTargetScope.SelectedEnemy
        };
        var rank1Effect = new EffectDefinition
        {
            Kind = EffectKind.Damage,
            Scaling = StatScaling.Attack,
            CoefficientBp = 12500,
            Sequence = new List<CardEffectStep>
            {
                new CardEffectStep
                {
                    Timing = CardEffectTiming.AfterDamage,
                    Effect = new EffectDefinition
                    {
                        Kind = EffectKind.ApplyStatus,
                        Target = EffectTargetScope.SelectedEnemy,
                        StatusDurationOverride = 1,
                        StatusRecipe = poisonRecipe
                    }
                }
            }
        };
        kingSkill.Ranks.Add(new SkillRankDefinition { Rank = 1, Effect = rank1Effect });
        kingSkill.Ranks.Add(new SkillRankDefinition { Rank = 2, Effect = rank1Effect });
        kingSkill.Ranks.Add(new SkillRankDefinition { Rank = 3, Effect = rank1Effect });

        var king = Fighter("fighter.king94", 1000, 300);
        king.Skills.Clear();
        king.Skills.Add(kingSkill);

        var dummy = Fighter("dummy", 1000, 50);

        var battle = BattleEngine.Create("king-poison-test", new[] { king }, null, new[] { dummy }, null, 60, TeamSide.Player);

        var card = battle.Player.Hand.Find(c => c.SkillId == "fighter.king94.skill.2");
        if (card == null)
        {
            card = new CardState
            {
                Id = "king-card-1",
                OwnerFighterId = battle.Player.Fighters[0].Id,
                SkillId = "fighter.king94.skill.2",
                Rank = 1,
                Kind = CardKind.Skill,
                Category = CardCategory.AttackDebuff,
                TargetScope = EffectTargetScope.SelectedEnemy
            };
            battle.Player.Hand.Insert(0, card);
        }

        var draft = new PlanDraft(battle);
        var targetFighterId = battle.Opponent.Fighters[0].Id;
        Check(draft.QueuePlay(card.Id, targetFighterId, out var playErr), playErr);
        Check(BattleEngine.TryResolvePlan(battle, draft.BuildPlan("king-poison-plan"), out var resolved, out var err), err);

        var statusAppliedEvents = resolved.Events.FindAll(e => e.Kind == BattleEventKind.StatusApplied && e.TargetId == battle.Opponent.Fighters[0].Id);
        Check(statusAppliedEvents.Count == 1, $"Expected 1 StatusApplied event for Poison on target, got {statusAppliedEvents.Count}");
        Check(statusAppliedEvents[0].StatusRecipeId == "status.debuff.poison", "Status applied was not poison");

        var enemyTarget = resolved.Opponent.Fighters[0];
        Check(enemyTarget.Statuses.Instances.Exists(s => s.RecipeId == "status.debuff.poison"), "Enemy target does not have poison status instance");
    }

    private static void CardTooltipAndKeywordChecks()
    {
        // 1. Status names and keywords verification
        Check(StatusLibrary.GetDisplayName("status.debuff.ignite") == "Ignite", "Ignite display name mismatch");
        Check(StatusLibrary.GetDisplayName("status.debuff.poison") == "Poison", "Poison display name mismatch");
        Check(StatusLibrary.GetDisplayName("status.debuff.bleed") == "Bleed", "Bleed display name mismatch");
        Check(StatusLibrary.GetDisplayName("status.debuff.shock") == "Shock", "Shock display name mismatch");

        // 2. King and Chin status recipes verification
        var recipes = StandardEffectDatabase.CreateStatusRecipes();
        var igniteRecipe = recipes.Find(r => r.Id == "status.debuff.ignite");
        Check(igniteRecipe != null, "Ignite status recipe missing");
        Check(igniteRecipe.Polarity == StatusPolarity.Debuff, "Ignite polarity should be debuff");
        Check(igniteRecipe.Tags.Contains(CombatTags.Ignite), "Ignite tag missing");

        var poisonRecipe = recipes.Find(r => r.Id == "status.debuff.poison");
        Check(poisonRecipe != null, "Poison status recipe missing");
        Check(poisonRecipe.Polarity == StatusPolarity.Debuff, "Poison polarity should be debuff");
        Check(poisonRecipe.Tags.Contains(CombatTags.Poison), "Poison tag missing");

        // 3. Attack effect keywords verification
        var attackEffects = StandardEffectDatabase.CreateAttackEffects();
        var pierceEffect = attackEffects.Find(a => a.Kind == AttackEffectKind.Pierce);
        Check(pierceEffect != null, "Pierce attack effect recipe missing");
        Check(pierceEffect.PrimaryValueBp == 30000, "Pierce multiplier should be 30000 (3x)");

        var ruptureEffect = attackEffects.Find(a => a.Kind == AttackEffectKind.Rupture);
        Check(ruptureEffect != null, "Rupture attack effect recipe missing");
        Check(ruptureEffect.PrimaryValueBp == 20000, "Rupture multiplier should be 20000 (2x)");
    }

    private static void DungeonFormationIsolationAndAbandonChecks()
    {
        var run = new RunState
        {
            RunId = "run-test-abandon",
            ProfileId = "dungeon.green-accord",
            Revision = 1,
            Status = RunStatus.InProgress,
            PreRunFormation = new List<string> { "char-1", "char-2", "", "" }
        };

        var cloned = run.Clone();
        Check(cloned.PreRunFormation != null && cloned.PreRunFormation.Count == 4, "PreRunFormation was not cloned correctly.");
        Check(cloned.PreRunFormation[0] == "char-1" && cloned.PreRunFormation[1] == "char-2", "PreRunFormation elements diverged in clone.");

        // Simulate abandoning the run
        Check(DungeonRunEngine.TryAbandon(run, run.Revision, "abandon-req-1", out var next, out var error), error);
        Check(next.Status == RunStatus.Abandoned, "Run status should be Abandoned.");
        Check(next.PreRunFormation != null && next.PreRunFormation.Count == 4, "PreRunFormation should persist through Abandon command.");
        Check(next.PreRunFormation[0] == "char-1", "PreRunFormation should retain original pre-run saved team.");
    }

    private static void CheckWeakpointRuptureAndKingPierce()
    {
        // 1. King Pierce test with additive formula:
        // King ATK = 375, Pierce = 89% (8900 bp), Skill 1 Rank 2: 300% ATK with Pierce (3x Pierce -> 26700 bp).
        // Enemy DEF = 150, Resistance = 20% (2000 bp).
        // Additive formula:
        // Skill Damage = max(0, 375 * 3.0 - 150) = 975
        // Pierce Damage = max(0, 375 * 2.67 - 150 * 0.20) = 1001.25 - 30 = 971.25
        // Total Base = 2051.25 - 150 = 1901.25 -> 1901
        var kingStats = new StatBlock { Attack = 375, PierceBp = 8900 };
        var enemyStats = new StatBlock { Defense = 150, ResistanceBp = 2000 };
        var kingPacket = new DamagePacket
        {
            BaseAmount = kingStats.Attack,
            CoefficientBp = 30000,
            KeywordFactorBp = 10000,
            Policy = new DamagePolicy()
        };
        var kingPreparedStats = kingStats.Clone();
        kingPreparedStats.PierceBp = 8900 * 3;
        var kingDmgCurrent = DamageResolver.Resolve(kingPacket, kingPreparedStats, enemyStats, 10000, 0, -1, -1);
        Check(kingDmgCurrent.CalculatedDamage == 1901,
            $"King Pierce damage must follow additive 7DSGC formula (expected 1946, got {kingDmgCurrent.CalculatedDamage})");

        // 2. Kyo Weakpoint test in real battle: 100% ATK, 3x on debuffed target
        var kyoSkill = new SkillDefinition { Id = "fighter.kyo94.skill.1", Slot = 1, Category = CardCategory.Attack, TargetScope = EffectTargetScope.SelectedEnemy };
        kyoSkill.Ranks.Add(new SkillRankDefinition
        {
            Rank = 1,
            Effect = new EffectDefinition { Kind = EffectKind.Damage, Scaling = StatScaling.Attack, CoefficientBp = 10000, KeywordId = "attack.weakpoint", KeywordFactorBp = 10000 }
        });
        var kyoFighter = Fighter("fighter.kyo94", 1000, 300);
        kyoFighter.Skills.Clear();
        kyoFighter.Skills.Add(kyoSkill);

        var dummyA = Fighter("dummyA", 10000, 50);
        dummyA.BaseStats.Defense = 50;
        var battleKyoNoDebuff = BattleEngine.Create("kyo-no-debuff", new[] { kyoFighter }, null, new[] { dummyA }, null, 1, TeamSide.Player);
        var cardKyo1 = new CardState { Id = "card-kyo-1", OwnerFighterId = battleKyoNoDebuff.Player.Fighters[0].Id, SkillId = "fighter.kyo94.skill.1", Rank = 1, Kind = CardKind.Skill, Category = CardCategory.Attack, TargetScope = EffectTargetScope.SelectedEnemy };
        battleKyoNoDebuff.Player.Hand.Clear();
        battleKyoNoDebuff.Player.Hand.Add(cardKyo1);
        var draftKyo1 = new PlanDraft(battleKyoNoDebuff);
        draftKyo1.QueuePlay(cardKyo1.Id, battleKyoNoDebuff.Opponent.Fighters[0].Id, out _);
        BattleEngine.TryResolvePlan(battleKyoNoDebuff, draftKyo1.BuildPlan("plan1"), out var resolvedKyoNoDebuff, out _);
        var dmgNoDebuff = resolvedKyoNoDebuff.Events.Find(e => e.Kind == BattleEventKind.DamageApplied);
        // Base ATK 300, DEF 50: 300 * 1.0 - 50 = 250
        Check(dmgNoDebuff != null && dmgNoDebuff.Amount == 250, $"Kyo Weakpoint without debuff expected 250, got {dmgNoDebuff?.Amount}");

        var dummyB = Fighter("dummyB", 10000, 50);
        dummyB.BaseStats.Defense = 50;
        var battleKyoWithDebuff = BattleEngine.Create("kyo-with-debuff", new[] { kyoFighter }, null, new[] { dummyB }, null, 1, TeamSide.Player);
        var igniteRecipe = StandardEffectDatabase.CreateStatusRecipes().Find(r => r.Id == "status.debuff.ignite");
        StatusSystem.Apply(battleKyoWithDebuff.Opponent.Fighters[0], "chin", TeamSide.Player, igniteRecipe, "ignite-1", "test", 1);
        var cardKyo2 = new CardState { Id = "card-kyo-2", OwnerFighterId = battleKyoWithDebuff.Player.Fighters[0].Id, SkillId = "fighter.kyo94.skill.1", Rank = 1, Kind = CardKind.Skill, Category = CardCategory.Attack, TargetScope = EffectTargetScope.SelectedEnemy };
        battleKyoWithDebuff.Player.Hand.Clear();
        battleKyoWithDebuff.Player.Hand.Add(cardKyo2);
        var draftKyo2 = new PlanDraft(battleKyoWithDebuff);
        draftKyo2.QueuePlay(cardKyo2.Id, battleKyoWithDebuff.Opponent.Fighters[0].Id, out _);
        BattleEngine.TryResolvePlan(battleKyoWithDebuff, draftKyo2.BuildPlan("plan2"), out var resolvedKyoWithDebuff, out _);
        var dmgWithDebuff = resolvedKyoWithDebuff.Events.Find(e => e.Kind == BattleEventKind.DamageApplied);
        // Card coeff becomes 300%: 300 * 3.0 - 50 = 850. Ignite adds 10% incoming: 850 * 1.1 = 935
        Check(dmgWithDebuff != null && dmgWithDebuff.Amount == 935, $"Kyo Weakpoint with debuff expected 935, got {dmgWithDebuff?.Amount}");
        Check(dmgWithDebuff.Amount > dmgNoDebuff.Amount * 3, "Weakpoint with debuff must deal over 3x damage compared to no debuff.");

        // 3. Mai Rupture test: 200% ATK, 2x against BUFFED target (NOT debuffed)
        var maiSkill = new SkillDefinition { Id = "fighter.mai94.skill.1", Slot = 1, Category = CardCategory.Attack, TargetScope = EffectTargetScope.SelectedEnemy };
        maiSkill.Ranks.Add(new SkillRankDefinition
        {
            Rank = 1,
            Effect = new EffectDefinition { Kind = EffectKind.Damage, Scaling = StatScaling.Attack, CoefficientBp = 20000, KeywordId = "attack.rupture", KeywordFactorBp = 10000 }
        });
        var maiFighter = Fighter("fighter.mai94", 1000, 300);
        maiFighter.Skills.Clear();
        maiFighter.Skills.Add(maiSkill);

        var dummyC = Fighter("dummyC", 10000, 50);
        dummyC.BaseStats.Defense = 50;
        var battleMaiNoBuff = BattleEngine.Create("mai-no-buff", new[] { maiFighter }, null, new[] { dummyC }, null, 1, TeamSide.Player);
        var cardMai1 = new CardState { Id = "card-mai-1", OwnerFighterId = battleMaiNoBuff.Player.Fighters[0].Id, SkillId = "fighter.mai94.skill.1", Rank = 1, Kind = CardKind.Skill, Category = CardCategory.Attack, TargetScope = EffectTargetScope.SelectedEnemy };
        battleMaiNoBuff.Player.Hand.Clear();
        battleMaiNoBuff.Player.Hand.Add(cardMai1);
        var draftMai1 = new PlanDraft(battleMaiNoBuff);
        draftMai1.QueuePlay(cardMai1.Id, battleMaiNoBuff.Opponent.Fighters[0].Id, out _);
        BattleEngine.TryResolvePlan(battleMaiNoBuff, draftMai1.BuildPlan("plan3"), out var resolvedMaiNoBuff, out _);
        var dmgMaiNoBuff = resolvedMaiNoBuff.Events.Find(e => e.Kind == BattleEventKind.DamageApplied);
        // Base ATK 300, 200% coeff, DEF 50: 300 * 2.0 - 50 = 550
        Check(dmgMaiNoBuff != null && dmgMaiNoBuff.Amount == 550, $"Mai Rupture without buff expected 550, got {dmgMaiNoBuff?.Amount}");

        // Mai Rupture vs Debuffed target (should NOT trigger 2x, since Rupture triggers on Buffs)
        var dummyDebuffOnly = Fighter("dummyDebuffOnly", 10000, 50);
        dummyDebuffOnly.BaseStats.Defense = 50;
        var battleMaiDebuffOnly = BattleEngine.Create("mai-debuff-only", new[] { maiFighter }, null, new[] { dummyDebuffOnly }, null, 1, TeamSide.Player);
        StatusSystem.Apply(battleMaiDebuffOnly.Opponent.Fighters[0], "chin", TeamSide.Player, igniteRecipe, "ignite-2", "test", 1);
        var cardMaiDebuff = new CardState { Id = "card-mai-debuff", OwnerFighterId = battleMaiDebuffOnly.Player.Fighters[0].Id, SkillId = "fighter.mai94.skill.1", Rank = 1, Kind = CardKind.Skill, Category = CardCategory.Attack, TargetScope = EffectTargetScope.SelectedEnemy };
        battleMaiDebuffOnly.Player.Hand.Clear();
        battleMaiDebuffOnly.Player.Hand.Add(cardMaiDebuff);
        var draftMaiDebuff = new PlanDraft(battleMaiDebuffOnly);
        draftMaiDebuff.QueuePlay(cardMaiDebuff.Id, battleMaiDebuffOnly.Opponent.Fighters[0].Id, out _);
        BattleEngine.TryResolvePlan(battleMaiDebuffOnly, draftMaiDebuff.BuildPlan("plan-debuff"), out var resolvedMaiDebuff, out _);
        var dmgMaiDebuff = resolvedMaiDebuff.Events.Find(e => e.Kind == BattleEventKind.DamageApplied);
        // Still 1x keyword factor (200% coeff): 300 * 2.0 - 50 = 550, with ignite (+10%): 550 * 1.1 = 605
        Check(dmgMaiDebuff != null && dmgMaiDebuff.Amount == 605, $"Mai Rupture against debuff only should not trigger Rupture (expected 605, got {dmgMaiDebuff?.Amount})");

        // Mai Rupture vs Buffed target (triggers 2x Rupture: 200% * 2 = 400%)
        var dummyD = Fighter("dummyD", 10000, 50);
        dummyD.BaseStats.Defense = 50;
        var battleMaiWithBuff = BattleEngine.Create("mai-with-buff", new[] { maiFighter }, null, new[] { dummyD }, null, 1, TeamSide.Player);
        var buffRecipe = new StatusRecipeDefinition { Id = "status.buff.stat.attack", Polarity = StatusPolarity.Buff, DefaultDuration = 2 };
        StatusSystem.Apply(battleMaiWithBuff.Opponent.Fighters[0], "dummyD", TeamSide.Opponent, buffRecipe, "buff-1", "test", 1);
        var cardMai2 = new CardState { Id = "card-mai-2", OwnerFighterId = battleMaiWithBuff.Player.Fighters[0].Id, SkillId = "fighter.mai94.skill.1", Rank = 1, Kind = CardKind.Skill, Category = CardCategory.Attack, TargetScope = EffectTargetScope.SelectedEnemy };
        battleMaiWithBuff.Player.Hand.Clear();
        battleMaiWithBuff.Player.Hand.Add(cardMai2);
        var draftMai2 = new PlanDraft(battleMaiWithBuff);
        draftMai2.QueuePlay(cardMai2.Id, battleMaiWithBuff.Opponent.Fighters[0].Id, out _);
        BattleEngine.TryResolvePlan(battleMaiWithBuff, draftMai2.BuildPlan("plan4"), out var resolvedMaiWithBuff, out _);
        var dmgMaiWithBuff = resolvedMaiWithBuff.Events.Find(e => e.Kind == BattleEventKind.DamageApplied);
        // Card coeff becomes 400%: 300 * 4.0 - 50 = 1150
        Check(dmgMaiWithBuff != null && dmgMaiWithBuff.Amount == 1150, $"Mai Rupture with buff expected 1150, got {dmgMaiWithBuff?.Amount}");
        Check(dmgMaiWithBuff.Amount > dmgMaiNoBuff.Amount * 2, "Rupture with buff must deal over 2x damage compared to no buff.");

        // 4. Chin AOE Weakpoint Ultimate test
        var chinUltEffect = new EffectDefinition
        {
            Kind = EffectKind.Damage,
            Scaling = StatScaling.Attack,
            CoefficientBp = 21000,
            KeywordId = "attack.weakpoint",
            KeywordFactorBp = 10000,
            Target = EffectTargetScope.AllEnemies,
            Sequence = new List<CardEffectStep>
            {
                new CardEffectStep
                {
                    Timing = CardEffectTiming.AfterDamage,
                    Effect = new EffectDefinition
                    {
                        Kind = EffectKind.ApplyStatus,
                        StatusRecipe = igniteRecipe,
                        StatusStackCount = 1,
                        StatusDurationOverride = 2
                    }
                }
            }
        };
        var chinFighter = Fighter("fighter.chin94", 4400, 320);
        chinFighter.UltimateTiers.Clear();
        chinFighter.UltimateTiers.Add(new UltimateTierDefinition
        {
            Tier = 0,
            Category = CardCategory.Attack,
            Effect = chinUltEffect
        });

        var dummyEnemy1 = Fighter("dummyEnemy1", 10000, 50);
        dummyEnemy1.BaseStats.Defense = 50;
        var dummyEnemy2 = Fighter("dummyEnemy2", 10000, 50);
        dummyEnemy2.BaseStats.Defense = 50;

        var battleChin = BattleEngine.Create("chin-test", new[] { chinFighter }, null, new[] { dummyEnemy1, dummyEnemy2 }, null, 1, TeamSide.Player);
        battleChin.Player.Fighters[0].PowerGauge = 5;
        StatusSystem.Apply(battleChin.Opponent.Fighters[0], "chin", TeamSide.Player, igniteRecipe, "ignite-chin-1", "test", 1);

        var cardChinUlt = new CardState
        {
            Id = "card-chin-ult",
            OwnerFighterId = battleChin.Player.Fighters[0].Id,
            Rank = 1,
            Kind = CardKind.Ultimate,
            Category = CardCategory.Attack,
            TargetScope = EffectTargetScope.AllEnemies,
            UltimateTier = 0
        };
        battleChin.Player.Hand.Clear();
        battleChin.Player.Hand.Add(cardChinUlt);

        var draftChin = new PlanDraft(battleChin);
        draftChin.QueuePlay(cardChinUlt.Id, battleChin.Opponent.Fighters[0].Id, out _);
        BattleEngine.TryResolvePlan(battleChin, draftChin.BuildPlan("chin-plan"), out var resolvedChin, out _);
        var chinDmgEvents = resolvedChin.Events.FindAll(e => e.Kind == BattleEventKind.DamageApplied && e.CardId == cardChinUlt.Id);
        Check(chinDmgEvents.Count == 2, "Chin AOE ultimate must hit both enemies.");
        var debuffedHit = chinDmgEvents.Find(e => e.TargetId == battleChin.Opponent.Fighters[0].Id);
        var cleanHit = chinDmgEvents.Find(e => e.TargetId == battleChin.Opponent.Fighters[1].Id);
        Check(debuffedHit != null && debuffedHit.Amount == 2162, $"Debuffed enemy must take full Weakpoint damage (expected 2162, got {debuffedHit?.Amount}).");
        Check(cleanHit != null && cleanHit.Amount == 622, $"Clean enemy must take base damage without Weakpoint (expected 622, got {cleanHit?.Amount}).");

        // Case B: Same-turn queue: Action 1 = Chin Skill 1 (applies Ignite), Action 2 = Chin Ultimate
        var chinSkill1Effect = new EffectDefinition
        {
            Kind = EffectKind.Damage,
            Scaling = StatScaling.Attack,
            CoefficientBp = 15000,
            Target = EffectTargetScope.SelectedEnemy,
            Sequence = new List<CardEffectStep>
            {
                new CardEffectStep
                {
                    Timing = CardEffectTiming.AfterAction,
                    Effect = new EffectDefinition
                    {
                        Kind = EffectKind.ApplyStatus,
                        StatusRecipe = igniteRecipe,
                        StatusStackCount = 1,
                        StatusDurationOverride = 2
                    }
                }
            }
        };
        chinFighter.Skills.Clear();
        var skill1Def = new SkillDefinition { Id = "fighter.chin94:skill:1", Slot = 1 };
        skill1Def.Ranks.Add(new SkillRankDefinition { Rank = 1, Effect = chinSkill1Effect });
        chinFighter.Skills.Add(skill1Def);

        var battleChinSameTurn = BattleEngine.Create("chin-same-turn", new[] { chinFighter }, null, new[] { dummyEnemy1, dummyEnemy2 }, null, 1, TeamSide.Player);
        battleChinSameTurn.Player.Fighters[0].PowerGauge = 5;

        var cardSkill1 = new CardState
        {
            Id = "card-chin-skill1",
            OwnerFighterId = battleChinSameTurn.Player.Fighters[0].Id,
            SkillId = "fighter.chin94:skill:1",
            Rank = 1,
            Kind = CardKind.Skill,
            Category = CardCategory.Attack,
            TargetScope = EffectTargetScope.SelectedEnemy
        };
        var cardChinUlt2 = new CardState
        {
            Id = "card-chin-ult2",
            OwnerFighterId = battleChinSameTurn.Player.Fighters[0].Id,
            Rank = 1,
            Kind = CardKind.Ultimate,
            Category = CardCategory.Attack,
            TargetScope = EffectTargetScope.AllEnemies,
            UltimateTier = 0
        };
        battleChinSameTurn.Player.Hand.Clear();
        battleChinSameTurn.Player.Hand.Add(cardSkill1);
        battleChinSameTurn.Player.Hand.Add(cardChinUlt2);

        var draftSameTurn = new PlanDraft(battleChinSameTurn);
        draftSameTurn.QueuePlay(cardSkill1.Id, battleChinSameTurn.Opponent.Fighters[0].Id, out _);
        draftSameTurn.QueuePlay(cardChinUlt2.Id, battleChinSameTurn.Opponent.Fighters[0].Id, out _);
        BattleEngine.TryResolvePlan(battleChinSameTurn, draftSameTurn.BuildPlan("same-turn-plan"), out var resolvedSameTurn, out _);

        var ultHitsSameTurn = resolvedSameTurn.Events.FindAll(e => e.Kind == BattleEventKind.DamageApplied && e.CardId == cardChinUlt2.Id);
        Check(ultHitsSameTurn.Count == 2, "Chin same-turn combo AOE ultimate must hit both enemies.");
        var sameTurnDebuffedHit = ultHitsSameTurn.Find(e => e.TargetId == battleChinSameTurn.Opponent.Fighters[0].Id);
        var sameTurnCleanHit = ultHitsSameTurn.Find(e => e.TargetId == battleChinSameTurn.Opponent.Fighters[1].Id);
        Check(sameTurnDebuffedHit != null && sameTurnDebuffedHit.Amount == 2162, $"Enemy debuffed by Chin Skill 1 must take full Weakpoint damage from subsequent Ultimate (expected 2162, got {sameTurnDebuffedHit?.Amount}).");
        Check(sameTurnCleanHit != null && sameTurnCleanHit.Amount == 622, $"Clean enemy must take base damage without Weakpoint in same-turn combo (expected 622, got {sameTurnCleanHit?.Amount}).");

        // Case C: Yuri Shock debuff (verify status.debuff.shock triggers Weakpoint)
        var battleShock = BattleEngine.Create("chin-shock-test", new[] { chinFighter }, null, new[] { dummyEnemy1, dummyEnemy2 }, null, 1, TeamSide.Player);
        battleShock.Player.Fighters[0].PowerGauge = 5;
        var shockRecipe = new StatusRecipeDefinition
        {
            Id = "status.debuff.shock",
            Polarity = StatusPolarity.Debuff,
            DefaultDuration = 3
        };
        StatusSystem.Apply(battleShock.Opponent.Fighters[0], "yuri", TeamSide.Player, shockRecipe, "shock-test-1", "test", 1);
        var cardChinUlt3 = new CardState
        {
            Id = "card-chin-ult3",
            OwnerFighterId = battleShock.Player.Fighters[0].Id,
            Rank = 1,
            Kind = CardKind.Ultimate,
            Category = CardCategory.Attack,
            TargetScope = EffectTargetScope.AllEnemies,
            UltimateTier = 0
        };
        battleShock.Player.Hand.Clear();
        battleShock.Player.Hand.Add(cardChinUlt3);
        var draftShock = new PlanDraft(battleShock);
        draftShock.QueuePlay(cardChinUlt3.Id, battleShock.Opponent.Fighters[0].Id, out _);
        BattleEngine.TryResolvePlan(battleShock, draftShock.BuildPlan("shock-plan"), out var resolvedShock, out _);
        var shockHit = resolvedShock.Events.Find(e => e.Kind == BattleEventKind.DamageApplied && e.CardId == cardChinUlt3.Id && e.TargetId == battleShock.Opponent.Fighters[0].Id);
        Check(shockHit != null && shockHit.Amount == 1966, $"Shocked enemy must take full 3x Weakpoint damage (expected 1966, got {shockHit?.Amount}).");

        // 5. 7DS Grand Cross Order: Crit and Attribute Advantage breaking Endurance
        var lowAtkStats = new StatBlock { Attack = 300, CritChanceBp = 10000, CritDamageBp = 15000 };
        var highDefStats = new StatBlock { Defense = 370, CritResistanceBp = 0, CritDefenseBp = 0 };
        var normalPacket = new DamagePacket
        {
            BaseAmount = lowAtkStats.Attack,
            CoefficientBp = 10000, // 100% ATK
            KeywordFactorBp = 10000,
            Policy = new DamagePolicy { AttributeFactorBp = 10000 }
        };

        // Case A: Ordinary hit below DEF (300 * 1.0 < 370) -> Endures (0 damage)
        var ordinaryResult = DamageResolver.Resolve(normalPacket, lowAtkStats, highDefStats, 10000, 0, -1, -1);
        Check(ordinaryResult.WasEndured && ordinaryResult.CalculatedDamage == 0,
            $"Ordinary hit below defense must endure (expected 0, got {ordinaryResult.CalculatedDamage})");

        // Case B: Critical hit scales attack before DEF (300 * 1.0 * 1.5 = 450 > 370) -> deals 80 damage, breaks endurance
        var critResult = DamageResolver.Resolve(normalPacket, lowAtkStats, highDefStats, 10000, 0, 0, -1);
        Check(!critResult.WasEndured && critResult.WasCritical && critResult.CalculatedDamage == 80,
            $"Critical hit must apply before DEF and break endurance (expected 80, got {critResult.CalculatedDamage})");

        // Case C: Attribute advantage (+30%) scales attack before DEF (300 * 1.0 * 1.3 = 390 > 370) -> deals 20 damage, breaks endurance
        var advantagePacket = new DamagePacket
        {
            BaseAmount = lowAtkStats.Attack,
            CoefficientBp = 10000,
            KeywordFactorBp = 10000,
            Policy = new DamagePolicy { AttributeFactorBp = 13000 }
        };
        var advantageResult = DamageResolver.Resolve(advantagePacket, lowAtkStats, highDefStats, 10000, 0, -1, -1);
        Check(!advantageResult.WasEndured && advantageResult.CalculatedDamage == 20,
            $"Attribute advantage must apply before DEF and break endurance (expected 20, got {advantageResult.CalculatedDamage})");

        // Case C2: Attacker Outgoing Bonus (+15% Joe aura) scales attack before DEF and in-contact Ignite applies post-DEF
        // Attacker: 300 ATK, 100% card, +30% Attribute, +8% Pierce (24 pierce), +15% Outgoing Dmg (Joe)
        // Defender: 420 DEF, 10% Ignite (+10% incoming damage)
        // Offense: (300 * 1.0 * 1.3 + 300 * 0.08) * 1.15 = (390 + 24) * 1.15 = 414 * 1.15 = 476.1
        // Post-DEF: max(0, 476.1 - 420) = 56.1
        // In-contact Ignite: 56.1 * 1.10 = 61.71 -> 61
        var buffedAttackerStats = new StatBlock { Attack = 300, PierceBp = 800 };
        var boostedDefStats = new StatBlock { Defense = 420 };
        var joeIgnitePacket = new DamagePacket
        {
            BaseAmount = buffedAttackerStats.Attack,
            CoefficientBp = 10000,
            KeywordFactorBp = 10000,
            Policy = new DamagePolicy { AttributeFactorBp = 13000, OutgoingIncreaseBp = 1500, IncomingIncreaseBp = 1000 }
        };
        var joeIgniteResult = DamageResolver.Resolve(joeIgnitePacket, buffedAttackerStats, boostedDefStats, 10000, 0, -1, -1);
        Check(!joeIgniteResult.WasEndured && joeIgniteResult.CalculatedDamage == 61,
            $"Outgoing bonus damage must scale attack before DEF and Ignite applies post-DEF (expected 61, got {joeIgniteResult.CalculatedDamage})");

        // Case D: Attribute affinity resolution via AttributeRules and StatusSystem.BuildDamagePolicy
        Check(AttributeRules.GetAffinity("attribute.red", "attribute.green") == AttributeAffinity.Advantage, "Red must have advantage over Green");
        Check(AttributeRules.GetAffinity("attribute.green", "attribute.red") == AttributeAffinity.Disadvantage, "Green must have disadvantage against Red");
        Check(AttributeRules.GetAffinity("attribute.green", "attribute.blue") == AttributeAffinity.Advantage, "Green must have advantage over Blue");
        Check(AttributeRules.GetAffinity("attribute.blue", "attribute.red") == AttributeAffinity.Advantage, "Blue must have advantage over Red");
        Check(AttributeRules.GetAffinity("attribute.red", "attribute.blue") == AttributeAffinity.Disadvantage, "Red must have disadvantage against Blue");
        Check(AttributeRules.GetAffinity("attribute.red", "attribute.red") == AttributeAffinity.Neutral, "Same attribute must be neutral");

        var redFighter = new FighterState { Definition = new CharacterDefinition { Id = "red_f", AttributeId = "attribute.red" } };
        var greenFighter = new FighterState { Definition = new CharacterDefinition { Id = "green_f", AttributeId = "attribute.green" } };
        var redVsGreenPolicy = StatusSystem.BuildDamagePolicy(redFighter, greenFighter, DamageFamily.Normal);
        Check(redVsGreenPolicy.AttributeFactorBp == AttributeRules.AdvantageFactorBp,
            $"Red attacking Green must yield AdvantageFactorBp ({AttributeRules.AdvantageFactorBp}), got {redVsGreenPolicy.AttributeFactorBp}");

        var greenVsRedPolicy = StatusSystem.BuildDamagePolicy(greenFighter, redFighter, DamageFamily.Normal);
        Check(greenVsRedPolicy.AttributeFactorBp == AttributeRules.DisadvantageFactorBp,
            $"Green attacking Red must yield DisadvantageFactorBp ({AttributeRules.DisadvantageFactorBp}), got {greenVsRedPolicy.AttributeFactorBp}");
    }

    private static void EvadeAndImmunityFeedbackChecks()
    {
        // 1. Evade attacks and debuffs
        var attackerDef = Fighter("attacker_evade", 5000, 200);
        var defenderDef = Fighter("defender_evade", 5000, 50);
        var battleEvade = BattleEngine.Create("battle-evade", new[] { attackerDef }, null, new[] { defenderDef }, null, 1, TeamSide.Player);

        var evadeRecipe = new StatusRecipeDefinition
        {
            Id = "status.buff.test.evade",
            Polarity = StatusPolarity.Buff,
            DefaultDuration = 2,
            EvadeAttacks = true
        };
        StatusSystem.Apply(battleEvade.Opponent.Fighters[0], "defender_evade", TeamSide.Opponent, evadeRecipe, "evade-inst", "test", 1);

        // Attack card with debuff step
        var poisonRecipe = StandardEffectDatabase.CreateStatusRecipes().Find(r => r.Id == "status.debuff.poison");
        var attackWithDebuffCard = new CardState
        {
            Id = "card-atk-debuff",
            OwnerFighterId = battleEvade.Player.Fighters[0].Id,
            SkillId = attackerDef.Id + ":skill:1",
            Rank = 1,
            Kind = CardKind.Skill,
            Category = CardCategory.AttackDebuff,
            TargetScope = EffectTargetScope.SelectedEnemy
        };
        var rankDef = attackerDef.Skills[0].Ranks[0];
        rankDef.Effect.Sequence.Clear();
        rankDef.Effect.Sequence.Add(new CardEffectStep { Timing = CardEffectTiming.Damaging, Effect = new EffectDefinition { Kind = EffectKind.Damage, Scaling = StatScaling.Attack, CoefficientBp = 10000 } });
        rankDef.Effect.Sequence.Add(new CardEffectStep { Timing = CardEffectTiming.AfterDamage, Effect = new EffectDefinition { Kind = EffectKind.ApplyStatus, StatusRecipe = poisonRecipe } });

        battleEvade.Player.Hand.Clear();
        battleEvade.Player.Hand.Add(attackWithDebuffCard);
        var draft = new PlanDraft(battleEvade);
        draft.QueuePlay(attackWithDebuffCard.Id, battleEvade.Opponent.Fighters[0].Id, out _);
        BattleEngine.TryResolvePlan(battleEvade, draft.BuildPlan("plan-evade"), out var resolvedEvade, out _);

        var evadeEvt = resolvedEvade.Events.Find(e => e.Kind == BattleEventKind.AttackEvaded);
        Check(evadeEvt != null, "AttackEvaded event must be emitted when target has Evade buff");
        Check(evadeEvt != null && evadeEvt.Message == "Evade", "AttackEvaded event message must be 'Evade'");
        var dmgEvt = resolvedEvade.Events.Find(e => e.Kind == BattleEventKind.DamageApplied);
        Check(dmgEvt == null, "No DamageApplied event must be emitted against Evade target");
        var debuffApplied = resolvedEvade.Events.Find(e => e.Kind == BattleEventKind.StatusApplied);
        Check(debuffApplied == null, "Debuff must not be applied to Evade target");

        // 2. Debuff Immunity
        var battleImmune = BattleEngine.Create("battle-immune", new[] { attackerDef }, null, new[] { defenderDef }, null, 1, TeamSide.Player);
        var immuneRecipe = new StatusRecipeDefinition
        {
            Id = "status.buff.test.immunity",
            Polarity = StatusPolarity.Buff,
            DefaultDuration = 2,
            DebuffImmunity = true
        };
        StatusSystem.Apply(battleImmune.Opponent.Fighters[0], "defender_evade", TeamSide.Opponent, immuneRecipe, "immune-inst", "test", 1);

        battleImmune.Player.Hand.Clear();
        battleImmune.Player.Hand.Add(attackWithDebuffCard);
        var draftImmune = new PlanDraft(battleImmune);
        draftImmune.QueuePlay(attackWithDebuffCard.Id, battleImmune.Opponent.Fighters[0].Id, out _);
        BattleEngine.TryResolvePlan(battleImmune, draftImmune.BuildPlan("plan-immune"), out var resolvedImmune, out _);

        var dmgImmune = resolvedImmune.Events.Find(e => e.Kind == BattleEventKind.DamageApplied);
        Check(dmgImmune != null && dmgImmune.Amount > 0, "Damage must be dealt to target with Debuff Immunity");
        var debuffImmuneEvt = resolvedImmune.Events.Find(e => e.Kind == BattleEventKind.StatusImmuned);
        Check(debuffImmuneEvt != null, "StatusImmuned event must be emitted when target has Debuff Immunity");
        Check(debuffImmuneEvt != null && debuffImmuneEvt.Message == "Immunity", "StatusImmuned event message must be 'Immunity'");
        var debuffAppliedImmune = resolvedImmune.Events.Find(e => e.Kind == BattleEventKind.StatusApplied);
        Check(debuffAppliedImmune == null, "Debuff must not be applied to Debuff Immunity target");
    }

    private static void YuriSkill2RefreshStrongerChecks()
    {
        var targetFighter = Fighter("target_yuri", 1000, 100);
        var target = new FighterState { Id = "target_1", Definition = targetFighter, Side = TeamSide.Player, Health = 1000 };

        // Define Yuri ATK buff recipe (status.increase.attack)
        // Rank 1: +20% (Amount = 2000), 2 turns
        var rank1Recipe = new StatusRecipeDefinition
        {
            Id = "status.increase.attack",
            Polarity = StatusPolarity.Buff,
            Behavior = StatusBehavior.Stat,
            Stacking = StatusStackingPolicy.RefreshStronger,
            DefaultDuration = 2,
            Modifiers = new List<StatModifierDefinition>
            {
                new StatModifierDefinition { Target = ModifierTarget.Stat, Stat = StatId.Attack, Operation = ModifierOperation.PercentOfBase, Amount = 2000 }
            }
        };

        // Rank 2: +40% (Amount = 4000), 2 turns
        var rank2Recipe = new StatusRecipeDefinition
        {
            Id = "status.increase.attack",
            Polarity = StatusPolarity.Buff,
            Behavior = StatusBehavior.Stat,
            Stacking = StatusStackingPolicy.RefreshStronger,
            DefaultDuration = 2,
            Modifiers = new List<StatModifierDefinition>
            {
                new StatModifierDefinition { Target = ModifierTarget.Stat, Stat = StatId.Attack, Operation = ModifierOperation.PercentOfBase, Amount = 4000 }
            }
        };

        // Rank 3: +60% (Amount = 6000), 3 turns
        var rank3Recipe = new StatusRecipeDefinition
        {
            Id = "status.increase.attack",
            Polarity = StatusPolarity.Buff,
            Behavior = StatusBehavior.Stat,
            Stacking = StatusStackingPolicy.RefreshStronger,
            DefaultDuration = 3,
            Modifiers = new List<StatModifierDefinition>
            {
                new StatModifierDefinition { Target = ModifierTarget.Stat, Stat = StatId.Attack, Operation = ModifierOperation.PercentOfBase, Amount = 6000 }
            }
        };

        // Step 1: Apply Rank 3 buff
        var res1 = StatusSystem.Apply(target, "yuri", TeamSide.Player, rank3Recipe, "yuri_buff_1", "action_1", 1);
        Check(res1.Outcome == StatusApplyOutcome.Added, "Rank 3 buff must be added initially.");
        Check(target.Statuses.Instances.Count == 1, "Expected 1 active status instance.");
        var activeInstance = target.Statuses.Instances[0];
        Check(activeInstance.RemainingDuration == 3, "Rank 3 buff should start with duration 3.");
        Check(activeInstance.Recipe.Modifiers[0].Amount == 6000, "Active buff should be 60% ATK.");

        // Step 2: Simulate 2 turns passing so remaining duration is 1
        activeInstance.RemainingDuration = 1;

        // Step 3: Attempt to apply Rank 1 buff (20% ATK, 2 turns).
        // Since Rank 1 is WEAKER than active Rank 3 (+20% < +60%), it must NOT refresh duration!
        var res2 = StatusSystem.Apply(target, "yuri", TeamSide.Player, rank1Recipe, "yuri_buff_2", "action_2", 2);
        Check(res2.Outcome == StatusApplyOutcome.Rejected, "Weaker Rank 1 buff must be Rejected when stronger Rank 3 is active.");
        Check(!res2.Accepted, "Weaker buff must not be Accepted.");
        Check(activeInstance.RemainingDuration == 1, "Rank 1 buff must NOT refresh the duration of stronger Rank 3 buff (expected 1, got " + activeInstance.RemainingDuration + ").");
        Check(activeInstance.Recipe.Modifiers[0].Amount == 6000, "Active buff must remain Rank 3 60% ATK.");

        // Step 4: Attempt to apply Rank 2 buff (40% ATK, 2 turns).
        // Rank 2 is also weaker than active Rank 3 (+40% < +60%), so it must also be Rejected.
        var res3 = StatusSystem.Apply(target, "yuri", TeamSide.Player, rank2Recipe, "yuri_buff_3", "action_3", 3);
        Check(res3.Outcome == StatusApplyOutcome.Rejected, "Weaker Rank 2 buff must be Rejected when stronger Rank 3 is active.");
        Check(activeInstance.RemainingDuration == 1, "Rank 2 buff must NOT refresh the duration of stronger Rank 3 buff.");

        // Step 5: Apply Rank 3 buff again (equal strength: +60% ATK, 3 turns).
        // Equal strength must refresh duration back to 3!
        var res4 = StatusSystem.Apply(target, "yuri", TeamSide.Player, rank3Recipe, "yuri_buff_4", "action_4", 4);
        Check(res4.Outcome == StatusApplyOutcome.Refreshed, "Equal strength Rank 3 buff must refresh duration.");
        Check(res4.Accepted, "Equal strength buff must be accepted.");
        Check(activeInstance.RemainingDuration == 3, "Equal strength Rank 3 buff must refresh remaining duration to 3.");
        Check(activeInstance.Recipe.Modifiers[0].Amount == 6000, "Active buff must remain 60% ATK.");

        // Step 6: Test upgrading from weaker to stronger:
        var targetUpgrade = new FighterState { Id = "target_2", Definition = targetFighter, Side = TeamSide.Player, Health = 1000 };
        var resUp1 = StatusSystem.Apply(targetUpgrade, "yuri", TeamSide.Player, rank1Recipe, "up_1", "action_up1", 1);
        Check(resUp1.Outcome == StatusApplyOutcome.Added, "Rank 1 buff should be added.");
        var upInstance = targetUpgrade.Statuses.Instances[0];
        Check(upInstance.RemainingDuration == 2, "Rank 1 starts with duration 2.");
        upInstance.RemainingDuration = 1; // 1 turn left

        // Apply Rank 3 over active Rank 1:
        var resUp2 = StatusSystem.Apply(targetUpgrade, "yuri", TeamSide.Player, rank3Recipe, "up_2", "action_up2", 2);
        Check(resUp2.Outcome == StatusApplyOutcome.Replaced, "Stronger Rank 3 must replace weaker Rank 1 buff.");
        Check(upInstance.RemainingDuration == 3, "Upgraded buff must set duration to 3.");
        Check(upInstance.Recipe.Modifiers[0].Amount == 6000, "Upgraded buff must have 60% ATK.");
    }

    private static void EffectTargetScopeEnumChecks()
    {
        var defaultEffect = new EffectDefinition();
        Check(defaultEffect.Target == EffectTargetScope.SelectedEnemy, "Default EffectDefinition.Target must be SelectedEnemy.");

        var cloned = defaultEffect.Clone();
        Check(cloned.Target == EffectTargetScope.SelectedEnemy, "Cloned EffectDefinition must retain Target enum value.");

        var sequenceEffect = new EffectDefinition
        {
            Kind = EffectKind.Damage,
            CoefficientBp = 10000,
            Target = EffectTargetScope.SelectedEnemy,
            Sequence = new List<CardEffectStep>
            {
                new CardEffectStep
                {
                    Timing = CardEffectTiming.AfterDamage,
                    Effect = new EffectDefinition
                    {
                        Kind = EffectKind.ApplyStatus,
                        Target = EffectTargetScope.Self,
                        StatusRecipe = new StatusRecipeDefinition { Id = "test.self-buff", Polarity = StatusPolarity.Buff, DefaultDuration = 2 }
                    }
                },
                new CardEffectStep
                {
                    Timing = CardEffectTiming.AfterDamage,
                    Effect = new EffectDefinition
                    {
                        Kind = EffectKind.ApplyStatus,
                        Target = EffectTargetScope.AllAllies,
                        StatusRecipe = new StatusRecipeDefinition { Id = "test.team-buff", Polarity = StatusPolarity.Buff, DefaultDuration = 2 }
                    }
                }
            }
        };

        Check(sequenceEffect.Sequence[0].Effect.Target == EffectTargetScope.Self, "Sequence step 0 must have Target Self enum.");
        Check(sequenceEffect.Sequence[1].Effect.Target == EffectTargetScope.AllAllies, "Sequence step 1 must have Target AllAllies enum.");

        var seqClone = sequenceEffect.Clone();
        Check(seqClone.Sequence[0].Effect.Target == EffectTargetScope.Self, "Cloned sequence step 0 must retain Target Self.");
        Check(seqClone.Sequence[1].Effect.Target == EffectTargetScope.AllAllies, "Cloned sequence step 1 must retain Target AllAllies.");

        // Test running battle with sequence step having Target enum
        var state = Create(999);
        var attacker = state.Player.LivingActive()[0];
        var targetEnemy = state.Opponent.LivingActive()[0];
        var beforeEvents = state.Events.Count;

        var next = PlayKind(state, CardCategory.Attack, EffectTargetScope.SelectedEnemy, sequenceEffect, targetEnemy.Id);
        var newEvents = next.Events.Skip(beforeEvents).ToList();

        // Self buff must only apply to attacker
        var selfEvents = newEvents.Where(e => e.StatusRecipeId == "test.self-buff").ToList();
        Check(selfEvents.Count == 1, "Self status must apply exactly once.");
        Check(selfEvents[0].TargetId == attacker.Id, "Self status must apply to attacker.");

        // Team buff must apply to all 3 player team members
        var teamEvents = newEvents.Where(e => e.StatusRecipeId == "test.team-buff").ToList();
        Check(teamEvents.Count == 3, "AllAllies status must apply to all 3 allies.");
        Check(teamEvents.All(e => next.Player.LivingActive().Any(a => a.Id == e.TargetId)), "AllAllies status targets must be on player team.");
    }

    private static void YuriUltShockTriggeringDamageChecks()
    {
        // 1. Single target: Yuri Ult with Shock debuff at AfterAction timing
        // Base ATK: 350. Ultimate deals 400% ATK = 1400 damage.
        // Shock is configured to deal 33% of triggering damage (3300 bp).
        var yuriDef = Fighter("yuri_shock_test", 5000, 350);
        var enemyDef = Fighter("dummy_enemy", 10000, 0);

        var shockRecipe = new StatusRecipeDefinition
        {
            Id = "status.debuff.shock",
            Polarity = StatusPolarity.Debuff,
            Behavior = StatusBehavior.DamageOverTime,
            Stacking = StatusStackingPolicy.IndependentStacks,
            DefaultDuration = 3,
            PeriodicDamage = new PeriodicDamageDefinition
            {
                Family = DamageFamily.DamageOverTime,
                Timing = StatusTickTiming.TargetTurnEnd,
                Scaling = StatusSnapshotScaling.TriggeringHealthDamage,
                CoefficientBp = 3300
            }
        };

        var ultEffect = new EffectDefinition
        {
            Kind = EffectKind.Damage,
            Scaling = StatScaling.Attack,
            CoefficientBp = 40000, // 400% of 350 ATK = 1400 damage
            Target = EffectTargetScope.SelectedEnemy,
            Sequence = new List<CardEffectStep>
            {
                new CardEffectStep
                {
                    Timing = CardEffectTiming.AfterAction, // Yuri's timing
                    Effect = new EffectDefinition
                    {
                        Kind = EffectKind.ApplyStatus,
                        Target = EffectTargetScope.SelectedEnemy,
                        StatusRecipe = shockRecipe
                    }
                }
            }
        };

        yuriDef.UltimateTiers.Clear();
        yuriDef.UltimateTiers.Add(new UltimateTierDefinition
        {
            Tier = 0,
            Category = CardCategory.Attack,
            Effect = ultEffect
        });

        var battle = BattleEngine.Create("yuri-shock-test", new[] { yuriDef }, null, new[] { enemyDef }, null, 1, TeamSide.Player);
        battle.Player.Fighters[0].PowerGauge = 5;

        var ultCard = new CardState
        {
            Id = "yuri-ult-card",
            OwnerFighterId = battle.Player.Fighters[0].Id,
            Rank = 1,
            Kind = CardKind.Ultimate,
            Category = CardCategory.Attack,
            TargetScope = EffectTargetScope.SelectedEnemy,
            UltimateTier = 0
        };
        battle.Player.Hand.Clear();
        battle.Player.Hand.Add(ultCard);

        var draft = new PlanDraft(battle);
        draft.QueuePlay(ultCard.Id, battle.Opponent.Fighters[0].Id, out _);
        Check(BattleEngine.TryResolvePlan(battle, draft.BuildPlan("yuri-ult-plan"), out var resolved, out var err), err);

        var damageEvent = resolved.Events.Find(e => e.Kind == BattleEventKind.DamageApplied && e.CardId == ultCard.Id);
        Check(damageEvent != null, "Yuri Ult damage event must exist.");
        Check(damageEvent.Amount == 1400, $"Yuri Ult damage must be 1400, got {damageEvent?.Amount}.");

        var enemy = resolved.Opponent.Fighters[0];
        var shockInstance = enemy.Statuses.Instances.Find(s => s.RecipeId == "status.debuff.shock");
        Check(shockInstance != null, "Shock status must be applied to enemy.");
        Check(shockInstance.Snapshot != null, "Shock snapshot must exist.");
        Check(shockInstance.Snapshot.TriggeringHealthDamage == 1400,
            $"Shock snapshot TriggeringHealthDamage must be 1400 (actual damage dealt), got {shockInstance?.Snapshot?.TriggeringHealthDamage}.");

        // Now collect ticks at TargetTurnEnd
        var ticks = enemy.Statuses.CollectTicks(StatusTickTiming.TargetTurnEnd);
        Check(ticks.Count == 1, $"Must have 1 Shock tick, got {ticks.Count}.");
        // 1400 * 33% = 462 damage! (If it fell back to ATK 350, it would have been 350 * 0.33 = 115)
        Check(ticks[0].Amount == 462, $"Shock tick must deal 462 damage (33% of 1400), got {ticks[0].Amount}.");

        // 2. AOE attack with Shock at AfterAction timing
        // Target 1 takes 1400 damage, Target 2 takes 1000 damage.
        var aoeAttacker = Fighter("aoe_attacker", 5000, 1000);
        var aoeEnemy1 = Fighter("aoe_enemy1", 10000, 0);
        var aoeEnemy2 = Fighter("aoe_enemy2", 10000, 0);

        var aoeEffect = new EffectDefinition
        {
            Kind = EffectKind.Damage,
            Scaling = StatScaling.Attack,
            CoefficientBp = 10000,
            Target = EffectTargetScope.AllEnemies,
            Sequence = new List<CardEffectStep>
            {
                new CardEffectStep
                {
                    Timing = CardEffectTiming.AfterAction,
                    Effect = new EffectDefinition
                    {
                        Kind = EffectKind.ApplyStatus,
                        Target = EffectTargetScope.AllEnemies,
                        StatusRecipe = shockRecipe
                    }
                }
            }
        };

        var aoeSkill = new SkillDefinition { Id = "aoe_skill", Slot = 1 };
        aoeSkill.Ranks.Add(new SkillRankDefinition { Rank = 1, Effect = aoeEffect, Category = CardCategory.Attack, TargetScope = EffectTargetScope.AllEnemies });
        aoeAttacker.Skills.Add(aoeSkill);

        var aoeBattle = BattleEngine.Create("aoe-shock-test", new[] { aoeAttacker }, null, new[] { aoeEnemy1, aoeEnemy2 }, null, 1, TeamSide.Player);
        // Set different defense stats so damage dealt differs: enemy 1 takes 1000, enemy 2 takes 500
        aoeBattle.Opponent.Fighters[0].Definition.BaseStats.Defense = 0;
        aoeBattle.Opponent.Fighters[1].Definition.BaseStats.Defense = 500;

        var aoeCard = new CardState
        {
            Id = "aoe-card",
            OwnerFighterId = aoeBattle.Player.Fighters[0].Id,
            SkillId = aoeSkill.Id,
            Rank = 1,
            Kind = CardKind.Skill,
            Category = CardCategory.Attack,
            TargetScope = EffectTargetScope.AllEnemies
        };
        aoeBattle.Player.Hand.Clear();
        aoeBattle.Player.Hand.Add(aoeCard);

        var aoeDraft = new PlanDraft(aoeBattle);
        aoeDraft.QueuePlay(aoeCard.Id, aoeBattle.Opponent.Fighters[0].Id, out _);
        Check(BattleEngine.TryResolvePlan(aoeBattle, aoeDraft.BuildPlan("aoe-plan"), out var aoeResolved, out var aoeErr), aoeErr);

        var hit1 = aoeResolved.Events.Find(e => e.Kind == BattleEventKind.DamageApplied && e.CardId == aoeCard.Id && e.TargetId == aoeBattle.Opponent.Fighters[0].Id);
        var hit2 = aoeResolved.Events.Find(e => e.Kind == BattleEventKind.DamageApplied && e.CardId == aoeCard.Id && e.TargetId == aoeBattle.Opponent.Fighters[1].Id);
        Check(hit1 != null && hit2 != null, "Both AOE targets must receive damage events.");

        var e1Status = aoeResolved.Opponent.Fighters[0].Statuses.Instances.Find(s => s.RecipeId == "status.debuff.shock");
        var e2Status = aoeResolved.Opponent.Fighters[1].Statuses.Instances.Find(s => s.RecipeId == "status.debuff.shock");
        Check(e1Status?.Snapshot?.TriggeringHealthDamage == hit1.Amount, $"AOE target 1 snapshot must match its hit ({hit1.Amount}), got {e1Status?.Snapshot?.TriggeringHealthDamage}.");
        Check(e2Status?.Snapshot?.TriggeringHealthDamage == hit2.Amount, $"AOE target 2 snapshot must match its hit ({hit2.Amount}), got {e2Status?.Snapshot?.TriggeringHealthDamage}.");

        var e1Ticks = aoeResolved.Opponent.Fighters[0].Statuses.CollectTicks(StatusTickTiming.TargetTurnEnd);
        var e2Ticks = aoeResolved.Opponent.Fighters[1].Statuses.CollectTicks(StatusTickTiming.TargetTurnEnd);
        Check(e1Ticks[0].Amount == hit1.Amount * 3300 / 10000, $"Enemy 1 tick must scale with its damage ({hit1.Amount * 3300 / 10000}), got {e1Ticks[0].Amount}.");
        Check(e2Ticks[0].Amount == hit2.Amount * 3300 / 10000, $"Enemy 2 tick must scale with its damage ({hit2.Amount * 3300 / 10000}), got {e2Ticks[0].Amount}.");
    }

    private static void EnemyAiPlannerTacticalChecks()
    {
        // 1. Multi-Action Full Budget Planning & Execution
        var p1 = Fighter("p_ai_1", 1000, 50);
        var p2 = Fighter("p_ai_2", 1000, 50);
        var e1 = Fighter("e_ai_1", 1000, 100);
        var e2 = Fighter("e_ai_2", 1000, 100);
        var e3 = Fighter("e_ai_3", 1000, 100);

        var battle = BattleEngine.Create("ai-budget-test", new[] { p1, p2 }, null, new[] { e1, e2, e3 }, null, 42, TeamSide.Opponent);
        Check(battle.ActionBudget == 3, "AI must have action budget of 3 with 3 active fighters.");
        Check(battle.Opponent.Hand.Count >= 3, "AI must have at least 3 cards in hand.");

        var plan = EnemyAiPlanner.CreatePlan(battle);
        Check(plan != null, "EnemyAiPlanner must create a non-null TurnPlan.");
        Check(plan.Actions.Count == 3, $"EnemyAiPlanner must fill all 3 action slots, got {plan.Actions.Count}.");
        Check(BattleEngine.TryResolvePlan(battle, plan, out var resolved, out var err), $"AI plan must resolve cleanly: {err}");
        Check(resolved != null, "Resolved state must not be null.");

        // 2. Personality Derivation based on Character Kits
        var recklessFighter = Fighter("reckless_f");
        recklessFighter.Skills[0].Category = CardCategory.Attack;
        recklessFighter.Skills[1].Category = CardCategory.Attack;
        var recklessTeam = new BattleTeamState();
        recklessTeam.Fighters.Add(new FighterState { Definition = recklessFighter, IsAlive = true });
        var recklessProfile = AiPersonalityProfile.DeriveFromTeam(recklessTeam);
        Check(recklessProfile.Personality == AiPersonality.Reckless, $"Expected Reckless personality, got {recklessProfile.Personality}.");

        var defensiveFighter = Fighter("defensive_f");
        defensiveFighter.Skills[0].Category = CardCategory.Recovery;
        defensiveFighter.Skills[1].Category = CardCategory.Buff;
        var defensiveTeam = new BattleTeamState();
        defensiveTeam.Fighters.Add(new FighterState { Definition = defensiveFighter, IsAlive = true });
        var defensiveProfile = AiPersonalityProfile.DeriveFromTeam(defensiveTeam);
        Check(defensiveProfile.Personality == AiPersonality.Defensive, $"Expected Defensive personality, got {defensiveProfile.Personality}.");

        var tacticalFighter = Fighter("tactical_f");
        tacticalFighter.Skills[0].Category = CardCategory.Debuff;
        tacticalFighter.Skills[1].Category = CardCategory.AttackDebuff;
        var tacticalTeam = new BattleTeamState();
        tacticalTeam.Fighters.Add(new FighterState { Definition = tacticalFighter, IsAlive = true });
        var tacticalProfile = AiPersonalityProfile.DeriveFromTeam(tacticalTeam);
        Check(tacticalProfile.Personality == AiPersonality.Tactical, $"Expected Tactical personality, got {tacticalProfile.Personality}.");

        // 3. Ultimate Card Priority
        var ultBattle = BattleEngine.Create("ai-ult-test", new[] { p1 }, null, new[] { e1 }, null, 10, TeamSide.Opponent);
        var ultFighter = ultBattle.Opponent.Fighters[0];
        ultFighter.PowerGauge = 5;
        var ultCard = new CardState
        {
            Id = "test-ai-ult-card",
            OwnerFighterId = ultFighter.Id,
            Rank = 1,
            Kind = CardKind.Ultimate,
            Category = CardCategory.Attack,
            TargetScope = EffectTargetScope.SelectedEnemy,
            UltimateTier = 0
        };
        ultBattle.Opponent.Hand.Insert(0, ultCard);

        var ultPlan = EnemyAiPlanner.CreatePlan(ultBattle);
        Check(ultPlan != null && ultPlan.Actions.Count > 0, "AI must produce plan with ready ultimate.");
        Check(ultPlan.Actions.Any(a => a.CardId == ultCard.Id), "AI must prioritize and play ready ultimate card.");

        // 4. Taunt Respect in AI Planning
        var tauntBattle = BattleEngine.Create("ai-taunt-test", new[] { p1, p2 }, null, new[] { e1 }, null, 15, TeamSide.Opponent);
        var tauntRecipe = new StatusRecipeDefinition
        {
            Id = "status.buff.taunt",
            Polarity = StatusPolarity.Buff,
            Taunt = true,
            DefaultDuration = 2
        };
        StatusSystem.Apply(tauntBattle.Player.Fighters[1], tauntBattle.Player.Fighters[1].Id, TeamSide.Player, tauntRecipe, "taunt_inst", null, 1);
        Check(tauntBattle.Player.Fighters[1].Statuses.Instances.Exists(s => s.Recipe?.Taunt == true), "Taunt status must be present on p2.");

        var tauntPlan = EnemyAiPlanner.CreatePlan(tauntBattle);
        Check(tauntPlan != null && tauntPlan.Actions.Count > 0, "AI must produce plan when opponent has taunt.");
        foreach (var act in tauntPlan.Actions)
        {
            if (!act.IsMove && !string.IsNullOrEmpty(act.TargetFighterId))
            {
                Check(act.TargetFighterId == tauntBattle.Player.Fighters[1].Id, $"AI single target attack must be directed to taunter ({tauntBattle.Player.Fighters[1].Id}), got {act.TargetFighterId}.");
            }
        }

        // 5. Tactical Single-Target Recovery / Heal Ally Targeting
        var healHealerDef = Fighter("e_healer", 1000, 100);
        healHealerDef.Skills[0].SourceType = "Heal";
        healHealerDef.Skills[0].SourceTarget = "SelectedAlly";
        healHealerDef.Skills[0].Ranks[0].Effect = new EffectDefinition
        {
            Kind = EffectKind.Heal,
            Target = EffectTargetScope.SelectedAlly,
            HealCoefficientBp = 20000,
            HealScalingStat = StatId.Attack
        };
        var healCarryDef = Fighter("e_injured_carry", 1000, 150);
        var healTankDef = Fighter("e_healthy_tank", 1000, 50);

        var healBattle = BattleEngine.Create("ai-heal-targeting-test",
            new[] { p1, p2 }, null,
            new[] { healHealerDef, healCarryDef, healTankDef }, null, 21, TeamSide.Opponent);

        var injuredCarry = healBattle.Opponent.Fighters.Find(f => f.Definition.Id == "e_injured_carry");
        var healthyTank = healBattle.Opponent.Fighters.Find(f => f.Definition.Id == "e_healthy_tank");
        injuredCarry.Health = 200; // 20% HP (critically low)
        healthyTank.Health = 1000; // 100% HP (full)

        var healCard = healBattle.Opponent.Hand.Find(c => c.OwnerFighterId == healBattle.Opponent.Fighters[0].Id && c.Category == CardCategory.Recovery);
        if (healCard == null)
        {
            healCard = new CardState
            {
                Id = "test-heal-card",
                OwnerFighterId = healBattle.Opponent.Fighters[0].Id,
                SkillId = healHealerDef.Skills[0].Id,
                Rank = 1,
                Kind = CardKind.Skill,
                Category = CardCategory.Recovery,
                TargetScope = EffectTargetScope.SelectedAlly
            };
            healBattle.Opponent.Hand.Insert(0, healCard);
        }

        var healPlan = EnemyAiPlanner.CreatePlan(healBattle);
        Check(healPlan != null && healPlan.Actions.Count > 0, "AI must produce plan with heal available.");
        var playedHeal = healPlan.Actions.Find(a => !a.IsMove && a.CardId == healCard.Id);
        Check(playedHeal != null, "AI must choose to play the critical recovery card.");
        Check(playedHeal.TargetFighterId == injuredCarry.Id,
            $"AI recovery card MUST target the critically injured ally ({injuredCarry.Id}), but targeted {playedHeal.TargetFighterId}.");

        // 6. Tactical Single-Target Buff Ally Targeting & Attack Combo
        var bufferDef = Fighter("e_buffer", 1000, 80);
        bufferDef.Skills[0].SourceType = "Buff";
        bufferDef.Skills[0].SourceTarget = "SelectedAlly";
        var atkBuffRecipe = new StatusRecipeDefinition
        {
            Id = "status.buff.atk_up",
            Polarity = StatusPolarity.Buff,
            DefaultDuration = 2,
            Modifiers = new List<StatModifierDefinition> { new StatModifierDefinition { Stat = StatId.Attack, Amount = 50 } }
        };
        bufferDef.Skills[0].Ranks[0].Effect = new EffectDefinition
        {
            Kind = EffectKind.ApplyStatus,
            Target = EffectTargetScope.SelectedAlly,
            StatusRecipe = atkBuffRecipe
        };

        var buffDpsDef = Fighter("e_dps_carry", 1000, 200);
        buffDpsDef.Role = "dps";
        var buffTankDef = Fighter("e_pure_tank", 1000, 40);
        buffTankDef.Role = "tank";

        var buffBattle = BattleEngine.Create("ai-buff-targeting-test",
            new[] { p1, p2 }, null,
            new[] { bufferDef, buffDpsDef, buffTankDef }, null, 25, TeamSide.Opponent);

        var buffDps = buffBattle.Opponent.Fighters.Find(f => f.Definition.Id == "e_dps_carry");
        var buffCard = new CardState
        {
            Id = "test-buff-card",
            OwnerFighterId = buffBattle.Opponent.Fighters[0].Id,
            SkillId = bufferDef.Skills[0].Id,
            Rank = 1,
            Kind = CardKind.Skill,
            Category = CardCategory.Buff,
            TargetScope = EffectTargetScope.SelectedAlly
        };
        var dpsAtkCard = new CardState
        {
            Id = "test-dps-attack-card",
            OwnerFighterId = buffDps.Id,
            SkillId = buffDpsDef.Skills[0].Id,
            Rank = 1,
            Kind = CardKind.Skill,
            Category = CardCategory.Attack,
            TargetScope = EffectTargetScope.SelectedEnemy
        };
        buffBattle.Opponent.Hand.Insert(0, buffCard);
        buffBattle.Opponent.Hand.Insert(1, dpsAtkCard);

        var buffPlan = EnemyAiPlanner.CreatePlan(buffBattle);
        Check(buffPlan != null && buffPlan.Actions.Count > 0, "AI must produce plan with buff available.");
        var playedBuff = buffPlan.Actions.Find(a => !a.IsMove && a.CardId == buffCard.Id);
        Check(playedBuff != null, "AI should include buff card in plan.");
        Check(playedBuff.TargetFighterId == buffDps.Id,
            $"AI offensive buff card MUST target DPS carry ({buffDps.Id}), but targeted {playedBuff.TargetFighterId}.");
    }
}
