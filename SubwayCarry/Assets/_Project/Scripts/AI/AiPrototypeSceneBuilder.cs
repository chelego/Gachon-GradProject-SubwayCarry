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
        public const string JourneyScenePath =
            "Assets/_Project/Scenes/Prototype/PassengerJourney_AI.unity";
        private const string MaterialFolder = "Assets/_Project/Art/PrototypeMaterials";
        private const float CarWidth = 24.6f;
        private const float CarHeight = 4.6f;
        private const float WallThickness = 0.2f;
        private const float ConnectorOpening = 2.2f;
        private const float SideWallY = 2.4f;
        private const float SeatY = 1.55f;
        private const float DoorInsideY = 0.65f;
        private const float DoorOutsideY = 3.15f;
        private const float DoorWidth = 1.9f;
        private const float SecondCarInteriorOffsetX = 0.55f;
        private const float ConnectorFloorInset = 0.15f;
        private const float ConnectorThresholdInset = 0.25f;
        private const float ConnectorPortalInset = 0.18f;
        private const float ConnectorArrivalInset = 0.85f;

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
                new Vector2(26.2f, 0f),
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

            for (int i = 0; i < 60; i++)
            {
                CreateGeneralPassenger("Passenger " + (i + 1), car1, doorCycle);
            }

            CreatePlayer(car1, doorCycle);

            cameraController.FocusOn(car1.Center);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = car1.Center.gameObject;

            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            Debug.Log("[AI Prototype] General passenger scene created: " + ScenePath);
        }

        [MenuItem("SubwayCarry/Prototype/Build Passenger Journey AI Scene")]
        public static void BuildJourney()
        {
            EnsureFolder("Assets/_Project/Scenes", "Prototype");
            EnsureFolder("Assets/_Project/Art", "PrototypeMaterials");

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            GameObject root = new GameObject("Passenger Journey AI Prototype");
            TrainCarCameraController cameraController = CreateCamera(root.transform);
            Camera camera = cameraController.GetComponent<Camera>();
            camera.orthographicSize = 10f;
            cameraController.transform.position = new Vector3(0f, -3.2f, -10f);

            TrainCarBuildData car = CreateTrainCar(
                "Journey Test Train",
                new Vector2(0f, 4f),
                false,
                false,
                root.transform);

            GameObject doorCycleObject = new GameObject("Journey Door Cycle");
            doorCycleObject.transform.SetParent(root.transform);
            TrainDoorCyclePrototype doorCycle =
                doorCycleObject.AddComponent<TrainDoorCyclePrototype>();
            doorCycle.Configure(car.Doors.ToArray());
            SetFloatValue(doorCycle, "initialDelay", 0.5f);
            SetFloatValue(doorCycle, "openDuration", 8f);
            SetFloatValue(doorCycle, "boardingClearanceDelay", 0.2f);
            SetFloatValue(doorCycle, "travelDuration", 2f);

            PassengerStationRoutePrototype stationRoute =
                CreateJourneyStation(car, doorCycle);
            for (int index = 0; index < 4; index++)
            {
                PassengerPostAlightingPlan plan = index % 2 == 0
                    ? PassengerPostAlightingPlan.ExitStation
                    : PassengerPostAlightingPlan.TransferOnceThenExit;
                CreateJourneyPassenger(
                    "Journey Passenger " + (index + 1),
                    new Vector2(-9.8f + index * 0.72f, -10.1f),
                    car,
                    doorCycle,
                    stationRoute,
                    plan);
            }

            EditorSceneManager.SaveScene(scene, JourneyScenePath);
            ValidateJourneyScene(scene);
            Selection.activeGameObject = stationRoute.gameObject;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            Debug.Log(
                "[AI Prototype] Passenger journey scene created: " +
                JourneyScenePath);
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

        private static PassengerStationRoutePrototype CreateJourneyStation(
            TrainCarBuildData car,
            TrainDoorCyclePrototype doorCycle)
        {
            GameObject stationRoot = new GameObject("Journey Station Route");
            stationRoot.transform.SetParent(car.Center);

            CreateBlock(
                "Station Journey Floor",
                new Vector2(0f, -4.8f),
                new Vector2(24f, 12.5f),
                new Color(0.12f, 0.16f, 0.21f),
                stationRoot.transform,
                false,
                "JourneyStationFloor",
                1.1f);
            CreateBlock(
                "Station Boundary Left",
                new Vector2(-12f, -4.8f),
                new Vector2(0.2f, 12.5f),
                new Color(0.24f, 0.31f, 0.4f),
                stationRoot.transform,
                true,
                "JourneyStationBoundary");
            CreateBlock(
                "Station Boundary Right",
                new Vector2(12f, -4.8f),
                new Vector2(0.2f, 12.5f),
                new Color(0.24f, 0.31f, 0.4f),
                stationRoot.transform,
                true,
                "JourneyStationBoundary");
            CreateBlock(
                "Station Boundary Bottom",
                new Vector2(0f, -11.05f),
                new Vector2(24f, 0.2f),
                new Color(0.24f, 0.31f, 0.4f),
                stationRoot.transform,
                true,
                "JourneyStationBoundary");

            CreateBlock(
                "Entry Fare Gate Left",
                new Vector2(-6.4f, -8.4f),
                new Vector2(0.5f, 1.8f),
                new Color(0.9f, 0.72f, 0.14f),
                stationRoot.transform,
                true,
                "JourneyFareGate");
            CreateBlock(
                "Entry Fare Gate Right",
                new Vector2(-4.6f, -8.4f),
                new Vector2(0.5f, 1.8f),
                new Color(0.9f, 0.72f, 0.14f),
                stationRoot.transform,
                true,
                "JourneyFareGate");
            CreateBlock(
                "Exit Fare Gate Left",
                new Vector2(6.6f, -8.1f),
                new Vector2(0.5f, 1.8f),
                new Color(0.9f, 0.72f, 0.14f),
                stationRoot.transform,
                true,
                "JourneyFareGate");
            CreateBlock(
                "Exit Fare Gate Right",
                new Vector2(8.4f, -8.1f),
                new Vector2(0.5f, 1.8f),
                new Color(0.9f, 0.72f, 0.14f),
                stationRoot.transform,
                true,
                "JourneyFareGate");

            foreach (float side in new[] { -1f, 1f })
            {
                CreateBlock(
                    side < 0f ? "Down Escalator" : "Up Escalator",
                    new Vector2(side * 1.1f, -5.1f),
                    new Vector2(1.6f, 4.4f),
                    new Color(0.18f, 0.25f, 0.32f),
                    stationRoot.transform,
                    false,
                    "JourneyEscalator",
                    0.3f);
            }

            GameObject navigationObject = new GameObject("Station Journey Navigation");
            navigationObject.transform.SetParent(stationRoot.transform);
            navigationObject.transform.position = new Vector2(0f, -4.8f);
            GridNavigation2D stationNavigation =
                navigationObject.AddComponent<GridNavigation2D>();
            stationNavigation.ConfigureDimensions(96, 50, 0.25f);

            PassengerIntentCoordinator coordinator =
                car.Center.GetComponent<PassengerIntentCoordinator>();
            if (coordinator == null)
            {
                coordinator = car.Center.gameObject.AddComponent<PassengerIntentCoordinator>();
            }

            PassengerJourneyWaypoint[] boardingRoute =
            {
                CreateJourneyWaypoint(
                    "Approach Entry Gate",
                    new Vector2(-7.5f, -9.6f),
                    PassengerJourneyWaypointAction.Walk,
                    0f,
                    4,
                    Vector2.down,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Tap Entry Fare Gate",
                    new Vector2(-5.5f, -8.4f),
                    PassengerJourneyWaypointAction.TapEntryGate,
                    0.35f,
                    1,
                    Vector2.down,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Enter Down Escalator",
                    new Vector2(-1.1f, -6.7f),
                    PassengerJourneyWaypointAction.EnterVerticalConnector,
                    0.15f,
                    2,
                    Vector2.down,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Leave Down Escalator",
                    new Vector2(-1.1f, -3.4f),
                    PassengerJourneyWaypointAction.LeaveVerticalConnector,
                    0f,
                    2,
                    Vector2.down,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Reach Boarding Platform",
                    new Vector2(-8.4f, -0.45f),
                    PassengerJourneyWaypointAction.ReachPlatform,
                    0f,
                    4,
                    Vector2.down,
                    stationRoot.transform)
            };

            PassengerJourneyWaypoint[] exitRoute =
            {
                CreateJourneyWaypoint(
                    "Clear Arrival Platform",
                    new Vector2(5.8f, -0.45f),
                    PassengerJourneyWaypointAction.ClearPlatform,
                    0f,
                    4,
                    Vector2.up,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Enter Up Escalator",
                    new Vector2(1.1f, -3.4f),
                    PassengerJourneyWaypointAction.EnterVerticalConnector,
                    0.15f,
                    2,
                    Vector2.up,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Leave Up Escalator",
                    new Vector2(1.1f, -6.7f),
                    PassengerJourneyWaypointAction.LeaveVerticalConnector,
                    0f,
                    2,
                    Vector2.up,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Tap Exit Fare Gate",
                    new Vector2(7.5f, -8.1f),
                    PassengerJourneyWaypointAction.TapExitGate,
                    0.35f,
                    1,
                    Vector2.up,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Leave Station",
                    new Vector2(9.7f, -10.1f),
                    PassengerJourneyWaypointAction.LeaveStation,
                    0f,
                    4,
                    Vector2.up,
                    stationRoot.transform)
            };

            PassengerDoorway transferDoorway =
                car.Doorways[Mathf.Min(3, car.Doorways.Count - 1)];
            PassengerJourneyWaypoint[] transferRoute =
            {
                CreateJourneyWaypoint(
                    "Clear Transfer Platform",
                    new Vector2(-5.8f, -0.45f),
                    PassengerJourneyWaypointAction.ClearPlatform,
                    0f,
                    4,
                    Vector2.up,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Enter Transfer Passage",
                    new Vector2(-4f, -3f),
                    PassengerJourneyWaypointAction.EnterTransferPassage,
                    0.2f,
                    2,
                    Vector2.up,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Transfer Passage Center",
                    new Vector2(0f, -4.2f),
                    PassengerJourneyWaypointAction.Walk,
                    0f,
                    4,
                    Vector2.left,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Reach Transfer Platform",
                    new Vector2(8.4f, -0.45f),
                    PassengerJourneyWaypointAction.ReachTransferPlatform,
                    0f,
                    4,
                    Vector2.down,
                    stationRoot.transform)
            };

            PassengerStationRoutePrototype route =
                stationRoot.AddComponent<PassengerStationRoutePrototype>();
            route.Configure(
                boardingRoute,
                exitRoute,
                transferRoute,
                new[] { stationNavigation },
                coordinator,
                transferDoorway,
                doorCycle,
                car.Center);
            return route;
        }

        private static PassengerJourneyWaypoint CreateJourneyWaypoint(
            string name,
            Vector2 position,
            PassengerJourneyWaypointAction action,
            float dwellDuration,
            int capacity,
            Vector2 queueDirection,
            Transform parent)
        {
            GameObject marker = CreateBlock(
                name,
                position,
                new Vector2(0.34f, 0.34f),
                new Color(0.25f, 0.85f, 1f),
                parent,
                false,
                "JourneyWaypoint",
                -0.18f);
            PassengerJourneyWaypoint waypoint =
                marker.AddComponent<PassengerJourneyWaypoint>();
            waypoint.Configure(action, dwellDuration, capacity, queueDirection);
            return waypoint;
        }

        private static void CreateJourneyPassenger(
            string name,
            Vector2 spawnPosition,
            TrainCarBuildData car,
            TrainDoorCyclePrototype doorCycle,
            PassengerStationRoutePrototype route,
            PassengerPostAlightingPlan plan)
        {
            PassengerDoorway boardingDoorway = car.Doorways[0];
            GameObject passengerObject = CreateBlock(
                name,
                spawnPosition,
                new Vector2(0.62f, 0.62f),
                plan == PassengerPostAlightingPlan.ExitStation
                    ? new Color(0.95f, 0.55f, 0.18f)
                    : new Color(0.72f, 0.4f, 0.95f),
                car.Center,
                false,
                plan == PassengerPostAlightingPlan.ExitStation
                    ? "JourneyPassengerExit"
                    : "JourneyPassengerTransfer",
                -0.3f);
            AddDynamicCollision(passengerObject);
            TextMesh label = CreatePassengerLabel(passengerObject.transform);
            GeneralPassengerPrototype passenger =
                passengerObject.AddComponent<GeneralPassengerPrototype>();
            PassengerStationJourneyPrototype journey =
                passengerObject.AddComponent<PassengerStationJourneyPrototype>();
            passenger.Configure(
                boardingDoorway,
                doorCycle,
                car.Center,
                false,
                label);
            passenger.ConfigureRideStops(1, 1);
            passenger.ConfigureStationJourney(journey);
            journey.Configure(route, plan, 3f);
        }

        private static void ValidateJourneyScene(Scene scene)
        {
            PassengerStationRoutePrototype route =
                UnityEngine.Object.FindFirstObjectByType<PassengerStationRoutePrototype>();
            PassengerStationJourneyPrototype[] journeys =
                UnityEngine.Object.FindObjectsByType<PassengerStationJourneyPrototype>(
                    FindObjectsSortMode.None);
            PassengerJourneyWaypoint[] waypoints =
                UnityEngine.Object.FindObjectsByType<PassengerJourneyWaypoint>(
                    FindObjectsSortMode.None);

            var errors = new List<string>();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                errors.Add("journey scene is not loaded");
            }
            if (route == null)
            {
                errors.Add("station route is missing");
            }
            if (journeys.Length != 4)
            {
                errors.Add("expected four journey passengers");
            }
            if (waypoints.Length != 14)
            {
                errors.Add("expected fourteen station journey waypoints");
            }
            if (route != null &&
                (route.TransferBoardingDoorway == null ||
                 route.TransferDoorCycle == null ||
                 route.TransferMapRoot == null))
            {
                errors.Add("transfer boarding handoff is not wired");
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Passenger journey scene validation failed: " +
                    string.Join(", ", errors));
            }
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

            float leftWallLocalX = openLeft
                ? -(CarWidth * 0.5f + WallThickness * 0.5f)
                : GetClosedLeftWallLocalX();
            float rightWallLocalX = CarWidth * 0.5f + WallThickness * 0.5f;
            float floorLeftLocalX = leftWallLocalX + WallThickness * 0.5f;
            float floorRightLocalX = rightWallLocalX - WallThickness * 0.5f;
            float floorWidth = floorRightLocalX - floorLeftLocalX;
            Vector2 floorCenter = center +
                                  Vector2.right *
                                  ((floorLeftLocalX + floorRightLocalX) * 0.5f);

            CreateBlock(
                "Floor",
                floorCenter,
                new Vector2(floorWidth, CarHeight),
                new Color(0.08f, 0.12f, 0.18f),
                carRoot.transform,
                false,
                "Floor",
                1f);
            CreatePlatforms(carRoot.transform, center);

            Color wallColor = new Color(0.24f, 0.31f, 0.4f);
            CreateSideWallSegments(
                carRoot.transform,
                center,
                1f,
                openLeft,
                leftWallLocalX,
                rightWallLocalX,
                wallColor);
            CreateSideWallSegments(
                carRoot.transform,
                center,
                -1f,
                openLeft,
                leftWallLocalX,
                rightWallLocalX,
                wallColor);

            CreateEndWall(
                carRoot.transform,
                center,
                leftWallLocalX,
                openLeft,
                wallColor);
            CreateEndWall(
                carRoot.transform,
                center,
                rightWallLocalX,
                openRight,
                wallColor);
            GameObject navigationObject = new GameObject("Grid Navigation");
            navigationObject.transform.SetParent(carRoot.transform);
            navigationObject.transform.position = floorCenter;
            data.Navigation = navigationObject.AddComponent<GridNavigation2D>();
            data.Navigation.ConfigureDimensions(
                Mathf.CeilToInt((floorWidth + 0.4f) / 0.25f),
                Mathf.CeilToInt((CarHeight + 0.4f) / 0.25f),
                0.25f);

            CreateDoorsAndSeats(
                carRoot.transform,
                center,
                data.Navigation,
                data.Doors,
                data.Doorways,
                data.Seats,
                data.ActivityPoints,
                openLeft,
                openRight);

            if (openLeft)
            {
                CreateConnector(carRoot.transform, center, -1f);
                data.LeftPortal = CreatePortal(
                    "Left Connector Portal",
                    center + new Vector2(-(CarWidth * 0.5f - ConnectorPortalInset), 0f),
                    carRoot.transform);
                data.LeftArrival = CreatePoint(
                    "Left Connector Arrival",
                    center + new Vector2(-(CarWidth * 0.5f - ConnectorArrivalInset), 0f),
                    carRoot.transform);
            }

            if (openRight)
            {
                CreateConnector(carRoot.transform, center, 1f);
                data.RightPortal = CreatePortal(
                    "Right Connector Portal",
                    center + new Vector2(CarWidth * 0.5f - ConnectorPortalInset, 0f),
                    carRoot.transform);
                data.RightArrival = CreatePoint(
                    "Right Connector Arrival",
                    center + new Vector2(CarWidth * 0.5f - ConnectorArrivalInset, 0f),
                    carRoot.transform);
            }

            return data;
        }

        private static void CreatePlatforms(Transform parent, Vector2 center)
        {
            Color platformColor = new Color(0.27f, 0.29f, 0.31f);
            Color safetyLineColor = new Color(0.95f, 0.73f, 0.12f);

            CreateBlock("Platform Lower", center + new Vector2(0f, -4.05f),
                new Vector2(CarWidth + 0.4f, 3f), platformColor, parent, false, "Platform", 1.2f);
            CreateBlock("Platform Safety Line Lower", center + new Vector2(0f, -2.8f),
                new Vector2(CarWidth + 0.2f, 0.12f), safetyLineColor, parent, false, "PlatformSafetyLine", -0.05f);
        }

        private static void CreateEndWall(
            Transform parent,
            Vector2 center,
            float localWallX,
            bool open,
            Color wallColor)
        {
            float x = center.x + localWallX;
            if (!open)
            {
                CreateBlock("Closed End Wall", new Vector2(x, center.y),
                    new Vector2(WallThickness, CarHeight + WallThickness),
                    wallColor, parent, true, "Wall");
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
            bool mirrorLayout,
            float leftWallLocalX,
            float rightWallLocalX,
            Color wallColor)
        {
            float[] doorX = GetDoorPositions(mirrorLayout);
            const float openingHalfWidth = DoorWidth * 0.5f + 0.05f;
            float segmentStart = leftWallLocalX;

            foreach (float localDoorX in doorX)
            {
                float segmentEnd = localDoorX - openingHalfWidth;
                CreateWallSegment(parent, center, direction, segmentStart, segmentEnd, wallColor);
                segmentStart = localDoorX + openingHalfWidth;
            }

            CreateWallSegment(
                parent,
                center,
                direction,
                segmentStart,
                rightWallLocalX,
                wallColor);
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
                center + new Vector2((startX + endX) * 0.5f, direction * SideWallY),
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
            List<PassengerActivityPoint> activityPoints,
            bool openLeft,
            bool openRight)
        {
            bool mirrorLayout = openLeft;
            float[] doorX = GetDoorPositions(mirrorLayout);
            float interiorOffsetX = mirrorLayout ? SecondCarInteriorOffsetX : 0f;
            float[] seatX =
            {
                -5.6f + interiorOffsetX,
                interiorOffsetX,
                5.6f + interiorOffsetX
            };
            Color doorColor = new Color(0.16f, 0.58f, 0.58f);
            Color seatColor = new Color(0.1f, 0.45f, 0.62f);
            Color priorityColor = new Color(0.72f, 0.31f, 0.48f);

            foreach (float localX in doorX)
            {
                CreateDoorway(
                    parent,
                    center,
                    localX,
                    1f,
                    navigation,
                    doorColor,
                    false);
                PassengerDoorway bottomDoorway = CreateDoorway(
                    parent,
                    center,
                    localX,
                    -1f,
                    navigation,
                    doorColor,
                    true);
                doorways.Add(bottomDoorway);
                doors.Add(bottomDoorway.Door);
            }

            foreach (float localX in seatX)
            {
                PassengerSeatPrototype topSeat = CreatePassengerSeat(
                    parent,
                    center + new Vector2(localX, SeatY),
                    seatColor,
                    "Seat");
                PassengerSeatPrototype bottomSeat = CreatePassengerSeat(
                    parent,
                    center + new Vector2(localX, -SeatY),
                    seatColor,
                    "Seat");
                seats.Add(topSeat);
                seats.Add(bottomSeat);
                CreateHandholdPoints(parent, center, topSeat, activityPoints);
                CreateHandholdPoints(parent, center, bottomSeat, activityPoints);
            }

            float prioritySeatX = mirrorLayout ? -10.85f : 10.85f;
            PassengerSeatPrototype topPrioritySeat = CreatePassengerSeat(
                parent,
                center + new Vector2(prioritySeatX, SeatY),
                priorityColor,
                "PrioritySeat");
            PassengerSeatPrototype bottomPrioritySeat = CreatePassengerSeat(
                parent,
                center + new Vector2(prioritySeatX, -SeatY),
                priorityColor,
                "PrioritySeat");
            seats.Add(topPrioritySeat);
            seats.Add(bottomPrioritySeat);
            CreateHandholdPoints(parent, center, topPrioritySeat, activityPoints);
            CreateHandholdPoints(parent, center, bottomPrioritySeat, activityPoints);
        }

        private static float[] GetDoorPositions(bool mirrorLayout)
        {
            float offsetX = mirrorLayout ? SecondCarInteriorOffsetX : 0f;
            return new[]
            {
                -8.4f + offsetX,
                -2.8f + offsetX,
                2.8f + offsetX,
                8.4f + offsetX
            };
        }

        private static float GetClosedLeftWallLocalX()
        {
            float leftmostDoorX = GetDoorPositions(false)[0];
            return leftmostDoorX - DoorWidth * 0.5f - WallThickness * 0.5f;
        }

        private static void CreateHandholdPoints(
            Transform parent,
            Vector2 center,
            PassengerSeatPrototype seat,
            List<PassengerActivityPoint> activityPoints)
        {
            float side = seat.transform.position.y >= center.y ? 1f : -1f;
            float y = center.y + side * 0.72f;

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

            CreateBlock("Door Opening", position, new Vector2(DoorWidth, WallThickness),
                new Color(0.025f, 0.035f, 0.05f), doorRoot.transform, false, "DoorOpening", -0.1f);
            GameObject leftPanel = CreateBlock("Left Panel", position + Vector2.left * 0.46f,
                new Vector2(0.9f, WallThickness), color, doorRoot.transform, false, "Door", -0.2f);
            GameObject rightPanel = CreateBlock("Right Panel", position + Vector2.right * 0.46f,
                new Vector2(0.9f, WallThickness), color, doorRoot.transform, false, "Door", -0.2f);
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
            Color color,
            bool serviceEnabled)
        {
            Vector2 doorPosition = center + new Vector2(localX, side * SideWallY);
            TrainDoorController door = CreateDoor(parent, doorPosition, color);
            Transform insidePoint = CreatePoint(
                "Passenger Inside Point",
                center + new Vector2(localX, side * DoorInsideY),
                parent);
            Transform outsidePoint = CreatePoint(
                "Passenger Outside Point",
                center + new Vector2(localX, side * DoorOutsideY),
                parent);

            PassengerDoorway doorway = door.gameObject.AddComponent<PassengerDoorway>();
            doorway.Configure(
                door,
                insidePoint,
                outsidePoint,
                navigation,
                serviceEnabled);
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
                new Vector2(0.62f, 0.62f),
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

        private static void CreatePlayer(
            TrainCarBuildData car,
            TrainDoorCyclePrototype doorCycle)
        {
            if (car.Doorways.Count == 0)
            {
                return;
            }

            PassengerDoorway doorway = car.Doorways[0];
            Vector2 spawnPosition = (Vector2)car.Center.position + new Vector2(5.8f, 0f);
            GameObject playerObject = CreateBlock(
                "Player",
                spawnPosition,
                new Vector2(0.72f, 0.72f),
                new Color(0.3f, 0.9f, 0.55f),
                car.Center,
                false,
                "Player",
                -0.35f);
            AddDynamicCollision(playerObject);
            Rigidbody2D playerBody = playerObject.GetComponent<Rigidbody2D>();
            playerBody.mass = 8f;
            playerBody.linearDamping = 8f;
            CreatePassengerLabel(playerObject.transform).text = "PLAYER";
            playerObject.AddComponent<PlayerBoardingCyclePrototype>().Configure(
                doorway.Door,
                doorCycle,
                car.Center.position);
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

            CircleCollider2D collider = actor.AddComponent<CircleCollider2D>();
            collider.radius = 0.46f;
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
            bool prioritySeat = materialKey == "PrioritySeat";
            float seatWidth = prioritySeat ? 1.85f : 3.55f;
            int capacity = prioritySeat ? 3 : 7;
            GameObject seatObject = CreateBlock("Seat", position, new Vector2(seatWidth, 0.85f),
                color, parent, true, materialKey);
            CreateSeatSidePartitions(
                parent,
                position,
                prioritySeat,
                seatWidth);
            var sittingPoints = new List<Transform>(capacity);
            float usableHalfWidth = seatWidth * 0.5f - 0.28f;
            for (int index = 0; index < capacity; index++)
            {
                float t = capacity <= 1
                    ? 0.5f
                    : index / (float)(capacity - 1);
                float offsetX = Mathf.Lerp(
                    -usableHalfWidth,
                    usableHalfWidth,
                    t);
                sittingPoints.Add(CreatePoint(
                    "Sitting Point " + (index + 1),
                    position + Vector2.right * offsetX,
                    seatObject.transform));
            }

            PassengerSeatPrototype seat = seatObject.AddComponent<PassengerSeatPrototype>();
            seat.Configure(sittingPoints.ToArray());
            return seat;
        }

        private static void CreateSeatSidePartitions(
            Transform parent,
            Vector2 seatPosition,
            bool prioritySeat,
            float seatWidth)
        {
            Color dividerColor = new Color(0.31f, 0.39f, 0.48f);
            float dividerOffset = seatWidth * 0.5f + 0.06f;
            float localSeatX = seatPosition.x - parent.position.x;
            float connectorSide = Mathf.Sign(localSeatX);
            float seatSide = Mathf.Sign(seatPosition.y - parent.position.y);

            foreach (float side in new[] { -1f, 1f })
            {
                bool connectorWall =
                    prioritySeat &&
                    Mathf.Approximately(side, connectorSide);
                Vector2 dividerSize = connectorWall
                    ? new Vector2(0.12f, 1.45f)
                    : new Vector2(0.12f, 1.12f);
                Vector2 dividerPosition =
                    seatPosition + Vector2.right * side * dividerOffset;
                if (connectorWall)
                {
                    dividerPosition += Vector2.up * seatSide * 0.08f;
                }

                CreateBlock(
                    connectorWall
                        ? "Priority Seat Connector Wall"
                        : side < 0f
                            ? "Seat Side Wall Left"
                            : "Seat Side Wall Right",
                    dividerPosition,
                    dividerSize,
                    dividerColor,
                    parent,
                    true,
                    "SeatDivider",
                    -0.05f);
            }
        }

        private static void CreateConnector(Transform parent, Vector2 center, float direction)
        {
            Vector2 connectorCenter = center +
                                      new Vector2(
                                          direction * (CarWidth * 0.5f - ConnectorFloorInset),
                                          0f);
            CreateBlock("Connector Floor", connectorCenter, new Vector2(0.48f, 2.1f),
                new Color(0.12f, 0.16f, 0.2f), parent, false, "Connector", 0.5f);
            CreateBlock(
                "Connector Threshold",
                center +
                new Vector2(direction * (CarWidth * 0.5f - ConnectorThresholdInset), 0f),
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
            trigger.size = new Vector2(0.08f, 1.8f);
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
            camera.orthographicSize = 7.5f;
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

        private static void SetFloatValue(
            UnityEngine.Object target,
            string propertyName,
            float value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Float)
            {
                throw new InvalidOperationException(
                    target.GetType().Name + "." + propertyName + " is missing.");
            }

            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
