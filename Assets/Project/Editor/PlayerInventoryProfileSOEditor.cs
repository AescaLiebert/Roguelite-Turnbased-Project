using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerInventoryProfileSO))]
public sealed class PlayerInventoryProfileSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var profileSO = (PlayerInventoryProfileSO)target;

        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Profile & Save Tools", EditorStyles.boldLabel);

        var snapshot = profileSO.Profile;
        if (snapshot != null)
        {
            var charCount = snapshot.characters != null ? snapshot.characters.Count : 0;
            var artifactCount = snapshot.items?.Artifacts?.Count ?? 0;
            EditorGUILayout.HelpBox($"Diamonds: {snapshot.diamonds:N0} | Characters: {charCount} | Artifacts: {artifactCount} | Guarantee: {snapshot.featuredGuaranteeProgress}/300", MessageType.Info);
        }

        EditorGUILayout.BeginVertical("box");

        // Action 1: Capture from Play Session
        GUI.enabled = Application.isPlaying && PlayerInventoryService.Instance != null;
        if (GUILayout.Button("Capture From Active Play Session"))
        {
            if (PlayerInventoryService.Instance != null && PlayerInventoryService.Instance.Snapshot != null)
            {
                Undo.RecordObject(profileSO, "Capture Player Inventory Snapshot");
                profileSO.CopyFrom(PlayerInventoryService.Instance.Snapshot);
                EditorUtility.SetDirty(profileSO);
                AssetDatabase.SaveAssets();
                Debug.Log($"[PlayerInventoryProfileSO] Successfully captured play session snapshot into {profileSO.name}.");
            }
        }
        GUI.enabled = true;

        // Action 2: Import from AppData Save File
        var savePath = PlayerInventoryService.GetSavePath();
        var saveExists = File.Exists(savePath);

        GUI.enabled = saveExists;
        if (GUILayout.Button("Import From AppData Save File"))
        {
            if (saveExists)
            {
                try
                {
                    var json = File.ReadAllText(savePath);
                    var loaded = JsonUtility.FromJson<PlayerInventorySnapshot>(json);
                    if (loaded != null)
                    {
                        Undo.RecordObject(profileSO, "Import From AppData Save File");
                        profileSO.CopyFrom(loaded);
                        EditorUtility.SetDirty(profileSO);
                        AssetDatabase.SaveAssets();
                        Debug.Log($"[PlayerInventoryProfileSO] Loaded snapshot from disk save ({savePath}) into {profileSO.name}.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PlayerInventoryProfileSO] Failed to import from save file: {ex.Message}");
                }
            }
        }
        GUI.enabled = true;

        // Action 3: Write this profile to AppData Save File
        if (GUILayout.Button("Write Profile To AppData Save File"))
        {
            if (EditorUtility.DisplayDialog("Confirm Overwrite",
                "This will overwrite the local save file in AppData with the contents of this ScriptableObject. Continue?",
                "Yes, Overwrite", "Cancel"))
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(savePath));
                    var json = JsonUtility.ToJson(profileSO.Profile, true);
                    var tmp = savePath + ".tmp";
                    File.WriteAllText(tmp, json);
                    if (File.Exists(savePath)) File.Replace(tmp, savePath, null);
                    else File.Move(tmp, savePath);

                    if (Application.isPlaying && PlayerInventoryService.Instance != null)
                    {
                        PlayerInventoryService.Instance.ReloadFromDisk();
                    }

                    Debug.Log($"[PlayerInventoryProfileSO] Wrote profile to AppData save file ({savePath}).");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PlayerInventoryProfileSO] Failed to write profile to disk: {ex.Message}");
                }
            }
        }

        // Action 4: Reset to default starter roster
        if (GUILayout.Button("Reset To Default Starter Roster"))
        {
            if (EditorUtility.DisplayDialog("Reset Profile",
                "Reset this profile to default starter roster (Kyo94, Benimaru94, Goro94) and 1,600 diamonds?",
                "Reset", "Cancel"))
            {
                Undo.RecordObject(profileSO, "Reset Profile To Starter");
                profileSO.ResetToStarterRoster();
                EditorUtility.SetDirty(profileSO);
                AssetDatabase.SaveAssets();
                Debug.Log($"[PlayerInventoryProfileSO] Reset {profileSO.name} to default starter roster.");
            }
        }

        // Action 5: Open Save Directory
        if (GUILayout.Button("Open AppData Save Folder in Explorer"))
        {
            var folder = Path.GetDirectoryName(savePath);
            if (Directory.Exists(folder))
            {
                EditorUtility.RevealInFinder(savePath);
            }
            else
            {
                EditorUtility.RevealInFinder(Application.persistentDataPath);
            }
        }

        EditorGUILayout.EndVertical();
    }
}
