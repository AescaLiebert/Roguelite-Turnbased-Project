using System;
using FightingAllstar.Contracts.Projections;

namespace FightingAllstar.Core.Combat
{
    /// <summary>Creates a recipient-scoped view without exposing the authoritative snapshot or the other hand.</summary>
    public static class BattleProjectionBuilder
    {
        public static BattleView Build(BattleState state, TeamSide recipientSide, string recipientSubjectId, long afterEventId = 0)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (string.IsNullOrWhiteSpace(recipientSubjectId)) throw new ArgumentException("A verified recipient subject is required.", nameof(recipientSubjectId));
            if (afterEventId < 0) throw new ArgumentOutOfRangeException(nameof(afterEventId));

            var own = state.Team(recipientSide);
            var opponent = state.OtherTeam(recipientSide);
            var view = new BattleView
            {
                MatchId = state.MatchId,
                RecipientSubjectId = recipientSubjectId,
                Revision = state.Revision,
                TurnNumber = state.TurnNumber,
                ActingSide = state.ActingSide.ToString(),
                Phase = state.Phase.ToString(),
                ActionBudget = state.ActingSide == recipientSide ? state.ActionBudget : 0,
                Winner = state.Winner.HasValue ? state.Winner.Value.ToString() : null,
                IsDraw = state.IsDraw,
                OwnTeam = BuildTeam(own, true),
                OpponentTeam = BuildTeam(opponent, false),
                OpponentHandSize = opponent.Hand == null ? 0 : opponent.Hand.Count
            };

            if (state.Events != null)
                foreach (var battleEvent in state.Events)
                {
                    if (battleEvent == null || battleEvent.Id <= afterEventId) continue;
                    view.Events.Add(BuildEvent(state, battleEvent, recipientSide));
                    view.LatestEventId = Math.Max(view.LatestEventId, battleEvent.Id);
                }
            return view;
        }

        private static BattleTeamView BuildTeam(BattleTeamState team, bool includeHand)
        {
            var view = new BattleTeamView();
            if (team == null) return view;
            if (team.Fighters != null)
                foreach (var fighter in team.Fighters)
                {
                    if (fighter == null) continue;
                    view.Fighters.Add(new FighterView
                    {
                        FighterId = fighter.Id,
                        DefinitionId = fighter.Definition == null ? null : fighter.Definition.Id,
                        Side = fighter.Side.ToString(),
                        TeamIndex = fighter.TeamIndex,
                        FormationSlot = fighter.FormationSlot,
                        IsReserve = fighter.IsReserve,
                        IsAlive = fighter.IsAlive,
                        Health = fighter.Health,
                        MaxHealth = StatusSystem.GetEffectiveStats(fighter).MaxHealth,
                        Shield = fighter.Shield,
                        PowerGauge = fighter.PowerGauge
                    });
                }
            if (includeHand && team.Hand != null)
                foreach (var card in team.Hand)
                {
                    if (card == null) continue;
                    view.Hand.Add(new CardView
                    {
                        CardId = card.Id,
                        OwnerFighterId = card.OwnerFighterId,
                        SkillId = card.SkillId,
                        Rank = card.Rank,
                        Kind = card.Kind.ToString(),
                        Category = card.Category.ToString(),
                        EffectCategory = CardRules.GetEffectCategory(card).ToString(),
                        TargetScope = card.TargetScope.ToString(),
                        UltimateTier = card.UltimateTier
                    });
                }
            return view;
        }

        private static BattleEventView BuildEvent(BattleState state, BattleEvent battleEvent, TeamSide recipientSide)
        {
            var source = FindFighter(state, battleEvent.SourceId);
            var privateCardDraw = battleEvent.Kind == BattleEventKind.CardDrawn && source != null && source.Side != recipientSide;
            return new BattleEventView
            {
                EventId = battleEvent.Id,
                HitIndex = battleEvent.HitIndex,
                HitCount = battleEvent.HitCount,
                AttackRange = battleEvent.AttackRange.ToString(),
                Kind = battleEvent.Kind.ToString(),
                SourceId = battleEvent.SourceId,
                TargetId = battleEvent.TargetId,
                TargetIds = battleEvent.TargetIds == null ? new System.Collections.Generic.List<string>() :
                    new System.Collections.Generic.List<string>(battleEvent.TargetIds),
                CardId = privateCardDraw ? null : battleEvent.CardId,
                Amount = battleEvent.Amount,
                HealthAfter = battleEvent.HealthAfter,
                ShieldAfter = battleEvent.ShieldAfter,
                ShieldLost = battleEvent.ShieldLost,
                EffectiveMaxHealth = battleEvent.EffectiveMaxHealth,
                PowerGaugeAfter = battleEvent.PowerGaugeAfter,
                RootActionId = battleEvent.RootActionId,
                WasCritical = battleEvent.WasCritical,
                WasBlocked = battleEvent.WasBlocked,
                WasEndured = battleEvent.WasEndured,
                Message = battleEvent.Message
            };
        }

        private static FighterState FindFighter(BattleState state, string fighterId)
        {
            if (string.IsNullOrEmpty(fighterId)) return null;
            return state.Player.FindFighter(fighterId) ?? state.Opponent.FindFighter(fighterId);
        }
    }
}
