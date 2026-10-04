using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using Vector3 = System.Numerics.Vector3;
using System.Linq;

public class GachaManager : MonoBehaviour
{
    [SerializeField] private GachaRate[] gacha;
    [SerializeField] private Transform parent, pos;
    [SerializeField] private GameObject card;

    [SerializeField] private InventoryObject playerInventory;

    GameObject characterCard;
    GachaChar charComponent;

    public void Gacha()
    {
        if (!TryPayForSummon(1)) return;
        if (characterCard != null)
        {
            Destroy(characterCard);
        }
        
        ClearPreviousSummonResults();
        
        GameObject summonContainer = new GameObject("SummonResults");
        summonContainer.transform.SetParent(parent);
        summonContainer.transform.localPosition = pos.localPosition;
        
        characterCard = Instantiate(card, new UnityEngine.Vector3(1000,600,0), Quaternion.identity);
        characterCard.transform.SetParent(summonContainer.transform);
        characterCard.transform.localScale = new UnityEngine.Vector3(1, 1, 1);
        
        charComponent = characterCard.GetComponent<GachaChar>();
        
        GachaRate selectedRate = RollRarity();
        
        if (selectedRate != null)
        {
            // Get a random character of the selected rarity
            charComponent._character = GetRandomCharacter(selectedRate.rarity);
            
            // Pass the reward to the character component
            charComponent.Reward(selectedRate.rarity);
            PlayerInventoryService.Instance.RecordPaidPull();
        }
        else
        {
            Debug.LogWarning("No rarity found for the rolled value.");
        }
    }

    public void Gacha10x()
    {
        if (!TryPayForSummon(10)) return;
        ClearPreviousSummonResults();

        GameObject summonContainer = new GameObject("SummonResults");
        summonContainer.transform.SetParent(parent);
        summonContainer.transform.localPosition = pos.localPosition;

        // Perform 10 summons
        List<CharacterObject> summonedCharacters = new List<CharacterObject>();
        for (int i = 0; i < 10; i++)
        {
            CharacterObject summonedCharacter = PerformSingleSummon();
            
            if (summonedCharacter != null)
            {
                summonedCharacters.Add(summonedCharacter);
                
                characterCard = Instantiate(card, GetPositionForMultiSummon(i), Quaternion.identity);
                characterCard.transform.SetParent(summonContainer.transform);
                characterCard.transform.localScale = new UnityEngine.Vector3(1, 1, 1);
        
                charComponent = characterCard.GetComponent<GachaChar>();
                charComponent._character = summonedCharacter;
                charComponent.Reward(summonedCharacter.FighterRarity.ToString());
                PlayerInventoryService.Instance.RecordPaidPull();
            }
        }
    }
    
    // Helper method to clear previous summon results
    private void ClearPreviousSummonResults()
    {
        // Find and destroy previous summon results container
        Transform existingContainer = parent.Find("SummonResults");
        if (existingContainer != null)
        {
            Destroy(existingContainer.gameObject);
        }
    }

    // Helper method to determine position for multi-summon cards
    private UnityEngine.Vector3 GetPositionForMultiSummon(int index)
    {
        // Calculate grid-like positioning for 10 cards
        float gridWidth = 320f;  // Adjust based on your UI layout
        float gridHeight = 230f; // Adjust based on your UI layout

        int row = index / 5;
        int col = index % 5;

        UnityEngine.Vector3 basePosition = pos.position;
        UnityEngine.Vector3 position = new UnityEngine.Vector3(basePosition.x, basePosition.y - 120, basePosition.z);
        return new UnityEngine.Vector3(position.x + (col - 2) * gridWidth, position.y + (row * gridHeight), position.z);
    }

    private CharacterObject PerformSingleSummon()
    {
        var selectedRate = RollRarity();
        return selectedRate == null ? null : GetRandomCharacter(selectedRate.rarity);
    }

    private bool TryPayForSummon(int count)
    {
        var inventory = PlayerInventoryService.Instance;
        if (inventory == null)
        {
            Debug.LogError("Player inventory is unavailable; summon was not processed.", this);
            return false;
        }
        if (inventory.TrySpendForSummon(count, out var error)) return true;
        Debug.LogWarning(error, this);
        return false;
    }

    private GachaRate RollRarity()
    {
        // KOF pool: 4% SSR, 36% SR and 60% R. Ten-pulls have no rarity guarantee.
        var roll = Random.Range(0f, 100f);
        var rarity = roll < 4f ? "SSR" : roll < 40f ? "SR" : "R";
        return new GachaRate { rarity = rarity };
    }
    
    private CharacterObject GetRandomCharacter(string rarity)
    {
        var pool = PlayerInventoryService.Instance.GetCatalog()
            .Where(character => character != null && character.FighterRarity.ToString() == rarity).ToArray();
        if (pool.Length == 0)
        {
            Debug.LogError("No runtime characters are configured for rarity: " + rarity, this);
            return null;
        }
        return pool[Random.Range(0, pool.Length)];
    }
}
