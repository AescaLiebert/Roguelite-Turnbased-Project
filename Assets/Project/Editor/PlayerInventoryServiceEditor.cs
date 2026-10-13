using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerInventoryService))]
public sealed class PlayerInventoryServiceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var service = (PlayerInventoryService)target;

        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Editor Save & Profile Controls", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical("box");

        var profile = service.EditorProfile;
        if (profile != null)
        {
            if (GUILayout.Button("Apply Assigned Profile To Inventory"))
            {
                service.ApplyProfile(profile);
                Debug.Log($"[PlayerInventoryService] Applied profile '{profile.name}' to current inventory.");
            }

            if (GUILayout.Button("Export Current Inventory To Assigned Profile"))
            {
                Undo.RecordObject(profile, "Export Inventory To Profile");
                service.ExportCurrentToProfile(profile);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                Debug.Log($"[PlayerInventoryService] Exported current inventory to '{profile.name}'.");
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Assign an Editor Profile asset above to enable one-click profile import/export.", MessageType.None);
        }

        EditorGUILayout.Space(4);

        if (GUILayout.Button("Reload Snapshot From AppData JSON"))
        {
            service.ReloadFromDisk();
            Debug.Log("[PlayerInventoryService] Reloaded inventory from disk JSON.");
        }

        if (GUILayout.Button("Open AppData Save Folder in Explorer"))
        {
            var savePath = PlayerInventoryService.GetSavePath();
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
