#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    /// <summary>Owns an isolated preview scene and a clock; shares the runtime playback driver.</summary>
    public sealed class StudioPresentationViewport : IDisposable
    {
        private readonly PreviewRenderUtility preview;
        private readonly IMGUIContainer canvas;
        private readonly Slider scrub;
        private readonly Label status;
        private readonly List<GameObject> actors = new List<GameObject>();
        private double started;
        private float time;
        private bool disposed, playing;
        public VisualElement Root { get; } = new VisualElement { name="studio-presentation-viewport" };
        public BattlePlaybackDriver Driver { get; }
        public GameObject Actor => actors[0];
        public GameObject Target => actors[1];
        public bool IsPlaying => playing;
        public float SampleTime => time;
        public bool IsDisposed => disposed;
        public StudioPresentationViewport(ActorPresentationProfile actor,ActorPresentationProfile target,GameObject fallback,IEnumerable<BattleEvent> facts,AbilityPresentationProfile ability)
        {
            preview=new PreviewRenderUtility();
            try
            {
                var copies=(facts ?? Enumerable.Empty<BattleEvent>()).Select(f=>f.Clone()).ToList();
                var sourceId=copies.FirstOrDefault(f=>f.Kind==BattleEventKind.CardPlayed)?.SourceId ?? "preview.actor";
                var targetId=copies.FirstOrDefault(f=>f.Kind==BattleEventKind.DamageApplied && f.TargetId!=sourceId)?.TargetId ?? "preview.target";
                actors.Add(BattlePlaybackDriver.CreateActor(actor,fallback,new Vector3(-1.8f,0,0),Quaternion.Euler(0,90,0),true));
                actors.Add(BattlePlaybackDriver.CreateActor(target,fallback,new Vector3(1.8f,0,0),Quaternion.Euler(0,-90,0),true));
                foreach(var obj in actors) preview.AddSingleGO(obj);
                var effectsRoot=new GameObject("Studio presentation effects"); effectsRoot.hideFlags=HideFlags.HideAndDontSave; preview.AddSingleGO(effectsRoot);
                var camera=preview.camera; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.035f,.05f,.085f); camera.nearClipPlane=.03f; camera.farClipPlane=200; camera.fieldOfView=32;
                var bounds=new Bounds(new Vector3(0,1,0),new Vector3(6,3,3));
                foreach(var obj in actors) foreach(var renderer in obj.GetComponentsInChildren<Renderer>(true)) bounds.Encapsulate(renderer.bounds);
                if(actor!=null) bounds.Encapsulate(new Bounds(Actor.transform.TransformPoint(actor.Bounds.center),Vector3.Scale(actor.Bounds.size,Actor.transform.lossyScale)));
                if(target!=null) bounds.Encapsulate(new Bounds(Target.transform.TransformPoint(target.Bounds.center),Vector3.Scale(target.Bounds.size,Target.transform.lossyScale)));
                camera.transform.position=bounds.center+new Vector3(0,bounds.size.y*.5f,-Mathf.Max(10,bounds.size.x*1.8f)); camera.transform.LookAt(bounds.center);
                preview.lights[0].intensity=1.3f; preview.lights[0].transform.rotation=Quaternion.Euler(35,145,0); preview.lights[1].intensity=.7f; preview.ambientColor=new Color(.35f,.4f,.5f);
                Driver=new BattlePlaybackDriver(new Dictionary<string,GameObject>{{sourceId,Actor},{targetId,Target}},new Dictionary<string,ActorPresentationProfile>{{sourceId,actor},{targetId,target}},camera,null,effectsRoot.transform,true);
                Driver.Load(copies,ability);
                var toolbar=new VisualElement(); toolbar.style.flexDirection=FlexDirection.Row;
                toolbar.Add(new Button(Play){text="Play"}); toolbar.Add(new Button(Pause){text="Pause"}); toolbar.Add(new Button(()=>Sample(0)){text="Reset"});
                toolbar.Add(new Button(()=>Step(-1)){text="Previous contact"}); toolbar.Add(new Button(()=>Step(1)){text="Next contact"}); Root.Add(toolbar);
                canvas=new IMGUIContainer(Draw){name="studio-presentation-canvas"}; canvas.style.height=350; canvas.style.minHeight=220; Root.Add(canvas);
                scrub=new Slider("Timeline (seconds)",0,Driver.Duration){showInputField=true,name="studio-presentation-scrub"}; scrub.RegisterValueChangedCallback(e=>Sample(e.newValue)); Root.Add(scrub);
                status=new Label(); status.style.whiteSpace=WhiteSpace.Normal; Root.Add(status);
                Root.RegisterCallback<DetachFromPanelEvent>(_=>Dispose());
                AssemblyReloadEvents.beforeAssemblyReload+=Dispose; EditorApplication.quitting+=Dispose;
                Sample(0);
            }
            catch { Driver?.Dispose(); preview.Cleanup(); throw; }
        }
        public void Play() { if(disposed)return; started=EditorApplication.timeSinceStartup-time; playing=true; EditorApplication.update-=Tick; EditorApplication.update+=Tick; }
        public void Pause() { playing=false; EditorApplication.update-=Tick; }
        private void Tick() { if(disposed)return; SampleInternal((float)(EditorApplication.timeSinceStartup-started)); if(time>=Driver.Duration)Pause(); }
        public void Sample(float seconds) { if(disposed)throw new ObjectDisposedException(nameof(StudioPresentationViewport)); Pause(); SampleInternal(seconds); }
        private void SampleInternal(float seconds)
        {
            time=Mathf.Clamp(seconds,0,Driver.Duration); Driver.Sample(time); scrub?.SetValueWithoutNotify(time);
            if(status!=null) status.text=time.ToString("0.00")+" / "+Driver.Duration.ToString("0.00")+"s · "+(Driver.LastContact ?? "No committed contact yet")+" · "+Driver.Health.Count+" HP projections · scrub is silent; audition SFX in Training Battle.";
            canvas?.MarkDirtyRepaint();
        }
        private void Step(int direction)
        {
            var times=Driver.Timeline.Entries.Where(e=>e.Fact.Kind==BattleEventKind.DamageApplied && e.Fact.HitCount>0).Select(e=>e.Time).Distinct().OrderBy(t=>t).ToList();
            Sample(direction>0 ? times.Where(t=>t>time+.001f).DefaultIfEmpty(Driver.Duration).First() : times.Where(t=>t<time-.001f).DefaultIfEmpty(0).Last());
        }
        private void Draw()
        {
            var rect=GUILayoutUtility.GetRect(100,10000,220,10000);
            if(disposed || Event.current.type!=EventType.Repaint || rect.width<2 || rect.height<2)return;
            preview.BeginPreview(rect,GUIStyle.none); preview.Render(true); GUI.DrawTexture(rect,preview.EndPreview(),ScaleMode.ScaleToFit);
        }
        public void Capture(string path)
        {
            preview.BeginStaticPreview(new Rect(0,0,960,540)); preview.Render(true); var texture=preview.EndStaticPreview();
            try { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))); File.WriteAllBytes(path,texture.EncodeToPNG()); } finally { Object.DestroyImmediate(texture); }
        }
        public void Dispose()
        {
            if(disposed)return; Pause(); Driver?.Dispose(); preview.Cleanup(); disposed=true;
            AssemblyReloadEvents.beforeAssemblyReload-=Dispose; EditorApplication.quitting-=Dispose;
        }
    }
}
#endif
