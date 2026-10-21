#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    [Serializable]
    public sealed class StudioAuthoringEvidence
    {
        public string UnityVersion;
        public string CapturedUtc;
        public string VerificationDraft;
        public string GameplayHash;
        public string BundleHash;
        public List<string> Passed = new List<string>();
        public List<string> Failed = new List<string>();
        public bool Success => Failed.Count == 0;
    }

    /// <summary>Uses real AssetDatabase persistence and Unity Undo; pure core tests cannot prove these contracts.</summary>
    public static class StudioAuthoringChecks
    {
        public static StudioAuthoringEvidence LastReport { get; private set; }
        public static StudioAuthoringEvidence Run()
        {
            var report = LastReport = new StudioAuthoringEvidence { UnityVersion = Application.unityVersion, CapturedUtc = DateTime.UtcNow.ToString("O") };
            void Check(bool condition, string message) { (condition ? report.Passed : report.Failed).Add(message); }
            var source = Array.Find(CharacterObjectRegistrySO.LoadAll(), c => c != null && c.DefinitionId == "fighter.kaeya");
            if (source == null) throw new InvalidOperationException("Kaeya is missing from the registered roster.");
            var sourcePath = AssetDatabase.GetAssetPath(source);
            var sourceHash = HashFile(sourcePath);
            var scene = SceneManager.GetActiveScene();
            var sceneDirty = scene.isDirty;
            var asset = StudioDraftRepository.Create(source);
            report.VerificationDraft = AssetDatabase.GetAssetPath(asset);
            StudioDraftRepository.TryRead(asset, out var document, out var library, out var readError);
            Check(readError == null, "Import reads the actual registered source and creates a versioned draft.");
            var initial = StudioCompiler.Compile(document, library);
            Check(initial.Success, "Imported draft compiles.");
            report.GameplayHash = initial.Hashes.Gameplay; report.BundleHash = initial.Hashes.Bundle;
            var originalJson = asset.DocumentJson;
            var originalLibrary = asset.LibraryJson;
            AssetDatabase.ImportAsset(report.VerificationDraft, ImportAssetOptions.ForceUpdate);
            var loaded = AssetDatabase.LoadAssetAtPath<StudioDraftAsset>(report.VerificationDraft);
            StudioDraftRepository.TryRead(loaded, out var reopened, out var reopenedLibrary, out _);
            Check(loaded.DocumentJson == originalJson && StudioCompiler.Compile(reopened, reopenedLibrary).Hashes.Bundle == initial.Hashes.Bundle,
                "Save/reimport/reopen preserves nested nodes, references and compiled identity.");

            Undo.IncrementCurrentGroup();
            var undoBefore = loaded.DocumentJson;
            reopened.Parameters.First(p => p.Id == "param.opening-gauge").Value = 2;
            StudioDraftRepository.Edit(loaded, reopened, reopenedLibrary, "Studio verification parameter edit");
            Undo.FlushUndoRecordObjects();
            var undoAfter = loaded.DocumentJson;
            Undo.PerformUndo();
            Check(loaded.DocumentJson == undoBefore, "Unity Undo restores the canonical payload.");
            Undo.PerformRedo();
            Check(loaded.DocumentJson == undoAfter, "Unity Redo restores edited parameter and references.");
            StudioDraftRepository.TryRead(loaded, out reopened, out reopenedLibrary, out _);
            var changed = StudioCompiler.Compile(reopened, reopenedLibrary);
            Check(changed.Success && changed.Hashes.Gameplay != initial.Hashes.Gameplay && changed.Description.PlainText.Contains("2 Power Gauge"),
                "The same edited parameter changes real gameplay output and rendered wording.");

            // Recovery is written from an unsaved payload, then tested after its in-memory state is lost.
            StudioDraftRepository.Save(loaded);
            var savedJson = loaded.DocumentJson;
            var savedLibrary = loaded.LibraryJson;
            reopened.Description.Locales[0].Blocks[0].Source += " ไทย: เพิ่มเกจให้ทีม";
            StudioDraftRepository.Edit(loaded, reopened, reopenedLibrary, "Pending Thai prose");
            var pendingJson = loaded.DocumentJson;
            loaded.DocumentJson = savedJson; loaded.LibraryJson = savedLibrary;
            var recovered = StudioDraftRepository.Recover(loaded, out var recoveryMessage);
            Check(recovered && loaded.DocumentJson == pendingJson, "Recovery reconstructs unsaved Thai text from its atomic on-disk journal.");
            StudioDraftRepository.TryRead(loaded, out reopened, out reopenedLibrary, out _);
            var textChanged = StudioCompiler.Compile(reopened, reopenedLibrary);
            Check(textChanged.Success && textChanged.Hashes.Gameplay == changed.Hashes.Gameplay && textChanged.Hashes.Text != changed.Hashes.Text,
                "Thai prose edits do not alter the gameplay identity.");
            StudioDraftRepository.Save(loaded);

            var duplicate = StudioDraftRepository.Duplicate(loaded);
            StudioDraftRepository.TryRead(duplicate, out var copy, out var copyLibrary, out _);
            Check(copy.Id != reopened.Id && !copy.Conditions.Select(n => n.Id).Intersect(reopened.Conditions.Select(n => n.Id)).Any() && StudioCompiler.Compile(copy, copyLibrary).Success,
                "Asset duplication remaps local identities and keeps external snippet/keyword references valid.");

            var bundle = StudioDraftRepository.ApplyTestBundle(loaded, textChanged);
            Check(bundle.UseStudioDefinition && bundle.ResolveDefinition().Passive.Support.OpeningTeamGauge == 0,
                "Test activation uses the compiled generic reaction instead of specialized opening support.");
            var workingBundle = bundle.CompiledDefinitionJson;
            reopened.Description.Locales[0].Blocks[0].Source += " {{term:keyword.missing}}";
            StudioDraftRepository.Edit(loaded, reopened, reopenedLibrary, "Invalid source preservation");
            StudioDraftRepository.Save(loaded);
            StudioDraftRepository.TryRead(loaded, out var invalid, out var invalidLibrary, out _);
            Check(invalid.Description.Locales[0].Blocks[0].Source.EndsWith("{{term:keyword.missing}}") && !StudioCompiler.Compile(invalid, invalidLibrary).Success,
                "Broken text references survive explicit save/reopen with navigable diagnostics.");
            var blocked = false;
            try { StudioDraftRepository.ApplyTestBundle(loaded, textChanged); } catch (InvalidOperationException) { blocked = true; }
            Check(blocked && bundle.CompiledDefinitionJson == workingBundle && bundle.UseStudioDefinition,
                "Invalid or stale activation does not overwrite the last working bundle.");
            invalid.SchemaVersion = 99;
            StudioDraftRepository.Edit(loaded, invalid, invalidLibrary, "Future schema preservation");
            var futureJson = loaded.DocumentJson;
            var migration = StudioDraftRepository.TryRead(loaded, out _, out _, out var migrationError);
            Check(!migration && migrationError != null && loaded.DocumentJson == futureJson && bundle.CompiledDefinitionJson == workingBundle,
                "Unsupported migration preserves both source and last working compiled output.");

            // Restore valid test authoring without deleting any verification evidence.
            var restored = StudioDocumentService.Clone(document);
            StudioDraftRepository.Edit(loaded, restored, library, "Restore valid verification draft"); StudioDraftRepository.Save(loaded);
            StudioDraftRepository.RevertTestBundle(bundle);
            Check(!bundle.UseStudioDefinition && bundle.ResolveDefinition().Passive.Support.OpeningTeamGauge == document.LegacyCharacter.Passive.Support.OpeningTeamGauge,
                "Test content can return to its frozen legacy source mode.");
            var run = StudioSimulation.Run(restored, library, 12345);
            Check(run.MatchesLegacy && run.Trace.Any(t => t.Fired), "Unity uses the production-engine fixture and actual passive trace with baseline parity.");
            var scenario = StudioScenarioAsset.Save(loaded, run);
            var scenarioPath = AssetDatabase.GetAssetPath(scenario);
            AssetDatabase.ImportAsset(scenarioPath, ImportAssetOptions.ForceUpdate);
            scenario = AssetDatabase.LoadAssetAtPath<StudioScenarioAsset>(scenarioPath);
            Check(scenario.Replay(out var result, out var replayMessage) && result.RngState == run.Battle.RngState,
                "Saved scenario reimports and replays its exact command/state/RNG into the expected production result.");
            Check(HashFile(sourcePath) == sourceHash, "The registered production CharacterObject bytes remain unchanged.");
            Check(SceneManager.GetActiveScene() == scene && scene.isDirty == sceneDirty && !EditorApplication.isPlaying,
                "Authoring, test activation and simulation do not load/edit the battle scene or enter Play Mode.");
            var saveUndo = RunSaveUndoRecovery();
            report.Passed.AddRange(saveUndo.Passed);
            report.Failed.AddRange(saveUndo.Failed);
            var path = Path.GetFullPath("Artifacts/CharacterCreatorStudio/M1/authoring-evidence.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonConvert.SerializeObject(report, Formatting.Indented));
            return report;
        }

        /// <summary>Exercises the same-frame edit/save boundary without flushing between those calls.</summary>
        public static StudioAuthoringEvidence RunSaveUndoRecovery()
        {
            var report = new StudioAuthoringEvidence { UnityVersion = Application.unityVersion, CapturedUtc = DateTime.UtcNow.ToString("O") };
            void Check(bool condition, string message) { (condition ? report.Passed : report.Failed).Add(message); }
            var source = Array.Find(CharacterObjectRegistrySO.LoadAll(), c => c != null && c.DefinitionId == "fighter.kaeya");
            if (source == null) throw new InvalidOperationException("Kaeya is missing from the registered roster.");
            var sourcePath = AssetDatabase.GetAssetPath(source);
            var sourceHash = HashFile(sourcePath);
            var scene = SceneManager.GetActiveScene();
            var sceneDirty = scene.isDirty;
            var probe = StudioDraftRepository.Create(source);
            report.VerificationDraft = AssetDatabase.GetAssetPath(probe);
            StudioDraftRepository.TryRead(probe, out var document, out var library, out var readError);
            if (readError != null) throw new InvalidOperationException(readError);
            var before = probe.DocumentJson;
            Undo.IncrementCurrentGroup();
            document.Name += " · same-frame save verification";
            StudioDraftRepository.Edit(probe, document, library, "Same-frame Studio save verification");
            // Do not flush here: Save itself must establish the correct baseline boundary.
            StudioDraftRepository.Save(probe);
            var savedJson = probe.DocumentJson;
            var savedLibrary = probe.LibraryJson;
            var diskRevision = probe.SavedRevision;
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Check(probe.DocumentJson == before, "Same-frame edit/save Undo restores the prior canonical document.");
            Check(probe.SavedRevision == diskRevision, "Undo retains the actual saved revision instead of restoring an obsolete baseline.");
            Check(StudioDraftRepository.Revision(probe.DocumentJson, probe.LibraryJson) != probe.SavedRevision,
                "Undo after Save correctly reports an unsaved draft.");
            StudioDraftRepository.WriteRecovery(probe);
            // Reconstruct the persisted state to model losing the unsaved Undo after a restart.
            probe.DocumentJson = savedJson;
            probe.LibraryJson = savedLibrary;
            probe.SavedRevision = diskRevision;
            var recovered = StudioDraftRepository.Recover(probe, out var recoveryMessage);
            Check(recovered && probe.DocumentJson == before,
                "Recovery restores the unsaved Undo against the current disk revision: " + recoveryMessage);
            Check(HashFile(sourcePath) == sourceHash, "The focused save/Undo probe preserves the production character bytes.");
            Check(SceneManager.GetActiveScene() == scene && scene.isDirty == sceneDirty && !EditorApplication.isPlaying,
                "The focused save/Undo probe leaves the current scene and Play Mode unchanged.");
            StudioDraftRepository.Save(probe);
            Undo.ClearUndo(probe);
            var path = Path.GetFullPath("Artifacts/CharacterCreatorStudio/M1/same-frame-undo-after-fix.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonConvert.SerializeObject(report, Formatting.Indented));
            return report;
        }

        private static string HashFile(string path)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path)));
        }
    }
}
#endif
