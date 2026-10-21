using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FightingAllstar.Rendering
{
    /// <summary>Controlled map-only benchmark; attach only to generated benchmark scenes.</summary>
    public sealed class MapPerformanceProbe : MonoBehaviour
    {
        [Serializable] public struct Pose { public string name;public Vector3 position;public Quaternion rotation;public float fov; }
        [Serializable] public sealed class Result
        {
            public string map,variant,device,cpu,gpu,unity,environment;
            public int width,height,samples;
            public bool nativeGpuOcclusionActive;
            public Shot[] shots;
            public string limitation="Map-only: no battle simulation/UI. Main thread elapsed includes waits; CPU busy excludes present wait when FrameTiming is available. Editor runs are diagnostic, Windows player is authoritative for the budget.";
        }
        [Serializable] public sealed class Shot
        {
            public string name;public int samples;
            public double drawCallsMedian,drawCallsP95,batchesMedian,setPassMedian,cpuMainMedianMs,cpuMainP95Ms,cpuBusyMedianMs,cpuBusyP95Ms,renderThreadMedianMs,gpuMedianMs,frameMedianMs;
            public int requestedSamples,drawCallsSamples,batchesSamples,setPassSamples,cpuMainSamples,cpuBusySamples,renderThreadSamples,gpuSamples,frameSamples,frameTimingSamples;
            public int cpuMainRecorderSamples,cpuMainFrameTimingSamples,renderThreadRecorderSamples,renderThreadFrameTimingSamples;
            public string cpuMainSource,renderThreadSource;
            public double cpuMainRecorderMedianMs,cpuMainRecorderP95Ms,cpuMainFrameTimingMedianMs,cpuMainFrameTimingP95Ms,renderThreadRecorderMedianMs,renderThreadFrameTimingMedianMs;
        }
        public string mapName,variant,nextScene;
        public Camera view;
        public Pose[] poses;
        public int warmupFrames=30,sampleFrames=90;
        public bool autoRun=true;
        readonly FrameTiming[] timings=new FrameTiming[1];
        readonly List<ProfilerRecorder> recorders=new List<ProfilerRecorder>();
        ProfilerRecorder draws,batches,passes,main,render;
        int previousVsync,previousTarget;bool previousBackground;
#if UNITY_EDITOR
        bool previousAsyncCompilation,changedAsyncCompilation;
#endif
        IEnumerator Start()
        {
            if(!autoRun)yield break;
            // Built-in Osaka requires its own baseline player. A URP build
            // must never time incompatible Standard-material fallback pixels.
            if(mapName=="Battle"&&variant=="Before"&&UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline!=null)
            {SceneManager.LoadScene(nextScene);yield break;}
            var args=Environment.GetCommandLineArgs();
            if(!Application.isEditor&&Array.IndexOf(args,"-mapAfterOnly")>=0&&variant=="Before")
            {SceneManager.LoadScene("MapProfile_BattleTeyvat_After");yield break;}
            var cellArg=Array.IndexOf(args,"-mapCellSize");
            if(variant=="After"&&cellArg>=0&&cellArg+1<args.Length&&float.TryParse(args[cellArg+1],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var cell)&&cell>=8&&cell<=256)
                foreach(var instancer in FindObjectsByType<MapLODInstancer>(FindObjectsSortMode.None)){instancer.cellSize=cell;instancer.Rebuild();}
            previousVsync=QualitySettings.vSyncCount;previousTarget=Application.targetFrameRate;previousBackground=Application.runInBackground;
            QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;Application.runInBackground=true;
            if(!Application.isEditor)Screen.SetResolution(1920,1080,FullScreenMode.Windowed);
            draws=Recorder(ProfilerCategory.Render,"Draw Calls Count");batches=Recorder(ProfilerCategory.Render,"Batches Count");passes=Recorder(ProfilerCategory.Render,"SetPass Calls Count");
            main=Recorder(ProfilerCategory.Internal,"Main Thread");render=Recorder(ProfilerCategory.Internal,"Render Thread");
#if UNITY_EDITOR
            // A first Forward+ variant can stay on Unity's cyan loading shader
            // despite the async toggle. Never benchmark that cheaper fallback.
            yield return null;yield return null;
            previousAsyncCompilation=UnityEditor.ShaderUtil.allowAsyncCompilation;changedAsyncCompilation=true;
            UnityEditor.ShaderUtil.allowAsyncCompilation=false;
            var materials=FindObjectsByType<Renderer>(FindObjectsSortMode.None).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct();
            foreach(var material in materials)for(var pass=0;pass<material.passCount;pass++)UnityEditor.ShaderUtil.CompilePass(material,pass,true);
#endif
            var result=new Result{map=mapName,variant=variant,device=SystemInfo.deviceModel,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,unity=Application.unityVersion,environment=Application.isEditor?"Editor diagnostic":"Windows standalone",samples=sampleFrames};
            var folder=Path.Combine(Directory.GetCurrentDirectory(),"Artifacts/MapOptimization",Application.isEditor?"Editor":"Player");Directory.CreateDirectory(folder);
            var tagArg=Array.IndexOf(args,"-mapBenchmarkTag");
            if(tagArg>=0&&tagArg+1<args.Length&&System.Text.RegularExpressions.Regex.IsMatch(args[tagArg+1],"^[a-zA-Z0-9_-]{1,32}$")){folder=Path.Combine(folder,args[tagArg+1]);Directory.CreateDirectory(folder);}
            var output=new List<Shot>();
            ulong lastTiming=0;
            foreach(var pose in poses)
            {
                view.transform.SetPositionAndRotation(pose.position,pose.rotation);view.fieldOfView=pose.fov;
                var ds=new List<double>();var bs=new List<double>();var ps=new List<double>();var ms=new List<double>();var rs=new List<double>();var timingMain=new List<double>();var timingRender=new List<double>();var busy=new List<double>();var gpu=new List<double>();var frame=new List<double>();
                var timingSamples=0;
                for(var i=0;i<warmupFrames+sampleFrames;i++)
                {
                    FrameTimingManager.CaptureFrameTimings();yield return new WaitForEndOfFrame();
                    if(i<warmupFrames)continue;
                    if(draws.Valid&&draws.Count>0)ds.Add(draws.LastValue);if(batches.Valid&&batches.Count>0)bs.Add(batches.LastValue);if(passes.Valid&&passes.Count>0)ps.Add(passes.LastValue);
                    if(main.Valid&&main.LastValue>0)ms.Add(main.LastValue/1000000.0);if(render.Valid&&render.LastValue>0)rs.Add(render.LastValue/1000000.0);frame.Add(Time.unscaledDeltaTime*1000.0);
                    if(FrameTimingManager.GetLatestTimings(1,timings)>0&&timings[0].frameStartTimestamp!=0&&timings[0].frameStartTimestamp!=lastTiming)
                    {
                        var timing=timings[0];lastTiming=timing.frameStartTimestamp;timingSamples++;
                        if(PositiveFinite(timing.cpuMainThreadFrameTime))
                        {
                            timingMain.Add(timing.cpuMainThreadFrameTime);
                            if(!double.IsNaN(timing.cpuMainThreadPresentWaitTime)&&!double.IsInfinity(timing.cpuMainThreadPresentWaitTime)&&timing.cpuMainThreadPresentWaitTime>=0&&timing.cpuMainThreadPresentWaitTime<=timing.cpuMainThreadFrameTime)
                                busy.Add(timing.cpuMainThreadFrameTime-timing.cpuMainThreadPresentWaitTime);
                        }
                        if(PositiveFinite(timing.gpuFrameTime))gpu.Add(timing.gpuFrameTime);
                        if(PositiveFinite(timing.cpuRenderThreadFrameTime))timingRender.Add(timing.cpuRenderThreadFrameTime);
                    }
                }
                // Keep independent instruments independent. Prefer the recorder
                // for existing aggregate fields, with FrameTiming as a named fallback.
                var mainValues=ms.Count>0?ms:timingMain;var renderValues=rs.Count>0?rs:timingRender;
                output.Add(new Shot
                {
                    name=pose.name,samples=frame.Count,requestedSamples=sampleFrames,
                    drawCallsMedian=P(ds,.5),drawCallsP95=P(ds,.95),batchesMedian=P(bs,.5),setPassMedian=P(ps,.5),
                    cpuMainMedianMs=P(mainValues,.5),cpuMainP95Ms=P(mainValues,.95),renderThreadMedianMs=P(renderValues,.5),cpuBusyMedianMs=P(busy,.5),cpuBusyP95Ms=P(busy,.95),gpuMedianMs=P(gpu,.5),frameMedianMs=P(frame,.5),
                    drawCallsSamples=ds.Count,batchesSamples=bs.Count,setPassSamples=ps.Count,cpuMainSamples=mainValues.Count,cpuBusySamples=busy.Count,renderThreadSamples=renderValues.Count,gpuSamples=gpu.Count,frameSamples=frame.Count,frameTimingSamples=timingSamples,
                    cpuMainRecorderSamples=ms.Count,cpuMainFrameTimingSamples=timingMain.Count,renderThreadRecorderSamples=rs.Count,renderThreadFrameTimingSamples=timingRender.Count,
                    cpuMainSource=ms.Count>0?"ProfilerRecorder":timingMain.Count>0?"FrameTiming":"Unavailable",
                    renderThreadSource=rs.Count>0?"ProfilerRecorder":timingRender.Count>0?"FrameTiming":"Unavailable",
                    cpuMainRecorderMedianMs=P(ms,.5),cpuMainRecorderP95Ms=P(ms,.95),cpuMainFrameTimingMedianMs=P(timingMain,.5),cpuMainFrameTimingP95Ms=P(timingMain,.95),renderThreadRecorderMedianMs=P(rs,.5),renderThreadFrameTimingMedianMs=P(timingRender,.5),
                });
                ScreenCapture.CaptureScreenshot(Path.Combine(folder,mapName+"_"+variant+"_"+pose.name+".png"));yield return null;
                result.shots=output.ToArray();result.width=Screen.width;result.height=Screen.height;
                result.nativeGpuOcclusionActive=UnityEngine.Rendering.GPUResidentDrawer.IsInstanceOcclusionCullingEnabled();
                File.WriteAllText(Path.Combine(folder,mapName+"_"+variant+".json"),JsonUtility.ToJson(result,true));
            }
            Cleanup();
            if(!string.IsNullOrEmpty(nextScene)&&Application.CanStreamedLevelBeLoaded(nextScene))SceneManager.LoadScene(nextScene);
            else if(Application.isEditor)
            {
#if UNITY_EDITOR
                UnityEditor.SessionState.SetBool("MapOptimization.ProfileComplete",true);
#endif
            }
            else Application.Quit();
        }
        ProfilerRecorder Recorder(ProfilerCategory category,string name)
        {var r=ProfilerRecorder.StartNew(category,name,1);recorders.Add(r);return r;}
        static double P(List<double> values,double percentile)
        {if(values.Count==0)return -1;values.Sort();return values[Math.Min(values.Count-1,(int)((values.Count-1)*percentile))];}
        static bool PositiveFinite(double value)=>value>0&&!double.IsNaN(value)&&!double.IsInfinity(value);
        void Cleanup()
        {
            foreach(var recorder in recorders)if(recorder.Valid)recorder.Dispose();recorders.Clear();
            QualitySettings.vSyncCount=previousVsync;Application.targetFrameRate=previousTarget;Application.runInBackground=previousBackground;
#if UNITY_EDITOR
            if(changedAsyncCompilation){UnityEditor.ShaderUtil.allowAsyncCompilation=previousAsyncCompilation;changedAsyncCompilation=false;}
#endif
        }
        void OnDestroy()
        {
            if(recorders.Count>0)Cleanup();
#if UNITY_EDITOR
            else if(changedAsyncCompilation){UnityEditor.ShaderUtil.allowAsyncCompilation=previousAsyncCompilation;changedAsyncCompilation=false;}
#endif
        }
    }
}
