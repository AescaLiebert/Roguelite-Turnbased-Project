using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;

namespace FightingAllstar.Core.Combat
{
    public static class BattleEngine
    {
        public const int MaximumCompletedTurns = 40;

        public static BattleState Create(string matchId, IReadOnlyList<CharacterDefinition> player,
            IReadOnlyList<int> playerTiers, IReadOnlyList<CharacterDefinition> opponent, IReadOnlyList<int> opponentTiers,
            ulong seed, TeamSide firstSide = TeamSide.Player, IReadOnlyList<RunBoonDefinition> runBoons = null)
        {
            if (string.IsNullOrWhiteSpace(matchId)) throw new ArgumentException("A stable match id is required.", nameof(matchId));
            var state = new BattleState { MatchId = matchId, Revision = 1, TurnNumber = 1, ActingSide = firstSide, Phase = BattlePhase.Setup };
            AddTeam(state.Player, player, playerTiers);
            AddTeam(state.Opponent, opponent, opponentTiers);
            state.Player.HandCapacity = CardRules.GetHandCapacity(state.Player);
            state.Opponent.HandCapacity = CardRules.GetHandCapacity(state.Opponent);
            AddEvent(state, BattleEventKind.BattleStarted, null, null, null, 0, "Battle initialized.");
            if (runBoons != null)
                foreach (var boon in runBoons)
                {
                    state.RunBoons.Add(boon.Clone());
                    if (boon.EffectKind == RunBoonEffectKind.StartPowerGauge && state.Player.LivingActive().Count > 0)
                    {
                        var first = state.Player.LivingActive()[0];
                        first.PowerGauge = Math.Min(CardRules.UltimateGaugeCost, first.PowerGauge + Math.Max(0, boon.MagnitudePoints));
                    }
                }
            var rng = new DeterministicRandom(seed);
            DrawOpeningHand(state, state.Player, rng);
            DrawOpeningHand(state, state.Opponent, rng);
            BeginTurn(state, firstSide, rng, false);
            state.RngState = rng.State;
            state.RngDrawCount = rng.DrawCount;
            return state;
        }

        public static bool TryResolvePlan(BattleState current, TurnPlan plan, out BattleState next, out string error)
        {
            next = null;
            error = null;
            if (current == null || plan == null) { error = "Battle or plan is missing."; return false; }
            if (current.Phase != BattlePhase.Planning || current.Winner.HasValue || current.IsDraw) { error = "The battle is not accepting a plan."; return false; }
            if (plan.ExpectedRevision != current.Revision) { error = "Battle revision is stale."; return false; }
            if (string.IsNullOrWhiteSpace(plan.RequestId)) { error = "Plan requires an idempotency request id."; return false; }
            if (plan.Actions == null || plan.Actions.Count > current.ActionBudget) { error = "Plan exceeds the frozen action budget."; return false; }
            if (!ValidatePlan(current, plan, out error)) return false;

            var work = current.Clone();
            var team = work.Team(work.ActingSide);
            var rng = DeterministicRandom.Restore(work.RngState, work.RngDrawCount);
            work.Phase = BattlePhase.Resolving;
            foreach (var action in plan.Actions)
            {
                if (action == null) { error = "Plan contains an empty action."; return false; }
                if (!ApplyAction(work, team, action, rng, out error)) return false;
                if (work.Winner.HasValue || work.IsDraw) break;
            }
            if (!work.Winner.HasValue && !work.IsDraw) EndTurn(work, rng);
            work.Revision++;
            work.RngState = rng.State;
            work.RngDrawCount = rng.DrawCount;
            next = work;
            return true;
        }

        private static bool ValidatePlan(BattleState state, TurnPlan plan, out string error)
        {
            error = null;
            var draft = new PlanDraft(state);
            foreach (var action in plan.Actions)
            {
                if (action == null) { error = "Plan contains an empty action."; return false; }
                var valid = action.IsMove
                    ? draft.QueueMove(action.CardId, action.DestinationIndex, out error)
                    : draft.QueuePlay(action.CardId, action.TargetFighterId, out error);
                if (!valid) return false;
            }
            return true;
        }

        private static bool ApplyAction(BattleState state, BattleTeamState team, PlannedAction action, DeterministicRandom rng, out string error)
        {
            error = null;
            var index = team.Hand.FindIndex(c => c.Id == action.CardId);
            if (index < 0) { Fizzle(state, action, null, "The planned card is no longer available."); return true; }
            var card = team.Hand[index];
            var owner = team.FindFighter(card.OwnerFighterId);
            if (owner == null || !owner.IsAlive || owner.IsReserve)
            { Fizzle(state, action, card.OwnerFighterId, "The card owner is no longer active."); return true; }
            if (action.IsMove)
            {
                var merged = new List<string>();
                if (!CardRules.TryMove(team, index, action.DestinationIndex, true, out error, merged))
                { Fizzle(state, action, owner.Id, error); error = null; return true; }
                AddEvent(state, BattleEventKind.CardMoved, owner.Id, null, card.Id, 1, "Card moved.");
                AddMergeEvents(state, team, merged);
                return true;
            }

            if (card.Kind == CardKind.Ultimate && owner.PowerGauge < CardRules.UltimateGaugeCost)
            { Fizzle(state, action, owner.Id, "Ultimate lost readiness before it resolved."); return true; }
            var target = state.OtherTeam(state.ActingSide).FindFighter(action.TargetFighterId);
            if (target == null || !target.IsAlive || target.IsReserve)
                target = LowestValidTarget(state.OtherTeam(state.ActingSide));
            if (target == null) { Fizzle(state, action, owner.Id, "No living active target remained."); return true; }
            EffectDefinition effect;
            var found = card.Kind == CardKind.Skill
                ? CardRules.TryGetSkill(owner.Definition, card.SkillId, card.Rank, out effect)
                : CardRules.TryGetUltimate(owner.Definition, card.UltimateTier, out effect);
            if (!found) { Fizzle(state, action, owner.Id, "The planned card has no runtime effect definition."); return true; }
            team.Hand.RemoveAt(index);
            if (card.Kind == CardKind.Ultimate) owner.PowerGauge = 0;
            else owner.PowerGauge = Math.Min(CardRules.UltimateGaugeCost, owner.PowerGauge + 1);
            AddEvent(state, BattleEventKind.CardPlayed, owner.Id, target.Id, card.Id, card.Rank, "Card played.");
            var mergedAfterPlay = new List<string>();
            CardRules.MergeAdjacent(team, mergedAfterPlay, true);
            AddMergeEvents(state, team, mergedAfterPlay);
            var attacker = owner.Stats;
            var defender = target.Stats;
            var baseAmount = effect.Scaling switch
            {
                StatScaling.Attack => attacker.Attack,
                StatScaling.Defense => attacker.Defense,
                StatScaling.MaxHealth => attacker.MaxHealth,
                _ => effect.Magnitude
            };
            if (effect.Scaling == StatScaling.Fixed) baseAmount = effect.Magnitude;
            var p = new DamagePolicy { Family = effect.Family, BypassDefense = effect.Family == DamageFamily.True,
                BypassResistance = effect.Family == DamageFamily.True, BypassGenericReduction = effect.Family == DamageFamily.True,
                BypassShield = effect.Family == DamageFamily.True,
                BypassDamageCap = effect.Family == DamageFamily.Additional || effect.Family == DamageFamily.DamageOverTime,
                BypassSurviveAtOne = effect.Family == DamageFamily.Destructive };
            var packet = new DamagePacket { BaseAmount = baseAmount, CoefficientBp = effect.CoefficientBp, KeywordFactorBp = effect.KeywordFactorBp, Policy = p };
            var critRoll = -1;
            var blockRoll = -1;
            if (effect.Family != DamageFamily.Destructive)
            {
                critRoll = rng.NextBasisPoints();
                var critChance = Math.Max(0, Math.Min(10000, attacker.CritChanceBp - defender.CritResistanceBp));
                if (critRoll >= critChance) blockRoll = rng.NextBasisPoints();
            }
            var damage = DamageResolver.Resolve(packet, attacker, defender, target.Health, target.Shield, critRoll, blockRoll);
            target.Health = damage.RemainingHealth;
            target.Shield = damage.RemainingShield;
            AddEvent(state, BattleEventKind.DamageApplied, owner.Id, target.Id, card.Id, damage.HealthLost,
                (damage.WasCritical ? "Critical. " : string.Empty) + (damage.WasBlocked ? "Blocked. " : string.Empty));
            state.Events[state.Events.Count - 1].HealthAfter = target.Health;
            state.Events[state.Events.Count - 1].ShieldAfter = target.Shield;
            if (damage.Executed || target.Health <= 0)
            {
                target.Health = 0;
                target.IsAlive = false;
                target.PowerGauge = 0;
                CardRules.RemoveDeadOwnerCards(state.Team(target.Side), target.Id);
                var mergedAfterDeath = new List<string>();
                CardRules.MergeAdjacent(state.Team(target.Side), mergedAfterDeath, true);
                AddMergeEvents(state, state.Team(target.Side), mergedAfterDeath);
                AddEvent(state, BattleEventKind.FighterDefeated, target.Id, null, card.Id, 0, "Fighter defeated.");
                DeployReserve(state, target.Side, target.FormationSlot);
            }
            CheckOutcome(state);
            return true;
        }

        private static void Fizzle(BattleState state, PlannedAction action, string sourceId, string reason) =>
            AddEvent(state, BattleEventKind.ActionFizzled, sourceId, action?.TargetFighterId, action?.CardId, 0, reason);

        private static FighterState LowestValidTarget(BattleTeamState team)
        {
            var targets = team.LivingActive();
            targets.Sort((left, right) => left.FormationSlot != right.FormationSlot
                ? left.FormationSlot.CompareTo(right.FormationSlot) : left.TeamIndex.CompareTo(right.TeamIndex));
            return targets.Count == 0 ? null : targets[0];
        }

        private static void DrawOpeningHand(BattleState state, BattleTeamState team, DeterministicRandom rng)
        {
            var drawn = new List<CardState>();
            var merged = new List<string>();
            CardRules.DrawOpeningHand(team, rng, drawn, merged);
            AddCardRuleEvents(state, team, drawn, merged);
        }

        private static void AddCardRuleEvents(BattleState state, BattleTeamState team,
            List<CardState> drawnCards, List<string> mergedCardIds)
        {
            foreach (var card in drawnCards)
                AddEvent(state, BattleEventKind.CardDrawn, card.OwnerFighterId, null, card.Id,
                    card.Rank, card.Kind == CardKind.Ultimate ? "Ultimate became ready." : "Card drawn.");
            AddMergeEvents(state, team, mergedCardIds);
        }

        private static void AddMergeEvents(BattleState state, BattleTeamState team, List<string> mergedCardIds)
        {
            if (mergedCardIds == null) return;
            foreach (var id in mergedCardIds)
            {
                var card = team.Hand.Find(item => item.Id == id);
                if (card == null) continue;
                AddEvent(state, BattleEventKind.CardsMerged, card.OwnerFighterId, null, card.Id, card.Rank,
                    "Adjacent cards merged to rank " + card.Rank + ".");
            }
        }

        private static void AddTeam(BattleTeamState team, IReadOnlyList<CharacterDefinition> characters, IReadOnlyList<int> tiers)
        {
            if (characters == null || characters.Count < 1 || characters.Count > 4) throw new ArgumentException("A team must contain one to four fighters.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < characters.Count; i++)
            {
                var definition = characters[i];
                if (definition == null || !ids.Add(definition.Id)) throw new ArgumentException("Team has a missing or duplicate fighter definition.");
                var tier = tiers != null && i < tiers.Count ? tiers[i] : 0;
                if (tier < 0 || tier > 6) throw new ArgumentOutOfRangeException(nameof(tiers), "Constellation tier must be 0-6.");
                team.Fighters.Add(new FighterState { Id = team.Side + ":" + i + ":" + definition.Id, Side = team.Side, Definition = definition.Clone(),
                    TeamIndex = i, FormationSlot = Math.Min(i, 2), IsReserve = i >= 3, Health = definition.BaseStats.MaxHealth, ConstellationTier = tier });
            }
        }

        private static void BeginTurn(BattleState state, TeamSide side, DeterministicRandom rng, bool drawTurnCards = true)
        {
            var team = state.Team(side);
            if (team.LivingActive().Count == 0) DeployReserve(state, side, 0);
            if (team.LivingActive().Count == 0) { CheckOutcome(state); return; }
            state.ActingSide = side;
            state.Phase = BattlePhase.TurnStart;
            if (drawTurnCards)
            {
                var drawn = new List<CardState>();
                var merged = new List<string>();
                CardRules.StartTurn(team, rng, drawn, merged);
                AddCardRuleEvents(state, team, drawn, merged);
            }
            // Three active fighters expose three ordered action slots. A reduced formation
            // keeps two slots so a surviving fighter can still form a meaningful turn.
            state.ActionBudget = team.LivingActive().Count > 2 ? 3 : 2;
            state.Phase = BattlePhase.Planning;
            AddEvent(state, BattleEventKind.TurnStarted, side.ToString(), null, null, state.ActionBudget, "Turn started.");
        }

        private static void EndTurn(BattleState state, DeterministicRandom rng)
        {
            state.Phase = BattlePhase.TurnEnd;
            AddEvent(state, BattleEventKind.TurnEnded, state.ActingSide.ToString(), null, null, 0, "Turn ended.");
            state.CompletedTurnCount++;
            if (state.CompletedTurnCount >= MaximumCompletedTurns)
            {
                state.Phase = BattlePhase.Complete;
                state.IsDraw = true;
                state.ActionBudget = 0;
                AddEvent(state, BattleEventKind.BattleCompleted, null, null, null, 0, "Battle reached the 40-turn limit and ended in a draw.");
                return;
            }
            var next = state.ActingSide == TeamSide.Player ? TeamSide.Opponent : TeamSide.Player;
            if (state.ActingSide == TeamSide.Player) state.TurnNumber++;
            BeginTurn(state, next, rng);
        }

        private static void DeployReserve(BattleState state, TeamSide side, int openSlot)
        {
            var team = state.Team(side);
            var reserve = team.Fighters.Find(f => f.IsAlive && f.IsReserve);
            if (reserve == null) return;
            reserve.IsReserve = false;
            reserve.FormationSlot = openSlot;
            AddEvent(state, BattleEventKind.ReserveEntered, reserve.Id, null, null, 0, "Reserve entered the open formation slot.");
        }

        private static void CheckOutcome(BattleState state)
        {
            var playerAlive = state.Player.Fighters.Exists(f => f.IsAlive);
            var opponentAlive = state.Opponent.Fighters.Exists(f => f.IsAlive);
            if (playerAlive && opponentAlive) return;
            state.Phase = BattlePhase.Complete;
            if (!playerAlive && !opponentAlive) state.IsDraw = true;
            else state.Winner = playerAlive ? TeamSide.Player : TeamSide.Opponent;
            AddEvent(state, BattleEventKind.BattleCompleted, null, null, null, 0,
                state.IsDraw ? "Battle ended in a draw." : "Battle completed.");
        }

        private static void AddEvent(BattleState state, BattleEventKind kind, string source, string target, string card, int amount, string message)
        {
            state.Events.Add(new BattleEvent { Id = state.Events.Count + 1, Kind = kind, SourceId = source,
                TargetId = target, CardId = card, Amount = amount, Message = message });
        }
    }
}
