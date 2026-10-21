#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public sealed partial class CharacterCreatorStudioWindow
    {
        [SerializeField] private ActorPresentationProfile _presentationActor, _presentationTarget;
        [SerializeField] private AbilityPresentationProfile _presentationAbility;
        [SerializeField] private int _presentationRank=1;
        [NonSerialized] private StudioPresentationViewport _presentationViewport;
        [NonSerialized] private List<BattleEvent> _presentationFacts=new List<BattleEvent>();
        public StudioPresentationViewport PresentationViewport => _presentationViewport;
        public void UsePresentationProfiles(ActorPresentationProfile actor,ActorPresentationProfile target,AbilityPresentationProfile ability)
        { _presentationActor=actor;_presentationTarget=target;_presentationAbility=ability;ShowWorkspace("presentation"); }
        private void BuildPresentationEvidence()
        {
            if(_presentationViewport==null) { Body(_problems,"Record an actual ability to inspect its visual contacts and HP projection.");return; }
            var driver=_presentationViewport.Driver;
            if(_evidenceTab=="state") foreach(var pair in driver.Health)Body(_problems,pair.Key+" · committed HP at sample: "+pair.Value,"studio-code");
            else if(_evidenceTab=="trace") foreach(var row in driver.Timeline.Entries)
            { var entry=row;Button(_problems,entry.Time.ToString("0.00")+"s · #"+entry.Fact.Id+" · "+entry.Fact.Kind+" · "+(entry.ContactId ?? ""),()=>_presentationViewport?.Sample(entry.Time)); }
            else
            {
                Body(_problems,driver.Timeline.Entries.Count+" frozen engine facts · "+driver.Timeline.Entries.Count(e=>e.Fact.Kind==BattleEventKind.DamageApplied)+" committed damage results · visual edits cannot modify gameplay.","studio-success");
                foreach(var message in driver.Diagnostics)Body(_problems,message,"studio-warning");
                if(_presentationAbility!=null)foreach(var message in _presentationAbility.Validate(_presentationFacts.Select(f=>f.HitCount).DefaultIfEmpty(0).Max()))Body(_problems,message,"studio-warning");
            }
        }
        private void BuildPresentation()
        {
            Heading(_host,"Actor & presentation lab");
            Body(_host,"Author reusable visual profiles, then record an actual engine action and scrub its immutable contacts. This isolated viewport does not enter Play Mode or change combat outcomes.","studio-subtle");
            ProfilePicker<ActorPresentationProfile>(_host,"Actor profile",_presentationActor,v=>{_presentationActor=v;Rebuild();});
            ProfilePicker<ActorPresentationProfile>(_host,"Target profile",_presentationTarget,v=>{_presentationTarget=v;Rebuild();});
            ProfilePicker<AbilityPresentationProfile>(_host,"Ability profile",_presentationAbility,v=>{_presentationAbility=v;Rebuild();});
            var create=Row(_host);
            Button(create,"New actor profile",()=>{_presentationActor=NewPresentationAsset<ActorPresentationProfile>("Actor");Rebuild();});
            Button(create,"New ability profile",()=>{
                _presentationAbility=NewPresentationAsset<AbilityPresentationProfile>("Ability");
                _presentationAbility.SkillId=_document.EditingCharacter?.Skills.FirstOrDefault()?.Id;
                if(_presentationActor!=null) { Undo.RecordObject(_presentationActor,"Link ability profile"); _presentationActor.Abilities.Add(_presentationAbility); EditorUtility.SetDirty(_presentationActor); }
                EditorUtility.SetDirty(_presentationAbility);Rebuild();
            });
            Button(create,"Save profile assets",()=>{AssetDatabase.SaveAssets();SetStatus("Presentation profiles saved; gameplay sources are unchanged.");});
            var rank=new IntegerField("Card rank (1–3)"){value=_presentationRank}; rank.RegisterValueChangedCallback(e=>_presentationRank=Mathf.Clamp(e.newValue,1,3));_host.Add(rank);
            if(_presentationAbility!=null && _compiled?.Success==true)
            {
                var skills=_compiled.Character.Skills.Select(s=>s.Id).Concat(new[]{"$ultimate"}).ToList();
                if(skills.Count>0) { var skill=new PopupField<string>("Actor skill ID",skills,skills.Contains(_presentationAbility.SkillId)?_presentationAbility.SkillId:skills[0]);
                    skill.RegisterValueChangedCallback(e=>{Undo.RecordObject(_presentationAbility,"Bind presentation skill");_presentationAbility.SkillId=e.newValue;EditorUtility.SetDirty(_presentationAbility);});_host.Add(skill); }
            }
            var actions=Row(_host);
            Button(actions,"Record selected ability",()=>Guard(()=>{RecordPresentationAction();Rebuild();})).name="studio-presentation-record";
            Button(actions,"Capture latest Battle Lab action",()=>Guard(()=>{CapturePresentationAction();Rebuild();}));
            Button(actions,"Rebuild visual preview",Rebuild);
            Body(_host,"Save stores reusable profile assets. Lab setup remains disposable. Profile edits take effect after rebuilding this preview or restarting Battle Lab. Clip animation events are suppressed; scrubbing is silent.","studio-subtle");
            if(_presentationFacts.Count>0 && (_presentationAbility==null || _presentationAbility.HasValidClock) && (_presentationActor==null || _presentationActor.HasValidTransform) && (_presentationTarget==null || _presentationTarget.HasValidTransform))
            {
                var fallback=_draft?.SourceCharacter?.Fighter3DPrefab ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefabs/In-Game-CharacterPrefab.prefab");
                _presentationViewport=new StudioPresentationViewport(_presentationActor,_presentationTarget,fallback,_presentationFacts,_presentationAbility);
                _host.Add(_presentationViewport.Root);
                _host.schedule.Execute(()=>{if(_page=="presentation" && _evidenceTab=="state")RefreshProblems();}).Every(300);
                Section(_host,"COMMITTED CONTACTS");
                foreach(var entry in _presentationViewport.Driver.Timeline.Entries.Where(e=>e.Fact.Kind==BattleEventKind.DamageApplied || e.Fact.Kind==BattleEventKind.HealApplied || e.Fact.Kind==BattleEventKind.FighterDefeated))
                {
                    var row=entry;
                    Button(_host,row.Time.ToString("0.00")+"s · "+(row.ContactId ?? row.Fact.Kind.ToString())+" · #"+row.Fact.Id+" → "+row.Fact.TargetId+" · "+row.Fact.Amount+" · HP "+row.Fact.HealthAfter,()=>_presentationViewport?.Sample(row.Time));
                }
                foreach(var diagnostic in _presentationViewport.Driver.Diagnostics) Body(_inspector,diagnostic,"studio-warning");
            }
            else Body(_host,"Record an ability to populate the viewport and contact timeline. Choose a supported skill ID and repair any invalid profile clock/transform values before rebuilding.","studio-subtle");
            Section(_inspector,"PROFILE EDITOR");
            ProfileEditor(_inspector,_presentationActor,"Actor · model, rig, sockets & reactions");
            ProfileEditor(_inspector,_presentationTarget,"Target · model & reactions");
            ProfileEditor(_inspector,_presentationAbility,"Ability · phases, contacts, FX, audio & camera");
            Section(_inspector,"VALIDATION");
            if(_presentationActor!=null) foreach(var message in _presentationActor.Validate())Body(_inspector,message,"studio-warning");
            if(_presentationAbility!=null) foreach(var message in _presentationAbility.Validate(_presentationFacts.Where(f=>f.Kind==BattleEventKind.DamageApplied).Select(f=>f.HitCount).DefaultIfEmpty(0).Max()))Body(_inspector,message,"studio-warning");
            Body(_inspector,"Missing clips or sockets use bounded visual fallbacks. Extra/unmatched contact markers are diagnostics and cannot add hits. A committed defeat wins over flinch; lethal prevention keeps the actor alive.","studio-subtle");
            Button(_inspector,"Test these profiles in Battle Lab",()=>{
                EnsureLabSlots();_labActors[0].Profile=_presentationActor;_labTargets[0].Profile=_presentationTarget;ShowWorkspace("lab");
            });
        }
        private static void ProfilePicker<T>(VisualElement parent,string label,T selected,Action<T> changed) where T:UnityEngine.Object
        { var field=new ObjectField(label){objectType=typeof(T),allowSceneObjects=false,value=selected};field.RegisterValueChangedCallback(e=>changed(e.newValue as T));parent.Add(field); }
        private static void ProfileEditor(VisualElement parent,UnityEngine.Object asset,string title)
        {
            if(asset==null)return;
            var fold=new Foldout{text=title,value=true}; parent.Add(fold);
            var serialized=new SerializedObject(asset); var iterator=serialized.GetIterator(); iterator.NextVisible(true);
            while(iterator.NextVisible(false)) { var field=new PropertyField(iterator.Copy());field.Bind(serialized);fold.Add(field); }
        }
        private static T NewPresentationAsset<T>(string name) where T:ScriptableObject
        {
            const string directory="Assets/Project/Editor/CharacterCreatorStudio/PresentationProfiles";
            if(!AssetDatabase.IsValidFolder(directory)) AssetDatabase.CreateFolder("Assets/Project/Editor/CharacterCreatorStudio","PresentationProfiles");
            var asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,AssetDatabase.GenerateUniqueAssetPath(directory+"/"+name+".asset"));Undo.RegisterCreatedObjectUndo(asset,"Create presentation profile");return asset;
        }
        public IReadOnlyList<BattleEvent> RecordPresentationAction()
        {
            Compile();if(_compiled?.Success!=true)throw new InvalidOperationException("Repair the current gameplay draft before recording.");
            var config=BuildLabConfiguration();config.Mode=BattleLabMode.TrainingBattle;config.FirstTurn=BattleLabFirstTurn.Player;
            config.Actors=config.Actors.Take(1).ToList();config.Targets=config.Targets.Take(1).ToList();
            var initial=BattleLabSimulation.Create(config,out _);var session=new LocalBattleSession(initial,true);var state=session.GetSnapshot();
            var skill=_presentationAbility?.SkillId ?? state.Player.Hand.FirstOrDefault(c=>c.Rank==_presentationRank)?.SkillId;
            var card=state.Player.Hand.FirstOrDefault(c=>AbilityPresentationProfile.AbilityKey(c)==skill && (c.Kind==CardKind.Ultimate || c.Rank==_presentationRank) &&
                (string.IsNullOrEmpty(_presentationAbility?.VariantId) || c.VariantId==_presentationAbility.VariantId));
            if(card==null)throw new InvalidOperationException("The selected skill ID/rank is not available on Actor 1. Choose its authored skill ID.");
            var draft=new PlanDraft(state);var target=card.TargetScope==EffectTargetScope.SelectedAlly ? card.OwnerFighterId : state.Opponent.Fighters[0].Id;
            if(!draft.QueuePlay(card.Id,target,out var error) || !session.Submit(draft.BuildPlan("studio.presentation:"+Guid.NewGuid()),out error))throw new InvalidOperationException(error);
            _presentationFacts=SelectRecordedAction(session.GetEventsAfter(state.Events.LastOrDefault()?.Id ?? 0),card.OwnerFighterId,card.SkillId);
            return _presentationFacts.Select(f=>f.Clone()).ToList();
        }
        private void CapturePresentationAction()
        {
            var facts=StudioBattleLabBridge.Controller?.LabSnapshot?.Events;
            if(facts==null)throw new InvalidOperationException("Launch Battle Lab and play a card before capturing.");
            var played=facts.LastOrDefault(f=>f.Kind==BattleEventKind.CardPlayed);
            if(played==null)throw new InvalidOperationException("No committed card action yet.");
            _presentationFacts=SelectRecordedAction(facts,played.SourceId,played.Card?.SkillId);
        }
        private static List<BattleEvent> SelectRecordedAction(IEnumerable<BattleEvent> facts,string source,string skill)
        {
            var list=facts.ToList();var start=list.FindLastIndex(f=>f.Kind==BattleEventKind.CardPlayed && f.SourceId==source && f.Card?.SkillId==skill);
            if(start<0)throw new InvalidOperationException("No matching committed action.");
            var result=new List<BattleEvent>();foreach(var fact in list.Skip(start)) { if(result.Count>0 && fact.Kind==BattleEventKind.CardPlayed)break;result.Add(fact.Clone());if(fact.Kind==BattleEventKind.ActionCompleted)break; }
            return result;
        }
    }
}
#endif
