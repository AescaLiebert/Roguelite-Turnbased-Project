using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FightingAllstar.EditorTools
{
    public static partial class TeyvatFoundationBuilder
    {
        [Serializable] sealed class VisualShotCheck {public string name;public int fullyVisibleActors;public int environmentOccludedActors;public string[] geometryHits;}
        [Serializable] sealed class SweepCheck {public string name;public int samples;public int samplesWithBlockedCenter;public string[] geometryHits;}
        [Serializable] sealed class TerrainGroundSample {public string terrain;public float groundWorldY;public float signedHeightError;public bool withinStartingTolerance;}
        [Serializable] sealed class AnchorGroundCheck {public string anchor;public Vector3 worldPosition;public bool terrainCovered;public bool withinStartingTolerance;public TerrainGroundSample[] samples;}
        [Serializable] sealed class EnvelopeReport
        {
            public string method;public string limitations;public bool bothPlanningViewsPass;
            public VisualShotCheck[] shots;public SweepCheck[] sweeps;
            public string terrainGroundMethod;public float terrainGroundStartingTolerance;
            public bool allAnchorsWithinTerrainGroundStartingTolerance;public AnchorGroundCheck[] anchorGroundChecks;
        }

        static void ValidateVisualEnvelope(Scene scene,string output)
        {
            var roots=scene.GetRootGameObjects();
            var ts=roots.SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            var actors=ts.Where(t=>t.name.StartsWith("Proxy_")).ToArray();
            var foundationRoots=roots.Where(g=>g.name.StartsWith("TG Foundation")).ToArray();
            var terrains=foundationRoots.SelectMany(g=>g.GetComponentsInChildren<Terrain>()).Where(t=>t.enabled&&t.terrainData!=null).Distinct().ToArray();
            var added=new List<MeshCollider>();var geometry=new HashSet<Collider>();
            foreach(var root in foundationRoots)
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=filter.GetComponent<Renderer>();
                if(filter.sharedMesh==null||renderer==null||!renderer.enabled)continue;
                var collider=filter.GetComponent<Collider>();
                if(collider==null){var mesh=filter.gameObject.AddComponent<MeshCollider>();mesh.sharedMesh=filter.sharedMesh;added.Add(mesh);collider=mesh;}
                geometry.Add(collider);
            }
            foreach(var collider in foundationRoots.SelectMany(g=>g.GetComponentsInChildren<TerrainCollider>()))
                if(collider.enabled&&collider.terrainData!=null)geometry.Add(collider);
            try
            {
                var checks=new List<VisualShotCheck>();
                var cameras=ts.Select(t=>t.GetComponent<Camera>()).Where(c=>c!=null).ToArray();
                foreach(var camera in cameras)
                {
                    var hits=new List<string>();var fullyVisible=0;var occluded=0;camera.aspect=16f/9f;
                    foreach(var actor in actors)
                    {
                        var bounds=new Bounds(actor.position+Vector3.up*1.5f,new Vector3(1.2f,3,1.2f));
                        var all=true;
                        foreach(var point in Corners(bounds)) {var v=camera.WorldToViewportPoint(point);if(v.z<=camera.nearClipPlane||v.x<0||v.x>1||v.y<0||v.y>1)all=false;}
                        if(all)fullyVisible++;
                        var obstruction=FirstHit(geometry,camera.transform.position,actor.position+Vector3.up*1.6f);
                        if(obstruction!=null){occluded++;hits.Add(actor.name+" blocked by "+obstruction);}
                    }
                    checks.Add(new VisualShotCheck{name=camera.name,fullyVisibleActors=fullyVisible,environmentOccludedActors=occluded,geometryHits=hits.ToArray()});
                }
                var data=JsonUtility.FromJson<LayoutData>(File.ReadAllText(LayoutPath));
                var center=data.origin+Vector3.up*1.5f;
                var paths=new[]{new[]{"PlayerPlanning","EnemyPlanning"},new[]{"EnemyPlanning","PlayerPlanning"},
                    new[]{"PlayerPlanning","PlayerAttack"},new[]{"PlayerAttack","PlayerContact"},new[]{"PlayerContact","PlayerPlanning"},
                    new[]{"EnemyPlanning","EnemyAttack"},new[]{"EnemyAttack","EnemyContact"},new[]{"EnemyContact","EnemyPlanning"}};
                var sweeps=new List<SweepCheck>();
                foreach(var path in paths)
                {
                    var from=data.shots.First(s=>s.name==path[0]);var to=data.shots.First(s=>s.name==path[1]);
                    var hits=new HashSet<string>();var blocked=0;const int samples=33;
                    for(var i=0;i<samples;i++)
                    {
                        var position=OrbitPosition(from.position,to.position,data.origin,i/(float)(samples-1));
                        var hit=FirstHit(geometry,position,center);
                        if(hit!=null){blocked++;hits.Add(hit);}
                    }
                    sweeps.Add(new SweepCheck{name=path[0]+" -> "+path[1],samples=samples,samplesWithBlockedCenter=blocked,geometryHits=hits.OrderBy(s=>s).ToArray()});
                }
                var planning=checks.Where(c=>c.name=="TG_PlayerPlanning"||c.name=="TG_EnemyPlanning").ToArray();
                // Starting diagnostic tolerance: sample every copied anchor; correct terrain if its height differs
                // by more than 0.02 m, then recapture. This is a foundation check, not gameplay acceptance.
                const float terrainGroundTolerance=.02f;
                var groundChecks=SampleAnchorGround(data,terrains,terrainGroundTolerance);
                var report=new EnvelopeReport{method="Collider ray checks against enabled mesh visual modules (temporary mesh colliders where needed) and native TerrainCollider/TerrainData surfaces, actor bounds projected at 16:9, and spherical camera-path samples derived from the current ApplyOrbit position formula. Collider references are deduplicated.",
                    limitations="Static review proxies and common-origin spherical paths only. Actual attack pivots, animated actor bounds, pose changes, chosen orbit direction, FOV fitting, shake and ultimate playback require runtime integration. Sampling is a starting value; increase until detected obstructions stabilize when modifying layout.",
                    bothPlanningViewsPass=planning.Length==2&&planning.All(c=>c.fullyVisibleActors==6&&c.environmentOccludedActors==0),shots=checks.ToArray(),sweeps=sweeps.ToArray(),
                    terrainGroundMethod="Read-only TerrainData.GetInterpolatedHeight samples at each copied anchor inside each enabled foundation terrain's X/Z bounds, transformed to world Y. Signed error is terrain Y minus anchor Y. Every overlapping terrain sample must satisfy the 0.02 m starting tolerance; missing coverage fails this diagnostic. If a check fails, adjust terrain height or coverage and recapture. This does not test runtime grounding, animation or gameplay acceptance.",
                    terrainGroundStartingTolerance=terrainGroundTolerance,allAnchorsWithinTerrainGroundStartingTolerance=groundChecks.Length>0&&groundChecks.All(c=>c.withinStartingTolerance),anchorGroundChecks=groundChecks};
                File.WriteAllText(output+"/CameraEnvelopeReview.json",JsonUtility.ToJson(report,true));
            }
            finally{foreach(var c in added)UnityEngine.Object.DestroyImmediate(c);}
        }

        static AnchorGroundCheck[] SampleAnchorGround(LayoutData data,IEnumerable<Terrain> terrains,float tolerance)
        {
            var checks=new List<AnchorGroundCheck>();
            foreach(var anchor in data.anchors)
            {
                var samples=new List<TerrainGroundSample>();
                foreach(var terrain in terrains)
                {
                    var local=terrain.transform.InverseTransformPoint(anchor.worldPosition);var size=terrain.terrainData.size;
                    if(size.x<=0||size.z<=0||local.x<0||local.x>size.x||local.z<0||local.z>size.z)continue;
                    var localHeight=terrain.terrainData.GetInterpolatedHeight(local.x/size.x,local.z/size.z);
                    var groundY=terrain.transform.TransformPoint(new Vector3(local.x,localHeight,local.z)).y;
                    var error=groundY-anchor.worldPosition.y;
                    samples.Add(new TerrainGroundSample{terrain=terrain.name,groundWorldY=groundY,signedHeightError=error,withinStartingTolerance=Mathf.Abs(error)<=tolerance});
                }
                checks.Add(new AnchorGroundCheck{anchor=anchor.name,worldPosition=anchor.worldPosition,terrainCovered=samples.Count>0,
                    withinStartingTolerance=samples.Count>0&&samples.All(s=>s.withinStartingTolerance),samples=samples.ToArray()});
            }
            return checks.ToArray();
        }

        static IEnumerable<Vector3> Corners(Bounds b)
        {
            for(var x=-1;x<=1;x+=2)for(var y=-1;y<=1;y+=2)for(var z=-1;z<=1;z+=2)
                yield return b.center+Vector3.Scale(b.extents,new Vector3(x,y,z));
        }

        static string FirstHit(IEnumerable<Collider> geometry,Vector3 from,Vector3 to)
        {
            var d=to-from;var ray=new Ray(from,d.normalized);string result=null;var distance=d.magnitude-.1f;
            foreach(var c in geometry)if(c.Raycast(ray,out var hit,distance)){distance=hit.distance;result=c.name;}
            return result;
        }

        static Vector3 OrbitPosition(Vector3 from,Vector3 to,Vector3 pivot,float t)
        {
            var a=from-pivot;var b=to-pivot;
            var yawA=Mathf.Atan2(a.x,a.z)*Mathf.Rad2Deg;var yawB=Mathf.Atan2(b.x,b.z)*Mathf.Rad2Deg;
            var yaw=(yawA+Mathf.DeltaAngle(yawA,yawB)*t)*Mathf.Deg2Rad;
            var pitch=Mathf.Lerp(Mathf.Asin(a.y/a.magnitude),Mathf.Asin(b.y/b.magnitude),t);
            var r=Mathf.Lerp(a.magnitude,b.magnitude,t);
            return pivot+new Vector3(Mathf.Sin(yaw)*Mathf.Cos(pitch),Mathf.Sin(pitch),Mathf.Cos(yaw)*Mathf.Cos(pitch))*r;
        }
    }
}
