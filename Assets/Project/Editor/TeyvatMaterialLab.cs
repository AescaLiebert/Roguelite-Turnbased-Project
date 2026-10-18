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
    /// <summary>WP02 uses a dedicated, cold-start Linear URP project, never changes the gameplay pipeline.</summary>
    public static partial class TeyvatMaterialLab
    {
        public const string Shared="Assets/Project/Art/Environment/TeyvatGrassland/Shared";
        public const string LabScene=Shared+"/Scenes/TG_MaterialLab.unity";
        const string Marker="TG_ISOLATED_MATERIAL_LAB.txt";
        static string output;
        static readonly Dictionary<string,Mesh> samples=new Dictionary<string,Mesh>();
        static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        static TeyvatMaterialGlobals globals;
        static Vector3 origin;
        static double nextTick;
        static bool pending;

        [Serializable] sealed class Layout {public Vector3 origin;public Anchor[] anchors;public Shot[] shots;}
        [Serializable] sealed class Anchor {public string name;public Vector3 worldPosition;public Vector3 localPosition;}
        [Serializable] sealed class Shot {public string name;public Vector3 position;public Quaternion rotation;public float fov;}
        [Serializable] sealed class ShaderCheck {public string name;public bool supported;public string[] errors;}
        [Serializable] sealed class LabReport
        {
            public string context;public string unity;public string gpu;public string pipeline;public string colorSpace;
            public bool importsPass;public bool windPass;public bool waterDepthPass;public bool colorFieldPass;public bool instancingCoveragePass;public bool shaderErrors;public ShaderCheck[] shaders;
            public int submittedGrassInstances;public int submittedGrassBatches;public int submissionFrames;
            public int renderers;public long meshTrianglesExcludingTerrain;public float editorCaptureMilliseconds;
            public string[] captures;public string limitations;
        }

        public static void RunIsolated()
        {
            var root=Path.GetDirectoryName(Application.dataPath);
            if(!File.Exists(Path.Combine(root,Marker))||File.Exists("Assets/Project/Scenes/Battle.unity"))
                throw new InvalidOperationException("WP02 requires the isolated material lab, never the main gameplay project.");
            output=File.ReadAllText(Path.Combine(root,Marker)).Trim();Directory.CreateDirectory(output);
            try
            {
                if(PlayerSettings.colorSpace!=ColorSpace.Linear)throw new InvalidOperationException("Start this project with Linear color space already saved.");
                ShaderUtil.allowAsyncCompilation=false;
                ConfigurePipeline();PrepareImports();Build();
                nextTick=EditorApplication.timeSinceStartup+6;pending=true;EditorApplication.update+=Finish;
            }
            catch(Exception e){Fail(e);}
        }

        [Serializable] sealed class SavedSceneReport
        {
            public bool passed;public bool windBoundsPass;public bool instancingCoveragePass;public string context;public string pipeline;public string colorSpace;
            public int grassInstances;public int submittedInstances;public int submittedBatches;public int cameras;
            public int missingScripts;public string[] errors;public string[] captures;
        }
        // A separate cold editor invocation checks the delivered scene without rebuilding it.
        public static void VerifySavedIsolated()
        {
            var root=Path.GetDirectoryName(Application.dataPath);
            if(!File.Exists(Path.Combine(root,Marker))||File.Exists("Assets/Project/Scenes/Battle.unity"))
                throw new InvalidOperationException("Saved-scene verification requires the isolated material lab.");
            output=File.ReadAllText(Path.Combine(root,Marker)).Trim()+"/Reload";Directory.CreateDirectory(output);
            try
            {
                ShaderUtil.allowAsyncCompilation=false;
                EditorSceneManager.OpenScene(LabScene,OpenSceneMode.Single);
                globals=Object.FindFirstObjectByType<TeyvatMaterialGlobals>();
                nextTick=EditorApplication.timeSinceStartup+4;pending=true;EditorApplication.update+=FinishSavedScene;
            }
            catch(Exception e){Fail(e);}
        }
        static void FinishSavedScene()
        {
            if(!pending||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<nextTick)return;
            pending=false;EditorApplication.update-=FinishSavedScene;
            try
            {
                var errors=new List<string>();
                if(!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))errors.Add("Saved project did not reopen with URP.");
                if(PlayerSettings.colorSpace!=ColorSpace.Linear)errors.Add("Saved project did not reopen in Linear color space.");
                if(globals==null)throw new InvalidOperationException("Missing saved material-global controller.");
                if(globals.grassMesh==null||globals.grassMaterial==null||globals.colorField==null)errors.Add("Saved grass/color assets are missing.");
                if(globals.grassInstances==null||globals.grassInstances.Length!=4200)errors.Add("Saved grass matrices did not survive serialization.");
                var missingScripts=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                if(missingScripts>0)errors.Add("Saved scene contains missing scripts.");
                foreach(var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    if(renderer.sharedMaterials.Any(m=>m==null||m.shader==null))errors.Add("Missing material/shader: "+renderer.name);
                foreach(var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                    if(filter.sharedMesh==null)errors.Add("Missing mesh: "+filter.name);
                var terrain=Object.FindFirstObjectByType<Terrain>();
                if(terrain==null||terrain.terrainData==null||terrain.materialTemplate==null||terrain.terrainData.terrainLayers.Any(l=>l==null))errors.Add("Saved TerrainData/layers/material missing.");
                var cameras=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
                if(cameras.Length!=11)errors.Add("Expected eleven saved review cameras.");
                var files=new List<string>();globals.fixedTime=true;globals.fixedSeconds=1;globals.Apply();
                var windBoundsPass=ValidateWindAndBounds(globals,output);
                if(!windBoundsPass)errors.Add("Saved meshes did not retain safe animated bounds.");
                var coveragePass=ValidateInstancedCoverage(output);
                if(!coveragePass)errors.Add("Saved runtime failed actual GPU instancing coverage.");
                foreach(var name in new[]{"TG_ReferencePlanningCandidate","TG_GrassTransition"})
                {
                    var camera=cameras.First(c=>c.name==name);var file=name+".png";Capture(camera,output+"/"+file);files.Add(file);
                }
                if(globals.submittedInstances!=4200||globals.submittedBatches!=Mathf.CeilToInt(4200f/TeyvatMaterialGlobals.InstancesPerSubmission))errors.Add("Saved scene did not submit all grass instances.");
                var report=new SavedSceneReport{passed=errors.Count==0,windBoundsPass=windBoundsPass,instancingCoveragePass=coveragePass,context="Separate cold Unity editor process; saved scene opened without Build or pipeline reconfiguration. Editor rendering, not a player performance measurement.",pipeline=GraphicsSettings.currentRenderPipeline==null?"null":GraphicsSettings.currentRenderPipeline.name,colorSpace=PlayerSettings.colorSpace.ToString(),grassInstances=globals.grassInstances==null?0:globals.grassInstances.Length,submittedInstances=globals.submittedInstances,submittedBatches=globals.submittedBatches,cameras=cameras.Length,missingScripts=missingScripts,errors=errors.ToArray(),captures=files.ToArray()};
                File.WriteAllText(output+"/SavedSceneReport.json",JsonUtility.ToJson(report,true));
                if(!report.passed)throw new InvalidOperationException("Saved-scene verification failed; inspect SavedSceneReport.json.");
                File.WriteAllText(output+"/COMPLETE.txt","Saved scene passed cold-editor reload and render checks.\n");EditorApplication.Exit(0);
            }
            catch(Exception e){Fail(e);}
        }

        static void Fail(Exception e)
        {
            Debug.LogException(e);Directory.CreateDirectory(output);File.WriteAllText(output+"/FAILED.txt",e.ToString());EditorApplication.Exit(1);
        }

        static void ConfigurePipeline()
        {
            Directory.CreateDirectory(Shared+"/Settings");AssetDatabase.Refresh();
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Shared+"/Settings/TG_MaterialRenderer.asset");
            if(renderer==null)
            {
                renderer=ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer,Shared+"/Settings/TG_MaterialRenderer.asset");
                UnityEngine.Rendering.ResourceReloader.TryReloadAllNullIn(renderer,UniversalRenderPipelineAsset.packagePath);
            }
            renderer.renderingMode=RenderingMode.Forward;
            // Transparent river shading must see opaque depth during its own pass.
            renderer.copyDepthMode=CopyDepthMode.AfterOpaques;
            var asset=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Shared+"/Settings/TG_MaterialURP.asset");
            if(asset==null){asset=UniversalRenderPipelineAsset.Create(renderer);AssetDatabase.CreateAsset(asset,Shared+"/Settings/TG_MaterialURP.asset");}
            asset.supportsHDR=true;asset.msaaSampleCount=4;asset.shadowDistance=80;asset.shadowCascadeCount=2;
            asset.supportsCameraDepthTexture=true;asset.supportsCameraOpaqueTexture=false;
            GraphicsSettings.defaultRenderPipeline=asset;QualitySettings.renderPipeline=asset;
            QualitySettings.vSyncCount=0;QualitySettings.shadows=UnityEngine.ShadowQuality.All;
            EditorUtility.SetDirty(renderer);EditorUtility.SetDirty(asset);AssetDatabase.SaveAssets();
        }

        static void PrepareImports()
        {
            var path=Shared+"/Models/TG_MaterialSamples.fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.globalScale=1;importer.bakeAxisConversion=true;importer.importNormals=ModelImporterNormals.Import;
            importer.importTangents=ModelImporterTangents.Import;importer.isReadable=true;importer.importCameras=false;importer.importLights=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.meshCompression=ModelImporterMeshCompression.Off;
            importer.SaveAndReimport();
            samples.Clear();
            foreach(var m in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())samples[m.name]=m;
            if(samples.Count!=6)throw new InvalidOperationException("Expected six Blender sample meshes, got "+samples.Count);
            var tex=(TextureImporter)AssetImporter.GetAtPath(Shared+"/Textures/TG_RockPainted.png");
            tex.sRGBTexture=true;tex.wrapMode=TextureWrapMode.Repeat;tex.mipmapEnabled=true;tex.maxTextureSize=2048;tex.textureCompression=TextureImporterCompression.CompressedHQ;tex.SaveAndReimport();
        }

        static void Build()
        {
            foreach(var folder in new[]{"Materials","Terrain","Meshes","Scenes","Settings","Prefabs"})Directory.CreateDirectory(Shared+"/"+folder);
            AssetDatabase.Refresh();materials.Clear();
            var layout=JsonUtility.FromJson<Layout>(File.ReadAllText(Shared+"/Settings/TG_WP01Layout.json"));origin=layout.origin;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=Group("TG WP02 authored material sample",null);root.position=origin;
            MakeMaterials();
            var terrain=BuildTerrain(root);
            globals=Group("Shared color field and deterministic wind + instanced grass",root).gameObject.AddComponent<TeyvatMaterialGlobals>();
            globals.colorField=BuildColorField();globals.colorBounds=new Vector4(origin.x-28,origin.z-18,1f/56,1f/66);
            globals.windStrength=.34f;globals.fixedTime=true;globals.fixedSeconds=1;globals.Apply();
            var grass=Object.Instantiate(samples["SM_GrassClump"]);grass.name="TG_GrassClump_AnimatedBounds";
            var b=grass.bounds;b.Expand(new Vector3(1.2f,.3f,1.2f));grass.bounds=b;
            globals.grassMesh=SaveMesh(grass);globals.grassMaterial=materials["Grass"];
            // Renderer.localBounds overrides do not serialize. Persist the animated
            // bounds in dedicated shared mesh assets so a cold reload retains them.
            foreach(var name in new[]{"SM_LeafCluster","SM_FlowerCluster"})
            {
                var animated=Object.Instantiate(samples[name]);animated.name=name.Replace("SM_","TG_")+"_AnimatedBounds";
                var bounds=animated.bounds;bounds.Expand(new Vector3(1.2f,.35f,1.2f));animated.bounds=bounds;
                samples[name]=SaveMesh(animated);
            }
            var matrices=new List<Matrix4x4>();var rng=new System.Random(20261018);
            for(var i=0;i<16500;i++)
            {
                var x=-26+(float)rng.NextDouble()*52;var z=-16+(float)rng.NextDouble()*43;
                var border=Mathf.Sqrt(x*x/(17*17)+z*z/(13*13));
                if(Mathf.Abs(x)<14.5f&&Mathf.Abs(z)<10.8f)continue;
                if(border>1.55f&&rng.NextDouble()>.22)continue;
                var y=SampleTerrain(terrain,x,z);if(z>17||y<-.1f)continue;
                var size=.60f+(float)rng.NextDouble()*.65f;
                matrices.Add(Matrix4x4.TRS(origin+new Vector3(x,y-.018f,z),Quaternion.Euler(0,(float)rng.NextDouble()*360,0),Vector3.one*size));
                if(matrices.Count==4200)break;
            }
            globals.grassInstances=matrices.ToArray();globals.grassBounds=new Bounds(origin+new Vector3(0,3,4),new Vector3(58,15,54));
            BuildTree(root,terrain,new Vector2(-18,5),1);
            BuildTree(root,terrain,new Vector2(22,31),.62f);
            BuildTree(root,terrain,new Vector2(-19,38),.57f);
            for(var i=0;i<85;i++)
            {
                var a=i/85f*Mathf.PI*2;var x=Mathf.Cos(a)*(17+i%4*.8f);var z=Mathf.Sin(a)*(13+i%3*.7f);
                if(z>17)continue;
                var flower=Mesh("White and gold meadow flowers "+i,root,samples["SM_FlowerCluster"],new Vector3(x,SampleTerrain(terrain,x,z),z),Vector3.one*(.8f+i%3*.15f),materials["Leaf"],materials["Flower"],materials["FlowerGold"]);
                flower.transform.localRotation=Quaternion.Euler(0,i*137,0);
            }
            var cliff=Group("Broad painted cliff sample and ledges",root);
            for(var i=0;i<15;i++)
            {
                var x=-28+i*4f;if(Mathf.Abs(x-4)<4)continue;
                var scale=new Vector3(2.25f,3.5f+(i%3)*.22f,3.5f);
                Mesh("Broad cliff plane "+i,cliff,samples["SM_Cliff"],new Vector3(x,-1.5f,30.2f+(i%3)*.55f),scale,materials["Rock"]);
            }
            for(var i=0;i<10;i++)
            {
                var x=-24+i*5.4f;var z=17.4f+(i%2)*1.2f;
                Mesh("Bank rock "+i,root,samples["SM_Cliff"],new Vector3(x,SampleTerrain(terrain,x,z)-.18f,z),new Vector3(.7f+i%2*.4f,.45f,.7f),materials["Rock"]);
            }
            Mesh("River depth surface",root,BuildRiverMesh(),Vector3.zero,Vector3.one,materials["River"]);
            Mesh("Waterfall directional UV",root,BuildFallMesh(),Vector3.zero,Vector3.one,materials["Waterfall"]);
            Mesh("Waterfall source",root,Quad(new Vector3(4,7.6f,32),6,6),Vector3.zero,Vector3.one,materials["River"]);
            var impact=Mesh("Impact foam ring",root,Quad(new Vector3(4,-1.08f,27),8,3),Vector3.zero,Vector3.one,materials["Foam"]);
            for(var i=0;i<4;i++)Mesh("Plunge rock "+i,root,samples["SM_Cliff"],new Vector3(1+i*2,-1.6f,26.5f+i%2),new Vector3(.55f,.6f,.6f),materials["Rock"]);
            foreach(var a in layout.anchors)
            {
                var actor=GameObject.CreatePrimitive(PrimitiveType.Capsule);actor.name="Copied fighter anchor "+a.name;actor.transform.SetParent(root,false);
                actor.transform.localPosition=a.localPosition+Vector3.up*1.5f;actor.transform.localScale=new Vector3(.85f,1.5f,.85f);
                actor.GetComponent<Renderer>().sharedMaterial=materials[a.name.StartsWith("Enemy")?"Enemy":"Player"];Object.DestroyImmediate(actor.GetComponent<Collider>());
            }
            foreach(var shot in layout.shots.Where(s=>new[]{"PlayerPlanning","EnemyPlanning","ReferencePlanningCandidate","PlayerAttack","EnemyAttack"}.Contains(s.name)))
                AddCamera("TG_"+shot.name,shot.position,shot.rotation,shot.fov);
            CameraAt("TG_MaterialOverview",new Vector3(25,16,-25),new Vector3(-1,4,17),57);
            CameraAt("TG_GrassTransition",new Vector3(-16,2,-8),new Vector3(-14,.7f,2),52);
            CameraAt("TG_CanopyBack",new Vector3(-26,5,9),new Vector3(-18,5,5),53);
            CameraAt("TG_WaterGrazing",new Vector3(15,1.8f,14),new Vector3(2,-1,25),56);
            CameraAt("TG_RockClose",new Vector3(-5,4,20),new Vector3(-9,3,30),50);
            CameraAt("TG_WaterDepth",new Vector3(18,14,15),new Vector3(8,-2,23),50);
            BuildDiagnostics(root);
            Lighting(false);EditorSceneManager.SaveScene(scene,LabScene);AssetDatabase.SaveAssets();
            Debug.Log("WP02 material lab scene generated: "+LabScene);
        }

        static Transform Group(string name,Transform parent){var t=new GameObject(name).transform;if(parent!=null)t.SetParent(parent,false);return t;}
        static Mesh SaveMesh(Mesh mesh)
        {
            var path=Shared+"/Meshes/"+mesh.name+".asset";var previous=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(previous==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            previous.Clear();previous.indexFormat=mesh.indexFormat;previous.vertices=mesh.vertices;previous.normals=mesh.normals;previous.uv=mesh.uv;previous.colors=mesh.colors;
            previous.subMeshCount=mesh.subMeshCount;for(var i=0;i<mesh.subMeshCount;i++)previous.SetTriangles(mesh.GetTriangles(i),i);
            previous.bounds=mesh.bounds;previous.UploadMeshData(false);EditorUtility.SetDirty(previous);Object.DestroyImmediate(mesh);return previous;
        }
        static GameObject Mesh(string name,Transform parent,Mesh mesh,Vector3 position,Vector3 scale,params Material[] mats)
        {
            var g=Group(name,parent).gameObject;g.transform.localPosition=position;g.transform.localScale=scale;
            g.AddComponent<MeshFilter>().sharedMesh=mesh;var r=g.AddComponent<MeshRenderer>();r.sharedMaterials=mats;
            return g;
        }
        static Material Mat(string key,string shader,Color color)
        {
            var path=Shared+"/Materials/TG_"+key+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);}else m.shader=Shader.Find(shader);
            m.name="TG_"+key;if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);m.enableInstancing=true;materials[key]=m;EditorUtility.SetDirty(m);return m;
        }
        static void MakeMaterials()
        {
            var ground=Mat("Ground","Teyvat/URP/Surface",Color.white);ground.SetFloat("_UseColorField",1);
            var rock=Mat("Rock","Teyvat/URP/Surface",new Color(.64f,.72f,.76f));rock.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Shared+"/Textures/TG_RockPainted.png"));rock.SetFloat("_TextureStrength",.35f);rock.SetFloat("_Mapping",1);rock.SetFloat("_WorldScale",.11f);rock.SetFloat("_MossAmount",.7f);rock.SetColor("_MossColor",Color.white);
            rock.SetFloat("_UseSharedRockStyle",1);
            Mat("Trunk","Teyvat/URP/Surface",new Color(.39f,.29f,.17f));
            foreach(var family in new[]{"Grass","Leaf","Flower","FlowerGold"})
            {
                var color=family=="Grass"?new Color(.56f,.72f,.23f):family=="Leaf"?new Color(.36f,.63f,.26f):family=="FlowerGold"?new Color(.98f,.73f,.17f):new Color(.97f,.96f,.78f);
                var m=Mat(family,"Teyvat/URP/Foliage",color);m.SetFloat("_Family",family=="Grass"?0:family=="Leaf"?1:2);
                m.SetFloat("_UseColorField",family=="Grass"?1:0);m.SetFloat("_AlphaClip",0);m.SetFloat("_Cull",0);
                m.SetColor("_RootColor",family=="Leaf"?new Color(.68f,.84f,.79f):Color.white);m.SetColor("_TipColor",family=="Leaf"?new Color(1.08f,1.12f,.80f):Color.white);
                m.SetFloat("_WindResponse",family=="Leaf"?.34f:family=="Grass"?1:.45f);
                m.SetFloat("_Wrap",.4f);m.SetFloat("_Transmission",.22f);
                m.SetColor("_CanopyShadeColor",new Color(.55f,.80f,.75f));
            }
            foreach(var name in new[]{"River","Waterfall","Foam"})
            {
                var m=Mat(name,"Teyvat/URP/Water",Color.white);m.SetFloat("_Mode",name=="River"?0:1);m.SetFloat("_UseSceneDepth",name=="River"?1:0);
                m.SetFloat("_FlowSpeed",name=="River"?.12f:.45f);m.SetFloat("_FallbackDepth",name=="River"?2:.2f);
                m.SetColor("_ShallowColor",new Color(.19f,.73f,.66f,.76f));m.SetColor("_DeepColor",new Color(.045f,.31f,.48f,.95f));
                m.SetFloat("_DepthRange",4);m.SetFloat("_FoamDistance",.6f);m.SetFloat("_Opacity",name=="Foam"?.50f:1);
                m.renderQueue=name=="River"?3000:name=="Waterfall"?3010:3020;
            }
            Mat("Player","Teyvat/URP/Surface",new Color(.22f,.43f,.72f));Mat("Enemy","Teyvat/URP/Surface",new Color(.70f,.32f,.23f));
        }

        static Texture2D Texture(string name,Func<int,int,Color> color,int size=128)
        {
            var path=Shared+"/Textures/"+name+".asset";var t=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(t==null){t=new Texture2D(size,size,TextureFormat.RGBA32,true,false){name=name};AssetDatabase.CreateAsset(t,path);}
            var c=new Color[size*size];for(var y=0;y<size;y++)for(var x=0;x<size;x++)c[y*size+x]=color(x,y);
            t.SetPixels(c);t.Apply();t.wrapMode=TextureWrapMode.Repeat;EditorUtility.SetDirty(t);return t;
        }
        static Texture2D BuildColorField()
        {
            var t=Texture("TG_WorldColorField",(x,y)=>
            {
                var n=Mathf.PerlinNoise(x*.022f+4,y*.022f+7)*.7f+Mathf.PerlinNoise(x*.13f+13,y*.13f+9)*.3f;
                var light=new Color(.59f,.73f,.28f);var dark=new Color(.32f,.50f,.16f);
                return Color.Lerp(dark,light,n);
            });t.wrapMode=TextureWrapMode.Clamp;return t;
        }
        static float H(float x,float z)
        {
            if(Mathf.Abs(x)<16&&z<13&&z>-12)return 0;
            var hills=(Mathf.PerlinNoise(x*.055f+9,z*.047f+4)-.45f)*1.7f;
            var front=17.7f+Mathf.Sin(x*.16f)*.8f;var back=28.6f+Mathf.Sin(x*.12f)*1.1f;
            if(z>front&&z<back){var t=Mathf.InverseLerp(front,back,z);return -1.4f-Mathf.Sin(t*Mathf.PI)*3.9f;}
            if(z>=back)return Mathf.Lerp(-2,7.7f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(back,back+2,z)))+hills*.25f;
            return hills*Mathf.Clamp01(Mathf.Max((Mathf.Abs(x)-16)/7,(z-12)/5));
        }
        static Terrain BuildTerrain(Transform parent)
        {
            var data=AssetDatabase.LoadAssetAtPath<TerrainData>(Shared+"/Terrain/TG_MaterialTerrain.asset");
            if(data==null){data=new TerrainData();AssetDatabase.CreateAsset(data,Shared+"/Terrain/TG_MaterialTerrain.asset");}
            data.heightmapResolution=257;data.size=new Vector3(56,20,66);var heights=new float[257,257];
            for(var z=0;z<257;z++)for(var x=0;x<257;x++)heights[z,x]=(H(-28+x/256f*56,-18+z/256f*66)+7)/20;
            data.SetHeights(0,0,heights);
            var textures=new[]{Texture("TG_GrassNeutral",(x,y)=>Color.white),Texture("TG_SoilPaint",(x,y)=>new Color(.64f,.56f,.35f)*(1+(Mathf.PerlinNoise(x*.04f,y*.04f)-.5f)*.08f)),AssetDatabase.LoadAssetAtPath<Texture2D>(Shared+"/Textures/TG_RockPainted.png")};
            var layers=new TerrainLayer[3];
            for(var i=0;i<3;i++)
            {
                var path=Shared+"/Terrain/TG_Layer"+i+".terrainlayer";layers[i]=AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if(layers[i]==null){layers[i]=new TerrainLayer();AssetDatabase.CreateAsset(layers[i],path);}
                layers[i].diffuseTexture=textures[i];layers[i].tileSize=new Vector2(i==2?8:6,i==2?8:6);layers[i].smoothness=0;EditorUtility.SetDirty(layers[i]);
            }
            data.terrainLayers=layers;data.alphamapResolution=128;var alpha=new float[128,128,3];
            for(var iz=0;iz<128;iz++)for(var ix=0;ix<128;ix++)
            {
                var x=-28+ix/127f*56;var z=-18+iz/127f*66;
                var slope=new Vector2(H(x+.3f,z)-H(x-.3f,z),H(x,z+.3f)-H(x,z-.3f)).magnitude/.6f;
                var stone=Mathf.SmoothStep(0,.9f,Mathf.InverseLerp(.7f,2,slope));
                var clearing=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,1,Mathf.Sqrt(x*x/(16*16)+z*z/(12*12))));
                var soil=clearing*Mathf.Clamp01((Mathf.PerlinNoise(x*.13f+5,z*.13f+3)-.4f)*1.2f)*(1-stone);
                alpha[iz,ix,0]=1-soil-stone;alpha[iz,ix,1]=soil;alpha[iz,ix,2]=stone;
            }
            data.SetAlphamaps(0,0,alpha);EditorUtility.SetDirty(data);
            var go=Terrain.CreateTerrainGameObject(data);go.name="Native terrain - shared grass color layer0";go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(-28,-7,-18);
            var t=go.GetComponent<Terrain>();t.materialTemplate=Mat("Terrain","Teyvat/URP/TerrainGrassland",Color.white);t.basemapDistance=100000;t.drawInstanced=true;t.heightmapPixelError=2;return t;
        }
        static float SampleTerrain(Terrain t,float x,float z){return t.terrainData.GetInterpolatedHeight((x+28)/56,(z+18)/66)-7;}
        static void BuildTree(Transform root,Terrain terrain,Vector2 pos,float scale)
        {
            var tree=Group("Authored tree sample "+pos,root);tree.localPosition=new Vector3(pos.x,SampleTerrain(terrain,pos.x,pos.y),pos.y);tree.localScale=Vector3.one*scale;
            Mesh("Rigid branching trunk",tree,samples["SM_Trunk"],Vector3.zero,Vector3.one*4.7f,materials["Trunk"]);
            var rng=new System.Random((int)pos.x+423);
            for(var i=0;i<25;i++)
            {
                var a=i*2.39996f;var radius=Mathf.Sqrt(i/25f)*3.0f;
                var p=new Vector3(Mathf.Cos(a)*radius,5.1f+(float)rng.NextDouble()*1.65f,Mathf.Sin(a)*radius*.85f);
                var leaves=Mesh("Fine leaf spray crown "+i,tree,samples["SM_LeafCluster"],p,Vector3.one*(1.8f+(float)rng.NextDouble()*.7f),materials["Leaf"]);
                leaves.transform.localRotation=Quaternion.Euler(0,i*137.5f,0);
            }
        }
        static Mesh Quad(Vector3 center,float width,float depth)
        {
            var v=new[]{center+new Vector3(-width/2,0,-depth/2),center+new Vector3(width/2,0,-depth/2),center+new Vector3(-width/2,0,depth/2),center+new Vector3(width/2,0,depth/2)};
            var m=new Mesh{name="TG_Quad_"+center.x+"_"+center.z+"_"+width+"_"+depth,vertices=v,uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one},triangles=new[]{0,2,1,1,2,3}};m.RecalculateNormals();return SaveMesh(m);
        }
        static Mesh BuildRiverMesh()
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var tr=new List<int>();
            for(var x=0;x<=64;x++)for(var z=0;z<=8;z++)
            {
                var px=-28+x/64f*56;var t=z/8f;var front=17.9f+Mathf.Sin(px*.16f)*.8f;var back=28.6f+Mathf.Sin(px*.12f)*1.1f;
                v.Add(new Vector3(px,-1.15f,Mathf.Lerp(front,back,t)));uv.Add(new Vector2(t,px*.1f));var k=x*9+z;
                if(x<64&&z<8)tr.AddRange(new[]{k,k+1,k+9,k+9,k+1,k+10});
            }
            var m=new Mesh{name="TG_RiverSample",vertices=v.ToArray(),uv=uv.ToArray(),triangles=tr.ToArray()};m.RecalculateNormals();return SaveMesh(m);
        }
        static Mesh BuildFallMesh()
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var tr=new List<int>();
            for(var r=0;r<=20;r++)for(var c=0;c<=10;c++)
            {
                var t=r/20f;var s=c/10f;v.Add(new Vector3(4+(s-.5f)*5.6f,Mathf.Lerp(7.7f,-1.14f,t),28.9f-.7f*Mathf.Sin(t*Mathf.PI*.5f)));uv.Add(new Vector2(s,t));var k=r*11+c;
                if(r<20&&c<10)tr.AddRange(new[]{k,k+1,k+11,k+1,k+12,k+11});
            }
            var m=new Mesh{name="TG_WaterfallSample",vertices=v.ToArray(),uv=uv.ToArray(),triangles=tr.ToArray()};m.RecalculateNormals();return SaveMesh(m);
        }
        static void AddCamera(string name,Vector3 position,Quaternion rotation,float fov)
        {
            var c=new GameObject(name).AddComponent<Camera>();c.transform.SetPositionAndRotation(position,rotation);c.fieldOfView=fov;c.nearClipPlane=.1f;c.farClipPlane=250;c.allowHDR=true;c.allowMSAA=true;c.enabled=name=="TG_ReferencePlanningCandidate";
            c.GetUniversalAdditionalCameraData().requiresDepthTexture=true;if(c.enabled)c.tag="MainCamera";
        }
        static void CameraAt(string name,Vector3 p,Vector3 focus,float fov){AddCamera(name,origin+p,Quaternion.LookRotation(focus-p),fov);}
        static void Lighting(bool neutral)
        {
            var sun=Object.FindFirstObjectByType<Light>();if(sun==null)sun=new GameObject("Material lab key light").AddComponent<Light>();
            sun.type=LightType.Directional;sun.intensity=neutral?1.1f:1.35f;sun.color=neutral?Color.white:new Color(1,.93f,.79f);sun.shadows=LightShadows.Soft;
            sun.transform.rotation=Quaternion.Euler(neutral?55:38,-43,0);RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=neutral?new Color(.60f,.60f,.60f):new Color(.55f,.69f,.82f);
            RenderSettings.ambientEquatorColor=neutral?new Color(.40f,.40f,.40f):new Color(.32f,.43f,.47f);
            RenderSettings.ambientGroundColor=neutral?new Color(.25f,.25f,.25f):new Color(.24f,.29f,.17f);
            RenderSettings.fog=!neutral;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=65;RenderSettings.fogEndDistance=220;RenderSettings.fogColor=new Color(.62f,.78f,.85f);
            var sky=AssetDatabase.LoadAssetAtPath<Material>(Shared+"/Settings/TG_MaterialSky.mat");
            if(sky==null)
            {
                sky=new Material(AssetDatabase.LoadAssetAtPath<Material>(Shared+"/Settings/TG_FantasyDaySky.mat")){name="TG_MaterialSky"};
                AssetDatabase.CreateAsset(sky,Shared+"/Settings/TG_MaterialSky.mat");
            }
            RenderSettings.skybox=sky;
        }
        static void BuildDiagnostics(Transform root)
        {
            Mesh("Imported axis X 2m Z 1m",root,samples["SM_AxisMarker"],new Vector3(22,SampleTerrain(Object.FindFirstObjectByType<Terrain>(),22,-10)+.03f,-10),Vector3.one,materials["Enemy"],materials["Player"],materials["Flower"]);
        }

        static void Finish()
        {
            if(!pending||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<nextTick)return;
            pending=false;EditorApplication.update-=Finish;
            try
            {
                var importPass=ValidateImports(Shared+"/Models/TG_MaterialSamples.manifest.json",output);
                globals.Apply();var windPass=ValidateWindAndBounds(globals,output);
                var waterPass=ValidateWaterDepth(output);
                var colorPass=ValidateGroundGrassColor(output);var coveragePass=ValidateInstancedCoverage(output);
                var files=new List<string>();var timer=System.Diagnostics.Stopwatch.StartNew();
                var cameras=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).OrderBy(c=>c.name).ToArray();
                foreach(var neutral in new[]{true,false})
                {
                    Lighting(neutral);globals.fixedTime=true;globals.fixedSeconds=1;globals.Apply();
                    foreach(var c in cameras){var file=(neutral?"Neutral/":"Afternoon/")+c.name+".png";Capture(c,output+"/"+file);files.Add(file);}
                }
                var motion=cameras.First(c=>c.name=="TG_GrassTransition");
                Shader.SetGlobalFloat("_TG_DebugAlbedo",1);Capture(motion,output+"/Diagnostics/GrassGround_Albedo.png");Shader.SetGlobalFloat("_TG_DebugAlbedo",0);
                foreach(var t in new[]{0f,1.5f,3f}){globals.fixedSeconds=t;globals.Apply();var file="Motion/Grass_t"+t.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+".png";Capture(motion,output+"/"+file);files.Add(file);}
                var waterCamera=cameras.First(c=>c.name=="TG_WaterGrazing");globals.fixedSeconds=0;globals.Apply();Capture(waterCamera,output+"/Motion/Water_t0.png");globals.fixedSeconds=1.5f;globals.Apply();Capture(waterCamera,output+"/Motion/Water_t1.5.png");
                timer.Stop();var shaders=AssetDatabase.FindAssets("t:Shader",new[]{Shared}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Shader>).Where(s=>s!=null).Select(s=>new ShaderCheck{name=s.name,supported=s.isSupported,errors=ShaderUtil.GetShaderMessages(s).Where(m=>m.severity.ToString()=="Error").Select(m=>m.message+" "+m.file+":"+m.line).ToArray()}).ToArray();
                var report=new LabReport{context="Isolated URP/Linear editor offscreen sample; static and fixed-time material checks, not combat playback or player-build performance.",unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName,pipeline=GraphicsSettings.currentRenderPipeline.name,colorSpace=PlayerSettings.colorSpace.ToString(),importsPass=importPass,windPass=windPass,waterDepthPass=waterPass,colorFieldPass=colorPass,instancingCoveragePass=coveragePass,shaders=shaders,shaderErrors=shaders.Any(s=>s.errors.Length>0||!s.supported),submittedGrassInstances=globals.submittedInstances,submittedGrassBatches=globals.submittedBatches,submissionFrames=globals.submissionFrames,renderers=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length,meshTrianglesExcludingTerrain=Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Sum(f=>(long)f.sharedMesh.triangles.Length/3),editorCaptureMilliseconds=timer.ElapsedMilliseconds,captures=files.ToArray(),limitations="Native terrain field supported within four layers, far basemap disabled and realtime lighting only. No SSAO/TAA/motion-vector pass, refraction, reflection camera or sorting-proof combat effects. RenderMeshInstanced grass explicit submissions are measured; no player-build timing claim."};
                File.WriteAllText(output+"/MaterialLabReport.json",JsonUtility.ToJson(report,true));
                globals.fixedTime=false;Lighting(false);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),LabScene);AssetDatabase.SaveAssets();
                if(!importPass||!windPass||!waterPass||!colorPass||!coveragePass||report.shaderErrors||globals.submittedInstances==0)throw new InvalidOperationException("Material lab checks failed; inspect reports before acceptance.");
                File.WriteAllText(output+"/COMPLETE.txt","WP02 URP Linear material sample generated and captured. Visual review and limitations recorded separately.\n");EditorApplication.Exit(0);
            }
            catch(Exception e){Fail(e);}
        }
        static void Capture(Camera camera,string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));var target=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var previous=RenderTexture.active;var texture=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try
            {
                camera.aspect=16f/9f;
                RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
                RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
                RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;Object.DestroyImmediate(texture);RenderTexture.ReleaseTemporary(target);}
        }
    }
}
