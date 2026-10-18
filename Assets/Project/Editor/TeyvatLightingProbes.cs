using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using FightingAllstar.EnvironmentArt;
using Object=UnityEngine.Object;
namespace FightingAllstar.EditorTools
{
    public static partial class TeyvatLightingLab
    {
        [Serializable] sealed class ProbeReport
        {
            public bool passed,bakeSucceeded,finite,positionsMatch;public int requested,actual;public float maximumCoefficient,minimumCoefficient,seconds;
            public string lightingDataAsset,lightingSettings;
            public string method="Legacy light-probe bake in saved review scene. Progressive CPU; approximate static URP/Lit ground and near boulder occluders only, realtime key. Wind foliage, animated grass, water, transparent effects and original stylized backdrop are excluded from GI contribution. The geometry proxies remain disabled after bake, and are excluded from the environment prefab.";
            public string limitations="Approximate indirect lighting for current URP/Lit actor placeholders. This is not a full stage lightmap bake, APV integration, physically accurate foliage bounce or support for detailed future character materials.";
            public string error;
        }
        static bool BakeActorProbes()
        {
            var r=new ProbeReport();var watch=System.Diagnostics.Stopwatch.StartNew();var proxy=Group("Baked GI approximation - renderers disabled after bake",rig.transform);var renderers=new List<Renderer>();
            try
            {
                var group=rig.gameObject.AddComponent<LightProbeGroup>();var points=new List<Vector3>();foreach(var x in new[]{-14,-7,0,7,14})foreach(var z in new[]{-9,0,9})foreach(var y in new[]{.4f,1.7f,3.5f})points.Add(origin+new Vector3(x,y,z));group.probePositions=points.ToArray();r.requested=points.Count;
                foreach(var t in environment.GetComponentsInChildren<Transform>(true))GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);
                var matPath=Lighting+"/Probes/TG_ProxyGround.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,matPath);}mat.SetColor("_BaseColor",new Color(.24f,.38f,.16f));mat.SetFloat("_Smoothness",0);
                var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="GI ground approximation";ground.transform.SetParent(proxy,false);ground.transform.position=origin+Vector3.down*.13f;ground.transform.localScale=new Vector3(70,.2f,70);Object.DestroyImmediate(ground.GetComponent<Collider>());var gr=ground.GetComponent<MeshRenderer>();gr.sharedMaterial=mat;renderers.Add(gr);GameObjectUtility.SetStaticEditorFlags(ground,StaticEditorFlags.ContributeGI);gr.scaleInLightmap=.1f;
                var geo=environment.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.Contains("Boulder")&&f.name.EndsWith("_LOD0")&&Vector3.Distance(f.GetComponent<Renderer>().bounds.center,origin)<44).Take(8).ToArray();
                foreach(var src in geo){var t=Group("GI "+src.name,proxy);t.SetPositionAndRotation(src.transform.position,src.transform.rotation);t.localScale=src.transform.lossyScale;t.gameObject.AddComponent<MeshFilter>().sharedMesh=src.sharedMesh;var rr=t.gameObject.AddComponent<MeshRenderer>();rr.sharedMaterials=Enumerable.Repeat(mat,src.sharedMesh.subMeshCount).ToArray();rr.scaleInLightmap=.1f;renderers.Add(rr);GameObjectUtility.SetStaticEditorFlags(t.gameObject,StaticEditorFlags.ContributeGI);}
                var settingsPath=Lighting+"/Settings/TG_ActorProbeBake.asset";var settings=AssetDatabase.LoadAssetAtPath<LightingSettings>(settingsPath);if(!settings){settings=new LightingSettings();AssetDatabase.CreateAsset(settings,settingsPath);}settings.bakedGI=true;settings.realtimeGI=false;settings.lightmapper=LightingSettings.Lightmapper.ProgressiveCPU;settings.lightmapResolution=1;settings.lightmapMaxSize=256;settings.directSampleCount=16;settings.indirectSampleCount=32;settings.environmentSampleCount=32;settings.maxBounces=2;Lightmapping.lightingSettings=settings;r.lightingSettings=settingsPath;EditorUtility.SetDirty(settings);
                Save();r.bakeSucceeded=Lightmapping.Bake();var probes=LightmapSettings.lightProbes;r.actual=probes?probes.count:0;r.finite=probes&&probes.bakedProbes.All(sh=>Enumerable.Range(0,27).All(i=>!float.IsNaN(sh[i/9,i%9])&&!float.IsInfinity(sh[i/9,i%9])));r.positionsMatch=r.actual==r.requested;
                if(probes&&probes.bakedProbes.Length>0){r.maximumCoefficient=probes.bakedProbes.Max(sh=>Enumerable.Range(0,27).Max(i=>sh[i/9,i%9]));r.minimumCoefficient=probes.bakedProbes.Min(sh=>Enumerable.Range(0,27).Min(i=>sh[i/9,i%9]));}
                r.lightingDataAsset=AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset);r.passed=r.bakeSucceeded&&r.actual>=45&&r.finite&&r.maximumCoefficient>.001f;
                LightProbes.Tetrahedralize();
            }catch(Exception e){r.error=e.ToString();}
            finally{foreach(var renderer in renderers)if(renderer)renderer.enabled=false;watch.Stop();r.seconds=(float)watch.Elapsed.TotalSeconds;File.WriteAllText(output+"/ProbeBake.json",JsonUtility.ToJson(r,true));rig.Apply();}
            return r.passed;
        }
        [Serializable] sealed class ReflectionReport{public bool passed;public string texture,mode,method,limitations;public int resolution;}
        static void BakeReflection()
        {
            var t=Group("Baked arena reflection - URP Lit actors",rig.transform);t.position=origin+new Vector3(0,2,0);var p=t.gameObject.AddComponent<ReflectionProbe>();p.mode=ReflectionProbeMode.Custom;p.renderDynamicObjects=true;p.refreshMode=ReflectionProbeRefreshMode.ViaScripting;p.size=new Vector3(48,18,42);p.center=new Vector3(0,3,0);p.resolution=128;p.hdr=true;p.boxProjection=true;p.blendDistance=8;p.intensity=.6f;p.cullingMask=1;p.nearClipPlane=.2f;p.farClipPlane=450;p.clearFlags=ReflectionProbeClearFlags.Skybox;
            // Exclude transient instanced foliage and dynamic actor fixtures from
            // a static reflection. The panorama and opaque backdrop are retained.
            var path=Lighting+"/Probes/TG_ArenaReflection.exr";var actors=GameObject.Find(ActorFixtureGroupName);var meadow=environment.GetComponentInChildren<TeyvatMeadowRenderer>();var wasActive=actors.activeSelf;var wasMeadow=meadow.enabled;bool succeeded;
            try{actors.SetActive(false);meadow.enabled=false;succeeded=Lightmapping.BakeReflectionProbe(p,path);}finally{actors.SetActive(wasActive);meadow.enabled=wasMeadow;}
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var texture=AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            // bakedTexture is an editor lighting-data binding, not a serialized
            // component reference. Keep the baked EXR as an explicit Custom
            // cubemap so a fresh editor can reproduce the static reflection.
            if(texture){p.mode=ReflectionProbeMode.Custom;p.customBakedTexture=texture;EditorUtility.SetDirty(p);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());}
            File.WriteAllText(output+"/ReflectionBake.json",JsonUtility.ToJson(new ReflectionReport{passed=succeeded&&texture,texture=path,resolution=128,mode="Custom (Unity-baked static HDR cubemap)",method="Unity BakeReflectionProbe output assigned through the serialized customBakedTexture reference. Static reflection for URP/Lit actor materials; no runtime planar reflection camera.",limitations="Stylized environment and water shaders do not sample this reflection probe; water retains its artist sky tint. Static capture does not reflect moving actors or effects."},true));if(!succeeded||!texture)throw new Exception("Baked reflection cubemap missing.");
        }
    }
}
