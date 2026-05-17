using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

[System.Serializable]
public class CharacterSlot
{
    public string SlotName;
    public CharacterLoadOut LoadOutPosition;
    public CharacterObject AssignedCharacter;

    public void AssignCharacter(CharacterObject character)
    {
        AssignedCharacter = character;
    }
}

public class CharacterSelectionManager : MonoBehaviour
{
    [Header("UI References")] [SerializeField]
    private GameObject inventoryScreen;

    [SerializeField] private GridLayoutGroup gridLayoutGroup;
    [SerializeField] private GameObject characterDisplayPrefab;
    [SerializeField] private TextMeshProUGUI totalCCText;
    [SerializeField] private CharacterLoadOut currentSelectedLoadOut;

    [Header("Character Management")] [SerializeField]
    private InventoryObject playerInventory;

    [SerializeField] private List<CharacterSlot> characterSlots;

    public static CharacterSelectionManager Instance { get; private set; }

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        InitializeUI();
    }

    private void InitializeUI()
    {
        if (inventoryScreen != null)
            inventoryScreen.SetActive(false);

        gridLayoutGroup ??= GetComponent<GridLayoutGroup>();
        PopulateCharacterGrid();
    }

    private void PopulateCharacterGrid()
    {
        // Clear existing grid
        foreach (Transform child in gridLayoutGroup.transform)
        {
            Destroy(child.gameObject);
        }

        // Populate grid with characters
        for (int i = 0; i < playerInventory.CharacterAmount; i++)
        {
            CharacterObject character = playerInventory.characterContainer[i];
            GameObject characterDisplay = Instantiate(characterDisplayPrefab, gridLayoutGroup.transform);
            UpdateGridCharacterDisplay(characterDisplay, character);
        }
    }

    private void UpdateGridCharacterDisplay(GameObject displayObject, CharacterObject character)
    {
        var characterComponent = displayObject.GetComponent<GachaChar>();
        if (characterComponent != null)
        {
            characterComponent.SetCharacter(character);
            characterComponent.UpdateCharacterUI();
        }
        else
        {
            Debug.LogError("GachaChar component not found on character display prefab!");
        }
    }

    //need to define if character is selected or unselected to show base image
    //need to know state of current baseInfo is selected or unselected
    public void Update()
    {
        //SelectingChar() when click ToggleSelectVisual open InventoryWindow. Contain variable of state "Selecting"
        //InventoryWindow() show inventory window that contain character Icon, character Icon contain Character ScriptableObject. if button will Selecting char
    }

    public void SelectCharacterLoadOut(CharacterLoadOut selectedLoadOut)
    {
        // Deselect all load outs
        characterSlots.ForEach(slot => slot.LoadOutPosition.ToggleSelectionVisual(false));

        // Select the clicked load out
        selectedLoadOut.ToggleSelectionVisual(true);
        currentSelectedLoadOut = selectedLoadOut;

        // Toggle inventory screen
        //inventoryScreen?.SetActive(!inventoryScreen.activeSelf);
        inventoryScreen.SetActive(true);
    }

    public void AssignCharacterToLoadOut(CharacterObject character)
    {
        if (currentSelectedLoadOut == null)
        {
            Debug.LogWarning("No load out selected!");
            return;
        }

        // Find the corresponding character slot
        var characterSlot = characterSlots.FirstOrDefault(slot => slot.LoadOutPosition == currentSelectedLoadOut);
        if (characterSlot != null)
        {
            characterSlot.AssignCharacter(character);
            currentSelectedLoadOut.DisplayCharacterInfo(character);

            UpdateTotalTeamCC();
        }
    }

    private void UpdateTotalTeamCC()
    {
        float totalCC = characterSlots
            .Where(slot => slot.AssignedCharacter != null)
            .Sum(slot => slot.AssignedCharacter.Classpower);

        totalCCText.text = $"Total Team CC: {totalCC:N0}";
    }

    private CharacterSlot GetSlotByPosition(CharacterLoadOut position)
    {
        foreach (CharacterSlot slot in characterSlots)
        {
            if (slot.LoadOutPosition == position)
            {
                return slot;
            }
        }

        return null;
    }

    public void DeselectAll()
    {
        foreach (CharacterSlot slot in characterSlots)
        {
            slot.LoadOutPosition.ToggleSelectionVisual(false);
        }
    }
    
    public void OnPlayButtonClicked()
    {
        // Save current team setup before loading battle scene
        TeamDataManager.Instance.SaveTeamSetup(characterSlots.ToList());
            
        // Load battle scene
        SceneManager.LoadScene("Scene-BattleGame");
    }
}
