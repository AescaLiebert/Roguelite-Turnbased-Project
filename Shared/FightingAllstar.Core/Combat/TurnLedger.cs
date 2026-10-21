using System;

namespace FightingAllstar.Core.Combat
{
    /// <summary>Queries authoritative committed inputs. Moving/merging a card is not using it; a fizzled card is not a play.</summary>
    public static class TurnLedger
    {
        public static int CardsPlayedDuringCurrentOwnerTurn(BattleState state, FighterState owner) => Count(state, owner, false);
        public static int CardsPlayedDuringPreviousOwnerTurn(BattleState state, FighterState owner) => Count(state, owner, true);

        private static int Count(BattleState state, FighterState owner, bool previous)
        {
            if (state?.Events == null || owner == null) return 0;
            var side = owner.Side.ToString();
            var latestStart = -1;
            var latestCompleted = false;
            for (var i = state.Events.Count - 1; i >= 0; i--)
            {
                var item = state.Events[i];
                if (item == null) continue;
                if (item.Kind == BattleEventKind.TurnEnded && item.SourceId == side) latestCompleted = true;
                if (item.Kind == BattleEventKind.TurnStarted && item.SourceId == side)
                {
                    latestStart = i; break;
                }
            }
            if (latestStart < 0 || !previous && latestCompleted) return 0;
            var start = latestStart;
            var end = state.Events.Count;
            if (previous && !latestCompleted)
            {
                end = latestStart;
                start = -1;
                for (var i = latestStart - 1; i >= 0; i--)
                    if (state.Events[i]?.Kind == BattleEventKind.TurnStarted && state.Events[i].SourceId == side)
                    { start = i; break; }
                if (start < 0) return 0;
            }
            var count = 0;
            for (var i = start + 1; i < end; i++)
            {
                var item = state.Events[i];
                if (item?.Kind == BattleEventKind.TurnEnded && item.SourceId == side) break;
                if (item?.Kind == BattleEventKind.CardPlayed && item.SourceId == owner.Id) count++;
            }
            return count;
        }

        public static bool CrossedAtLeast(int previousValue, int currentValue, int threshold) =>
            previousValue < threshold && currentValue >= threshold;
        public static bool CrossedBelow(int previousValue, int currentValue, int threshold) =>
            previousValue >= threshold && currentValue < threshold;
    }
}
