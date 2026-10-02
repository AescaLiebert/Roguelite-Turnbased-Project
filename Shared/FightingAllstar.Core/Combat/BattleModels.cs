using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;

namespace FightingAllstar.Core.Combat
{
    public enum BattlePhase { Setup, TurnStart, Planning, Resolving, TurnEnd, Complete }
    public enum TeamSide { Player, Opponent }
    public enum CardKind { Skill, Ultimate }
    public enum BattleEventKind { BattleStarted, TurnStarted, CardDrawn, CardsMerged, CardMoved, CardPlayed, DamageApplied, FighterDefeated, ReserveEntered, TurnEnded, BattleCompleted, ActionFizzled }

    [Serializable]
    public sealed class FighterState
    {
        public string Id;
        public TeamSide Side;
        public CharacterDefinition Definition;
        public int TeamIndex;
        public int FormationSlot;
        public bool IsReserve;
        public bool IsAlive = true;
        public int Health;
        public int Shield;
        public int PowerGauge;
        public int ConstellationTier;

        public FighterState Clone() => new FighterState { Id = Id, Side = Side, Definition = Definition?.Clone(), TeamIndex = TeamIndex,
            FormationSlot = FormationSlot, IsReserve = IsReserve, IsAlive = IsAlive, Health = Health, Shield = Shield,
            PowerGauge = PowerGauge, ConstellationTier = ConstellationTier };
        public StatBlock Stats => Definition.BaseStats;
    }

    [Serializable]
    public sealed class CardState
    {
        public string Id;
        public string OwnerFighterId;
        public string SkillId;
        public int Rank;
        public CardKind Kind;
        public int UltimateTier;

        public CardState Clone() => (CardState)MemberwiseClone();
    }

    [Serializable]
    public sealed class BattleTeamState
    {
        public TeamSide Side;
        public long NextCardSequence;
        public int HandCapacity;
        public List<FighterState> Fighters = new List<FighterState>();
        public List<CardState> Hand = new List<CardState>();

        public BattleTeamState Clone()
        {
            var copy = new BattleTeamState { Side = Side, NextCardSequence = NextCardSequence, HandCapacity = HandCapacity };
            foreach (var f in Fighters) copy.Fighters.Add(f.Clone());
            foreach (var c in Hand) copy.Hand.Add(c.Clone());
            return copy;
        }
        public List<FighterState> LivingActive()
        {
            var result = new List<FighterState>();
            foreach (var f in Fighters) if (f.IsAlive && !f.IsReserve) result.Add(f);
            result.Sort((a, b) => a.FormationSlot != b.FormationSlot
                ? a.FormationSlot.CompareTo(b.FormationSlot) : a.TeamIndex.CompareTo(b.TeamIndex));
            return result;
        }
        public FighterState FindFighter(string id) => Fighters.Find(x => x.Id == id);
    }

    [Serializable]
    public sealed class BattleState
    {
        public string MatchId;
        public long Revision;
        public int TurnNumber;
        public int CompletedTurnCount;
        public TeamSide ActingSide;
        public BattlePhase Phase;
        public int ActionBudget;
        public TeamSide? Winner;
        public bool IsDraw;
        public BattleTeamState Player = new BattleTeamState { Side = TeamSide.Player };
        public BattleTeamState Opponent = new BattleTeamState { Side = TeamSide.Opponent };
        public ulong RngState;
        public ulong RngDrawCount;
        public List<BattleEvent> Events = new List<BattleEvent>();
        public List<RunBoonDefinition> RunBoons = new List<RunBoonDefinition>();

        public BattleState Clone()
        {
            var copy = new BattleState { MatchId = MatchId, Revision = Revision, TurnNumber = TurnNumber, CompletedTurnCount = CompletedTurnCount,
                ActingSide = ActingSide, Phase = Phase, ActionBudget = ActionBudget, Winner = Winner, IsDraw = IsDraw,
                Player = Player.Clone(), Opponent = Opponent.Clone(), RngState = RngState, RngDrawCount = RngDrawCount };
            foreach (var e in Events) copy.Events.Add(e.Clone());
            foreach (var boon in RunBoons) copy.RunBoons.Add(boon.Clone());
            return copy;
        }
        public BattleTeamState Team(TeamSide side) => side == TeamSide.Player ? Player : Opponent;
        public BattleTeamState OtherTeam(TeamSide side) => side == TeamSide.Player ? Opponent : Player;
    }

    [Serializable]
    public sealed class BattleEvent
    {
        public long Id;
        public BattleEventKind Kind;
        public string SourceId;
        public string TargetId;
        public string CardId;
        public int Amount;
        public int HealthAfter;
        public int ShieldAfter;
        public string Message;
        public BattleEvent Clone() => (BattleEvent)MemberwiseClone();
    }

    [Serializable]
    public sealed class PlannedAction
    {
        public bool IsMove;
        public string CardId;
        public string TargetFighterId;
        public int DestinationIndex = -1;
        public PlannedAction Clone() => (PlannedAction)MemberwiseClone();
    }

    [Serializable]
    public sealed class TurnPlan
    {
        public string RequestId;
        public long ExpectedRevision;
        public List<PlannedAction> Actions = new List<PlannedAction>();
    }
}
