using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using Newtonsoft.Json.Linq;

namespace FightingAllstar.Core.Studio
{
    public enum StudioScenarioPreset { OpeningSkill, IncomingCap, FatalSave }
    [Serializable]
    public sealed class StudioSimulationOptions
    {
        public int Level = 1;
        public int Rank = 1;
        public int Tier;
        public StudioScenarioPreset Preset;
        public StudioSimulationOptions Clone() => (StudioSimulationOptions)MemberwiseClone();
    }

    /// <summary>Frozen source/run data. Every state/list returned is a copy, so editing a draft cannot rewrite a prior run.</summary>
    public sealed class StudioRunSnapshot
    {
        private readonly string battleJson;
        private readonly string baselineJson;
        private readonly string openingJson;
        private readonly string sourceJson;
        private readonly string hashesJson;
        private readonly string optionsJson;
        private readonly string planJson;
        private readonly List<string> changes;
        internal StudioRunSnapshot(StudioDocument source, StudioHashes hashes, BattleState opening,
            BattleState battle, BattleState baseline, ulong seed, StudioSimulationOptions options = null, TurnPlan plan = null)
        {
            Seed = seed; sourceJson = SnapshotJson.Serialize(source); hashesJson = SnapshotJson.Serialize(hashes);
            planJson = SnapshotJson.Serialize(plan ?? StudioSimulation.CreateInputPlan(opening));
            battleJson = SnapshotJson.Serialize(battle); baselineJson = SnapshotJson.Serialize(baseline);
            openingJson = SnapshotJson.Serialize(opening);
            optionsJson = SnapshotJson.Serialize(options ?? new StudioSimulationOptions());
            StateHash = StudioSimulation.StateHash(battle); BaselineStateHash = StudioSimulation.StateHash(baseline);
            MatchesLegacy = StateHash == BaselineStateHash;
            changes = StudioSimulation.Differences(baseline, battle);
        }
        public ulong Seed { get; }
        public StudioSimulationOptions Options => SnapshotJson.Deserialize<StudioSimulationOptions>(optionsJson);
        public bool MatchesLegacy { get; }
        public string StateHash { get; }
        public string BaselineStateHash { get; }
        public StudioHashes Hashes => SnapshotJson.Deserialize<StudioHashes>(hashesJson);
        public TurnPlan InputPlan => SnapshotJson.Deserialize<TurnPlan>(planJson);
        public StudioDocument Source => SnapshotJson.Deserialize<StudioDocument>(sourceJson);
        public BattleState Battle => SnapshotJson.Deserialize<BattleState>(battleJson);
        public BattleState BaselineBattle => SnapshotJson.Deserialize<BattleState>(baselineJson);
        public BattleState OpeningBattle => SnapshotJson.Deserialize<BattleState>(openingJson);
        public IReadOnlyList<PassiveRuleTrace> Trace => Battle.PassiveTrace.AsReadOnly();
        public IReadOnlyList<string> Changes => new List<string>(changes).AsReadOnly();
    }

    public static class StudioSimulation
    {
        public static StudioRunSnapshot Run(StudioDocument document, DescriptionLibrary library = null, ulong seed = 12345,
            StudioSimulationOptions options = null)
        {
            options = options?.Clone() ?? new StudioSimulationOptions();
            if (options.Level < 1 || options.Rank < 1 || options.Rank > 3 || options.Tier < 0 || options.Tier > 5)
                throw new ArgumentOutOfRangeException(nameof(options), "Use level ≥ 1, card rank 1–3 and constellation tier 0–5.");
            if (!Enum.IsDefined(typeof(StudioScenarioPreset), options.Preset)) throw new ArgumentOutOfRangeException(nameof(options.Preset));
            var source = StudioDocumentService.Clone(document);
            var compiled = StudioCompiler.Compile(source, library ?? DescriptionLibrary.CreateOpeningGaugeLibrary());
            if (!compiled.Success) throw new InvalidOperationException("Draft compilation failed: " + string.Join("; ", compiled.Diagnostics));
            var opening = ConfiguredFixture(compiled.Character, seed, options, true);
            var plan = CreateInputPlan(opening);
            var next = PlayFirstSkill(opening);
            var baseline = PlayFirstSkill(ConfiguredFixture(source.LegacyCharacter.Clone(), seed, options, false));
            return new StudioRunSnapshot(source, compiled.Hashes, opening, next, baseline, seed, options, plan);
        }

        private static BattleState ConfiguredFixture(CharacterDefinition character, ulong seed, StudioSimulationOptions options, bool trace)
        {
            var incoming = options.Preset != StudioScenarioPreset.OpeningSkill;
            var state = CreateFixture(character, seed, trace, firstSide: incoming ? TeamSide.Opponent : TeamSide.Player,
                tier: options.Tier, level: options.Level, rank: options.Rank);
            if (!incoming) return state;
            var defender = state.Player.Fighters[0];
            defender.Health = options.Preset == StudioScenarioPreset.FatalSave ? 1 : StatusSystem.GetEffectiveStats(defender).MaxHealth;
            var attacker = state.Opponent.Fighters[0];
            attacker.Definition.BaseStats.CritChanceBp = 0; attacker.Definition.BaseStats.PerceptionBp = 100000;
            foreach (var rank in attacker.Definition.Skills[0].Ranks)
                rank.Effect = new EffectDefinition { Id = "studio.incoming-contact", Kind = EffectKind.Damage, Scaling = StatScaling.Fixed,
                    Magnitude = 999999, CoefficientBp = 10000, Target = EffectTargetScope.SelectedEnemy, Attack = new DamageAttackDefinition { HitCount = 1 } };
            return state;
        }

        public static BattleState CreateFixture(CharacterDefinition character, ulong seed, bool captureTrace = true,
            bool ownerInReserve = false, TeamSide firstSide = TeamSide.Player, int tier = 0, int level = 1, int rank = 1)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            CharacterDefinition Dummy(string id)
            {
                var dummy = character.Clone(); dummy.Id = id; dummy.Passive = null; dummy.PassiveId = null;
                dummy.BaseStats.MaxHealth = Math.Max(100000, character.BaseStats.MaxHealth); return dummy;
            }
            var players = new[] { character.Clone(), Dummy("studio.ally.1"), Dummy("studio.ally.2"), Dummy("studio.ally.reserve") };
            if (ownerInReserve) { var swap = players[0]; players[0] = players[3]; players[3] = swap; }
            var state = BattleEngine.Create("studio.m2.fixed-fixture", players, new[] { tier, tier, tier, tier },
                new[] { Dummy("studio.enemy.1"), Dummy("studio.enemy.2"), Dummy("studio.enemy.3"), Dummy("studio.enemy.reserve") },
                null, seed, firstSide, capturePassiveTrace: captureTrace, playerLevels: new[] { level, level, level, level });
            if (rank < 1 || rank > 3) throw new ArgumentOutOfRangeException(nameof(rank));
            var owner = state.Player.Fighters[ownerInReserve ? 3 : 0];
            foreach (var card in state.Player.Hand.Where(c => c.OwnerFighterId == owner.Id && c.Kind == CardKind.Skill))
            { card.Rank = rank; CardRules.RefreshCardKind(card, owner.Definition); }
            AbilityVariantSystem.RefreshCards(state);
            return state;
        }

        public static BattleState PlayFirstSkill(BattleState state)
        {
            var plan = CreateInputPlan(state);
            if (!BattleEngine.TryResolvePlan(state, plan, out var next, out var error))
                throw new InvalidOperationException("Production engine rejected Studio fixture: " + error);
            if (!next.Events.Any(e => e.Kind == BattleEventKind.DamageApplied))
                throw new InvalidOperationException("Fixture produced no committed damage event for actor playback.");
            return next;
        }

        public static TurnPlan CreateInputPlan(BattleState state)
        {
            var team = state.ActingSide == TeamSide.Player ? state.Player : state.Opponent;
            var opponents = state.ActingSide == TeamSide.Player ? state.Opponent : state.Player;
            var owner = team.Fighters[0];
            var skillId = owner.Definition.Skills.OrderBy(s => s.Slot).FirstOrDefault()?.Id;
            var card = team.Hand.FirstOrDefault(c => c.OwnerFighterId == owner.Id && c.Kind == CardKind.Skill && c.SkillId == skillId);
            if (card == null) throw new InvalidOperationException("Fixture requires a legal opening skill card for the imported owner.");
            return new TurnPlan { RequestId = state.ActingSide == TeamSide.Player ? "studio.m2.first-skill" : "studio.m3.incoming-hit", ExpectedRevision = state.Revision,
                Actions = new List<PlannedAction> { new PlannedAction { CardId = card.Id,
                    TargetFighterId = opponents.LivingActive()[0].Id } } };
        }

        public static string StateHash(BattleState state) => StudioHash.Compute("studio-state-v1", Projection(state));

        internal static List<string> Differences(BattleState baseline, BattleState actual)
        {
            var differences = new List<string>();
            foreach (var fighter in actual.Player.Fighters.Concat(actual.Opponent.Fighters))
            {
                var old = baseline.Player.FindFighter(fighter.Id) ?? baseline.Opponent.FindFighter(fighter.Id);
                if (old == null) { differences.Add(fighter.Id + ": added"); continue; }
                if (old.PowerGauge != fighter.PowerGauge) differences.Add(fighter.Id + " gauge: " + old.PowerGauge + " → " + fighter.PowerGauge);
                if (old.Health != fighter.Health) differences.Add(fighter.Id + " HP: " + old.Health + " → " + fighter.Health);
                if (old.Shield != fighter.Shield) differences.Add(fighter.Id + " shield: " + old.Shield + " → " + fighter.Shield);
            }
            if (StateHash(baseline) != StateHash(actual) && differences.Count == 0)
                differences.Add("Cards, statuses, derived stats, event outcomes, or RNG differ; inspect frozen snapshots.");
            return differences;
        }

        // Compare authoritative outcomes, not the different source definitions, root labels, trace observer,
        // or human text. Damage/utility facts, ordering and RNG are part of parity.
        private static JToken Projection(BattleState state)
        {
            JObject Team(BattleTeamState team) => new JObject {
                ["fighters"] = new JArray(team.Fighters.Select(f => new JObject {
                    ["id"] = f.Id, ["slot"] = f.FormationSlot, ["reserve"] = f.IsReserve, ["alive"] = f.IsAlive,
                    ["level"] = f.Level, ["tier"] = f.ConstellationTier,
                    ["health"] = f.Health, ["shield"] = f.Shield, ["gauge"] = f.PowerGauge, ["gaugeBp"] = f.PowerGaugeRemainderBp,
                    ["statuses"] = JToken.Parse(SnapshotJson.Serialize(f.Statuses)),
                    ["elements"] = JToken.Parse(SnapshotJson.Serialize(f.ElementStatuses)),
                    ["counters"] = JToken.Parse(SnapshotJson.Serialize(f.PassiveCounters)),
                    ["abilityVariants"] = JToken.Parse(SnapshotJson.Serialize(f.AbilityVariants)),
                    ["stats"] = JToken.Parse(SnapshotJson.Serialize(StatusSystem.GetEffectiveStats(f))) })),
                ["hand"] = JToken.Parse(SnapshotJson.Serialize(team.Hand)) };
            var events = new JArray(state.Events.Where(e => e.Kind == BattleEventKind.DamageApplied || e.Kind == BattleEventKind.HealApplied ||
                e.Kind == BattleEventKind.PowerGaugeChanged || e.Kind == BattleEventKind.StatusApplied || e.Kind == BattleEventKind.StatusRemoved ||
                e.Kind == BattleEventKind.FighterDefeated || e.Kind == BattleEventKind.CardPlayed).Select(e => new JObject {
                    ["kind"] = (int)e.Kind, ["source"] = e.SourceId, ["target"] = e.TargetId, ["amount"] = e.Amount,
                    ["health"] = e.HealthAfter, ["shield"] = e.ShieldAfter, ["gauge"] = e.PowerGaugeAfter,
                    ["healthLost"] = e.HealthLost, ["shieldLost"] = e.ShieldLost, ["critical"] = e.WasCritical,
                    ["blocked"] = e.WasBlocked, ["hit"] = e.HitIndex, ["hits"] = e.HitCount, ["recipe"] = e.StatusRecipeId }));
            return new JObject { ["player"] = Team(state.Player), ["opponent"] = Team(state.Opponent), ["events"] = events,
                ["phase"] = (int)state.Phase, ["side"] = (int)state.ActingSide, ["turn"] = state.TurnNumber,
                ["revision"] = state.Revision, ["rng"] = state.RngState, ["draws"] = state.RngDrawCount,
                ["cardRng"] = state.CardRngState, ["cardDraws"] = state.CardRngDrawCount,
                ["activationReceipts"] = JToken.Parse(SnapshotJson.Serialize(state.PassiveActivations)) };
        }
    }
}
