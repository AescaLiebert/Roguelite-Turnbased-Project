using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FightingAllstar.EditorTools
{
    /// <summary>Index authored assets without regenerating or overwriting their configuration.</summary>
    public sealed class CharacterRegistrySync : AssetPostprocessor
    {
        private static bool _queued;
        [InitializeOnLoadMethod]
        private static void Initialize() { EditorApplication.delayCall += Sync; }
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (_queued || !imported.Concat(deleted).Concat(moved).Concat(movedFrom)
                .Any(p => p.StartsWith("Assets/Project/Data/Character/") && p.EndsWith(".asset"))) return;
            _queued = true;
            EditorApplication.delayCall += () => { _queued = false; Sync(); };
        }
        [MenuItem("Fighting Allstar/Sync Authored Character Registry")]
        public static void Sync()
        {
            var registry = AssetDatabase.LoadAssetAtPath<CharacterObjectRegistrySO>("Assets/Resources/CharacterObjectRegistry.asset");
            if (registry == null) return;
            var characters = AssetDatabase.FindAssets("t:CharacterObject", new[] { "Assets/Project/Data" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<CharacterObject>).Where(c => c != null).ToArray();
            if (registry.Characters.SequenceEqual(characters)) return;
            registry.SetCharacters(characters);
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssetIfDirty(registry);
        }
    }
}
