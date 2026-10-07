using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;

internal static partial class BattlePlaybackChecks
{
    private static void ReserveEntryRefillChecks()
    {
        CheckReserveEntryRefill(TeamSide.Player);
        CheckReserveEntryRefill(TeamSide.Opponent);
        CheckDeathRefillWithoutReserve(TeamSide.Player);
        CheckDeathRefillWithoutReserve(TeamSide.Opponent);
    }

    private static void CheckReserveEntryRefill(TeamSide incomingSide)
    {
        var outgoingSide = incomingSide == TeamSide.Player ? TeamSide.Opponent : TeamSide.Player;
        var incoming = new[]
        {
            Fighter(incomingSide + "-front", 100, 10),
            Fighter(incomingSide + "-down-1", 100, 10),
            Fighter(incomingSide + "-down-2", 100, 10),
            Fighter(incomingSide + "-sub", 100, 10)
        };
        var outgoing = new[] { Fighter(outgoingSide + "-attacker", 1000, 500) };
        var player = incomingSide == TeamSide.Player ? incoming : outgoing;
        var opponent = incomingSide == TeamSide.Opponent ? incoming : outgoing;
        var state = BattleEngine.Create("reserve-refill-" + incomingSide, player, null, opponent, null,
            17, outgoingSide, cardDrawSeed: 23);
        var incomingTeam = state.Team(incomingSide);
        var outgoingTeam = state.Team(outgoingSide);
        var victim = incomingTeam.Fighters[0];
        var reserve = incomingTeam.Fighters[3];

        // Isolate the replacement boundary: the front fighter is the only active survivor,
        // while SUB is the only fighter that can own the refill cards after the defeat.
        for (var i = 1; i < 3; i++)
        {
            incomingTeam.Fighters[i].Health = 0;
            incomingTeam.Fighters[i].IsAlive = false;
        }
        victim.Health = 1;
        incomingTeam.Hand = new List<CardState> { Card("removed-with-owner", victim, 1) };

        var attacker = outgoingTeam.LivingActive()[0];
        outgoingTeam.Hand = new List<CardState> { Card("finisher", attacker, 1) };
        var beforeEventCount = state.Events.Count;
        var draft = new PlanDraft(state);
        Check(draft.QueuePlay("finisher", victim.Id, out var queueError), queueError);
        Check(BattleEngine.TryResolvePlan(state, draft.BuildPlan("defeat-before-sub-" + incomingSide),
            out var resolved, out var resolveError), resolveError);

        var events = resolved.Events.Skip(beforeEventCount).ToList();
        var reserveIndex = events.FindIndex(item => item.Kind == BattleEventKind.ReserveEntered && item.SourceId == reserve.Id);
        var firstRefillIndex = events.FindIndex(item => item.Kind == BattleEventKind.CardDrawn && item.SourceId == reserve.Id);
        Check(reserveIndex >= 0, incomingSide + " SUB did not enter at its turn start.");
        Check(firstRefillIndex > reserveIndex, incomingSide + " SUB cards must draw after its entrance event.");

        var resolvedTeam = resolved.Team(incomingSide);
        Check(resolvedTeam.Hand.Count == CardRules.GetHandCapacity(resolvedTeam),
            incomingSide + " replacement turn did not refill to the hand cap.");
        Check(resolvedTeam.Hand.All(card => card.OwnerFighterId == reserve.Id),
            incomingSide + " replacement refill drew a card for an inactive fighter.");
        Check(resolvedTeam.FindFighter(reserve.Id).IsAlive && !resolvedTeam.FindFighter(reserve.Id).IsReserve,
            incomingSide + " SUB was not active after the refill.");
    }

    private static void CheckDeathRefillWithoutReserve(TeamSide incomingSide)
    {
        var outgoingSide = incomingSide == TeamSide.Player ? TeamSide.Opponent : TeamSide.Player;
        var incoming = new[]
        {
            Fighter(incomingSide + "-victim", 100, 10),
            Fighter(incomingSide + "-survivor", 100, 10),
            Fighter(incomingSide + "-already-down", 100, 10)
        };
        var outgoing = new[] { Fighter(outgoingSide + "-attacker-no-sub", 1000, 500) };
        var state = BattleEngine.Create("death-refill-no-sub-" + incomingSide,
            incomingSide == TeamSide.Player ? incoming : outgoing, null,
            incomingSide == TeamSide.Opponent ? incoming : outgoing, null,
            31, outgoingSide, cardDrawSeed: 41);
        var incomingTeam = state.Team(incomingSide);
        var outgoingTeam = state.Team(outgoingSide);
        var victim = incomingTeam.Fighters[0];
        var survivor = incomingTeam.Fighters[1];
        incomingTeam.Fighters[2].Health = 0;
        incomingTeam.Fighters[2].IsAlive = false;
        victim.Health = 1;
        incomingTeam.Hand = new List<CardState> { Card("removed-with-owner-no-sub", victim, 1) };

        var attacker = outgoingTeam.LivingActive()[0];
        outgoingTeam.Hand = new List<CardState> { Card("finisher-no-sub", attacker, 1) };
        var beforeEventCount = state.Events.Count;
        var draft = new PlanDraft(state);
        Check(draft.QueuePlay("finisher-no-sub", victim.Id, out var queueError), queueError);
        Check(BattleEngine.TryResolvePlan(state, draft.BuildPlan("defeat-no-sub-" + incomingSide),
            out var resolved, out var resolveError), resolveError);

        var events = resolved.Events.Skip(beforeEventCount).ToList();
        Check(!events.Any(item => item.Kind == BattleEventKind.ReserveEntered),
            incomingSide + " emitted a SUB entry without a reserve.");
        Check(events.Any(item => item.Kind == BattleEventKind.CardDrawn && item.SourceId == survivor.Id),
            incomingSide + " did not refill after an opposing-turn death without a SUB.");
        Check(resolved.Team(incomingSide).Hand.Count == CardRules.GetHandCapacity(resolved.Team(incomingSide)),
            incomingSide + " no-SUB death refill did not reach the hand cap.");
    }
}
