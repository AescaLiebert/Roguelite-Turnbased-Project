using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Scene-independent visual playback. Injected actors/camera/audio; no engine or account access.</summary>
    public sealed class BattlePlaybackDriver : IDisposable
    {
        private sealed class Rig
        {
            public GameObject Root;
            public ActorPresentationProfile Profile;
            public Animator Animator;
            public Transform ClipRoot;
            public Transform[] Bones;
            public Vector3[] Positions, Scales;
            public Quaternion[] Rotations;
            public Pose Home;
            public Vector3 Scale;
            public bool AnimatorEnabled;
            public bool FireEvents, ApplyRootMotion;
            public RuntimeAnimatorController PreviousController;
        }
        private readonly Dictionary<string,Rig> rigs = new Dictionary<string,Rig>(StringComparer.Ordinal);
        private readonly Dictionary<AnimationClip,AnimationClip> clips = new Dictionary<AnimationClip,AnimationClip>();
        private readonly List<Object> owned = new List<Object>();
        private readonly Dictionary<long,GameObject> effects = new Dictionary<long,GameObject>();
        private readonly HashSet<long> audible = new HashSet<long>();
        private readonly List<BattleEvent> liveFacts = new List<BattleEvent>();
        private readonly Camera camera;
        private readonly AudioSource audio;
        private readonly Transform effectsRoot;
        private readonly bool isolated;
        private Pose cameraHome;
        private float cameraFov;
        private bool disposed;
        private float lastTime = -1, liveClock;
        private int liveFrame;
        private AbilityPresentationProfile ability;
        private BattleEvent action;
        public PresentationTimeline Timeline { get; private set; }
        public IReadOnlyDictionary<string,int> Health { get; private set; } = new Dictionary<string,int>();
        public bool LiveActive => action != null && !disposed;
        public float Duration => Timeline?.Duration ?? (ability != null ? ability.Windup + ability.Action + ability.Recovery : 1.7f);
        public int EffectCount => effects.Count;
        public int AudioCuesPlayed { get; private set; }
        public float CurrentTime => liveClock;
        public int OwnedClipCount => clips.Count;
        public string LastContact { get; private set; }
        public List<string> Diagnostics { get; } = new List<string>();

        public BattlePlaybackDriver(IDictionary<string,GameObject> actors, IDictionary<string,ActorPresentationProfile> profiles,
            Camera injectedCamera, AudioSource injectedAudio, Transform injectedEffectsRoot, bool isolatedPreview)
        {
            camera = injectedCamera; audio = injectedAudio; effectsRoot = injectedEffectsRoot; isolated = isolatedPreview;
            cameraHome = camera != null ? new Pose(camera.transform.position,camera.transform.rotation) : default;
            cameraFov = camera != null ? camera.fieldOfView : 32;
            foreach (var pair in actors)
            {
                if (pair.Value == null) continue;
                var root = pair.Value; profiles.TryGetValue(pair.Key, out var source);
                var profile = source != null ? Object.Instantiate(source) : ScriptableObject.CreateInstance<ActorPresentationProfile>();
                owned.Add(profile);
                // Clone referenced ability data too: editing an asset cannot retime a running execution.
                profile.Abilities = profile.Abilities.Where(a => a != null).Select(a => { var frozen=Object.Instantiate(a); owned.Add(frozen); return frozen; }).ToList();
                var animator = !string.IsNullOrEmpty(profile.AnimatorPath) ? root.transform.Find("Model/Rig/" + profile.AnimatorPath)?.GetComponent<Animator>() : root.GetComponentInChildren<Animator>(true);
                var animatorEnabled=animator!=null && animator.enabled;
                var originalController=animator?.runtimeAnimatorController; var fireEvents=animator!=null && animator.fireEvents; var rootMotion=animator!=null && animator.applyRootMotion;
                if (animator != null && (source != null || isolated)) { if (profile.AnimatorController != null) animator.runtimeAnimatorController = profile.AnimatorController; animator.fireEvents = false; animator.applyRootMotion = false; if(isolated)animator.enabled = false; }
                var bones = root.GetComponentsInChildren<Transform>(true);
                rigs[pair.Key] = new Rig { Root=root, Profile=profile, Animator=animator, ClipRoot=animator != null ? animator.transform : root.transform.Find("Model/Rig") ?? root.transform,
                    Home=new Pose(root.transform.position,root.transform.rotation), Scale=root.transform.localScale, AnimatorEnabled=animatorEnabled, PreviousController=originalController,FireEvents=fireEvents,ApplyRootMotion=rootMotion,
                    Bones=bones,Positions=bones.Select(b=>b.localPosition).ToArray(),Rotations=bones.Select(b=>b.localRotation).ToArray(),Scales=bones.Select(b=>b.localScale).ToArray() };
                Diagnostics.AddRange(profile.Validate().Select(d=>pair.Key+": "+d));
                foreach (var socket in profile.Sockets.Where(s=>s!=null))
                    if (!string.IsNullOrEmpty(socket.Path) && root.transform.Find("Model/Rig/"+socket.Path)==null) Diagnostics.Add(pair.Key+": missing socket "+socket.Id+"; bounds fallback.");
                foreach(var clip in profile.Animations.Where(b=>b!=null).Select(b=>b.Clip).Concat(profile.Reactions.Where(b=>b!=null).Select(b=>b.Clip))
                    .Concat(profile.Abilities.SelectMany(a=>new[]{a.ActionClip,a.WindupClip,a.RecoveryClip})).Concat(animator?.runtimeAnimatorController?.animationClips ?? Array.Empty<AnimationClip>())) FreezeClip(clip);
            }
        }
        public static GameObject CreateActor(ActorPresentationProfile profile, GameObject fallback, Vector3 position, Quaternion facing, bool isolate)
        {
            var prefab=profile?.Prefab != null ? profile.Prefab : fallback;
            GameObject actor;
            if (prefab != null) actor=BattleFighterView.Create(prefab,position,facing,profile?.Yaw ?? 0);
            else { actor=GameObject.CreatePrimitive(PrimitiveType.Capsule); actor.transform.SetPositionAndRotation(position+Vector3.up,facing); }
            actor.name="Studio presentation actor";
            actor.transform.localScale *= Mathf.Max(.01f,profile?.Scale ?? 1);
            if (isolate)
            {
                actor.hideFlags=HideFlags.HideAndDontSave;
                foreach (var behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled=false;
                foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) collider.enabled=false;
                foreach (var body in actor.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic=true; body.detectCollisions=false; }
                foreach (var sound in actor.GetComponentsInChildren<AudioSource>(true)) { sound.Stop(); sound.enabled=false; }
            }
            return actor;
        }
        public bool CanPlay(BattleEvent played) => played != null && rigs.TryGetValue(played.SourceId ?? "",out var rig) && rig.Profile.Ability(AbilityPresentationProfile.AbilityKey(played.Card),played.Card?.VariantId)?.HasValidClock == true;
        public void Load(IEnumerable<BattleEvent> facts, AbilityPresentationProfile selected = null)
        {
            ThrowIfDisposed(); Cancel(); ability=selected;
            if(selected!=null && !selected.HasValidClock)throw new ArgumentException("Repair the ability timeline clock before previewing.");
            if(selected!=null) { ability=Object.Instantiate(selected);owned.Add(ability);FreezeClip(ability.ActionClip);FreezeClip(ability.WindupClip);FreezeClip(ability.RecoveryClip); }
            Timeline=PresentationTimeline.Build(facts,selected?.Windup ?? .2f,selected?.Action ?? 1.2f,selected?.Recovery ?? .3f,selected?.ContactTimes());
            Sample(0,false);
        }
        public IEnumerator BeginAction(BattleEvent played)
        {
            Cancel(); action=played.Clone(); ability=rigs[played.SourceId].Profile.Ability(AbilityPresentationProfile.AbilityKey(played.Card),played.Card?.VariantId);
            foreach(var rig in rigs.Values)
            {
                rig.Home=new Pose(rig.Root.transform.position,rig.Root.transform.rotation);rig.Scale=rig.Root.transform.localScale;
                if(rig.Animator!=null)rig.Animator.enabled=false;
            }
            if(camera!=null) {cameraHome=new Pose(camera.transform.position,camera.transform.rotation);cameraFov=camera.fieldOfView;}
            liveClock=0;liveFrame=Time.frameCount; liveFacts.Add(action); lastTime=-1;
            yield return WaitContact(1);
        }
        public IEnumerator WaitContact(int index)
        {
            if (!LiveActive) yield break;
            var count=Math.Max(1,action.HitCount);
            var fraction=ability.ContactTimes().TryGetValue(index,out var at) ? at : Mathf.Clamp01((float)index/count);
            var deadline=Mathf.Max(0,ability.Windup)+Mathf.Max(.01f,ability.Action)*Mathf.Clamp01(fraction);
            while (LiveActive && liveClock < deadline) { TickLive(); yield return null; }
            TickLive();
        }
        public void Record(BattleEvent fact)
        {
            if (!LiveActive || fact == null) return;
            TickLive();liveFacts.Add(fact.Clone());liveTimes.Add(liveClock);
            TickLive();
        }
        private readonly List<float> liveTimes = new List<float>();
        public void TickLive()
        { if(!LiveActive)return; if(liveFrame!=Time.frameCount) { liveClock+=Mathf.Min(.1f,Time.unscaledDeltaTime);liveFrame=Time.frameCount; }Render(liveClock,true,true); }
        public IEnumerator FinishAction()
        {
            if (!LiveActive) yield break;
            var deadline=Mathf.Clamp(ability.Windup+ability.Action+ability.Recovery,.01f,30);
            for(var i=1;i<liveFacts.Count;i++) if(liveFacts[i].Kind==BattleEventKind.DamageApplied || liveFacts[i].Kind==BattleEventKind.FighterDefeated)deadline=Mathf.Max(deadline,liveTimes[i-1]+.5f);
            while (LiveActive && liveClock<deadline) { TickLive(); yield return null; }
            Cancel();
        }
        public void Sample(float seconds,bool emitAudio=false) { ThrowIfDisposed(); Render(Mathf.Clamp(seconds,0,Duration),emitAudio,false); }
        private void Render(float time,bool emitAudio,bool realtime)
        {
            var rows = realtime ? liveFacts.Select((f,i)=>new VisualFact { Fact=f,Time=i==0?0:liveTimes[i-1] }).ToList()
                : Timeline?.Entries.Select(e=>new VisualFact { Fact=e.Fact,Time=e.Time }).ToList() ?? new List<VisualFact>();
            foreach(var rig in rigs.Values) { Reset(rig); SampleClip(rig,rig.Profile.Resolve("Idle",rig.Animator),time); }
            if(camera != null) { camera.transform.SetPositionAndRotation(cameraHome.position,cameraHome.rotation); camera.fieldOfView=cameraFov; }
            var current=rows.LastOrDefault(r=>r.Time<=time && r.Fact.Kind==BattleEventKind.CardPlayed);
            var activeAbility=ability;
            if (current != null && rigs.TryGetValue(current.Fact.SourceId ?? "",out var source))
            {
                activeAbility=ability ?? source.Profile.Ability(AbilityPresentationProfile.AbilityKey(current.Fact.Card),current.Fact.Card?.VariantId);
                var windup=activeAbility?.Windup ?? .2f; var duration=Mathf.Max(.01f,activeAbility?.Action ?? 1.2f); var recovery=activeAbility?.Recovery ?? .3f;
                var elapsed=time-current.Time;
                var clip=elapsed<windup ? activeAbility?.WindupClip ?? source.Profile.Resolve("WindUp",source.Animator) : elapsed<windup+duration ? activeAbility?.ActionClip ?? source.Profile.Resolve("Action",source.Animator) : activeAbility?.RecoveryClip ?? source.Profile.Resolve("Recovery",source.Animator);
                var clipTime=elapsed<windup ? elapsed : elapsed<windup+duration ? elapsed-windup : elapsed-windup-duration;
                if (elapsed < windup+duration+recovery)
                {
                    var pulse=Mathf.Sin(Mathf.Clamp01(elapsed/(windup+duration+recovery))*Mathf.PI);
                    var targetId=current.Fact.TargetIds?.FirstOrDefault() ?? current.Fact.TargetId;
                    var target=targetId!=null && rigs.TryGetValue(targetId,out var selectedTarget) ? selectedTarget : rigs.FirstOrDefault(p=>p.Key!=current.Fact.SourceId).Value;
                    var direction=target!=null ? Vector3.ProjectOnPlane(target.Home.position-source.Home.position,Vector3.up).normalized : source.Home.rotation*Vector3.forward;
                    source.Root.transform.position+=direction*(Mathf.Min(source.Profile.Approach,target!=null ? Vector3.Distance(source.Home.position,target.Home.position)*.25f:1)*pulse);
                    if (!SampleClip(source,clip,clipTime)) source.Root.transform.rotation*=Quaternion.Euler(-8*pulse,0,0);
                    if(camera!=null && activeAbility?.CameraCue==true) { var center=source.Root.transform.position+Vector3.up; camera.transform.position=center+activeAbility.CameraOffset; camera.transform.LookAt(center); camera.fieldOfView=activeAbility.CameraFov; }
                }
            }
            Health=realtime ? HealthFrom(rows,time) : Timeline?.HealthAt(time) ?? new Dictionary<string,int>();
            LastContact=null;
            foreach(var rig in rigs)
            {
                var defeated=rows.LastOrDefault(r=>r.Time<=time && (r.Fact.TargetId ?? r.Fact.SourceId)==rig.Key && r.Fact.Kind==BattleEventKind.FighterDefeated);
                if(defeated != null) { if(!SampleClip(rig.Value,rig.Value.Profile.Resolve("Death",rig.Value.Animator),time-defeated.Time)) rig.Value.Root.transform.rotation*=Quaternion.Euler(0,0,-80*Mathf.Clamp01((time-defeated.Time)/.5f)); continue; }
                var hit=rows.Where(r=>r.Time<=time && time-r.Time<=.5f && r.Fact.TargetId==rig.Key && r.Fact.Kind==BattleEventKind.DamageApplied)
                    .OrderBy(r=>r.Fact.HasHitReaction && r.Fact.Reaction!=HitReaction.None ? (int)r.Fact.Reaction+1 : r.Fact.HasHitReaction ? 0:1).ThenBy(r=>r.Time).LastOrDefault();
                if(hit==null || time-hit.Time>.5f) continue;
                var fact=hit.Fact; var reaction=fact.HasHitReaction ? fact.Reaction : HitReaction.Hit;
                if(reaction==HitReaction.None) continue;
                var binding=rig.Value.Profile.Reactions.FirstOrDefault(r=>r!=null && r.Reaction==reaction);
                if (!SampleClip(rig.Value,binding?.Clip,time-hit.Time) && (binding==null || binding.ProceduralFallback))
                {
                    var pulse=Mathf.Sin(Mathf.Clamp01((time-hit.Time)/.5f)*Mathf.PI);
                    rig.Value.Root.transform.position+=rig.Value.Home.rotation*Vector3.back*(.2f*pulse);
                    if(reaction==HitReaction.KnockUp) rig.Value.Root.transform.position+=Vector3.up*pulse;
                    rig.Value.Root.transform.rotation*=Quaternion.Euler(-12*pulse,0,reaction==HitReaction.KnockDown ? 60*pulse:0);
                }
            }
            var aliveEffects=new HashSet<long>();
            foreach(var row in rows)
            {
                var fact=row.Fact;
                if(fact.Kind!=BattleEventKind.DamageApplied || fact.HitIndex<=0 || row.Time>time) continue;
                LastContact=PresentationTimeline.ContactId(fact.HitIndex)+" · event #"+fact.Id+" · "+fact.Amount;
                var cue=activeAbility?.Contacts.FirstOrDefault(c=>c!=null && c.Ordinal==fact.HitIndex);
                if(cue==null || time-row.Time>Mathf.Clamp(cue.Lifetime,.01f,10) || !rigs.TryGetValue(fact.TargetId ?? "",out var recipient)) continue;
                var key=fact.Id;
                if(cue.Effect!=null)
                {
                    aliveEffects.Add(key);
                    if(!effects.TryGetValue(key,out var effect) || effect==null)
                    {
                        effect=Object.Instantiate(cue.Effect,effectsRoot,false); effects[key]=effect;
                        if(isolated) { effect.hideFlags=HideFlags.HideAndDontSave; foreach(var script in effect.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled=false; }
                    }
                    effect.transform.position=Socket(recipient,cue.Socket);
                    foreach(var particles in effect.GetComponentsInChildren<ParticleSystem>(true)) { particles.useAutoRandomSeed=false; particles.randomSeed=(uint)Math.Max(1,key); particles.Simulate(Mathf.Max(0,time-row.Time),false,true,true); }
                }
                if(emitAudio && Application.isPlaying && audio!=null && cue.Audio!=null && lastTime<=row.Time && audible.Add(key)) { audio.PlayOneShot(cue.Audio,Mathf.Clamp01(cue.Volume));AudioCuesPlayed++; }
            }
            foreach(var key in effects.Keys.Where(k=>!aliveEffects.Contains(k)).ToList()) { Destroy(effects[key]); effects.Remove(key); }
            if(time<lastTime) audible.Clear(); lastTime=time;
        }
        private sealed class VisualFact { public BattleEvent Fact; public float Time; }
        private static Dictionary<string,int> HealthFrom(List<VisualFact> rows,float time)
        { var d=new Dictionary<string,int>(); foreach(var r in rows) if(r.Time<=time && r.Fact.TargetId!=null && (r.Fact.Kind==BattleEventKind.DamageApplied || r.Fact.Kind==BattleEventKind.HealApplied)) d[r.Fact.TargetId]=r.Fact.HealthAfter; return d; }
        private static Vector3 Socket(Rig rig,string id)
        {
            var socket=rig.Profile.Sockets.FirstOrDefault(s=>s!=null && s.Id==id);
            var bone=socket!=null && !string.IsNullOrEmpty(socket.Path) ? rig.Root.transform.Find("Model/Rig/"+socket.Path) : null;
            return bone!=null ? bone.TransformPoint(socket.Offset) : rig.Root.transform.TransformPoint(socket?.Offset ?? rig.Profile.Bounds.center);
        }
        private static void Reset(Rig rig)
        {
            for(var i=1;i<rig.Bones.Length;i++) if(rig.Bones[i]!=null) { rig.Bones[i].localPosition=rig.Positions[i]; rig.Bones[i].localRotation=rig.Rotations[i]; rig.Bones[i].localScale=rig.Scales[i]; }
            rig.Root.transform.SetPositionAndRotation(rig.Home.position,rig.Home.rotation); rig.Root.transform.localScale=rig.Scale;
        }
        private bool SampleClip(Rig rig,AnimationClip clip,float time)
        {
            if(clip==null || rig.ClipRoot==null) return false;
            var safe=FreezeClip(clip);
            var root=rig.ClipRoot; var pose=new Pose(root.localPosition,root.localRotation); var scale=root.localScale;
            safe.SampleAnimation(root.gameObject,Mathf.Clamp(time,0,clip.length));
            if(rig.Profile.RootMotion==VisualRootMotion.Locked) { root.localPosition=pose.position; root.localRotation=pose.rotation; root.localScale=scale; }
            return true;
        }
        private AnimationClip FreezeClip(AnimationClip clip)
        { if(clip==null)return null; if(!clips.TryGetValue(clip,out var safe)) { safe=Object.Instantiate(clip);safe.events=Array.Empty<AnimationEvent>();clips[clip]=safe; }return safe; }
        public void Cancel()
        {
            foreach(var effect in effects.Values) Destroy(effect); effects.Clear(); audible.Clear(); liveFacts.Clear(); liveTimes.Clear();
            if(audio!=null) audio.Stop(); foreach(var rig in rigs.Values) {if(rig.Root!=null && (isolated || LiveActive)) Reset(rig);if(!isolated && rig.Animator!=null)rig.Animator.enabled=rig.AnimatorEnabled;}
            if(camera!=null && (isolated || LiveActive)) { camera.transform.SetPositionAndRotation(cameraHome.position,cameraHome.rotation); camera.fieldOfView=cameraFov; }
            action=null; lastTime=-1; LastContact=null; Health=new Dictionary<string,int>();
        }
        private void ThrowIfDisposed() { if(disposed) throw new ObjectDisposedException(nameof(BattlePlaybackDriver)); }
        private static void Destroy(Object obj) { if(obj==null)return; if(Application.isPlaying) Object.Destroy(obj); else Object.DestroyImmediate(obj); }
        public void Dispose()
        { if(disposed)return; Cancel(); foreach(var rig in rigs.Values) if(rig.Animator!=null) { rig.Animator.runtimeAnimatorController=rig.PreviousController;rig.Animator.fireEvents=rig.FireEvents;rig.Animator.applyRootMotion=rig.ApplyRootMotion;rig.Animator.enabled=rig.AnimatorEnabled; } foreach(var clip in clips.Values) Destroy(clip); clips.Clear(); foreach(var obj in owned) Destroy(obj); owned.Clear(); rigs.Clear(); disposed=true; }
    }
}
