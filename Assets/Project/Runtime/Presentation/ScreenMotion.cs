using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation
{
    public static class ScreenMotion
    {
        // Starting values: entering keeps input live; repeated close/open must never leave a blocker.
        public const int EnterDurationMs = 320;
        public const int ExitDurationMs = 180;
        public const float EnterTravel = 16f, ExitTravel = 10f;
        private static readonly ConditionalWeakTable<VisualElement, Motion> Motions = new ConditionalWeakTable<VisualElement, Motion>();
        private sealed class Motion
        {
            public IVisualElementScheduledItem Item;
            public StyleFloat Opacity;
            public StyleTranslate Translate;
            public StyleScale Scale;
            public StyleEnum<Position> Position;
            public StyleLength Left, Top, Width, Height;
            public bool Enabled, Active, Exiting;
            public float Alpha;
            public Vector3 Translation, Scaling;
            public string Name;
            public EventCallback<DetachFromPanelEvent> Detached;
        }
        public static float EaseOut(float progress) => 1f - Mathf.Pow(1f - Mathf.Clamp01(progress), 3f);
        /// <summary>Linear 0..1 frames on unscaled time. Detached elements cancel their callbacks.</summary>
        public static IVisualElementScheduledItem Animate(VisualElement element, int durationMs, Action<float> frame, Action complete = null, int delayMs = 0)
        {
            if (element == null || frame == null) return null;
            IVisualElementScheduledItem item = null;
            var started = -1f;
            var finished = false;
            EventCallback<DetachFromPanelEvent> detached = null;
            Action cleanup = () => { finished = true; item?.Pause(); element.UnregisterCallback(detached); };
            detached = _ => cleanup();
            element.RegisterCallback(detached);
            item = element.schedule.Execute(() =>
            {
                if (finished) return;
                if (started < 0) started = Time.realtimeSinceStartup;
                var progress = durationMs <= 0 ? 1f : Mathf.Clamp01((Time.realtimeSinceStartup - started) * 1000f / durationMs);
                frame(progress);
                if (progress < 1f) return;
                cleanup(); complete?.Invoke();
            }).Every(16);
            item.ExecuteLater(Math.Max(0, delayMs));
            return item;
        }
        private static Motion Prepare(VisualElement element)
        {
            var motion = Motions.GetValue(element, _ => new Motion());
            var replacing = motion.Active;
            var alpha = motion.Alpha; var translation = motion.Translation; var scaling = motion.Scaling;
            if (motion.Active) Restore(element, motion);
            if (motion.Detached == null)
            {
                motion.Detached = _ => { if (motion.Active) Restore(element, motion); };
                element.RegisterCallback(motion.Detached);
            }
            motion.Opacity = element.style.opacity; motion.Translate = element.style.translate;
            motion.Scale = element.style.scale; motion.Enabled = element.enabledSelf; motion.Active = true; motion.Exiting = false;
            motion.Position = element.style.position; motion.Left = element.style.left; motion.Top = element.style.top;
            motion.Width = element.style.width; motion.Height = element.style.height;
            motion.Name = element.name;
            // Restoring inline style does not recalculate resolved style until the next panel update.
            motion.Alpha = replacing ? alpha : element.resolvedStyle.opacity;
            motion.Translation = replacing ? translation : element.resolvedStyle.translate;
            motion.Scaling = replacing ? scaling : element.resolvedStyle.scale.value;
            // USS transitions would chase each frame and delay the requested motion.
            element.RemoveFromClassList("animated-panel"); element.RemoveFromClassList("panel-enter");
            return motion;
        }
        private static void Restore(VisualElement element, Motion motion)
        {
            motion.Item?.Pause(); motion.Item = null;
            element.style.opacity = motion.Opacity; element.style.translate = motion.Translate; element.style.scale = motion.Scale;
            element.style.position = motion.Position; element.style.left = motion.Left; element.style.top = motion.Top;
            element.style.width = motion.Width; element.style.height = motion.Height;
            if (motion.Exiting) { element.SetEnabled(motion.Enabled); element.name = motion.Name; }
            motion.Active = false;
        }
        public static void Cancel(VisualElement element)
        { if (element != null && Motions.TryGetValue(element, out var motion) && motion.Active) Restore(element, motion); }
        public static void Show(VisualElement panel) => Enter(panel);
        public static void Reveal(VisualElement panel, int delayMs = 0) => Enter(panel, delayMs, false);
        public static void Enter(VisualElement panel, int delayMs = 0, bool sparkle = true)
        {
            if (panel == null) return;
            var motion = Prepare(panel);
            if (sparkle) Sparkle(panel);
            panel.style.display = DisplayStyle.Flex; panel.style.opacity = 0;
            panel.style.translate = new Translate(motion.Translation.x, motion.Translation.y + EnterTravel);
            panel.style.scale = new Scale(new Vector3(motion.Scaling.x * .985f, motion.Scaling.y * .985f, motion.Scaling.z));
            motion.Item = Animate(panel, EnterDurationMs, progress =>
            {
                var eased = EaseOut(progress);
                panel.style.opacity = motion.Alpha * eased;
                panel.style.translate = new Translate(motion.Translation.x, motion.Translation.y + EnterTravel * (1f - eased));
                var scale = Mathf.Lerp(.985f, 1f, eased);
                panel.style.scale = new Scale(new Vector3(motion.Scaling.x * scale, motion.Scaling.y * scale, motion.Scaling.z));
            }, () => Restore(panel, motion), delayMs);
        }
        public static void ShowModal(VisualElement overlay, VisualElement content = null)
        {
            if (overlay == null) return;
            if (content == null)
                foreach (var child in overlay.Children())
                    if (child.name != "screen-motion-sparkle") { content = child; break; }
            Enter(overlay, 0, content == null || content == overlay);
            if (content == null || content == overlay) return;
            Enter(content, 35);
        }
        public static void Hide(VisualElement panel, Action complete = null) => Dismiss(panel, false, complete);
        public static void Remove(VisualElement panel, Action complete = null) => Dismiss(panel, true, complete);
        public static void Exit(VisualElement panel, Action complete) => Remove(panel, complete);
        private static void Dismiss(VisualElement panel, bool remove, Action complete)
        {
            if (panel == null) { complete?.Invoke(); return; }
            if (Motions.TryGetValue(panel, out var existing) && existing.Active && existing.Exiting) return;
            if (panel.panel == null || panel.resolvedStyle.display == DisplayStyle.None)
            {
                Cancel(panel);
                if (remove) panel.RemoveFromHierarchy(); else panel.style.display = DisplayStyle.None;
                complete?.Invoke(); return;
            }
            var opacity = float.IsNaN(panel.resolvedStyle.opacity) ? 1f : panel.resolvedStyle.opacity;
            var initialTranslate = panel.resolvedStyle.translate; var initialScale = panel.resolvedStyle.scale.value;
            var motion = Prepare(panel); motion.Exiting = true; panel.SetEnabled(false);
            // Outgoing flex pages stop occupying layout while their visible content fades out.
            if (panel.parent != null && panel.resolvedStyle.position == Position.Relative)
            {
                var bounds = panel.layout;
                panel.style.position = Position.Absolute;
                panel.style.left = bounds.x - panel.resolvedStyle.marginLeft;
                panel.style.top = bounds.y - panel.resolvedStyle.marginTop;
                panel.style.width = bounds.width; panel.style.height = bounds.height;
            }
            // Release named queries immediately so a replacement can open during this exit.
            if (remove) panel.name = string.Empty;
            motion.Item = Animate(panel, ExitDurationMs, progress =>
            {
                var eased = EaseOut(progress); panel.style.opacity = opacity * (1f - eased);
                panel.style.translate = new Translate(Mathf.Lerp(initialTranslate.x, motion.Translation.x, eased),
                    Mathf.Lerp(initialTranslate.y, motion.Translation.y - ExitTravel, eased));
                panel.style.scale = new Scale(Vector3.Lerp(initialScale,
                    new Vector3(motion.Scaling.x * .985f, motion.Scaling.y * .985f, motion.Scaling.z), eased));
            }, () =>
            {
                Restore(panel, motion);
                if (remove) panel.RemoveFromHierarchy(); else panel.style.display = DisplayStyle.None;
                complete?.Invoke();
            });
        }
        public static void Pulse(VisualElement target, bool sparkle = true)
        {
            if (target == null) return;
            var motion = Prepare(target); if (sparkle) Sparkle(target);
            motion.Item = Animate(target, 520, progress =>
            { var scale = Mathf.Lerp(1.16f, 1f, EaseOut(progress)); target.style.scale = new Scale(new Vector3(motion.Scaling.x * scale, motion.Scaling.y * scale, motion.Scaling.z)); },
                () => Restore(target, motion));
        }
        public static void Sparkle(VisualElement target)
        {
            if (target == null) return;
            for (var index = target.hierarchy.childCount - 1; index >= 0; index--)
                if (target.hierarchy[index].name == "screen-motion-sparkle") target.hierarchy[index].RemoveFromHierarchy();
            var particles = new VisualElement { name = "screen-motion-sparkle", pickingMode = PickingMode.Ignore };
            particles.style.position = Position.Absolute;
            particles.style.left = particles.style.right = particles.style.top = particles.style.bottom = 0;
            target.hierarchy.Add(particles); var phase = 0f;
            particles.generateVisualContent += context =>
            {
                var painter = context.painter2D; var rect = particles.contentRect;
                if (rect.width <= 0 || rect.height <= 0) return;
                for (var index = 0; index < 6; index++)
                {
                    var t = Mathf.Clamp01(phase * 1.4f - index * .055f); if (t <= 0 || t >= 1) continue;
                    var x = rect.width * (.12f + index * .153f);
                    var y = rect.height * (index % 2 == 0 ? .18f : .74f) - 10f * EaseOut(t);
                    var radius = Mathf.Sin(t * Mathf.PI) * (index % 2 == 0 ? 5f : 3f);
                    painter.fillColor = new Color(1f, .91f, .62f, Mathf.Sin(t * Mathf.PI) * .7f); painter.BeginPath();
                    painter.MoveTo(new Vector2(x, y - radius)); painter.LineTo(new Vector2(x + radius * .3f, y));
                    painter.LineTo(new Vector2(x, y + radius)); painter.LineTo(new Vector2(x - radius * .3f, y));
                    painter.ClosePath(); painter.Fill();
                }
            };
            Animate(particles, 620, progress => { phase = progress; particles.MarkDirtyRepaint(); }, particles.RemoveFromHierarchy);
        }
        public static void Enter(RectTransform target) => ScreenMotionDriver.Instance.Present(target, false, null);
        public static void Exit(RectTransform target, Action complete) => ScreenMotionDriver.Instance.Present(target, true, complete);
        public static void LoadScene(string scene) => ScreenMotionDriver.Instance.Navigate(scene);
        public static void FadeSceneToBlack(Action complete) => ScreenMotionDriver.Instance.FadeSceneToBlack(complete);
    }
}
