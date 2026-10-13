using System;
using System.Collections.Generic;
using UnityEngine;
using FightingAllstar.Core.Run;

/// <summary>
/// ScriptableObject profile representing a player inventory snapshot for editor authoring, testing, and presets.
/// </summary>
[CreateAssetMenu(fileName = "NewPlayerInventoryProfile", menuName = "Fighting Allstar/Player Inventory Profile")]
public sealed class PlayerInventoryProfileSO : ScriptableObject
{
    [SerializeField] private PlayerInventorySnapshot profile = new PlayerInventorySnapshot();

    public PlayerInventorySnapshot Profile
    {
        get => profile;
        set => profile = value;
    }

    /// <summary>
    /// Returns a deep clone of the authored snapshot so play-mode modifications do not unintentionally mutate the asset.
    /// </summary>
    public PlayerInventorySnapshot CloneSnapshot()
    {
        if (profile == null) return new PlayerInventorySnapshot();
        var json = JsonUtility.ToJson(profile);
        return JsonUtility.FromJson<PlayerInventorySnapshot>(json) ?? new PlayerInventorySnapshot();
    }

    /// <summary>
    /// Deep-copies the provided snapshot into this ScriptableObject.
    /// </summary>
    public void CopyFrom(PlayerInventorySnapshot source)
    {
        if (source == null) return;
        var json = JsonUtility.ToJson(source);
        profile = JsonUtility.FromJson<PlayerInventorySnapshot>(json) ?? new PlayerInventorySnapshot();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    /// <summary>
    /// Resets the profile to standard starter roster fighters and default diamond balance.
    /// </summary>
    public void ResetToStarterRoster(IEnumerable<string> starterIds = null)
    {
        var ids = starterIds ?? new[] { "fighter.kyo94", "fighter.benimaru94", "fighter.goro94" };
        profile = new PlayerInventorySnapshot
        {
            schemaVersion = 1,
            subjectId = "guest:local-development",
            diamonds = 1600,
            featuredGuaranteeProgress = 0,
            selectorEntitlements = 0,
            duplicateTokens = 0,
            artifactAutoSelectRarity = 1,
            characters = new List<OwnedCharacterRecord>(),
            formation = new List<string> { "", "", "", "" },
            dungeonFormations = new List<DungeonFormationSave>(),
            claimedRunRewards = new List<string>(),
            items = new ItemInventory()
        };

        foreach (var defId in ids)
        {
            if (string.IsNullOrWhiteSpace(defId)) continue;
            profile.characters.Add(new OwnedCharacterRecord
            {
                instanceId = Guid.NewGuid().ToString("N"),
                definitionId = defId,
                constellationTier = 0,
                level = 1
            });
        }
        for (var i = 0; i < 4; i++)
        {
            if (i < profile.characters.Count) profile.formation[i] = profile.characters[i].instanceId;
            else profile.formation[i] = string.Empty;
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
}
