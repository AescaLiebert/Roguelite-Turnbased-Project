using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class OwnedCharacterRecord
{
    public string instanceId;
    public string definitionId;
    public int constellationTier;
    public int level = 1;
}

[Serializable]
public sealed class FormationSelection
{
    public int slotPosition;
    public CharacterObject character;
    public string ownedFighterId;
    public int constellationTier;
}

[Serializable]
public sealed class DungeonFormationSave
{
    public string dungeonId;
    public List<string> formation = new List<string> { "", "", "", "" };
}

[Serializable]
public sealed class PlayerInventorySnapshot
{
    public int schemaVersion = 1;
    public string subjectId;
    public int diamonds = 1600;
    public int featuredGuaranteeProgress;
    public int selectorEntitlements;
    public int duplicateTokens;
    public List<OwnedCharacterRecord> characters = new List<OwnedCharacterRecord>();
    public List<string> formation = new List<string> { "", "", "", "" };
    public List<DungeonFormationSave> dungeonFormations = new List<DungeonFormationSave>();
    public List<string> claimedRunRewards = new List<string>();

    public OwnedCharacterRecord FindOwned(string instanceId) => characters.Find(x => x != null && x.instanceId == instanceId);
    public OwnedCharacterRecord FindDefinition(string definitionId) => characters.Find(x => x != null && x.definitionId == definitionId);
}

/// <summary>
/// Account-scoped inventory facade. The development file repository is keyed by the authenticated subject ID;
/// a Firebase-backed repository can replace it without changing Gacha, Loadout, or Battle scene consumers.
/// </summary>
public sealed class PlayerInventoryService : MonoBehaviour
{
    private const string GuestSubjectId = "guest:local-development";
    private const int SingleSummonCost = 160;
    private const int TenSummonCost = 1600;
    private const int GuaranteeThreshold = 300;
    private const string ResourcesPath = "Character_WIP-Phase";
    private static PlayerInventoryService _instance;

    [SerializeField] private string currentSubjectId;
    [SerializeField] private PlayerInventorySnapshot snapshot;
    private CharacterObject[] _catalog;
    private string _loadedSubjectId;

    public static PlayerInventoryService Instance => _instance;
    public string SubjectId => _loadedSubjectId;
    public PlayerInventorySnapshot Snapshot => snapshot;
    public int Diamonds => snapshot == null ? 0 : snapshot.diamonds;
    public int GuaranteeProgress => snapshot == null ? 0 : snapshot.featuredGuaranteeProgress;
    public int SelectorEntitlements => snapshot == null ? 0 : snapshot.selectorEntitlements;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var root = new GameObject("PlayerInventoryService");
        root.AddComponent<PlayerInventoryService>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        _catalog = Resources.LoadAll<CharacterObject>(ResourcesPath);
        BindSubject(string.IsNullOrWhiteSpace(currentSubjectId) ? GuestSubjectId : currentSubjectId);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>Called by the authentication adapter after sign-in; IDs are provider subject IDs, not display names.</summary>
    public void BindAuthenticatedSubject(string subjectId)
    {
        if (string.IsNullOrWhiteSpace(subjectId)) throw new ArgumentException("Authenticated subject ID is required.", nameof(subjectId));
        BindSubject(subjectId.Trim());
    }

    private void BindSubject(string subjectId)
    {
        if (_loadedSubjectId == subjectId && snapshot != null) return;
        SaveCurrent();
        currentSubjectId = subjectId;
        _loadedSubjectId = subjectId;
        snapshot = Load(subjectId);
        if (snapshot == null)
        {
            snapshot = new PlayerInventorySnapshot { subjectId = subjectId };
            SeedInitialRoster();
            SaveCurrent();
        }
        NormalizeSnapshot();
    }

    public IReadOnlyList<CharacterObject> GetCatalog()
    {
        EnsureCatalog();
        return _catalog;
    }

    public CharacterObject FindDefinition(string definitionId)
    {
        EnsureCatalog();
        return Array.Find(_catalog, x => x != null && x.DefinitionId == definitionId);
    }

    public List<CharacterObject> GetOwnedDefinitions()
    {
        NormalizeSnapshot();
        var result = new List<CharacterObject>();
        foreach (var owned in snapshot.characters)
        {
            var definition = FindDefinition(owned.definitionId);
            if (definition != null) result.Add(definition);
        }
        return result;
    }

    public OwnedCharacterRecord FindOwnedByDefinition(string definitionId)
    {
        NormalizeSnapshot();
        return snapshot.FindDefinition(definitionId);
    }

    public OwnedCharacterRecord FindOwnedByInstance(string instanceId)
    {
        NormalizeSnapshot();
        return snapshot.FindOwned(instanceId);
    }

    public OwnedCharacterRecord GrantCharacter(CharacterObject definition)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.DefinitionId))
            throw new ArgumentException("A stable character definition is required.", nameof(definition));
        NormalizeSnapshot();
        var owned = snapshot.FindDefinition(definition.DefinitionId);
        if (owned == null)
        {
            owned = new OwnedCharacterRecord { instanceId = Guid.NewGuid().ToString("N"), definitionId = definition.DefinitionId };
            snapshot.characters.Add(owned);
        }
        else if (owned.constellationTier < 6)
        {
            owned.constellationTier++;
        }
        else
        {
            snapshot.duplicateTokens++;
        }
        SaveCurrent();
        return owned;
    }

    public bool TrySpendForSummon(int count, out string error)
    {
        error = null;
        var cost = count == 1 ? SingleSummonCost : count == 10 ? TenSummonCost : -1;
        if (cost < 0) { error = "Summon count must be one or ten."; return false; }
        NormalizeSnapshot();
        if (snapshot.diamonds < cost) { error = "Not enough Diamonds for this summon."; return false; }
        snapshot.diamonds -= cost;
        SaveCurrent();
        return true;
    }

    public int RecordPaidPull()
    {
        NormalizeSnapshot();
        snapshot.featuredGuaranteeProgress++;
        if (snapshot.featuredGuaranteeProgress >= GuaranteeThreshold)
        {
            snapshot.featuredGuaranteeProgress -= GuaranteeThreshold;
            snapshot.selectorEntitlements++;
        }
        SaveCurrent();
        return snapshot.featuredGuaranteeProgress;
    }

    public bool TryClaimSelector(string definitionId, out string error)
    {
        error = null;
        var definition = FindDefinition(definitionId);
        if (definition == null || definition.FighterRarity != FighterRarity.SSR)
        { error = "Choose an available featured SSR character."; return false; }
        NormalizeSnapshot();
        if (snapshot.selectorEntitlements < 1) { error = "No featured selector has been earned."; return false; }
        snapshot.selectorEntitlements--;
        GrantCharacter(definition);
        SaveCurrent();
        return true;
    }

    public bool TrySetFormationSlot(int slot, string ownedInstanceId)
    {
        NormalizeSnapshot();
        if (slot < 0 || slot >= 4 || snapshot.FindOwned(ownedInstanceId) == null) return false;
        for (var i = 0; i < snapshot.formation.Count; i++)
            if (i != slot && snapshot.formation[i] == ownedInstanceId) return false;
        snapshot.formation[slot] = ownedInstanceId;
        SaveCurrent();
        return true;
    }

    public void SaveFormation(IReadOnlyList<string> instanceIds)
    {
        if (instanceIds == null || instanceIds.Count != 4) throw new ArgumentException("Formation must contain four slots.", nameof(instanceIds));
        NormalizeSnapshot();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < instanceIds.Count; i++)
        {
            var id = instanceIds[i];
            if (string.IsNullOrEmpty(id)) { snapshot.formation[i] = string.Empty; continue; }
            if (snapshot.FindOwned(id) == null || !seen.Add(id)) throw new InvalidOperationException("Formation entries must be unique owned characters.");
            snapshot.formation[i] = id;
        }
        SaveCurrent();
    }

    public IReadOnlyList<string> GetDungeonFormation(string dungeonId)
    {
        NormalizeSnapshot();
        if (string.IsNullOrEmpty(dungeonId)) return snapshot.formation;
        var saved = snapshot.dungeonFormations.Find(x => x != null && string.Equals(x.dungeonId, dungeonId, StringComparison.OrdinalIgnoreCase));
        if (saved != null && saved.formation != null && saved.formation.Count == 4)
            return saved.formation;
        return snapshot.formation;
    }

    public void SaveDungeonFormation(string dungeonId, IReadOnlyList<string> instanceIds)
    {
        if (string.IsNullOrEmpty(dungeonId))
        {
            SaveFormation(instanceIds);
            return;
        }
        if (instanceIds == null || instanceIds.Count != 4)
            throw new ArgumentException("Dungeon formation must contain four slots.", nameof(instanceIds));

        NormalizeSnapshot();
        var saved = snapshot.dungeonFormations.Find(x => x != null && string.Equals(x.dungeonId, dungeonId, StringComparison.OrdinalIgnoreCase));
        if (saved == null)
        {
            saved = new DungeonFormationSave { dungeonId = dungeonId };
            snapshot.dungeonFormations.Add(saved);
        }
        saved.formation = new List<string>(4);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < instanceIds.Count; i++)
        {
            var id = instanceIds[i];
            if (string.IsNullOrEmpty(id)) { saved.formation.Add(string.Empty); continue; }
            if (snapshot.FindOwned(id) == null || !seen.Add(id))
            {
                saved.formation.Add(string.Empty);
                continue;
            }
            saved.formation.Add(id);
        }
        while (saved.formation.Count < 4) saved.formation.Add(string.Empty);
        SaveCurrent();
    }

    public List<string> GetDefinitionFormation(IReadOnlyList<string> instanceIds)
    {
        NormalizeSnapshot();
        var defs = new List<string>(4);
        if (instanceIds == null) return defs;
        for (var i = 0; i < instanceIds.Count; i++)
        {
            var inst = instanceIds[i];
            if (string.IsNullOrEmpty(inst)) { defs.Add(string.Empty); continue; }
            var owned = snapshot.FindOwned(inst);
            defs.Add(owned == null ? string.Empty : owned.definitionId);
        }
        return defs;
    }

    public List<string> GetInstanceFormation(IReadOnlyList<string> definitionIds)
    {
        NormalizeSnapshot();
        var insts = new List<string>(4);
        if (definitionIds == null) return insts;
        for (var i = 0; i < definitionIds.Count; i++)
        {
            var def = definitionIds[i];
            if (string.IsNullOrEmpty(def)) { insts.Add(string.Empty); continue; }
            var owned = snapshot.FindDefinition(def);
            insts.Add(owned == null ? string.Empty : owned.instanceId);
        }
        return insts;
    }

    public bool GrantRunReward(string runId, int amount)
    {
        if (string.IsNullOrWhiteSpace(runId)) throw new ArgumentException("A run ID is required.", nameof(runId));
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        NormalizeSnapshot();
        if (snapshot.claimedRunRewards.Contains(runId)) return false;
        snapshot.diamonds += amount;
        snapshot.claimedRunRewards.Add(runId);
        SaveCurrent();
        return true;
    }

    public List<FormationSelection> GetPlayerFormation()
    {
        NormalizeSnapshot();
        var result = new List<FormationSelection>(4);
        for (var slot = 0; slot < 4; slot++)
        {
            if (slot >= snapshot.formation.Count) break;
            var owned = snapshot.FindOwned(snapshot.formation[slot]);
            var definition = owned == null ? null : FindDefinition(owned.definitionId);
            if (definition != null)
                result.Add(new FormationSelection { slotPosition = slot, character = definition,
                    ownedFighterId = owned.instanceId, constellationTier = owned.constellationTier });
        }
        return result;
    }

    public void SaveCurrent()
    {
        if (snapshot == null || string.IsNullOrWhiteSpace(_loadedSubjectId)) return;
        snapshot.subjectId = _loadedSubjectId;
        var path = GetPath(_loadedSubjectId);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonUtility.ToJson(snapshot, true));
        if (File.Exists(path)) File.Delete(path);
        File.Move(temporaryPath, path);
    }

    private void SeedInitialRoster()
    {
        var starterIds = new[] { "fighter.kyo94", "fighter.chin94", "fighter.kensou94", "fighter.king94" };
        foreach (var definitionId in starterIds)
        {
            var definition = FindDefinition(definitionId);
            if (definition != null)
                snapshot.characters.Add(new OwnedCharacterRecord { instanceId = Guid.NewGuid().ToString("N"), definitionId = definitionId });
        }
        if (snapshot.characters.Count == 4)
            for (var i = 0; i < 4; i++) snapshot.formation[i] = snapshot.characters[i].instanceId;
    }

    private PlayerInventorySnapshot Load(string subjectId)
    {
        var path = GetPath(subjectId);
        if (!File.Exists(path)) return null;
        try
        {
            var loaded = JsonUtility.FromJson<PlayerInventorySnapshot>(File.ReadAllText(path));
            return loaded != null && loaded.schemaVersion == 1 && loaded.subjectId == subjectId ? loaded : null;
        }
        catch (Exception exception)
        {
            Debug.LogError("Could not load player inventory for this account: " + exception.Message, this);
            return null;
        }
    }

    private void NormalizeSnapshot()
    {
        if (snapshot == null) snapshot = new PlayerInventorySnapshot { subjectId = _loadedSubjectId };
        if (snapshot.characters == null) snapshot.characters = new List<OwnedCharacterRecord>();
        if (snapshot.formation == null) snapshot.formation = new List<string>();
        if (snapshot.dungeonFormations == null) snapshot.dungeonFormations = new List<DungeonFormationSave>();
        if (snapshot.claimedRunRewards == null) snapshot.claimedRunRewards = new List<string>();
        while (snapshot.formation.Count < 4) snapshot.formation.Add(string.Empty);
        if (snapshot.formation.Count > 4) snapshot.formation.RemoveRange(4, snapshot.formation.Count - 4);
        foreach (var df in snapshot.dungeonFormations)
        {
            if (df == null) continue;
            if (df.formation == null) df.formation = new List<string>();
            while (df.formation.Count < 4) df.formation.Add(string.Empty);
            if (df.formation.Count > 4) df.formation.RemoveRange(4, df.formation.Count - 4);
        }
    }

    private void EnsureCatalog()
    {
        if (_catalog == null || _catalog.Length == 0) _catalog = Resources.LoadAll<CharacterObject>(ResourcesPath);
    }

    private static string GetPath(string subjectId)
    {
        using (var sha = SHA256.Create())
        {
            var digest = sha.ComputeHash(Encoding.UTF8.GetBytes(subjectId));
            var safeId = BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant();
            return Path.Combine(Application.persistentDataPath, "PlayerInventory", safeId + ".json");
        }
    }
}
