using System;
using System.Collections.Generic;

namespace FightingAllstar.Contracts.Projections
{
    /// <summary>Recipient-specific battle view. Opponent cards and private RNG state have no fields in this contract.</summary>
    [Serializable]
    public sealed class BattleView
    {
        public int ElementalSchemaVersion;
        public string ElementalRulesVersion;
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
        public bool ElementalReactionsEnabled;
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
        public int PowerGaugeRemainderBp;
        public bool EvadesAttacks;
        public bool HasTaunt;
        public bool DebuffImmune;
        public bool FreezeImmune;
        public List<PublicStatusView> Statuses = new List<PublicStatusView>();
    }

    [Serializable]
    public sealed class PublicStatusView
    {
        public string InstanceId;
        public string RecipeId;
        public string Name;
        public string Element;
        public bool EligibleFuel;
        public int StackCount;
        public int RemainingDuration;
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
        public string DamageElement;
        public string TriggerElement;
        public bool ReactionOutputBlocked;
        public string ElementalReaction;
        public string DamageOrigin;
        public long ParentEventId;
        public List<string> ConsumedStatusInstanceIds = new List<string>();
        public long EventId;
        public int HitIndex;
        public int HitCount;
        public string AttackRange;
        public bool HasHitReaction;
        public string Reaction;
        public bool WasReactionResisted;
        public bool WasStanceCancelled;
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
        public int PowerGaugeRemainderAfter = -1;
        public int GaugeDeltaBp;
        public string RootActionId;
        public bool WasCritical;
        public bool WasBlocked;
        public bool WasEndured;
        public string Message;
    }
}
