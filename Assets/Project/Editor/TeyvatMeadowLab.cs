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
    /// <summary>WP04 authored assets and deterministic meadow. Runs in the isolated URP lab.</summary>
    public static partial class TeyvatMeadowLab
    {
        const string Meadow="Assets/Project/Art/Environment/TeyvatGrassland/Meadow";
        const string World="Assets/Project/Art/Environment/TeyvatGrassland/World";
        const string Shared="Assets/Project/Art/Environment/TeyvatGrassland/Shared";
        const string Foundation="Assets/Project/Art/Environment/TeyvatGrassland/FoundationR2";
        const string Model=Meadow+"/Models/TG_MeadowKit.fbx";
        const string ReviewScene=Meadow+"/Scenes/TG_MeadowReview.unity";
        const int Seed=20261018;
        static string output;static bool reload,pending;static double nextTick;
        static Layout layout;static Transform environment;static TeyvatMeadowRenderer meadow;
        static TeyvatMaterialGlobals globals;static Texture2D mask;static Color[] maskPixels;
        static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
        static readonly Dictionary<string,string[]> slots=new Dictionary<string,string[]>();
        static readonly string[] speciesNames={"ShortGrass","MeadowGrass","TallGrass","AccentGrass","Clover","DaisyWhite","FlowerGold","FlowerPurple"};
        [Serializable] sealed class Layout {public Vector3 origin;public Quaternion rotation;public Anchor[] anchors;public Shot[] shots;}
        [Serializable] sealed class Anchor {public string name;public Vector3 worldPosition;public Vector3 localPosition;}
        [Serializable] sealed class Shot {public string name;public Vector3 position;public Quaternion rotation;public float fov;}
        [Serializable] sealed class PlacementSummary {public int seed,candidates,placed,greenExcluded,cameraExcluded,slopeOrSurfaceExcluded;public int[] speciesCounts;public string method;}
        static PlacementSummary placementSummary;

        public static void RunIsolated(){Start(false);}
        public static void VerifySaved(){Start(true);}
        static void Start(bool saved)
        {
            var marker=Path.Combine(Path.GetDirectoryName(Application.dataPath),"TG_ISOLATED_MEADOW_LAB.txt");
            if(!File.Exists(marker)||File.Exists("Assets/Project/Scenes/Battle.unity"))throw new InvalidOperationException("WP04 requires the isolated art lab.");
            output=File.ReadAllText(marker).Trim()+(saved?"/Reload":"");reload=saved;Directory.CreateDirectory(output);
            try
            {
                ShaderUtil.allowAsyncCompilation=false;
                if(PlayerSettings.colorSpace!=ColorSpace.Linear)throw new InvalidOperationException("Cold start in Linear color space required.");
                if(!saved){var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Shared+"/Settings/TG_MaterialURP.asset");GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;QualitySettings.vSyncCount=0;}
                layout=JsonUtility.FromJson<Layout>(File.ReadAllText(Foundation+"/TG_Layout.json"));
                if(saved)EditorSceneManager.OpenScene(ReviewScene,OpenSceneMode.Single);
                else
                {
                    foreach(var folder in new[]{"Materials","Meshes","Data","Prefabs","Scenes","Terrain"})Directory.CreateDirectory(Meadow+"/"+folder);
                    AssetDatabase.Refresh();PrepareImports();
                    EditorSceneManager.OpenScene(World+"/Scenes/TG_WorldReview.unity",OpenSceneMode.Single);
                }
                environment=GameObject.Find("TG WP03 World geometry only").transform;
                globals=Object.FindFirstObjectByType<TeyvatMaterialGlobals>();
                if(!saved)Build();else meadow=Object.FindFirstObjectByType<TeyvatMeadowRenderer>();
                LoadModel();pending=true;nextTick=EditorApplication.timeSinceStartup+3;EditorApplication.update+=Finish;
            }
            catch(Exception e){Fail(e);}
        }
        static void Fail(Exception e){Debug.LogException(e);Directory.CreateDirectory(output);File.WriteAllText(output+"/FAILED.txt",e.ToString());EditorApplication.Exit(1);}
        static void PrepareImports()
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(Model);
            if(importer==null)throw new InvalidOperationException("Export the Blender meadow kit before building.");
            importer.globalScale=1;importer.bakeAxisConversion=true;importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.Import;
            importer.isReadable=true;importer.importCameras=false;importer.importLights=false;importer.meshCompression=ModelImporterMeshCompression.Off;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.SaveAndReimport();LoadModel();
        }
        static void LoadModel()
        {
            meshes.Clear();slots.Clear();foreach(var mesh in AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Mesh>())meshes[mesh.name]=mesh;
            foreach(var f in AssetDatabase.LoadAssetAtPath<GameObject>(Model).GetComponentsInChildren<MeshFilter>(true))
                slots[f.sharedMesh.name]=f.GetComponent<Renderer>().sharedMaterials.Select(m=>m.name).ToArray();
        }
        static Mesh AnimatedMesh(string name)
        {
            var source=meshes[name];var path=Meadow+"/Meshes/"+name+"_Animated.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=Object.Instantiate(source);AssetDatabase.CreateAsset(mesh,path);}else EditorUtility.CopySerialized(source,mesh);
            mesh.name=name+"_Animated";
            // Conservative stored bounds cover strength1, response.65 and the
            // authored nonuniform scale range; world wind padding is also saved.
            var b=source.bounds;b.Expand(new Vector3(1.8f,1.8f,1.8f));mesh.bounds=b;EditorUtility.SetDirty(mesh);return mesh;
        }
        static Material MakeMaterial(string name,Color tint,float family)
        {
            var path=Meadow+"/Materials/TG_Meadow_"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Teyvat/URP/MeadowFoliage"));AssetDatabase.CreateAsset(m,path);}
            m.shader=Shader.Find("Teyvat/URP/MeadowFoliage");m.enableInstancing=true;m.SetColor("_BaseColor",tint);
            m.SetColor("_RootColor",Color.white);m.SetColor("_TipColor",Color.white);m.SetFloat("_Family",family);
            // All slots in one flower clump share amplitude to keep petal/stem joins attached.
            m.SetFloat("_UseColorField",family==0?1:0);m.SetFloat("_WindResponse",.65f);
            m.SetFloat("_AlphaClip",0);m.SetFloat("_Cull",0);m.SetFloat("_Wrap",.4f);m.SetFloat("_Transmission",.22f);
            EditorUtility.SetDirty(m);return m;
        }
        static void Build()
        {
            var materials=new Dictionary<string,Material>{
                {"Grass",MakeMaterial("Grass",new Color(.53f,.72f,.24f),0)},
                {"White",MakeMaterial("White",new Color(.93f,.94f,.78f),2)},
                {"Gold",MakeMaterial("Gold",new Color(.93f,.70f,.21f),2)},
                {"Purple",MakeMaterial("Purple",new Color(.53f,.38f,.76f),2)} };
            var dataPath=Meadow+"/Data/TG_MeadowPlacement.asset";var data=AssetDatabase.LoadAssetAtPath<TeyvatMeadowData>(dataPath);
            if(data==null){data=ScriptableObject.CreateInstance<TeyvatMeadowData>();AssetDatabase.CreateAsset(data,dataPath);}
            data.seed=Seed;data.revision="WP04-r1";
            data.species=speciesNames.Select(n=>new TeyvatMeadowData.Species{
                name=n,meshLOD0=AnimatedMesh("SM_"+n+"_LOD0"),meshLOD1=AnimatedMesh("SM_"+n+"_LOD1"),
                materialsLOD0=slots["SM_"+n+"_LOD0"].Select(s=>materials[s]).ToArray(),
                materialsLOD1=slots["SM_"+n+"_LOD1"].Select(s=>materials[s]).ToArray()}).ToArray();
            BuildMeadowGround();BuildPlacement(data);
            var node=new GameObject("TG WP04 Meadow - seeded grass and flowers");meadow=node.AddComponent<TeyvatMeadowRenderer>();meadow.data=data;meadow.globals=globals;
            EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
            // Shared clock/field is owned by the review/master scene, not the module.
            meadow.globals=null;PrefabUtility.SaveAsPrefabAsset(node,Meadow+"/Prefabs/TG_Meadow.prefab");meadow.globals=globals;
            AddCloseCamera("TG_MeadowClose",new Vector3(-28,1.35f,-14),new Vector3(-18,.45f,-4),54);
            AddCloseCamera("TG_MeadowFlowers",new Vector3(24,1.25f,10),new Vector3(20,.4f,19),48);
            globals.fixedTime=false;EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ReviewScene);AssetDatabase.SaveAssets();
        }
        static void BuildMeadowGround()
        {
            var terrain=environment.GetComponentInChildren<Terrain>();var source=AssetDatabase.LoadAssetAtPath<TerrainData>(World+"/Terrain/TG_WorldGround.asset");
            var path=Meadow+"/Terrain/TG_MeadowGround.asset";
            // Allocate the owned native asset BEFORE populating height/control
            // arrays. CreateAsset can reset transient native splat backing.
            var meta=File.Exists(path+".meta")?File.ReadAllBytes(path+".meta"):null;
            if(AssetDatabase.LoadAssetAtPath<TerrainData>(path)!=null)AssetDatabase.DeleteAsset(path);
            if(meta!=null)File.WriteAllBytes(path+".meta",meta);
            var clone=new TerrainData();AssetDatabase.CreateAsset(clone,path);
            clone.heightmapResolution=source.heightmapResolution;clone.alphamapResolution=source.alphamapResolution;clone.baseMapResolution=source.baseMapResolution;clone.size=source.size;clone.terrainLayers=source.terrainLayers;
            clone.SetHeights(0,0,source.GetHeights(0,0,source.heightmapResolution,source.heightmapResolution));
            clone.SetHoles(0,0,source.GetHoles(0,0,source.holesResolution,source.holesResolution));
            var alpha=source.GetAlphamaps(0,0,source.alphamapWidth,source.alphamapHeight);
            for(var z=0;z<clone.alphamapHeight;z++)for(var x=0;x<clone.alphamapWidth;x++)
            {
                var local=terrain.transform.localPosition+new Vector3(x/(float)(clone.alphamapWidth-1)*clone.size.x,0,z/(float)(clone.alphamapHeight-1)*clone.size.z);
                var blend=1-Mathf.SmoothStep(0,1,Mathf.Max((Mathf.Abs(local.x)-20)/6,(Mathf.Abs(local.z)-18)/6));
                if(blend<=0)continue;var rock=alpha[z,x,2];var desired=Mathf.Min(1-rock,Mathf.Lerp(.84f,.95f,Noise(local.x,local.z,.11f,32)));
                alpha[z,x,0]=Mathf.Lerp(alpha[z,x,0],Mathf.Max(alpha[z,x,0],desired),blend);alpha[z,x,1]=Mathf.Max(0,1-rock-alpha[z,x,0]);
            }
            clone.SetAlphamaps(0,0,alpha);
            var restored=clone.GetAlphamaps(0,0,clone.alphamapWidth,clone.alphamapHeight);var maximumError=0f;
            for(var z=0;z<clone.alphamapHeight;z++)for(var x=0;x<clone.alphamapWidth;x++)for(var i=0;i<clone.alphamapLayers;i++)maximumError=Mathf.Max(maximumError,Mathf.Abs(alpha[z,x,i]-restored[z,x,i]));
            File.WriteAllText(output+"/TerrainControlWrite.json","{\"maximumImmediateWriteError\":"+maximumError.ToString(System.Globalization.CultureInfo.InvariantCulture)+"}");
            if(maximumError>2f/255)throw new InvalidOperationException("Native terrain control array did not survive its write.");
            terrain.terrainData=clone;terrain.GetComponent<TerrainCollider>().terrainData=clone;EditorUtility.SetDirty(clone);
        }
        static void AddCloseCamera(string name,Vector3 local,Vector3 look,float fov)
        {
            var p=GroundPoint(local.x,local.z);if(p.HasValue)local.y=p.Value.y-layout.origin.y+local.y;
            var q=GroundPoint(look.x,look.z);if(q.HasValue)look.y=q.Value.y-layout.origin.y+look.y;
            var c=new GameObject(name).AddComponent<Camera>();c.transform.SetPositionAndRotation(layout.origin+local,Quaternion.LookRotation(look-local));c.fieldOfView=fov;c.nearClipPlane=.08f;c.farClipPlane=800;c.enabled=false;c.allowMSAA=true;c.GetUniversalAdditionalCameraData().requiresDepthTexture=true;
        }
        static readonly List<Collider> groundColliders=new List<Collider>();
        static readonly List<Bounds> groundColliderBounds=new List<Bounds>();
        static readonly List<MeshCollider> temporaryColliders=new List<MeshCollider>();
        static void AddGroundColliders()
        {
            groundColliders.Clear();temporaryColliders.Clear();
            foreach(var filter in environment.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!filter.name.EndsWith("_LOD0"))continue;var c=filter.gameObject.AddComponent<MeshCollider>();c.sharedMesh=filter.sharedMesh;groundColliders.Add(c);temporaryColliders.Add(c);
            }
            groundColliders.Add(environment.GetComponentInChildren<TerrainCollider>());Physics.SyncTransforms();groundColliderBounds.Clear();groundColliderBounds.AddRange(groundColliders.Select(c=>c.bounds));
        }
        static void RemoveGroundColliders(){foreach(var c in temporaryColliders)Object.DestroyImmediate(c);temporaryColliders.Clear();groundColliders.Clear();groundColliderBounds.Clear();}
        static bool GroundAt(Vector3 p,out float height)
        {
            var closest=200f;var found=false;height=0;
            for(var i=0;i<groundColliders.Count;i++)
            {
                var b=groundColliderBounds[i];if(p.x<b.min.x||p.x>b.max.x||p.z<b.min.z||p.z>b.max.z)continue;
                if(groundColliders[i].Raycast(new Ray(p+Vector3.up*100,Vector3.down),out var h,closest)){closest=h.distance;height=h.point.y;found=true;}
            }
            return found;
        }
        static Vector3? GroundPoint(float x,float z)
        {
            var terrain=environment.GetComponentInChildren<Terrain>();var world=layout.origin+new Vector3(x,0,z);
            return new Vector3(world.x,terrain.SampleHeight(world)+terrain.transform.position.y,world.z);
        }
        static Color PlacementMask(float x,float z)
        {
            if(Mathf.Abs(x)>50||Mathf.Abs(z)>50)return Color.clear;
            var u=Mathf.Clamp(Mathf.RoundToInt((x+50)/100*(mask.width-1)),0,mask.width-1);
            var v=Mathf.Clamp(Mathf.RoundToInt((z+50)/100*(mask.height-1)),0,mask.height-1);return maskPixels[v*mask.width+u];
        }
        static float Noise(float x,float z,float scale,float offset){return Mathf.PerlinNoise(x*scale+offset,z*scale+offset*.37f);}
        static void BuildPlacement(TeyvatMeadowData data)
        {
            // Read the original numerical mask without changing its importer or bytes.
            var path=Foundation+"/TG_MeadowPlacementMask.png";mask=new Texture2D(2,2,TextureFormat.RGB24,false,true);mask.LoadImage(File.ReadAllBytes(path));maskPixels=mask.GetPixels();
            var cells=new Dictionary<Vector2Int,List<TeyvatMeadowData.Placement>>();var rng=new System.Random(Seed);var counts=new int[8];
            placementSummary=new PlacementSummary{seed=Seed,speciesCounts=counts,method="Jittered candidate grid, seeded broad patches. User WP04 direction permits grass on battlefield/character anchors: original WP01 green now marks SHORT cover, not bare-ground exclusion. Taller grass/flowers remain outside; original mask bytes/layout unchanged. Native splat/slope and actual WP03 surface, rock/water and camera/torso rejection."};
            var terrain=environment.GetComponentInChildren<Terrain>();var terrainData=terrain.terrainData;var alpha=terrainData.GetAlphamaps(0,0,terrainData.alphamapWidth,terrainData.alphamapHeight);
            var cameraPoints=new List<Vector3>();cameraPoints.AddRange(layout.shots.Select(s=>s.position));
            var paths=new[]{new[]{"PlayerPlanning","EnemyPlanning"},new[]{"PlayerPlanning","PlayerAttack"},new[]{"PlayerAttack","PlayerContact"},new[]{"PlayerContact","PlayerPlanning"},new[]{"EnemyPlanning","EnemyAttack"},new[]{"EnemyAttack","EnemyContact"},new[]{"EnemyContact","EnemyPlanning"}};
            foreach(var pair in paths)for(var i=0;i<33;i++)cameraPoints.Add(Orbit(layout.shots.First(s=>s.name==pair[0]).position,layout.shots.First(s=>s.name==pair[1]).position,layout.origin,i/32f));
            var meshRoots=RootSamples(data);AddGroundColliders();
            try
            {
                const float spacing=.34f;
                for(var iz=-205;iz<=380;iz++)for(var ix=-275;ix<=275;ix++)
                {
                    var x=(ix+(float)rng.NextDouble()*.88f)*spacing;var z=(iz+(float)rng.NextDouble()*.88f)*spacing;placementSummary.candidates++;
                    var m=PlacementMask(x,z);
                    // Explicit user revision: grass may grow at character positions.
                    // Green is a short-growth region, retaining the original mask asset.
                    var combat=m.g>.1f||(Mathf.Abs(x)<18.9f&&Mathf.Abs(z)<17.9f);
                    var n=Noise(x,z,.085f,17);var broad=Noise(x,z,.027f,9);var meadowWeight=Mathf.SmoothStep(0,1,(n-.27f)/.40f);
                    var border=m.r*Mathf.Lerp(.65f,1.25f,n);var outside=(Mathf.Abs(x)>29||Mathf.Abs(z)>27)?Mathf.Lerp(.06f,.20f,broad)*meadowWeight:0;
                    var probability=combat?Mathf.Lerp(.42f,.98f,Mathf.SmoothStep(0,1,(Noise(x,z,.16f,23)-.22f)/.50f)):Mathf.Max(border,outside);
                    if(rng.NextDouble()>probability)continue;
                    var ray=new Ray(layout.origin+new Vector3(x,100,z),Vector3.down);var found=false;var closest=200f;RaycastHit best=default;Collider surface=null;
                    foreach(var c in groundColliders){if(c.Raycast(ray,out var h,closest)){closest=h.distance;best=h;surface=c;found=true;}}
                    if(!found||best.normal.y<.81f||best.point.y<layout.origin.y-1.0f){placementSummary.slopeOrSurfaceExcluded++;continue;}
                    if(surface is MeshCollider)
                    {
                        var f=surface.GetComponent<MeshFilter>();var mesh=f.sharedMesh;var index=best.triangleIndex;var sub=0;
                        while(sub<mesh.subMeshCount-1&&index>=mesh.GetIndexCount(sub)/3){index-=(int)mesh.GetIndexCount(sub)/3;sub++;}
                        var material=surface.GetComponent<Renderer>().sharedMaterials[sub];
                        if(!material.name.EndsWith("Moss")&&!material.name.EndsWith("Ground")){placementSummary.slopeOrSurfaceExcluded++;continue;}
                    }
                    else
                    {
                        var p=terrain.transform.InverseTransformPoint(best.point);var ax=Mathf.Clamp(Mathf.RoundToInt(p.x/terrainData.size.x*(terrainData.alphamapWidth-1)),0,terrainData.alphamapWidth-1);var az=Mathf.Clamp(Mathf.RoundToInt(p.z/terrainData.size.z*(terrainData.alphamapHeight-1)),0,terrainData.alphamapHeight-1);
                        if(alpha[az,ax,0]<.62f){placementSummary.slopeOrSurfaceExcluded++;continue;}
                    }
                    var inner=Mathf.Max(Mathf.Abs(x)-18.9f,Mathf.Abs(z)-17.9f);var type=inner<2.3f?0:1;
                    var roll=(float)rng.NextDouble();if(type!=0){if(roll<.12f)type=2;else if(roll<.14f)type=3;else if(roll<.22f)type=4;}
                    var patch=Noise(x,z,.16f,41);
                    if(inner>2&&m.b>.10f&&patch>.40f&&rng.NextDouble()<m.b*.62f)
                    {var species=Noise(x,z,.075f,71);type=species<.43f?5:species>.59f?7:6;}
                    var scale=Mathf.Lerp(.78f,1.23f,(float)rng.NextDouble());var yscale=scale*Mathf.Lerp(.86f,1.17f,(float)rng.NextDouble());
                    if(combat){type=roll<.78f?0:roll<.96f?1:4;scale*=1.16f;yscale=type==0?Mathf.Lerp(1.30f,2.05f,(float)rng.NextDouble()):type==1?Mathf.Lerp(.37f,.61f,(float)rng.NextDouble()):.60f;}
                    var pworld=best.point-Vector3.up*.025f;
                    var matrix=Matrix4x4.TRS(pworld,Quaternion.FromToRotation(Vector3.up,best.normal)*Quaternion.Euler(0,(float)rng.NextDouble()*360,0),new Vector3(scale,yscale,scale));
                    var test=ClearanceBounds(data.species[type],matrix);
                    var roots=meshRoots[type];var contact=true;
                    for(var ri=0;ri<roots.Length;ri+=Mathf.Max(1,roots.Length/12))
                    {
                        var rp=matrix.MultiplyPoint3x4(roots[ri]);if(!GroundAt(rp,out var gy)||rp.y-gy>.08f||rp.y-gy<-.13f){contact=false;break;}
                    }
                    if(!contact){placementSummary.slopeOrSurfaceExcluded++;continue;}
                    // Conservatively reject any clump bound that cuts a registered torso
                    // sightline or the sampled moving camera's own clearance volume.
                    var blocked=false;
                    foreach(var cp in cameraPoints)
                    {
                        if(test.SqrDistance(cp)<.65f*.65f){blocked=true;break;}
                        foreach(var a in layout.anchors){var d=a.worldPosition+Vector3.up*1.6f-cp;if(test.IntersectRay(new Ray(cp,d.normalized),out var t)&&t<d.magnitude-.2f){blocked=true;break;}}
                        if(blocked)break;
                    }
                    if(blocked){placementSummary.cameraExcluded++;continue;}
                    var key=new Vector2Int(Mathf.FloorToInt(x/12),Mathf.FloorToInt(z/12));if(!cells.ContainsKey(key))cells[key]=new List<TeyvatMeadowData.Placement>();
                    cells[key].Add(new TeyvatMeadowData.Placement{speciesIndex=type,matrix=matrix,phase=(float)rng.NextDouble()});counts[type]++;
                }
            }
            finally{RemoveGroundColliders();Object.DestroyImmediate(mask);}
            var totalBounds=new Bounds(layout.origin,Vector3.zero);
            data.cells=cells.OrderBy(c=>c.Key.y).ThenBy(c=>c.Key.x).Select(c=>{
                var b=new Bounds(c.Value[0].matrix.GetColumn(3),Vector3.zero);
                foreach(var p in c.Value)b.Encapsulate(PlacementBounds(data.species[p.speciesIndex],p.matrix));
                totalBounds.Encapsulate(b);return new TeyvatMeadowData.Cell{name="Cell_"+c.Key.x+"_"+c.Key.y,worldBounds=b,placements=c.Value.ToArray()};}).ToArray();
            data.worldBounds=totalBounds;data.placementCount=counts.Sum();placementSummary.placed=data.placementCount;
            File.WriteAllText(output+"/PlacementGeneration.json",JsonUtility.ToJson(placementSummary,true));
            File.WriteAllText(Meadow+"/Data/TG_MeadowDensityPolicy.json","{\n  \"revision\":\"WP04-r1-user-direction-2026-10-18\",\n  \"seed\":20261018,\n  \"greenMaskPolicy\":\"Short grass allowed across battlefield and at all six character positions; tall accents and flowers outside green. Original WP01 mask retained unchanged.\",\n  \"combatGrassHeightStartingCapMetres\":0.35,\n  \"density\":\"Seeded clustered continuous short cover; denser mixed meadow borders; local soil/rock/water/camera rejection.\"\n}\n");
        }
        static Bounds TransformBounds(Bounds source,Matrix4x4 matrix)
        {
            var e=source.extents;var x=matrix.MultiplyVector(Vector3.right*e.x);var y=matrix.MultiplyVector(Vector3.up*e.y);var z=matrix.MultiplyVector(Vector3.forward*e.z);
            return new Bounds(matrix.MultiplyPoint3x4(source.center),new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z))*2);
        }
        static Bounds PlacementBounds(TeyvatMeadowData.Species species,Matrix4x4 matrix)
        {
            var b=TransformBounds(species.meshLOD0.bounds,matrix);b.Encapsulate(TransformBounds(species.meshLOD1.bounds,matrix));b.Expand(1.6f);return b;
        }
        static Bounds ClearanceBounds(TeyvatMeadowData.Species species,Matrix4x4 matrix)
        {
            var b=TransformBounds(meshes["SM_"+species.name+"_LOD0"].bounds,matrix);b.Encapsulate(TransformBounds(meshes["SM_"+species.name+"_LOD1"].bounds,matrix));b.Expand(new Vector3(1.08f,.09f,1.08f));return b;
        }
        static Vector3[][] RootSamples(TeyvatMeadowData data)
        {
            return data.species.Select(s=>{var m=s.meshLOD0;var v=m.vertices;var c=m.colors;return Enumerable.Range(0,v.Length).Where(i=>c[i].r<.0001f&&c[i].g<.0001f).Select(i=>v[i]).Distinct().ToArray();}).ToArray();
        }
        static void Finish()
        {
            if(!pending||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<nextTick)return;pending=false;EditorApplication.update-=Finish;
            try
            {
                globals.fixedTime=true;globals.fixedSeconds=1;globals.Apply();
                var imports=ValidateImports();var placement=ValidatePlacement();var surface=ValidateTerrainSurface();var envelope=ValidateEnvelope();var wind=ValidateWind();
                var cameras=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).OrderBy(c=>c.name).ToArray();
                foreach(var neutral in reload?new[]{false}:new[]{true,false})
                {
                    Lighting(neutral);
                    foreach(var camera in cameras.Where(c=>!reload||new[]{"TG_ReferencePlanningCandidate","TG_EnemyAttack","TG_MeadowClose"}.Contains(c.name)))Capture(camera,output+(neutral?"/Neutral/":"/Afternoon/")+camera.name+".png");
                }
                if(!reload){TemporalAndDistance(cameras);Measure(cameras.First(c=>c.name=="TG_ReferencePlanningCandidate"));}
                var coverage=ValidateCoverage();
                var shaders=meadow.data.species.SelectMany(s=>s.materialsLOD0).Distinct().All(m=>m.shader.isSupported&&!ShaderUtil.GetShaderMessages(m.shader).Any(e=>e.severity.ToString()=="Error"));
                var missing=SceneManager.GetActiveScene().GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
                File.WriteAllText(output+"/MeadowLabReport.json",JsonUtility.ToJson(new FinalReport{passed=imports&&placement&&surface&&envelope.Item1&&envelope.Item2&&wind&&coverage&&shaders&&missing==0,imports=imports,placement=placement,camera=envelope.Item1,terrain=envelope.Item2&&surface,wind=wind,gpuCoverage=coverage,shaders=shaders,missingScripts=missing,placements=meadow.data.placementCount,cells=meadow.data.cells.Length,species=meadow.data.species.Length,cameras=cameras.Length,unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName,context=reload?"Cold saved-scene process; no Build or pipeline reconfiguration.":"Isolated URP Forward Linear Windows Editor; static copied actor proxies.",limitations="WP04 module, not final map. Trees, water, lighting remain WP01/WP02 context for WP05/WP06/WP08. Actual animated combat and minimum-PC player performance remain WP07/WP09. Distance thresholds are starting values reviewed in captures."},true));
                if(!(imports&&placement&&surface&&envelope.Item1&&envelope.Item2&&wind&&coverage&&shaders&&missing==0))throw new InvalidOperationException("WP04 checks failed; read evidence reports.");
                globals.fixedTime=false;Lighting(false);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ReviewScene);AssetDatabase.SaveAssets();
                if(reload)
                {
                    var own=AssetDatabase.FindAssets("",new[]{Meadow,Shared+"/Shaders",Shared+"/Settings"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>!AssetDatabase.IsValidFolder(p)).ToArray();
                    var paths=AssetDatabase.GetDependencies(own,true).Concat(own).Where(p=>p.StartsWith("Assets/")&&!AssetDatabase.IsValidFolder(p)).Distinct().ToArray();
                    AssetDatabase.ExportPackage(paths,Path.GetFullPath(output+"/../TG_WP04_Meadow.unitypackage"),(ExportPackageOptions)0);
                }
                File.WriteAllText(output+"/COMPLETE.txt","WP04 meadow checks passed; scope and limitations recorded in handoff.\n");EditorApplication.Exit(0);
            }
            catch(Exception e){Fail(e);}
        }
        [Serializable] sealed class FinalReport {public bool passed,imports,placement,camera,terrain,wind,gpuCoverage,shaders;public int missingScripts,placements,cells,species,cameras;public string unity,gpu,context,limitations;}
    }
}

