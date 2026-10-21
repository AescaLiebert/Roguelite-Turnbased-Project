using System;
using System.Collections;
using System.Collections.Generic;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    public enum BattleEntityVisualState { Appearing, Idle, Acting, Absorbing, Breaking, Despawning, Removed }

    /// <summary>Battle-owned, event-driven views. Clip completion gates playback, never combat authority.</summary>
    public sealed class BattleEntityPresenter : MonoBehaviour
    {
        private sealed class Playback
        {
            public string actionId;
            public AnimationClip clip;
            public BattleEntityVisualState state;
            public float elapsed, duration;
            public bool cancelled;
        }
        private sealed class EntityView
        {
            public BattleEntityInstance entity;
            public BattleEntityPresentationProfile profile;
            public GameObject root, model;
            public Renderer[] renderers;
            public AudioSource audio;
            public ParticleSystem particles;
            public LineRenderer bolt;
            public Transform emit;
            public TextMesh label;
            public float idleTime, effectTime;
            public bool removing;
            public readonly List<Playback> playback = new List<Playback>();
            public readonly Dictionary<AnimationClip, AnimationClip> clips = new Dictionary<AnimationClip, AnimationClip>();
        }
        private readonly Dictionary<string, EntityView> _views = new Dictionary<string, EntityView>();
        private readonly Dictionary<string, Playback> _actions = new Dictionary<string, Playback>();
        private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
        private BattleEntityPresentationCatalog _catalog;
        private BattleStagePresenter _stage;
        private BattleState _display;
        private Material _effectMaterial;
        public float PlaybackSpeed { get; set; } = 1;
        public int LiveCount => _views.Count;
        public bool IsResolving => _actions.Count > 0;
        public BattleEntityVisualState StateOf(string id) => _views.TryGetValue(id, out var v)
            ? v.playback.Count > 0 ? v.playback[v.playback.Count - 1].state : BattleEntityVisualState.Idle : BattleEntityVisualState.Removed;

        public void Initialize(BattleStagePresenter stage)
        {
            _stage = stage;
            _catalog = Resources.Load<BattleEntityPresentationCatalog>("BattleEntityPresentationCatalog");
        }

        public void Sync(BattleState state)
        {
            if (state == null) return;
            _display = state;
            foreach (var entity in state.Entities)
            {
                var view = Ensure(entity, state);
                if (view != null) view.entity = entity.Clone();
            }
            foreach (var id in new List<string>(_views.Keys))
                if (!state.Entities.Exists(e => e.Id == id) && !_views[id].removing) Release(id);
        }

        public IEnumerator Create(BattleEvent fact, BattleState display)
        {
            _display = display;
            var view = Ensure(fact.Entity, display);
            if (view == null) yield break;
            var token = Push(view, null, view.profile?.spawn, BattleEntityVisualState.Appearing, .8f);
            Cue(view, view.profile?.spawnSound);
            yield return Wait(view, token, view.profile != null ? view.profile.revealTime : .28f);
            Emit(view, false);
            yield return Wait(view, token, token.duration);
            Pop(view, token);
        }

        public IEnumerator BeginAction(BattleEvent fact, BattleState display)
        {
            _display = display;
            var view = Ensure(fact.Entity, display);
            if (view == null) yield break;
            // A reaction follow-up may interrupt another action by this same entity.
            // Stacking freezes its outer clip until this inner clip is complete.
            var token = Push(view, fact.RootActionId, view.profile?.attack, BattleEntityVisualState.Acting, 1.25f);
            if (!string.IsNullOrEmpty(fact.RootActionId)) _actions[fact.RootActionId] = token;
            Cue(view, view.profile?.attackSound);
            yield return Wait(view, token, view.profile != null ? view.profile.emitTime : .48f);
            if (!token.cancelled) Emit(view, true);
        }

        public IEnumerator EndAction(BattleEvent fact)
        {
            if (string.IsNullOrEmpty(fact.RootActionId) || !_actions.TryGetValue(fact.RootActionId, out var token)) yield break;
            if (_views.TryGetValue(fact.EntityId, out var view))
            {
                yield return Wait(view, token, token.duration);
                Pop(view, token);
            }
            _actions.Remove(fact.RootActionId);
        }

        public IEnumerator Absorb(BattleEvent fact)
        {
            if (!_views.TryGetValue(fact.EntityId, out var view)) yield break;
            var token = Push(view, null, view.profile?.absorbHit, BattleEntityVisualState.Absorbing, .42f);
            Cue(view, view.profile?.absorbSound);
            view.effectTime = .42f;
            yield return Wait(view, token, token.duration);
            Pop(view, token);
        }

        public IEnumerator Remove(BattleEvent fact)
        {
            if (!_views.TryGetValue(fact.EntityId, out var view)) yield break;
            Cancel(view);
            view.removing = true;
            var breaking = fact.EntityRemovalCause == BattleEntityRemovalCause.CapacityDepleted;
            var token = Push(view, null, breaking ? view.profile?.capacityBreak : view.profile?.despawn,
                breaking ? BattleEntityVisualState.Breaking : BattleEntityVisualState.Despawning, breaking ? .85f : .65f);
            Cue(view, breaking ? view.profile?.breakSound : view.profile?.despawnSound);
            if (breaking) Emit(view, false);
            yield return Wait(view, token, token.duration);
            Release(fact.EntityId);
        }

        private EntityView Ensure(BattleEntityInstance entity, BattleState state)
        {
            if (entity == null) return null;
            if (_views.TryGetValue(entity.Id, out var old)) return old;
            var owner = BattleEntitySystem.FindOwner(state, entity);
            if (owner == null || _stage == null || !_stage.TryEntityPlacement(owner, null, out var placement)) return null;
            var profile = _catalog?.Find(entity.Definition.PresentationKey ?? entity.Definition.Id);
            _stage.TryEntityPlacement(owner, profile, out placement);
            var root = new GameObject(entity.Definition.NameKey + " · " + entity.Id);
            root.transform.SetParent(transform, false); root.transform.SetPositionAndRotation(placement.position, placement.rotation);
            var model = profile?.prefab != null ? Instantiate(profile.prefab, root.transform) : CreateFallback(entity, root.transform);
            model.name = "Model"; model.transform.localScale = Vector3.one * (profile != null ? profile.modelScale : .7f);
            model.transform.localRotation = Quaternion.Euler(0,profile != null ? profile.modelYaw : 0,0);
            var view = new EntityView { entity = entity.Clone(), profile = profile, root = root, model = model,
                renderers = model.GetComponentsInChildren<Renderer>(true), audio = root.AddComponent<AudioSource>() };
            view.audio.playOnAwake = false; view.audio.spatialBlend = .45f; view.audio.volume = .45f;
            foreach (var animator in model.GetComponentsInChildren<Animator>()) { animator.applyRootMotion = false; animator.enabled = false; }
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.updateWhenOffscreen = true;
            foreach (var child in model.GetComponentsInChildren<Transform>()) if (child.name == "Emit") view.emit = child;
            if (view.emit == null) view.emit = model.transform;
            AddEffects(view);
            var labelObject = new GameObject("Owner, lifetime and shared capacity");labelObject.transform.SetParent(root.transform, false);
            labelObject.transform.localPosition = Vector3.up * (profile != null ? profile.presentationBounds.max.y*profile.modelScale+.2f : 1.8f);
            view.label = labelObject.AddComponent<TextMesh>();view.label.characterSize=.07f;view.label.fontSize=48;
            view.label.anchor=TextAnchor.MiddleCenter;view.label.alignment=TextAlignment.Center;view.label.color=new Color(1,.94f,.72f);
            _views.Add(entity.Id, view);
            Sample(view, profile?.idle, 0);
            return view;
        }

        private GameObject CreateFallback(BattleEntityInstance entity, Transform parent)
        {
            var root = new GameObject("Missing presentation: " + entity.Definition.Id); root.transform.SetParent(parent, false);
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere); body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.up; body.transform.localScale = Vector3.one * .5f;
            var collider = body.GetComponent<Collider>(); if (collider != null) Destroy(collider);
            var label = root.AddComponent<TextMesh>(); label.text = entity.Definition.NameKey; label.characterSize = .12f;
            label.anchor = TextAnchor.MiddleCenter; label.color = Color.white;
            return root;
        }

        private Playback Push(EntityView view, string actionId, AnimationClip clip, BattleEntityVisualState state, float fallback)
        {
            var token = new Playback { actionId = actionId, clip = clip, state = state,
                duration = Mathf.Clamp(clip != null ? clip.length : fallback, .05f, 10f) };
            view.playback.Add(token); Sample(view, clip, 0); return token;
        }
        private static IEnumerator Wait(EntityView view, Playback token, float until)
        { while (view.root != null && !token.cancelled && token.elapsed < Mathf.Min(until, token.duration)) yield return null; }
        private void Pop(EntityView view, Playback token)
        { view.playback.Remove(token); if (view.playback.Count == 0) Sample(view, view.profile?.idle, view.idleTime); }
        private void Cancel(EntityView view)
        {
            foreach (var token in view.playback) { token.cancelled = true; if (token.actionId != null) _actions.Remove(token.actionId); }
            view.playback.Clear(); view.audio.Stop(); view.particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); view.bolt.enabled = false;
        }
        private void Sample(EntityView view, AnimationClip clip, float time)
        {
            if (clip == null) return;
            if (!view.clips.TryGetValue(clip, out var safe))
            { safe = Instantiate(clip); safe.events = Array.Empty<AnimationEvent>(); view.clips.Add(clip, safe); }
            safe.SampleAnimation(view.model, Mathf.Min(time, clip.length));
        }
        private void Update() => Tick(Time.unscaledDeltaTime * Mathf.Clamp(PlaybackSpeed, .1f, 8f));

        // Explicit delta permits deterministic scrubbing/tests without reading authoritative clocks.
        public void Tick(float delta)
        {
            foreach (var view in _views.Values)
            {
                if (view.root == null) continue;
                view.idleTime += Mathf.Max(0, delta);
                if (_display != null && view.label != null)
                {
                    var owner = BattleEntitySystem.FindOwner(_display, view.entity);
                    var remaining = owner?.Statuses?.Instances?.Find(s=>s.InstanceId==view.entity.MarkerStatusInstanceId)?.RemainingDuration ?? 0;
                    var live = _display.Entities.Find(e=>e.Id==view.entity.Id);
                    var category = view.entity.Definition.Category==BattleEntityCategory.GeoConstruct ? "CONSTRUCT" : "SUMMON";
                    view.label.text = category+" [LOCK]  "+view.entity.Definition.NameKey+" · "+remaining+" turns"+
                        (view.entity.MaximumCapacity>0 ? "\n"+(live?.Capacity ?? view.entity.Capacity)+" / "+view.entity.MaximumCapacity+" shared capacity" : "\nActs at owner turn end")+
                        "\n"+(owner?.Definition?.DisplayName ?? view.entity.OwnerFighterId);
                    if(Camera.main!=null) view.label.transform.rotation=Camera.main.transform.rotation;
                }
                if (view.profile?.followFormation == true && _display != null)
                {
                    var owner = BattleEntitySystem.FindOwner(_display, view.entity);
                    if (owner != null && _stage.TryEntityPlacement(owner, view.profile, out var placement))
                        view.root.transform.SetPositionAndRotation(placement.position, placement.rotation);
                }
                var visibility = 1f; var pulse = 0f;
                if (view.playback.Count > 0)
                {
                    var token = view.playback[view.playback.Count - 1];token.elapsed += Mathf.Max(0, delta);
                    Sample(view, token.clip, token.elapsed);
                    var t = Mathf.Clamp01(token.elapsed / token.duration);
                    if (token.state == BattleEntityVisualState.Appearing) visibility = Mathf.Clamp01(t * 3);
                    if (token.state == BattleEntityVisualState.Breaking || token.state == BattleEntityVisualState.Despawning) visibility = 1-t;
                    if (token.state == BattleEntityVisualState.Absorbing) pulse = Mathf.Sin(t*Mathf.PI);
                    if (token.clip == null) view.model.transform.localScale = Vector3.one * (view.profile != null ? view.profile.modelScale : .7f) * Mathf.Max(.001f, visibility);
                }
                else if (view.profile?.idle != null) Sample(view, view.profile.idle, view.idleTime % view.profile.idle.length);
                view.effectTime -= delta;view.bolt.enabled = view.effectTime > 0 && view.entity.Definition.Id == "summon.oz";
                foreach (var renderer in view.renderers)
                {
                    if (renderer == null) continue;
                    renderer.GetPropertyBlock(_properties);_properties.SetFloat("_Visibility",visibility);
                    _properties.SetFloat("_Pulse",pulse);renderer.SetPropertyBlock(_properties);
                }
            }
        }

        private void AddEffects(EntityView view)
        {
            if (_effectMaterial == null)
            { _effectMaterial = new Material(Shader.Find("FightingAllstar/SummonPaint")); _effectMaterial.SetColor("_BaseColor", Color.white); }
            var fx = new GameObject("Entity visual effects");fx.transform.SetParent(view.emit, false);
            var ps = fx.AddComponent<ParticleSystem>();view.particles = ps;
            var main = ps.main;main.playOnAwake = false;main.loop = false;main.startLifetime = .4f;main.startSpeed = 4;
            main.startSize = .15f;main.startColor = view.profile != null ? view.profile.effectColor : Color.white;main.maxParticles = 48;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;emission.enabled = false;
            var shape = ps.shape;shape.shapeType = ParticleSystemShapeType.Cone;shape.angle = 18;shape.radius = .06f;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = _effectMaterial; ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var boltObject = new GameObject("Electro bolt");boltObject.transform.SetParent(view.root.transform, false);
            view.bolt = boltObject.AddComponent<LineRenderer>();view.bolt.sharedMaterial = _effectMaterial;
            view.bolt.startWidth = .055f;view.bolt.endWidth = .015f;view.bolt.positionCount = 7;view.bolt.enabled = false;
            view.bolt.startColor = view.profile != null ? view.profile.effectColor : Color.white;view.bolt.endColor = Color.white;
        }
        private void Emit(EntityView view, bool attack)
        {
            var start = view.emit.position;var end = start + view.root.transform.forward * 4;
            if (attack && _display != null)
            {
                var targets = _display.OtherTeam(view.entity.Side).LivingActive();var total = Vector3.zero;var count = 0;
                foreach (var target in targets) if (_stage.TryFighterPosition(target.Id, out var p)) { total += p+Vector3.up;count++; }
                if (count > 0) end = total/count;
            }
            view.particles.transform.rotation = Quaternion.LookRotation((end-start).normalized, Vector3.up);
            view.particles.Emit(attack ? 26 : 16);view.effectTime = .28f;
            for (var i = 0; i < 7; i++)
                view.bolt.SetPosition(i, Vector3.Lerp(start,end,i/6f) + view.root.transform.right * (i==0||i==6 ? 0 : (i%2==0 ? .18f : -.18f)));
        }
        private static void Cue(EntityView view, AudioClip clip) { if (clip != null) view.audio.PlayOneShot(clip); }
        public void CancelAndSync(BattleState state)
        {
            foreach (var id in new List<string>(_views.Keys)) Release(id);
            _actions.Clear();Sync(state);
        }
        private void Release(string id)
        {
            if (!_views.TryGetValue(id, out var view)) return;
            Cancel(view);foreach (var clip in view.clips.Values) if (clip != null) Destroy(clip);
            if (view.root != null) Destroy(view.root);_views.Remove(id);
        }
        public void Clear()
        { foreach (var id in new List<string>(_views.Keys)) Release(id);_display = null; }
        private void OnDisable() => Clear();
        private void OnDestroy() { Clear(); if (_effectMaterial != null) Destroy(_effectMaterial); }
    }
}
