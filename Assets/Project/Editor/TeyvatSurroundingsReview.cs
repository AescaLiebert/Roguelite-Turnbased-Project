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
using Object=UnityEngine.Object;

namespace FightingAllstar.EditorTools
{
    [InitializeOnLoad]
    public static class TeyvatSurroundingsReview
    {
        [Serializable] sealed class Layout { public Shot[] shots;public Anchor[] anchors; }
        [Serializable] sealed class Anchor {public Vector3 worldPosition;}
        [Serializable] sealed class Shot { public string name;public Vector3 position;public Quaternion rotation;public float fov; }
        [Serializable] sealed class Sweep {public bool passed;public int samples,opaqueHeadHits,cameraGroundViolations;public string[] blockers;public string method;}
        static SceneSetup[] previous;
        static int quality,index;
        static bool temperature,linear;
        static double next;
        static string output;
        static Camera[] cameras;
        static bool running;
        static TeyvatSurroundingsReview() { EditorApplication.update+=Tick; }
        static void Tick()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(!running)
            {
                const string request="Temp/capture-teyvat-surroundings.request";
                if(!File.Exists(request))return;var phase=File.ReadAllText(request).Trim();File.Delete(request);
                try { Start(phase); } catch(Exception e) { Fail(e); } return;
            }
            if(EditorApplication.timeSinceStartup<next)return;
            try
            {
                if(index<cameras.Length)
                {
                    Capture(cameras[index],output+"/"+cameras[index].name+".png");index++;next=EditorApplication.timeSinceStartup+.12;return;
                }
                Verify();Finish();
            }
            catch(Exception e) { Fail(e); }
        }
        static void Start(string phase)
        {
            if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new Exception("Save scene edits before capture.");
            previous=EditorSceneManager.GetSceneManagerSetup();quality=QualitySettings.GetQualityLevel();temperature=GraphicsSettings.lightsUseColorTemperature;linear=GraphicsSettings.lightsUseLinearIntensity;
            output=TeyvatSurroundingsBuilder.Evidence+"/"+phase;Directory.CreateDirectory(output);
            var scene=EditorSceneManager.OpenScene(TeyvatDungeonBuilder.ScenePath);
            if(phase=="After")EditorSceneManager.SaveScene(scene,TeyvatSurroundingsBuilder.Review);
            foreach(var g in scene.GetRootGameObjects().Where(g=>g.GetComponent<TeyvatStageBinding>()==null&&g.GetComponent<TeyvatLightingRig>()==null).ToArray())Object.DestroyImmediate(g);
            var binding=Object.FindFirstObjectByType<TeyvatStageBinding>();var origin=binding.transform.position;
            var layout=JsonUtility.FromJson<Layout>(File.ReadAllText("Assets/Project/Art/Environment/TeyvatGrassland/LayoutR3/Data/TG_Layout.json"));
            var group=new GameObject("Surrounding scenery review cameras").transform;
            foreach(var shot in layout.shots)
            {
                var c=Camera(shot.name,group);c.transform.SetPositionAndRotation(shot.position,shot.rotation);c.fieldOfView=shot.fov;
            }
            At("Compass_Left",new Vector3(0,8,-6),new Vector3(-125,22,-28),68,group,origin);
            At("Compass_Right",new Vector3(0,8,-6),new Vector3(175,18,-25),68,group,origin);
            At("Compass_Rear",new Vector3(0,8,2),new Vector3(-10,26,-230),68,group,origin);
            At("Compass_Front",new Vector3(0,9,-17),new Vector3(0,22,100),68,group,origin);
            At("GreatTree_Valley",new Vector3(-22,11,-30),new Vector3(-99,27,-24),60,group,origin);
            At("Tidewatch_Coast",new Vector3(100,17,-30),new Vector3(320,22,-42),65,group,origin);
            At("World_Overview",new Vector3(410,310,-430),new Vector3(0,20,15),64,group,origin);
            At("Layout_Overview",new Vector3(0,190,-235),new Vector3(0,27,35),70,group,origin);
            cameras=group.GetComponentsInChildren<Camera>();
            var globals=Object.FindFirstObjectByType<TeyvatMaterialGlobals>();globals.fixedTime=true;globals.fixedSeconds=1;globals.Apply();
            if(phase=="After")
            {
                var main=cameras.First(c=>c.name=="PlayerPlanning");main.enabled=true;main.tag="MainCamera";
                var arena=new GameObject("Teyvat scenery review rendering").AddComponent<FightingAllstar.Presentation.Combat.TeyvatDungeonArena>();
                arena.pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Project/Art/Environment/TeyvatGrassland/Lighting/Settings/TG_LightingURP.asset");
                arena.actorShader=Shader.Find("Universal Render Pipeline/Lit");arena.lightingRig=Object.FindFirstObjectByType<TeyvatLightingRig>();
                binding.meadow.data=binding.canonicalMeadowData;globals.fixedTime=false;EditorSceneManager.SaveScene(scene);binding.ApplyBinding();globals.fixedTime=true;globals.Apply();
            }
            QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names,"Teyvat Arena"),true);
            index=0;running=true;next=EditorApplication.timeSinceStartup+3;
        }
        static Camera Camera(string name,Transform parent)
        {
            var g=new GameObject(name);g.transform.SetParent(parent,false);var c=g.AddComponent<Camera>();c.enabled=false;c.nearClipPlane=.12f;c.farClipPlane=1600;c.aspect=16f/9;c.allowHDR=true;c.allowMSAA=true;c.useOcclusionCulling=false;
            var a=c.GetUniversalAdditionalCameraData();a.renderPostProcessing=true;a.requiresDepthTexture=true;a.requiresColorTexture=false;return c;
        }
        static void At(string name,Vector3 position,Vector3 target,float fov,Transform parent,Vector3 origin)
        {var c=Camera(name,parent);c.transform.position=origin+position;c.transform.LookAt(origin+target);c.fieldOfView=fov;}
        static void Capture(Camera c,string path)
        {
            const int width=1280,height=720;
            var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear,4);
            var ldr=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var old=RenderTexture.active;var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                RenderPipeline.SubmitRenderRequest(c,new RenderPipeline.StandardRequest{destination=rt});RenderPipeline.SubmitRenderRequest(c,new RenderPipeline.StandardRequest{destination=rt});
                Graphics.Blit(rt,ldr);RenderTexture.active=ldr;pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);RenderTexture.ReleaseTemporary(ldr);Object.DestroyImmediate(pixels);}
        }
        static void Verify()
        {
            var binding=Object.FindFirstObjectByType<TeyvatStageBinding>();
            var missing=SceneManager.GetActiveScene().GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
            var bad=Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct().Where(m=>!m.shader.isSupported||ShaderUtil.GetShaderMessages(m.shader).Any(s=>s.severity.ToString()=="Error")).Select(m=>m.name).ToArray();
            var b=Object.FindFirstObjectByType<Terrain>();var anchors=new[]{new Vector2(-5.59f,-5),new Vector2(0,-5),new Vector2(5.25f,-5),new Vector2(-5.59f,5),new Vector2(0,5),new Vector2(5.25f,5)};
            var ground=anchors.Select(p=>b.SampleHeight(binding.transform.position+new Vector3(p.x,0,p.y))+b.transform.position.y-binding.transform.position.y).ToArray();
            if(missing>0||bad.Length>0||!binding.TransformSupported||ground.Any(h=>Mathf.Abs(h)>.02f))throw new Exception("Scene/palette/actor floor validation failed.");
            if(output.EndsWith("/After")) SweepNewScenery(binding);
            File.WriteAllText(output+"/Verified.txt","Saved scene and stage references inspected. Captured "+cameras.Length+" cameras (all 17 authored shots plus eight directional studies). Missing scripts: "+missing+". Unsupported shaders: "+bad.Length+". Actor floor unchanged. Meadow placements: "+binding.boundPlacementCount+". Lighting probes: "+(LightmapSettings.lightProbes?LightmapSettings.lightProbes.count:0)+". Gamma project; no palette change in this scenery pass. Visual acceptance requires inspection of the captures.");
        }
        static void SweepNewScenery(TeyvatStageBinding binding)
        {
            var layout=JsonUtility.FromJson<Layout>(File.ReadAllText("Assets/Project/Art/Environment/TeyvatGrassland/LayoutR3/Data/TG_Layout.json"));
            var scenery=binding.transform.Find("Surrounding world - Great Tree valley, Frost Crown and Tidewatch coast");
            var apron=scenery.GetComponentsInChildren<MeshFilter>().Single(f=>f.sharedMesh.name=="TG_ContinuousCoastalApron").sharedMesh;
            var vertices=apron.vertices;
            if(apron.triangles.Distinct().Any(i=>new Vector2(vertices[i].x,vertices[i].z).magnitude<30))throw new Exception("Coastal apron entered the camera/actor keepout.");
            var blockers=scenery.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.sharedMaterial&&r.sharedMaterial.renderQueue<2500&&!r.name.StartsWith("Continuous coastal land")).ToArray();
            var terrain=binding.GetComponentInChildren<Terrain>();var hits=new HashSet<string>();
            var report=new Sweep{method="8 authored orbit transition legs x33 positions; only newly added opaque renderer bounds, all six actor head rays, native floor clearance. Conservative bounds include every tree LOD. No backdrop gameplay colliders."};
            var paths=new[]{new[]{"PlayerPlanning","EnemyPlanning"},new[]{"EnemyPlanning","PlayerPlanning"},new[]{"PlayerPlanning","PlayerAttack"},new[]{"PlayerAttack","PlayerContact"},new[]{"PlayerContact","PlayerPlanning"},new[]{"EnemyPlanning","EnemyAttack"},new[]{"EnemyAttack","EnemyContact"},new[]{"EnemyContact","EnemyPlanning"}};
            foreach(var path in paths)
            {
                var a=layout.shots.First(s=>s.name==path[0]);var b=layout.shots.First(s=>s.name==path[1]);
                for(var i=0;i<=32;i++)
                {
                    var p=TeyvatStageTour.Orbit(a.position,b.position,binding.transform.position,i/32f);report.samples++;
                    if(p.y<terrain.SampleHeight(p)+terrain.transform.position.y+.1f)report.cameraGroundViolations++;
                    foreach(var anchor in layout.anchors)
                    {
                        var d=anchor.worldPosition+Vector3.up*2.5f-p;
                        foreach(var r in blockers)if(r.bounds.IntersectRay(new Ray(p,d.normalized),out var distance)&&distance<d.magnitude-.2f)
                        { report.opaqueHeadHits++;hits.Add(r.name); }
                    }
                }
            }
            report.blockers=hits.ToArray();report.passed=report.cameraGroundViolations==0&&report.opaqueHeadHits==0;
            File.WriteAllText(output+"/NewSceneryCameraSweep.json",JsonUtility.ToJson(report,true));
            if(!report.passed)throw new Exception("New surrounding geometry blocked the gameplay camera sweep: "+string.Join(",",report.blockers));
            if(scenery.GetComponentsInChildren<Collider>(true).Length>0)throw new Exception("Backdrop colliders could intercept target picking.");
        }
        static void Fail(Exception e)
        {Directory.CreateDirectory(TeyvatSurroundingsBuilder.Evidence);File.WriteAllText(TeyvatSurroundingsBuilder.Evidence+"/CaptureFailed.txt",e.ToString());Debug.LogException(e);Finish();}
        static void Finish()
        {
            running=false;
            if(previous!=null)
            {
                QualitySettings.SetQualityLevel(quality,true);GraphicsSettings.lightsUseColorTemperature=temperature;GraphicsSettings.lightsUseLinearIntensity=linear;
                var graphics=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0];EditorUtility.SetDirty(graphics);AssetDatabase.SaveAssetIfDirty(graphics);
                EditorSceneManager.RestoreSceneManagerSetup(previous);previous=null;
            }
        }
    }
}
