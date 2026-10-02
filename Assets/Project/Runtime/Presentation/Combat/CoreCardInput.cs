using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Separates a quick card tap from a hold gesture used for inspection.</summary>
    public sealed class CoreCardInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField, Min(0.1f)] private float holdDuration = 0.42f;

        private Action _onTap;
        private Action _onHoldStarted;
        private Action _onHoldEnded;
        private Coroutine _holdRoutine;
        private bool _pointerDown;
        private bool _holding;
        private bool _cancelled;

        public void Configure(Action onTap, Action onHoldStarted, Action onHoldEnded)
        {
            _onTap = onTap;
            _onHoldStarted = onHoldStarted;
            _onHoldEnded = onHoldEnded;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pointerDown = true;
            _holding = false;
            _cancelled = false;
            if (_holdRoutine != null) StopCoroutine(_holdRoutine);
            _holdRoutine = StartCoroutine(DetectHold());
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            var wasHolding = _holding;
            CancelHold();
            if (wasHolding) _onHoldEnded?.Invoke();
            else if (!_cancelled) _onTap?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            var wasHolding = _holding;
            CancelHold();
            _cancelled = true;
            if (wasHolding) _onHoldEnded?.Invoke();
        }

        private IEnumerator DetectHold()
        {
            yield return new WaitForSecondsRealtime(holdDuration);
            if (!_pointerDown) yield break;
            _holding = true;
            _onHoldStarted?.Invoke();
        }

        private void CancelHold()
        {
            _pointerDown = false;
            if (_holdRoutine != null) StopCoroutine(_holdRoutine);
            _holdRoutine = null;
            _holding = false;
        }

        private void OnDisable()
        {
            var wasHolding = _holding;
            CancelHold();
            if (wasHolding) _onHoldEnded?.Invoke();
        }
    }
}
