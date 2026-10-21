using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;

namespace FightingAllstar.Core.Studio
{
    public enum BattleLabMode { RealBattle, TrainingBattle }
    public enum BattleLabFirstTurn { CombatClass, Player, Opponent }
    [Serializable]
    public sealed class BattleLabFighter
    {
        public CharacterDefinition Definition;
        public int Level = 1, Tier, HealthPercent = 100, Gauge = -1, Shield, StatusDamage = -1;
        public string StatusRecipeId = "";
    }
    [Serializable]
    public sealed class BattleLabConfiguration
    {
        public BattleLabMode Mode;
        public BattleLabFirstTurn FirstTurn;
        public ulong Seed = 12345;
        public List<BattleLabFighter> Actors = new List<BattleLabFighter>();
        public List<BattleLabFighter> Targets = new List<BattleLabFighter>();
        public BattleLabConfiguration Clone() => SnapshotJson.Deserialize<BattleLabConfiguration>(SnapshotJson.Serialize(this));
    }
    /// <summary>Disposable in-memory setup over the production engine. No scenario persistence or economy writes.</summary>
    public static class BattleLabSimulation
    {
        public static BattleState Create(BattleLabConfiguration settings, out EncounterProjection encounter)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var config = settings.Clone();
            if (!Enum.IsDefined(typeof(BattleLabMode), config.Mode) || !Enum.IsDefined(typeof(BattleLabFirstTurn), config.FirstTurn)) throw new ArgumentException("Unsupported lab mode/first turn.");
            Validate(config.Actors); Validate(config.Targets);
            var training = config.Mode == BattleLabMode.TrainingBattle;
            if (training && (config.Actors.Count != 1 || config.Targets.Count != 1)) throw new ArgumentException("Training Battle is a single actor versus one target.");
            encounter = new EncounterProjection { BattleId = "battle-lab" };
            Populate(config.Actors, encounter.PlayerTeam, TeamSide.Player);
            Populate(config.Targets, encounter.EnemyTeam, TeamSide.Opponent);
            var side = training || config.FirstTurn == BattleLabFirstTurn.Player ? TeamSide.Player : config.FirstTurn == BattleLabFirstTurn.Opponent ? TeamSide.Opponent :
                encounter.PlayerTeam.Sum(f => f.Stats.CombatClass) >= encounter.EnemyTeam.Sum(f => f.Stats.CombatClass) ? TeamSide.Player : TeamSide.Opponent;
            var battle = BattleEngine.Create(encounter.BattleId, encounter.PlayerTeam.Select(f => f.Definition).ToList(), config.Actors.Select(f => f.Tier).ToList(),
                encounter.EnemyTeam.Select(f => f.Definition).ToList(), config.Targets.Select(f => f.Tier).ToList(), config.Seed, side,
                playerHealth: encounter.PlayerTeam.Select(f => f.CurrentHealth).ToList(), opponentHealth: encounter.EnemyTeam.Select(f => f.CurrentHealth).ToList(),
                cardDrawSeed: config.Seed, training: training, playerLevels: config.Actors.Select(f => f.Level).ToList(), opponentLevels: config.Targets.Select(f => f.Level).ToList(), capturePassiveTrace: true);
            ApplySetup(battle, battle.Player, config.Actors); ApplySetup(battle, battle.Opponent, config.Targets);
            CharacterPassiveRuntime.Refresh(battle);
            return battle;
        }
        private static void Validate(List<BattleLabFighter> fighters)
        {
            if (fighters == null || fighters.Count < 1 || fighters.Count > 4) throw new ArgumentException("Select 1–4 fighters per team (fourth is reserve).");
            foreach (var f in fighters)
            {
                if (f?.Definition == null || string.IsNullOrWhiteSpace(f.Definition.Id)) throw new ArgumentException("Select a runtime-ready fighter for every enabled slot.");
                if (f.Level < 1 || f.Level > CharacterProgression.MaxLevel || f.Tier < 0 || f.Tier > 5 || f.HealthPercent < 1 || f.HealthPercent > 100 || f.Gauge < -1 || f.Gauge > 5 || f.Shield < 0 || f.StatusDamage < -1)
                    throw new ArgumentException("Invalid lab level/tier/HP/gauge/shield. Use HP 1–100%, gauge -1 (runtime) or 0–5.");
                if (f.StatusRecipeId != "" && !StandardEffectDatabase.CreateStatusRecipes().Any(s => s.Id == f.StatusRecipeId)) throw new ArgumentException("Unknown setup status " + f.StatusRecipeId);
            }
        }
        private static void Populate(List<BattleLabFighter> fighters, List<EncounterFighterSnapshot> team, TeamSide side)
        {
            for (var i = 0; i < fighters.Count; i++)
            {
                var f = fighters[i]; var definition = f.Definition.Clone(); var stats = CharacterProgression.StatsAtLevel(definition.BaseStats, definition.StatGrowth, f.Level);
                definition.BaseStats = stats.Clone();
                team.Add(new EncounterFighterSnapshot { FighterId = side + ":" + i + ":" + definition.Id, DefinitionId = definition.Id, Definition = definition, Stats = stats,
                    Level = f.Level, ConstellationTier = f.Tier, FormationSlot = Math.Min(i, 2), IsReserve = i == 3, CurrentHealth = Math.Max(1, (int)((long)stats.MaxHealth * f.HealthPercent / 100)) });
            }
        }
        private static void ApplySetup(BattleState battle, BattleTeamState team, List<BattleLabFighter> settings)
        {
            for (var i = 0; i < settings.Count; i++)
            {
                var fighter = team.Fighters[i]; var f = settings[i];
                if (f.Gauge >= 0 && !fighter.PowerGaugeDisabled) { fighter.PowerGauge = f.Gauge; fighter.PowerGaugeRemainderBp = 0; }
                if (f.Shield > 0) fighter.Shield += f.Shield;
                if (string.IsNullOrEmpty(f.StatusRecipeId)) continue;
                var recipe = StandardEffectDatabase.CreateStatusRecipes().First(s => s.Id == f.StatusRecipeId).Clone();
                var source = battle.OtherTeam(fighter.Side).Fighters[0]; var stats = StatusSystem.GetEffectiveStats(source);
                var order = battle.Player.Fighters.Concat(battle.Opponent.Fighters).SelectMany(x => x.Statuses.Instances).Select(x => x.AppliedOrder).DefaultIfEmpty(0).Max() + 1;
                StatusSystem.Apply(fighter, source.Id, source.Side, recipe, "lab:status:" + fighter.Id, "lab:setup", order,
                    new StatusSnapshot { TriggeringHealthDamage = f.StatusDamage < 0 ? stats.Attack : f.StatusDamage, SourceAttack = stats.Attack, SourceDefense = stats.Defense, SourceMaxHealth = stats.MaxHealth, TargetMaxHealth = StatusSystem.GetEffectiveStats(fighter).MaxHealth },
                    recipe.DefaultPotencyBp, duration: recipe.DefaultDuration);
            }
        }
    }
}


