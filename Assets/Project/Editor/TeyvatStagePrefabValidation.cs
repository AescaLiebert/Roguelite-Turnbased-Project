using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FightingAllstar.EnvironmentArt;
using Object = UnityEngine.Object;

namespace FightingAllstar.EditorTools
{
    public static partial class TeyvatStageLab
    {
        [Serializable] sealed class PrefabStandaloneReport
        {
            public string schema = "TG_WP07_PrefabStandalone_v1";
            public bool passed, emptySceneInstantiation, exactlyOneGlobalsMeadowBinding;
            public bool noReviewObjects, materialsPresent, scriptsPresent, stageTerrainReferences;
            public bool serializedCanonicalReference, bindingReferences, translationOnlyTransform;
            public bool authoredOriginMatches, ownedDeepCopy, unchangedApplyStable, translationKeepsClone;
            public bool disableRestoresCanonical, disabledCloneDestroyed, reenableCreatesOneClone;
            public bool rotationRejectedWithoutMutation, canonicalMemoryUnchanged, canonicalFileUnchanged;
            public bool activeSceneRestored, temporarySceneClosed, originalGlobalsRestored;
            public int rootObjects, globals, meadowRenderers, stageBindings, cameras, lights, tours, proxies;
            public int terrains, terrainColliders, missingScripts, missingMaterialSlots, legacyGrassInstances;
            public int cells, placements, initialCloneCount, stableCloneCount, reenabledCloneCount;
            public int initialRebaseCount, stableRebaseCount;
            public string canonicalPath, terrainPath, colliderTerrainPath;
            public string canonicalMemoryHashBefore, canonicalMemoryHashAfter;
            public string canonicalFileHashBefore, canonicalFileHashAfter;
            public Vector3 requestedTranslation, actualTranslation;
            public float colorBoundsMaximumError;
            public PrefabPlacementComparison translated, returned;
            public string[] errors;
            public string method = "Instantiate the saved environment prefab into an additive empty editor scene without review dependencies. Compare every persisted meadow placement matrix, cell bound and data bound against canonical data after XYZ translation and return; verify shared mesh/material identities, stable no-change calls, disable/re-enable ownership, raw serialized references and unsupported rotation. Fingerprint canonical data in memory and on disk. Close without saving and restore the active scene and original shader globals.";
            public string limitations = "Structural and CPU binding validation only. No camera render, GPU coverage, continuous camera clearance, gameplay collision, animated combat, whole-game render-pipeline migration or player-build performance is established by this test. The matrix/bounds tolerance of 0.0001 is a starting numerical tolerance in world metres, not an art or performance budget.";
        }

        [Serializable] sealed class PrefabPlacementComparison
        {
            public bool passed, independentArrays, sharedMeshMaterialReferences, metadataMatches;
            public int cells, placements;
            public float maximumMatrixError, maximumBoundsError;
        }

        static bool ValidatePrefabStandalone()
        {
            var report = new PrefabStandaloneReport();
            var errors = new List<string>();
            var previousScene = SceneManager.GetActiveScene();
            var originalGlobals = globals;
            var temporaryScene = default(Scene);
            TeyvatMeadowData canonical = null;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StagePrefab);
                if (prefab == null) throw new InvalidOperationException("Saved stage prefab is missing: " + StagePrefab);
                var savedBinding = prefab.GetComponent<TeyvatStageBinding>();
                var savedMeadow = prefab.GetComponentInChildren<TeyvatMeadowRenderer>(true);
                if (savedBinding == null || savedMeadow == null)
                    throw new InvalidOperationException("Saved stage prefab has no stage binding or meadow renderer.");

                canonical = savedBinding.canonicalMeadowData;
                if (canonical == null) throw new InvalidOperationException("Saved stage binding has no canonical meadow asset.");
                report.canonicalPath = AssetDatabase.GetAssetPath(canonical);
                var serializedMeadow = new SerializedObject(savedMeadow);
                var savedDataProperty = serializedMeadow.FindProperty("data");
                report.serializedCanonicalReference = savedDataProperty != null
                    && savedDataProperty.objectReferenceValue == canonical
                    && report.canonicalPath == Stage + "/Data/TG_StageMeadowPlacement.asset";
                report.canonicalMemoryHashBefore = PrefabMeadowFingerprint(canonical);
                report.canonicalFileHashBefore = PrefabFileFingerprint(report.canonicalPath);

                temporaryScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(temporaryScene);
                var instance = PrefabUtility.InstantiatePrefab(prefab, temporaryScene) as GameObject;
                if (instance == null) throw new InvalidOperationException("Could not instantiate the saved stage prefab.");
                report.rootObjects = temporaryScene.GetRootGameObjects().Length;
                report.emptySceneInstantiation = instance.scene == temporaryScene && report.rootObjects == 1;

                var globalOwners = instance.GetComponentsInChildren<TeyvatMaterialGlobals>(true);
                var meadows = instance.GetComponentsInChildren<TeyvatMeadowRenderer>(true);
                var bindings = instance.GetComponentsInChildren<TeyvatStageBinding>(true);
                report.globals = globalOwners.Length;
                report.meadowRenderers = meadows.Length;
                report.stageBindings = bindings.Length;
                report.exactlyOneGlobalsMeadowBinding = report.globals == 1 && report.meadowRenderers == 1 && report.stageBindings == 1;
                report.cameras = instance.GetComponentsInChildren<Camera>(true).Length;
                report.lights = instance.GetComponentsInChildren<Light>(true).Length;
                report.tours = instance.GetComponentsInChildren<TeyvatStageTour>(true).Length;
                var transforms = instance.GetComponentsInChildren<Transform>(true);
                report.proxies = transforms.Count(t => t.name.StartsWith("Proxy_", StringComparison.OrdinalIgnoreCase));
                report.noReviewObjects = report.cameras == 0 && report.lights == 0 && report.tours == 0 && report.proxies == 0;
                report.missingScripts = transforms.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                report.scriptsPresent = report.missingScripts == 0;
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    var slots = renderer.sharedMaterials;
                    report.missingMaterialSlots += slots.Length == 0 ? 1 : slots.Count(m => m == null || m.shader == null);
                }
                // Instanced meadow materials do not appear on a Renderer component.
                foreach (var species in canonical.species ?? Array.Empty<TeyvatMeadowData.Species>())
                {
                    if (species == null) { report.missingMaterialSlots++; continue; }
                    var nearSlots = species.materialsLOD0 ?? Array.Empty<Material>();
                    var farSlots = species.materialsLOD1 ?? Array.Empty<Material>();
                    report.missingMaterialSlots += nearSlots.Length == 0 ? 1 : nearSlots.Count(m => m == null || m.shader == null);
                    report.missingMaterialSlots += farSlots.Count(m => m == null || m.shader == null);
                }
                var terrains = instance.GetComponentsInChildren<Terrain>(true);
                var terrainColliders = instance.GetComponentsInChildren<TerrainCollider>(true);
                report.terrains = terrains.Length;
                report.terrainColliders = terrainColliders.Length;
                report.missingMaterialSlots += terrains.Count(t => t.materialTemplate == null || t.materialTemplate.shader == null);
                report.materialsPresent = report.missingMaterialSlots == 0;
                if (terrains.Length == 1 && terrainColliders.Length == 1)
                {
                    report.terrainPath = AssetDatabase.GetAssetPath(terrains[0].terrainData);
                    report.colliderTerrainPath = AssetDatabase.GetAssetPath(terrainColliders[0].terrainData);
                    report.stageTerrainReferences = terrains[0].terrainData != null
                        && terrains[0].terrainData == terrainColliders[0].terrainData
                        && report.terrainPath == Stage + "/Terrain/TG_StageLandscape.asset"
                        && report.colliderTerrainPath == report.terrainPath;
                }
                if (!report.exactlyOneGlobalsMeadowBinding)
                    throw new InvalidOperationException("Standalone prefab must contain exactly one globals owner, meadow renderer and stage binding.");

                var binding = bindings[0];
                var meadow = meadows[0];
                var stageGlobals = globalOwners[0];
                report.legacyGrassInstances = stageGlobals.grassInstances == null ? 0 : stageGlobals.grassInstances.Length;
                report.bindingReferences = binding.gameObject == instance && binding.enabled
                    && binding.canonicalMeadowData == canonical && binding.meadow == meadow
                    && binding.globals == stageGlobals && meadow.globals == stageGlobals
                    && stageGlobals.transform != instance.transform && stageGlobals.transform.IsChildOf(instance.transform)
                    && report.legacyGrassInstances == 0;
                report.translationOnlyTransform = binding.TransformSupported;
                report.authoredOriginMatches = Vector3.Distance(instance.transform.position, binding.authoredWorldOrigin) < .0001f;
                binding.ApplyBinding();
                var initialData = binding.RuntimeMeadowData;
                if (initialData == null) throw new InvalidOperationException("Binding did not create its owned meadow data.");
                report.initialCloneCount = binding.cloneCreationCount;
                report.initialRebaseCount = binding.rebaseCount;
                report.cells = binding.boundCellCount;
                report.placements = binding.boundPlacementCount;
                var baseline = ComparePrefabPlacements(canonical, initialData, binding.WorldTranslation);
                report.ownedDeepCopy = initialData != canonical && meadow.data == initialData
                    && report.initialCloneCount == 1 && !EditorUtility.IsPersistent(initialData) && baseline.passed;

                for (int i = 0; i < 6; i++) binding.ApplyBinding();
                report.stableCloneCount = binding.cloneCreationCount;
                report.stableRebaseCount = binding.rebaseCount;
                report.unchangedApplyStable = binding.RuntimeMeadowData == initialData
                    && report.stableCloneCount == report.initialCloneCount
                    && report.stableRebaseCount == report.initialRebaseCount;

                var originalPosition = instance.transform.position;
                report.requestedTranslation = new Vector3(37.25f, 3.75f, -21.5f);
                instance.transform.position = binding.authoredWorldOrigin + report.requestedTranslation;
                binding.ApplyBinding();
                report.actualTranslation = binding.WorldTranslation;
                report.translated = ComparePrefabPlacements(canonical, binding.RuntimeMeadowData, report.actualTranslation);
                report.translationKeepsClone = binding.RuntimeMeadowData == initialData && binding.cloneCreationCount == report.initialCloneCount;
                var expectedColorBounds = new Vector4(binding.authoredColorBounds.x + report.actualTranslation.x,
                    binding.authoredColorBounds.y + report.actualTranslation.z,
                    binding.authoredColorBounds.z, binding.authoredColorBounds.w);
                report.colorBoundsMaximumError = PrefabVectorError(expectedColorBounds, stageGlobals.colorBounds);
                instance.transform.position = originalPosition;
                binding.ApplyBinding();
                report.returned = ComparePrefabPlacements(canonical, binding.RuntimeMeadowData, binding.WorldTranslation);
                expectedColorBounds = new Vector4(binding.authoredColorBounds.x + binding.WorldTranslation.x,
                    binding.authoredColorBounds.y + binding.WorldTranslation.z,
                    binding.authoredColorBounds.z, binding.authoredColorBounds.w);
                report.colorBoundsMaximumError = Mathf.Max(report.colorBoundsMaximumError, PrefabVectorError(expectedColorBounds, stageGlobals.colorBounds));

                var releasedClone = binding.RuntimeMeadowData;
                var creationsBeforeDisable = binding.cloneCreationCount;
                binding.enabled = false;
                report.disableRestoresCanonical = meadow.data == canonical && binding.RuntimeMeadowData == null;
                report.disabledCloneDestroyed = releasedClone == null;
                binding.enabled = true;
                binding.ApplyBinding();
                report.reenabledCloneCount = binding.cloneCreationCount;
                report.reenableCreatesOneClone = report.reenabledCloneCount == creationsBeforeDisable + 1
                    && binding.RuntimeMeadowData != null && binding.RuntimeMeadowData != canonical
                    && !ReferenceEquals(binding.RuntimeMeadowData, releasedClone)
                    && meadow.data == binding.RuntimeMeadowData
                    && ComparePrefabPlacements(canonical, binding.RuntimeMeadowData, binding.WorldTranslation).passed;

                var rotation = instance.transform.rotation;
                var requestedRotation = Quaternion.Euler(0, 17, 0);
                var cloneBeforeRotation = binding.RuntimeMeadowData;
                var creationsBeforeRotation = binding.cloneCreationCount;
                var rebasesBeforeRotation = binding.rebaseCount;
                instance.transform.rotation = requestedRotation;
                binding.ApplyBinding();
                binding.ApplyBinding();
                report.rotationRejectedWithoutMutation = !binding.TransformSupported
                    && Quaternion.Angle(instance.transform.rotation, requestedRotation) < .001f
                    && binding.RuntimeMeadowData == cloneBeforeRotation
                    && binding.cloneCreationCount == creationsBeforeRotation && binding.rebaseCount == rebasesBeforeRotation;
                instance.transform.rotation = rotation;
                binding.ApplyBinding();

                report.canonicalMemoryHashAfter = PrefabMeadowFingerprint(canonical);
                report.canonicalFileHashAfter = PrefabFileFingerprint(report.canonicalPath);
                report.canonicalMemoryUnchanged = report.canonicalMemoryHashBefore == report.canonicalMemoryHashAfter;
                report.canonicalFileUnchanged = report.canonicalFileHashBefore == report.canonicalFileHashAfter;
            }
            catch (Exception exception) { errors.Add(exception.ToString()); }
            finally
            {
                try
                {
                    if (temporaryScene.IsValid() && temporaryScene.isLoaded)
                        EditorSceneManager.CloseScene(temporaryScene, true);
                    report.temporarySceneClosed = !temporaryScene.IsValid() || !temporaryScene.isLoaded;
                }
                catch (Exception exception) { errors.Add("Close temporary scene: " + exception); }
                try
                {
                    if (previousScene.IsValid() && previousScene.isLoaded)
                        SceneManager.SetActiveScene(previousScene);
                    report.activeSceneRestored = SceneManager.GetActiveScene() == previousScene;
                }
                catch (Exception exception) { errors.Add("Restore active scene: " + exception); }
                try
                {
                    if (originalGlobals != null)
                    {
                        originalGlobals.Apply();
                        report.originalGlobalsRestored = true;
                    }
                }
                catch (Exception exception) { errors.Add("Restore original shader globals: " + exception); }
                try
                {
                    if (canonical != null)
                    {
                        report.canonicalMemoryHashAfter = PrefabMeadowFingerprint(canonical);
                        report.canonicalFileHashAfter = PrefabFileFingerprint(report.canonicalPath);
                        report.canonicalMemoryUnchanged = report.canonicalMemoryHashBefore == report.canonicalMemoryHashAfter;
                        report.canonicalFileUnchanged = report.canonicalFileHashBefore == report.canonicalFileHashAfter;
                    }
                }
                catch (Exception exception) { errors.Add("Canonical post-cleanup fingerprint: " + exception); }
            }
            report.passed = errors.Count == 0 && report.emptySceneInstantiation && report.exactlyOneGlobalsMeadowBinding
                && report.noReviewObjects && report.materialsPresent && report.scriptsPresent && report.stageTerrainReferences
                && report.serializedCanonicalReference && report.bindingReferences && report.translationOnlyTransform
                && report.authoredOriginMatches && report.ownedDeepCopy && report.unchangedApplyStable && report.translationKeepsClone
                && report.disableRestoresCanonical && report.disabledCloneDestroyed && report.reenableCreatesOneClone
                && report.rotationRejectedWithoutMutation && report.canonicalMemoryUnchanged && report.canonicalFileUnchanged
                && report.translated != null && report.translated.passed && report.returned != null && report.returned.passed
                && report.colorBoundsMaximumError <= .0001f && report.temporarySceneClosed
                && report.activeSceneRestored && report.originalGlobalsRestored;
            report.errors = errors.ToArray();
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "PrefabValidation.json"), JsonUtility.ToJson(report, true));
            return report.passed;
        }

        static PrefabPlacementComparison ComparePrefabPlacements(TeyvatMeadowData source, TeyvatMeadowData target, Vector3 translation)
        {
            var result = new PrefabPlacementComparison { independentArrays = true, sharedMeshMaterialReferences = true, metadataMatches = true };
            if (source == null || target == null || source == target) return result;
            var sourceSpecies = source.species ?? Array.Empty<TeyvatMeadowData.Species>();
            var targetSpecies = target.species ?? Array.Empty<TeyvatMeadowData.Species>();
            var sourceCells = source.cells ?? Array.Empty<TeyvatMeadowData.Cell>();
            var targetCells = target.cells ?? Array.Empty<TeyvatMeadowData.Cell>();
            if (sourceSpecies.Length != targetSpecies.Length || sourceCells.Length != targetCells.Length) return result;
            result.independentArrays = !ReferenceEquals(source.species, target.species) && !ReferenceEquals(source.cells, target.cells);
            result.metadataMatches = source.revision == target.revision && source.seed == target.seed && source.placementCount == target.placementCount;
            for (int i = 0; i < sourceSpecies.Length; i++)
            {
                var a = sourceSpecies[i]; var b = targetSpecies[i];
                if (a == null || b == null) { result.metadataMatches &= a == b; continue; }
                result.independentArrays &= !ReferenceEquals(a, b);
                result.metadataMatches &= a.name == b.name;
                result.sharedMeshMaterialReferences &= a.meshLOD0 == b.meshLOD0 && a.meshLOD1 == b.meshLOD1
                    && PrefabMaterialReferencesEqual(a.materialsLOD0, b.materialsLOD0)
                    && PrefabMaterialReferencesEqual(a.materialsLOD1, b.materialsLOD1);
                if (a.materialsLOD0 != null && a.materialsLOD0.Length > 0) result.independentArrays &= !ReferenceEquals(a.materialsLOD0, b.materialsLOD0);
                if (a.materialsLOD1 != null && a.materialsLOD1.Length > 0) result.independentArrays &= !ReferenceEquals(a.materialsLOD1, b.materialsLOD1);
            }
            result.maximumBoundsError = PrefabBoundsError(source.worldBounds, target.worldBounds, translation);
            for (int i = 0; i < sourceCells.Length; i++)
            {
                var a = sourceCells[i]; var b = targetCells[i];
                if (a == null || b == null) { result.metadataMatches &= a == b; continue; }
                result.cells++;
                result.independentArrays &= !ReferenceEquals(a, b);
                result.metadataMatches &= a.name == b.name;
                result.maximumBoundsError = Mathf.Max(result.maximumBoundsError, PrefabBoundsError(a.worldBounds, b.worldBounds, translation));
                var placementsA = a.placements ?? Array.Empty<TeyvatMeadowData.Placement>();
                var placementsB = b.placements ?? Array.Empty<TeyvatMeadowData.Placement>();
                if (placementsA.Length != placementsB.Length) { result.metadataMatches = false; continue; }
                if (placementsA.Length > 0) result.independentArrays &= !ReferenceEquals(a.placements, b.placements);
                for (int j = 0; j < placementsA.Length; j++)
                {
                    result.placements++;
                    var expected = placementsA[j].matrix;
                    expected.m03 += translation.x; expected.m13 += translation.y; expected.m23 += translation.z;
                    for (int component = 0; component < 16; component++)
                        result.maximumMatrixError = Mathf.Max(result.maximumMatrixError, PrefabScalarError(expected[component], placementsB[j].matrix[component]));
                    result.metadataMatches &= placementsA[j].speciesIndex == placementsB[j].speciesIndex && placementsA[j].phase.Equals(placementsB[j].phase);
                }
            }
            result.passed = result.independentArrays && result.sharedMeshMaterialReferences && result.metadataMatches
                && result.placements == source.placementCount && result.maximumMatrixError <= .0001f && result.maximumBoundsError <= .0001f;
            return result;
        }

        static bool PrefabMaterialReferencesEqual(Material[] a, Material[] b)
        {
            a = a ?? Array.Empty<Material>(); b = b ?? Array.Empty<Material>();
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        static float PrefabScalarError(float a, float b)
        {
            var value = Mathf.Abs(a - b);
            return float.IsNaN(value) || float.IsInfinity(value) ? float.PositiveInfinity : value;
        }
        static float PrefabVectorError(Vector4 a, Vector4 b)
        {
            return Mathf.Max(Mathf.Max(PrefabScalarError(a.x, b.x), PrefabScalarError(a.y, b.y)),
                Mathf.Max(PrefabScalarError(a.z, b.z), PrefabScalarError(a.w, b.w)));
        }
        static float PrefabBoundsError(Bounds source, Bounds target, Vector3 translation)
        {
            var expected = source.center + translation;
            return Mathf.Max(PrefabVectorError(new Vector4(expected.x, expected.y, expected.z, 0), new Vector4(target.center.x, target.center.y, target.center.z, 0)),
                PrefabVectorError(new Vector4(source.extents.x, source.extents.y, source.extents.z, 0), new Vector4(target.extents.x, target.extents.y, target.extents.z, 0)));
        }

        static string PrefabFileFingerprint(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new FileNotFoundException("Canonical meadow asset file is missing.", path);
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        static string PrefabMeadowFingerprint(TeyvatMeadowData data)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(data.revision ?? ""); writer.Write(data.seed); writer.Write(data.placementCount);
                PrefabWriteBounds(writer, data.worldBounds);
                var species = data.species ?? Array.Empty<TeyvatMeadowData.Species>();
                writer.Write(species.Length);
                foreach (var value in species)
                {
                    writer.Write(value != null); if (value == null) continue;
                    writer.Write(value.name ?? ""); writer.Write(value.meshLOD0 == null ? 0 : value.meshLOD0.GetInstanceID()); writer.Write(value.meshLOD1 == null ? 0 : value.meshLOD1.GetInstanceID());
                    foreach (var materials in new[] { value.materialsLOD0, value.materialsLOD1 })
                    {
                        writer.Write(materials == null ? -1 : materials.Length);
                        if (materials != null) foreach (var material in materials) writer.Write(material == null ? 0 : material.GetInstanceID());
                    }
                }
                var cells = data.cells ?? Array.Empty<TeyvatMeadowData.Cell>();
                writer.Write(cells.Length);
                foreach (var cell in cells)
                {
                    writer.Write(cell != null); if (cell == null) continue;
                    writer.Write(cell.name ?? ""); PrefabWriteBounds(writer, cell.worldBounds);
                    writer.Write(cell.placements == null ? -1 : cell.placements.Length);
                    if (cell.placements == null) continue;
                    foreach (var placement in cell.placements)
                    {
                        writer.Write(placement.speciesIndex); writer.Write(placement.phase);
                        for (int component = 0; component < 16; component++) writer.Write(placement.matrix[component]);
                    }
                }
                writer.Flush();
                using (var sha = SHA256.Create())
                    return BitConverter.ToString(sha.ComputeHash(stream.GetBuffer(), 0, checked((int)stream.Length))).Replace("-", "").ToLowerInvariant();
            }
        }
        static void PrefabWriteBounds(BinaryWriter writer, Bounds bounds)
        {
            writer.Write(bounds.center.x); writer.Write(bounds.center.y); writer.Write(bounds.center.z);
            writer.Write(bounds.extents.x); writer.Write(bounds.extents.y); writer.Write(bounds.extents.z);
        }
    }
}
