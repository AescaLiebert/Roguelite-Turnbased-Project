#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Combat;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    /// <summary>Native preview acceptance. Synthetic visual assets only; no roster character is installed.</summary>
    public static class StudioM6Checks
    {
        public static string FixtureDirectory { get; private set; }
        public static string Run()
        {
            var passed=new List<string>();
            Action<bool,string> check=(ok,message)=>{if(!ok)throw new InvalidOperationException("M6: "+message);passed.Add(message);};
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var wasDirty=scene.isDirty;
            var w=CharacterCreatorStudioWindow.Open();w.ShowWorkspace("presentation");
            var actual=w.RecordPresentationAction().Select(e=>e.Clone()).ToList();
            check(actual.Count(e=>e.Kind==BattleEventKind.DamageApplied)==4,"Studio records four actual Kaeya engine contacts without entering Play Mode.");
            check(!EditorApplication.isPlaying,"Recording does not enter Play Mode.");
            var parent="Assets/Project/Editor/CharacterCreatorStudio";
            FixtureDirectory=AssetDatabase.GenerateUniqueAssetPath(parent+"/M6VisualVerification");AssetDatabase.CreateFolder(parent,Path.GetFileName(FixtureDirectory));
            var model=new GameObject("Acceptance rig");var visual=GameObject.CreatePrimitive(PrimitiveType.Capsule);visual.name="Visual";visual.transform.SetParent(model.transform,false);visual.transform.localPosition=Vector3.up;
            var prefab=PrefabUtility.SaveAsPrefabAsset(model,FixtureDirectory+"/SyntheticRig.prefab");UnityEngine.Object.DestroyImmediate(model);
            var clip=new AnimationClip{name="Acceptance action",legacy=true};clip.SetCurve("Visual",typeof(Transform),"localPosition.x",AnimationCurve.Linear(0,0,1,.5f));
            clip.events=new[]{new AnimationEvent{time=.1f,functionName="MustNeverExecuteGameplay"}};AssetDatabase.CreateAsset(clip,FixtureDirectory+"/Action.anim");
            var ability=ScriptableObject.CreateInstance<AbilityPresentationProfile>();ability.SkillId=actual.First(e=>e.Kind==BattleEventKind.CardPlayed).Card.SkillId;ability.ActionClip=clip;
            for(var i=1;i<=4;i++)ability.Contacts.Add(new AbilityContactCue{Ordinal=i,At=i/4f});
            ability.Contacts[0].Effect=prefab;
            ability.Contacts[0].Audio=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Project/Sound/mw-SFX-sword-hit5.wav");
            AssetDatabase.CreateAsset(ability,FixtureDirectory+"/Ability.asset");
            var actor=ScriptableObject.CreateInstance<ActorPresentationProfile>();actor.Prefab=prefab;actor.Abilities.Add(ability);actor.Sockets[0].Path="Missing/Bone";AssetDatabase.CreateAsset(actor,FixtureDirectory+"/Actor.asset");
            AssetDatabase.SaveAssets();
            var bytes=SnapshotJson.Serialize(actual);var originalDuration=ability.Action;
            StudioPresentationViewport viewport=null;UnityEngine.SceneManagement.Scene previewScene=default;
            try
            {
                viewport=new StudioPresentationViewport(actor,null,prefab,actual,ability);previewScene=viewport.Actor.scene;
                check(previewScene!=scene,"Viewport owns an isolated preview scene.");
                check(viewport.Driver.Diagnostics.Any(d=>d.Contains("missing socket")),"Missing sockets report their deterministic fallback.");
                check(viewport.Driver.OwnedClipCount==1,"Authored clips are copied into an owned animation cache.");
                check(clip.events.Length==1,"Animation-event suppression preserves the source clip.");
                viewport.Sample(.7f);var first=viewport.Actor.transform.position;var bone=viewport.Actor.transform.Find("Model/Rig/Visual").localPosition;
                check(viewport.Driver.EffectCount==1,"The committed first contact instantiates its authored socket effect.");
                check(bone.x>.05f,"The isolated viewport samples an authored animation clip on the rig.");
                viewport.Sample(0);check(viewport.Actor.transform.Find("Model/Rig/Visual").localPosition.x==0,"Scrubbing backwards resets the authored bone pose.");
                viewport.Sample(.7f);check(Vector3.Distance(first,viewport.Actor.transform.position)<.0001f,"Repeated absolute samples reproduce actor motion.");
                ability.Action=7;clip.SetCurve("Visual",typeof(Transform),"localPosition.x",AnimationCurve.Linear(0,0,1,9));
                viewport.Sample(.7f);check(Vector3.Distance(bone,viewport.Actor.transform.Find("Model/Rig/Visual").localPosition)<.0001f,"Frozen profile and clip copies ignore source edits during playback.");
                check(viewport.Driver.Duration<3,"Source clock edits cannot retime the frozen preview.");
                viewport.Sample(viewport.Driver.Duration);var last=actual.Last(e=>e.Kind==BattleEventKind.DamageApplied);
                check(viewport.Driver.Health[last.TargetId]==last.HealthAfter,"Final HP projection matches the engine's committed damage facts.");
                check(viewport.Target.transform.rotation==Quaternion.Euler(0,-90,0),"Nonfatal damage returns its target to rest.");
                check(bytes==SnapshotJson.Serialize(actual),"All authored playback leaves input combat facts unchanged.");
                Directory.CreateDirectory("Artifacts/CharacterCreatorStudio/M6");viewport.Sample(.75f);viewport.Capture("Artifacts/CharacterCreatorStudio/M6/authored-viewport.png");
                viewport.Play();check(viewport.IsPlaying,"Preview play starts its own editor clock.");viewport.Pause();check(!viewport.IsPlaying,"Pause releases the editor clock.");
            }
            finally { viewport?.Dispose();ability.Action=originalDuration;clip.SetCurve("Visual",typeof(Transform),"localPosition.x",AnimationCurve.Linear(0,0,1,.5f));EditorUtility.SetDirty(ability);EditorUtility.SetDirty(clip);AssetDatabase.SaveAssets(); }
            check(!previewScene.IsValid(),"Disposal closes the preview scene.");
            check(viewport.Driver.OwnedClipCount==0 && viewport.Driver.EffectCount==0,"Disposal releases owned clip and effect resources.");
            var runtimeRig=BattlePlaybackDriver.CreateActor(actor,null,Vector3.zero,Quaternion.identity,false);var runtimeAnimator=runtimeRig.AddComponent<Animator>();
            using(var runtimeDriver=new BattlePlaybackDriver(new Dictionary<string,GameObject>{{"actor",runtimeRig}},new Dictionary<string,ActorPresentationProfile>{{"actor",actor}},null,null,null,false))
                check(runtimeAnimator.enabled,"An idle runtime profile preserves Animator availability for unprofiled fallback actions.");
            UnityEngine.Object.DestroyImmediate(runtimeRig);
            for(var iteration=0;iteration<5;iteration++) using(var repeat=new StudioPresentationViewport(actor,null,prefab,actual,ability)){repeat.Sample(repeat.Driver.Duration);}
            check(scene==UnityEngine.SceneManagement.SceneManager.GetActiveScene() && scene.isDirty==wasDirty,"Repeated preview creation preserves the editor scene and dirty state.");
            var reactionFacts=new List<BattleEvent>{new BattleEvent{Id=101,Kind=BattleEventKind.CardPlayed,SourceId="actor",Card=new CardState{SkillId=ability.SkillId}},
                new BattleEvent{Id=102,Kind=BattleEventKind.DamageApplied,SourceId="actor",TargetId="target",HitIndex=1,HitCount=1,HasHitReaction=true,Reaction=HitReaction.KnockUp,HealthAfter=1},
                new BattleEvent{Id=103,Kind=BattleEventKind.DamageApplied,SourceId="actor",TargetId="target",HitIndex=1,HitCount=1,HasHitReaction=true,Reaction=HitReaction.Hit,HealthAfter=1}};
            using(var reactions=new StudioPresentationViewport(actor,null,prefab,reactionFacts,ability))
            {
                var at=reactions.Driver.Timeline.Entries[1].Time;reactions.Sample(at+.15f);
                check(reactions.Target.transform.position.y>.1f,"Overlapping impacts retain the stronger committed reaction.");
                reactions.Sample(reactions.Driver.Duration);check(reactions.Target.activeSelf,"A lethal save without committed defeat never hides the actor.");
            }
            reactionFacts.Add(new BattleEvent{Id=104,Kind=BattleEventKind.FighterDefeated,SourceId="target"});
            using(var defeated=new StudioPresentationViewport(actor,null,prefab,reactionFacts,ability)){defeated.Sample(defeated.Driver.Duration);check(Quaternion.Angle(defeated.Target.transform.rotation,Quaternion.Euler(0,-90,0))>20,"Committed defeat takes precedence over flinch.");}
            ability.Contacts.Add(new AbilityContactCue{Ordinal=9});check(ability.Validate(4).Any(d=>d.Contains("Unmatched")),"Extra markers are diagnosed against the recorded hit count.");ability.Contacts.RemoveAt(ability.Contacts.Count-1);
            ability.Action=float.NaN;check(!ability.HasValidClock,"Malformed clocks cannot enter runtime profile playback.");ability.Action=originalDuration;EditorUtility.SetDirty(ability);AssetDatabase.SaveAssets();
            // Populate the designer's workspace with the verified reusable fixture, not a roster registration.
            w.UsePresentationProfiles(actor,null,ability);
            var ultimate=UnityEngine.Object.Instantiate(ability);ultimate.SkillId="$ultimate";w.UsePresentationProfiles(actor,null,ultimate);
            check(w.RecordPresentationAction().First(f=>f.Kind==BattleEventKind.CardPlayed).Card.Kind==CardKind.Ultimate,"The recorder supports an actual ultimate through its stable $ultimate binding.");
            UnityEngine.Object.DestroyImmediate(ultimate);w.UsePresentationProfiles(actor,null,ability);w.RecordPresentationAction();w.ShowWorkspace("presentation");
            var variant=UnityEngine.Object.Instantiate(ability);variant.VariantId="fixture.ex";actor.Abilities.Add(variant);
            check(actor.Ability(ability.SkillId,"fixture.ex")==variant && actor.Ability(ability.SkillId,"other")==ability,"Variant profiles resolve exact bindings before the base visual fallback.");actor.Abilities.Remove(variant);UnityEngine.Object.DestroyImmediate(variant);
            var result=JsonConvert.SerializeObject(new{Success=true,Checks=passed,FixtureDirectory,Scope="Synthetic rig/clip native viewport acceptance and real engine action recording. No playable character added."},Formatting.Indented);
            File.WriteAllText("Artifacts/CharacterCreatorStudio/M6/native-preview-evidence.json",result);return result;
        }
    }
}
#endif
