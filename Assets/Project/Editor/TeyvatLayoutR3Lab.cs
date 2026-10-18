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
    public static partial class TeyvatLayoutR3Lab
    {
        const string R3="Assets/Project/Art/Environment/TeyvatGrassland/LayoutR3";
        const string World=R3;
        const string OldWorld="Assets/Project/Art/Environment/TeyvatGrassland/World";
        const string Shared="Assets/Project/Art/Environment/TeyvatGrassland/Shared";
        const string Meadow="Assets/Project/Art/Environment/TeyvatGrassland/Meadow";
        const string Foundation="Assets/Project/Art/Environment/TeyvatGrassland/FoundationR2";
        const string ReviewScene=R3+"/Scenes/TG_LayoutReview_r3.unity";
        const string Model=R3+"/Models/TG_LayoutGeometry.fbx";
        static string output;static Layout layout;static Manifest manifest;static Plan plan;static HeightData heights;
        static Transform environment;static Vector3 origin;static TeyvatMaterialGlobals globals;static TeyvatMeadowRenderer meadow;
        static bool pending,reload;static double nextTick;
        static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
        static readonly Dictionary<string,string[]> slots=new Dictionary<string,string[]>();
        static readonly Dictionary<string,Material> mats=new Dictionary<string,Material>();
        [Serializable] sealed class Layout {public Vector3 origin;public Quaternion rotation;public Anchor[] anchors;public Shot[] shots;}
        [Serializable] sealed class Anchor {public string name;public Vector3 worldPosition,localPosition;}
        [Serializable] sealed class Shot {public string name;public Vector3 position;public Quaternion rotation;public float fov;}
        [Serializable] sealed class BoundData {public float[] min,max,size;}
        [Serializable] sealed class SourceObject {public string name,role,collision_for;public int lod,expected_triangles_after_export,welded_boundary_edges,welded_nonmanifold_edges;public BoundData bounds_unity_local_metres;public float[] unity_normal_min,unity_normal_max;public string[] material_slots;}
        [Serializable] sealed class Manifest {public string schema,fbx_sha256;public SourceObject[] objects;}
        [Serializable] sealed class Island {public string name;public float x,z,width,depth,top,rotation;}
        [Serializable] sealed class Fall {public string name;public float x,half_width,front_z,crest_y;public float[] path_flat;}
        [Serializable] sealed class Plan {public string revision;public float water_level;public Island[] islands;public Fall[] waterfalls;}
        [Serializable] sealed class HeightData {public int resolution;public float[] position,size,heights_local_metres;}
        [Serializable] sealed class TreeAnchor {public string name,role;public Vector3 localPosition;public float heightStartingValue;}
        [Serializable] sealed class TreeComposition {public int seed=731;public string method="Designed foreground/island anchors plus seeded mixed groves on terraces and reverse hills. Layout placeholders; final tree species/canopy asset WP05.";public TreeAnchor[] trees;}
        public static void RunIsolated(){Start(false);}
        public static void VerifySaved(){Start(true);}
        static void Start(bool saved)
        {
            var marker=Path.Combine(Path.GetDirectoryName(Application.dataPath),"TG_ISOLATED_LAYOUT_R3.txt");
            if(!File.Exists(marker)||File.Exists("Assets/Project/Scenes/Battle.unity"))throw new InvalidOperationException("R3 rebuild requires isolated art host.");
            output=File.ReadAllText(marker).Trim()+(saved?"/Reload":"");Directory.CreateDirectory(output);reload=saved;
            try
            {
                ShaderUtil.allowAsyncCompilation=false;if(PlayerSettings.colorSpace!=ColorSpace.Linear)throw new Exception("Cold start in Linear required.");
                layout=JsonUtility.FromJson<Layout>(File.ReadAllText(Foundation+"/TG_Layout.json"));origin=layout.origin;
                plan=JsonUtility.FromJson<Plan>(File.ReadAllText(R3+"/Data/TG_LayoutPlan.json"));heights=JsonUtility.FromJson<HeightData>(File.ReadAllText(R3+"/Data/TG_LayoutTerrainSource.json"));
                manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(R3+"/Models/TG_LayoutGeometry.manifest.json"));
                if(saved){EditorSceneManager.OpenScene(ReviewScene);environment=GameObject.Find("TG WP01 R3 redesigned landscape").transform;globals=Object.FindFirstObjectByType<TeyvatMaterialGlobals>();meadow=Object.FindFirstObjectByType<TeyvatMeadowRenderer>();LoadModel();}
                else
                {
                    var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Shared+"/Settings/TG_MaterialURP.asset");GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;QualitySettings.vSyncCount=0;
                    foreach(var f in new[]{"Materials","Terrain","Meshes","Scenes","Prefabs","Meadow"})Directory.CreateDirectory(R3+"/"+f);
                    AssetDatabase.Refresh();var importer=(ModelImporter)AssetImporter.GetAtPath(Model);importer.globalScale=1;importer.bakeAxisConversion=true;importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.Import;importer.isReadable=true;importer.importCameras=false;importer.importLights=false;importer.meshCompression=ModelImporterMeshCompression.Off;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.SaveAndReimport();LoadModel();Build();
                }
                pending=true;nextTick=EditorApplication.timeSinceStartup+3;EditorApplication.update+=Finish;
            }catch(Exception e){Fail(e);}
        }
        static void Fail(Exception e){Debug.LogException(e);File.WriteAllText(output+"/FAILED.txt",e.ToString());EditorApplication.Exit(1);}
        static void LoadModel()
        {
            meshes.Clear();slots.Clear();foreach(var m in AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Mesh>())meshes[m.name]=m;
            foreach(var f in AssetDatabase.LoadAssetAtPath<GameObject>(Model).GetComponentsInChildren<MeshFilter>())slots[f.sharedMesh.name]=f.GetComponent<Renderer>().sharedMaterials.Select(m=>m.name).ToArray();
        }
        static Transform Group(string name,Transform parent=null){var t=new GameObject(name).transform;if(parent!=null)t.SetParent(parent,false);return t;}
        static Material Mat(string key,string old=null)
        {
            var path=R3+"/Materials/TG_R3_"+key+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);var source=AssetDatabase.LoadAssetAtPath<Material>(old??(OldWorld+"/Materials/TG_World_"+key+".mat"));
            if(mat==null){mat=source!=null?new Material(source):new Material(Shader.Find("Teyvat/URP/Surface"));AssetDatabase.CreateAsset(mat,path);}else if(source!=null)EditorUtility.CopySerialized(source,mat);
            mat.name="TG_R3_"+key;mat.enableInstancing=true;EditorUtility.SetDirty(mat);mats[key]=mat;return mat;
        }
        static void Visual(string name,Mesh mesh,Material[] materials,Transform parent,Vector3 local)
        {
            var t=Group(name,parent);t.localPosition=local;t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterials=materials;
        }
        static void Build()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);environment=Group("TG WP01 R3 redesigned landscape");environment.position=origin;
            foreach(var k in new[]{"Ground","Soil","Rock","RockShade","Moss","DistantBlue","Roof","Window","Player","Enemy","Terrain"})Mat(k);
            BuildTerrain();var field=AssetDatabase.LoadAssetAtPath<Texture2D>(OldWorld+"/Terrain/TG_WorldColorField.asset");
            var owner=Group("One shared wind and world-color owner");globals=owner.gameObject.AddComponent<TeyvatMaterialGlobals>();globals.colorField=field;globals.colorBounds=new Vector4(origin.x-192,origin.z-128,1f/384,1f/384);globals.windStrength=.34f;globals.fixedTime=true;globals.fixedSeconds=1;globals.Apply();
            var geo=Group("R3 authored cliff terraces islands and prop hierarchy",environment);
            foreach(var item in manifest.objects.Where(o=>o.role!="diagnostic"))
                Visual(item.name,meshes[item.name],slots[item.name].Select(s=>mats[s]).ToArray(),geo,Vector3.zero);
            // Both authored LODs remain available, with automatic screen transitions.
            foreach(var pair in geo.Cast<Transform>().Where(t=>t.name.EndsWith("_LOD0")).ToArray())
            {
                var other=geo.Find(pair.name.Replace("_LOD0","_LOD1"));if(other==null)continue;var group=Group(pair.name.Replace("_LOD0",""),geo);pair.SetParent(group,false);other.SetParent(group,false);
                var lod=group.gameObject.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.035f,new[]{pair.GetComponent<Renderer>()}),new LOD(.001f,new[]{other.GetComponent<Renderer>()})});lod.RecalculateBounds();
            }
            BuildLandmarks();BuildWater();BuildTrees();
            foreach(var anchor in layout.anchors)
            {
                var capsule=GameObject.CreatePrimitive(PrimitiveType.Capsule);capsule.name="Proxy_"+anchor.name;capsule.transform.position=anchor.worldPosition+Vector3.up*1.5f;capsule.transform.localScale=new Vector3(.85f,1.5f,.85f);capsule.GetComponent<Renderer>().sharedMaterial=mats[anchor.name.StartsWith("Enemy")?"Enemy":"Player"];Object.DestroyImmediate(capsule.GetComponent<Collider>());
            }
            foreach(var shot in layout.shots)AddCamera("TG_"+shot.name,shot.position,shot.rotation,shot.fov);
            CameraAt("TG_R3_IslandStudy",new Vector3(-47,15,7),new Vector3(-7,5,63),56);
            CameraAt("TG_R3_CascadeStudy",new Vector3(-30,16,46),new Vector3(-12,13,78),55);
            CameraAt("TG_R3_TerraceStudy",new Vector3(76,54,61),new Vector3(-22,25,101),58);
            meadow=BuildMeadow();Lighting(false);globals.fixedTime=false;
            PrefabUtility.SaveAsPrefabAsset(environment.gameObject,R3+"/Prefabs/TG_LayoutR3.prefab");EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ReviewScene);AssetDatabase.SaveAssets();
        }
        static void BuildTerrain()
        {
            var path=R3+"/Terrain/TG_Landscape.asset";var meta=File.Exists(path+".meta")?File.ReadAllBytes(path+".meta"):null;
            if(AssetDatabase.LoadAssetAtPath<TerrainData>(path)!=null)AssetDatabase.DeleteAsset(path);if(meta!=null)File.WriteAllBytes(path+".meta",meta);
            var data=new TerrainData();AssetDatabase.CreateAsset(data,path);data.heightmapResolution=heights.resolution;data.alphamapResolution=512;data.baseMapResolution=1024;data.size=new Vector3(heights.size[0],heights.size[1],heights.size[2]);
            var values=new float[heights.resolution,heights.resolution];for(var z=0;z<heights.resolution;z++)for(var x=0;x<heights.resolution;x++)values[z,x]=(heights.heights_local_metres[z*heights.resolution+x]-heights.position[1])/heights.size[1];data.SetHeights(0,0,values);
            data.terrainLayers=Enumerable.Range(0,3).Select(i=>AssetDatabase.LoadAssetAtPath<TerrainLayer>(OldWorld+"/Terrain/TG_WorldLayer"+i+".terrainlayer")).ToArray();
            var alpha=new float[512,512,3];for(var z=0;z<512;z++)for(var x=0;x<512;x++)
            {
                var u=x/511f;var v=z/511f;var lx=-192+u*384;var lz=-128+v*384;var h=data.GetInterpolatedHeight(u,v)-12;var slope=data.GetSteepness(u,v);
                var rock=Mathf.SmoothStep(0,1,Mathf.InverseLerp(35,62,slope));var soil=(1-rock)*(.05f+.07f*Mathf.PerlinNoise(lx*.16f+40,lz*.16f+22));if(h<-.4f)soil=(1-rock)*.85f;
                alpha[z,x,0]=1-rock-soil;alpha[z,x,1]=soil;alpha[z,x,2]=rock;
            }
            data.SetAlphamaps(0,0,alpha);var terrain=Terrain.CreateTerrainGameObject(data).GetComponent<Terrain>();terrain.name="R3 sculpted native ground - revised banks and islands";terrain.transform.SetParent(environment,false);terrain.transform.localPosition=new Vector3(-192,-12,-128);terrain.materialTemplate=mats["Terrain"];terrain.drawInstanced=true;terrain.heightmapPixelError=2;terrain.basemapDistance=100000;EditorUtility.SetDirty(data);
        }
        static float Ground(float x,float z)
        {
            var terrain=environment.GetComponentInChildren<Terrain>();return terrain.SampleHeight(origin+new Vector3(x,0,z))+terrain.transform.position.y-origin.y;
        }
        static void BuildLandmarks()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(OldWorld+"/Models/TG_WorldGeometry.fbx");var group=Group("Recomposed cathedral aqueduct ruin and background",environment);
            foreach(var f in prefab.GetComponentsInChildren<MeshFilter>())
            {
                if(!new[]{"Cathedral","Aqueduct","RuinArch","DistantRidge"}.Any(k=>f.name.Contains(k))||f.name.StartsWith("COL_")||f.name.EndsWith("_LOD1"))continue;
                var mesh=f.sharedMesh;var offset=Vector3.zero;
                if(f.name.Contains("Cathedral"))offset=new Vector3(-17,Ground(-67,127)-mesh.bounds.min.y-.35f,21);
                if(f.name.Contains("Aqueduct"))offset=new Vector3(2,Ground(69,145)+13-mesh.bounds.max.y,20);
                if(f.name.Contains("RuinArch"))offset=new Vector3(4,Ground(30,12)-mesh.bounds.min.y-.3f,4);
                var materials=f.GetComponent<Renderer>().sharedMaterials.Select(m=>mats.ContainsKey(m.name)?mats[m.name]:mats["Rock"]).ToArray();Visual(f.name,mesh,materials,group,offset);
                if(f.name.Contains("DistantRidge")){var reverse=Group(f.name+" reverse landscape",group);reverse.localRotation=Quaternion.Euler(0,180,0);reverse.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;reverse.gameObject.AddComponent<MeshRenderer>().sharedMaterials=materials;}
                if(f.name.Contains("Aqueduct"))for(var i=0;i<6;i++)
                {
                    var x=25+i*17;var bottom=mesh.vertices.Where(v=>Mathf.Abs(v.x-x)<1.5f&&Mathf.Abs(v.z-125)<2.2f).Min(v=>v.y)+offset.y;
                    var ground=Ground(x+2,145)-.3f;if(bottom<=ground)continue;
                    var pier=GameObject.CreatePrimitive(PrimitiveType.Cube);pier.name="Aqueduct grounded pier extension "+i;pier.transform.SetParent(group,false);pier.transform.localPosition=new Vector3(x+2,(bottom+ground)*.5f,145);pier.transform.localScale=new Vector3(2.8f,bottom-ground+.3f,4.2f);pier.GetComponent<Renderer>().sharedMaterial=mats["Rock"];Object.DestroyImmediate(pier.GetComponent<Collider>());
                }
            }
        }
        static Mesh SaveMesh(Mesh mesh)
        {
            var path=R3+"/Meshes/"+mesh.name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);return old;
        }
        static void Water(string name,Vector3[] vertices,Vector2[] uv,int[] tri,Material mat)
        {
            var m=new Mesh{name=name,vertices=vertices,uv=uv,triangles=tri};m.RecalculateNormals();m.RecalculateBounds();Visual(name,SaveMesh(m),new[]{mat},environment,Vector3.zero);
        }
        static float NearBank(float x){return 23+2.8f*Mathf.Sin(x*.037f)+Mathf.Sin(x*.13f);}
        static float FarBank(float x){return 57+9*Mathf.Exp(-Mathf.Pow((x+43)/20,2))+10*Mathf.Exp(-Mathf.Pow((x-43)/24,2));}
        static void BuildWater()
        {
            var river=Mat("River",Shared+"/Materials/TG_River.mat");river.SetColor("_ShallowColor",new Color(.18f,.73f,.68f,.83f));river.SetColor("_DeepColor",new Color(.09f,.52f,.66f,.93f));river.SetColor("_FoamColor",new Color(.50f,.81f,.81f));river.SetFloat("_DepthRange",6.5f);river.SetFloat("_FlowStrength",.07f);river.SetFloat("_FoamDistance",.30f);
            var fall=Mat("Waterfall",Shared+"/Materials/TG_Waterfall.mat");fall.SetFloat("_FlowStrength",.06f);
            var v=new Vector3[193*19];var uv=new Vector2[v.Length];var tr=new List<int>();
            for(var i=0;i<193;i++)for(var j=0;j<19;j++){var x=-192+i*2;var t=j/18f;var k=i*19+j;v[k]=new Vector3(x,-1.35f,Mathf.Lerp(NearBank(x)-3,FarBank(x)+1,t));uv[k]=new Vector2(x*.05f,t);if(i<192&&j<18)tr.AddRange(new[]{k,k+1,k+19,k+19,k+1,k+20});}
            Water("R3_River_with_five_islands",v,uv,tr.ToArray(),river);
            foreach(var f in plan.waterfalls)
            {
                var points=Enumerable.Range(0,f.path_flat.Length/3).Select(i=>new Vector3(f.path_flat[i*3],f.path_flat[i*3+1],f.path_flat[i*3+2])).ToArray();var vs=new List<Vector3>();var uvs=new List<Vector2>();tr.Clear();
                var rows=(points.Length-1)*12;
                for(var row=0;row<=rows;row++)
                {
                    var segment=Mathf.Min(points.Length-2,row/12);var t=(row-segment*12)/12f;var p=Vector3.Lerp(points[segment],points[segment+1],t);
                    for(var col=0;col<13;col++){var s=col/12f;var k=row*13+col;vs.Add(p+new Vector3((s*2-1)*f.half_width*(1+.08f*Mathf.Sin(row*.13f)),.03f, -.10f+Mathf.Sin(s*Mathf.PI*6+row*.18f)*.04f));uvs.Add(new Vector2(s,row/(float)rows));if(row<rows&&col<12)tr.AddRange(new[]{k,k+1,k+13,k+1,k+14,k+13});}
                }
                Water("R3_Cascade_"+f.name,vs.ToArray(),uvs.ToArray(),tr.ToArray(),fall);
                for(var landing=0;landing<2;landing++)
                {
                    var p=points[landing==0?2:points.Length-1];var vv=new List<Vector3>{p+Vector3.up*.11f};var uu=new List<Vector2>{new Vector2(.5f,.5f)};var tt=new List<int>();
                    for(var i=0;i<=40;i++){var a=i/40f*Mathf.PI*2;vv.Add(p+new Vector3(Mathf.Cos(a)*(f.half_width+2),.11f,Mathf.Sin(a)*3));uu.Add(new Vector2(Mathf.Cos(a)*.5f+.5f,Mathf.Sin(a)*.5f+.5f));if(i<40)tt.AddRange(new[]{0,i+1,i+2});}Water("R3_CatchPool_"+f.name+landing,vv.ToArray(),uu.ToArray(),tt.ToArray(),river);
                }
            }
        }
        static void BuildTrees()
        {
            var group=Group("R3 tree and shrub composition placeholders for WP05",environment);var rng=new System.Random(731);
            var samples=AssetDatabase.LoadAllAssetsAtPath(Shared+"/Models/TG_MaterialSamples.fbx").OfType<Mesh>().ToDictionary(m=>m.name);
            var leaf=Mat("Leaf",Shared+"/Materials/TG_Leaf.mat");leaf.SetColor("_BaseColor",new Color(.48f,.68f,.27f));var trunk=Mat("Trunk",Shared+"/Materials/TG_Trunk.mat");
            var canopy=Mat("CanopyVolume",OldWorld+"/Materials/TG_World_Ground.mat");canopy.SetFloat("_UseColorField",0);canopy.SetColor("_BaseColor",new Color(.27f,.50f,.16f));canopy.SetFloat("_Wrap",.45f);
            var placements=new List<Vector3>{new Vector3(-32,0,-1),new Vector3(38,0,7),new Vector3(-47,0,10),new Vector3(46,0,-29),new Vector3(-38,0,-37)};
            foreach(var island in plan.islands)placements.Add(new Vector3(island.x-.9f,0,island.z));
            var forestCenters=new[]{new Vector2(-106,100),new Vector2(-76,127),new Vector2(-92,147),new Vector2(-35,108),new Vector2(14,103),new Vector2(78,121),new Vector2(115,146),new Vector2(-50,-67),new Vector2(0,-90),new Vector2(50,-69)};
            foreach(var center in forestCenters)for(var i=0;i<11;i++){var a=(float)rng.NextDouble()*Mathf.PI*2;var r=Mathf.Sqrt((float)rng.NextDouble())*15;placements.Add(new Vector3(center.x+Mathf.Cos(a)*r,0,center.y+Mathf.Sin(a)*r));}
            var pine=Mat("PineVolume",OldWorld+"/Materials/TG_World_Ground.mat");pine.SetFloat("_UseColorField",0);pine.SetColor("_BaseColor",new Color(.19f,.39f,.28f));
            var cone=new Mesh{name="R3_ConiferCrown"};var cv=new List<Vector3>();var ci=new List<int>();
            for(var i=0;i<12;i++){var a=i*Mathf.PI/6;cv.Add(new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)));}cv.Add(Vector3.up*2.5f);cv.Add(Vector3.zero);
            for(var i=0;i<12;i++){ci.AddRange(new[]{i,12,(i+1)%12});ci.AddRange(new[]{13,i,(i+1)%12});}cone.SetVertices(cv);cone.SetTriangles(ci,0);cone.RecalculateNormals();cone.RecalculateBounds();cone=SaveMesh(cone);
            var treeAnchors=new List<TreeAnchor>();
            for(var i=0;i<placements.Count;i++)
            {
                var p=placements[i];var y=Ground(p.x,p.z);if(y<-.8f)continue;var hero=i==0;var size=hero?12:i<5?7:4+(float)rng.NextDouble()*3;
                var tree=Group("TreeGroup_"+i,group);tree.localPosition=new Vector3(p.x,y-.1f,p.z);
                treeAnchors.Add(new TreeAnchor{name=tree.name,role=i==0?"hero tree":i<5?"perimeter framing":i<10?"river island tree":"terrace/reverse grove",localPosition=tree.localPosition,heightStartingValue=size});
                Visual("Rooted trunk",samples["SM_Trunk"],new[]{trunk},tree,Vector3.zero);tree.GetChild(0).localScale=new Vector3(size*.22f,size*.34f,size*.22f);
                if(i>=10&&i%3!=0)
                {
                    for(var j=0;j<4;j++){Visual("Layered conifer crown "+j,cone,new[]{pine},tree,new Vector3(0,size*(.15f+j*.16f),0));tree.GetChild(tree.childCount-1).localScale=Vector3.one*size*(.24f-j*.035f);}continue;
                }
                for(var j=0;j<(hero?14:7);j++)
                {
                    var angle=j*2.399f;var radial=j==0?0:size*.22f;var local=new Vector3(Mathf.Cos(angle)*radial,size*.47f+(j%3)*size*.07f,Mathf.Sin(angle)*radial);
                    Visual("Layered leafy canopy "+j,samples["SM_LeafCluster"],new[]{leaf},tree,local);tree.GetChild(tree.childCount-1).localScale=Vector3.one*size*.48f;
                    var volume=GameObject.CreatePrimitive(PrimitiveType.Sphere);volume.name="Canopy mass "+j;volume.transform.SetParent(tree,false);volume.transform.localPosition=local+Vector3.up*size*.12f;volume.transform.localScale=new Vector3(size*.41f,size*.26f,size*.40f);volume.GetComponent<Renderer>().sharedMaterial=canopy;Object.DestroyImmediate(volume.GetComponent<Collider>());
                }
            }
            File.WriteAllText(R3+"/Data/TG_TreeComposition.json",JsonUtility.ToJson(new TreeComposition{trees=treeAnchors.ToArray()},true));
        }
        static void AddCamera(string name,Vector3 p,Quaternion q,float fov)
        {
            var c=new GameObject(name).AddComponent<Camera>();c.transform.SetPositionAndRotation(p,q);c.fieldOfView=fov;c.nearClipPlane=.1f;c.farClipPlane=800;c.allowHDR=true;c.allowMSAA=true;c.enabled=name=="TG_ReferencePlanningCandidate";if(c.enabled)c.tag="MainCamera";c.GetUniversalAdditionalCameraData().requiresDepthTexture=true;
        }
        static void CameraAt(string name,Vector3 p,Vector3 target,float fov){AddCamera(name,origin+p,Quaternion.LookRotation(target-p),fov);}
        static void Lighting(bool neutral)
        {
            var sun=Object.FindFirstObjectByType<Light>();if(sun==null)sun=new GameObject("R3 review sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=neutral?1.1f:1.35f;sun.color=neutral?Color.white:new Color(1,.93f,.79f);sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(neutral?55:38,-43,0);RenderSettings.sun=sun;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.55f,.69f,.82f);RenderSettings.ambientEquatorColor=new Color(.32f,.43f,.47f);RenderSettings.ambientGroundColor=new Color(.24f,.29f,.17f);RenderSettings.fog=!neutral;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=80;RenderSettings.fogEndDistance=550;RenderSettings.fogColor=new Color(.62f,.78f,.85f);RenderSettings.skybox=AssetDatabase.LoadAssetAtPath<Material>(Shared+"/Settings/TG_MaterialSky.mat");
        }
        static void Capture(Camera camera,string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));var rt=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var previous=RenderTexture.active;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try{camera.aspect=16f/9;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{RenderTexture.active=previous;Object.DestroyImmediate(image);RenderTexture.ReleaseTemporary(rt);}
        }
        static void Finish()
        {
            if(!pending||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<nextTick)return;pending=false;EditorApplication.update-=Finish;
            try
            {
                globals.fixedTime=true;globals.fixedSeconds=1;globals.Apply();var imports=ValidateImports();var envelope=ValidateEnvelope();var composition=ValidateComposition();
                var cameras=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);foreach(var neutral in reload?new[]{false}:new[]{true,false}){Lighting(neutral);foreach(var camera in cameras.Where(c=>!reload||new[]{"TG_ReferencePlanningCandidate","TG_PlayerAttack","TG_R3_IslandStudy"}.Contains(c.name)))Capture(camera,output+(neutral?"/Neutral/":"/Afternoon/")+camera.name+".png");}
                var height=JsonUtility.FromJson<R3HeightReport>(File.ReadAllText(R3+"/Meadow/TG_HeightPolicy.json"));var pass=imports&&composition&&envelope.Item1&&envelope.Item2&&height.rootContactPassed&&height.grassRangePassed;
                File.WriteAllText(output+"/LayoutR3Report.json",JsonUtility.ToJson(new R3Report{passed=pass,imports=imports,camera=envelope.Item1,anchors=envelope.Item2,tallGrass=height.grassRangePassed,rootContact=height.rootContactPassed,islands=plan.islands.Length,cascades=plan.waterfalls.Length,meshes=manifest.objects.Length,cameras=cameras.Length,placements=meadow.data.placementCount,gpu=SystemInfo.graphicsDeviceName,context=reload?"Independent cold saved-scene process, no rebuild.":"R3 authored layout redesign, isolated URP Linear editor review; static actor proxies, tree/sky/water look development remains."},true));
                if(!pass)throw new Exception("R3 checks failed; read reports.");globals.fixedTime=false;Lighting(false);EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ReviewScene);AssetDatabase.SaveAssets();
                if(reload){var own=AssetDatabase.FindAssets("",new[]{R3,Shared+"/Shaders",Shared+"/Settings",Meadow+"/Shaders"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>!AssetDatabase.IsValidFolder(p)).ToArray();var dependencies=AssetDatabase.GetDependencies(own,true).Concat(own).Where(p=>p.StartsWith("Assets/")&&!AssetDatabase.IsValidFolder(p)).Distinct().ToArray();AssetDatabase.ExportPackage(dependencies,Path.GetFullPath(output+"/../TG_WP01_LayoutR3.unitypackage"),(ExportPackageOptions)0);}
                File.WriteAllText(output+"/COMPLETE.txt","R3 authored layout and sampled camera/anchor review passed. Final combat/player/visual acceptance remains recorded in handoff.");EditorApplication.Exit(0);
            }catch(Exception e){Fail(e);}
        }
        [Serializable] sealed class R3Report {public bool passed,imports,camera,anchors,tallGrass,rootContact;public int islands,cascades,meshes,cameras,placements;public string gpu,context;}
    }
}
