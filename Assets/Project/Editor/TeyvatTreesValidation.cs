using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FightingAllstar.EnvironmentArt;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FightingAllstar.EditorTools
{
    public static partial class TeyvatTreesLab
    {
        [Serializable] sealed class TVMeshCheck
        {
            public string name, species; public int lod, vertices, triangles, submeshes, rootVertices;
            public bool passed; public float boundsErrorMetres, normalRangeError, maximumNormalLengthError;
            public Color maskMinimum, maskMaximum; public string[] errors;
        }
        [Serializable] sealed class TVRegistryCheck { public string name; public bool present, passed; public float xzErrorMetres; }
        [Serializable] sealed class TVWindCheck
        {
            public string mesh; public bool passed; public int sampledVertices, sampledRoots, timeDirectionVertexTests, escapedSamples;
            public float maximumDisplacementMetres, maximumRootDisplacementMetres;
        }
        [Serializable] sealed class TVViewCheck
        {
            public string name; public int cameraInsideConservativeBounds, broadPhaseHeadHits, actualTriangleHeadHits; public string[] hits;
        }
        [Serializable] sealed class TVSweepCheck
        {
            public string name; public int samples, cameraInsideConservativeBounds, broadPhaseHeadHits, actualTriangleHeadHits; public string[] hits;
        }
        [Serializable] sealed class TVReport
        {
            public string schema = "TG_WP05_TreeValidation_v1";
            public string method = "Read-only raw FBX comparison against authored mesh counts/metre bounds/UV0/numerical masks/custom-normal ranges and SHA256. All registry group XZ anchors checked. Actual persisted animated tree mesh bounds tested using sampled shared wind displacement at strength 1, response .65, 21 times 0..10 seconds and four wind directions. Original registry cameras plus eight 33-point spherical camera paths test conservative LOD0 world bounds, then actual LOD0 triangles for head rays.";
            public string limitations = "No scene objects, colliders, materials or source assets are modified. CPU double-sided ray/triangle tests replace temporary MeshCollider queries and test undeformed LOD0 geometry; broad bounds include stored animation padding. Wind samples roots and up to 512 evenly spaced vertices per unique mesh at its smallest placed scale, not every animated surface point. Proxy-head rays use 2.5m, not full animated characters or projected silhouettes. Runtime LOD, GPU shadow/depth agreement, leaf temporal quality and player-build performance require the separate visual/runtime reviews.";
            public bool passed, hashMatches, importedMeshesPass, registryPass, windPass, camerasPass, sweepsPass;
            public int manifestMeshCount, importedMeshCount, registryCount, treeGroupCount, shrubGroupCount, cameraCount, sweepSamples;
            public float positionStartingTolerance = .002f, normalStartingTolerance = .03f, maskStartingTolerance = .0041f;
            public TVMeshCheck[] meshes; public TVRegistryCheck[] registry; public TVWindCheck[] wind; public TVViewCheck[] cameras; public TVSweepCheck[] sweeps; public string[] errors;
        }
        sealed class TVMeshGeometry { public Mesh mesh; public Vector3[] vertices; public int[] triangles; public Color[] colors; }
        sealed class TVTreeGeometry
        {
            public string name; public Bounds bounds; public readonly List<TVMeshGeometry> parts = new List<TVMeshGeometry>();
            public readonly List<Matrix4x4> worldToLocal = new List<Matrix4x4>();
        }

        static bool ValidateTrees()
        {
            var report = new TVReport(); var errors = new List<string>();
            var imported = new List<TVMeshCheck>(); var registry = new List<TVRegistryCheck>(); var wind = new List<TVWindCheck>();
            var views = new List<TVViewCheck>(); var sweeps = new List<TVSweepCheck>();
            try
            {
                if (forest == null || environment == null || composition == null || composition.trees == null || layout == null || manifest == null)
                    throw new InvalidOperationException("Tree validation requires saved forest, environment, composition, layout and source manifest.");
                using (var hash = SHA256.Create())
                    report.hashMatches = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(Trees + "/Models/TG_TreeKit.fbx"))).Replace("-", "").ToLowerInvariant() == manifest.fbx_sha256;
                report.manifestMeshCount = manifest.objects.Length; report.importedMeshCount = meshes.Count;
                foreach (var source in manifest.objects)
                {
                    var meshErrors = new List<string>(); var check = new TVMeshCheck { name = source.name, species = source.species, lod = source.lod };
                    Mesh mesh;
                    if (!meshes.TryGetValue(source.name, out mesh)) { check.errors = new[] { "Raw imported mesh missing." }; imported.Add(check); continue; }
                    var vertices = mesh.vertices; var normals = mesh.normals; var colors = mesh.colors; var uv = mesh.uv;
                    check.vertices = vertices.Length; check.triangles = mesh.triangles.Length / 3; check.submeshes = mesh.subMeshCount;
                    if (vertices.Length == 0) meshErrors.Add("Mesh is empty.");
                    if (check.triangles != source.expected_triangles_after_export) meshErrors.Add("Triangle count differs from source.");
                    if (uv.Length != vertices.Length || normals.Length != vertices.Length || colors.Length != vertices.Length) meshErrors.Add("UV0, imported normals or RGBA stream is missing.");
                    var expectedMin = TVVector(source.bounds_unity_local_metres.min); var expectedMax = TVVector(source.bounds_unity_local_metres.max);
                    var actual = new Bounds(vertices[0], Vector3.zero); foreach (var vertex in vertices) actual.Encapsulate(vertex);
                    check.boundsErrorMetres = Mathf.Max(TVMax(actual.min - expectedMin), TVMax(actual.max - expectedMax));
                    check.boundsErrorMetres = Mathf.Max(check.boundsErrorMetres, TVMax(actual.min - mesh.bounds.min), TVMax(actual.max - mesh.bounds.max));
                    if (check.boundsErrorMetres > .002f) meshErrors.Add("Raw geometry metre/axis bounds differ from the authored source.");
                    var minimum = new Color(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                    var maximum = new Color(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
                    foreach (var color in colors)
                    {
                        for (var channel = 0; channel < 4; channel++) { minimum[channel] = Mathf.Min(minimum[channel], color[channel]); maximum[channel] = Mathf.Max(maximum[channel], color[channel]); }
                        if (color.r < -.0041f || color.r > 1.0041f || color.g < -.0041f || color.g > .5541f || color.b < -.0041f || color.b > 1.0041f || Mathf.Abs(color.a - 1) > .0041f)
                            if (!meshErrors.Contains("Numerical wind masks are outside their authored ranges.")) meshErrors.Add("Numerical wind masks are outside their authored ranges.");
                        if (color.r < .0001f && color.g < .0001f) check.rootVertices++;
                    }
                    check.maskMinimum = minimum; check.maskMaximum = maximum;
                    if (source.species != "diagnostic" && colors.Length > 0 && (Mathf.Abs(minimum.b - .41f) > .0041f || Mathf.Abs(maximum.b - .41f) > .0041f)) meshErrors.Add("Stable tree phase B=.41 did not survive import.");
                    if (uv.Any(value => !TVFinite(value.x) || !TVFinite(value.y))) meshErrors.Add("UV0 contains a non-finite coordinate.");
                    if (normals.Length > 0)
                    {
                        var normalMin = normals[0]; var normalMax = normals[0];
                        foreach (var normal in normals)
                        {
                            normalMin = Vector3.Min(normalMin, normal); normalMax = Vector3.Max(normalMax, normal);
                            check.maximumNormalLengthError = Mathf.Max(check.maximumNormalLengthError, Mathf.Abs(normal.magnitude - 1));
                        }
                        if (source.unity_normal_min != null && source.unity_normal_min.Length == 3 && source.unity_normal_max != null && source.unity_normal_max.Length == 3)
                            check.normalRangeError = Mathf.Max(TVMax(normalMin - TVVector(source.unity_normal_min)), TVMax(normalMax - TVVector(source.unity_normal_max)));
                        if (check.normalRangeError > .03f || check.maximumNormalLengthError > .003f) meshErrors.Add("Imported custom normals differ from the source ranges or unit length.");
                    }
                    if (source.species != "diagnostic" && check.rootVertices == 0) meshErrors.Add("No stationary R0/G0 root vertices were imported.");
                    check.errors = meshErrors.ToArray(); check.passed = meshErrors.Count == 0; imported.Add(check);
                }
                report.importedMeshesPass = report.hashMatches && imported.Count == manifest.objects.Length && meshes.Count == manifest.objects.Length && imported.All(check => check.passed);
                report.registryCount = composition.trees.Length;
                for (var index = 0; index < forest.childCount; index++)
                {
                    var name = forest.GetChild(index).name; if (name.StartsWith("TreeGroup_")) report.treeGroupCount++; if (name.StartsWith("ShrubGroup_")) report.shrubGroupCount++;
                }
                foreach (var anchor in composition.trees)
                {
                    var group = forest.Find(anchor.name); var expected = origin + anchor.localPosition;
                    var error = group == null ? float.MaxValue : Vector2.Distance(new Vector2(group.position.x, group.position.z), new Vector2(expected.x, expected.z));
                    registry.Add(new TVRegistryCheck { name = anchor.name, present = group != null, xzErrorMetres = error, passed = group != null && error <= .002f });
                }
                report.registryPass = report.registryCount == 120 && report.treeGroupCount == 120 && registry.All(check => check.passed);

                var geometry = new Dictionary<Mesh, TVMeshGeometry>(); var treeGeometry = new List<TVTreeGeometry>();
                var smallestPlacement = new Dictionary<Mesh, Transform>();
                for (var index = 0; index < forest.childCount; index++)
                {
                    var group = forest.GetChild(index); if (!group.name.StartsWith("TreeGroup_") && !group.name.StartsWith("ShrubGroup_")) continue;
                    TVTreeGeometry tree = null;
                    foreach (var filter in group.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var mesh = filter.sharedMesh; if (mesh == null) continue;
                        TVMeshGeometry part;
                        if (!geometry.TryGetValue(mesh, out part)) { part = new TVMeshGeometry { mesh = mesh, vertices = mesh.vertices, triangles = mesh.triangles, colors = mesh.colors }; geometry.Add(mesh, part); }
                        Transform smallest;
                        if (!smallestPlacement.TryGetValue(mesh, out smallest) || TVMinimumScale(filter.transform.lossyScale) < TVMinimumScale(smallest.lossyScale)) smallestPlacement[mesh] = filter.transform;
                        if (!mesh.name.Contains("_LOD0")) continue;
                        var bounds = TVBounds(mesh.bounds, filter.transform.localToWorldMatrix);
                        if (tree == null) { tree = new TVTreeGeometry { name = group.name, bounds = bounds }; treeGeometry.Add(tree); } else tree.bounds.Encapsulate(bounds);
                        tree.parts.Add(part); tree.worldToLocal.Add(filter.transform.worldToLocalMatrix);
                    }
                }
                foreach (var pair in smallestPlacement) wind.Add(TVValidateWind(geometry[pair.Key], pair.Value));
                report.windPass = wind.Count > 0 && wind.All(check => check.passed);
                var heads = layout.anchors.Select(anchor => origin + anchor.localPosition + Vector3.up * 2.5f).ToArray();
                var sceneCameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(camera => camera.gameObject.scene == forest.gameObject.scene).ToArray();
                foreach (var shot in layout.shots)
                {
                    var camera = sceneCameras.FirstOrDefault(candidate => candidate.name == shot.name || candidate.name == "TG_" + shot.name);
                    if (camera == null) { errors.Add("Original registry camera missing: " + shot.name); continue; }
                    views.Add(TVCheckView(shot.name, camera.transform.position, heads, treeGeometry));
                }
                report.cameraCount = views.Count;
                report.camerasPass = views.Count == layout.shots.Length && views.All(check => check.cameraInsideConservativeBounds == 0 && check.actualTriangleHeadHits == 0);
                var paths = new[] { new[] { "PlayerPlanning", "EnemyPlanning" }, new[] { "EnemyPlanning", "PlayerPlanning" }, new[] { "PlayerPlanning", "PlayerAttack" }, new[] { "PlayerAttack", "PlayerContact" }, new[] { "PlayerContact", "PlayerPlanning" }, new[] { "EnemyPlanning", "EnemyAttack" }, new[] { "EnemyAttack", "EnemyContact" }, new[] { "EnemyContact", "EnemyPlanning" } };
                foreach (var path in paths)
                {
                    var from = layout.shots.FirstOrDefault(shot => shot.name == path[0]); var to = layout.shots.FirstOrDefault(shot => shot.name == path[1]);
                    if (from == null || to == null) { errors.Add("Camera path registry entry missing: " + path[0] + " -> " + path[1]); continue; }
                    var sweep = new TVSweepCheck { name = path[0] + " -> " + path[1], samples = 33 }; var hits = new HashSet<string>();
                    for (var sample = 0; sample < 33; sample++)
                    {
                        var check = TVCheckView("sample " + sample, TVOrbit(from.position, to.position, origin, sample / 32f), heads, treeGeometry);
                        sweep.cameraInsideConservativeBounds += check.cameraInsideConservativeBounds; sweep.broadPhaseHeadHits += check.broadPhaseHeadHits; sweep.actualTriangleHeadHits += check.actualTriangleHeadHits;
                        foreach (var hit in check.hits) hits.Add(hit);
                    }
                    sweep.hits = hits.ToArray(); sweeps.Add(sweep); report.sweepSamples += sweep.samples;
                }
                report.sweepsPass = report.sweepSamples == 264 && sweeps.All(check => check.cameraInsideConservativeBounds == 0 && check.actualTriangleHeadHits == 0);
            }
            catch (Exception exception) { errors.Add(exception.ToString()); }
            report.meshes = imported.ToArray(); report.registry = registry.ToArray(); report.wind = wind.ToArray(); report.cameras = views.ToArray(); report.sweeps = sweeps.ToArray(); report.errors = errors.ToArray();
            report.passed = errors.Count == 0 && report.importedMeshesPass && report.registryPass && report.windPass && report.camerasPass && report.sweepsPass;
            Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "TreeValidation.json"), JsonUtility.ToJson(report, true)); return report.passed;
        }

        static TVWindCheck TVValidateWind(TVMeshGeometry part, Transform placement)
        {
            var check = new TVWindCheck { mesh = part.mesh.name }; var samples = new HashSet<int>();
            for (var index = 0; index < part.vertices.Length; index += Mathf.Max(1, part.vertices.Length / 512)) samples.Add(index);
            for (var index = 0; index < part.colors.Length; index++) if (part.colors[index].r < .0001f && part.colors[index].g < .0001f) { samples.Add(index); check.sampledRoots++; }
            check.sampledVertices = samples.Count;
            if (part.colors.Length != part.vertices.Length) return check;
            var directions = new[] { Vector2.right, Vector2.up, new Vector2(.7071068f, .7071068f), new Vector2(-.7071068f, .7071068f) };
            var toWorld = placement.localToWorldMatrix; var toLocal = placement.worldToLocalMatrix; var bounds = part.mesh.bounds;
            foreach (var index in samples)
            {
                var point = toWorld.MultiplyPoint3x4(part.vertices[index]); var color = part.colors[index];
                foreach (var direction in directions) for (var time = 0; time <= 20; time++)
                {
                    var displacement = TeyvatMaterialGlobals.Displacement(point, color, new Vector4(direction.x, direction.y, 1, time * .5f), .65f);
                    check.timeDirectionVertexTests++; check.maximumDisplacementMetres = Mathf.Max(check.maximumDisplacementMetres, displacement.magnitude);
                    if (color.r < .0001f && color.g < .0001f) check.maximumRootDisplacementMetres = Mathf.Max(check.maximumRootDisplacementMetres, displacement.magnitude);
                    if (!bounds.Contains(toLocal.MultiplyPoint3x4(point + displacement))) check.escapedSamples++;
                }
            }
            check.passed = check.escapedSamples == 0 && check.sampledRoots > 0 && check.maximumRootDisplacementMetres <= .00001f; return check;
        }

        static TVViewCheck TVCheckView(string name, Vector3 camera, Vector3[] heads, List<TVTreeGeometry> trees)
        {
            var report = new TVViewCheck { name = name }; var hits = new HashSet<string>();
            foreach (var tree in trees)
            {
                if (tree.bounds.Contains(camera)) { report.cameraInsideConservativeBounds++; hits.Add("Camera inside " + tree.name + " conservative animated LOD0 bounds"); }
                for (var index = 0; index < heads.Length; index++)
                {
                    var vector = heads[index] - camera; var ray = new Ray(camera, vector.normalized); float distance;
                    if (!tree.bounds.IntersectRay(ray, out distance) || distance > vector.magnitude - .1f) continue;
                    report.broadPhaseHeadHits++;
                    if (TVTriangleHit(tree, camera, vector.normalized, vector.magnitude - .1f)) { report.actualTriangleHeadHits++; hits.Add("Head " + index + " ray blocked by actual " + tree.name + " LOD0 triangles"); }
                }
            }
            report.hits = hits.ToArray(); return report;
        }

        static bool TVTriangleHit(TVTreeGeometry tree, Vector3 worldOrigin, Vector3 worldDirection, float maximumDistance)
        {
            for (var partIndex = 0; partIndex < tree.parts.Count; partIndex++)
            {
                var part = tree.parts[partIndex]; var matrix = tree.worldToLocal[partIndex];
                var rayOrigin = matrix.MultiplyPoint3x4(worldOrigin); var rayDirection = matrix.MultiplyVector(worldDirection);
                for (var index = 0; index < part.triangles.Length; index += 3)
                {
                    var a = part.vertices[part.triangles[index]]; var edge1 = part.vertices[part.triangles[index + 1]] - a; var edge2 = part.vertices[part.triangles[index + 2]] - a;
                    var p = Vector3.Cross(rayDirection, edge2); var determinant = Vector3.Dot(edge1, p); if (Mathf.Abs(determinant) < 1e-8f) continue;
                    var inverse = 1 / determinant; var t = rayOrigin - a; var u = Vector3.Dot(t, p) * inverse; if (u < 0 || u > 1) continue;
                    var q = Vector3.Cross(t, edge1); var v = Vector3.Dot(rayDirection, q) * inverse; if (v < 0 || u + v > 1) continue;
                    var distance = Vector3.Dot(edge2, q) * inverse; if (distance >= 0 && distance < maximumDistance) return true;
                }
            }
            return false;
        }
        static Vector3 TVOrbit(Vector3 from, Vector3 to, Vector3 center, float time)
        {
            var a = from - center; var b = to - center; var yaw = (Mathf.Atan2(a.x, a.z) * Mathf.Rad2Deg + Mathf.DeltaAngle(Mathf.Atan2(a.x, a.z) * Mathf.Rad2Deg, Mathf.Atan2(b.x, b.z) * Mathf.Rad2Deg) * time) * Mathf.Deg2Rad;
            var pitch = Mathf.Lerp(Mathf.Asin(a.y / a.magnitude), Mathf.Asin(b.y / b.magnitude), time); var radius = Mathf.Lerp(a.magnitude, b.magnitude, time);
            return center + new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch)) * radius;
        }
        static Bounds TVBounds(Bounds source, Matrix4x4 matrix)
        {
            var extent = source.extents; var x = matrix.MultiplyVector(Vector3.right * extent.x); var y = matrix.MultiplyVector(Vector3.up * extent.y); var z = matrix.MultiplyVector(Vector3.forward * extent.z);
            return new Bounds(matrix.MultiplyPoint3x4(source.center), new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x), Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z)) * 2);
        }
        static float TVMinimumScale(Vector3 scale) { return Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)); }
        static Vector3 TVVector(float[] values) { return new Vector3(values[0], values[1], values[2]); }
        static float TVMax(Vector3 value) { return Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z)); }
        static bool TVFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
