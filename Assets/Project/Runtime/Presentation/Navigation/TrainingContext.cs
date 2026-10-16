using System;
using System.Linq;
using FightingAllstar.Core.Run;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Content;

namespace FightingAllstar.Presentation.Navigation
{
    // Disposable practice only: no inventory, dungeon save, or reward mutations.
    public static class TrainingContext
    {
        private static CharacterDefinition _player, _dummy;
        private static int _tier;
        private static int _playerLevel = 1;
        public static bool IsReady => _player != null && _dummy != null;

        public static void Prepare(CharacterObject character, int tier)
        {
            Clear();
            if (character == null || !character.RuntimeReady) throw new InvalidOperationException("This fighter is not ready for battle.");
            var dummy = CharacterObjectRegistrySO.LoadAll().FirstOrDefault(c => c != null && c.DefinitionId == "fighter.kyo98" && c.RuntimeReady);
            if (dummy == null) throw new InvalidOperationException("Training needs a runtime-ready Kyo98 in the character registry.");
            var playerDefinition = CharacterObjectCatalogBuilder.CreateDefinition(character);
            var dummyDefinition = CharacterObjectCatalogBuilder.CreateDefinition(dummy);
            var catalog = new ContentCatalog {
                ElementalRules = ElementalRuleSet.CreateDefault(),
                StatusRecipes = StandardEffectDatabase.CreateStatusRecipes(),
                AttackEffectRecipes = StandardEffectDatabase.CreateAttackEffects()
            };
            catalog.Characters.Add(playerDefinition);
            if (dummyDefinition.Id != playerDefinition.Id) catalog.Characters.Add(dummyDefinition);
            var errors = ContentValidator.Validate(catalog);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("; ", errors));
            _player = playerDefinition;
            var inventory = PlayerInventoryService.Instance;
            var owned = inventory?.FindOwnedByDefinition(character.DefinitionId);
            _playerLevel = Math.Max(1, owned?.level ?? 1);
            _player.ArtifactLoadout = inventory?.GetEquippedArtifacts(owned?.instanceId).Select(a => a.Clone()).ToList()
                ?? new System.Collections.Generic.List<ArtifactInstance>();
            var levelStats = CharacterProgression.StatsAtLevel(playerDefinition.BaseStats, playerDefinition.StatGrowth, owned?.level ?? 1);
            _player.BaseStats = ArtifactRules.ApplyStats(levelStats, _player.ArtifactLoadout);
            _dummy = dummyDefinition;
            _tier = Math.Max(0, Math.Min(playerDefinition.UltimateTiers.Max(t => t.Tier), tier));
        }

        public static bool TryCreateEncounter(out EncounterProjection encounter, out ulong seed)
        {
            encounter = null;
            seed = 1;
            if (!IsReady) return false;
            encounter = new EncounterProjection { BattleId = "training:" + Guid.NewGuid().ToString("N") };
            encounter.PlayerTeam.Add(Snapshot(_player, "training:player", _tier, _playerLevel));
            encounter.EnemyTeam.Add(Snapshot(_dummy, "training:dummy", 0));
            return true;
        }

        private static EncounterFighterSnapshot Snapshot(CharacterDefinition definition, string id, int tier, int level = 1)
        {
            var copy = definition.Clone();
            return new EncounterFighterSnapshot { FighterId = id, DefinitionId = copy.Id, Definition = copy,
                Stats = copy.BaseStats.Clone(), CurrentHealth = copy.BaseStats.MaxHealth, Level = level, ConstellationTier = tier,
                FormationSlot = 0, IsReserve = false };
        }

        public static void Clear() { _player = null; _dummy = null; _tier = 0; _playerLevel = 1; }
    }
}
