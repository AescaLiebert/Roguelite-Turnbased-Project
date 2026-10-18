using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using FightingAllstar.EnvironmentArt;
using Object=UnityEngine.Object;

namespace FightingAllstar.EditorTools
{
    public static partial class TeyvatLightingLab
    {
        const string Root="Assets/Project/Art/Environment/TeyvatGrassland",Lighting=Root+"/Lighting",Shared=Root+"/Shared",Integration=Root+"/Integration";
        const string InputScene="Assets/Project/Scenes/EnvironmentReview/TeyvatGrassland/TG_StageReview_r1.unity";
        const string ReviewScene="Assets/Project/Scenes/EnvironmentReview/TeyvatGrassland/TG_LightingReview_r2.unity";
        const string FinalPrefab=Lighting+"/Prefabs/TG_LitBattleStage_r2.prefab";
        static string output;static Vector3 origin;static Transform environment;static TeyvatMaterialGlobals globals;static TeyvatLightingRig rig;static Light sun;
        static Layout layout;static bool sample,reload,pending;static double ready;
        static readonly Dictionary<Material,Material> remap=new Dictionary<Material,Material>();
        [Serializable] sealed class Anchor{public string name;public Vector3 worldPosition,localPosition;}
        [Serializable] sealed class Shot{public string name;public Vector3 position;public Quaternion rotation;public float fov;}
        [Serializable] sealed class Layout{public Vector3 origin;public Anchor[] anchors;public Shot[] shots;}
        public static void SampleOnly(){Start(true,false);}
        public static void RunIsolated(){Start(false,false);}
        public static void VerifySaved(){Start(false,true);}
        public static void ExportVerifiedPackage()
        {
            var marker=Path.Combine(Path.GetDirectoryName(Application.dataPath),"TG_ISOLATED_LIGHTING.txt");
            if(!File.Exists(marker)||File.Exists("Assets/Project/Scenes/Battle.unity"))throw new InvalidOperationException("WP08 export requires isolated art host.");
            output=File.ReadAllText(marker).Trim();Directory.CreateDirectory(output);try{EditorSceneManager.OpenScene(ReviewScene);Export();File.WriteAllText(output+"/COMPLETE.txt","WP08 package exported with explicit include dependencies.");EditorApplication.Exit(0);}catch(Exception e){Fail(e);}
        }
        static void Start(bool justSample,bool saved)
        {
            var marker=Path.Combine(Path.GetDirectoryName(Application.dataPath),"TG_ISOLATED_LIGHTING.txt");
            if(!File.Exists(marker)||File.Exists("Assets/Project/Scenes/Battle.unity"))throw new InvalidOperationException("WP08 requires isolated URP art host.");
            output=File.ReadAllText(marker).Trim();Directory.CreateDirectory(output);sample=justSample;reload=saved;
            try
            {
                ShaderUtil.allowAsyncCompilation=false;if(PlayerSettings.colorSpace!=ColorSpace.Linear)throw new Exception("Linear color space required.");
                layout=JsonUtility.FromJson<Layout>(File.ReadAllText(Root+"/LayoutR3/Data/TG_Layout.json"));origin=layout.origin;
                foreach(var name in new[]{"Materials","Data","Settings","Profiles","Probes","Prefabs"})Directory.CreateDirectory(Lighting+"/"+name);AssetDatabase.Refresh();
                if(saved){EditorSceneManager.OpenScene(ReviewScene);environment=GameObject.Find("TG Battle Stage WP08").transform;globals=environment.GetComponentInChildren<TeyvatMaterialGlobals>();rig=Object.FindFirstObjectByType<TeyvatLightingRig>();sun=rig.sun;}
                else Build();
                pending=true;ready=EditorApplication.timeSinceStartup+3;EditorApplication.update+=Finish;
            }catch(Exception e){Fail(e);}
        }
        static void Fail(Exception e){Debug.LogException(e);File.WriteAllText(output+"/FAILED.txt",e.ToString());EditorApplication.Exit(1);}
        static Transform Group(string name,Transform parent=null){var t=new GameObject(name).transform;if(parent)t.SetParent(parent,false);return t;}
        static T OwnedCopy<T>(T source,string path)where T:Object
        {
            var target=AssetDatabase.LoadAssetAtPath<T>(path);if(!target){target=Object.Instantiate(source);target.name=Path.GetFileNameWithoutExtension(path);AssetDatabase.CreateAsset(target,path);}else EditorUtility.CopySerialized(source,target);EditorUtility.SetDirty(target);return target;
        }
        static void ConfigurePipeline()
        {
            var renderer=OwnedCopy(AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Shared+"/Settings/TG_MaterialRenderer.asset"),Lighting+"/Settings/TG_LightingRenderer.asset");
            renderer.postProcessData=AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            if(!renderer.postProcessData)throw new Exception("Installed URP post-process resource asset is missing.");
            renderer.copyDepthMode=CopyDepthMode.AfterOpaques;
            var pipeline=OwnedCopy(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Shared+"/Settings/TG_MaterialURP.asset"),Lighting+"/Settings/TG_LightingURP.asset");
            var serialized=new SerializedObject(pipeline);serialized.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue=renderer;serialized.FindProperty("m_SoftShadowsSupported").boolValue=true;serialized.FindProperty("m_MainLightShadowmapResolution").intValue=2048;serialized.FindProperty("m_ReflectionProbeBlending").boolValue=true;serialized.FindProperty("m_ReflectionProbeBoxProjection").boolValue=true;serialized.ApplyModifiedPropertiesWithoutUndo();
            pipeline.shadowDistance=150;pipeline.shadowCascadeCount=4;pipeline.cascade4Split=new Vector3(.08f,.22f,.5f);pipeline.supportsHDR=true;pipeline.supportsCameraDepthTexture=true;pipeline.supportsCameraOpaqueTexture=false;
            GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;QualitySettings.vSyncCount=0;EditorUtility.SetDirty(pipeline);
        }
        static void Build()
        {
            ConfigurePipeline();EditorSceneManager.OpenScene(InputScene);EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ReviewScene);
            environment=GameObject.Find("TG Battle Stage r1").transform;environment.name="TG Battle Stage WP08";globals=environment.GetComponentInChildren<TeyvatMaterialGlobals>();
            var binding=environment.GetComponent<TeyvatStageBinding>();binding.enabled=false;var canonical=binding.canonicalMeadowData;
            remap.Clear();foreach(var renderer in environment.GetComponentsInChildren<Renderer>(true))renderer.sharedMaterials=renderer.sharedMaterials.Select(RemapMaterial).ToArray();
            var terrain=environment.GetComponentInChildren<Terrain>();terrain.materialTemplate=RemapMaterial(terrain.materialTemplate);
            var dataPath=Lighting+"/Data/TG_LitMeadowPlacement.asset";var data=AssetDatabase.LoadAssetAtPath<TeyvatMeadowData>(dataPath);if(!data){data=ScriptableObject.CreateInstance<TeyvatMeadowData>();AssetDatabase.CreateAsset(data,dataPath);}
            data.seed=canonical.seed;data.revision="WP08 palette only; WP07 positions retained";data.placementCount=canonical.placementCount;data.worldBounds=canonical.worldBounds;
            data.cells=canonical.cells.Select(c=>new TeyvatMeadowData.Cell{name=c.name,worldBounds=c.worldBounds,placements=(TeyvatMeadowData.Placement[])c.placements.Clone()}).ToArray();
            data.species=canonical.species.Select(s=>new TeyvatMeadowData.Species{name=s.name,meshLOD0=s.meshLOD0,meshLOD1=s.meshLOD1,materialsLOD0=s.materialsLOD0.Select(RemapMaterial).ToArray(),materialsLOD1=s.materialsLOD1.Select(RemapMaterial).ToArray()}).ToArray();EditorUtility.SetDirty(data);
            binding.canonicalMeadowData=data;binding.meadow.data=data;binding.enabled=true;binding.ApplyBinding();
            BuildColorField();BuildProfiles();ApplyReferenceRockStyle();
            sun=Object.FindFirstObjectByType<Light>();sun.type=LightType.Directional;sun.lightmapBakeType=LightmapBakeType.Realtime;sun.shadows=LightShadows.Soft;sun.shadowBias=.15f;sun.shadowNormalBias=.20f;sun.shadowStrength=1;
            var root=Group("TG WP08 lighting rig");sun.transform.SetParent(root,true);rig=root.gameObject.AddComponent<TeyvatLightingRig>();rig.sun=sun;rig.globals=globals;rig.volume=root.gameObject.AddComponent<Volume>();rig.volume.isGlobal=true;rig.volume.priority=10;rig.profile=AssetDatabase.LoadAssetAtPath<TeyvatLightingProfile>(Lighting+"/Profiles/TG_ReferenceDay.asset");rig.Apply();
            foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)){var d=c.GetUniversalAdditionalCameraData();d.renderPostProcessing=true;d.requiresDepthTexture=true;d.requiresColorTexture=false;d.antialiasing=AntialiasingMode.None;c.allowHDR=true;}
            BuildActors();Save();
        }
        static Material RemapMaterial(Material source)
        {
            if(!source)return null;if(remap.TryGetValue(source,out var known))return known;
            var shader=source.shader.name;string target=null;
            if(shader=="Teyvat/URP/Surface")target="Teyvat/URP/LightingSurface";
            else if(shader=="Teyvat/URP/Foliage")target="Teyvat/URP/LightingFoliage";
            else if(shader=="Teyvat/URP/MeadowFoliage")target="Teyvat/URP/LightingMeadow";
            else if(shader.Contains("Terrain"))target="Teyvat/URP/LightingTerrain";
            var guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)).Substring(0,8);var m=OwnedCopy(source,Lighting+"/Materials/"+source.name+"_"+guid+".mat");if(target!=null)m.shader=Shader.Find(target);remap[source]=m;
            if(m.HasProperty("_Family"))
            {
                var family=m.GetFloat("_Family");if(family<.5f){m.SetColor("_BaseColor",new Color(.54f,.79f,.30f));m.SetColor("_TipColor",new Color(1.10f,1.06f,.92f));m.SetFloat("_Wrap",.26f);m.SetFloat("_Transmission",.17f);}
                else if(family<1.5f){var c=m.GetColor("_BaseColor");m.SetColor("_BaseColor",new Color(c.r*.76f,c.g*.95f,c.b*1.28f,c.a));m.SetColor("_CanopyShadeColor",new Color(.42f,.74f,.74f));m.SetFloat("_Wrap",.23f);}
            }
            if(m.HasProperty("_UseSharedRockStyle")&&m.GetFloat("_UseSharedRockStyle")>.5f)m.SetFloat("_TextureStrength",.8f);
            if(m.HasProperty("_ShallowColor")){var mode=m.HasProperty("_Mode")?m.GetFloat("_Mode"):0;if(mode<.5f||mode>1.5f){m.SetColor("_ShallowColor",new Color(.13f,.73f,.69f,.79f));m.SetColor("_DeepColor",new Color(.07f,.40f,.58f,.94f));m.SetColor("_SkyColor",new Color(.43f,.66f,.82f));m.SetFloat("_ReflectionStrength",.16f);}}
            EditorUtility.SetDirty(m);return m;
        }
        static void BuildColorField()
        {
            var source=globals.colorField;var pixels=source.GetPixels();for(var i=0;i<pixels.Length;i++){Color.RGBToHSV(pixels[i],out var h,out var s,out var v);pixels[i]=Color.HSVToRGB(h,s,v);}
            var path=Lighting+"/Data/TG_LightingColorField.asset";var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(!texture){texture=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false,false);AssetDatabase.CreateAsset(texture,path);}texture.SetPixels(pixels);texture.Apply(false,false);texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Bilinear;EditorUtility.SetDirty(texture);globals.colorField=texture;
        }
        static VolumeProfile VolumeAsset(string name,bool neutral)
        {
            var path=Lighting+"/Profiles/TG_"+name+"Volume.asset";var p=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);if(!p){p=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(p,path);}foreach(var old in p.components.ToArray())Object.DestroyImmediate(old,true);p.components.Clear();
            var tone=p.Add<Tonemapping>(true);tone.mode.value=TonemappingMode.Neutral;AssetDatabase.AddObjectToAsset(tone,p);
            var color=p.Add<ColorAdjustments>(true);color.postExposure.value=0;color.contrast.value=neutral?0:6;color.saturation.value=0;AssetDatabase.AddObjectToAsset(color,p);
            var bloom=p.Add<Bloom>(true);bloom.threshold.value=1.4f;bloom.intensity.value=neutral?0:.035f;bloom.scatter.value=.45f;bloom.clamp.value=5;AssetDatabase.AddObjectToAsset(bloom,p);EditorUtility.SetDirty(p);return p;
        }
        static void BuildProfiles()
        {
            const string sourceSky="Assets/Fantasy Skybox FREE/Panoramics/FS003/FS003_Day_Sunless.png";
            var cloudPath=Lighting+"/Data/TG_DaylightCloudShape.png";
            if(!AssetDatabase.LoadAssetAtPath<Texture2D>(cloudPath))AssetDatabase.CopyAsset(sourceSky,cloudPath);
            var importer=(TextureImporter)AssetImporter.GetAtPath(cloudPath);importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;importer.wrapModeU=TextureWrapMode.Repeat;importer.wrapModeV=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.crunchedCompression=false;importer.ClearPlatformTextureSettings("Standalone");
            var platform=importer.GetDefaultPlatformTextureSettings();platform.textureCompression=TextureImporterCompression.Uncompressed;platform.format=TextureImporterFormat.RGB24;importer.SetPlatformTextureSettings(platform);importer.SaveAndReimport();
            foreach(var neutral in new[]{true,false})
            {
                var name=neutral?"Neutral":"ReferenceDay";var skyPath=Lighting+"/Materials/TG_"+name+"Sky.mat";var sky=AssetDatabase.LoadAssetAtPath<Material>(skyPath);if(!sky){sky=new Material(Shader.Find("Teyvat/URP/PaintedSky"));AssetDatabase.CreateAsset(sky,skyPath);}sky.shader=Shader.Find("Teyvat/URP/PaintedSky");sky.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(cloudPath));sky.SetFloat("_Rotation",212);sky.SetFloat("_Exposure",1);sky.SetColor("_ZenithColor",new Color(.10f,.55f,.83f).linear);sky.SetColor("_HorizonColor",new Color(.54f,.78f,.89f).linear);sky.SetColor("_CloudShadeColor",new Color(.64f,.78f,.92f).linear);sky.SetColor("_CloudSunColor",(neutral?new Color(.96f,.98f,1):new Color(.99f,1,1)).linear);sky.SetFloat("_CloudAmount",1);sky.SetFloat("_CloudDrift",0);sky.SetFloat("_SunGlow",0);sky.SetFloat("_SunSize",.32f);EditorUtility.SetDirty(sky);
                var path=Lighting+"/Profiles/TG_"+name+".asset";var p=AssetDatabase.LoadAssetAtPath<TeyvatLightingProfile>(path);if(!p){p=ScriptableObject.CreateInstance<TeyvatLightingProfile>();AssetDatabase.CreateAsset(p,path);}p.skybox=sky;p.volume=VolumeAsset(name,neutral);p.sunEuler=new Vector3(neutral?55:52,145,0);p.sunColor=neutral?Color.white:new Color(1,1,.99f);p.sunIntensity=neutral?1.15f:1.70f;p.ambientSky=new Color(.41f,.64f,.84f);p.ambientEquator=new Color(.27f,.44f,.51f);p.ambientGround=new Color(.19f,.29f,.25f);p.fog=!neutral;p.fogStart=140;p.fogEnd=650;p.fogColor=new Color(.48f,.72f,.86f);p.lightStyle=new Vector4(.85f,1.10f,.16f,1.15f);p.shadowTint=new Color(.64f,.83f,1,.6f);p.ambientFloor=new Color(.08f,.105f,.13f);p.rockTint=new Color(.64f,.72f,.78f);p.cloudShadow=new Vector4(.021f,.018f,neutral?0:.16f,neutral?0:1);EditorUtility.SetDirty(p);
            }
        }
        static void SetProfile(bool neutral){rig.profile=AssetDatabase.LoadAssetAtPath<TeyvatLightingProfile>(Lighting+"/Profiles/TG_"+(neutral?"Neutral":"ReferenceDay")+".asset");rig.Apply();}
        static void Save()
        {
            var binding=environment.GetComponent<TeyvatStageBinding>();binding.meadow.data=binding.canonicalMeadowData;AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ReviewScene);PrefabUtility.SaveAsPrefabAsset(environment.gameObject,FinalPrefab);binding.ApplyBinding();
        }
        static Texture2D Capture(Camera c,string path,int width=1600,int height=900,bool retain=false)
        {
            if(path!=null)Directory.CreateDirectory(Path.GetDirectoryName(path));var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear,4);var ldr=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var previous=RenderTexture.active;var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            try{c.aspect=(float)width/height;RenderPipeline.SubmitRenderRequest(c,new RenderPipeline.StandardRequest{destination=rt});RenderPipeline.SubmitRenderRequest(c,new RenderPipeline.StandardRequest{destination=rt});Graphics.Blit(rt,ldr);RenderTexture.active=ldr;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();if(path!=null)File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);RenderTexture.ReleaseTemporary(ldr);}if(retain)return image;Object.DestroyImmediate(image);return null;
        }
        static void Finish()
        {
            if(!pending||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<ready)return;pending=false;EditorApplication.update-=Finish;
            try
            {
                globals.fixedTime=true;globals.fixedSeconds=1;globals.Apply();var cameras=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c=>c.name!="TG Review camera tour").ToArray();
                if(!sample&&!reload){SetProfile(false);if(!BakeActorProbes())throw new Exception("Actor probe bake failed; read ProbeBake.json.");BakeReflection();Save();}
                foreach(var neutral in sample?new[]{false}:new[]{true,false}){SetProfile(neutral);foreach(var c in cameras.Where(c=>!sample||new[]{"TG_EnvironmentOverview","TG_PlayerPlanning","TG_EnemyPlanning","TG_PlayerAttack","TG_WaterSample"}.Contains(c.name)))Capture(c,output+(neutral?"/Neutral/":"/ReferenceDay/")+c.name+".png");}
                SetProfile(false);CaptureEffects();CaptureBeauty();CaptureReferenceRockAblations();if(!sample)CaptureAblations();var pass=ValidateLighting();
                if(!pass)throw new Exception("WP08 validation failed; read LightingValidation.json.");globals.fixedTime=false;globals.Apply();Save();
                if(reload)Export();File.WriteAllText(output+"/COMPLETE.txt",sample?"WP08 representative lighting sample complete; inspect before full bake/review.":"WP08 saved lighting, actual current actor/effect fixtures and scoped probe review complete.");EditorApplication.Exit(0);
            }catch(Exception e){Fail(e);}
        }
        static void CaptureEffects()
        {
            foreach(var name in new[]{"TG_PlayerAttack","TG_EnemyAttack","TG_PlayerPlanning","TG_EnemyPlanning"})
            {
                var a=layout.anchors[name.Contains("Enemy")?4:1];var root=CreateAuraFixture(a.worldPosition,"WP08 temporary actual aura");try{Capture(GameObject.Find(name).GetComponent<Camera>(),output+"/ActualAura/"+name+".png");}finally{Object.DestroyImmediate(root);}
            }
        }
        static void CaptureBeauty()
        {
            var actors=GameObject.Find(ActorFixtureGroupName);actors.SetActive(false);
            try{foreach(var name in new[]{"TG_EnvironmentOverview","TG_PlayerAttack","TG_EnemyAttack","TG_WaterSample","TG_CanopySample"}){var go=GameObject.Find(name);if(go)Capture(go.GetComponent<Camera>(),output+"/Beauty/"+name+".png");}}
            finally{actors.SetActive(true);}
        }
        static void CaptureAblations()
        {
            var c=GameObject.Find("TG_EnvironmentOverview").GetComponent<Camera>();var profile=rig.profile;var fog=profile.fog;profile.fog=false;rig.Apply();Capture(c,output+"/Ablations/NoDistanceHaze.png");profile.fog=fog;rig.Apply();
            var bloom=profile.volume.components.OfType<Bloom>().First();var intensity=bloom.intensity.value;bloom.intensity.value=0;Capture(GameObject.Find("TG_PlayerAttack").GetComponent<Camera>(),output+"/Ablations/NoBloom.png");bloom.intensity.value=intensity;
            var shadows=sun.shadows;sun.shadows=LightShadows.None;Capture(GameObject.Find("TG_PlayerPlanning").GetComponent<Camera>(),output+"/Ablations/NoSunShadows.png");sun.shadows=shadows;
            var root=CreateAuraFixture(layout.anchors[1].worldPosition,"Temporary actual aura post-process check");try
            {
                var camera=GameObject.Find("TG_PlayerAttack").GetComponent<Camera>();bloom.intensity.value=0;Capture(camera,output+"/Ablations/AuraNoBloom.png");bloom.intensity.value=intensity;
                var d=camera.GetUniversalAdditionalCameraData();d.renderPostProcessing=false;Capture(camera,output+"/Ablations/AuraNoPostProcess.png");d.renderPostProcessing=true;
            }finally{Object.DestroyImmediate(root);bloom.intensity.value=intensity;}
            var actors=GameObject.Find(ActorFixtureGroupName).GetComponentsInChildren<MeshRenderer>();foreach(var a in actors){a.lightProbeUsage=LightProbeUsage.Off;a.reflectionProbeUsage=ReflectionProbeUsage.Off;}Capture(GameObject.Find("TG_PlayerAttack").GetComponent<Camera>(),output+"/Ablations/ActorsNoProbes.png");foreach(var a in actors){a.lightProbeUsage=LightProbeUsage.BlendProbes;a.reflectionProbeUsage=ReflectionProbeUsage.BlendProbesAndSkybox;}
        }
        static void Export()
        {
            var inputs=new[]{ReviewScene,FinalPrefab,Lighting+"/Profiles/TG_Neutral.asset",Lighting+"/Profiles/TG_ReferenceDay.asset",Lighting+"/Settings/TG_LightingURP.asset"}.Concat(AssetDatabase.FindAssets("",new[]{Lighting+"/Shaders",Lighting+"/Runtime",Shared+"/Shaders",Root+"/Integration/Shaders",Root+"/Water/Shaders"}).Select(AssetDatabase.GUIDToAssetPath)).Where(p=>!AssetDatabase.IsValidFolder(p)).ToArray();var paths=AssetDatabase.GetDependencies(inputs,true).Concat(inputs).Where(p=>p.StartsWith("Assets/")&&!AssetDatabase.IsValidFolder(p)).Distinct().ToArray();AssetDatabase.ExportPackage(paths,Path.GetFullPath(output+"/../TG_WP08_Lighting.unitypackage"),(ExportPackageOptions)0);
        }
    }
}
