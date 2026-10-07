using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime index for CharacterObjects authored outside a Resources folder.
/// Character assets remain in Project/Data; this small Resources asset holds references to them.
/// </summary>
[CreateAssetMenu(menuName = "Fighting Allstar/Character Object Registry")]
public sealed class CharacterObjectRegistrySO : ScriptableObject
{
    private const string RegistryResourcesPath = "CharacterObjectRegistry";

    [SerializeField] private List<CharacterObject> characters = new List<CharacterObject>();
    public IReadOnlyList<CharacterObject> Characters => characters;

    public void SetCharacters(IEnumerable<CharacterObject> values)
    {
        characters = values == null ? new List<CharacterObject>() : new List<CharacterObject>(values);
    }

    public static CharacterObject[] LoadAll()
    {
        var registry = Resources.Load<CharacterObjectRegistrySO>(RegistryResourcesPath);
        if (registry != null)
        {
            if (registry.characters == null) return Array.Empty<CharacterObject>();
            return registry.characters.FindAll(character => character != null).ToArray();
        }

        // Keep compatibility for projects that still keep character assets directly under Resources.
        return Resources.LoadAll<CharacterObject>(string.Empty);
    }
}
