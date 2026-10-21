#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Studio;
using FightingAllstar.Presentation.Combat;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    [InitializeOnLoad]
    public static class StudioM6LiveChecks
    {
        private const string Key="CharacterStudio.M6.Live";
        [Serializable] private sealed class Trial
        {
            public int Stage;public bool Running=true,SawActive,SawClip,SawFx,SawFct,SawAudio,Paused;
            public string Directory,Scene,SceneHash,Damage,Health,Error;public bool Dirty;
            public ulong Rng,CardRng;public float Clock;public double Deadline;
            public List<string> Checks=new List<string>();
        }
        private static Trial trial;
        static StudioM6LiveChecks(){trial=JsonConvert.DeserializeObject<Trial>(SessionState.GetString(Key,"null"));if(trial?.Running==true)EditorApplication.update+=Tick;}
        public static string Status=>JsonConvert.SerializeObject(trial);
        public static void Start()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before starting the M6 trial.");
            dynamic evidence=JsonConvert.DeserializeObject(File.ReadAllText("Artifacts/CharacterCreatorStudio/M6/native-preview-evidence.json"));
            trial=new Trial{Directory=(string)evidence.FixtureDirectory,Scene=SceneManager.GetActiveScene().path,Dirty=SceneManager.GetActiveScene().isDirty,SceneHash=Hash(StudioBattleLabBridge.ScenePath),Deadline=EditorApplication.timeSinceStartup+180};
            Store();EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        }
        private static void Check(bool ok,string description){if(!ok)throw new InvalidOperationException(description);trial.Checks.Add(description);Store();}
        private static void Stage(int next){trial.Stage=next;trial.Deadline=EditorApplication.timeSinceStartup+180;Store();}
        private static void Tick()
        {
            if(trial?.Running!=true || EditorApplication.isCompiling || EditorApplication.isUpdating)return;
            try
            {
                if(trial.Stage!=20 && EditorApplication.timeSinceStartup>trial.Deadline)throw new TimeoutException("M6 live stage "+trial.Stage);
                var controller=StudioBattleLabBridge.Controller;
                switch(trial.Stage)
                {
                    case 0:
                        var w=CharacterCreatorStudioWindow.Open();w.ShowWorkspace("lab");
                        w.rootVisualElement.Query<EnumField>().ToList().First(f=>f.label=="Battle mode").value=BattleLabMode.TrainingBattle;
                        var config=w.BuildLabConfiguration();var id="Player:0:"+config.Actors[0].Definition.Id;
                        Stage(1);StudioBattleLabBridge.Launch(config,new List<StudioBattleLabBridge.VisualSelection>{new StudioBattleLabBridge.VisualSelection{FighterId=id,ProfilePath=trial.Directory+"/Actor.asset"}});break;
                    case 1:
                        if(controller==null || !controller.LabCanAct)return;
                        var state=controller.LabSnapshot;var profile=AssetDatabase.LoadAssetAtPath<AbilityPresentationProfile>(trial.Directory+"/Ability.asset");
                        var card=state.Player.Hand.First(c=>c.SkillId==profile.SkillId && c.Rank==1);var target=state.Opponent.Fighters[0].Id;
                        var expected=new LocalBattleSession(state,true);var draft=new PlanDraft(state);
                        if(!draft.QueuePlay(card.Id,target,out var error) || !expected.Submit(draft.BuildPlan("m6.expected"),out error))throw new InvalidOperationException(error);
                        var after=expected.GetSnapshot();trial.Damage=Damage(after);trial.Health=Health(after);trial.Rng=after.RngDrawCount;trial.CardRng=after.CardRngDrawCount;
                        Check(controller.LabFighterViewCount==2,"Profile setup still uses the actual 3D battle actors.");
                        Stage(2);Check(controller.LabPlayCard(card.Id,target,out error),"Normal lab card submission accepts the profiled ability.");break;
                    case 2:
                    case 21:
                        if(controller==null)return;
                        trial.SawActive|=controller.LabProfilePlaybackActive;trial.SawFx|=controller.LabProfileEffectCount>0;trial.SawFct|=controller.LabActiveFloatingText>0;
                        trial.SawAudio|=UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(a=>a.isPlaying);
                        var actor=GameObject.Find("CoreFighter_"+controller.LabSnapshot.Player.Fighters[0].Id);var visual=actor?.transform.Find("Model/Rig/Visual");trial.SawClip|=visual!=null && visual.localPosition.x>.05f;
                        if(trial.Stage==2 && controller.LabProfilePlaybackActive && controller.LabProfileTime>.25f)
                        {trial.Clock=controller.LabProfileTime;trial.Deadline=EditorApplication.timeSinceStartup+1;trial.Stage=20;EditorApplication.isPaused=true;Store();return;}
                        if(!trial.SawActive || !controller.LabCanAct){Store();return;}
                        Check(trial.SawActive,"The extracted playback driver runs the authored ability in Play Mode.");
                        Check(trial.SawClip,"The runtime driver samples the authored rig clip.");Check(trial.SawFx,"Committed contact playback emits authored socket FX.");
                        Check(trial.SawFct && trial.SawAudio,"Normal floating combat text and impact audio remain active.");
                        Check(controller.LabProfileAudioCues>0,"The authored contact audio cue plays through the injected runtime audio source.");
                        Check(!controller.LabProfilePlaybackActive && controller.LabProfileEffectCount==0,"Recovery releases the driver and every contact effect.");
                        var actual=controller.LabSnapshot;
                        Check(Damage(actual)==trial.Damage && Health(actual)==trial.Health,"Profiled playback preserves exact engine contact amounts, targets, indices and final HP.");
                        Check(actual.RngDrawCount==trial.Rng && actual.CardRngDrawCount==trial.CardRng,"Animation, FX and camera consume no combat or card RNG.");
                        var projection=controller.LabDisplaySnapshot;Check(Health(projection)==Health(actual),"Final displayed HP projection agrees with authoritative state.");
                        var document=UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).First(d=>d.rootVisualElement.Q("battle-lab-controls")!=null);
                        BattlePreviewCapture.Save(Path.GetFullPath("Artifacts/CharacterCreatorStudio/M6/live-profile-battle.png"),document);
                        Stage(3);StudioBattleLabBridge.Stop();break;
                    case 20:
                        if(EditorApplication.timeSinceStartup<trial.Deadline)return;
                        Check(controller!=null && controller.LabProfileTime==trial.Clock,"Pausing Play Mode freezes the visual profile clock.");
                        EditorApplication.isPaused=false;trial.Paused=true;Stage(21);break;
                    case 3:
                        if(EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool("CharacterStudio.BattleLab.Pending",false))return;
                        Check(SceneManager.GetActiveScene().path==trial.Scene && SceneManager.GetActiveScene().isDirty==trial.Dirty,"Stopping restores the original editor scene and dirty state.");
                        Check(Hash(StudioBattleLabBridge.ScenePath)==trial.SceneHash && !BattleLabContext.IsReady,"Live profiles leave scene bytes unchanged and clear their volatile context.");
                        trial.Running=false;Store();Write();EditorApplication.update-=Tick;break;
                }
            }
            catch(Exception error){trial.Error="Stage "+trial.Stage+": "+error;trial.Running=false;Store();Write();EditorApplication.update-=Tick;StudioBattleLabBridge.Stop();}
        }
        private static string Damage(BattleState state)=>JsonConvert.SerializeObject(state.Events.Where(e=>e.Kind==BattleEventKind.DamageApplied).Select(e=>new{e.SourceId,e.TargetId,e.HitIndex,e.HitCount,e.Amount,e.HealthAfter,e.WasCritical}));
        private static string Health(BattleState state)=>JsonConvert.SerializeObject(state.Player.Fighters.Concat(state.Opponent.Fighters).Select(f=>new{f.Id,f.Health,f.Shield,f.PowerGauge}));
        private static string Hash(string path){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));}
        private static void Store()=>SessionState.SetString(Key,JsonConvert.SerializeObject(trial));
        private static void Write()=>File.WriteAllText("Artifacts/CharacterCreatorStudio/M6/live-profile-evidence.json",JsonConvert.SerializeObject(new{Success=trial.Error==null && trial.Stage==3,trial.Error,trial.Checks,trial.SawClip,trial.SawFx,trial.SawFct,trial.SawAudio,trial.Paused,RuntimeAssemblyHash=Hash(typeof(CoreBattleSceneController).Assembly.Location),EditorAssemblyHash=Hash(typeof(StudioM6LiveChecks).Assembly.Location),CapturedUtc=DateTime.UtcNow.ToString("O")},Formatting.Indented));
    }
}
#endif
