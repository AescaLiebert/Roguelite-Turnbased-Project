using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using FightingAllstar.Core.Economy;
using FightingAllstar.Adapters;
using UnityEngine;

namespace FightingAllstar.Presentation.Economy
{
    /// <summary>Offline profile persistence. The authenticated subject is the storage key when an auth adapter is connected.</summary>
    public sealed class LocalEconomyStore
    {
        private readonly string _path;
        public LocalEconomyStore(string fileName = "fighting-allstar-local-economy.json")
        {
            _path = Path.Combine(Application.persistentDataPath, "FightingAllstar", "Profiles", GetSubjectKey(), fileName);
        }

        public void Save(LocalEconomyState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            SyncToPlayerInventory(state);
            var directory = Path.GetDirectoryName(_path);
            if (string.IsNullOrEmpty(directory)) throw new InvalidOperationException("Local economy profile path has no parent directory.");
            Directory.CreateDirectory(directory);
            var temp = _path + ".tmp";
            var backup = _path + ".bak";
            File.WriteAllText(temp, JsonUtility.ToJson(state));
            if (!File.Exists(_path)) { File.Move(temp, _path); return; }
            if (File.Exists(backup)) File.Delete(backup);
            File.Replace(temp, _path, backup);
            try { if (File.Exists(backup)) File.Delete(backup); }
            catch (IOException) { /* The committed snapshot is valid; a stale backup is safe. */ }
        }

        public bool TryLoad(out LocalEconomyState state)
        {
            state = null;
            foreach (var candidate in new[] { _path, _path + ".bak" })
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    state = JsonUtility.FromJson<LocalEconomyState>(File.ReadAllText(candidate));
                    if (state != null && state.SchemaVersion == 1) return true;
                    state = null;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Local economy snapshot could not be read from " + candidate + ": " + exception.Message);
                }
            }
            return false;
        }

        public LocalEconomyState LoadOrCreateLocalProfile()
        {
            if (TryLoad(out var state))
            {
                state.SubjectId = LocalPlayerAccountContext.SubjectId;
                EnsureStarterRoster(state);
                SyncFromPlayerInventory(state);
                Save(state);
                return state;
            }
            state = LocalEconomyState.CreateLocalProfile(LocalPlayerAccountContext.SubjectId, 1600, 1);
            EnsureStarterRoster(state);
            SyncFromPlayerInventory(state);
            Save(state);
            return state;
        }

        private static void SyncFromPlayerInventory(LocalEconomyState state)
        {
            state.Roster = state.Roster ?? new System.Collections.Generic.List<LocalOwnedCharacter>();
            foreach (var existing in state.Roster)
                if (existing != null) existing.ConstellationTier = Math.Max(0, Math.Min(5, existing.ConstellationTier));
            var inventory = PlayerInventoryService.Instance;
            if (inventory == null || inventory.Snapshot == null) return;
            state.Diamonds = inventory.Diamonds;
            if (inventory.Snapshot.formation != null && inventory.Snapshot.formation.Count == 4 &&
                inventory.Snapshot.formation.Exists(id => !string.IsNullOrEmpty(id)))
            {
                var defs = inventory.GetDefinitionFormation(inventory.Snapshot.formation);
                if (defs != null && defs.Exists(d => !string.IsNullOrEmpty(d)))
                {
                    state.FormationDefinitionIds = defs;
                }
            }
            if (inventory.Snapshot.characters != null)
            {
                foreach (var owned in inventory.Snapshot.characters)
                {
                    if (owned == null) continue;
                    var existing = state.Roster.Find(x => x != null && x.DefinitionId == owned.definitionId);
                    if (existing != null)
                    {
                        existing.ConstellationTier = Math.Min(5, Math.Max(existing.ConstellationTier, owned.constellationTier));
                    }
                    else
                    {
                        state.Roster.Add(new LocalOwnedCharacter
                        {
                            DefinitionId = owned.definitionId,
                            ConstellationTier = Math.Max(0, Math.Min(5, owned.constellationTier)),
                            CrestCount = 0
                        });
                    }
                }
            }
        }

        private static void SyncToPlayerInventory(LocalEconomyState state)
        {
            var inventory = PlayerInventoryService.Instance;
            if (inventory == null || inventory.Snapshot == null) return;
            if (state.FormationDefinitionIds != null && state.FormationDefinitionIds.Count == 4)
            {
                var instanceIds = inventory.GetInstanceFormation(state.FormationDefinitionIds);
                if (instanceIds != null && instanceIds.Count == 4 && instanceIds.Exists(id => !string.IsNullOrEmpty(id)))
                {
                    for (var i = 0; i < 4; i++)
                    {
                        inventory.Snapshot.formation[i] = instanceIds[i];
                    }
                }
            }
        }

        public LocalEconomyLedgerEntry GrantRunCompletion(string runId, int amount)
        {
            if (string.IsNullOrWhiteSpace(runId)) throw new ArgumentException("A run ID is required.", nameof(runId));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            var state = LoadOrCreateLocalProfile();
            var operationId = "run-completion:" + runId;
            var existing = state.Ledger.Find(x => x != null && x.OperationId == operationId);
            if (existing != null)
            {
                if (existing.Amount != amount) throw new InvalidOperationException("Run completion ID was reused with a different reward quote.");
                return existing.Clone();
            }
            state.Diamonds += amount;
            var entry = new LocalEconomyLedgerEntry { OperationId = operationId, Kind = "RunCompletion",
                Amount = amount, BalanceAfter = state.Diamonds };
            state.Ledger.Add(entry.Clone());
            state.Revision++;
            Save(state);
            return entry;
        }

        private static void EnsureStarterRoster(LocalEconomyState state)
        {
            var starters = new[] { "fighter.kyo94", "fighter.chin94", "fighter.kensou94", "fighter.king94" };
            state.Roster = state.Roster ?? new System.Collections.Generic.List<LocalOwnedCharacter>();
            foreach (var id in starters)
                if (!state.Roster.Exists(owned => owned != null && owned.DefinitionId == id))
                    state.Roster.Add(new LocalOwnedCharacter { DefinitionId = id, ConstellationTier = 0 });
            if (state.FormationDefinitionIds == null || state.FormationDefinitionIds.Count != 4 ||
                !state.FormationDefinitionIds.Exists(id => !string.IsNullOrEmpty(id)))
                state.FormationDefinitionIds = new System.Collections.Generic.List<string>(starters);
        }

        private static string GetSubjectKey()
        {
            using (var sha = SHA256.Create())
            {
                var digest = sha.ComputeHash(Encoding.UTF8.GetBytes(LocalPlayerAccountContext.SubjectId));
                return BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
