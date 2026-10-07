using System;
using System.Collections.Generic;

namespace FightingAllstar.Contracts.Projections
{
    /// <summary>Recipient-specific battle view. Opponent cards and private RNG state have no fields in this contract.</summary>
    [Serializable]
    public sealed class BattleView
    {
        public string MatchId;
        public string RecipientSubjectId;
        public long Revision;
        public long LatestEventId;
        public int TurnNumber;
        public string ActingSide;
        public string Phase;
        public int ActionBudget;
        public string Winner;
        public bool IsDraw;
        public BattleTeamView OwnTeam;
        public BattleTeamView OpponentTeam;
        public int OpponentHandSize;
        public List<BattleEventView> Events = new List<BattleEventView>();
    }

    [Serializable]
    public sealed class BattleTeamView
    {
        public List<FighterView> Fighters = new List<FighterView>();
        public List<CardView> Hand = new List<CardView>();
    }

    [Serializable]
    public sealed class FighterView
    {
        public string FighterId;
        public string DefinitionId;
        public string Side;
        public int TeamIndex;
        public int FormationSlot;
        public bool IsReserve;
        public bool IsAlive;
        public int Health;
        public int MaxHealth;
        public int Shield;
        public int PowerGauge;
    }

    [Serializable]
    public sealed class CardView
    {
        public string CardId;
        public string OwnerFighterId;
        public string SkillId;
        public int Rank;
        public string Kind;
        public string Category;
        public string EffectCategory;
        public string TargetScope;
        public int UltimateTier;
    }

    [Serializable]
    public sealed class BattleEventView
    {
        public long EventId;
        public int HitIndex;
        public int HitCount;
        public string AttackRange;
        public string Kind;
        public string SourceId;
        public string TargetId;
        public string CardId;
        public List<string> TargetIds = new List<string>();
        public int Amount;
        public int HealthAfter;
        public int ShieldAfter;
        public int ShieldLost;
        public int EffectiveMaxHealth;
        public int PowerGaugeAfter = -1;
        public string RootActionId;
        public bool WasCritical;
        public bool WasBlocked;
        public bool WasEndured;
        public string Message;
    }
}
