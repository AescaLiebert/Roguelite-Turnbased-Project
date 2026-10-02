using System.Collections.Generic;
using System.Linq;
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

        foreach (var character in inventory.GetOwnedDefinitions())
        {
            if (character == null) continue;
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

        try
        {
            inventory.SaveFormation(formation);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("Could not save this formation: " + exception.Message, this);
            return;
        }

        RestoreSavedFormation();
        UpdateTotalTeamCC();
        CloseInventory();
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
        PlayerInventoryService.Instance.SaveFormation(formation);
        RestoreSavedFormation();
        UpdateTotalTeamCC();
        CloseInventory();
    }

    // Kept as a compatibility entry point for older scene button bindings.
    public void ClearFighterInfo() => ClearCurrentSelectedSlot();

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
        totalCCText.text = $"Total Team CC: {total:N0}";
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
