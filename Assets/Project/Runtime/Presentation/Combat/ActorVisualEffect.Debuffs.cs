using System.Collections.Generic;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    public sealed partial class ActorVisualEffect
    {
        private readonly Dictionary<ActorVisualEffectKind, ActorDebuffVisual> _debuffs =
            new Dictionary<ActorVisualEffectKind, ActorDebuffVisual>();
        private bool _paralyzed;
        private float _paralyzeStarted;
        private float _bodyRadius = .5f;
        private ActorDebuffVisual _detonation;

        public bool IsParalyzed => _paralyzed;
        public bool IsUnableToAct => _stunned || _paralyzed || _frozen;
        public bool IsExploding => _detonation != null && _detonation.Active;
        public int ActiveStatusCount { get { var n = ActiveCryoCount; foreach (var effect in _debuffs.Values) if (effect.Active) n++; return n; } }
        public int ActiveStatusEnterCount { get { var n = ActiveCryoEnterCount; foreach (var effect in _debuffs.Values) if (effect.Active && effect.Entering) n++; return n; } }
        public float DebuffEnterDuration { get { Ensure(); return _settings.DebuffEnterDuration; } }
        public int ActiveDebuffCount { get { var n = ActiveCryoDebuffCount; foreach (var effect in _debuffs.Values) if (effect.Active && ActorVisualEffectRules.IsPersistentDebuff(effect.Kind)) n++; return n; } }
        public int ActiveDebuffEnterCount { get { var n = ActiveCryoDebuffEnterCount; foreach (var effect in _debuffs.Values) if (effect.Active && effect.Entering && ActorVisualEffectRules.IsPersistentDebuff(effect.Kind)) n++; return n; } }

        /// <summary>Reconcile idle effects with state. Stack count and snapshot refreshes never replay entrances.</summary>
        public void SynchronizeDebuffs(IReadOnlyList<ActorVisualEffectKind> kinds)
        { SynchronizeStatuses(kinds); }

        public void SynchronizeStatuses(IReadOnlyList<ActorVisualEffectKind> kinds)
        {
            Ensure();
            var desired = new HashSet<ActorVisualEffectKind>();
            if (kinds != null) foreach (var kind in kinds)
                if (ActorVisualEffectRules.IsPersistentStatus(kind)) desired.Add(kind);
            SynchronizeCryo(desired);
            foreach (var kind in desired) if (!ActorVisualEffectRules.IsCryoStatus(kind)) Debuff(kind).SetActive(true);
            foreach (var pair in _debuffs) if (!desired.Contains(pair.Key)) pair.Value.SetActive(false);
            var wasParalyzed = _paralyzed;
            _paralyzed = desired.Contains(ActorVisualEffectKind.Paralyze);
            if (_paralyzed && !wasParalyzed) { _paralyzeStarted = Time.unscaledTime; CapturePose(); }
            if (!_paralyzed && wasParalyzed && !_stunned && !_frozen) { RestorePose(); _flinchStarted = -100f; }
            UpdateDebuffDensity();
        }

        /// <summary>Only an accepted status grant should call this, including refreshes of an existing status.</summary>
        public void EnterDebuff(ActorVisualEffectKind kind)
        { EnterStatus(kind); }

        public void EnterStatus(ActorVisualEffectKind kind)
        {
            if (!ActorVisualEffectRules.IsPersistentStatus(kind) || !isActiveAndEnabled) return;
            Ensure();
            if (ActorVisualEffectRules.IsCryoStatus(kind)) { Cryo(kind).Enter(); SetFrozen(kind == ActorVisualEffectKind.Freeze || _frozen); return; }
            Debuff(kind).Enter();
            if (kind == ActorVisualEffectKind.Paralyze && !_paralyzed)
            { _paralyzed = true; _paralyzeStarted = Time.unscaledTime; CapturePose(); }
            UpdateDebuffDensity();
        }

        public void Explode()
        {
            if (!isActiveAndEnabled) return;
            Ensure();
            if (_detonation == null) _detonation = new ActorDebuffVisual(transform, ActorVisualEffectKind.ExplosionDetonation,
                _height, _bodyRadius, _groundOffset, _settings);
            // A confirmed impact remains visible even when cinematic idle suppression is active.
            _detonation.Enter();
        }

        private ActorDebuffVisual Debuff(ActorVisualEffectKind kind)
        {
            if (_debuffs.TryGetValue(kind, out var effect)) return effect;
            effect = new ActorDebuffVisual(transform, kind, _height, _bodyRadius, _groundOffset, _settings);
            effect.SetHidden(_hidden);
            _debuffs.Add(kind, effect);
            return effect;
        }

        private void UpdateDebuffDensity()
        {
            var density = _settings.DebuffParticleDensity / Mathf.Sqrt(Mathf.Max(1, ActiveStatusCount));
            foreach (var effect in _debuffs.Values) effect.SetDensity(density);
        }

        private void EvaluateDebuffs(float now, Camera camera)
        {
            foreach (var effect in _debuffs.Values) effect.Evaluate(now, camera);
            EvaluateCryo(now, camera);
            _detonation?.Evaluate(now, camera);
            if (!_paralyzed || _stunned || _frozen || _hidden || _model == null) return;
            if (!_poseCaptured) CapturePose();
            var age = now - _paralyzeStarted;
            var flinch = Mathf.Clamp01((now - _flinchStarted) / _settings.StunFlinchDuration);
            var kick = Mathf.Sin(flinch * Mathf.PI);
            var twitch = Mathf.Pow(Mathf.Max(0, Mathf.Sin(age * 7)), 12);
            _model.localPosition = _modelPosition + Vector3.down * (.015f + kick * .045f);
            _model.localRotation = _modelRotation * Quaternion.Euler(2 + kick * 7, twitch * .8f,
                Mathf.Sin(flinch * Mathf.PI * 8) * kick * 2 + twitch * .6f);
        }

        private void ClearDebuffs(bool preserveImpact = false)
        {
            foreach (var effect in _debuffs.Values) effect.SetActive(false);
            ClearCryo();
            if (!preserveImpact) _detonation?.SetActive(false);
            _paralyzed = false;
        }
    }
}
