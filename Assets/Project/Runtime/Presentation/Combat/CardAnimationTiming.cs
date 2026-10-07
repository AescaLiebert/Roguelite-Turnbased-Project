using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Animation events on the character Animator mark the damage window.</summary>
    public sealed class CardAnimationTiming : MonoBehaviour
    {
        public bool FirstHit { get; private set; }
        public bool LastHit { get; private set; }
        private Animator _animator;
        public bool HasFirstHitEvent => HasEvent(nameof(CardFirstHit));
        public bool HasLastHitEvent => HasEvent(nameof(CardLastHit));

        public void BeginAction()
        {
            FirstHit = LastHit = false;
            _animator = GetComponent<Animator>();
        }

        private bool HasEvent(string name)
        {
            if (_animator == null || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null) return false;
            // Inspect the active action, not unrelated clips elsewhere in the controller.
            var clips = _animator.IsInTransition(0) ? _animator.GetNextAnimatorClipInfo(0) : _animator.GetCurrentAnimatorClipInfo(0);
            foreach (var clip in clips)
                foreach (var marker in clip.clip.events)
                    if (marker.functionName == name) return true;
            return false;
        }

        public void CardFirstHit() => FirstHit = true;
        public void CardLastHit() { FirstHit = true; LastHit = true; }
    }
}
