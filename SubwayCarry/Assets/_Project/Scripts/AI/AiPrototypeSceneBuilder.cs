#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SubwayCarry.AI
{
    public static class AiPrototypeSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Prototype_AI.unity";
        private const string MaterialFolder = "Assets/_Project/Art/PrototypeMaterials";
        private const float CarWidth = 18f;
        private const float CarHeight = 8f;
        private const float WallThickness = 0.2f;
        private const float ConnectorOpening = 2.2f;

        private sealed class TrainCarBuildData
        {
            public Transform Center;
            public GridNavigation2D Navigation;
            public TrainCarPortal2D LeftPortal;
            public TrainCarPortal2D RightPortal;
            public Transform LeftArrival;
            public Transform RightArrival;
            public readonly List<TrainDoorController> Doors = new List<TrainDoorController>();
            public readonly List<PassengerSeatPrototype> Seats = new List<PassengerSeatPrototype>();
        }

        [MenuItem("SubwayCarry/Prototype/Build Passenger AI Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureFolder("Assets/_Project/Art", "PrototypeMaterials");
            DeleteUnusedPrototypeMaterials();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("AI Prototype");
            TrainCarCameraController cameraController = CreateCamera(root.transform);

            TrainCarBuildData car1 = CreateTrainCar(
                "Train Car 1",
                Vector2.zero,
                false,
                true,
                root.transform);
            TrainCarBuildData car2 = CreateTrainCar(
                "Train Car 2",
                new Vector2(22f, 0f),
                true,
                false,
                root.transform);

            car1.RightPortal.Configure(
                car2.LeftArrival,
                car2.Center,
                car2.Navigation,
                cameraController);
            car2.LeftPortal.Configure(
                car1.RightArrival,
                car1.Center,
                car1.Navigation,
                cameraController);

            var allDoors = new List<TrainDoorController>();
            allDoors.AddRange(car1.Doors);
            allDoors.AddRange(car2.Doors);
            GameObject doorCycleObject = new GameObject("Door Cycle Prototype");
            doorCycleObject.transform.SetParent(root.transform);
            doorCycleObject.AddComponent<TrainDoorCyclePrototype>().Configure(allDoors.ToArray());

            PopulatePermanentPassengers(car1, 1);
            PopulatePermanentPassengers(car2, 0);
            CreateBoardingPassenger(
                "Boarding Passenger A",
                car1,
                0,
                -7.4f,
                new Vector2(-0.8f, 1.2f),
                new Color(0.95f, 0.55f, 0.18f),
                "BoardingPassengerA");
            CreateBoardingPassenger(
                "Boarding Passenger B",
                car1,
                2,
                -3.8f,
                new Vector2(0.8f, 1.2f),
                new Color(0.75f, 0.55f, 0.95f),
                "BoardingPassengerB");

            cameraController.FocusOn(car1.Center);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = car1.Center.gameObject;

            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            Debug.Log("[AI Prototype] Passenger seat scene created: " + ScenePath);
        }

        [MenuItem("SubwayCarry/Prototype/Validate A* Pathfinding")]
        public static void ValidatePathfinder()
        {
            var blocked = new HashSet<Vector2Int>
            {
                new Vector2Int(3, 1),
                new Vector2Int(3, 2),
                new Vector2Int(3, 3)
            };

            List<Vector2Int> path = AStarGrid.FindPath(
                new Vector2Int(1, 2),
                new Vector2Int(6, 2),
                8,
                5,
                cell => !blocked.Contains(cell));

            if (path.Count == 0)
            {
                throw new InvalidOperationException("A* validation failed: no path returned.");
            }

            foreach (Vector2Int cell in path)
            {
                if (blocked.Contains(cell))
                {
                    throw new InvalidOperationException("A* validation failed: path crosses an obstacle.");
                }
            }

            Debug.Log("[AI Prototype] A* validation passed. Path nodes: " + path.Count);
        }

        private static TrainCarBuildData CreateTrainCar(
            string name,
            Vector2 center,
            bool openLeft,
            bool openRight,
            Transform parent)
        {
            GameObject carRoot = new GameObject(name);
            carRoot.transform.SetParent(parent);
            carRoot.transform.position = center;

            var data = new TrainCarBuildData
            {
                Center = carRoot.transform
            };

            CreateBlock(
                "Floor",
                center,
                new Vector2(CarWidth, CarHeight),
                new Color(0.08f, 0.12f, 0.18f),
                carRoot.transform,
                false,
                "Floor",
                1f);

            Color wallColor = new Color(0.24f, 0.31f, 0.4f);
            CreateBlock("Top Wall", center + new Vector2(0f, 4.1f),
                new Vector2(18.2f, WallThickness), wallColor, carRoot.transform, true, "Wall");
            CreateBlock("Bottom Wall", center + new Vector2(0f, -4.1f),
                new Vector2(18.2f, WallThickness), wallColor, carRoot.transform, true, "Wall");

            CreateEndWall(carRoot.transform, center, -1f, openLeft, wallColor);
            CreateEndWall(carRoot.transform, center, 1f, openRight, wallColor);
            CreateWallMarkings(carRoot.transform, center, openLeft, openRight);
            CreateDoorsAndSeats(carRoot.transform, center, data.Doors, data.Seats);

            GameObject navigationObject = new GameObject("Grid Navigation");
            navigationObject.transform.SetParent(carRoot.transform);
            navigationObject.transform.position = center;
            data.Navigation = navigationObject.AddComponent<GridNavigation2D>();

            if (openLeft)
            {
                CreateConnector(carRoot.transform, center, -1f);
                data.LeftPortal = CreatePortal(
                    "Left Connector Portal",
                    center + new Vector2(-8.35f, 0f),
                    carRoot.transform);
                data.LeftArrival = CreatePoint(
                    "Left Connector Arrival",
                    center + new Vector2(-7.45f, 0f),
                    carRoot.transform);
            }

            if (openRight)
            {
                CreateConnector(carRoot.transform, center, 1f);
                data.RightPortal = CreatePortal(
                    "Right Connector Portal",
                    center + new Vector2(8.35f, 0f),
                    carRoot.transform);
                data.RightArrival = CreatePoint(
                    "Right Connector Arrival",
                    center + new Vector2(7.45f, 0f),
                    carRoot.transform);
            }

            return data;
        }

        private static void CreateEndWall(
            Transform parent,
            Vector2 center,
            float direction,
            bool open,
            Color wallColor)
        {
            float x = center.x + direction * 9.1f;
            if (!open)
            {
                CreateBlock("Closed End Wall", new Vector2(x, center.y),
                    new Vector2(WallThickness, 8.2f), wallColor, parent, true, "Wall");
                return;
            }

            float segmentHeight = (CarHeight - ConnectorOpening) * 0.5f;
            float segmentOffset = ConnectorOpening * 0.5f + segmentHeight * 0.5f;
            CreateBlock("Connector Wall Upper", new Vector2(x, center.y + segmentOffset),
                new Vector2(WallThickness, segmentHeight), wallColor, parent, true, "Wall");
            CreateBlock("Connector Wall Lower", new Vector2(x, center.y - segmentOffset),
                new Vector2(WallThickness, segmentHeight), wallColor, parent, true, "Wall");
        }

        private static void CreateWallMarkings(
            Transform parent,
            Vector2 center,
            bool openLeft,
            bool openRight)
        {
            Color markColor = new Color(0.65f, 0.72f, 0.78f);
            for (int i = 0; i < 9; i++)
            {
                float x = center.x - 8f + i * 2f;
                float angle = i % 2 == 0 ? 18f : -18f;
                CreateBlock("Top Wall Mark", new Vector2(x, center.y + 4.08f),
                    new Vector2(0.5f, 0.07f), markColor, parent, false, "WallMark", -0.1f, angle);
                CreateBlock("Bottom Wall Mark", new Vector2(x, center.y - 4.08f),
                    new Vector2(0.5f, 0.07f), markColor, parent, false, "WallMark", -0.1f, angle);
            }

            if (!openLeft)
            {
                CreateEndWallMarkings(parent, center, -1f, markColor);
            }

            if (!openRight)
            {
                CreateEndWallMarkings(parent, center, 1f, markColor);
            }
        }

        private static void CreateEndWallMarkings(
            Transform parent,
            Vector2 center,
            float direction,
            Color color)
        {
            for (int i = 0; i < 6; i++)
            {
                float y = center.y - 3f + i * 1.2f;
                CreateBlock("End Wall Mark", new Vector2(center.x + direction * 9.08f, y),
                    new Vector2(0.07f, 0.45f), color, parent, false, "WallMark", -0.1f, 18f);
            }
        }

        private static void CreateDoorsAndSeats(
            Transform parent,
            Vector2 center,
            List<TrainDoorController> doors,
            List<PassengerSeatPrototype> seats)
        {
            float[] doorX = { -7.4f, -3.8f, -0.2f, 3.4f };
            float[] seatX = { -5.6f, -2f, 1.6f };
            Color doorColor = new Color(0.16f, 0.58f, 0.58f);
            Color seatColor = new Color(0.1f, 0.45f, 0.62f);
            Color priorityColor = new Color(0.72f, 0.31f, 0.48f);

            foreach (float localX in doorX)
            {
                doors.Add(CreateDoor(parent, center + new Vector2(localX, 3.92f), doorColor));
                doors.Add(CreateDoor(parent, center + new Vector2(localX, -3.92f), doorColor));
            }

            foreach (float localX in seatX)
            {
                seats.Add(CreatePassengerSeat(parent, center + new Vector2(localX, 3.3f), seatColor));
                seats.Add(CreatePassengerSeat(parent, center + new Vector2(localX, -3.3f), seatColor));
            }

            CreateBlock("PrioritySeat", center + new Vector2(5.6f, 3.3f), new Vector2(1.45f, 0.85f),
                priorityColor, parent, true, "PrioritySeat");
            CreateBlock("PrioritySeat", center + new Vector2(5.6f, -3.3f), new Vector2(1.45f, 0.85f),
                priorityColor, parent, true, "PrioritySeat");
        }

        private static TrainDoorController CreateDoor(Transform parent, Vector2 position, Color color)
        {
            GameObject doorRoot = new GameObject("Door");
            doorRoot.transform.SetParent(parent);
            doorRoot.transform.position = position;

            CreateBlock("Door Opening", position, new Vector2(1.2f, 0.34f),
                new Color(0.025f, 0.035f, 0.05f), doorRoot.transform, false, "DoorOpening", -0.1f);
            GameObject leftPanel = CreateBlock("Left Panel", position + Vector2.left * 0.29f,
                new Vector2(0.56f, 0.28f), color, doorRoot.transform, false, "Door", -0.2f);
            GameObject rightPanel = CreateBlock("Right Panel", position + Vector2.right * 0.29f,
                new Vector2(0.56f, 0.28f), color, doorRoot.transform, false, "Door", -0.2f);

            TrainDoorController controller = doorRoot.AddComponent<TrainDoorController>();
            controller.Configure(leftPanel.transform, rightPanel.transform);
            return controller;
        }

        private static void CreateBoardingPassenger(
            string name,
            TrainCarBuildData car,
            int doorIndex,
            float doorLocalX,
            Vector2 standingLocalPosition,
            Color color,
            string materialKey)
        {
            if (doorIndex < 0 || doorIndex >= car.Doors.Count)
            {
                return;
            }

            Vector2 center = (Vector2)car.Center.position;
            Transform outsidePoint = CreatePoint(
                "Outside Waiting Point",
                center + new Vector2(doorLocalX, 4.85f),
                car.Center);
            Transform insidePoint = CreatePoint(
                "Inside Boarding Point",
                center + new Vector2(doorLocalX, 2.15f),
                car.Center);
            Transform standingPoint = CreatePoint(
                "Standing Point",
                center + standingLocalPosition,
                car.Center);
            GameObject passengerObject = CreateBlock(
                name,
                (Vector2)outsidePoint.position,
                new Vector2(0.55f, 0.72f),
                color,
                car.Center,
                false,
                materialKey,
                -0.3f);
            passengerObject.AddComponent<PassengerBoardingPrototype>().Configure(
                car.Doors[doorIndex],
                outsidePoint,
                insidePoint,
                standingPoint,
                car.Seats.ToArray());
        }

        private static PassengerSeatPrototype CreatePassengerSeat(
            Transform parent,
            Vector2 position,
            Color color)
        {
            GameObject seatObject = CreateBlock("Seat", position, new Vector2(1.45f, 0.85f),
                color, parent, true, "Seat");
            Transform leftPoint = CreatePoint(
                "Sitting Point Left",
                position + Vector2.left * 0.34f,
                seatObject.transform);
            Transform rightPoint = CreatePoint(
                "Sitting Point Right",
                position + Vector2.right * 0.34f,
                seatObject.transform);

            PassengerSeatPrototype seat = seatObject.AddComponent<PassengerSeatPrototype>();
            seat.Configure(new[] { leftPoint, rightPoint });
            return seat;
        }

        private static void PopulatePermanentPassengers(TrainCarBuildData car, int freeSeatCount)
        {
            int remainingFreeSeats = freeSeatCount;
            int passengerNumber = 1;

            foreach (PassengerSeatPrototype seat in car.Seats)
            {
                for (int slot = 0; slot < seat.Capacity; slot++)
                {
                    if (remainingFreeSeats > 0)
                    {
                        remainingFreeSeats--;
                        continue;
                    }

                    Transform sittingPoint = seat.GetSittingPoint(slot);
                    GameObject passenger = CreateBlock(
                        "Seated Passenger " + passengerNumber,
                        (Vector2)sittingPoint.position,
                        new Vector2(0.32f, 0.46f),
                        new Color(0.72f, 0.78f, 0.84f),
                        car.Center,
                        false,
                        "SeatedPassenger",
                        -0.3f);
                    seat.SetInitialOccupant(slot, passenger);
                    passengerNumber++;
                }
            }
        }

        private static void CreateConnector(Transform parent, Vector2 center, float direction)
        {
            Vector2 connectorCenter = center + new Vector2(direction * 8.65f, 0f);
            CreateBlock("Connector Floor", connectorCenter, new Vector2(0.9f, 2.1f),
                new Color(0.12f, 0.16f, 0.2f), parent, false, "Connector", 0.5f);
            CreateBlock("Connector Threshold", center + new Vector2(direction * 8.18f, 0f),
                new Vector2(0.08f, 2f), new Color(0.84f, 0.72f, 0.2f),
                parent, false, "ConnectorThreshold", -0.1f);
        }

        private static TrainCarPortal2D CreatePortal(string name, Vector2 position, Transform parent)
        {
            GameObject portalObject = new GameObject(name);
            portalObject.transform.SetParent(parent);
            portalObject.transform.position = position;

            BoxCollider2D trigger = portalObject.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(0.5f, 1.8f);
            return portalObject.AddComponent<TrainCarPortal2D>();
        }

        private static Transform CreatePoint(string name, Vector2 position, Transform parent)
        {
            GameObject point = new GameObject(name);
            point.transform.SetParent(parent);
            point.transform.position = position;
            return point.transform;
        }

        private static GameObject CreateBlock(
            string name,
            Vector2 position,
            Vector2 size,
            Color color,
            Transform parent,
            bool obstacle,
            string materialKey,
            float z = 0f,
            float rotationZ = 0f)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Quad);
            block.name = name;
            block.transform.SetParent(parent);
            block.transform.position = new Vector3(position.x, position.y, z);
            block.transform.rotation = Quaternion.Euler(0f, 0f, rotationZ);
            block.transform.localScale = new Vector3(size.x, size.y, 1f);

            MeshCollider meshCollider = block.GetComponent<MeshCollider>();
            if (meshCollider != null)
            {
                UnityEngine.Object.DestroyImmediate(meshCollider);
            }

            block.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial(materialKey, color);

            if (obstacle)
            {
                block.AddComponent<BoxCollider2D>();
                block.AddComponent<NavigationObstacle>();
            }

            return block;
        }

        private static TrainCarCameraController CreateCamera(Transform parent)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(parent);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.tag = "MainCamera";

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.15f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f);
            return cameraObject.AddComponent<TrainCarCameraController>();
        }

        private static Material GetOrCreateMaterial(string sourceName, Color color)
        {
            string safeName = sourceName.Replace(" ", string.Empty).Replace("/", string.Empty);
            string path = MaterialFolder + "/" + safeName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Color");
                }

                material = new Material(shader);
                material.color = color;
                AssetDatabase.CreateAsset(material, path);
                return material;
            }

            Color currentColor = material.HasProperty("_BaseColor")
                ? material.GetColor("_BaseColor")
                : material.color;
            if (!ColorsApproximately(currentColor, color))
            {
                material.color = color;
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private static bool ColorsApproximately(Color left, Color right)
        {
            return Mathf.Abs(left.r - right.r) < 0.0001f &&
                   Mathf.Abs(left.g - right.g) < 0.0001f &&
                   Mathf.Abs(left.b - right.b) < 0.0001f &&
                   Mathf.Abs(left.a - right.a) < 0.0001f;
        }

        private static void DeleteUnusedPrototypeMaterials()
        {
            string[] unusedNames =
            {
                "CenterObstacle",
                "GoalA",
                "GoalB",
                "SeatsBottomLeft",
                "SeatsBottomRight",
                "SeatsTopLeft",
                "SeatsTopRight",
                "Start",
                "WallBottom",
                "WallLeft",
                "WallRight",
                "WallTop",
                "BoardingPassenger",
                "Marker",
                "PassengerAgent",
                "Path"
            };

            foreach (string unusedName in unusedNames)
            {
                AssetDatabase.DeleteAsset(MaterialFolder + "/" + unusedName + ".mat");
            }
        }

        private static void EnsureFolder(string parent, string child)
        {
            string fullPath = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
#endif
