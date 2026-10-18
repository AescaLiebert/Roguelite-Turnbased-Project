using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FightingAllstar.EnvironmentArt;
using UnityEditor;
using UnityEngine;

namespace FightingAllstar.EditorTools
{
    public static partial class TeyvatLightingLab
    {
        const string ReferenceRockTexture = Shared + "/Textures/TG_RockPainted.png";
        const float ReferenceRockPaintStrength = .85f;
        const float ReferenceRockWorldScale = .055f;

        [Serializable] sealed class ReferenceRockMaterialRecord
        {
            public string path, name, texture;
            public int rendererSlots;
            public float previousTextureStrength, previousSharedStyle, textureStrength, sharedStyle, slotTintScale;
        }
        [Serializable] sealed class ReferenceRockReport
        {
            public bool bindingsPresent, nativeLayerUsesOriginalTexture, noTerrainDataOrLayerMutation;
            public string texture = ReferenceRockTexture;
            public string terrainData, nativeRockLayer, nativeRockTexture;
            public float previousSharedPaintStrength, sharedPaintStrength, worldUVPerMetre, tileMetres;
            public int meshRockMaterialCount, meshRockRendererSlots, nativeControlSamples, nativeRockDominantSamples;
            public float maximumNativeRockWeight;
            public Vector3 textureMeasuredLinearMean = new Vector3(.17163f, .18891f, .21022f);
            public ReferenceRockMaterialRecord[] materials;
            public string finding = "The original texture was already bound to mesh rocks and native layer2. The mesh shared-style branch ignored _TextureStrength and used only the scene's .35 paint strength; RockShade used a separate weak .35 path. Both suppressed the low-contrast painted strata. Native terrain already had world triplanar mapping.";
            public string change = "Lighting-owned mesh and native layer2 now use the same normalized painted albedo, coarse world projection and stronger shared paint amount. Normalize the original texture's linear mean around one before applying the profile's linear rock tint. Upward ground and soil layers retain their accepted color-field/control-map behavior; no geometry, heights, layer weights, source texture or earlier-lane material changes.";
            public string startingValues = "Paint .85; UV .055 per metre (18.18m tile); relative painted contrast1.20 clamped .50..1.65; shared mesh RockShade tint multiplier .88. Starting look-development values, to be judged in lit and unlit matched-camera ablations. These do not establish exact reference parity.";
        }

        /// <summary>Call after material remapping and BuildProfiles, before captures/save.</summary>
        static void ApplyReferenceRockStyle()
        {
            if (environment == null || globals == null)
                throw new InvalidOperationException("Build the owned environment and shared material globals before applying reference rock style.");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ReferenceRockTexture);
            if (texture == null) throw new FileNotFoundException("The original painted rock albedo is missing.", ReferenceRockTexture);
            var terrain = environment.GetComponentInChildren<Terrain>();
            if (terrain == null || terrain.terrainData == null)
                throw new InvalidOperationException("Reference rock validation needs the accepted native TerrainData.");
            var layers = terrain.terrainData.terrainLayers;
            if (layers == null || layers.Length < 3 || layers[2] == null)
                throw new InvalidOperationException("The native rock material expects the accepted layer2 rock channel.");
            var report = new ReferenceRockReport
            {
                terrainData = AssetDatabase.GetAssetPath(terrain.terrainData),
                nativeRockLayer = AssetDatabase.GetAssetPath(layers[2]),
                nativeRockTexture = AssetDatabase.GetAssetPath(layers[2].diffuseTexture),
                nativeLayerUsesOriginalTexture = layers[2].diffuseTexture == texture,
                noTerrainDataOrLayerMutation = true,
                previousSharedPaintStrength = globals.rockPaintStrength,
                sharedPaintStrength = ReferenceRockPaintStrength,
                worldUVPerMetre = ReferenceRockWorldScale,
                tileMetres = 1f / ReferenceRockWorldScale
            };
            // Current layer2 is already correct. Fail on an unexpected source
            // instead of silently editing a World/Shared TerrainLayer asset.
            if (!report.nativeLayerUsesOriginalTexture)
                throw new InvalidOperationException("Native rock layer2 must reference " + ReferenceRockTexture + "; found " + report.nativeRockTexture);
            var weights = terrain.terrainData.GetAlphamaps(0, 0, terrain.terrainData.alphamapWidth, terrain.terrainData.alphamapHeight);
            for (int z = 0; z < weights.GetLength(0); z++)
                for (int x = 0; x < weights.GetLength(1); x++)
                {
                    report.nativeControlSamples++;
                    var rockWeight = weights[z, x, 2];
                    if (rockWeight > .5f) report.nativeRockDominantSamples++;
                    report.maximumNativeRockWeight = Mathf.Max(report.maximumNativeRockWeight, rockWeight);
                }
            var renderers = environment.GetComponentsInChildren<MeshRenderer>(true);
            var materials = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null)
                .Where(m => m.shader != null && m.shader.name == "Teyvat/URP/LightingSurface")
                .Where(m => m.name.IndexOf("Rock", StringComparison.OrdinalIgnoreCase) >= 0
                    || (m.HasProperty("_UseSharedRockStyle") && m.GetFloat("_UseSharedRockStyle") > .5f))
                .Distinct().ToArray();
            var records = new List<ReferenceRockMaterialRecord>();
            foreach (var material in materials)
            {
                var path = AssetDatabase.GetAssetPath(material);
                if (!path.StartsWith(Lighting + "/", StringComparison.Ordinal))
                    throw new InvalidOperationException("Only Lighting-owned cliff materials may be revised: " + path);
                if (!material.HasProperty("_ReferenceRock"))
                    throw new InvalidOperationException("Refresh the LightingSurface shader before applying its reference rock preset.");
                var record = new ReferenceRockMaterialRecord
                {
                    path = path, name = material.name,
                    previousTextureStrength = material.GetFloat("_TextureStrength"),
                    previousSharedStyle = material.GetFloat("_UseSharedRockStyle"),
                    rendererSlots = renderers.Sum(r => r.sharedMaterials.Count(m => m == material))
                };
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", Vector2.one);
                material.SetTextureOffset("_BaseMap", Vector2.zero);
                material.SetFloat("_Mapping", 1);
                material.SetFloat("_UseSharedRockStyle", 1);
                material.SetFloat("_ReferenceRock", 1);
                material.SetFloat("_TextureStrength", 1);
                material.SetFloat("_WorldScale", ReferenceRockWorldScale);
                material.SetFloat("_UseColorField", 0);
                var tintScale = material.name.IndexOf("RockShade", StringComparison.OrdinalIgnoreCase) >= 0 ? .88f : 1;
                material.SetFloat("_RockTintScale", tintScale);
                EditorUtility.SetDirty(material);
                record.texture = AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap"));
                record.textureStrength = material.GetFloat("_TextureStrength");
                record.sharedStyle = material.GetFloat("_UseSharedRockStyle");
                record.slotTintScale = tintScale;
                records.Add(record);
            }
            globals.rockPaintStrength = ReferenceRockPaintStrength;
            globals.rockWorldScale = ReferenceRockWorldScale;
            globals.Apply();
            report.materials = records.ToArray();
            report.meshRockMaterialCount = records.Count;
            report.meshRockRendererSlots = records.Sum(r => r.rendererSlots);
            report.bindingsPresent = report.nativeLayerUsesOriginalTexture && report.meshRockMaterialCount > 0
                && report.nativeRockDominantSamples > 0;
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "ReferenceRockBindings.json"), JsonUtility.ToJson(report, true));
            if (!report.bindingsPresent) throw new InvalidOperationException("No owned mesh-cliff materials use the reference rock preset.");
        }

        /// <summary>Optional fixed-camera proof of actual lit/unlit painted albedo.</summary>
        static void CaptureReferenceRockAblations()
        {
            var oldStrength = globals.rockPaintStrength;
            var oldDebug = Shader.GetGlobalFloat("_TG_DebugAlbedo");
            try
            {
                foreach (var name in new[] { "TG_WaterSample", "TG_EnvironmentOverview" })
                {
                    var go = GameObject.Find(name);
                    if (go == null || go.GetComponent<Camera>() == null) continue;
                    var camera = go.GetComponent<Camera>();
                    Shader.SetGlobalFloat("_TG_DebugAlbedo", 0);
                    globals.rockPaintStrength = oldStrength; globals.Apply();
                    Capture(camera, output + "/RockTexture/" + name + "_PaintedLit.png");
                    globals.rockPaintStrength = 0; globals.Apply();
                    Capture(camera, output + "/RockTexture/" + name + "_FlatTintLit.png");
                    globals.rockPaintStrength = oldStrength; globals.Apply();
                    Shader.SetGlobalFloat("_TG_DebugAlbedo", 1);
                    Capture(camera, output + "/RockTexture/" + name + "_PaintedAlbedo.png");
                }
            }
            finally
            {
                globals.rockPaintStrength = oldStrength;
                Shader.SetGlobalFloat("_TG_DebugAlbedo", oldDebug);
                globals.Apply();
            }
        }
    }
}
