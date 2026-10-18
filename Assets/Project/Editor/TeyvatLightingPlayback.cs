using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using FightingAllstar.EnvironmentArt;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

namespace FightingAllstar.EditorTools
{
    public static partial class TeyvatLightingLab
    {
        const string PlayPhase="TG_WP08_PlayPhase",PlayOutput="TG_WP08_PlayOutput";
        static bool playbackStarted;static int playbackFrames;static double nextPlaybackFrame;static float clockStart;static Vector3 tourStart;static string playbackDirectory;
        [InitializeOnLoadMethod] static void ResumePlaybackAfterDomainReload()
        {if(SessionState.GetInt(PlayPhase,0)>0){EditorApplication.update-=PlaybackTick;EditorApplication.update+=PlaybackTick;}}
        public static void RunPlayback()
        {
            var marker=Path.Combine(Path.GetDirectoryName(Application.dataPath),"TG_ISOLATED_LIGHTING.txt");
            if(!File.Exists(marker)||File.Exists("Assets/Project/Scenes/Battle.unity"))throw new InvalidOperationException("WP08 playback requires isolated art host.");
            var dir=File.ReadAllText(marker).Trim()+"/Playback";Directory.CreateDirectory(dir);SessionState.SetString(PlayOutput,dir);SessionState.SetInt(PlayPhase,1);
            EditorSceneManager.OpenScene(ReviewScene);SessionState.SetBool("TG_WP08_PlayPassed",false);EditorApplication.update-=PlaybackTick;EditorApplication.update+=PlaybackTick;EditorApplication.EnterPlaymode();
        }
        [Serializable] sealed class PlaybackReport
        {
            public bool passed,actualPlayMode,sharedClockAdvances,grassSubmitted,tourMoves,oneClock,liveBinding,lightingOwner,postProcessing,reflectionTexture;
            public int frames,globalsOwners,placements,cloneCreations;public float clockBegin,clockEnd;public Vector3 tourBegin,tourEnd;
            public string method="Cold-open saved master review scene, enter actual Unity Play Mode with domain reload.24 camera-matched water frames and24 running review-tour frames captured while shared realtime clock advances; no UI. Check actual grass submissions and transient meadow binding. Exit Play Mode without saving play state.";
            public string limitations="Review cameras and six current project actor placeholders. This is not a real combat controller, animated-character/material migration or Windows player performance certification.";
        }
        static void PlaybackTick()
        {
            var phase=SessionState.GetInt(PlayPhase,0);if(phase==0){EditorApplication.update-=PlaybackTick;return;}
            if(phase==2)
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                var dir=SessionState.GetString(PlayOutput,"");var passed=SessionState.GetBool("TG_WP08_PlayPassed",false);File.WriteAllText(dir+(passed?"/COMPLETE.txt":"/FAILED.txt"),passed?"Saved stage actual Play Mode review passed.":"Read PlaybackValidation.json.");SessionState.SetInt(PlayPhase,0);EditorApplication.update-=PlaybackTick;EditorApplication.Exit(passed?0:1);return;
            }
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            try
            {
                if(!playbackStarted)
                {
                    playbackDirectory=SessionState.GetString(PlayOutput,"");environment=GameObject.Find("TG Battle Stage WP08").transform;globals=environment.GetComponentInChildren<TeyvatMaterialGlobals>();rig=UnityEngine.Object.FindFirstObjectByType<TeyvatLightingRig>();rig.Apply();globals.fixedTime=false;globals.Apply();clockStart=Shader.GetGlobalVector("_TG_Wind").w;tourStart=GameObject.Find("TG Review camera tour").transform.position;
                    playbackStarted=true;nextPlaybackFrame=EditorApplication.timeSinceStartup+1;playbackFrames=0;
                }
                if(EditorApplication.timeSinceStartup<nextPlaybackFrame)return;
                Capture(GameObject.Find("TG_WaterSample").GetComponent<Camera>(),playbackDirectory+"/Water/Frame"+playbackFrames.ToString("D3")+".png",960,540);
                Capture(GameObject.Find("TG Review camera tour").GetComponent<Camera>(),playbackDirectory+"/Tour/Frame"+playbackFrames.ToString("D3")+".png",960,540);playbackFrames++;nextPlaybackFrame=EditorApplication.timeSinceStartup+.12;
                if(playbackFrames<24)return;
                var binding=environment.GetComponent<TeyvatStageBinding>();var meadow=environment.GetComponentInChildren<TeyvatMeadowRenderer>();var tourEnd=GameObject.Find("TG Review camera tour").transform.position;var end=Shader.GetGlobalVector("_TG_Wind").w;
                var r=new PlaybackReport{actualPlayMode=Application.isPlaying,frames=playbackFrames,clockBegin=clockStart,clockEnd=end,sharedClockAdvances=end>clockStart+.5f,globalsOwners=UnityEngine.Object.FindObjectsByType<TeyvatMaterialGlobals>(FindObjectsSortMode.None).Length,grassSubmitted=meadow.submittedBatches>0,tourBegin=tourStart,tourEnd=tourEnd,tourMoves=Vector3.Distance(tourStart,tourEnd)>.1f,placements=binding.boundPlacementCount,cloneCreations=binding.cloneCreationCount,liveBinding=binding.RuntimeMeadowData&&meadow.data==binding.RuntimeMeadowData&&binding.TransformSupported};r.lightingOwner=rig&&rig.profile&&Shader.GetGlobalFloat("_TG_StyleEnabled")>.5f;r.postProcessing=GameObject.Find("TG Review camera tour").GetComponent<Camera>().GetUniversalAdditionalCameraData().renderPostProcessing;r.oneClock=r.globalsOwners==1;r.passed=r.lightingOwner&&r.postProcessing&&r.actualPlayMode&&r.sharedClockAdvances&&r.grassSubmitted&&r.tourMoves&&r.oneClock&&r.liveBinding;
                var reflection=rig.GetComponentInChildren<ReflectionProbe>();r.reflectionTexture=reflection&&reflection.mode==ReflectionProbeMode.Custom&&reflection.customBakedTexture is Cubemap;r.passed&=r.reflectionTexture;
                File.WriteAllText(playbackDirectory+"/PlaybackValidation.json",JsonUtility.ToJson(r,true));SessionState.SetBool("TG_WP08_PlayPassed",r.passed);SessionState.SetInt(PlayPhase,2);EditorApplication.ExitPlaymode();
            }
            catch(Exception e){playbackDirectory=SessionState.GetString(PlayOutput,"");File.WriteAllText(playbackDirectory+"/Exception.txt",e.ToString());SessionState.SetBool("TG_WP08_PlayPassed",false);SessionState.SetInt(PlayPhase,2);EditorApplication.ExitPlaymode();}
        }
    }
}
