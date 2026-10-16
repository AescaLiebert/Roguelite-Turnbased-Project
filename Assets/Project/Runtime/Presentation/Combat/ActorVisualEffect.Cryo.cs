using System.Collections.Generic;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    public sealed partial class ActorVisualEffect
    {
        private readonly Dictionary<ActorVisualEffectKind, ActorCryoVisual> _cryo = new Dictionary<ActorVisualEffectKind, ActorCryoVisual>();
        private bool _frozen, _frozenAnimatorCaptured;
        private float _animatorSpeedBeforeFreeze;
        public bool IsFrozen => _frozen;
        public int ActiveCryoCount { get { var n = 0; foreach (var e in _cryo.Values) if (e.Active) n++; return n; } }
        public int ActiveCryoEnterCount { get { var n = 0; foreach (var e in _cryo.Values) if (e.Active && e.Entering) n++; return n; } }
        private int ActiveCryoDebuffCount { get { var n = 0; foreach (var e in _cryo.Values) if (e.Active && ActorVisualEffectRules.IsPersistentDebuff(e.Kind)) n++; return n; } }
        private int ActiveCryoDebuffEnterCount { get { var n = 0; foreach (var e in _cryo.Values) if (e.Active && e.Entering && ActorVisualEffectRules.IsPersistentDebuff(e.Kind)) n++; return n; } }
        public int ExitingCryoCount { get { var n = 0; foreach (var e in _cryo.Values) if (e.Exiting) n++; return n; } }

        private ActorCryoVisual Cryo(ActorVisualEffectKind kind)
        {
            if (_cryo.TryGetValue(kind, out var result)) return result;
            result = new ActorCryoVisual(transform, kind, _height, _bodyRadius, _groundOffset, _settings);
            result.SetHidden(_hidden);
            _cryo.Add(kind, result);
            return result;
        }

        private void SynchronizeCryo(HashSet<ActorVisualEffectKind> desired)
        {
            foreach (var kind in desired) if (ActorVisualEffectRules.IsCryoStatus(kind)) Cryo(kind).SetActive(true);
            foreach (var pair in _cryo) if (!desired.Contains(pair.Key)) pair.Value.SetActive(false);
            SetFrozen(desired.Contains(ActorVisualEffectKind.Freeze));
        }

        private void SetFrozen(bool frozen)
        {
            if (_frozen == frozen) return;
            _frozen = frozen;
            if (frozen) CapturePose();
            else if (!_stunned && !_paralyzed) { RestorePose(); _flinchStarted = -100f; }
            UpdateFrozenAnimator();
        }

        private void UpdateFrozenAnimator()
        {
            var suspend = _frozen && !_hidden && isActiveAndEnabled;
            if (suspend && !_frozenAnimatorCaptured && _animator != null)
            {
                _animatorSpeedBeforeFreeze = _animator.speed;
                _frozenAnimatorCaptured = true;
                _animator.speed = 0;
            }
            else if (!suspend && _frozenAnimatorCaptured)
            {
                if (_animator != null) _animator.speed = _animatorSpeedBeforeFreeze;
                _frozenAnimatorCaptured = false;
            }
        }

        private void EvaluateCryo(float now, Camera camera)
        {
            var kick = _frozen ? Mathf.Sin(Mathf.Clamp01((now - _flinchStarted) / _settings.StunFlinchDuration) * Mathf.PI) : 0;
            foreach (var effect in _cryo.Values) effect.Evaluate(now, camera, kick);
            if (!_frozen || _hidden || _model == null) return;
            CapturePose();
            _model.localPosition = _modelPosition + Vector3.down * (.035f * kick);
            _model.localRotation = _modelRotation * Quaternion.Euler(kick * 5, 0, Mathf.Sin((now - _flinchStarted) * 55) * kick * 2);
        }

        private void ClearCryo()
        {
            SetFrozen(false);
            foreach (var effect in _cryo.Values) effect.SetActive(false, true);
        }
    }
}
