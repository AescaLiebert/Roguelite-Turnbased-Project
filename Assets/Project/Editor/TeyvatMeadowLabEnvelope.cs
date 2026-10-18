using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace FightingAllstar.EditorTools
{
    public static partial class TeyvatMeadowLab
    {
        [Serializable] sealed class ViewCheck {public string name;public int fullyVisibleActors,blockedActorCenters;public string[] hits;}
        [Serializable] sealed class SweepCheck {public string name;public int samples,blocked;public string[] hits;}
        [Serializable] sealed class GroundCheck {public string name;public bool covered,passed;public float signedHeightError;}
        [Serializable] sealed class EnvelopeReport
        {
            public bool passed,bothPlanningViewsPass,allRegisteredCentersClear,allSampledPathsClear,allAnchorsPass,terrainHeightsMatchWP01;
            public string method,limitations;public ViewCheck[] cameras;public SweepCheck[] paths;public GroundCheck[] anchors;public float terrainHeightStartingTolerance;
        }
        static Tuple<bool,bool> ValidateEnvelope()
        {
            var added=new List<MeshCollider>();var geometry=new List<Collider>();
            foreach(var filter in environment.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!filter.name.EndsWith("_LOD0"))continue;
                var collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;added.Add(collider);geometry.Add(collider);
            }
            var terrain=environment.GetComponentInChildren<Terrain>();geometry.Add(terrain.GetComponent<TerrainCollider>());
            try
            {
                Physics.SyncTransforms();var views=new List<ViewCheck>();
                // The two added botanical macro cameras frame petals/roots, not fighters.
                // Keep the original nineteen WP03 camera checks unchanged in scope.
                foreach(var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c=>c.name!="TG_MeadowClose"&&c.name!="TG_MeadowFlowers"))
                {
                    camera.aspect=16f/9f;var visible=0;var hits=new List<string>();
                    foreach(var anchor in layout.anchors)
                    {
                        var b=new Bounds(anchor.worldPosition+Vector3.up*1.5f,new Vector3(1.2f,3,1.2f));var all=true;
                        for(var x=-1;x<=1;x+=2)for(var y=-1;y<=1;y+=2)for(var z=-1;z<=1;z+=2)
                        {var p=camera.WorldToViewportPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)));if(p.z<=camera.nearClipPlane||p.x<0||p.x>1||p.y<0||p.y>1)all=false;}
                        if(all)visible++;var hit=Hit(geometry,camera.transform.position,anchor.worldPosition+Vector3.up*1.6f);if(hit!=null)hits.Add(anchor.name+" by "+hit);
                    }
                    views.Add(new ViewCheck{name=camera.name,fullyVisibleActors=visible,blockedActorCenters=hits.Count,hits=hits.ToArray()});
                }
                var paths=new[]{new[]{"PlayerPlanning","EnemyPlanning"},new[]{"EnemyPlanning","PlayerPlanning"},new[]{"PlayerPlanning","PlayerAttack"},new[]{"PlayerAttack","PlayerContact"},new[]{"PlayerContact","PlayerPlanning"},new[]{"EnemyPlanning","EnemyAttack"},new[]{"EnemyAttack","EnemyContact"},new[]{"EnemyContact","EnemyPlanning"}};
                var sweeps=new List<SweepCheck>();
                foreach(var path in paths)
                {
                    var from=layout.shots.First(s=>s.name==path[0]);var to=layout.shots.First(s=>s.name==path[1]);var hits=new List<string>();
                    for(var i=0;i<33;i++){var p=Orbit(from.position,to.position,layout.origin,i/32f);var hit=Hit(geometry,p,layout.origin+Vector3.up*1.5f);if(hit!=null)hits.Add(hit);}
                    sweeps.Add(new SweepCheck{name=path[0]+" -> "+path[1],samples=33,blocked=hits.Count,hits=hits.Distinct().ToArray()});
                }
                var ground=new List<GroundCheck>();
                foreach(var anchor in layout.anchors)
                {
                    var p=terrain.transform.InverseTransformPoint(anchor.worldPosition);var size=terrain.terrainData.size;var covered=p.x>=0&&p.z>=0&&p.x<=size.x&&p.z<=size.z;
                    var error=covered?terrain.transform.TransformPoint(new Vector3(p.x,terrain.terrainData.GetInterpolatedHeight(p.x/size.x,p.z/size.z),p.z)).y-anchor.worldPosition.y:float.MaxValue;
                    ground.Add(new GroundCheck{name=anchor.name,covered=covered,passed=covered&&Mathf.Abs(error)<=.02f,signedHeightError=error});
                }
                var original=AssetDatabase.LoadAssetAtPath<TerrainData>(Foundation+"/Terrain/TG_ReferenceLand.asset");var heightsMatch=original.heightmapResolution==terrain.terrainData.heightmapResolution;
                if(heightsMatch)
                {
                    var a=original.GetHeights(0,0,original.heightmapResolution,original.heightmapResolution);var b=terrain.terrainData.GetHeights(0,0,original.heightmapResolution,original.heightmapResolution);
                    for(var z=0;z<original.heightmapResolution;z++)for(var x=0;x<original.heightmapResolution;x++)if(a[z,x]!=b[z,x])heightsMatch=false;
                }
                var planning=views.Where(v=>v.name=="TG_PlayerPlanning"||v.name=="TG_EnemyPlanning").ToArray();
                var cameraPass=planning.Length==2&&planning.All(v=>v.fullyVisibleActors==6&&v.blockedActorCenters==0)&&views.All(v=>v.blockedActorCenters==0)&&sweeps.All(s=>s.blocked==0);
                var groundPass=ground.All(g=>g.passed)&&heightsMatch;
                var report=new EnvelopeReport{passed=cameraPass&&groundPass,bothPlanningViewsPass=planning.Length==2&&planning.All(v=>v.fullyVisibleActors==6&&v.blockedActorCenters==0),allRegisteredCentersClear=views.All(v=>v.blockedActorCenters==0),allSampledPathsClear=sweeps.All(s=>s.blocked==0),allAnchorsPass=ground.All(g=>g.passed),terrainHeightsMatchWP01=heightsMatch,terrainHeightStartingTolerance=.02f,method="Static conservative 3m actor bounds at copied WP01 anchors, actual LOD0 visual-mesh collider ray checks plus native TerrainCollider, all registry cameras and eight spherical paths with 33 samples each. All native TerrainData height samples compared exactly with WP01 r2.",limitations="Review proxies and sampled common-origin camera paths only. Does not replace animated combat, moving attack pivots/FOV, both orbit directions, shake, physics gameplay or continuous path clearance. Water and placeholder foliage are context outside the geometry prefab.",cameras=views.ToArray(),paths=sweeps.ToArray(),anchors=ground.ToArray()};
                File.WriteAllText(output+"/CameraEnvelopeReview.json",JsonUtility.ToJson(report,true));return Tuple.Create(cameraPass,groundPass);
            }
            finally{foreach(var collider in added)Object.DestroyImmediate(collider);}
        }
        static string Hit(IEnumerable<Collider> colliders,Vector3 from,Vector3 to)
        {
            var d=to-from;var ray=new Ray(from,d.normalized);var distance=d.magnitude-.1f;string result=null;
            foreach(var collider in colliders){RaycastHit hit;if(collider.Raycast(ray,out hit,distance)){distance=hit.distance;result=collider.name;}}return result;
        }
        static Vector3 Orbit(Vector3 from,Vector3 to,Vector3 center,float t)
        {
            var a=from-center;var b=to-center;var yawA=Mathf.Atan2(a.x,a.z)*Mathf.Rad2Deg;var yawB=Mathf.Atan2(b.x,b.z)*Mathf.Rad2Deg;var yaw=(yawA+Mathf.DeltaAngle(yawA,yawB)*t)*Mathf.Deg2Rad;
            var pitch=Mathf.Lerp(Mathf.Asin(a.y/a.magnitude),Mathf.Asin(b.y/b.magnitude),t);var radius=Mathf.Lerp(a.magnitude,b.magnitude,t);
            return center+new Vector3(Mathf.Sin(yaw)*Mathf.Cos(pitch),Mathf.Sin(pitch),Mathf.Cos(yaw)*Mathf.Cos(pitch))*radius;
        }
    }
}

