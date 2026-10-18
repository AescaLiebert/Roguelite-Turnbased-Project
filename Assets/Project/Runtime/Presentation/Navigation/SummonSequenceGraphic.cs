using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Navigation
{
    /// <summary>Locally sampled vector light. It never touches the game's random stream or reward state.</summary>
    internal sealed class SummonSequenceGraphic : VisualElement
    {
        private readonly bool _premium;
        private readonly bool _foreground;
        public float TimeValue;
        public float LoopTime;
        public FighterRarity Rarity;
        public Color Accent = SummonAttributeTheme.ColorFor(FighterAttribute.Red);

        public SummonSequenceGraphic(bool premium, bool foreground = false)
        {
            _premium = premium;
            _foreground = foreground;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.right = style.top = style.bottom = 0;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            var w = contentRect.width;
            var h = contentRect.height;
            if (w <= 0f || h <= 0f) return;
            if (!_premium)
            {
                if (_foreground) DrawBoardLight(context, w, h);
                else DrawBoard(context, w, h);
                return;
            }
            var center = new Vector2(w * .5f, h * .43f);
            if (_foreground)
            {
                DrawForeground(context, w, h, center);
                return;
            }
            DrawBackdrop(context, w, h, center);
            DrawCharge(context, w, h, center);
            DrawSparkles(context.painter2D, w, h);
        }

        private void DrawBoard(MeshGenerationContext context, float w, float h)
        {
            var p = context.painter2D;
            GradientQuad(context, Vector2.zero, new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h),
                new Color(.97f, .99f, 1f, .9f), new Color(.86f, .93f, 1f, .78f),
                new Color(.93f, .91f, 1f, .72f), new Color(.82f, .9f, .98f, .72f));
            var clock = LoopTime != 0 ? LoopTime : TimeValue;
            var drift = Mathf.Repeat(clock * .007f, .1f);
            for (var i = -3; i < 16; i++)
            {
                var x = w * (i * .095f + drift);
                var color = i % 3 == 0 ? new Color(.71f, .63f, .9f, .13f) : new Color(.57f, .76f, .94f, .10f);
                Quad(p, color, new Vector2(x, h), new Vector2(x + w * .19f, 0),
                    new Vector2(x + w * .225f, 0), new Vector2(x + w * .035f, h));
            }
            Glow(context, new Vector2(w * .48f, h * .15f), new Vector2(w * .55f, h * .85f), new Color(1f, 1f, 1f, .68f));
            for (var i = 0; i < 12; i++)
            {
                var at = new Vector2(w * Hash(i + 46), h * Hash(i + 216));
                var alpha = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(clock * .9f + i * 2.1f)), 5f) * .28f;
                Star(p, at, h * (.008f + Hash(i) * .008f), new Color(1f, 1f, 1f, alpha));
            }
        }

        private void DrawBoardLight(MeshGenerationContext context, float w, float h)
        {
            // Foreground-only opening sheen; it dissipates before ordinary reward arrivals.
            var opening = Mathf.Clamp01((TimeValue - .03f) / .48f);
            if (opening > 0f && opening < 1f)
            {
                var x = Mathf.Lerp(-w * .45f, w * 1.45f, Ease(opening));
                var width = w * .13f;
                GradientQuad(context, new Vector2(x - width, 0), new Vector2(x + width, 0),
                    new Vector2(x - w * .17f + width, h), new Vector2(x - w * .17f - width, h),
                    new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, .82f * Mathf.Sin(opening * Mathf.PI)),
                    new Color(.91f, .85f, 1f, .70f * Mathf.Sin(opening * Mathf.PI)), new Color(1f, 1f, 1f, 0f));
            }
        }

        private void DrawBackdrop(MeshGenerationContext context, float w, float h, Vector2 center)
        {
            var t = TimeValue;
            var clock = LoopTime != 0 ? LoopTime : t;
            var charge = 1f - Smooth(1.16f, 1.34f, t);
            var revealed = Smooth(1.05f, 1.65f, t);
            var accent = Accent;
            var dark = Color.Lerp(new Color(.018f, .022f, .035f), accent, .035f);
            GradientQuad(context, new Vector2(-w * .14f, -h * .14f), new Vector2(w * 1.14f, -h * .14f),
                new Vector2(w * 1.14f, h * 1.14f), new Vector2(-w * .14f, h * 1.14f),
                dark, Shade(accent, .035f), Shade(accent, .075f), dark);
            // Broad charcoal facets and silver cuts survive beneath the moving halo after the X clears.
            var facetClock = Mathf.Sin(clock * .08f) * w * .009f;
            for (var i = -2; i < 8; i++)
            {
                var x = w * (i * .21f) + facetClock;
                var high = i % 2 == 0 ? new Color(.11f, .13f, .17f, .30f) : new Color(.015f, .022f, .032f, .70f);
                GradientQuad(context, new Vector2(x, -h * .15f), new Vector2(x + w * .15f, -h * .15f),
                    new Vector2(x - w * .31f, h * 1.15f), new Vector2(x - w * .52f, h * 1.15f),
                    high, new Color(.16f, .18f, .23f, .10f), new Color(.015f, .02f, .03f, .65f), high);
            }
            Glow(context, center, new Vector2(w * .63f, h * .94f), WithAlpha(accent, .20f * revealed + .10f * charge));
            Glow(context, new Vector2(w * .50f, h * 1.03f), new Vector2(w * .83f, h * .70f),
                WithAlpha(Color.Lerp(accent, new Color(1f, .71f, .43f), .23f), charge * .78f + revealed * .28f));
            Glow(context, new Vector2(w * .5f, h * 1.10f), new Vector2(w * .66f, h * .35f),
                WithAlpha(Color.Lerp(accent, Color.white, .75f), charge * .52f));
            if (revealed > 0f)
            {
                var origin = new Vector2(w * .5f, h * .94f);
                for (var i = 0; i < 18; i++)
                {
                    var x = (i - 8.5f) * w * .105f + Mathf.Sin(clock * .11f + i * .8f) * w * .025f;
                    var width = w * (.018f + Hash(i + 11) * .035f);
                    var brightness = .026f + .019f * Mathf.Sin(clock * .45f + i * 1.9f);
                    GradientTriangle(context, origin, new Vector2(w * .5f + x - width, -h * .2f),
                        new Vector2(w * .5f + x + width, -h * .2f),
                        WithAlpha(Color.Lerp(accent, Color.white, .42f), revealed * brightness * .2f),
                        WithAlpha(Color.Lerp(accent, Color.white, .55f), revealed * brightness), new Color(1f, 1f, 1f, 0f));
                }
                Glow(context, new Vector2(w * .5f, h * .83f), new Vector2(w * .25f, h * .12f), WithAlpha(accent, .25f * revealed));
                Glow(context, new Vector2(w * .5f, h * .42f), new Vector2(w * .22f, h * .63f), new Color(1f, .94f, .86f, .055f * revealed));
                var p = context.painter2D;
                for (var i = 0; i < 7; i++)
                {
                    var x = w * (.06f + Hash(i + 330) * .88f);
                    var y = h * (.18f + Hash(i + 690) * .60f);
                    var point = new Vector2(x, y);
                    var glint = (.05f + .12f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(clock * .7f + i * 2.4f)), 4f)) * revealed;
                    var halfHeight = h * (.035f + Hash(i + 92) * .075f);
                    Quad(p, new Color(.85f, .92f, 1f, glint), point + Vector2.up * halfHeight,
                        point + Vector2.right * h * .0023f, point - Vector2.up * halfHeight, point - Vector2.right * h * .0023f);
                }
                for (var i = 0; i < 5; i++)
                {
                    var x = w * (.15f + i * .22f);
                    Line(p, new Vector2(x, -h * .1f), new Vector2(x - w * .45f, h * 1.1f),
                        Mathf.Max(.6f, h * .001f), new Color(.78f, .87f, 1f, .045f * revealed));
                }
                // The muted colored rim sits behind the artwork rather than washing its face in a white frame.
                var halo = center + Vector2.down * h * .005f;
                for (var i = 0; i < 3; i++)
                {
                    var ringColor = i == 0 ? Color.Lerp(accent, new Color(1f, .75f, .38f), .35f) :
                        i == 1 ? new Color(.50f, .72f, 1f) : new Color(.78f, .52f, 1f);
                    p.lineWidth = h * (.012f - i * .0035f);
                    p.strokeColor = WithAlpha(ringColor, revealed * (.045f - i * .008f));
                    p.BeginPath(); p.Arc(halo, h * (.31f + i * .014f), 0, 360); p.Stroke();
                }
            }
            // Soft dark sides retain a clear silhouette and keep the floor light from flattening the artwork.
            GradientQuad(context, new Vector2(-w * .1f, -h * .1f), new Vector2(w * .22f, -h * .1f),
                new Vector2(w * .22f, h * 1.1f), new Vector2(-w * .1f, h * 1.1f),
                new Color(0f, 0f, 0f, .50f), Color.clear, Color.clear, new Color(0f, 0f, 0f, .50f));
            GradientQuad(context, new Vector2(w * .78f, -h * .1f), new Vector2(w * 1.1f, -h * .1f),
                new Vector2(w * 1.1f, h * 1.1f), new Vector2(w * .78f, h * 1.1f),
                Color.clear, new Color(0f, 0f, 0f, .50f), new Color(0f, 0f, 0f, .50f), Color.clear);
        }

        private void DrawCharge(MeshGenerationContext context, float w, float h, Vector2 center)
        {
            var t = TimeValue;
            if (t < 0f || t >= 1.34f) return;
            var p = context.painter2D;
            var alpha = 1f - Smooth(1.20f, 1.34f, t);
            var impulse = Mathf.Exp(-t * 17f);
            var launch = Smooth(1.03f, 1.27f, t);
            // Reference beat: hold a solid emblem, then push it rapidly through the camera before the cut-in.
            var scale = (1f + impulse * .15f) * (1f + launch * 6.5f);
            var wide = Mathf.Min(w * .23f, h * .31f) * scale;
            var tall = h * .28f * scale;
            var thickness = h * (.045f + impulse * .018f) * scale;
            for (var layer = 3; layer >= 1; layer--)
            {
                var glowColor = WithAlpha(Accent, alpha * (.045f + impulse * .025f));
                CrossBlade(p, center, wide, tall, thickness * (1f + layer * .65f), glowColor, 1, false);
                CrossBlade(p, center, wide, tall, thickness * (1f + layer * .65f), glowColor, -1, false);
            }
            CrossBlade(p, center, wide, tall, thickness, WithAlpha(Color.Lerp(Accent, Color.white, .62f), alpha), 1, true);
            CrossBlade(p, center, wide, tall, thickness * .92f, WithAlpha(Color.Lerp(Accent, new Color(1f, 1f, .83f), .83f), alpha), -1, true);
            var flareAlpha = alpha * (.26f + impulse * .60f + launch * .20f);
            Glow(context, center, new Vector2(w * (.48f + launch * .4f), h * .085f), WithAlpha(Accent, flareAlpha));
            Glow(context, center, new Vector2(w * .27f, h * .022f), new Color(1f, .96f, .78f, flareAlpha * .8f));
            Line(p, new Vector2(-w * .1f, center.y), new Vector2(w * 1.1f, center.y),
                h * .0013f, new Color(1f, .93f, .77f, flareAlpha * .70f));
            for (var i = 0; i < 38; i++)
            {
                var sign = i % 2 == 0 ? 1f : -1f;
                var at = Hash(i + 180) * 2f - 1f;
                var direction = new Vector2(wide, tall * sign).normalized;
                var normal = new Vector2(-direction.y, direction.x);
                var drift = Mathf.Sin(t * (.9f + Hash(i + 50)) + i) * h * .013f;
                var point = center + new Vector2(at * wide, at * tall * sign) +
                    normal * (h * (.025f + Hash(i + 17) * .075f) + drift) * scale;
                var size = h * (.005f + Hash(i + 91) * .011f) * (1f + launch * 2f);
                var rotation = t * (.4f + Hash(i + 22)) * (i % 3 - 1) + i * 2.3f;
                var axis = new Vector2(Mathf.Cos(rotation), Mathf.Sin(rotation)) * size;
                var side = new Vector2(-axis.y, axis.x) * (.35f + Hash(i + 7) * .55f);
                var chip = WithAlpha(Color.Lerp(Accent, new Color(1f, .97f, .67f), Hash(i + 13)), alpha * (.3f + Hash(i) * .55f));
                Triangle(p, chip, point + axis, point - axis + side, point - side);
            }
        }

        private void DrawSparkles(Painter2D p, float w, float h)
        {
            var t = LoopTime != 0 ? LoopTime : TimeValue;
            var hero = Smooth(1.05f, 1.8f, TimeValue);
            for (var i = 0; i < 62; i++)
            {
                var hash = Hash(i + 3);
                var progress = Mathf.Repeat(t * (.075f + Hash(i + 99) * .10f) + hash, 1f);
                var x = w * (.035f + Hash(i + 40) * .93f) + Mathf.Sin(t * .25f + i) * w * .006f;
                var y = h * (1.12f - progress * 1.27f);
                var alpha = Mathf.Sin(progress * Mathf.PI) * (.16f + hash * .48f);
                var color = WithAlpha(Color.Lerp(Accent, Color.white, .40f + hash * .5f), alpha * (1f - hero * .3f));
                if (i % 3 == 0)
                {
                    var twinkle = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * (1.3f + hash) + i)), 6f);
                    Star(p, new Vector2(x, y), h * (.004f + hash * .004f), WithAlpha(color, color.a * (.12f + twinkle * .88f)));
                }
                else
                    Line(p, new Vector2(x, y), new Vector2(x + h * .001f, y + h * (.003f + hash * .009f)),
                        Mathf.Max(.65f, h * (.00065f + hash * .0011f)), color);
            }
        }

        private void DrawForeground(MeshGenerationContext context, float w, float h, Vector2 center)
        {
            var p = context.painter2D;
            var t = TimeValue;
            // Fragments fly past the lens as the charge emblem expands, ahead of the character impact.
            if (t >= 1.05f && t < 1.34f)
            {
                var burst = Mathf.Clamp01((t - 1.05f) / .29f);
                DrawGlass(context, w, h, center, burst, (1f - burst) * .9f, true);
            }
            var age = t - 1.35f;
            if (age >= 0f && age < 1.05f)
            {
                var progress = Mathf.Clamp01(age / 1.05f);
                var fade = 1f - Smooth(.35f, 1.05f, age);
                DrawGlass(context, w, h, center, progress, fade, false);
                if (age < .40f)
                {
                    var blast = age / .40f;
                    var alpha = (1f - blast) * .7f;
                    Glow(context, center, new Vector2(w * (.18f + blast * .65f), h * (.12f + blast * .55f)),
                        WithAlpha(Color.Lerp(Accent, Color.white, .74f), alpha * .28f));
                    for (var i = 0; i < 22; i++)
                    {
                        var angle = (i + Hash(i + 26) * .7f) * Mathf.PI * 2f / 22f;
                        var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        var near = center + direction * h * (.10f + blast * .50f);
                        var far = center + direction * h * (.36f + blast * 1.1f);
                        Line(p, near, far, h * (.0007f + Hash(i) * .0015f), new Color(1f, .96f, .84f, alpha * (.3f + Hash(i + 2) * .5f)));
                    }
                }
                if (age < .24f)
                {
                    var crackAlpha = (1f - age / .24f) * .45f;
                    for (var i = 0; i < 13; i++)
                    {
                        var angle = i * 2.39996f;
                        var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        var side = new Vector2(-direction.y, direction.x);
                        var reach = h * (.36f + Hash(i + 6) * .7f);
                        var a = center + direction * h * .04f;
                        var b = center + direction * reach * .45f + side * h * (.015f + Hash(i + 5) * .035f);
                        var c = center + direction * reach * .67f - side * h * .025f;
                        var d = center + direction * reach;
                        p.lineWidth = Mathf.Max(.7f, h * .0012f);
                        p.strokeColor = new Color(.94f, .98f, 1f, crackAlpha);
                        p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.LineTo(c); p.LineTo(d); p.Stroke();
                    }
                }
            }
            var sweep = Mathf.Clamp01((t - 1.56f) / .65f);
            if (sweep > 0f && sweep < 1f)
            {
                var x = Mathf.Lerp(-w * .45f, w * 1.45f, Ease(sweep));
                var alpha = Mathf.Sin(sweep * Mathf.PI) * .10f;
                GradientQuad(context, new Vector2(x - w * .055f, -h * .1f), new Vector2(x + w * .055f, -h * .1f),
                    new Vector2(x - w * .24f, h * 1.1f), new Vector2(x - w * .35f, h * 1.1f),
                    new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, alpha),
                    new Color(1f, 1f, 1f, alpha), new Color(1f, 1f, 1f, 0f));
            }
            var exit = Mathf.Clamp01((t - 3.8f) / .3f);
            if (exit > 0f)
            {
                var edge = Mathf.Lerp(-w * .35f, w * 1.4f, Ease(exit));
                Quad(p, new Color(1f, 1f, 1f, .75f * Mathf.Sin(exit * Mathf.PI * .5f)),
                    new Vector2(-w * .1f, -h * .1f), new Vector2(edge + w * .24f, -h * .1f),
                    new Vector2(edge, h * 1.1f), new Vector2(-w * .1f, h * 1.1f));
            }
        }

        private void DrawGlass(MeshGenerationContext context, float w, float h, Vector2 center, float progress, float fade, bool charge)
        {
            var p = context.painter2D;
            var travel = 1f - Mathf.Pow(1f - progress, 2f);
            for (var i = 0; i < 30; i++)
            {
                var hash = Hash(i + 17);
                var angle = i * 2.39996f + Hash(i + 8) * .48f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var start = .045f + Hash(i + 37) * .18f;
                var distance = start + travel * (.48f + Hash(i + 63) * .75f);
                var at = center + new Vector2(direction.x * w * distance, direction.y * h * distance);
                var rotation = angle + progress * (i % 2 == 0 ? 3.1f : -2.4f);
                var length = h * (.025f + hash * .065f) * (charge ? 1.2f + progress * 2f : 1f);
                var axis = new Vector2(Mathf.Cos(rotation), Mathf.Sin(rotation)) * length;
                var normal = new Vector2(-axis.y, axis.x) * (.18f + Hash(i + 9) * .65f) * (.55f + .45f * Mathf.Abs(Mathf.Cos(progress * 5f + i)));
                var a = at + axis;
                var b = at - axis * .70f + normal;
                var c = at - axis * .42f - normal * .70f;
                var light = Color.Lerp(Accent, Color.white, .76f + hash * .22f);
                var alpha = fade * (.10f + hash * .19f);
                GradientTriangle(context, a, b, c, WithAlpha(light, alpha * .10f), WithAlpha(light, alpha), WithAlpha(Accent, alpha * .30f));
                p.lineWidth = Mathf.Max(.65f, h * .0008f);
                p.strokeColor = WithAlpha(light, fade * (.17f + hash * .40f));
                p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.LineTo(c); p.ClosePath(); p.Stroke();
                Line(p, b, Vector2.Lerp(a, c, .32f), Mathf.Max(.5f, h * .0006f), WithAlpha(Color.white, fade * .14f));
            }
        }

        private void CrossBlade(Painter2D p, Vector2 center, float wide, float tall, float thickness, Color color, int sign, bool detail)
        {
            var a = center + new Vector2(-wide, -tall * sign);
            var b = center + new Vector2(wide, tall * sign);
            var axis = (b - a).normalized;
            var normal = new Vector2(-axis.y, axis.x) * thickness;
            var bevel = axis * thickness * .7f;
            p.fillColor = color;
            p.BeginPath(); p.MoveTo(a - normal * .18f); p.LineTo(a + normal * .62f + bevel);
            p.LineTo(b + normal * .62f - bevel); p.LineTo(b + normal * .18f);
            p.LineTo(b - normal * .62f - bevel); p.LineTo(a - normal * .62f + bevel); p.ClosePath(); p.Fill();
            if (!detail) return;
            Line(p, a + normal * .42f + bevel * 1.7f, b + normal * .42f - bevel * 1.7f,
                Mathf.Max(1f, thickness * .035f), new Color(1f, 1f, 1f, color.a * .82f));
            var ink = WithAlpha(Color.Lerp(Accent, Color.black, .76f), color.a * .88f);
            for (var i = 0; i < 4; i++)
            {
                var along = i < 2 ? .09f + i * .035f : .76f + (i - 2) * .035f;
                var point = Vector2.Lerp(a, b, along);
                Line(p, point - normal * .34f, point + axis * thickness * 1.5f - normal * .34f, thickness * .045f, ink);
                if (i % 2 == 0)
                    Line(p, point + normal * .16f, point + axis * thickness * .55f + normal * .16f, thickness * .075f, ink);
            }
            // Narrow broken seams give the emblem a plate-like face rather than a rounded cartoon cross.
            var split = Vector2.Lerp(a, b, sign > 0 ? .30f : .67f);
            Quad(p, ink, split - normal * .63f, split - normal * .20f + axis * thickness * .15f,
                split + normal * .63f + axis * thickness * .48f, split + normal * .63f + axis * thickness * .59f);
        }

        private static float Hash(int value) => Mathf.Repeat(Mathf.Sin(value * 127.1f) * 43758.5453f, 1f);
        private static float Ease(float value) => 1f - Mathf.Pow(1f - Mathf.Clamp01(value), 3f);
        private static float Smooth(float from, float to, float value)
        { var t = Mathf.Clamp01((value - from) / (to - from)); return t * t * (3f - 2f * t); }
        private static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);
        private static Color Shade(Color color, float amount) => new Color(color.r * amount, color.g * amount, color.b * amount, 1f);
        private static void Line(Painter2D p, Vector2 a, Vector2 b, float width, Color color)
        { p.lineWidth = width; p.strokeColor = color; p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke(); }
        private static void Triangle(Painter2D p, Color color, Vector2 a, Vector2 b, Vector2 c)
        { p.fillColor = color; p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.LineTo(c); p.ClosePath(); p.Fill(); }
        private static void Quad(Painter2D p, Color color, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        { p.fillColor = color; p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.LineTo(c); p.LineTo(d); p.ClosePath(); p.Fill(); }
        private static void Star(Painter2D p, Vector2 at, float size, Color color)
        {
            if (color.a < .005f) return;
            Quad(p, color, at + Vector2.up * size, at + Vector2.right * size * .14f,
                at - Vector2.up * size, at - Vector2.right * size * .14f);
            Quad(p, WithAlpha(color, color.a * .75f), at + Vector2.right * size * .65f, at + Vector2.up * size * .12f,
                at - Vector2.right * size * .65f, at - Vector2.up * size * .12f);
        }

        private static void GradientTriangle(MeshGenerationContext context, Vector2 a, Vector2 b, Vector2 c, Color ca, Color cb, Color cc)
        {
            var mesh = context.Allocate(3, 3);
            mesh.SetNextVertex(Point(a, ca)); mesh.SetNextVertex(Point(b, cb)); mesh.SetNextVertex(Point(c, cc));
            mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(2);
        }
        internal static void GradientQuad(MeshGenerationContext context, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            Color ca, Color cb, Color cc, Color cd)
        {
            var mesh = context.Allocate(4, 6);
            mesh.SetNextVertex(Point(a, ca)); mesh.SetNextVertex(Point(b, cb)); mesh.SetNextVertex(Point(c, cc)); mesh.SetNextVertex(Point(d, cd));
            mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(2);
            mesh.SetNextIndex(2); mesh.SetNextIndex(3); mesh.SetNextIndex(0);
        }
        private static Vertex Point(Vector2 at, Color color) => new Vertex
        { position = new Vector3(at.x, at.y, Vertex.nearZ), tint = color };
        private static void Glow(MeshGenerationContext context, Vector2 center, Vector2 radius, Color color)
        {
            if (color.a <= .001f) return;
            const int segments = 48;
            var mesh = context.Allocate(segments + 1, segments * 3);
            mesh.SetNextVertex(Point(center, color));
            for (var i = 0; i < segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                mesh.SetNextVertex(Point(center + new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y), WithAlpha(color, 0f)));
            }
            for (ushort i = 0; i < segments; i++)
            { mesh.SetNextIndex(0); mesh.SetNextIndex((ushort)(i + 1)); mesh.SetNextIndex((ushort)((i + 1) % segments + 1)); }
        }
    }

    internal sealed class SummonSequenceBand : VisualElement
    {
        public Color Accent = SummonAttributeTheme.ColorFor(FighterAttribute.Red);
        public float TimeValue;
        public SummonSequenceBand()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.right = style.top = style.bottom = 0;
            generateVisualContent += Draw;
        }
        private void Draw(MeshGenerationContext context)
        {
            var w = contentRect.width; var h = contentRect.height;
            if (w <= 0f || h <= 0f) return;
            var p = context.painter2D;
            SummonSequenceGraphic.GradientQuad(context, Vector2.zero, new Vector2(w * .985f, 0),
                new Vector2(w * .97f, h), new Vector2(0, h),
                Color.Lerp(Accent, Color.white, .14f), Color.Lerp(Accent, Color.white, .05f),
                new Color(Accent.r * .66f, Accent.g * .66f, Accent.b * .66f, .97f),
                new Color(Accent.r * .85f, Accent.g * .85f, Accent.b * .85f, .97f));
            var spacing = Mathf.Max(16f, h * .37f);
            var drift = Mathf.Repeat(TimeValue * h * .09f, spacing);
            p.lineWidth = Mathf.Max(1f, h * .015f);
            p.strokeColor = new Color(.02f, .015f, .025f, .12f);
            for (var x = -spacing + drift; x < w * .96f; x += spacing)
            {
                p.BeginPath(); p.MoveTo(new Vector2(x + h * .30f, 0)); p.LineTo(new Vector2(x, h)); p.Stroke();
            }
            p.lineWidth = Mathf.Max(1f, h * .012f);
            p.strokeColor = Color.Lerp(Accent, Color.white, .65f);
            p.BeginPath(); p.MoveTo(Vector2.zero); p.LineTo(new Vector2(w * .985f, 0)); p.Stroke();
            p.strokeColor = new Color(.12f, .045f, .08f, .78f);
            p.lineWidth = Mathf.Max(2f, h * .026f);
            for (var i = 0; i < 7; i++)
            {
                var x = w * (.92f + i * .008f);
                p.BeginPath(); p.MoveTo(new Vector2(x, 0)); p.LineTo(new Vector2(x - h * .22f, h)); p.Stroke();
            }
            var sheen = Mathf.Repeat(TimeValue * .075f, 1.45f) - .22f;
            var at = w * sheen;
            SummonSequenceGraphic.GradientQuad(context, new Vector2(at - w * .04f, 0), new Vector2(at + w * .04f, 0),
                new Vector2(at - h * .35f + w * .04f, h), new Vector2(at - h * .35f - w * .04f, h),
                new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, .055f),
                new Color(1f, 1f, 1f, .055f), new Color(1f, 1f, 1f, 0f));
        }
    }
}
