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
            public readonly List<PassengerDoorway> Doorways = new List<PassengerDoorway>();
            public readonly List<PassengerSeatPrototype> Seats = new List<PassengerSeatPrototype>();
            public readonly List<PassengerActivityPoint> ActivityPoints = new List<PassengerActivityPoint>();
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
            TrainDoorCyclePrototype doorCycle = doorCycleObject.AddComponent<TrainDoorCyclePrototype>();
            doorCycle.Configure(allDoors.ToArray());

            for (int i = 0; i < 5; i++)
            {
                CreateGeneralPassenger("Passenger " + (i + 1), car1, doorCycle);
            }

            for (int i = 5; i < 10; i++)
            {
                CreateGeneralPassenger("Passenger " + (i + 1), car2, doorCycle);
            }

            cameraController.FocusOn(car1.Center);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = car1.Center.gameObject;

            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            Debug.Log("[AI Prototype] General passenger scene created: " + ScenePath);
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
            CreateSideWallSegments(carRoot.transform, center, 1f, wallColor);
            CreateSideWallSegments(carRoot.transform, center, -1f, wallColor);

            CreateEndWall(carRoot.transform, center, -1f, openLeft, wallColor);
            CreateEndWall(carRoot.transform, center, 1f, openRight, wallColor);
            GameObject navigationObject = new GameObject("Grid Navigation");
            navigationObject.transform.SetParent(carRoot.transform);
            navigationObject.transform.position = center;
            data.Navigation = navigationObject.AddComponent<GridNavigation2D>();

            CreateDoorsAndSeats(
                carRoot.transform,
                center,
                data.Navigation,
                data.Doors,
                data.Doorways,
                data.Seats,
                data.ActivityPoints);

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

        private static void CreateSideWallSegments(
            Transform parent,
            Vector2 center,
            float direction,
            Color wallColor)
        {
            float[] doorX = { -7.4f, -3.8f, -0.2f, 3.4f };
            const float openingHalfWidth = 0.65f;
            float segmentStart = -9.1f;

            foreach (float localDoorX in doorX)
            {
                float segmentEnd = localDoorX - openingHalfWidth;
                CreateWallSegment(parent, center, direction, segmentStart, segmentEnd, wallColor);
                segmentStart = localDoorX + openingHalfWidth;
            }

            CreateWallSegment(parent, center, direction, segmentStart, 9.1f, wallColor);
        }

        private static void CreateWallSegment(
            Transform parent,
            Vector2 center,
            float direction,
            float startX,
            float endX,
            Color wallColor)
        {
            float width = endX - startX;
            if (width <= 0f)
            {
                return;
            }

            CreateBlock(
                direction > 0f ? "Top Wall" : "Bottom Wall",
                center + new Vector2((startX + endX) * 0.5f, direction * 4.1f),
                new Vector2(width, WallThickness),
                wallColor,
                parent,
                true,
                "Wall");
        }

        private static void CreateDoorsAndSeats(
            Transform parent,
            Vector2 center,
            GridNavigation2D navigation,
            List<TrainDoorController> doors,
            List<PassengerDoorway> doorways,
            List<PassengerSeatPrototype> seats,
            List<PassengerActivityPoint> activityPoints)
        {
            float[] doorX = { -7.4f, -3.8f, -0.2f, 3.4f };
            float[] seatX = { -5.6f, -2f, 1.6f };
            Color doorColor = new Color(0.16f, 0.58f, 0.58f);
            Color seatColor = new Color(0.1f, 0.45f, 0.62f);
            Color priorityColor = new Color(0.72f, 0.31f, 0.48f);

            foreach (float localX in doorX)
            {
                PassengerDoorway topDoorway = CreateDoorway(
                    parent,
                    center,
                    localX,
                    1f,
                    navigation,
                    doorColor);
                PassengerDoorway bottomDoorway = CreateDoorway(
                    parent,
                    center,
                    localX,
                    -1f,
                    navigation,
                    doorColor);
                doorways.Add(topDoorway);
                doorways.Add(bottomDoorway);
                doors.Add(topDoorway.Door);
                doors.Add(bottomDoorway.Door);
                CreateDoorActivityPoints(parent, center, localX, 1f, activityPoints);
                CreateDoorActivityPoints(parent, center, localX, -1f, activityPoints);
            }

            foreach (float localX in seatX)
            {
                PassengerSeatPrototype topSeat = CreatePassengerSeat(
                    parent,
                    center + new Vector2(localX, 3.3f),
                    seatColor,
                    "Seat");
                PassengerSeatPrototype bottomSeat = CreatePassengerSeat(
                    parent,
                    center + new Vector2(localX, -3.3f),
                    seatColor,
                    "Seat");
                seats.Add(topSeat);
                seats.Add(bottomSeat);
                CreateHandholdPoints(parent, center, topSeat, activityPoints);
                CreateHandholdPoints(parent, center, bottomSeat, activityPoints);
            }

            PassengerSeatPrototype topPrioritySeat = CreatePassengerSeat(
                parent,
                center + new Vector2(5.6f, 3.3f),
                priorityColor,
                "PrioritySeat");
            PassengerSeatPrototype bottomPrioritySeat = CreatePassengerSeat(
                parent,
                center + new Vector2(5.6f, -3.3f),
                priorityColor,
                "PrioritySeat");
            seats.Add(topPrioritySeat);
            seats.Add(bottomPrioritySeat);
            CreateHandholdPoints(parent, center, topPrioritySeat, activityPoints);
            CreateHandholdPoints(parent, center, bottomPrioritySeat, activityPoints);
            CreateAisleActivityPoints(parent, center, activityPoints);
        }

        private static void CreateDoorActivityPoints(
            Transform parent,
            Vector2 center,
            float doorLocalX,
            float side,
            List<PassengerActivityPoint> activityPoints)
        {
            float y = center.y + side * 3.55f;
            float[] leanOffsets = { -0.75f, 0.75f };

            foreach (float offset in leanOffsets)
            {
                activityPoints.Add(CreateActivityPoint(
                    "Lean Point",
                    new Vector2(center.x + doorLocalX + offset, y),
                    new Vector2(0.26f, 0.08f),
                    new Color(0.42f, 0.88f, 0.72f),
                    parent,
                    PassengerActivityType.Lean,
                    "LeanPoint"));
            }

            activityPoints.Add(CreateActivityPoint(
                "Door Standing Point",
                new Vector2(center.x + doorLocalX, center.y + side * 2.75f),
                new Vector2(0.13f, 0.13f),
                new Color(0.82f, 0.4f, 0.32f),
                parent,
                PassengerActivityType.DoorStanding,
                "DoorStandingPoint"));
        }

        private static void CreateHandholdPoints(
            Transform parent,
            Vector2 center,
            PassengerSeatPrototype seat,
            List<PassengerActivityPoint> activityPoints)
        {
            float side = seat.transform.position.y >= center.y ? 1f : -1f;
            float y = center.y + side * 2.35f;

            for (int slot = 0; slot < seat.Capacity; slot++)
            {
                Transform sittingPoint = seat.GetSittingPoint(slot);
                activityPoints.Add(CreateActivityPoint(
                    "Handhold Point",
                    new Vector2(sittingPoint.position.x, y),
                    new Vector2(0.12f, 0.28f),
                    new Color(0.95f, 0.77f, 0.22f),
                    parent,
                    PassengerActivityType.Handhold,
                    "Handhold"));
            }
        }

        private static void CreateAisleActivityPoints(
            Transform parent,
            Vector2 center,
            List<PassengerActivityPoint> activityPoints)
        {
            float[] xPositions = { -6.4f, -4.2f, -2f, 0.2f, 2.4f, 4.6f, 6.8f };
            float[] yPositions = { -0.8f, 0f, 0.8f };

            for (int i = 0; i < xPositions.Length; i++)
            {
                float y = yPositions[i % yPositions.Length];
                activityPoints.Add(CreateActivityPoint(
                    "Aisle Standing Point",
                    center + new Vector2(xPositions[i], y),
                    new Vector2(0.1f, 0.1f),
                    new Color(0.52f, 0.58f, 0.66f),
                    parent,
                    PassengerActivityType.AisleStanding,
                    "AisleStandingPoint"));
            }
        }

        private static PassengerActivityPoint CreateActivityPoint(
            string name,
            Vector2 position,
            Vector2 markerSize,
            Color color,
            Transform parent,
            PassengerActivityType activityType,
            string materialKey)
        {
            GameObject marker = CreateBlock(
                name,
                position,
                markerSize,
                color,
                parent,
                false,
                materialKey,
                -0.12f);
            PassengerActivityPoint point = marker.AddComponent<PassengerActivityPoint>();
            point.Configure(activityType);
            return point;
        }

        private static TrainDoorController CreateDoor(Transform parent, Vector2 position, Color color)
        {
            GameObject doorRoot = new GameObject("Door");
            doorRoot.transform.SetParent(parent);
            doorRoot.transform.position = position;

            CreateBlock("Door Opening", position, new Vector2(1.2f, WallThickness),
                new Color(0.025f, 0.035f, 0.05f), doorRoot.transform, false, "DoorOpening", -0.1f);
            GameObject leftPanel = CreateBlock("Left Panel", position + Vector2.left * 0.29f,
                new Vector2(0.56f, WallThickness), color, doorRoot.transform, false, "Door", -0.2f);
            GameObject rightPanel = CreateBlock("Right Panel", position + Vector2.right * 0.29f,
                new Vector2(0.56f, WallThickness), color, doorRoot.transform, false, "Door", -0.2f);
            AddKinematicCollision(leftPanel);
            AddKinematicCollision(rightPanel);

            TrainDoorController controller = doorRoot.AddComponent<TrainDoorController>();
            controller.Configure(leftPanel.transform, rightPanel.transform);
            return controller;
        }

        private static PassengerDoorway CreateDoorway(
            Transform parent,
            Vector2 center,
            float localX,
            float side,
            GridNavigation2D navigation,
            Color color)
        {
            Vector2 doorPosition = center + new Vector2(localX, side * 4.1f);
            TrainDoorController door = CreateDoor(parent, doorPosition, color);
            Transform insidePoint = CreatePoint(
                "Passenger Inside Point",
                center + new Vector2(localX, side * 2.15f),
                parent);
            Transform outsidePoint = CreatePoint(
                "Passenger Outside Point",
                center + new Vector2(localX, side * 4.85f),
                parent);

            PassengerDoorway doorway = door.gameObject.AddComponent<PassengerDoorway>();
            doorway.Configure(door, insidePoint, outsidePoint, navigation);
            return doorway;
        }

        private static void CreateGeneralPassenger(
            string name,
            TrainCarBuildData car,
            TrainDoorCyclePrototype doorCycle)
        {
            if (car.Doorways.Count == 0)
            {
                return;
            }

            PassengerDoorway previewDoorway = car.Doorways[0];
            GameObject passengerObject = CreateBlock(
                name,
                (Vector2)previewDoorway.OutsidePoint.position,
                new Vector2(0.55f, 0.72f),
                new Color(0.95f, 0.55f, 0.18f),
                car.Center,
                false,
                "GeneralPassenger",
                -0.3f);
            AddDynamicCollision(passengerObject);
            TextMesh label = CreatePassengerLabel(passengerObject.transform);
            passengerObject.AddComponent<GeneralPassengerPrototype>().Configure(
                null,
                doorCycle,
                car.Center,
                true,
                label);
        }

        private static TextMesh CreatePassengerLabel(Transform passenger)
        {
            GameObject labelObject = new GameObject("Passenger State Label");
            labelObject.transform.SetParent(passenger);
            labelObject.transform.localPosition = new Vector3(0f, -0.55f, -0.2f);

            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = passenger.name + "\nWaiting";
            label.anchor = TextAnchor.UpperCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 0.07f;
            label.fontSize = 42;
            label.color = Color.white;
            return label;
        }

        private static void AddDynamicCollision(GameObject actor)
        {
            Rigidbody2D body = actor.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CapsuleCollider2D collider = actor.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.86f, 0.9f);
        }

        private static void AddKinematicCollision(GameObject panel)
        {
            Rigidbody2D body = panel.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            panel.AddComponent<BoxCollider2D>();
        }

        private static PassengerSeatPrototype CreatePassengerSeat(
            Transform parent,
            Vector2 position,
            Color color,
            string materialKey)
        {
            GameObject seatObject = CreateBlock("Seat", position, new Vector2(1.45f, 0.85f),
                color, parent, true, materialKey);
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
                "Path",
                "WallMark"
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
