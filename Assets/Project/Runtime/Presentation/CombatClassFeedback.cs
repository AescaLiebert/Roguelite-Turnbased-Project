using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation
{
    /// <summary>Tracks committed CC for the inspected fighter, with an interruptible count and signed delta.</summary>
    public sealed class CombatClassFeedback
    {
        // Starting timings: check readability during rapid equipment changes; tune hold first.
        private const float FadeIn = .18f, CountDuration = .65f, Hold = 1.1f, FadeOut = .35f;
        private readonly VisualElement _overlay, _arrow;
        private readonly VisualElement _root;
        private readonly Label _value, _delta;
        private IVisualElementScheduledItem _animation;
        private string _identity;
        private int _observed, _burstStart;
        private double _displayed;
        private bool _visible, _positive;
        private static readonly AudioClip[] Cues = new AudioClip[2];
        private static AudioSource _cueSource;

        public CombatClassFeedback(VisualElement root)
        {
            _root = root;
            var sheet = Resources.Load<StyleSheet>("UI/CombatClassFeedback");
            if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
            _overlay = Element("cc-change-feedback", "cc-change-feedback");
            _arrow = Element("cc-change-arrow", "cc-change-arrow"); _overlay.Add(_arrow);
            _arrow.generateVisualContent += context =>
            {
                var painter = context.painter2D; var width = _arrow.contentRect.width; var height = _arrow.contentRect.height;
                painter.fillColor = _positive ? new Color(.58f, .88f, .22f) : new Color(1, .30f, .32f);
                painter.BeginPath();
                Vector2 Point(float x, float y) => new Vector2(x * width, (_positive ? y : 1 - y) * height);
                painter.MoveTo(Point(.5f, 0)); painter.LineTo(Point(1, .48f)); painter.LineTo(Point(.68f, .48f));
                painter.LineTo(Point(.68f, 1)); painter.LineTo(Point(.32f, 1)); painter.LineTo(Point(.32f, .48f));
                painter.LineTo(Point(0, .48f)); painter.ClosePath(); painter.Fill();
            };
            var bar = Element("cc-change-bar", "cc-change-bar");
            var caption = new Label("CC") { pickingMode = PickingMode.Ignore }; caption.AddToClassList("cc-change-caption"); bar.Add(caption);
            _value = new Label { name = "cc-change-value", pickingMode = PickingMode.Ignore }; bar.Add(_value);
            _delta = new Label { name = "cc-change-delta", pickingMode = PickingMode.Ignore }; bar.Add(_delta);
            _overlay.Add(bar); root.Add(_overlay); Hide();
            _overlay.RegisterCallback<DetachFromPanelEvent>(_ => Hide());
        }

        private static VisualElement Element(string name, string cls)
        { var element = new VisualElement { name = name, pickingMode = PickingMode.Ignore }; element.AddToClassList(cls); return element; }

        public void Reset(string identity, int value)
        { Hide(); _identity = identity; _observed = _burstStart = value; _displayed = value; }

        public void Observe(string identity, int value)
        {
            if (_identity != identity) { Reset(identity, value); return; }
            if (value == _observed) return;
            if (!_visible) { _burstStart = _observed; _displayed = _observed; }
            _observed = value;
            var difference = (long)value - _burstStart;
            if (difference == 0) { Hide(); _displayed = value; return; }
            _animation?.Pause(); _visible = true; _positive = difference > 0;
            _overlay.EnableInClassList("cc-decrease", !_positive);
            _delta.text = (_positive ? "▲  +" : "▼  −") + Math.Abs(difference).ToString("N0");
            _overlay.style.display = DisplayStyle.Flex; _overlay.style.opacity = 0;
            _overlay.style.bottom = _root.Q("artifact-equipped-scroll") != null ? 194 : 40;
            _overlay.BringToFront(); _arrow.MarkDirtyRepaint();
            PlayCue(_positive);
            var start = Time.realtimeSinceStartupAsDouble; var from = _displayed;
            _animation = _overlay.schedule.Execute(() =>
            {
                var elapsed = (float)(Time.realtimeSinceStartupAsDouble - start);
                var progress = Mathf.Clamp01(elapsed / CountDuration); var eased = 1 - Mathf.Pow(1 - progress, 3);
                _displayed = from + (value - from) * eased;
                _value.text = Math.Round(_displayed).ToString("N0");
                var enter = 1 - Mathf.Pow(1 - Mathf.Clamp01(elapsed / FadeIn), 3);
                var exit = Mathf.Clamp01((elapsed - CountDuration - Hold) / FadeOut);
                _overlay.style.opacity = enter * (1 - exit);
                _overlay.style.translate = new Translate(0, 18 * (1 - enter) - 10 * exit);
                if (exit >= 1) { _displayed = value; Hide(); }
            }).Every(16);
        }

        public void Hide()
        { _animation?.Pause(); _visible = false; _overlay.style.display = DisplayStyle.None; }

        private static void PlayCue(bool positive)
        {
            if (!Application.isPlaying) return;
            var index = positive ? 0 : 1;
            if (Cues[index] == null)
            {
                const int rate = 22050; var samples = new float[3308];
                for (var i = 0; i < samples.Length; i++)
                {
                    var progress = i / (float)samples.Length; var time = i / (float)rate;
                    var first = positive ? 520 : 720; var last = positive ? 780 : 440;
                    // Integral of a linear pitch ramp, with a quiet envelope.
                    var phase = first * time + (last - first) * time * progress * .5f;
                    samples[i] = .08f * Mathf.Sin(2 * Mathf.PI * phase) * Mathf.Sin(Mathf.PI * progress);
                }
                Cues[index] = AudioClip.Create(positive ? "CC increased" : "CC decreased", samples.Length, 1, rate, false);
                Cues[index].SetData(samples, 0);
            }
            if (_cueSource != null) UnityEngine.Object.Destroy(_cueSource.gameObject);
            var cue = new GameObject("CC feedback audio") { hideFlags = HideFlags.HideInHierarchy };
            _cueSource = cue.AddComponent<AudioSource>(); _cueSource.spatialBlend = 0; _cueSource.clip = Cues[index]; _cueSource.Play();
            UnityEngine.Object.Destroy(cue, .3f);
        }
    }
}
