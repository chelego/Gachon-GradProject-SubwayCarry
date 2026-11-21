using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace SubwayCarry.Gameplay3D.Editor
{
    internal static partial class MoranStationBuilder
    {
        static readonly Color SignCream = new Color32(251, 246, 225, 255);
        static readonly Color SignInk = new Color32(23, 48, 46, 255);

        static void BuildInteriors(Transform root)
        {
            var west = Group(root, "B2 west · Yatap");
            var east = Group(root, "B2 east · Gachon");
            PlatformInteriors(west, -1, 13.2f, new[] {20f, 53f, 80f}, 80f);
            PlatformInteriors(east, 1, 12.4f, new[] {21f, 64f, 83f}, 21f);

            var hall = Group(root, "B1 · concourse fittings");
            SuspendedSign(hall, new Vector3(0, 6.29f, 27.5f), 7.7f, 5.2f,
                "모란  Moran", "B2   타는 곳   ↓", "나가는 곳  ↑", "Exit");
            SuspendedSign(hall, new Vector3(-2.25f, 6.29f, 33.4f), 7.7f, 5.4f,
                "←   야탑 방면     |     가천대 방면   ↑", "B2   승강장",
                "야탑 방면   →", "B2   승강장");
            SuspendedSign(hall, new Vector3(3.5f, 6.29f, 60.5f), 7.7f, 3.5f,
                "가천대 방면   →", "B2   승강장", "←   가천대 방면", "B2   승강장");

            // Keep the counter and machines on the public side of the fare line.
            var tickets = Group(hall, "Ticket machines", new Vector3(-12.57f, 3.6f, 23.4f));
            tickets.localRotation = Quaternion.Euler(0, 90, 0);
            foreach (float x in new[] {-1f, 0f, 1f}) {
                var machine = InteriorModel(tickets, "TicketMachine", new Vector3(x, 0, 0));
                PhysicalFootprint(machine, new Vector3(0, .8f, .03f), new Vector3(.74f, 1.6f, .66f));
            }
            WallIdentity(hall, new Vector3(12.36f, 5.5f, 22.8f), 90);
            WallIdentity(hall, new Vector3(-13.16f, 5.5f, 66.3f), -90);

            HallLights(Group(hall, "Concourse ceiling lights"));
        }

        static void PlatformInteriors(Transform parent, int side, float edge, float[] bays, float reliefBay)
        {
            float wall = side * (edge - .04f);
            float faceYaw = side * 90;
            var art = Group(parent, "Wall panels and seating bays");
            foreach (float z in bays) {
                bool isRelief = z == reliefBay;
                if (isRelief) PeonyMural(art, new Vector3(wall, 1.73f, z), faceYaw);
                else WallIdentity(art, new Vector3(wall, 1.84f, z), faceYaw);
                foreach (float offset in new[] {-1.27f, 1.27f})
                    SeatingBay(art, new Vector3(side * (edge - 1.06f), 0, z + offset), -faceYaw);
            }
            var services = Group(parent, "Safety and service fittings");
            foreach (float z in new[] {10.3f, 70.5f, 91.6f}) {
                var cabinet = InteriorModel(services, "FireCabinet", new Vector3(side * (edge - .34f), 0, z), -faceYaw);
                PhysicalFootprint(cabinet, Vector3.up * .76f, new Vector3(.71f, 1.52f, .37f));
            }
            foreach (float z in new[] {14f, 27f, 68f, 89f})
                InteriorModel(services, "VentPanel", new Vector3(side * (edge - .029f), .42f, z), -faceYaw);

            var bin = InteriorModel(services, "WasteBin", new Vector3(side * (edge - .64f), 0, reliefBay + 5.4f), -faceYaw);
            PhysicalFootprint(bin, Vector3.up * .43f, new Vector3(1f, .86f, .45f));
            PlatformLights(Group(parent, "Boarding lane lights"), side);

            foreach (float z in new[] {15f, 74f}) {
                float towardStairs = z < 46.5f ? 0 : 180;
                var board = Group(parent, "Platform wayfinding", new Vector3(side * 7.1f, 2.69f, z));
                board.localRotation = Quaternion.Euler(0, towardStairs, 0);
                SuspendedSign(board, Vector3.zero, .63f, 3.5f, "↑  나가는 곳", "B1   Exit",
                    side < 0 ? "야탑 방면" : "가천대 방면", "수인분당선", .55f);
            }
        }

        static Transform InteriorModel(Transform parent, string model, Vector3 local, float yaw = 0f)
            => KitPart(parent, model, local, yaw).transform;

        static void PhysicalFootprint(Transform part, Vector3 center, Vector3 size)
        {
            var collider = part.gameObject.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
        }

        static void SeatingBay(Transform parent, Vector3 feet, float yaw)
        {
            var bench = InteriorModel(parent, "StationBench", feet, yaw);
            PhysicalFootprint(bench, Vector3.up * .25f, new Vector3(1.74f, .50f, .49f));
            for (int i = -1; i <= 1; ++i) {
                var slot = Group(bench, "Seat " + (i + 2), new Vector3(i * .56f, 0, 0));
                var trigger = slot.gameObject.AddComponent<BoxCollider>();
                trigger.size = new Vector3(.48f, .48f, .48f);
                trigger.center = new Vector3(0, .65f, .13f);
                trigger.isTrigger = true;
                var seat = slot.gameObject.AddComponent<Metro3DFacility>();
                seat.kind = Metro3DFacilityKind.Seat;
                seat.anchor = Group(slot, "Feet anchor", Vector3.forward * .13f);
                seat.approach = Group(slot, "Approach anchor", Vector3.forward * .75f);
                seat.caption = "앉기";
            }
        }

        static void HallLights(Transform fittings)
        {
            for (int z = 21; z <= 71; z += 5)
                foreach (float x in new[] { -6.7f, 0f, 6.7f })
                {
                    // Recess the centre row between the hanging sign supports.
                    if (x == 0 && (z == 26 || z == 31 || z == 61)) continue;
                    bool castShadow = x == 0 && (z == 21 || z == 36 || z == 56);
                    InteriorLight(fittings, new Vector3(x, 7.70f, z), 3.8f, 90, 8.2f, castShadow);
                }
            InteriorLight(fittings, new Vector3(0, 7.16f, 14.3f), 2.5f, 90, 6, false);
        }

        static void PlatformLights(Transform fittings, int side)
        {
            for (int z = 6; z <= 96; z += 4)
            {
                // Across the stair wells the fixtures stay above the boarding lane.
                bool stair = side < 0 ? z >= 34 && z <= 46 : z >= 46 && z <= 58;
                float x = stair ? 5.85f : 7.25f;
                bool castShadow = side < 0 ? z == 46 || z == 78 : z == 22 || z == 46;
                InteriorLight(fittings, new Vector3(side * x, 3.32f, z),
                    stair ? 2.8f : 3.85f, 0, 7.8f, castShadow);
            }
        }

        static void InteriorLight(Transform parent, Vector3 ceiling, float length, float yaw, float range, bool castShadow)
        {
            // The kit's opal face is at 2.885 m and its mounting pad ends at 3.004 m.
            var fixture = InteriorModel(parent, "FluorescentLight", ceiling - Vector3.up * 3f, yaw);
            fixture.localScale = new Vector3(length / 1.35f, 1, 1);
            var source = Group(parent, "Downlight", ceiling - Vector3.up * .38f);
            source.localRotation = Quaternion.Euler(90, 0, 0);
            var light = source.gameObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(.94f, .965f, 1f);
            light.range = range;
            light.spotAngle = 138f;
            light.innerSpotAngle = 100f;
            light.intensity = 8.4f;
            if (castShadow)
            {
                light.shadows = LightShadows.Soft;
                light.shadowStrength = .78f;
                light.shadowBias = .025f;
                light.shadowNormalBias = .10f;
                source.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
            }
        }

        static void WallIdentity(Transform parent, Vector3 center, float yaw)
        {
            var sign = Group(parent, "Station name · K225", center);
            sign.localRotation = Quaternion.Euler(0, yaw, 0);
            Box(sign, "Framed station name", Vector3.zero, new Vector3(4.54f, .91f, .075f), Finish("Brass"), false);
            Box(sign, "Enamel face", new Vector3(0, 0, -.043f), new Vector3(4.48f, .85f, .026f), Finish("Parchment"), false);
            Box(sign, "Line number field", new Vector3(-1.74f, 0, -.065f), new Vector3(.90f, .84f, .019f), Finish("Teal"), false);
            Box(sign, "Bundang line bar", new Vector3(-1.74f, -.34f, -.081f), new Vector3(.90f, .075f, .008f), Mat("Yellow"), false);
            Lettering(sign, "K225", new Vector3(-1.74f, .02f, -.085f), new Vector2(.78f, .30f), 2.6f, SignCream);
            Lettering(sign, "모란", new Vector3(.43f, .12f, -.064f), new Vector2(2.97f, .50f), 4.6f, SignInk);
            Lettering(sign, "Moran", new Vector3(.43f, -.23f, -.064f), new Vector2(2.97f, .23f), 2.1f, SignInk);
        }

        static void SuspendedSign(Transform parent, Vector3 center, float ceilingY, float width,
            string frontTitle, string frontDetail, string backTitle, string backDetail, float height = .77f)
        {
            var sign = Group(parent, "Double-sided wayfinding", center);
            float drop = ceilingY - center.y - height * .5f;
            foreach (float x in new[] {-width * .36f, width * .36f})
                Box(sign, "Suspension", new Vector3(x, (height + drop) * .5f, 0),
                    new Vector3(.032f, drop, .032f), Mat("Metal"), false);
            Box(sign, "Enamel housing", Vector3.zero, new Vector3(width, height, .13f), Finish("Ink"), false);
            Box(sign, "Line colour", new Vector3(0, -height * .443f, 0),
                new Vector3(width, height * .095f, .137f), Mat("Yellow"), false);
            BoardFace(sign, frontTitle, frontDetail, width, height);
            var back = Group(sign, "Reverse");
            back.localRotation = Quaternion.Euler(0, 180, 0);
            BoardFace(back, backTitle, backDetail, width, height);
        }

        static void BoardFace(Transform face, string title, string detail, float width, float height)
        {
            Lettering(face, title, new Vector3(0, height * .135f, -.072f),
                new Vector2(width - .17f, height * .54f), height * 4.8f, SignCream);
            Lettering(face, detail, new Vector3(0, -height * .212f, -.072f),
                new Vector2(width - .17f, height * .24f), height * 2.05f, new Color32(255, 215, 118, 255));
        }

        static TextMeshPro Lettering(Transform parent, string label, Vector3 local, Vector2 panel,
            float fontSize, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            // Start with a RectTransform so TMP never replaces the transform after placement.
            var go = new GameObject(label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = Resources.Load<TMP_FontAsset>("Metro3DSignFont");
            tmp.fontSharedMaterial = tmp.font.material;
            tmp.text = label;
            tmp.alignment = align;
            tmp.color = color;
            tmp.richText = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = fontSize * .5f;
            tmp.fontSizeMax = fontSize;
            tmp.fontSize = fontSize;
            tmp.rectTransform.sizeDelta = panel;
            tmp.renderer.shadowCastingMode = ShadowCastingMode.Off;
            tmp.renderer.receiveShadows = false;
            tmp.ForceMeshUpdate(true);
            return tmp;
        }

        static void PeonyMural(Transform parent, Vector3 center, float yaw)
        {
            var panel = Group(parent, "Moran · peony ceramic relief", center);
            panel.localRotation = Quaternion.Euler(0, yaw, 0);
            Box(panel, "Bronze edge", Vector3.zero, new Vector3(8.4f, 2.37f, .095f), Finish("Brass"), false);
            Box(panel, "Glazed jade panel", new Vector3(0, 0, -.055f),
                new Vector3(8.31f, 2.28f, .045f), Finish("Teal"), false);
            for (int i = -10; i <= 10; i++)
                Box(panel, "Vertical glaze rib", new Vector3(i * .385f, 0, -.080f),
                    new Vector3(.011f, 2.23f, .008f), Finish("Seafoam"), false);
            Box(panel, "Name inset", new Vector3(-2.68f, 0, -.094f),
                new Vector3(2.82f, 1.73f, .035f), Finish("Teal"), false);
            Lettering(panel, "모란", new Vector3(-2.7f, .22f, -.126f), new Vector2(2.36f, .72f), 7.2f, SignCream);
            Lettering(panel, "K225  ·  Moran", new Vector3(-2.7f, -.41f, -.126f),
                new Vector2(2.42f, .29f), 2.5f, new Color32(244, 201, 140, 255));

            CeramicFlower(panel, new Vector2(-.38f, .04f), .79f, -9);
            CeramicFlower(panel, new Vector2(1.28f, .36f), .63f, 11);
            CeramicFlower(panel, new Vector2(3.06f, -.01f), .88f, 17);
            foreach (float x in new[] {.12f, 1.77f, 3.5f}) {
                ReliefOval(panel, "Leaf", new Vector3(x, -.63f, -.095f), new Vector2(.22f, .70f), -48, Finish("Seafoam"));
                ReliefOval(panel, "Leaf", new Vector3(x - .40f, -.75f, -.10f), new Vector2(.17f, .52f), 48, Finish("Seafoam"));
            }
        }

        static void CeramicFlower(Transform panel, Vector2 center, float radius, float phase)
        {
            for (int layer = 0; layer < 2; ++layer) {
                int petals = layer == 0 ? 8 : 6;
                float scale = layer == 0 ? radius : radius * .60f;
                var glaze = Finish(layer == 0 ? "Terra" : "Parchment");
                for (int i = 0; i < petals; ++i) {
                    float angle = phase + i * 360f / petals + layer * 15f;
                    float a = angle * Mathf.Deg2Rad;
                    Vector2 p = center + new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * (.51f * scale);
                    ReliefOval(panel, "Glazed petal", new Vector3(p.x, p.y, -.11f - layer * .021f),
                        new Vector2(.50f, .83f) * scale, angle, glaze);
                }
            }
            ReliefOval(panel, "Peony centre", new Vector3(center.x, center.y, -.17f),
                Vector2.one * (.28f * radius), 0, Finish("Brass"));
        }

        static void ReliefOval(Transform parent, string name, Vector3 local, Vector2 size, float tilt, Material material)
        {
            var oval = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(oval.GetComponent<Collider>());
            oval.name = name;
            oval.transform.SetParent(parent, false);
            oval.transform.localPosition = local;
            oval.transform.localRotation = Quaternion.Euler(0, 0, tilt);
            oval.transform.localScale = new Vector3(size.x, size.y, .043f);
            oval.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
