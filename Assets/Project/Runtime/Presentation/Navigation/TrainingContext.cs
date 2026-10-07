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
        public static bool IsReady => _player != null && _dummy != null;

        public static void Prepare(CharacterObject character, int tier)
        {
            Clear();
            if (character == null || !character.RuntimeReady) throw new InvalidOperationException("This fighter is not ready for battle.");
            var dummy = CharacterObjectRegistrySO.LoadAll().FirstOrDefault(c => c != null && c.DefinitionId == "fighter.kyo98" && c.RuntimeReady);
            if (dummy == null) throw new InvalidOperationException("Training needs a runtime-ready Kyo98 in the character registry.");
            var playerDefinition = CharacterObjectCatalogBuilder.CreateDefinition(character);
            var dummyDefinition = CharacterObjectCatalogBuilder.CreateDefinition(dummy);
            var catalog = new ContentCatalog();
            catalog.Characters.Add(playerDefinition);
            if (dummyDefinition.Id != playerDefinition.Id) catalog.Characters.Add(dummyDefinition);
            var errors = ContentValidator.Validate(catalog);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("; ", errors));
            _player = playerDefinition;
            _dummy = dummyDefinition;
            _tier = Math.Max(0, Math.Min(6, tier));
        }

        public static bool TryCreateEncounter(out EncounterProjection encounter, out ulong seed)
        {
            encounter = null;
            seed = 1;
            if (!IsReady) return false;
            encounter = new EncounterProjection { BattleId = "training:" + Guid.NewGuid().ToString("N") };
            encounter.PlayerTeam.Add(Snapshot(_player, "training:player", _tier));
            encounter.EnemyTeam.Add(Snapshot(_dummy, "training:dummy", 0));
            return true;
        }

        private static EncounterFighterSnapshot Snapshot(CharacterDefinition definition, string id, int tier)
        {
            var copy = definition.Clone();
            return new EncounterFighterSnapshot { FighterId = id, DefinitionId = copy.Id, Definition = copy,
                Stats = copy.BaseStats.Clone(), CurrentHealth = copy.BaseStats.MaxHealth, ConstellationTier = tier,
                FormationSlot = 0, IsReserve = false };
        }

        public static void Clear() { _player = null; _dummy = null; _tier = 0; }
    }
}
