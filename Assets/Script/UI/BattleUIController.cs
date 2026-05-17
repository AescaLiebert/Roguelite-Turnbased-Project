using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class BattleUIController : MonoBehaviour
{
    [Header("Combat Power UI")]
    [SerializeField] private GameObject combatPowerPanel;
    [SerializeField] private TextMeshProUGUI playerCCValueText;
    [SerializeField] private TextMeshProUGUI enemyCCValueText;
    [SerializeField] private TextMeshProUGUI turnDecisionText;

    [Header("Animation Settings")]
    [SerializeField] private float countDuration = 1.0f;

    [Header("Unit Icons")]
    [SerializeField] private Image player1Icon;
    [SerializeField] private Image enemy1Icon;

    [Header("Card Hand UI")]
    [SerializeField] private Transform handContainer;
    [SerializeField] private Transform actionSlotContainer; // NEW: Container for used cards
    [SerializeField] private GameObject skillCardPrefab;

    public void RefreshHandUI()
    {
        if (CardDeckManager.Instance == null)
        {
            Debug.LogError("[BattleUIController] CardDeckManager Instance is null!");
            return;
        }

        if (handContainer == null || skillCardPrefab == null)
        {
             Debug.LogWarning("[BattleUIController] HandContainer or SkillCardPrefab is missing!");
             return;
        }

        // Clear existing cards
        foreach (Transform child in handContainer)
        {
            Destroy(child.gameObject);
        }
        
        // Also clear Action Slots if needed or handle separately? 
        // For now, let's assume we are refreshing HAND only, but we should probably ensure action slots are clean or synced.
        // Let's just focus on spawning the HAND cards.

        // Instantiate new cards
        var hand = CardDeckManager.Instance.GetHand();
        foreach (var card in hand)
        {
            GameObject cardObj = Instantiate(skillCardPrefab, handContainer);
            SkillCard cardScript = cardObj.GetComponent<SkillCard>();
            if (cardScript != null)
            {
                cardScript.Setup(card);
                
                // Subscribe to events
                cardScript.OnCardClicked += OnCardClickedHandler;
                cardScript.OnCardDragEnd += OnCardReorderedHandler;
            }
        }
    }

    private void OnCardClickedHandler(SkillCard cardScript)
    {
        if (CardDeckManager.Instance == null) return;
        
        // Try move to Action Queue
        bool success = CardDeckManager.Instance.MoveCardToActionQueue(cardScript.GetRuntimeCard());
        
        if (success)
        {
            // Visual Move
            if (actionSlotContainer != null)
            {
                cardScript.transform.SetParent(actionSlotContainer);
                
                // Cleanup listeners? Or keep them if we can move back? 
                // Usually once in action queue, interaction changes. For now let's keep it simple.
            }
            else
            {
                Debug.LogWarning("ActionSlotContainer not assigned!");
                Destroy(cardScript.gameObject); // Just destroy if nowhere to go
            }
        }
    }

    private void OnCardReorderedHandler(SkillCard cardScript, int newIndex)
    {
        // Simple logic: We trust the card moved to 'newIndex' in the hierarchy.
        // We need to match the backend list to this.
        
        RuntimeCard card = cardScript.GetRuntimeCard();
        int oldIndex = CardDeckManager.Instance.GetHand().IndexOf(card);
        
        if (oldIndex != -1 && oldIndex != newIndex)
        {
            CardDeckManager.Instance.ReorderHand(oldIndex, newIndex);
        }
    }

    public void ShowStartSequence(float playerCC, float enemyCC, bool isPlayerFirst, Sprite p1Icon, Sprite e1Icon)
    {
        gameObject.SetActive(true); // Ensure object is active for Coroutine
        
        // Setup Player Icon state
        if (player1Icon != null)
        {
            player1Icon.sprite = p1Icon;
            player1Icon.gameObject.SetActive(false); // Hide initially
            player1Icon.transform.localScale = Vector3.zero; // Prepare for animation
        }

        // Setup Enemy Icon state
        if (enemy1Icon != null)
        {
            enemy1Icon.sprite = e1Icon;
            enemy1Icon.gameObject.SetActive(false); // Hide initially
            enemy1Icon.transform.localScale = Vector3.zero; // Prepare for animation
        }

        StartCoroutine(AnimateSequence(playerCC, enemyCC, isPlayerFirst));
    }

    private IEnumerator AnimateSequence(float playerCC, float enemyCC, bool isPlayerFirst)
    {
        combatPowerPanel.SetActive(true);
        turnDecisionText.gameObject.SetActive(false);

        // Animate Numbers
        yield return StartCoroutine(CountUpTo(playerCCValueText, playerCC));
        yield return StartCoroutine(CountUpTo(enemyCCValueText, enemyCC));

        // Trigger Icon Animations
        yield return StartCoroutine(AnimateUnitPortraits());

        yield return new WaitForSeconds(0.5f);

        // Show Decision
        turnDecisionText.gameObject.SetActive(true);
        turnDecisionText.text = isPlayerFirst ? "PLAYER TURN" : "ENEMY TURN";
        
        yield return new WaitForSeconds(1.5f);
        
        combatPowerPanel.SetActive(false);
    }

    private IEnumerator CountUpTo(TextMeshProUGUI textComponent, float targetValue)
    {
        float current = 0;
        float elapsed = 0;

        while (elapsed < countDuration)
        {
            elapsed += Time.deltaTime;
            current = Mathf.Lerp(0, targetValue, elapsed / countDuration);
            textComponent.text = $"{Mathf.RoundToInt(current):N0}";
            yield return null;
        }
        textComponent.text = $"{Mathf.RoundToInt(targetValue):N0}";
    }

    private IEnumerator AnimateUnitPortraits()
    {
        // Animate Player Icon
        if (player1Icon != null && player1Icon.sprite != null)
        {
            StartCoroutine(AnimateIconEntrance(player1Icon));
        }

        // Animate Enemy Icon (can be simultaneous or slightly delayed)
        if (enemy1Icon != null && enemy1Icon.sprite != null)
        {
            // Optional: wait a tiny bit or just run together
            // yield return new WaitForSeconds(0.1f); 
            StartCoroutine(AnimateIconEntrance(enemy1Icon));
        }
        
        // Wait for animation duration (approx 0.5s) to ensure flow doesn't cut off
        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator AnimateIconEntrance(Image icon)
    {
        icon.gameObject.SetActive(true);
        
        // Fancy Bounce Effect
        float duration = 0.5f;
        float elapsed = 0f;
        Vector3 targetScale = Vector3.one;
        Vector3 startScale = Vector3.zero;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Elastic ease out
            float scale = Mathf.Sin(-13f * (t + 1) * Mathf.PI * 0.5f) * Mathf.Pow(2f, -10f * t) + 1f;
            
            icon.transform.localScale = startScale + (targetScale - startScale) * scale;
            yield return null;
        }
        icon.transform.localScale = targetScale;
    }
}
