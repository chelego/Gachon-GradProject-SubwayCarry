using UnityEngine;

namespace SubwayCarry.Gameplay3D.Editor
{
    internal static partial class MoranStationBuilder
    {
        static void BuildLifts(Transform root)
        {
            var pair = Group(root, "엘리베이터 · Lifts");
            CreateMoranLift(pair, "서측 엘리베이터 · West", new Vector3(-10.8f, 0, 60), 90);
            CreateMoranLift(pair, "동측 엘리베이터 · East", new Vector3(10.4f, 0, 33), -90);
        }

        static void CreateMoranLift(Transform parent, string title, Vector3 position, float yaw)
        {
            var shaft = Group(parent, title, position);
            shaft.localRotation = Quaternion.Euler(0, yaw, 0);
            KitPart(shaft, "LiftShaft", Vector3.zero);
            Box(shaft, "Shaft head enclosure", new Vector3(0, 6.99f, 0), new Vector3(2.25f, 1.40f, 2.24f), Finish("Parchment"));
            Box(shaft, "Shaft head cornice", new Vector3(0, 6.31f, 0), new Vector3(2.27f, .065f, 2.26f), Finish("Teal"), false);

            // Shaft walls are continuous except at the two landing entrances.
            foreach (int side in new[] { -1, 1 })
            {
                LiftBarrier(shaft, "Shaft side", new Vector3(side * 1.067f, 3.15f, 0), new Vector3(.13f, 6.3f, 2.24f));
                LiftBarrier(shaft, "Landing door pocket", new Vector3(side * .915f, 3.15f, 1.05f), new Vector3(.43f, 6.3f, .13f));
            }
            LiftBarrier(shaft, "Shaft back", new Vector3(0, 3.15f, -1.065f), new Vector3(2.24f, 6.3f, .13f));
            var landings = new Transform[2];
            var barriers = new Collider[2];
            var leaves = new Transform[12];
            for (int floor = 0; floor < 2; floor++)
            {
                float h = 3.6f * floor;
                string level = floor == 0 ? "B2 승강장" : "B1 대합실";
                var front = Group(shaft, level + " · Entrance", new Vector3(0, h, 0));
                landings[floor] = Group(shaft, level + " · Call approach", new Vector3(0, h, 2f));
                var frame = KitPart(front, "ElevatorFront", new Vector3(0, 0, 1.08f));
                frame.name = "Landing portal";
                CreateLiftLeaves(front, 1.06f, leaves, floor * 4);
                barriers[floor] = LiftBarrier(front, "Closed landing gate", new Vector3(0, 1.1f, 1.05f), new Vector3(1.4f, 2.2f, .07f));

                // At B1 this apron closes the clearance between the shaft and the rectangular slab cut.
                float tread = floor == 0 ? .012f : 0;
                var granite = Mat("Circulation_Granite");
                foreach (int side in new[] { -1, 1 })
                    Box(front, "Apron side return", new Vector3(1.25f * side, tread - .09f, 0), new Vector3(.5f, .18f, 2.8f), granite);
                Box(front, "Apron rear return", new Vector3(0, tread - .09f, -1.19f), new Vector3(2, .18f, .42f), granite);
                Box(front, "Door sill", new Vector3(0, tread - .09f, 1.18f), new Vector3(2, .18f, .44f), granite);
                for (int rib = 0; rib < 3; rib++)
                    Box(front, "Sill guide", new Vector3(0, tread + .002f, 1f + rib * .047f),
                        new Vector3(1.37f, .004f, .012f), Mat("Circulation_Steel"), false);

                var panel = Group(front, "Lift identification", new Vector3(0, 2.66f, 1.14f));
                panel.localRotation = Quaternion.Euler(0, 180, 0);
                Box(panel, "Enamel panel", Vector3.zero, new Vector3(2.02f, .37f, .06f), Finish("Ink"), false);
                Box(panel, "Enamel reveal", new Vector3(0, -.16f, -.033f), new Vector3(1.95f, .018f, .009f), Finish("Seafoam"), false);
                Lettering(panel, level + "  |  엘리베이터", new Vector3(0, .018f, -.041f), new Vector2(1.92f, .23f),
                    1.45f, Color.white);
            }

            var car = Group(shaft, "Moving cabin");
            KitPart(car, "LiftCar", Vector3.zero);
            Group(car, "탑승 위치 · Cabin standing point");
            CreateLiftLeaves(car, .935f, leaves, 8);
            var carGate = LiftBarrier(car, "Closed car gate", new Vector3(0, 1.1f, .93f), new Vector3(1.90f, 2.2f, .05f));
            foreach (int side in new[] { -1, 1 })
                LiftBarrier(car, "Car side", new Vector3(side * .967f, 1.16f, 0), new Vector3(.055f, 2.32f, 1.94f));
            LiftBarrier(car, "Car back", new Vector3(0, 1.16f, -.967f), new Vector3(1.94f, 2.32f, .055f));
            LiftBarrier(car, "Car floor", new Vector3(0, -.06f, 0), new Vector3(1.94f, .12f, 1.94f));
            LiftBarrier(car, "Car ceiling", new Vector3(0, 2.355f, 0), new Vector3(1.94f, .06f, 1.94f));
            var interior = Group(car, "Cabin directory", new Vector3(0, 1.55f, -.925f));
            interior.localRotation = Quaternion.Euler(0, 180, 0);
            Box(interior, "Etched directory plate", Vector3.zero, new Vector3(.98f, .39f, .025f), Finish("Ink"), false);
            Lettering(interior, "B1  대합실\nB2  승강장", new Vector3(0, 0, -.016f), new Vector2(.9f, .32f), 1.12f, Color.white);
            shaft.gameObject.AddComponent<MoranStationLift>().Configure(car, landings, leaves, barriers, carGate);
        }

        static void CreateLiftLeaves(Transform parent, float depth, Transform[] leaves, int start)
        {
            for (int i = 0; i < 4; i++)
            {
                bool inner = i == 1 || i == 2;
                var leaf = KitPart(parent, "LiftDoorLeaf", new Vector3(0, 0, depth - (inner ? 0 : .045f))).transform;
                leaf.localScale = new Vector3(i < 2 ? -.5f : .5f, 1, 1);
                leaf.name = "Telescopic leaf " + (i + 1);
                leaves[start + i] = leaf;
            }
        }

        static BoxCollider LiftBarrier(Transform parent, string label, Vector3 position, Vector3 size)
        {
            var solid = Group(parent, label, position).gameObject.AddComponent<BoxCollider>();
            solid.size = size;
            return solid;
        }
    }
}
