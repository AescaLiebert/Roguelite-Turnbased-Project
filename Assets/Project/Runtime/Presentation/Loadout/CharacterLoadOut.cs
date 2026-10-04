using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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

public class CharacterLoadOut : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
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

    [Header("Gesture Settings")]
    [SerializeField, Min(0.1f)] private float holdDuration = 0.25f;

    private CharacterObject currentCharacter;
    public CharacterObject CurrentCharacter => currentCharacter;

    private bool _pointerDown;
    private bool _isHolding;
    private bool _isDragging;
    private Coroutine _holdRoutine;
    private GameObject _dragProxy;
    private CanvasGroup _dragCanvasGroup;
    private bool _originalBlocksRaycasts;
    private bool _dropHandled;
    private Vector3 _originalScale = Vector3.one;
    private CharacterLoadOut _currentHoverTarget;

    private void Awake()
    {
        _originalScale = transform.localScale;
        ResetUI();
    }

    private void OnDisable()
    {
        _pointerDown = false;
        _isHolding = false;
        _isDragging = false;
        if (_holdRoutine != null) { StopCoroutine(_holdRoutine); _holdRoutine = null; }
        if (_dragProxy != null) { Destroy(_dragProxy); _dragProxy = null; }
        if (_currentHoverTarget != null) { _currentHoverTarget.ToggleSelectionVisual(false); _currentHoverTarget = null; }
        if (_dragCanvasGroup != null)
        {
            _dragCanvasGroup.blocksRaycasts = _originalBlocksRaycasts;
            _dragCanvasGroup = null;
        }
        transform.localScale = _originalScale == Vector3.zero ? Vector3.one : _originalScale;
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

    public void OnPointerDown(PointerEventData eventData)
    {
        _pointerDown = true;
        _isHolding = false;
        _isDragging = false;
        if (_holdRoutine != null) StopCoroutine(_holdRoutine);
        if (currentCharacter != null)
        {
            _holdRoutine = StartCoroutine(DetectHold());
        }
    }

    private IEnumerator DetectHold()
    {
        yield return new WaitForSecondsRealtime(holdDuration);
        if (!_pointerDown) yield break;
        _isHolding = true;
        transform.localScale = _originalScale * 1.05f;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (currentCharacter == null) return;
        if (_holdRoutine != null) { StopCoroutine(_holdRoutine); _holdRoutine = null; }
        _isHolding = true;
        _isDragging = true;
        _dropHandled = false;
        _dragCanvasGroup = GetComponent<CanvasGroup>();
        if (_dragCanvasGroup == null) _dragCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        _originalBlocksRaycasts = _dragCanvasGroup.blocksRaycasts;
        _dragCanvasGroup.blocksRaycasts = false;
        CreateDragProxy(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;
        if (_dragProxy != null)
        {
            _dragProxy.transform.position = eventData.position;
        }

        var target = eventData.pointerCurrentRaycast.gameObject != null
            ? eventData.pointerCurrentRaycast.gameObject.GetComponentInParent<CharacterLoadOut>()
            : null;

        if (target != _currentHoverTarget)
        {
            if (_currentHoverTarget != null && _currentHoverTarget != this)
            {
                _currentHoverTarget.ToggleSelectionVisual(false);
            }
            _currentHoverTarget = (target != null && target != this) ? target : null;
            if (_currentHoverTarget != null)
            {
                _currentHoverTarget.ToggleSelectionVisual(true);
            }
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag != null)
        {
            var source = eventData.pointerDrag.GetComponentInParent<CharacterLoadOut>();
            if (source != null && source != this)
            {
                if (CharacterSelectionManager.Instance != null)
                {
                    CharacterSelectionManager.Instance.SwapSlots(source, this);
                    source._dropHandled = true;
                }
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dropHandled && _currentHoverTarget != null && _currentHoverTarget != this &&
            CharacterSelectionManager.Instance != null)
        {
            CharacterSelectionManager.Instance.SwapSlots(this, _currentHoverTarget);
            _dropHandled = true;
        }
        CleanupDragVisuals();
        _isDragging = false;
        StartCoroutine(ClearHoldAtEndOfFrame());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _pointerDown = false;
        if (_holdRoutine != null) { StopCoroutine(_holdRoutine); _holdRoutine = null; }

        if (!_isHolding && !_isDragging)
        {
            // Quick tap: Select slot to open character selection
            OnLoadOutClicked();
        }
        else
        {
            CleanupDragVisuals();
            StartCoroutine(ClearHoldAtEndOfFrame());
        }
    }

    private void CreateDragProxy(Vector2 screenPos)
    {
        var rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null || currentCharacter == null) return;

        if (_dragProxy != null) Destroy(_dragProxy);

        _dragProxy = new GameObject("LoadOutDragProxy", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        _dragProxy.transform.SetParent(rootCanvas.transform, false);
        _dragProxy.transform.SetAsLastSibling();

        var rect = _dragProxy.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(160, 200);

        var img = _dragProxy.GetComponent<Image>();
        img.sprite = currentCharacter.FighterPic != null ? currentCharacter.FighterPic : currentCharacter.FighterIcon;
        img.preserveAspect = true;
        img.raycastTarget = false;

        var group = _dragProxy.GetComponent<CanvasGroup>();
        group.alpha = 0.85f;
        group.blocksRaycasts = false;

        _dragProxy.transform.position = screenPos;
    }

    private void CleanupDragVisuals()
    {
        if (_currentHoverTarget != null)
        {
            _currentHoverTarget.ToggleSelectionVisual(false);
            _currentHoverTarget = null;
        }
        if (_dragProxy != null)
        {
            Destroy(_dragProxy);
            _dragProxy = null;
        }
        if (_dragCanvasGroup != null)
        {
            _dragCanvasGroup.blocksRaycasts = _originalBlocksRaycasts;
            _dragCanvasGroup = null;
        }
        transform.localScale = _originalScale == Vector3.zero ? Vector3.one : _originalScale;
    }

    private IEnumerator ClearHoldAtEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        _isHolding = false;
        _isDragging = false;
    }

    public void OnLoadOutClicked()
    {
        if (_isHolding || _isDragging) return;
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
