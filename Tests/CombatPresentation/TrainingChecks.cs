using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

internal static partial class BattlePlaybackChecks
{
    private static void TrainingChecks()
    {
        var player = Fighter("practice-player", 100000, 10);
        var dummy = Fighter("practice-dummy", 100000, 10);
        var battle = BattleEngine.Create("practice", new[] { player }, null, new[] { dummy }, null, 42, training: true);
        Check(battle.IsTraining && battle.ActionBudget == 1, "Training must allow one action.");
        Check(battle.Player.Hand.Count == 7 && battle.Player.Hand.Count(c => c.Kind == CardKind.Skill) == 6,
            "Training must deal both skills at every rank and an ultimate.");
        Check(battle.Opponent.Fighters[0].PowerGauge == 0, "Dummy received opening PG.");
        Check(CharacterPassiveRuntime.ChangePowerGauge(battle, battle.Player.Fighters[0], battle.Opponent.Fighters[0], 5) == 0,
            "Dummy accepted effect-driven PG.");
        var cloned = SnapshotJson.Deserialize<BattleState>(SnapshotJson.Serialize(battle));
        Check(cloned.IsTraining && cloned.Player.TrainingDeck && cloned.Opponent.Fighters[0].PowerGaugeDisabled,
            "Training rules were lost during serialization.");
        var session = new LocalBattleSession(battle);
        foreach (var card in battle.Player.Hand.Where(c => c.Kind == CardKind.Skill))
        {
            var rankDraft = new PlanDraft(battle);
            Check(rankDraft.QueuePlay(card.Id, battle.Opponent.Fighters[0].Id, out _), "A training skill rank was not playable.");
            rankDraft.Reset();
            Check(rankDraft.Preview.Hand.Count == 7 && rankDraft.RemainingActions == 1, "Reset failed to restore the training draft.");
        }
        for (var turn = 0; turn < 23; turn++)
        {
            var before = session.GetSnapshot();
            var ultimate = before.Player.Hand.First(c => c.Kind == CardKind.Ultimate);
            var draft = new PlanDraft(before);
            Check(draft.QueuePlay(ultimate.Id, before.Opponent.Fighters[0].Id, out var error), "Free training ultimate blocked: " + error);
            Check(!draft.QueuePlay(before.Player.Hand.First(c => c.Kind == CardKind.Skill).Id, before.Opponent.Fighters[0].Id, out _),
                "Training allowed a second player action.");
            var plan = draft.BuildPlan("training-turn:" + turn);
            var lastEvent = before.Events.Last().Id;
            Check(session.Submit(plan, out error), "Training turn failed: " + error);
            var after = session.GetSnapshot();
            Check(after.ActingSide == TeamSide.Player && after.Phase == BattlePhase.Planning && after.ActionBudget == 1,
                "Training failed to return control after the dummy turn.");
            Check(after.Opponent.Fighters[0].PowerGauge == 0 && after.Opponent.Hand.All(c => c.Kind != CardKind.Ultimate),
                "Dummy gained PG or an ultimate.");
            Check(after.Player.Hand.Count == 7 && after.Player.Hand.Any(c => c.Kind == CardKind.Ultimate), "Training hand was not replenished.");
            Check(session.GetEventsAfter(lastEvent).Count(e => e.Kind == BattleEventKind.CardPlayed && e.SourceId == after.Opponent.Fighters[0].Id) == 1,
                "Dummy must play exactly one card per turn.");
            Check(session.Submit(plan, out error) && session.GetSnapshot().Revision == after.Revision, "Retry duplicated a training turn.");
        }
        Check(session.GetSnapshot().CompletedTurnCount > 40 && !session.GetSnapshot().IsDraw, "Training inherited the dungeon turn limit.");
        Check(player.BaseStats.MaxHealth == 100000 && dummy.BaseStats.MaxHealth == 100000, "Practice mutated source character data.");
        var normal = BattleEngine.Create("normal", new[] { player }, null, new[] { dummy }, null, 42);
        Check(!normal.IsTraining && normal.ActionBudget == 2 && normal.Player.Hand.Count == 4 && !normal.Opponent.Fighters[0].PowerGaugeDisabled,
            "Training rules leaked into normal battles.");
        normal.Player.Fighters[0].PowerGauge = 0;
        var gatedUltimate = battle.Player.Hand.First(c => c.Kind == CardKind.Ultimate).Clone();
        gatedUltimate.OwnerFighterId = normal.Player.Fighters[0].Id;
        normal.Player.Hand.Add(gatedUltimate);
        Check(!new PlanDraft(normal).QueuePlay(gatedUltimate.Id, normal.Opponent.Fighters[0].Id, out _),
            "Normal battles lost the ultimate PG requirement.");
    }
}
