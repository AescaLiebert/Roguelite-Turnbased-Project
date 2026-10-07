using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat.AI
{
    // ──────────────────────────────────────────────────────────
    //  Situation snapshot produced by BoardAnalyzer
    // ──────────────────────────────────────────────────────────

    /// <summary>Immutable snapshot of the battlefield from the AI's perspective.</summary>
    public sealed class BattleSituation
    {
        // --- Own team (the AI side) ---
        public List<FighterProfile> OwnFighters = new List<FighterProfile>();
        public List<CardOption> OwnHand = new List<CardOption>();
        public int ActionBudget;
        public bool AnyUltimateReady;
        public int TurnNumber;

        // --- Merge opportunities ---
        public List<MergeCandidate> MergeCandidates = new List<MergeCandidate>();

        // --- Player team (what the AI can see without reading their hand) ---
        public List<ThreatProfile> PlayerFighters = new List<ThreatProfile>();
        public ThreatProfile MostDangerous;
        public ThreatProfile Weakest;
        public bool PlayerHasUltimateReady;

        // --- Tactical context ---
        public float OwnTeamHealthRatio;
        public float PlayerTeamHealthRatio;
        public bool IsLosingFight;
        public bool CanKillAnyoneThisTurn;
        public int ActiveDebuffsOnPlayer;
        public int ActiveBuffsOnSelf;
        public bool AnyOwnFighterLowHp;
    }

    // ──────────────────────────────────────────────────────────
    //  Fighter profiles
    // ──────────────────────────────────────────────────────────

    public enum FighterRole { Dps, Tank, Support, Debuffer, Hybrid }

    /// <summary>Analyzed profile of an AI-owned fighter.</summary>
    public sealed class FighterProfile
    {
        public string FighterId;
        public FighterState State;
        public StatBlock EffectiveStats;
        public float HealthRatio;       // 0.0 – 1.0
        public int PowerGauge;
        public bool UltimateReady;
        public FighterRole Role;
        public int AttackCardCount;     // How many attack cards this fighter owns in hand
        public int RecoveryCardCount;
        public int DebuffCardCount;
    }

    /// <summary>What the AI can see about a player fighter (no hand peeking).</summary>
    public sealed class ThreatProfile
    {
        public string FighterId;
        public FighterState State;
        public StatBlock EffectiveStats;
        public float HealthRatio;
        public int PowerGauge;
        public bool UltimateReady;      // PG >= 5
        public float EstimatedDps;      // Based on Attack stat and role
        public bool HasStance;
        public bool HasTaunt;
        public int DebuffCount;
        public bool EstimatedKillable;  // Can the AI kill this fighter this turn?
        public int EstimatedDamageToKill;
    }

    // ──────────────────────────────────────────────────────────
    //  Card & merge models
    // ──────────────────────────────────────────────────────────

    /// <summary>A card in the AI's hand with pre-computed metadata.</summary>
    public sealed class CardOption
    {
        public string CardId;
        public CardState Card;
        public FighterState Owner;
        public int HandIndex;           // Position in the hand list
        public CardCategory Category;
        public EffectTargetScope TargetScope;
        public int Rank;
        public bool IsUltimate;
        public bool IsPlayable;         // Owner alive, PG sufficient for ult, and not status-blocked
        public bool IsDisabled;         // Selectable as a discard, but its card effect will not resolve
        public bool CanMerge;           // A same-type card exists in hand
        public float BaseUtility;       // Quick heuristic score (rank, category, etc.)
    }

    /// <summary>Two cards that could merge if moved adjacent.</summary>
    public sealed class MergeCandidate
    {
        public CardOption CardA;
        public CardOption CardB;
        public int MoveFromIndex;       // Which card to move
        public int MoveToIndex;         // Where to move it (adjacent to the other)
        public int ResultingRank;       // Rank after merge (2 or 3)
        public float MergeValue;        // Estimated value of the merge
    }

    // ──────────────────────────────────────────────────────────
    //  Candidate plan models
    // ──────────────────────────────────────────────────────────

    /// <summary>A complete turn plan candidate, ready for scoring and validation.</summary>
    public sealed class CandidatePlan
    {
        public string StrategyName;
        public List<PlannedAction> Actions = new List<PlannedAction>();
        public float EstimatedDamage;
        public float EstimatedHealing;
        public int MergesTriggered;
        public int KillsEstimated;
        public int PgGained;
        public bool UsedUltimate;
        public List<string> TargetedFighterIds = new List<string>();
        public string Description;

        public CandidatePlan Clone()
        {
            var copy = new CandidatePlan
            {
                StrategyName = StrategyName, EstimatedDamage = EstimatedDamage,
                EstimatedHealing = EstimatedHealing, MergesTriggered = MergesTriggered,
                KillsEstimated = KillsEstimated, PgGained = PgGained, UsedUltimate = UsedUltimate,
                Description = Description
            };
            foreach (var a in Actions) copy.Actions.Add(a.Clone());
            foreach (var id in TargetedFighterIds) copy.TargetedFighterIds.Add(id);
            return copy;
        }
    }

    /// <summary>A candidate plan with its final computed score.</summary>
    public sealed class ScoredPlan
    {
        public CandidatePlan Candidate;
        public float TotalScore;
        public float[] DimensionScores;     // Per-dimension breakdown for debugging
        public bool SimulationValid;        // Passed PlanDraft validation?
        public TurnPlan FinalPlan;          // The TurnPlan to submit
    }

    // ──────────────────────────────────────────────────────────
    //  Simulation result
    // ──────────────────────────────────────────────────────────

    public sealed class SimulationResult
    {
        public bool IsValid;
        public int ActionsCompleted;        // How many actions succeeded before a fizzle
        public int MergesOccurred;
        public int PgAfterPlan;             // Total PG across team after the plan
        public int HandSizeAfterPlan;
        public string FailureReason;
    }
}
