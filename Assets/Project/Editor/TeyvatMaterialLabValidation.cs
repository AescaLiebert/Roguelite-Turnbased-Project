using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using FightingAllstar.EnvironmentArt;

namespace FightingAllstar.EditorTools
{
    public static partial class TeyvatMaterialLab
    {
        // Starting round-trip tolerances: fail a diagnostic, inspect the source/import and repeat.
        // They are numerical authoring checks, not visual or player-build acceptance thresholds.
        const float ImportPositionTolerance = .002f;
        const float ImportMaskTolerance = .0041f; // Allows at most one 8-bit quantization step.
        const float ImportNormalTolerance = .03f;
        const float WindPositionTolerance = .0001f;
        static readonly string[] RequiredSampleNames = { "SM_GrassClump", "SM_LeafCluster", "SM_Trunk", "SM_FlowerCluster", "SM_Cliff", "SM_AxisMarker" };

        [Serializable] sealed class SourceBounds { public float[] min; public float[] max; public float[] size; }
        [Serializable] sealed class SourceMask { public string name; public string domain; public string type; public float[] rgba_min; public float[] rgba_max; public string meaning; }
        [Serializable] sealed class SourceSample
        {
            public string name; public int vertices_before_fbx_seam_splitting; public int expected_triangles_after_export;
            public SourceBounds bounds_export_axis_basis_metres; public SourceMask vertex_attribute;
            public bool custom_split_normals; public float[] normal_component_min; public float[] normal_component_max;
            public string[] material_slots;
        }
        [Serializable] sealed class SourceManifest { public string schema; public int seed; public string units; public string blender_version; public string fbx_sha256; public SourceSample[] objects; }
        [Serializable] sealed class SampleImportCheck
        {
            public string name; public bool passed; public string[] errors;
            public int vertices; public int sourceVerticesBeforeSeams; public int triangles; public int expectedTriangles; public int subMeshes;
            public Bounds computedBounds; public Bounds declaredBounds; public Bounds expectedBounds;
            public float maxBoundsErrorMetres; public bool unitsAndAxisBoundsMatch; public bool declaredBoundsContainVertices;
            public int uvCount; public Vector2 uvMin; public Vector2 uvMax; public int colorCount; public Color rgbaMin; public Color rgbaMax;
            public Color sourceRgbaMin; public Color sourceRgbaMax; public float maxMaskRangeError; public bool numericalMasksMatch; public bool semanticMaskChecksPass;
            public int rootVertices; public int tipVertices; public int normalCount; public Vector3 normalMin; public Vector3 normalMax;
            public Vector3 expectedNormalMin; public Vector3 expectedNormalMax; public float maxNormalRangeError;
            public float normalLengthMin; public float normalLengthMax; public bool importedNormalsMatch; public bool leafUpwardAndOutwardNormals;
        }
        [Serializable] sealed class ImportDiagnostic
        {
            public string schema = "TG_WP02_ImportValidation_v1"; public bool passed; public string method; public string limitations;
            public string manifestPath; public string modelPath; public string sourceBlenderVersion; public int seed; public bool modelSha256Matches;
            public string detectedAxisMapping; public bool axisMappingDetermined; public string normalsImportMode; public bool readable; public bool bakeAxisConversion;
            public float importerGlobalScale; public float positionStartingTolerance; public float maskStartingTolerance; public float normalStartingTolerance;
            public string[] criticalErrors; public SampleImportCheck[] meshes;
        }

        static bool ValidateImports(string manifestPath, string outputDirectory)
        {
            var modelPath = Shared + "/Models/TG_MaterialSamples.fbx";
            var errors = new List<string>();
            var report = new ImportDiagnostic
            {
                method = "Read-only comparison of actual FBX Mesh vertices, UV0, numerical RGBA ranges, imported normal extrema and topology against the Blender sidecar. An asymmetric axis-marker bounds check chooses one consistent X/Z sign mapping (from four combinations, retaining Y-up) for every mesh.",
                limitations = "Range checks do not prove per-vertex correspondence. FBX may split/weld vertices at seams; raw vertex counts are recorded, not required to equal Blender counts. Raised X/Z labels still need visual inspection. These diagnostics do not validate shading, temporal quality or performance.",
                manifestPath = manifestPath, modelPath = modelPath, positionStartingTolerance = ImportPositionTolerance,
                maskStartingTolerance = ImportMaskTolerance, normalStartingTolerance = ImportNormalTolerance,
            };
            try
            {
                if (!File.Exists(manifestPath)) throw new FileNotFoundException("Blender import manifest is missing", manifestPath);
                var source = JsonUtility.FromJson<SourceManifest>(File.ReadAllText(manifestPath));
                if (source == null || source.objects == null) throw new InvalidDataException("Blender import manifest has no objects");
                report.seed = source.seed; report.sourceBlenderVersion = source.blender_version;
                if (source.schema != "TG_WP02_MaterialSamples_v1" || source.units != "metres") errors.Add("Unsupported source manifest schema or unit convention");
                var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
                if (importer == null) throw new InvalidDataException("No ModelImporter found for " + modelPath);
                report.normalsImportMode = importer.importNormals.ToString(); report.readable = importer.isReadable;
                report.bakeAxisConversion = importer.bakeAxisConversion; report.importerGlobalScale = importer.globalScale;
                if (importer.importNormals != ModelImporterNormals.Import) errors.Add("ModelImporter must preserve authored normals with Import mode");
                if (!report.readable) errors.Add("Initial round-trip validation requires a readable model");
                if (!report.bakeAxisConversion) errors.Add("The agreed import preset requires bakeAxisConversion");
                if (Mathf.Abs(importer.globalScale - 1) > .00001f) errors.Add("ModelImporter globalScale differs from one");
                using (var hash = System.Security.Cryptography.SHA256.Create())
                {
                    var actual = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(modelPath))).Replace("-", "").ToLowerInvariant();
                    report.modelSha256Matches = string.Equals(actual, source.fbx_sha256, StringComparison.OrdinalIgnoreCase);
                    if (!report.modelSha256Matches) errors.Add("FBX bytes do not match the source manifest hash");
                }
                var meshes = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Mesh>().ToArray();
                var sourceMarker = source.objects.FirstOrDefault(s => s.name == "SM_AxisMarker");
                var marker = meshes.FirstOrDefault(m => m.name == "SM_AxisMarker");
                var mirrorX = false; var mirrorZ = false;
                if (marker != null && marker.isReadable && sourceMarker != null)
                {
                    var actual = VerticesBounds(marker.vertices);
                    var bestError = float.PositiveInfinity;
                    foreach (var candidateX in new[] { false, true }) foreach (var candidateZ in new[] { false, true })
                    {
                        var error = BoundsError(actual, ManifestBounds(sourceMarker.bounds_export_axis_basis_metres, candidateX, candidateZ));
                        if (error >= bestError) continue;
                        bestError = error; mirrorX = candidateX; mirrorZ = candidateZ;
                    }
                    report.axisMappingDetermined = bestError <= ImportPositionTolerance;
                    report.detectedAxisMapping = "Unity raw mesh relative to the manifest FBX -Z-forward/Y-up basis: X sign " + (mirrorX ? "-1" : "+1")
                        + ", Y sign +1, Z sign " + (mirrorZ ? "-1" : "+1") + "; the axis marker must prove this single mapping and every mesh/normal must match it";
                }
                if (!report.axisMappingDetermined) errors.Add("Axis marker does not establish the expected metre-scaled Y-up orientation");
                var checks = new List<SampleImportCheck>();
                foreach (var name in RequiredSampleNames)
                {
                    var matches = meshes.Where(m => m.name == name).ToArray(); var expected = source.objects.FirstOrDefault(s => s.name == name);
                    if (matches.Length != 1 || expected == null) { errors.Add(name + " is missing, duplicated or absent from the manifest"); continue; }
                    var check = CheckImportedSample(matches[0], expected, mirrorX, mirrorZ); checks.Add(check);
                    if (!check.passed) errors.Add(name + ": " + string.Join("; ", check.errors));
                }
                foreach (var mesh in meshes.Where(m => !RequiredSampleNames.Contains(m.name))) errors.Add("Unexpected imported mesh: " + mesh.name);
                report.meshes = checks.ToArray();
            }
            catch (Exception exception) { errors.Add(exception.GetType().Name + ": " + exception.Message); }
            report.criticalErrors = errors.ToArray(); report.passed = errors.Count == 0;
            Directory.CreateDirectory(outputDirectory); File.WriteAllText(Path.Combine(outputDirectory, "ImportValidation.json"), JsonUtility.ToJson(report, true));
            return report.passed;
        }

        static SampleImportCheck CheckImportedSample(Mesh mesh, SourceSample source, bool mirrorX, bool mirrorZ)
        {
            var errors = new List<string>();
            var check = new SampleImportCheck { name = mesh.name, sourceVerticesBeforeSeams = source.vertices_before_fbx_seam_splitting, expectedTriangles = source.expected_triangles_after_export };
            if (!mesh.isReadable) { check.errors = new[] { "Mesh data is not readable" }; return check; }
            var vertices = mesh.vertices; var normals = mesh.normals; var colors = mesh.colors; var uv = mesh.uv;
            check.vertices = vertices.Length; check.triangles = mesh.triangles.Length / 3; check.subMeshes = mesh.subMeshCount;
            check.computedBounds = VerticesBounds(vertices); check.declaredBounds = mesh.bounds; check.expectedBounds = ManifestBounds(source.bounds_export_axis_basis_metres, mirrorX, mirrorZ);
            check.maxBoundsErrorMetres = BoundsError(check.computedBounds, check.expectedBounds);
            check.unitsAndAxisBoundsMatch = check.maxBoundsErrorMetres <= ImportPositionTolerance;
            var tolerantBounds = mesh.bounds; tolerantBounds.Expand(ImportPositionTolerance * 2);
            check.declaredBoundsContainVertices = vertices.All(tolerantBounds.Contains);
            if (!check.unitsAndAxisBoundsMatch) errors.Add("Raw vertex bounds do not match the agreed metre/axis basis");
            if (!check.declaredBoundsContainVertices) errors.Add("Declared mesh bounds omit source vertices");
            if (check.triangles != check.expectedTriangles) errors.Add("Triangle count differs from the triangulated source");
            if (check.subMeshes != source.material_slots.Length) errors.Add("Submesh/material-slot count differs from source");
            check.uvCount = uv.Length;
            if (uv.Length == vertices.Length && uv.Length > 0)
            {
                check.uvMin = new Vector2(uv.Min(p => p.x), uv.Min(p => p.y)); check.uvMax = new Vector2(uv.Max(p => p.x), uv.Max(p => p.y));
                if (uv.Any(p => !Finite(p.x) || !Finite(p.y)) || (check.uvMax - check.uvMin).sqrMagnitude < .000001f) errors.Add("UV0 contains invalid or collapsed coordinates");
            }
            else errors.Add("UV0 is missing or has an unexpected element count");
            check.colorCount = colors.Length;
            if (colors.Length == vertices.Length && colors.Length > 0)
            {
                check.rgbaMin = ColorRange(colors, false); check.rgbaMax = ColorRange(colors, true);
                check.sourceRgbaMin = ArrayColor(source.vertex_attribute.rgba_min); check.sourceRgbaMax = ArrayColor(source.vertex_attribute.rgba_max);
                check.maxMaskRangeError = Mathf.Max(ColorError(check.rgbaMin, check.sourceRgbaMin), ColorError(check.rgbaMax, check.sourceRgbaMax));
                check.numericalMasksMatch = check.maxMaskRangeError <= ImportMaskTolerance && colors.All(c => Finite(c.r) && Finite(c.g) && Finite(c.b) && Finite(c.a));
                check.rootVertices = colors.Count(c => Mathf.Abs(c.r) <= ImportMaskTolerance && Mathf.Abs(c.g) <= ImportMaskTolerance);
                check.tipVertices = colors.Count(c => c.r >= 1 - ImportMaskTolerance);
                check.semanticMaskChecksPass = check.rgbaMin.a >= 1 - ImportMaskTolerance && check.rgbaMax.a <= 1 + ImportMaskTolerance;
                if (mesh.name == "SM_GrassClump" || mesh.name == "SM_FlowerCluster")
                    check.semanticMaskChecksPass &= check.rootVertices > 0 && check.tipVertices > 0 && Mathf.Abs(check.rgbaMax.g) <= ImportMaskTolerance && check.rgbaMax.b - check.rgbaMin.b > .1f;
                else if (mesh.name == "SM_LeafCluster")
                    check.semanticMaskChecksPass &= check.rgbaMin.r >= .2f - ImportMaskTolerance && check.tipVertices > 0 && check.rgbaMin.g >= .1f - ImportMaskTolerance && check.rgbaMax.g <= .8f + ImportMaskTolerance && check.rgbaMax.b - check.rgbaMin.b > .1f;
                else check.semanticMaskChecksPass &= Mathf.Abs(check.rgbaMax.r) <= ImportMaskTolerance && Mathf.Abs(check.rgbaMax.g) <= ImportMaskTolerance;
                if (!check.numericalMasksMatch) errors.Add("RGBA range differs from numerical source masks; inspect gamma/color import conversion");
                if (!check.semanticMaskChecksPass) errors.Add("Root/tip/branch/phase/alpha mask semantics failed");
            }
            else errors.Add("RGBA data is missing or has an unexpected element count");
            check.normalCount = normals.Length;
            if (normals.Length == vertices.Length && normals.Length > 0)
            {
                check.normalMin = VectorRange(normals, false); check.normalMax = VectorRange(normals, true);
                check.normalLengthMin = normals.Min(n => n.magnitude); check.normalLengthMax = normals.Max(n => n.magnitude);
                var min = source.normal_component_min; var max = source.normal_component_max;
                check.expectedNormalMin = new Vector3(mirrorX ? -max[0] : min[0], min[2], mirrorZ ? min[1] : -max[1]);
                check.expectedNormalMax = new Vector3(mirrorX ? -min[0] : max[0], max[2], mirrorZ ? max[1] : -min[1]);
                check.maxNormalRangeError = Mathf.Max(MaxComponent(check.normalMin - check.expectedNormalMin), MaxComponent(check.normalMax - check.expectedNormalMax));
                check.importedNormalsMatch = source.custom_split_normals && check.maxNormalRangeError <= ImportNormalTolerance && check.normalLengthMin > .98f && check.normalLengthMax < 1.02f;
                check.leafUpwardAndOutwardNormals = mesh.name != "SM_LeafCluster" || (check.normalMin.y > .45f && check.normalMax.x - check.normalMin.x > 1 && check.normalMax.z - check.normalMin.z > 1);
                if (!check.importedNormalsMatch) errors.Add("Imported normal extrema or lengths differ from the authored normals");
                if (!check.leafUpwardAndOutwardNormals) errors.Add("Leaf normals lost the coherent upward/outward field");
            }
            else errors.Add("Normals are missing or have an unexpected element count");
            check.errors = errors.ToArray(); check.passed = errors.Count == 0; return check;
        }

        [Serializable] sealed class WindMeshCheck
        {
            public string renderer; public string mesh; public int vertices; public int placements; public int vertexTimeSamples;
            public int rootSamples; public int tipSamples; public int outsideBoundsSamples; public float maxRootDisplacement; public float maxTipDisplacement;
            public float maxDisplacement; public float maxBetweenTimeDisplacement; public float windResponse; public Bounds originalVertexBounds; public Bounds meshAssetBounds;
            public Bounds rendererLocalBounds; public Vector3 rendererLocalBoundsPaddingPerSide;
            public bool usesInstanceWorldBounds; public Bounds instanceWorldBounds; public bool materialInterfacePresent;
            public bool passed; public string[] errors;
        }
        [Serializable] sealed class WindDiagnostic
        {
            public string schema = "TG_WP02_WindBoundsValidation_v1"; public bool passed; public string method; public string limitations;
            public float[] fixedTimes; public float positionStartingTolerance; public Vector4 wind; public bool globalInterfaceMatches;
            public bool colorFieldPresent; public int configuredGrassInstances; public int submittedInstancesAtReview; public int submittedBatchesAtReview; public int submissionFramesAtReview;
            public float syntheticZeroMaskMaxDisplacement; public WindMeshCheck[] meshes; public string[] criticalErrors;
        }

        static bool ValidateWindAndBounds(TeyvatMaterialGlobals globals, string outputDirectory)
        {
            var errors = new List<string>(); var times = new[] { 0f, 1.37f };
            var report = new WindDiagnostic
            {
                method = "Read-only CPU evaluation of TeyvatMaterialGlobals.Displacement (the runtime mirror of TGDeform) at two fixed times on every active foliage vertex/placement. Scene MeshFilter samples are checked against the owning MeshRenderer.localBounds (resolved from persisted expanded mesh bounds), recorded separately from the mesh asset bounds; instanced grass is checked against the configured world draw bounds. Globals and material/mesh state are not changed.",
                limitations = "Two fixed times are starting samples, not exhaustive oscillation extrema. CPU checks do not establish that every GPU pass uses the same function, actual frustum culling, motion quality or performance. Render captures and shader-pass inspection are separate evidence. Existing submission counters are observed without issuing a draw.",
                fixedTimes = times, positionStartingTolerance = WindPositionTolerance,
            };
            try
            {
                if (globals == null) throw new ArgumentNullException(nameof(globals));
                var direction = globals.windDirection.sqrMagnitude > .001f ? globals.windDirection.normalized : Vector2.right;
                var wind = new Vector4(direction.x, direction.y, globals.windStrength, 0); report.wind = wind;
                var shaderWind = Shader.GetGlobalVector("_TG_Wind");
                report.colorFieldPresent = globals.colorField != null;
                report.globalInterfaceMatches = (new Vector3(shaderWind.x, shaderWind.y, shaderWind.z) - new Vector3(wind.x, wind.y, wind.z)).sqrMagnitude < .000001f
                    && Shader.GetGlobalTexture("_TG_ColorField") == globals.colorField
                    && (Shader.GetGlobalVector("_TG_ColorBounds") - globals.colorBounds).sqrMagnitude < .000001f
                    && Mathf.Abs(Shader.GetGlobalFloat("_TG_ColorFieldEnabled") - (globals.colorField != null ? 1 : 0)) < .0001f;
                if (!report.globalInterfaceMatches) errors.Add("Current shader wind/color globals do not match the configured shared source");
                if (!report.colorFieldPresent) errors.Add("Shared ground/grass color field is missing");
                if (globals.colorBounds.z <= 0 || globals.colorBounds.w <= 0) errors.Add("Shared world color bounds have invalid inverse dimensions");
                report.configuredGrassInstances = globals.grassInstances == null ? 0 : globals.grassInstances.Length;
                report.submittedInstancesAtReview = globals.submittedInstances; report.submittedBatchesAtReview = globals.submittedBatches; report.submissionFramesAtReview = globals.submissionFrames;
                if (report.configuredGrassInstances == 0) errors.Add("No grass instances are configured");
                foreach (var time in times)
                {
                    wind.w = time;
                    foreach (var phase in new[] { 0f, .37f, 1f })
                        report.syntheticZeroMaskMaxDisplacement = Mathf.Max(report.syntheticZeroMaskMaxDisplacement,
                            TeyvatMaterialGlobals.Displacement(new Vector3(2.3f, .8f, -4.7f), new Color(0, 0, phase, 1), wind, 1).magnitude);
                }
                if (report.syntheticZeroMaskMaxDisplacement > WindPositionTolerance) errors.Add("Zero red/green root masks produce a displacement");
                var checks = new List<WindMeshCheck>();
                if (globals.grassMesh != null && globals.grassMaterial != null && report.configuredGrassInstances > 0)
                    checks.Add(CheckWindMesh("Instanced grass", globals.grassMesh, globals.grassMaterial, globals.grassInstances, true, globals.grassBounds, wind, times));
                else errors.Add("Instanced grass mesh/material/placements are incomplete");
                if (!globals.gameObject.scene.IsValid()) errors.Add("Shared globals are not attached to a loaded scene");
                else foreach (var filter in globals.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MeshFilter>()))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (filter.sharedMesh == null || renderer == null || !renderer.enabled) continue;
                    var foliage = renderer.sharedMaterials.Where(m => m != null && m.shader != null && m.shader.name == "Teyvat/URP/Foliage").ToArray();
                    if (foliage.Length == 0) continue;
                    // All vertices conservatively use the largest response of the material slots.
                    var material = foliage.OrderByDescending(m => m.HasProperty("_WindResponse") ? m.GetFloat("_WindResponse") : 0).First();
                    checks.Add(CheckWindMesh(filter.name, filter.sharedMesh, material, new[] { filter.transform.localToWorldMatrix }, false, renderer.localBounds, wind, times));
                }
                if (checks.Count == 0) errors.Add("No foliage meshes were available for deformation checks");
                foreach (var check in checks.Where(c => !c.passed)) errors.Add(check.renderer + ": " + string.Join("; ", check.errors));
                if (!checks.Any(c => c.tipSamples > 0 && c.maxBetweenTimeDisplacement > WindPositionTolerance)) errors.Add("No authored foliage tip changed position between the two fixed times");
                report.meshes = checks.ToArray();
            }
            catch (Exception exception) { errors.Add(exception.GetType().Name + ": " + exception.Message); }
            report.criticalErrors = errors.ToArray(); report.passed = errors.Count == 0;
            Directory.CreateDirectory(outputDirectory); File.WriteAllText(Path.Combine(outputDirectory, "WindBoundsValidation.json"), JsonUtility.ToJson(report, true));
            return report.passed;
        }

        static WindMeshCheck CheckWindMesh(string name, Mesh mesh, Material material, Matrix4x4[] placements, bool worldBounds, Bounds drawBounds, Vector4 wind, float[] times)
        {
            var errors = new List<string>(); var check = new WindMeshCheck { renderer = name, mesh = mesh.name, placements = placements.Length, usesInstanceWorldBounds = worldBounds,
                instanceWorldBounds = worldBounds ? drawBounds : default(Bounds), rendererLocalBounds = worldBounds ? default(Bounds) : drawBounds };
            var required = new[] { "_BaseColor", "_RootColor", "_TipColor", "_WindResponse", "_Family", "_UseColorField", "_AlphaClip", "_Cutoff", "_Cull" };
            check.materialInterfacePresent = material.shader != null && material.shader.name == "Teyvat/URP/Foliage" && required.All(material.HasProperty);
            if (!check.materialInterfacePresent) errors.Add("Shared foliage material interface is incomplete");
            if (worldBounds && !material.enableInstancing) errors.Add("Instanced grass material does not enable instancing");
            check.windResponse = material.HasProperty("_WindResponse") ? material.GetFloat("_WindResponse") : 0;
            if (!mesh.isReadable) { errors.Add("Foliage mesh is not readable"); check.errors = errors.ToArray(); return check; }
            var vertices = mesh.vertices; var colors = mesh.colors; check.vertices = vertices.Length;
            check.originalVertexBounds = VerticesBounds(vertices); check.meshAssetBounds = mesh.bounds;
            if (!worldBounds) check.rendererLocalBoundsPaddingPerSide = new Vector3(
                Mathf.Min(check.originalVertexBounds.min.x - drawBounds.min.x, drawBounds.max.x - check.originalVertexBounds.max.x),
                Mathf.Min(check.originalVertexBounds.min.y - drawBounds.min.y, drawBounds.max.y - check.originalVertexBounds.max.y),
                Mathf.Min(check.originalVertexBounds.min.z - drawBounds.min.z, drawBounds.max.z - check.originalVertexBounds.max.z));
            if (colors.Length != vertices.Length) errors.Add("Foliage wind masks are missing");
            else foreach (var placement in placements)
            {
                if (Mathf.Abs(placement.determinant) < .000001f) { errors.Add("Foliage placement transform is singular"); continue; }
                var inverse = placement.inverse; var localBounds = drawBounds; localBounds.Expand(WindPositionTolerance * 2);
                var worldDrawBounds = drawBounds; worldDrawBounds.Expand(WindPositionTolerance * 2);
                for (var vertex = 0; vertex < vertices.Length; vertex++)
                {
                    var position = placement.MultiplyPoint3x4(vertices[vertex]); var mask = colors[vertex]; var previous = Vector3.zero;
                    for (var sample = 0; sample < times.Length; sample++)
                    {
                        wind.w = times[sample]; var displacement = TeyvatMaterialGlobals.Displacement(position, mask, wind, check.windResponse);
                        check.vertexTimeSamples++; check.maxDisplacement = Mathf.Max(check.maxDisplacement, displacement.magnitude);
                        if (Mathf.Abs(mask.r) <= .00001f && Mathf.Abs(mask.g) <= .00001f) { check.rootSamples++; check.maxRootDisplacement = Mathf.Max(check.maxRootDisplacement, displacement.magnitude); }
                        if (mask.r >= .99f) { check.tipSamples++; check.maxTipDisplacement = Mathf.Max(check.maxTipDisplacement, displacement.magnitude); }
                        if (sample > 0) check.maxBetweenTimeDisplacement = Mathf.Max(check.maxBetweenTimeDisplacement, (displacement - previous).magnitude);
                        previous = displacement; var moved = position + displacement;
                        if (worldBounds ? !worldDrawBounds.Contains(moved) : !localBounds.Contains(inverse.MultiplyPoint3x4(moved))) check.outsideBoundsSamples++;
                    }
                }
            }
            if (check.rootSamples > 0 && check.maxRootDisplacement > WindPositionTolerance) errors.Add("Authored root vertices move despite zero red/green masks");
            if (check.tipSamples > 0 && check.windResponse > 0 && check.maxTipDisplacement <= WindPositionTolerance) errors.Add("Authored tip vertices do not move");
            if (check.outsideBoundsSamples > 0) errors.Add("Deformed vertices leave the configured expanded culling bounds");
            check.errors = errors.ToArray(); check.passed = errors.Count == 0; return check;
        }

        static Bounds VerticesBounds(Vector3[] vertices)
        {
            if (vertices == null || vertices.Length == 0) return new Bounds();
            var bounds = new Bounds(vertices[0], Vector3.zero); for (var i = 1; i < vertices.Length; i++) bounds.Encapsulate(vertices[i]); return bounds;
        }
        static Bounds ManifestBounds(SourceBounds value, bool mirrorX, bool mirrorZ)
        {
            if (value == null || value.min == null || value.max == null || value.min.Length != 3 || value.max.Length != 3) throw new InvalidDataException("Malformed source bounds");
            var min = new Vector3(mirrorX ? -value.max[0] : value.min[0], value.min[1], mirrorZ ? -value.max[2] : value.min[2]);
            var max = new Vector3(mirrorX ? -value.min[0] : value.max[0], value.max[1], mirrorZ ? -value.min[2] : value.max[2]);
            return new Bounds((min + max) * .5f, max - min);
        }
        static float BoundsError(Bounds a, Bounds b) { return Mathf.Max(MaxComponent(a.min - b.min), MaxComponent(a.max - b.max)); }
        static float MaxComponent(Vector3 value) { return Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z)); }
        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        static Color ArrayColor(float[] values) { if (values == null || values.Length != 4) throw new InvalidDataException("Malformed source RGBA range"); return new Color(values[0], values[1], values[2], values[3]); }
        static float ColorError(Color a, Color b) { return Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b), Mathf.Abs(a.a - b.a)); }
        static Color ColorRange(Color[] colors, bool maximum)
        {
            return new Color(maximum ? colors.Max(c => c.r) : colors.Min(c => c.r), maximum ? colors.Max(c => c.g) : colors.Min(c => c.g), maximum ? colors.Max(c => c.b) : colors.Min(c => c.b), maximum ? colors.Max(c => c.a) : colors.Min(c => c.a));
        }
        static Vector3 VectorRange(Vector3[] values, bool maximum)
        {
            return new Vector3(maximum ? values.Max(v => v.x) : values.Min(v => v.x), maximum ? values.Max(v => v.y) : values.Min(v => v.y), maximum ? values.Max(v => v.z) : values.Min(v => v.z));
        }
    }
}
