using UnityEngine;
using System;
using UnityEngine.UI;
using System.Linq;

[Serializable]
public class RarityUIAssets
{
    public string rarityName;
    public Sprite rarityIcon;
    public Sprite backgroundIcon;
    public Sprite frameIcon;
}

[System.Serializable]
public class AttributeUIAssets
{
    public string attributeName;
    public Sprite attributeIcon;
}

[System.Serializable]
public class StarUIAssets
{
    public int starLevel;
    public Sprite starIcon;
    public Sprite starCut;
}

public class GachaChar : MonoBehaviour
{
    [Header("Character References")]
    [SerializeField] private CharacterObject character;
    public CharacterObject _character
    {
        get { return character; }
        set { character = value; }
    }
        
    [Header("UI Components")]
    [SerializeField] private Image charIcon;
    [SerializeField] private Image rarityIcon;
    [SerializeField] private Image attributeIcon;
    [SerializeField] private Image backgroundIcon;
    [SerializeField] private Image frameIcon;
        
    [Header("UI Configurations")]
    [SerializeField] private RarityUIAssets[] rarityConfigurations;
    [SerializeField] private AttributeUIAssets[] attributeConfigurations;

    [SerializeField] private InventoryObject playerInventory;
    
    public void Start()
    {
        if (character == null || (charIcon == null && (charIcon = GetComponent<Image>()) == null) || character.FighterIcon == null)
        {
            Debug.LogError("Required components are missing!");
            return;
        }
    }
    
    private void Awake()
    {
        ValidateComponents();
    }
    
    private void ValidateComponents()
    {
        // Null checks and component validation
        if (charIcon == null) 
            Debug.LogWarning("Character Icon is not assigned!");
            
        if (playerInventory == null)
            Debug.LogWarning("Player Inventory is not assigned!");
    }


    public CharacterObject GetCharacter(CharacterObject characterObject)
    {
        this.character = characterObject;
        return characterObject;
    }
    
    public void SetCharacter(CharacterObject newCharacter)
    {
        character = newCharacter;
        UpdateCharacterUI();
    }
    
    
    //Add Char to Player Inventory
    public void Reward(string rarity)
    {
        if (rarity == "SSR")
        {
            Debug.Log($"Received character {character.FighterName} of rarity: {rarity}");
        }
        
        if (character != null && PlayerInventoryService.Instance != null)
        {
            try
            {
                var owned = PlayerInventoryService.Instance.GrantCharacter(character);
                Debug.Log("Added " + character.DefinitionId + " to account inventory at C" + owned.constellationTier + ".", this);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to add character to inventory: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("Player inventory service or summoned character is unavailable.", this);
        }

        UpdateCharacterUI();
    }
    
    //ไว้เเก้ตอนทำระบบ Upgrade System ให้ Update ตามที่ Upgrade เเละต้องทำ Default Upgrade Rarity สำหรับตอนสุ่ม Character ด้วย
    public void UpdateCharacterUI()
    {
        if (character == null) return;

        // Find UI configurations
        var rarityConfig = rarityConfigurations.FirstOrDefault(
            config => config.rarityName == character.FighterRarity.ToString());
            
        var attributeConfig = attributeConfigurations.FirstOrDefault(
            config => config.attributeName == character.FighterAttribute.ToString());

        // Update UI elements
        UpdateUIElement(charIcon, character.FighterIcon);
            
        if (rarityConfig != null)
        {
            UpdateUIElement(rarityIcon, rarityConfig.rarityIcon);
            UpdateUIElement(backgroundIcon, rarityConfig.backgroundIcon);
            UpdateUIElement(frameIcon, rarityConfig.frameIcon);
        }

        if (attributeConfig != null)
        {
            UpdateUIElement(attributeIcon, attributeConfig.attributeIcon);
        }
    }
    
    private void UpdateUIElement(Image imageComponent, Sprite newSprite)
    {
        if (imageComponent != null && newSprite != null)
        {
            imageComponent.sprite = newSprite;
            imageComponent.color = Color.white;
        }
    }

    public void OnCharacterClicked()
    {
        if (character == null)
        {
            Debug.LogWarning("No character selected!");
            return;
        }

        //Debug.Log($"Character clicked: {character.FighterName}");
        CharacterSelectionManager.Instance.AssignCharacterToLoadOut(character);
    }
}
