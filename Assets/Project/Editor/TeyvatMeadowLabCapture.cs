using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using FightingAllstar.EnvironmentArt;
using Object=UnityEngine.Object;

namespace FightingAllstar.EditorTools
{
    public static partial class TeyvatMeadowLab
    {
        static void Lighting(bool neutral)
        {
            var sun=Object.FindFirstObjectByType<Light>();sun.intensity=neutral?1.1f:1.35f;sun.color=neutral?Color.white:new Color(1,.93f,.79f);sun.transform.rotation=Quaternion.Euler(neutral?55:38,-43,0);
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=neutral?new Color(.6f,.6f,.6f):new Color(.55f,.69f,.82f);RenderSettings.ambientEquatorColor=neutral?new Color(.4f,.4f,.4f):new Color(.32f,.43f,.47f);RenderSettings.ambientGroundColor=neutral?new Color(.25f,.25f,.25f):new Color(.24f,.29f,.17f);
            RenderSettings.fog=!neutral;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=100;RenderSettings.fogEndDistance=600;RenderSettings.fogColor=new Color(.62f,.78f,.85f);
        }
        static void Capture(Camera camera,string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));var rt=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var old=RenderTexture.active;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try{camera.aspect=16f/9;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{RenderTexture.active=old;Object.DestroyImmediate(image);RenderTexture.ReleaseTemporary(rt);}
        }
        static void TemporalAndDistance(Camera[] cameras)
        {
            var close=cameras.First(c=>c.name=="TG_MeadowClose");var attack=cameras.First(c=>c.name=="TG_EnemyAttack");
            globals.fixedTime=true;var oldStrength=globals.windStrength;
            globals.windStrength=0;globals.Apply();Capture(close,output+"/Wind/Still.png");
            globals.windStrength=oldStrength;
            for(var i=0;i<4;i++){globals.fixedSeconds=i*.35f;globals.Apply();Capture(close,output+"/Wind/Time_"+i+".png");}
            globals.fixedSeconds=1;globals.Apply();var position=attack.transform.position;
            for(var i=0;i<4;i++){attack.transform.position=position+attack.transform.right*(i*.12f);Capture(attack,output+"/Temporal/Attack_"+i+".png");}attack.transform.position=position;
            var planning=cameras.First(c=>c.name=="TG_ReferencePlanningCandidate");position=planning.transform.position;
            for(var i=0;i<4;i++){planning.transform.position=position-planning.transform.forward*i*8;Capture(planning,output+"/Distance/Planning_"+i+".png");}planning.transform.position=position;
        }
        [Serializable] sealed class CostCase {public bool meadowEnabled;public double medianRenderAndReadbackMs,minRenderAndReadbackMs,maxRenderAndReadbackMs,medianPrepareMs,medianSubmissionMs;public int instances,lod0,lod1,apiSubmissions;public long submittedTriangles;}
        [Serializable] sealed class CostReport {public string gpu,cpu,unity,context,limitations;public int width=1600,height=900,samplesPerCase=12;public CostCase[] cases;}
        static void Measure(Camera camera)
        {
            var cases=new List<CostCase>();var old=RenderTexture.active;var rt=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var tex=new Texture2D(1,1,TextureFormat.RGB24,false);
            try
            {
                foreach(var enabled in new[]{false,true})
                {
                    meadow.enabled=enabled;var times=new List<double>();var prepare=new List<double>();var draw=new List<double>();
                    for(var i=0;i<16;i++)
                    {
                        var timer=System.Diagnostics.Stopwatch.StartNew();RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});RenderTexture.active=rt;tex.ReadPixels(new Rect(800,450,1,1),0,0);tex.Apply();timer.Stop();
                        if(i>=4){times.Add(timer.Elapsed.TotalMilliseconds);prepare.Add(meadow.prepareMilliseconds);draw.Add(meadow.drawMilliseconds);}
                    }
                    times.Sort();prepare.Sort();draw.Sort();cases.Add(new CostCase{meadowEnabled=enabled,medianRenderAndReadbackMs=times[6],minRenderAndReadbackMs=times[0],maxRenderAndReadbackMs=times[11],medianPrepareMs=prepare[6],medianSubmissionMs=draw[6],instances=meadow.visibleInstances,lod0=meadow.visibleLOD0,lod1=meadow.visibleLOD1,apiSubmissions=meadow.submittedBatches,submittedTriangles=meadow.submittedTriangles});
                }
            }
            finally{meadow.enabled=true;RenderTexture.active=old;Object.DestroyImmediate(tex);RenderTexture.ReleaseTemporary(rt);}
            File.WriteAllText(output+"/EditorCostComparison.json",JsonUtility.ToJson(new CostReport{gpu=SystemInfo.graphicsDeviceName,cpu=SystemInfo.processorType,unity=Application.unityVersion,cases=cases.ToArray(),context="Windows isolated URP/Linear Editor, identical static planning camera, accepted4xMSAA settings, same fixed windtime.4warmups+12 synchronized render requests per case. GPU readback forces completion; CPU stopwatch includes render, driver/editor overhead and1pixel readback. Runtime Prepare and API submission CPU counters shown separately.",limitations="Not player frame time or FPS, no GPU timestamp query, animation/gameplay/transparent VFX absent. API submissions are not hardware drawcalls. Minimum PC hardware, display target and standalone build remain WP09 acceptance dependencies."},true));
        }
    }
}
