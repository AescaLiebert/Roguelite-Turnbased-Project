using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FightingAllstar.EditorTools
{
    /// <summary>Deterministic, closed scenery meshes. Dimensions are full local-space extents.</summary>
    public static class TeyvatSurroundingsGeometry
    {
        const float Tau = Mathf.PI * 2f;

        /// <summary>Rock is submesh 0; the broad top and intermediate grassy shelves are submesh 1.</summary>
        public static Mesh Landform(string name, float width, float depth, float height, int seed, bool terraced)
        {
            CheckDimensions(width, depth, height);
            const int sides = 64;
            var random = new System.Random(seed);
            var phase = Phases(random);
            var outline = new float[16];
            for (var i = 0; i < outline.Length; i++)
            {
                var a = Tau * i / outline.Length;
                outline[i] = 1f + .14f * Mathf.Sin(a * 3f + phase[0])
                              + .065f * Mathf.Sin(a * 5f + phase[1]) + (float)random.NextDouble() * .12f;
            }
            // Two broken coves introduce recognisable angular bays without punching holes in the mesh.
            var notch = random.Next(outline.Length);
            outline[notch] *= .75f; outline[(notch + 7) % outline.Length] *= .83f;
            var lowerCentre = phase[2];
            var upperCentre = lowerCentre + 1.8f + (float)random.NextDouble() * 1.3f;
            var lowerWidth = .65f + (float)random.NextDouble() * .50f;
            var upperWidth = .48f + (float)random.NextDouble() * .45f;
            var secondLedge = random.Next(3) != 0;
            var lowerHeight = .24f + (float)random.NextDouble() * .12f;
            var upperHeight = .57f + (float)random.NextDouble() * .13f;
            var capHeight = .84f + (float)random.NextDouble() * .045f;
            // A single tall geological face supports a broad cap. Ledges occupy only short angular sectors,
            // so they disappear into the face rather than wrapping around as identical stacked platforms.
            var levels = new[] { -.025f, 0f, .12f, lowerHeight, lowerHeight + .025f,
                upperHeight, upperHeight + .022f, capHeight, capHeight + .035f, .94f, .976f, 1f };
            var radii = new[] { .92f, 1f, .995f, .975f, .972f, .955f, .950f, .925f, .895f, .67f, .36f, .095f };
            var rings = new Vector3[levels.Length][];
            for (var row = 0; row < levels.Length; row++)
            {
                rings[row] = new Vector3[sides];
                for (var i = 0; i < sides; i++)
                {
                    var angle = Tau * i / sides;
                    var silhouette = MesaOutline(angle, outline);
                    var inset = Mathf.Clamp01(levels[row]);
                    var r = radii[row] * silhouette;
                    if (terraced)
                    {
                        var lower = LedgeSector(angle, lowerCentre, lowerWidth);
                        var upper = secondLedge ? LedgeSector(angle, upperCentre, upperWidth) : 0f;
                        if (row == 2) r += lower * .055f;
                        if (row == 3) r += lower * .095f;
                        if (row == 4) r -= lower * .095f;
                        if (row == 5) r += upper * .065f;
                        if (row == 6) r -= upper * .080f;
                    }
                    // Offset strata and sloping joint lines break the extrusion; the grassy roof stays broad.
                    r += Mathf.Sin(angle * 4f + phase[3] + inset * 1.7f) * .018f * Mathf.Sin(inset * Mathf.PI);
                    var driftX = .12f * inset * Mathf.Sin(phase[0]);
                    var driftZ = .11f * inset * Mathf.Cos(phase[1]);
                    var x = Mathf.Cos(angle) * r + driftX;
                    var z = Mathf.Sin(angle) * r + driftZ;
                    var stratum = (Mathf.Sin(angle + phase[3]) * .045f
                                  + Mathf.Sin(angle * 3f + phase[4]) * .018f) * Mathf.Sin(inset * Mathf.PI * .83f);
                    var y = levels[row] < 0f ? levels[row] : levels[row] + stratum;
                    rings[row][i] = new Vector3(x, y * height, z);
                }
            }
            FitXZ(rings, width, depth);
            var output = new Facets(name);
            for (var row = 0; row < rings.Length - 1; row++)
            {
                for (var i = 0; i < sides; i++)
                {
                    var next = (i + 1) % sides;
                    var angle = Tau * (i + .5f) / sides;
                    var shelf = row >= 8 || terraced &&
                        (row == 3 && LedgeSector(angle, lowerCentre, lowerWidth) > .25f ||
                         row == 5 && secondLedge && LedgeSector(angle, upperCentre, upperWidth) > .25f);
                    // Reversing the diagonal on alternating cells avoids a conspicuous spiral pattern.
                    if (((i + row) & 1) == 0)
                    {
                        output.Face(rings[row][i], rings[row + 1][i], rings[row][next], shelf ? 1 : 0);
                        output.Face(rings[row][next], rings[row + 1][i], rings[row + 1][next], shelf ? 1 : 0);
                    }
                    else
                    {
                        output.Face(rings[row][i], rings[row + 1][i], rings[row + 1][next], shelf ? 1 : 0);
                        output.Face(rings[row][i], rings[row + 1][next], rings[row][next], shelf ? 1 : 0);
                    }
                }
            }
            Close(output, rings[0], rings[rings.Length - 1], -.025f * height, height * 1.006f, 1);
            return output.Build();
        }

        static float LedgeSector(float angle, float centre, float halfWidth)
        {
            var distance = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, centre * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((distance / halfWidth - .48f) / .52f));
        }

        static float MesaOutline(float angle, float[] corners)
        {
            var corner = angle / Tau * corners.Length;
            var i = Mathf.FloorToInt(corner) % corners.Length;
            var a = Tau * i / corners.Length;
            var b = Tau * (i + 1) / corners.Length;
            var ax = Mathf.Cos(a) * corners[i]; var az = Mathf.Sin(a) * corners[i];
            var bx = Mathf.Cos(b) * corners[(i + 1) % corners.Length];
            var bz = Mathf.Sin(b) * corners[(i + 1) % corners.Length];
            // Intersect the radial ray with a straight polygon edge, retaining broad planar cliff facets.
            return (ax * bz - az * bx) / (Mathf.Cos(angle) * (bz - az) - Mathf.Sin(angle) * (bx - ax));
        }

        /// <summary>Rock is submesh 0; altitude- and slope-dependent snow is submesh 1.</summary>
        public static Mesh SnowPeak(string name, float width, float depth, float height, int seed)
        {
            CheckDimensions(width, depth, height);
            const int sides = 72, ringCount = 24;
            var random = new System.Random(seed);
            var phase = Phases(random);
            var mirror = random.Next(2) == 0 ? -1f : 1f;
            var peaks = new[]
            {
                new Vector3(-.23f * mirror, 1.00f, .08f),
                new Vector3(.22f * mirror, .84f, .23f),
                new Vector3(.43f * mirror, .61f, -.11f),
                new Vector3(-.46f * mirror, .58f, -.22f),
                new Vector3(-.02f * mirror, .63f, -.40f)
            };
            var rings = new Vector3[ringCount][];
            var highest = .001f;
            for (var row = 0; row < ringCount; row++)
            {
                rings[row] = new Vector3[sides];
                var radius = Mathf.Lerp(1f, .035f, row / (float)(ringCount - 1));
                for (var i = 0; i < sides; i++)
                {
                    var angle = Tau * i / sides;
                    var r = radius * Outline(angle, phase);
                    var x = Mathf.Cos(angle) * r;
                    var z = Mathf.Sin(angle) * r;
                    var y = row == 0 ? 0f : AlpineHeight(x, z, radius, peaks, phase);
                    highest = Mathf.Max(highest, y);
                    rings[row][i] = new Vector3(x, y, z);
                }
            }
            var centreHeight = AlpineHeight(0f, 0f, 0f, peaks, phase);
            highest = Mathf.Max(highest, centreHeight);
            for (var row = 0; row < ringCount; row++)
                for (var i = 0; i < sides; i++)
                {
                    var p = rings[row][i];
                    p.y = p.y / highest * height;
                    rings[row][i] = p;
                }
            FitXZ(rings, width, depth);
            var output = new Facets(name);
            for (var row = 0; row < ringCount - 1; row++)
                for (var i = 0; i < sides; i++)
                {
                    var next = (i + 1) % sides;
                    var a = rings[row][i]; var b = rings[row + 1][i];
                    var c = rings[row][next]; var d = rings[row + 1][next];
                    if (((row + i) & 1) == 0)
                    {
                        SnowFace(output, a, b, c, width, depth, height, phase);
                        SnowFace(output, c, b, d, width, depth, height, phase);
                    }
                    else
                    {
                        SnowFace(output, a, b, d, width, depth, height, phase);
                        SnowFace(output, a, d, c, width, depth, height, phase);
                    }
                }
            var top = AverageXZ(rings[ringCount - 1]);
            top.y = centreHeight / highest * height;
            var bottom = AverageXZ(rings[0]); bottom.y = -.025f * height;
            for (var i = 0; i < sides; i++)
            {
                var next = (i + 1) % sides;
                var a = rings[0][i]; var b = rings[0][next];
                var lowA = a; lowA.y = bottom.y;
                var lowB = b; lowB.y = bottom.y;
                output.Face(lowA, a, lowB, 0); output.Face(lowB, a, b, 0);
                output.Face(bottom, lowA, lowB, 0);
                SnowFace(output, top, rings[ringCount - 1][next], rings[ringCount - 1][i], width, depth, height, phase);
            }
            return output.Build();
        }

        static float AlpineHeight(float x, float z, float radius, Vector3[] peaks, float[] phase)
        {
            var y = 0f;
            for (var i = 0; i < peaks.Length; i++)
            {
                var p = peaks[i];
                var dx = (x - p.x) / (i == 0 ? .70f : .47f);
                var dz = (z - p.z) / (i == 0 ? .58f : .43f);
                var distance = Mathf.Sqrt(dx * dx + dz * dz);
                // Piecewise-linear slopes retain a sculpted alpine shape rather than a rounded Gaussian hill.
                y = Mathf.Max(y, p.y * Mathf.Max(0f, 1f - distance));
            }
            for (var i = 1; i < peaks.Length; i++)
            {
                var a = new Vector2(peaks[0].x, peaks[0].z);
                var b = new Vector2(peaks[i].x, peaks[i].z);
                var p = new Vector2(x, z);
                var t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
                var distance = Vector2.Distance(p, Vector2.Lerp(a, b, t));
                var ridge = Mathf.Lerp(peaks[0].y, peaks[i].y, t) * .94f - distance * 2.15f;
                y = Mathf.Max(y, ridge);
            }
            var foothill = .27f * Mathf.Max(0f, 1f - Mathf.Sqrt(x * x * .84f + z * z));
            y = Mathf.Max(y, foothill);
            var grooves = Mathf.Sin(x * 17f + z * 6f + phase[0]) * .022f
                          + Mathf.Sin(z * 13f - x * 7f + phase[2]) * .015f;
            y = Mathf.Max(0f, y + grooves * Mathf.Clamp01(y * 5f));
            // Every outer boundary is at base level, even along scalloped bays.
            return y * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - radius) * 5f));
        }

        static void SnowFace(Facets output, Vector3 a, Vector3 b, Vector3 c, float width, float depth, float height, float[] phase)
        {
            var normal = Vector3.Cross(b - a, c - a).normalized;
            var centre = (a + b + c) / 3f;
            var x = centre.x / width * 2f; var z = centre.z / depth * 2f;
            var gully = .055f * Mathf.Sin(x * 18f + z * 5f + phase[3])
                        + .035f * Mathf.Sin(z * 14f - x * 4f + phase[4]);
            var snowline = .51f + gully + .09f * x;
            // Very steep rock needles and lower gullies interrupt the snow instead of a white horizontal cap.
            var snowy = centre.y / height > snowline && normal.y > .21f;
            output.Face(a, b, c, snowy ? 1 : 0);
        }

        static float Outline(float angle, float[] phase)
        {
            return 1f + .115f * Mathf.Sin(angle * 3f + phase[0])
                      + .070f * Mathf.Sin(angle * 5f + phase[1])
                      + .032f * Mathf.Sin(angle * 9f + phase[2]);
        }

        static float[] Phases(System.Random random)
        {
            var result = new float[5];
            for (var i = 0; i < result.Length; i++) result[i] = (float)random.NextDouble() * Tau;
            return result;
        }

        static void CheckDimensions(float width, float depth, float height)
        {
            if (width <= 0f || depth <= 0f || height <= 0f || float.IsNaN(width + depth + height)
                || float.IsInfinity(width + depth + height))
                throw new ArgumentOutOfRangeException(nameof(width), "Scenery dimensions must be finite and positive.");
        }

        static void FitXZ(Vector3[][] rings, float width, float depth)
        {
            var minX = float.PositiveInfinity; var maxX = float.NegativeInfinity;
            var minZ = float.PositiveInfinity; var maxZ = float.NegativeInfinity;
            foreach (var ring in rings) foreach (var p in ring)
            { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z); }
            var midX = (minX + maxX) * .5f; var midZ = (minZ + maxZ) * .5f;
            foreach (var ring in rings) for (var i = 0; i < ring.Length; i++)
            {
                var p = ring[i];
                p.x = (p.x - midX) / (maxX - minX) * width;
                p.z = (p.z - midZ) / (maxZ - minZ) * depth;
                ring[i] = p;
            }
        }

        static Vector3 AverageXZ(Vector3[] ring)
        {
            var centre = Vector3.zero;
            foreach (var p in ring) centre += p;
            centre /= ring.Length;
            return centre;
        }

        static void Close(Facets output, Vector3[] bottomRing, Vector3[] topRing, float bottomY, float topY, int topMaterial)
        {
            var bottom = AverageXZ(bottomRing); bottom.y = bottomY;
            var top = AverageXZ(topRing); top.y = topY;
            for (var i = 0; i < bottomRing.Length; i++)
            {
                var next = (i + 1) % bottomRing.Length;
                output.Face(bottom, bottomRing[i], bottomRing[next], 0);
                output.Face(top, topRing[next], topRing[i], topMaterial);
            }
        }

        sealed class Facets
        {
            readonly string name;
            readonly List<Vector3> vertices = new List<Vector3>(12000);
            readonly List<Vector3> normals = new List<Vector3>(12000);
            readonly List<Vector2> uv = new List<Vector2>(12000);
            readonly List<int>[] triangles = { new List<int>(10000), new List<int>(5000) };

            public Facets(string name) { this.name = name; }

            public void Face(Vector3 a, Vector3 b, Vector3 c, int material)
            {
                var cross = Vector3.Cross(b - a, c - a);
                if (cross.sqrMagnitude < 1e-12f) return;
                var normal = cross.normalized;
                var first = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                normals.Add(normal); normals.Add(normal); normals.Add(normal);
                uv.Add(Project(a, normal)); uv.Add(Project(b, normal)); uv.Add(Project(c, normal));
                triangles[material].Add(first); triangles[material].Add(first + 1); triangles[material].Add(first + 2);
            }

            static Vector2 Project(Vector3 p, Vector3 normal)
            {
                var x = Mathf.Abs(normal.x); var y = Mathf.Abs(normal.y); var z = Mathf.Abs(normal.z);
                if (y >= x && y >= z) return new Vector2(p.x, p.z) * .05f;
                return x >= z ? new Vector2(p.z, p.y) * .05f : new Vector2(p.x, p.y) * .05f;
            }

            public Mesh Build()
            {
                var mesh = new Mesh { name = name, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv);
                mesh.subMeshCount = 2;
                mesh.SetTriangles(triangles[0], 0, false); mesh.SetTriangles(triangles[1], 1, false);
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
                return mesh;
            }
        }
    }
}
