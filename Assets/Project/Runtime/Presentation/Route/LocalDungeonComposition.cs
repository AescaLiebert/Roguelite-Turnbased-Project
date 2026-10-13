using System;
using System.Collections.Generic;


using FightingAllstar.Core.Run;
using FightingAllstar.Presentation.Content;
using UnityEngine;

namespace FightingAllstar.Presentation.Route
{
    /// <summary>Offline-only scene composition for SO-backed combat content and run-scoped formations.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class LocalDungeonComposition : MonoBehaviour
    {
        [SerializeField] private DungeonFlowController flow;


        private void Start()
        {
            if (flow == null)
            {
                Debug.LogError("Local dungeon composition has no DungeonFlowController reference.", this);
                return;
            }
            if (flow.HasBattleResultPresentation) return;
            try
            {
                var catalog = CharacterObjectCatalogBuilder.Load(null);
                var errors = FightingAllstar.Core.Content.ContentValidator.Validate(catalog);
                if (errors.Count > 0)
                    throw new InvalidOperationException("Combat content is invalid: " + string.Join("; ", errors));
                if (flow.ResumeSavedRun(catalog.Characters)) return;
                var inventory = PlayerInventoryService.Instance;
                if (inventory == null) throw new InvalidOperationException("Player inventory is not initialized. Return to Gacha and try again.");
                var selected = new List<RunFighterSeed>();
                flow.OpenDungeon(inventory.SubjectId, catalog.ContentVersion, catalog.ContentHash, 0,
                    catalog.Characters, selected);
            }
            catch (Exception exception)
            {
                flow.ShowEntryUnavailable("Dungeon entry is unavailable: " + exception.Message);
                Debug.LogWarning("Local dungeon composition could not open entry: " + exception.Message, this);
            }
        }

    }
}
