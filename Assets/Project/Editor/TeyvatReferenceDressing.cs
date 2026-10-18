using System.Collections.Generic;
using UnityEngine;

namespace FightingAllstar.EditorTools
{
    public static partial class TeyvatFoundationBuilder
    {
        static void BuildReferenceDressing(Transform root)
        {
            Make("ReferenceWindow", new Color(.16f, .23f, .28f));
            Make("ReferenceRoof", new Color(.30f, .40f, .47f));
            var vegetation = Group("04 Reference tree silhouettes - replaceable WP05 volumes", root);
            ReferenceBroadleaf(vegetation, -33, 3, 1.1f, 3100);
            ReferenceBroadleaf(vegetation, 40, -6, .72f, 3101);
            ReferenceBroadleaf(vegetation, -62, 21, .58f, 3102);
            ReferenceBroadleaf(vegetation, 69, 22, .53f, 3103);
            ReferenceBroadleaf(vegetation, -78, 67, .59f, 3104);
            ReferenceBroadleaf(vegetation, -18, 70, .51f, 3105);
            ReferenceBroadleaf(vegetation, 26, 72, .45f, 3106);
            ReferenceBroadleaf(vegetation, 73, 68, .56f, 3107);
            ReferenceBroadleaf(vegetation, 109, 90, .50f, 3108);
            ReferenceBroadleaf(vegetation, -92, 113, .55f, 3109);
            ReferenceBroadleaf(vegetation, 12, -48, .58f, 3110);
            ReferenceBroadleaf(vegetation, -52, -44, .53f, 3111);
            ReferenceBroadleaf(vegetation, 65, -42, .62f, 3112);

            var conifers = Group("Terrace and reverse ridge conifers", vegetation);
            var rng = new System.Random(3429);
            for (var i = 0; i < 38; i++)
            {
                var x = -113 + (i % 13) * 18f + ((float)rng.NextDouble() - .5f) * 9f;
                var z = 92 + (i / 13) * 25f + (float)rng.NextDouble() * 10f;
                if (Mathf.Abs(x + 50) < 12 && Mathf.Abs(z - 106) < 18) continue;
                ReferencePine(conifers, x, z, .58f + (float)rng.NextDouble() * .42f, 3400 + i);
            }
            for (var i = 0; i < 11; i++)
                ReferencePine(conifers, -105 + i * 21, -61 - (i % 3) * 6, .65f + (i % 3) * .13f, 3480 + i);

            var architecture = Group("05 Reference landmarks - arch cathedral aqueduct", root);
            ReferenceRuinArch(architecture);
            ReferenceCathedral(architecture);
            ReferenceAqueduct(architecture);

            var border = Group("07 Low meadow frame - outside protected battle clearing", root);
            // Unequal loose groups leave open stretches of meadow rather than drawing a stone necklace.
            var rockLayout = new[]
            {
                new Vector3(-36, 4, 1.35f), new Vector3(-31, -3, 1.05f), new Vector3(-28, 6, .70f),
                new Vector3(-36, 11, .82f), new Vector3(-41, -1, .63f), new Vector3(-28, -8, .52f),
                new Vector3(31, 8, 1.08f), new Vector3(34, 15, .87f), new Vector3(26, 16, .60f), new Vector3(38, 5, .48f),
                new Vector3(-28, -26, 1.40f), new Vector3(-23, -29, .63f), new Vector3(-31, -24, .49f),
                new Vector3(26, -28, 1.17f), new Vector3(31, -24, .55f)
            };
            for (var i = 0; i < rockLayout.Length; i++)
            {
                var x = rockLayout[i].x; var z = rockLayout[i].y; var size = rockLayout[i].z;
                if (Mathf.Abs(x) < 24 && Mathf.Abs(z) < 21.5f) continue;
                var h = ReferenceHeight(x, z);
                Rock("Meadow frame boulder " + i, border, new Vector3(x, h - .15f, z),
                    new Vector3(4.7f * size, 2.3f * size, 3.6f * size),
                    i % 4 == 0 ? "RockShade" : "Rock", 3600 + i);
            }
            ReferenceMeadowBorder(border);
        }

        static void ReferenceBroadleaf(Transform parent, float x, float z, float scale, int seed)
        {
            var tree = Group("Broadleaf tree " + seed, parent);
            tree.localPosition = new Vector3(x, ReferenceHeight(x, z) - .08f, z);
            tree.localScale = Vector3.one * scale;
            var rng = new System.Random(seed);
            ReferenceBranch("Bent trunk", tree, new Vector3(0, 0, 0), new Vector3(-.45f, 4.7f, .18f), 1.10f, .65f);
            ReferenceBranch("Trunk crown fork", tree, new Vector3(-.45f, 4.7f, .18f), new Vector3(.45f, 8.2f, -.1f), .65f, .28f);
            for (var i = 0; i < 5; i++)
            {
                var a = i / 5f * Mathf.PI * 2 + .3f;
                ReferenceBranch("Buttress root " + i, tree,
                    new Vector3(Mathf.Cos(a) * 2.1f, .03f, Mathf.Sin(a) * 2.1f),
                    new Vector3(-.1f, 1.8f, .06f), .08f, .54f);
            }
            ReferenceCanopy("Connected central crown", tree, new Vector3(.1f, 9.9f, 0), new Vector3(5.8f, 3.6f, 5.2f), seed, "Leaves");
            for (var i = 0; i < 8; i++)
            {
                var a = i / 8f * Mathf.PI * 2f + .15f;
                var reach = 4.2f + (float)rng.NextDouble() * 1.5f;
                var tip = new Vector3(Mathf.Cos(a) * reach, 7.3f + (i % 3) * 1.35f, Mathf.Sin(a) * reach * .78f);
                var fork = new Vector3(-.25f, 4.2f + i % 3 * .7f, .1f);
                var elbow = Vector3.Lerp(fork, tip, .58f) + Vector3.down * .6f;
                ReferenceBranch("Branch fork " + i, tree, fork, elbow, .4f, .22f);
                ReferenceBranch("Branch tip " + i, tree, elbow, tip + Vector3.up * .6f, .22f, .07f);
                var radius = 3.6f + (float)rng.NextDouble() * .9f;
                ReferenceCanopy("Overlapping organic canopy " + i, tree, tip + Vector3.up * 1.1f,
                    new Vector3(radius, 2.4f + (i % 2) * .6f, radius * .85f), seed + i * 19,
                    i % 3 == 0 ? "LeafLight" : "Leaves");
            }
        }

        static void ReferenceBranch(string name, Transform parent, Vector3 start, Vector3 end, float baseRadius, float tipRadius)
        {
            const int sides = 9;
            var axis = (end - start).normalized;
            var tangent = Vector3.Cross(axis, Mathf.Abs(axis.y) > .95f ? Vector3.right : Vector3.up).normalized;
            var bitangent = Vector3.Cross(tangent, axis).normalized;
            var verts = new List<Vector3>();
            var indices = new List<int>();
            for (var i = 0; i < sides; i++)
            {
                var a = i * Mathf.PI * 2 / sides;
                var b = (i + 1) * Mathf.PI * 2 / sides;
                var va = tangent * Mathf.Cos(a) + bitangent * Mathf.Sin(a);
                var vb = tangent * Mathf.Cos(b) + bitangent * Mathf.Sin(b);
                AddFace(verts, indices, start + va * baseRadius, end + va * tipRadius, start + vb * baseRadius);
                AddFace(verts, indices, start + vb * baseRadius, end + va * tipRadius, end + vb * tipRadius);
                AddFace(verts, indices, end, end + vb * tipRadius, end + va * tipRadius);
            }
            var mesh = new Mesh { vertices = verts.ToArray(), triangles = indices.ToArray() };
            mesh.RecalculateNormals();
            MeshObject(name, parent, mesh, "Trunk");
        }

        static void ReferenceCanopy(string name, Transform parent, Vector3 center, Vector3 radii, int seed, string material)
        {
            // A single irregular shell per overlapping crown lobe produces connected, natural tree masses.
            const int rings = 10, sides = 18;
            var vertices = new Vector3[(rings + 1) * (sides + 1)];
            var normals = new Vector3[vertices.Length];
            var triangles = new List<int>();
            for (var j = 0; j <= rings; j++)
            {
                var phi = j / (float)rings * Mathf.PI;
                for (var i = 0; i <= sides; i++)
                {
                    var theta = (i % sides) / (float)sides * Mathf.PI * 2f;
                    var n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                    var scallop = 1 + Mathf.Sin(theta * 5 + seed) * Mathf.Sin(phi) * .075f
                        + (Mathf.PerlinNoise(n.x * 2.4f + seed * .13f, n.z * 2.4f + n.y * 1.7f + 5) - .5f) * .24f;
                    var index = j * (sides + 1) + i;
                    vertices[index] = center + Vector3.Scale(n, radii) * scallop;
                    normals[index] = new Vector3(n.x * .5f, n.y + .45f, n.z * .5f).normalized;
                    if (j < rings && i < sides)
                    {
                        var k = index;
                        triangles.AddRange(new[] { k, k + 1, k + sides + 1, k + 1, k + sides + 2, k + sides + 1 });
                    }
                }
            }
            var mesh = new Mesh { vertices = vertices, normals = normals, triangles = triangles.ToArray() };
            mesh.RecalculateBounds();
            MeshObject(name, parent, mesh, material);
        }

        static void ReferencePine(Transform parent, float x, float z, float scale, int seed)
        {
            var tree = Group("Pine " + seed, parent);
            tree.localPosition = new Vector3(x, ReferenceHeight(x, z), z);
            tree.localScale = Vector3.one * scale;
            ReferenceBranch("Pine trunk", tree, Vector3.zero, new Vector3(.12f, 10, 0), .35f, .05f);
            var verts = new List<Vector3>();
            var tri = new List<int>();
            const int sides = 10;
            for (var tier = 0; tier < 5; tier++)
            {
                var y = 2f + tier * 1.6f;
                var radius = 3.4f - tier * .52f;
                for (var i = 0; i < sides; i++)
                {
                    var a = i * Mathf.PI * 2 / sides + tier * .24f;
                    var b = (i + 1) * Mathf.PI * 2 / sides + tier * .24f;
                    var p = new Vector3(Mathf.Cos(a) * radius, y + (i % 2) * .24f, Mathf.Sin(a) * radius);
                    var q = new Vector3(Mathf.Cos(b) * radius, y + ((i + 1) % 2) * .24f, Mathf.Sin(b) * radius);
                    AddFace(verts, tri, p, new Vector3(.12f, y + 4.3f - tier * .2f, 0), q);
                    AddFace(verts, tri, Vector3.up * (y + .25f), p, q);
                }
            }
            var mesh = new Mesh { vertices = verts.ToArray(), triangles = tri.ToArray() };
            mesh.RecalculateNormals();
            MeshObject("Layered conifer crown", tree, mesh, seed % 3 == 0 ? "LeafLight" : "Leaves");
        }

        static void ReferenceRuinArch(Transform parent)
        {
            const float x = 26, z = 8;
            var ruin = Group("Mossy stone arch - actual opening", parent);
            ruin.localPosition = new Vector3(x, ReferenceHeight(x, z), z);
            ruin.localRotation = Quaternion.Euler(0, -17, 0);
            for (var side = -1; side <= 1; side += 2)
            {
                for (var i = 0; i < 3; i++)
                {
                    var block = Primitive("Worn pier masonry", PrimitiveType.Cube, ruin,
                        new Vector3(side * 3f, .8f + i * 1.5f, 0), new Vector3(1.65f, 1.48f, 2.3f), i % 2 == 0 ? "Rock" : "RockShade");
                    block.transform.localRotation = Quaternion.Euler(0, side * (i % 2) * 4, side * (i % 2) * 2);
                }
                Primitive("Arch springer", PrimitiveType.Cube, ruin, new Vector3(side * 3, 4.7f, 0), new Vector3(2f, .45f, 2.5f), "Rock");
            }
            ReferenceArchStones(ruin, Vector3.up * 4.75f, 3, 3.4f, 1.2f, 2.3f, 11, "Rock");
            Rock("Grass on ruined keystone", ruin, new Vector3(.1f, 8.06f, 0), new Vector3(2.4f, .25f, 2.25f), "GrassCap", 3710);
            Rock("Fallen arch block", ruin, new Vector3(4.5f, .05f, -2.2f), new Vector3(2.2f, 1.5f, 1.9f), "Rock", 3711);
            ReferenceBranch("Climbing vine", ruin, new Vector3(-3.9f, .1f, -1.24f), new Vector3(-3.5f, 5.4f, -1.24f), .1f, .04f);
            for (var i = 0; i < 6; i++)
                ReferenceCanopy("Arch ivy clump " + i, ruin,
                    new Vector3(-3.1f + i * .55f, 5.3f + Mathf.Sin(i * .45f) * 2.8f, -.9f),
                    new Vector3(.65f, .22f, .38f), 3720 + i, "Leaves");
        }

        static void ReferenceArchStones(Transform parent, Vector3 center, float rx, float ry, float thickness, float depth, int count, string material)
        {
            // Each radial voussoir is a wedge, so adjacent stones meet around a clear hole.
            for (var i = 0; i < count; i++)
            {
                var a = i / (float)count * Mathf.PI + .009f;
                var b = (i + 1) / (float)count * Mathf.PI - .009f;
                var innerA = new Vector3(Mathf.Cos(a) * (rx - thickness * .5f), Mathf.Sin(a) * (ry - thickness * .5f), 0);
                var innerB = new Vector3(Mathf.Cos(b) * (rx - thickness * .5f), Mathf.Sin(b) * (ry - thickness * .5f), 0);
                var outerA = new Vector3(Mathf.Cos(a) * (rx + thickness * .5f), Mathf.Sin(a) * (ry + thickness * .5f), 0);
                var outerB = new Vector3(Mathf.Cos(b) * (rx + thickness * .5f), Mathf.Sin(b) * (ry + thickness * .5f), 0);
                var v = new List<Vector3>(); var t = new List<int>();
                var f = Vector3.back * depth * .5f; var r = -f;
                ReferenceQuad(v, t, center + innerA + f, center + outerA + f, center + outerB + f, center + innerB + f);
                ReferenceQuad(v, t, center + innerB + r, center + outerB + r, center + outerA + r, center + innerA + r);
                ReferenceQuad(v, t, center + innerA + r, center + outerA + r, center + outerA + f, center + innerA + f);
                ReferenceQuad(v, t, center + innerB + f, center + outerB + f, center + outerB + r, center + innerB + r);
                ReferenceQuad(v, t, center + outerA + f, center + outerA + r, center + outerB + r, center + outerB + f);
                ReferenceQuad(v, t, center + innerB + f, center + innerB + r, center + innerA + r, center + innerA + f);
                var mesh = new Mesh { vertices = v.ToArray(), triangles = t.ToArray() }; mesh.RecalculateNormals();
                MeshObject("Radial arch stone " + i, parent, mesh, material);
            }
        }

        static void ReferenceQuad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            AddFace(v, t, a, c, b); AddFace(v, t, a, d, c);
        }

        static void ReferenceCathedral(Transform parent)
        {
            const float x = -50, z = 106;
            var church = Group("Gothic hilltop cathedral - nave belfry and pinnacles", parent);
            church.localPosition = new Vector3(x, ReferenceHeight(x, z), z);
            church.localRotation = Quaternion.Euler(0, -8, 0);
            Primitive("Cathedral stone plinth", PrimitiveType.Cube, church, new Vector3(0, .7f, 1.5f), new Vector3(17, 1.4f, 25), "Rock");
            Primitive("Long nave", PrimitiveType.Cube, church, new Vector3(0, 5.7f, 4), new Vector3(11, 10, 17), "Rock");
            ReferenceGableRoof(church, new Vector3(0, 10.6f, 4), 12.2f, 18.5f, 5.3f);
            Primitive("Crossing transept", PrimitiveType.Cube, church, new Vector3(0, 4.9f, 7), new Vector3(17.5f, 8.2f, 5.1f), "Rock");
            for (var side = -1; side <= 1; side += 2)
                for (var i = 0; i < 4; i++)
                {
                    var wallZ = -2f + i * 4.2f;
                    Primitive("Nave buttress", PrimitiveType.Cube, church, new Vector3(side * 6.2f, 4.3f, wallZ), new Vector3(1.1f, 8.2f, 1.5f), "RockShade");
                    var window = Group("Recessed lancet window", church);
                    window.localPosition = new Vector3(side * 5.56f, 5.5f, wallZ + 1.9f);
                    window.localRotation = Quaternion.Euler(0, 90, 0);
                    Primitive("Dark window inset", PrimitiveType.Cube, window, Vector3.zero, new Vector3(1.05f, 4.4f, .10f), "ReferenceWindow");
                    ReferenceArchStones(window, Vector3.up * 2.15f, .6f, 1.05f, .2f, .18f, 5, "RockShade");
                }
            var tower = Group("Cathedral front tower", church);
            tower.localPosition = new Vector3(0, .8f, -7.1f);
            Primitive("Tower lower keep", PrimitiveType.Cube, tower, new Vector3(0, 8.4f, 0), new Vector3(7.6f, 16.8f, 7.6f), "RockShade");
            Primitive("Tower vertical front face", PrimitiveType.Cube, tower, new Vector3(0, 10.7f, -3.85f), new Vector3(6.9f, 11.7f, .45f), "Rock");
            Primitive("Portal recess", PrimitiveType.Cube, tower, new Vector3(0, 3.9f, -4.1f), new Vector3(2.8f, 6.2f, .1f), "ReferenceWindow");
            ReferenceArchStones(tower, new Vector3(0, 6.8f, -4.15f), 1.6f, 2.4f, .5f, .6f, 7, "Rock");
            Primitive("Belfry lower cornice", PrimitiveType.Cube, tower, new Vector3(0, 17, 0), new Vector3(8.8f, .9f, 8.8f), "Rock");
            for (var i = 0; i < 4; i++)
            {
                var corner = new Vector3(i % 2 == 0 ? -3.3f : 3.3f, 21, i < 2 ? -3.3f : 3.3f);
                Primitive("Open belfry corner column", PrimitiveType.Cube, tower, corner, new Vector3(1.1f, 7.2f, 1.1f), "Rock");
                Primitive("Corner pinnacle pedestal", PrimitiveType.Cube, tower, new Vector3(corner.x, 26.1f, corner.z), new Vector3(1.3f, 4, 1.3f), "RockShade");
                ReferenceSpire(tower, new Vector3(corner.x, 28.1f, corner.z), 1.05f, 6.8f, "RockShade");
            }
            for (var side = 0; side < 4; side++)
            {
                var arch = Group("Open gothic belfry arch " + side, tower);
                arch.localRotation = Quaternion.Euler(0, side * 90, 0);
                arch.localPosition = arch.localRotation * new Vector3(0, 21.4f, -3.3f);
                ReferenceArchStones(arch, Vector3.zero, 2.8f, 3.1f, .65f, .9f, 9, "Rock");
            }
            Primitive("Belfry crown ledge", PrimitiveType.Cube, tower, new Vector3(0, 25.1f, 0), new Vector3(8.4f, .85f, 8.4f), "Rock");
            ReferenceSpire(tower, new Vector3(0, 25.5f, 0), 3.1f, 11.5f, "ReferenceRoof");
        }

        static void ReferenceGableRoof(Transform parent, Vector3 center, float width, float length, float rise)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            var a = center + new Vector3(-width * .5f, 0, -length * .5f);
            var b = center + new Vector3(width * .5f, 0, -length * .5f);
            var c = center + new Vector3(-width * .5f, 0, length * .5f);
            var d = center + new Vector3(width * .5f, 0, length * .5f);
            var e = center + new Vector3(0, rise, -length * .5f);
            var f = center + new Vector3(0, rise, length * .5f);
            ReferenceQuad(v, t, a, e, f, c); ReferenceQuad(v, t, b, d, f, e);
            AddFace(v, t, a, e, b); AddFace(v, t, c, d, f);
            var mesh = new Mesh { vertices = v.ToArray(), triangles = t.ToArray() }; mesh.RecalculateNormals();
            MeshObject("Steep cathedral slate roof", parent, mesh, "ReferenceRoof");
        }

        static void ReferenceSpire(Transform parent, Vector3 bottom, float radius, float height, string material)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            const int sides = 8;
            for (var i = 0; i < sides; i++)
            {
                var a = i * Mathf.PI * 2 / sides; var b = (i + 1) * Mathf.PI * 2 / sides;
                AddFace(v, t, bottom + new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius),
                    bottom + Vector3.up * height, bottom + new Vector3(Mathf.Cos(b) * radius, 0, Mathf.Sin(b) * radius));
            }
            var mesh = new Mesh { vertices = v.ToArray(), triangles = t.ToArray() }; mesh.RecalculateNormals();
            MeshObject("Stone pinnacle spire", parent, mesh, material);
        }

        static void ReferenceAqueduct(Transform parent)
        {
            const float start = 25, end = 110, z = 125;
            const int spans = 5;
            var bridge = Group("Distant aqueduct - grounded five open arches", parent);
            var highestGround = -1000f;
            for (var i = 0; i <= spans; i++)
                highestGround = Mathf.Max(highestGround, ReferenceHeight(Mathf.Lerp(start, end, i / (float)spans), z));
            var deck = highestGround + 15.5f;
            var span = (end - start) / spans;
            for (var i = 0; i <= spans; i++)
            {
                var x = start + i * span;
                var ground = ReferenceHeight(x, z) - .8f;
                Primitive("Aqueduct grounded pier " + i, PrimitiveType.Cube, bridge,
                    new Vector3(x, (deck + ground) * .5f, z), new Vector3(3f, deck - ground, 4.2f), "RockShade");
                Primitive("Pier capital " + i, PrimitiveType.Cube, bridge,
                    new Vector3(x, deck - 7.7f, z), new Vector3(3.8f, .65f, 4.8f), "Rock");
            }
            for (var i = 0; i < spans; i++)
                ReferenceArchStones(bridge, new Vector3(start + span * (i + .5f), deck - 8f, z),
                    span * .5f - 1f, 6.5f, 1.45f, 4.2f, 13, "Rock");
            Primitive("Aqueduct continuous deck", PrimitiveType.Cube, bridge,
                new Vector3((start + end) * .5f, deck, z), new Vector3(end - start + 5, 2.6f, 4.5f), "Rock");
            Primitive("Front stone parapet", PrimitiveType.Cube, bridge,
                new Vector3((start + end) * .5f, deck + 1.8f, z - 2.15f), new Vector3(end - start + 5, 1f, .65f), "RockShade");
            Primitive("Back stone parapet", PrimitiveType.Cube, bridge,
                new Vector3((start + end) * .5f, deck + 1.8f, z + 2.15f), new Vector3(end - start + 5, 1f, .65f), "RockShade");
            ReferenceAqueductBank(bridge, start, z, deck, -1);
            ReferenceAqueductBank(bridge, end, z, deck, 1);
        }

        static void ReferenceAqueductBank(Transform parent, float endX, float centerZ, float deck, float outward)
        {
            // Broad attached shoulders taper back to the sampled landscape on every edge.
            // Their top meets the deck and conceals each end pier, avoiding floating bridge termini.
            const int columns = 18, rows = 20;
            var points = new Vector3[(columns + 1) * (rows + 1)];
            for (var j = 0; j <= rows; j++)
                for (var i = 0; i <= columns; i++)
                {
                    var distance = Mathf.Lerp(-3f, 34f, i / (float)columns);
                    var lateral = Mathf.Lerp(-19f, 19f, j / (float)rows);
                    var x = endX + outward * distance;
                    var z = centerZ + lateral;
                    var ground = ReferenceHeight(x, z) - .08f;
                    var bridgeFace = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-3f, -.6f, distance));
                    var outerSlope = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(12f, 34f, distance));
                    var sideSlope = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(4f, 19f, Mathf.Abs(lateral)));
                    var profile = bridgeFace * outerSlope * sideSlope;
                    var crown = deck + 1.1f + Mathf.Sin(distance * .16f + outward) * .15f;
                    var y = Mathf.Lerp(ground, Mathf.Max(ground, crown), profile);
                    points[j * (columns + 1) + i] = new Vector3(x, y, z);
                }
            var rock = new List<Vector3>(); var rockTri = new List<int>();
            var grass = new List<Vector3>(); var grassTri = new List<int>();
            for (var j = 0; j < rows; j++)
                for (var i = 0; i < columns; i++)
                {
                    var k = j * (columns + 1) + i;
                    var a = points[k]; var b = points[k + 1];
                    var c = points[k + columns + 1]; var d = points[k + columns + 2];
                    if (outward < 0) { var swap = a; a = b; b = swap; swap = c; c = d; d = swap; }
                    var normal = Vector3.Cross(c - a, b - a).normalized;
                    var isGrass = normal.y > .68f;
                    var vertices = isGrass ? grass : rock; var triangles = isGrass ? grassTri : rockTri;
                    AddFace(vertices, triangles, a, c, b);
                    AddFace(vertices, triangles, b, c, d);
                }
            ReferenceDressingMesh("Aqueduct " + (outward < 0 ? "west" : "east") + " broad rocky bank", parent, rock, rockTri, "Rock");
            ReferenceDressingMesh("Aqueduct " + (outward < 0 ? "west" : "east") + " grass bank crown", parent, grass, grassTri, "GrassCap");
        }

        static void ReferenceMeadowBorder(Transform parent)
        {
            var blades = new List<Vector3>(); var bladeTri = new List<int>();
            var white = new List<Vector3>(); var whiteTri = new List<int>();
            var purple = new List<Vector3>(); var purpleTri = new List<int>();
            var rng = new System.Random(3801);
            for (var i = 0; i < 1150; i++)
            {
                var a = (float)rng.NextDouble() * Mathf.PI * 2;
                var x = Mathf.Cos(a) * (25f + (float)rng.NextDouble() * 11f);
                var z = Mathf.Sin(a) * (21f + (float)rng.NextDouble() * 9f) - 2f;
                if (Mathf.Abs(x) < 21.5f && Mathf.Abs(z) < 18.5f || z > 23) continue;
                var y = ReferenceHeight(x, z) + .035f;
                var h = .38f + (float)rng.NextDouble() * .65f;
                var p = new Vector3(x, y, z);
                var sideways = new Vector3(Mathf.Cos(a + .6f), 0, Mathf.Sin(a + .6f)) * .065f;
                var tip = p + new Vector3(.15f, h, .08f);
                AddFace(blades, bladeTri, p - sideways, tip, p + sideways);
                AddFace(blades, bladeTri, p + sideways, tip, p - sideways);
                if (i % 3 != 0) continue;
                var usePurple = i % 5 == 0;
                var v = usePurple ? purple : white; var tri = usePurple ? purpleTri : whiteTri;
                var center = p + Vector3.up * (h * .7f);
                for (var j = 0; j < (usePurple ? 3 : 1); j++)
                {
                    var flowerCenter = center + Vector3.up * j * .14f;
                    for (var petal = 0; petal < 5; petal++)
                    {
                        var angle = petal / 5f * Mathf.PI * 2f;
                        var radius = usePurple ? .13f : .20f;
                        var tip1 = flowerCenter + new Vector3(Mathf.Cos(angle) * radius, .04f, Mathf.Sin(angle) * radius);
                        var tip2 = flowerCenter + new Vector3(Mathf.Cos(angle + .75f) * radius, .04f, Mathf.Sin(angle + .75f) * radius);
                        AddFace(v, tri, flowerCenter, tip2, tip1);
                    }
                }
            }
            ReferenceDressingMesh("Low framing grass outside movement envelope", parent, blades, bladeTri, "GrassCap");
            ReferenceDressingMesh("White meadow flower clusters", parent, white, whiteTri, "WhiteFlower");
            ReferenceDressingMesh("Purple flower spikes", parent, purple, purpleTri, "PurpleFlower");
        }

        static void ReferenceDressingMesh(string name, Transform parent, List<Vector3> vertices, List<int> triangles, string material)
        {
            var mesh = new Mesh { vertices = vertices.ToArray(), triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            MeshObject(name, parent, mesh, material);
        }
    }
}
