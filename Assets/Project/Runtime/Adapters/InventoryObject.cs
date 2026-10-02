using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.TextCore.Text;

[CreateAssetMenu(fileName = "New Inventory", menuName = "Inventory System/Inventory")]

public class InventoryObject : ScriptableObject
{
    public List<CharacterObject> characterContainer = new List<CharacterObject>();

    public int CharacterAmount{ get { return characterContainer.Count; } }

    public void AddCharacter(CharacterObject character)
    {
        if (character == null)
        {
            Debug.LogError("Attempting to add null character!");
            return;
        }

        // Use LINQ for more robust checking
        bool characterExists = characterContainer.Any(c => c.FighterName == character.FighterName);

        if (!characterExists)
        {
            characterContainer.Add(character);
            Debug.Log($"Character {character.FighterName} has been added to Inventory!");
        }
    }
    
    public CharacterObject GetCharacterById(int id)
    {
        return characterContainer.Find(character => character.ID == id);
    }
}
