#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using SubwayCarry.Prototype.Gameplay;
using StationFareGatePrototype = SubwayCarry.Gameplay.StationFareGatePrototype;
using SubwayCarry.Prototype;
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
        public const string JourneySketchScenePath =
            "Assets/_Project/Scenes/Prototype/StationDirection_PlayerPrototype.unity";
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

        [MenuItem("SubwayCarry/Prototype/Build Directional Station Player Scene")]
        public static void BuildJourneySketch()
        {
            EnsureFolder("Assets/_Project/Scenes", "Prototype");
            EnsureFolder("Assets/_Project/Art", "PrototypeMaterials");

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            GameObject root = new GameObject("Station Direction Player Prototype");
            GameObject concourseScreen = new GameObject("MAP 1 - Concourse");
            concourseScreen.transform.SetParent(root.transform);
            GameObject upboundStairsScreen = new GameObject("MAP 2A - Upbound Stairs");
            upboundStairsScreen.transform.SetParent(root.transform);
            GameObject downboundStairsScreen = new GameObject("MAP 2B - Downbound Stairs");
            downboundStairsScreen.transform.SetParent(root.transform);
            GameObject platformScreen = new GameObject("MAP 3 - Platform");
            platformScreen.transform.SetParent(root.transform);
            GameObject screenFlowObject = new GameObject("Station Map Screen Flow");
            screenFlowObject.transform.SetParent(root.transform);
            StationMapScreenSwitcherPrototype screenSwitcher =
                screenFlowObject.AddComponent<StationMapScreenSwitcherPrototype>();

            TrainCarCameraController cameraController = CreateCamera(root.transform);
            Camera camera = cameraController.GetComponent<Camera>();
            camera.orthographicSize = 10.8f;
            cameraController.transform.position = new Vector3(0f, 0f, -10f);

            Vector2 upperTrainPosition = new Vector2(0f, 3.15f);
            Vector2 lowerTrainPosition = new Vector2(0f, -3.15f);
            TrainCarBuildData upperTrain = CreateTrainCar(
                "Upbound Train",
                upperTrainPosition,
                false,
                false,
                platformScreen.transform,
                false,
                1f);
            TrainCarBuildData lowerTrain = CreateTrainCar(
                "Downbound Train",
                lowerTrainPosition,
                false,
                false,
                platformScreen.transform,
                false,
                -1f);

            TrainDoorCyclePrototype upperDoorCycle = CreateJourneyDoorCycle(
                "Upbound Door Cycle",
                upperTrain,
                platformScreen.transform,
                14.4f);
            TrainDoorCyclePrototype lowerDoorCycle = CreateJourneyDoorCycle(
                "Downbound Door Cycle",
                lowerTrain,
                platformScreen.transform,
                14.4f);

            BuildDirectionalPlayerStation(
                screenSwitcher,
                concourseScreen,
                upboundStairsScreen,
                downboundStairsScreen,
                platformScreen);

            CreateStationPlayer(
                root.transform,
                camera,
                new Vector2(-10.5f, 1.05f));

            upperTrain.Center.gameObject
                .AddComponent<StationTrainArrivalPrototype>()
                .Configure(new Vector2(0f, 3.15f), -34f, 3.5f, 0.25f);
            lowerTrain.Center.gameObject
                .AddComponent<StationTrainArrivalPrototype>()
                .Configure(new Vector2(0f, -3.15f), 34f, 3.5f, 0.25f);
            SetFloatValue(upperDoorCycle, "initialDelay", 4.2f);
            SetFloatValue(lowerDoorCycle, "initialDelay", 4.2f);

            screenSwitcher.Configure(
                concourseScreen,
                new[]
                {
                    concourseScreen,
                    upboundStairsScreen,
                    downboundStairsScreen,
                    platformScreen
                });

            EditorSceneManager.SaveScene(scene, JourneySketchScenePath);
            ValidateDirectionalPlayerScene(scene);
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            Debug.Log(
                "[Prototype] Directional station player scene created: " +
                JourneySketchScenePath);
        }

        private static PlayerController CreateStationPlayer(
            Transform parent,
            Camera facingCamera,
            Vector2 spawnPosition)
        {
            GameObject playerObject = CreateBlock(
                "Player",
                spawnPosition,
                new Vector2(0.72f, 0.88f),
                new Color(0.35f, 0.95f, 0.65f),
                parent,
                false,
                "JourneyPlayer",
                -0.5f);
            playerObject.tag = "Player";

            Rigidbody2D body = playerObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.mass = 8f;
            body.linearDamping = 8f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CapsuleCollider2D collider = playerObject.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.7f, 0.84f);

            playerObject.AddComponent<PlayerPosture>();
            PlayerController controller = playerObject.AddComponent<PlayerController>();
            playerObject.AddComponent<PlayerInteraction>();

            GameObject facingIndicator = CreateBlock(
                "Player Facing Indicator",
                spawnPosition + Vector2.down * 0.56f,
                new Vector2(0.42f, 0.13f),
                new Color(0.2f, 0.9f, 0.95f),
                playerObject.transform,
                false,
                "JourneyPlayerFacing",
                -0.28f);
            facingIndicator.transform.localPosition =
                new Vector3(0f, -0.56f, -0.28f);
            controller.ConfigureFacing(facingCamera, facingIndicator.transform);
            return controller;
        }

        private static void BuildDirectionalPlayerStation(
            StationMapScreenSwitcherPrototype screenSwitcher,
            GameObject concourseScreen,
            GameObject upboundStairsScreen,
            GameObject downboundStairsScreen,
            GameObject platformScreen)
        {
            Color floorColor = new Color(0.11f, 0.15f, 0.2f);
            Color boundaryColor = new Color(0.28f, 0.35f, 0.43f);
            Color gateHousingColor = new Color(0.2f, 0.25f, 0.31f);
            Color fareGateOrange = new Color(1f, 0.32f, 0.035f);
            Color stairColor = new Color(0.42f, 0.5f, 0.58f);
            Color stairTreadColor = new Color(0.72f, 0.78f, 0.83f);
            Color platformColor = new Color(0.34f, 0.37f, 0.4f);
            Color trackColor = new Color(0.025f, 0.035f, 0.05f);

            CreateStationScreenFrame(
                "Concourse",
                new Vector2(28f, 16f),
                floorColor,
                boundaryColor,
                concourseScreen.transform);

            float[] housingY = { -7f, -4.7f, -2.4f, -0.1f, 2.2f, 4.5f, 7f };
            foreach (float y in housingY)
            {
                CreateBlock(
                    "Fare Gate Housing",
                    new Vector2(0f, y),
                    new Vector2(1.8f, 0.45f),
                    gateHousingColor,
                    concourseScreen.transform,
                    true,
                    "JourneyFareGateHousing");
            }

            float[] gateY = { -5.85f, -3.55f, -1.25f, 1.05f, 3.35f, 5.75f };
            for (int index = 0; index < gateY.Length; index++)
            {
                GameObject barrier = CreateBlock(
                    index == 3 ? "Interactive Fare Gate" : "Closed Fare Gate",
                    new Vector2(0f, gateY[index]),
                    new Vector2(0.2f, 1.25f),
                    fareGateOrange,
                    concourseScreen.transform,
                    true,
                    "JourneyFareGateOrange",
                    -0.08f);
                if (index == 3)
                {
                    barrier.AddComponent<StationFareGatePrototype>().Configure(
                        barrier.GetComponent<Collider2D>(),
                        barrier.GetComponent<Renderer>(),
                        2.5f);
                }
            }

            CreateStairLanding(
                "Upbound Concourse Stairs",
                new Vector2(10.4f, 4.45f),
                stairColor,
                stairTreadColor,
                concourseScreen.transform);
            CreateStairLanding(
                "Downbound Concourse Stairs",
                new Vector2(10.4f, -4.45f),
                stairColor,
                stairTreadColor,
                concourseScreen.transform);
            CreateStationSketchLabel(
                "Upbound Concourse Sign",
                "UPBOUND STAIRS",
                new Vector2(9.8f, 6.2f),
                Color.white,
                concourseScreen.transform,
                0.055f);
            CreateStationSketchLabel(
                "Downbound Concourse Sign",
                "DOWNBOUND STAIRS",
                new Vector2(9.8f, -6.2f),
                Color.white,
                concourseScreen.transform,
                0.055f);
            CreateStationSketchLabel(
                "Fare Gate Sign",
                "FARE GATES",
                new Vector2(0f, 0f),
                Color.white,
                concourseScreen.transform,
                0.05f);

            CreateStairPassageScreen(
                "Upbound",
                upboundStairsScreen.transform,
                stairColor,
                stairTreadColor,
                boundaryColor);
            CreateStairPassageScreen(
                "Downbound",
                downboundStairsScreen.transform,
                stairColor,
                stairTreadColor,
                boundaryColor);

            CreateStationScreenFrame(
                "Platform",
                new Vector2(30f, 21f),
                floorColor,
                boundaryColor,
                platformScreen.transform);
            CreateBlock(
                "Upbound Platform",
                new Vector2(0f, 8f),
                new Vector2(28f, 4f),
                platformColor,
                platformScreen.transform,
                false,
                "JourneySketchPlatform",
                0.9f);
            CreateBlock(
                "Upbound Track",
                new Vector2(0f, 3.15f),
                new Vector2(28f, 5.7f),
                trackColor,
                platformScreen.transform,
                false,
                "JourneySketchTrack",
                1.2f);
            CreateBlock(
                "Downbound Track",
                new Vector2(0f, -3.15f),
                new Vector2(28f, 5.7f),
                trackColor,
                platformScreen.transform,
                false,
                "JourneySketchTrack",
                1.2f);
            CreateBlock(
                "Downbound Platform",
                new Vector2(0f, -8f),
                new Vector2(28f, 4f),
                platformColor,
                platformScreen.transform,
                false,
                "JourneySketchPlatform",
                0.9f);
            CreateStairLanding(
                "Upbound Platform Stairs",
                new Vector2(0f, 9.2f),
                stairColor,
                stairTreadColor,
                platformScreen.transform);
            CreateStairLanding(
                "Downbound Platform Stairs",
                new Vector2(0f, -9.2f),
                stairColor,
                stairTreadColor,
                platformScreen.transform);
            CreateStationSketchLabel(
                "Upbound Platform Sign",
                "UPBOUND PLATFORM",
                new Vector2(8.5f, 8f),
                Color.white,
                platformScreen.transform,
                0.055f);
            CreateStationSketchLabel(
                "Downbound Platform Sign",
                "DOWNBOUND PLATFORM",
                new Vector2(8.5f, -8f),
                Color.white,
                platformScreen.transform,
                0.055f);

            Transform concourseUpArrival = CreatePoint(
                "Concourse Upbound Stair Arrival",
                new Vector2(8.2f, 4.45f),
                concourseScreen.transform);
            Transform concourseDownArrival = CreatePoint(
                "Concourse Downbound Stair Arrival",
                new Vector2(8.2f, -4.45f),
                concourseScreen.transform);
            Transform upStairsLeftArrival = CreatePoint(
                "Upbound Stairs Concourse Arrival",
                new Vector2(-10.5f, 0f),
                upboundStairsScreen.transform);
            Transform upStairsRightArrival = CreatePoint(
                "Upbound Stairs Platform Arrival",
                new Vector2(10.5f, 0f),
                upboundStairsScreen.transform);
            Transform downStairsLeftArrival = CreatePoint(
                "Downbound Stairs Concourse Arrival",
                new Vector2(-10.5f, 0f),
                downboundStairsScreen.transform);
            Transform downStairsRightArrival = CreatePoint(
                "Downbound Stairs Platform Arrival",
                new Vector2(10.5f, 0f),
                downboundStairsScreen.transform);
            Transform upPlatformArrival = CreatePoint(
                "Upbound Platform Stair Arrival",
                new Vector2(0f, 7.7f),
                platformScreen.transform);
            Transform downPlatformArrival = CreatePoint(
                "Downbound Platform Stair Arrival",
                new Vector2(0f, -7.7f),
                platformScreen.transform);

            CreateStationPortal(
                "Concourse To Upbound Stairs",
                new Vector2(12.2f, 4.45f),
                new Vector2(1.2f, 2.5f),
                concourseScreen.transform,
                screenSwitcher,
                upboundStairsScreen,
                upStairsLeftArrival);
            CreateStationPortal(
                "Concourse To Downbound Stairs",
                new Vector2(12.2f, -4.45f),
                new Vector2(1.2f, 2.5f),
                concourseScreen.transform,
                screenSwitcher,
                downboundStairsScreen,
                downStairsLeftArrival);
            CreateStationPortal(
                "Upbound Stairs To Concourse",
                new Vector2(-12.4f, 0f),
                new Vector2(1.2f, 6f),
                upboundStairsScreen.transform,
                screenSwitcher,
                concourseScreen,
                concourseUpArrival);
            CreateStationPortal(
                "Upbound Stairs To Platform",
                new Vector2(12.4f, 0f),
                new Vector2(1.2f, 6f),
                upboundStairsScreen.transform,
                screenSwitcher,
                platformScreen,
                upPlatformArrival);
            CreateStationPortal(
                "Downbound Stairs To Concourse",
                new Vector2(-12.4f, 0f),
                new Vector2(1.2f, 6f),
                downboundStairsScreen.transform,
                screenSwitcher,
                concourseScreen,
                concourseDownArrival);
            CreateStationPortal(
                "Downbound Stairs To Platform",
                new Vector2(12.4f, 0f),
                new Vector2(1.2f, 6f),
                downboundStairsScreen.transform,
                screenSwitcher,
                platformScreen,
                downPlatformArrival);
            CreateStationPortal(
                "Upbound Platform To Stairs",
                new Vector2(0f, 9.45f),
                new Vector2(4.6f, 1.2f),
                platformScreen.transform,
                screenSwitcher,
                upboundStairsScreen,
                upStairsRightArrival);
            CreateStationPortal(
                "Downbound Platform To Stairs",
                new Vector2(0f, -9.45f),
                new Vector2(4.6f, 1.2f),
                platformScreen.transform,
                screenSwitcher,
                downboundStairsScreen,
                downStairsRightArrival);
        }

        private static void CreateStationScreenFrame(
            string prefix,
            Vector2 size,
            Color floorColor,
            Color boundaryColor,
            Transform parent)
        {
            CreateBlock(
                prefix + " Floor",
                Vector2.zero,
                size,
                floorColor,
                parent,
                false,
                "JourneySketchFloor",
                1.3f);
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            CreateBlock(prefix + " Top Wall", new Vector2(0f, halfHeight), new Vector2(size.x, 0.22f), boundaryColor, parent, true, "JourneySketchBoundary");
            CreateBlock(prefix + " Bottom Wall", new Vector2(0f, -halfHeight), new Vector2(size.x, 0.22f), boundaryColor, parent, true, "JourneySketchBoundary");
            CreateBlock(prefix + " Left Wall", new Vector2(-halfWidth, 0f), new Vector2(0.22f, size.y), boundaryColor, parent, true, "JourneySketchBoundary");
            CreateBlock(prefix + " Right Wall", new Vector2(halfWidth, 0f), new Vector2(0.22f, size.y), boundaryColor, parent, true, "JourneySketchBoundary");
        }

        private static void CreateStairPassageScreen(
            string direction,
            Transform parent,
            Color stairColor,
            Color treadColor,
            Color boundaryColor)
        {
            CreateBlock(
                direction + " Stair Passage",
                Vector2.zero,
                new Vector2(28f, 8f),
                stairColor,
                parent,
                false,
                "JourneyStairs",
                0.9f);
            for (int index = -10; index <= 10; index += 2)
            {
                CreateBlock(
                    direction + " Stair Tread",
                    new Vector2(index, 0f),
                    new Vector2(0.18f, 6.8f),
                    treadColor,
                    parent,
                    false,
                    "JourneyStairTread",
                    0.4f);
            }

            CreateBlock(direction + " Stair Top Wall", new Vector2(0f, 4f), new Vector2(28f, 0.22f), boundaryColor, parent, true, "JourneySketchBoundary");
            CreateBlock(direction + " Stair Bottom Wall", new Vector2(0f, -4f), new Vector2(28f, 0.22f), boundaryColor, parent, true, "JourneySketchBoundary");
            CreateBlock(direction + " Stair Left Wall", new Vector2(-14f, 0f), new Vector2(0.22f, 8f), boundaryColor, parent, true, "JourneySketchBoundary");
            CreateBlock(direction + " Stair Right Wall", new Vector2(14f, 0f), new Vector2(0.22f, 8f), boundaryColor, parent, true, "JourneySketchBoundary");
            CreateStationSketchLabel(
                direction + " Stair Sign",
                direction.ToUpperInvariant() + " STAIRS",
                new Vector2(0f, 3.25f),
                Color.white,
                parent,
                0.06f);
        }

        private static void CreateStairLanding(
            string name,
            Vector2 center,
            Color stairColor,
            Color treadColor,
            Transform parent)
        {
            CreateBlock(
                name,
                center,
                new Vector2(5.2f, 2.7f),
                stairColor,
                parent,
                false,
                "JourneyStairs",
                0.25f);
            for (int index = -2; index <= 2; index++)
            {
                CreateBlock(
                    name + " Tread",
                    center + Vector2.right * index * 0.9f,
                    new Vector2(0.15f, 2.35f),
                    treadColor,
                    parent,
                    false,
                    "JourneyStairTread",
                    0.12f);
            }
        }

        private static StationMapPortalPrototype CreateStationPortal(
            string name,
            Vector2 position,
            Vector2 size,
            Transform parent,
            StationMapScreenSwitcherPrototype screenSwitcher,
            GameObject targetScreen,
            Transform arrivalPoint)
        {
            GameObject portalObject = new GameObject(name);
            portalObject.transform.SetParent(parent);
            portalObject.transform.position = position;
            BoxCollider2D trigger = portalObject.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = size;
            StationMapPortalPrototype portal =
                portalObject.AddComponent<StationMapPortalPrototype>();
            portal.Configure(screenSwitcher, targetScreen, arrivalPoint);
            return portal;
        }

        private static void ValidateDirectionalPlayerScene(Scene scene)
        {
            var players = new List<PlayerController>();
            var screenSwitchers = new List<StationMapScreenSwitcherPrototype>();
            var portals = new List<StationMapPortalPrototype>();
            var fareGates = new List<StationFareGatePrototype>();
            var passengers = new List<GeneralPassengerPrototype>();
            var journeys = new List<PassengerStationJourneyPrototype>();
            var trainArrivals = new List<StationTrainArrivalPrototype>();
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                players.AddRange(sceneRoot.GetComponentsInChildren<PlayerController>(true));
                screenSwitchers.AddRange(sceneRoot.GetComponentsInChildren<StationMapScreenSwitcherPrototype>(true));
                portals.AddRange(sceneRoot.GetComponentsInChildren<StationMapPortalPrototype>(true));
                fareGates.AddRange(sceneRoot.GetComponentsInChildren<StationFareGatePrototype>(true));
                passengers.AddRange(sceneRoot.GetComponentsInChildren<GeneralPassengerPrototype>(true));
                journeys.AddRange(sceneRoot.GetComponentsInChildren<PassengerStationJourneyPrototype>(true));
                trainArrivals.AddRange(sceneRoot.GetComponentsInChildren<StationTrainArrivalPrototype>(true));
            }

            var errors = new List<string>();
            if (!scene.IsValid() || !scene.isLoaded) errors.Add("player station scene is not loaded");
            if (players.Count != 1) errors.Add("expected one controllable player");
            if (screenSwitchers.Count != 1 || screenSwitchers[0].ScreenCount != 4) errors.Add("expected four exclusive map screens");
            if (portals.Count != 8) errors.Add("expected eight bidirectional stair portals");
            if (fareGates.Count != 1) errors.Add("expected one interactive fare gate");
            if (passengers.Count != 0 || journeys.Count != 0) errors.Add("passenger AI must not exist in player scene");
            if (trainArrivals.Count != 2) errors.Add("expected one train per direction");
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Directional player scene validation failed: " +
                    string.Join(", ", errors));
            }
        }

        private static void PartitionJourneySketchScreens(
            Transform routeRoot,
            Transform concourseScreen,
            Transform escalatorScreen,
            Transform platformScreen)
        {
            var children = new List<Transform>();
            foreach (Transform child in routeRoot)
            {
                children.Add(child);
            }

            foreach (Transform child in children)
            {
                float x = child.position.x;
                Transform targetScreen;
                float horizontalOffset;
                if (x < 30f)
                {
                    targetScreen = concourseScreen;
                    horizontalOffset = 0f;
                }
                else if (x < 90f)
                {
                    targetScreen = escalatorScreen;
                    horizontalOffset = 55f;
                }
                else
                {
                    targetScreen = platformScreen;
                    horizontalOffset = 120f;
                }

                child.SetParent(targetScreen, true);
                child.position -= Vector3.right * horizontalOffset;
            }
        }

        private static void MoveTrainToPlatformScreen(
            TrainCarBuildData train,
            Transform platformScreen,
            float verticalPosition)
        {
            train.Center.SetParent(platformScreen, true);
            train.Center.position = new Vector3(0f, verticalPosition, 0f);
        }

        private static void ConfigureJourneySketchScreenTransitions(
            PassengerStationRoutePrototype route,
            PassengerJourneyScreenSwitcherPrototype switcher,
            GameObject concourseScreen,
            GameObject escalatorScreen,
            GameObject platformScreen)
        {
            var waypoints = new HashSet<PassengerJourneyWaypoint>();
            AddJourneyWaypoints(waypoints, route.BoardingRoute);
            AddJourneyWaypoints(waypoints, route.ExitRoute);
            AddJourneyWaypoints(waypoints, route.TransferRoute);
            AddJourneyWaypoints(waypoints, route.PostTransferExitRoute);

            foreach (PassengerJourneyWaypoint waypoint in waypoints)
            {
                if (waypoint == null || waypoint.TransitionArrival == null)
                {
                    continue;
                }

                GameObject targetScreen = null;
                switch (waypoint.gameObject.name)
                {
                    case "Enter Down Escalator Screen":
                    case "Enter Top Platform Escalator":
                    case "Enter Bottom Platform Escalator":
                        targetScreen = escalatorScreen;
                        break;

                    case "Leave Down Escalator Screen":
                    case "Enter Transfer Escalator":
                        targetScreen = platformScreen;
                        break;

                    case "Leave Up Escalator Screen":
                    case "Leave Post Transfer Up Escalator":
                        targetScreen = concourseScreen;
                        break;
                }

                if (targetScreen != null)
                {
                    waypoint.ConfigureTransition(
                        waypoint.TransitionArrival,
                        switcher,
                        targetScreen);
                }
            }
        }

        private static void AddJourneyWaypoints(
            HashSet<PassengerJourneyWaypoint> destination,
            PassengerJourneyWaypoint[] source)
        {
            if (source == null)
            {
                return;
            }

            foreach (PassengerJourneyWaypoint waypoint in source)
            {
                if (waypoint != null)
                {
                    destination.Add(waypoint);
                }
            }
        }

        private static TrainDoorCyclePrototype CreateJourneyDoorCycle(
            string name,
            TrainCarBuildData car,
            Transform parent,
            float initialDelay)
        {
            GameObject cycleObject = new GameObject(name);
            cycleObject.transform.SetParent(parent);
            TrainDoorCyclePrototype cycle =
                cycleObject.AddComponent<TrainDoorCyclePrototype>();
            cycle.Configure(car.Doors.ToArray());
            SetFloatValue(cycle, "initialDelay", initialDelay);
            SetFloatValue(cycle, "openDuration", 9f);
            SetFloatValue(cycle, "boardingClearanceDelay", 0.25f);
            SetFloatValue(cycle, "travelDuration", 2f);
            return cycle;
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

        private static PassengerStationRoutePrototype CreateJourneySketchStation(
            TrainCarBuildData car,
            TrainDoorCyclePrototype doorCycle,
            Transform parent)
        {
            GameObject stationRoot = new GameObject("Station Sketch Route");
            stationRoot.transform.SetParent(parent);

            Color floorColor = new Color(0.11f, 0.15f, 0.2f);
            Color boundaryColor = new Color(0.28f, 0.35f, 0.43f);
            Color gateHousingColor = new Color(0.2f, 0.25f, 0.31f);
            Color fareGateOrange = new Color(1f, 0.32f, 0.035f);
            Color escalatorYellow = new Color(1f, 0.88f, 0.035f);
            Color platformColor = new Color(0.34f, 0.37f, 0.4f);
            Color trackColor = new Color(0.035f, 0.045f, 0.06f);

            CreateBlock(
                "Concourse Floor",
                new Vector2(-22f, -1.5f),
                new Vector2(20f, 13f),
                floorColor,
                stationRoot.transform,
                false,
                "JourneySketchFloor",
                1.15f);
            CreateBlock(
                "Escalator Hall Background",
                new Vector2(-3f, -1.5f),
                new Vector2(18f, 10f),
                new Color(0.025f, 0.035f, 0.05f),
                stationRoot.transform,
                false,
                "JourneySketchVoid",
                1.15f);
            CreateBlock(
                "Platform Area Background",
                new Vector2(18f, -1.5f),
                new Vector2(24f, 13f),
                floorColor,
                stationRoot.transform,
                false,
                "JourneySketchFloor",
                1.15f);

            CreateBlock(
                "Concourse Top Wall Left",
                new Vector2(-30.8f, 5.1f),
                new Vector2(2.4f, 0.22f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");
            CreateBlock(
                "Concourse Top Wall Right",
                new Vector2(-19.6f, 5.1f),
                new Vector2(15.2f, 0.22f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");
            CreateBlock(
                "Concourse Left Wall Upper",
                new Vector2(-32.1f, 0.2f),
                new Vector2(0.22f, 9.8f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");
            CreateBlock(
                "Concourse Left Wall Lower",
                new Vector2(-32.1f, -7.35f),
                new Vector2(0.22f, 1.3f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");
            CreateBlock(
                "Concourse Bottom Wall",
                new Vector2(-22f, -8.1f),
                new Vector2(20.2f, 0.22f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");

            CreateBlock(
                "Concourse Right Wall Bottom",
                new Vector2(-11.9f, -7.2f),
                new Vector2(0.22f, 1.8f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");
            CreateBlock(
                "Concourse Right Wall Middle",
                new Vector2(-11.9f, -2f),
                new Vector2(0.22f, 4.4f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");
            CreateBlock(
                "Concourse Right Wall Top",
                new Vector2(-11.9f, 3.95f),
                new Vector2(0.22f, 2.3f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");

            float[] gateHousingY =
            {
                -7.7f,
                -5.5f,
                -3.3f,
                -1.1f,
                1.1f,
                3.3f,
                4.85f
            };
            foreach (float y in gateHousingY)
            {
                CreateBlock(
                    "Fare Gate Housing",
                    new Vector2(-22f, y),
                    new Vector2(1.8f, 0.46f),
                    gateHousingColor,
                    stationRoot.transform,
                    true,
                    "JourneyFareGateHousing");
            }

            float[] fareGateY = { -6.6f, -4.4f, -2.2f, 0f, 2.2f, 4.05f };
            foreach (float y in fareGateY)
            {
                CreateBlock(
                    "Orange Fare Gate",
                    new Vector2(-22f, y),
                    new Vector2(0.2f, 1.34f),
                    fareGateOrange,
                    stationRoot.transform,
                    false,
                    "JourneyFareGateOrange",
                    -0.08f);
            }

            CreateBlock(
                "Concourse Down Escalator",
                new Vector2(-14f, 1.5f),
                new Vector2(4f, 2.35f),
                escalatorYellow,
                stationRoot.transform,
                false,
                "JourneyEscalatorYellow",
                0.25f);
            CreateBlock(
                "Concourse Up Escalator",
                new Vector2(-14f, -5.3f),
                new Vector2(4f, 2.15f),
                escalatorYellow,
                stationRoot.transform,
                false,
                "JourneyEscalatorYellow",
                0.25f);

            CreateBlock(
                "Down Escalator Lane",
                new Vector2(-3f, 1.5f),
                new Vector2(18f, 2.35f),
                escalatorYellow,
                stationRoot.transform,
                false,
                "JourneyEscalatorYellow",
                0.25f);
            CreateBlock(
                "Up Escalator Lane",
                new Vector2(-3f, -5.3f),
                new Vector2(18f, 2.15f),
                escalatorYellow,
                stationRoot.transform,
                false,
                "JourneyEscalatorYellow",
                0.25f);
            CreateBlock(
                "Escalator Hall Top Wall",
                new Vector2(-3f, 3.6f),
                new Vector2(18f, 0.22f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");
            CreateBlock(
                "Escalator Hall Divider",
                new Vector2(-3.3f, -1.55f),
                new Vector2(17.4f, 0.22f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");
            CreateBlock(
                "Escalator Hall Bottom Wall",
                new Vector2(-3f, -6.6f),
                new Vector2(18f, 0.22f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");

            CreateBlock(
                "Platform Connector Middle Wall",
                new Vector2(6.1f, -1.8f),
                new Vector2(0.22f, 4.4f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");
            CreateBlock(
                "Platform Connector Top Wall",
                new Vector2(6.1f, 4.05f),
                new Vector2(0.22f, 2.3f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");
            CreateBlock(
                "Platform Connector Bottom Wall",
                new Vector2(6.1f, -7.3f),
                new Vector2(0.22f, 1.4f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");

            CreateBlock(
                "Top Platform",
                new Vector2(18f, 3.65f),
                new Vector2(24f, 3f),
                platformColor,
                stationRoot.transform,
                false,
                "JourneySketchPlatform",
                0.75f);
            CreateBlock(
                "Upper Track",
                new Vector2(18.5f, 0.95f),
                new Vector2(20.4f, 1.8f),
                trackColor,
                stationRoot.transform,
                true,
                "JourneySketchTrack",
                0.65f);
            CreateBlock(
                "Lower Track",
                new Vector2(18.5f, -1.25f),
                new Vector2(20.4f, 1.8f),
                trackColor,
                stationRoot.transform,
                true,
                "JourneySketchTrack",
                0.65f);
            CreateBlock(
                "Bottom Platform",
                new Vector2(18f, -5.1f),
                new Vector2(24f, 5.8f),
                platformColor,
                stationRoot.transform,
                false,
                "JourneySketchPlatform",
                0.75f);
            CreateBlock(
                "Top Platform Escalator",
                new Vector2(7.15f, 3.65f),
                new Vector2(1.85f, 2.35f),
                escalatorYellow,
                stationRoot.transform,
                false,
                "JourneyEscalatorYellow",
                0.2f);
            CreateBlock(
                "Bottom Platform Escalator",
                new Vector2(7.15f, -5.3f),
                new Vector2(1.85f, 2.15f),
                escalatorYellow,
                stationRoot.transform,
                false,
                "JourneyEscalatorYellow",
                0.2f);
            CreateBlock(
                "Platform Right Wall",
                new Vector2(30.2f, -1.5f),
                new Vector2(0.22f, 13.2f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");
            CreateBlock(
                "Platform Bottom Wall",
                new Vector2(18.1f, -8.1f),
                new Vector2(24.2f, 0.22f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");

            CreateStationSketchLabel(
                "Station Entrance Top Label",
                "STATION ENTRANCE",
                new Vector2(-28.5f, 4.45f),
                Color.white,
                stationRoot.transform);
            CreateStationSketchLabel(
                "Station Exit Bottom Label",
                "STATION EXIT",
                new Vector2(-29.3f, -6.1f),
                Color.white,
                stationRoot.transform);
            CreateStationSketchLabel(
                "Fare Gate Label",
                "FARE GATES",
                new Vector2(-22f, -0.1f),
                Color.white,
                stationRoot.transform,
                0.055f);
            CreateStationSketchLabel(
                "Down Escalator Label",
                "DOWN ESCALATOR",
                new Vector2(-3f, 1.5f),
                new Color(0.08f, 0.08f, 0.05f),
                stationRoot.transform);
            CreateStationSketchLabel(
                "Up Escalator Label",
                "UP ESCALATOR",
                new Vector2(-3f, -5.3f),
                new Color(0.08f, 0.08f, 0.05f),
                stationRoot.transform);
            CreateStationSketchLabel(
                "Platform Label",
                "PLATFORM",
                new Vector2(18f, 3.4f),
                Color.white,
                stationRoot.transform);
            CreateStationSketchLabel(
                "Upper Track Label",
                "TRACK",
                new Vector2(18.5f, 0.95f),
                new Color(0.72f, 0.75f, 0.8f),
                stationRoot.transform);
            CreateStationSketchLabel(
                "Lower Track Label",
                "TRACK",
                new Vector2(18.5f, -1.25f),
                new Color(0.72f, 0.75f, 0.8f),
                stationRoot.transform);
            CreateStationSketchLabel(
                "Bottom Platform Label",
                "PLATFORM",
                new Vector2(18f, -5.2f),
                Color.white,
                stationRoot.transform);

            GameObject navigationObject = new GameObject("Station Sketch Navigation");
            navigationObject.transform.SetParent(stationRoot.transform);
            navigationObject.transform.position = new Vector2(-1f, -1.5f);
            GridNavigation2D stationNavigation =
                navigationObject.AddComponent<GridNavigation2D>();
            stationNavigation.ConfigureDimensions(184, 44, 0.35f);

            PassengerIntentCoordinator coordinator =
                car.Center.GetComponent<PassengerIntentCoordinator>();
            if (coordinator == null)
            {
                coordinator = car.Center.gameObject.AddComponent<PassengerIntentCoordinator>();
            }

            PassengerJourneyWaypoint[] boardingRoute =
            {
                CreateJourneyWaypoint(
                    "Approach Sketch Entry Gate",
                    new Vector2(-26.4f, 2.2f),
                    PassengerJourneyWaypointAction.Walk,
                    0f,
                    4,
                    Vector2.left,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Tap Sketch Entry Fare Gate",
                    new Vector2(-22f, 2.2f),
                    PassengerJourneyWaypointAction.TapEntryGate,
                    0.35f,
                    1,
                    Vector2.left,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Cross Paid Concourse",
                    new Vector2(-17.2f, 2.2f),
                    PassengerJourneyWaypointAction.Walk,
                    0f,
                    4,
                    Vector2.left,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Enter Sketch Down Escalator",
                    new Vector2(-13.4f, 1.5f),
                    PassengerJourneyWaypointAction.EnterVerticalConnector,
                    0.15f,
                    2,
                    Vector2.left,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Leave Sketch Down Escalator",
                    new Vector2(5.2f, 1.5f),
                    PassengerJourneyWaypointAction.LeaveVerticalConnector,
                    0f,
                    2,
                    Vector2.left,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Reach Platform Escalator Landing",
                    new Vector2(7.2f, 3.65f),
                    PassengerJourneyWaypointAction.Walk,
                    0f,
                    4,
                    Vector2.left,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Reach Sketch Boarding Platform",
                    new Vector2(9.6f, 4.55f),
                    PassengerJourneyWaypointAction.ReachPlatform,
                    0f,
                    4,
                    Vector2.left,
                    stationRoot.transform)
            };

            PassengerJourneyWaypoint[] exitRoute =
            {
                CreateJourneyWaypoint(
                    "Clear Sketch Arrival Platform",
                    new Vector2(26.4f, 4.55f),
                    PassengerJourneyWaypointAction.ClearPlatform,
                    0f,
                    4,
                    Vector2.right,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Enter Sketch Up Escalator",
                    new Vector2(7.2f, -5.3f),
                    PassengerJourneyWaypointAction.EnterVerticalConnector,
                    0.15f,
                    2,
                    Vector2.right,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Leave Sketch Up Escalator",
                    new Vector2(-13.4f, -5.3f),
                    PassengerJourneyWaypointAction.LeaveVerticalConnector,
                    0f,
                    2,
                    Vector2.right,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Approach Sketch Exit Gate",
                    new Vector2(-17.2f, -4.4f),
                    PassengerJourneyWaypointAction.Walk,
                    0f,
                    4,
                    Vector2.right,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Tap Sketch Exit Fare Gate",
                    new Vector2(-22f, -4.4f),
                    PassengerJourneyWaypointAction.TapExitGate,
                    0.35f,
                    1,
                    Vector2.right,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Leave Sketch Station",
                    new Vector2(-31.1f, -5.7f),
                    PassengerJourneyWaypointAction.LeaveStation,
                    0f,
                    4,
                    Vector2.right,
                    stationRoot.transform)
            };

            PassengerDoorway transferDoorway =
                car.Doorways[Mathf.Min(3, car.Doorways.Count - 1)];
            PassengerJourneyWaypoint[] transferRoute =
            {
                CreateJourneyWaypoint(
                    "Clear Sketch Transfer Platform",
                    new Vector2(26.4f, 4.55f),
                    PassengerJourneyWaypointAction.ClearPlatform,
                    0f,
                    4,
                    Vector2.left,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Enter Sketch Transfer Passage",
                    new Vector2(29.35f, 3.6f),
                    PassengerJourneyWaypointAction.EnterTransferPassage,
                    0.2f,
                    2,
                    Vector2.left,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Reach Lower Transfer Landing",
                    new Vector2(29.35f, -5f),
                    PassengerJourneyWaypointAction.Walk,
                    0f,
                    2,
                    Vector2.up,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Cross Lower Transfer Platform",
                    new Vector2(18f, -5f),
                    PassengerJourneyWaypointAction.Walk,
                    0f,
                    4,
                    Vector2.right,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Reach Sketch Transfer Platform",
                    new Vector2(9.6f, 4.55f),
                    PassengerJourneyWaypointAction.ReachTransferPlatform,
                    0f,
                    4,
                    Vector2.right,
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

        private static PassengerStationRoutePrototype CreateSeparatedJourneySketchStation(
            TrainCarBuildData upperTrain,
            TrainDoorCyclePrototype upperDoorCycle,
            TrainCarBuildData lowerTrain,
            TrainDoorCyclePrototype lowerDoorCycle,
            Transform parent)
        {
            GameObject stationRoot = new GameObject("Separated Station Screens");
            stationRoot.transform.SetParent(parent);

            Color floorColor = new Color(0.11f, 0.15f, 0.2f);
            Color boundaryColor = new Color(0.28f, 0.35f, 0.43f);
            Color gateHousingColor = new Color(0.2f, 0.25f, 0.31f);
            Color fareGateOrange = new Color(1f, 0.32f, 0.035f);
            Color escalatorYellow = new Color(1f, 0.88f, 0.035f);
            Color platformColor = new Color(0.34f, 0.37f, 0.4f);
            Color trackColor = new Color(0.025f, 0.035f, 0.05f);

            CreateBlock(
                "Concourse Screen Floor",
                Vector2.zero,
                new Vector2(26f, 14f),
                floorColor,
                stationRoot.transform,
                false,
                "JourneySketchFloor",
                1.15f);
            CreateBlock(
                "Concourse Top Wall Left",
                new Vector2(-11.8f, 7.1f),
                new Vector2(2.4f, 0.22f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");
            CreateBlock(
                "Concourse Top Wall Right",
                new Vector2(1.8f, 7.1f),
                new Vector2(22.4f, 0.22f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");
            CreateBlock(
                "Concourse Bottom Wall",
                new Vector2(0f, -7.1f),
                new Vector2(26.2f, 0.22f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");
            CreateBlock(
                "Concourse Left Wall Upper",
                new Vector2(-13.1f, 1.2f),
                new Vector2(0.22f, 11.8f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");
            CreateBlock(
                "Concourse Left Wall Lower",
                new Vector2(-13.1f, -6.5f),
                new Vector2(0.22f, 1.2f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");
            CreateBlock(
                "Concourse Right Wall Center",
                new Vector2(13.1f, 0f),
                new Vector2(0.22f, 3.8f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");
            CreateBlock(
                "Concourse Right Wall Top",
                new Vector2(13.1f, 6.2f),
                new Vector2(0.22f, 1.8f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");
            CreateBlock(
                "Concourse Right Wall Bottom",
                new Vector2(13.1f, -6.2f),
                new Vector2(0.22f, 1.8f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");

            float[] gateHousingY = { -6.8f, -4.7f, -2.6f, -0.5f, 1.6f, 3.7f, 5.8f, 6.85f };
            foreach (float y in gateHousingY)
            {
                CreateBlock(
                    "Fare Gate Housing",
                    new Vector2(0f, y),
                    new Vector2(1.8f, 0.45f),
                    gateHousingColor,
                    stationRoot.transform,
                    true,
                    "JourneyFareGateHousing");
            }

            float[] fareGateY = { -5.75f, -3.65f, -1.55f, 0.55f, 2.65f, 4.75f };
            foreach (float y in fareGateY)
            {
                CreateBlock(
                    "Orange Fare Gate",
                    new Vector2(0f, y),
                    new Vector2(0.2f, 1.25f),
                    fareGateOrange,
                    stationRoot.transform,
                    false,
                    "JourneyFareGateOrange",
                    -0.08f);
            }

            CreateBlock(
                "Concourse Down Escalator",
                new Vector2(11f, 3.6f),
                new Vector2(4f, 2.7f),
                escalatorYellow,
                stationRoot.transform,
                false,
                "JourneyEscalatorYellow",
                0.2f);
            CreateBlock(
                "Concourse Up Escalator",
                new Vector2(11f, -3.6f),
                new Vector2(4f, 2.7f),
                escalatorYellow,
                stationRoot.transform,
                false,
                "JourneyEscalatorYellow",
                0.2f);

            Vector2 escalatorCenter = new Vector2(55f, 0f);
            CreateBlock(
                "Escalator Screen Background",
                escalatorCenter,
                new Vector2(28f, 14f),
                new Color(0.025f, 0.035f, 0.05f),
                stationRoot.transform,
                false,
                "JourneySketchVoid",
                1.15f);
            CreateBlock(
                "Down Escalator Lane",
                escalatorCenter + Vector2.up * 3.2f,
                new Vector2(24f, 2.8f),
                escalatorYellow,
                stationRoot.transform,
                false,
                "JourneyEscalatorYellow",
                0.2f);
            CreateBlock(
                "Up Escalator Lane",
                escalatorCenter + Vector2.down * 3.2f,
                new Vector2(24f, 2.8f),
                escalatorYellow,
                stationRoot.transform,
                false,
                "JourneyEscalatorYellow",
                0.2f);
            CreateBlock(
                "Escalator Screen Top Wall",
                escalatorCenter + Vector2.up * 7.1f,
                new Vector2(28.2f, 0.22f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");
            CreateBlock(
                "Escalator Screen Divider",
                escalatorCenter,
                new Vector2(28.2f, 0.22f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");
            CreateBlock(
                "Escalator Screen Bottom Wall",
                escalatorCenter + Vector2.down * 7.1f,
                new Vector2(28.2f, 0.22f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");

            Vector2 platformCenter = new Vector2(120f, 0f);
            CreateBlock(
                "Platform Screen Background",
                platformCenter,
                new Vector2(30f, 21f),
                floorColor,
                stationRoot.transform,
                false,
                "JourneySketchFloor",
                1.3f);
            CreateBlock(
                "Top Platform",
                platformCenter + Vector2.up * 8f,
                new Vector2(28f, 4f),
                platformColor,
                stationRoot.transform,
                false,
                "JourneySketchPlatform",
                0.9f);
            CreateBlock(
                "Upper Direction Track",
                platformCenter + Vector2.up * 3.15f,
                new Vector2(28f, 5.7f),
                trackColor,
                stationRoot.transform,
                false,
                "JourneySketchTrack",
                1.2f);
            CreateBlock(
                "Lower Direction Track",
                platformCenter + Vector2.down * 3.15f,
                new Vector2(28f, 5.7f),
                trackColor,
                stationRoot.transform,
                false,
                "JourneySketchTrack",
                1.2f);
            CreateBlock(
                "Bottom Platform",
                platformCenter + Vector2.down * 8f,
                new Vector2(28f, 4f),
                platformColor,
                stationRoot.transform,
                false,
                "JourneySketchPlatform",
                0.9f);
            CreateBlock(
                "Top Platform Escalator Exit",
                platformCenter + Vector2.up * 8f,
                new Vector2(5f, 1.7f),
                escalatorYellow,
                stationRoot.transform,
                false,
                "JourneyEscalatorYellow",
                0.15f);
            CreateBlock(
                "Bottom Platform Escalator Exit",
                platformCenter + Vector2.down * 8f,
                new Vector2(5f, 1.7f),
                escalatorYellow,
                stationRoot.transform,
                false,
                "JourneyEscalatorYellow",
                0.15f);
            CreateBlock(
                "Platform Screen Top Wall",
                platformCenter + Vector2.up * 10.1f,
                new Vector2(28.2f, 0.22f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");
            CreateBlock(
                "Platform Screen Bottom Wall",
                platformCenter + Vector2.down * 10.1f,
                new Vector2(28.2f, 0.22f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");
            CreateBlock(
                "Platform Screen Left Wall",
                platformCenter + Vector2.left * 14.1f,
                new Vector2(0.22f, 20.2f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");
            CreateBlock(
                "Platform Screen Right Wall",
                platformCenter + Vector2.right * 14.1f,
                new Vector2(0.22f, 20.2f),
                boundaryColor,
                stationRoot.transform,
                true,
                "JourneySketchBoundary");

            CreateStationSketchLabel(
                "Concourse Label",
                "CONCOURSE",
                new Vector2(-6f, 5.7f),
                Color.white,
                stationRoot.transform);
            CreateStationSketchLabel(
                "Fare Gate Label",
                "FARE GATES",
                new Vector2(0f, -0.5f),
                Color.white,
                stationRoot.transform,
                0.055f);
            CreateStationSketchLabel(
                "Down Escalator Label",
                "DOWN ESCALATOR",
                escalatorCenter + Vector2.up * 3.2f,
                new Color(0.08f, 0.08f, 0.05f),
                stationRoot.transform);
            CreateStationSketchLabel(
                "Up Escalator Label",
                "UP ESCALATOR",
                escalatorCenter + Vector2.down * 3.2f,
                new Color(0.08f, 0.08f, 0.05f),
                stationRoot.transform);
            CreateStationSketchLabel(
                "Top Platform Label",
                "PLATFORM",
                platformCenter + Vector2.up * 8f,
                Color.white,
                stationRoot.transform);
            CreateStationSketchLabel(
                "Bottom Platform Label",
                "PLATFORM",
                platformCenter + Vector2.down * 8f,
                Color.white,
                stationRoot.transform);
            CreateStationSketchLabel(
                "Upper Direction Label",
                "DIRECTION A",
                platformCenter + Vector2.up * 3.15f,
                new Color(0.72f, 0.75f, 0.8f),
                stationRoot.transform);
            CreateStationSketchLabel(
                "Lower Direction Label",
                "DIRECTION B",
                platformCenter + Vector2.down * 3.15f,
                new Color(0.72f, 0.75f, 0.8f),
                stationRoot.transform);

            GridNavigation2D concourseNavigation = CreateJourneyNavigation(
                "Concourse Navigation",
                Vector2.zero,
                80,
                44,
                0.35f,
                stationRoot.transform);
            GridNavigation2D escalatorNavigation = CreateJourneyNavigation(
                "Escalator Navigation",
                escalatorCenter,
                84,
                44,
                0.35f,
                stationRoot.transform);
            GridNavigation2D platformNavigation = CreateJourneyNavigation(
                "Platform Navigation",
                platformCenter,
                88,
                64,
                0.35f,
                stationRoot.transform);

            Transform downEscalatorArrival = CreatePoint(
                "Down Escalator Screen Arrival",
                new Vector2(43.5f, 3.2f),
                stationRoot.transform);
            Transform platformTopArrival = CreatePoint(
                "Top Platform Center Arrival",
                new Vector2(120f, 8f),
                stationRoot.transform);
            Transform platformBottomArrival = CreatePoint(
                "Bottom Platform Center Arrival",
                new Vector2(120f, -8f),
                stationRoot.transform);
            Transform upEscalatorArrival = CreatePoint(
                "Up Escalator Screen Arrival",
                new Vector2(66.5f, -3.2f),
                stationRoot.transform);
            Transform concourseExitArrival = CreatePoint(
                "Concourse Up Escalator Arrival",
                new Vector2(11f, -3.6f),
                stationRoot.transform);

            PassengerJourneyWaypoint enterDownEscalator = CreateJourneyWaypoint(
                "Enter Down Escalator Screen",
                new Vector2(11f, 3.6f),
                PassengerJourneyWaypointAction.EnterVerticalConnector,
                0.15f,
                2,
                Vector2.left,
                stationRoot.transform);
            enterDownEscalator.ConfigureTransition(downEscalatorArrival);
            PassengerJourneyWaypoint leaveDownEscalator = CreateJourneyWaypoint(
                "Leave Down Escalator Screen",
                new Vector2(66.5f, 3.2f),
                PassengerJourneyWaypointAction.LeaveVerticalConnector,
                0f,
                2,
                Vector2.left,
                stationRoot.transform);
            leaveDownEscalator.ConfigureTransition(platformTopArrival);

            PassengerJourneyWaypoint enterTopPlatformEscalator = CreateJourneyWaypoint(
                "Enter Top Platform Escalator",
                new Vector2(120f, 8f),
                PassengerJourneyWaypointAction.EnterVerticalConnector,
                0.15f,
                2,
                Vector2.right,
                stationRoot.transform);
            enterTopPlatformEscalator.ConfigureTransition(upEscalatorArrival);
            PassengerJourneyWaypoint leaveUpperEscalator = CreateJourneyWaypoint(
                "Leave Up Escalator Screen",
                new Vector2(43.5f, -3.2f),
                PassengerJourneyWaypointAction.LeaveVerticalConnector,
                0f,
                2,
                Vector2.right,
                stationRoot.transform);
            leaveUpperEscalator.ConfigureTransition(concourseExitArrival);

            PassengerJourneyWaypoint enterTransferEscalator = CreateJourneyWaypoint(
                "Enter Transfer Escalator",
                new Vector2(120f, 8f),
                PassengerJourneyWaypointAction.EnterTransferPassage,
                0.2f,
                2,
                Vector2.up,
                stationRoot.transform);
            enterTransferEscalator.ConfigureTransition(platformBottomArrival);

            PassengerJourneyWaypoint enterBottomPlatformEscalator = CreateJourneyWaypoint(
                "Enter Bottom Platform Escalator",
                new Vector2(120f, -8f),
                PassengerJourneyWaypointAction.EnterVerticalConnector,
                0.15f,
                2,
                Vector2.left,
                stationRoot.transform);
            enterBottomPlatformEscalator.ConfigureTransition(upEscalatorArrival);
            PassengerJourneyWaypoint leavePostTransferEscalator = CreateJourneyWaypoint(
                "Leave Post Transfer Up Escalator",
                new Vector2(43.5f, -3.2f),
                PassengerJourneyWaypointAction.LeaveVerticalConnector,
                0f,
                2,
                Vector2.right,
                stationRoot.transform);
            leavePostTransferEscalator.ConfigureTransition(concourseExitArrival);

            PassengerJourneyWaypoint[] boardingRoute =
            {
                CreateJourneyWaypoint(
                    "Approach Entry Gate",
                    new Vector2(-5f, 2.65f),
                    PassengerJourneyWaypointAction.Walk,
                    0f,
                    4,
                    Vector2.left,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Tap Entry Fare Gate",
                    new Vector2(0f, 2.65f),
                    PassengerJourneyWaypointAction.TapEntryGate,
                    0.35f,
                    1,
                    Vector2.left,
                    stationRoot.transform),
                CreateJourneyWaypoint(
                    "Cross Paid Concourse",
                    new Vector2(6f, 2.65f),
                    PassengerJourneyWaypointAction.Walk,
                    0f,
                    4,
                    Vector2.left,
                    stationRoot.transform),
                enterDownEscalator,
                leaveDownEscalator,
                CreateJourneyWaypoint(
                    "Reach Upper Direction Platform",
                    upperTrain.Doorways[0].OutsidePoint.position,
                    PassengerJourneyWaypointAction.ReachPlatform,
                    0f,
                    4,
                    Vector2.up,
                    stationRoot.transform)
            };

            PassengerJourneyWaypoint[] exitRoute = CreateSeparatedExitRoute(
                "Upper",
                new Vector2(128f, 8f),
                enterTopPlatformEscalator,
                leaveUpperEscalator,
                stationRoot.transform);

            PassengerDoorway transferDoorway =
                lowerTrain.Doorways[Mathf.Min(3, lowerTrain.Doorways.Count - 1)];
            PassengerJourneyWaypoint[] transferRoute =
            {
                CreateJourneyWaypoint(
                    "Clear Upper Transfer Platform",
                    new Vector2(128f, 8f),
                    PassengerJourneyWaypointAction.ClearPlatform,
                    0f,
                    4,
                    Vector2.left,
                    stationRoot.transform),
                enterTransferEscalator,
                CreateJourneyWaypoint(
                    "Reach Lower Direction Platform",
                    transferDoorway.OutsidePoint.position,
                    PassengerJourneyWaypointAction.ReachTransferPlatform,
                    0f,
                    4,
                    Vector2.down,
                    stationRoot.transform)
            };

            PassengerJourneyWaypoint[] postTransferExitRoute = CreateSeparatedExitRoute(
                "Lower",
                new Vector2(112f, -8f),
                enterBottomPlatformEscalator,
                leavePostTransferEscalator,
                stationRoot.transform);

            PassengerIntentCoordinator coordinator =
                upperTrain.Center.GetComponent<PassengerIntentCoordinator>();
            if (coordinator == null)
            {
                coordinator =
                    upperTrain.Center.gameObject.AddComponent<PassengerIntentCoordinator>();
            }

            PassengerStationRoutePrototype route =
                stationRoot.AddComponent<PassengerStationRoutePrototype>();
            route.Configure(
                boardingRoute,
                exitRoute,
                transferRoute,
                new[] { concourseNavigation, escalatorNavigation, platformNavigation },
                coordinator,
                transferDoorway,
                lowerDoorCycle,
                lowerTrain.Center,
                postTransferExitRoute);
            return route;
        }

        private static PassengerJourneyWaypoint[] CreateSeparatedExitRoute(
            string prefix,
            Vector2 clearPlatformPosition,
            PassengerJourneyWaypoint enterEscalator,
            PassengerJourneyWaypoint leaveEscalator,
            Transform parent)
        {
            return new[]
            {
                CreateJourneyWaypoint(
                    "Clear " + prefix + " Arrival Platform",
                    clearPlatformPosition,
                    PassengerJourneyWaypointAction.ClearPlatform,
                    0f,
                    4,
                    Vector2.right,
                    parent),
                enterEscalator,
                leaveEscalator,
                CreateJourneyWaypoint(
                    "Approach " + prefix + " Exit Gate",
                    new Vector2(6f, -3.65f),
                    PassengerJourneyWaypointAction.Walk,
                    0f,
                    4,
                    Vector2.right,
                    parent),
                CreateJourneyWaypoint(
                    "Tap " + prefix + " Exit Fare Gate",
                    new Vector2(0f, -3.65f),
                    PassengerJourneyWaypointAction.TapExitGate,
                    0.35f,
                    1,
                    Vector2.right,
                    parent),
                CreateJourneyWaypoint(
                    "Leave Station After " + prefix + " Route",
                    new Vector2(-12f, -5.5f),
                    PassengerJourneyWaypointAction.LeaveStation,
                    0f,
                    4,
                    Vector2.right,
                    parent)
            };
        }

        private static GridNavigation2D CreateJourneyNavigation(
            string name,
            Vector2 position,
            int columns,
            int rows,
            float cellSize,
            Transform parent)
        {
            GameObject navigationObject = new GameObject(name);
            navigationObject.transform.SetParent(parent);
            navigationObject.transform.position = position;
            GridNavigation2D navigation =
                navigationObject.AddComponent<GridNavigation2D>();
            navigation.ConfigureDimensions(columns, rows, cellSize);
            return navigation;
        }

        private static TextMesh CreateStationSketchLabel(
            string name,
            string text,
            Vector2 position,
            Color color,
            Transform parent,
            float characterSize = 0.08f)
        {
            GameObject labelObject = new GameObject(name);
            labelObject.transform.SetParent(parent);
            labelObject.transform.position = new Vector3(position.x, position.y, -0.42f);

            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = characterSize;
            label.fontSize = 48;
            label.color = color;
            return label;
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
            GameObject marker = new GameObject(name);
            marker.transform.SetParent(parent);
            marker.transform.position = position;
            PassengerJourneyWaypoint waypoint =
                marker.AddComponent<PassengerJourneyWaypoint>();
            waypoint.Configure(action, dwellDuration, capacity, queueDirection);
            return waypoint;
        }

        private static Transform CreateJourneyPassenger(
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
                route != null ? route.transform : car.Center,
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
            return passengerObject.transform;
        }

        private static void ValidateJourneyScene(
            Scene scene,
            int expectedWaypointCount = 14,
            int expectedJourneyCount = 4)
        {
            PassengerStationRoutePrototype route =
                UnityEngine.Object.FindFirstObjectByType<PassengerStationRoutePrototype>();
            var journeyList = new List<PassengerStationJourneyPrototype>();
            var waypointList = new List<PassengerJourneyWaypoint>();
            GameObject[] sceneRoots = scene.GetRootGameObjects();
            for (int index = 0; index < sceneRoots.Length; index++)
            {
                journeyList.AddRange(
                    sceneRoots[index].GetComponentsInChildren<PassengerStationJourneyPrototype>(
                        true));
                waypointList.AddRange(
                    sceneRoots[index].GetComponentsInChildren<PassengerJourneyWaypoint>(true));
            }

            PassengerStationJourneyPrototype[] journeys = journeyList.ToArray();
            PassengerJourneyWaypoint[] waypoints = waypointList.ToArray();

            var errors = new List<string>();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                errors.Add("journey scene is not loaded");
            }
            if (route == null)
            {
                errors.Add("station route is missing");
            }
            if (journeys.Length != expectedJourneyCount)
            {
                errors.Add(
                    "expected " + expectedJourneyCount +
                    " journey passengers");
            }
            if (waypoints.Length != expectedWaypointCount)
            {
                errors.Add(
                    "expected " + expectedWaypointCount +
                    " station journey waypoints");
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
            Transform parent,
            bool createExteriorPlatform = true,
            float serviceSide = -1f)
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
            if (createExteriorPlatform)
            {
                CreatePlatforms(carRoot.transform, center);
            }

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
                openRight,
                serviceSide);

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
            bool openRight,
            float serviceSide)
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
                PassengerDoorway topDoorway = CreateDoorway(
                    parent,
                    center,
                    localX,
                    1f,
                    navigation,
                    doorColor,
                    serviceSide > 0f);
                PassengerDoorway bottomDoorway = CreateDoorway(
                    parent,
                    center,
                    localX,
                    -1f,
                    navigation,
                    doorColor,
                    serviceSide <= 0f);
                PassengerDoorway serviceDoorway = serviceSide > 0f
                    ? topDoorway
                    : bottomDoorway;
                doorways.Add(serviceDoorway);
                doors.Add(serviceDoorway.Door);
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
