#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using FightingAllstar.Presentation.Combat;
using FightingAllstar.Presentation.Content;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    [InitializeOnLoad]
    public static class StudioBridgeLiveChecks
    {
        private const string Key="CharacterStudio.Bridge.Live";
        [Serializable] private sealed class Trial
        {
            public int Stage,Numeric; public bool Running=true,SawFct,SawAudio;
            public string Id,Draft,OriginalDraft,Scene,Error;public bool Dirty;public double Deadline;
            public List<string> Checks=new List<string>(); public Dictionary<string,string> Original=new Dictionary<string,string>();
        }
        private static Trial trial;
        static StudioBridgeLiveChecks(){trial=JsonConvert.DeserializeObject<Trial>(SessionState.GetString(Key,"null"));if(trial?.Running==true)EditorApplication.update+=Tick;}
        public static object Status=>trial==null ? (object)new{Running=false}:new{trial.Running,trial.Stage,trial.Error,Checks=trial.Checks.Count};
        public static object Start()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)throw new InvalidOperationException("Start from stable Edit Mode.");
            var window=CharacterCreatorStudioWindow.Open();var chars=CharacterObjectRegistrySO.LoadAll();
            trial=new Trial{Id="fighter.bridge-live-"+Guid.NewGuid().ToString("N").Substring(0,8),Numeric=900001,
                OriginalDraft=AssetDatabase.GetAssetPath(window.Draft),Scene=SceneManager.GetActiveScene().path,Dirty=SceneManager.GetActiveScene().isDirty};
            while(chars.Any(c=>c.ID==trial.Numeric))trial.Numeric++;
            foreach(var path in chars.SelectMany(c=>new UnityEngine.Object[]{c,c.Skill1,c.Skill2,c.Ultimate,c.PassiveDefinition}).Select(AssetDatabase.GetAssetPath).Append("Assets/Resources/CharacterObjectRegistry.asset").Distinct())trial.Original[path]=Hash(path);
            Stage(0);EditorApplication.update-=Tick;EditorApplication.update+=Tick;return Status;
        }
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);trial.Checks.Add(message);Store();}
        private static void Stage(int next){trial.Stage=next;trial.Deadline=EditorApplication.timeSinceStartup+180;Store();}
        private static void Tick()
        {
            if(trial?.Running!=true || EditorApplication.isCompiling || EditorApplication.isUpdating)return;
            try
            {
                if(trial.Stage!=99 && EditorApplication.timeSinceStartup>trial.Deadline)throw new TimeoutException("Bridge live stage "+trial.Stage);
                var w=CharacterCreatorStudioWindow.Open();var draft=AssetDatabase.LoadAssetAtPath<StudioDraftAsset>(trial.Draft ?? "");var controller=StudioBattleLabBridge.Controller;
                switch(trial.Stage)
                {
                    case 0:
                        var source=CharacterObjectRegistrySO.LoadAll().Single(c=>c.DefinitionId=="fighter.kaeya");
                        draft=StudioDraftRepository.Create(source,AssetDatabase.GenerateUniqueAssetPath(StudioDraftRepository.Root+"/Drafts/BridgeLiveVerification.asset"));trial.Draft=AssetDatabase.GetAssetPath(draft);
                        StudioDraftRepository.TryRead(draft,out var doc,out var library,out _);
                        doc.Actions.Add(new StudioActionNode{Id="bridge.live.sequence",Kind=StudioActionKind.Sequence,Children=new List<string>{"bridge.live.gauge"}});
                        doc.Actions.Add(new StudioActionNode{Id="bridge.live.gauge",Kind=StudioActionKind.Command,Command=new PassiveCommandDefinition{Kind=PassiveCommandKind.ExecuteEffect,Effect=new CardEffectOperationDefinition{Id="bridge.live.op",Kind=CardEffectOperationKind.ChangePowerGauge,Target=EffectTargetScope.AllAllies,Magnitude=2}}});
                        doc.Rules[0].ActionRootId="bridge.live.sequence";doc.Description.Locales[0].Blocks[0].Source="Published bridge live wording";
                        StudioDraftRepository.Edit(draft,doc,library,"Prepare disposable live bridge check");StudioDraftRepository.Save(draft);
                        w.LoadDraft(draft);w.ShowWorkspace("bundle");w.position=new Rect(0,0,1920,1080);
                        Field<TextField>(w,"Runtime character ID").value=trial.Id;Field<IntegerField>(w,"Unique numeric ID").value=trial.Numeric;Field<TextField>(w,"Runtime character name").value="Bridge live verification";
                        Field<EnumField>(w,"Authored wording destination").value=StudioPublishWording.Passive;
                        Stage(1);break;
                    case 1:
                        Click(w,"studio-publish-review");Stage(2);break;
                    case 2:
                        var commit=w.rootVisualElement.Q<Button>("studio-publish-commit");if(commit==null)return;
                        Check(!Directory.Exists(StudioProductionBridge.RuntimeRoot+"/"+trial.Id.Substring(8)),"Review UI validates the native package without writing source assets.");
                        var scroll=w.rootVisualElement.Q<ScrollView>("studio-document-host");scroll.ScrollTo(commit);Stage(3);break;
                    case 3:
                        Directory.CreateDirectory("Artifacts/CharacterCreatorStudio/Bridge");StudioPreviewChecks.CaptureEditorWindowEvidence(w,Path.GetFullPath("Artifacts/CharacterCreatorStudio/Bridge/publish-review.png"));
                        Click(w,"studio-publish-commit");Stage(4);break;
                    case 4:
                        if(draft?.PublishedCharacter==null)return;
                        var published=draft.PublishedCharacter;Check(published.RuntimeReady && published.StudioOwned,"The actual Publish button creates an active native CharacterObject package.");
                        var content=ScriptableObject.CreateInstance<CombatContentSource>();ContentCatalog catalog;try{catalog=content.LoadCatalog();}finally{UnityEngine.Object.DestroyImmediate(content);}
                        var native=catalog.Characters.Single(c=>c.Id==trial.Id);
                        Check(native.PassiveSource.Description.Contains("Published bridge live wording"),"Normal runtime catalog loading receives published description text.");
                        var target=catalog.Characters.First(c=>c.Id=="fighter.kyo98");Stage(5);
                        StudioBattleLabBridge.Launch(new BattleLabConfiguration{Mode=BattleLabMode.RealBattle,FirstTurn=BattleLabFirstTurn.Player,Seed=12345,
                            Actors=new List<BattleLabFighter>{new BattleLabFighter{Definition=native}},Targets=new List<BattleLabFighter>{new BattleLabFighter{Definition=target}}});break;
                    case 5:
                        if(controller?.LabCanAct!=true)return;
                        var reloaded=draft.PublishedCharacter;var definition=CharacterObjectCatalogBuilder.CreateDefinition(reloaded);
                        Check(definition.Passive.Reactions.Single(r=>r.Id=="studio.opening-team-gauge").Actions[0].Children.Count==1,"Published composed actions survive the Play Mode domain reload.");
                        Check(controller.LabFighterViewCount==2 && controller.LabSnapshot.Player.Fighters[0].Definition.Id==trial.Id,"Actual 3D battle spawns the registered native character, without the compiled-draft override.");
                        Check(controller.LabSnapshot.Player.Fighters[0].PowerGauge>=2,"The persisted composed passive executes at runtime battle start.");
                        var state=controller.LabSnapshot;var card=state.Player.Hand.First(c=>c.Kind==CardKind.Skill);
                        if(!controller.LabPlayCard(card.Id,state.Opponent.Fighters[0].Id,out var error))throw new InvalidOperationException(error);Stage(6);break;
                    case 6:
                        if(controller==null)return;
                        trial.SawFct|=controller.LabActiveFloatingText>0;trial.SawAudio|=UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(a=>a.isPlaying);
                        if(!controller.LabCanAct){Store();return;}
                        Check(controller.LabSnapshot.Events.Any(e=>e.Kind==BattleEventKind.DamageApplied && e.SourceId.Contains(trial.Id)),"The native character's card resolves real runtime damage.");
                        Check(trial.SawFct && trial.SawAudio,"Native character actions use normal 3D FCT and impact audio.");
                        Check(controller.LabInspect(controller.LabSnapshot.Player.Fighters[0].Id),"The actual runtime character inspector opens for the published character.");Stage(7);break;
                    case 7:
                        if(controller?.LabInspectorOpen!=true)return;
                        var inspector=UnityEngine.Object.FindFirstObjectByType<BattleCharacterInspector>();
                        Check(inspector.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t=>t.text.Contains("Published bridge live wording")),"The actual runtime inspector displays the authored published passive wording.");
                        var gameType=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");var game=EditorWindow.GetWindow(gameType);game.Show();Stage(8);break;
                    case 8:
                        var view=Resources.FindObjectsOfTypeAll<EditorWindow>().First(v=>v.GetType().Name=="GameView");
                        StudioPreviewChecks.CaptureEditorWindowEvidence(view,Path.GetFullPath("Artifacts/CharacterCreatorStudio/Bridge/runtime-inspector.png"));
                        Stage(9);StudioBattleLabBridge.Stop();break;
                    case 9:
                        if(EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool("CharacterStudio.BattleLab.Pending",false))return;
                        StudioProductionBridge.Rollback(draft);
                        Check(!CharacterObjectRegistrySO.LoadAll().Any(c=>c.DefinitionId==trial.Id),"Rollback removes the disposable published character from normal runtime discovery.");
                        Finish(w,true);break;
                    case 99:
                        if(EditorApplication.isPlayingOrWillChangePlaymode)return;Finish(w,false);break;
                }
            }
            catch(Exception exception){trial.Error="Stage "+trial.Stage+": "+exception;Stage(99);StudioBattleLabBridge.Stop();}
        }
        private static T Field<T>(CharacterCreatorStudioWindow w,string label)where T:VisualElement=>w.rootVisualElement.Query<T>().ToList().First(f=>f is BaseField<string> text && text.label==label || f is BaseField<int> number && number.label==label || f is BaseField<Enum> choice && choice.label==label);
        private static void Click(CharacterCreatorStudioWindow w,string name)
        {var button=w.rootVisualElement.Q<Button>(name);if(button==null || !button.enabledInHierarchy)throw new InvalidOperationException("Unavailable publish button: "+name);button.Focus();using(var submit=NavigationSubmitEvent.GetPooled())button.SendEvent(submit);}
        private static void Finish(CharacterCreatorStudioWindow window,bool success)
        {
            var draft=AssetDatabase.LoadAssetAtPath<StudioDraftAsset>(trial.Draft ?? "");
            if(draft!=null && draft.PublishedCharacter!=null)StudioProductionBridge.Rollback(draft);
            var original=AssetDatabase.LoadAssetAtPath<StudioDraftAsset>(trial.OriginalDraft);if(original!=null)window.LoadDraft(original);window.ShowWorkspace("bundle");
            if(draft!=null)AssetDatabase.DeleteAsset(trial.Draft);FightingAllstar.EditorTools.CharacterRegistrySync.Sync();
            if(success){Check(trial.Original.All(p=>Hash(p.Key)==p.Value),"All original native source and registry files remain byte-for-byte unchanged.");Check(SceneManager.GetActiveScene().path==trial.Scene && SceneManager.GetActiveScene().isDirty==trial.Dirty && !BattleLabContext.IsReady,"The original editor scene/dirty state is restored and volatile lab context is cleared.");}
            trial.Running=false;Store();EditorApplication.update-=Tick;
            File.WriteAllText("Artifacts/CharacterCreatorStudio/Bridge/live-evidence.json",JsonConvert.SerializeObject(new{Success=success,trial.Error,trial.Checks,trial.SawFct,trial.SawAudio,SourceFiles=trial.Original.Count,Scene=trial.Scene,RuntimeAssemblyHash=Hash(typeof(CharacterObject).Assembly.Location),EditorAssemblyHash=Hash(typeof(StudioBridgeLiveChecks).Assembly.Location),CoreAssemblyHash=Hash(typeof(StudioCompiler).Assembly.Location),CapturedUtc=DateTime.UtcNow.ToString("O")},Formatting.Indented));
        }
        private static string Hash(string path){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));}
        private static void Store()=>SessionState.SetString(Key,JsonConvert.SerializeObject(trial));
    }
}
#endif
