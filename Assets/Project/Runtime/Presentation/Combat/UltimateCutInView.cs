using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>One unscaled timeline, with independently staggered layers and no cropped character strip.</summary>
    public sealed class UltimateCutInView : VisualElement
    {
        // Starting values: verify the name can be read once and the exit hands cleanly to the next camera shot.
        public const float OpeningDuration = .28f;
        public const float HoldDuration = .72f;
        public const float ExitDuration = .42f;
        public const float Duration = OpeningDuration + HoldDuration + ExitDuration;
        private readonly VisualElement _wash, _ribbon, _copy, _rule;
        private readonly Image _art;
        private readonly Label _kicker, _title, _name;
        private readonly List<VisualElement> _streaks = new List<VisualElement>();

        public UltimateCutInView(Sprite art, string title, string fighterName, Color accent)
        {
            name = "ultimate-cut-in"; pickingMode = PickingMode.Ignore;
            AddToClassList("ultimate-cut-in");
            _wash = Layer("ultimate-wash", "ultimate-wash");
            _wash.style.backgroundColor = new Color(accent.r * .18f, accent.g * .18f, accent.b * .18f, .26f);
            Add(_wash);
            _ribbon = Layer("ultimate-ribbon", "ultimate-slash");
            _ribbon.style.backgroundColor = new Color(accent.r, accent.g, accent.b, .17f);
            _ribbon.style.borderTopColor = _ribbon.style.borderBottomColor = new Color(accent.r, accent.g, accent.b, .6f);
            Add(_ribbon);
            for (int i = 0; i < 7; i++)
            {
                var streak = Layer("ultimate-streak-" + i, "ultimate-speed-line");
                streak.style.top = Length.Percent(16 + i * 10);
                streak.style.height = i % 3 == 0 ? 3 : 1;
                streak.style.backgroundColor = accent;
                Add(streak); _streaks.Add(streak);
            }
            _art = new Image { name = "ultimate-art", sprite = art, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            _art.AddToClassList("ultimate-cut-in-portrait"); Add(_art);
            _copy = Layer("ultimate-copy", "ultimate-cut-in-copy");
            _kicker = Text("ultimate-kicker", "ULTIMATE", "ultimate-cut-in-kicker");
            _kicker.style.color = Color.Lerp(accent, Color.white, .65f);
            _title = Text("ultimate-title", title, "ultimate-cut-in-title");
            _name = Text("ultimate-name", fighterName, "ultimate-cut-in-name");
            _rule = Layer("ultimate-title-rule", "ultimate-title-rule"); _rule.style.backgroundColor = accent;
            _copy.Add(_kicker); _copy.Add(_title); _copy.Add(_rule); _copy.Add(_name); Add(_copy);
            Apply(0, 1280, 720);
        }

        public void Apply(float seconds, float width, float height)
        {
            bool portrait = height > width;
            EnableInClassList("ultimate-portrait-layout", portrait);
            var travel = Mathf.Clamp(width * .065f, 32, 120);
            var opening = Ease(seconds / OpeningDuration);
            var artIn = Ease(seconds / .24f);
            var kickerIn = Ease((seconds - .035f) / .18f);
            var titleIn = Ease((seconds - .07f) / .2f);
            var nameIn = Ease((seconds - .1f) / .18f);
            var exit = Mathf.Clamp01((seconds - OpeningDuration - HoldDuration) / ExitDuration);
            var artOut = exit * exit * exit;
            var textOut = Ease(exit / .8f);
            var backdropOut = Ease((exit - .18f) / .82f);
            var drift = Mathf.Clamp01((seconds - OpeningDuration) / HoldDuration);
            _wash.style.opacity = opening * (1 - backdropOut);
            _ribbon.style.opacity = opening * (1 - backdropOut);
            _ribbon.style.translate = new Translate(-travel * 2 * (1 - opening) + travel * 2 * artOut, 0);
            _ribbon.style.scale = new Scale(new Vector3(.88f + .12f * opening, 1, 1));
            for (int i = 0; i < _streaks.Count; i++)
            {
                var reveal = Ease((seconds - i * .012f) / .2f);
                _streaks[i].style.opacity = reveal * (i % 3 == 0 ? .38f : .16f) * (1 - backdropOut);
                _streaks[i].style.translate = new Translate(-travel * (1 - reveal) + (i % 2 == 0 ? 1 : -1) * travel * .3f * drift + travel * 2 * artOut, 0);
            }
            _art.style.opacity = artIn * (1 - Ease(exit));
            _art.style.translate = new Translate(-travel * (1 - artIn) + travel * 1.7f * artOut, 20 * (1 - artIn) - 12 * artOut - 4 * drift);
            // Settle slightly inward, then gently breathe outward. ScaleToFit preserves the complete sprite.
            _art.style.scale = new Scale(Vector3.one * (.965f + .025f * artIn + .01f * drift + .025f * artOut));
            AnimateText(_kicker, kickerIn, textOut, travel * .5f);
            AnimateText(_title, titleIn, textOut, travel * .7f);
            AnimateText(_name, nameIn, textOut, travel * .45f);
            _rule.style.opacity = titleIn * (1 - textOut);
            _rule.style.scale = new Scale(new Vector3(titleIn * (1 - textOut), 1, 1));
        }

        private static void AnimateText(VisualElement element, float reveal, float exit, float travel)
        {
            element.style.opacity = reveal * (1 - exit);
            element.style.translate = new Translate(travel * (1 - reveal) - travel * .65f * exit, 8 * (1 - reveal) - 6 * exit);
        }
        private static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * t * (t * (t * 6 - 15) + 10); }
        private static VisualElement Layer(string name, string css)
        { var element = new VisualElement { name = name, pickingMode = PickingMode.Ignore }; element.AddToClassList(css); return element; }
        private static Label Text(string name, string text, string css)
        { var label = new Label(text) { name = name, pickingMode = PickingMode.Ignore }; label.AddToClassList(css); return label; }
    }
}
