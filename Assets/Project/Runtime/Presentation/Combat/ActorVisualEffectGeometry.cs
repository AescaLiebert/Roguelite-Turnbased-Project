using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Small extruded silhouettes, shared by every icon in an actor's AVE.</summary>
    internal sealed class ActorVisualEffectGeometry
    {
        private readonly Dictionary<string, Mesh> _meshes = new Dictionary<string, Mesh>();
        private readonly Material _material;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private static readonly Color Ink = new Color(.055f, .07f, .13f);

        public ActorVisualEffectGeometry()
        {
            // A Resources shader reference also prevents stripping in standalone builds.
            var shader = Resources.Load<Shader>("ActorVisualEffects");
            _material = new Material(shader) { name = "AVE Silhouettes", hideFlags = HideFlags.DontSave };
        }

        public Transform Shape(Transform parent, string shape, Color color, Vector3 position, float scale = 1f)
        {
            var root = new GameObject(shape).transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            root.localScale = Vector3.one * scale;
            var mesh = MeshFor(shape);
            var outline = Renderer(root, mesh, "Outline", Ink);
            outline.transform.localScale = new Vector3(1.12f, 1.12f, 1f);
            outline.transform.localPosition = Vector3.forward * .045f;
            Renderer(root, mesh, "Face", color);
            return root;
        }

        private MeshRenderer Renderer(Transform parent, Mesh mesh, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Tint(renderer, color);
            return renderer;
        }

        public void Tint(MeshRenderer renderer, Color color)
        {
            _block.Clear();
            _block.SetColor("_Tint", color);
            renderer.SetPropertyBlock(_block);
        }

        private Mesh MeshFor(string shape)
        {
            if (_meshes.TryGetValue(shape, out var cached)) return cached;
            var polygon = new List<Vector2>();
            switch (shape)
            {
                case "Sword":
                    polygon.AddRange(new[] { new Vector2(0,.6f), new Vector2(.14f,.37f), new Vector2(.11f,-.15f),
                        new Vector2(.32f,-.15f), new Vector2(.32f,-.25f), new Vector2(.075f,-.25f),
                        new Vector2(.075f,-.55f), new Vector2(-.075f,-.55f), new Vector2(-.075f,-.25f),
                        new Vector2(-.32f,-.25f), new Vector2(-.32f,-.15f), new Vector2(-.11f,-.15f), new Vector2(-.14f,.37f) });
                    break;
                case "Shield":
                    polygon.AddRange(new[] { new Vector2(0,.52f), new Vector2(.43f,.34f), new Vector2(.38f,-.12f),
                        new Vector2(.22f,-.39f), new Vector2(0,-.57f), new Vector2(-.22f,-.39f),
                        new Vector2(-.38f,-.12f), new Vector2(-.43f,.34f) });
                    break;
                case "Shield Left":
                    polygon.AddRange(new[] { new Vector2(0,.52f), new Vector2(-.43f,.34f), new Vector2(-.38f,-.12f),
                        new Vector2(-.22f,-.39f), new Vector2(0,-.57f), new Vector2(-.09f,-.18f),
                        new Vector2(.07f,.02f), new Vector2(-.08f,.23f) });
                    break;
                case "Shield Right":
                    polygon.AddRange(new[] { new Vector2(0,.52f), new Vector2(.43f,.34f), new Vector2(.38f,-.12f),
                        new Vector2(.22f,-.39f), new Vector2(0,-.57f), new Vector2(-.09f,-.18f),
                        new Vector2(.07f,.02f), new Vector2(-.08f,.23f) });
                    break;
                case "Heart":
                    for (var i = 0; i < 48; i++)
                    {
                        var t = i * Mathf.PI * 2 / 48;
                        polygon.Add(new Vector2(Mathf.Pow(Mathf.Sin(t), 3) * .5f,
                            (13 * Mathf.Cos(t) - 5 * Mathf.Cos(2*t) - 2 * Mathf.Cos(3*t) - Mathf.Cos(4*t)) / 32f + .05f));
                    }
                    break;
                case "Plus":
                    polygon.AddRange(new[] { new Vector2(-.1f,.35f), new Vector2(.1f,.35f), new Vector2(.1f,.1f),
                        new Vector2(.35f,.1f), new Vector2(.35f,-.1f), new Vector2(.1f,-.1f),
                        new Vector2(.1f,-.35f), new Vector2(-.1f,-.35f), new Vector2(-.1f,-.1f),
                        new Vector2(-.35f,-.1f), new Vector2(-.35f,.1f), new Vector2(-.1f,.1f) });
                    break;
                case "Arrow":
                    polygon.AddRange(new[] { new Vector2(0,.38f), new Vector2(.24f,.1f), new Vector2(.09f,.1f),
                        new Vector2(.09f,-.35f), new Vector2(-.09f,-.35f), new Vector2(-.09f,.1f), new Vector2(-.24f,.1f) });
                    break;
                case "Crest":
                    polygon.AddRange(new[] { new Vector2(0,.34f),new Vector2(.16f,.12f),
                        new Vector2(0,-.32f),new Vector2(-.16f,.12f) });
                    break;
                default: // Five-point dizzy stars and smaller burst sparks.
                    for (var i = 0; i < 10; i++)
                    {
                        var angle = (90 - i * 36) * Mathf.Deg2Rad;
                        var r = (i & 1) == 0 ? .5f : .22f;
                        polygon.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r);
                    }
                    break;
            }
            // Ear clipping preserves the heart notch, sword hilt and jagged shield cracks.
            var triangles = Triangulate(polygon);
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var indices = new List<int>();
            foreach (var p in polygon) { vertices.Add(new Vector3(p.x,p.y,-.035f)); colors.Add(Color.white); }
            foreach (var p in polygon) { vertices.Add(new Vector3(p.x,p.y,.035f)); colors.Add(new Color(.68f,.68f,.68f)); }
            foreach (var t in triangles) indices.Add(t + polygon.Count);
            for (var i = 0; i < polygon.Count; i++)
            {
                var j = (i+1) % polygon.Count;
                indices.AddRange(new[] { i, j, j+polygon.Count, i, j+polygon.Count, i+polygon.Count });
            }
            indices.AddRange(triangles);
            var mesh = new Mesh { name = "AVE " + shape, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds(); mesh.RecalculateNormals();
            _meshes[shape] = mesh;
            return mesh;
        }

        private static List<int> Triangulate(List<Vector2> p)
        {
            var result = new List<int>();
            var remaining = new List<int>();
            float area = 0;
            for (var i = 0; i < p.Count; i++) { remaining.Add(i); area += Cross(p[i], p[(i+1)%p.Count]); }
            if (area < 0) remaining.Reverse();
            var limit = p.Count * p.Count;
            while (remaining.Count > 2 && limit-- > 0)
            {
                var clipped = false;
                for (var i = 0; i < remaining.Count; i++)
                {
                    var a = remaining[(i+remaining.Count-1)%remaining.Count];
                    var b = remaining[i]; var c = remaining[(i+1)%remaining.Count];
                    if (Cross(p[b]-p[a], p[c]-p[b]) <= .000001f) continue;
                    var contains = false;
                    foreach (var k in remaining)
                        if (k != a && k != b && k != c && Cross(p[b]-p[a],p[k]-p[a]) >= 0 &&
                            Cross(p[c]-p[b],p[k]-p[b]) >= 0 && Cross(p[a]-p[c],p[k]-p[c]) >= 0)
                        { contains = true; break; }
                    if (contains) continue;
                    result.AddRange(new[] { a,b,c }); remaining.RemoveAt(i); clipped = true; break;
                }
                if (!clipped) break;
            }
            return result;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x*b.y - a.y*b.x;

        public void Dispose()
        {
            foreach (var mesh in _meshes.Values) Release(mesh);
            _meshes.Clear(); Release(_material);
        }

        internal static void Release(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Object.Destroy(obj); else Object.DestroyImmediate(obj);
        }
    }
}
