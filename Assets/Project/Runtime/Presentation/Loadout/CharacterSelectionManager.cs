using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Adapters;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;
using FightingAllstar.Presentation.Content;
using FightingAllstar.Presentation.Economy;
using FightingAllstar.Presentation.Route;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[System.Serializable]
public class CharacterSlot
{
    public string SlotName;
    public CharacterLoadOut LoadOutPosition;
    public CharacterObject AssignedCharacter;

    public void AssignCharacter(CharacterObject character) => AssignedCharacter = character;
}

public class CharacterSelectionManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject inventoryScreen;
    [SerializeField] private GridLayoutGroup gridLayoutGroup;
    [SerializeField] private GameObject characterDisplayPrefab;
    [SerializeField] private TextMeshProUGUI totalCCText;
    [SerializeField] private CharacterLoadOut currentSelectedLoadOut;

    [Header("Formation")]
    [SerializeField] private List<CharacterSlot> characterSlots;

    public static CharacterSelectionManager Instance { get; private set; }

    private bool _isInitialized;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            _isInitialized = true;
            InitializeUI();
        }
    }

    private void Start()
    {
        EnsureInitialized();
    }

    private void OnEnable()
    {
        if (_isInitialized)
        {
            RestoreSavedFormation();
            UpdateTotalTeamCC();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void InitializeUI()
    {
        if (inventoryScreen != null) inventoryScreen.SetActive(false);
        if (characterSlots == null || characterSlots.Count != 4)
            Debug.LogError("Character Loadout needs four configured positions: three active slots and an optional reserve slot.", this);

        if (DungeonFlowContext.ActiveRun == null && new LocalRunStateStore().TryLoad(out var saved) &&
            (saved.Status == RunStatus.InProgress || saved.Status == RunStatus.InBattle))
        {
            DungeonFlowContext.SetCatalog(CharacterObjectCatalogBuilder.Load(null).Characters);
            DungeonFlowContext.BeginDungeonFlowForEncounter(DungeonFlowContext.GetProfileForId(saved.ProfileId), saved, null);
        }
        RestoreSavedFormation();
        PopulateCharacterGrid();
        UpdateTotalTeamCC();

        if (DungeonFlowContext.IsDungeonLoadoutMode)
        {
            var changeSceneObj = GameObject.Find("ChangeScene");
            if (changeSceneObj != null)
            {
                var text = changeSceneObj.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null) text.text = DungeonFlowContext.ActiveRun != null ? "Back to Route" : "Back to Stages";
                var btn = changeSceneObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(OnBackButtonClicked);
                }
            }
        }
    }

    public int GetSlotIndex(CharacterLoadOut loadOut)
    {
        if (characterSlots == null || loadOut == null) return -1;
        return characterSlots.FindIndex(slot => slot != null && slot.LoadOutPosition == loadOut);
    }

    public CharacterObject GetAssignedCharacter(CharacterLoadOut loadOut)
    {
        EnsureInitialized();
        if (characterSlots == null || loadOut == null) return null;
        var slot = characterSlots.FirstOrDefault(s => s != null && s.LoadOutPosition == loadOut);
        return slot?.AssignedCharacter;
    }

    public CharacterSlot GetSlot(int index)
    {
        if (characterSlots == null || index < 0 || index >= characterSlots.Count) return null;
        return characterSlots[index];
    }

    private void RestoreSavedFormation()
    {
        if (characterSlots == null) return;
        var inventory = PlayerInventoryService.Instance;
        if (inventory == null) return;

        var profile = DungeonFlowContext.IsDungeonLoadoutMode ? DungeonFlowContext.ActiveProfile : null;
        var run = DungeonFlowContext.ActiveRun;
        var definitions = inventory.GetOwnedDefinitions();

        var candidateInstanceIds = new List<string>(new string[characterSlots.Count]);
        for (var i = 0; i < candidateInstanceIds.Count; i++) candidateInstanceIds[i] = string.Empty;

        // 1. If in Dungeon Flow with an Active Run, check run formation or run roster
        if (run != null)
        {
            if (run.Formation != null && run.Formation.Any(id => !string.IsNullOrEmpty(id)))
            {
                for (var i = 0; i < characterSlots.Count && i < run.Formation.Count; i++)
                {
                    candidateInstanceIds[i] = run.Formation[i] ?? string.Empty;
                }
            }
            else if (run.Roster != null && run.Roster.Count > 0)
            {
                for (var i = 0; i < run.Roster.Count; i++)
                {
                    var rf = run.Roster[i];
                    if (rf == null) continue;
                    var slot = (rf.OriginalFormationIndex >= 0 && rf.OriginalFormationIndex < characterSlots.Count)
                        ? rf.OriginalFormationIndex
                        : i;
                    if (slot >= 0 && slot < characterSlots.Count && string.IsNullOrEmpty(candidateInstanceIds[slot]))
                    {
                        var owned = inventory.FindOwnedByDefinition(rf.DefinitionId);
                        if (owned != null) candidateInstanceIds[slot] = owned.instanceId;
                    }
                }
            }
        }

        // 2. If in Dungeon Mode and still empty, check profile dungeon formation
        if (profile != null && !candidateInstanceIds.Any(id => !string.IsNullOrEmpty(id)))
        {
            var dungeonFormation = inventory.GetDungeonFormation(profile.Id);
            if (dungeonFormation != null && dungeonFormation.Count == characterSlots.Count && dungeonFormation.Any(id => !string.IsNullOrEmpty(id)))
            {
                for (var i = 0; i < characterSlots.Count; i++)
                {
                    candidateInstanceIds[i] = dungeonFormation[i] ?? string.Empty;
                }
            }
        }

        // 3. If still empty, check PlayerInventoryService snapshot formation
        if (!candidateInstanceIds.Any(id => !string.IsNullOrEmpty(id)))
        {
            var savedFormation = inventory.Snapshot?.formation;
            if (savedFormation != null && savedFormation.Any(id => !string.IsNullOrEmpty(id)))
            {
                for (var i = 0; i < characterSlots.Count && i < savedFormation.Count; i++)
                {
                    candidateInstanceIds[i] = savedFormation[i] ?? string.Empty;
                }
            }
        }

        // 4. If still empty, check LocalEconomyStore
        if (!candidateInstanceIds.Any(id => !string.IsNullOrEmpty(id)))
        {
            try
            {
                var store = new LocalEconomyStore();
                var state = store.LoadOrCreateLocalProfile();
                if (state != null && state.FormationDefinitionIds != null && state.FormationDefinitionIds.Count == characterSlots.Count)
                {
                    var instIds = inventory.GetInstanceFormation(state.FormationDefinitionIds);
                    if (instIds != null && instIds.Any(id => !string.IsNullOrEmpty(id)))
                    {
                        for (var i = 0; i < characterSlots.Count; i++)
                        {
                            candidateInstanceIds[i] = instIds[i] ?? string.Empty;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Could not load local economy formation: " + ex.Message, this);
            }
        }

        // 5. If still empty, check if characterSlots already have AssignedCharacter pre-configured
        if (!candidateInstanceIds.Any(id => !string.IsNullOrEmpty(id)))
        {
            for (var i = 0; i < characterSlots.Count; i++)
            {
                var assigned = characterSlots[i]?.AssignedCharacter;
                if (assigned != null)
                {
                    var owned = inventory.FindOwnedByDefinition(assigned.DefinitionId);
                    if (owned != null)
                    {
                        candidateInstanceIds[i] = owned.instanceId;
                    }
                }
            }
        }

        // 6. If STILL empty, auto-fill slots with eligible owned characters
        if (!candidateInstanceIds.Any(id => !string.IsNullOrEmpty(id)))
        {
            var eligible = definitions.Where(d => d != null && (profile == null || DungeonFlowContext.IsCharacterEligible(profile, d))).ToList();
            var used = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < characterSlots.Count && i < eligible.Count; i++)
            {
                var owned = inventory.FindOwnedByDefinition(eligible[i].DefinitionId);
                if (owned != null && used.Add(owned.instanceId))
                {
                    candidateInstanceIds[i] = owned.instanceId;
                }
            }
        }

        // Filter against profile eligibility if in dungeon mode, and prevent duplicates
        var usedInstances = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < candidateInstanceIds.Count; i++)
        {
            var instId = candidateInstanceIds[i];
            if (string.IsNullOrEmpty(instId) || !usedInstances.Add(instId))
            {
                candidateInstanceIds[i] = string.Empty;
                continue;
            }

            var owned = inventory.FindOwnedByInstance(instId);
            if (owned == null)
            {
                candidateInstanceIds[i] = string.Empty;
                continue;
            }

            var def = definitions.FirstOrDefault(c => c != null && c.DefinitionId == owned.definitionId);
            if (def == null || (profile != null && !DungeonFlowContext.IsCharacterEligible(profile, def)))
            {
                candidateInstanceIds[i] = string.Empty;
            }
        }

        ApplyFormationToSlots(candidateInstanceIds, inventory);
    }

    private void ApplyFormationToSlots(IReadOnlyList<string> formation, PlayerInventoryService inventory)
    {
        if (characterSlots == null || formation == null || inventory == null) return;
        var definitions = inventory.GetOwnedDefinitions();
        for (var i = 0; i < characterSlots.Count; i++)
        {
            var slot = characterSlots[i];
            if (slot == null || slot.LoadOutPosition == null) continue;
            var ownedId = i < formation.Count ? formation[i] : string.Empty;
            var owned = string.IsNullOrEmpty(ownedId) ? null : inventory.FindOwnedByInstance(ownedId);
            var character = owned == null ? null : definitions.FirstOrDefault(item => item != null && item.DefinitionId == owned.definitionId);
            slot.AssignCharacter(character);
            slot.LoadOutPosition.DisplayCharacterInfo(character, owned);
        }
    }

    private void PopulateCharacterGrid()
    {
        if (gridLayoutGroup == null || characterDisplayPrefab == null)
        {
            Debug.LogError("Character grid or character card prefab is not assigned in the loadout scene.", this);
            return;
        }

        for (var i = gridLayoutGroup.transform.childCount - 1; i >= 0; i--)
            Destroy(gridLayoutGroup.transform.GetChild(i).gameObject);

        var inventory = PlayerInventoryService.Instance;
        if (inventory == null)
        {
            Debug.LogError("Player inventory is unavailable; cannot populate the owned roster.", this);
            return;
        }

        // Add "Empty This Slot" command as the first item in the grid
        var emptyCardObj = Instantiate(characterDisplayPrefab, gridLayoutGroup.transform);
        var emptyCard = emptyCardObj.GetComponent<GachaChar>();
        if (emptyCard != null)
        {
            emptyCard.SetAsEmptySlotCommand();
        }

        var profile = DungeonFlowContext.IsDungeonLoadoutMode ? DungeonFlowContext.ActiveProfile : null;

        foreach (var character in inventory.GetOwnedDefinitions())
        {
            if (character == null) continue;

            // Apply Dungeon Policy Filter if active
            if (profile != null && !DungeonFlowContext.IsCharacterEligible(profile, character))
            {
                continue;
            }

            var card = Instantiate(characterDisplayPrefab, gridLayoutGroup.transform);
            var characterCard = card.GetComponent<GachaChar>();
            if (characterCard == null)
            {
                Debug.LogError("Character card prefab is missing its GachaChar component.", card);
                Destroy(card);
                continue;
            }
            characterCard.SetCharacter(character);
        }
    }

    public void SelectCharacterLoadOut(CharacterLoadOut selectedLoadOut)
    {
        if (selectedLoadOut == null || characterSlots == null ||
            !characterSlots.Any(slot => slot != null && slot.LoadOutPosition == selectedLoadOut)) return;

        currentSelectedLoadOut = selectedLoadOut;
        foreach (var slot in characterSlots)
            slot?.LoadOutPosition?.ToggleSelectionVisual(slot.LoadOutPosition == selectedLoadOut);

        if (inventoryScreen == null) return;
        inventoryScreen.SetActive(true);
        var scrollRect = inventoryScreen.GetComponent<ScrollRect>();
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
    }

    public void AssignCharacterToLoadOut(CharacterObject character)
    {
        var inventory = PlayerInventoryService.Instance;
        var targetIndex = characterSlots == null || currentSelectedLoadOut == null
            ? -1
            : characterSlots.FindIndex(slot => slot != null && slot.LoadOutPosition == currentSelectedLoadOut);
        var owned = character == null || inventory == null ? null : inventory.FindOwnedByDefinition(character.DefinitionId);
        if (targetIndex < 0 || owned == null)
        {
            Debug.LogWarning("Choose a formation position and an owned character before assigning.", this);
            return;
        }

        var formation = new List<string>(4);
        for (var i = 0; i < 4; i++)
        {
            var assigned = characterSlots[i]?.AssignedCharacter;
            var existing = assigned == null ? null : inventory.FindOwnedByDefinition(assigned.DefinitionId);
            formation.Add(existing == null ? string.Empty : existing.instanceId);
        }

        var sourceIndex = formation.IndexOf(owned.instanceId);
        var displaced = formation[targetIndex];
        formation[targetIndex] = owned.instanceId;
        if (sourceIndex >= 0 && sourceIndex != targetIndex)
            formation[sourceIndex] = displaced;

        ApplyFormationToSlots(formation, inventory);
        UpdateTotalTeamCC();
        CloseInventory();
    }

    public void SwapSlots(CharacterLoadOut slotA, CharacterLoadOut slotB)
    {
        if (slotA == null || slotB == null || slotA == slotB || characterSlots == null) return;
        var indexA = characterSlots.FindIndex(s => s != null && s.LoadOutPosition == slotA);
        var indexB = characterSlots.FindIndex(s => s != null && s.LoadOutPosition == slotB);
        if (indexA < 0 || indexB < 0) return;

        var inventory = PlayerInventoryService.Instance;
        if (inventory == null) return;

        var formation = new List<string>(4);
        for (var i = 0; i < 4; i++)
        {
            var assigned = characterSlots[i]?.AssignedCharacter;
            var owned = assigned == null ? null : inventory.FindOwnedByDefinition(assigned.DefinitionId);
            formation.Add(owned == null ? string.Empty : owned.instanceId);
        }

        var temp = formation[indexA];
        formation[indexA] = formation[indexB];
        formation[indexB] = temp;

        ApplyFormationToSlots(formation, inventory);
        UpdateTotalTeamCC();
    }

    public void ClearCurrentSelectedSlot()
    {
        if (characterSlots == null || currentSelectedLoadOut == null) return;
        var slotIndex = characterSlots.FindIndex(slot => slot != null && slot.LoadOutPosition == currentSelectedLoadOut);
        if (slotIndex < 0 || PlayerInventoryService.Instance == null) return;

        var formation = new List<string>(4);
        for (var i = 0; i < 4; i++)
        {
            var assigned = characterSlots[i]?.AssignedCharacter;
            var owned = assigned == null ? null : PlayerInventoryService.Instance.FindOwnedByDefinition(assigned.DefinitionId);
            formation.Add(owned == null ? string.Empty : owned.instanceId);
        }
        formation[slotIndex] = string.Empty;
        var inventory = PlayerInventoryService.Instance;
        ApplyFormationToSlots(formation, inventory);
        UpdateTotalTeamCC();
        CloseInventory();
    }

    public void EmptyThisSlot() => ClearCurrentSelectedSlot();

    // Kept as a compatibility entry point for older scene button bindings.
    public void ClearFighterInfo() => ClearCurrentSelectedSlot();

    public void OnBackButtonClicked()
    {
        if (DungeonFlowContext.IsDungeonLoadoutMode)
        {
            if (DungeonFlowContext.ActiveRun != null)
            {
                SceneManager.LoadScene("Combat");
            }
            else
            {
                DungeonFlowContext.Clear();
                SceneManager.LoadScene("Combat");
            }
        }
        else
        {
            SceneManager.LoadScene("MainMenu");
        }
    }

    private void CloseInventory()
    {
        foreach (var slot in characterSlots)
            slot?.LoadOutPosition?.ToggleSelectionVisual(false);
        if (inventoryScreen != null) inventoryScreen.SetActive(false);
        currentSelectedLoadOut = null;
    }

    private void UpdateTotalTeamCC()
    {
        SaveRunFormation();
        if (totalCCText == null || characterSlots == null) return;
        var total = characterSlots
            .Where(slot => slot?.AssignedCharacter != null)
            .Sum(slot => slot.AssignedCharacter.Classpower);

        if (DungeonFlowContext.IsDungeonLoadoutMode && DungeonFlowContext.ActiveProfile != null)
        {
            totalCCText.text = $"{DungeonFlowContext.ActiveProfile.DisplayName} (Filtered) · CC: {total:N0}";
        }
        else
        {
            totalCCText.text = $"Total Team CC: {total:N0}";
        }
    }

    public void DeselectAll()
    {
        if (characterSlots == null) return;
        foreach (var slot in characterSlots)
            slot?.LoadOutPosition?.ToggleSelectionVisual(false);
    }

    private void SaveRunFormation()
    {
        var inventory = PlayerInventoryService.Instance;
        if (inventory == null || characterSlots == null || characterSlots.Count != 4) return;

        var formation = characterSlots.Select(s => s?.AssignedCharacter == null ? "" :
            inventory.FindOwnedByDefinition(s.AssignedCharacter.DefinitionId)?.instanceId ?? "").ToList();

        // 1. If in Dungeon run, persist to ActiveRun and LocalRunStateStore
        var run = DungeonFlowContext.ActiveRun;
        if (run != null)
        {
            run.Formation = new List<string>(formation);
            new LocalRunStateStore().Save(run);
        }

        // 2. If in Dungeon mode with profile, persist dungeon formation
        if (DungeonFlowContext.IsDungeonLoadoutMode && DungeonFlowContext.ActiveProfile != null)
        {
            inventory.SaveDungeonFormation(DungeonFlowContext.ActiveProfile.Id, formation);
        }

        // 3. Persist standard account formation to inventory snapshot and LocalEconomyStore
        if (inventory.Snapshot != null)
        {
            while (inventory.Snapshot.formation.Count < 4) inventory.Snapshot.formation.Add(string.Empty);
            for (var i = 0; i < 4; i++)
            {
                inventory.Snapshot.formation[i] = formation[i];
            }
        }

        try
        {
            var defIds = inventory.GetDefinitionFormation(formation);
            var store = new LocalEconomyStore();
            if (store.TryLoad(out var state))
            {
                state.FormationDefinitionIds = defIds;
                store.Save(state);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed saving formation to LocalEconomyStore: " + ex.Message, this);
        }
    }

    public void OnPlayButtonClicked()
    {
        SaveRunFormation();
        var inventory = PlayerInventoryService.Instance;
        var run = DungeonFlowContext.ActiveRun;
        if (run == null) { SceneManager.LoadScene("Combat"); return; }
        if (inventory == null || characterSlots == null || characterSlots.Count != 4) return;
        try
        {
            var characters = characterSlots.Select(s => s?.AssignedCharacter).ToList();
            if (!DungeonFlowContext.ValidateFormation(DungeonFlowContext.ActiveProfile, characters, out var error))
                throw new InvalidOperationException(error);
            var selected = new List<RunFighterState>();
            var ids = new HashSet<string>();
            for (var slot = 0; slot < 4; slot++)
            {
                var character = characters[slot];
                if (character == null) continue;
                var owned = inventory.FindOwnedByDefinition(character.DefinitionId);
                if (owned == null || !ids.Add(owned.instanceId)) throw new InvalidOperationException("Choose unique owned fighters.");
                var existing = run.Roster.Find(f => f.DefinitionId == character.DefinitionId);
                if (existing != null)
                {
                    var fighter = existing.Clone();
                    fighter.OriginalFormationIndex = slot;
                    selected.Add(fighter);
                }
                else
                {
                    var definition = DungeonFlowContext.Catalog.Find(c => c.Id == character.DefinitionId && c.RuntimeReady);
                    if (definition == null) throw new InvalidOperationException("This fighter is not ready for battle.");
                    selected.Add(new RunFighterState { RunFighterId = owned.instanceId, DefinitionId = definition.Id,
                        Stats = definition.BaseStats.Clone(), CurrentHealth = definition.BaseStats.MaxHealth,
                        ConstellationTier = owned.constellationTier, OriginalFormationIndex = slot });
                }
            }
            var candidate = run.Clone();
            candidate.Roster = selected;
            if (!DungeonRunEngine.TryBuildEncounter(candidate, DungeonFlowContext.Catalog, out var encounter, out error))
                throw new InvalidOperationException(error);
            new LocalRunStateStore().Save(candidate);
            DungeonFlowContext.BeginDungeonFlowForEncounter(DungeonFlowContext.ActiveProfile, candidate, encounter);
            LocalEncounterContext.Begin(encounter, candidate.Seed ^ (ulong)candidate.Revision);
            SceneManager.LoadScene("Battle");
        }
        catch (Exception ex)
        {
            if (totalCCText != null) totalCCText.text = ex.Message;
            Debug.LogWarning(ex.Message, this);
        }
    }
}
