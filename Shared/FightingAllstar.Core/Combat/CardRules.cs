using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    public static class CardRules
    {
        public const int UltimateGaugeCost = 5;

        public static void DrawOpeningHand(BattleTeamState team, DeterministicRandom rng,
            List<CardState> drawnCards = null, List<string> mergedCardIds = null, List<BattleEvent> timeline = null)
        {
            // Deal Pos3 first and Pos1 last. The hand presents appended cards from right to left,
            // leaving the random draw on the left and the Pos3 group on the right.
            if (team.TrainingDeck) { DealTrainingHand(team, drawnCards, timeline); return; }
            var openingOrder = team.LivingActive();
            openingOrder.Reverse();
            foreach (var fighter in openingOrder)
            {
                var skills = OrderedSkills(fighter.Definition);
                for (var index = 0; index < skills.Count && index < 2; index++)
                {
                    RecordDraw(AddCard(team, fighter, skills[index], 1, CardKind.Skill, 0), drawnCards, timeline);
                    MergeAdjacent(team, mergedCardIds, timeline, grantGauge: true);
                }
            }
            DrawRandomUntilCapacity(team, rng, drawnCards, mergedCardIds, GetHandCapacity(team), timeline, grantGauge: true);
        }

        public static void StartTurn(BattleTeamState team, DeterministicRandom rng,
            List<CardState> drawnCards = null, List<string> mergedCardIds = null, List<BattleEvent> timeline = null)
        {
            if (team.TrainingDeck) { DealTrainingHand(team, drawnCards, timeline); return; }
            var capacity = GetHandCapacity(team);
            foreach (var fighter in team.LivingActive())
            {
                if (team.Hand.Count >= capacity) break;
                if (fighter.PowerGauge >= UltimateGaugeCost && !HasPendingUltimate(team, fighter.Id))
                {
                    RecordDraw(AddCard(team, fighter, null, 1, CardKind.Ultimate, fighter.ConstellationTier), drawnCards, timeline);
                }
            }
            DrawRandomUntilCapacity(team, rng, drawnCards, mergedCardIds, capacity, timeline, grantGauge: true);
        }

        // Every surviving formation member contributes to hand size, including an off-field SUB.
        // Once only the SUB survives and enters the field, the cap is therefore 1 + 3 = 4.
        public static int GetHandCapacity(BattleTeamState team)
        {
            if (team?.TrainingDeck == true)
            {
                var count = 0;
                foreach (var fighter in team.LivingActive())
                {
                    foreach (var skill in fighter.Definition.Skills) count += skill.Ranks.Count;
                    count++;
                }
                return count;
            }
            var survivingFormationSize = 0;
            if (team?.Fighters != null)
                foreach (var fighter in team.Fighters)
                    if (fighter != null && fighter.IsAlive && fighter.Health > 0) survivingFormationSize++;
            return survivingFormationSize + 3;
        }

        public static bool TryMove(BattleTeamState team, int from, int to, bool grantGauge, out string reason)
            => TryMove(team, from, to, grantGauge, out reason, null);

        public static bool TryMove(BattleTeamState team, int from, int to, bool grantGauge, out string reason,
            List<string> mergedCardIds, List<BattleEvent> timeline = null)
        {
            reason = null;
            if (from < 0 || from >= team.Hand.Count || to < 0 || to >= team.Hand.Count) { reason = "Card slot is outside the hand."; return false; }
            var card = team.Hand[from];
            if (from == to) { reason = "That move does not change the hand."; return false; }
            team.Hand.RemoveAt(from);
            team.Hand.Insert(to, card);
            if (grantGauge)
            {
                var owner = team.FindFighter(card.OwnerFighterId);
                if (owner != null && owner.IsAlive && !owner.IsReserve && !owner.PowerGaugeDisabled) owner.PowerGauge = Math.Min(UltimateGaugeCost, owner.PowerGauge + 1);
            }
            timeline?.Add(new BattleEvent { Kind = BattleEventKind.CardMoved, SourceId = card.OwnerFighterId,
                CardId = card.Id, Card = card.Clone(), DestinationIndex = to,
                PowerGaugeAfter = team.FindFighter(card.OwnerFighterId)?.PowerGauge ?? -1, Message = "Card moved." });
            MergeAdjacent(team, mergedCardIds, timeline, grantGauge);
            return true;
        }

        public static bool MergeAdjacent(BattleTeamState team, List<string> mergedCardIds = null,
            List<BattleEvent> timeline = null, bool grantGauge = true)
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
                    RefreshCardKind(left, team.FindFighter(left.OwnerFighterId)?.Definition);
                    team.Hand.RemoveAt(i + 1);
                    mergedCardIds?.Add(left.Id);
                    var owner = team.FindFighter(left.OwnerFighterId);
                    if (grantGauge && owner != null && owner.IsAlive && !owner.IsReserve && !owner.PowerGaugeDisabled)
                    {
                        owner.PowerGauge = Math.Min(UltimateGaugeCost, owner.PowerGauge + 1);
                    }
                    timeline?.Add(new BattleEvent { Kind = BattleEventKind.CardsMerged, SourceId = left.OwnerFighterId,
                        CardId = left.Id, Card = left.Clone(), ConsumedCardId = right.Id, Amount = left.Rank,
                        PowerGaugeAfter = grantGauge && owner != null ? owner.PowerGauge : -1,
                        Message = "RANK UP" });
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

        private static void DealTrainingHand(BattleTeamState team, List<CardState> drawnCards, List<BattleEvent> timeline)
        {
            foreach (var card in team.Hand)
                timeline?.Add(new BattleEvent { Kind = BattleEventKind.CardRemoved, SourceId = card.OwnerFighterId,
                    CardId = card.Id, Message = "Training hand refreshed." });
            team.Hand.Clear();
            var openingOrder = team.LivingActive();
            openingOrder.Reverse();
            foreach (var fighter in openingOrder)
            {
                foreach (var skill in OrderedSkills(fighter.Definition))
                    foreach (var rank in skill.Ranks)
                        RecordDraw(AddCard(team, fighter, skill, rank.Rank, CardKind.Skill, 0), drawnCards, timeline);
                RecordDraw(AddCard(team, fighter, null, 1, CardKind.Ultimate, fighter.ConstellationTier), drawnCards, timeline);
            }
        }

        private static List<SkillDefinition> OrderedSkills(CharacterDefinition definition)
        {
            var skills = new List<SkillDefinition>(definition.Skills);
            skills.Sort((a, b) => a.Slot != b.Slot ? a.Slot.CompareTo(b.Slot) : string.CompareOrdinal(a.Id, b.Id));
            return skills;
        }

        private static void DrawRandomUntilCapacity(BattleTeamState team, DeterministicRandom rng,
            List<CardState> drawnCards, List<string> mergedCardIds, int targetCapacity, List<BattleEvent> timeline,
            bool grantGauge = true)
        {
            while (team != null && team.Hand.Count < targetCapacity && DrawRandomCard(team, rng, drawnCards, timeline))
                MergeAdjacent(team, mergedCardIds, timeline, grantGauge);
        }

        private static bool DrawRandomCard(BattleTeamState team, DeterministicRandom rng, List<CardState> drawnCards, List<BattleEvent> timeline)
        {
            if (team == null || rng == null) return false;
            var eligible = team.LivingActive();
            if (eligible.Count == 0) return false;
            var fighter = eligible[rng.Next(eligible.Count)];
            var skills = OrderedSkills(fighter.Definition);
            if (skills.Count == 0) return false;
            var skill = skills[rng.Next(skills.Count)];
            RecordDraw(AddCard(team, fighter, skill, 1, CardKind.Skill, 0), drawnCards, timeline);
            return true;
        }

        private static CardState AddCard(BattleTeamState team, FighterState fighter, SkillDefinition skill, int rank,
            CardKind kind, int tier)
        {
            var ultimateScope = EffectTargetScope.SelectedEnemy;
            if (kind == CardKind.Ultimate && fighter != null && TryGetUltimate(fighter.Definition, tier, out var ultimateEffect))
                ultimateScope = ultimateEffect.Target;
            var effectCategory = kind == CardKind.Ultimate
                ? fighter?.Definition?.UltimateTiers?.Find(t => t.Tier == tier)?.Category ?? CardCategory.Attack
                : skill == null ? CardCategory.Attack : ResolveCategory(skill);
            var card = new CardState { Id = team.Side + ":card:" + team.NextCardSequence++,
                OwnerFighterId = fighter.Id, SkillId = skill?.Id, Rank = rank, Kind = kind,
                Category = kind == CardKind.Ultimate ? CardCategory.Ultimate : effectCategory,
                EffectCategory = effectCategory,
                TargetScope = kind == CardKind.Ultimate ? ultimateScope : skill == null ? EffectTargetScope.SelectedEnemy : ResolveTargetScope(skill),
                UltimateTier = tier };
            RefreshCardKind(card, fighter.Definition);
            team.Hand.Add(card);
            return card;
        }

        public static void RefreshCardKind(CardState card, CharacterDefinition definition)
        {
            if (card == null || card.Kind != CardKind.Skill) return;
            var skill = definition?.Skills?.Find(s => s.Id == card.SkillId);
            var rank = skill?.Ranks?.Find(r => r.Rank == card.Rank);
            if (rank?.HasCardKind != true) return;
            card.Category = rank.Category;
            card.EffectCategory = rank.Category;
            card.TargetScope = rank.TargetScope;
        }

        public static CardCategory GetEffectCategory(CardState card)
        {
            if (card == null) return CardCategory.Attack;
            if (card.Kind == CardKind.Ultimate)
            {
                // Older snapshots used Category for the ultimate's effect behavior.
                if (card.Category != CardCategory.Ultimate) return card.Category;
                return card.EffectCategory;
            }
            // Fall back to Category for cards/snapshots created before EffectCategory existed.
            if (card.EffectCategory == CardCategory.Attack && card.Category != CardCategory.Attack)
                return card.Category;
            return card.EffectCategory;
        }

        public static CardCategory ResolveCategory(SkillDefinition skill)
        {
            if (skill == null) return CardCategory.Attack;
            if (Enum.IsDefined(typeof(CardCategory), skill.Category) && skill.Category != CardCategory.Attack)
                return skill.Category;
            if (string.Equals(skill.SourceType, "DebuffAtk", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(skill.SourceType, "AttackDebuff", StringComparison.OrdinalIgnoreCase)) return CardCategory.AttackDebuff;
            if (string.Equals(skill.SourceType, "Heal", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(skill.SourceType, "Recovery", StringComparison.OrdinalIgnoreCase)) return CardCategory.Recovery;
            if (Enum.TryParse(skill.SourceType, true, out CardCategory category)) return category;
            return CardCategory.Attack;
        }

        public static EffectTargetScope ResolveTargetScope(SkillDefinition skill)
        {
            if (skill == null) return EffectTargetScope.SelectedEnemy;
            if (skill.TargetScope != EffectTargetScope.SelectedEnemy) return skill.TargetScope;
            return ResolveTargetScope(skill.SourceTarget);
        }

        public static EffectTargetScope ResolveTargetScope(string sourceTarget)
        {
            if (string.Equals(sourceTarget, "AOE", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sourceTarget, "AllEnemies", StringComparison.OrdinalIgnoreCase)) return EffectTargetScope.AllEnemies;
            if (string.Equals(sourceTarget, "AllAllies", StringComparison.OrdinalIgnoreCase)) return EffectTargetScope.AllAllies;
            if (string.Equals(sourceTarget, "Self", StringComparison.OrdinalIgnoreCase)) return EffectTargetScope.Self;
            if (string.Equals(sourceTarget, "SelectedAlly", StringComparison.OrdinalIgnoreCase)) return EffectTargetScope.SelectedAlly;
            return EffectTargetScope.SelectedEnemy;
        }

        private static void RecordDraw(CardState card, List<CardState> drawnCards, List<BattleEvent> timeline)
        {
            if (card != null) drawnCards?.Add(card.Clone());
            if (card != null) timeline?.Add(new BattleEvent { Kind = BattleEventKind.CardDrawn,
                SourceId = card.OwnerFighterId, CardId = card.Id, Card = card.Clone(), Amount = card.Rank,
                Message = card.Kind == CardKind.Ultimate ? "ULTIMATE READY" : "Card drawn." });
        }

        private static bool HasPendingUltimate(BattleTeamState team, string fighterId) =>
            team.Hand.Exists(c => c.OwnerFighterId == fighterId && c.Kind == CardKind.Ultimate);
    }
}
