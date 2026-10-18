using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FightingAllstar.EditorTools
{
    public static partial class TeyvatFoundationBuilder
    {
        /// <summary>Scene-local WP01 sky preview. The installed sky asset and project rendering settings stay untouched.</summary>
        static void ConfigureReferenceSky()
        {
            const string sourcePath = "Assets/Fantasy Skybox FREE/Panoramics/FS003/FS003_Day_Sunless.mat";
            var source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            if (source == null)
                throw new InvalidOperationException("The WP01 reference sky material is missing: " + sourcePath);

            var materialFolder = Art + "/Materials";
            if (!AssetDatabase.IsValidFolder(materialFolder))
            {
                Directory.CreateDirectory(materialFolder);
                AssetDatabase.Refresh();
            }

            var skyPath = materialFolder + "/TG_FantasyDaySky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(source) { name = "TG_FantasyDaySky" };
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            else
            {
                sky.shader = source.shader;
                sky.CopyPropertiesFromMaterial(source);
            }

            // Panoramic skybox's neutral tint is 0.5. Keep the source's blue sky and white cumulus colors.
            sky.SetColor("_Tint", new Color(.5f, .5f, .5f, 1f));
            sky.SetFloat("_Exposure", 1f);
            sky.SetFloat("_Rotation", 18f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;

            // Trilight keeps the terrain's shaded faces readable without baking the sky texture into its palette.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.62f, .76f, .92f);
            RenderSettings.ambientEquatorColor = new Color(.44f, .53f, .57f);
            RenderSettings.ambientGroundColor = new Color(.30f, .35f, .23f);
            RenderSettings.ambientIntensity = 1f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.61f, .79f, .91f);
            RenderSettings.fogStartDistance = 120f;
            RenderSettings.fogEndDistance = 480f;
        }
    }
}
