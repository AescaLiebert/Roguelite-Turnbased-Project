using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace FightingAllstar.EditorTools
{
    public static partial class TeyvatWaterLab
    {
        [Serializable] sealed class WaterDepthSample
        {
            public string name;
            public float bedDepthMetres;
            public float expectedFraction;
            public float actualFraction;
            public float minimumFraction;
            public float maximumFraction;
            public float absoluteErrorMetres;
            public float rawHardwareDepth;
            public float sceneEyeDepthMetres;
            public float surfaceEyeDepthMetres;
            public float unclampedThicknessMetres;
            public bool passed;
        }

        [Serializable] sealed class WaterDepthDiagnostic
        {
            public string schema = "TG_WP06_WaterDepthValidation_v2";
            public string method = "Temporary isolated layer-31 known-depth rig, independent perspective and orthographic cameras. The actual water shader outputs thickness / DepthRange into a linear floating-point render target. Nine center pixels are averaged per bed after two render requests.";
            public string limitations = "Numerical depth and fallback validation only; this does not validate water appearance, transparency sorting, refraction, reflected light or player-build performance. All temporary scene objects and resources are destroyed after capture.";
            public bool passed;
            public bool requiresCameraDepthTexture;
            public bool floatingPointLinearTarget;
            public bool sceneDepthMonotonic;
            public float depthRangeMetres = 4;
            public float fractionStartingTolerance = .025f;
            public float equivalentMetreStartingTolerance = .1f;
            public WaterDepthSample[] sceneDepth;
            public WaterDepthSample[] fallbackDepth;
            public string[] captures;
            public string[] errors;
        }

        static bool ValidateWaterDepth()
        { return ValidateWaterDepthRig(output+"/Perspective",false) & ValidateWaterDepthRig(output+"/Orthographic",true); }

        static bool ValidateWaterDepthRig(string outputDirectory,bool orthographic)
        {
            const int width = 1600, height = 900, isolatedLayer = 31;
            const float range = 4, fractionTolerance = .025f;
            var report = new WaterDepthDiagnostic();
            var errors = new List<string>();
            var sceneSamples = new List<WaterDepthSample>();
            var fallbackSamples = new List<WaterDepthSample>();
            var captures = new List<string>();
            GameObject rig = null;
            Material bedMaterial = null, waterMaterial = null;
            Mesh waterMesh = null;
            RenderTexture target = null;
            Texture2D readback = null;
            var previousActive = RenderTexture.active;
            Directory.CreateDirectory(outputDirectory);
            try
            {
                if (!materials.ContainsKey("River"))
                    throw new InvalidOperationException("The River material must exist before the depth diagnostic runs.");
                waterMaterial = new Material(materials["River"]) { name = "Temporary actual-water depth diagnostic", hideFlags = HideFlags.HideAndDontSave };
                if (!waterMaterial.HasProperty("_DebugDepth"))
                    throw new InvalidOperationException("Water shader is missing the _DebugDepth diagnostic property.");
                waterMaterial.SetFloat("_DebugDepth", 1);
                waterMaterial.SetFloat("_UseSceneDepth", 1);
                waterMaterial.SetFloat("_DepthRange", range);
                waterMaterial.SetFloat("_Opacity", 1);
                waterMaterial.SetFloat("_Mode", 0);
                var bedShader = Shader.Find("Universal Render Pipeline/Lit");
                if (bedShader == null) throw new InvalidOperationException("URP Lit is unavailable for the depth-diagnostic bed.");
                bedMaterial = new Material(bedShader) { name = "Temporary opaque depth bed", hideFlags = HideFlags.HideAndDontSave };
                bedMaterial.SetColor("_BaseColor", new Color(.25f, .25f, .25f, 1));
                bedMaterial.SetFloat("_Metallic", 0);
                bedMaterial.SetFloat("_Smoothness", 0);

                rig = new GameObject("Temporary WP02 water-depth validation") { hideFlags = HideFlags.HideAndDontSave, layer = isolatedLayer };
                waterMesh = new Mesh { name = "Temporary upward water diagnostic quad", hideFlags = HideFlags.HideAndDontSave };
                waterMesh.vertices = new[] { new Vector3(-1.5f, 0, -2.5f), new Vector3(-1.5f, 0, 2.5f), new Vector3(1.5f, 0, -2.5f), new Vector3(1.5f, 0, 2.5f) };
                waterMesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.right, Vector2.one };
                waterMesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
                waterMesh.RecalculateNormals();
                waterMesh.RecalculateBounds();
                var bedDepths = new[] { .2f, 1f, 3f };
                var centers = new[] { new Vector3(115, 0, 0), new Vector3(120, 0, 0), new Vector3(125, 0, 0) };
                for (var index = 0; index < centers.Length; index++)
                {
                    var bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bed.name = "Opaque bed " + bedDepths[index] + " metres below water";
                    bed.hideFlags = HideFlags.HideAndDontSave;
                    bed.layer = isolatedLayer;
                    bed.transform.SetParent(rig.transform, false);
                    bed.transform.position = centers[index] + Vector3.down * (bedDepths[index] + .1f);
                    bed.transform.localScale = new Vector3(3, .2f, 5);
                    var bedRenderer = bed.GetComponent<MeshRenderer>();
                    bedRenderer.sharedMaterial = bedMaterial;
                    bedRenderer.shadowCastingMode = ShadowCastingMode.Off;
                    bedRenderer.receiveShadows = false;
                    var surface = new GameObject("Actual water above bed " + index) { layer = isolatedLayer, hideFlags = HideFlags.HideAndDontSave };
                    surface.transform.SetParent(rig.transform, false);
                    surface.transform.position = centers[index];
                    surface.AddComponent<MeshFilter>().sharedMesh = waterMesh;
                    var waterRenderer = surface.AddComponent<MeshRenderer>();
                    waterRenderer.sharedMaterial = waterMaterial;
                    waterRenderer.shadowCastingMode = ShadowCastingMode.Off;
                    waterRenderer.receiveShadows = false;
                }

                var cameraObject = new GameObject("Temporary water-depth perspective camera") { hideFlags = HideFlags.HideAndDontSave, layer = isolatedLayer };
                cameraObject.transform.SetParent(rig.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.transform.SetPositionAndRotation(new Vector3(120, 12, 0), Quaternion.LookRotation(Vector3.down, Vector3.forward));
                camera.orthographic = orthographic;
                camera.orthographicSize=6;
                camera.fieldOfView = 50;
                camera.aspect = width / (float)height;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 40;
                camera.cullingMask = 1 << isolatedLayer;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.allowHDR = true;
                camera.allowMSAA = false;
                var cameraData = camera.GetUniversalAdditionalCameraData();
                cameraData.requiresDepthTexture = true;
                cameraData.requiresColorTexture = false;
                cameraData.renderPostProcessing = false;
                report.requiresCameraDepthTexture = cameraData.requiresDepthTexture;

                target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                target.antiAliasing = 1;
                target.filterMode = FilterMode.Point;
                readback = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true) { hideFlags = HideFlags.HideAndDontSave };
                report.floatingPointLinearTarget = target.format == RenderTextureFormat.ARGBFloat && !target.sRGB;
                WaterDepthRenderAndRead(camera, target, readback);
                for (var index = 0; index < centers.Length; index++)
                    sceneSamples.Add(WaterDepthReadSample(camera, readback, centers[index], bedDepths[index], bedDepths[index] / range, range, fractionTolerance, "Scene depth"));
                var sceneCapture = "Diagnostics/WaterDepth.png";
                WaterDepthSavePreview(readback, Path.Combine(outputDirectory, sceneCapture));
                captures.Add(sceneCapture);
                report.sceneDepthMonotonic = sceneSamples[0].actualFraction < sceneSamples[1].actualFraction && sceneSamples[1].actualFraction < sceneSamples[2].actualFraction;

                // Keep the camera, beds and actual water binding unchanged, and
                // expose each term of the depth expression independently. Modes
                // 3 and 4 encode eye depth / 40 m to preserve floating-point data.
                foreach (var diagnosticMode in new[] { 2, 3, 4 })
                {
                    waterMaterial.SetFloat("_DebugDepth", diagnosticMode);
                    WaterDepthRenderAndRead(camera, target, readback);
                    for (var index = 0; index < centers.Length; index++)
                    {
                        var term = WaterDepthReadSample(camera, readback, centers[index], bedDepths[index], 0, 1, float.MaxValue, "Depth-expression term").actualFraction;
                        if (diagnosticMode == 2) sceneSamples[index].rawHardwareDepth = term;
                        if (diagnosticMode == 3) sceneSamples[index].sceneEyeDepthMetres = term * 40;
                        if (diagnosticMode == 4) sceneSamples[index].surfaceEyeDepthMetres = term * 40;
                    }
                    var termCapture = diagnosticMode == 2 ? "Diagnostics/WaterDepth_RawHardware.png" : diagnosticMode == 3 ? "Diagnostics/WaterDepth_SceneEye.png" : "Diagnostics/WaterDepth_SurfaceEye.png";
                    WaterDepthSavePreview(readback, Path.Combine(outputDirectory, termCapture));
                    captures.Add(termCapture);
                }
                foreach (var sample in sceneSamples)
                    sample.unclampedThicknessMetres = sample.sceneEyeDepthMetres - sample.surfaceEyeDepthMetres;

                waterMaterial.SetFloat("_DebugDepth", 1);
                waterMaterial.SetFloat("_UseSceneDepth", 0);
                waterMaterial.SetFloat("_FallbackDepth", 2);
                WaterDepthRenderAndRead(camera, target, readback);
                for (var index = 0; index < centers.Length; index++)
                    fallbackSamples.Add(WaterDepthReadSample(camera, readback, centers[index], bedDepths[index], .5f, range, fractionTolerance, "Fixed fallback"));
                var fallbackCapture = "Diagnostics/WaterDepth_Fallback.png";
                WaterDepthSavePreview(readback, Path.Combine(outputDirectory, fallbackCapture));
                captures.Add(fallbackCapture);

                foreach (var sample in sceneSamples)
                    if (!sample.passed) errors.Add("Scene-depth fraction mismatch above " + sample.bedDepthMetres + " metre bed: expected " + sample.expectedFraction + ", measured " + sample.actualFraction + ".");
                foreach (var sample in fallbackSamples)
                    if (!sample.passed) errors.Add("Fallback fraction mismatch above " + sample.bedDepthMetres + " metre bed: expected .5, measured " + sample.actualFraction + ".");
                if (!report.sceneDepthMonotonic) errors.Add("Water thickness did not increase monotonically with bed depth.");
                if (!report.requiresCameraDepthTexture) errors.Add("The diagnostic camera did not request its depth texture.");
                if (!report.floatingPointLinearTarget) errors.Add("Readback target is not an unambiguous linear floating-point target.");
            }
            catch (Exception exception)
            {
                errors.Add(exception.ToString());
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (rig != null) Object.DestroyImmediate(rig);
                if (readback != null) Object.DestroyImmediate(readback);
                if (target != null) RenderTexture.ReleaseTemporary(target);
                if (waterMesh != null) Object.DestroyImmediate(waterMesh);
                if (waterMaterial != null) Object.DestroyImmediate(waterMaterial);
                if (bedMaterial != null) Object.DestroyImmediate(bedMaterial);
            }
            report.sceneDepth = sceneSamples.ToArray();
            report.fallbackDepth = fallbackSamples.ToArray();
            report.captures = captures.ToArray();
            report.errors = errors.ToArray();
            report.passed = errors.Count == 0 && sceneSamples.Count == 3 && fallbackSamples.Count == 3;
            File.WriteAllText(Path.Combine(outputDirectory, "WaterDepthValidation.json"), JsonUtility.ToJson(report, true));
            return report.passed;
        }

        static void WaterDepthRenderAndRead(Camera camera, RenderTexture target, Texture2D readback)
        {
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            readback.Apply(false, false);
        }

        static WaterDepthSample WaterDepthReadSample(Camera camera, Texture2D texture, Vector3 center, float bedDepth, float expected, float range, float tolerance, string name)
        {
            var viewport = camera.WorldToViewportPoint(center);
            if (viewport.z <= 0 || viewport.x <= 0 || viewport.x >= 1 || viewport.y <= 0 || viewport.y >= 1)
                throw new InvalidOperationException("A water-depth sample is outside the perspective camera view.");
            var x = Mathf.Clamp(Mathf.RoundToInt(viewport.x * (texture.width - 1)), 1, texture.width - 2);
            var y = Mathf.Clamp(Mathf.RoundToInt(viewport.y * (texture.height - 1)), 1, texture.height - 2);
            float sum = 0, minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
            for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    var value = texture.GetPixel(x + dx, y + dy).r;
                    if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidOperationException("Water depth produced a non-finite pixel.");
                    sum += value; minimum = Mathf.Min(minimum, value); maximum = Mathf.Max(maximum, value);
                }
            var actual = sum / 9;
            return new WaterDepthSample
            {
                name = name, bedDepthMetres = bedDepth, expectedFraction = expected, actualFraction = actual,
                minimumFraction = minimum, maximumFraction = maximum, absoluteErrorMetres = Mathf.Abs(actual - expected) * range,
                passed = Mathf.Abs(actual - expected) <= tolerance && minimum >= expected - tolerance && maximum <= expected + tolerance,
            };
        }

        static void WaterDepthSavePreview(Texture2D linearFloat, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var preview = new Texture2D(linearFloat.width, linearFloat.height, TextureFormat.RGB24, false, false) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var colors = linearFloat.GetPixels();
                for (var index = 0; index < colors.Length; index++) colors[index] = colors[index].gamma;
                preview.SetPixels(colors); preview.Apply(false, false);
                File.WriteAllBytes(path, preview.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(preview); }
        }
    }
}
