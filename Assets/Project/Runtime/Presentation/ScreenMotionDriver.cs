using System;
using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FightingAllstar.Presentation
{
    /// <summary>Uses the same unscaled presentation timing for legacy Canvas and scene navigation.</summary>
    internal sealed class ScreenMotionDriver : MonoBehaviour
    {
        private static ScreenMotionDriver _instance;
        public static ScreenMotionDriver Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var owner = new GameObject("UI motion"); DontDestroyOnLoad(owner);
                _instance = owner.AddComponent<ScreenMotionDriver>();
                SceneManager.sceneLoaded += _instance.SceneLoaded;
                return _instance;
            }
        }
        private bool _loading;
        private CanvasGroup _transitionGroup;
        private Coroutine _fadeRoutine;
        private readonly ConditionalWeakTable<RectTransform, CanvasMotion> _canvasMotions = new ConditionalWeakTable<RectTransform, CanvasMotion>();
        private sealed class CanvasMotion
        {
            public Coroutine Routine;
            public CanvasGroup Group;
            public float Alpha;
            public Vector2 Position;
            public Vector3 Scale;
            public bool Interactable, BlocksRaycasts, Active, Exiting;
        }
        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureTransitionOverlay();
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _transitionGroup.alpha = 1f;
            _fadeRoutine = StartCoroutine(FadeOverlay(0f, ScreenMotion.EnterDurationMs, () =>
            {
                _loading = false;
                _fadeRoutine = null;
            }));
        }
        public void Present(RectTransform target, bool exit, Action complete)
        {
            if (target == null) { complete?.Invoke(); return; }
            var state = _canvasMotions.GetValue(target, _ => new CanvasMotion());
            if (state.Active)
            {
                if (state.Routine != null) StopCoroutine(state.Routine);
                RestoreCanvas(target, state);
            }
            state.Group = target.GetComponent<CanvasGroup>();
            if (state.Group == null) state.Group = target.gameObject.AddComponent<CanvasGroup>();
            state.Alpha = state.Group.alpha; state.Position = target.anchoredPosition; state.Scale = target.localScale;
            state.Interactable = state.Group.interactable; state.BlocksRaycasts = state.Group.blocksRaycasts; state.Active = true; state.Exiting = exit;
            if (exit) { state.Group.interactable = false; state.Group.blocksRaycasts = false; }
            else { target.gameObject.SetActive(true); StartCoroutine(Flash(target)); }
            state.Routine = StartCoroutine(CanvasTween(target, state, exit, complete));
        }
        private IEnumerator CanvasTween(RectTransform target, CanvasMotion state, bool exit, Action complete)
        {
            var duration = (exit ? ScreenMotion.ExitDurationMs : ScreenMotion.EnterDurationMs) / 1000f;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                if (target == null || state.Group == null) yield break;
                var visible = exit ? 1f - ScreenMotion.EaseOut(elapsed / duration) : ScreenMotion.EaseOut(elapsed / duration);
                state.Group.alpha = state.Alpha * visible;
                var travel = exit ? ScreenMotion.ExitTravel : -ScreenMotion.EnterTravel;
                target.anchoredPosition = state.Position + new Vector2(0, travel * (1f - visible));
                target.localScale = state.Scale * Mathf.Lerp(.985f, 1f, visible);
                yield return null;
            }
            if (target == null || state.Group == null) yield break;
            if (exit) target.gameObject.SetActive(false);
            RestoreCanvas(target, state); complete?.Invoke();
        }
        private static void RestoreCanvas(RectTransform target, CanvasMotion state)
        {
            target.anchoredPosition = state.Position; target.localScale = state.Scale;
            if (state.Group != null)
            {
                state.Group.alpha = state.Alpha;
                if (state.Exiting) { state.Group.interactable = state.Interactable; state.Group.blocksRaycasts = state.BlocksRaycasts; }
            }
            state.Active = false; state.Routine = null;
        }
        public void Navigate(string scene)
        {
            if (_loading || string.IsNullOrWhiteSpace(scene)) return;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            { Debug.LogError("Cannot open UI scene because it is not available in Build Settings: " + scene); return; }
            _loading = true; StartCoroutine(LoadSceneWithFade(scene));
        }
        public void FadeSceneToBlack(Action complete)
        {
            if (_loading) return;
            _loading = true; EnsureTransitionOverlay();
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeOverlay(1f, ScreenMotion.ExitDurationMs, complete));
        }
        private IEnumerator LoadSceneWithFade(string scene)
        {
            EnsureTransitionOverlay();
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            yield return FadeOverlay(1f, ScreenMotion.ExitDurationMs, null);
            var operation = SceneManager.LoadSceneAsync(scene);
            if (operation == null)
            {
                _loading = false;
                StartFadeFromBlack();
                yield break;
            }
            while (!operation.isDone) yield return null;
        }
        private void StartFadeFromBlack()
        {
            EnsureTransitionOverlay();
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeOverlay(0f, ScreenMotion.EnterDurationMs, () =>
            {
                _loading = false;
                _fadeRoutine = null;
            }));
        }
        private IEnumerator FadeOverlay(float targetAlpha, int durationMs, Action complete)
        {
            EnsureTransitionOverlay();
            var startAlpha = _transitionGroup.alpha;
            var coverInput = startAlpha > 0.001f || targetAlpha > 0.001f;
            _transitionGroup.blocksRaycasts = coverInput;
            _transitionGroup.interactable = coverInput;
            var elapsed = 0f;
            var duration = Mathf.Max(1, durationMs) / 1000f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _transitionGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, ScreenMotion.EaseOut(elapsed / duration));
                yield return null;
            }
            _transitionGroup.alpha = targetAlpha;
            _transitionGroup.blocksRaycasts = targetAlpha > 0.001f;
            _transitionGroup.interactable = targetAlpha > 0.001f;
            _fadeRoutine = null;
            complete?.Invoke();
        }
        private void EnsureTransitionOverlay()
        {
            if (_transitionGroup != null) return;
            var overlay = new GameObject("Scene transition black overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup),
                typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.GraphicRaycaster));
            overlay.transform.SetParent(transform, false);
            var rect = (RectTransform)overlay.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.overrideSorting = true; canvas.sortingOrder = short.MaxValue;
            var image = overlay.GetComponent<UnityEngine.UI.Image>(); image.color = Color.black; image.raycastTarget = true;
            _transitionGroup = overlay.GetComponent<CanvasGroup>();
            _transitionGroup.alpha = 0f; _transitionGroup.interactable = false; _transitionGroup.blocksRaycasts = false;
        }
        private static IEnumerator Flash(RectTransform target)
        {
            var flash = new GameObject("UI entry flash", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.LayoutElement));
            flash.transform.SetParent(target, false);
            var rect = (RectTransform)flash.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            flash.GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
            var image = flash.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget = false;
            for (var elapsed = 0f; elapsed < .24f; elapsed += Time.unscaledDeltaTime)
            {
                if (image == null) yield break;
                image.color = new Color(1f, .91f, .7f, .065f * (1f - ScreenMotion.EaseOut(elapsed / .24f)));
                yield return null;
            }
            if (flash != null) Destroy(flash);
        }
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            if (_instance == this) _instance = null;
        }
    }
}
