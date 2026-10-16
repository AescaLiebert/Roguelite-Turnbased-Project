using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;

internal static partial class BattlePlaybackChecks
{
    private static void DungeonEnemyRefactorChecks()
    {
        var catalog = new List<CharacterDefinition>();
        for (var i = 0; i < 8; i++)
        {
            var fighter = Fighter("enemy-refactor-" + i, 10000, 100);
            fighter.RuntimeReady = true; fighter.SourceId = "source." + i;
            fighter.PassiveId = "passive." + i; fighter.SeriesId = "series.soulcalibur";
            fighter.CategoryId = 2300 + i; fighter.BaseStats.Defense = 80; fighter.BaseStats.CombatClass = 1000;
            for (var tier = 1; tier < 6; tier++)
                fighter.UltimateTiers.Add(new UltimateTierDefinition { Tier = tier, Effect = fighter.UltimateTiers[0].Effect.Clone() });
            catalog.Add(fighter);
        }
        var phases = DungeonAvailability.Phases("series.soulcalibur", catalog);
        Check(phases[0] == 2 && !phases.Contains(1), "Empty phases must be filtered out.");
        Check(DungeonAvailability.SubPhases("series.soulcalibur", 2, catalog).SequenceEqual(new[] { 3, 4 }), "Soul Calibur must start at Pre-Release / Late.");
        Check(DungeonAvailability.SubPhases("series.soulcalibur", 3, catalog).Count == 5, "Later phases must include earlier released fighters.");
        Check(DungeonAvailability.Phases("series.missing", catalog).Count == 0, "Missing series must not borrow another series roster.");
        Check(DungeonAvailability.Phases(null, catalog.Take(3).ToList()).Count == 0, "An unavailable four-fighter roster must not appear enterable.");
        var profile = DungeonProfile.Filtered("series.soulcalibur", 2, 3);
        var catalogWithElements = catalog.Concat(GenshinStarterRoster.Create()).ToList();
        Check(RouteGenerator.Generate(profile, catalogWithElements, 0, 42).Any(n => n.Type == RouteNodeType.Boss),
            "A valid elemental fighter elsewhere in the catalog prevented dungeon generation.");
        Check(DungeonEnemyFactory.RecommendedCombatClass(profile, catalogWithElements, 0) > 0,
            "Dungeon entry's combat-class preview rejected the elemental catalog.");
        var authoredAlias = catalog[0].Clone(); authoredAlias.SeriesId = "series.sc";
        Check(profile.Accepts(authoredAlias) && DungeonProfile.SeriesDisplayName(authoredAlias.SeriesId) == "Soul Calibur", "Authored Soul Calibur series.sc assets must match the entry policy.");
        for (var tier = 1; tier <= 5; tier++)
        {
            var difficulty = DungeonDifficulty.BonusPercent(tier);
            var route = RouteGenerator.Generate(profile, catalog, difficulty, 42);
            Check(SnapshotJson.Serialize(route) == SnapshotJson.Serialize(RouteGenerator.Generate(profile, catalog, difficulty, 42)), "Enemy upgrades must replay from the route seed.");
            foreach (var node in route.Where(n => n.EnemyTeamSnapshot.Count > 0))
            foreach (var enemy in node.EnemyTeamSnapshot)
            {
                var source = catalog.Single(c => c.Id == enemy.DefinitionId);
                Check(enemy.ConstellationTier >= 0 && enemy.ConstellationTier <= 5, "Constellations must remain valid.");
                if (tier == 1)
                {
                    Check(enemy.EnemyKind == DungeonEnemyKind.Fighter && enemy.Level == 1,
                        "Easy encounters must use level-one fighters, including elites and bosses.");
                    Check(enemy.Definition.ArtifactLoadout.Count == 0 &&
                        SnapshotJson.Serialize(enemy.Stats) == SnapshotJson.Serialize(CharacterProgression.StatsAtLevel(source.BaseStats, source.StatGrowth, 1)),
                        "Easy fighters must use full level-one stats without artifacts.");
                    Check(enemy.Definition.Passive?.Id == source.Passive?.Id &&
                        SnapshotJson.Serialize(enemy.Definition.Skills) == SnapshotJson.Serialize(source.Skills) &&
                        SnapshotJson.Serialize(enemy.Definition.UltimateTiers) == SnapshotJson.Serialize(source.UltimateTiers),
                        "Easy fighters must inherit their complete fighter kit.");
                }
                else
                {
                    Check(enemy.EnemyKind == DungeonEnemyKind.Fighter, "Boss/higher difficulty must use actual fighters.");
                    var levelStats = CharacterProgression.StatsAtLevel(source.BaseStats, source.StatGrowth, enemy.Level);
                    Check(SnapshotJson.Serialize(ArtifactRules.ApplyStats(levelStats, enemy.Definition.ArtifactLoadout)) == SnapshotJson.Serialize(enemy.Stats), "Enemy stats must match the same upgrades as players.");
                }
                if (tier == 5)
                    Check(enemy.Level == 90 && enemy.Definition.ArtifactLoadout.Count == 5 && enemy.Definition.ArtifactLoadout.All(a =>
                        a.Level == 20 && a.Rarity == ArtifactRarity.SSR) && enemy.Definition.ArtifactLoadout.Select(a => a.Set).Distinct().Count() == 1,
                        "Nightmare needs a max level fighter with a full maxed legendary set.");
                var copy = SnapshotJson.Deserialize<EncounterFighterSnapshot>(SnapshotJson.Serialize(enemy));
                Check(copy.Level == enemy.Level && copy.EnemyKind == enemy.EnemyKind && copy.Clone().Level == enemy.Level, "Upgrades/type must persist in snapshots and clones.");
            }
            Check(DungeonEnemyFactory.RecommendedCombatClass(profile, catalog, difficulty) > 0, "Every difficulty needs a CC recommendation.");
        }
        Check(catalog.All(c => c.ArtifactLoadout.Count == 0 && c.DisplayName != "Minion"), "Enemy generation must not mutate fighter definitions.");
        var preset = new EnemyFormationTemplate { Id = "max-constellation-template", Score = 1 };
        foreach (var fighter in catalog.Take(4)) preset.Fighters.Add(new EnemyFormationMember { FighterId = fighter.Id, ConstellationTier = 5 });
        profile.EnemyFormationTemplates.Add(preset); profile.PresetTeamChancePercent = 100;
        var nightmarePreset = RouteGenerator.Generate(profile, catalog, 100, 42);
        Check(nightmarePreset.SelectMany(n => n.EnemyTeamSnapshot).Any(e => e.ConstellationTier < 5),
            "Nightmare must randomize constellations even when a preset specifies C5.");
        Check(RouteGenerator.Generate(profile, catalog, 25, 42).SelectMany(n => n.EnemyTeamSnapshot).All(e => e.ConstellationTier == 5),
            "Non-Nightmare presets must retain their authored constellation tiers.");
        ActionPassiveRegressionChecks();
    }

    private static void ActionPassiveRegressionChecks()
    {
        var attacker = Fighter("ivy-action", 10000, 100);
        attacker.BaseStats.CritChanceBp = 10000; attacker.BaseStats.CritDamageBp = 15000;
        attacker.Passive = new PassiveDefinition { Id = "ivy-action-passive" };
        var crit = new PassiveReactionDefinition { Id = "crit-attack", Trigger = PassiveEventKind.DamageResolved,
            ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Enemies, RequireCritical = true,
            CardOriginOnly = true };
        crit.Commands.Add(StackBuff("ivy-attack", "ATK", StatId.Attack, 1000)); attacker.Passive.Reactions.Add(crit);
        crit.Commands[0].Effect.Target = EffectTargetScope.Self;
        var defender = Fighter("sophitia-action", 10000, 100);
        defender.Passive = new PassiveDefinition { Id = "sophitia-action-passive" };
        var stanceRule = new PassiveReactionDefinition { Id = "stance-stack", Trigger = PassiveEventKind.DamageResolved,
            ActorRelation = PassiveRelation.Enemies, TargetRelation = PassiveRelation.Self, CardOriginOnly = true };
        stanceRule.Conditions.Add(new EffectConditionDefinition { Kind = EffectConditionKind.TargetHasStatusTag, Tag = "sophitia.stance" });
        stanceRule.Commands.Add(StackBuff("sophitia-chance", "Crit Chance", StatId.CritChance, 700));
        defender.Passive.Reactions.Add(stanceRule);
        var start = BattleEngine.Create("action-passive", new[] { attacker }, null, new[] { defender }, null, 77);
        StatusSystem.Apply(start.Opponent.Fighters[0], "stance", TeamSide.Opponent, new StatusRecipeDefinition {
            Id = "sophitia.stance", Behavior = StatusBehavior.Stance, Tags = new List<string> { "sophitia.stance", CombatTags.Stance },
            DefaultDuration = 5 }, "stance-instance", "setup", 1);
        var multi = new EffectDefinition { Scaling = StatScaling.Attack, CoefficientBp = 10000,
            Attack = new DamageAttackDefinition { HitCount = 5 } };
        var withoutCancel = PlayKind(start.Clone(), CardCategory.Attack, EffectTargetScope.SelectedEnemy, multi.Clone());
        Check(withoutCancel.Player.Fighters[0].Statuses.Instances.Single(s => s.RecipeId == "ivy-attack").StackCount == 1, "Ivy must gain only one ATK stack from five critical hits.");
        Check(withoutCancel.Opponent.Fighters[0].Statuses.Instances.Single(s => s.RecipeId == "sophitia-chance").StackCount == 1, "When attacked must activate once per action.");
        var secondDefender = defender.Clone(); secondDefender.Id = "sophitia-action-2";
        var areaStart = BattleEngine.Create("area-passive", new[] { attacker }, null,
            new[] { defender, secondDefender }, null, 77);
        foreach (var enemy in areaStart.Opponent.Fighters)
            StatusSystem.Apply(enemy, enemy.Id, enemy.Side, new StatusRecipeDefinition { Id = "sophitia.stance",
                Behavior = StatusBehavior.Stance, Tags = new List<string> { "sophitia.stance", CombatTags.Stance },
                DefaultDuration = 5 }, "area-stance-" + enemy.Id, "setup", 1);
        var area = PlayKind(areaStart, CardCategory.Attack, EffectTargetScope.AllEnemies, multi.Clone());
        Check(area.Player.Fighters[0].Statuses.Instances.Single(s => s.RecipeId == "ivy-attack").StackCount == 1,
            "Multiple critically hit targets must still grant Ivy only one ATK stack.");
        Check(area.Opponent.Fighters.All(f => f.Statuses.Instances.Single(s => s.RecipeId == "sophitia-chance").StackCount == 1),
            "Each attacked owner must activate its passive once per action.");
        multi.Sequence.Add(new CardEffectStep { Timing = CardEffectTiming.AfterDamage,
            Effect = new EffectDefinition { Kind = EffectKind.RemoveStance, Target = EffectTargetScope.SelectedEnemy } });
        var cancelled = PlayKind(start.Clone(), CardCategory.Attack, EffectTargetScope.SelectedEnemy, multi);
        Check(!cancelled.Opponent.Fighters[0].Statuses.Instances.Any(s => s.RecipeId == "sophitia-chance"), "Stance removed by this attack must not grant a stance passive stack.");
        Check(cancelled.PendingPassiveDamageFacts.Count == 0, "Action facts must be drained.");
        var replay = start.Clone(); foreach (var e in cancelled.Events.Skip(start.Events.Count)) BattlePlaybackState.Apply(replay, e);
        Check(replay.Player.Fighters[0].Statuses.Instances.Single(s => s.RecipeId == "ivy-attack").StackCount == 1, "Passive stack presentation must replay once.");
        var aggregate = start.Clone(); var source = aggregate.Player.Fighters[0]; var target = aggregate.Opponent.Fighters[0];
        CharacterPassiveRuntime.NotifyDamageResolved(aggregate, source, target, 1, "mixed", true, wasCritical: true, hitIndex: 1, hitCount: 2, deferUntilActionResolved: true);
        CharacterPassiveRuntime.NotifyDamageResolved(aggregate, source, target, 1, "mixed", true, wasCritical: false, hitIndex: 2, hitCount: 2, deferUntilActionResolved: true);
        var frozen = aggregate.Clone(); CharacterPassiveRuntime.ResolveActionDamageReactions(frozen, "mixed");
        Check(frozen.Player.Fighters[0].Statuses.Instances.Any(s => s.RecipeId == "ivy-attack"), "A crit on an earlier hit must qualify even when the final hit is not critical.");
        Check(aggregate.Player.Fighters[0].Statuses.Instances.Count == 0, "Pending action facts must clone without aliasing.");
        CharacterPassiveRuntime.ResolveActionDamageReactions(frozen, "mixed");
        Check(frozen.Player.Fighters[0].Statuses.Instances.Single(s => s.RecipeId == "ivy-attack").StackCount == 1, "Flushing an action twice must be idempotent.");
    }
}
