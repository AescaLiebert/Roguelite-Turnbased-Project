using System;
using System.Diagnostics;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Combat.AI;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;

internal static partial class BattlePlaybackChecks
{
    private static CardState AiCard(BattleState state, int slot, EffectDefinition effect, CardCategory category = CardCategory.Attack)
    {
        var owner = state.Team(state.ActingSide).LivingActive()[0];
        var skill = owner.Definition.Skills[slot - 1];
        skill.Category = category;
        foreach (var rank in skill.Ranks) rank.Effect = effect.Clone();
        var card = new CardState { Id = "ai-card:" + slot, OwnerFighterId = owner.Id, SkillId = skill.Id,
            Kind = CardKind.Skill, Rank = 1, Category = category, TargetScope = effect.Target };
        state.Team(state.ActingSide).Hand.Add(card);
        return card;
    }

    private static CandidatePlan AiCandidate(params (CardState card, string target)[] actions)
    {
        var plan = new CandidatePlan { StrategyName = "fixture" };
        foreach (var action in actions) plan.Actions.Add(new PlannedAction { CardId = action.card.Id, TargetFighterId = action.target });
        return plan;
    }

    private static void ElementalAiChecks()
    {
        var start = _assertions;
        var state = ElementFixture(ElementalRuleSet.CreateDefault(), side: TeamSide.Opponent);
        state.Opponent.Hand.Clear(); state.ActionBudget = 2;
        var setup = AiCard(state, 1, ElementAttack(ElementId.Cryo));
        var pyro = AiCard(state, 2, ElementAttack(ElementId.Pyro));
        var target = state.Player.Fighters[0].Id;
        var profile = new AiPersonalityProfile { NoisePercent = 0 };
        var observation = AiPlanningObservation.Create(state);
        Check(observation.Player.Hand.Count == 0 && observation.Opponent.Hand.Count == 2 && observation.Events.Count == 0,
            "Planning observation copied private opponent cards or history.");
        var ordered = ElementalPlanEvaluator.Evaluate(AiCandidate((setup, target), (pyro, target)), state);
        var reversed = ElementalPlanEvaluator.Evaluate(AiCandidate((pyro, target), (setup, target)), state);
        Check(ordered.IsValid && reversed.IsValid && ordered.Damage > reversed.Damage,
            "Outcome evaluation did not value setup before amplification.");
        Check(reversed.Control == 0 && ordered.Control == 0, "Neutral aura gained negative-effect control value.");
        var candidates = CandidateBuilder.Generate(BoardAnalyzer.Analyze(observation), observation, profile);
        Check(candidates.Count <= profile.MaxCandidates && candidates.Any(c => c.StrategyName == "ElementalOrder" && c.Actions[0].CardId == setup.Id && c.Actions[1].CardId == pyro.Id),
            "Bounded candidate generation omitted prepare/trigger order.");
        var before = SnapshotJson.Serialize(state);
        var best = EnemyAiPlanner.CreatePlan(state, profile);
        Check(best != null && best.Actions.Count == 2 && best.Actions[0].CardId == setup.Id && best.Actions[1].CardId == pyro.Id &&
            best.Actions[0].TargetFighterId == best.Actions[1].TargetFighterId, "Enemy failed to choose the controlled setup/amplification combo.");
        Check(SnapshotJson.Serialize(state) == before, "Planning mutated live state, hand, events or entropy.");
        var hidden = state.Clone(); hidden.RngState = 17; hidden.RngDrawCount = 923; hidden.CardRngState = 881; hidden.CardRngDrawCount = 482;
        hidden.Player.Hand.Clear(); hidden.Player.Hand.Add(new CardState { Id = "hidden-injected-card", SkillId = "invalid-private-content" });
        hidden.Events.Clear();
        var hiddenPlan = EnemyAiPlanner.CreatePlan(hidden, profile);
        string Fingerprint(TurnPlan p) => string.Join(";", p.Actions.Select(a => a.CardId + ":" + a.TargetFighterId + ":" + a.DestinationIndex));
        Check(Fingerprint(best) == Fingerprint(hiddenPlan), "Enemy selection depended on private hand/history or live random streams.");
        var draft = new PlanDraft(state);
        foreach (var action in best.Actions) Check(draft.QueuePlay(action.CardId, action.TargetFighterId, out _), "Selected combo was not legal.");
        var preview = draft.Forecast();
        Check(!preview.Lines.Any(l => l.ActionIndex == 0), "Pure preparation incorrectly forecast an automatic reaction.");
        Check(preview.Lines.Any(l => l.ActionIndex == 1 && l.Reaction == ElementalReactionId.Melt && !l.Conditional),
            "Forecast omitted a deterministic aura setup/trigger.");

        var hold = ElementFixture(ElementalRuleSet.CreateDefault(), side: TeamSide.Opponent);
        hold.Opponent.Hand.Clear(); hold.ActionBudget = 1;
        var physical = AiCard(hold, 1, ElementAttack(ElementId.None));
        var convert = AiCard(hold, 2, ElementAttack(ElementId.Pyro));
        foreach (var victim in hold.Player.LivingActive())
            StatusSystem.Apply(victim, hold.Opponent.Fighters[0].Id, TeamSide.Opponent,
                StandardEffectDatabase.CreateStatusRecipes().Find(r => r.Id == "status.debuff.shock"), "valuable:" + victim.Id, "fixture", 1,
                snapshot: new StatusSnapshot { TriggeringHealthDamage = 10000 }, duration: 4);
        var spent = ElementalPlanEvaluator.Evaluate(AiCandidate((convert, hold.Player.Fighters[0].Id)), hold);
        var kept = ElementalPlanEvaluator.Evaluate(AiCandidate((physical, hold.Player.Fighters[0].Id)), hold);
        Check(spent.FutureDamage == kept.FutureDamage, "Elemental contact incorrectly consumed or synchronized ordinary DOT.");
        var retained = EnemyAiPlanner.CreatePlan(hold, profile);
        Check(retained != null && retained.Actions.Count == 1, "Planner failed to retain a legal action after DOT separation.");

        var freeze = ElementFixture(ElementalRuleSet.CreateDefault(), side: TeamSide.Opponent);
        freeze.Opponent.Hand.Clear(); freeze.ActionBudget = 1;
        var cryo = AiCard(freeze, 1, ElementAttack(ElementId.Cryo));
        ElementAura(freeze.Player.Fighters[0], ElementId.Hydro, "freeze-ai");
        var control = ElementalPlanEvaluator.Evaluate(AiCandidate((cryo, freeze.Player.Fighters[0].Id)), freeze);
        Check(control.Control > 0, "Accepted Frozen had no control value.");
        StatusSystem.Apply(freeze.Player.Fighters[0], freeze.Player.Fighters[0].Id, TeamSide.Player,
            new StatusRecipeDefinition { Id = "fixture.debuff-immunity", Polarity = StatusPolarity.Buff, DebuffImmunity = true }, "immune-ai", "fixture", 1);
        var immune = ElementalPlanEvaluator.Evaluate(AiCandidate((cryo, freeze.Player.Fighters[0].Id)), freeze);
        Check(immune.Control == 0, "Rejected Frozen gained fictitious control value.");
        var invalid = new CandidatePlan(); invalid.Actions.Add(new PlannedAction { CardId = "missing", TargetFighterId = target });
        Check(!ElementalPlanEvaluator.Evaluate(invalid, state).IsValid, "Invalid plans received elemental utility.");
        Check(PlanSelector.Select(new System.Collections.Generic.List<ScoredPlan> { new ScoredPlan { Candidate = invalid, TotalScore = 100 } }, state, profile) == null,
            "Selector returned an invalid fallback plan.");
        var mixed = ElementFixture(mixedReserve: true);
        var option = new CandidatePlan();
        Check(UtilityScorer.Score(option, BoardAnalyzer.Analyze(mixed), mixed, profile).ElementalValue == null,
            "Elemental scoring leaked into an ineligible starting roster.");
        Console.WriteLine("PASS: " + (_assertions - start) + " elemental AI ordering, retention, legality and privacy assertions.");
    }

    private static void ElementalAcceptanceChecks()
    {
        var start = _assertions;
        var watch = Stopwatch.StartNew();
        var turns = 0;
        long planningMs = 0, longestPlanMs = 0;
        for (var seed = 1; seed <= 3; seed++)
        {
            var roster = GenshinStarterRoster.Create();
            var player = roster.Skip(seed - 1).Concat(roster.Take(seed - 1)).Take(4).ToArray();
            var opponent = roster.AsEnumerable().Reverse().Take(4).ToArray();
            var battle = BattleEngine.Create("elemental-acceptance:" + seed, player, new[] { 5, 5, 5, 5 }, opponent, new[] { 5, 5, 5, 5 }, (ulong)seed);
            for (var turn = 0; turn < 8 && !battle.Winner.HasValue && !battle.IsDraw; turn++)
            {
                var restored = SnapshotJson.Deserialize<BattleState>(SnapshotJson.Serialize(battle));
                var planningWatch = Stopwatch.StartNew();
                var plan = EnemyAiPlanner.CreatePlan(restored, new AiPersonalityProfile { NoisePercent = 0, MaxCandidates = 20 });
                planningMs += planningWatch.ElapsedMilliseconds; longestPlanMs = Math.Max(longestPlanMs, planningWatch.ElapsedMilliseconds);
                Check(plan != null && plan.Actions.Count > 0 && plan.Actions.Count <= restored.ActionBudget, "Integrated enemy planner stalled or exceeded its budget.");
                plan.RequestId = "acceptance:" + seed + ":" + turn;
                var eventStart = restored.Events.Count;
                Check(BattleEngine.TryResolvePlan(restored, plan, out var next, out var error), "Integrated plan failed: " + error);
                Check(BattleEngine.TryResolvePlan(battle, plan, out var repeated, out error) && SnapshotJson.Serialize(next) == SnapshotJson.Serialize(repeated),
                    "Save/resume changed authority outcomes or random streams.");
                var playback = battle.Clone();
                foreach (var evt in next.Events.Skip(eventStart)) BattlePlaybackState.Apply(playback, evt);
                foreach (var side in new[] { TeamSide.Player, TeamSide.Opponent })
                foreach (var fighter in next.Team(side).Fighters)
                {
                    var displayed = playback.Team(side).FindFighter(fighter.Id);
                    Check(fighter.Health == displayed.Health && fighter.Shield == displayed.Shield && fighter.PowerGauge == displayed.PowerGauge &&
                        fighter.PowerGaugeRemainderBp == displayed.PowerGaugeRemainderBp && fighter.IsAlive == displayed.IsAlive,
                        "Replay/skip lost HP, shield, gauge, fraction or defeat.");
                    Check(SnapshotJson.Serialize(fighter.Statuses) == SnapshotJson.Serialize(displayed.Statuses), "Replay/skip lost status clocks or snapshots.");
                    Check(SnapshotJson.Serialize(fighter.ElementStatuses) == SnapshotJson.Serialize(displayed.ElementStatuses), "Replay/skip lost neutral aura state.");
                }
                var view = BattleProjectionBuilder.Build(next, next.ActingSide, "acceptance", long.MaxValue);
                Check(view.OpponentTeam.Hand.Count == 0, "Reconnect projection exposed private cards.");
                Check(next.Events.Count - eventStart < 2000, "Integrated elemental playback exceeded the finite-event stress limit.");
                battle = next; turns++;
            }
        }
        Console.WriteLine("PASS: " + (_assertions - start) + " G–H integrated replay/save/privacy assertions; " + turns + " turns in " + watch.ElapsedMilliseconds + " ms. Planning mean/max: " + planningMs / Math.Max(1, turns) + "/" + longestPlanMs + " ms (desktop fixture, not an Android benchmark).");
    }
}
