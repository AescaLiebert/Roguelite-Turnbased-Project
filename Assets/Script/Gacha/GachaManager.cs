using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
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
        
        float randomRoll = GetPreciseRandomChance();
        GachaRate selectedRate = null;

        // Find the first rarity that matches the random roll
        selectedRate = gacha.FirstOrDefault(g => randomRoll <= g.rate) ?? gacha.Last();
        
        if (selectedRate != null)
        {
            // Get a random character of the selected rarity
            charComponent._character = GetRandomCharacter(selectedRate.rarity);
            
            // Pass the reward to the character component
            charComponent.Reward(selectedRate.rarity);
        }
        else
        {
            Debug.LogWarning("No rarity found for the rolled value.");
        }
    }

    public void Gacha10x()
    {
        ClearPreviousSummonResults();

        GameObject summonContainer = new GameObject("SummonResults");
        summonContainer.transform.SetParent(parent);
        summonContainer.transform.localPosition = pos.localPosition;

        // Perform 10 summons
        List<CharacterObject> summonedCharacters = new List<CharacterObject>();
        for (int i = 0; i < 10; i++)
        {
            CharacterObject summonedCharacter = PerformSingleSummon();
            
            if (summonedCharacters != null)
            {
                summonedCharacters.Add(summonedCharacter);
                
                characterCard = Instantiate(card, GetPositionForMultiSummon(i), Quaternion.identity);
                characterCard.transform.SetParent(summonContainer.transform);
                characterCard.transform.localScale = new UnityEngine.Vector3(1, 1, 1);
        
                charComponent = characterCard.GetComponent<GachaChar>();
                charComponent._character = summonedCharacter;
                charComponent.Reward(summonedCharacter.FighterRarity.ToString());
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

    private CharacterObject PerformSingleSummon(bool guaranteedHigherRarity = false)
    {
        float randomRoll = GetPreciseRandomChance();
        GachaRate selectedRate = null;

        if (guaranteedHigherRarity)
        {
            // Ensure at least SR or SSR
            selectedRate = gacha.FirstOrDefault(r => r.rarity == "SR");
        }
        else
        {
            // Existing random selection logic
            //Debug.Log(randomRoll);
            selectedRate = gacha.FirstOrDefault(g => randomRoll <= g.rate) ?? gacha.Last();
        }

        return selectedRate != null 
            ? GetRandomCharacter(selectedRate.rarity) 
            : null;
    }

    private bool IsGuaranteedHigherRarity(int pullIndex)
    {
        // Example guarantee logic:
        // Guarantee at least one SR or SSR in 10 pulls
        return pullIndex == 9;  // Last pull is guaranteed to be SR or higher
    }
    
    private CharacterObject GetRandomCharacter(string rarity)
    {
        // Find the GachaRate for the specific rarity
        GachaRate selectedRate = null;
        for (int i = 0; i < gacha.Length; i++)
        {
            if (gacha[i].rarity == rarity)
            {
                selectedRate = gacha[i];
                break;
            }
        }

        if (selectedRate != null && selectedRate.reward.Length > 0)
        {
            // Return a random character from the rewards of this rarity
            int randomIndex = Random.Range(0, selectedRate.reward.Length);
            return selectedRate.reward[randomIndex];
        }

        Debug.LogError($"No characters found for rarity: {rarity}");
        return null;
    }
    
    private float GetPreciseRandomChance()
    {
        // Generates a random float between 1.00 and 101.00
        return Mathf.Round(Random.Range(1f, 101f) * 10f) / 10f;
    }
}
