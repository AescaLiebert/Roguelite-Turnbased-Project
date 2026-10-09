using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Animated vector energy behind card artwork. No textures or pointer interception.</summary>
    public sealed class CardEnergyElement : VisualElement
    {
        public bool Ultimate { get; set; }
        public bool Merge { get; set; }
        public bool Landing { get; set; }
        public bool Foreground { get; set; }
        public bool MergeReady { get; set; }
        public float Burst { get; set; }
        private readonly IVisualElementScheduledItem _animation;

        public CardEnergyElement()
        {
            name = "card-energy";
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.right = -14;
            style.top = style.bottom = -16;
            _animation = schedule.Execute(() => { if (Ultimate || Merge || Landing) MarkDirtyRepaint(); }).Every(33);
            RegisterCallback<DetachFromPanelEvent>(_ => _animation.Pause());
            RegisterCallback<AttachToPanelEvent>(_ => _animation.Resume());
            generateVisualContent += DrawEnergy;
        }

        private void DrawEnergy(MeshGenerationContext context)
        {
            if (!Ultimate && !Merge && !Landing) return;
            var painter = context.painter2D;
            var time = Time.unscaledTime;
            var rect = contentRect;
            if (rect.width <= 0 || rect.height <= 0) return;
            if (Foreground)
            {
                DrawMergeHint(painter, rect, time);
                return;
            }
            var color = Merge || Landing ? new Color(.35f, 1f, .86f) : new Color(1f, .58f, .1f);
            var pulse = .65f + .18f * Mathf.Sin(time * 4f);
            var x = 12f; var y = 14f; var w = rect.width - 24f; var h = rect.height - 28f;
            for (var layer = 3; layer >= 0; layer--)
            {
                var pad = layer * 3f + Burst * 5f;
                painter.lineWidth = layer == 0 ? 2.5f : 5f;
                painter.strokeColor = new Color(color.r, color.g, color.b, (layer == 0 ? .8f : .09f) * pulse);
                painter.BeginPath();
                painter.MoveTo(new Vector2(x - pad, y - pad));
                painter.LineTo(new Vector2(x + w + pad, y - pad));
                painter.LineTo(new Vector2(x + w + pad, y + h + pad));
                painter.LineTo(new Vector2(x - pad, y + h + pad));
                painter.ClosePath(); painter.Stroke();
            }
            if (!Ultimate) return;
            // Flames rise outside the face; thin arcs keep titles and ranks readable.
            for (var i = 0; i < 10; i++)
            {
                var phase = Mathf.Repeat(time * .6f + i * .137f, 1f);
                var side = i % 2 == 0 ? -1f : 1f;
                var edge = side < 0 ? x : x + w;
                var baseY = y + h * (1 - phase);
                var length = 16f + 12f * Mathf.Sin(time * 3f + i);
                painter.fillColor = new Color(1f, .32f + phase * .4f, .05f, (1 - phase) * .55f);
                painter.BeginPath(); painter.MoveTo(new Vector2(edge, baseY));
                painter.BezierCurveTo(new Vector2(edge + side * 16f, baseY - 6f),
                    new Vector2(edge + side * 4f, baseY - length), new Vector2(edge + side * 9f, baseY - length - 7f));
                painter.LineTo(new Vector2(edge, baseY - 6)); painter.ClosePath(); painter.Fill();
            }
            painter.lineWidth = 1.5f;
            painter.strokeColor = new Color(1f, .95f, .7f, .7f * pulse);
            painter.BeginPath();
            for (var i = 0; i < 13; i++)
            {
                var point = new Vector2(x + w + Mathf.Sin(time * 8f + i * 2.3f) * 5f, y + h * i / 12f);
                if (i == 0) painter.MoveTo(point); else painter.LineTo(point);
            }
            painter.Stroke();
        }

        private void DrawMergeHint(Painter2D painter, Rect rect, float time)
        {
            if (!Merge) return;
            var pulse = .75f + .25f * Mathf.Sin(time * 5f);
            var color = MergeReady ? new Color(.75f, 1f, .92f) : new Color(1f, .79f, .28f);
            var bounds = new Rect(11, 13, rect.width - 22, rect.height - 26);
            // Draw above the frame, leaving the art, rank stars, and skill icon unobscured.
            for (var corner = 0; corner < 4; corner++)
            {
                var right = corner % 2 != 0;
                var bottom = corner >= 2;
                var point = new Vector2(right ? bounds.xMax : bounds.xMin, bottom ? bounds.yMax : bounds.yMin);
                for (var glow = 1; glow >= 0; glow--)
                {
                    painter.lineWidth = glow == 0 ? (MergeReady ? 3f : 2f) : 8f;
                    painter.strokeColor = new Color(color.r, color.g, color.b, (glow == 0 ? .95f : .18f) * pulse);
                    painter.BeginPath();
                    painter.MoveTo(point + new Vector2(right ? -13 : 13, 0));
                    painter.LineTo(point);
                    painter.LineTo(point + new Vector2(0, bottom ? -14 : 14));
                    painter.Stroke();
                }
            }
            if (!MergeReady) return;
            painter.lineWidth = 2;
            painter.strokeColor = new Color(.9f, 1f, .97f, pulse);
            painter.BeginPath();
            painter.MoveTo(new Vector2(bounds.xMin, bounds.yMin));
            painter.LineTo(new Vector2(bounds.xMax, bounds.yMin));
            painter.LineTo(new Vector2(bounds.xMax, bounds.yMax));
            painter.LineTo(new Vector2(bounds.xMin, bounds.yMax));
            painter.ClosePath(); painter.Stroke();
        }
    }
}
