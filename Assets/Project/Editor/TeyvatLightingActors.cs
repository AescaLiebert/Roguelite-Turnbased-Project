using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FightingAllstar.EditorTools
{
    public static partial class TeyvatLightingLab
    {
        const string ActorPrefabSource = "Assets/Project/Prefabs/In-Game-CharacterPrefab.prefab";
        const string ActorMaterialSource = "Assets/Project/Art/Character/3D-Material/";
        const string ActorDefinitionSource = "Assets/Project/Data/Character/WIP_Phase/WIP_GenshinImpact/";
        const string ActorFixtureGroupName = "WP08 current-project actor visuals";
        static Transform actorFixtureRoot;

        [Serializable] sealed class LightingActorMaterialRecord
        {
            public string character, definition, sourceMaterial, sourceShader, sourceTexture, reviewMaterial, reviewShader;
            public Color baseColor;
            public Vector2 textureScale, textureOffset;
            public float metallic, smoothness;
            public int sourceSurfaceMode;
        }

        [Serializable] sealed class LightingActorPlacementRecord
        {
            public string name, anchor, character;
            public Vector3 groundAnchor, actorCentre, scale;
            public float yawDegrees, finalGroundError;
        }

        [Serializable] sealed class LightingActorProvenance
        {
            public string schema = "TG_WP08_CurrentProjectActorFixture_v1";
            public string sourcePrefab = ActorPrefabSource;
            public string geometry = "The current Lumine and Amber CharacterObjects both reference the shared native cube placeholder prefab, with no fighter3DMesh override. This review reconstructs that same built-in cube at scale (1.5,3,1.5), removes its collider and applies source-derived URP/Lit review materials. It does not contain detailed character meshes, skin, hair, rigs or animation.";
            public string grounding = "After placing each cube at the existing world anchor, shift its centre by anchor.worldPosition.y minus Renderer.bounds.min.y, matching CoreBattleSceneController's production ground-offset rule. Anchor transforms and the environment prefab are unchanged.";
            public string scope = "Lighting review objects outside the environment root only; no gameplay actor prefab or source material is saved or converted. The two teams face yaw 0/180. Each centre uses Lumine and each outer slot uses Amber.";
            public string probeSupport = "Opaque URP/Lit renderers use BlendProbes and BlendProbesAndSkybox. This establishes a supported test route, not a completed bake or proof of detailed character appearance.";
            public string aura = "Optional temporary fixture uses the repository UltimateReadyAura.Create implementation at radius .85m and height 3m, explicit particle seeds and Evaluate(.8). Its original additive unlit shader has no probe/shadow lighting response; assess transparency, exposure, bloom and actor readability. These dimensions and sample time are review starting values.";
            public int actors;
            public LightingActorMaterialRecord[] materials;
            public LightingActorPlacementRecord[] placements;
        }

        static void BuildActors()
        {
            if (layout == null || layout.anchors == null)
                throw new InvalidOperationException("Load the accepted layout before building the WP08 actor fixture.");
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects().Where(g => g.name == ActorFixtureGroupName).ToArray())
                Object.DestroyImmediate(root);
            actorFixtureRoot = Group(ActorFixtureGroupName);

            var materialRecords = new List<LightingActorMaterialRecord>();
            var lumineMaterial = CreateLightingActorMaterial("Lumine", "lumine-anemo", "gi-lumine_anemo", materialRecords);
            var amberMaterial = CreateLightingActorMaterial("Amber", "amber", "gi-amber", materialRecords);
            var placements = new List<LightingActorPlacementRecord>();
            foreach (var anchor in layout.anchors)
            {
                // Exact actor-proxy names only: descriptive meadow names can also
                // contain "proxy height" and are part of the accepted environment.
                var proxyName = "Proxy_" + anchor.name;
                foreach (var proxy in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                    .Where(t => t.name == proxyName).ToArray())
                    Object.DestroyImmediate(proxy.gameObject);

                bool enemy = anchor.name.StartsWith("Enemy", StringComparison.Ordinal);
                bool lumine = anchor.name.EndsWith("2", StringComparison.Ordinal);
                var actor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                actor.name = "WP08 " + (lumine ? "Lumine" : "Amber") + " " + anchor.name;
                actor.transform.SetParent(actorFixtureRoot, false);
                actor.transform.SetPositionAndRotation(anchor.worldPosition, Quaternion.Euler(0, enemy ? 180 : 0, 0));
                actor.transform.localScale = new Vector3(1.5f, 3, 1.5f);
                var collider = actor.GetComponent<Collider>();
                if (collider != null) Object.DestroyImmediate(collider);
                var renderer = actor.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = lumine ? lumineMaterial : amberMaterial;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                actor.transform.position += Vector3.up * (anchor.worldPosition.y - renderer.bounds.min.y);
                placements.Add(new LightingActorPlacementRecord
                {
                    name = actor.name, anchor = anchor.name, character = lumine ? "Lumine" : "Amber",
                    groundAnchor = anchor.worldPosition, actorCentre = actor.transform.position,
                    scale = actor.transform.localScale, yawDegrees = enemy ? 180 : 0,
                    finalGroundError = Mathf.Abs(renderer.bounds.min.y - anchor.worldPosition.y)
                });
            }
            var provenance = new LightingActorProvenance
            {
                actors = placements.Count, materials = materialRecords.ToArray(), placements = placements.ToArray()
            };
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "ActorFixtureProvenance.json"), JsonUtility.ToJson(provenance, true));
        }

        static Material CreateLightingActorMaterial(string character, string definition, string sourceName,
            List<LightingActorMaterialRecord> records)
        {
            var sourcePath = ActorMaterialSource + sourceName + ".mat";
            var source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            if (source == null) throw new FileNotFoundException("Copy the original actor material and texture dependencies into the isolated lighting host.", sourcePath);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("The isolated actor fixture requires URP/Lit.");
            var directory = Lighting + "/Actors/Materials";
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }
            var path = directory + "/TG_" + character + "_URPLit.mat";
            var target = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (target == null)
            {
                target = new Material(shader);
                AssetDatabase.CreateAsset(target, path);
            }
            else target.shader = shader;
            // Explicit mapping, repeated on every build. Never change the source
            // shader or copy opaque Standard state into the original material.
            target.name = "TG_" + character + "_URPLit";
            target.shaderKeywords = Array.Empty<string>();
            var color = source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
            var texture = source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
            if (texture == null) throw new InvalidOperationException("The actor fixture source portrait texture is missing: " + sourcePath);
            var scale = source.HasProperty("_MainTex") ? source.GetTextureScale("_MainTex") : Vector2.one;
            var offset = source.HasProperty("_MainTex") ? source.GetTextureOffset("_MainTex") : Vector2.zero;
            var metallic = source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : .254f;
            var smoothness = source.HasProperty("_Glossiness") ? source.GetFloat("_Glossiness") : .5f;
            target.SetColor("_BaseColor", color);
            target.SetTexture("_BaseMap", texture);
            target.SetTextureScale("_BaseMap", scale);
            target.SetTextureOffset("_BaseMap", offset);
            target.SetFloat("_WorkflowMode", 1);
            target.SetFloat("_Metallic", metallic);
            target.SetFloat("_Smoothness", smoothness);
            target.SetFloat("_SmoothnessTextureChannel", 0);
            target.SetFloat("_SpecularHighlights", source.HasProperty("_SpecularHighlights") ? source.GetFloat("_SpecularHighlights") : 1);
            target.SetFloat("_EnvironmentReflections", source.HasProperty("_GlossyReflections") ? source.GetFloat("_GlossyReflections") : 1);
            target.SetFloat("_Surface", 0);
            target.SetFloat("_Blend", 0);
            target.SetFloat("_AlphaClip", 0);
            target.SetFloat("_Cull", (float)CullMode.Back);
            target.SetFloat("_ZWrite", 1);
            target.SetFloat("_ReceiveShadows", 1);
            target.SetFloat("_SrcBlend", (float)BlendMode.One);
            target.SetFloat("_DstBlend", (float)BlendMode.Zero);
            target.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            target.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
            target.SetColor("_EmissionColor", Color.black);
            target.SetOverrideTag("RenderType", "Opaque");
            target.renderQueue = (int)RenderQueue.Geometry;
            target.SetShaderPassEnabled("ShadowCaster", true);
            target.SetShaderPassEnabled("DepthOnly", true);
            EditorUtility.SetDirty(target);
            records.Add(new LightingActorMaterialRecord
            {
                character = character, definition = ActorDefinitionSource + definition + ".asset",
                sourceMaterial = sourcePath, sourceShader = source.shader == null ? "missing" : source.shader.name,
                sourceTexture = texture == null ? "" : AssetDatabase.GetAssetPath(texture),
                reviewMaterial = path, reviewShader = shader.name, baseColor = color,
                textureScale = scale, textureOffset = offset, metallic = metallic, smoothness = smoothness,
                sourceSurfaceMode = source.HasProperty("_Mode") ? Mathf.RoundToInt(source.GetFloat("_Mode")) : 0
            });
            return target;
        }

        static GameObject CreateAuraFixture(Vector3 world, string name)
        {
            // Keep the production shader/geometry; missing Resources must never
            // silently exercise the script's Sprites/Default fallback instead.
            if (Resources.Load<Shader>("UltimateReadyAura") == null)
                throw new InvalidOperationException("Copy Resources/UltimateReadyAura.shader into the isolated lighting host before building the aura fixture.");
            var fixture = UltimateReadyAura.Create(null, world, .85f, 3);
            fixture.name = string.IsNullOrEmpty(name) ? "WP08 actual Ultimate Ready Aura fixture" : name;
            var systems = fixture.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                var system = systems[i];
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                system.useAutoRandomSeed = false;
                system.randomSeed = (uint)(808301 + i * 97);
            }
            fixture.GetComponent<UltimateReadyAura>().Evaluate(.8f);
            return fixture;
        }
    }
}
