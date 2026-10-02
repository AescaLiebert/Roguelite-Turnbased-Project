using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Keeps a screen-space HUD positioned over its fighter view.</summary>
    public sealed class WorldBillboardFollower : MonoBehaviour
    {
        [SerializeField] private Vector3 offset = new Vector3(0f, 2f, 0f);
        [SerializeField] private bool hideIfBehindCamera = true;

        private Transform _target;
        private RectTransform _rectTransform;
        private Camera _mainCamera;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _mainCamera = Camera.main;
        }

        public void SetTarget(Transform target) => _target = target;

        private void LateUpdate()
        {
            if (_target == null || _rectTransform == null) return;
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            var screenPosition = _mainCamera.WorldToScreenPoint(_target.position + offset);
            if (hideIfBehindCamera && screenPosition.z < 0f)
                screenPosition = new Vector3(-1000f, -1000f, 0f);
            _rectTransform.position = screenPosition;
        }
    }
}
