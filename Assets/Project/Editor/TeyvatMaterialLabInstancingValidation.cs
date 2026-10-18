using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FightingAllstar.EditorTools
{
    public static partial class TeyvatMaterialLab
    {
        [Serializable] sealed class InstanceCoverageBatch
        {
            public int submissionIndex;
            public int firstIndex;
            public int expectedCount;
            public int visibleCount;
            public int[] missingIndices;
        }

        [Serializable] sealed class InstanceCoverageBoundary
        {
            public int index;
            public int nominalSubmission;
            public int offsetWithinSubmission;
            public bool visible;
            public Color linearRGB;
        }

        [Serializable] sealed class InstanceCoverageDiagnostic
        {
            public string schema = "TG_WP02_InstancedCoverageValidation_v1";
            public string method = "Temporary additive rig scene with a 70x60 grid of separated white quads, submitted by the actual ExecuteAlways TeyvatMaterialGlobals RenderMeshInstanced route. Two URP render requests warm the same-scene orthographic camera. Linear floating-point readback checks nine pixels around each projected instance center, all 4200 instances, API counters and a separate black-background spot.";
            public string limitations = "Coverage and matrix/submission-boundary microtest only. It does not establish per-instance frustum culling, shadow batching/correctness, textured foliage quality, full-scene rendering or standalone performance. Counters describe API submissions, not guaranteed hardware draw calls. Rig objects use layer29; the camera also includes layer0 because the unchanged runtime RenderParams defaults to layer0. Camera.scene isolates this temporary scene.";
            public bool passed;
            public bool linearColorSpace;
            public bool floatingPointLinearTarget;
            public bool allMatricesFiniteUniform;
            public bool backgroundPassed;
            public int expectedInstances = 4200;
            public int visibleInstances;
            public int submittedInstances;
            public int submittedBatches;
            public int submissionFrames;
            public int runtimeInstancesPerSubmission;
            public int expectedSubmissions;
            public float cellSizeMetres = .3f;
            public float quadWidthMetres = .18f;
            public float minimumWhiteChannelStartingThreshold = .95f;
            public float maximumBackgroundChannelStartingThreshold = .01f;
            public Color backgroundLinear;
            public Vector3 backgroundWorld;
            public int[] missingIndices;
            public InstanceCoverageBatch[] batches;
            public InstanceCoverageBoundary[] boundaries;
            public string capture = "Diagnostics/InstancedCoverage.png";
            public string[] errors;
        }

        static bool ValidateInstancedCoverage(string outputDirectory)
        {
            const int columns = 70, rows = 60, count = columns * rows;
            const int width = 1600, height = 900, isolatedLayer = 29;
            const int batchSize = FightingAllstar.EnvironmentArt.TeyvatMaterialGlobals.InstancesPerSubmission;
            const float spacing = .3f, halfQuad = .09f, whiteThreshold = .95f, blackThreshold = .01f;
            var report = new InstanceCoverageDiagnostic();
            report.runtimeInstancesPerSubmission = batchSize;
            report.expectedSubmissions = (count + batchSize - 1) / batchSize;
            var errors = new List<string>();
            var missing = new List<int>();
            var batches = new List<InstanceCoverageBatch>();
            var boundaries = new List<InstanceCoverageBoundary>();
            var oldActiveScene = SceneManager.GetActiveScene();
            var oldTarget = RenderTexture.active;
            var oldDebug = Shader.GetGlobalFloat("_TG_DebugAlbedo");
            var temporaryScene = default(Scene);
            GameObject rig = null;
            Mesh quad = null;
            Material foliage = null;
            Texture2D white = null, readback = null, preview = null;
            RenderTexture target = null;
            FightingAllstar.EnvironmentArt.TeyvatMaterialGlobals instanceRoute = null;
            Directory.CreateDirectory(outputDirectory);
            try
            {
                var sourceGrass=globals==null?null:globals.grassMaterial;
                if (sourceGrass==null) throw new InvalidOperationException("The saved lab Grass material must exist before the instancing coverage test.");
                report.linearColorSpace = QualitySettings.activeColorSpace == ColorSpace.Linear;
                if (!report.linearColorSpace) errors.Add("The instanced-coverage proof requires the selected Linear project.");
                temporaryScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(temporaryScene);
                rig = new GameObject("Temporary actual-runtime instanced coverage rig")
                    { layer = isolatedLayer, hideFlags = HideFlags.HideAndDontSave };
                white = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
                    { name = "Temporary white coverage albedo", hideFlags = HideFlags.HideAndDontSave };
                white.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                white.Apply(false, false);
                foliage = new Material(sourceGrass)
                    { name = "Temporary actual-foliage instanced proof", hideFlags = HideFlags.HideAndDontSave, enableInstancing = true };
                foliage.SetTexture("_BaseMap", white);
                foliage.SetColor("_BaseColor", Color.white);
                foliage.SetColor("_RootColor", Color.white);
                foliage.SetColor("_TipColor", Color.white);
                if (foliage.HasProperty("_CanopyShadeColor")) foliage.SetColor("_CanopyShadeColor", Color.white);
                foliage.SetFloat("_Family", 2);
                foliage.SetFloat("_WindResponse", 0);
                foliage.SetFloat("_UseColorField", 0);
                foliage.SetFloat("_AlphaClip", 0);
                foliage.SetFloat("_Cull", 0);
                quad = new Mesh { name = "Temporary horizontal instancing proof quad", hideFlags = HideFlags.HideAndDontSave };
                quad.vertices = new[] { new Vector3(-halfQuad, 0, -halfQuad), new Vector3(-halfQuad, 0, halfQuad), new Vector3(halfQuad, 0, -halfQuad), new Vector3(halfQuad, 0, halfQuad) };
                quad.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                quad.uv = new[] { Vector2.zero, Vector2.up, Vector2.right, Vector2.one };
                var mask = new Color(0, 0, 0, 1);
                quad.colors = new[] { mask, mask, mask, mask };
                quad.triangles = new[] { 0, 1, 2, 2, 1, 3 };
                quad.RecalculateBounds();

                var center = new Vector3(120, 0, 120);
                var centers = new Vector3[count];
                var matrices = new Matrix4x4[count];
                report.allMatricesFiniteUniform = true;
                for (var row = 0; row < rows; row++) for (var col = 0; col < columns; col++)
                {
                    var index = row * columns + col;
                    centers[index] = center + new Vector3((col - (columns - 1) * .5f) * spacing, 0, (row - (rows - 1) * .5f) * spacing);
                    matrices[index] = Matrix4x4.TRS(centers[index], Quaternion.identity, Vector3.one);
                    for (var element = 0; element < 16; element++)
                        if (float.IsNaN(matrices[index][element]) || float.IsInfinity(matrices[index][element])) report.allMatricesFiniteUniform = false;
                    var scale = matrices[index].lossyScale;
                    if (Mathf.Abs(scale.x - 1) > .00001f || Mathf.Abs(scale.y - 1) > .00001f || Mathf.Abs(scale.z - 1) > .00001f) report.allMatricesFiniteUniform = false;
                }
                instanceRoute = rig.AddComponent<FightingAllstar.EnvironmentArt.TeyvatMaterialGlobals>();
                instanceRoute.fixedTime = true;
                instanceRoute.fixedSeconds = 0;
                instanceRoute.windStrength = 0;
                instanceRoute.grassMesh = quad;
                instanceRoute.grassMaterial = foliage;
                instanceRoute.grassInstances = matrices;
                instanceRoute.grassBounds = new Bounds(center, new Vector3(columns * spacing + 2, 4, rows * spacing + 2));
                instanceRoute.Apply();

                var cameraObject = new GameObject("Temporary same-scene orthographic coverage camera")
                    { layer = isolatedLayer, hideFlags = HideFlags.HideAndDontSave };
                cameraObject.transform.SetParent(rig.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.scene = temporaryScene;
                camera.transform.SetPositionAndRotation(center + Vector3.up * 20, Quaternion.LookRotation(Vector3.down, Vector3.forward));
                camera.orthographic = true;
                camera.orthographicSize = 10.5f;
                camera.aspect = width / (float)height;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 40;
                camera.cullingMask = (1 << isolatedLayer) | 1;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.allowHDR = true;
                camera.allowMSAA = false;
                var additional = camera.GetUniversalAdditionalCameraData();
                additional.requiresDepthTexture = false;
                additional.requiresColorTexture = false;
                additional.renderPostProcessing = false;
                target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                target.filterMode = FilterMode.Point;
                target.antiAliasing = 1;
                readback = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true)
                    { name = "Temporary linear instanced coverage readback", hideFlags = HideFlags.HideAndDontSave };
                report.floatingPointLinearTarget = target.format == RenderTextureFormat.ARGBFloat && !target.sRGB;
                Shader.SetGlobalFloat("_TG_DebugAlbedo", 1);
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                report.submittedInstances = instanceRoute.submittedInstances;
                report.submittedBatches = instanceRoute.submittedBatches;
                report.submissionFrames = instanceRoute.submissionFrames;
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readback.Apply(false, false);

                var covered = new bool[count];
                var measured = new Color[count];
                for (var index = 0; index < count; index++)
                {
                    measured[index] = InstancingReadNine(camera, readback, centers[index]);
                    var rgb = measured[index];
                    covered[index] = InstancingFiniteRGB(rgb) && rgb.r >= whiteThreshold && rgb.g >= whiteThreshold && rgb.b >= whiteThreshold;
                    if (covered[index]) report.visibleInstances++; else missing.Add(index);
                }
                for (var first = 0; first < count; first += batchSize)
                {
                    var batchCount = Mathf.Min(batchSize, count - first);
                    var batchMissing = new List<int>();
                    for (var i = first; i < first + batchCount; i++) if (!covered[i]) batchMissing.Add(i);
                    batches.Add(new InstanceCoverageBatch { submissionIndex = first / batchSize, firstIndex = first, expectedCount = batchCount,
                        visibleCount = batchCount - batchMissing.Count, missingIndices = batchMissing.ToArray() });
                    var checkedOffsets = new HashSet<int>();
                    foreach (var offset in new[] { 0, 1, 254, 255, 256, 498, 499, 500, 510, 511, 512, batchCount - 2, batchCount - 1 })
                    {
                        var index = first + offset;
                        if (offset < 0 || offset >= batchCount || !checkedOffsets.Add(offset)) continue;
                        boundaries.Add(new InstanceCoverageBoundary { index = index, nominalSubmission = first / batchSize,
                            offsetWithinSubmission = offset, visible = covered[index], linearRGB = measured[index] });
                    }
                }
                report.backgroundWorld = center + new Vector3(15, 0, 9.5f);
                report.backgroundLinear = InstancingReadNine(camera, readback, report.backgroundWorld);
                var background = report.backgroundLinear;
                report.backgroundPassed = InstancingFiniteRGB(background) && Mathf.Abs(background.r) <= blackThreshold
                    && Mathf.Abs(background.g) <= blackThreshold && Mathf.Abs(background.b) <= blackThreshold;
                if (!report.allMatricesFiniteUniform) errors.Add("Generated proof matrices are not all finite unit-scale transforms.");
                if (!report.floatingPointLinearTarget) errors.Add("Coverage target is not unambiguously Linear floating point.");
                if (report.submissionFrames < 2) errors.Add("The actual runtime route did not observe both warm render requests.");
                if (report.submittedInstances != count || report.submittedBatches != report.expectedSubmissions)
                    errors.Add("Actual runtime API counters do not describe 4200 instances in " + report.expectedSubmissions + " submissions at the runtime's current " + batchSize + "-instance ceiling.");
                if (report.visibleInstances != count) errors.Add("GPU coverage is missing " + missing.Count + " of 4200 instances; inspect missingIndices and per-submission boundaries.");
                if (!report.backgroundPassed) errors.Add("The separate background sample was not black; the proof frame may be contaminated or uninitialized.");

                preview = new Texture2D(width, height, TextureFormat.RGB24, false, false)
                    { name = "Temporary instancing proof PNG", hideFlags = HideFlags.HideAndDontSave };
                var pixels = readback.GetPixels();
                var gamma = new Color32[pixels.Length];
                for (var i = 0; i < pixels.Length; i++) gamma[i] = pixels[i].gamma;
                preview.SetPixels32(gamma);
                preview.Apply(false, false);
                var imagePath = Path.Combine(outputDirectory, report.capture);
                Directory.CreateDirectory(Path.GetDirectoryName(imagePath));
                File.WriteAllBytes(imagePath, preview.EncodeToPNG());
            }
            catch (Exception exception) { errors.Add(exception.ToString()); }
            finally
            {
                Shader.SetGlobalFloat("_TG_DebugAlbedo", oldDebug);
                RenderTexture.active = oldTarget;
                if (rig != null) Object.DestroyImmediate(rig);
                if (temporaryScene.IsValid() && temporaryScene.isLoaded) EditorSceneManager.CloseScene(temporaryScene, true);
                if (oldActiveScene.IsValid() && oldActiveScene.isLoaded) SceneManager.SetActiveScene(oldActiveScene);
                if (preview != null) Object.DestroyImmediate(preview);
                if (readback != null) Object.DestroyImmediate(readback);
                if (target != null) RenderTexture.ReleaseTemporary(target);
                if (quad != null) Object.DestroyImmediate(quad);
                if (foliage != null) Object.DestroyImmediate(foliage);
                if (white != null) Object.DestroyImmediate(white);
                if (globals != null) globals.Apply();
            }
            report.missingIndices = missing.ToArray();
            report.batches = batches.ToArray();
            report.boundaries = boundaries.ToArray();
            report.errors = errors.ToArray();
            report.passed = errors.Count == 0 && report.visibleInstances == count && report.backgroundPassed;
            File.WriteAllText(Path.Combine(outputDirectory, "InstancingCoverageValidation.json"), JsonUtility.ToJson(report, true));
            return report.passed;
        }

        static Color InstancingReadNine(Camera camera, Texture2D texture, Vector3 world)
        {
            var viewport = camera.WorldToViewportPoint(world);
            var x = Mathf.RoundToInt(viewport.x * texture.width - .5f);
            var y = Mathf.RoundToInt(viewport.y * texture.height - .5f);
            if (viewport.z <= 0 || x < 1 || y < 1 || x >= texture.width - 1 || y >= texture.height - 1)
                throw new InvalidOperationException("Instancing proof sample is outside the target: " + world);
            var value = new Color(0, 0, 0, 0);
            for (var dy = -1; dy <= 1; dy++) for (var dx = -1; dx <= 1; dx++) value += texture.GetPixel(x + dx, y + dy);
            return value / 9;
        }

        static bool InstancingFiniteRGB(Color color)
        {
            return !float.IsNaN(color.r) && !float.IsInfinity(color.r) && !float.IsNaN(color.g) && !float.IsInfinity(color.g)
                && !float.IsNaN(color.b) && !float.IsInfinity(color.b);
        }
    }
}
