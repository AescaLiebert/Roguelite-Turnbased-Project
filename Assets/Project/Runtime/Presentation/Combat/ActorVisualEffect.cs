using System.Collections.Generic;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Actor-owned lifetime and pooled procedural AVE. Never changes combat state.</summary>
    [DisallowMultipleComponent]
    public sealed partial class ActorVisualEffect : MonoBehaviour
    {
        private sealed class Piece
        {
            public Transform Transform;
            public MeshRenderer[] Renderers;
            public Color Color;
            public Vector3 Position;
            public Quaternion Rotation;
            public float Scale;
        }

        private sealed class Burst
        {
            public ActorVisualEffectKind Kind;
            public Transform Root;
            public float Started;
            public bool Active;
            public readonly List<Piece> Pieces = new List<Piece>();
        }

        private ActorVisualEffectGeometry _geometry;
        private ActorVisualEffectSettings _settings;
        private bool _ownsSettings;
        private readonly Dictionary<ActorVisualEffectKind, Burst> _bursts = new Dictionary<ActorVisualEffectKind, Burst>();
        private readonly List<Piece> _stars = new List<Piece>();
        private Transform _stunRoot;
        private Transform _model;
        private Vector3 _modelPosition;
        private Quaternion _modelRotation;
        private bool _poseCaptured;
        private bool _stunned;
        private bool _hidden;
        private float _flinchStarted = -100f;
        private float _stunStarted;
        private float _height = 2f;
        private float _groundOffset;
        private Animator _animator;
        private bool _hasStunBool;
        private bool _stunBoolBefore;
        private bool _stunBoolCaptured;
        private static readonly int StunnedParameter = Animator.StringToHash("Stunned");

        public bool IsStunned => _stunned;
        public float GrantDuration { get { Ensure(); return _settings.GrantDuration; } }
        public float FlinchDuration { get { Ensure(); return _settings.StunFlinchDuration; } }
        public int ActiveGrantCount
        {
            get { var count = 0; foreach (var b in _bursts.Values) if (b.Active) count++; return count; }
        }

        private void Ensure()
        {
            if (_geometry != null) return;
            _geometry = new ActorVisualEffectGeometry();
            _settings = Resources.Load<ActorVisualEffectSettings>("ActorVisualEffectSettings");
            if (_settings == null) { _settings = ScriptableObject.CreateInstance<ActorVisualEffectSettings>(); _ownsSettings = true; }
            _model = transform.Find("Model");
            _animator = GetComponentInChildren<Animator>();
            if (_model == null && _animator != null && _animator.transform != transform) _model = _animator.transform;
            if (_animator != null && _animator.runtimeAnimatorController != null)
                foreach (var parameter in _animator.parameters)
                    if (parameter.nameHash == StunnedParameter && parameter.type == AnimatorControllerParameterType.Bool)
                        _hasStunBool = true;
            var found = false;
            var bounds = new Bounds();
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                if (renderer is LineRenderer || renderer is ParticleSystemRenderer || IsEffectRenderer(renderer)) continue;
                if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
            }
            if (found)
            {
                _height = Mathf.Clamp(bounds.size.y, 1.5f, 4.2f);
                _groundOffset = bounds.min.y - transform.position.y;
                _bodyRadius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z), .35f, 1.2f);
            }
        }

        public static bool IsEffectRenderer(Renderer renderer)
        {
            for (var t = renderer.transform; t != null; t = t.parent)
                if (t.name.StartsWith("AVE ") || t.name.StartsWith("Shield ") || t.name == "Active Shield Aura") return true;
            return false;
        }

        public void Synchronize(bool stunned, bool hidden = false)
        {
            Ensure();
            SetHidden(hidden);
            if (_stunned == stunned) return;
            _stunned = stunned;
            if (stunned)
            {
                _stunStarted = Time.unscaledTime;
                CapturePose();
                if (_hasStunBool)
                {
                    _stunBoolBefore = _animator.GetBool(StunnedParameter); _stunBoolCaptured = true;
                    _animator.SetBool(StunnedParameter, true);
                }
                CreateStars();
                _stunRoot.gameObject.SetActive(!hidden);
            }
            else
            {
                if (!_paralyzed) RestorePose();
                else if (_stunBoolCaptured)
                { _animator.SetBool(StunnedParameter, _stunBoolBefore); _stunBoolCaptured = false; }
                if (_stunRoot != null) _stunRoot.gameObject.SetActive(false);
                _flinchStarted = -100f;
            }
        }

        public void SetHidden(bool hidden)
        {
            _hidden = hidden;
            if (hidden) RestorePose(false);
            if (_stunRoot != null) _stunRoot.gameObject.SetActive(_stunned && !hidden);
            foreach (var effect in _debuffs.Values) effect.SetHidden(hidden);
        }

        public void Flinch()
        {
            if (!IsUnableToAct || !isActiveAndEnabled) return;
            _flinchStarted = Time.unscaledTime;
            if (_animator != null && _animator.runtimeAnimatorController != null)
                foreach (var parameter in _animator.parameters)
                    if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == (_stunned ? "StunFlinch" : "ParalyzeFlinch"))
                    { _animator.SetTrigger(parameter.nameHash); break; }
        }

        public void Play(ActorVisualEffectKind kind)
        {
            if (kind < ActorVisualEffectKind.AttackIncrease || kind > ActorVisualEffectKind.HealthDecrease || !isActiveAndEnabled) return;
            Ensure();
            if (!_bursts.TryGetValue(kind, out var burst))
            {
                burst = BuildBurst(kind);
                _bursts.Add(kind, burst);
            }
            // Repeated grants in one event window coalesce instead of obscuring the actor.
            burst.Started = Time.unscaledTime; burst.Active = true;
            burst.Root.gameObject.SetActive(true);
            UpdateBurst(burst, 0f, 0f);
        }

        private Burst BuildBurst(ActorVisualEffectKind kind)
        {
            var root = new GameObject("AVE " + kind).transform;
            root.SetParent(transform, false);
            var burst = new Burst { Kind = kind, Root = root };
            var up = IsIncrease(kind);
            var color = up ? kind == ActorVisualEffectKind.AttackIncrease ? _settings.AttackUp :
                kind == ActorVisualEffectKind.DefenseIncrease ? _settings.DefenseUp : _settings.HealthUp : _settings.StatDown;
            if (kind == ActorVisualEffectKind.DefenseDecrease)
            {
                Add(burst, "Shield Left", color, Vector3.zero, 1);
                Add(burst, "Shield Right", color, Vector3.zero, 1);
            }
            else
            {
                var shape = kind == ActorVisualEffectKind.AttackIncrease || kind == ActorVisualEffectKind.AttackDecrease ? "Sword" :
                    kind == ActorVisualEffectKind.DefenseIncrease ? "Shield" : "Heart";
                Add(burst, shape, color, Vector3.zero, 1);
            }
            // White engraving keeps the main silhouette readable on bright backgrounds.
            if (kind == ActorVisualEffectKind.DefenseIncrease) Add(burst, "Crest", Color.white, new Vector3(0,0,-.09f), .72f);
            if (kind == ActorVisualEffectKind.HealthIncrease) Add(burst, "Plus", Color.white, new Vector3(.37f,.24f,-.09f), .52f);
            var arrow = Add(burst, "Arrow", up ? new Color(.75f,1f,.86f) : new Color(1f,.5f,.63f), new Vector3(.64f,0,-.02f), .65f);
            if (!up) { arrow.Rotation = Quaternion.Euler(0,0,180); arrow.Transform.localRotation = arrow.Rotation; }
            for (var i = 0; i < 5; i++)
            {
                var angle = i * Mathf.PI * 2 / 5;
                Add(burst, "Spark", color, new Vector3(Mathf.Cos(angle)*.52f,Mathf.Sin(angle)*.52f,0), .12f);
            }
            return burst;
        }

        private Piece Add(Burst burst, string shape, Color color, Vector3 position, float scale)
        {
            var piece = MakePiece(burst.Root, shape, color, position, scale);
            burst.Pieces.Add(piece); return piece;
        }

        private Piece MakePiece(Transform parent, string shape, Color color, Vector3 position, float scale)
        {
            var t = _geometry.Shape(parent, shape, color, position, scale);
            return new Piece { Transform = t, Renderers = t.GetComponentsInChildren<MeshRenderer>(), Color = color,
                Position = position, Rotation = Quaternion.identity, Scale = scale };
        }

        private void CreateStars()
        {
            if (_stunRoot != null) return;
            _stunRoot = new GameObject("AVE Stun Stars").transform;
            _stunRoot.SetParent(transform, false);
            for (var i = 0; i < 3; i++)
                _stars.Add(MakePiece(_stunRoot, "Star", _settings.Stun, Vector3.zero, .38f));
        }

        private void LateUpdate()
        {
            Evaluate(Time.unscaledTime, Camera.main);
        }

#if UNITY_EDITOR
        /// <summary>Deterministic sampling for the editor preview and visual regression checks.</summary>
        public void SamplePreview(float elapsed, Camera camera)
        {
            foreach (var effect in _debuffs.Values) effect.SamplePreview(elapsed);
            Evaluate(Time.unscaledTime + elapsed, camera);
        }
#endif

        private void Evaluate(float now, Camera camera)
        {
            if (_geometry == null) return;
            var rotation = camera != null ? camera.transform.rotation : Quaternion.identity;
            var anchor = transform.position + Vector3.up * (_height + _groundOffset);
            var active = ActiveGrantCount;
            var slot = 0;
            foreach (var burst in _bursts.Values)
            {
                if (!burst.Active) continue;
                var t = Mathf.Clamp01((now - burst.Started) / _settings.GrantDuration);
                if (t >= 1f) { burst.Active = false; burst.Root.gameObject.SetActive(false); continue; }
                var offset = (slot++ - (active-1)*.5f) * _settings.IconSize * 1.35f;
                burst.Root.SetPositionAndRotation(anchor + Vector3.up * .45f, rotation);
                UpdateBurst(burst, t, offset);
            }
            EvaluateDebuffs(now, camera);
            if (!_stunned) return;
            if (!_poseCaptured) CapturePose();
            var elapsed = now - _stunStarted;
            var flinch = Mathf.Clamp01((now - _flinchStarted) / _settings.StunFlinchDuration);
            var kick = Mathf.Sin(flinch * Mathf.PI);
            var radius = Mathf.Clamp(_height * .26f, .45f, .8f);
            for (var i = 0; i < _stars.Count; i++)
            {
                var phase = elapsed * 3.3f + i * Mathf.PI * 2/3;
                var star = _stars[i];
                star.Transform.SetPositionAndRotation(anchor + new Vector3(Mathf.Cos(phase)*radius,
                    .23f + Mathf.Sin(phase*2)*.075f + kick*.1f, Mathf.Sin(phase)*radius*.55f),
                    rotation * Quaternion.Euler(0,0, Mathf.Sin(phase)*20));
                star.Transform.localScale = Vector3.one * (.38f + kick*.08f) * Mathf.Min(1,elapsed*7);
            }
            if (_model != null && _poseCaptured && !_hidden)
            {
                var entry = Mathf.Clamp01(elapsed * 5);
                var sway = Mathf.Sin(elapsed * 2.5f);
                _model.localPosition = _modelPosition + Vector3.down * (.025f*entry + .075f*kick);
                _model.localRotation = _modelRotation * Quaternion.Euler(6*entry + 9*kick,
                    Mathf.Sin(flinch*Mathf.PI*6)*kick*3, sway*1.5f + kick*3);
            }
        }

        private void UpdateBurst(Burst burst, float t, float slotOffset)
        {
            var up = IsIncrease(burst.Kind);
            var pop = Mathf.Sin(Mathf.Clamp01(t/.18f) * Mathf.PI * .5f);
            var alpha = Mathf.Clamp01(t/.07f) * (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.62f,1f,t)));
            // A quick engage, short readable hold, then lift/sink and dissolve.
            burst.Root.localScale = Vector3.one * _settings.IconSize * (pop * (1 + .12f*Mathf.Sin(t*Mathf.PI)));
            burst.Root.position += burst.Root.right * slotOffset + Vector3.up * ((up ? 1 : -1)*.5f*t);
            var fracture = Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.2f)/.5f));
            for (var i = 0; i < burst.Pieces.Count; i++)
            {
                var p = burst.Pieces[i];
                p.Transform.localPosition = p.Position;
                p.Transform.localRotation = p.Rotation;
                p.Transform.localScale = Vector3.one*p.Scale;
                if (burst.Kind == ActorVisualEffectKind.DefenseDecrease && i < 2)
                {
                    var side = i == 0 ? -1 : 1;
                    p.Transform.localPosition += new Vector3(side*.3f*fracture,-.15f*fracture,0);
                    p.Transform.localRotation = Quaternion.Euler(0,0,-side*28*fracture);
                }
                else if (i == 0 && burst.Kind == ActorVisualEffectKind.AttackDecrease)
                {
                    p.Transform.localRotation = Quaternion.Euler(0,0,Mathf.Lerp(0,-42,fracture));
                    p.Transform.localScale *= 1-.18f*fracture;
                }
                else if (i == 0 && burst.Kind == ActorVisualEffectKind.HealthDecrease)
                    p.Transform.localRotation = Quaternion.Euler(0,0,-18*fracture);
                if (p.Transform.name == "Spark")
                {
                    p.Transform.localPosition = p.Position * (1 + t*1.2f) + Vector3.up*(up ? t*.3f : -t*.3f);
                    p.Transform.localScale *= 1-t;
                }
                foreach (var renderer in p.Renderers)
                {
                    var color = renderer.name == "Outline" ? new Color(.055f,.07f,.13f) : p.Color;
                    color.a = alpha; _geometry.Tint(renderer, color);
                }
            }
        }

        private static bool IsIncrease(ActorVisualEffectKind kind) => kind == ActorVisualEffectKind.AttackIncrease ||
            kind == ActorVisualEffectKind.DefenseIncrease || kind == ActorVisualEffectKind.HealthIncrease;

        private void CapturePose()
        {
            if (_model == null || _poseCaptured) return;
            _modelPosition = _model.localPosition; _modelRotation = _model.localRotation; _poseCaptured = true;
        }

        private void RestorePose(bool restoreAnimator = true)
        {
            if (_model != null && _poseCaptured) { _model.localPosition = _modelPosition; _model.localRotation = _modelRotation; }
            _poseCaptured = false;
            if (restoreAnimator && _stunBoolCaptured)
            {
                if (_animator != null) _animator.SetBool(StunnedParameter, _stunBoolBefore);
                _stunBoolCaptured = false;
            }
        }

        public void ClearTransient()
        {
            foreach (var burst in _bursts.Values) { burst.Active = false; if (burst.Root != null) burst.Root.gameObject.SetActive(false); }
            _flinchStarted = -100f;
            foreach (var effect in _debuffs.Values) effect.ClearEnter();
        }

        public void ClearAll()
        {
            ClearTransient(); RestorePose(); _stunned = false; ClearDebuffs();
            if (_stunRoot != null) _stunRoot.gameObject.SetActive(false);
        }

        private void OnDisable() { ClearAll(); }
        private void OnDestroy()
        {
            ClearAll();
            foreach (var burst in _bursts.Values) if (burst.Root != null) ActorVisualEffectGeometry.Release(burst.Root.gameObject);
            if (_stunRoot != null) ActorVisualEffectGeometry.Release(_stunRoot.gameObject);
            _geometry?.Dispose();
            foreach (var effect in _debuffs.Values) effect.Dispose();
            if (_ownsSettings) ActorVisualEffectGeometry.Release(_settings);
        }
    }
}
