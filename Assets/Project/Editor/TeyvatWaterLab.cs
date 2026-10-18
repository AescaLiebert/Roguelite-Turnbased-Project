using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using FightingAllstar.EnvironmentArt;
using Object=UnityEngine.Object;
using Debug=UnityEngine.Debug;

namespace FightingAllstar.EditorTools
{
    public static partial class TeyvatWaterLab
    {
        const string Water="Assets/Project/Art/Environment/TeyvatGrassland/Water";
        const string Shared="Assets/Project/Art/Environment/TeyvatGrassland/Shared";
        const string R3="Assets/Project/Art/Environment/TeyvatGrassland/LayoutR3";
        const string Trees="Assets/Project/Art/Environment/TeyvatGrassland/Trees";
        const string ReviewScene=Water+"/Scenes/TG_WaterReview_r1.unity";
        static string output;static Transform environment,waterRoot;static Vector3 origin;
        static Layout layout;static Plan plan;static Manifest manifest;static TeyvatMaterialGlobals globals;
        static bool sample,reload,pending;static double ready;
        static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
        static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        [Serializable] sealed class Anchor{public string name;public Vector3 worldPosition,localPosition;}
        [Serializable] sealed class Shot{public string name;public Vector3 position;public Quaternion rotation;public float fov;}
        [Serializable] sealed class Layout{public Vector3 origin;public Quaternion rotation;public Anchor[] anchors;public Shot[] shots;}
        [Serializable] sealed class Island{public string name;public float x,z,width,depth,top,rotation;}
        [Serializable] sealed class Fall{public string name;public float x,half_width,front_z,crest_y;public float[] path_flat;}
        [Serializable] sealed class Plan{public float water_level;public Island[] islands;public Fall[] waterfalls;}
        [Serializable] sealed class BoundData{public float[] min,max,size;}
        [Serializable] sealed class SourceObject{public string name,role;public int expected_triangles_after_export;public BoundData bounds_unity_local_metres;public float[] unity_normal_min,unity_normal_max;}
        [Serializable] sealed class Manifest{public string schema,fbx_sha256;public int triangles;public SourceObject[] objects;}
        public static void SampleOnly(){Start(true,false);}
        public static void RunIsolated(){Start(false,false);}
        public static void VerifySaved(){Start(false,true);}
        static void Start(bool justSample,bool saved)
        {
            var marker=Path.Combine(Path.GetDirectoryName(Application.dataPath),"TG_ISOLATED_WATER.txt");
            if(!File.Exists(marker)||File.Exists("Assets/Project/Scenes/Battle.unity"))throw new InvalidOperationException("WP06 requires isolated art host.");
            output=File.ReadAllText(marker).Trim();Directory.CreateDirectory(output);sample=justSample;reload=saved;
            try
            {
                ShaderUtil.allowAsyncCompilation=false;if(PlayerSettings.colorSpace!=ColorSpace.Linear)throw new Exception("Cold Linear host required.");
                foreach(var f in new[]{"Materials","Prefabs","Scenes","Data"})Directory.CreateDirectory(Water+"/"+f);
                AssetDatabase.Refresh();layout=JsonUtility.FromJson<Layout>(File.ReadAllText(R3+"/Data/TG_Layout.json"));origin=layout.origin;
                plan=JsonUtility.FromJson<Plan>(File.ReadAllText(Water+"/Data/TG_WaterPlan.json"));manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Water+"/Models/TG_WaterKit.manifest.json"));
                if(!saved)
                {
                    var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Shared+"/Settings/TG_MaterialURP.asset");GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;QualitySettings.vSyncCount=0;
                    var importer=(ModelImporter)AssetImporter.GetAtPath(Water+"/Models/TG_WaterKit.fbx");importer.globalScale=1;importer.bakeAxisConversion=true;importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.Import;importer.isReadable=true;importer.meshCompression=ModelImporterMeshCompression.Off;importer.importCameras=false;importer.importLights=false;importer.SaveAndReimport();
                }
                foreach(var m in AssetDatabase.LoadAllAssetsAtPath(Water+"/Models/TG_WaterKit.fbx").OfType<Mesh>())meshes[m.name]=m;
                if(saved){EditorSceneManager.OpenScene(ReviewScene);environment=GameObject.Find("TG WP01 R3 redesigned landscape").transform;waterRoot=environment.Find("TG WP06 water and cascades");globals=Object.FindFirstObjectByType<TeyvatMaterialGlobals>();foreach(var role in new[]{"River","Waterfall","Pool","Foam","Mist","Shore"})materials[role]=AssetDatabase.LoadAssetAtPath<Material>(Water+"/Materials/TG_Water_"+role+".mat");}
                else{BuildMaterials();BuildStage();}
                pending=true;ready=EditorApplication.timeSinceStartup+3;EditorApplication.update+=Finish;
            }catch(Exception e){Fail(e);}
        }
        static void Fail(Exception e){Debug.LogException(e);File.WriteAllText(output+"/FAILED.txt",e.ToString());EditorApplication.Exit(1);}
        static Transform Group(string name,Transform parent=null){var t=new GameObject(name).transform;if(parent!=null)t.SetParent(parent,false);return t;}
        static Material Mat(string name,string shader)
        {
            var path=Water+"/Materials/TG_Water_"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);}else m.shader=Shader.Find(shader);m.name="TG_Water_"+name;m.enableInstancing=true;materials[name]=m;EditorUtility.SetDirty(m);return m;
        }
        static void BuildMaterials()
        {
            foreach(var role in new[]{"River","Waterfall","Pool"})
            {
                var m=Mat(role,"Teyvat/URP/WaterProduction");var fall=role=="Waterfall";
                m.SetColor("_ShallowColor",fall?new Color(.16f,.66f,.75f,.85f):new Color(.12f,.76f,.71f,.76f));m.SetColor("_DeepColor",fall?new Color(.08f,.43f,.60f,.9f):new Color(.08f,.52f,.65f,.94f));m.SetColor("_FoamColor",new Color(.93f,.99f,.98f));m.SetColor("_SkyColor",new Color(.48f,.77f,.88f));
                m.SetFloat("_DepthRange",5.4f);m.SetFloat("_FoamDistance",.56f);m.SetFloat("_FlowSpeed",fall?.32f:.65f);m.SetVector("_FlowDirection",fall?new Vector4(0,1,0,0):new Vector4(.96f,-.28f,0,0));m.SetFloat("_FlowStrength",fall?.46f:.25f);m.SetFloat("_UseSceneDepth",fall?0:1);m.SetFloat("_FallbackDepth",2);m.SetFloat("_Mode",fall?1:role=="Pool"?2:0);m.SetFloat("_Opacity",1);m.SetFloat("_ReflectionStrength",fall?0:.18f);m.SetFloat("_FoamStrength",fall?.98f:.72f);m.SetFloat("_RippleScale",.24f);m.SetFloat("_EdgeSoftness",fall?.07f:.11f);m.SetFloat("_Quality",1);m.SetFloat("_DebugDepth",0);m.renderQueue=role=="River"?3000:role=="Waterfall"?3001:3002;
            }
            foreach(var role in new[]{"Foam","Mist","Shore"})
            {
                var m=Mat(role,"Teyvat/URP/WaterEffects");m.SetColor("_BaseColor",role=="Mist"?new Color(.76f,.90f,.93f):new Color(.88f,.97f,.95f));m.SetFloat("_Opacity",role=="Mist"?.40f:role=="Shore"?.48f:.95f);m.SetFloat("_EffectMode",role=="Mist"?2:role=="Shore"?3:0);m.SetFloat("_FlowSpeed",role=="Mist"?.18f:.35f);m.SetFloat("_DepthFade",role=="Mist"?.7f:.04f);m.SetFloat("_UseSceneDepth",1);m.SetFloat("_NoiseScale",role=="Shore"?1.15f:3.3f);m.SetFloat("_Softness",.38f);m.SetFloat("_Quality",1);m.renderQueue=role=="Mist"?3010:3005;
            }
            AssetDatabase.SaveAssets();
        }
        static void BuildStage()
        {
            EditorSceneManager.OpenScene(Trees+"/Scenes/TG_CanopyReview_r1.unity");EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),sample?Water+"/Scenes/TG_WaterSample_r1.unity":ReviewScene);
            environment=GameObject.Find("TG WP01 R3 redesigned landscape").transform;
            foreach(var t in environment.Cast<Transform>().Where(t=>t.name=="R3_River_with_five_islands"||t.name.StartsWith("R3_Cascade_")||t.name.StartsWith("R3_CatchPool_")).ToArray())Object.DestroyImmediate(t.gameObject);
            waterRoot=Group("TG WP06 water and cascades",environment);globals=Object.FindFirstObjectByType<TeyvatMaterialGlobals>();
            foreach(var item in manifest.objects)
            {
                if(item.role=="Diagnostic")continue;
                if(sample&&item.role!="River"&&item.role!="Shore"&&!item.name.Contains("Hero"))continue;
                Visual(item.name.Substring(3),meshes[item.name],materials[item.role],waterRoot);
            }
            CameraAt("TG_WaterSample",new Vector3(-28,13,39),new Vector3(-12,9,70),56);
            CameraAt("TG_WaterGrazing",new Vector3(-30,.4f,27),new Vector3(5,-.6f,48),59);
            CameraAt("TG_WaterIslandChannels",new Vector3(1,18,17),new Vector3(-4,1,46),58);
            CameraAt("TG_WaterReverse",new Vector3(22,9,70),new Vector3(-8,1,30),57);
            CameraAt("TG_WaterRightCascade",new Vector3(65,10,44),new Vector3(43,7,81),56);
            if(!sample)
            {
                PrefabUtility.SaveAsPrefabAsset(waterRoot.gameObject,Water+"/Prefabs/TG_WaterComposition.prefab");
                BuildFallback();
            }
            Lighting(false);globals.fixedTime=false;EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),sample?Water+"/Scenes/TG_WaterSample_r1.unity":ReviewScene);AssetDatabase.SaveAssets();
        }
        static void Visual(string name,Mesh mesh,Material mat,Transform parent)
        {
            var t=Group(name,parent);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;
        }
        static void BuildFallback()
        {
            var root=Group("TG WP06 lower cost depth independent water");var fallback=new Dictionary<string,Material>();
            foreach(var role in new[]{"River","Waterfall","Pool"})
            {
                var path=Water+"/Materials/TG_Water_"+role+"Fallback.mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(materials[role]);AssetDatabase.CreateAsset(m,path);}else EditorUtility.CopySerialized(materials[role],m);m.name="TG_Water_"+role+"Fallback";m.SetFloat("_UseSceneDepth",0);m.SetFloat("_Quality",0);m.SetFloat("_FoamStrength",role=="Waterfall"?.85f:.4f);m.SetFloat("_ReflectionStrength",.12f);EditorUtility.SetDirty(m);fallback[role]=m;
            }
            foreach(var item in manifest.objects.Where(i=>fallback.ContainsKey(i.role)))Visual(item.name.Substring(3),meshes[item.name],fallback[item.role],root);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject,Water+"/Prefabs/TG_WaterFallback.prefab");Object.DestroyImmediate(root.gameObject);
        }
        static void CameraAt(string name,Vector3 p,Vector3 target,float fov)
        {
            var c=Group(name).gameObject.AddComponent<Camera>();c.transform.SetPositionAndRotation(origin+p,Quaternion.LookRotation(target-p));c.fieldOfView=fov;c.nearClipPlane=.1f;c.farClipPlane=800;c.enabled=false;c.allowHDR=true;c.allowMSAA=true;var d=c.GetUniversalAdditionalCameraData();d.requiresDepthTexture=true;d.requiresColorTexture=false;
        }
        static void Lighting(bool neutral)
        {
            var sun=Object.FindFirstObjectByType<Light>();sun.type=LightType.Directional;sun.intensity=neutral?1.1f:1.35f;sun.color=neutral?Color.white:new Color(1,.93f,.79f);sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(neutral?55:38,-43,0);RenderSettings.sun=sun;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.55f,.69f,.82f);RenderSettings.ambientEquatorColor=new Color(.32f,.43f,.47f);RenderSettings.ambientGroundColor=new Color(.24f,.29f,.17f);RenderSettings.fog=!neutral;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=80;RenderSettings.fogEndDistance=550;RenderSettings.fogColor=new Color(.62f,.78f,.85f);RenderSettings.skybox=AssetDatabase.LoadAssetAtPath<Material>(Shared+"/Settings/TG_MaterialSky.mat");
        }
        static double Capture(Camera camera,string path)
        {
            if(path!=null)Directory.CreateDirectory(Path.GetDirectoryName(path));var rt=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,4);var resolved=RenderTexture.GetTemporary(1600,900,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var previous=RenderTexture.active;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);var clock=Stopwatch.StartNew();
            try{camera.aspect=16f/9;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});Graphics.Blit(rt,resolved);RenderTexture.active=resolved;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();clock.Stop();if(path!=null)File.WriteAllBytes(path,image.EncodeToPNG());return clock.Elapsed.TotalMilliseconds;}
            finally{RenderTexture.active=previous;Object.DestroyImmediate(image);RenderTexture.ReleaseTemporary(rt);RenderTexture.ReleaseTemporary(resolved);}
        }
        [Serializable] sealed class CostReport{public string context,gpu,cpu;public double[] fullMs,fallbackMs,offMs;public int fullRenderers,fallbackRenderers,triangles;}
        static void Finish()
        {
            if(!pending||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<ready)return;pending=false;EditorApplication.update-=Finish;
            try
            {
                globals.fixedTime=true;globals.fixedSeconds=1;globals.Apply();var pass=ValidateWaterDepth();if(!sample)pass=ValidateWater()&&pass;
                var cameras=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);var sampleNames=new[]{"TG_WaterSample","TG_WaterGrazing","TG_WaterIslandChannels"};
                foreach(var neutral in reload?new[]{false}:new[]{true,false}){Lighting(neutral);foreach(var c in cameras.Where(c=>sample?sampleNames.Contains(c.name):!reload||new[]{"TG_EnvironmentOverview","TG_PlayerAttack","TG_WaterSample","TG_WaterGrazing"}.Contains(c.name)))Capture(c,output+(sample?"/Sample":"")+(neutral?"/Neutral/":"/Afternoon/")+c.name+".png");}
                if(!reload)
                {
                    var c=cameras.First(c=>c.name=="TG_WaterSample");foreach(var t in new[]{0f,.5f,1f,1.5f,2f,2.5f,3f,3.5f}){globals.fixedSeconds=t;globals.Apply();Capture(c,output+(sample?"/Sample":"")+"/Flow/T"+t+".png");}globals.fixedSeconds=1;globals.Apply();
                    if(!sample){CaptureSorting();MeasureCost(cameras.First(c=>c.name=="TG_EnvironmentOverview"));}
                }
                if(!pass)throw new Exception("WP06 validation failed; read water reports.");
                globals.fixedTime=false;Lighting(false);EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),sample?Water+"/Scenes/TG_WaterSample_r1.unity":ReviewScene);AssetDatabase.SaveAssets();
                if(reload)
                {
                    var own=AssetDatabase.FindAssets("",new[]{Water,Shared+"/Shaders",Shared+"/Settings"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>!AssetDatabase.IsValidFolder(p)).ToArray();var paths=AssetDatabase.GetDependencies(own,true).Concat(own).Where(p=>p.StartsWith("Assets/")&&!AssetDatabase.IsValidFolder(p)).Distinct().ToArray();AssetDatabase.ExportPackage(paths,Path.GetFullPath(output+"/../TG_WP06_Water.unitypackage"),(ExportPackageOptions)0);
                }
                File.WriteAllText(output+"/COMPLETE.txt",sample?"WP06 representative water sample and GPU depth check completed; inspect images before full assembly.":"WP06 water assembled and saved-scene/import/depth review passed. Read handoff for transparency and player-performance limits.");EditorApplication.Exit(0);
            }catch(Exception e){Fail(e);}
        }
        static void MeasureCost(Camera camera)
        {
            var fallback=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Water+"/Prefabs/TG_WaterFallback.prefab"));fallback.transform.SetParent(environment,false);fallback.SetActive(false);var full=new List<double>();var cheap=new List<double>();var off=new List<double>();
            try{for(var i=0;i<7;i++){waterRoot.gameObject.SetActive(true);fallback.SetActive(false);var a=Capture(camera,null);waterRoot.gameObject.SetActive(false);fallback.SetActive(true);var b=Capture(camera,null);fallback.SetActive(false);var c=Capture(camera,null);if(i>0){full.Add(a);cheap.Add(b);off.Add(c);}}waterRoot.gameObject.SetActive(false);fallback.SetActive(true);Capture(camera,output+"/Fallback/EnvironmentOverview.png");File.WriteAllText(output+"/WaterCost.json",JsonUtility.ToJson(new CostReport{context="Unity6000.3.4f1 Editor D3D11 Linear URP1600x900 4xMSAA, six alternating observations after warmup. Two render submissions and synchronous CPU readback per observation. Not GPU timings, player FPS or minimum PC certification.",gpu=SystemInfo.graphicsDeviceName,cpu=SystemInfo.processorType,fullMs=full.ToArray(),fallbackMs=cheap.ToArray(),offMs=off.ToArray(),fullRenderers=waterRoot.GetComponentsInChildren<Renderer>(true).Length,fallbackRenderers=fallback.GetComponentsInChildren<Renderer>(true).Length,triangles=manifest.triangles},true));}
            finally{Object.DestroyImmediate(fallback);waterRoot.gameObject.SetActive(true);}
        }
        static void CaptureSorting()
        {
            // Representative transparent magic patches, not copied gameplay effects.
            var rig=Group("Temporary transparent-combat sorting fixture");var mat=new Material(Shader.Find("Teyvat/URP/WaterEffects"));mat.SetColor("_BaseColor",new Color(.70f,.22f,1f));mat.SetFloat("_Opacity",.75f);mat.SetFloat("_EffectMode",2);mat.SetFloat("_UseSceneDepth",1);mat.SetFloat("_DepthFade",.2f);mat.renderQueue=3020;
            try
            {
                var patch=new Mesh{name="Temporary magic patch",vertices=new[]{new Vector3(-3,-2,0),new Vector3(3,-2,0),new Vector3(3,2,0),new Vector3(-3,2,0)},uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up},colors=Enumerable.Repeat(Color.white,4).ToArray(),triangles=new[]{0,1,2,0,2,3}};patch.RecalculateBounds();
                for(var i=0;i<3;i++){Visual("Effect plane "+i,patch,mat,rig);rig.GetChild(i).position=origin+new Vector3(-12+(i-1)*8,2,42+i*9);}
                foreach(var name in new[]{"TG_PlayerAttack","TG_EnemyAttack","TG_WaterGrazing","TG_WaterReverse"})Capture(GameObject.Find(name).GetComponent<Camera>(),output+"/Transparency/"+name+".png");Object.DestroyImmediate(patch);
                File.WriteAllText(output+"/TransparencyReview.txt","Captured temporary representative purple transparent effect planes before/between/behind island water and mist from both acting-side attacks and grazing/reverse views. Water queue3000, curtains3001, pools3002, foam3005, mist3010, effects3020; ZTestLEqual, ZWriteOff. Terrain/actors occlude by opaque depth. Fixed queue ensures representative overlay readability, not correct arbitrary interpenetrating transparent geometry. Actual game VFX/character shaders, overlapping effects and animation remain WP07 integration checks. No opaque texture, extra reflection camera or transparent depth pass is enabled.");
            }finally{Object.DestroyImmediate(rig.gameObject);Object.DestroyImmediate(mat);}
        }
    }
}
