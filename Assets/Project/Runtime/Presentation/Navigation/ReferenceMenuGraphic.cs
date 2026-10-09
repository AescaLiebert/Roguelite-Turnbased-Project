using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Navigation
{
    /// <summary>Samples source art directly from an atlas; polygons supply native angled UI frames.</summary>
    internal sealed class ReferenceMenuGraphic : VisualElement
    {
        private readonly Texture2D _texture;
        private readonly Rect _source;
        private readonly Vector2[] _polygon;
        private readonly Color _fill, _border;
        private readonly float _imageHeight;

        public ReferenceMenuGraphic(Texture2D texture, Rect source, Vector2[] polygon, Color fill, Color border, float imageHeight = 1)
        {
            _texture = texture; _source = source; _polygon = polygon; _fill = fill; _border = border; _imageHeight = imageHeight;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.right = style.top = style.bottom = 0;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            var bounds = contentRect;
            if (bounds.width <= 0 || bounds.height <= 0) return;
            if (_texture != null)
            {
                var mesh = context.Allocate(_polygon.Length, (_polygon.Length - 2) * 3, _texture);
                for (var i = 0; i < _polygon.Length; i++)
                {
                    var point = _polygon[i];
                    var uv = new Vector2((_source.x + _source.width * point.x) / _texture.width,
                        1 - (_source.y + _source.height * point.y / _imageHeight) / _texture.height);
                    mesh.SetNextVertex(new Vertex { position = new Vector3(point.x * bounds.width, point.y * bounds.height, Vertex.nearZ), tint = Color.white, uv = uv });
                }
                for (ushort i = 1; i < _polygon.Length - 1; i++)
                {
                    mesh.SetNextIndex(0); mesh.SetNextIndex(i); mesh.SetNextIndex((ushort)(i + 1));
                }
            }
            var painter = context.painter2D;
            painter.BeginPath();
            painter.MoveTo(new Vector2(_polygon[0].x * bounds.width, _polygon[0].y * bounds.height));
            for (var i = 1; i < _polygon.Length; i++) painter.LineTo(new Vector2(_polygon[i].x * bounds.width, _polygon[i].y * bounds.height));
            painter.ClosePath();
            if (_texture == null && _fill.a > 0) { painter.fillColor = _fill; painter.Fill(); }
            if (_border.a > 0) { painter.lineWidth = 1.8f; painter.strokeColor = _border; painter.Stroke(); }
        }
    }
}

