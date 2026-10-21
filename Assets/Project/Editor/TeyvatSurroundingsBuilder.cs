using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using FightingAllstar.EnvironmentArt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FightingAllstar.EditorTools
{
    /// <summary>Authored scenery outside the playable footprint; deterministic and rebuildable.</summary>
    [InitializeOnLoad]
    public static class TeyvatSurroundingsBuilder
    {
        public const string Root = "Assets/Project/Art/Environment/TeyvatGrassland/Surroundings";
        public const string Stage = Root + "/Prefabs/TG_SurroundedBattleStage.prefab";
        public const string Review = "Assets/Project/Scenes/EnvironmentReview/TeyvatGrassland/TG_SurroundingsReview.unity";
        public const string Evidence = "Artifacts/TeyvatGrassland/Surroundings";
        const string Source = "Assets/Project/Art/Environment/TeyvatGrassland/Lighting/Prefabs/TG_LitBattleStage_r2.prefab";
        const string Lighting = "Assets/Project/Art/Environment/TeyvatGrassland/Lighting";
        static Transform root, landscape;
        static Terrain terrain;
        static Vector3 origin;
        static System.Random random;
        static Material rock, turf, snow, sea;
        static readonly Dictionary<string, Material> treeMaterials = new Dictionary<string, Material>();
        static readonly List<MeshFilter> landforms = new List<MeshFilter>();
        static readonly List<Mesh> rocks = new List<Mesh>();
        static readonly List<Material[]> rockMaterials = new List<Material[]>();
        static int treeCount, rockCount;
        static TeyvatSurroundingsBuilder() { EditorApplication.update += Requests; }
        static void Requests()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            const string request = "Temp/build-teyvat-surroundings.request";
            if (!File.Exists(request)) return;
            File.Delete(request);
            try { Build(); }
            catch (Exception e) { Directory.CreateDirectory(Evidence); File.WriteAllText(Evidence + "/Failed.txt", e.ToString()); Debug.LogException(e); }
        }

        [MenuItem("Fighting Allstar/Stage/Build Teyvat Surrounding Scenery")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save open scene edits and leave Play Mode before rebuilding scenery.");
            foreach (var folder in new[] { "Meshes", "Materials", "Prefabs" }) Directory.CreateDirectory(Root + "/" + folder);
            Directory.CreateDirectory(Evidence); AssetDatabase.Refresh();
            random = new System.Random(194731); landforms.Clear(); rocks.Clear(); rockMaterials.Clear(); treeMaterials.Clear(); treeCount = rockCount = 0;
            var contents = PrefabUtility.LoadPrefabContents(Source);
            try
            {
                root = contents.transform; origin = root.position; terrain = contents.GetComponentInChildren<Terrain>();
                landscape = Group("Surrounding world - Great Tree valley, Frost Crown and Tidewatch coast", root);
                var materials = contents.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToArray();
                rock = materials.First(m => m.name == "TG_R3_Rock");
                // The old broad strips read as vertical blue walls from side/rear
                // cameras and hide the new middle/farland. Replace their silhouettes.
                foreach(var wall in contents.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("SM_DistantRidge_")).ToArray())
                    Object.DestroyImmediate(wall.gameObject);
                turf = Copy(materials.First(m => m.name == "TG_R3_Moss"), "TG_SurroundingTurf");
                turf.SetFloat("_UseColorField", 0); turf.SetColor("_BaseColor", new Color(.36f, .59f, .18f));
                snow = Copy(rock, "TG_FrostCrownSnow"); snow.SetFloat("_ReferenceRock", 0); snow.SetFloat("_UseSharedRockStyle", 0);
                snow.SetFloat("_TextureStrength", .16f); snow.SetColor("_BaseColor", new Color(.82f, .92f, .98f)); snow.SetFloat("_MossAmount", 0);
                sea = Copy(materials.First(m => m.name == "TG_Stage_River"), "TG_OpenCoastWater");
                foreach (var m in materials.Where(m => m.name.StartsWith("TG_Tree") || m.name.StartsWith("TG_Broadleaf") || m.name.StartsWith("TG_Leaf") || m.name.StartsWith("TG_Pine"))) treeMaterials[m.name] = m;
                BuildRockKit(contents);
                var coastTurf=Copy(turf,"TG_CoastalGrassBlend");coastTurf.SetFloat("_UseColorField",1);coastTurf.SetColor("_BaseColor",Color.white);
                coastTurf.SetTexture("_BaseMap",terrain.terrainData.terrainLayers[0].diffuseTexture);coastTurf.SetFloat("_Mapping",1);coastTurf.SetFloat("_WorldScale",1f/6);coastTurf.SetFloat("_Wrap",.4f);
                Visual("Continuous coastal land beyond the native terrain edge", Apron(), new[] { rock, coastTurf }, landscape, Vector3.zero);
                BuildSea();
                // Starting dimensions: read from four compass views and all authored battle shots.
                // Move a silhouette outward or lower it if it crosses a fighter head sightline.
                Land("Great Tree root terrace", -96, -24, 78, 64, 12, 8);
                Land("Great Tree outer bluff", -164, -6, 98, 90, 22, 19);
                Land("West river gate", -156, 70, 74, 90, 29, 25);
                Land("West high pasture", -200, 156, 108, 120, 30, 28);
                Land("West horizon headland", -292, -28, 154, 135, 47, 34);
                Land("Rear shepherd ridge west", -106, -139, 125, 70, 19, 43);
                Land("Rear shepherd ridge east", 97, -146, 104, 84, 23, 45);
                Land("Rear forest shoulder", -178, -203, 140, 96, 35, 51);
                Land("Rear glacial foothill", 42, -248, 182, 125, 45, 54);
                Land("Tidewatch near bluff", 112, -76, 90, 68, 13, 63);
                Land("East river gate", 168, 78, 75, 86, 28, 69);
                Land("Tidewatch high pasture", 210, 165, 108, 116, 30, 72);
                Land("Tidewatch island - lighthouse", 287, -56, 100, 112, 38, 82, true);
                Land("Tidewatch island - split rock", 368, 65, 118, 86, 52, 83, true);
                Land("Tidewatch island - long reef", 433, -132, 135, 73, 29, 84, true);
                Land("Tidewatch island - far headland", 548, 72, 150, 122, 63, 86, true);
                Land("Western offshore mesa", -380, 100, 138, 119, 55, 87, true);
                Land("West scalloped shoreline", -189, -77, 88, 96, 7, 131);
                Land("West broken coast spur", -205, 18, 72, 110, 9, 134);
                Land("East low tide headland", 194, -39, 96, 88, 4.5f, 139);
                Land("East wooded shore shoulder", 211, -113, 105, 77, 10, 142);
                Land("Rear meadow saddle", -13, -128, 133, 67, 6, 146);
                Peak("Frost Crown massif", -54, -438, 420, 280, 222, 95);
                Peak("Frost Crown eastern spur", 205, -414, 215, 182, 136, 102);
                Peak("Western blue ridge", -415, -286, 265, 190, 112, 109);
                var blue=Copy(rock,"TG_NorthernHazeRock");blue.SetFloat("_ReferenceRock",0);blue.SetFloat("_UseSharedRockStyle",0);blue.SetFloat("_TextureStrength",.12f);blue.SetColor("_BaseColor",new Color(.30f,.44f,.54f));
                Visual("Northern ridge beyond the cathedral",TeyvatSurroundingsGeometry.SnowPeak("TG_NorthernRidge",650,240,152,117),new[]{blue,blue},landscape,new Vector3(0,-5,458));
                Dress();
                var binding = contents.GetComponent<TeyvatStageBinding>(); binding.meadow.data = binding.canonicalMeadowData;
                PrefabUtility.SaveAsPrefabAsset(contents, Stage); AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            ApplyToDungeon();
            File.WriteAllText(Evidence + "/Build.json", "{\n  \"seed\":194731,\n  \"addedTrees\":" + treeCount + ",\n  \"addedRocks\":" + rockCount + ",\n  \"stage\":\"" + Stage + "\",\n  \"scope\":\"left, right, rear middle-to-farland and cliff groves\"\n}");
            if (File.Exists(Evidence + "/Failed.txt")) File.Delete(Evidence + "/Failed.txt");
        }

        static Transform Group(string name, Transform parent)
        { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
        static Material Copy(Material source, string name)
        {
            var path = Root + "/Materials/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(source); AssetDatabase.CreateAsset(m, path); } else EditorUtility.CopySerialized(source, m);
            m.name = name; m.enableInstancing = true; EditorUtility.SetDirty(m); return m;
        }
        static Mesh Save(Mesh source)
        {
            var path = Root + "/Meshes/" + source.name + ".asset"; var target = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!target) { AssetDatabase.CreateAsset(source, path); return source; }
            // Explicit mesh setters invalidate the native/GPU vertex buffers as
            // well as the saved asset. CopySerialized alone can leave old buffers
            // visible after regenerating a mesh with a different vertex count.
            target.Clear();target.indexFormat=source.indexFormat;target.vertices=source.vertices;
            target.normals=source.normals;target.uv=source.uv;target.colors=source.colors;target.tangents=source.tangents;
            target.subMeshCount=source.subMeshCount;
            for(var i=0;i<source.subMeshCount;i++)target.SetTriangles(source.GetTriangles(i),i,false);
            target.bounds=source.bounds;target.UploadMeshData(false);
            Object.DestroyImmediate(source); EditorUtility.SetDirty(target); return target;
        }
        static MeshFilter Visual(string name, Mesh mesh, Material[] materials, Transform parent, Vector3 position)
        {
            var t = Group(name, parent); t.localPosition = position;
            var f = t.gameObject.AddComponent<MeshFilter>(); f.sharedMesh = Save(mesh);
            var r = t.gameObject.AddComponent<MeshRenderer>(); r.sharedMaterials = materials;
            r.receiveShadows = true; r.shadowCastingMode = position.magnitude > 240 ? ShadowCastingMode.Off : ShadowCastingMode.On;
            return f;
        }
        static float Rand(float min, float max) => min + (float)random.NextDouble() * (max - min);
        static float Native(float x, float z)
        {
            var p = terrain.transform.position - origin; var size = terrain.terrainData.size;
            return terrain.terrainData.GetInterpolatedHeight(Mathf.Clamp01((x-p.x)/size.x), Mathf.Clamp01((z-p.z)/size.z)) + p.y;
        }
        static float ApronHeight(float x, float z)
        {
            var px = Mathf.Clamp(x, -192, 192); var pz = Mathf.Clamp(z, -128, 256);
            var edge = Mathf.Max(Mathf.Abs(x) - 192, -z - 128, z - 256);
            // Broad pastoral shoulders lead to a scalloped coastline, then submerged seabed.
            var coast = 58 + 20 * Mathf.Sin(z * .018f) + 13 * Mathf.Sin(x * .027f + z * .01f);
            var blend = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(8, coast, edge));
            var baseHeight = Native(px, pz) + 3 * Mathf.Sin(x*.018f)*Mathf.Sin(z*.022f) * Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,25,edge));
            return Mathf.Lerp(baseHeight - .30f, -8, blend);
        }
        static Mesh Apron()
        {
            const int nx=171,nz=155; const float step=8;
            var vertices=new Vector3[nx*nz]; var uv=new Vector2[vertices.Length]; var rockTris=new List<int>(); var turfTris=new List<int>();
            for(var z=0;z<nz;z++)for(var x=0;x<nx;x++) {var xx=-680+x*step;var zz=-624+z*step;var i=z*nx+x;vertices[i]=new Vector3(xx,ApronHeight(xx,zz),zz);uv[i]=new Vector2(xx*.03f,zz*.03f);}
            for(var z=0;z<nz-1;z++)for(var x=0;x<nx-1;x++)
            {
                var i=z*nx+x;var p=vertices[i];
                // Keep the entire authored arena, river, cascades and forward cliffs untouched.
                if(p.x>=-112&&p.x+step<=112&&p.z>=-80&&p.z+step<=248)continue;
                var slope=Mathf.Max(Mathf.Abs(vertices[i+1].y-p.y),Mathf.Abs(vertices[i+nx].y-p.y))/step;
                var tris=slope>.65f?rockTris:turfTris;
                tris.AddRange(new[]{i,i+nx,i+1,i+1,i+nx,i+nx+1});
            }
            var mesh=new Mesh{name="TG_ContinuousCoastalApron",indexFormat=IndexFormat.UInt32};mesh.vertices=vertices;mesh.uv=uv;mesh.subMeshCount=2;
            mesh.SetTriangles(rockTris,0);mesh.SetTriangles(turfTris,1);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static void BuildSea()
        {
            // Four disjoint exterior grids leave the authored native basin open.
            // Fog and displacement are evaluated per vertex; giant horizon quads
            // would expose their diagonals as large differently fogged panels.
            var v=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
            foreach(var r in new[]{new Vector4(-2400,-192,-2400,2400),new Vector4(192,2400,-2400,2400),new Vector4(-192,192,-2400,-128),new Vector4(-192,192,256,2400)})
            {
                var start=v.Count;var nx=Mathf.CeilToInt((r.y-r.x)/32);var nz=Mathf.CeilToInt((r.w-r.z)/32);
                for(var z=0;z<=nz;z++)for(var x=0;x<=nx;x++)
                {var p=new Vector3(Mathf.Lerp(r.x,r.y,(float)x/nx),-1.35f,Mathf.Lerp(r.z,r.w,(float)z/nz));v.Add(p);uv.Add(new Vector2(p.x*.05f,p.z*.05f));}
                for(var z=0;z<nz;z++)for(var x=0;x<nx;x++)
                {var i=start+z*(nx+1)+x;tris.AddRange(new[]{i,i+nx+1,i+1,i+1,i+nx+1,i+nx+2});}
            }
            var mesh=new Mesh{name="TG_SeaToHorizon",indexFormat=IndexFormat.UInt32};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            Visual("Open coast water - continuous horizon below every land edge",mesh,new[]{sea},landscape,Vector3.zero);
        }
        static void Land(string name,float x,float z,float width,float depth,float height,int seed,bool offshore=false)
        {
            var y=offshore?-5:Mathf.Max(-.5f,Native(x,z)-1.5f);
            landforms.Add(Visual(name,TeyvatSurroundingsGeometry.Landform("TG_"+name.Replace(' ','_'),width,depth,height,seed,true),new[]{rock,turf},landscape,new Vector3(x,y,z)));
        }
        static void Peak(string name,float x,float z,float width,float depth,float height,int seed)
        { Visual(name,TeyvatSurroundingsGeometry.SnowPeak("TG_"+name.Replace(' ','_'),width,depth,height,seed),new[]{rock,snow},landscape,new Vector3(x,-6,z)); }
        static float Ground(float x,float z)
        {
            var h = x>=-192&&x<=192&&z>=-128&&z<=256 ? Native(x,z) : ApronHeight(x,z);
            foreach(var f in landforms)
            {
                var p=f.transform.localPosition;var b=f.sharedMesh.bounds;
                if(x<p.x+b.min.x||x>p.x+b.max.x||z<p.z+b.min.z||z>p.z+b.max.z)continue;
                var v=f.sharedMesh.vertices;var tris=f.sharedMesh.triangles;var xx=x-p.x;var zz=z-p.z;
                for(var i=0;i<tris.Length;i+=3)
                {
                    var a=v[tris[i]];var c=v[tris[i+1]];var d=v[tris[i+2]];
                    var den=(c.z-d.z)*(a.x-d.x)+(d.x-c.x)*(a.z-d.z);if(Mathf.Abs(den)<.0001f)continue;
                    var u=((c.z-d.z)*(xx-d.x)+(d.x-c.x)*(zz-d.z))/den;var w=((d.z-a.z)*(xx-d.x)+(a.x-d.x)*(zz-d.z))/den;
                    if(u>=0&&w>=0&&u+w<=1)h=Mathf.Max(h,p.y+u*a.y+w*c.y+(1-u-w)*d.y);
                }
            }
            return h;
        }
        static void BuildRockKit(GameObject source)
        {
            foreach(var f in source.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("SM_R3_Boulder_")&&f.name.EndsWith("LOD1")).Take(6))
            {
                var m=Object.Instantiate(f.sharedMesh);m.name="TG_SurroundRock_"+rocks.Count;var b=m.bounds;
                var v=m.vertices;for(var i=0;i<v.Length;i++)v[i]-=new Vector3(b.center.x,b.min.y,b.center.z);m.vertices=v;m.RecalculateBounds();rocks.Add(Save(m));
                rockMaterials.Add(f.GetComponent<Renderer>().sharedMaterials.Select(material=>material.name=="TG_R3_Moss"?turf:material).ToArray());
            }
        }
        static void Tree(string species,float x,float z,float height,Transform parent)
        {
            var p=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Art/Environment/TeyvatGrassland/Trees/Prefabs/TG_"+species+".prefab");
            var t=((GameObject)PrefabUtility.InstantiatePrefab(p,parent)).transform;t.localPosition=new Vector3(x,Ground(x,z)-.12f,z);t.localRotation=Quaternion.Euler(0,Rand(0,360),0);
            var native=species=="HeroOak"?12:species=="MeadowOak"?8:species=="IslandTree"?6:species=="WindPine"?8:species=="YoungPine"?6:1.8f;
            t.localScale=Vector3.one*(height/native);
            foreach(var r in t.GetComponentsInChildren<Renderer>(true))
            {r.sharedMaterials=r.sharedMaterials.Select(m=>m!=null&&treeMaterials.TryGetValue(m.name,out var lit)?lit:m).ToArray();if(new Vector2(x,z).magnitude>230)r.shadowCastingMode=ShadowCastingMode.Off;}
            treeCount++;
        }
        static void Rock(float x,float z,float scale,Transform parent)
        {
            var t=Group("Weathered cliff rock "+rockCount++,parent);t.localPosition=new Vector3(x,Ground(x,z)-.35f*scale,z);t.localRotation=Quaternion.Euler(Rand(-8,8),Rand(0,360),Rand(-7,7));t.localScale=Vector3.one*scale;
            var index=random.Next(rocks.Count);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=rocks[index];var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterials=rockMaterials[index];
        }
        static void Grove(string name,float cx,float cz,float rx,float rz,int count,bool pine=false,float min=7,float max=13)
        {
            var g=Group(name,landscape);var placed=new List<Vector2>();var tries=0;
            while(placed.Count<count&&tries++<count*30)
            {
                var a=Rand(0,Mathf.PI*2);var r=Mathf.Sqrt(Rand(0,1));var x=cx+Mathf.Cos(a)*rx*r;var z=cz+Mathf.Sin(a)*rz*r;
                if(Mathf.Abs(x)<31&&z>-34&&z<28)continue;
                if(Ground(x,z)<-.3f||placed.Any(p=>(p-new Vector2(x,z)).sqrMagnitude<16))continue;
                var slope=Mathf.Max(Mathf.Abs(Ground(x+2,z)-Ground(x-2,z)),Mathf.Abs(Ground(x,z+2)-Ground(x,z-2)))/4;
                if(slope>.85f)continue;
                Tree(pine?(random.Next(3)==0?"YoungPine":"WindPine"):(random.Next(4)==0?"IslandTree":"MeadowOak"),x,z,Rand(min,max),g);
                placed.Add(new Vector2(x,z));if(placed.Count%3==0)Rock(x+Rand(-3,3),z+Rand(-3,3),Rand(.45f,1.25f),g);
            }
        }
        static void Dress()
        {
            var great=Group("Great Tree sanctuary - ancient roots and a broken pilgrim trail",landscape);
            Tree("HeroOak",-97,-24,54,great);
            for(var i=0;i<15;i++){var a=i*2.39996f;Rock(-97+Mathf.Cos(a)*Rand(13,25),-24+Mathf.Sin(a)*Rand(10,20),Rand(.7f,1.7f),great);}
            Grove("West valley woodland",-83,-24,49,51,58,false,8,16);
            Grove("Left river and cliff groves",-130,91,48,85,68,false,7,15);
            Grove("Rear near meadow copses",-7,-76,126,29,60,false,8,14);
            Grove("Rear conifer foothills",-75,-166,105,43,67,true,10,19);
            Grove("Rear eastern woodland",107,-140,58,45,43,true,9,17);
            Grove("East coastal woodland - north frame",104,12,51,19,27,false,8,15);
            Grove("East coastal woodland - south frame",104,-74,49,26,30,false,8,15);
            Grove("Right cliff terraces",122,142,61,82,75,true,8,16);
            Grove("High cliff canopy groups",-96,192,66,51,54,true,9,17);
            Grove("Tidewatch distant island trees",286,-56,32,36,19,false,7,13);
            Grove("Split rock island trees",368,65,37,22,14,true,10,16);
            Grove("Western coastal edge copse",-180,-79,23,32,16,false,7,12);
            Grove("Eastern coastal edge copse",187,-98,27,27,15,false,7,12);
            // Repeated small groups on existing cliff ledges, with open waterfall channels.
            foreach(var p in new[]{new Vector2(-82,102),new Vector2(84,108),new Vector2(-115,155),new Vector2(121,192),new Vector2(-154,-87),new Vector2(155,-92)})
                for(var i=0;i<9;i++)Rock(p.x+Rand(-12,12),p.y+Rand(-10,10),Rand(.6f,1.8f),landscape);
            BuildWaymarkers(great); BuildWatchtower();
        }
        static void BuildWaymarkers(Transform parent)
        {
            for(var i=0;i<7;i++)
            {
                var x=-58-i*4.5f;var z=-45+Mathf.Sin(i*.6f)*7;
                var m=TeyvatSurroundingsGeometry.Landform("TG_PilgrimStone"+i,1.7f,1.5f,i==6?4:1.9f,201+i,false);
                Visual("Old pilgrim waymarker "+i,m,new[]{rock,rock},parent,new Vector3(x,Ground(x,z)-.2f,z));
            }
        }
        static void BuildWatchtower()
        {
            var g=Group("Tidewatch - abandoned beacon on a sea stack",landscape);var x=286f;var z=-53f;var y=Ground(x,z);
            Visual("Weathered beacon shaft and broken battlements",BeaconMesh(),new[]{rock},g,new Vector3(x,y-.6f,z));
        }
        static Mesh BeaconMesh()
        {
            var v=new List<Vector3>();var triangles=new List<int>();
            var radii=new[]{3.8f,3.8f,3.0f,3.7f};var levels=new[]{0f,1.2f,14f,15.3f};
            for(var ring=0;ring<4;ring++)for(var i=0;i<8;i++){var a=i*Mathf.PI/4;v.Add(new Vector3(Mathf.Cos(a)*radii[ring],levels[ring],Mathf.Sin(a)*radii[ring]));}
            for(var ring=0;ring<3;ring++)for(var i=0;i<8;i++){var a=ring*8+i;var b=ring*8+(i+1)%8;triangles.AddRange(new[]{a,a+8,b,b,a+8,b+8});}
            var center=v.Count;v.Add(new Vector3(0,15.3f,0));for(var i=0;i<8;i++)triangles.AddRange(new[]{center,24+(i+1)%8,24+i});
            foreach(var angle in new[]{0f,90f,180f,270f})
            {
                var rotation=Quaternion.Euler(0,angle,0);var start=v.Count;
                foreach(var p in new[]{new Vector3(-1,15,-3.9f),new Vector3(1,15,-3.9f),new Vector3(1,18,-3.9f),new Vector3(-1,18,-3.9f),new Vector3(-1,15,-2.3f),new Vector3(1,15,-2.3f),new Vector3(1,18,-2.3f),new Vector3(-1,18,-2.3f)})v.Add(rotation*p);
                triangles.AddRange(new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5}.Select(i=>i+start));
            }
            var mesh=new Mesh{name="TG_TidewatchBeaconBase"};mesh.SetVertices(v);mesh.SetTriangles(triangles,0);mesh.uv=v.Select(p=>new Vector2(p.x*.1f,p.y*.1f)).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        public static void ApplyToDungeon()
        {
            var previous=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene=EditorSceneManager.OpenScene(TeyvatDungeonBuilder.ScenePath);
                var old=Object.FindFirstObjectByType<TeyvatStageBinding>();var pos=old.transform.position;
                Object.DestroyImmediate(old.gameObject);
                var stage=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Stage),scene);
                stage.name="Teyvat Arena";stage.transform.position=pos;
                var binding=stage.GetComponent<TeyvatStageBinding>();binding.ApplyBinding();
                var rig=Object.FindFirstObjectByType<TeyvatLightingRig>();rig.globals=binding.globals;rig.Apply();
                Camera.main.farClipPlane=1600;
                binding.meadow.data=binding.canonicalMeadowData;EditorSceneManager.SaveScene(scene);binding.ApplyBinding();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }
    }
}
