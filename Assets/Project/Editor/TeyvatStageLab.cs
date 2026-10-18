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
    public static partial class TeyvatStageLab
    {
        const string Root="Assets/Project/Art/Environment/TeyvatGrassland";
        const string Stage=Root+"/Integration",Water=Root+"/Water",R3=Root+"/LayoutR3",Shared=Root+"/Shared";
        const string ReviewScene="Assets/Project/Scenes/EnvironmentReview/TeyvatGrassland/TG_StageReview_r1.unity";
        const string StagePrefab=Root+"/Prefabs/TG_BattleStage_r1.prefab";
        static string output;static Transform environment,waterRoot;static Vector3 origin;static bool pending,reload,sample;static double ready;
        static Layout layout;static Plan plan;static TeyvatMaterialGlobals globals;
        static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        [Serializable] sealed class Anchor{public string name;public Vector3 worldPosition,localPosition;}
        [Serializable] sealed class Shot{public string name;public Vector3 position;public Quaternion rotation;public float fov;}
        [Serializable] sealed class Layout{public Vector3 origin;public Anchor[] anchors;public Shot[] shots;}
        [Serializable] sealed class Fall{public string name;public float half_width;public float[] path_flat;}
        [Serializable] sealed class Plan{public float water_level;public Fall[] waterfalls;}
        [Serializable] sealed class Contact{public string name,replaces;public string[] slots;}
        [Serializable] sealed class ContactPlan{public Contact[] objects;}
        public static void RunIsolated(){Start(false,false);}
        public static void SampleOnly(){Start(false,true);}
        public static void VerifySaved(){Start(true,false);}
        public static void ExportVerifiedPackage()
        {
            var marker=Path.Combine(Path.GetDirectoryName(Application.dataPath),"TG_ISOLATED_STAGE.txt");
            if(!File.Exists(marker)||File.Exists("Assets/Project/Scenes/Battle.unity"))throw new InvalidOperationException("WP07 export requires isolated art host.");
            output=File.ReadAllText(marker).Trim();Directory.CreateDirectory(output);
            try{EditorSceneManager.OpenScene(ReviewScene);ExportStagePackage();File.WriteAllText(output+"/COMPLETE.txt","Verified saved stage exported with explicit shader-include/settings/license dependencies.");EditorApplication.Exit(0);}catch(Exception e){Fail(e);}
        }
        static void ExportStagePackage()
        {
            var explicitRoots=AssetDatabase.FindAssets("",new[]{Stage,Shared+"/Shaders",Shared+"/Settings",Water+"/Shaders"}).Select(AssetDatabase.GUIDToAssetPath)
                .Concat(AssetDatabase.FindAssets("",new[]{Root}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".hlsl")||p.EndsWith("/LICENSE.md")));
            var inputs=new[]{StagePrefab,ReviewScene,Water+"/Scenes/TG_WaterReview_r1.unity"}.Concat(explicitRoots).Where(p=>!AssetDatabase.IsValidFolder(p)).Distinct().ToArray();
            var paths=AssetDatabase.GetDependencies(inputs,true).Concat(inputs).Where(p=>p.StartsWith("Assets/")&&!AssetDatabase.IsValidFolder(p)).Distinct().ToArray();
            AssetDatabase.ExportPackage(paths,Path.GetFullPath(output+"/../TG_WP07_IntegratedStage.unitypackage"),(ExportPackageOptions)0);
        }
        static void Start(bool saved,bool justSample)
        {
            var marker=Path.Combine(Path.GetDirectoryName(Application.dataPath),"TG_ISOLATED_STAGE.txt");
            if(!File.Exists(marker)||File.Exists("Assets/Project/Scenes/Battle.unity"))throw new InvalidOperationException("WP07 requires the isolated art host.");
            output=File.ReadAllText(marker).Trim();Directory.CreateDirectory(output);reload=saved;sample=justSample;
            try
            {
                ShaderUtil.allowAsyncCompilation=false;if(PlayerSettings.colorSpace!=ColorSpace.Linear)throw new Exception("Linear URP host required.");
                layout=JsonUtility.FromJson<Layout>(File.ReadAllText(R3+"/Data/TG_Layout.json"));origin=layout.origin;
                plan=JsonUtility.FromJson<Plan>(File.ReadAllText(Water+"/Data/TG_WaterPlan.json"));
                foreach(var dir in new[]{"Materials","Meshes","Terrain","Data"})Directory.CreateDirectory(Stage+"/"+dir);
                AssetDatabase.Refresh();
                if(saved)
                {
                    EditorSceneManager.OpenScene(ReviewScene);environment=GameObject.Find("TG Battle Stage r1").transform;waterRoot=environment.Find("TG WP07 water and cascade effects");globals=environment.GetComponentInChildren<TeyvatMaterialGlobals>();
                    foreach(var role in new[]{"River","Waterfall","Pool","Foam","Wake","Mist","Shore","Spray"})materials[role]=AssetDatabase.LoadAssetAtPath<Material>(Stage+"/Materials/TG_Stage_"+role+".mat");
                }
                else Build();
                pending=true;ready=EditorApplication.timeSinceStartup+3;EditorApplication.update+=Finish;
            }catch(Exception e){Fail(e);}
        }
        static void Fail(Exception e){Debug.LogException(e);File.WriteAllText(output+"/FAILED.txt",e.ToString());EditorApplication.Exit(1);}
        static Transform Group(string name,Transform parent=null){var t=new GameObject(name).transform;if(parent)t.SetParent(parent,false);return t;}
        static void Build()
        {
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Shared+"/Settings/TG_MaterialURP.asset");GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;QualitySettings.vSyncCount=0;
            EditorSceneManager.OpenScene(Water+"/Scenes/TG_WaterReview_r1.unity");EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ReviewScene);
            environment=GameObject.Find("TG WP01 R3 redesigned landscape").transform;environment.name="TG Battle Stage r1";
            waterRoot=environment.Find("TG WP06 water and cascades");waterRoot.name="TG WP07 water and cascade effects";
            globals=Object.FindFirstObjectByType<TeyvatMaterialGlobals>();globals.transform.SetParent(environment,true);
            var meadow=environment.GetComponentInChildren<TeyvatMeadowRenderer>();
            BuildMaterials();PatchCliffContacts();PatchNativeWaterBeds();
            foreach(var r in waterRoot.GetComponentsInChildren<MeshRenderer>())
            {
                string role=r.sharedMaterial.name.Replace("TG_Water_","");
                if(role=="Foam"&&(r.name.Contains("RippleFan")||r.name.Contains("IslandWake")))role="Wake";
                r.sharedMaterial=materials[role];
                if(r.name.Contains("Cascade_"))
                {
                    var f=plan.waterfalls.First(x=>r.name.EndsWith("_"+x.name));var length=0f;for(var j=3;j<f.path_flat.Length;j+=3)length+=Vector3.Distance(P(f,j-3),P(f,j));
                    var path=Stage+"/Materials/TG_Stage_Fall_"+f.name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(materials["Waterfall"]);AssetDatabase.CreateAsset(m,path);}else EditorUtility.CopySerialized(materials["Waterfall"],m);
                    m.SetFloat("_FlowLength",length);m.SetFloat("_FlowWidth",f.half_width*2);r.sharedMaterial=m;EditorUtility.SetDirty(m);
                }
            }
            BuildSpray();BuildFoamFringes();BuildStageMeadow(meadow);Dress();
            var binding=environment.gameObject.AddComponent<TeyvatStageBinding>();binding.authoredWorldOrigin=origin;binding.meadow=meadow;binding.canonicalMeadowData=meadow.data;binding.globals=globals;binding.authoredColorBounds=globals.colorBounds;binding.ApplyBinding();
            foreach(var f in plan.waterfalls)
            {
                var q=P(f,6);CameraAt("TG_StageFall_"+f.name,new Vector3(q.x-9,q.y+6,q.z-17),q+new Vector3(0,4,3),54);
                var toe=P(f,f.path_flat.Length-3);CameraAt("TG_StageImpact_"+f.name,toe+new Vector3(-8,3,-10),toe+new Vector3(0,1,0),54);
            }
            CameraAt("TG_StageFallSide",new Vector3(6,12,65),new Vector3(-12,10,72),59);
            // Review playback contains cameras and proxies; the final module does not.
            var tour=Group("TG Review camera tour").gameObject.AddComponent<TeyvatStageTour>();tour.GetComponent<Camera>().enabled=false;tour.shots=new[]{"PlayerPlanning","EnemyPlanning","PlayerPlanning","PlayerAttack","PlayerContact","PlayerPlanning","EnemyPlanning","EnemyAttack","EnemyContact","EnemyPlanning"}.Select(n=>layout.shots.First(s=>s.name==n)).Select(s=>new TeyvatStageTour.Shot{position=s.position,rotation=s.rotation,fieldOfView=s.fov,name=s.name}).ToArray();tour.arenaOrigin=origin;
            tour.GetComponent<Camera>().GetUniversalAdditionalCameraData().requiresDepthTexture=true;
            globals.fixedTime=false;Lighting(false);SaveStage();
        }
        static Vector3 P(Fall f,int i){return new Vector3(f.path_flat[i],f.path_flat[i+1],f.path_flat[i+2]);}
        static void SaveStage()
        {
            var binding=environment.GetComponent<TeyvatStageBinding>();
            // Serialize the durable canonical reference, never the DontSave copy.
            // Keep the behaviour enabled so reload/instantiation recreates it.
            binding.meadow.data=binding.canonicalMeadowData;
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ReviewScene);AssetDatabase.SaveAssets();
            PrefabUtility.SaveAsPrefabAsset(environment.gameObject,StagePrefab);binding.ApplyBinding();
        }
        static Material CopyMat(string role,string source,string shader)
        {
            var path=Stage+"/Materials/TG_Stage_"+role+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);var src=AssetDatabase.LoadAssetAtPath<Material>(source);
            if(m==null){m=src?new Material(src):new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);}else if(src)EditorUtility.CopySerialized(src,m);
            m.name="TG_Stage_"+role;m.shader=Shader.Find(shader);m.enableInstancing=true;EditorUtility.SetDirty(m);materials[role]=m;return m;
        }
        static void BuildMaterials()
        {
            foreach(var role in new[]{"River","Waterfall","Pool"})CopyMat(role,Water+"/Materials/TG_Water_"+role+".mat","Teyvat/URP/IntegratedWater");
            var fall=materials["Waterfall"];fall.SetColor("_ShallowColor",new Color(.035f,.52f,.56f,.96f));fall.SetColor("_DeepColor",new Color(.025f,.26f,.35f,.98f));fall.SetFloat("_Opacity",1);fall.SetFloat("_FoamStrength",.92f);fall.SetFloat("_FlowSpeed",1.9f);
            foreach(var role in new[]{"Foam","Wake","Mist"})
            {
                var m=CopyMat(role,Water+"/Materials/TG_Water_"+(role=="Wake"?"Foam":role)+".mat","Teyvat/URP/ImpactFoam");m.SetFloat("_EffectMode",role=="Foam"?0:role=="Wake"?1:2);m.SetFloat("_Opacity",role=="Mist"?.48f:.94f);m.SetFloat("_DepthFade",role=="Mist"?.45f:.025f);m.SetFloat("_FlowSpeed",role=="Mist"?.4f:.8f);m.renderQueue=role=="Mist"?3010:3005;
            }
            CopyMat("Shore",Water+"/Materials/TG_Water_Shore.mat","Teyvat/URP/WaterEffects");
            var spray=CopyMat("Spray",null,"Teyvat/URP/ImpactSpray");spray.SetColor("_BaseColor",new Color(.84f,.97f,1,1));spray.SetFloat("_Opacity",.9f);spray.SetFloat("_DepthFade",.18f);spray.SetFloat("_UseSceneDepth",1);spray.renderQueue=3015;
            var fringe=CopyMat("Fringe",Water+"/Materials/TG_Water_Foam.mat","Teyvat/URP/ImpactFoam");fringe.SetFloat("_EffectMode",3);fringe.SetFloat("_Opacity",.96f);fringe.SetFloat("_UseSceneDepth",1);fringe.SetFloat("_DepthFade",.035f);fringe.SetFloat("_FlowSpeed",1.2f);fringe.renderQueue=3007;
        }
        static void PatchCliffContacts()
        {
            var model=Stage+"/Models/TG_StageContacts.fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(model);importer.globalScale=1;importer.bakeAxisConversion=true;importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.Import;importer.isReadable=true;importer.meshCompression=ModelImporterMeshCompression.Off;importer.SaveAndReimport();
            var all=AssetDatabase.LoadAllAssetsAtPath(model).OfType<Mesh>().ToDictionary(m=>m.name);var src=AssetDatabase.LoadAssetAtPath<GameObject>(model).GetComponentsInChildren<MeshFilter>().ToDictionary(f=>f.sharedMesh.name,f=>f.GetComponent<Renderer>().sharedMaterials.Select(m=>m.name).ToArray());
            var contacts=JsonUtility.FromJson<ContactPlan>(File.ReadAllText(Stage+"/Models/TG_StageContacts.manifest.json"));
            foreach(var item in contacts.objects)
            {
                var f=environment.GetComponentsInChildren<MeshFilter>(true).Single(x=>x.sharedMesh&&x.sharedMesh.name==item.replaces);var r=f.GetComponent<Renderer>();
                var byName=r.sharedMaterials.Where(m=>m).ToDictionary(m=>m.name.Replace("TG_R3_",""));f.sharedMesh=all[item.name];r.sharedMaterials=src[item.name].Select(s=>byName.ContainsKey(s)?byName[s]:AssetDatabase.LoadAssetAtPath<Material>(R3+"/Materials/TG_R3_"+s+".mat")).ToArray();
            }
            foreach(var lod in environment.GetComponentsInChildren<LODGroup>())lod.RecalculateBounds();
        }
        static void PatchNativeWaterBeds()
        {
            var terrain=environment.GetComponentInChildren<Terrain>();var path=Stage+"/Terrain/TG_StageLandscape.asset";var data=AssetDatabase.LoadAssetAtPath<TerrainData>(path);var src=AssetDatabase.LoadAssetAtPath<TerrainData>(R3+"/Terrain/TG_Landscape.asset");
            if(data==null){data=new TerrainData();AssetDatabase.CreateAsset(data,path);}
            // Allocate independent native height/control buffers. Never alias a
            // lane's TerrainData through Instantiate/CopySerialized.
            data.heightmapResolution=src.heightmapResolution;data.size=src.size;data.alphamapResolution=src.alphamapResolution;data.baseMapResolution=src.baseMapResolution;data.terrainLayers=src.terrainLayers;
            var n=src.heightmapResolution;var h=src.GetHeights(0,0,n,n);data.SetAlphamaps(0,0,src.GetAlphamaps(0,0,src.alphamapWidth,src.alphamapHeight));var local=terrain.transform.position-origin;int changed=0;
            for(var iz=0;iz<n;iz++)for(var ix=0;ix<n;ix++)
            {
                var x=local.x+ix*data.size.x/(n-1);var z=local.z+iz*data.size.z/(n-1);var original=local.y+h[iz,ix]*data.size.y;var y=original;
                foreach(var f in plan.waterfalls)for(var j=0;j<f.path_flat.Length-3;j+=3)
                {
                    var a=P(f,j);var b=P(f,j+3);if(z<a.z&&z>b.z){var t=(a.z-z)/(a.z-b.z);var cx=Mathf.Lerp(a.x,b.x,t);var half=f.half_width*1.16f+.45f;if(Mathf.Abs(x-cx)<half)y=Mathf.Min(y,Mathf.Lerp(a.y,b.y,t)-.36f);}
                }
                if(y<original){h[iz,ix]=(y-local.y)/data.size.y;changed++;}
            }
            data.SetHeights(0,0,h);terrain.terrainData=data;terrain.GetComponent<TerrainCollider>().terrainData=data;EditorUtility.SetDirty(data);File.WriteAllText(Stage+"/Data/TG_WaterBedCorrection.json","{\"changedHeightSamples\":"+changed+",\"clearanceMetres\":0.36,\"source\":\"LayoutR3 TerrainData unchanged\"}");
        }
        static Mesh SaveMesh(Mesh mesh,string name)
        {
            mesh.name=name;var path=Stage+"/Meshes/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            // Copy explicit native buffers; CopySerialized alone leaves stale
            // native mesh geometry after some Unity reimport paths.
            old.Clear();old.vertices=mesh.vertices;old.uv=mesh.uv;var uv1=new List<Vector4>();mesh.GetUVs(1,uv1);old.SetUVs(1,uv1);var uv2=new List<Vector2>();mesh.GetUVs(2,uv2);old.SetUVs(2,uv2);old.colors=mesh.colors;old.triangles=mesh.triangles;old.bounds=mesh.bounds;old.UploadMeshData(false);EditorUtility.SetDirty(old);Object.DestroyImmediate(mesh);return old;
        }
        static void BuildSpray()
        {
            var rng=new System.Random(707731);foreach(var fall in plan.waterfalls)for(var landing=0;landing<2;landing++)
            {
                var q=P(fall,landing==0?6:fall.path_flat.Length-3);if(landing==1)q.y=plan.water_level+.12f;
                var count=fall.name=="Hero"?200:130;var vertices=new List<Vector3>();var uv=new List<Vector2>();var velocity=new List<Vector4>();var phase=new List<Vector2>();var colors=new List<Color>();var triangles=new List<int>();
                for(var i=0;i<count;i++)
                {
                    var emitter=q+new Vector3(((float)rng.NextDouble()*2-1)*fall.half_width*.94f,.12f,-.25f-(float)rng.NextDouble()*.7f);var v=new Vector3(((float)rng.NextDouble()*2-1)*1.5f,1.8f+(float)rng.NextDouble()*3.2f,-.9f-(float)rng.NextDouble()*2.5f);var life=.55f+(float)rng.NextDouble()*.55f;var seed=(float)rng.NextDouble();var size=.045f+(float)rng.NextDouble()*.065f;var k=vertices.Count;
                    foreach(var corner in new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)}){vertices.Add(emitter);uv.Add(corner);velocity.Add(new Vector4(v.x,v.y,v.z,life));phase.Add(new Vector2(seed,size));colors.Add(Color.white);}triangles.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});
                }
                var m=new Mesh();m.SetVertices(vertices);m.SetUVs(0,uv);m.SetUVs(1,velocity);m.SetUVs(2,phase);m.SetColors(colors);m.SetTriangles(triangles,0);m.bounds=new Bounds(q+new Vector3(0,-1,-2),new Vector3(fall.half_width*2+5,10,11));m=SaveMesh(m,"SM_StageSpray_"+fall.name+"_"+landing);
                var g=Group("Impact spray "+fall.name+" "+landing,waterRoot);g.gameObject.AddComponent<MeshFilter>().sharedMesh=m;var r=g.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=materials["Spray"];r.shadowCastingMode=ShadowCastingMode.Off;
            }
        }
        static void BuildFoamFringes()
        {
            foreach(var fall in plan.waterfalls)for(var landing=0;landing<2;landing++)
            {
                var q=P(fall,landing==0?6:fall.path_flat.Length-3);if(landing==1)q.y=plan.water_level+.12f;var v=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color>();var triangles=new List<int>();
                for(var row=0;row<=4;row++)for(var col=0;col<=40;col++)
                {
                    var u=col/40f;var t=row/4f;var height=.48f+.23f*Mathf.Sin(u*31+.9f)+.11f*Mathf.Sin(u*67);v.Add(q+new Vector3((u*2-1)*(fall.half_width+.25f),-.06f+t*height,-.18f-t*.28f));uv.Add(new Vector2(u,t));colors.Add(new Color(.3f,0,0,1));if(row<4&&col<40){var k=row*41+col;triangles.AddRange(new[]{k,k+1,k+42,k,k+42,k+41});}
                }
                var m=new Mesh();m.SetVertices(v);m.SetUVs(0,uv);m.SetColors(colors);m.SetTriangles(triangles,0);m.RecalculateBounds();m=SaveMesh(m,"SM_StageFringe_"+fall.name+"_"+landing);var g=Group("Churning foam fringe "+fall.name+" "+landing,waterRoot);g.gameObject.AddComponent<MeshFilter>().sharedMesh=m;var r=g.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=materials["Fringe"];r.shadowCastingMode=ShadowCastingMode.Off;
            }
        }
        static void BuildStageMeadow(TeyvatMeadowRenderer meadow)
        {
            var src=AssetDatabase.LoadAssetAtPath<TeyvatMeadowData>(R3+"/Meadow/TG_MeadowPlacement.asset");var path=Stage+"/Data/TG_StageMeadowPlacement.asset";var data=AssetDatabase.LoadAssetAtPath<TeyvatMeadowData>(path);if(data==null){data=ScriptableObject.CreateInstance<TeyvatMeadowData>();AssetDatabase.CreateAsset(data,path);}data.seed=src.seed;data.revision="WP07 channel exclusions from R3";data.species=src.species;data.worldBounds=src.worldBounds;
            int removed=0;data.cells=src.cells.Select(c=>new TeyvatMeadowData.Cell{name=c.name,worldBounds=c.worldBounds,placements=c.placements.Where(p=>
            {
                var x=p.matrix.m03-origin.x;var z=p.matrix.m23-origin.z;foreach(var f in plan.waterfalls)for(var j=0;j<f.path_flat.Length-3;j+=3){var a=P(f,j);var b=P(f,j+3);if(z<a.z+.4f&&z>b.z-.4f){var t=Mathf.Clamp01((a.z-z)/(a.z-b.z));if(Mathf.Abs(x-Mathf.Lerp(a.x,b.x,t))<f.half_width*1.16f+1.1f){removed++;return false;}}}return true;
            }).ToArray()}).ToArray();data.placementCount=data.cells.Sum(c=>c.placements.Length);EditorUtility.SetDirty(data);meadow.data=data;
            File.WriteAllText(Stage+"/Data/TG_MeadowChannelExclusion.json","{\"sourcePlacements\":"+src.placementCount+",\"removedFromWatercourses\":"+removed+",\"stagePlacements\":"+data.placementCount+",\"battlefieldPlacementPolicy\":\"R3 retained, including actor positions\"}");
        }
        static void Dress()
        {
            var terrain=environment.GetComponentInChildren<Terrain>();var root=Group("WP07 seeded flower accents",environment);var data=environment.GetComponentInChildren<TeyvatMeadowRenderer>().data;var flowers=data.species.Where(s=>s.name.ToLowerInvariant().Contains("flower")).ToArray();if(flowers.Length==0)return;
            var rng=new System.Random(707512);var records=new List<string>();
            foreach(var center in new[]{new Vector2(-25,-10),new Vector2(-30,7),new Vector2(-29,18),new Vector2(30,10),new Vector2(32,19),new Vector2(-29,-26),new Vector2(27,-27),new Vector2(-10,-33),new Vector2(19,-34)})for(var j=0;j<9;j++)
            {
                var s=flowers[rng.Next(flowers.Length)];var x=center.x+((float)rng.NextDouble()*2-1)*3;var z=center.y+((float)rng.NextDouble()*2-1)*2;var pos=origin+new Vector3(x,0,z);pos.y=terrain.SampleHeight(pos)+terrain.transform.position.y;
                var g=Group("Flower accent "+root.childCount,root);g.position=pos;g.localRotation=Quaternion.Euler(0,(float)rng.NextDouble()*360,0);g.localScale=Vector3.one*(.65f+(float)rng.NextDouble()*.3f);g.gameObject.AddComponent<MeshFilter>().sharedMesh=s.meshLOD0;g.gameObject.AddComponent<MeshRenderer>().sharedMaterials=s.materialsLOD0;
                records.Add(JsonUtility.ToJson(new Dressing{name=g.name,species=s.name,position=g.localPosition,rotation=g.localRotation,scale=g.localScale}));
            }
            File.WriteAllText(Stage+"/Data/TG_DressingPlan.json","{\"seed\":707512,\"placements\":["+string.Join(",",records)+"]}");
        }
        [Serializable] sealed class Dressing{public string name,species;public Vector3 position,scale;public Quaternion rotation;}
        static void CameraAt(string name,Vector3 p,Vector3 target,float fov)
        {
            var c=Group(name).gameObject.AddComponent<Camera>();c.transform.SetPositionAndRotation(origin+p,Quaternion.LookRotation(target-p));c.fieldOfView=fov;c.nearClipPlane=.1f;c.farClipPlane=800;c.enabled=false;c.allowHDR=true;c.allowMSAA=true;var d=c.GetUniversalAdditionalCameraData();d.requiresDepthTexture=true;d.requiresColorTexture=false;
        }
        static void Lighting(bool neutral)
        {
            var sun=Object.FindFirstObjectByType<Light>();sun.type=LightType.Directional;sun.intensity=neutral?1.1f:1.35f;sun.color=neutral?Color.white:new Color(1,.93f,.79f);sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(neutral?55:38,-43,0);RenderSettings.sun=sun;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.55f,.69f,.82f);RenderSettings.ambientEquatorColor=new Color(.32f,.43f,.47f);RenderSettings.ambientGroundColor=new Color(.24f,.29f,.17f);RenderSettings.fog=!neutral;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=80;RenderSettings.fogEndDistance=550;RenderSettings.fogColor=new Color(.62f,.78f,.85f);RenderSettings.skybox=AssetDatabase.LoadAssetAtPath<Material>(Shared+"/Settings/TG_MaterialSky.mat");
        }
        static void Capture(Camera camera,string path,int width=1600,int height=900)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,4);var resolved=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var previous=RenderTexture.active;var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            try{camera.aspect=(float)width/height;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});Graphics.Blit(rt,resolved);RenderTexture.active=resolved;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{RenderTexture.active=previous;Object.DestroyImmediate(image);RenderTexture.ReleaseTemporary(rt);RenderTexture.ReleaseTemporary(resolved);}
        }
        static void Finish()
        {
            if(!pending||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<ready)return;pending=false;EditorApplication.update-=Finish;
            try
            {
                globals.fixedTime=true;globals.fixedSeconds=1;globals.Apply();var pass=ValidateStage();pass=ValidateWaterDepth()&&pass;pass=ValidatePrefabStandalone()&&pass;
                var cameras=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c=>c.name!="TG Review camera tour").ToArray();
                foreach(var neutral in reload||sample?new[]{false}:new[]{true,false}){Lighting(neutral);foreach(var c in cameras.Where(c=>!sample||c.name=="TG_WaterSample"||c.name=="TG_StageImpact_Hero"||c.name=="TG_EnvironmentOverview"))Capture(c,output+(neutral?"/Neutral/":"/Afternoon/")+c.name+".png");}
                if(!reload)
                {
                    Lighting(false);var c=cameras.First(x=>x.name=="TG_WaterSample");for(var i=0;i<(sample?6:60);i++){globals.fixedSeconds=i/15f;globals.Apply();Capture(c,output+"/Flow/Frame"+i.ToString("D3")+".png",960,540);}
                    globals.fixedSeconds=1;globals.Apply();if(!sample)CaptureTransitions();
                }
                if(!pass)throw new Exception("WP07 stage checks failed; read StageValidation and contact/depth reports.");
                globals.fixedTime=false;globals.Apply();Lighting(false);SaveStage();
                if(reload)
                {
                    ExportStagePackage();
                }
                File.WriteAllText(output+"/COMPLETE.txt","WP07 integrated stage, waterfall contact and deterministic motion review completed; see reports and WP07 handoff for actual-combat and final-lighting limits.");EditorApplication.Exit(0);
            }catch(Exception e){Fail(e);}
        }
    }
}
