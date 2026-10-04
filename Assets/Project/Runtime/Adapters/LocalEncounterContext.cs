using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Run;
using CoreBattleState = FightingAllstar.Core.Combat.BattleState;

namespace FightingAllstar.Adapters
{
    /// <summary>Short-lived scene handoff for local practice. The durable run snapshot remains in LocalRunStateStore.</summary>
    public static class LocalEncounterContext
    {
        public static EncounterProjection PendingEncounter { get; private set; }
        public static ulong EncounterSeed { get; private set; }
        public static CoreBattleState CompletedBattle { get; private set; }
        public static bool IsPreview { get; private set; }

        public static void Begin(EncounterProjection encounter, ulong seed, bool previewOnly = false)
        {
            PendingEncounter = encounter;
            EncounterSeed = seed;
            CompletedBattle = null;
            IsPreview = previewOnly;
        }

        public static bool TryConsumeEncounter(out EncounterProjection encounter, out ulong seed)
        {
            encounter = PendingEncounter;
            seed = EncounterSeed;
            PendingEncounter = null;
            return encounter != null;
        }

        public static void StoreResult(CoreBattleState battle)
        {
            CompletedBattle = battle == null ? null : battle.Clone();
        }

        public static bool TryConsumeResult(out CoreBattleState battle)
        {
            battle = CompletedBattle;
            CompletedBattle = null;
            return battle != null;
        }

        public static void Clear()
        {
            PendingEncounter = null;
            CompletedBattle = null;
            EncounterSeed = 0;
            IsPreview = false;
        }
    }
}
