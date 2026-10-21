#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using FightingAllstar.Presentation.Content;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public static partial class StudioProductionBridge
    {
        // This object is an Undo/preview buffer only. It is never created as a .asset.
        public static StudioDraftAsset OpenSource(CharacterObject source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var current = CharacterObjectCatalogBuilder.CreateDefinition(source);
            StudioDocument doc; DescriptionLibrary library;
            if (string.IsNullOrEmpty(source.StudioAuthoringJson))
            {
                doc = StudioDocumentService.ImportCharacter(current); library = DescriptionLibrary.CreateOpeningGaugeLibrary();
                doc.Id = "studio.native." + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
                doc.Description.OwnerId = doc.Id;
            }
            else
            {
                var saved = SnapshotJson.Deserialize<StudioDocument>(source.StudioAuthoringJson);
                if (!StudioDocumentService.TryMigrate(saved, out doc, out var diagnostics)) throw new InvalidOperationException(string.Join("\n", diagnostics));
                doc.LegacyCharacter = current.Clone();
                doc.AuthoredCharacter = RemoveCompiledAdditions(current, doc);
                library = SnapshotJson.Deserialize<DescriptionLibrary>(source.StudioLibraryJson);
                if (library == null) throw new InvalidOperationException("The embedded wording library is missing.");
            }
            StudioPassiveEditing.Normalize(doc);
            StudioPassiveEditing.ImportReactions(doc, library);
            var session = ScriptableObject.CreateInstance<StudioDraftAsset>(); session.name = source.name + " · editing";
            session.hideFlags = HideFlags.HideInHierarchy;
            session.SourceCharacter = source; session.ImportedSourceHash = SourceFingerprint(source);
            session.DocumentJson = SnapshotJson.Serialize(doc); session.LibraryJson = SnapshotJson.Serialize(library);
            session.SavedRevision = StudioDraftRepository.Revision(session.DocumentJson, session.LibraryJson);
            return session;
        }

        private static CharacterDefinition RemoveCompiledAdditions(CharacterDefinition current, StudioDocument doc)
        {
            return StudioPassiveEditing.RemoveGenerated(current, doc);
        }

        private static void EmbedAuthoring(CharacterObject character, StudioDocument doc, DescriptionLibrary library, string oldId)
        {
            var embedded = StudioDocumentService.Clone(doc); embedded.LegacyCharacter = null; embedded.AuthoredCharacter = null;
            string Remap(string json)
            {
                var token = JToken.Parse(json);
                foreach (var value in ((JContainer)token).Descendants().OfType<JValue>().Where(v => v.Type == JTokenType.String))
                { var text = (string)value.Value; if (text == oldId || text.StartsWith(oldId + ".", StringComparison.Ordinal)) value.Value = character.DefinitionId + text.Substring(oldId.Length); }
                return token.ToString(Newtonsoft.Json.Formatting.None);
            }
            character.SetStudioAuthoring(Remap(SnapshotJson.Serialize(embedded)), Remap(SnapshotJson.Serialize(library)));
        }

        public static void SaveSource(StudioDraftAsset session) => SaveSource(session, true, true);

        public static void SaveSource(StudioDraftAsset session, bool rehydrateSession) => SaveSource(session, rehydrateSession, true);

        public static void SaveSource(StudioDraftAsset session, bool rehydrateSession, bool registerAssetUndo)
        {
            EditingOnly();
            var source = session?.SourceCharacter;
            if (source == null || !session.IsDirectSession) throw new InvalidOperationException("Open a native character to save its asset set.");
            var fingerprint = SourceFingerprint(source);
            if (fingerprint != session.ImportedSourceHash)
            {
                // A domain reload can materialize fields added by a newer code schema.
                // Accept the old fingerprint only when existing source data still matches exactly.
                if (LegacyCompatibleFingerprint(source) != session.ImportedSourceHash)
                    throw new InvalidOperationException("The native character changed since opening. Reopen it before saving; pending edits are retained in recovery.");
                session.ImportedSourceHash = fingerprint;
            }
            if (!StudioDraftRepository.TryRead(session, out var doc, out var library, out var error)) throw new InvalidOperationException(error);
            var compiled = StudioCompiler.Compile(doc, library);
            if (!compiled.Success) throw new InvalidOperationException(string.Join("\n", compiled.Diagnostics));
            var definition = compiled.Character;
            if (definition.Id != source.DefinitionId || definition.CategoryId != source.ID) throw new InvalidOperationException("Save keeps the native identity. Use Save as new character for another identity.");
            var objects = new Object[] { source.Skill1, source.Skill2, source.Ultimate, source.PassiveDefinition, source };
            if (objects.Any(o => o == null || !EditorUtility.IsPersistent(o)) || objects.Distinct().Count() != 5) throw new InvalidOperationException("The character must reference five distinct, saved native assets.");
            foreach (var other in CharacterObjectRegistrySO.LoadAll().Where(c => c != source))
                if (new Object[] { other.Skill1, other.Skill2, other.Ultimate, other.PassiveDefinition }.Any(o => o != null && objects.Contains(o)))
                    throw new InvalidOperationException("A data asset is shared by another character. Give this character its own data asset before editing it.");
            ValidateNativeRoundtrip(source, definition, compiled.Hashes.Bundle); ValidateCandidate(definition, source);
            var before = objects.ToDictionary(AssetDatabase.GetAssetPath, o => File.ReadAllBytes(AssetDatabase.GetAssetPath(o)));
            var originalJson = objects.ToDictionary(o => o, EditorJsonUtility.ToJson);
            Undo.FlushUndoRecordObjects();
            var group = -1;
            if (registerAssetUndo)
            {
                Undo.IncrementCurrentGroup(); group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Save character kit");
                Undo.RegisterCompleteObjectUndo(objects.Append(session).ToArray(), "Save character kit");
            }
            try
            {
                Apply(source, source.Skill1, source.Skill2, source.Ultimate, source.PassiveDefinition, definition, source, compiled.Hashes.Bundle);
                // Only the flat rule/writer metadata is embedded. Native SOs own all base kit data.
                EmbedAuthoring(source, doc, library, doc.SourceCharacterId);
                foreach (var obj in objects) { EditorUtility.SetDirty(obj); AssetDatabase.SaveAssetIfDirty(obj); VerificationCheckpoint?.Invoke(AssetDatabase.GetAssetPath(obj)); }
                foreach (var path in before.Keys) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                if (GameplayHash(CharacterObjectCatalogBuilder.CreateDefinition(source)) != GameplayHash(definition)) throw new InvalidOperationException("Native save changed the authored gameplay definition.");
                if (rehydrateSession)
                {
                    // Rehydrate from the saved five assets so the next Save never appends Studio rules twice.
                    var reopened = OpenSource(source);
                    session.DocumentJson = reopened.DocumentJson; session.LibraryJson = reopened.LibraryJson;
                    session.ImportedSourceHash = reopened.ImportedSourceHash; session.SavedRevision = reopened.SavedRevision;
                    Object.DestroyImmediate(reopened);
                }
                else
                {
                    // A non-rehydrating commit keeps the active document object graph alive for existing callers.
                    session.ImportedSourceHash = SourceFingerprint(source);
                    session.SavedRevision = StudioDraftRepository.Revision(session.DocumentJson, session.LibraryJson);
                }
                if (group >= 0) Undo.CollapseUndoOperations(group);
            }
            catch
            {
                foreach (var obj in objects) { EditorJsonUtility.FromJsonOverwrite(originalJson[obj], obj); EditorUtility.ClearDirty(obj); }
                foreach (var pair in before) { File.WriteAllBytes(pair.Key, pair.Value); AssetDatabase.ImportAsset(pair.Key, ImportAssetOptions.ForceUpdate); }
                if (group >= 0) Undo.RevertAllDownToGroup(group);
                throw;
            }
        }
    }
}
#endif
