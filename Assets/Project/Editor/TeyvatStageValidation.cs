using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using FightingAllstar.EnvironmentArt;
using Object=UnityEngine.Object;
namespace FightingAllstar.EditorTools
{
    public static partial class TeyvatStageLab
    {
        [Serializable] sealed class StageReport
        {
            public bool passed,originalCameras,actorFloor,oneClock,shaders,stagePrefabReferences,translationBinding,canonicalMeadowUnchanged;
            public int cameras,trees,shrubs,flowerAccents,missingScripts,sprayEmitters,sprayRenderers,contactMeshes;
            public float maxGroundError;public ContactCheck[] falls;public string[] errors;
            public string method="Serialized stage dependency and17 original-camera comparison; six actual TerrainData anchor samples; actual imported water vertices checked against corrected native ground and closed opaque LOD0 rocks using odd/even backface ray parity in three directions. Excludes the first/last2% of curtain length and outer4% width for intentional joins. Translation rebase tested twice then restored, canonical meadow object and first-placement matrix unchanged. All tests use review proxies, not gameplay character animation.";
            public string limitations="No fluid simulation or arbitrary transparent sorting. Samples test curtain core points, not an exhaustive triangle/triangle Boolean proof. Final lighting, real combat transitions and Windows player performance remain WP08/WP09.";
        }
        [Serializable] sealed class ContactCheck{public string name;public int samples,insideRock,belowNativeTerrain;public float minimumNativeClearance;public string[] occluders;public bool passed;}
        static bool ValidateStage()
        {
            var r=new StageReport();var errors=new List<string>();
            try
            {
                var terrain=environment.GetComponentInChildren<Terrain>();r.maxGroundError=layout.anchors.Max(a=>Mathf.Abs(terrain.SampleHeight(a.worldPosition)+terrain.transform.position.y-a.worldPosition.y));r.actorFloor=r.maxGroundError<.02f;
                r.cameras=layout.shots.Count(s=>GameObject.Find("TG_"+s.name));r.originalCameras=layout.shots.All(s=>{var c=GameObject.Find("TG_"+s.name).GetComponent<Camera>();return Vector3.Distance(c.transform.position,s.position)<.0001f&&Quaternion.Angle(c.transform.rotation,s.rotation)<.001f&&Mathf.Abs(c.fieldOfView-s.fov)<.001f;});
                r.oneClock=Object.FindObjectsByType<TeyvatMaterialGlobals>(FindObjectsSortMode.None).Length==1;
                var forest=environment.Find("TG WP05 trees and shrubs");r.trees=forest.Cast<Transform>().Count(t=>t.name.StartsWith("TreeGroup_"));r.shrubs=forest.Cast<Transform>().Count(t=>t.name.StartsWith("ShrubGroup_"));r.flowerAccents=environment.Find("WP07 seeded flower accents").childCount;
                r.missingScripts=SceneManager.GetActiveScene().GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
                var mats=environment.GetComponentsInChildren<Renderer>(true).SelectMany(v=>v.sharedMaterials).Where(m=>m).Distinct().ToArray();r.shaders=mats.All(m=>m.shader&&m.shader.isSupported&&!ShaderUtil.GetShaderMessages(m.shader).Any(e=>e.severity.ToString()=="Error"));
                foreach(var m in mats.Where(m=>m.shader))foreach(var message in ShaderUtil.GetShaderMessages(m.shader).Where(e=>e.severity.ToString()=="Error"))errors.Add(m.name+": "+message.message);
                r.contactMeshes=environment.GetComponentsInChildren<MeshFilter>(true).Count(f=>f.sharedMesh&&f.sharedMesh.name.StartsWith("SM_WP07_"));
                var spray=waterRoot.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("Impact spray")).ToArray();r.sprayRenderers=spray.Length;r.sprayEmitters=spray.Sum(f=>f.sharedMesh.vertexCount/4);
                r.falls=ValidateContacts();
                var binding=environment.GetComponent<TeyvatStageBinding>();var source=binding.canonicalMeadowData;var first=source.cells.First(c=>c!=null&&c.placements.Length>0);var matrix=first.placements[0].matrix;var before=JsonUtility.ToJson(source);var old=environment.position;var shift=new Vector3(11,2,-7);environment.position=old+shift;binding.ApplyBinding();
                var target=binding.RuntimeMeadowData.cells.First(c=>c!=null&&c.placements.Length>0).placements[0].matrix;var moved=new Vector3(target.m03-matrix.m03,target.m13-matrix.m13,target.m23-matrix.m23);var clone=binding.RuntimeMeadowData;binding.ApplyBinding();r.translationBinding=binding.TransformSupported&&Vector3.Distance(shift,moved)<.001f&&binding.RuntimeMeadowData==clone;
                environment.position=old;binding.ApplyBinding();r.canonicalMeadowUnchanged=before==JsonUtility.ToJson(source);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(StagePrefab);r.stagePrefabReferences=prefab&&prefab.GetComponentsInChildren<Camera>(true).Length==0&&prefab.GetComponentsInChildren<Light>(true).Length==0&&prefab.GetComponent<TeyvatStageBinding>().canonicalMeadowData==source;
                r.passed=r.actorFloor&&r.originalCameras&&r.oneClock&&r.shaders&&r.missingScripts==0&&r.translationBinding&&r.canonicalMeadowUnchanged&&r.stagePrefabReferences&&r.trees==120&&r.shrubs==70&&r.sprayRenderers==6&&r.falls.All(c=>c.passed);
            }catch(Exception e){errors.Add(e.ToString());}
            r.errors=errors.ToArray();r.passed&=errors.Count==0;File.WriteAllText(output+"/StageValidation.json",JsonUtility.ToJson(r,true));return r.passed;
        }
        static ContactCheck[] ValidateContacts()
        {
            var rig=Group("Temporary actual-geometry contact probes");var probes=new List<MeshCollider>();var was=Physics.queriesHitBackfaces;Physics.queriesHitBackfaces=true;
            try
            {
                var geo=environment.Find("R3 authored cliff terraces islands and prop hierarchy");
                foreach(var f in geo.GetComponentsInChildren<MeshFilter>(true).Where(f=>!f.name.EndsWith("_LOD1")&&f.sharedMesh&&f.sharedMesh.bounds.max.z>50&&f.sharedMesh.bounds.min.z<100))
                {var g=Group(f.sharedMesh.name,rig);g.SetPositionAndRotation(f.transform.position,f.transform.rotation);g.localScale=f.transform.lossyScale;var c=g.gameObject.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;probes.Add(c);}
                Physics.SyncTransforms();var terrain=environment.GetComponentInChildren<Terrain>();var checks=new List<ContactCheck>();
                foreach(var fall in plan.waterfalls)
                {
                    var f=waterRoot.GetComponentsInChildren<MeshFilter>().First(x=>x.name=="WP06_Cascade_"+fall.name);var m=f.sharedMesh;var v=m.vertices;var uv=m.uv;var report=new ContactCheck{name=fall.name,minimumNativeClearance=1000};var names=new HashSet<string>();
                    for(var i=0;i<v.Length;i++)
                    {
                        if(uv[i].x<.04f||uv[i].x>.96f||uv[i].y<.02f||uv[i].y>.98f)continue;var p=f.transform.TransformPoint(v[i]);report.samples++;
                        var clearance=p.y-(terrain.SampleHeight(p)+terrain.transform.position.y);report.minimumNativeClearance=Mathf.Min(clearance,report.minimumNativeClearance);if(clearance<-.025f)report.belowNativeTerrain++;
                        var inside=false;foreach(var c in probes){if(!c.bounds.Contains(p))continue;if(InsideClosedMesh(c,p)){inside=true;names.Add(c.name);}}if(inside)report.insideRock++;
                    }
                    report.occluders=names.ToArray();report.passed=report.insideRock==0&&report.belowNativeTerrain==0;checks.Add(report);
                }
                return checks.ToArray();
            }finally{Object.DestroyImmediate(rig.gameObject);Physics.queriesHitBackfaces=was;}
        }
        static bool InsideClosedMesh(MeshCollider collider,Vector3 point)
        {
            var votes=0;foreach(var direction in new[]{new Vector3(.973f,.131f,.183f).normalized,new Vector3(-.153f,.982f,.107f).normalized,new Vector3(.127f,.151f,-.98f).normalized})
            {
                var p=point;var hits=0;for(var j=0;j<24;j++){if(!collider.Raycast(new Ray(p,direction),out var hit,600))break;hits++;p=hit.point+direction*.002f;}if(hits%2==1)votes++;
            }
            return votes>=2;
        }
        static Vector3 Orbit(Vector3 a,Vector3 b,float t)
        {
            return TeyvatStageTour.Orbit(a,b,origin,t);
        }
        [Serializable] sealed class SweepReport{public int positions,cameraGroundViolations,opaqueHeadHits;public bool passed;public string method,limitations;public string[] hits;}
        static void CaptureTransitions()
        {
            var paths=new[]{new[]{"PlayerPlanning","EnemyPlanning"},new[]{"EnemyPlanning","PlayerPlanning"},new[]{"PlayerPlanning","PlayerAttack"},new[]{"PlayerAttack","PlayerContact"},new[]{"PlayerContact","PlayerPlanning"},new[]{"EnemyPlanning","EnemyAttack"},new[]{"EnemyAttack","EnemyContact"},new[]{"EnemyContact","EnemyPlanning"}};
            var temp=Group("Temporary camera transition review").gameObject.AddComponent<Camera>();temp.enabled=false;temp.nearClipPlane=.1f;temp.farClipPlane=800;temp.GetUniversalAdditionalCameraData().requiresDepthTexture=true;
            var terrain=environment.GetComponentInChildren<Terrain>();var r=new SweepReport{method="Eight declared orbit transition legs,33 positions each; native ground camera clearance and6 proxy-head opaque bounds ray probes. Rendered4 samples perleg.",limitations="AABB probes are conservative foliage/rock silhouettes; they do not model leaf alpha or actual character animation. Recorded orbit paths are a review harness; the game's runtime controller is unchanged."};var hits=new HashSet<string>();
            try
            {
                var blockers=environment.GetComponentsInChildren<MeshRenderer>(true).Where(v=>v.enabled&&v.sharedMaterial&&v.sharedMaterial.renderQueue<2500&&!v.name.EndsWith("_LOD1")).ToArray();
                for(var k=0;k<paths.Length;k++)
                {
                    var a=layout.shots.First(s=>s.name==paths[k][0]);var b=layout.shots.First(s=>s.name==paths[k][1]);
                    for(var j=0;j<33;j++)
                    {
                        var t=j/32f;var p=Orbit(a.position,b.position,t);temp.transform.SetPositionAndRotation(p,Quaternion.Slerp(a.rotation,b.rotation,t));temp.fieldOfView=Mathf.Lerp(a.fov,b.fov,t);r.positions++;if(p.y<terrain.SampleHeight(p)+terrain.transform.position.y+.1f)r.cameraGroundViolations++;
                        foreach(var anchor in layout.anchors){var d=anchor.worldPosition+Vector3.up*2.5f-p;foreach(var blocker in blockers){if(blocker.bounds.IntersectRay(new Ray(p,d.normalized),out var distance)&&distance<d.magnitude-.2f){r.opaqueHeadHits++;hits.Add(blocker.name);}}}
                        if(j==0||j==10||j==21||j==32)Capture(temp,output+"/Transitions/Leg"+k+"_"+j.ToString("D2")+".png",960,540);
                    }
                }
                r.hits=hits.ToArray();r.passed=r.cameraGroundViolations==0&&r.opaqueHeadHits==0;File.WriteAllText(output+"/CameraSweep.json",JsonUtility.ToJson(r,true));
            }finally{Object.DestroyImmediate(temp.gameObject);}
        }
    }
}
