using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using FightingAllstar.EnvironmentArt;
using Object=UnityEngine.Object;

namespace FightingAllstar.EditorTools
{
    /// <summary>WP03 owns world assets and this isolated review. Main scene/pipeline are untouched.</summary>
    public static partial class TeyvatWorldLab
    {
        public const string World="Assets/Project/Art/Environment/TeyvatGrassland/World";
        const string Shared="Assets/Project/Art/Environment/TeyvatGrassland/Shared";
        const string Foundation="Assets/Project/Art/Environment/TeyvatGrassland/FoundationR2";
        public const string ReviewScene=World+"/Scenes/TG_WorldReview.unity";
        const string Model=World+"/Models/TG_WorldGeometry.fbx";
        static string output;static Layout layout;static Manifest manifest;static Transform environment;
        static bool pending,reload;static double nextTick;
        static readonly Dictionary<string,Material> mats=new Dictionary<string,Material>();
        static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
        static readonly Dictionary<string,string[]> slots=new Dictionary<string,string[]>();
        [Serializable] sealed class Layout {public Vector3 origin;public Quaternion rotation;public Anchor[] anchors;public Shot[] shots;}
        [Serializable] sealed class Anchor {public string name;public Vector3 worldPosition;public Vector3 localPosition;}
        [Serializable] sealed class Shot {public string name;public Vector3 position;public Quaternion rotation;public float fov;}
        [Serializable] sealed class BoundData {public float[] min,max,size;}
        [Serializable] sealed class SourceObject {public string name,role,collision_for;public int lod,expected_triangles_after_export,welded_boundary_edges,welded_nonmanifold_edges;public BoundData bounds_unity_local_metres;public float[] unity_normal_min,unity_normal_max;public string[] material_slots;}
        [Serializable] sealed class Fall {public float x,front_z,crest_y,half_width;}
        [Serializable] sealed class Manifest {public string schema,fbx_sha256,units;public SourceObject[] objects;public Fall[] waterfalls;}
        [Serializable] sealed class Report
        {
            public bool passed,importPass,cameraPass,groundPass,savedPrefabPass,shadersPass;
            public string context,unity,gpu,pipeline,colorSpace;public int authoredMeshes,lodGroups,collisionMeshes,reviewCameras;
            public long sourceTrianglesIncludingLODAndCollision;public string[] captures,errors;public string limitations;
        }

        public static void RunIsolated(){Start(false);}
        public static void VerifySaved(){Start(true);}
        static void Start(bool saved)
        {
            var marker=Path.Combine(Path.GetDirectoryName(Application.dataPath),"TG_ISOLATED_WORLD_LAB.txt");
            if(!File.Exists(marker)||File.Exists("Assets/Project/Scenes/Battle.unity"))throw new InvalidOperationException("WP03 runs only in the isolated world lab.");
            output=File.ReadAllText(marker).Trim()+(saved?"/Reload":"");Directory.CreateDirectory(output);reload=saved;
            try
            {
                ShaderUtil.allowAsyncCompilation=false;
                if(PlayerSettings.colorSpace!=ColorSpace.Linear)throw new InvalidOperationException("Cold start in Linear color space required.");
                layout=JsonUtility.FromJson<Layout>(File.ReadAllText(Foundation+"/TG_Layout.json"));
                manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(World+"/Models/TG_WorldGeometry.manifest.json"));
                if(saved){EditorSceneManager.OpenScene(ReviewScene,OpenSceneMode.Single);environment=GameObject.Find("TG WP03 World geometry only").transform;}
                else{ConfigurePipeline();PrepareImports();Build();}
                pending=true;nextTick=EditorApplication.timeSinceStartup+5;EditorApplication.update+=Finish;
            }
            catch(Exception e){Fail(e);}
        }
        static void Fail(Exception e){Debug.LogException(e);Directory.CreateDirectory(output);File.WriteAllText(output+"/FAILED.txt",e.ToString());EditorApplication.Exit(1);}
        static void ConfigurePipeline()
        {
            var source=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Shared+"/Settings/TG_MaterialURP.asset");
            if(source==null)throw new InvalidOperationException("Accepted WP02 pipeline asset is missing.");
            GraphicsSettings.defaultRenderPipeline=source;QualitySettings.renderPipeline=source;
            QualitySettings.vSyncCount=0;AssetDatabase.SaveAssets();
        }
        static void PrepareImports()
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(Model);
            importer.globalScale=1;importer.bakeAxisConversion=true;importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.Import;
            importer.isReadable=true;importer.importCameras=false;importer.importLights=false;importer.meshCompression=ModelImporterMeshCompression.Off;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.SaveAndReimport();
            LoadModel();
        }
        static void LoadModel()
        {
            meshes.Clear();slots.Clear();foreach(var m in AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Mesh>())meshes[m.name]=m;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            foreach(var f in prefab.GetComponentsInChildren<MeshFilter>(true))
                slots[f.sharedMesh.name]=f.GetComponent<Renderer>().sharedMaterials.Select(m=>m==null?"Rock":m.name).ToArray();
        }
        static Transform Group(string name,Transform parent){var t=new GameObject(name).transform;if(parent!=null)t.SetParent(parent,false);return t;}
        static Material Material(string name,Color color,string shader="Teyvat/URP/Surface")
        {
            var path=World+"/Materials/TG_World_"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);}else m.shader=Shader.Find(shader);
            m.SetColor("_BaseColor",color);m.enableInstancing=true;EditorUtility.SetDirty(m);mats[name]=m;return m;
        }
        static void Build()
        {
            foreach(var folder in new[]{"Materials","Terrain","Meshes","Scenes","Prefabs","Settings"})Directory.CreateDirectory(World+"/"+folder);
            AssetDatabase.Refresh();mats.Clear();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            environment=Group("TG WP03 World geometry only",null);environment.SetPositionAndRotation(layout.origin,layout.rotation);
            var field=BuildField();MakeMaterials();BuildTerrain(environment);
            var visual=Group("Authored Blender cliff rock and landmark modules",environment);
            var collisions=Group("Simplified static collision - no renderers",environment);
            var pairs=new Dictionary<string,List<Renderer>>();
            foreach(var item in manifest.objects)
            {
                if(item.role=="diagnostic")continue;
                var mesh=meshes[item.name];
                if(item.role=="collision")
                {
                    var node=Group(item.name,collisions);node.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;continue;
                }
                var key=item.name.Replace("_LOD0","").Replace("_LOD1","");
                var parent=visual.Find(key);if(parent==null)parent=Group(key,visual);
                var nodeVisual=Group(item.name,parent);nodeVisual.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=nodeVisual.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterials=slots[item.name].Select(n=>mats.ContainsKey(n)?mats[n]:mats["Rock"]).ToArray();
                if(!pairs.ContainsKey(key))pairs[key]=new List<Renderer>();pairs[key].Add(renderer);
            }
            foreach(var pair in pairs)
            {
                var zero=pair.Value.FirstOrDefault(r=>r.name.EndsWith("_LOD0"));var one=pair.Value.FirstOrDefault(r=>r.name.EndsWith("_LOD1"));
                if(zero==null||one==null)continue;
                var group=zero.transform.parent.gameObject.AddComponent<LODGroup>();
                // Starting screen-relative transitions: inspect forced LOD views and
                // adjust thresholds outward if a silhouette change is visible in combat.
                group.SetLODs(new[]{new LOD(.05f,new[]{zero}),new LOD(.001f,new[]{one})});group.RecalculateBounds();
            }
            PrefabUtility.SaveAsPrefabAsset(environment.gameObject,World+"/Prefabs/TG_WorldGeometry.prefab");
            var owner=Group("Review context - WP02 globals water and WP01 vegetation",null);
            var globals=owner.gameObject.AddComponent<TeyvatMaterialGlobals>();globals.colorField=field;
            globals.colorBounds=new Vector4(layout.origin.x-192,layout.origin.z-128,1f/384,1f/384);globals.fixedTime=true;globals.fixedSeconds=1;globals.windStrength=.34f;globals.Apply();
            BuildWater(owner);BuildVegetationContext(owner);BuildActors();
            foreach(var shot in layout.shots)AddCamera("TG_"+shot.name,shot.position,shot.rotation,shot.fov);
            CameraAt("TG_SculptClose",new Vector3(-62,12,18),new Vector3(-42,14,54),54);
            CameraAt("TG_ShelfDepth",new Vector3(70,48,80),new Vector3(-30,22,65),58);
            Lighting(false);EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ReviewScene);AssetDatabase.SaveAssets();
        }
        static void MakeMaterials()
        {
            var ground=Material("Ground",Color.white);ground.SetFloat("_UseColorField",1);
            Material("Soil",new Color(.69f,.61f,.39f));
            var rock=Material("Rock",new Color(.64f,.72f,.76f));rock.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Shared+"/Textures/TG_RockPainted.png"));rock.SetFloat("_Mapping",1);rock.SetFloat("_UseSharedRockStyle",1);rock.SetFloat("_MossAmount",0);
            var shade=Material("RockShade",new Color(.56f,.64f,.69f));shade.SetTexture("_BaseMap",rock.GetTexture("_BaseMap"));shade.SetFloat("_Mapping",1);shade.SetFloat("_TextureStrength",.35f);shade.SetFloat("_WorldScale",.11f);
            var moss=Material("Moss",Color.white);moss.SetFloat("_UseColorField",1);
            Material("DistantBlue",new Color(.32f,.52f,.68f));Material("Roof",new Color(.29f,.39f,.48f));Material("Window",new Color(.12f,.20f,.25f));
            Material("Player",new Color(.22f,.43f,.72f));Material("Enemy",new Color(.70f,.32f,.23f));
        }
        static Texture2D BuildField()
        {
            var path=World+"/Terrain/TG_WorldColorField.asset";var t=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(t==null){t=new Texture2D(256,256,TextureFormat.RGBA32,true,false);AssetDatabase.CreateAsset(t,path);}
            var colors=new Color[256*256];
            for(var y=0;y<256;y++)for(var x=0;x<256;x++)
            {
                var n=Mathf.PerlinNoise(x*.031f+4,y*.034f+7)*.7f+Mathf.PerlinNoise(x*.12f+13,y*.11f+9)*.3f;
                colors[y*256+x]=Color.Lerp(new Color(.38f,.57f,.18f),new Color(.58f,.72f,.28f),n);
            }
            t.SetPixels(colors);t.wrapMode=TextureWrapMode.Clamp;t.Apply();EditorUtility.SetDirty(t);return t;
        }
        static void BuildTerrain(Transform parent)
        {
            var path=World+"/Terrain/TG_WorldGround.asset";
            var data=AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if(data==null){data=Object.Instantiate(AssetDatabase.LoadAssetAtPath<TerrainData>(Foundation+"/Terrain/TG_ReferenceLand.asset"));AssetDatabase.CreateAsset(data,path);}
            else EditorUtility.CopySerialized(AssetDatabase.LoadAssetAtPath<TerrainData>(Foundation+"/Terrain/TG_ReferenceLand.asset"),data);
            var layers=new TerrainLayer[3];
            for(var i=0;i<3;i++)
            {
                var lp=World+"/Terrain/TG_WorldLayer"+i+".terrainlayer";layers[i]=AssetDatabase.LoadAssetAtPath<TerrainLayer>(lp);
                if(layers[i]==null){layers[i]=Object.Instantiate(AssetDatabase.LoadAssetAtPath<TerrainLayer>(Shared+"/Terrain/TG_Layer"+i+".terrainlayer"));AssetDatabase.CreateAsset(layers[i],lp);}
                layers[i].diffuseRemapMax=Vector4.one;layers[i].diffuseRemapMin=Vector4.zero;EditorUtility.SetDirty(layers[i]);
            }
            data.terrainLayers=layers;EditorUtility.SetDirty(data);
            var terrain=Terrain.CreateTerrainGameObject(data).GetComponent<Terrain>();terrain.name="WP01 r2 native ground - unchanged heights cloned for WP03";
            terrain.transform.SetParent(parent,false);terrain.transform.localPosition=new Vector3(-192,-10,-128);
            terrain.materialTemplate=Material("Terrain",Color.white,"Teyvat/URP/TerrainGrassland");terrain.basemapDistance=100000;terrain.drawInstanced=true;terrain.heightmapPixelError=2;
        }
        static Mesh SaveMesh(Mesh mesh)
        {
            var path=World+"/Meshes/"+mesh.name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            old.Clear();old.indexFormat=mesh.indexFormat;old.vertices=mesh.vertices;old.normals=mesh.normals;old.uv=mesh.uv;old.triangles=mesh.triangles;old.bounds=mesh.bounds;old.UploadMeshData(false);EditorUtility.SetDirty(old);Object.DestroyImmediate(mesh);return old;
        }
        static float RiverCenter(float x){return 35+2.7f*Mathf.Sin(x*.042f)+1.8f*Mathf.Cos(x*.071f);}
        static float RiverWidth(float x){return 14.5f+2.2f*Mathf.Sin(x*.057f+1);}
        static float CliffFront(float x){return RiverCenter(x)+RiverWidth(x)+7-7*Mathf.Exp(-Mathf.Pow((x+67)/25,2))+8*Mathf.Exp(-Mathf.Pow((x+8)/25,2))-4*Mathf.Exp(-Mathf.Pow((x-65)/20,2))+1.1f*Mathf.Sin(x*.084f);}
        static GameObject WaterMesh(string name,Transform parent,Mesh mesh,Material material)
        {
            mesh.RecalculateNormals();mesh.RecalculateBounds();var node=Group(name,parent);node.position=layout.origin;node.gameObject.AddComponent<MeshFilter>().sharedMesh=SaveMesh(mesh);node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;return node.gameObject;
        }
        static void BuildWater(Transform parent)
        {
            var river=AssetDatabase.LoadAssetAtPath<Material>(Shared+"/Materials/TG_River.mat");var fall=AssetDatabase.LoadAssetAtPath<Material>(Shared+"/Materials/TG_Waterfall.mat");
            const int columns=193,rows=13;var v=new Vector3[columns*rows];var uv=new Vector2[v.Length];var tri=new List<int>();
            for(var i=0;i<columns;i++)for(var j=0;j<rows;j++)
            {
                var x=-192+i/(float)(columns-1)*384;var t=j/(float)(rows-1);var k=i*rows+j;v[k]=new Vector3(x,-1.35f,Mathf.Lerp(RiverCenter(x)-RiverWidth(x)-.2f,CliffFront(x)-1.8f,t));uv[k]=new Vector2(x*.055f,t);
                if(i<columns-1&&j<rows-1)tri.AddRange(new[]{k,k+1,k+rows,k+rows,k+1,k+rows+1});
            }
            WaterMesh("Context_WP02 river over actual WP01 basin",parent,new Mesh{name="TG_WorldContextRiver",vertices=v,uv=uv,triangles=tri.ToArray()},river);
            foreach(var f in manifest.waterfalls)
            {
                const int rcount=24,ccount=9;v=new Vector3[(rcount+1)*ccount];uv=new Vector2[v.Length];tri.Clear();
                for(var r=0;r<=rcount;r++)for(var c=0;c<ccount;c++)
                {
                    var t=r/(float)rcount;var s=c/(float)(ccount-1);var k=r*ccount+c;var x=f.x+(s*2-1)*f.half_width*(1+t*.15f)+Mathf.Sin(t*5)*.20f;
                    v[k]=new Vector3(x,Mathf.Lerp(f.crest_y,-1.53f,t),CliffFront(x)-3.4f-2*Mathf.Sin(t*Mathf.PI*.5f));uv[k]=new Vector2(s,t);
                    if(r<rcount&&c<ccount-1)tri.AddRange(new[]{k,k+1,k+ccount,k+1,k+ccount+1,k+ccount});
                }
                WaterMesh("Context_WP02 waterfall "+f.x,parent,new Mesh{name="TG_WorldContextFall_"+f.x,vertices=v,uv=uv,triangles=tri.ToArray()},fall);
            }
        }
        static void BuildVegetationContext(Transform parent)
        {
            // Layout silhouettes stay explicitly outside the reusable WP03 world prefab.
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Foundation+"/TG_FoundationLayout.prefab");
            var node=Object.Instantiate(source);node.name="Context_WP01 tree silhouettes - replace in WP05";node.transform.SetParent(parent,false);node.transform.SetPositionAndRotation(layout.origin,layout.rotation);
            foreach(Transform child in node.transform.Cast<Transform>().ToArray())if(!child.name.StartsWith("04 Reference tree"))Object.DestroyImmediate(child.gameObject);
            var map=new Dictionary<Material,Material>();
            foreach(var renderer in node.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterials=renderer.sharedMaterials.Select(original=>
                {
                    if(!map.ContainsKey(original))
                    {
                        var color=original.HasProperty("_Color")?original.color:new Color(.3f,.5f,.16f);
                        var m=Material("Context_"+original.name,color);map[original]=m;
                    }
                    return map[original];
                }).ToArray();
            }
            foreach(var collider in node.GetComponentsInChildren<Collider>())Object.DestroyImmediate(collider);
        }
        static void BuildActors()
        {
            var root=Group("Review actor proxies - copied anchors",null);
            foreach(var a in layout.anchors)
            {
                var anchor=Group("Proxy_"+a.name,root);anchor.position=a.worldPosition;
                var capsule=GameObject.CreatePrimitive(PrimitiveType.Capsule);capsule.transform.SetParent(anchor,false);capsule.transform.localPosition=Vector3.up*1.5f;capsule.transform.localScale=new Vector3(.85f,1.5f,.85f);
                capsule.GetComponent<Renderer>().sharedMaterial=mats[a.name.StartsWith("Enemy")?"Enemy":"Player"];Object.DestroyImmediate(capsule.GetComponent<Collider>());
            }
        }
        static void AddCamera(string name,Vector3 p,Quaternion q,float fov)
        {
            var c=new GameObject(name).AddComponent<Camera>();c.transform.SetPositionAndRotation(p,q);c.fieldOfView=fov;c.nearClipPlane=.1f;c.farClipPlane=800;c.allowHDR=true;c.allowMSAA=true;c.enabled=name=="TG_ReferencePlanningCandidate";if(c.enabled)c.tag="MainCamera";c.GetUniversalAdditionalCameraData().requiresDepthTexture=true;
        }
        static void CameraAt(string name,Vector3 p,Vector3 focus,float fov){AddCamera(name,layout.origin+p,Quaternion.LookRotation(focus-p),fov);}
        static void Lighting(bool neutral)
        {
            var sun=Object.FindFirstObjectByType<Light>();if(sun==null)sun=new GameObject("WP02 review light baseline").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=neutral?1.1f:1.35f;sun.color=neutral?Color.white:new Color(1,.93f,.79f);sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(neutral?55:38,-43,0);RenderSettings.sun=sun;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=neutral?new Color(.60f,.60f,.60f):new Color(.55f,.69f,.82f);RenderSettings.ambientEquatorColor=neutral?new Color(.4f,.4f,.4f):new Color(.32f,.43f,.47f);RenderSettings.ambientGroundColor=neutral?new Color(.25f,.25f,.25f):new Color(.24f,.29f,.17f);
            RenderSettings.fog=!neutral;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=100;RenderSettings.fogEndDistance=600;RenderSettings.fogColor=new Color(.62f,.78f,.85f);RenderSettings.skybox=AssetDatabase.LoadAssetAtPath<Material>(Shared+"/Settings/TG_MaterialSky.mat");
        }
        static void Finish()
        {
            if(!pending||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<nextTick)return;pending=false;EditorApplication.update-=Finish;
            try
            {
                LoadModel();var imports=ValidateImports();var envelope=ValidateEnvelope();var errors=new List<string>();
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(World+"/Prefabs/TG_WorldGeometry.prefab");
                var prefabPass=source!=null&&source.GetComponentsInChildren<Camera>(true).Length==0&&source.GetComponentsInChildren<Light>(true).Length==0&&source.GetComponentsInChildren<TeyvatMaterialGlobals>(true).Length==0;
                var shaderPass=AssetDatabase.FindAssets("t:Shader",new[]{Shared}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Shader>).All(s=>s.isSupported&&!ShaderUtil.GetShaderMessages(s).Any(m=>m.severity.ToString()=="Error"));
                var groups=Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None);foreach(var group in groups)group.ForceLOD(0);
                var cameras=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).OrderBy(c=>c.name).ToArray();var captures=new List<string>();
                foreach(var neutral in reload?new[]{false}:new[]{true,false})
                {
                    Lighting(neutral);
                    Object.FindFirstObjectByType<TeyvatMaterialGlobals>().Apply();
                    foreach(var camera in cameras.Where(c=>!reload||new[]{"TG_ReferencePlanningCandidate","TG_ReferenceSide","TG_EnemyAttack"}.Contains(c.name)))
                    {var file=(neutral?"Neutral/":"Afternoon/")+camera.name+".png";Capture(camera,output+"/"+file);captures.Add(file);}
                }
                if(!reload)
                {
                    foreach(var group in groups)group.ForceLOD(1);
                    foreach(var camera in cameras.Where(c=>new[]{"TG_ReferencePlanningCandidate","TG_ReferenceSide","TG_EnvironmentOverview"}.Contains(c.name)))Capture(camera,output+"/LOD1/"+camera.name+".png");
                }
                foreach(var group in groups)group.ForceLOD(-1);
                if(!imports)errors.Add("Imported sculpt differs from authored manifest.");if(!envelope.Item1)errors.Add("Camera environment clearance failed.");if(!envelope.Item2)errors.Add("Copied anchor ground height failed.");if(!prefabPass)errors.Add("World prefab is missing or contains review components.");if(!shaderPass)errors.Add("Unsupported or failed shared shader.");
                var report=new Report{passed=errors.Count==0,importPass=imports,cameraPass=envelope.Item1,groundPass=envelope.Item2,savedPrefabPass=prefabPass,shadersPass=shaderPass,context=reload?"Cold saved-scene reload in independent isolated Unity process, no rebuild or pipeline reconfiguration.":"WP03 authored Blender geometry + cloned native WP01 TerrainData; isolated URP Linear editor renders, no gameplay migration.",unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName,pipeline=GraphicsSettings.currentRenderPipeline.name,colorSpace=PlayerSettings.colorSpace.ToString(),authoredMeshes=manifest.objects.Length,lodGroups=groups.Length,collisionMeshes=manifest.objects.Count(o=>o.role=="collision"),reviewCameras=cameras.Length,sourceTrianglesIncludingLODAndCollision=manifest.objects.Sum(o=>(long)o.expected_triangles_after_export),captures=captures.ToArray(),errors=errors.ToArray(),limitations="Static actor proxies, sampled camera paths, two authored LOD levels with starting transitions, simplified collision. Water uses WP02 context; trees use WP01 context. Final foliage/water/lighting and animated combat/player performance remain later packages. No reference-parity claim."};
                File.WriteAllText(output+"/WorldLabReport.json",JsonUtility.ToJson(report,true));
                if(!reload){Object.FindFirstObjectByType<TeyvatMaterialGlobals>().fixedTime=false;Lighting(false);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ReviewScene);AssetDatabase.SaveAssets();}
                if(!report.passed)throw new InvalidOperationException("WP03 world checks failed; inspect reports.");
                if(reload)
                {
                    // Shader include dependencies are not all reported by GetDependencies.
                    // Explicitly retain the shared source/license and pipeline settings.
                    var own=AssetDatabase.FindAssets("",new[]{World,Shared+"/Shaders",Shared+"/Settings"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>!AssetDatabase.IsValidFolder(p)).ToArray();
                    var paths=AssetDatabase.GetDependencies(own,true).Concat(own).Where(p=>p.StartsWith("Assets/")&&!AssetDatabase.IsValidFolder(p)).Distinct().ToArray();
                    AssetDatabase.ExportPackage(paths,Path.GetFullPath(Path.Combine(output,"..","TG_WP03_WorldGeometry.unitypackage")),(ExportPackageOptions)0);
                }
                File.WriteAllText(output+"/COMPLETE.txt","WP03 world review checks passed. Scope and visual limitations are recorded separately.\n");EditorApplication.Exit(0);
            }
            catch(Exception e){Fail(e);}
        }
        static void Capture(Camera camera,string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));var rt=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var old=RenderTexture.active;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try{camera.aspect=16f/9f;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{RenderTexture.active=old;Object.DestroyImmediate(image);RenderTexture.ReleaseTemporary(rt);}
        }
    }
}
