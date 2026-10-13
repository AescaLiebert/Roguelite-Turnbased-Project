using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;

[Serializable]
public sealed class OwnedCharacterRecord
{
    public string instanceId;
    public string definitionId;
    public int constellationTier;
    public int level = 1;
    public int levelCap = CharacterProgression.InitialCap;
}

[Serializable]
public sealed class CharacterDupeTokenStack
{
    public string characterDefinitionId;
    public int amount;
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
    // Retained for save compatibility only. New duplicate tokens are character-specific below.
    public int duplicateTokens;
    public List<CharacterDupeTokenStack> characterDupeTokens = new List<CharacterDupeTokenStack>();
    public int artifactAutoSelectRarity = 1;
    public List<OwnedCharacterRecord> characters = new List<OwnedCharacterRecord>();
    public List<string> formation = new List<string> { "", "", "", "" };
    public List<DungeonFormationSave> dungeonFormations = new List<DungeonFormationSave>();
    public List<string> claimedRunRewards = new List<string>();
    public ItemInventory items = new ItemInventory();

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
    public static int SingleSummonPrice => SingleSummonCost;
    public static int TenSummonPrice => TenSummonCost;
    public static int FeaturedSelectorThreshold => GuaranteeThreshold;
    private static PlayerInventoryService _instance;

    [SerializeField] private string currentSubjectId;
    [SerializeField] private PlayerInventorySnapshot snapshot;
    [Header("Editor Profile (Optional)")]
    [Tooltip("If checked in the Editor, uses the assigned ScriptableObject profile rather than the disk JSON.")]
    [SerializeField] private bool useEditorProfile;
    [Tooltip("Authored ScriptableObject profile to load when Use Editor Profile is enabled.")]
    [SerializeField] private PlayerInventoryProfileSO editorProfile;
    private CharacterObject[] _catalog;
    private string _loadedSubjectId;

    public bool UseEditorProfile
    {
        get => useEditorProfile;
        set => useEditorProfile = value;
    }
    public PlayerInventoryProfileSO EditorProfile
    {
        get => editorProfile;
        set => editorProfile = value;
    }

    public static PlayerInventoryService Instance => _instance;
    public static event Action InventoryChanged;
    public string SubjectId => _loadedSubjectId;
    public PlayerInventorySnapshot Snapshot => snapshot;
    public int Diamonds => snapshot == null ? 0 : snapshot.diamonds;
    public long Coins
    {
        get { NormalizeSnapshot(); return snapshot.items.Coins; }
    }
    public long GetItemAmount(string itemId)
    {
        NormalizeSnapshot();
        if (string.IsNullOrWhiteSpace(itemId)) return 0;
        if (itemId == ItemCatalog.Diamond) return Diamonds;
        if (ItemCatalog.TryGetCharacterDupeDefinitionId(itemId, out var definitionId)) return GetCharacterDupeAmount(definitionId);
        return snapshot.items.Amount(itemId);
    }

    public int GetCharacterDupeAmount(string characterDefinitionId)
    {
        NormalizeSnapshot();
        if (string.IsNullOrWhiteSpace(characterDefinitionId)) return 0;
        var stack = snapshot.characterDupeTokens.Find(x => x.characterDefinitionId == characterDefinitionId);
        return stack == null ? 0 : stack.amount;
    }

    public List<CharacterDupeTokenStack> GetCharacterDupeTokens()
    {
        NormalizeSnapshot();
        return snapshot.characterDupeTokens.Where(x => x.amount > 0)
            .Select(x => new CharacterDupeTokenStack { characterDefinitionId = x.characterDefinitionId, amount = x.amount }).ToList();
    }

    /// <summary>Consumes only tokens belonging to the specified fighter definition.</summary>
    public bool TrySpendCharacterDupeTokens(string characterDefinitionId, int amount, out string error)
    {
        NormalizeSnapshot();
        if (string.IsNullOrWhiteSpace(characterDefinitionId) || amount <= 0)
        { error = "Choose a character and a positive token amount."; return false; }
        var stack = snapshot.characterDupeTokens.Find(x => x.characterDefinitionId == characterDefinitionId);
        if (stack == null || stack.amount < amount)
        { error = "Not enough duplicate tokens for this character."; return false; }
        var previous = stack.amount;
        stack.amount -= amount;
        try { SaveCurrent(); }
        catch (Exception exception)
        {
            stack.amount = previous;
            error = "Character duplicate tokens could not be saved.";
            Debug.LogError("Character duplicate token save failed: " + exception.Message, this);
            return false;
        }
        error = null;
        return true;
    }
    public ItemInventory GetItemInventorySnapshot()
    {
        NormalizeSnapshot();
        return snapshot.items.Clone();
    }

    public int AutoArtifactRarity
    {
        get { NormalizeSnapshot(); return snapshot.artifactAutoSelectRarity; }
    }

    /// <summary>Returns a detached artifact; callers cannot alter account equipment or favourite locks.</summary>
    public ArtifactInstance FindArtifact(string instanceId)
    {
        NormalizeSnapshot();
        return snapshot.items.Artifacts.Find(a => a.InstanceId == instanceId)?.Clone();
    }

    public IReadOnlyList<ArtifactInstance> GetEquippedArtifacts(string characterInstanceId)
    {
        NormalizeSnapshot();
        if (string.IsNullOrWhiteSpace(characterInstanceId) || snapshot.FindOwned(characterInstanceId) == null) return Array.Empty<ArtifactInstance>();
        return snapshot.items.Artifacts.Where(a => a.EquippedCharacterId == characterInstanceId)
            .OrderBy(a => a.Slot).Select(a => a.Clone()).ToList();
    }

    public bool TrySetArtifactAutoRarity(int rarity, out string error)
    {
        NormalizeSnapshot();
        if (!HasLoadedArtifactAccount(out error)) return false;
        if (rarity < 1 || rarity > 5) { error = "Choose an auto-select rarity from Common to SSR."; return false; }
        if (snapshot.artifactAutoSelectRarity == rarity) return true;
        var previous = snapshot.artifactAutoSelectRarity;
        snapshot.artifactAutoSelectRarity = rarity;
        try { SaveCurrent(); }
        catch (Exception exception)
        {
            snapshot.artifactAutoSelectRarity = previous;
            error = "The auto-select setting could not be saved.";
            Debug.LogError("Artifact preference save failed: " + exception.Message, this);
            return false;
        }
        return true;
    }

    public bool TrySetArtifactFavourite(string artifactId, bool favourite, out string error)
    {
        NormalizeSnapshot();
        if (!HasLoadedArtifactAccount(out error)) return false;
        var artifact = snapshot.items.Artifacts.Find(a => a.InstanceId == artifactId);
        if (artifact == null) { error = "This artifact is no longer in your backpack."; return false; }
        if (artifact.Favourite == favourite) return true;
        var next = snapshot.items.Clone();
        next.Artifacts.Find(a => a.InstanceId == artifactId).Favourite = favourite;
        return CommitArtifactInventory(next, out error);
    }

    public bool TryEquipArtifact(string artifactId, string characterInstanceId, out string error)
    {
        NormalizeSnapshot();
        if (!HasLoadedArtifactAccount(out error)) return false;
        if (string.IsNullOrWhiteSpace(characterInstanceId) || snapshot.FindOwned(characterInstanceId) == null)
        { error = "Choose a fighter owned by this account."; return false; }
        var artifact = snapshot.items.Artifacts.Find(a => a.InstanceId == artifactId);
        if (artifact == null) { error = "This artifact is no longer in your backpack."; return false; }
        if (artifact.EquippedCharacterId == characterInstanceId) return true;
        if (!string.IsNullOrEmpty(artifact.EquippedCharacterId))
        {
            var owner = snapshot.FindOwned(artifact.EquippedCharacterId);
            var definition = owner == null ? null : FindDefinition(owner.definitionId);
            error = "Unequip this artifact from " + (definition != null ? definition.name : "its current fighter") + " first.";
            return false;
        }
        var next = snapshot.items.Clone();
        // Replacing a slot returns its previous piece to the backpack; no artifact is consumed.
        foreach (var equipped in next.Artifacts)
            if (equipped.EquippedCharacterId == characterInstanceId && equipped.Slot == artifact.Slot)
                equipped.EquippedCharacterId = string.Empty;
        next.Artifacts.Find(a => a.InstanceId == artifactId).EquippedCharacterId = characterInstanceId;
        return CommitArtifactInventory(next, out error);
    }

    public bool TryUnequipArtifact(string characterInstanceId, ArtifactSlot slot, out string error)
    {
        NormalizeSnapshot();
        if (!HasLoadedArtifactAccount(out error)) return false;
        if (string.IsNullOrWhiteSpace(characterInstanceId) || snapshot.FindOwned(characterInstanceId) == null)
        { error = "Choose a fighter owned by this account."; return false; }
        if (!Enum.IsDefined(typeof(ArtifactSlot), slot))
        { error = "Choose a valid artifact slot."; return false; }
        var equipped = snapshot.items.Artifacts.Find(a => a.EquippedCharacterId == characterInstanceId && a.Slot == slot);
        if (equipped == null) return true;
        var next = snapshot.items.Clone();
        next.Artifacts.Find(a => a.InstanceId == equipped.InstanceId).EquippedCharacterId = string.Empty;
        return CommitArtifactInventory(next, out error);
    }

    public bool PreviewArtifactEnhancement(string targetId, long dustAmount, IEnumerable<string> fodderIds,
        out ArtifactEnhancementPreview preview, out string error)
    {
        NormalizeSnapshot();
        preview = null;
        if (!HasLoadedArtifactAccount(out error)) return false;
        return ArtifactRules.PreviewEnhance(snapshot.items, targetId, dustAmount, fodderIds, out preview, out error);
    }

    public bool TryEnhanceArtifact(string targetId, long dustAmount, IEnumerable<string> fodderIds,
        out ArtifactEnhancementPreview preview, out string error)
    {
        NormalizeSnapshot();
        preview = null;
        if (!HasLoadedArtifactAccount(out error)) return false;
        if (!ArtifactRules.TryEnhance(snapshot.items, targetId, dustAmount, fodderIds, out var next, out var result, out error))
            return false;
        if (!CommitArtifactInventory(next, out error)) return false;
        preview = result;
        return true;
    }

    private bool HasLoadedArtifactAccount(out string error)
    {
        error = null;
        if (!string.IsNullOrWhiteSpace(_loadedSubjectId) &&
            string.Equals(snapshot.subjectId, _loadedSubjectId, StringComparison.Ordinal)) return true;
        error = "Load your account before changing artifacts.";
        return false;
    }

    private bool CommitArtifactInventory(ItemInventory next, out string error)
    {
        error = null;
        var previous = snapshot.items;
        snapshot.items = next;
        try { SaveCurrent(); }
        catch (Exception exception)
        {
            snapshot.items = previous;
            error = "This change could not be saved. Your artifacts and materials were kept.";
            Debug.LogError("Artifact inventory save failed: " + exception.Message, this);
            return false;
        }
        return true;
    }

    public bool GrantDungeonReward(DungeonRewardReceipt reward)
    {
        if (reward == null || reward.CompletionBonus) return false;
        NormalizeSnapshot();
        // The saved victory receipt is also the account delivery key.
        if (!snapshot.items.TryGrant(reward, out var next)) return false;
        var diamonds = checked(snapshot.diamonds + checked((int)reward.Amount(ItemCatalog.Diamond)));
        var previous = snapshot.items;
        var previousDiamonds = snapshot.diamonds;
        snapshot.items = next; snapshot.diamonds = diamonds;
        try { SaveCurrent(); }
        catch
        {
            snapshot.items = previous; snapshot.diamonds = previousDiamonds;
            throw;
        }
        return true;
    }

    public bool TrySpendUpgradeResources(IEnumerable<ItemAmount> costs, UpgradeResourceUse use, out string error)
    {
        NormalizeSnapshot();
        if (!snapshot.items.TrySpend(costs, use, out var next, out error)) return false;
        var previous = snapshot.items; snapshot.items = next;
        try { SaveCurrent(); }
        catch { snapshot.items = previous; throw; }
        return true;
    }

    public StatBlock GetCharacterStats(CharacterObject character)
    {
        if (character == null) return null;
        var definition = FightingAllstar.Presentation.Content.CharacterObjectCatalogBuilder.CreateDefinition(character);
        var owned = FindOwnedByDefinition(character.DefinitionId);
        var levelStats = CharacterProgression.StatsAtLevel(definition.BaseStats, definition.StatGrowth, owned?.level ?? 1);
        return ArtifactRules.ApplyStats(levelStats, GetEquippedArtifacts(owned?.instanceId));
    }

    public RunFighterSeed CreateRunFighterSeed(CharacterDefinition definition, int slot)
    {
        var owned = definition == null ? null : FindOwnedByDefinition(definition.Id);
        if (owned == null || !definition.RuntimeReady || slot < 0 || slot > 3)
            throw new InvalidOperationException("Choose an owned, battle-ready fighter in a valid formation slot.");
        // Freeze equipment with the run seed. Account changes must not alter fighters in an existing run.
        var runDefinition = definition.Clone();
        runDefinition.ArtifactLoadout = GetEquippedArtifacts(owned.instanceId).Select(a => a.Clone()).ToList();
        var levelStats = CharacterProgression.StatsAtLevel(definition.BaseStats, definition.StatGrowth, owned.level);
        return new RunFighterSeed { OwnedFighterId = owned.instanceId, Definition = runDefinition,
            ResolvedStats = ArtifactRules.ApplyStats(levelStats, runDefinition.ArtifactLoadout),
            ConstellationTier = owned.constellationTier, FormationSlot = slot, IsReserve = slot == 3 };
    }

    public bool TryUpgradeCharacter(string instanceId, bool limitBreak, out string error)
    {
        NormalizeSnapshot();
        var owned = snapshot.FindOwned(instanceId);
        var character = owned == null ? null : FindDefinition(owned.definitionId);
        if (character == null || !character.RuntimeReady || string.IsNullOrWhiteSpace(_loadedSubjectId))
        { error = "Choose an owned, battle-ready fighter on a loaded account."; return false; }
        if (!CharacterProgression.TryCosts(character.ID, character.SeriesId, owned.level, owned.levelCap,
            limitBreak, out var costs, out error)) return false;
        if (!snapshot.items.TrySpend(costs, UpgradeResourceUse.CharacterUpgrade, out var next, out error)) return false;
        var previousItems = snapshot.items; var previousLevel = owned.level; var previousCap = owned.levelCap;
        snapshot.items = next;
        if (limitBreak) owned.levelCap += 10; else owned.level++;
        try { SaveCurrent(); }
        catch (Exception exception)
        {
            snapshot.items = previousItems; owned.level = previousLevel; owned.levelCap = previousCap;
            error = "Upgrade could not be saved. Your resources were kept.";
            Debug.LogError("Character upgrade save failed: " + exception.Message, this);
            return false;
        }
        return true;
    }
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
        _catalog = CharacterObjectRegistrySO.LoadAll();
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
#if UNITY_EDITOR
        if (useEditorProfile && editorProfile != null)
        {
            snapshot = editorProfile.CloneSnapshot();
            if (snapshot == null) snapshot = new PlayerInventorySnapshot { subjectId = subjectId };
            snapshot.subjectId = subjectId;
        }
        else
#endif
        {
            snapshot = Load(subjectId);
            if (snapshot == null)
            {
                snapshot = new PlayerInventorySnapshot { subjectId = subjectId };
                SeedInitialRoster();
                SaveCurrent();
            }
        }
        NormalizeSnapshot();
        // Migrate legacy account-level formations. Active runs own their own slots.
        snapshot.formation = new List<string> { "", "", "", "" };
        snapshot.dungeonFormations.Clear();
        SaveCurrent();
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
        else if (owned.constellationTier < 5)
        {
            owned.constellationTier++;
        }
        else
        {
            GrantCharacterDupeToken(definition.DefinitionId);
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

    public bool TrySummon(int count, SummonBannerConfigurationSO banner, out List<CharacterObject> results, out string error)
    {
        results = new List<CharacterObject>();
        error = null;
        if (count != 1 && count != 10) { error = "Choose one or ten summons."; return false; }
        if (banner == null || !banner.Validate(out error))
        {
            if (string.IsNullOrEmpty(error)) error = "Summon banner configuration is missing.";
            return false;
        }
        NormalizeSnapshot();
        var cost = count == 1 ? SingleSummonCost : TenSummonCost;
        if (snapshot.diamonds < cost) { error = "Not enough Diamonds."; return false; }
        var before = JsonUtility.ToJson(snapshot);
        try
        {
            for (var i = 0; i < count; i++)
            {
                var roll = UnityEngine.Random.Range(0, banner.TotalRateBasisPoints);
                var cumulativeRate = 0;
                SummonBannerRarityPool selectedPool = null;
                foreach (var pool in banner.RarityPools)
                {
                    cumulativeRate += pool.RateBasisPoints;
                    if (roll < cumulativeRate)
                    {
                        selectedPool = pool;
                        break;
                    }
                }
                var poolCharacters = selectedPool?.Characters;
                var character = poolCharacters == null || poolCharacters.Count == 0
                    ? null
                    : poolCharacters[UnityEngine.Random.Range(0, poolCharacters.Count)];
                if (character == null) throw new InvalidOperationException("Banner roll did not resolve to a character.");
                results.Add(character);
                var owned = snapshot.FindDefinition(character.DefinitionId);
                if (owned == null) snapshot.characters.Add(new OwnedCharacterRecord { instanceId = Guid.NewGuid().ToString("N"), definitionId = character.DefinitionId });
                else if (owned.constellationTier < 5) owned.constellationTier++;
                else GrantCharacterDupeToken(character.DefinitionId);
                snapshot.featuredGuaranteeProgress++;
                if (snapshot.featuredGuaranteeProgress >= GuaranteeThreshold)
                {
                    snapshot.featuredGuaranteeProgress -= GuaranteeThreshold;
                    snapshot.selectorEntitlements++;
                }
            }
            snapshot.diamonds -= cost;
            SaveCurrent();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
            snapshot = JsonUtility.FromJson<PlayerInventorySnapshot>(before);
            results.Clear(); error = "Summon could not be saved: " + ex.Message; return false;
        }
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
        return TryClaimSelector(FindDefinition(definitionId), out error);
    }

    public bool TryClaimSelector(CharacterObject definition, out string error)
    {
        error = null;
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
        if (string.IsNullOrEmpty(dungeonId)) return new List<string> { "", "", "", "" };
        var saved = snapshot.dungeonFormations.Find(x => x != null && string.Equals(x.dungeonId, dungeonId, StringComparison.OrdinalIgnoreCase));
        if (saved != null && saved.formation != null && saved.formation.Count == 4)
            return saved.formation;
        return new List<string> { "", "", "", "" };
    }

    public bool HasDungeonFormation(string dungeonId)
    {
        NormalizeSnapshot();
        if (string.IsNullOrEmpty(dungeonId)) return false;
        var saved = snapshot.dungeonFormations.Find(x => x != null && string.Equals(x.dungeonId, dungeonId, StringComparison.OrdinalIgnoreCase));
        if (saved == null || saved.formation == null || saved.formation.Count != 4) return false;
        for (var i = 0; i < saved.formation.Count; i++)
        {
            if (!string.IsNullOrEmpty(saved.formation[i])) return true;
        }
        return false;
    }

    public void ClearDungeonFormation(string dungeonId)
    {
        if (string.IsNullOrEmpty(dungeonId)) return;
        NormalizeSnapshot();
        snapshot.dungeonFormations.RemoveAll(x => x != null && string.Equals(x.dungeonId, dungeonId, StringComparison.OrdinalIgnoreCase));
        SaveCurrent();
    }

    public void RevertDungeonFormation(string dungeonId, IReadOnlyList<string> previousFormation)
    {
        if (string.IsNullOrEmpty(dungeonId)) return;
        NormalizeSnapshot();
        var hasAny = false;
        if (previousFormation != null)
        {
            for (var i = 0; i < previousFormation.Count; i++)
            {
                if (!string.IsNullOrEmpty(previousFormation[i])) { hasAny = true; break; }
            }
        }
        if (!hasAny)
        {
            ClearDungeonFormation(dungeonId);
            return;
        }
        SaveDungeonFormation(dungeonId, previousFormation);
    }

    public void SaveDungeonFormation(string dungeonId, IReadOnlyList<string> instanceIds)
    {
        if (string.IsNullOrEmpty(dungeonId)) return;
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
        var accountOnly = JsonUtility.FromJson<PlayerInventorySnapshot>(JsonUtility.ToJson(snapshot));
        accountOnly.formation = new List<string> { "", "", "", "" };
        accountOnly.dungeonFormations.Clear();
        File.WriteAllText(temporaryPath, JsonUtility.ToJson(accountOnly, true));
        if (File.Exists(path)) File.Replace(temporaryPath, path, null);
        else File.Move(temporaryPath, path);
        // A UI subscriber failure must not roll back an already committed account file.
        foreach (var listener in InventoryChanged?.GetInvocationList() ?? Array.Empty<Delegate>())
        {
            try { ((Action)listener)(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }
    }

    private void SeedInitialRoster()
    {
        var starterIds = new[] { "fighter.kyo94", "fighter.benimaru94", "fighter.goro94" };
        foreach (var definitionId in starterIds)
        {
            if (snapshot.FindDefinition(definitionId) != null) continue;
            snapshot.characters.Add(new OwnedCharacterRecord
            {
                instanceId = Guid.NewGuid().ToString("N"),
                definitionId = definitionId,
                constellationTier = 0,
                level = 1
            });
        }
        while (snapshot.formation.Count < 4) snapshot.formation.Add(string.Empty);
        for (var i = 0; i < 4; i++)
        {
            if (i < snapshot.characters.Count) snapshot.formation[i] = snapshot.characters[i].instanceId;
            else snapshot.formation[i] = string.Empty;
        }
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
        if (snapshot.characterDupeTokens == null) snapshot.characterDupeTokens = new List<CharacterDupeTokenStack>();
        if (snapshot.formation == null) snapshot.formation = new List<string>();
        if (snapshot.dungeonFormations == null) snapshot.dungeonFormations = new List<DungeonFormationSave>();
        if (snapshot.claimedRunRewards == null) snapshot.claimedRunRewards = new List<string>();
        if (snapshot.items == null) snapshot.items = new ItemInventory();
        if (snapshot.items.Materials == null) snapshot.items.Materials = new List<ItemAmount>();
        if (snapshot.items.Artifacts == null) snapshot.items.Artifacts = new List<ArtifactInstance>();
        if (snapshot.items.ClaimedReceiptIds == null) snapshot.items.ClaimedReceiptIds = new List<string>();
        snapshot.artifactAutoSelectRarity = Mathf.Clamp(snapshot.artifactAutoSelectRarity, 1, 5);
        NormalizeResourceInventory();
        if (snapshot.characters.Count == 0)
        {
            SeedInitialRoster();
        }
        while (snapshot.formation.Count < 4) snapshot.formation.Add(string.Empty);
        if (snapshot.formation.Count > 4) snapshot.formation.RemoveRange(4, snapshot.formation.Count - 4);
        foreach (var df in snapshot.dungeonFormations)
        {
            if (df == null) continue;
            if (df.formation == null) df.formation = new List<string>();
            while (df.formation.Count < 4) df.formation.Add(string.Empty);
            if (df.formation.Count > 4) df.formation.RemoveRange(4, df.formation.Count - 4);
        }
        foreach (var owned in snapshot.characters)
            if (owned != null)
            {
                owned.constellationTier = Mathf.Clamp(owned.constellationTier, 0, 5);
                owned.level = CharacterProgression.NormalizeLevel(owned.level);
                owned.levelCap = CharacterProgression.NormalizeCap(owned.level, owned.levelCap);
            }
        NormalizeArtifactInventory();
        NormalizeCharacterDupeTokens();
    }

    private void GrantCharacterDupeToken(string characterDefinitionId)
    {
        if (string.IsNullOrWhiteSpace(characterDefinitionId)) return;
        if (snapshot.characterDupeTokens == null) snapshot.characterDupeTokens = new List<CharacterDupeTokenStack>();
        var stack = snapshot.characterDupeTokens.Find(x => x != null && x.characterDefinitionId == characterDefinitionId);
        if (stack == null)
        {
            stack = new CharacterDupeTokenStack { characterDefinitionId = characterDefinitionId, amount = 0 };
            snapshot.characterDupeTokens.Add(stack);
        }
        if (stack.amount < int.MaxValue) stack.amount++;
    }

    private void NormalizeCharacterDupeTokens()
    {
        var balances = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var stack in snapshot.characterDupeTokens)
        {
            if (stack == null || string.IsNullOrWhiteSpace(stack.characterDefinitionId) || stack.amount <= 0) continue;
            balances.TryGetValue(stack.characterDefinitionId, out var current);
            balances[stack.characterDefinitionId] = current > int.MaxValue - stack.amount ? int.MaxValue : current + stack.amount;
        }
        snapshot.characterDupeTokens = balances.Select(pair => new CharacterDupeTokenStack
        { characterDefinitionId = pair.Key, amount = pair.Value }).ToList();
    }

    private void NormalizeResourceInventory()
    {
        snapshot.items.Coins = Math.Max(0, snapshot.items.Coins);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var needsRepair = false;
        foreach (var stack in snapshot.items.Materials)
            if (stack == null || string.IsNullOrWhiteSpace(stack.ItemId) || stack.Amount <= 0 || !ids.Add(stack.ItemId))
            { needsRepair = true; break; }
        if (!needsRepair) return;
        var balances = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var stack in snapshot.items.Materials)
        {
            if (stack == null || string.IsNullOrWhiteSpace(stack.ItemId) || stack.Amount <= 0) continue;
            balances.TryGetValue(stack.ItemId, out var current);
            // Imported profiles may contain several stacks. Saturation keeps a malformed save loadable
            // without wrapping its inventory into a negative balance or crashing a resource preview.
            balances[stack.ItemId] = current > long.MaxValue - stack.Amount ? long.MaxValue : current + stack.Amount;
        }
        snapshot.items.Materials = balances.Select(pair => new ItemAmount { ItemId = pair.Key, Amount = pair.Value }).ToList();
    }

    private void NormalizeArtifactInventory()
    {
        snapshot.items.Artifacts.RemoveAll(a => a == null);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var equippedSlots = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < snapshot.items.Artifacts.Count; index++)
        {
            var artifact = snapshot.items.Artifacts[index];
            if (string.IsNullOrWhiteSpace(artifact.InstanceId) || !seenIds.Add(artifact.InstanceId))
            {
                // Legacy profiles occasionally omitted or duplicated instance IDs. Preserve every piece,
                // assigning a stable account-scoped replacement rather than rerolling on each load.
                var source = (_loadedSubjectId ?? snapshot.subjectId ?? string.Empty) + ":artifact:" + index + ":" +
                    artifact.InstanceId + ":" + artifact.ItemId + ":" + artifact.SourceRunId + ":" + artifact.SourceNodeId;
                using (var hash = SHA256.Create())
                    artifact.InstanceId = "legacy:artifact:" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(source)))
                        .Replace("-", string.Empty).ToLowerInvariant();
                while (!seenIds.Add(artifact.InstanceId)) artifact.InstanceId += ":duplicate";
            }
            ArtifactRules.Normalize(artifact);
            if (string.IsNullOrEmpty(artifact.EquippedCharacterId)) continue;
            var equippedKey = artifact.EquippedCharacterId + ":" + (int)artifact.Slot;
            if (snapshot.FindOwned(artifact.EquippedCharacterId) == null || !equippedSlots.Add(equippedKey))
                artifact.EquippedCharacterId = string.Empty;
        }
    }

    private void EnsureCatalog()
    {
        if (_catalog == null || _catalog.Length == 0) _catalog = CharacterObjectRegistrySO.LoadAll();
    }

    public void ApplyProfile(PlayerInventoryProfileSO profileToApply)
    {
        if (profileToApply == null) return;
        snapshot = profileToApply.CloneSnapshot();
        snapshot.subjectId = _loadedSubjectId ?? GuestSubjectId;
        NormalizeSnapshot();
        SaveCurrent();
    }

    public void ExportCurrentToProfile(PlayerInventoryProfileSO targetProfile)
    {
        if (targetProfile == null) return;
        NormalizeSnapshot();
        targetProfile.CopyFrom(snapshot);
    }

    public void ReloadFromDisk()
    {
        snapshot = Load(_loadedSubjectId ?? GuestSubjectId);
        NormalizeSnapshot();
        InventoryChanged?.Invoke();
    }

    public static string GetSavePath(string subjectId = null) => GetPath(string.IsNullOrWhiteSpace(subjectId) ? GuestSubjectId : subjectId);

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
