using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using FightingAllstar.EnvironmentArt;
using Object=UnityEngine.Object;
namespace FightingAllstar.EditorTools
{
    public static partial class TeyvatWaterLab
    {
        [Serializable] sealed class WaterMeshCheck{public string name,role;public int triangles,expected;public float boundsError,normalRangeError;public bool passed;public string[] errors;}
        [Serializable] sealed class CourseCheck{public string name;public int points,rows;public float crestError,endError,minimumU,maximumU,minimumV,maximumV;public bool descending,uvDownstream,poolsPresent,passed;}
        [Serializable] sealed class WaterReport
        {
            public bool passed,hashMatches,imports,originalCameraPoses,terrainReference,meadowReference,treeCompositionReference,actorsDry,shadersSupported,depthAfterOpaques,opaqueTextureDisabled,noExtraReflectionCameras,noRuntimeColliders,originalWaterRemoved;
            public int globalsOwners,missingScripts,trees,shrubs,waterRenderers,sourceMeshes,sourceTriangles,originalCameras;public float waterLevelError,maxAnchorGroundError;
            public WaterMeshCheck[] meshChecks;public CourseCheck[] courses;public string[] errors;
            public string method="Read-only imported FBX hash/counts/metre bounds/UV0/normal ranges/finite RGBA, actual river level, all17 retained camera poses/FOV, six native-ground anchors and horizontal river-triangle actor dry checks, current terrain/meadow/tree data references, one shared global owner and compiled shader dependencies. Three actual curtain UV row centers/arc direction/endpoints and six pool centers compared against frozen R3 plan. Pipeline copies opaque depth before transparents. GPU depth fixtures reported separately.";
            public string limitations="No fluid simulation, arbitrary transparent per-pixel sorting, animated combat/player performance or continuous camera proof. The unchanged cliff geometry is context; plan-conforming curtains/pools do not prove absence of every shelf intersection. GPU fixture proves depth algebra, not appearance. Mist uses intersecting fixed soft mesh patches, not camera-facing simulated particles.";
        }
        static bool ValidateWater()
        {
            var report=new WaterReport();var errors=new List<string>();var checks=new List<WaterMeshCheck>();var courses=new List<CourseCheck>();
            try
            {
                using(var sha=System.Security.Cryptography.SHA256.Create())report.hashMatches=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Water+"/Models/TG_WaterKit.fbx"))).Replace("-","").ToLowerInvariant()==manifest.fbx_sha256;
                foreach(var item in manifest.objects)
                {
                    var e=new List<string>();var m=meshes[item.name];var v=m.vertices;var n=m.normals;var uv=m.uv;var colors=m.colors;var minimum=new Vector3(item.bounds_unity_local_metres.min[0],item.bounds_unity_local_metres.min[1],item.bounds_unity_local_metres.min[2]);var maximum=new Vector3(item.bounds_unity_local_metres.max[0],item.bounds_unity_local_metres.max[1],item.bounds_unity_local_metres.max[2]);
                    var check=new WaterMeshCheck{name=item.name,role=item.role,triangles=m.triangles.Length/3,expected=item.expected_triangles_after_export,boundsError=Mathf.Max(WMax(m.bounds.min-minimum),WMax(m.bounds.max-maximum))};
                    if(check.triangles!=check.expected||check.boundsError>.002f)e.Add("Triangle count or metre/axis bounds differ from source.");
                    if(v.Length==0||n.Length!=v.Length||uv.Length!=v.Length||colors.Length!=v.Length)e.Add("Vertex/normal/UV0/color stream incomplete.");
                    if(uv.Any(p=>!Finite(p.x)||!Finite(p.y))||v.Any(p=>!Finite(p.x)||!Finite(p.y)||!Finite(p.z))||n.Any(p=>Mathf.Abs(p.magnitude-1)>.003f))e.Add("Non-finite stream or invalid imported unit normal.");
                    if(item.unity_normal_min!=null&&item.unity_normal_min.Length==3){var min=n.Aggregate(Vector3.Min);var max=n.Aggregate(Vector3.Max);check.normalRangeError=Mathf.Max(WMax(min-new Vector3(item.unity_normal_min[0],item.unity_normal_min[1],item.unity_normal_min[2])),WMax(max-new Vector3(item.unity_normal_max[0],item.unity_normal_max[1],item.unity_normal_max[2])));if(check.normalRangeError>.03f)e.Add("Imported normal range differs from authored source.");}
                    check.errors=e.ToArray();check.passed=e.Count==0;checks.Add(check);
                }
                report.imports=report.hashMatches&&checks.Count==41&&meshes.Count==41&&checks.All(c=>c.passed);report.sourceMeshes=meshes.Count;report.sourceTriangles=checks.Sum(c=>c.triangles);
                var terrain=environment.GetComponentInChildren<Terrain>();var meadow=environment.GetComponentInChildren<TeyvatMeadowRenderer>();var forest=environment.Find("TG WP05 trees and shrubs");
                report.terrainReference=AssetDatabase.GetAssetPath(terrain.terrainData)==R3+"/Terrain/TG_Landscape.asset";report.meadowReference=AssetDatabase.GetAssetPath(meadow.data)==R3+"/Meadow/TG_MeadowPlacement.asset";
                report.trees=forest.Cast<Transform>().Count(t=>t.name.StartsWith("TreeGroup_"));report.shrubs=forest.Cast<Transform>().Count(t=>t.name.StartsWith("ShrubGroup_"));report.treeCompositionReference=report.trees==120&&report.shrubs==70;
                report.originalCameras=layout.shots.Count(s=>GameObject.Find("TG_"+s.name)!=null);report.originalCameraPoses=layout.shots.All(s=>{var c=GameObject.Find("TG_"+s.name).GetComponent<Camera>();return Vector3.Distance(c.transform.position,s.position)<.0001f&&Quaternion.Angle(c.transform.rotation,s.rotation)<.001f&&Mathf.Abs(c.fieldOfView-s.fov)<.001f;});
                report.maxAnchorGroundError=layout.anchors.Max(a=>Mathf.Abs(terrain.SampleHeight(a.worldPosition)+terrain.transform.position.y-a.worldPosition.y));
                var river=waterRoot.Find("WP06_River").GetComponent<MeshFilter>();report.waterLevelError=river.sharedMesh.vertices.Max(v=>Mathf.Abs(v.y-plan.water_level));
                report.actorsDry=layout.anchors.All(a=>!PointOnRiver(river.sharedMesh,new Vector2(a.localPosition.x,a.localPosition.z)));
                foreach(var f in plan.waterfalls)
                {
                    var mesh=waterRoot.Find("WP06_Cascade_"+f.name).GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var uv=mesh.uv;var groups=Enumerable.Range(0,uv.Length).GroupBy(i=>Mathf.RoundToInt(uv[i].y*100000)).OrderBy(g=>g.Key).ToArray();
                    var first=groups.First();var last=groups.Last();var a=first.Select(i=>vertices[i]).Aggregate(Vector3.zero,(x,y)=>x+y)/first.Count();var b=last.Select(i=>vertices[i]).Aggregate(Vector3.zero,(x,y)=>x+y)/last.Count();
                    var expectedA=new Vector3(f.path_flat[0],f.path_flat[1]+.13f,f.path_flat[2]-.13f);var count=f.path_flat.Length;var expectedB=new Vector3(f.path_flat[count-3],f.path_flat[count-2]+.13f,f.path_flat[count-1]-.13f);
                    var down=true;var uvDown=true;Vector3? previous=null;foreach(var row in groups){var center=row.Select(i=>vertices[i]).Aggregate(Vector3.zero,(x,y)=>x+y)/row.Count();if(previous.HasValue){if(center.y>previous.Value.y+.001f)down=false;if(center.z>=previous.Value.z-.0001f)uvDown=false;}previous=center;}
                    var check=new CourseCheck{name=f.name,points=count/3,rows=groups.Length,crestError=Vector3.Distance(a,expectedA),endError=Vector3.Distance(b,expectedB),minimumU=uv.Min(t=>t.x),maximumU=uv.Max(t=>t.x),minimumV=uv.Min(t=>t.y),maximumV=uv.Max(t=>t.y),descending=down,uvDownstream=uvDown,poolsPresent=true};
                    for(var i=0;i<2;i++){var pool=waterRoot.Find("WP06_CatchPool_"+f.name+"_"+i);if(pool==null){check.poolsPresent=false;continue;}var pm=pool.GetComponent<MeshFilter>().sharedMesh;var py=i==0?f.path_flat[7]+.12f:plan.water_level+.045f;if(pm.vertices.Any(v=>Mathf.Abs(v.y-py)>.002f))check.poolsPresent=false;}
                    check.passed=down&&check.poolsPresent&&check.crestError<.03f&&check.endError<.03f&&check.minimumU==0&&Mathf.Abs(check.maximumU-1)<.0001f&&groups.Length==101&&check.minimumV==0&&Mathf.Abs(check.maximumV-1)<.0001f;courses.Add(check);
                }
                report.globalsOwners=Object.FindObjectsByType<TeyvatMaterialGlobals>(FindObjectsSortMode.None).Length;report.waterRenderers=waterRoot.GetComponentsInChildren<Renderer>().Length;
                report.missingScripts=SceneManager.GetActiveScene().GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
                report.shadersSupported=environment.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).All(m=>m.shader.isSupported&&!ShaderUtil.GetShaderMessages(m.shader).Any(e=>e.severity.ToString()=="Error"));
                var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;var so=new SerializedObject(pipeline);var renderer=(UniversalRendererData)so.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue;report.depthAfterOpaques=renderer.copyDepthMode==CopyDepthMode.AfterOpaques;report.opaqueTextureDisabled=!pipeline.supportsCameraOpaqueTexture;report.noExtraReflectionCameras=!Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Any(c=>c.name.ToLowerInvariant().Contains("reflection"));report.noRuntimeColliders=waterRoot.GetComponentsInChildren<Collider>(true).Length==0;
                report.originalWaterRemoved=!environment.Cast<Transform>().Any(t=>t.name=="R3_River_with_five_islands"||t.name.StartsWith("R3_Cascade_")||t.name.StartsWith("R3_CatchPool_"));
                report.passed=report.imports&&report.originalCameraPoses&&report.terrainReference&&report.meadowReference&&report.treeCompositionReference&&report.actorsDry&&report.waterLevelError<.001f&&report.maxAnchorGroundError<.02f&&courses.All(c=>c.passed)&&report.globalsOwners==1&&report.missingScripts==0&&report.shadersSupported&&report.depthAfterOpaques&&report.opaqueTextureDisabled&&report.noExtraReflectionCameras&&report.noRuntimeColliders&&report.originalWaterRemoved&&report.waterRenderers==40;
            }catch(Exception e){errors.Add(e.ToString());}
            report.meshChecks=checks.ToArray();report.courses=courses.ToArray();report.errors=errors.ToArray();report.passed&=errors.Count==0;File.WriteAllText(output+"/WaterValidation.json",JsonUtility.ToJson(report,true));return report.passed;
        }
        static bool PointOnRiver(Mesh mesh,Vector2 point)
        {
            var v=mesh.vertices;var t=mesh.triangles;for(var i=0;i<t.Length;i+=3){var a=new Vector2(v[t[i]].x,v[t[i]].z);var b=new Vector2(v[t[i+1]].x,v[t[i+1]].z);var c=new Vector2(v[t[i+2]].x,v[t[i+2]].z);var x=Cross(b-a,point-a);var y=Cross(c-b,point-b);var z=Cross(a-c,point-c);if((x>=0&&y>=0&&z>=0)||(x<=0&&y<=0&&z<=0))return true;}return false;
        }
        static float Cross(Vector2 a,Vector2 b){return a.x*b.y-a.y*b.x;}
        static float WMax(Vector3 v){return Mathf.Max(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z));}
        static bool Finite(float v){return !float.IsNaN(v)&&!float.IsInfinity(v);}
    }
}
