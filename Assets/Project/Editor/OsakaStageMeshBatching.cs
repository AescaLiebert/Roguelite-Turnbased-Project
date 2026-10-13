using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FightingAllstar.EditorTools
{
    public static partial class OsakaBattleStageBuilder
    {
        private sealed class StreetMeshBatch
        {
            public string Name;
            public string MergeGroup;
            public Transform Parent;
            public readonly List<MeshFilter> Sources = new List<MeshFilter>();
            public Bounds Bounds;
            public bool HasBounds;
            public float MaxHorizontalSpan;

            public void Add(IEnumerable<MeshFilter> filters)
            {
                foreach (var filter in filters)
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (filter.sharedMesh == null || renderer == null || !renderer.enabled ||
                        !renderer.gameObject.activeInHierarchy || renderer.sharedMaterials.Length == 0 ||
                        renderer.sharedMaterials.Any(m => m == null)) continue;
                    Sources.Add(filter);
                    if (HasBounds) Bounds.Encapsulate(renderer.bounds);
                    else { Bounds = renderer.bounds; HasBounds = true; }
                }
            }

            public HashSet<Material> Materials()
            {
                return new HashSet<Material>(Sources.SelectMany(f => f.GetComponent<MeshRenderer>().sharedMaterials));
            }
        }

        /// <summary>
        /// Save the editable street, then replace its thousands of small submissions with
        /// local material batches. Lights and particles survive; source meshes remain in the
        /// authoring prefab and are removed from the optimized runtime prefab.
        /// </summary>
        private static void BatchStageMeshes(Transform root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            // Preserve the complete individual-piece model before any renderer is disabled.
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, Art + "/Prefabs/OsakaNightAuthoring.prefab");
            var sourceCount = root.GetComponentsInChildren<MeshRenderer>().Count(r => r.enabled);
            var batches = CollectStreetMeshBatches(root);
            var otherRenderers = root.GetComponentsInChildren<Renderer>().Count(r => r.enabled && !(r is MeshRenderer));
            MergeNearbyStreetBatches(batches, Mathf.Max(1, 238 - otherRenderers));

            var meshSerial = 0;
            var removedSourceObjects = 0;
            foreach (var batch in batches)
            {
                if (batch.Sources.Count == 0) continue;
                var unitObject = new GameObject("Render batches · " + batch.Name);
                var unit = unitObject.transform;
                unit.SetParent(batch.Parent, false);
                unit.position = batch.Bounds.center;
                unit.localRotation = Quaternion.identity;
                unit.localScale = Vector3.one;
                var materialGroups = batch.Sources.SelectMany(filter =>
                {
                    var materials = filter.GetComponent<MeshRenderer>().sharedMaterials;
                    // Every submesh retains its material, including future authored props.
                    return Enumerable.Range(0, filter.sharedMesh.subMeshCount).Select(index => new
                    {
                        Filter = filter,
                        SubMesh = index,
                        Material = materials[Mathf.Min(index, materials.Length - 1)]
                    });
                }).GroupBy(source => source.Material).OrderBy(group =>
                    AssetDatabase.GetAssetPath(group.Key) + "/" + group.Key.name, StringComparer.Ordinal);

                foreach (var materialGroup in materialGroups)
                {
                    var sources = materialGroup.ToArray();
                    var mesh = new Mesh { name = "Osaka batch · " + batch.Name + " · " + materialGroup.Key.name };
                    if (sources.Sum(source => source.Filter.sharedMesh.vertexCount) > 65535)
                        mesh.indexFormat = IndexFormat.UInt32;
                    var combine = sources.Select(source => new CombineInstance
                    {
                        mesh = source.Filter.sharedMesh,
                        subMeshIndex = source.SubMesh,
                        transform = unit.worldToLocalMatrix * source.Filter.transform.localToWorldMatrix
                    }).ToArray();
                    mesh.CombineMeshes(combine, true, true, false);
                    mesh.RecalculateBounds();
                    var path = Art + "/Meshes/Batched_" + (meshSerial++).ToString("D3") + ".asset";
                    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (existing != null)
                    {
                        EditorUtility.CopySerialized(mesh, existing);
                        UnityEngine.Object.DestroyImmediate(mesh);
                        mesh = existing;
                        EditorUtility.SetDirty(mesh);
                    }
                    else AssetDatabase.CreateAsset(mesh, path);

                    var combinedObject = new GameObject("Combined · " + materialGroup.Key.name);
                    combinedObject.transform.SetParent(unit, false);
                    combinedObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var combinedRenderer = combinedObject.AddComponent<MeshRenderer>();
                    combinedRenderer.sharedMaterial = materialGroup.Key;
                    var glowing = IsStreetGlowMaterial(materialGroup.Key);
                    combinedRenderer.shadowCastingMode = glowing ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    combinedRenderer.receiveShadows = !glowing;
                    combinedObject.isStatic = true;
                }
                foreach (var source in batch.Sources)
                {
                    // The individual pieces were saved above in OsakaNightAuthoring.prefab.
                    // Remove their leaf objects from the runtime prefab after combining so
                    // they use neither memory nor transform/renderer overhead at runtime.
                    var sourceObject = source.gameObject;
                    var components = sourceObject.GetComponents<Component>();
                    if (sourceObject.transform.childCount == 0 && components.Length == 3 &&
                        components.Any(component => component is MeshFilter) &&
                        components.Any(component => component is MeshRenderer))
                    {
                        UnityEngine.Object.DestroyImmediate(sourceObject);
                        removedSourceObjects++;
                    }
                    else
                    {
                        source.GetComponent<MeshRenderer>().enabled = false;
                        sourceObject.isStatic = false;
                    }
                }
            }
            AssetDatabase.SaveAssets();
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, Art + "/Prefabs/OsakaNightStage.prefab");
            var active = root.GetComponentsInChildren<Renderer>().Count(r => r.enabled && r.gameObject.activeInHierarchy);
            Debug.Log("OSAKA MESH BATCHING: " + sourceCount + " source meshes → " + active +
                " active renderers in " + batches.Count + " local groups; removed " + removedSourceObjects +
                " hidden source objects from the runtime prefab; editable authoring prefab preserved.");
        }

        private static bool IsStreetGlowMaterial(Material material)
        {
            return material.IsKeywordEnabled("_EMISSION") ||
                (material.shader != null && material.shader.name.IndexOf("OsakaNight/Neon", StringComparison.Ordinal) >= 0);
        }

        private static List<StreetMeshBatch> CollectStreetMeshBatches(Transform root)
        {
            var batches = new List<StreetMeshBatch>();
            foreach (Transform section in root)
            {
                if (section.name.StartsWith("02", StringComparison.Ordinal))
                {
                    foreach (Transform shop in section)
                    {
                        var alley = shop.name.StartsWith("East alley shop", StringComparison.Ordinal) ? "East alley" :
                            shop.name.StartsWith("West alley shop", StringComparison.Ordinal) ? "West alley" : shop.name;
                        AddStreetBatch(batches, shop.name, "Architecture/" + alley, section,
                            shop.GetComponentsInChildren<MeshFilter>(), 30f);
                    }
                }
                else if (section.name.StartsWith("04", StringComparison.Ordinal))
                {
                    // Three adjacent towers share a small skyline sector; the whole city
                    // is never one mesh with a circumference-wide culling bound.
                    StreetMeshBatch sector = null;
                    var sectorNumber = 0;
                    var sectorTowerCount = 0;
                    foreach (Transform tower in section)
                    {
                        if (!tower.name.StartsWith("Distant city block", StringComparison.Ordinal)) continue;
                        var filters = tower.GetComponentsInChildren<MeshFilter>();
                        var towerBounds = StreetSourceBounds(filters);
                        var candidate = sector == null ? towerBounds : sector.Bounds;
                        candidate.Encapsulate(towerBounds);
                        if (sector == null || sectorTowerCount >= 3 || sector.Sources.Count > 0 &&
                            (candidate.size.x > 34f || candidate.size.z > 34f))
                        {
                            sector = AddStreetBatch(batches, "Skyline sector " + sectorNumber++, "Skyline", section,
                                Array.Empty<MeshFilter>(), 34f);
                            sectorTowerCount = 0;
                        }
                        sector.Add(filters);
                        sectorTowerCount++;
                    }
                    AddStreetBatch(batches, "Alley service poles and cables", "Service lines", section,
                        section.GetComponents<MeshFilter>().Concat(section.Cast<Transform>()
                            .Where(child => child.GetComponent<MeshFilter>() != null).Select(child => child.GetComponent<MeshFilter>())), 50f);
                }
                else if (section.name.StartsWith("Street life", StringComparison.Ordinal))
                {
                    var cells = new Dictionary<Vector2Int, StreetMeshBatch>();
                    foreach (Transform propGroup in section)
                    {
                        // Crowd members are assigned to a sidewalk cell individually;
                        // benches/bikes/trees stay intact inside their existing prop group.
                        var props = propGroup.name.StartsWith("Arcade crowd", StringComparison.Ordinal)
                            ? propGroup.Cast<Transform>() : new[] { propGroup };
                        foreach (var prop in props)
                        {
                            var filters = prop.GetComponentsInChildren<MeshFilter>();
                            if (filters.Length == 0) continue;
                            var local = section.InverseTransformPoint(StreetSourceBounds(filters).center);
                            var cell = new Vector2Int(Mathf.FloorToInt((local.x - Center.x) / 18f), Mathf.FloorToInt(local.z / 18f));
                            StreetMeshBatch batch;
                            if (!cells.TryGetValue(cell, out batch))
                            {
                                batch = AddStreetBatch(batches, "Sidewalk cell " + cell.x + "," + cell.y,
                                    "Street props", section, Array.Empty<MeshFilter>(), 30f);
                                cells.Add(cell, batch);
                            }
                            batch.Add(filters);
                        }
                    }
                }
                else if (section.name.StartsWith("05", StringComparison.Ordinal))
                {
                    foreach (Transform lamp in section)
                        AddStreetBatch(batches, lamp.name, "Lighting/" + lamp.name, lamp,
                            lamp.GetComponentsInChildren<MeshFilter>(), 18f);
                }
                else AddStreetBatch(batches, section.name, "Section/" + section.name, section,
                    section.GetComponentsInChildren<MeshFilter>(), 80f);
            }
            batches.RemoveAll(batch => batch.Sources.Count == 0);
            return batches;
        }

        private static StreetMeshBatch AddStreetBatch(List<StreetMeshBatch> batches, string name,
            string mergeGroup, Transform parent, IEnumerable<MeshFilter> sources, float maxSpan)
        {
            var batch = new StreetMeshBatch { Name = name, MergeGroup = mergeGroup, Parent = parent, MaxHorizontalSpan = maxSpan };
            batch.Add(sources);
            batches.Add(batch);
            return batch;
        }

        private static Bounds StreetSourceBounds(IEnumerable<MeshFilter> filters)
        {
            var found = false;
            var bounds = new Bounds();
            foreach (var filter in filters)
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (found) bounds.Encapsulate(renderer.bounds);
                else { bounds = renderer.bounds; found = true; }
            }
            return bounds;
        }

        private static void MergeNearbyStreetBatches(List<StreetMeshBatch> batches, int rendererTarget)
        {
            var materials = batches.ToDictionary(batch => batch, batch => batch.Materials());
            var rendererCount = materials.Values.Sum(set => set.Count);
            while (rendererCount > rendererTarget)
            {
                StreetMeshBatch first = null, second = null;
                var bestCost = float.MaxValue;
                var savings = 0;
                for (var i = 0; i < batches.Count; i++)
                    for (var j = i + 1; j < batches.Count; j++)
                    {
                        var a = batches[i]; var b = batches[j];
                        if (a.MergeGroup != b.MergeGroup) continue;
                        var common = materials[a].Count(material => materials[b].Contains(material));
                        if (common == 0) continue;
                        var bounds = a.Bounds; bounds.Encapsulate(b.Bounds);
                        var limit = Mathf.Min(a.MaxHorizontalSpan, b.MaxHorizontalSpan);
                        if (bounds.size.x > limit || bounds.size.z > limit) continue;
                        var cost = Vector3.SqrMagnitude(a.Bounds.center - b.Bounds.center) / common;
                        if (cost >= bestCost) continue;
                        first = a; second = b; bestCost = cost; savings = common;
                    }
                if (first == null) break;
                // Adjacent alley shops may share a block; all major shops remain separate.
                first.Name = first.MergeGroup + " local cluster";
                first.Add(second.Sources);
                materials[first].UnionWith(materials[second]);
                materials.Remove(second); batches.Remove(second);
                rendererCount -= savings;
            }
        }
    }
}
