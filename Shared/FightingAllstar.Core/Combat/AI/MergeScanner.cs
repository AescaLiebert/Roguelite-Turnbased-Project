using System;
using System.Collections.Generic;

namespace FightingAllstar.Core.Combat.AI
{
    /// <summary>
    /// Scans hand for merge possibilities and determines optimal move actions.
    /// In 7DSGC rules, moving a card grants +1 PG, and merging adjacent identical cards grants another +1 PG
    /// while upgrading the card to the next rank (Rank 1 -> 2, Rank 2 -> 3).
    /// </summary>
    public static class MergeScanner
    {
        public static List<MergeCandidate> FindMerges(BattleTeamState team, List<CardOption> hand)
        {
            var candidates = new List<MergeCandidate>();
            if (team == null || hand == null || hand.Count < 2) return candidates;

            // Iterate over all pairs of cards in hand
            for (int i = 0; i < hand.Count; i++)
            {
                var cardA = hand[i];
                if (!cardA.IsPlayable && cardA.Owner?.IsAlive != true) continue;
                if (cardA.IsUltimate || cardA.Rank >= 3) continue;

                for (int j = i + 1; j < hand.Count; j++)
                {
                    var cardB = hand[j];
                    if (!cardB.IsPlayable && cardB.Owner?.IsAlive != true) continue;
                    if (cardB.IsUltimate || cardB.Rank >= 3) continue;

                    // Match same owner, same skill id, same rank
                    if (cardA.Card.OwnerFighterId == cardB.Card.OwnerFighterId &&
                        cardA.Card.SkillId == cardB.Card.SkillId &&
                        cardA.Rank == cardB.Rank)
                    {
                        // Check if they are already adjacent
                        if (Math.Abs(i - j) == 1)
                        {
                            // Already adjacent - they will merge automatically if cards between/after are played
                            // Or they might already be merging, but if they are adjacent right now,
                            // why haven't they merged? In BattleEngine, MergeAdjacent happens on draw/play/move.
                            // If they are adjacent now, they would have merged unless just placed.
                            continue;
                        }

                        // We can move Card B to be next to Card A (j to i + 1 or i),
                        // or move Card A to be next to Card B (i to j - 1 or j).
                        // Moving card B next to card A:
                        int targetSlotForB = j > i ? i + 1 : i;
                        int targetSlotForA = i < j ? j - 1 : j;

                        // Evaluate value of this merge:
                        // 1. Upgraded rank impact (Rank 2 is ~1.5x, Rank 3 is ~2.2x power, often adds CC/effects)
                        float value = cardA.Rank == 1 ? 1.5f : 2.2f;

                        // 2. PG gains: moving grants +1 PG to mover, merging grants +1 PG to owner.
                        // Since both cards have the same owner, the owner gets +2 PG!
                        if (cardA.Owner != null)
                        {
                            int currentPg = cardA.Owner.PowerGauge;
                            if (currentPg == 3) value += 1.2f; // Reaches 5 (Ultimate ready next turn!)
                            else if (currentPg == 4) value += 0.8f; // Reaches 5 (Caps at 5)
                            else if (currentPg <= 2) value += 0.6f;
                        }

                        // Option 1: Move B next to A
                        candidates.Add(new MergeCandidate
                        {
                            CardA = cardA,
                            CardB = cardB,
                            MoveFromIndex = j,
                            MoveToIndex = targetSlotForB,
                            ResultingRank = cardA.Rank + 1,
                            MergeValue = value
                        });

                        // Option 2: Move A next to B
                        candidates.Add(new MergeCandidate
                        {
                            CardA = cardB,
                            CardB = cardA,
                            MoveFromIndex = i,
                            MoveToIndex = targetSlotForA,
                            ResultingRank = cardA.Rank + 1,
                            MergeValue = value
                        });
                    }
                }
            }

            // Sort by merge value descending
            candidates.Sort((x, y) => y.MergeValue.CompareTo(x.MergeValue));
            return candidates;
        }
    }
}
