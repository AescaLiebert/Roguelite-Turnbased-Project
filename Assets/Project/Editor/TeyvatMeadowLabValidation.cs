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
    public static partial class TeyvatMeadowLab
    {
        [Serializable] sealed class BoundData {public float[] min,max;}
        [Serializable] sealed class SourceObject {public string name,role;public int expected_triangles_after_export;public BoundData expected_unity_bounds_metres;public float[] expected_unity_normal_min,expected_unity_normal_max;public string[] material_slots;}
        [Serializable] sealed class SourceManifest {public string fbx_sha256;public SourceObject[] objects;}
        [Serializable] sealed class ImportResult {public string name;public bool passed;public int triangles,vertices,submeshes;public float boundsError,normalError;public string[] errors;}
        [Serializable] sealed class ImportSummary {public bool passed,hashMatch;public ImportResult[] meshes;public string method;}
        static float Max(Vector3 v){return Mathf.Max(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z));}
        static string Hash(string path){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        static bool ValidateImports()
        {
            var source=JsonUtility.FromJson<SourceManifest>(File.ReadAllText(Meadow+"/Models/TG_MeadowKit.manifest.json"));var results=new List<ImportResult>();
            foreach(var item in source.objects)
            {
                var e=new List<string>();if(!meshes.TryGetValue(item.name,out var mesh)){results.Add(new ImportResult{name=item.name,errors=new[]{"Missing mesh"}});continue;}
                var b=item.expected_unity_bounds_metres;var expectedMin=new Vector3(b.min[0],b.min[1],b.min[2]);var expectedMax=new Vector3(b.max[0],b.max[1],b.max[2]);
                var be=Mathf.Max(Max(mesh.bounds.min-expectedMin),Max(mesh.bounds.max-expectedMax));if(be>.002f)e.Add("Metre/axis bounds differ.");
                if(mesh.triangles.Length/3!=item.expected_triangles_after_export)e.Add("Triangle count differs.");
                if(mesh.uv.Length!=mesh.vertexCount||mesh.normals.Length!=mesh.vertexCount||mesh.colors.Length!=mesh.vertexCount)e.Add("UV0/normal/color stream missing.");
                if(mesh.colors.Any(c=>Mathf.Abs(c.a-1)>.0041f||Mathf.Abs(c.g)>.0041f||c.r<-.0041f||c.r>1.0041f||c.b<-.0041f||c.b>1.0041f))e.Add("Numerical wind RGBA invalid.");
                var normals=mesh.normals;var nmin=new Vector3(normals.Min(n=>n.x),normals.Min(n=>n.y),normals.Min(n=>n.z));var nmax=new Vector3(normals.Max(n=>n.x),normals.Max(n=>n.y),normals.Max(n=>n.z));
                var ne=Mathf.Max(Max(nmin-new Vector3(item.expected_unity_normal_min[0],item.expected_unity_normal_min[1],item.expected_unity_normal_min[2])),Max(nmax-new Vector3(item.expected_unity_normal_max[0],item.expected_unity_normal_max[1],item.expected_unity_normal_max[2])));
                if(ne>.03f||normals.Any(n=>Mathf.Abs(n.magnitude-1)>.002f))e.Add("Custom normals differ.");
                if(slots[item.name].Length!=mesh.subMeshCount||slots[item.name].Any(s=>!item.material_slots.Contains(s)))e.Add("Imported material slots differ.");
                results.Add(new ImportResult{name=item.name,passed=e.Count==0,triangles=mesh.triangles.Length/3,vertices=mesh.vertexCount,submeshes=mesh.subMeshCount,boundsError=be,normalError=ne,errors=e.ToArray()});
            }
            var hash=Hash(Model)==source.fbx_sha256;var pass=hash&&results.Count==17&&results.All(r=>r.passed);
            File.WriteAllText(output+"/ImportValidation.json",JsonUtility.ToJson(new ImportSummary{passed=pass,hashMatch=hash,meshes=results.ToArray(),method="Actual imported raw FBX meshes compared with authored axis/bounds, triangle, UV0, numerical mask and normal-range manifest. Does not prove per-loop correspondence; Unity captures supply visual review."},true));return pass;
        }
        [Serializable] sealed class PlacementReport {public bool passed;public int placements,tallCombatViolations,invalidSpecies,unboundedCells,floatingOrigins,torsoOrCameraViolations,rootContactSamples,rootContactFailures;public float maxOriginError,maxRootAboveGround,maxRootBurial;public string placementHash,maskSha256,method;}
        static bool ValidatePlacement()
        {
            var report=new PlacementReport{placements=meadow.data.cells.Sum(c=>c.placements.Length),maskSha256=Hash(Foundation+"/TG_MeadowPlacementMask.png"),method="Saved world matrices: user-directed short cover in green region (<.35m undeformed), species, full rotated both-LOD bounds plus world wind inside cells, max-wind torso/camera AABBs for original19 views and264 path samples, actual highest WP03 ground origins.64 placements/species have every authored R0/G0 root checked against actual ground; root contact starting range [-.13,+.08]m includes .025m burial. Not all individual blade roots are sampled."};
            var maskImage=new Texture2D(2,2,TextureFormat.RGB24,false,true);maskImage.LoadImage(File.ReadAllBytes(Foundation+"/TG_MeadowPlacementMask.png"));mask=maskImage;maskPixels=mask.GetPixels();AddGroundColliders();
            var roots=RootSamples(meadow.data);var sampled=new int[8];var cameraPoints=layout.shots.Select(s=>s.position).ToList();
            var paths=new[]{new[]{"PlayerPlanning","EnemyPlanning"},new[]{"EnemyPlanning","PlayerPlanning"},new[]{"PlayerPlanning","PlayerAttack"},new[]{"PlayerAttack","PlayerContact"},new[]{"PlayerContact","PlayerPlanning"},new[]{"EnemyPlanning","EnemyAttack"},new[]{"EnemyAttack","EnemyContact"},new[]{"EnemyContact","EnemyPlanning"}};
            foreach(var pair in paths)for(var i=0;i<33;i++)cameraPoints.Add(Orbit(layout.shots.First(s=>s.name==pair[0]).position,layout.shots.First(s=>s.name==pair[1]).position,layout.origin,i/32f));
            try
            {
                foreach(var cell in meadow.data.cells)foreach(var p in cell.placements)
                {
                    var root=(Vector3)p.matrix.GetColumn(3);var local=root-layout.origin;
                    if(p.speciesIndex<0||p.speciesIndex>=meadow.data.species.Length)report.invalidSpecies++;
                    var species=meadow.data.species[p.speciesIndex];var full=PlacementBounds(species,p.matrix);
                    var allowed=cell.worldBounds;allowed.Expand(.0002f);if(!allowed.Contains(full.min)||!allowed.Contains(full.max))report.unboundedCells++;
                    var combat=PlacementMask(local.x,local.z).g>.1f||(Mathf.Abs(local.x)<18.9f&&Mathf.Abs(local.z)<17.9f);
                    var raw=TransformBounds(meshes["SM_"+species.name+"_LOD0"].bounds,p.matrix);
                    if(combat&&(p.speciesIndex>4||raw.max.y-root.y>.351f))report.tallCombatViolations++;
                    var found=GroundAt(root,out var top);var e=found?Mathf.Abs(root.y-top):100;report.maxOriginError=Mathf.Max(report.maxOriginError,e);if(e>.055f)report.floatingOrigins++;
                    var clearance=ClearanceBounds(species,p.matrix);var blocked=false;
                    foreach(var cp in cameraPoints)
                    {
                        if(clearance.SqrDistance(cp)<.65f*.65f){blocked=true;break;}
                        foreach(var a in layout.anchors){var d=a.worldPosition+Vector3.up*1.6f-cp;if(clearance.IntersectRay(new Ray(cp,d.normalized),out var t)&&t<d.magnitude-.2f){blocked=true;break;}}
                        if(blocked)break;
                    }
                    if(blocked)report.torsoOrCameraViolations++;
                    if(sampled[p.speciesIndex]++<64)foreach(var v in roots[p.speciesIndex])
                    {
                        var wp=p.matrix.MultiplyPoint3x4(v);report.rootContactSamples++;var ok=GroundAt(wp,out var gy);var error=wp.y-gy;report.maxRootAboveGround=Mathf.Max(report.maxRootAboveGround,error);report.maxRootBurial=Mathf.Max(report.maxRootBurial,-error);if(!ok||error>.08f||error<-.13f)report.rootContactFailures++;
                    }
                }
            }
            finally{RemoveGroundColliders();Object.DestroyImmediate(maskImage);}
            report.placementHash=Hash(AssetDatabase.GetAssetPath(meadow.data));
            report.passed=report.placements==meadow.data.placementCount&&report.placements>1000&&report.tallCombatViolations+report.invalidSpecies+report.unboundedCells+report.floatingOrigins+report.torsoOrCameraViolations+report.rootContactFailures==0;
            File.WriteAllText(output+"/PlacementValidation.json",JsonUtility.ToJson(report,true));return report.passed;
        }
        [Serializable] sealed class WindReport {public bool passed;public int testedVertices,testedRootVertices,escapedVertices;public float maxRootDisplacement,maxAnimatedDisplacement;public string method,limitations;}
        static bool ValidateWind()
        {
            var r=new WindReport{method="Every vertex in all16 persisted animated meshes,24 common wind times,4 horizontal directions at strength1 and response.65. CPU mirrors unchanged TG_Common, without far-fade reduction (conservative). Persisted mesh bounds tested at minimum XZ.78/Y.37 scale; rotated placement bounds plus independent world-wind padding checked separately. R0/G0 roots stationary.",limitations="CPU deformation/bounds proof, not GPU pass-by-pass silhouette or shading-normal rotation proof. Lane shader Forward/ShadowCaster/DepthOnly call same TGFoliagePositionWS including distance-wind fade; no DepthNormals/motion vectors, SSAO/TAA off."};
            foreach(var species in meadow.data.species)foreach(var mesh in new[]{species.meshLOD0,species.meshLOD1})
            {
                var verts=mesh.vertices;var colors=mesh.colors;var bounds=mesh.bounds;
                for(var vi=0;vi<verts.Length;vi++)for(var ti=0;ti<24;ti++)for(var di=0;di<4;di++)
                {
                    var d=new Vector2(Mathf.Cos(di*Mathf.PI*.5f),Mathf.Sin(di*Mathf.PI*.5f));var wind=new Vector4(d.x,d.y,1,ti*.53f);
                    // World amplitude does not scale with the instance. Test its
                    // most demanding authored minimum vertical/horizontal scale.
                    var displacement=TeyvatMaterialGlobals.Displacement(verts[vi],colors[vi],wind,.65f);
                    r.testedVertices++;r.maxAnimatedDisplacement=Mathf.Max(r.maxAnimatedDisplacement,displacement.magnitude);
                    if(colors[vi].r<.0001f&&colors[vi].g<.0001f){r.testedRootVertices++;r.maxRootDisplacement=Mathf.Max(r.maxRootDisplacement,displacement.magnitude);}
                    var local=verts[vi]+new Vector3(displacement.x/.78f,displacement.y/.37f,displacement.z/.78f);
                    if(!bounds.Contains(local))r.escapedVertices++;
                }
            }
            r.passed=r.escapedVertices==0&&r.maxRootDisplacement<.00001f&&r.testedRootVertices>0;File.WriteAllText(output+"/WindBoundsValidation.json",JsonUtility.ToJson(r,true));return r.passed;
        }
        [Serializable] sealed class SurfaceReport {public bool passed,independentTerrainAsset;public int changedControlSamples,outsideRegionViolations;public float maximumOutsideChange,minimumCombatGrassWeight;public string method;}
        static bool ValidateTerrainSurface()
        {
            var terrain=environment.GetComponentInChildren<Terrain>();var clone=terrain.terrainData;var original=AssetDatabase.LoadAssetAtPath<TerrainData>(World+"/Terrain/TG_WorldGround.asset");
            var a=original.GetAlphamaps(0,0,original.alphamapWidth,original.alphamapHeight);var b=clone.GetAlphamaps(0,0,clone.alphamapWidth,clone.alphamapHeight);
            var r=new SurfaceReport{independentTerrainAsset=clone!=original&&AssetDatabase.GetAssetPath(clone)==Meadow+"/Terrain/TG_MeadowGround.asset",minimumCombatGrassWeight=1,method="WP04 nativeTerrain clone control weights compared with unchanged WP03 asset. Only local |X|<26,|Z|<24 may change; outside tolerance2/255 allows control texture quantization. All height samples compared independently in CameraEnvelopeReview."};
            for(var z=0;z<clone.alphamapHeight;z++)for(var x=0;x<clone.alphamapWidth;x++)
            {
                var local=terrain.transform.localPosition+new Vector3(x/(float)(clone.alphamapWidth-1)*clone.size.x,0,z/(float)(clone.alphamapHeight-1)*clone.size.z);var delta=0f;
                for(var i=0;i<clone.alphamapLayers;i++)delta=Mathf.Max(delta,Mathf.Abs(a[z,x,i]-b[z,x,i]));
                if(delta>2f/255)r.changedControlSamples++;
                if(Mathf.Abs(local.x)>=26||Mathf.Abs(local.z)>=24){r.maximumOutsideChange=Mathf.Max(r.maximumOutsideChange,delta);if(delta>2f/255)r.outsideRegionViolations++;}
                if(Mathf.Abs(local.x)<18&&Mathf.Abs(local.z)<17)r.minimumCombatGrassWeight=Mathf.Min(r.minimumCombatGrassWeight,b[z,x,0]);
            }
            r.passed=r.independentTerrainAsset&&r.changedControlSamples>0&&r.outsideRegionViolations==0&&r.minimumCombatGrassWeight>.80f;File.WriteAllText(output+"/TerrainSurfaceValidation.json",JsonUtility.ToJson(r,true));return r.passed;
        }
        [Serializable] sealed class CoverageReport {public bool passed;public int expectedPlacements,expectedSamples,LOD0Samples,LOD1Samples,missingSamples,LOD0Batches,LOD1Batches,maximumGroupSize;public string method,limitations;}
        static bool ValidateCoverage()
        {
            const int count=3600,columns=60;const float spacing=.3f;var result=new CoverageReport{expectedPlacements=count,expectedSamples=count*2,method="Actual runtime new meadow route in temporary additive scene:3species×1200 placements, each mesh2 colored submeshes, maximum500 matrices. Linear GPU readback at7200 separated left/right submesh centers for both forced-by-distance LOD0 andLOD1, 18API submissions each.",limitations="Matrix/submesh/LOD coverage microtest. Not a final player GPU performance or full scene temporal-quality proof."};
            var old=SceneManager.GetActiveScene();var oldRT=RenderTexture.active;var oldDebug=Shader.GetGlobalFloat("_TG_DebugAlbedo");Scene rigScene=default;GameObject rig=null;TeyvatMeadowData data=null;Mesh quad=null;Material red=null,green=null;RenderTexture rt=null;Texture2D read=null;
            try
            {
                rigScene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(rigScene);rig=new GameObject("Temporary WP04 actual multi-species coverage");
                red=new Material(meadow.data.species[0].materialsLOD0[0]);green=new Material(red);foreach(var m in new[]{red,green}){m.enableInstancing=true;m.SetFloat("_Family",2);m.SetFloat("_WindResponse",0);m.SetFloat("_UseColorField",0);}
                red.SetColor("_BaseColor",Color.red);green.SetColor("_BaseColor",Color.green);
                quad=new Mesh();quad.vertices=new[]{new Vector3(-.11f,0,-.10f),new Vector3(-.11f,0,.10f),new Vector3(-.015f,0,-.10f),new Vector3(-.015f,0,.10f),new Vector3(.015f,0,-.10f),new Vector3(.015f,0,.10f),new Vector3(.11f,0,-.10f),new Vector3(.11f,0,.10f)};
                quad.normals=Enumerable.Repeat(Vector3.up,8).ToArray();quad.colors=Enumerable.Repeat(new Color(0,0,0,1),8).ToArray();quad.uv=Enumerable.Repeat(Vector2.zero,8).ToArray();quad.subMeshCount=2;quad.SetTriangles(new[]{0,1,2,2,1,3},0);quad.SetTriangles(new[]{4,5,6,6,5,7},1);quad.RecalculateBounds();
                data=ScriptableObject.CreateInstance<TeyvatMeadowData>();data.species=Enumerable.Range(0,3).Select(i=>new TeyvatMeadowData.Species{name="Coverage_"+i,meshLOD0=quad,meshLOD1=quad,materialsLOD0=new[]{red,green},materialsLOD1=new[]{red,green}}).ToArray();
                var center=new Vector3(120,0,120);var places=new TeyvatMeadowData.Placement[count];
                for(var i=0;i<count;i++){var p=center+new Vector3((i%columns-29.5f)*spacing,0,(i/columns-29.5f)*spacing);places[i]=new TeyvatMeadowData.Placement{speciesIndex=i%3,matrix=Matrix4x4.TRS(p,Quaternion.identity,Vector3.one),phase=(i%97)/97f};}
                data.cells=new[]{new TeyvatMeadowData.Cell{name="Coverage",worldBounds=new Bounds(center,new Vector3(20,2,20)),placements=places}};data.placementCount=count;
                var route=rig.AddComponent<TeyvatMeadowRenderer>();route.data=data;route.renderLayer=29;
                var g=rig.AddComponent<TeyvatMaterialGlobals>();g.windStrength=0;g.fixedTime=true;route.globals=g;
                var c=new GameObject("WP04 GPU coverage camera").AddComponent<Camera>();c.transform.SetParent(rig.transform);c.enabled=false;c.orthographic=true;c.orthographicSize=10;c.aspect=16f/9;c.nearClipPlane=.1f;c.farClipPlane=100;c.cullingMask=1<<29;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=Color.black;c.allowHDR=true;c.allowMSAA=false;
                rt=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);read=new Texture2D(1600,900,TextureFormat.RGBAFloat,false,true);Shader.SetGlobalFloat("_TG_DebugAlbedo",1);
                for(var lod=0;lod<2;lod++)
                {
                    c.transform.SetPositionAndRotation(center+Vector3.up*(lod==0?20:60),Quaternion.LookRotation(Vector3.down,Vector3.forward));
                    RenderPipeline.SubmitRenderRequest(c,new RenderPipeline.StandardRequest{destination=rt});RenderPipeline.SubmitRenderRequest(c,new RenderPipeline.StandardRequest{destination=rt});
                    RenderTexture.active=rt;read.ReadPixels(new Rect(0,0,1600,900),0,0);read.Apply();var visible=0;
                    foreach(var p in places)for(var side=0;side<2;side++)
                    {
                        var root=(Vector3)p.matrix.GetColumn(3);var uv=c.WorldToViewportPoint(root+Vector3.right*(side==0?-.0625f:.0625f));var rgb=read.GetPixel(Mathf.RoundToInt(uv.x*1600-.5f),Mathf.RoundToInt(uv.y*900-.5f));
                        var ok=side==0?rgb.r>.95f&&rgb.g<.01f:rgb.g>.95f&&rgb.r<.01f;if(ok)visible++;else result.missingSamples++;
                    }
                    if(lod==0){result.LOD0Samples=visible;result.LOD0Batches=route.submittedBatches;}else{result.LOD1Samples=visible;result.LOD1Batches=route.submittedBatches;}
                    result.maximumGroupSize=Mathf.Max(result.maximumGroupSize,route.DrawGroups.Max(d=>d.count));
                }
                result.passed=result.missingSamples==0&&result.LOD0Batches==18&&result.LOD1Batches==18&&result.maximumGroupSize==500;
            }
            finally
            {
                Shader.SetGlobalFloat("_TG_DebugAlbedo",oldDebug);RenderTexture.active=oldRT;if(rig!=null)Object.DestroyImmediate(rig);if(rigScene.IsValid())EditorSceneManager.CloseScene(rigScene,true);SceneManager.SetActiveScene(old);
                if(data!=null)Object.DestroyImmediate(data);if(quad!=null)Object.DestroyImmediate(quad);if(red!=null)Object.DestroyImmediate(red);if(green!=null)Object.DestroyImmediate(green);if(read!=null)Object.DestroyImmediate(read);if(rt!=null)RenderTexture.ReleaseTemporary(rt);globals.Apply();
            }
            File.WriteAllText(output+"/InstancingCoverageValidation.json",JsonUtility.ToJson(result,true));return result.passed;
        }
    }
}
