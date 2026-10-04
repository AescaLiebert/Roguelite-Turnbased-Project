using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Adapters;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;
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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitializeUI();
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

    private void RestoreSavedFormation()
    {
        if (characterSlots == null) return;
        foreach (var slot in characterSlots)
        {
            if (slot == null) continue;
            slot.AssignCharacter(null);
            slot.LoadOutPosition?.ResetUI();
        }

        var inventory = PlayerInventoryService.Instance;
        if (inventory == null) return;

        var profile = DungeonFlowContext.IsDungeonLoadoutMode ? DungeonFlowContext.ActiveProfile : null;

        if (profile != null)
        {
            var dungeonFormation = inventory.GetDungeonFormation(profile.Id);
            var definitions = inventory.GetOwnedDefinitions();
            var used = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < characterSlots.Count; i++)
            {
                var instanceId = i < dungeonFormation.Count ? dungeonFormation[i] : string.Empty;
                var owned = string.IsNullOrEmpty(instanceId) ? null : inventory.FindOwnedByInstance(instanceId);
                var character = owned == null ? null : definitions.FirstOrDefault(c => c != null && c.DefinitionId == owned.definitionId);
                if (character != null && DungeonFlowContext.IsCharacterEligible(profile, character))
                {
                    characterSlots[i]?.AssignCharacter(character);
                    characterSlots[i]?.LoadOutPosition?.DisplayCharacterInfo(character, owned);
                    used.Add(character.DefinitionId);
                }
            }

            // Auto-format: if active slots are empty, auto-fill with eligible characters from roster
            for (var i = 0; i < characterSlots.Count; i++)
            {
                if (characterSlots[i]?.AssignedCharacter != null) continue;
                var candidate = definitions.FirstOrDefault(c => c != null && !used.Contains(c.DefinitionId) && DungeonFlowContext.IsCharacterEligible(profile, c));
                if (candidate != null)
                {
                    var owned = inventory.FindOwnedByDefinition(candidate.DefinitionId);
                    characterSlots[i]?.AssignCharacter(candidate);
                    characterSlots[i]?.LoadOutPosition?.DisplayCharacterInfo(candidate, owned);
                    used.Add(candidate.DefinitionId);
                }
            }
        }
        else
        {
            foreach (var selection in inventory.GetPlayerFormation())
            {
                if (selection.slotPosition < 0 || selection.slotPosition >= characterSlots.Count || selection.character == null) continue;

                var slot = characterSlots[selection.slotPosition];
                if (slot == null || slot.LoadOutPosition == null) continue;
                slot.AssignCharacter(selection.character);
                slot.LoadOutPosition.DisplayCharacterInfo(selection.character,
                    inventory.FindOwnedByInstance(selection.ownedFighterId));
            }
        }
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

        if (DungeonFlowContext.IsDungeonLoadoutMode)
        {
            var profile = DungeonFlowContext.ActiveProfile;
            if (profile != null)
            {
                inventory.SaveDungeonFormation(profile.Id, formation);
            }
        }
        else
        {
            try
            {
                inventory.SaveFormation(formation);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not save this formation: " + exception.Message, this);
                return;
            }
        }

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

        if (DungeonFlowContext.IsDungeonLoadoutMode)
        {
            var profile = DungeonFlowContext.ActiveProfile;
            if (profile != null)
            {
                inventory.SaveDungeonFormation(profile.Id, formation);
            }
        }
        else
        {
            try
            {
                inventory.SaveFormation(formation);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not save swapped formation: " + exception.Message, this);
                return;
            }
        }

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
        if (DungeonFlowContext.IsDungeonLoadoutMode)
        {
            var profile = DungeonFlowContext.ActiveProfile;
            if (profile != null)
            {
                inventory.SaveDungeonFormation(profile.Id, formation);
            }
        }
        else
        {
            inventory.SaveFormation(formation);
        }
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

    public void OnPlayButtonClicked()
    {
        var inventory = PlayerInventoryService.Instance;
        if (inventory == null)
        {
            Debug.LogWarning("Player inventory is unavailable; cannot save the selected formation.", this);
            return;
        }
        if (characterSlots == null || characterSlots.Count != 4)
        {
            Debug.LogError("Character Loadout needs four configured positions: three active slots and an optional reserve slot.", this);
            return;
        }

        var characters = characterSlots.Select(s => s?.AssignedCharacter).ToList();

        if (DungeonFlowContext.IsDungeonLoadoutMode)
        {
            var profile = DungeonFlowContext.ActiveProfile;
            if (!DungeonFlowContext.ValidateFormation(profile, characters, out var error))
            {
                Debug.LogWarning(error, this);
                if (totalCCText != null) totalCCText.text = error;
                return;
            }

            foreach (var slot in characterSlots)
            {
                var character = slot?.AssignedCharacter;
                if (character == null) continue;
                var owned = inventory.FindOwnedByDefinition(character.DefinitionId);
                if (owned == null)
                {
                    Debug.LogWarning("The selected formation contains a character that is not owned by this account.", this);
                    return;
                }
            }

            // Auto-save the formation specifically for this dungeon profile
            var dungeonFormation = new List<string>(4);
            foreach (var slot in characterSlots)
            {
                var charObj = slot?.AssignedCharacter;
                var owned = charObj == null ? null : inventory.FindOwnedByDefinition(charObj.DefinitionId);
                dungeonFormation.Add(owned == null ? string.Empty : owned.instanceId);
            }
            inventory.SaveDungeonFormation(profile.Id, dungeonFormation);

            if (DungeonFlowContext.ActiveRun != null)
            {
                try
                {
                    var run = DungeonFlowContext.ActiveRun;
                    for (var slot = 0; slot < 4; slot++)
                    {
                        var character = characterSlots[slot]?.AssignedCharacter;
                        if (character == null) continue;
                        var owned = inventory.FindOwnedByDefinition(character.DefinitionId);
                        var existing = run.Roster.Find(f => f.DefinitionId == character.DefinitionId);
                        if (existing != null)
                        {
                            existing.OriginalFormationIndex = slot;
                            if (existing.CurrentHealth <= 0) existing.CurrentHealth = existing.Stats.MaxHealth;
                            existing.IsDefeated = false;
                        }
                        else
                        {
                            var stat = new StatBlock
                            {
                                Attack = Mathf.RoundToInt(character.attack),
                                Defense = Mathf.RoundToInt(character.defense),
                                MaxHealth = Mathf.RoundToInt(character.health),
                                CombatClass = Mathf.RoundToInt(character.Classpower)
                            };
                            var def = new CharacterDefinition
                            {
                                Id = character.DefinitionId,
                                RuntimeReady = true,
                                BaseStats = stat.Clone(),
                                AttributeId = "attribute." + character.FighterAttribute.ToString().ToLowerInvariant(),
                                TraitIds = character.TraitIds == null ? new List<string>() : new List<string>(character.TraitIds),
                                Passive = FightingAllstar.Core.Content.StandardCharacterPassives.Create(character.DefinitionId)
                            };
                            run.Roster.Add(new RunFighterState
                            {
                                RunFighterId = run.RunId + ":hero:" + character.DefinitionId.Replace("fighter.", string.Empty),
                                DefinitionId = character.DefinitionId,
                                Definition = def,
                                Stats = stat,
                                ConstellationTier = owned == null ? 0 : owned.constellationTier,
                                OriginalFormationIndex = slot,
                                CurrentHealth = stat.MaxHealth,
                                IsDefeated = false
                            });
                        }
                    }

                    new LocalRunStateStore().Save(run);

                    if (DungeonRunEngine.TryBuildEncounter(run, out var encounter, out var encError))
                    {
                        LocalEncounterContext.Begin(encounter, run.Seed ^ (ulong)run.Revision);
                        SceneManager.LoadScene("Battle");
                        return;
                    }
                    else
                    {
                        Debug.LogWarning("Could not build encounter: " + encError, this);
                        if (totalCCText != null) totalCCText.text = encError;
                        return;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Could not enter dungeon battle: " + exception.Message, this);
                    if (totalCCText != null) totalCCText.text = exception.Message;
                    return;
                }
            }

            // Build RunFighterSeed roster
            var fighters = new List<RunFighterSeed>();
            for (var slot = 0; slot < 4; slot++)
            {
                var character = characterSlots[slot]?.AssignedCharacter;
                if (character == null) continue;
                var owned = inventory.FindOwnedByDefinition(character.DefinitionId);
                var definition = DungeonFlowContext.Catalog?.Find(c => c != null && c.Id == character.DefinitionId && c.RuntimeReady);
                if (definition == null && DungeonFlowContext.Catalog != null)
                {
                    definition = new CharacterDefinition
                    {
                        Id = character.DefinitionId,
                        RuntimeReady = true,
                        BaseStats = new StatBlock
                        {
                            Attack = Mathf.RoundToInt(character.attack),
                            Defense = Mathf.RoundToInt(character.defense),
                            MaxHealth = Mathf.RoundToInt(character.health),
                            CombatClass = Mathf.RoundToInt(character.Classpower)
                        },
                        AttributeId = "attribute." + character.FighterAttribute.ToString().ToLowerInvariant(),
                        TraitIds = character.TraitIds == null ? new List<string>() : new List<string>(character.TraitIds),
                        Passive = FightingAllstar.Core.Content.StandardCharacterPassives.Create(character.DefinitionId)
                    };
                }
                else if (definition != null && (definition.Passive == null || string.IsNullOrWhiteSpace(definition.Passive.Id)))
                {
                    definition.Passive = FightingAllstar.Core.Content.StandardCharacterPassives.Create(definition.Id);
                }

                if (definition != null)
                {
                    fighters.Add(new RunFighterSeed
                    {
                        OwnedFighterId = owned.instanceId,
                        Definition = definition.Clone(),
                        ResolvedStats = definition.BaseStats?.Clone(),
                        ConstellationTier = owned.constellationTier,
                        FormationSlot = slot,
                        IsReserve = slot == 3
                    });
                }
            }

            try
            {
                var run = DungeonRunEngine.CreateRun(Guid.NewGuid().ToString("N"), DungeonFlowContext.UserId,
                    Guid.NewGuid().ToString("N"), profile, DungeonFlowContext.SelectedDifficulty,
                    DungeonFlowContext.Seed, DungeonFlowContext.ContentVersion, DungeonFlowContext.ContentHash,
                    fighters, DungeonFlowContext.Catalog, DungeonFlowContext.BaseCompletionDiamonds);

                // Auto-advance start node into first battle node
                var firstBattleNode = run.Nodes.Find(n => n.Row == 1 && (n.Type == RouteNodeType.Battle || n.Type == RouteNodeType.Elite));
                if (firstBattleNode != null)
                {
                    if (DungeonRunEngine.TryChooseNode(run, firstBattleNode.Id, run.Revision, run.RunId + ":start:battle:1", out var activeRun, out _))
                    {
                        run = activeRun;
                    }
                }

                new LocalRunStateStore().Save(run);

                if (DungeonRunEngine.TryBuildEncounter(run, out var encounter, out var encError))
                {
                    LocalEncounterContext.Begin(encounter, run.Seed ^ (ulong)run.Revision);
                    SceneManager.LoadScene("Battle");
                    return;
                }
                else
                {
                    Debug.LogWarning("Could not build initial encounter: " + encError, this);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not start dungeon run: " + exception.Message, this);
                if (totalCCText != null) totalCCText.text = exception.Message;
                return;
            }
        }
        else
        {
            if (characterSlots.Take(3).All(slot => slot == null || slot.AssignedCharacter == null))
            {
                Debug.LogWarning("Assign at least one fighter to an active position before entering Battle.", this);
                return;
            }

            var formation = new List<string>(4);
            foreach (var slot in characterSlots)
            {
                var character = slot?.AssignedCharacter;
                if (character == null)
                {
                    formation.Add(string.Empty);
                    continue;
                }

                var owned = inventory.FindOwnedByDefinition(character.DefinitionId);
                if (owned == null)
                {
                    Debug.LogWarning("The selected formation contains a character that is not owned by this account.", this);
                    return;
                }
                formation.Add(owned.instanceId);
            }

            inventory.SaveFormation(formation);
            SceneManager.LoadScene("Combat");
        }
    }
}
