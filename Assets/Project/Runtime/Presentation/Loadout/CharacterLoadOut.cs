using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class LoadOutRarityUIAssets
{
    public string rarityName;
    public Sprite rarityIcon;
    public Sprite backgroundIcon;
    public Sprite frameIcon;
}

[Serializable]
public class LoadOutAttributeUIAssets
{
    public string attributeName;
    public Sprite attributeIcon;
}

[Serializable]
public class LoadOutSeriesUIAssets
{
    public string seriesId;
    public Sprite seriesIcon;
}

public class CharacterLoadOut : MonoBehaviour
{
    [Header("Selection Components")]
    [SerializeField] private GameObject selectionMarker;
    [SerializeField] private GameObject emptySlotIndicator;

    [Header("Character Card")]
    [SerializeField] private Image fighterPicture;
    [SerializeField] private Image fighterBackground;
    [SerializeField] private Image fighterRarity;
    [SerializeField] private Image fighterFrame;
    [SerializeField] private Image fighterAttribute;
    [SerializeField] private Image fighterSeries;
    [SerializeField] private Image hpIcon;
    [SerializeField] private Image atkIcon;
    [SerializeField] private Image defIcon;
    [SerializeField] private TextMeshProUGUI atkText;
    [SerializeField] private TextMeshProUGUI defText;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private TextMeshProUGUI levelText;

    [Header("Card Art")]
    [SerializeField] private LoadOutRarityUIAssets[] rarityConfigurations;
    [SerializeField] private LoadOutAttributeUIAssets[] attributeConfigurations;
    [SerializeField] private LoadOutSeriesUIAssets[] seriesConfigurations;

    private CharacterObject currentCharacter;

    private void Awake()
    {
        ResetUI();
    }

    public void ResetUI()
    {
        currentCharacter = null;
        SetImage(fighterPicture, null);
        SetImage(fighterBackground, null);
        SetImage(fighterRarity, null);
        SetImage(fighterFrame, null);
        SetImage(fighterAttribute, null);
        SetImage(fighterSeries, null);
        SetText(atkText, string.Empty);
        SetText(defText, string.Empty);
        SetText(hpText, string.Empty);
        SetText(levelText, string.Empty);
        SetIconVisible(atkIcon, false);
        SetIconVisible(defIcon, false);
        SetIconVisible(hpIcon, false);
        emptySlotIndicator?.SetActive(true);
        selectionMarker?.SetActive(false);
    }

    public void DisplayCharacterInfo(CharacterObject character)
    {
        var owned = character == null || PlayerInventoryService.Instance == null
            ? null
            : PlayerInventoryService.Instance.FindOwnedByDefinition(character.DefinitionId);
        DisplayCharacterInfo(character, owned);
    }

    public void DisplayCharacterInfo(CharacterObject character, OwnedCharacterRecord owned)
    {
        if (character == null)
        {
            ResetUI();
            return;
        }

        currentCharacter = character;
        emptySlotIndicator?.SetActive(false);
        SetImage(fighterPicture, character.FighterPic != null ? character.FighterPic : character.FighterIcon);

        var rarity = rarityConfigurations?.FirstOrDefault(config => config != null && config.rarityName == character.FighterRarity.ToString());
        SetImage(fighterRarity, rarity?.rarityIcon);
        SetImage(fighterBackground, rarity?.backgroundIcon);
        SetImage(fighterFrame, rarity?.frameIcon);

        var attribute = attributeConfigurations?.FirstOrDefault(config => config != null && config.attributeName == character.FighterAttribute.ToString());
        SetImage(fighterAttribute, attribute?.attributeIcon);

        var series = seriesConfigurations?.FirstOrDefault(config => config != null && config.seriesId == character.SeriesId);
        SetImage(fighterSeries, series?.seriesIcon);

        SetIconVisible(atkIcon, true);
        SetIconVisible(defIcon, true);
        SetIconVisible(hpIcon, true);
        SetText(atkText, Mathf.RoundToInt(character.attack).ToString("N0"));
        SetText(defText, Mathf.RoundToInt(character.defense).ToString("N0"));
        SetText(hpText, Mathf.RoundToInt(character.health).ToString("N0"));
        var level = owned == null ? 1 : Mathf.Max(1, owned.level);
        var constellation = owned == null ? 0 : Mathf.Max(0, owned.constellationTier);
        SetText(levelText, $"Lv. {level}  ·  C{constellation}");
    }

    public void ToggleSelectionVisual(bool isSelected)
    {
        selectionMarker?.SetActive(isSelected);
    }

    public void OnLoadOutClicked()
    {
        if (CharacterSelectionManager.Instance != null)
            CharacterSelectionManager.Instance.SelectCharacterLoadOut(this);
    }

    private static void SetImage(Image image, Sprite sprite)
    {
        if (image == null) return;
        image.gameObject.SetActive(sprite != null);
        image.sprite = sprite;
        image.enabled = sprite != null;
        image.color = Color.white;
    }

    private static void SetIconVisible(Image image, bool visible)
    {
        if (image == null) return;
        image.enabled = visible;
        image.color = Color.white;
    }

    private static void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null) text.text = value;
    }
}
