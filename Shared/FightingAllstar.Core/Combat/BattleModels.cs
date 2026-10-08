using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;

namespace FightingAllstar.Core.Combat
{
    public enum BattlePhase { Setup, TurnStart, Planning, Resolving, TurnEnd, Complete }
    public enum TeamSide { Player, Opponent }
    public enum CardKind { Skill, Ultimate }
    public enum BattleEventKind { BattleStarted, TurnStarted, CardDrawn, CardsMerged, CardMoved, CardPlayed, DamageApplied, HealApplied, StatusApplied, StatusRemoved, FighterDefeated, ReserveEntered, TurnEnded, BattleCompleted, ActionFizzled,
        PassiveStatsChanged, PassiveTriggered, PowerGaugeChanged, CardRemoved, CardRankChanged, CounterStarted, CounterEnded, AttackEvaded, StatusesChanged, ActionTiming, ActionCompleted, StatusResolutionStarted, StatusResolutionEnded, StatusImmuned, RecoveryBlocked, HitStarted, StatusWeaker }

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
        public bool PowerGaugeDisabled;
        public bool CannotDie;
        public int ConstellationTier;
        public int OwnerTurnsCompleted;
        public StatusContainer Statuses = new StatusContainer();
        public List<PassiveCounterState> PassiveCounters = new List<PassiveCounterState>();
        public List<PassiveStatContribution> PassiveContributions = new List<PassiveStatContribution>();

        public FighterState Clone()
        {
            var copy = new FighterState { Id = Id, Side = Side, Definition = Definition?.Clone(), TeamIndex = TeamIndex,
            FormationSlot = FormationSlot, IsReserve = IsReserve, IsAlive = IsAlive, Health = Health, Shield = Shield,
            PowerGauge = PowerGauge, PowerGaugeDisabled = PowerGaugeDisabled, CannotDie = CannotDie,
            ConstellationTier = ConstellationTier, OwnerTurnsCompleted = OwnerTurnsCompleted,
            Statuses = Statuses?.Clone() ?? new StatusContainer() };
            foreach (var counter in PassiveCounters) copy.PassiveCounters.Add(counter.Clone());
            foreach (var contribution in PassiveContributions) copy.PassiveContributions.Add(contribution.Clone());
            return copy;
        }
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
        public CardCategory Category;
        public CardCategory EffectCategory;
        public EffectTargetScope TargetScope = EffectTargetScope.SelectedEnemy;
        public int UltimateTier;

        public CardState Clone() => (CardState)MemberwiseClone();
    }

    [Serializable]
    public sealed class BattleTeamState
    {
        public TeamSide Side;
        public long NextCardSequence;
        public int HandCapacity;
        public bool TrainingDeck;
        public List<FighterState> Fighters = new List<FighterState>();
        public List<CardState> Hand = new List<CardState>();

        public BattleTeamState Clone()
        {
            var copy = new BattleTeamState { Side = Side, NextCardSequence = NextCardSequence, HandCapacity = HandCapacity, TrainingDeck = TrainingDeck };
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
        public bool IsTraining;
        public BattleModeMask Mode = BattleModeMask.PvE;
        public long PassiveEventSequence;
        public BattleTeamState Player = new BattleTeamState { Side = TeamSide.Player };
        public BattleTeamState Opponent = new BattleTeamState { Side = TeamSide.Opponent };
        public ulong RngState;
        public ulong RngDrawCount;
        public ulong CardRngState;
        public ulong CardRngDrawCount;
        public List<BattleEvent> Events = new List<BattleEvent>();
        public List<RunBoonDefinition> RunBoons = new List<RunBoonDefinition>();
        public List<string> ActivePassiveAuras = new List<string>();
        public List<PendingPassiveEffect> PendingPassiveEffects = new List<PendingPassiveEffect>();

        public BattleState Clone()
        {
            var copy = new BattleState { MatchId = MatchId, Revision = Revision, TurnNumber = TurnNumber, CompletedTurnCount = CompletedTurnCount,
                ActingSide = ActingSide, Phase = Phase, ActionBudget = ActionBudget, Winner = Winner, IsDraw = IsDraw,
                Mode = Mode, IsTraining = IsTraining, PassiveEventSequence = PassiveEventSequence,
                Player = Player.Clone(), Opponent = Opponent.Clone(), RngState = RngState, RngDrawCount = RngDrawCount,
                CardRngState = CardRngState, CardRngDrawCount = CardRngDrawCount };
            foreach (var e in Events) copy.Events.Add(e.Clone());
            foreach (var boon in RunBoons) copy.RunBoons.Add(boon.Clone());
            foreach (var aura in ActivePassiveAuras) copy.ActivePassiveAuras.Add(aura);
            foreach (var pending in PendingPassiveEffects)
                copy.PendingPassiveEffects.Add(pending?.Clone());
            return copy;
        }
        public BattleTeamState Team(TeamSide side) => side == TeamSide.Player ? Player : Opponent;
        public BattleTeamState OtherTeam(TeamSide side) => side == TeamSide.Player ? Opponent : Player;
    }

    [Serializable]
    public sealed class PendingPassiveEffect
    {
        public string EffectOwnerId;
        public string ActorId;
        public string SelectedTargetId;
        public string RootActionId;
        public string ReactionId;
        public CardCategory CardCategory;
        public int CardRank;
        public bool IsUltimate;
        public DamageFamily DamageFamily;
        public bool WasCritical;
        public bool WasBlocked;
        public CardEffectOperationDefinition Operation;

        public PendingPassiveEffect Clone() => new PendingPassiveEffect {
            EffectOwnerId = EffectOwnerId, ActorId = ActorId, SelectedTargetId = SelectedTargetId,
            RootActionId = RootActionId, ReactionId = ReactionId,
            CardCategory = CardCategory, CardRank = CardRank,
            IsUltimate = IsUltimate, DamageFamily = DamageFamily, WasCritical = WasCritical,
            WasBlocked = WasBlocked, Operation = Operation?.Clone() };
    }

    [Serializable]
    public sealed class BattleEvent
    {
        public int HitIndex;
        public int HitCount;
        public AttackRange AttackRange;
        public long Id;
        public CardEffectTiming Timing;
        public BattleEventKind Kind;
        public string SourceId;
        public string TargetId;
        public string CardId;
        public string RootActionId;
        public List<StatusInstance> StatusesAfter;
        public string StatusInstanceId;
        public string StatusRecipeId;
        public StatusApplyOutcome StatusOutcome;
        public int Amount;
        public int HealthAfter;
        public int ShieldAfter;
        public bool ShieldChanged;
        public string Message;
        // Immutable-at-emission presentation facts; never infer these from the final hand.
        public CardState Card;
        public List<string> TargetIds = new List<string>();
        public string ConsumedCardId;
        public int DestinationIndex = -1;
        public int PowerGaugeAfter = -1;
        public bool WasCritical;
        public bool WasBlocked;
        public bool WasEndured;
        public AttributeAffinity Affinity;
        public int ShieldLost;
        public int EffectiveMaxHealth;
        public List<PassiveStatContribution> PassiveContributions = new List<PassiveStatContribution>();
        public BattleEvent Clone()
        {
            var copy = (BattleEvent)MemberwiseClone();
            copy.StatusesAfter = StatusesAfter?.ConvertAll(status => status.Clone());
            copy.Card = Card?.Clone();
            copy.TargetIds = TargetIds == null ? new List<string>() : new List<string>(TargetIds);
            copy.PassiveContributions = new List<PassiveStatContribution>();
            foreach (var contribution in PassiveContributions) copy.PassiveContributions.Add(contribution.Clone());
            return copy;
        }
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
