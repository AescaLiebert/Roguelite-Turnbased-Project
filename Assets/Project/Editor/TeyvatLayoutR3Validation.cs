using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace FightingAllstar.EditorTools
{
    public static partial class TeyvatLayoutR3Lab
    {
        [Serializable] sealed class ImportCheck
        {
            public string name,role;public bool passed;public int triangles,expectedTriangles,subMeshes,uvCount,colorCount;
            public Bounds actualBounds;public float boundsError,normalRangeError;public int sourceBoundaryEdges;public string[] errors;
        }
        [Serializable] sealed class ImportReport {public bool passed,hashMatches,axisMarkerPass;public string method,limitations;public ImportCheck[] meshes;public string[] errors;}
        static bool ValidateImports()
        {
            var errors=new List<string>();var checks=new List<ImportCheck>();
            var importer=(ModelImporter)AssetImporter.GetAtPath(Model);
            var hash=Hash(Model);var hashPass=hash==manifest.fbx_sha256;
            if(!hashPass)errors.Add("FBX bytes differ from authored manifest.");
            if(importer.globalScale!=1||!importer.bakeAxisConversion||importer.importNormals!=ModelImporterNormals.Import||!importer.isReadable)errors.Add("Import preset differs from accepted metre/custom-normal preset.");
            foreach(var item in manifest.objects)
            {
                var e=new List<string>();Mesh mesh;
                if(!meshes.TryGetValue(item.name,out mesh)){errors.Add("Missing mesh "+item.name);continue;}
                var b=mesh.bounds;var expected=item.bounds_unity_local_metres;
                var min=new Vector3(expected.min[0],expected.min[1],expected.min[2]);var max=new Vector3(expected.max[0],expected.max[1],expected.max[2]);
                var error=Mathf.Max(Max(b.min-min),Max(b.max-max));
                if(error>.002f)e.Add("Metre/axis bounds mismatch.");if(mesh.triangles.Length/3!=item.expected_triangles_after_export)e.Add("Triangle count mismatch.");
                if(mesh.uv.Length!=mesh.vertexCount)e.Add("Missing UV0.");if(mesh.normals.Length!=mesh.vertexCount)e.Add("Missing normals.");
                var colors=mesh.colors;if(colors.Length!=mesh.vertexCount||colors.Any(c=>Mathf.Abs(c.a-1)>.0041f||Mathf.Abs(c.r)>.0041f||Mathf.Abs(c.g)>.0041f||Mathf.Abs(c.b)>.0041f))e.Add("Rigid RGBA masks changed.");
                var normalError=0f;var normals=mesh.normals;
                if(normals.Length>0)
                {
                    var nmin=new Vector3(normals.Min(n=>n.x),normals.Min(n=>n.y),normals.Min(n=>n.z));var nmax=new Vector3(normals.Max(n=>n.x),normals.Max(n=>n.y),normals.Max(n=>n.z));
                    normalError=Mathf.Max(Max(nmin-new Vector3(item.unity_normal_min[0],item.unity_normal_min[1],item.unity_normal_min[2])),Max(nmax-new Vector3(item.unity_normal_max[0],item.unity_normal_max[1],item.unity_normal_max[2])));
                    if(normalError>.03f||normals.Any(n=>Mathf.Abs(n.magnitude-1)>.002f))e.Add("Imported custom normal range/length changed.");
                }
                if(!slots.ContainsKey(item.name)||slots[item.name].Length!=mesh.subMeshCount)e.Add("Material slot binding missing.");
                if(slots.ContainsKey(item.name)&&item.role!="diagnostic"&&slots[item.name].Any(n=>!manifest.objects.First(o=>o.name==item.name).material_slots.Contains(n)))e.Add("Unknown imported material slot; do not silently use the rock fallback.");
                if(item.role!="diagnostic"&&(item.welded_boundary_edges!=0||item.welded_nonmanifold_edges!=0))e.Add("Authored volume has open or nonmanifold welded edges.");
                checks.Add(new ImportCheck{name=item.name,role=item.role,passed=e.Count==0,triangles=mesh.triangles.Length/3,expectedTriangles=item.expected_triangles_after_export,subMeshes=mesh.subMeshCount,uvCount=mesh.uv.Length,colorCount=mesh.colors.Length,actualBounds=mesh.bounds,boundsError=error,normalRangeError=normalError,sourceBoundaryEdges=item.welded_boundary_edges,errors=e.ToArray()});
                if(e.Count>0)errors.Add(item.name+": "+string.Join(" ",e));
            }
            var marker=checks.FirstOrDefault(c=>c.name=="SM_R3_AxisMarker");var markerPass=marker!=null&&marker.passed;
            if(!markerPass)errors.Add("Asymmetric raw-mesh axis marker did not prove the coordinate conversion.");
            var report=new ImportReport{passed=errors.Count==0&&checks.Count==manifest.objects.Length,hashMatches=hashPass,axisMarkerPass=markerPass,method="Read-only FBX raw mesh bounds, topology, UV0, rigid numerical RGBA and custom-normal range checks against Blender's manifest in actual Unity-local coordinates. Asymmetric axis marker verifies the baked conversion; no prefab-node rotations are needed.",limitations="Ranges and triangle counts do not establish per-loop correspondence, silhouette quality, collision quality or performance. Authored welded boundary counts are reported separately; overlapping closed masonry pieces are not a Boolean union.",meshes=checks.ToArray(),errors=errors.ToArray()};
            File.WriteAllText(output+"/ImportValidation.json",JsonUtility.ToJson(report,true));return report.passed;
        }
        static float Max(Vector3 v){return Mathf.Max(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z));}
        static string Hash(string path){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        [Serializable] sealed class ViewCheck {public string name;public int fullyVisibleActors,blockedActorCenters;public string[] hits;}
        [Serializable] sealed class SweepCheck {public string name;public int samples,blocked;public string[] hits;}
        [Serializable] sealed class GroundCheck {public string name;public bool covered,passed;public float signedHeightError;}
        [Serializable] sealed class EnvelopeReport
        {
            public bool passed,bothPlanningViewsPass,allRegisteredCentersClear,allSampledPathsClear,allAnchorsPass,terrainHeightsMatchR3Source;
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
                foreach(var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c=>!c.name.StartsWith("TG_R3_")))
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
                var heightsMatch=heights.resolution==terrain.terrainData.heightmapResolution;
                var actual=terrain.terrainData.GetHeights(0,0,heights.resolution,heights.resolution);
                for(var z=0;z<heights.resolution;z++)for(var x=0;x<heights.resolution;x++)
                    if(Mathf.Abs(actual[z,x]*heights.size[1]+heights.position[1]-heights.heights_local_metres[z*heights.resolution+x])>.004f)heightsMatch=false;
                var planning=views.Where(v=>v.name=="TG_PlayerPlanning"||v.name=="TG_EnemyPlanning").ToArray();
                var cameraPass=planning.Length==2&&planning.All(v=>v.fullyVisibleActors==6&&v.blockedActorCenters==0)&&views.All(v=>v.blockedActorCenters==0)&&sweeps.All(s=>s.blocked==0);
                var groundPass=ground.All(g=>g.passed)&&heightsMatch;
                var report=new EnvelopeReport{passed=cameraPass&&groundPass,bothPlanningViewsPass=planning.Length==2&&planning.All(v=>v.fullyVisibleActors==6&&v.blockedActorCenters==0),allRegisteredCentersClear=views.All(v=>v.blockedActorCenters==0),allSampledPathsClear=sweeps.All(s=>s.blocked==0),allAnchorsPass=ground.All(g=>g.passed),terrainHeightsMatchR3Source=heightsMatch,terrainHeightStartingTolerance=.02f,method="Static conservative 3m actor bounds at copied WP01 anchors, actual LOD0 visual-mesh collider ray checks plus native TerrainCollider, all registry cameras and eight spherical paths with 33 samples each. All native height samples compared with the canonical R3 source within .004m quantization tolerance; original six anchor heights retained.",limitations="Review proxies and sampled common-origin camera paths only. Does not replace animated combat, moving attack pivots/FOV, both orbit directions, shake, physics gameplay or continuous path clearance. Tall foliage checked separately through R3 height/contact report and captures; this envelope uses static world geometry and native terrain.",cameras=views.ToArray(),paths=sweeps.ToArray(),anchors=ground.ToArray()};
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
