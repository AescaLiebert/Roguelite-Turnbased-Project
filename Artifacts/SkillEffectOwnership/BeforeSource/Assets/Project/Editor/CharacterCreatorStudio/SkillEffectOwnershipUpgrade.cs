#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using FightingAllstar.Presentation.Content;
using UnityEditor;
using UnityEngine;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public static class SkillEffectOwnershipUpgrade
    {
        [MenuItem("Fighting Allstar/Content/Migrate Skill Effect Ownership")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Migration requires Edit Mode.");
            var ids = new[] { "fighter.scii-taki", "fighter.pre-release.billy97", "fighter.benimaru99" };
            var sources = AssetDatabase.FindAssets("t:CharacterObject").Select(g => AssetDatabase.LoadAssetAtPath<CharacterObject>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null && ids.Contains(c.DefinitionId)).ToArray();
            if (sources.Length != ids.Length) throw new InvalidOperationException("Expected exactly the three target characters.");
            var output = "Artifacts/SkillEffectOwnership"; Directory.CreateDirectory(output);
            foreach (var source in sources)
            {
                var before = CharacterObjectCatalogBuilder.CreateDefinition(source);
                var paths = new UnityEngine.Object[] { source, source.Skill1, source.Skill2, source.Ultimate, source.PassiveDefinition }
                    .Select(AssetDatabase.GetAssetPath).ToArray();
                foreach (var path in paths)
                {
                    var backup = output + "/BeforeAssets/" + path;
                    Directory.CreateDirectory(Path.GetDirectoryName(backup));
                    if (!File.Exists(backup)) File.Copy(path, backup);
                }
                var session = StudioProductionBridge.OpenSource(source);
                try
                {
                    var doc = SnapshotJson.Deserialize<StudioDocument>(session.DocumentJson);
                    var library = SnapshotJson.Deserialize<DescriptionLibrary>(session.LibraryJson);
                    var compiled = StudioCompiler.Compile(doc, library);
                    if (!compiled.Success) throw new InvalidOperationException(string.Join("\n", compiled.Diagnostics));
                    var next = compiled.Character;
                    var oldIds = next.Passive.Reactions.Where(r => r.Id == "skill-2-target-max-health-damage" ||
                        r.Id.EndsWith(".guard-burn", StringComparison.Ordinal) || r.Id == "apply-debuff-women-defense-down").Select(r => r.Id).ToArray();
                    if (!SkillEffectOwnershipMigration.Apply(next)) continue;
                    doc.AuthoredCharacter = StudioPassiveEditing.RemoveGenerated(next, doc);
                    doc.Rules.RemoveAll(r => oldIds.Contains(r.Id));
                    session.DocumentJson = SnapshotJson.Serialize(doc);
                    StudioProductionBridge.SaveSource(session);
                    var after = CharacterObjectCatalogBuilder.CreateDefinition(source);
                    File.WriteAllText(output + "/" + source.name + "-before.json", SnapshotJson.Serialize(before));
                    File.WriteAllText(output + "/" + source.name + "-after.json", SnapshotJson.Serialize(after));
                    var reopened = StudioProductionBridge.OpenSource(source);
                    try
                    {
                        StudioProductionBridge.SaveSource(reopened);
                        if (SnapshotJson.Serialize(after) != SnapshotJson.Serialize(CharacterObjectCatalogBuilder.CreateDefinition(source)))
                            throw new InvalidOperationException(source.name + ": repeated save changed the migrated definition.");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(reopened); }
                    Debug.Log("Migrated skill effect ownership: " + source.DefinitionId);
                }
                finally { UnityEngine.Object.DestroyImmediate(session); }
            }
        }
    }
}
#endif
