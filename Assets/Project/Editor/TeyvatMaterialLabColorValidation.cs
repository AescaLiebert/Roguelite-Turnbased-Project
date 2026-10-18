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
        [Serializable] sealed class GroundGrassColorDiagnostic
        {
            public string schema = "TG_WP02_GroundGrassAlbedoValidation_v1";
            public string method = "Actual native TerrainGrassland layer-zero surface and actual Foliage grass-root surface, in a temporary isolated scene/layer30 rig. The common debug-albedo mode removes lighting/fog; after two URP render requests, linear floating-point RGB is averaged over 3x3 center pixels for each surface. Both centers share world Z and clamp to world-field U1.";
            public string limitations = "Numerical flat-root albedo parity only. This does not establish lit terrain/grass seams, imported blade masks, wind behavior, canopy appearance, texture tiling, combat playback or performance.";
            public bool passed;
            public bool linearColorSpace;
            public bool floatingPointLinearTarget;
            public float startingMaximumChannelTolerance = .003f;
            public Vector4 colorBounds;
            public Vector2 expectedFieldUV;
            public Color expectedFieldLinear;
            public Color terrainLinear;
            public Color grassRootLinear;
            public float terrainGrassMaximumChannelDifference;
            public float terrainExpectedMaximumChannelDifference;
            public float grassExpectedMaximumChannelDifference;
            public Vector3 terrainSampleWorld = new Vector3(120, 0, 20);
            public Vector3 grassSampleWorld = new Vector3(124, 0, 20);
            public string capture = "Diagnostics/GroundGrassAlbedoProof.png";
            public string[] errors;
        }

        static bool ValidateGroundGrassColor(string outputDirectory)
        {
            const int width = 1600, height = 900, isolatedLayer = 30;
            const float tolerance = .003f;
            var report = new GroundGrassColorDiagnostic();
            var errors = new List<string>();
            var oldDebug = Shader.GetGlobalFloat("_TG_DebugAlbedo");
            var oldActiveTexture = RenderTexture.active;
            var oldActiveScene = SceneManager.GetActiveScene();
            var rigScene = default(Scene);
            GameObject rig = null;
            TerrainData terrainData = null;
            TerrainLayer terrainLayer = null;
            Texture2D whiteTexture = null, readback = null, preview = null;
            Material terrainMaterial = null, grassMaterial = null;
            Mesh quad = null;
            RenderTexture target = null;
            Directory.CreateDirectory(outputDirectory);
            try
            {
                if (globals == null || globals.colorField == null)
                    throw new InvalidOperationException("The material lab must publish a readable sRGB world color field before this test.");
                if (!materials.ContainsKey("Terrain") || !materials.ContainsKey("Grass"))
                    throw new InvalidOperationException("The material lab Terrain and Grass material instances are required.");
                globals.Apply();
                report.colorBounds = globals.colorBounds;
                report.expectedFieldUV = new Vector2(1, Mathf.Clamp01((20 - report.colorBounds.y) * report.colorBounds.w));
                // Hardware converts each sRGB texel before filtering, with texel
                // centers at (index + .5)/size. CPU GetPixelBilinear(...).linear
                // uses a different sampling/conversion order on a varying field.
                report.expectedFieldLinear = SampleFieldLikeGPU(globals.colorField,report.expectedFieldUV);
                report.linearColorSpace = QualitySettings.activeColorSpace == ColorSpace.Linear;
                if (!report.linearColorSpace) errors.Add("This diagnostic requires the selected Linear color-space project.");

                rigScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(rigScene);
                rig = new GameObject("Temporary WP02 actual terrain and grass albedo parity rig")
                    { layer = isolatedLayer, hideFlags = HideFlags.HideAndDontSave };
                whiteTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
                    { name = "Temporary neutral sRGB terrain layer", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat };
                whiteTexture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                whiteTexture.Apply(false, false);
                terrainLayer = new TerrainLayer
                    { name = "Temporary neutral grass layer zero", hideFlags = HideFlags.HideAndDontSave,
                        diffuseTexture = whiteTexture, tileSize = Vector2.one, metallic = 0, smoothness = 0,
                        diffuseRemapMin = Vector4.zero, diffuseRemapMax = Vector4.one };
                terrainData = new TerrainData
                    { name = "Temporary actual native flat TerrainData", hideFlags = HideFlags.HideAndDontSave,
                        heightmapResolution = 33, size = new Vector3(3, 1, 3), alphamapResolution = 16 };
                terrainData.SetHeights(0, 0, new float[33, 33]);
                terrainData.terrainLayers = new[] { terrainLayer };
                var weights = new float[16, 16, 1];
                for (var y = 0; y < 16; y++) for (var x = 0; x < 16; x++) weights[y, x, 0] = 1;
                terrainData.SetAlphamaps(0, 0, weights);
                terrainMaterial = new Material(materials["Terrain"])
                    { name = "Temporary actual TerrainGrassland material", hideFlags = HideFlags.HideAndDontSave };
                var terrainObject = Terrain.CreateTerrainGameObject(terrainData);
                terrainObject.name = "Actual terrain grass layer zero";
                terrainObject.layer = isolatedLayer;
                terrainObject.hideFlags = HideFlags.HideAndDontSave;
                terrainObject.transform.SetParent(rig.transform, false);
                terrainObject.transform.position = new Vector3(118.5f, 0, 18.5f);
                var terrain = terrainObject.GetComponent<Terrain>();
                terrain.materialTemplate = terrainMaterial;
                terrain.basemapDistance = 100000;
                terrain.drawInstanced = true;
                terrain.shadowCastingMode = ShadowCastingMode.Off;
                terrain.Flush();

                grassMaterial = new Material(materials["Grass"])
                    { name = "Temporary actual foliage roots material", hideFlags = HideFlags.HideAndDontSave };
                grassMaterial.SetTexture("_BaseMap", whiteTexture);
                grassMaterial.SetColor("_BaseColor", Color.white);
                grassMaterial.SetColor("_RootColor", Color.white);
                grassMaterial.SetColor("_TipColor", Color.white);
                grassMaterial.SetFloat("_UseColorField", 1);
                grassMaterial.SetFloat("_WindResponse", 0);
                grassMaterial.SetFloat("_Family", 0);
                grassMaterial.SetFloat("_AlphaClip", 0);
                grassMaterial.SetFloat("_Cull", 0);
                quad = new Mesh { name = "Temporary flat grass-root proof quad", hideFlags = HideFlags.HideAndDontSave };
                quad.vertices = new[] { new Vector3(-1.5f, 0, -1.5f), new Vector3(-1.5f, 0, 1.5f), new Vector3(1.5f, 0, -1.5f), new Vector3(1.5f, 0, 1.5f) };
                quad.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                quad.uv = new[] { Vector2.zero, Vector2.up, Vector2.right, Vector2.one };
                var rootMask = new Color(0, 0, 0, 1);
                quad.colors = new[] { rootMask, rootMask, rootMask, rootMask };
                quad.triangles = new[] { 0, 1, 2, 2, 1, 3 };
                quad.RecalculateBounds();
                var grassObject = new GameObject("Actual grass-root shader surface")
                    { layer = isolatedLayer, hideFlags = HideFlags.HideAndDontSave };
                grassObject.transform.SetParent(rig.transform, false);
                grassObject.transform.position = report.grassSampleWorld;
                grassObject.AddComponent<MeshFilter>().sharedMesh = quad;
                var grassRenderer = grassObject.AddComponent<MeshRenderer>();
                grassRenderer.sharedMaterial = grassMaterial;
                grassRenderer.shadowCastingMode = ShadowCastingMode.Off;
                grassRenderer.receiveShadows = false;

                var cameraObject = new GameObject("Temporary ground-grass topdown perspective camera")
                    { layer = isolatedLayer, hideFlags = HideFlags.HideAndDontSave };
                cameraObject.transform.SetParent(rig.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.scene = rigScene;
                camera.transform.SetPositionAndRotation(new Vector3(122, 9, 20), Quaternion.LookRotation(Vector3.down, Vector3.forward));
                camera.orthographic = false;
                camera.fieldOfView = 50;
                camera.aspect = width / (float)height;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 25;
                camera.cullingMask = 1 << isolatedLayer;
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
                    { name = "Temporary linear proof readback", hideFlags = HideFlags.HideAndDontSave };
                report.floatingPointLinearTarget = target.format == RenderTextureFormat.ARGBFloat && !target.sRGB;
                Shader.SetGlobalFloat("_TG_DebugAlbedo", 1);
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readback.Apply(false, false);
                report.terrainLinear = GroundGrassReadNine(camera, readback, report.terrainSampleWorld);
                report.grassRootLinear = GroundGrassReadNine(camera, readback, report.grassSampleWorld);
                report.terrainGrassMaximumChannelDifference = GroundGrassRGBError(report.terrainLinear, report.grassRootLinear);
                report.terrainExpectedMaximumChannelDifference = GroundGrassRGBError(report.terrainLinear, report.expectedFieldLinear);
                report.grassExpectedMaximumChannelDifference = GroundGrassRGBError(report.grassRootLinear, report.expectedFieldLinear);
                if (!report.floatingPointLinearTarget) errors.Add("Proof render target is not unambiguously linear floating-point.");
                if (report.terrainGrassMaximumChannelDifference >= tolerance) errors.Add("Terrain and grass-root albedo differ by at least the starting 0.003 maximum-channel tolerance.");
                if (report.terrainExpectedMaximumChannelDifference >= tolerance) errors.Add("Terrain albedo does not match the expected sRGB-field-to-Linear sample.");
                if (report.grassExpectedMaximumChannelDifference >= tolerance) errors.Add("Grass root albedo does not match the expected sRGB-field-to-Linear sample.");

                preview = new Texture2D(width, height, TextureFormat.RGB24, false, false)
                    { name = "Temporary sRGB proof preview", hideFlags = HideFlags.HideAndDontSave };
                var linearPixels = readback.GetPixels();
                var gammaPixels = new Color32[linearPixels.Length];
                for (var i = 0; i < linearPixels.Length; i++) gammaPixels[i] = linearPixels[i].gamma;
                preview.SetPixels32(gammaPixels);
                preview.Apply(false, false);
                var capturePath = Path.Combine(outputDirectory, report.capture);
                Directory.CreateDirectory(Path.GetDirectoryName(capturePath));
                File.WriteAllBytes(capturePath, preview.EncodeToPNG());
            }
            catch (Exception exception)
            {
                errors.Add(exception.ToString());
            }
            finally
            {
                Shader.SetGlobalFloat("_TG_DebugAlbedo", oldDebug);
                RenderTexture.active = oldActiveTexture;
                if (rig != null) Object.DestroyImmediate(rig);
                if (rigScene.IsValid() && rigScene.isLoaded) EditorSceneManager.CloseScene(rigScene, true);
                if (oldActiveScene.IsValid() && oldActiveScene.isLoaded) SceneManager.SetActiveScene(oldActiveScene);
                if (preview != null) Object.DestroyImmediate(preview);
                if (readback != null) Object.DestroyImmediate(readback);
                if (target != null) RenderTexture.ReleaseTemporary(target);
                if (quad != null) Object.DestroyImmediate(quad);
                if (grassMaterial != null) Object.DestroyImmediate(grassMaterial);
                if (terrainMaterial != null) Object.DestroyImmediate(terrainMaterial);
                if (terrainData != null) Object.DestroyImmediate(terrainData);
                if (terrainLayer != null) Object.DestroyImmediate(terrainLayer);
                if (whiteTexture != null) Object.DestroyImmediate(whiteTexture);
            }
            report.errors = errors.ToArray();
            report.passed = errors.Count == 0;
            File.WriteAllText(Path.Combine(outputDirectory, "ColorFieldValidation.json"), JsonUtility.ToJson(report, true));
            return report.passed;
        }

        static Color GroundGrassReadNine(Camera camera, Texture2D texture, Vector3 world)
        {
            var screen = camera.WorldToViewportPoint(world);
            var x = Mathf.RoundToInt(screen.x * texture.width - .5f);
            var y = Mathf.RoundToInt(screen.y * texture.height - .5f);
            if (screen.z <= 0 || x < 1 || y < 1 || x >= texture.width - 1 || y >= texture.height - 1)
                throw new InvalidOperationException("Ground-grass proof sample is outside the camera target.");
            var sum = new Color(0, 0, 0, 0);
            for (var dy = -1; dy <= 1; dy++) for (var dx = -1; dx <= 1; dx++) sum += texture.GetPixel(x + dx, y + dy);
            return sum / 9;
        }

        static Color SampleFieldLikeGPU(Texture2D texture,Vector2 uv)
        {
            var x=uv.x*texture.width-.5f;var y=uv.y*texture.height-.5f;
            var ix=Mathf.FloorToInt(x);var iy=Mathf.FloorToInt(y);
            var x0=Mathf.Clamp(ix,0,texture.width-1);var x1=Mathf.Clamp(ix+1,0,texture.width-1);
            var y0=Mathf.Clamp(iy,0,texture.height-1);var y1=Mathf.Clamp(iy+1,0,texture.height-1);
            return Color.Lerp(Color.Lerp(texture.GetPixel(x0,y0).linear,texture.GetPixel(x1,y0).linear,x-ix),
                Color.Lerp(texture.GetPixel(x0,y1).linear,texture.GetPixel(x1,y1).linear,x-ix),y-iy);
        }

        static float GroundGrassRGBError(Color a, Color b)
        {
            var error = Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b));
            return float.IsNaN(error) || float.IsInfinity(error) ? float.PositiveInfinity : error;
        }
    }
}
