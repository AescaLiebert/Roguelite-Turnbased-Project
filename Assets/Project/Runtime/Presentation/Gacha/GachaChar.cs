using UnityEngine;
using System;
using UnityEngine.UI;
using System.Linq;
using TMPro;

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
    [SerializeField] private bool isEmptySlotCommand;
    
    public void Start()
    {
        if (isEmptySlotCommand) return;
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

    public void SetAsEmptySlotCommand()
    {
        isEmptySlotCommand = true;
        character = null;

        if (rarityIcon != null) rarityIcon.gameObject.SetActive(false);
        if (attributeIcon != null) attributeIcon.gameObject.SetActive(false);
        if (charIcon != null) charIcon.gameObject.SetActive(false);
        if (backgroundIcon != null)
        {
            backgroundIcon.gameObject.SetActive(true);
            backgroundIcon.color = new Color(0.18f, 0.2f, 0.25f, 0.95f);
        }
        if (frameIcon != null)
        {
            frameIcon.gameObject.SetActive(true);
            frameIcon.color = new Color(0.7f, 0.7f, 0.75f, 0.8f);
        }

        var label = GetComponentInChildren<TextMeshProUGUI>();
        if (label == null)
        {
            var textObj = new GameObject("EmptyCommandLabel", typeof(RectTransform));
            textObj.transform.SetParent(transform, false);
            var rect = textObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            label = textObj.AddComponent<TextMeshProUGUI>();
        }

        if (label != null)
        {
            label.gameObject.SetActive(true);
            label.text = "EMPTY\nSLOT";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 24;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.95f, 0.45f, 0.45f, 1f);
        }
    }

    public void OnCharacterClicked()
    {
        if (isEmptySlotCommand)
        {
            if (CharacterSelectionManager.Instance != null)
                CharacterSelectionManager.Instance.ClearCurrentSelectedSlot();
            return;
        }

        if (character == null)
        {
            Debug.LogWarning("No character selected!");
            return;
        }

        //Debug.Log($"Character clicked: {character.FighterName}");
        CharacterSelectionManager.Instance.AssignCharacterToLoadOut(character);
    }
}
