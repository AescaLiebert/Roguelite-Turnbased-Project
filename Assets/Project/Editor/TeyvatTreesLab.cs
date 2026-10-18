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
    public static partial class TeyvatTreesLab
    {
        const string Trees="Assets/Project/Art/Environment/TeyvatGrassland/Trees";
        const string Shared="Assets/Project/Art/Environment/TeyvatGrassland/Shared";
        const string R3="Assets/Project/Art/Environment/TeyvatGrassland/LayoutR3";
        const string ScenePath=Trees+"/Scenes/TG_CanopyReview_r1.unity";
        static string output;static Transform environment,forest;static Vector3 origin;
        static TreeComposition composition;static Layout layout;static Manifest manifest;
        static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
        static readonly Dictionary<string,GameObject> prefabs=new Dictionary<string,GameObject>();
        static readonly Dictionary<string,float> heights=new Dictionary<string,float>();
        static TeyvatMaterialGlobals globals;static bool sample,reload,pending;static double ready;
        static readonly string[] Species={"HeroOak","MeadowOak","IslandTree","WindPine","YoungPine","Shrub"};
        [Serializable] sealed class TreeAnchor{public string name,role;public Vector3 localPosition;public float heightStartingValue;}
        [Serializable] sealed class TreeComposition{public int seed;public TreeAnchor[] trees;}
        [Serializable] sealed class Anchor{public string name;public Vector3 worldPosition,localPosition;}
        [Serializable] sealed class Shot{public string name;public Vector3 position;public Quaternion rotation;public float fov;}
        [Serializable] sealed class Layout{public Vector3 origin;public Quaternion rotation;public Anchor[] anchors;public Shot[] shots;}
        [Serializable] sealed class BoundData{public float[] min,max,size;}
        [Serializable] sealed class SourceObject{public string name,species;public int lod,expected_triangles_after_export;public BoundData bounds_unity_local_metres;public float[] unity_normal_min,unity_normal_max;public float height;}
        [Serializable] sealed class Manifest{public string schema,fbx_sha256;public SourceObject[] objects;}
        [Serializable] sealed class Placement{public string anchor,species;public Vector3 localPosition;public float yaw,height;}
        [Serializable] sealed class PlacementFile{public int seed=510731;public string policy;public Placement[] placements;}
        public static void SampleOnly(){Start(true,false);}
        public static void RunIsolated(){Start(false,false);}
        public static void VerifySaved(){Start(false,true);}
        static void Start(bool justSample,bool saved)
        {
            var marker=Path.Combine(Path.GetDirectoryName(Application.dataPath),"TG_ISOLATED_TREES.txt");
            if(!File.Exists(marker)||File.Exists("Assets/Project/Scenes/Battle.unity"))throw new InvalidOperationException("WP05 requires isolated art host.");
            output=File.ReadAllText(marker).Trim();Directory.CreateDirectory(output);sample=justSample;reload=saved;
            try
            {
                ShaderUtil.allowAsyncCompilation=false;if(PlayerSettings.colorSpace!=ColorSpace.Linear)throw new Exception("Cold Linear host required.");
                foreach(var folder in new[]{"Meshes","Materials","Prefabs","Scenes","Data"})Directory.CreateDirectory(Trees+"/"+folder);
                AssetDatabase.Refresh();manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Trees+"/Models/TG_TreeKit.manifest.json"));
                layout=JsonUtility.FromJson<Layout>(File.ReadAllText(R3+"/Data/TG_Layout.json"));origin=layout.origin;
                composition=JsonUtility.FromJson<TreeComposition>(File.ReadAllText(R3+"/Data/TG_TreeComposition.json"));
                if(!saved)
                {
                    var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Shared+"/Settings/TG_MaterialURP.asset");GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;QualitySettings.vSyncCount=0;
                    var importer=(ModelImporter)AssetImporter.GetAtPath(Trees+"/Models/TG_TreeKit.fbx");
                    importer.globalScale=1;importer.bakeAxisConversion=true;importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.Import;importer.isReadable=true;importer.meshCompression=ModelImporterMeshCompression.Off;importer.importCameras=false;importer.importLights=false;importer.SaveAndReimport();
                }
                foreach(var mesh in AssetDatabase.LoadAllAssetsAtPath(Trees+"/Models/TG_TreeKit.fbx").OfType<Mesh>())meshes[mesh.name]=mesh;
                if(saved){EditorSceneManager.OpenScene(ScenePath);environment=GameObject.Find("TG WP01 R3 redesigned landscape").transform;forest=environment.Find("TG WP05 trees and shrubs");globals=Object.FindFirstObjectByType<TeyvatMaterialGlobals>();}
                else{BuildPrefabs();if(sample)BuildSample();else BuildStage();}
                pending=true;ready=EditorApplication.timeSinceStartup+3;EditorApplication.update+=Finish;
            }catch(Exception e){Fail(e);}
        }
        static void Fail(Exception e){Debug.LogException(e);File.WriteAllText(output+"/FAILED.txt",e.ToString());EditorApplication.Exit(1);}
        static Transform Group(string name,Transform parent=null){var t=new GameObject(name).transform;if(parent!=null)t.SetParent(parent,false);return t;}
        static Material Material(string name,Color color,bool bark=false,bool pine=false)
        {
            var path=Trees+"/Materials/TG_"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Teyvat/URP/Foliage"));AssetDatabase.CreateAsset(mat,path);}mat.name="TG_"+name;
            mat.SetColor("_BaseColor",color);mat.SetColor("_RootColor",bark?Color.white:new Color(.88f,.96f,.90f));mat.SetColor("_TipColor",bark?Color.white:new Color(1.08f,1.06f,.88f));mat.SetColor("_CanopyShadeColor",new Color(.70f,.90f,.84f));
            mat.SetFloat("_Family",bark?2:1);mat.SetFloat("_UseColorField",0);mat.SetFloat("_WindResponse",.65f);mat.SetFloat("_Wrap",bark?.18f:.52f);mat.SetFloat("_Transmission",bark?0:.28f);mat.SetFloat("_AlphaClip",0);mat.SetFloat("_Cull",bark?2:0);mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
        }
        static void BuildPrefabs()
        {
            var texturePath=Trees+"/Textures/TG_LeafSprayAlbedoOpacity.png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;importer.mipmapEnabled=true;importer.mipMapsPreserveCoverage=true;importer.alphaTestReferenceValue=.60f;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=2;importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.npotScale=TextureImporterNPOTScale.None;importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            var bark=Material("TreeBark",new Color(.43f,.29f,.17f),true);
            var leaf=Material("Broadleaf",new Color(.36f,.61f,.17f));var light=Material("BroadleafSunTips",new Color(.49f,.69f,.21f));
            var needle=Material("PineNeedles",new Color(.19f,.43f,.28f),false,true);var needleTip=Material("PineTips",new Color(.29f,.52f,.30f),false,true);
            var spray=Material("LeafSpray",new Color(.47f,.72f,.24f));var sprayLight=Material("LeafSpraySunTips",new Color(.58f,.82f,.29f));
            foreach(var m in new[]{spray,sprayLight}){m.SetTexture("_BaseMap",texture);m.SetFloat("_AlphaClip",1);m.SetFloat("_Cutoff",.60f);EditorUtility.SetDirty(m);}
            var modelSlots=AssetDatabase.LoadAssetAtPath<GameObject>(Trees+"/Models/TG_TreeKit.fbx").GetComponentsInChildren<MeshFilter>().ToDictionary(f=>f.sharedMesh.name,f=>f.GetComponent<Renderer>().sharedMaterials.Select(m=>m.name).ToArray());
            File.WriteAllLines(output+"/MaterialBindings.txt",modelSlots.Select(s=>s.Key+": "+string.Join(", ",s.Value)));
            foreach(var species in Species)
            {
                var root=Group("TG_"+species);var renderers=new List<Renderer>();
                for(var lod=0;lod<3;lod++)
                {
                    var raw=meshes["SM_TG_"+species+"_LOD"+lod];var path=Trees+"/Meshes/"+raw.name+"_Animated.asset";
                    var copy=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(copy==null){copy=new Mesh();AssetDatabase.CreateAsset(copy,path);}
                    // Copy the actual buffers explicitly. CopySerialized on an imported
                    // Mesh can retain stale native buffers while updating its bounds.
                    copy.Clear();copy.indexFormat=raw.indexFormat;copy.vertices=raw.vertices;copy.normals=raw.normals;copy.tangents=raw.tangents;copy.uv=raw.uv;copy.colors=raw.colors;copy.subMeshCount=raw.subMeshCount;
                    for(var slot=0;slot<raw.subMeshCount;slot++)copy.SetTriangles(raw.GetTriangles(slot),slot,false);copy.UploadMeshData(false);
                    copy.name=raw.name+"_Animated";
                    // Shared wind maximum at strength1 response.65 is <.65m.
                    // Persist padding in mesh asset: renderer overrides are not saved.
                    var bounds=raw.bounds;bounds.Expand(new Vector3(3.2f,.30f,3.2f));copy.bounds=bounds;EditorUtility.SetDirty(copy);
                    var node=Group(species+"_LOD"+lod,root);node.gameObject.AddComponent<MeshFilter>().sharedMesh=copy;
                    var renderer=node.gameObject.AddComponent<MeshRenderer>();var binding=new Dictionary<string,Material>{{"Bark",bark},{"Leaf",species.Contains("Pine")?needle:leaf},{"LeafLight",species.Contains("Pine")?needleTip:light},{"LeafSpray",spray},{"LeafSprayLight",sprayLight}};
                    renderer.sharedMaterials=modelSlots[raw.name].Select(slot=>binding[slot]).ToArray();renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;renderers.Add(renderer);
                }
                var group=root.gameObject.AddComponent<LODGroup>();group.fadeMode=LODFadeMode.None;
                group.SetLODs(new[]{new LOD(.12f,new[]{renderers[0]}),new LOD(.045f,new[]{renderers[1]}),new LOD(.002f,new[]{renderers[2]})});group.RecalculateBounds();
                prefabs[species]=PrefabUtility.SaveAsPrefabAsset(root.gameObject,Trees+"/Prefabs/TG_"+species+".prefab");heights[species]=manifest.objects.First(o=>o.species==species&&o.lod==0).height;Object.DestroyImmediate(root.gameObject);
            }
            AssetDatabase.SaveAssets();
        }
        static Transform Place(string name,string species,Transform parent,Vector3 local,float height,float yaw)
        {
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[species]);obj.name=name;obj.transform.SetParent(parent,false);obj.transform.localPosition=local;obj.transform.localRotation=Quaternion.Euler(0,yaw,0);obj.transform.localScale=Vector3.one*(height/heights[species]);return obj.transform;
        }
        static void BuildSample()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);environment=Group("WP05 representative canopy sample");forest=Group("TG WP05 trees and shrubs",environment);origin=Vector3.zero;
            Place("Sample_HeroOak","HeroOak",forest,new Vector3(-9,0,0),12,20);Place("Sample_WindPine","WindPine",forest,new Vector3(8,0,1),10,80);Place("Sample_Shrub","Shrub",forest,new Vector3(3,0,-5),1.8f,20);
            var groundPath=Trees+"/Materials/TG_CanopySampleGround.mat";var ground=AssetDatabase.LoadAssetAtPath<Material>(groundPath);if(ground==null){ground=new Material(Shader.Find("Teyvat/URP/Surface"));AssetDatabase.CreateAsset(ground,groundPath);}ground.SetColor("_BaseColor",new Color(.68f,.68f,.64f));ground.SetFloat("_UseColorField",0);EditorUtility.SetDirty(ground);
            var plane=GameObject.CreatePrimitive(PrimitiveType.Plane);plane.transform.localScale=Vector3.one*10;plane.GetComponent<Renderer>().sharedMaterial=ground;Object.DestroyImmediate(plane.GetComponent<Collider>());
            globals=Group("One sample wind owner").gameObject.AddComponent<TeyvatMaterialGlobals>();globals.windStrength=.34f;globals.fixedTime=true;globals.fixedSeconds=1;globals.Apply();
            CameraAt("TG_CanopySample",new Vector3(-2,7,-26),new Vector3(-2,6,0),55);
            CameraAt("TG_CanopyUnderside",new Vector3(-9,2,-9),new Vector3(-9,8,0),60);
            CameraAt("TG_CanopyReverse",new Vector3(1,6,25),new Vector3(-2,6,0),55);
            Lighting(false);EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),Trees+"/Scenes/TG_CanopySample_r1.unity");
        }
        static void BuildStage()
        {
            EditorSceneManager.OpenScene(R3+"/Scenes/TG_LayoutReview_r3.unity");EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ScenePath);
            environment=GameObject.Find("TG WP01 R3 redesigned landscape").transform;
            var placeholder=environment.Find("R3 tree and shrub composition placeholders for WP05");if(placeholder==null)throw new Exception("Expected R3 tree placeholders missing; refusing broad deletion.");Object.DestroyImmediate(placeholder.gameObject);
            forest=Group("TG WP05 trees and shrubs",environment);globals=Object.FindFirstObjectByType<TeyvatMaterialGlobals>();
            var placements=new List<Placement>();var rng=new System.Random(510731);var terrain=environment.GetComponentInChildren<Terrain>();
            for(var i=0;i<composition.trees.Length;i++)
            {
                var a=composition.trees[i];var p=a.localPosition+Vector3.up*.06f;string species;
                if(a.role=="hero tree")species="HeroOak";
                else if(a.role=="perimeter framing")species="MeadowOak";
                else if(a.role=="river island tree")species="IslandTree";
                else
                {
                    // Species follow elevation/cluster roles rather than periodic index stripes.
                    var upper=p.z>95;var chance=upper?.66:.24;species=rng.NextDouble()<chance?(rng.NextDouble()<.35?"YoungPine":"WindPine"):"MeadowOak";
                }
                var yaw=(float)rng.NextDouble()*360;Place(a.name,species,forest,p,a.heightStartingValue,yaw);
                placements.Add(new Placement{anchor=a.name,species=species,localPosition=p,height=a.heightStartingValue,yaw=yaw});
                // Root clusters are sparse accents. Ground height comes from the unchanged
                // native R3 surface, with water and battle-core rejection.
                if(i<10||rng.NextDouble()<.23)for(var j=0;j<(i==0?4:2);j++)
                {
                    var angle=(float)rng.NextDouble()*Mathf.PI*2;var distance=a.heightStartingValue*(i==0?.28f:.35f)+(float)rng.NextDouble();
                    var q=p+new Vector3(Mathf.Cos(angle)*distance,0,Mathf.Sin(angle)*distance);var wp=origin+q;
                    q.y=terrain.SampleHeight(wp)+terrain.transform.position.y-origin.y-.06f;
                    if(q.y<-1||Mathf.Abs(q.x)<19&&Mathf.Abs(q.z)<19)continue;
                    var size=.9f+(float)rng.NextDouble()*.8f;var syaw=(float)rng.NextDouble()*360;
                    var name="ShrubGroup_"+placements.Count;Place(name,"Shrub",forest,q,size,syaw);placements.Add(new Placement{anchor=name,species="Shrub",localPosition=q,height=size,yaw=syaw});
                }
            }
            File.WriteAllText(Trees+"/Data/TG_TreePlacement.json",JsonUtility.ToJson(new PlacementFile{policy="All120 R3 tree trunk XZ and target heights retained. Species selected by authored role/elevation with seed510731. Root lowered .04m relative to native ground; shrub accents reject water and central actor region. No terrain, meadow, cameras or shared wind changes.",placements=placements.ToArray()},true));
            CameraAt("TG_TreeHeroStudy",new Vector3(-32,7,-20),new Vector3(-32,10,-1),54);
            CameraAt("TG_TreeIslandStudy",new Vector3(-31,7,26),new Vector3(-12,4.8f,43),55);
            CameraAt("TG_TreeReverseStudy",new Vector3(5,6,35),new Vector3(-15,6,-40),58);
            PrefabUtility.SaveAsPrefabAsset(forest.gameObject,Trees+"/Prefabs/TG_TreeComposition.prefab");
            Lighting(false);globals.fixedTime=false;EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ScenePath);AssetDatabase.SaveAssets();
        }
        static void CameraAt(string name,Vector3 p,Vector3 target,float fov)
        {
            var c=Group(name).gameObject.AddComponent<Camera>();c.transform.SetPositionAndRotation(origin+p,Quaternion.LookRotation(target-p));c.fieldOfView=fov;c.nearClipPlane=.1f;c.farClipPlane=800;c.enabled=false;c.allowHDR=true;c.allowMSAA=true;c.GetUniversalAdditionalCameraData().requiresDepthTexture=true;
        }
        static void Lighting(bool neutral)
        {
            var sun=Object.FindFirstObjectByType<Light>();if(sun==null)sun=Group("WP05 review sun").gameObject.AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=neutral?1.1f:1.35f;sun.color=neutral?Color.white:new Color(1,.93f,.79f);sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(neutral?55:38,-43,0);RenderSettings.sun=sun;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.55f,.69f,.82f);RenderSettings.ambientEquatorColor=new Color(.32f,.43f,.47f);RenderSettings.ambientGroundColor=new Color(.24f,.29f,.17f);RenderSettings.fog=!neutral;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=80;RenderSettings.fogEndDistance=550;RenderSettings.fogColor=new Color(.62f,.78f,.85f);RenderSettings.skybox=AssetDatabase.LoadAssetAtPath<Material>(Shared+"/Settings/TG_MaterialSky.mat");
        }
        static double Capture(Camera camera,string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));var rt=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,4);var resolved=RenderTexture.GetTemporary(1600,900,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var previous=RenderTexture.active;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);var clock=Stopwatch.StartNew();
            try{camera.aspect=16f/9;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});Graphics.Blit(rt,resolved);RenderTexture.active=resolved;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();clock.Stop();if(path!=null)File.WriteAllBytes(path,image.EncodeToPNG());return clock.Elapsed.TotalMilliseconds;}
            finally{RenderTexture.active=previous;Object.DestroyImmediate(image);RenderTexture.ReleaseTemporary(rt);RenderTexture.ReleaseTemporary(resolved);}
        }
        [Serializable] sealed class CostReport{public string gpu,cpu,context;public double[] canopyOnMs,canopyOffMs;public int trees,shrubs,lod0TrianglesPerKit,lod1TrianglesPerKit,lod2TrianglesPerKit;}
        [Serializable] sealed class IntegrationReport{public bool passed,originalCameraPoses,terrainReference,meadowReference,shadersSupported,textureCoverage,animatedBuffersMatch;public int globalsOwners,missingScripts,textureWidth,textureHeight,textureMips;public float maxTreeHeightError,maxRootOriginError;public string method;}
        static bool ValidateIntegration()
        {
            var terrain=environment.GetComponentInChildren<Terrain>();var meadow=environment.GetComponentInChildren<TeyvatMeadowRenderer>();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Trees+"/Textures/TG_LeafSprayAlbedoOpacity.png");var importer=(TextureImporter)AssetImporter.GetAtPath(Trees+"/Textures/TG_LeafSprayAlbedoOpacity.png");
            var report=new IntegrationReport{originalCameraPoses=layout.shots.All(s=>{var c=GameObject.Find("TG_"+s.name).GetComponent<Camera>();return Vector3.Distance(c.transform.position,s.position)<.0001f&&Quaternion.Angle(c.transform.rotation,s.rotation)<.001f&&Mathf.Abs(c.fieldOfView-s.fov)<.001f;}),terrainReference=AssetDatabase.GetAssetPath(terrain.terrainData)==R3+"/Terrain/TG_Landscape.asset",meadowReference=AssetDatabase.GetAssetPath(meadow.data)==R3+"/Meadow/TG_MeadowPlacement.asset",globalsOwners=Object.FindObjectsByType<TeyvatMaterialGlobals>(FindObjectsSortMode.None).Length,textureWidth=texture.width,textureHeight=texture.height,textureMips=texture.mipmapCount,textureCoverage=importer.sRGBTexture&&importer.mipmapEnabled&&importer.mipMapsPreserveCoverage&&Mathf.Abs(importer.alphaTestReferenceValue-.60f)<.001f,method="Read-only actual source references, original camera poses/FOVs, one global wind owner, all scene materials/shaders, texture import/mips, actualLOD0 species height and root-origin height against unchanged native R3 terrain. Root origin only; buttress tips on slopes remain a visual set-dressing check."};
            report.animatedBuffersMatch=true;
            foreach(var mesh in forest.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh).Distinct())
            {var raw=meshes[mesh.name.Replace("_Animated","")];if(mesh.vertexCount!=raw.vertexCount||mesh.subMeshCount!=raw.subMeshCount||!mesh.vertices.SequenceEqual(raw.vertices)||!mesh.colors.SequenceEqual(raw.colors)||!mesh.normals.SequenceEqual(raw.normals))report.animatedBuffersMatch=false;for(var i=0;i<Mathf.Min(mesh.subMeshCount,raw.subMeshCount);i++)if(!mesh.GetTriangles(i).SequenceEqual(raw.GetTriangles(i)))report.animatedBuffersMatch=false;}
            foreach(var a in composition.trees){var tree=forest.Find(a.name);var m=tree.GetComponentsInChildren<MeshFilter>(true).First(f=>f.sharedMesh.name.Contains("_LOD0"));var raw=meshes[m.sharedMesh.name.Replace("_Animated","")];report.maxTreeHeightError=Mathf.Max(report.maxTreeHeightError,Mathf.Abs(raw.bounds.size.y*tree.localScale.y-a.heightStartingValue));report.maxRootOriginError=Mathf.Max(report.maxRootOriginError,Mathf.Abs(tree.position.y-(terrain.SampleHeight(tree.position)+terrain.transform.position.y)));}
            report.missingScripts=SceneManager.GetActiveScene().GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
            report.shadersSupported=environment.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).All(m=>m.shader.isSupported&&!ShaderUtil.GetShaderMessages(m.shader).Any(e=>e.severity.ToString()=="Error"));
            report.passed=report.animatedBuffersMatch&&report.originalCameraPoses&&report.terrainReference&&report.meadowReference&&report.globalsOwners==1&&report.textureCoverage&&report.textureMips>1&&report.missingScripts==0&&report.shadersSupported&&report.maxTreeHeightError<.01f&&report.maxRootOriginError<.15f;
            File.WriteAllText(output+"/TreeIntegration.json",JsonUtility.ToJson(report,true));return report.passed;
        }
        static void Finish()
        {
            if(!pending||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<ready)return;pending=false;EditorApplication.update-=Finish;
            try
            {
                globals.fixedTime=true;globals.fixedSeconds=1;globals.Apply();
                var cameras=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);var pass=sample||(ValidateTrees()&ValidateIntegration());
                foreach(var neutral in reload?new[]{false}:new[]{true,false})
                {
                    Lighting(neutral);foreach(var camera in cameras.Where(c=>!reload||new[]{"TG_EnvironmentOverview","TG_PlayerAttack","TG_TreeHeroStudy"}.Contains(c.name)))Capture(camera,output+(sample?"/Sample":"")+(neutral?"/Neutral/":"/Afternoon/")+camera.name+".png");
                }
                var hero=cameras.FirstOrDefault(c=>c.name==(sample?"TG_CanopyUnderside":"TG_TreeHeroStudy"));
                if(!reload&&hero!=null)
                {
                    foreach(var t in new[]{0f,2f,5f}){globals.fixedSeconds=t;globals.Apply();Capture(hero,output+(sample?"/Sample":"")+"/Wind/T"+t+".png");}
                    var strength=globals.windStrength;globals.windStrength=0;globals.Apply();Capture(hero,output+(sample?"/Sample":"")+"/Wind/Off.png");globals.windStrength=strength;globals.fixedSeconds=1;globals.Apply();
                    foreach(var lod in new[]{0,1,2}){foreach(var g in forest.GetComponentsInChildren<LODGroup>())g.ForceLOD(lod);Capture(hero,output+(sample?"/Sample":"")+"/LOD/LOD"+lod+".png");}foreach(var g in forest.GetComponentsInChildren<LODGroup>())g.ForceLOD(-1);
                }
                if(!sample&&!reload)
                {
                    var cam=cameras.First(c=>c.name=="TG_EnvironmentOverview");var on=new List<double>();var off=new List<double>();
                    for(var i=0;i<7;i++){forest.gameObject.SetActive(true);var a=Capture(cam,output+"/CostLastOn.png");forest.gameObject.SetActive(false);var b=Capture(cam,output+"/CostLastOff.png");if(i>0){on.Add(a);off.Add(b);}}forest.gameObject.SetActive(true);
                    File.WriteAllText(output+"/CanopyCost.json",JsonUtility.ToJson(new CostReport{gpu=SystemInfo.graphicsDeviceName,cpu=SystemInfo.processorType,context="Isolated Unity6000.3.4f1 Editor D3D11 Linear URP1600x900,4xMSAA resolved before readback. Six alternating measurements after warmup; each includes two offscreen render submissions and synchronous CPU image readback. Editor diagnostics, not GPU timings, player FPS or PC minimum-spec certification.",canopyOnMs=on.ToArray(),canopyOffMs=off.ToArray(),trees=composition.trees.Length,shrubs=forest.childCount-composition.trees.Length,lod0TrianglesPerKit=manifest.objects.Where(o=>o.lod==0&&o.species!="diagnostic").Sum(o=>o.expected_triangles_after_export),lod1TrianglesPerKit=manifest.objects.Where(o=>o.lod==1).Sum(o=>o.expected_triangles_after_export),lod2TrianglesPerKit=manifest.objects.Where(o=>o.lod==2).Sum(o=>o.expected_triangles_after_export)},true));
                }
                if(!pass)throw new Exception("WP05 tree validation failed; read TreeValidation.json.");
                globals.fixedTime=false;Lighting(false);EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),sample?Trees+"/Scenes/TG_CanopySample_r1.unity":ScenePath);AssetDatabase.SaveAssets();
                if(reload)
                {
                    var own=AssetDatabase.FindAssets("",new[]{Trees,Shared+"/Shaders",Shared+"/Settings"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>!AssetDatabase.IsValidFolder(p)).ToArray();var dependencies=AssetDatabase.GetDependencies(own,true).Concat(own).Where(p=>p.StartsWith("Assets/")&&!AssetDatabase.IsValidFolder(p)).Distinct().ToArray();AssetDatabase.ExportPackage(dependencies,Path.GetFullPath(output+"/../TG_WP05_Trees.unitypackage"),(ExportPackageOptions)0);
                }
                File.WriteAllText(output+"/COMPLETE.txt",sample?"Representative WP05 sample rendered; inspect before assembly.":"WP05 trees imported, assembled and sampled camera/wind validation passed. See handoff for integration and performance limits.");EditorApplication.Exit(0);
            }catch(Exception e){Fail(e);}
        }
    }
}
