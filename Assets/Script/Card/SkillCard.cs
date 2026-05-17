using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System;

public class SkillCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("Data")]
    [SerializeField] private RuntimeCard runtimeCard;
    public RuntimeCard GetRuntimeCard() => runtimeCard;

    [Header("UI Components")]
    [SerializeField] private Image cardIcon;
    [SerializeField] private Image cardFrame; // Can be used as the target for rank sprites if intended, or us separate rankImage
    [SerializeField] private Image rankImage; // The Image component that will display the rank sprite
    [SerializeField] private Image cardTypeIcon;
    [SerializeField] private TextMeshProUGUI cardNameText; // Optional but good to have

    [Header("Rank Configurations")]
    [Tooltip("Visuals for Rank 1 (e.g., 1 Star)")]
    [SerializeField] private Sprite rank1bject;
    
    [Tooltip("Visuals for Rank 2 (e.g., 2 Stars)")]
    [SerializeField] private Sprite rank2bject;
    
    [Tooltip("Visuals for Rank 3 (e.g., 3 Stars)")]
    [SerializeField] private Sprite rank3bject;

    [Header("Type Icons Configuration")]
    // You might want a ScriptableObject for this later, but for now lists/arrays work or just assigning in inspector if static
    [SerializeField] private Sprite attackTypeSprite;
    [SerializeField] private Sprite buffTypeSprite;
    [SerializeField] private Sprite debuffTypeSprite;
    [SerializeField] private Sprite stanceTypeSprite;
    [SerializeField] private Sprite healTypeSprite;
    [SerializeField] private Sprite debuffatkTypeSprite;

    // Events
    public Action<SkillCard> OnCardClicked;
    public Action<SkillCard, int> OnCardDragEnd; // Card, NewIndex

    // Drag State
    private Transform parentToReturnTo = null;
    private Transform placeholderParent = null;
    private GameObject placeholder = null;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Setup(RuntimeCard card)
    {
        this.runtimeCard = card;
        RefreshVisuals();
    }

    public void RefreshVisuals()
    {
        if (runtimeCard == null || runtimeCard.cardData == null) return;

        // 1. Set Icon
        if (cardIcon != null)
        {
            cardIcon.sprite = runtimeCard.cardData.cardIcon;
        }

        // 2. Set Name (Removed as per user request to comment out)
        /*
        if (cardNameText != null)
        {
            cardNameText.text = runtimeCard.cardData.cardName;
        }
        */

        // 3. Set Rank Visuals
        UpdateRankVisuals(runtimeCard.rank);

        // 4. Set Type Icon
        CardRankData rankData = runtimeCard.cardData.GetRankData(runtimeCard.rank);
        if (rankData != null)
        {
            SetTypeIcon(rankData.skillType);
        }
    }

    private void UpdateRankVisuals(int rank)
    {
        // Require a target image to set the sprite on
        if (rankImage == null) return;

        Sprite targetSprite = null;

        switch (rank)
        {
            case 1:
                targetSprite = rank1bject;
                break;
            case 2:
                targetSprite = rank2bject;
                break;
            case 3:
                targetSprite = rank3bject;
                break;
        }

        if (targetSprite != null)
        {
            rankImage.sprite = targetSprite;
            rankImage.gameObject.SetActive(true);
        }
        else
        {
            rankImage.gameObject.SetActive(false);
        }
    }

    private void SetTypeIcon(SkillType type)
    {
        if (cardTypeIcon == null) return;

        Sprite targetSprite = null;
        switch (type)
        {
            case SkillType.Attack: targetSprite = attackTypeSprite; break;
            case SkillType.Buff: targetSprite = buffTypeSprite; break;
            case SkillType.Debuff: targetSprite = debuffTypeSprite; break;
            case SkillType.Stance: targetSprite = stanceTypeSprite; break;
            case SkillType.Heal: targetSprite = healTypeSprite; break;
            case SkillType.DebuffAtk: targetSprite = debuffatkTypeSprite; break;
        }

        if (targetSprite != null)
        {
            cardTypeIcon.sprite = targetSprite;
            cardTypeIcon.gameObject.SetActive(true);
        }
        else
        {
            // If we don't have a sprite, maybe hide the icon or keep default
             cardTypeIcon.gameObject.SetActive(false);
        }
    }

    #region Drag & Drop Implementation

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Create Placeholder
        placeholder = new GameObject("Checklist_Placeholder");
        placeholder.transform.SetParent(this.transform.parent);
        LayoutElement le = placeholder.AddComponent<LayoutElement>();
        le.preferredWidth = this.GetComponent<LayoutElement>() ? this.GetComponent<LayoutElement>().preferredWidth : 100;
        le.preferredHeight = this.GetComponent<LayoutElement>() ? this.GetComponent<LayoutElement>().preferredHeight : 150;
        le.flexibleWidth = 0;
        le.flexibleHeight = 0;

        placeholder.transform.SetSiblingIndex(this.transform.GetSiblingIndex());

        parentToReturnTo = this.transform.parent;
        placeholderParent = parentToReturnTo;
        
        // Move card to root canvas so it draws over everything
        // Assumption: Parent's parent's parent... find logic or just set to root for now
        this.transform.SetParent(this.transform.parent.parent.parent); // Hacky safe bet: Hand -> HandPanel -> Canvas

        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        this.transform.position = eventData.position;

        if (placeholderParent == null) return;
        
        int newSiblingIndex = placeholderParent.childCount;

        for (int i = 0; i < placeholderParent.childCount; i++)
        {
            if (this.transform.position.x < placeholderParent.GetChild(i).position.x)
            {
                newSiblingIndex = i;
                if (placeholder.transform.GetSiblingIndex() < newSiblingIndex)
                    newSiblingIndex--;
                break;
            }
        }

        placeholder.transform.SetSiblingIndex(newSiblingIndex);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        this.transform.SetParent(parentToReturnTo);
        
        if (placeholder != null)
        {
            this.transform.SetSiblingIndex(placeholder.transform.GetSiblingIndex());
            Destroy(placeholder);
        }

        canvasGroup.blocksRaycasts = true;

        // Notify Reorder complete
        OnCardDragEnd?.Invoke(this, this.transform.GetSiblingIndex());
    }

    #endregion

    #region Click Implementation

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.dragging) return;
        
        OnCardClicked?.Invoke(this);
    }

    #endregion
}
