using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using FightingAllstar.EnvironmentArt;
namespace FightingAllstar.EditorTools
{
    public static partial class TeyvatLayoutR3Lab
    {
        [Serializable] sealed class IslandCheck {public string name;public Vector3 center;public float nativeTopAboveWater;public int wetSurroundSamples;public bool passed;}
        [Serializable] sealed class CascadeCheck {public string name;public int pathPoints;public float drop,totalHorizontalRun,waterJoinError;public bool descending,passed;}
        [Serializable] sealed class CompositionReport
        {
            public bool passed,originalCameraPosesRetained,independentTerrain,shadersSupported;
            public int missingScripts,grassHeadClearanceViolations,grassCellBoundsViolations,grassAnimatedBoundsViolations,grassVerticesSampled;
            public int sweepPositions;public float surroundingLandMin,surroundingLandMax;
            public IslandCheck[] islands;public CascadeCheck[] cascades;public string method,limitations;
        }
        static bool ValidateComposition()
        {
            var terrain=environment.GetComponentInChildren<Terrain>();var r=new CompositionReport{independentTerrain=AssetDatabase.GetAssetPath(terrain.terrainData)==R3+"/Terrain/TG_Landscape.asset",surroundingLandMin=float.PositiveInfinity,surroundingLandMax=float.NegativeInfinity,method="Actual native TerrainData, saved geometry and vegetation matrices. Five island centers and12 exterior wet-bed probes each; three descending6point cascade paths. Original17 camera transforms retained. Grass full rotated bothLOD bounds inside saved cells; sampled animated vertices (every200th placement,12 times,strength1) plus264 gameplay sweep-to-head AABB checks. Surrounding land sampled outside core and above water.",limitations="Wet-bed probes establish local separation, not fluid connectivity. Cascade paths and terrain support are layout evidence; water shader breakup, mist, erosion and polished foam are WP06. Static proxy head rays and sampled bounds do not replace actual animated combat or player performance."};
            r.islands=plan.islands.Select(i=>{
                var wet=0;var a=i.rotation*Mathf.Deg2Rad;for(var j=0;j<12;j++){var t=j/12f*Mathf.PI*2;var u=Mathf.Cos(t)*i.width*.83f;var v=Mathf.Sin(t)*i.depth*.83f;var x=i.x+u*Mathf.Cos(a)-v*Mathf.Sin(a);var z=i.z+u*Mathf.Sin(a)+v*Mathf.Cos(a);if(Ground(x,z)<plan.water_level-.08f)wet++;}
                var above=Ground(i.x,i.z)-plan.water_level;return new IslandCheck{name=i.name,center=origin+new Vector3(i.x,Ground(i.x,i.z),i.z),nativeTopAboveWater=above,wetSurroundSamples=wet,passed=above>.4f&&wet>=10};}).ToArray();
            r.cascades=plan.waterfalls.Select(f=>{
                var a=f.path_flat;var down=true;for(var j=3;j<a.Length;j+=3)if(a[j+1]>a[j-2]+.001f)down=false;var dy=a[1]-a[a.Length-2];var horizontal=Vector2.Distance(new Vector2(a[0],a[2]),new Vector2(a[a.Length-3],a[a.Length-1]));var error=Mathf.Abs(a[a.Length-2]-plan.water_level);return new CascadeCheck{name=f.name,pathPoints=a.Length/3,drop=dy,totalHorizontalRun=horizontal,waterJoinError=error,descending=down,passed=down&&dy>10&&horizontal>10&&error<.15f};}).ToArray();
            for(var z=-42;z<24;z+=2)for(var x=-54;x<=54;x+=2){if(Mathf.Abs(x)<20&&Mathf.Abs(z)<20)continue;var h=Ground(x,z);if(h<-.8f)continue;r.surroundingLandMin=Mathf.Min(r.surroundingLandMin,h);r.surroundingLandMax=Mathf.Max(r.surroundingLandMax,h);}
            r.originalCameraPosesRetained=layout.shots.All(s=>{var c=GameObject.Find("TG_"+s.name).GetComponent<Camera>();return (c.transform.position-s.position).sqrMagnitude<.000001f&&Quaternion.Angle(c.transform.rotation,s.rotation)<.001f&&Mathf.Abs(c.fieldOfView-s.fov)<.001f;});
            var cameraPoints=new List<Vector3>();var paths=new[]{new[]{"PlayerPlanning","EnemyPlanning"},new[]{"EnemyPlanning","PlayerPlanning"},new[]{"PlayerPlanning","PlayerAttack"},new[]{"PlayerAttack","PlayerContact"},new[]{"PlayerContact","PlayerPlanning"},new[]{"EnemyPlanning","EnemyAttack"},new[]{"EnemyAttack","EnemyContact"},new[]{"EnemyContact","EnemyPlanning"}};
            foreach(var path in paths)for(var j=0;j<33;j++)cameraPoints.Add(Orbit(layout.shots.First(s=>s.name==path[0]).position,layout.shots.First(s=>s.name==path[1]).position,origin,j/32f));r.sweepPositions=cameraPoints.Count;
            var counter=0;var sourceGeometry=meadow.data.species.Select(s=>R3PlantGeometry(s.meshLOD0)).ToArray();
            foreach(var cell in meadow.data.cells)foreach(var p in cell.placements)
            {
                var species=meadow.data.species[p.speciesIndex];var full=R3MeadowBounds(species.meshLOD0.bounds,p.matrix);full.Encapsulate(R3MeadowBounds(species.meshLOD1.bounds,p.matrix));var allowed=cell.worldBounds;allowed.Expand(.001f);if(!allowed.Contains(full.min)||!allowed.Contains(full.max))r.grassCellBoundsViolations++;
                var visible=R3MeadowBounds(sourceGeometry[p.speciesIndex].rawBounds,p.matrix);visible.Expand(new Vector3(.36f,.06f,.36f));var blocked=false;
                foreach(var cp in cameraPoints){foreach(var anchor in layout.anchors){var d=anchor.worldPosition+Vector3.up*2.5f-cp;if(visible.IntersectRay(new Ray(cp,d.normalized),out var hit)&&hit<d.magnitude-.16f){blocked=true;break;}}if(blocked)break;}
                if(blocked)r.grassHeadClearanceViolations++;
                if(counter++%200!=0)continue;
                var mesh=species.meshLOD0;var vertices=mesh.vertices;var colors=mesh.colors;var inverse=p.matrix.inverse;var bounds=mesh.bounds;
                for(var j=0;j<vertices.Length;j++)for(var t=0;t<12;t++)
                {
                    var world=p.matrix.MultiplyPoint3x4(vertices[j]);var wind=new Vector4(.93f,.36f,1,t*.7f);var displaced=world+TeyvatMaterialGlobals.Displacement(world,colors[j],wind,.65f);r.grassVerticesSampled++;if(!bounds.Contains(inverse.MultiplyPoint3x4(displaced)))r.grassAnimatedBoundsViolations++;
                }
            }
            r.missingScripts=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
            r.shadersSupported=environment.GetComponentsInChildren<Renderer>(true).SelectMany(v=>v.sharedMaterials).Where(m=>m!=null).All(m=>m.shader.isSupported&&!ShaderUtil.GetShaderMessages(m.shader).Any(e=>e.severity.ToString()=="Error"));
            r.passed=r.islands.All(i=>i.passed)&&r.cascades.All(c=>c.passed)&&r.surroundingLandMax-r.surroundingLandMin>2&&r.originalCameraPosesRetained&&r.independentTerrain&&r.shadersSupported&&r.missingScripts+r.grassCellBoundsViolations+r.grassAnimatedBoundsViolations+r.grassHeadClearanceViolations==0;
            File.WriteAllText(output+"/CompositionValidation.json",JsonUtility.ToJson(r,true));return r.passed;
        }
    }
}
