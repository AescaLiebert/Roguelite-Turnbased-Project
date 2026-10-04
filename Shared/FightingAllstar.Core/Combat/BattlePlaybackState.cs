namespace FightingAllstar.Core.Combat
{
    /// <summary>Display-only event reducer. Never submitted back to the battle authority.</summary>
    public static class BattlePlaybackState
    {
        public static BattleState BeforeOpeningDeal(BattleState initial)
        {
            var display = initial.Clone();
            display.Player.Hand.Clear();
            display.Opponent.Hand.Clear();
            display.Events.Clear();
            return display;
        }

        public static void Apply(BattleState display, BattleEvent item)
        {
            var source = display.Player.FindFighter(item.SourceId) ?? display.Opponent.FindFighter(item.SourceId);
            var target = display.Player.FindFighter(item.TargetId) ?? display.Opponent.FindFighter(item.TargetId);
            var team = source == null ? null : display.Team(source.Side);
            if (source != null && item.PowerGaugeAfter >= 0 && item.Kind != BattleEventKind.PowerGaugeChanged)
                source.PowerGauge = item.PowerGaugeAfter;
            switch (item.Kind)
            {
                case BattleEventKind.PowerGaugeChanged:
                    if (target != null) target.PowerGauge = item.PowerGaugeAfter;
                    break;
                case BattleEventKind.CardRemoved:
                    team?.Hand.RemoveAll(c => c.Id == item.CardId);
                    break;
                case BattleEventKind.CardRankChanged:
                    var rankedCard = team?.Hand.Find(c => c.Id == item.CardId);
                    if (rankedCard != null) rankedCard.Rank = item.Amount;
                    break;
                case BattleEventKind.PassiveStatsChanged:
                    if (target != null)
                    {
                        target.Health = item.HealthAfter;
                        target.PassiveContributions.Clear();
                        foreach (var contribution in item.PassiveContributions) target.PassiveContributions.Add(contribution.Clone());
                    }
                    break;
                case BattleEventKind.TurnStarted:
                    display.ActingSide = item.SourceId == TeamSide.Player.ToString() ? TeamSide.Player : TeamSide.Opponent;
                    display.ActionBudget = item.Amount;
                    break;
                case BattleEventKind.CardDrawn:
                    if (team != null && item.Card != null && !team.Hand.Exists(c => c.Id == item.CardId))
                        team.Hand.Add(item.Card.Clone());
                    break;
                case BattleEventKind.CardMoved:
                    var moved = team?.Hand.Find(c => c.Id == item.CardId);
                    if (moved != null)
                    {
                        team.Hand.Remove(moved);
                        team.Hand.Insert(System.Math.Max(0, System.Math.Min(item.DestinationIndex, team.Hand.Count)), moved);
                    }
                    break;
                case BattleEventKind.CardPlayed:
                    team?.Hand.RemoveAll(c => c.Id == item.CardId);
                    break;
                case BattleEventKind.CardsMerged:
                    team?.Hand.RemoveAll(c => c.Id == item.ConsumedCardId);
                    var survivor = team?.Hand.Find(c => c.Id == item.CardId);
                    if (survivor != null) survivor.Rank = item.Amount;
                    break;
                case BattleEventKind.DamageApplied:
                    if (target != null) { target.Health = item.HealthAfter; target.Shield = item.ShieldAfter; }
                    break;
                case BattleEventKind.HealApplied:
                    if (target != null) target.Health = item.HealthAfter;
                    break;
                case BattleEventKind.FighterDefeated:
                    if (source != null)
                    {
                        source.IsAlive = false;
                        source.Health = source.PowerGauge = source.Shield = 0;
                        team.Hand.RemoveAll(c => c.OwnerFighterId == source.Id);
                    }
                    break;
                case BattleEventKind.ReserveEntered:
                    if (source != null) { source.IsReserve = false; source.FormationSlot = item.DestinationIndex; }
                    break;
            }
        }
    }
}
