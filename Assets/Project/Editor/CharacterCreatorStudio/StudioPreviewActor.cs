using System;
using System.Collections.Generic;
using System.IO;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    /// <summary>
    /// An isolated projection of committed engine events. It never executes a card,
    /// calls the engine, opens a scene, or uses the runtime account/roster services.
    /// Camera, actors and clock belong to the fixture; actor creation and card phases
    /// use the production presentation classes with animation events suppressed.
    /// </summary>
    public sealed class StudioPreviewActor : IDisposable
    {
        private sealed class Cue
        {
            public float Start;
            public BattleEvent Event;
        }

        private readonly List<BattleEvent> _events = new List<BattleEvent>();
        private readonly List<Cue> _cues = new List<Cue>();
        private readonly Dictionary<string, int> _health = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Label _status = new Label();
        private readonly IMGUIContainer _viewport;
        private readonly Slider _scrubber;
        private PreviewRenderUtility _preview;
        private GameObject _actor;
        private GameObject _target;
        private CharacterObject _source;
        private string _actorId;
        private string _targetId;
        private bool _playing;
        private double _playStarted;
        private bool _disposed;
        private float _sample;
        private float _duration = 1.5f;
        private Bounds _bounds;

        public VisualElement Root { get; }
        public string Status => _status.text;
        public bool IsPlaying => _playing;
        public bool HasFixture => _preview != null;
        public float Duration => _duration;
        public float SampleTime => _sample;
        public string ActorId => _actorId;
        public string TargetId => _targetId;
        public GameObject Actor => _actor;
        public GameObject Target => _target;
        public IReadOnlyDictionary<string, int> HealthByFighter => _health;
        public int CommittedEventCount => _events.Count;

        public StudioPreviewActor()
        {
            Root = new VisualElement { name = "studio-actor-preview" };
            Root.style.flexGrow = 1;
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.Add(new Button(Play) { text = "Replay committed events" });
            toolbar.Add(new Button(() => Sample(0)) { text = "Reset" });
            Root.Add(toolbar);
            _viewport = new IMGUIContainer(Draw) { name = "actor-viewport" };
            _viewport.style.height = 300;
            _viewport.style.minHeight = 180;
            _viewport.style.flexGrow = 1;
            Root.Add(_viewport);
            _scrubber = new Slider("Playback", 0, _duration) { showInputField = true };
            _scrubber.RegisterValueChangedCallback(evt => Sample(evt.newValue));
            Root.Add(_scrubber);
            _status.style.whiteSpace = WhiteSpace.Normal;
            Root.Add(_status);
            Root.RegisterCallback<DetachFromPanelEvent>(_ => ReleaseFixture());
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.quitting += Dispose;
            _status.text = "Run a scenario to preview its committed action and reaction.";
        }

        public void SetScenario(CharacterObject source, IReadOnlyList<BattleEvent> committedEvents, string actorId = null)
        {
            ThrowIfDisposed();
            ReleaseFixture();
            _source = source;
            _events.Clear();
            _cues.Clear();
            _health.Clear();
            if (committedEvents != null)
                foreach (var item in committedEvents)
                    if (item != null) _events.Add(item.Clone());

            _actorId = actorId;
            if (string.IsNullOrEmpty(_actorId))
            {
                foreach (var item in _events)
                    if (item.Kind == BattleEventKind.CardPlayed && !string.IsNullOrEmpty(item.SourceId))
                    { _actorId = item.SourceId; break; }
            }
            if (string.IsNullOrEmpty(_actorId))
                foreach (var item in _events)
                    if (!string.IsNullOrEmpty(item.SourceId)) { _actorId = item.SourceId; break; }
            _actorId = _actorId ?? "preview.actor";
            _targetId = null;
            // Opening utility facts can reference allied gauge recipients before the
            // played skill. The visible reaction target must follow committed damage.
            foreach (var item in _events)
                if (item.Kind == BattleEventKind.DamageApplied && item.SourceId == _actorId &&
                    !string.IsNullOrEmpty(item.TargetId) && item.TargetId != _actorId)
                { _targetId = item.TargetId; break; }
            if (_targetId == null)
                foreach (var item in _events)
                    if (item.Kind == BattleEventKind.DamageApplied && !string.IsNullOrEmpty(item.TargetId) && item.TargetId != _actorId)
                    { _targetId = item.TargetId; break; }
            if (_targetId == null)
                foreach (var item in _events)
                    if (!string.IsNullOrEmpty(item.TargetId) && item.TargetId != _actorId)
                    { _targetId = item.TargetId; break; }
            _targetId = _targetId ?? "preview.target";

            var next = .15f;
            foreach (var item in _events)
            {
                if (item.Kind != BattleEventKind.CardPlayed && item.Kind != BattleEventKind.DamageApplied &&
                    item.Kind != BattleEventKind.HealApplied && item.Kind != BattleEventKind.FighterDefeated) continue;
                _cues.Add(new Cue { Start = next, Event = item });
                next += item.Kind == BattleEventKind.CardPlayed ? .7f : .45f;
            }
            _duration = Mathf.Max(1.5f, next + .7f);
            _scrubber.highValue = _duration;
            _sample = 0;
            _scrubber.SetValueWithoutNotify(0);
            EnsureFixture();
            Sample(0);
        }

        public void Play()
        {
            ThrowIfDisposed();
            if (_source == null && _events.Count == 0) return;
            EnsureFixture();
            _playStarted = EditorApplication.timeSinceStartup;
            _playing = true;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            SampleInternal(0);
        }

        public void Sample(float seconds)
        {
            ThrowIfDisposed();
            Stop();
            EnsureFixture();
            SampleInternal(seconds);
        }

        private void Tick()
        {
            if (!_playing || _disposed) return;
            var time = (float)(EditorApplication.timeSinceStartup - _playStarted);
            SampleInternal(time);
            if (time >= _duration) Stop();
        }

        private void SampleInternal(float seconds)
        {
            _sample = Mathf.Clamp(seconds, 0, _duration);
            _scrubber.SetValueWithoutNotify(_sample);
            if (_actor == null || _target == null) return;
            ResetActor(_actor, new Vector3(-1.8f, 0, 0), Quaternion.Euler(0, 90, 0));
            ResetActor(_target, new Vector3(1.8f, 0, 0), Quaternion.Euler(0, -90, 0));
            _health.Clear();
            Cue active = null;
            foreach (var cue in _cues)
            {
                if (cue.Start > _sample) break;
                active = cue;
                if (!string.IsNullOrEmpty(cue.Event.TargetId) &&
                    (cue.Event.Kind == BattleEventKind.DamageApplied || cue.Event.Kind == BattleEventKind.HealApplied))
                    _health[cue.Event.TargetId] = cue.Event.HealthAfter;
            }
            if (active == null)
            {
                _status.text = _events.Count == 0 ? "No committed events. Preview uses the selected model only." :
                    _events.Count + " committed events copied. Ready to replay.";
                _viewport.MarkDirtyRepaint();
                return;
            }
            var item = active.Event;
            var elapsed = Mathf.Max(0, _sample - active.Start);
            var authored = false;
            var shown = item.TargetId == _actorId ? _actor : item.TargetId == _targetId ? _target : null;
            if (item.Kind == BattleEventKind.CardPlayed)
            {
                shown = item.SourceId == _actorId ? _actor : item.SourceId == _targetId ? _target : null;
                if (shown != null)
                {
                    var phase = elapsed < .13f ? ActorCardPhase.WindUp : elapsed < .6f ? ActorCardPhase.Action : ActorCardPhase.Recovery;
                    authored = shown.GetComponent<ActorCardPresentation>().Enter(phase, "Attack");
                    UpdateAnimator(shown, elapsed);
                    if (!authored)
                    {
                        var pulse = Mathf.Sin(Mathf.Clamp01(elapsed / .7f) * Mathf.PI);
                        shown.transform.position += (shown == _actor ? Vector3.right : Vector3.left) * (.45f * pulse);
                        shown.transform.rotation *= Quaternion.Euler(0, 0, -8f * pulse);
                    }
                }
            }
            else if (shown != null && item.Kind == BattleEventKind.DamageApplied)
            {
                // Only committed damage can produce a reaction. Missing authored clips have
                // a bounded deterministic pose with the same pulse as the production Hit response.
                authored = PlayReaction(shown, item.HasHitReaction ? item.Reaction : HitReaction.Hit);
                UpdateAnimator(shown, elapsed);
                if (!authored && (!item.HasHitReaction || item.Reaction != HitReaction.None))
                {
                    var pulse = Mathf.Sin(Mathf.Clamp01(elapsed / .45f) * Mathf.PI);
                    shown.transform.position += (shown == _actor ? Vector3.left : Vector3.right) * (.25f * pulse);
                    shown.transform.rotation *= Quaternion.Euler(-12f * pulse, 0, 6f * pulse);
                    shown.transform.localScale = new Vector3(1 + pulse * .05f, 1 - pulse * .08f, 1);
                }
            }
            else if (shown != null && item.Kind == BattleEventKind.HealApplied)
            {
                var heal = ActorHealVisual.GetOrCreate(shown.transform, 2.3f, .7f, 0);
                heal.Play();
                foreach (var particles in heal.GetComponentsInChildren<ParticleSystem>(true))
                {
                    particles.useAutoRandomSeed = false;
                    particles.randomSeed = 17;
                    particles.Simulate(elapsed, false, true, true);
                }
            }
            else if (shown != null && item.Kind == BattleEventKind.FighterDefeated)
            {
                shown.transform.rotation *= Quaternion.Euler(0, 0, 80 * Mathf.Clamp01(elapsed / .4f));
            }
            _status.text = "Event #" + item.Id + " · " + item.Kind + " · " + (item.SourceId ?? "—") + " → " +
                (item.TargetId ?? "—") + " · amount " + item.Amount +
                (item.Kind == BattleEventKind.HealApplied || item.Kind == BattleEventKind.DamageApplied ? " · HP after " + item.HealthAfter : "") +
                (shown == null ? " · outside the two-actor viewport" : authored ? " · authored animation" : " · deterministic fallback") +
                " · " + _sample.ToString("0.00") + "s";
            _viewport.MarkDirtyRepaint();
        }

        private static void ResetActor(GameObject actor, Vector3 position, Quaternion rotation)
        {
            actor.transform.SetPositionAndRotation(position, rotation);
            actor.transform.localScale = Vector3.one;
            var heal = actor.GetComponentInChildren<ActorHealVisual>();
            if (heal != null) heal.Stop();
            var animator = actor.GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.Rebind();
                animator.Update(0);
                actor.GetComponent<ActorCardPresentation>().Enter(ActorCardPhase.Rest);
                animator.Update(0);
            }
        }

        private static bool PlayReaction(GameObject actor, HitReaction reaction)
        {
            if (reaction == HitReaction.None) return false;
            var animator = actor.GetComponentInChildren<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            var name = reaction == HitReaction.KnockBack ? "KnockBack" : reaction == HitReaction.KnockDown ? "KnockDown" :
                reaction == HitReaction.KnockUp ? "KnockUp" : "Hurt";
            foreach (var parameter in animator.parameters)
                if (parameter.name == name && parameter.type == AnimatorControllerParameterType.Trigger)
                { animator.SetTrigger(name); return true; }
            if (!animator.HasState(0, Animator.StringToHash(name))) return false;
            animator.Play(name, 0, 0);
            return true;
        }

        private static void UpdateAnimator(GameObject actor, float elapsed)
        {
            var animator = actor.GetComponentInChildren<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) return;
            var count = Mathf.CeilToInt(Mathf.Min(elapsed, 8f) * 60);
            for (var step = 0; step < count; step++) animator.Update(1f / 60);
        }

        private void EnsureFixture()
        {
            if (_preview != null || _disposed) return;
            _preview = new PreviewRenderUtility();
            try
            {
                var prefab = _source != null ? _source.Fighter3DPrefab : null;
                if (prefab == null)
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefabs/In-Game-CharacterPrefab.prefab");
                _actor = CreateActor(prefab, "Studio actor · " + _actorId, new Vector3(-1.8f, 0, 0), 90);
                _target = CreateActor(prefab, "Studio target · " + _targetId, new Vector3(1.8f, 0, 0), -90);
                _bounds = new Bounds(new Vector3(0, 1, 0), new Vector3(6, 3, 3));
                foreach (var renderer in _actor.GetComponentsInChildren<Renderer>()) _bounds.Encapsulate(renderer.bounds);
                foreach (var renderer in _target.GetComponentsInChildren<Renderer>()) _bounds.Encapsulate(renderer.bounds);
                var camera = _preview.camera;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.035f, .05f, .085f);
                camera.nearClipPlane = .03f;
                camera.farClipPlane = 100;
                camera.fieldOfView = 32;
                camera.transform.position = _bounds.center + new Vector3(0, _bounds.size.y * .5f, -Mathf.Max(10, _bounds.size.x * 1.8f));
                camera.transform.LookAt(_bounds.center);
                _preview.lights[0].intensity = 1.3f;
                _preview.lights[0].transform.rotation = Quaternion.Euler(35, 145, 0);
                _preview.lights[1].intensity = .7f;
                _preview.ambientColor = new Color(.35f, .4f, .5f);
                SampleInternal(_sample);
            }
            catch { ReleaseFixture(); throw; }
        }

        private GameObject CreateActor(GameObject prefab, string name, Vector3 position, float yaw)
        {
            GameObject actor;
            if (prefab != null)
                actor = BattleFighterView.Create(prefab, position, Quaternion.Euler(0, yaw, 0),
                    _source != null ? _source.FighterModelYawOffset : 0, _source?.Fighter3DMesh, _source?.Fighter3DMaterial);
            else
            {
                actor = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                actor.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            }
            actor.name = name;
            foreach (var transform in actor.GetComponentsInChildren<Transform>(true))
                transform.gameObject.hideFlags = HideFlags.HideAndDontSave;
            // Prefabs are visual inputs. Runtime scripts, collisions, audio and clip events
            // must never execute gameplay or account effects in the authoring fixture.
            foreach (var behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var audio in actor.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            foreach (var animator in actor.GetComponentsInChildren<Animator>(true))
            {
                animator.fireEvents = false;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            actor.AddComponent<ActorCardPresentation>();
            _preview.AddSingleGO(actor);
            return actor;
        }

        private void Draw()
        {
            var rect = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (Event.current.type != EventType.Repaint || rect.width <= 1 || rect.height <= 1 || _disposed) return;
            EnsureFixture();
            _preview.BeginPreview(rect, GUIStyle.none);
            _preview.Render();
            GUI.DrawTexture(rect, _preview.EndPreview(), ScaleMode.ScaleToFit);
        }

        public void Capture(string absolutePngPath, int width = 960, int height = 540)
        {
            ThrowIfDisposed();
            EnsureFixture();
            _preview.BeginStaticPreview(new Rect(0, 0, Mathf.Max(32, width), Mathf.Max(32, height)));
            _preview.Render();
            var image = _preview.EndStaticPreview();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(absolutePngPath)));
                File.WriteAllBytes(absolutePngPath, image.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(image); }
        }

        private void Stop() { _playing = false; EditorApplication.update -= Tick; }

        private void ReleaseFixture()
        {
            Stop();
            _preview?.Cleanup();
            _preview = null;
            _actor = null;
            _target = null;
        }

        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(StudioPreviewActor)); }

        public void Dispose()
        {
            if (_disposed) return;
            ReleaseFixture();
            _disposed = true;
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            EditorApplication.quitting -= Dispose;
        }
    }
}
