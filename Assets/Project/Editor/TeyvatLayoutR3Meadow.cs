using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FightingAllstar.EnvironmentArt;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FightingAllstar.EditorTools
{
    public static partial class TeyvatLayoutR3Lab
    {
        const int R3MeadowSeed = 20261021;
        const float R3RootTolerance = .08f;
        [Serializable] sealed class R3MeadowLayout { public float actorProxyHeight; public R3MeadowAnchor[] anchors; public R3MeadowShot[] shots; }
        [Serializable] sealed class R3MeadowAnchor { public string name; public Vector3 localPosition; }
        [Serializable] sealed class R3MeadowShot { public string name; public Vector3 position; }
        [Serializable] sealed class R3HeightReport
        {
            public string revision = "LayoutR3-user-height-direction";
            public string policy = "Grass is allowed throughout the battle field and character positions. Most grass targets 40-60 percent of the actual 3m review proxy (1.2-1.8m), lower near actors and taller in broad border clusters. The previous 0.35m short-cover cap is superseded. Clover is understory; flowers are accents. Actual highest terrain/mesh surface controls placement, with a 0.08m maximum root gap. Head visibility uses 2.5m targets, not a 1.6m torso prohibition.";
            public string measurement = "Height statistics use transformed actual LOD0 vertex positions, excluding stored animation bounds. Root tests sample actual root vertices against highest scene geometry. Values are starting art choices, not a player performance guarantee.";
            public int seed, candidates, placed, surfaceRejected, contactRejected, cameraRejected, densityRejected, budgetRejected, grassPlants, flowerPlants;
            public int[] speciesCounts;
            public float actorProxyHeight, requestedGrassMinMetres, requestedGrassMaxMetres, grassMinMetres, grassMaxMetres, grassAverageMetres, grassMinRatio, grassMaxRatio, maximumRootGap, maximumRootBurial;
            public bool rootContactPassed, grassRangePassed;
        }
        sealed class R3PlantSource
        {
            public Vector3[] vertices, roots;
            public Bounds rawBounds;
        }
        sealed class R3Surface
        {
            public Collider collider;
            public Bounds bounds;
            public bool temporary;
            public bool[] eligibleSlots;
            public int[] triangleEnds;
            public Terrain terrain;
            public float[,,] alpha;
        }

        static TeyvatMeadowRenderer BuildMeadow()
        {
            var owned = R3 + "/Meadow";
            Directory.CreateDirectory(owned + "/Meshes");
            Directory.CreateDirectory(output);
            AssetDatabase.Refresh();
            var sourceData = AssetDatabase.LoadAssetAtPath<TeyvatMeadowData>(Meadow + "/Data/TG_MeadowPlacement.asset");
            if (sourceData == null || sourceData.species == null || sourceData.species.Length != 8)
                throw new InvalidOperationException("The accepted eight-species WP04 meadow data is required for LayoutR3.");
            var metadata = JsonUtility.FromJson<R3MeadowLayout>(File.ReadAllText("Assets/Project/Art/Environment/TeyvatGrassland/FoundationR2/TG_Layout.json"));
            var proxyHeight = metadata.actorProxyHeight > 0 ? metadata.actorProxyHeight : 3;
            var report = new R3HeightReport { seed = R3MeadowSeed, actorProxyHeight = proxyHeight, requestedGrassMinMetres = proxyHeight * .4f, requestedGrassMaxMetres = proxyHeight * .6f, speciesCounts = new int[8], grassMinMetres = float.PositiveInfinity };
            var dataPath = owned + "/TG_MeadowPlacement.asset";
            var data = AssetDatabase.LoadAssetAtPath<TeyvatMeadowData>(dataPath);
            if (data == null) { data = ScriptableObject.CreateInstance<TeyvatMeadowData>(); AssetDatabase.CreateAsset(data, dataPath); }
            data.revision = "LayoutR3-tall-clustered-meadow"; data.seed = R3MeadowSeed;
            var sourceGeometry = new R3PlantSource[8];
            data.species = new TeyvatMeadowData.Species[8];
            for (var index = 0; index < 8; index++)
            {
                var species = sourceData.species[index];
                sourceGeometry[index] = R3PlantGeometry(species.meshLOD0);
                data.species[index] = new TeyvatMeadowData.Species
                {
                    name = species.name,
                    meshLOD0 = R3MeadowMesh(species.meshLOD0, owned + "/Meshes/" + species.name + "_LOD0.asset"),
                    meshLOD1 = R3MeadowMesh(species.meshLOD1, owned + "/Meshes/" + species.name + "_LOD1.asset"),
                    materialsLOD0 = species.materialsLOD0, materialsLOD1 = species.materialsLOD1,
                };
            }
            var cells = new Dictionary<Vector2Int, List<TeyvatMeadowData.Placement>>();
            var surfaces = R3MeadowSurfaces();
            var cameraPoints = new List<Vector3>();
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (camera.gameObject.scene == environment.gameObject.scene && !camera.name.Contains("Overview") && !camera.name.Contains("ReferenceSide") && !camera.name.Contains("ReferenceRear"))
                    cameraPoints.Add(camera.transform.position);
            // Registered gameplay camera origins remain a second source even if
            // the review scene uses different display names for its cameras.
            foreach (var shot in metadata.shots)
                if (shot.name.Contains("Planning") || shot.name.Contains("Attack") || shot.name.Contains("Contact"))
                    if (!cameraPoints.Any(point => (point - shot.position).sqrMagnitude < .01f)) cameraPoints.Add(shot.position);
            var heads = metadata.anchors.Select(anchor => origin + anchor.localPosition + Vector3.up * (proxyHeight * 5 / 6)).ToArray();
            var rng = new System.Random(R3MeadowSeed);
            double grassHeightSum = 0;
            try
            {
                // A common grid avoids a visible density seam. Broad correlated
                // patches, species choices and height fields form a meadow garden.
                const float spacing = .46f;
                for (var iz = -152; iz <= 318; iz++) for (var ix = -209; ix <= 209; ix++)
                {
                    report.candidates++;
                    var x = (ix + (float)rng.NextDouble() * .91f) * spacing;
                    var z = (iz + (float)rng.NextDouble() * .91f) * spacing;
                    var cluster = R3MeadowNoise(x, z, .071f, 13);
                    var broad = R3MeadowNoise(x, z, .024f, 38);
                    var arena = x * x / (27 * 27) + z * z / (23 * 23) < 1;
                    var near = Mathf.Abs(x) < 59 && z > -34 && z < 26;
                    var probability = arena ? Mathf.Lerp(.74f, .99f, cluster) : near ? Mathf.Lerp(.40f, .88f, cluster) : Mathf.Lerp(.025f, .25f, Mathf.SmoothStep(0, 1, (broad - .24f) / .52f)) * Mathf.Lerp(.4f, 1, cluster);
                    if (rng.NextDouble() > probability) { report.densityRejected++; continue; }
                    if (report.placed >= 35000) { report.budgetRejected++; continue; }
                    var probe = origin + new Vector3(x, 0, z);
                    RaycastHit hit; R3Surface surface;
                    if (!R3MeadowHighest(surfaces, probe, out hit, out surface) || hit.normal.y < .85f || hit.point.y < origin.y - .85f || !R3MeadowGrassSurface(surface, hit))
                    { report.surfaceRejected++; continue; }
                    var flowerPatch = R3MeadowNoise(x, z, .103f, 73);
                    var choice = (float)rng.NextDouble();
                    var type = choice < .56f ? 1 : choice < .88f ? 2 : 3;
                    if (choice > .97f && !arena) type = 4;
                    if (flowerPatch > .58f && rng.NextDouble() < (arena ? .06 : .18))
                    {
                        var hue = R3MeadowNoise(x, z, .046f, 92);
                        type = hue < .42f ? 5 : hue > .60f ? 7 : 6;
                    }
                    var nearestActor = 100f;
                    foreach (var anchor in metadata.anchors)
                        nearestActor = Mathf.Min(nearestActor, Vector2.Distance(new Vector2(x, z), new Vector2(anchor.localPosition.x, anchor.localPosition.z)));
                    var borderHeight = Mathf.SmoothStep(0, 1, Mathf.Clamp01((nearestActor - 3) / 22));
                    var requestedHeight = proxyHeight * Mathf.Lerp(.4f, .6f, Mathf.Clamp01(borderHeight * .75f + cluster * .25f));
                    if (nearestActor < 3) requestedHeight = proxyHeight * Mathf.Lerp(.4f, .43f, cluster);
                    if (type == 4) requestedHeight = Mathf.Lerp(.14f, .22f, cluster);
                    if (type >= 5) requestedHeight = Mathf.Lerp(.92f, 1.46f, cluster);
                    var source = sourceGeometry[type];
                    var widthScale = Mathf.Lerp(.86f, 1.24f, (float)rng.NextDouble());
                    var rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0);
                    var yScale = requestedHeight / Mathf.Max(.025f, source.rawBounds.size.y * hit.normal.y);
                    var matrix = Matrix4x4.TRS(hit.point - Vector3.up * .024f, rotation, new Vector3(widthScale, yScale, widthScale));
                    // Tilt adds a small vertical contribution from clump width.
                    // Correct against real vertices so 1.2-1.8m remains an actual
                    // vertical height policy rather than a scale-factor claim.
                    if (type < 4)
                    {
                        for (var correction = 0; correction < 2; correction++)
                        {
                            var minimum = float.PositiveInfinity; var maximum = float.NegativeInfinity;
                            foreach (var vertex in source.vertices) { var height = matrix.MultiplyPoint3x4(vertex).y; minimum = Mathf.Min(minimum, height); maximum = Mathf.Max(maximum, height); }
                            yScale *= requestedHeight / Mathf.Max(.025f, maximum - minimum);
                            matrix = Matrix4x4.TRS(hit.point - Vector3.up * .024f, rotation, new Vector3(widthScale, yScale, widthScale));
                        }
                    }
                    var contact = true; float maxGap = 0, maxBurial = 0;
                    for (var rootIndex = 0; rootIndex < source.roots.Length; rootIndex += Mathf.Max(1, source.roots.Length / 10))
                    {
                        var root = matrix.MultiplyPoint3x4(source.roots[rootIndex]); RaycastHit rootHit; R3Surface rootSurface;
                        if (!R3MeadowHighest(surfaces, root, out rootHit, out rootSurface)) { contact = false; break; }
                        var gap = root.y - rootHit.point.y; maxGap = Mathf.Max(maxGap, gap); maxBurial = Mathf.Max(maxBurial, -gap);
                        if (gap > R3RootTolerance || gap < -.13f) { contact = false; break; }
                    }
                    if (!contact) { report.contactRejected++; continue; }
                    var rawWorldBounds = R3MeadowBounds(source.rawBounds, matrix);
                    var clearance = rawWorldBounds; clearance.Expand(new Vector3(.36f, .06f, .36f));
                    var blocked = false;
                    foreach (var cameraPoint in cameraPoints)
                    {
                        if (clearance.SqrDistance(cameraPoint) < .55f * .55f) { blocked = true; break; }
                        foreach (var head in heads)
                        {
                            var vector = head - cameraPoint; float distance;
                            if (clearance.IntersectRay(new Ray(cameraPoint, vector.normalized), out distance) && distance < vector.magnitude - .16f) { blocked = true; break; }
                        }
                        if (blocked) break;
                    }
                    if (blocked) { report.cameraRejected++; continue; }
                    var key = new Vector2Int(Mathf.FloorToInt(x / 12), Mathf.FloorToInt(z / 12));
                    List<TeyvatMeadowData.Placement> list;
                    if (!cells.TryGetValue(key, out list)) { list = new List<TeyvatMeadowData.Placement>(); cells.Add(key, list); }
                    list.Add(new TeyvatMeadowData.Placement { speciesIndex = type, matrix = matrix, phase = (float)rng.NextDouble() });
                    report.placed++; report.speciesCounts[type]++;
                    report.maximumRootGap = Mathf.Max(report.maximumRootGap, maxGap); report.maximumRootBurial = Mathf.Max(report.maximumRootBurial, maxBurial);
                    if (type < 4)
                    {
                        var actualMin = float.PositiveInfinity; var actualMax = float.NegativeInfinity;
                        foreach (var vertex in source.vertices) { var height = matrix.MultiplyPoint3x4(vertex).y; actualMin = Mathf.Min(actualMin, height); actualMax = Mathf.Max(actualMax, height); }
                        var measured = actualMax - actualMin;
                        report.grassPlants++; grassHeightSum += measured;
                        report.grassMinMetres = Mathf.Min(report.grassMinMetres, measured); report.grassMaxMetres = Mathf.Max(report.grassMaxMetres, measured);
                    }
                    else if (type >= 5) report.flowerPlants++;
                }
            }
            finally { foreach (var surface in surfaces) if (surface.temporary && surface.collider != null) Object.DestroyImmediate(surface.collider); }
            var totalBounds = new Bounds(origin, Vector3.zero);
            data.cells = cells.OrderBy(pair => pair.Key.y).ThenBy(pair => pair.Key.x).Select(pair =>
            {
                var cellBounds = new Bounds(pair.Value[0].matrix.GetColumn(3), Vector3.zero);
                foreach (var placement in pair.Value)
                {
                    var species = data.species[placement.speciesIndex];
                    cellBounds.Encapsulate(R3MeadowBounds(species.meshLOD0.bounds, placement.matrix));
                    cellBounds.Encapsulate(R3MeadowBounds(species.meshLOD1.bounds, placement.matrix));
                }
                cellBounds.Expand(1.6f); totalBounds.Encapsulate(cellBounds);
                return new TeyvatMeadowData.Cell { name = "R3_Meadow_" + pair.Key.x + "_" + pair.Key.y, worldBounds = cellBounds, placements = pair.Value.ToArray() };
            }).ToArray();
            data.worldBounds = totalBounds; data.placementCount = report.placed;
            report.grassAverageMetres = report.grassPlants > 0 ? (float)(grassHeightSum / report.grassPlants) : 0;
            report.grassMinRatio = report.grassMinMetres / proxyHeight; report.grassMaxRatio = report.grassMaxMetres / proxyHeight;
            report.rootContactPassed = report.maximumRootGap <= R3RootTolerance;
            report.grassRangePassed = report.grassPlants > 0 && report.grassMinMetres >= proxyHeight * .38f && report.grassMaxMetres <= proxyHeight * .65f;
            File.WriteAllText(Path.Combine(output, "HeightRatioReport.json"), JsonUtility.ToJson(report, true));
            File.WriteAllText(owned + "/TG_HeightPolicy.json", JsonUtility.ToJson(report, true));
            EditorUtility.SetDirty(data); AssetDatabase.SaveAssets();
            var node = new GameObject("TG LayoutR3 meadow - 40 to 60 percent proxy height");
            node.transform.SetParent(environment, true);
            var renderer = node.AddComponent<TeyvatMeadowRenderer>(); renderer.data = data; renderer.globals = globals; renderer.windBoundsPadding = .8f;
            renderer.InvalidatePlacementCache();
            renderer.globals = null; PrefabUtility.SaveAsPrefabAsset(node, owned + "/TG_Meadow.prefab"); renderer.globals = globals;
            if (!report.rootContactPassed || !report.grassRangePassed || data.placementCount == 0) throw new InvalidOperationException("R3 meadow height/contact acceptance failed; inspect HeightRatioReport.json.");
            return renderer;
        }

        static R3PlantSource R3PlantGeometry(Mesh mesh)
        {
            var vertices = mesh.vertices; var bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (var vertex in vertices) bounds.Encapsulate(vertex);
            var colors = mesh.colors;
            var roots = vertices.Where((vertex, index) => colors.Length == vertices.Length ? colors[index].r < .0001f && colors[index].g < .0001f : vertex.y < bounds.min.y + .025f).Distinct().ToArray();
            if (roots.Length == 0) roots = vertices.Where(vertex => vertex.y < bounds.min.y + .025f).Distinct().ToArray();
            return new R3PlantSource { vertices = vertices, roots = roots, rawBounds = bounds };
        }

        static Mesh R3MeadowMesh(Mesh source, string path)
        {
            if (source == null) throw new InvalidOperationException("Every R3 meadow species needs both accepted WP04 LOD meshes.");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = Object.Instantiate(source); AssetDatabase.CreateAsset(mesh, path); } else EditorUtility.CopySerialized(source, mesh);
            mesh.name = "R3_" + source.name;
            // Read real geometry, then save independent animation bounds. Local
            // vertical padding is conservative under the larger nonuniform Y scale.
            var vertices = mesh.vertices; var bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (var vertex in vertices) bounds.Encapsulate(vertex);
            bounds.Expand(new Vector3(1.6f, .6f, 1.6f)); mesh.bounds = bounds; EditorUtility.SetDirty(mesh); return mesh;
        }

        static List<R3Surface> R3MeadowSurfaces()
        {
            var result = new List<R3Surface>();
            foreach (var terrain in environment.GetComponentsInChildren<Terrain>(true))
            {
                var collider = terrain.GetComponent<TerrainCollider>(); if (collider == null) continue;
                result.Add(new R3Surface { collider = collider, terrain = terrain, alpha = terrain.terrainData.GetAlphamaps(0, 0, terrain.terrainData.alphamapWidth, terrain.terrainData.alphamapHeight) });
            }
            foreach (var filter in environment.GetComponentsInChildren<MeshFilter>(true))
            {
                var visual = filter.GetComponent<Renderer>(); var mesh = filter.sharedMesh;
                if (visual == null || !visual.enabled || mesh == null || filter.name.Contains("_LOD1") || filter.name.StartsWith("COL_") || filter.name.StartsWith("UCX_")) continue;
                var materials = visual.sharedMaterials;
                if (materials.Any(material => material != null && (material.renderQueue >= 2500 || material.shader.name.Contains("Foliage")))) continue;
                var collider = filter.GetComponent<MeshCollider>(); var temporary = collider == null;
                if (temporary) collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                var eligible = new bool[mesh.subMeshCount]; var ends = new int[mesh.subMeshCount]; var count = 0;
                for (var slot = 0; slot < mesh.subMeshCount; slot++)
                {
                    count += (int)mesh.GetIndexCount(slot) / 3; ends[slot] = count;
                    var material = slot < materials.Length ? materials[slot] : null;
                    eligible[slot] = material != null && (material.name.IndexOf("Ground", StringComparison.OrdinalIgnoreCase) >= 0 || material.name.IndexOf("Moss", StringComparison.OrdinalIgnoreCase) >= 0);
                }
                result.Add(new R3Surface { collider = collider, temporary = temporary, eligibleSlots = eligible, triangleEnds = ends });
            }
            Physics.SyncTransforms(); foreach (var surface in result) surface.bounds = surface.collider.bounds; return result;
        }

        static bool R3MeadowHighest(List<R3Surface> surfaces, Vector3 position, out RaycastHit best, out R3Surface chosen)
        {
            var ray = new Ray(new Vector3(position.x, origin.y + 160, position.z), Vector3.down); var nearest = 240f;
            best = default; chosen = null;
            foreach (var surface in surfaces)
            {
                var bounds = surface.bounds;
                if (position.x < bounds.min.x || position.x > bounds.max.x || position.z < bounds.min.z || position.z > bounds.max.z) continue;
                RaycastHit hit; if (!surface.collider.Raycast(ray, out hit, nearest)) continue;
                nearest = hit.distance; best = hit; chosen = surface;
            }
            return chosen != null;
        }

        static bool R3MeadowGrassSurface(R3Surface surface, RaycastHit hit)
        {
            if (surface.terrain != null)
            {
                var data = surface.terrain.terrainData; var point = surface.terrain.transform.InverseTransformPoint(hit.point);
                var x = Mathf.Clamp(Mathf.RoundToInt(point.x / data.size.x * (data.alphamapWidth - 1)), 0, data.alphamapWidth - 1);
                var z = Mathf.Clamp(Mathf.RoundToInt(point.z / data.size.z * (data.alphamapHeight - 1)), 0, data.alphamapHeight - 1);
                return surface.alpha[z, x, 0] >= .62f;
            }
            var slot = 0; while (slot < surface.triangleEnds.Length - 1 && hit.triangleIndex >= surface.triangleEnds[slot]) slot++;
            return surface.eligibleSlots[slot];
        }

        static Bounds R3MeadowBounds(Bounds source, Matrix4x4 matrix)
        {
            var extent = source.extents; var x = matrix.MultiplyVector(Vector3.right * extent.x); var y = matrix.MultiplyVector(Vector3.up * extent.y); var z = matrix.MultiplyVector(Vector3.forward * extent.z);
            return new Bounds(matrix.MultiplyPoint3x4(source.center), new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x), Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z)) * 2);
        }
        static float R3MeadowNoise(float x, float z, float scale, float offset) { return Mathf.PerlinNoise(x * scale + offset, z * scale + offset * .37f); }
    }
}
