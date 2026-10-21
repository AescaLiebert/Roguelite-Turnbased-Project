using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;

namespace CharacterCreatorStudio.Checks
{
    internal static class M5BattleLabChecks
    {
        public static int Run(CharacterDefinition baseline)
        {
            var checks = 0;
            void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException("M5: " + message); checks++; }
            var enemy = baseline.Clone(); enemy.Id = "fixture.lab.target"; enemy.Passive = new PassiveDefinition { Id = "passive.lab.target" };
            enemy.BaseStats.MaxHealth = 200000;
            var config = new BattleLabConfiguration { FirstTurn = BattleLabFirstTurn.Player,
                Actors = new List<BattleLabFighter> { new BattleLabFighter { Definition = StudioCompiler.Compile(StudioDocumentService.ImportOpeningGauge(baseline)).Character } },
                Targets = new List<BattleLabFighter> { new BattleLabFighter { Definition = enemy } } };
            var bytes = SnapshotJson.Serialize(config);
            var real = BattleLabSimulation.Create(config, out var encounter);
            Check(!real.IsTraining && !real.Player.TrainingDeck && !real.Player.Fighters[0].CannotDie && !real.Opponent.Fighters[0].CannotDie, "Real Battle keeps normal defeat/deck rules.");
            Check(real.CapturePassiveTrace && real.PassiveTrace.Count > 0, "Opening passive facts are captured through the production engine.");
            Check(real.ActionBudget == 2 && real.Player.Hand.Count > 0, "Real 1v1 uses the normal action budget and dealt hand.");
            Check(encounter.PlayerTeam[0].FighterId == real.Player.Fighters[0].Id && encounter.EnemyTeam[0].FighterId == real.Opponent.Fighters[0].Id, "3D encounter identities match authoritative state.");
            Check(SnapshotJson.Serialize(config) == bytes, "Creation preserves source setup and definitions.");
            var reset = BattleLabSimulation.Create(config, out _);
            Check(StudioSimulation.StateHash(reset) == StudioSimulation.StateHash(real) && reset.RngState == real.RngState && reset.CardRngState == real.CardRngState, "Restart with the same setup/seed reproduces state and both streams.");
            var cloned = config.Clone(); cloned.Actors[0].Definition.BaseStats.Attack++;
            Check(SnapshotJson.Serialize(config) == bytes, "Frozen launch clone isolates subsequent source edits.");
            var session = new LocalBattleSession(real);
            var initial = session.GetSnapshot(); var draft = new PlanDraft(initial);
            var card = initial.Player.Hand.First(c => c.Kind == CardKind.Skill);
            Check(draft.QueuePlay(card.Id, initial.Opponent.Fighters[0].Id, out var error), "Quick action uses the real plan validator: " + error);
            var plan = draft.BuildPlan("lab.real.action");
            Check(session.Submit(plan, out error), "Real Battle commits a normal turn: " + error);
            var after = session.GetSnapshot();
            Check(after.Events.Any(e => e.Kind == BattleEventKind.DamageApplied && e.SourceId == initial.Player.Fighters[0].Id), "Actor damage is committed by the real resolver.");
            Check(after.Events.Any(e => e.Kind == BattleEventKind.CardPlayed && e.SourceId == initial.Opponent.Fighters[0].Id), "Normal enemy AI responds.");
            Check(after.CompletedTurnCount >= 2 && after.ActingSide == TeamSide.Player, "Both sides advance real turn clocks.");
            Check(session.Submit(plan, out _) && session.GetSnapshot().Revision == after.Revision, "Retry does not duplicate a lab turn.");
            var tampered = session.GetSnapshot(); tampered.Player.Fighters[0].Health = 0;
            Check(session.GetSnapshot().Player.Fighters[0].Health > 0, "Inspector snapshots cannot mutate authority.");
            config.Mode = BattleLabMode.TrainingBattle;
            var training = BattleLabSimulation.Create(config, out _); var practice = new LocalBattleSession(training, passiveOpponent: true);
            Check(training.IsTraining && training.Player.TrainingDeck && training.Player.Fighters[0].CannotDie && training.Opponent.Fighters[0].CannotDie, "Training delegates no-defeat and deck rules to the existing engine.");
            Check(training.Player.Hand.Count == 7 && training.ActionBudget == 1 && training.Opponent.Fighters[0].PowerGaugeDisabled, "Training exposes all ranks/free ultimate and one action.");
            var beforePractice = practice.GetSnapshot(); var practicePlan = new PlanDraft(beforePractice);
            var ultimate = beforePractice.Player.Hand.First(c => c.Kind == CardKind.Ultimate);
            Check(practicePlan.QueuePlay(ultimate.Id, beforePractice.Opponent.Fighters[0].Id, out error), "Training ultimate is available without gauge.");
            Check(practice.Submit(practicePlan.BuildPlan("lab.training"), out error), "Training action resolves normally: " + error);
            var practiced = practice.GetSnapshot();
            Check(!practiced.Events.Any(e => e.Kind == BattleEventKind.CardPlayed && e.SourceId == beforePractice.Opponent.Fighters[0].Id), "Idle target commits an empty response without inventing an attack.");
            Check(practiced.ActingSide == TeamSide.Player && practiced.Player.Hand.Count == 7, "Practice returns to a refreshed hand.");
            Check(practiced.Opponent.Fighters[0].Health == beforePractice.Opponent.Fighters[0].Health, "Practice remains repeatable without depleting target HP.");
            config.Mode = BattleLabMode.RealBattle; config.Targets[0].StatusRecipeId = "status.debuff.bleed";
            var dot = BattleLabSimulation.Create(config, out _); var dotSession = new LocalBattleSession(dot, passiveOpponent: true);
            Check(dot.Opponent.Fighters[0].Statuses.Instances.Any(s => s.RecipeId == "status.debuff.bleed"), "Starting status is installed on the cloned target.");
            Check(dotSession.Submit(new TurnPlan { RequestId = "lab.dot", ExpectedRevision = dot.Revision }, out error), "Pass turn advances normal status clocks.");
            Check(dotSession.GetSnapshot().Events.Any(e => e.Kind == BattleEventKind.DamageApplied && e.DamageOrigin == ElementalDamageOrigin.DamageOverTime), "Actual DOT resolution occurs after passing.");
            config.Targets[0].StatusRecipeId = ""; config.Actors[0].Level = 50; config.Actors[0].HealthPercent = 40; config.Actors[0].Gauge = 4; config.Actors[0].Shield = 123;
            var configured = BattleLabSimulation.Create(config, out var configuredEncounter); var fighter = configured.Player.Fighters[0];
            Check(fighter.Level == 50 && fighter.Definition.BaseStats.MaxHealth == configuredEncounter.PlayerTeam[0].Stats.MaxHealth, "Selected level reaches opening rules and progression stats.");
            Check(configuredEncounter.PlayerTeam[0].CurrentHealth == configuredEncounter.PlayerTeam[0].Stats.MaxHealth * 40L / 100 && fighter.Health >= configuredEncounter.PlayerTeam[0].CurrentHealth, "HP override enters creation before normal opening/turn-start recovery.");
            Check(fighter.PowerGauge == 4 && fighter.Shield >= 123, "Gauge and shield setup overrides remain local.");
            var raw = SnapshotJson.Serialize(config); var changedSeed = config.Clone(); changedSeed.Seed++;
            Check(BattleLabSimulation.Create(changedSeed, out _).RngState != configured.RngState && SnapshotJson.Serialize(config) == raw, "New seed changes RNG without rewriting the frozen setup.");
            config.FirstTurn = BattleLabFirstTurn.Opponent; var enemyFirst = BattleLabSimulation.Create(config, out _); var enemyFirstSession = new LocalBattleSession(enemyFirst);
            Check(enemyFirst.ActingSide == TeamSide.Opponent && enemyFirstSession.GetSnapshot().ActingSide == TeamSide.Player, "Enemy-first Real Battle uses the normal automatic AI entry.");
            var team = config.Clone(); team.FirstTurn = BattleLabFirstTurn.Player;
            for (var i = 1; i < 4; i++) { var a = baseline.Clone(); a.Id += "." + i; var t = enemy.Clone(); t.Id += "." + i; team.Actors.Add(new BattleLabFighter { Definition = a }); team.Targets.Add(new BattleLabFighter { Definition = t }); }
            var four = BattleLabSimulation.Create(team, out var fourEncounter);
            Check(four.Player.LivingActive().Count == 3 && four.Player.Fighters[3].IsReserve && fourEncounter.PlayerTeam[3].IsReserve, "Real Battle supports three active plus reserve with matching presentation data.");
            Check(four.ActionBudget == 3 && four.Opponent.Fighters.Count == 4, "Team-sized action economy stays authoritative.");
            bool Reject(Action action) { try { action(); return false; } catch (ArgumentException) { return true; } }
            team.Mode = BattleLabMode.TrainingBattle; Check(Reject(() => BattleLabSimulation.Create(team, out _)), "Training rejects accidental multi-fighter setup.");
            var invalid = config.Clone(); invalid.Actors[0].HealthPercent = 0; Check(Reject(() => BattleLabSimulation.Create(invalid, out _)), "Invalid opening HP rejected.");
            invalid = config.Clone(); invalid.Targets[0].StatusRecipeId = "absent"; Check(Reject(() => BattleLabSimulation.Create(invalid, out _)), "Missing setup status rejected.");
            invalid = config.Clone(); invalid.Actors[0].Gauge = 6; Check(Reject(() => BattleLabSimulation.Create(invalid, out _)), "Invalid gauge rejected.");
            invalid = config.Clone(); invalid.Targets.Clear(); Check(Reject(() => BattleLabSimulation.Create(invalid, out _)), "Empty targets rejected before launch.");
            Console.WriteLine("PASS: " + checks + " M5 Battle Lab state, real AI, training, status, seed and isolation assertions."); return checks;
        }
    }
}



