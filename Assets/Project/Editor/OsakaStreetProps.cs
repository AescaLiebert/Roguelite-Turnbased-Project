using UnityEngine;

namespace FightingAllstar.EditorTools
{
    public static partial class OsakaBattleStageBuilder
    {
        // Set dressing lives entirely on the sidewalks. The central battle apron stays open.
        static void BuildStreetProps(Transform root)
        {
            var street = Group("Street life · sidewalk furniture and spectators", root);

            StreetBench(street, new Vector3(-7.9f, .16f, 13.8f), 0f);
            StreetBench(street, new Vector3(11.1f, .16f, 13.8f), 0f);
            StreetBench(street, new Vector3(-8.8f, .16f, -12.7f), 180f);
            StreetBench(street, new Vector3(12.6f, .16f, -12.7f), 180f);
            StreetBench(street, new Vector3(-13.1f, .16f, -2.7f), 90f);
            StreetBench(street, new Vector3(18.5f, .16f, 2.6f), -90f);

            StreetTree(street, new Vector3(-11f, .16f, 13.2f), 0);
            StreetTree(street, new Vector3(15.2f, .16f, 13.1f), 1);
            StreetTree(street, new Vector3(-10.5f, .16f, -13.5f), 2);
            StreetTree(street, new Vector3(15f, .16f, -13.5f), 3);
            StreetTree(street, new Vector3(-13.4f, .16f, 7.1f), 4);
            StreetTree(street, new Vector3(18.5f, .16f, -7.1f), 5);

            StreetRopeBarrier(street, 11.15f, -9.4f, 13.6f, false);
            StreetRopeBarrier(street, -11.15f, -9.4f, 13.6f, true);
            StreetBicycle(street, new Vector3(-10.2f, .16f, 12.1f), -7f, "Red");
            StreetBicycle(street, new Vector3(13.8f, .16f, 12f), 11f, "ClothBlue");
            StreetVending(street, new Vector3(-12.7f, .16f, 13.2f), 18f);
            StreetBins(street, new Vector3(17.9f, .16f, 7.8f));
            StreetBins(street, new Vector3(-12.7f, .16f, -8.4f));
            StreetHydrant(street, new Vector3(15.5f, .16f, 11.35f));
            StreetLanternString(street, new Vector3(-12.1f, .16f, 13.6f), 28.2f);

            var crowd = Group("Arcade crowd · sixteen sculpted spectators", street);
            var shirtKeys = new[] { "ClothBlue", "ClothOchre", "ClothBurgundy", "Cream" };
            for (var i = 0; i < 16; i++)
            {
                var x = -8.6f + i * 1.37f;
                var z = 12.25f + ((i * 7) % 4) * .22f;
                StreetSpectator(crowd, new Vector3(x, .16f, z), i, shirtKeys[i % 4]);
            }
        }

        static void StreetBench(Transform parent, Vector3 position, float yaw)
        {
            var bench = Group("Slatted timber bench", parent, position);
            bench.localRotation = Quaternion.Euler(0f, yaw, 0f);
            for (var i = 0; i < 5; i++)
                Box("Seat timber slat", bench, new Vector3(0f, .49f, -.24f + i * .12f), new Vector3(2.15f, .075f, .096f), M("Wood"));
            for (var i = 0; i < 4; i++)
            {
                var slat = Box("Raked back timber slat", bench, new Vector3(0f, .65f + i * .115f, .34f + i * .022f), new Vector3(2.15f, .086f, .075f), M("Wood"));
                slat.transform.localRotation = Quaternion.Euler(9f, 0f, 0f);
            }
            foreach (var x in new[] { -.82f, .82f })
            {
                Tube("Front cast iron leg", bench, new Vector3(x, .025f, -.20f), new Vector3(x, .51f, -.16f), .035f, M("Metal"));
                Tube("Back leg and seat support", bench, new Vector3(x, .025f, .24f), new Vector3(x, 1.03f, .42f), .035f, M("Metal"));
                Tube("Seat support", bench, new Vector3(x, .43f, -.31f), new Vector3(x, .43f, .37f), .029f, M("Metal"));
                Tube("Armrest front riser", bench, new Vector3(x, .49f, -.19f), new Vector3(x, .75f, -.19f), .024f, M("Metal"));
                Tube("Armrest rounded rail", bench, new Vector3(x, .75f, -.19f), new Vector3(x, .78f, .36f), .028f, M("Metal"));
                Box("Front footplate", bench, new Vector3(x, .025f, -.21f), new Vector3(.16f, .04f, .15f), M("Metal"));
                Box("Back footplate", bench, new Vector3(x, .025f, .24f), new Vector3(.16f, .04f, .15f), M("Metal"));
            }
            Tube("Lower bench brace", bench, new Vector3(-.82f, .24f, .18f), new Vector3(.82f, .24f, .18f), .021f, M("Metal"));
        }

        static void StreetTree(Transform parent, Vector3 position, int variation)
        {
            var tree = Group("Raised planter and clipped street tree", parent, position);
            Box("Planter body", tree, new Vector3(0f, .28f, 0f), new Vector3(1.35f, .56f, 1.15f), M("Concrete"));
            Box("Soil bed", tree, new Vector3(0f, .575f, 0f), new Vector3(1.14f, .03f, .94f), M("Dark"));
            foreach (var x in new[] { -.62f, .62f })
                Box("Stone planter coping", tree, new Vector3(x, .58f, 0f), new Vector3(.14f, .12f, 1.25f), M("Cream"));
            foreach (var z in new[] { -.52f, .52f })
                Box("Stone planter coping", tree, new Vector3(0f, .58f, z), new Vector3(1.14f, .12f, .14f), M("Cream"));
            Tube("Tree trunk", tree, new Vector3(0f, .60f, 0f), new Vector3(.09f, 2.2f, .04f), .085f, M("Wood"));
            Tube("Branch left", tree, new Vector3(.05f, 1.58f, 0f), new Vector3(-.46f, 2.29f, .05f), .041f, M("Wood"));
            Tube("Branch right", tree, new Vector3(.06f, 1.71f, 0f), new Vector3(.53f, 2.38f, -.08f), .038f, M("Wood"));
            Sphere("Faceted crown center", tree, new Vector3(.04f, 2.57f, 0f), new Vector3(1.52f, 1.25f, 1.3f), M("Green"));
            Sphere("Faceted crown left", tree, new Vector3(-.59f, 2.3f, .12f), new Vector3(1.06f, .90f, 1.05f), M("Green"));
            Sphere("Faceted crown right", tree, new Vector3(.63f, 2.38f, -.07f), new Vector3(1.11f, .95f, 1.04f), M("Green"));
            Sphere("Faceted crown top", tree, new Vector3(.07f, 3.07f, .12f), new Vector3(1.02f, .84f, .99f), M("Green"));
            // Small foliage at the planter lip prevents a bare-box silhouette at eye level.
            for (var i = 0; i < 2; i++)
            {
                var angle = (i * 180f + variation * 19f) * Mathf.Deg2Rad;
                Sphere("Planter shrub", tree, new Vector3(Mathf.Cos(angle) * .40f, .67f, Mathf.Sin(angle) * .32f), new Vector3(.33f, .26f, .34f), M("Green"));
            }
        }

        static void StreetRopeBarrier(Transform parent, float z, float xMin, float xMax, bool reverse)
        {
            var rail = Group(reverse ? "South glowing bollards and red safety ropes" : "North glowing bollards and red safety ropes", parent);
            const int bays = 7;
            for (var i = 0; i <= bays; i++)
            {
                var x = Mathf.Lerp(xMin, xMax, i / (float)bays);
                var post = Group("Bollard with amber lens", rail, new Vector3(x, .16f, z));
                Cylinder("Weighted circular foot", post, new Vector3(0f, .035f, 0f), new Vector3(.32f, .035f, .32f), M("Metal"));
                Cylinder("Iron stem", post, new Vector3(0f, .40f, 0f), new Vector3(.095f, .37f, .095f), M("Metal"));
                Cylinder("Red reflective collar", post, new Vector3(0f, .65f, 0f), new Vector3(.105f, .04f, .105f), M("Red"));
                Cylinder("Lens guard base", post, new Vector3(0f, .80f, 0f), new Vector3(.13f, .025f, .13f), M("Metal"));
                Cylinder("Warm bollard lens", post, new Vector3(0f, .86f, 0f), new Vector3(.11f, .045f, .11f), M("AmberGlow"));
                Cylinder("Lens cap", post, new Vector3(0f, .915f, 0f), new Vector3(.14f, .014f, .14f), M("Metal"));
                if (i == bays) continue;
                var nextX = Mathf.Lerp(xMin, xMax, (i + 1f) / bays);
                const int ropeSegments = 12;
                for (var j = 0; j < ropeSegments; j++)
                {
                    var t0 = j / (float)ropeSegments;
                    var t1 = (j + 1f) / ropeSegments;
                    var a = new Vector3(Mathf.Lerp(x, nextX, t0), .90f - .24f * Mathf.Sin(t0 * Mathf.PI), z);
                    var b = new Vector3(Mathf.Lerp(x, nextX, t1), .90f - .24f * Mathf.Sin(t1 * Mathf.PI), z);
                    Tube("Sagging red crowd rope", rail, a, b, .026f, M("Red"));
                }
            }
        }

        static void StreetBicycle(Transform parent, Vector3 position, float yaw, string frameMaterial)
        {
            var bike = Group("Parked city bicycle · modeled frame wheels and spokes", parent, position);
            bike.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var frame = M(frameMaterial);
            var rear = new Vector3(-.61f, .47f, 0f);
            var front = new Vector3(.61f, .47f, 0f);
            var bottom = new Vector3(-.09f, .48f, 0f);
            var seat = new Vector3(-.25f, 1.02f, 0f);
            var head = new Vector3(.35f, 1.02f, 0f);
            StreetBikeWheel(bike, rear);
            StreetBikeWheel(bike, front);
            Tube("Seat stay", bike, rear, seat, .023f, frame);
            Tube("Chain stay", bike, rear, bottom, .023f, frame);
            Tube("Seat tube", bike, bottom, seat, .027f, frame);
            Tube("Top tube", bike, seat, head, .029f, frame);
            Tube("Down tube", bike, bottom, head, .029f, frame);
            Tube("Head tube", bike, head + new Vector3(.025f, -.07f, 0f), head + new Vector3(-.025f, .08f, 0f), .03f, M("Metal"));
            Tube("Front fork near", bike, front + new Vector3(0f, 0f, -.055f), head + new Vector3(0f, 0f, -.055f), .022f, frame);
            Tube("Front fork far", bike, front + new Vector3(0f, 0f, .055f), head + new Vector3(0f, 0f, .055f), .022f, frame);
            Tube("Saddle post", bike, seat, seat + new Vector3(-.012f, .13f, 0f), .021f, M("Metal"));
            Sphere("Contoured saddle", bike, seat + new Vector3(-.055f, .145f, 0f), new Vector3(.29f, .075f, .18f), M("Dark"));
            var barCenter = head + new Vector3(-.065f, .25f, 0f);
            Tube("Steering stem", bike, head, barCenter, .023f, M("Metal"));
            Tube("Handlebar center", bike, barCenter + new Vector3(0f, 0f, -.25f), barCenter + new Vector3(0f, 0f, .25f), .017f, M("Metal"));
            foreach (var side in new[] { -1f, 1f })
            {
                Tube("Swept city handlebar", bike, barCenter + new Vector3(0f, 0f, side * .25f), barCenter + new Vector3(-.15f, .025f, side * .29f), .017f, M("Metal"));
                Tube("Rubber hand grip", bike, barCenter + new Vector3(-.15f, .025f, side * .29f), barCenter + new Vector3(-.27f, .025f, side * .29f), .024f, M("Dark"));
            }
            var sprocket = Cylinder("Crank chainring", bike, bottom + new Vector3(0f, 0f, -.055f), new Vector3(.20f, .012f, .20f), M("Metal"));
            sprocket.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Tube("Chain top", bike, bottom + new Vector3(0f, .09f, -.07f), rear + new Vector3(0f, .06f, -.07f), .010f, M("Metal"));
            Tube("Chain lower", bike, bottom + new Vector3(0f, -.09f, -.07f), rear + new Vector3(0f, -.06f, -.07f), .010f, M("Metal"));
            Tube("Crank arm", bike, bottom + new Vector3(0f, 0f, -.085f), bottom + new Vector3(.10f, -.12f, -.085f), .019f, M("Metal"));
            Box("Near pedal", bike, bottom + new Vector3(.10f, -.12f, -.13f), new Vector3(.11f, .035f, .14f), M("Dark"));
            Box("Far pedal", bike, bottom + new Vector3(-.10f, .12f, .13f), new Vector3(.11f, .035f, .14f), M("Dark"));
            Tube("Rear luggage rack", bike, new Vector3(-.86f, .90f, 0f), new Vector3(-.35f, .90f, 0f), .022f, M("Metal"));
            Tube("Rack stay", bike, rear, new Vector3(-.83f, .90f, 0f), .013f, M("Metal"));
            Tube("Kickstand", bike, bottom + new Vector3(.07f, .03f, 0f), bottom + new Vector3(-.13f, -.45f, .19f), .014f, M("Metal"));
            Sphere("Front reflector lamp", bike, head + new Vector3(.12f, .10f, 0f), new Vector3(.075f, .075f, .075f), M("WarmGlow"));
        }

        static void StreetBikeWheel(Transform parent, Vector3 center)
        {
            const int segments = 24;
            const float radius = .43f;
            for (var i = 0; i < segments; i++)
            {
                var a0 = i * Mathf.PI * 2f / segments;
                var a1 = (i + 1) * Mathf.PI * 2f / segments;
                var radial0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f);
                var radial1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);
                Tube("Curved rubber tyre", parent, center + radial0 * radius, center + radial1 * radius, .036f, M("Dark"));
                Tube("Metal wheel rim", parent, center + radial0 * (radius - .03f), center + radial1 * (radius - .03f), .012f, M("Metal"));
                if ((i & 1) == 0)
                    Tube("Radial spoke", parent, center, center + radial0 * (radius - .045f), .0045f, M("Metal"));
            }
            var axle = Cylinder("Wheel axle hub", parent, center, new Vector3(.065f, .055f, .065f), M("Metal"));
            axle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        static void StreetVending(Transform parent, Vector3 position, float yaw)
        {
            var machine = Group("Lit Japanese drink vending machine", parent, position);
            machine.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box("Enamel machine case", machine, new Vector3(0f, 1.08f, 0f), new Vector3(1.26f, 2.16f, .81f), M("Red"));
            Box("Front inset surround", machine, new Vector3(0f, 1.19f, -.419f), new Vector3(1.10f, 1.82f, .044f), M("Cream"));
            Sign("Vending drink illustration and labels", machine, new Vector3(-.08f, 1.38f, -.45f), new Vector2(.93f, 1.35f), "Vending.png");
            Box("Lit drink cabinet", machine, new Vector3(-.12f, 1.53f, -.466f), new Vector3(.84f, .88f, .020f), M("Dark"));
            for (var row = 0; row < 3; row++)
            {
                Box("Bottle display shelf", machine, new Vector3(-.12f, 1.23f + row * .25f, -.53f), new Vector3(.83f, .023f, .15f), M("Metal"));
                for (var col = 0; col < 6; col++)
                {
                    var x = -.45f + col * .13f;
                    var material = M((col + row) % 3 == 0 ? "Red" : (col + row) % 3 == 1 ? "Cream" : "ClothBlue");
                    Cylinder("Displayed drink can", machine, new Vector3(x, 1.34f + row * .25f, -.53f), new Vector3(.073f, .075f, .073f), material);
                    Cylinder("Silver can lid", machine, new Vector3(x, 1.419f + row * .25f, -.53f), new Vector3(.074f, .004f, .074f), M("Metal"));
                    Box("Lit selection button", machine, new Vector3(x, 1.22f + row * .25f, -.615f), new Vector3(.048f, .020f, .015f), M("WarmGlow"));
                }
            }
            Box("Drink delivery opening", machine, new Vector3(-.10f, .53f, -.454f), new Vector3(.79f, .25f, .027f), M("Dark"));
            Box("Delivery flap", machine, new Vector3(-.10f, .49f, -.484f), new Vector3(.73f, .17f, .025f), M("Metal"));
            Box("Coin and card console", machine, new Vector3(.45f, 1.02f, -.47f), new Vector3(.17f, .49f, .050f), M("Metal"));
            Box("Coin slot", machine, new Vector3(.45f, 1.15f, -.503f), new Vector3(.090f, .017f, .013f), M("Dark"));
            Box("Cash reader", machine, new Vector3(.45f, .91f, -.503f), new Vector3(.090f, .045f, .013f), M("Dark"));
            Box("Payment status LED", machine, new Vector3(.45f, 1.05f, -.508f), new Vector3(.045f, .027f, .008f), M("CyanGlow"));
            Box("Top cabinet light strip", machine, new Vector3(-.1f, 2.02f, -.458f), new Vector3(.89f, .046f, .025f), M("WarmGlow"));
            Box("Lower ventilation panel", machine, new Vector3(0f, .16f, -.42f), new Vector3(1.05f, .13f, .033f), M("Dark"));
            for (var i = 0; i < 8; i++)
                Box("Vent fin", machine, new Vector3(-.43f + i * .12f, .16f, -.446f), new Vector3(.020f, .11f, .025f), M("Metal"));
            foreach (var x in new[] { -.45f, .45f })
                Box("Machine rubber foot", machine, new Vector3(x, .035f, .20f), new Vector3(.19f, .07f, .23f), M("Dark"));
        }

        static void StreetBins(Transform parent, Vector3 position)
        {
            var bins = Group("Separated recycling and rubbish bins", parent, position);
            for (var i = 0; i < 2; i++)
            {
                var bin = Group(i == 0 ? "Bottle recycling bin" : "Street rubbish bin", bins, new Vector3(i * .65f, 0f, 0f));
                var body = M(i == 0 ? "Green" : "ClothBlue");
                Cylinder("Ribbed waste bin barrel", bin, new Vector3(0f, .40f, 0f), new Vector3(.48f, .36f, .48f), body);
                Cylinder("Lower protective rim", bin, new Vector3(0f, .075f, 0f), new Vector3(.52f, .032f, .52f), M("Metal"));
                Cylinder("Upper bin rim", bin, new Vector3(0f, .77f, 0f), new Vector3(.52f, .032f, .52f), M("Metal"));
                Sphere("Domed refuse lid", bin, new Vector3(0f, .81f, 0f), new Vector3(.52f, .23f, .52f), body);
                Box("Dark disposal slot", bin, new Vector3(0f, .84f, -.24f), new Vector3(.22f, .075f, .026f), M("Dark"));
                Box("Sorting label", bin, new Vector3(0f, .55f, -.244f), new Vector3(.17f, .16f, .025f), M("Cream"));
                foreach (var side in new[] { -1f, 1f })
                {
                    Tube("Side handle riser", bin, new Vector3(side * .24f, .61f, -.07f), new Vector3(side * .28f, .61f, -.07f), .015f, M("Metal"));
                    Tube("Side carry handle", bin, new Vector3(side * .28f, .61f, -.07f), new Vector3(side * .28f, .61f, .07f), .015f, M("Metal"));
                }
                for (var rib = 0; rib < 8; rib++)
                {
                    var angle = rib * Mathf.PI * .25f;
                    Tube("Vertical bin rib", bin, new Vector3(Mathf.Cos(angle) * .243f, .12f, Mathf.Sin(angle) * .243f), new Vector3(Mathf.Cos(angle) * .243f, .71f, Mathf.Sin(angle) * .243f), .011f, M("Metal"));
                }
            }
        }

        static void StreetHydrant(Transform parent, Vector3 position)
        {
            var hydrant = Group("Red street fire hydrant", parent, position);
            Cylinder("Bolt plate foot", hydrant, new Vector3(0f, .055f, 0f), new Vector3(.44f, .055f, .44f), M("Metal"));
            Cylinder("Hydrant barrel", hydrant, new Vector3(0f, .40f, 0f), new Vector3(.27f, .30f, .27f), M("Red"));
            Cylinder("Collar flange", hydrant, new Vector3(0f, .65f, 0f), new Vector3(.34f, .025f, .34f), M("Metal"));
            Sphere("Domed hydrant cap", hydrant, new Vector3(0f, .70f, 0f), new Vector3(.33f, .20f, .33f), M("Red"));
            Cylinder("Top valve nut", hydrant, new Vector3(0f, .815f, 0f), new Vector3(.10f, .026f, .10f), M("Metal"));
            foreach (var side in new[] { -1f, 1f })
            {
                Tube("Hose outlet", hydrant, new Vector3(0f, .51f, 0f), new Vector3(side * .30f, .51f, 0f), .070f, M("Red"));
                var cap = Cylinder("Hose cap", hydrant, new Vector3(side * .30f, .51f, 0f), new Vector3(.18f, .024f, .18f), M("Metal"));
                cap.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                Tube("Cap safety chain", hydrant, new Vector3(side * .29f, .45f, .025f), new Vector3(side * .17f, .32f, .03f), .007f, M("Metal"));
            }
            for (var i = 0; i < 4; i++)
            {
                var angle = i * Mathf.PI * .5f + Mathf.PI * .25f;
                Cylinder("Footplate fastening bolt", hydrant, new Vector3(Mathf.Cos(angle) * .17f, .119f, Mathf.Sin(angle) * .17f), new Vector3(.041f, .012f, .041f), M("Cream"));
            }
        }

        static void StreetLanternString(Transform parent, Vector3 position, float length)
        {
            var line = Group("Red paper lanterns above the north sidewalk", parent, position);
            foreach (var x in new[] { 0f, length })
            {
                Cylinder("Lantern string pole footing", line, new Vector3(x, .055f, 0f), new Vector3(.28f, .055f, .28f), M("Metal"));
                Tube("Lantern string side pole", line, new Vector3(x, .05f, 0f), new Vector3(x, 3.92f, 0f), .047f, M("Metal"));
            }
            const int segments = 30;
            for (var i = 0; i < segments; i++)
            {
                var t0 = i / (float)segments;
                var t1 = (i + 1f) / segments;
                Tube("Catenary lantern cable", line, new Vector3(length * t0, 3.87f - .32f * Mathf.Sin(t0 * Mathf.PI), 0f), new Vector3(length * t1, 3.87f - .32f * Mathf.Sin(t1 * Mathf.PI), 0f), .012f, M("Dark"));
            }
            for (var i = 0; i < 15; i++)
            {
                var t = (i + .5f) / 15f;
                var x = length * t;
                var cableY = 3.87f - .32f * Mathf.Sin(t * Mathf.PI);
                var lantern = Group("Ribbed red paper lantern", line, new Vector3(x, cableY - .38f, 0f));
                Tube("Lantern hanger", lantern, new Vector3(0f, .25f, 0f), new Vector3(0f, .38f, 0f), .011f, M("Metal"));
                Sphere("Round red paper body", lantern, Vector3.zero, new Vector3(.44f, .53f, .44f), M("Red"));
                Cylinder("Upper lantern cap", lantern, new Vector3(0f, .24f, 0f), new Vector3(.19f, .022f, .19f), M("Dark"));
                Cylinder("Lower lantern cap", lantern, new Vector3(0f, -.24f, 0f), new Vector3(.19f, .022f, .19f), M("Dark"));
                Cylinder("Paper lantern lower glow", lantern, new Vector3(0f, -.269f, 0f), new Vector3(.115f, .004f, .115f), M("AmberGlow"));
                foreach (var y in new[] { -.12f, 0f, .12f })
                    Cylinder("Subtle horizontal paper rib", lantern, new Vector3(0f, y, 0f), new Vector3(y == 0f ? .444f : .405f, .006f, y == 0f ? .444f : .405f), M("ClothBurgundy"));
                Box("Lantern face brushstroke", lantern, new Vector3(0f, .015f, -.224f), new Vector3(.055f, .18f, .012f), M("Dark"));
                Box("Lantern face brushstroke", lantern, new Vector3(0f, .028f, -.232f), new Vector3(.14f, .034f, .012f), M("Dark"));
            }
        }

        static void StreetSpectator(Transform parent, Vector3 position, int index, string shirtKey)
        {
            var pose = index % 6;
            var person = Group("Spectator " + (index + 1).ToString("00") + " · " + new[] { "cheering", "hands on hips", "watching", "waving", "taking a photo", "arms folded" }[pose], parent, position);
            person.localRotation = Quaternion.Euler(0f, -13f + index % 5 * 6f, 0f);
            person.localScale = Vector3.one * (.94f + (index % 5) * .024f);
            var shirt = M(shirtKey);
            var trousers = M(index % 3 == 0 ? "ClothBlue" : "Dark");
            var skin = M("Skin");

            Sphere("Sculpted shirt torso", person, new Vector3(0f, 1.15f, 0f), new Vector3(.44f, .53f, .28f), shirt);
            Cylinder("Shirt waist hem", person, new Vector3(0f, .95f, 0f), new Vector3(.32f, .063f, .24f), shirt);
            Cylinder("Trouser hip shape", person, new Vector3(0f, .855f, 0f), new Vector3(.31f, .074f, .245f), trousers);
            Cylinder("Collar and neck", person, new Vector3(0f, 1.445f, 0f), new Vector3(.11f, .04f, .11f), skin);
            Sphere("Low detail face", person, new Vector3(0f, 1.58f, -.006f), new Vector3(.207f, .253f, .202f), skin);
            Sphere("Hair cap", person, new Vector3(0f, 1.664f, .006f), new Vector3(.215f, .12f, .212f), M("Dark"));
            Box("Left eye", person, new Vector3(-.046f, 1.595f, -.105f), new Vector3(.018f, .016f, .013f), M("Dark"));
            Box("Right eye", person, new Vector3(.046f, 1.595f, -.105f), new Vector3(.018f, .016f, .013f), M("Dark"));
            Box("Nose", person, new Vector3(0f, 1.554f, -.116f), new Vector3(.026f, .039f, .035f), skin);
            if (index % 4 == 3)
                Sphere("Tied back hair", person, new Vector3(.038f, 1.60f, .125f), new Vector3(.12f, .17f, .14f), M("Dark"));
            if (index % 4 == 1)
            {
                var brim = Box("Cap visor", person, new Vector3(0f, 1.68f, -.125f), new Vector3(.23f, .034f, .15f), shirt);
                brim.transform.localRotation = Quaternion.Euler(-5f, 0f, 0f);
            }
            if (index % 3 == 1)
            {
                var lapelL = Box("Left jacket lapel", person, new Vector3(-.066f, 1.26f, -.145f), new Vector3(.044f, .20f, .022f), M("Cream"));
                lapelL.transform.localRotation = Quaternion.Euler(0f, 0f, 20f);
                var lapelR = Box("Right jacket lapel", person, new Vector3(.066f, 1.26f, -.145f), new Vector3(.044f, .20f, .022f), M("Cream"));
                lapelR.transform.localRotation = Quaternion.Euler(0f, 0f, -20f);
            }
            if (index % 4 == 0)
                Box("Shirt front pocket", person, new Vector3(.10f, 1.20f, -.147f), new Vector3(.081f, .071f, .018f), M("Cream"));

            for (var side = -1; side <= 1; side += 2)
            {
                var s = (float)side;
                var knee = new Vector3(s * .13f, .48f, index % 2 == 0 && side == 1 ? -.07f : .01f);
                var ankle = new Vector3(s * .145f, .115f, index % 2 == 0 && side == 1 ? -.08f : .015f);
                Tube("Trouser upper leg", person, new Vector3(s * .105f, .88f, 0f), knee, .083f, trousers);
                Tube("Trouser lower leg", person, knee, ankle, .072f, trousers);
                Box("Dark street shoe", person, ankle + new Vector3(0f, -.068f, -.053f), new Vector3(.15f, .095f, .255f), M("Dark"));
                Box("Shoe sole", person, ankle + new Vector3(0f, -.104f, -.053f), new Vector3(.153f, .020f, .261f), M("Metal"));

                var shoulder = new Vector3(s * .21f, 1.29f, 0f);
                var elbow = new Vector3(s * .30f, 1.075f, -.012f);
                var hand = new Vector3(s * .315f, .875f, -.025f);
                if (pose == 0)
                {
                    elbow = new Vector3(s * .38f, 1.49f, .02f);
                    hand = new Vector3(s * .28f, 1.79f, -.015f);
                }
                else if (pose == 1)
                {
                    elbow = new Vector3(s * .385f, 1.085f, .0f);
                    hand = new Vector3(s * .18f, .975f, -.10f);
                }
                else if (pose == 3 && side == 1)
                {
                    elbow = new Vector3(.385f, 1.435f, .01f);
                    hand = new Vector3(.475f, 1.675f, -.02f);
                }
                else if (pose == 4)
                {
                    elbow = new Vector3(s * .30f, 1.15f, -.065f);
                    hand = new Vector3(s * .08f, 1.34f, -.285f);
                }
                else if (pose == 5)
                {
                    elbow = new Vector3(s * .28f, 1.09f, -.06f);
                    hand = new Vector3(-s * .13f, 1.13f + s * .033f, -.215f);
                }
                Tube("Shirt sleeve and upper arm", person, shoulder, elbow, .074f, shirt);
                Tube("Forearm", person, elbow, hand, .049f, skin);
                var handShape = Cylinder("Sculpted mitten hand", person, hand, new Vector3(.095f, .055f, .092f), skin);
                handShape.transform.localRotation = Quaternion.FromToRotation(Vector3.up, hand - elbow);
            }
            if (pose == 4)
            {
                Box("Camera phone", person, new Vector3(0f, 1.35f, -.303f), new Vector3(.15f, .10f, .026f), M("Dark"));
                Box("Phone camera lens", person, new Vector3(-.047f, 1.372f, -.320f), new Vector3(.022f, .022f, .008f), M("Metal"));
            }
            if (index == 2 || index == 11)
            {
                Tube("Messenger bag strap", person, new Vector3(-.14f, 1.36f, -.15f), new Vector3(.20f, .88f, -.15f), .017f, M("Dark"));
                Box("Canvas shoulder bag", person, new Vector3(.25f, .88f, .04f), new Vector3(.21f, .25f, .13f), M("Wood"));
                Box("Bag flap", person, new Vector3(.25f, .97f, -.032f), new Vector3(.22f, .12f, .025f), M("ClothOchre"));
            }
        }
    }
}
