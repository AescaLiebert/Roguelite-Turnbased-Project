using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/*public class CharacterInstantiator : MonoBehaviour
{
    [SerializeField] private CharacterTeamData teamDataContainer;
    [SerializeField] private GameObject[] characterPrefabs;
    
    private void InstantiateTeamCharacters()
    {
        foreach (var characterData in teamDataContainer.TeamCharacters)
        {
            // Find the corresponding 3D prefab
            GameObject characterPrefab = FindCharacterPrefab(characterData.Character);
                
            if (characterPrefab != null)
            {
                // Instantiate the character at the specified position
                GameObject instantiatedCharacter = Instantiate(
                    characterPrefab, 
                    characterData.Position, 
                    Quaternion.identity
                );

                // Optional: Set up additional character components
                SetupCharacterComponents(instantiatedCharacter, characterData.Character);
            }
            else
            {
                Debug.LogWarning($"No prefab found for character: {characterData.Character.FighterName}");
            }
        }
    }
    
    private GameObject FindCharacterPrefab(CharacterObject characterObject)
    {
        // Find prefab that matches the character's attributes
        return characterPrefabs.FirstOrDefault(prefab => 
        {
            var characterComponent = prefab.GetComponent<CharacterComponent>();
            return characterComponent != null && 
                   characterComponent.CharacterData == characterObject;
        });
    }

    private void SetupCharacterComponents(GameObject characterObject, CharacterObject characterData)
    {
        // Additional setup like stats, abilities, etc.
        var characterComponent = characterObject.GetComponent<CharacterComponent>();
        if (characterComponent != null)
        {
            characterComponent.Initialize(characterData);
        }
    }
}

// Additional component for 3D character prefabs
public class CharacterComponent : MonoBehaviour
{
    public CharacterObject CharacterData { get; private set; }

    public void Initialize(CharacterObject characterData)
    {
        CharacterData = characterData;
        // Set up character-specific properties
        // For example:
        // transform.name = characterData.FighterName;
        // Apply stats, materials, etc.
    }
}*/
