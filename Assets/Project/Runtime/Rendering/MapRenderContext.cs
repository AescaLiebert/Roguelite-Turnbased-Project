using System.Collections.Generic;
using FightingAllstar.EnvironmentArt;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FightingAllstar.Rendering
{
    /// <summary>Shared rendering lifetime and actor compatibility for URP battle maps.</summary>
    [DefaultExecutionOrder(-11000), DisallowMultipleComponent]
    public class MapRenderContext : MonoBehaviour
    {
        public UniversalRenderPipelineAsset pipeline;
        public Shader actorShader;
        public string qualityTier = "Map Standard URP";
        public float farClipMinimum = 0;
        public bool postProcessing = true;
        public int rendererIndex;
        public bool adaptSceneStandardMaterials;
        public TeyvatLightingRig lightingRig;
        private int previousQuality, arenaQuality;
        private bool applied;
        private bool previousColorTemperature, previousLinearIntensity;
        private readonly Dictionary<Material, Material> actorMaterials = new Dictionary<Material, Material>();
        private readonly Dictionary<Renderer, RendererState> originalRenderers = new Dictionary<Renderer, RendererState>();
        private readonly HashSet<GameObject> preparedActors = new HashSet<GameObject>();
        public static MapRenderContext Active { get; private set; }

        private sealed class RendererState
        {
            public Material[] materials;
            public LightProbeUsage lightProbes;
            public ReflectionProbeUsage reflectionProbes;
        }

        // OnEnable also handles a context toggled while its scene and actors
        // remain loaded. The retained Teyvat subclass inherits this lifecycle.
        protected virtual void OnEnable()
        {
            ApplyContext();
        }

        private void ApplyContext()
        {
            if (applied) return;
            if (pipeline == null || actorShader == null)
            {
                Debug.LogError("Battle map is missing its rendering assets.", this);
                return;
            }
            arenaQuality = System.Array.IndexOf(QualitySettings.names, qualityTier);
            if (arenaQuality < 0 || QualitySettings.GetRenderPipelineAssetAt(arenaQuality) != pipeline)
            { Debug.LogError("Map rendering tier is not registered. Rebuild the map rendering setup.", this); return; }
            Active = this;
            previousQuality = QualitySettings.GetQualityLevel();
            previousColorTemperature = GraphicsSettings.lightsUseColorTemperature;
            previousLinearIntensity = GraphicsSettings.lightsUseLinearIntensity;
            QualitySettings.SetQualityLevel(arenaQuality, true);
            applied = true;
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (camera.gameObject.scene != gameObject.scene) continue;
                var data = camera.GetUniversalAdditionalCameraData();
                data.SetRenderer(rendererIndex);
                data.renderPostProcessing = postProcessing;
                data.requiresDepthTexture = true;
                data.requiresColorTexture = false;
                camera.allowHDR = true;
                camera.allowMSAA = true;
                // Surrounding coastal islands and the snowy skyline extend well
                // beyond the original compact Osaka camera's 140 m far plane.
                camera.farClipPlane = Mathf.Max(camera.farClipPlane, farClipMinimum);
            }
            if (lightingRig != null) lightingRig.Apply();
            preparedActors.RemoveWhere(actor => actor == null);
            // Recreate transient materials for fighters spawned before a toggle.
            // PrepareActor adds to this set, so enumerate a stable snapshot.
            var actors = new GameObject[preparedActors.Count];
            preparedActors.CopyTo(actors);
            foreach (var actor in actors) PrepareActor(actor);
            if (adaptSceneStandardMaterials)
                foreach (var root in gameObject.scene.GetRootGameObjects()) PrepareActor(root);
        }

        /// <summary>Use transient material copies; the account's authored materials stay intact.</summary>
        public void PrepareActor(GameObject actor)
        {
            if (actor == null || !applied || !isActiveAndEnabled) return;
            preparedActors.Add(actor);
            foreach (var renderer in actor.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                if (!originalRenderers.ContainsKey(renderer))
                    originalRenderers.Add(renderer, new RendererState
                    {
                        materials = (Material[])materials.Clone(),
                        lightProbes = renderer.lightProbeUsage,
                        reflectionProbes = renderer.reflectionProbeUsage
                    });
                for (var i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (source == null || source.shader == null ||
                        !(source.shader.name == "Standard" || source.shader.name == "Standard (Specular setup)")) continue;
                    if (!actorMaterials.TryGetValue(source, out var copy))
                    {
                        copy = new Material(actorShader) { name = source.name + " (map runtime)", hideFlags = HideFlags.DontSave };
                        CopyStandardSurface(source, copy);
                        actorMaterials.Add(source, copy);
                    }
                    materials[i] = copy;
                }
                renderer.sharedMaterials = materials;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;
            }
        }

        private static void CopyStandardSurface(Material source, Material copy)
        {
            CopyTexture(source, copy, "_MainTex", "_BaseMap");
            copy.SetColor("_BaseColor", source.color);
            var specular = source.shader.name == "Standard (Specular setup)";
            copy.SetFloat("_WorkflowMode", specular ? 0 : 1);
            Keyword(copy, "_SPECULAR_SETUP", specular);
            CopyFloat(source, copy, "_Metallic");
            CopyColor(source, copy, "_SpecColor");
            var glossProperty = specular ? "_SpecGlossMap" : "_MetallicGlossMap";
            var glossMap = CopyTexture(source, copy, glossProperty, glossProperty);
            Keyword(copy, "_METALLICSPECGLOSSMAP", glossMap);

            var mode = source.HasProperty("_Mode") ? (int)source.GetFloat("_Mode") : 0;
            var transparent = mode >= 2;
            var alphaClip = mode == 1;
            var albedoSmoothness = !transparent && source.HasProperty("_SmoothnessTextureChannel") &&
                source.GetFloat("_SmoothnessTextureChannel") == 1;
            // Standard uses GlossMapScale for map alpha and Glossiness for a
            // constant. URP uses Smoothness for both, so copy the active value.
            var smoothnessProperty = glossMap || albedoSmoothness ? "_GlossMapScale" : "_Glossiness";
            copy.SetFloat("_Smoothness", source.HasProperty(smoothnessProperty) ? source.GetFloat(smoothnessProperty) : .2f);
            copy.SetFloat("_SmoothnessTextureChannel", albedoSmoothness ? 1 : 0);
            Keyword(copy, "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A", albedoSmoothness);

            Keyword(copy, "_NORMALMAP", CopyTexture(source, copy, "_BumpMap", "_BumpMap"));
            CopyFloat(source, copy, "_BumpScale");
            Keyword(copy, "_OCCLUSIONMAP", CopyTexture(source, copy, "_OcclusionMap", "_OcclusionMap"));
            CopyFloat(source, copy, "_OcclusionStrength");
            Keyword(copy, "_PARALLAXMAP", CopyTexture(source, copy, "_ParallaxMap", "_ParallaxMap"));
            CopyFloat(source, copy, "_Parallax");
            CopyTexture(source, copy, "_EmissionMap", "_EmissionMap");
            CopyColor(source, copy, "_EmissionColor");
            Keyword(copy, "_EMISSION", source.IsKeywordEnabled("_EMISSION"));

            var detailAlbedo = CopyTexture(source, copy, "_DetailAlbedoMap", "_DetailAlbedoMap");
            var detailNormal = CopyTexture(source, copy, "_DetailNormalMap", "_DetailNormalMap");
            CopyTexture(source, copy, "_DetailMask", "_DetailMask");
            CopyFloat(source, copy, "_DetailNormalMapScale");
            copy.SetFloat("_DetailAlbedoMapScale", 1);
            Keyword(copy, "_DETAIL_MULX2", detailAlbedo || detailNormal);
            // URP applies detail UV after the primary UV transform. Standard
            // applies both transforms independently to UV0. Compensate without
            // changing the shared source mesh or either source texture.
            if ((detailAlbedo || detailNormal) && source.HasProperty("_DetailAlbedoMap"))
            {
                var primaryScale = source.mainTextureScale;
                if (Mathf.Abs(primaryScale.x) > .000001f && Mathf.Abs(primaryScale.y) > .000001f)
                {
                    var detailScale = source.GetTextureScale("_DetailAlbedoMap");
                    detailScale = new Vector2(detailScale.x / primaryScale.x, detailScale.y / primaryScale.y);
                    copy.SetTextureScale("_DetailAlbedoMap", detailScale);
                    copy.SetTextureOffset("_DetailAlbedoMap", source.GetTextureOffset("_DetailAlbedoMap") -
                        Vector2.Scale(source.mainTextureOffset, detailScale));
                }
            }

            CopyFloat(source, copy, "_SpecularHighlights");
            Keyword(copy, "_SPECULARHIGHLIGHTS_OFF", source.HasProperty("_SpecularHighlights") && source.GetFloat("_SpecularHighlights") == 0);
            if (source.HasProperty("_GlossyReflections"))
                copy.SetFloat("_EnvironmentReflections", source.GetFloat("_GlossyReflections"));
            Keyword(copy, "_ENVIRONMENTREFLECTIONS_OFF", source.HasProperty("_GlossyReflections") && source.GetFloat("_GlossyReflections") == 0);

            copy.SetFloat("_Surface", transparent ? 1 : 0);
            copy.SetFloat("_AlphaClip", alphaClip ? 1 : 0);
            CopyFloat(source, copy, "_Cutoff");
            Keyword(copy, "_SURFACE_TYPE_TRANSPARENT", transparent);
            Keyword(copy, "_ALPHATEST_ON", alphaClip);
            // In URP 17, Blend=Premultiply expects RGB pre-multiplied in the
            // asset. Standard mode 3 instead multiplies diffuse in the shader
            // and preserves specular, matching URP Alpha + Preserve Specular.
            var preserveSpecular = mode == 3;
            copy.SetFloat("_Blend", 0);
            copy.SetFloat("_BlendModePreserveSpecular", preserveSpecular ? 1 : 0);
            Keyword(copy, "_ALPHAPREMULTIPLY_ON", preserveSpecular);
            copy.SetFloat("_SrcBlend", transparent && !preserveSpecular ? (int)BlendMode.SrcAlpha : (int)BlendMode.One);
            copy.SetFloat("_DstBlend", transparent ? (int)BlendMode.OneMinusSrcAlpha : (int)BlendMode.Zero);
            copy.SetFloat("_SrcBlendAlpha", (int)BlendMode.One);
            copy.SetFloat("_DstBlendAlpha", transparent ? (int)BlendMode.OneMinusSrcAlpha : (int)BlendMode.Zero);
            copy.SetFloat("_ZWrite", transparent ? 0 : 1);
            copy.SetOverrideTag("RenderType", transparent ? "Transparent" : alphaClip ? "TransparentCutout" : "Opaque");
            copy.SetShaderPassEnabled("DepthOnly", !transparent);
            copy.SetShaderPassEnabled("ShadowCaster", !transparent);
            var baseQueue = transparent ? 3000 : alphaClip ? 2450 : 2000;
            copy.renderQueue = source.renderQueue;
            copy.SetFloat("_QueueOffset", source.renderQueue - baseQueue);
            copy.enableInstancing = source.enableInstancing;
            copy.doubleSidedGI = source.doubleSidedGI;
            copy.globalIlluminationFlags = source.globalIlluminationFlags;
        }

        private static bool CopyTexture(Material source, Material target, string from, string to)
        {
            if (!source.HasProperty(from) || !target.HasProperty(to)) return false;
            var texture = source.GetTexture(from);
            target.SetTexture(to, texture);
            target.SetTextureScale(to, source.GetTextureScale(from));
            target.SetTextureOffset(to, source.GetTextureOffset(from));
            return texture != null;
        }

        private static void CopyFloat(Material source, Material target, string property)
        { if (source.HasProperty(property) && target.HasProperty(property)) target.SetFloat(property, source.GetFloat(property)); }

        private static void CopyColor(Material source, Material target, string property)
        { if (source.HasProperty(property) && target.HasProperty(property)) target.SetColor(property, source.GetColor(property)); }

        private static void Keyword(Material material, string keyword, bool enabled)
        { if (enabled) material.EnableKeyword(keyword); else material.DisableKeyword(keyword); }

        protected virtual void OnDisable()
        {
            if (Active == this) Active = null;
            // Restore shared asset references before destroying their runtime
            // replacements. Disabled contexts must leave surviving actors valid.
            foreach (var entry in originalRenderers)
            {
                if (entry.Key == null) continue;
                entry.Key.sharedMaterials = entry.Value.materials;
                entry.Key.lightProbeUsage = entry.Value.lightProbes;
                entry.Key.reflectionProbeUsage = entry.Value.reflectionProbes;
            }
            originalRenderers.Clear();
            if (applied)
            {
                // Do not overwrite an explicit later rendering owner.
                if (QualitySettings.GetQualityLevel() == arenaQuality) QualitySettings.SetQualityLevel(previousQuality, true);
                GraphicsSettings.lightsUseColorTemperature = previousColorTemperature;
                GraphicsSettings.lightsUseLinearIntensity = previousLinearIntensity;
#if UNITY_EDITOR
                // URP persists its light interpretation when initialized in the
                // Editor. Persist the restored values as well, not only native state.
                var graphics = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
                if (graphics.Length > 0)
                {
                    UnityEditor.EditorUtility.SetDirty(graphics[0]);
                    UnityEditor.AssetDatabase.SaveAssetIfDirty(graphics[0]);
                }
#endif
                applied = false;
            }
            foreach (var material in actorMaterials.Values) if (material != null)
            { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
            actorMaterials.Clear();
        }
    }
}

