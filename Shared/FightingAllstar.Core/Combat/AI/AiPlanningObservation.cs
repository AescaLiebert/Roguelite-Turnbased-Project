using System.Linq;

namespace FightingAllstar.Core.Combat.AI
{
    /// <summary>Detached planning inputs. Never copies the other hand, event history or live random streams.</summary>
    public static class AiPlanningObservation
    {
        public static BattleState Create(BattleState source)
        {
            if (source == null) return null;
            var copy = new BattleState {
                MatchId = source.MatchId, Revision = source.Revision, TurnNumber = source.TurnNumber,
                CompletedTurnCount = source.CompletedTurnCount, ActingSide = source.ActingSide,
                Phase = source.Phase, ActionBudget = source.ActionBudget, Mode = source.Mode,
                IsTraining = source.IsTraining, Winner = source.Winner, IsDraw = source.IsDraw,
                ElementalSchemaVersion = source.ElementalSchemaVersion, ElementalRules = source.ElementalRules?.Clone(),
                RngState = 0xA0761D6478BD642FUL, CardRngState = 0xE7037ED1A0B428DBUL
            };
            foreach (var side in new[] { TeamSide.Player, TeamSide.Opponent })
            {
                var original = source.Team(side);
                var team = copy.Team(side);
                team.ElementalReactionsEnabled = original.ElementalReactionsEnabled;
                team.HandCapacity = original.HandCapacity;
                team.TrainingDeck = original.TrainingDeck;
                team.Fighters = original.Fighters.Select(f => f.Clone()).ToList();
                if (side == source.ActingSide)
                {
                    team.Hand = original.Hand.Select(c => c.Clone()).ToList();
                    team.NextCardSequence = original.NextCardSequence;
                }
            }
            copy.ActivePassiveAuras.AddRange(source.ActivePassiveAuras);
            return copy;
        }
    }
}
