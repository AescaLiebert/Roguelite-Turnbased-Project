 using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Linq;

[System.Serializable]
public class LoadOutRarityUIAssets
{
    public string rarityName;
    public Sprite rarityIcon;
    public Sprite backgroundIcon;
    public Sprite frameIcon;
}

[System.Serializable]
public class LoadOutAttributeUIAssets
{
    public string attributeName;
    public Sprite attributeIcon;
}

[System.Serializable]
public class LoadOutStarUIAssets
{
    public int starLevel;
    public Sprite starIcon;
    public Sprite starCut;
}
public class CharacterLoadOut : MonoBehaviour
{
    [Header("Selection Components")]
    [SerializeField] private GameObject selectionMarker;
    [SerializeField] private GameObject emptySlotIndicator;

    [Header("UI Components")]
    [SerializeField] private Image fighterPicture;
    [SerializeField] private Image fighterBackground;
    [SerializeField] private Image fighterRarity;
    [SerializeField] private Image fighterFrame;
    [SerializeField] private Image fighterAttribute;
        
    [SerializeField] private Image hpIcon, atkIcon, defIcon;
    [SerializeField] private TextMeshProUGUI atkText, defText, hpText, levelText;

    [Header("UI Configurations")]
    [SerializeField] private RarityUIAssets[] rarityConfigurations;
    [SerializeField] private AttributeUIAssets[] attributeConfigurations;

    private CharacterObject currentCharacter;
    public static CharacterLoadOut Instance { get; private set; }
    
    //ต้อง Save Previous Fighter ไม่ใช่ Remove ทิ้ง
    private void Awake()
    {
        // Singleton-like pattern for instance access
        if (Instance == null)
            Instance = this;

        ResetUI();
    }
    
    private void ResetUI()
    {
        // Reset all UI elements to their default state
        SetUIElementState(fighterPicture, null);
        SetUIElementState(fighterBackground, null);
        SetUIElementState(fighterRarity, null);
        SetUIElementState(fighterFrame, null);
        SetUIElementState(fighterAttribute, null);


        atkIcon.color = Color.clear;
        defIcon.color = Color.clear;
        hpIcon.color = Color.clear;
        
        
        SetTextState(atkText, "");
        SetTextState(defText, "");
        SetTextState(hpText, "");
        SetTextState(levelText, "");

        emptySlotIndicator?.SetActive(true);
    }
    
    private void SetUIElementState(Image imageComponent, Sprite sprite)
    {
        if (imageComponent != null)
        {
            imageComponent.sprite = sprite;
            imageComponent.color = sprite == null ? Color.clear : Color.white;
        }
    }
    
    private void SetTextState(TextMeshProUGUI textComponent, string content)
    {
        if (textComponent != null)
            textComponent.text = content;
    }

    public void DisplayCharacterInfo(CharacterObject character)
    {
        if (character == null)
        {
            ResetUI();
            return;
        }

        currentCharacter = character;
        emptySlotIndicator?.SetActive(false);

        // Find UI configurations
        var rarityConfig = rarityConfigurations.FirstOrDefault(
            config => config.rarityName == character.FighterRarity.ToString());
            
        var attributeConfig = attributeConfigurations.FirstOrDefault(
            config => config.attributeName == character.FighterAttribute.ToString());

        // Update UI elements
        SetUIElementState(fighterPicture, character.FighterPic);
            
        if (rarityConfig != null)
        {
            SetUIElementState(fighterRarity, rarityConfig.rarityIcon);
            SetUIElementState(fighterBackground, rarityConfig.backgroundIcon);
            SetUIElementState(fighterFrame, rarityConfig.frameIcon);
        }

        if (attributeConfig != null)
        {
            SetUIElementState(fighterAttribute, attributeConfig.attributeIcon);
        }
        
        atkIcon.color = Color.white;
        defIcon.color = Color.white;
        hpIcon.color = Color.white;

        // Update stat texts
        SetTextState(atkText, character.attack.ToString());
        SetTextState(defText, character.defense.ToString());
        SetTextState(hpText, character.health.ToString());
    }
    
    public void ToggleSelectionVisual(bool isSelected)
    {
        selectionMarker?.SetActive(isSelected);
    }

    public void OnLoadOutClicked()
    {
        CharacterSelectionManager.Instance.SelectCharacterLoadOut(this);
    }
    
}



