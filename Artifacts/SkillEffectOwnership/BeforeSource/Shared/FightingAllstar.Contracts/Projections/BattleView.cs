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
        public List<BattleEntityView> Entities = new List<BattleEntityView>();
        public List<BattleEventView> Events = new List<BattleEventView>();
    }

    [Serializable]
    public sealed class BattleEntityView
    {
        public string EntityId;
        public string DefinitionId;
        public string OwnerFighterId;
        public string MarkerStatusInstanceId;
        public string Name;
        public string Category;
        public string PresentationKey;
        public string Side;
        public long SpawnOrder;
        public int RemainingDuration;
        public int Capacity;
        public int MaximumCapacity;
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
        public int Level = 1;
        public List<PublicElementStatusView> ElementStatuses = new List<PublicElementStatusView>();
        public List<PublicElementalReactionStatusView> ElementalReactionStatuses = new List<PublicElementalReactionStatusView>();
        public List<PublicStatusView> Statuses = new List<PublicStatusView>();
    }

    [Serializable]
    public sealed class PublicElementStatusView
    {
        public string InstanceId;
        public string Element;
        public int RemainingDuration;
        public string Polarity = "Neutral";
    }

    [Serializable]
    public sealed class PublicElementalReactionStatusView
    {
        public string InstanceId;
        public string Reaction;
        public int RemainingDuration;
        public string Polarity = "Neutral";
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
        public string LinkedEntityId;
        public bool IsRemovable = true;
        public string Polarity;
        public string Color;
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
        public BattleEntityView Entity;
        public string EntityId;
        public string EntityOwnerFighterId;
        public string EntityDefinitionId;
        public string EntityRemovalCause;
        public long EntityOccurrence;
        public int EntityCapacityAfter;
        public int EntityMaximumCapacity;
        public int EntityAbsorbedAmount;
        public int EntityRemainingDuration;
        public string DamageElement;
        public string TriggerElement;
        public bool ReactionOutputBlocked;
        public bool ElementalReactionStatusApplied;
        public bool ElementalReactionStatusAlreadyActive;
        public string ElementalReaction;
        public string DamageOrigin;
        public long ParentEventId;
        public List<string> ConsumedStatusInstanceIds = new List<string>();
        public List<PublicElementStatusView> ElementStatusesAfter;
        public List<PublicElementalReactionStatusView> ElementalReactionStatusesAfter;
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
        public int FormulaDamage;
        public int ConstructAbsorbed;
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
