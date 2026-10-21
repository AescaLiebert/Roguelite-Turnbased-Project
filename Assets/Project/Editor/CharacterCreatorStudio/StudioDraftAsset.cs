#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using UnityEditor;
using UnityEngine;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    /// <summary>JSON is the durable envelope; Unity never traverses the imported recursive runtime graph.</summary>
    public sealed class StudioDraftAsset : ScriptableObject
    {
        public int EnvelopeVersion = 1;
        [HideInInspector] public string DocumentJson;
        [HideInInspector] public string LibraryJson;
        [HideInInspector] public string SavedRevision;
        public CharacterObject SourceCharacter;
        [HideInInspector] public string ImportedSourceHash;
        [HideInInspector] public CharacterObject PublishedCharacter;
        [HideInInspector] public string PublishedFilesJson, LastPublishBackup;
        public bool IsDirectSession => !EditorUtility.IsPersistent(this);
    }

    [Serializable]
    internal sealed class StudioRecoveryEnvelope
    {
        public int Version = 1;
        public string BaseRevision;
        public string DocumentJson;
        public string LibraryJson;
    }

    public static class StudioDraftRepository
    {
        public const string Root = "Assets/Project/Editor/CharacterCreatorStudio";
        public const string DefaultDraft = Root + "/Drafts/KaeyaOpeningGauge.asset";
        public const string TestBundle = Root + "/Drafts/KaeyaTestBundle.asset";

        public static StudioDraftAsset Create(CharacterObject source, string path = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var legacy = FightingAllstar.Presentation.Content.CharacterObjectCatalogBuilder.CreateDefinition(source);
            var document = StudioDocumentService.ImportCharacter(legacy);
            document.Presentation.ActorAssetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source.Fighter3DPrefab));
            var asset = ScriptableObject.CreateInstance<StudioDraftAsset>();
            asset.SourceCharacter = source;
            asset.ImportedSourceHash = StudioProductionBridge.SourceFingerprint(source);
            asset.DocumentJson = SnapshotJson.Serialize(document);
            asset.LibraryJson = SnapshotJson.Serialize(DescriptionLibrary.CreateOpeningGaugeLibrary());
            EnsureFolder(Root + "/Drafts");
            path = path ?? AssetDatabase.GenerateUniqueAssetPath(Root + "/Drafts/" + source.name + "Studio.asset");
            AssetDatabase.CreateAsset(asset, path);
            Save(asset);
            return asset;
        }

        public static StudioDraftAsset GetOrCreateDefault()
        {
            var existing = AssetDatabase.LoadAssetAtPath<StudioDraftAsset>(DefaultDraft);
            if (existing != null) return existing;
            var source = Array.Find(CharacterObjectRegistrySO.LoadAll(), c => c != null && c.DefinitionId == "fighter.kaeya");
            if (source == null) throw new InvalidOperationException("The registered Kaeya source asset is unavailable.");
            return Create(source, DefaultDraft);
        }

        public static bool TryRead(StudioDraftAsset asset, out StudioDocument document, out DescriptionLibrary library, out string error)
        {
            document = null; library = null; error = null;
            try
            {
                if (asset == null) throw new ArgumentNullException(nameof(asset));
                if (asset.EnvelopeVersion != 1) throw new InvalidOperationException("Unsupported draft envelope version; source was preserved.");
                var source = SnapshotJson.Deserialize<StudioDocument>(asset.DocumentJson);
                if (!StudioDocumentService.TryMigrate(source, out document, out var diagnostics))
                    throw new InvalidOperationException(string.Join("\n", diagnostics));
                library = SnapshotJson.Deserialize<DescriptionLibrary>(asset.LibraryJson);
                if (library == null) throw new InvalidOperationException("Description library is missing; source was preserved.");
                return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        public static void Edit(StudioDraftAsset asset, StudioDocument document, DescriptionLibrary library, string operation)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            Undo.RecordObject(asset, operation);
            asset.DocumentJson = SnapshotJson.Serialize(document);
            asset.LibraryJson = SnapshotJson.Serialize(library);
            EditorUtility.SetDirty(asset);
        }

        public static void Save(StudioDraftAsset asset)
        {
            if (asset == null) return;
            if (asset.IsDirectSession) { StudioProductionBridge.SaveSource(asset); return; }
            // Close the edit's Undo delta before updating the on-disk baseline. Otherwise
            // an edit and Save in the same frame can make Undo restore an obsolete baseline,
            // falsely report Saved, and journal against a revision no longer on disk.
            Undo.FlushUndoRecordObjects();
            asset.SavedRevision = Revision(asset.DocumentJson, asset.LibraryJson);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        public static StudioDraftAsset Duplicate(StudioDraftAsset source)
        {
            if (!TryRead(source, out var document, out var library, out var error)) throw new InvalidOperationException(error);
            var copy = ScriptableObject.CreateInstance<StudioDraftAsset>();
            copy.SourceCharacter = source.SourceCharacter;
            copy.DocumentJson = SnapshotJson.Serialize(StudioDocumentService.Duplicate(document, out var remapped));
            copy.LibraryJson = SnapshotJson.Serialize(DescriptionAuthoring.RemapLibrary(library, remapped));
            var path = AssetDatabase.GenerateUniqueAssetPath(Root + "/Drafts/" + source.name + "Copy.asset");
            AssetDatabase.CreateAsset(copy, path);
            Save(copy);
            return copy;
        }

        public static void WriteRecovery(StudioDraftAsset asset)
        {
            if (asset?.IsDirectSession == true) return;
            var path = RecoveryPath(asset);
            if (path == null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path))
            {
                var previous = File.ReadAllText(path);
                StudioRecoveryEnvelope old = null;
                try { old = SnapshotJson.Deserialize<StudioRecoveryEnvelope>(previous); } catch { /* Preserve unreadable journals before replacing them. */ }
                if (old == null || old.BaseRevision != asset.SavedRevision &&
                    (old.DocumentJson != asset.DocumentJson || old.LibraryJson != asset.LibraryJson))
                    File.Copy(path, path + ".conflict-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfffffff") + ".json", false);
            }
            var recovery = new StudioRecoveryEnvelope { BaseRevision = asset.SavedRevision,
                DocumentJson = asset.DocumentJson, LibraryJson = asset.LibraryJson };
            var temporary = path + ".pending";
            File.WriteAllText(temporary, SnapshotJson.Serialize(recovery), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

        public static bool Recover(StudioDraftAsset asset, out string message)
        {
            message = asset?.IsDirectSession == true
                ? "Unsaved character edits are held in memory only. Press Ctrl+S to save."
                : "No pending recovery.";
            if (asset?.IsDirectSession == true) return false;
            try
            {
                var path = RecoveryPath(asset);
                if (path == null || !File.Exists(path)) return false;
                var recovery = SnapshotJson.Deserialize<StudioRecoveryEnvelope>(File.ReadAllText(path));
                if (recovery == null || recovery.Version != 1) { message = "Unsupported recovery envelope; file preserved."; return false; }
                if (recovery.BaseRevision != asset.SavedRevision) { message = "Recovery conflicts with a newer saved revision; file preserved."; return false; }
                if (recovery.DocumentJson == asset.DocumentJson && recovery.LibraryJson == asset.LibraryJson) return false;
                Undo.RecordObject(asset, "Recover Studio draft");
                asset.DocumentJson = recovery.DocumentJson; asset.LibraryJson = recovery.LibraryJson;
                EditorUtility.SetDirty(asset);
                message = "Recovered pending draft. Save to persist it into the asset.";
                return true;
            }
            catch (Exception exception) { message = "Recovery could not be read; source preserved: " + exception.Message; return false; }
        }

        public static StudioTestBundleAsset ApplyTestBundle(StudioDraftAsset asset, StudioCompileResult compiled)
        {
            if (asset == null || compiled == null || !compiled.Success) throw new InvalidOperationException("Validate a complete draft before applying test content.");
            if (!TryRead(asset, out var current, out var library, out var error)) throw new InvalidOperationException(error);
            var verified = StudioCompiler.Compile(current, library);
            if (!verified.Success || verified.Hashes.Bundle != compiled.Hashes.Bundle)
                throw new InvalidOperationException("The draft changed after validation. Validate again.");
            var path = AssetDatabase.GenerateUniqueAssetPath(Root + "/Drafts/" + asset.name + "Bundle.asset");
            var bundle = ScriptableObject.CreateInstance<StudioTestBundleAsset>();
            bundle.SourceDraft = asset;
            bundle.LegacyDefinitionJson = SnapshotJson.Serialize(current.LegacyCharacter);
            bundle.CompiledDefinitionJson = SnapshotJson.Serialize(verified.Character);
            bundle.DescriptionJson = SnapshotJson.Serialize(verified.Description);
            bundle.HashesJson = SnapshotJson.Serialize(verified.Hashes);
            bundle.UseStudioDefinition = true;
            AssetDatabase.CreateAsset(bundle, path);
            AssetDatabase.SaveAssetIfDirty(bundle);
            return bundle;
        }

        public static void RevertTestBundle(StudioTestBundleAsset bundle)
        {
            if (bundle == null) return;
            Undo.RecordObject(bundle, "Return test content to legacy source");
            bundle.UseStudioDefinition = false;
            EditorUtility.SetDirty(bundle); AssetDatabase.SaveAssetIfDirty(bundle);
        }

        public static string Revision(string document, string library)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes((document ?? "") + "\n" + (library ?? ""))))
                    .Replace("-", "").ToLowerInvariant();
        }

        private static string RecoveryPath(StudioDraftAsset asset)
        {
            if (asset == null) return null;
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset.IsDirectSession ? (UnityEngine.Object)asset.SourceCharacter : asset));
            if (asset.IsDirectSession && !string.IsNullOrEmpty(guid)) guid += "-direct";
            return string.IsNullOrEmpty(guid) ? null : Path.GetFullPath("Library/CharacterCreatorStudio/Recovery/" + guid + ".json");
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = folder.Substring(0, folder.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(folder.LastIndexOf('/') + 1));
        }
    }
}
#endif
