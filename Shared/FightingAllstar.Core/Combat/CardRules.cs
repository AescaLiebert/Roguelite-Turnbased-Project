using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    public static class CardRules
    {
        public const int HandLimit = 7;
        public const int UltimateGaugeCost = 5;

        public static void DrawOpeningHand(BattleTeamState team, DeterministicRandom rng,
            List<CardState> drawnCards = null, List<string> mergedCardIds = null)
        {
            // The opening deal is the only position-ordered deal: left to right is
            // Position 1, Position 2, then Position 3. Random draws insert at the left.
            var openingOrder = team.LivingActive();
            foreach (var fighter in openingOrder)
            {
                var skills = OrderedSkills(fighter.Definition);
                for (var index = 0; index < skills.Count && index < 2; index++)
                    RecordDraw(AddCard(team, fighter, skills[index], 1, CardKind.Skill, 0), drawnCards);
            }
            while (team.Hand.Count < GetHandCapacity(team) && DrawRandomCard(team, rng, drawnCards)) { }
            MergeAdjacent(team, mergedCardIds, false);
        }

        public static void StartTurn(BattleTeamState team, DeterministicRandom rng,
            List<CardState> drawnCards = null, List<string> mergedCardIds = null)
        {
            var ready = team.LivingActive();
            foreach (var fighter in ready)
            {
                if (fighter.PowerGauge < UltimateGaugeCost || HasPendingUltimate(team, fighter.Id)) continue;
                if (team.Hand.Count >= GetHandCapacity(team)) break;
                RecordDraw(AddCard(team, fighter, null, 1, CardKind.Ultimate, fighter.ConstellationTier), drawnCards);
            }
            while (team.Hand.Count < GetHandCapacity(team) && DrawRandomCard(team, rng, drawnCards)) { }
            MergeAdjacent(team, mergedCardIds, true);
        }

        public static int GetHandCapacity(BattleTeamState team) => Math.Min(HandLimit, Math.Max(0, team?.Fighters.Count ?? 0) + 3);

        public static bool TryMove(BattleTeamState team, int from, int to, bool grantGauge, out string reason)
            => TryMove(team, from, to, grantGauge, out reason, null);

        public static bool TryMove(BattleTeamState team, int from, int to, bool grantGauge, out string reason,
            List<string> mergedCardIds)
        {
            reason = null;
            if (from < 0 || from >= team.Hand.Count || to < 0 || to >= team.Hand.Count) { reason = "Card slot is outside the hand."; return false; }
            var card = team.Hand[from];
            if (card.Kind == CardKind.Ultimate) { reason = "Ultimates cannot be moved."; return false; }
            if (from == to) { reason = "That move does not change the hand."; return false; }
            team.Hand.RemoveAt(from);
            team.Hand.Insert(to, card);
            MergeAdjacent(team, mergedCardIds, grantGauge);
            if (grantGauge)
            {
                var owner = team.FindFighter(card.OwnerFighterId);
                if (owner != null && owner.IsAlive) owner.PowerGauge = Math.Min(UltimateGaugeCost, owner.PowerGauge + 1);
            }
            return true;
        }

        public static bool MergeAdjacent(BattleTeamState team, List<string> mergedCardIds)
        {
            return MergeAdjacent(team, mergedCardIds, false);
        }

        public static bool MergeAdjacent(BattleTeamState team, List<string> mergedCardIds, bool grantGauge)
        {
            bool any = false;
            for (var i = 0; i + 1 < team.Hand.Count;)
            {
                var left = team.Hand[i];
                var right = team.Hand[i + 1];
                if (left.Kind == CardKind.Skill && right.Kind == CardKind.Skill && left.OwnerFighterId == right.OwnerFighterId &&
                    left.SkillId == right.SkillId && left.Rank == right.Rank && left.Rank < 3)
                {
                    left.Rank++;
                    team.Hand.RemoveAt(i + 1);
                    mergedCardIds?.Add(left.Id);
                    if (grantGauge)
                    {
                        var owner = team.FindFighter(left.OwnerFighterId);
                        if (owner != null && owner.IsAlive) owner.PowerGauge = Math.Min(UltimateGaugeCost, owner.PowerGauge + 1);
                    }
                    any = true;
                    if (i > 0) i--;
                }
                else i++;
            }
            return any;
        }

        public static void RemoveDeadOwnerCards(BattleTeamState team, string fighterId) => team.Hand.RemoveAll(c => c.OwnerFighterId == fighterId);

        public static bool TryGetSkill(CharacterDefinition definition, string skillId, int rank, out EffectDefinition effect)
        {
            effect = null;
            if (definition?.Skills == null) return false;
            var skill = definition.Skills.Find(x => x.Id == skillId);
            var data = skill?.Ranks?.Find(x => x.Rank == rank);
            if (data == null) return false;
            effect = data.Effect;
            return effect != null;
        }

        public static bool TryGetUltimate(CharacterDefinition definition, int tier, out EffectDefinition effect)
        {
            effect = null;
            var entry = definition?.UltimateTiers?.Find(x => x.Tier == tier);
            if (entry == null) return false;
            effect = entry.Effect;
            return effect != null;
        }

        private static List<SkillDefinition> OrderedSkills(CharacterDefinition definition)
        {
            var skills = new List<SkillDefinition>(definition.Skills);
            skills.Sort((a, b) => a.Slot != b.Slot ? a.Slot.CompareTo(b.Slot) : string.CompareOrdinal(a.Id, b.Id));
            return skills;
        }

        private static bool DrawRandomCard(BattleTeamState team, DeterministicRandom rng, List<CardState> drawnCards)
        {
            if (team == null || rng == null || team.Hand.Count >= GetHandCapacity(team)) return false;
            var eligible = team.LivingActive();
            if (eligible.Count == 0) return false;
            var fighter = eligible[rng.Next(eligible.Count)];
            var skills = OrderedSkills(fighter.Definition);
            if (skills.Count == 0) return false;
            var skill = skills[rng.Next(skills.Count)];
            RecordDraw(AddCard(team, fighter, skill, 1, CardKind.Skill, 0, true), drawnCards);
            return true;
        }

        private static CardState AddCard(BattleTeamState team, FighterState fighter, SkillDefinition skill, int rank,
            CardKind kind, int tier, bool insertAtFront = false)
        {
            var card = new CardState { Id = team.Side + ":card:" + team.NextCardSequence++,
                OwnerFighterId = fighter.Id, SkillId = skill?.Id, Rank = rank, Kind = kind, UltimateTier = tier };
            if (insertAtFront) team.Hand.Insert(0, card);
            else team.Hand.Add(card);
            return card;
        }

        private static void RecordDraw(CardState card, List<CardState> drawnCards)
        {
            if (card != null) drawnCards?.Add(card.Clone());
        }

        private static bool HasPendingUltimate(BattleTeamState team, string fighterId) =>
            team.Hand.Exists(c => c.OwnerFighterId == fighterId && c.Kind == CardKind.Ultimate);
    }
}
