using System;
using System.Collections;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    public enum ActorCardPhase { Rest, WindUp, Action, Recovery }

    /// <summary>The acting fighter owns its card phases; the stage observes them for camera shots.</summary>
    public sealed class ActorCardPresentation : MonoBehaviour
    {
        public ActorCardPhase Phase { get; private set; }
        public event Action<ActorCardPhase> PhaseChanged;
        private Animator _animator;

        public bool Enter(ActorCardPhase phase, string actionTrigger = null)
        {
            Phase = phase;
            _animator = GetComponentInChildren<Animator>();
            var authored = false;
            if (phase == ActorCardPhase.Action)
                authored = Play(actionTrigger) || Play("Action");
            else
                authored = Play(phase == ActorCardPhase.Rest ? "Idle" : phase.ToString());
            PhaseChanged?.Invoke(phase);
            return authored;
        }

        public IEnumerator WaitForAnimationEnd()
        {
            if (_animator == null || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null) yield break;
            yield return null;
            // Starting watchdog: broken/looping authored phase clips cannot stall the queue.
            var deadline = Time.realtimeSinceStartup + 8f;
            while (_animator != null && _animator.isActiveAndEnabled && Time.realtimeSinceStartup < deadline)
            {
                var state = _animator.GetCurrentAnimatorStateInfo(0);
                if (!_animator.IsInTransition(0) && (state.IsName("Idle") || state.IsTag("Idle") || state.normalizedTime >= 1f))
                    yield break;
                yield return null;
            }
        }

        private bool Play(string name)
        {
            if (string.IsNullOrEmpty(name) || _animator == null || !_animator.isActiveAndEnabled ||
                _animator.runtimeAnimatorController == null) return false;
            foreach (var parameter in _animator.parameters)
                if (parameter.name == name && parameter.type == AnimatorControllerParameterType.Trigger)
                { _animator.SetTrigger(name); return true; }
            var hash = Animator.StringToHash(name);
            if (_animator.HasState(0, hash)) { _animator.CrossFade(hash, .08f, 0, 0f); return true; }
            // Legacy sample controllers have clips but no triggers. Reuse their skill/idle
            // states without rewriting shared assets or requiring new animation art.
            var suffix = name == "Attack" ? "_skill1" : name == "Idle" ? "_idle" : null;
            if (suffix != null)
                foreach (var clip in _animator.runtimeAnimatorController.animationClips)
                    if (clip.name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
                        _animator.HasState(0, Animator.StringToHash(clip.name)))
                    { _animator.CrossFade(clip.name, .08f, 0, 0f); return true; }
            return false;
        }
    }
}
