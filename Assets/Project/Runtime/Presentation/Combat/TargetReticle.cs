using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>
    /// Displays a 7DSGC-style glowing target reticle on top of Unity's ScreenSpaceOverlay Canvas.
    /// </summary>
    public sealed class TargetReticle : MonoBehaviour
    {
        [Header("Targeting & Dimensions")]
        [SerializeField] private float heightOffset = 1.15f;
        [SerializeField] private float reticlePixelSize = 95f;
        [SerializeField] private int sortingOrder = 50;

        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color attackPulseColor = new Color(1f, 0.45f, 0.45f, 1f);

        private Transform _targetTransform;
        private Canvas _canvas;
        private CanvasScaler _scaler;
        private RectTransform _imageRect;
        private Image _reticleImage;
        private Coroutine _animationRoutine;
        private float _popScale = 1f;

        public Transform CurrentTarget => _targetTransform;

        private void Awake()
        {
            SetupOverlayCanvas();
        }

        private void LateUpdate()
        {
            if (_targetTransform == null || _reticleImage == null) return;

            var cam = Camera.main;
            if (cam == null) return;

            var worldPos = _targetTransform.position + new Vector3(0f, heightOffset, 0f);
            var screenPos = cam.WorldToScreenPoint(worldPos);

            // Hide if behind camera
            if (screenPos.z <= 0f)
            {
                if (_reticleImage.enabled) _reticleImage.enabled = false;
                return;
            }

            if (!_reticleImage.enabled) _reticleImage.enabled = true;

            _imageRect.position = screenPos;

            // Idle subtle breathing pulse
            var breath = 1f + Mathf.Sin(Time.time * 3.5f) * 0.035f;
            var currentScale = _popScale * breath;
            _imageRect.localScale = new Vector3(currentScale, currentScale, 1f);
        }

        public void AttachTo(Transform target, float? customHeight = null)
        {
            if (customHeight.HasValue) heightOffset = customHeight.Value;

            var changedTarget = _targetTransform != target;
            _targetTransform = target;

            if (_targetTransform != null)
            {
                SetVisible(true);
                if (changedTarget)
                {
                    PlayPopAnimation();
                }
            }
            else
            {
                SetVisible(false);
            }
        }

        public void SetVisible(bool visible)
        {
            if (_reticleImage != null)
            {
                _reticleImage.enabled = visible;
            }
            gameObject.SetActive(visible);
        }

        public void HighlightAttack()
        {
            if (_animationRoutine != null) StopCoroutine(_animationRoutine);
            _animationRoutine = StartCoroutine(DoHighlightAttack());
        }

        private void PlayPopAnimation()
        {
            if (_animationRoutine != null) StopCoroutine(_animationRoutine);
            _animationRoutine = StartCoroutine(DoPopAnimation());
        }

        private IEnumerator DoPopAnimation()
        {
            const float duration = 0.16f;
            var elapsed = 0f;
            _popScale = 1.4f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                _popScale = Mathf.Lerp(1.4f, 1f, Mathf.Sin(t * Mathf.PI * 0.5f));
                yield return null;
            }

            _popScale = 1f;
            _animationRoutine = null;
        }

        private IEnumerator DoHighlightAttack()
        {
            if (_reticleImage != null) _reticleImage.color = attackPulseColor;
            _popScale = 1.3f;
            yield return new WaitForSeconds(0.2f);
            if (_reticleImage != null) _reticleImage.color = normalColor;
            _popScale = 1f;
            _animationRoutine = null;
        }

        private void SetupOverlayCanvas()
        {
            if (_canvas != null) return;

            // 1. Ensure Canvas in ScreenSpaceOverlay with sortingOrder on top of UnitUICanvas
            _canvas = GetComponent<Canvas>();
            if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = sortingOrder;

            // 2. Ensure CanvasScaler matching 1920x1080 resolution
            _scaler = GetComponent<CanvasScaler>();
            if (_scaler == null) _scaler = gameObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(1920f, 1080f);
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            _scaler.matchWidthOrHeight = 0.5f;

            // 3. Child Reticle Image
            var imageObj = new GameObject("ReticleImage", typeof(RectTransform), typeof(Image));
            imageObj.transform.SetParent(transform, false);

            _imageRect = imageObj.GetComponent<RectTransform>();
            _imageRect.sizeDelta = new Vector2(reticlePixelSize, reticlePixelSize);
            _imageRect.pivot = new Vector2(0.5f, 0.5f);

            _reticleImage = imageObj.GetComponent<Image>();
            _reticleImage.raycastTarget = false;
            _reticleImage.color = normalColor;

            var sprite = LoadReticleSprite();
            if (sprite != null)
            {
                _reticleImage.sprite = sprite;
            }
        }

        private static Sprite LoadReticleSprite()
        {
            var sprite = Resources.Load<Sprite>("TargetReticle");
            if (sprite != null) return sprite;

            var path = Path.Combine(Application.dataPath, "Project", "Art", "UI", "TargetReticle.png");
            if (File.Exists(path))
            {
                try
                {
                    var bytes = File.ReadAllBytes(path);
                    var tex = new Texture2D(512, 512, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(bytes))
                    {
                        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("TargetReticle: Could not load texture from file: " + ex.Message);
                }
            }

            return null;
        }
    }
}
