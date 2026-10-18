using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using FightingAllstar.EnvironmentArt;
using Object=UnityEngine.Object;
namespace FightingAllstar.EditorTools
{
    public static partial class TeyvatLightingLab
    {
        [Serializable] sealed class LightingReport
        {
            public bool passed,linear,lightingPipeline,depthBeforeTransparent,originalCameras,actorsGrounded,oneClock,ownedMaterials,meadowPositionsRetained,noMissingDependencies,shadersSupported,postProcessing,fixedExposure,noCombatBlur,legacyProbes,reflectionTexture,postProcessPixelDifference,referenceRockTexture;
            public int cameras,actors,placements,materialCopies,missingScripts,probeCount,lightingRigs,reflectionProbes;public float actorGroundError,anchorGroundError,maximumPlacementMatrixError;
            public string[] errors;public string method="Saved scene, actual shader compilation/dependency references, all17camera transforms,6actor grounding bounds, all27737meadow matrices versusWP07, one windclock andone lightingrig, fixedNeutraltonemapper/exposure and bloom profile, no DOF/motionblur, actual baked legacy probes and cubemap where full review requested.";
            public string limitations="Numerical/structural checks accompany visual captures. Actor visuals are the current cube placeholders. No detailed skin/hair/rig, gameplay controller migration, full stylized lightmap integration, volumetric clouds or Windows player performance acceptance.";
        }
        static bool ValidateLighting()
        {
            var r=new LightingReport();var e=new List<string>();try
            {
                r.linear=PlayerSettings.colorSpace==ColorSpace.Linear;r.lightingPipeline=AssetDatabase.GetAssetPath(GraphicsSettings.currentRenderPipeline)==Lighting+"/Settings/TG_LightingURP.asset";var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;var so=new SerializedObject(pipeline);var renderer=(UniversalRendererData)so.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue;r.depthBeforeTransparent=renderer.copyDepthMode==CopyDepthMode.AfterOpaques&&!pipeline.supportsCameraOpaqueTexture;
                r.cameras=layout.shots.Count(s=>GameObject.Find("TG_"+s.name));r.originalCameras=layout.shots.All(s=>{var c=GameObject.Find("TG_"+s.name).GetComponent<Camera>();return Vector3.Distance(c.transform.position,s.position)<.0001f&&Quaternion.Angle(c.transform.rotation,s.rotation)<.001f&&Mathf.Abs(c.fieldOfView-s.fov)<.001f;});
                var actorRoot=GameObject.Find(ActorFixtureGroupName);r.actors=actorRoot.transform.childCount;r.actorGroundError=layout.anchors.Max(a=>Mathf.Abs(actorRoot.transform.Cast<Transform>().Single(t=>t.name.EndsWith(a.name)).GetComponent<Renderer>().bounds.min.y-a.worldPosition.y));r.actorsGrounded=r.actorGroundError<.001f;
                var terrain=environment.GetComponentInChildren<Terrain>();r.anchorGroundError=layout.anchors.Max(a=>Mathf.Abs(terrain.SampleHeight(a.worldPosition)+terrain.transform.position.y-a.worldPosition.y));
                var binding=environment.GetComponent<TeyvatStageBinding>();var data=binding.canonicalMeadowData;var previous=AssetDatabase.LoadAssetAtPath<TeyvatMeadowData>(Integration+"/Data/TG_StageMeadowPlacement.asset");r.placements=data.placementCount;
                for(var i=0;i<data.cells.Length;i++)for(var j=0;j<data.cells[i].placements.Length;j++)for(var k=0;k<16;k++)r.maximumPlacementMatrixError=Mathf.Max(r.maximumPlacementMatrixError,Mathf.Abs(data.cells[i].placements[j].matrix[k]-previous.cells[i].placements[j].matrix[k]));r.meadowPositionsRetained=data.placementCount==previous.placementCount&&r.maximumPlacementMatrixError<.0001f;
                var mats=environment.GetComponentsInChildren<Renderer>(true).SelectMany(x=>x.sharedMaterials).Concat(data.species.SelectMany(s=>s.materialsLOD0.Concat(s.materialsLOD1))).Append(terrain.materialTemplate).Where(m=>m).Distinct().ToArray();r.materialCopies=mats.Length;r.ownedMaterials=mats.All(m=>AssetDatabase.GetAssetPath(m).StartsWith(Lighting+"/"));
                r.shadersSupported=mats.All(m=>m.shader&&m.shader.isSupported&&!ShaderUtil.GetShaderMessages(m.shader).Any(x=>x.severity.ToString()=="Error"));foreach(var m in mats)foreach(var error in ShaderUtil.GetShaderMessages(m.shader).Where(x=>x.severity.ToString()=="Error"))e.Add(m.name+": "+error.message);
                var rockMaterials=mats.Where(m=>m.HasProperty("_ReferenceRock")&&m.GetFloat("_ReferenceRock")>.5f).ToArray();r.referenceRockTexture=rockMaterials.Length>=2&&rockMaterials.All(m=>AssetDatabase.GetAssetPath(m.GetTexture("_BaseMap"))==ReferenceRockTexture)&&AssetDatabase.GetAssetPath(terrain.terrainData.terrainLayers[2].diffuseTexture)==ReferenceRockTexture&&globals.rockPaintStrength>.8f;
                r.missingScripts=SceneManager.GetActiveScene().GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));r.noMissingDependencies=r.missingScripts==0&&mats.All(m=>m.shader);
                r.lightingRigs=Object.FindObjectsByType<TeyvatLightingRig>(FindObjectsSortMode.None).Length;r.oneClock=Object.FindObjectsByType<TeyvatMaterialGlobals>(FindObjectsSortMode.None).Length==1;
                r.postProcessing=renderer.postProcessData&&layout.shots.All(s=>GameObject.Find("TG_"+s.name).GetComponent<Camera>().GetUniversalAdditionalCameraData().renderPostProcessing);
                r.postProcessPixelDifference=ValidatePostProcessPixels();
                r.fixedExposure=rig.volume.sharedProfile.TryGet<ColorAdjustments>(out var color)&&Mathf.Abs(color.postExposure.value)<.001f;r.noCombatBlur=!rig.volume.sharedProfile.components.Any(c=>(c is DepthOfField||c is MotionBlur)&&c.active);
                r.probeCount=LightmapSettings.lightProbes?LightmapSettings.lightProbes.count:0;r.legacyProbes=r.probeCount>=45;var refl=Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None);r.reflectionProbes=refl.Length;r.reflectionTexture=refl.Any(p=>p.mode==ReflectionProbeMode.Custom&&p.customBakedTexture is Cubemap&&AssetDatabase.GetAssetPath(p.customBakedTexture)==Lighting+"/Probes/TG_ArenaReflection.exr");
                r.passed=r.linear&&r.lightingPipeline&&r.depthBeforeTransparent&&r.originalCameras&&r.actorsGrounded&&r.anchorGroundError<.02f&&r.oneClock&&r.lightingRigs==1&&r.ownedMaterials&&r.meadowPositionsRetained&&r.noMissingDependencies&&r.shadersSupported&&r.postProcessing&&r.postProcessPixelDifference&&r.fixedExposure&&r.noCombatBlur&&(sample||(r.legacyProbes&&r.reflectionTexture));
            }catch(Exception ex){e.Add(ex.ToString());}r.errors=e.ToArray();r.passed&=e.Count==0&&r.referenceRockTexture;File.WriteAllText(output+"/LightingValidation.json",JsonUtility.ToJson(r,true));return r.passed;
        }
        [Serializable] sealed class PostProcessPixelReport{public bool passed;public double meanAbsoluteRGBDifference;public string method="Actual-source aura under fixed camera/time; same URP HDR render with postprocessing on versus off. Starting numerical tolerance .01 RGB units (0..255). The comparison must change output; it is not a tone-quality score.";}
        static bool ValidatePostProcessPixels()
        {
            var camera=GameObject.Find("TG_PlayerAttack").GetComponent<Camera>();var d=camera.GetUniversalAdditionalCameraData();var root=CreateAuraFixture(layout.anchors[1].worldPosition,"Temporary post-process pixel fixture");Texture2D a=null,b=null;
            try
            {
                d.renderPostProcessing=true;a=Capture(camera,output+"/PostProcess/Enabled.png",800,450,true);d.renderPostProcessing=false;b=Capture(camera,output+"/PostProcess/Disabled.png",800,450,true);var av=a.GetPixels32();var bv=b.GetPixels32();double difference=0;for(var i=0;i<av.Length;i++)difference+=Math.Abs(av[i].r-bv[i].r)+Math.Abs(av[i].g-bv[i].g)+Math.Abs(av[i].b-bv[i].b);difference/=av.Length*3;
                var r=new PostProcessPixelReport{meanAbsoluteRGBDifference=difference,passed=difference>.01};File.WriteAllText(output+"/PostProcess/PixelValidation.json",JsonUtility.ToJson(r,true));return r.passed;
            }
            finally{d.renderPostProcessing=true;if(a)Object.DestroyImmediate(a);if(b)Object.DestroyImmediate(b);Object.DestroyImmediate(root);}
        }
    }
}
