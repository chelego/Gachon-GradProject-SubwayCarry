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
        }

        [MenuItem("SubwayCarry/Prototype/Build AI Pathfinding Scene")]
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

            GameObject start = CreateMarker(
                "Start",
                new Vector2(-7f, -1.5f),
                new Color(0.25f, 0.85f, 0.4f),
                car1.Center);
            GameObject car2Goal = CreateMarker(
                "Car 2 Goal",
                new Vector2(28.5f, 1.5f),
                new Color(0.95f, 0.35f, 0.3f),
                car2.Center);

            GameObject agent = CreateBlock(
                "Passenger Agent",
                start.transform.position,
                new Vector2(0.65f, 0.85f),
                new Color(1f, 0.85f, 0.15f),
                car1.Center,
                false,
                "PassengerAgent",
                -0.2f);
            var body = agent.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            agent.AddComponent<CapsuleCollider2D>();

            var line = agent.AddComponent<LineRenderer>();
            line.sharedMaterial = GetOrCreateMaterial("Path", new Color(1f, 0.85f, 0.2f));
            line.widthMultiplier = 0.08f;
            line.useWorldSpace = true;
            line.sortingOrder = 10;

            var passenger = agent.AddComponent<PassengerPrototypeAgent>();
            passenger.Configure(car1.Navigation, new[]
            {
                car1.RightPortal.transform,
                car2Goal.transform,
                car2.LeftPortal.transform,
                start.transform
            }, line);

            cameraController.FocusOn(car1.Center);
            EditorSceneManager.SaveScene(scene, ScenePath);
            ValidatePathfinder();
            Selection.activeGameObject = agent;

            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            Debug.Log("[AI Prototype] Two-car scene created: " + ScenePath);
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
            CreateDoorsAndSeats(carRoot.transform, center);

            var data = new TrainCarBuildData
            {
                Center = carRoot.transform
            };

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

        private static void CreateDoorsAndSeats(Transform parent, Vector2 center)
        {
            float[] doorX = { -7.4f, -3.8f, -0.2f, 3.4f };
            float[] seatX = { -5.6f, -2f, 1.6f };
            Color doorColor = new Color(0.16f, 0.58f, 0.58f);
            Color seatColor = new Color(0.1f, 0.45f, 0.62f);
            Color priorityColor = new Color(0.72f, 0.31f, 0.48f);

            foreach (float localX in doorX)
            {
                CreateDoor(parent, center + new Vector2(localX, 3.92f), doorColor);
                CreateDoor(parent, center + new Vector2(localX, -3.92f), doorColor);
            }

            foreach (float localX in seatX)
            {
                CreateSeat(parent, center + new Vector2(localX, 3.3f), seatColor, "Seat");
                CreateSeat(parent, center + new Vector2(localX, -3.3f), seatColor, "Seat");
            }

            CreateSeat(parent, center + new Vector2(5.6f, 3.3f), priorityColor, "PrioritySeat");
            CreateSeat(parent, center + new Vector2(5.6f, -3.3f), priorityColor, "PrioritySeat");
        }

        private static void CreateDoor(Transform parent, Vector2 position, Color color)
        {
            CreateBlock("Door", position, new Vector2(1.15f, 0.24f),
                color, parent, false, "Door", -0.1f);
            CreateBlock("Door Divider", position, new Vector2(0.05f, 0.25f),
                new Color(0.82f, 0.87f, 0.76f), parent, false, "WallMark", -0.2f);
        }

        private static void CreateSeat(
            Transform parent,
            Vector2 position,
            Color color,
            string materialKey)
        {
            CreateBlock(materialKey, position, new Vector2(1.45f, 0.85f),
                color, parent, true, materialKey);
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

        private static GameObject CreateMarker(string name, Vector2 position, Color color, Transform parent)
        {
            return CreateBlock(name, position, new Vector2(0.5f, 0.5f),
                color, parent, false, "Marker", -0.1f);
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
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
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
                "WallTop"
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
