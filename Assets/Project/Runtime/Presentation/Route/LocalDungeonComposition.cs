using System;
using System.Collections.Generic;
using System.Globalization;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;
using FightingAllstar.Presentation.Combat;
using UnityEngine;

namespace FightingAllstar.Presentation.Route
{
    /// <summary>Offline-only scene composition for a pinned catalog and the saved four-slot formation.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class LocalDungeonComposition : MonoBehaviour
    {
        [SerializeField] private DungeonFlowController flow;
        [SerializeField] private CombatContentSource contentSource;
        [SerializeField] private string seed = "1";

        private void Start()
        {
            if (flow == null)
            {
                Debug.LogError("Local dungeon composition has no DungeonFlowController reference.", this);
                return;
            }
            if (flow.ResumeSavedRun()) return;
            if (contentSource == null)
            {
                const string message = "Dungeon entry is unavailable because no runtime-ready combat content source is assigned.";
                flow.ShowEntryUnavailable(message);
                Debug.LogWarning(message, this);
                return;
            }
            try
            {
                var catalog = contentSource.LoadCatalog();
                var inventory = PlayerInventoryService.Instance;
                if (inventory == null) throw new InvalidOperationException("Player inventory is not initialized. Return to Gacha and try again.");
                var selected = BuildRoster(catalog, inventory);
                if (!ulong.TryParse(seed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSeed))
                    throw new InvalidOperationException("Local dungeon seed must be a non-negative integer.");
                flow.OpenDungeon(inventory.SubjectId, catalog.ContentVersion, catalog.ContentHash, parsedSeed,
                    catalog.Characters, selected);
            }
            catch (Exception exception)
            {
                flow.ShowEntryUnavailable("Dungeon entry is unavailable: " + exception.Message);
                Debug.LogWarning("Local dungeon composition could not open entry: " + exception.Message, this);
            }
        }

        private List<RunFighterSeed> BuildRoster(ContentCatalog catalog, PlayerInventoryService inventory)
        {
            if (inventory == null || inventory.Snapshot == null || inventory.Snapshot.formation == null || inventory.Snapshot.formation.Count != 4)
                throw new InvalidOperationException("CharacterLoadOut formation data is unavailable.");
            var fighters = new List<RunFighterSeed>(4);
            for (var slot = 0; slot < inventory.Snapshot.formation.Count; slot++)
            {
                var instanceId = inventory.Snapshot.formation[slot];
                if (string.IsNullOrWhiteSpace(instanceId)) continue;
                var owned = inventory.Snapshot.FindOwned(instanceId);
                if (owned == null) throw new InvalidOperationException("The selected character instance is not owned by this account: " + instanceId);
                var definition = catalog.Characters.Find(character => character.Id == owned.definitionId && character.RuntimeReady);
                if (definition == null) throw new InvalidOperationException("Published catalog has no runtime definition " + owned.definitionId + ".");
                fighters.Add(new RunFighterSeed { OwnedFighterId = owned.instanceId,
                    Definition = definition.Clone(), ResolvedStats = definition.BaseStats.Clone(),
                    ConstellationTier = owned.constellationTier, FormationSlot = slot, IsReserve = slot == 3 });
            }
            if (!fighters.Exists(fighter => !fighter.IsReserve))
                throw new InvalidOperationException("Assign at least one fighter to an active position in CharacterLoadOut.");
            return fighters;
        }
    }
}
