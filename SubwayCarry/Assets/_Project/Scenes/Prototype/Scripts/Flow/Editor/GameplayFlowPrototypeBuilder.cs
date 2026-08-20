#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using SubwayCarry.AI;
using SubwayCarry.Delivery;
using SubwayCarry.Delivery.Mocks;
using SubwayCarry.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SubwayCarry.Prototype.Editor
{
    public static class GameplayFlowPrototypeBuilder
    {
        public const string ScenePath =
            "Assets/_Project/Scenes/Prototype/GameplayFlow_Prototype.unity";

        private const string MaterialFolder =
            "Assets/_Project/Art/PrototypeMaterials";
        private const string DeliveryDataPath =
            "Assets/_Project/Scenes/Prototype/Data/Delivery/DeliveryData_1-1.asset";

        private const float HubCenterX = -100f;
        private const float DepartureConcourseCenterX = -60f;
        private const float DeparturePlatformCenterX = -20f;
        private const float TravelTrainCenterX = 20f;
        private const float DestinationPlatformCenterX = 60f;
        private const float DestinationConcourseCenterX = 100f;

        private const float CarWidth = 24.6f;
        private const float CarHeight = 4.6f;
        private const float WallThickness = 0.2f;
        private const float SideWallY = 2.4f;
        private const float DoorWidth = 1.9f;
        private const float SeatY = 1.55f;
        private const float PlatformY = -4.05f;
        private static readonly float[] TrainDoorLocalX =
        {
            -8.4f,
            -2.8f,
            2.8f,
            8.4f
        };

        [MenuItem("SubwayCarry/Prototype/Build Gameplay Flow Scene")]
        public static void Build()
        {
            EnsureFolder("Assets/_Project/Scenes", "Prototype");

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            GameObject root = new GameObject("Gameplay Flow Prototype");

            Camera camera = CreateCamera(root.transform);
            PlayerBuildData player = CreatePlayer(root.transform);
            PrototypeCameraFollow cameraFollow =
                camera.gameObject.AddComponent<PrototypeCameraFollow>();
            cameraFollow.Configure(player.Controller.transform);

            ServiceBuildData services = CreateServices(root.transform, player);
            GameObject flowObject = new GameObject("Prototype Game Flow");
            flowObject.transform.SetParent(root.transform);
            PrototypeGameFlowController flow =
                flowObject.AddComponent<PrototypeGameFlowController>();

            HubBuildData hub = CreateGachonHub(root.transform);
            AccessHallBuildData departureConcourse = CreateAccessHall(
                "Gachon Paid Access Hall",
                new Vector2(DepartureConcourseCenterX, 0f),
                "GACHON UNIVERSITY - PAID AREA",
                false,
                root.transform);
            PlatformBuildData departure = CreatePlatformStation(
                "Gachon Departure Platform",
                new Vector2(DeparturePlatformCenterX, 0f),
                "GACHON UNIVERSITY",
                false,
                root.transform);
            RideTrainBuildData travel = CreateTravelTrainZone(
                new Vector2(TravelTrainCenterX, 0f),
                root.transform);
            PlatformBuildData destination = CreatePlatformStation(
                "Jeongja Destination Platform",
                new Vector2(DestinationPlatformCenterX, 0f),
                "JEONGJA",
                true,
                root.transform);
            AccessHallBuildData destinationConcourse = CreateAccessHall(
                "Jeongja Access Hall",
                new Vector2(DestinationConcourseCenterX, 0f),
                "JEONGJA - EXIT CONCOURSE",
                true,
                root.transform);

            flow.Configure(
                player.Controller,
                player.Posture,
                player.Durability,
                cameraFollow,
                services.Delivery,
                services.Economy,
                services.Transit,
                hub.Spawn,
                departureConcourse.Spawn,
                departure.PlatformSpawn,
                travel.TrainSpawn,
                destination.TrainSpawn,
                destinationConcourse.Spawn,
                departure.Doors,
                destination.Doors);

            hub.MapTerminal.Configure(
                flow,
                PrototypeInteractionAction.OpenDeliveryMap,
                SubwayCarry.Core.Contracts.InteractionKind.StationFacility,
                "E: 배송 목록 열기");
            hub.DepartureGate.Configure(
                flow,
                PrototypeInteractionAction.TapDepartureGate,
                SubwayCarry.Core.Contracts.InteractionKind.StationFacility,
                "E: 교통카드 찍기");
            ConfigureAreaTriggers(
                departureConcourse.PlatformAccessTriggers,
                flow,
                PrototypeAreaAction.EnterDeparturePlatform);
            ConfigureAreaTriggers(
                departure.DoorwayTriggers,
                flow,
                PrototypeAreaAction.BoardTrain);
            ConfigureAreaTriggers(
                destination.DoorwayTriggers,
                flow,
                PrototypeAreaAction.LeaveTrainAtDestination);
            ConfigureAreaTriggers(
                destination.ConcourseAccessTriggers,
                flow,
                PrototypeAreaAction.EnterDestinationConcourse);
            destinationConcourse.ExitGate.Configure(
                flow,
                PrototypeInteractionAction.CompleteAtDestinationGate,
                SubwayCarry.Core.Contracts.InteractionKind.StationFacility,
                "E: 개찰구로 나가기");

            EditorSceneManager.SaveScene(scene, ScenePath);
            ValidateScene(scene);
            Selection.activeGameObject = player.Controller.gameObject;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            Debug.Log("[Gameplay Flow Prototype] Scene created: " + ScenePath);
        }

        [MenuItem("SubwayCarry/Prototype/Validate Gameplay Flow Scene")]
        public static void ValidateSavedScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ValidateScene(scene);
            Debug.Log("[Gameplay Flow Prototype] Scene validation passed.");
        }

        private static PlayerBuildData CreatePlayer(Transform parent)
        {
            GameObject playerObject = CreateBlock(
                "Player",
                new Vector2(HubCenterX - 9f, -3.8f),
                new Vector2(0.72f, 0.88f),
                "Player",
                parent,
                false,
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

            PlayerPosture posture = playerObject.AddComponent<PlayerPosture>();
            PlayerController controller = playerObject.AddComponent<PlayerController>();
            playerObject.AddComponent<PlayerInteraction>();
            PackageDurability durability = playerObject.AddComponent<PackageDurability>();
            PlayerCollisionImpact collisionImpact =
                playerObject.AddComponent<PlayerCollisionImpact>();
            SetObjectReference(
                collisionImpact,
                "heldPackageSource",
                durability);

            GameObject package = CreateBlock(
                "Cake Package",
                (Vector2)playerObject.transform.position + new Vector2(0f, -0.58f),
                new Vector2(0.56f, 0.28f),
                "PrioritySeat",
                playerObject.transform,
                false,
                -0.2f);
            package.transform.localPosition = new Vector3(0f, -0.58f, -0.2f);
            CreateLabel(
                "PLAYER",
                (Vector2)playerObject.transform.position + Vector2.down * 0.9f,
                playerObject.transform,
                0.055f,
                -0.3f);

            return new PlayerBuildData
            {
                Controller = controller,
                Posture = posture,
                Durability = durability
            };
        }

        private static ServiceBuildData CreateServices(
            Transform parent,
            PlayerBuildData player)
        {
            GameObject serviceRoot = new GameObject("Gameplay Services");
            serviceRoot.transform.SetParent(parent);

            EconomyService economy = serviceRoot.AddComponent<EconomyService>();
            SchoolServiceShop shop = serviceRoot.AddComponent<SchoolServiceShop>();
            MockTransitProgressProvider transit =
                serviceRoot.AddComponent<MockTransitProgressProvider>();
            DeliveryService delivery = serviceRoot.AddComponent<DeliveryService>();

            DeliveryData deliveryData =
                AssetDatabase.LoadAssetAtPath<DeliveryData>(DeliveryDataPath);
            if (deliveryData == null)
            {
                throw new InvalidOperationException(
                    "Delivery data is missing: " + DeliveryDataPath);
            }

            SetObjectReference(shop, "economyService", economy);
            SetObjectReference(delivery, "economyService", economy);
            SetObjectReference(delivery, "serviceShop", shop);
            SetObjectReference(
                delivery,
                "packageDurabilityProviderSource",
                player.Durability);
            SetObjectReference(
                delivery,
                "transitProgressProviderSource",
                transit);
            SetObjectReferenceList(delivery, "catalog", deliveryData);

            return new ServiceBuildData
            {
                Economy = economy,
                Delivery = delivery,
                Transit = transit
            };
        }

        private static HubBuildData CreateGachonHub(Transform parent)
        {
            Vector2 center = new Vector2(HubCenterX, 0f);
            GameObject hubRoot = new GameObject("Gachon Hub - Unpaid Concourse");
            hubRoot.transform.SetParent(parent);

            CreateBlock(
                "Unpaid Concourse Floor",
                center,
                new Vector2(28f, 12f),
                "Floor",
                hubRoot.transform,
                false,
                1f);
            CreateBoundaryRect(hubRoot.transform, center, new Vector2(28f, 12f));
            CreateLabel(
                "GACHON UNIVERSITY STATION - UNPAID CONCOURSE",
                center + new Vector2(0f, 4.8f),
                hubRoot.transform,
                0.105f);

            Vector2 terminalPosition = center + new Vector2(-6.5f, 0.4f);
            CreateLabel(
                "DELIVERY MAP",
                terminalPosition + Vector2.up * 1.75f,
                hubRoot.transform,
                0.07f);
            GameObject terminal = CreateBlock(
                "Delivery Map Terminal",
                terminalPosition,
                new Vector2(2.4f, 2.6f),
                "DoorOpening",
                hubRoot.transform,
                false,
                -0.15f);
            BoxCollider2D terminalTrigger = terminal.AddComponent<BoxCollider2D>();
            terminalTrigger.isTrigger = true;
            terminalTrigger.size = new Vector2(1.7f, 1.9f);
            PrototypeInteractable mapTerminal =
                terminal.AddComponent<PrototypeInteractable>();

            PrototypeInteractable departureGate = CreateFareGate(
                "Departure Fare Gate",
                center + new Vector2(8.4f, 0f),
                "TAP TRANSIT CARD",
                hubRoot.transform);
            CreateTicketMachineBank(
                center + new Vector2(-10.8f, -3.4f),
                hubRoot.transform);
            CreateStationColumns(
                center,
                new[] { -11.4f, -2.5f, 3.5f, 11.4f },
                hubRoot.transform);

            Transform spawn = CreatePoint(
                "Hub Spawn",
                center + new Vector2(-9f, -3.8f),
                hubRoot.transform);

            return new HubBuildData
            {
                Spawn = spawn,
                MapTerminal = mapTerminal,
                DepartureGate = departureGate
            };
        }

        private static AccessHallBuildData CreateAccessHall(
            string name,
            Vector2 center,
            string stationLabel,
            bool destination,
            Transform parent)
        {
            GameObject hallRoot = new GameObject(name);
            hallRoot.transform.SetParent(parent);

            CreateBlock(
                "Paid Access Hall Floor",
                center,
                new Vector2(28f, 12f),
                "Floor",
                hallRoot.transform,
                false,
                1f);
            CreateBoundaryRect(hallRoot.transform, center, new Vector2(28f, 12f));
            CreateLabel(
                stationLabel,
                center + new Vector2(0f, 4.9f),
                hallRoot.transform,
                0.1f);
            CreateLabel(
                destination
                    ? "PLATFORM ACCESS / EXIT GATE"
                    : "CHOOSE STAIRS, ESCALATOR, OR ELEVATOR",
                center + new Vector2(0f, 3.85f),
                hallRoot.transform,
                0.062f);

            string[] routeNames = { "STAIRS", "ESCALATOR", "ELEVATOR" };
            var platformAccessTriggers = new List<PrototypeAreaTrigger>();
            for (int index = 0; index < routeNames.Length; index++)
            {
                PrototypeAreaTrigger trigger = CreateAccessBay(
                    routeNames[index],
                    center + new Vector2((index - 1) * 7f, 1.1f),
                    hallRoot.transform,
                    !destination);
                if (trigger != null)
                {
                    platformAccessTriggers.Add(trigger);
                }
            }

            PrototypeInteractable exitGate = null;
            if (destination)
            {
                exitGate = CreateFareGate(
                    "Destination Fare Gate",
                    center + new Vector2(9.3f, -2.7f),
                    "EXIT TO STREET",
                    hallRoot.transform);
            }

            CreateStationColumns(
                center,
                new[] { -11.6f, 11.6f },
                hallRoot.transform);
            Transform spawn = CreatePoint(
                destination ? "Destination Concourse Spawn" : "Departure Concourse Spawn",
                center + new Vector2(-9.2f, -3.6f),
                hallRoot.transform);

            return new AccessHallBuildData
            {
                Spawn = spawn,
                PlatformAccessTriggers = platformAccessTriggers.ToArray(),
                ExitGate = exitGate
            };
        }

        private static PlatformBuildData CreatePlatformStation(
            string name,
            Vector2 center,
            string stationLabel,
            bool destination,
            Transform parent)
        {
            GameObject stationRoot = new GameObject(name);
            stationRoot.transform.SetParent(parent);

            Vector2 carCenter = center + new Vector2(0f, 2.3f);
            GameObject platformRoot = new GameObject(
                destination
                    ? "Destination Platform Environment"
                    : "Departure Platform Environment");
            platformRoot.transform.SetParent(stationRoot.transform);
            Vector2 platformCenter = carCenter + Vector2.up * PlatformY;

            CreateBlock(
                "Platform",
                platformCenter,
                new Vector2(25f, 3f),
                "Platform",
                platformRoot.transform,
                false,
                1.2f);
            CreateBlock(
                "Platform Safety Line",
                carCenter + new Vector2(0f, -2.8f),
                new Vector2(24.8f, 0.12f),
                "PlatformSafetyLine",
                platformRoot.transform,
                false,
                -0.05f);
            CreatePlatformBoundary(platformRoot.transform, platformCenter);
            CreateLabel(
                stationLabel + (destination ? " - ARRIVAL" : " - DEPARTURE"),
                platformCenter + new Vector2(0f, -1.12f),
                platformRoot.transform,
                0.085f);

            var concourseAccessTriggers = new List<PrototypeAreaTrigger>();
            string[] routeNames = { "STAIRS", "ESCALATOR", "ELEVATOR" };
            for (int index = 0; index < routeNames.Length; index++)
            {
                PrototypeAreaTrigger trigger = CreateAccessBay(
                    routeNames[index],
                    platformCenter + new Vector2((index - 1) * 7f, -0.45f),
                    platformRoot.transform,
                    destination);
                if (trigger != null)
                {
                    concourseAccessTriggers.Add(trigger);
                }
            }

            TrainBuildData train = CreateTrainCar(
                destination ? "Destination Train Car" : "Departure Train Car",
                carCenter,
                stationRoot.transform);
            var doorwayTriggers = new List<PrototypeAreaTrigger>();
            for (int index = 0; index < TrainDoorLocalX.Length; index++)
            {
                Vector2 triggerPosition = carCenter + new Vector2(
                    TrainDoorLocalX[index],
                    destination ? -2.95f : -1.15f);
                doorwayTriggers.Add(CreateAreaTrigger(
                    (destination ? "Leave Train" : "Board Train") +
                    " Door " + (index + 1) + " Trigger",
                    triggerPosition,
                    new Vector2(1.55f, 1.25f),
                    stationRoot.transform));
            }

            Transform platformSpawn = CreatePoint(
                destination ? "Destination Platform Spawn" : "Departure Platform Spawn",
                platformCenter + new Vector2(-10.2f, 0.15f),
                platformRoot.transform);

            return new PlatformBuildData
            {
                PlatformSpawn = platformSpawn,
                TrainSpawn = train.InteriorSpawn,
                Doors = train.Doors,
                DoorwayTriggers = doorwayTriggers.ToArray(),
                ConcourseAccessTriggers = concourseAccessTriggers.ToArray()
            };
        }

        private static RideTrainBuildData CreateTravelTrainZone(
            Vector2 center,
            Transform parent)
        {
            GameObject zoneRoot = new GameObject("Travel Train Interior");
            zoneRoot.transform.SetParent(parent);

            GameObject tunnelRoot = new GameObject("Moving Tunnel Visual");
            tunnelRoot.transform.SetParent(zoneRoot.transform);
            tunnelRoot.transform.position = center;
            CreateBlock(
                "Tunnel Backdrop",
                center,
                new Vector2(32f, 16f),
                "DoorOpening",
                tunnelRoot.transform,
                false,
                1.6f);

            var movingStrips = new List<Transform>();
            for (int index = 0; index < 6; index++)
            {
                float x = center.x - 13.5f + index * 5.4f;
                foreach (float y in new[] { center.y - 4.8f, center.y + 4.8f })
                {
                    GameObject strip = CreateBlock(
                        "Tunnel Light Strip",
                        new Vector2(x, y),
                        new Vector2(0.42f, 2.6f),
                        "ConnectorThreshold",
                        tunnelRoot.transform,
                        false,
                        0.7f);
                    movingStrips.Add(strip.transform);
                }
            }

            PrototypeTunnelMotion tunnelMotion =
                tunnelRoot.AddComponent<PrototypeTunnelMotion>();
            tunnelMotion.Configure(movingStrips.ToArray(), 16f, 7f);
            CreateLabel(
                "TRAIN IN MOTION",
                center + new Vector2(0f, 5.8f),
                zoneRoot.transform,
                0.08f);

            TrainBuildData train = CreateTrainCar(
                "Travel Train Car",
                center,
                zoneRoot.transform);
            return new RideTrainBuildData
            {
                TrainSpawn = train.InteriorSpawn
            };
        }

        private static TrainBuildData CreateTrainCar(
            string name,
            Vector2 center,
            Transform parent)
        {
            GameObject trainRoot = new GameObject(name);
            trainRoot.transform.SetParent(parent);
            trainRoot.transform.position = center;

            CreateBlock(
                "Train Floor",
                center,
                new Vector2(CarWidth, CarHeight),
                "Floor",
                trainRoot.transform,
                false,
                1f);
            CreateBlock(
                "Train Top Wall",
                center + Vector2.up * SideWallY,
                new Vector2(CarWidth, WallThickness),
                "Wall",
                trainRoot.transform,
                true);
            CreateBlock(
                "Train Left End Wall",
                center + Vector2.left * (CarWidth * 0.5f + WallThickness * 0.5f),
                new Vector2(WallThickness, CarHeight + WallThickness),
                "Wall",
                trainRoot.transform,
                true);
            CreateBlock(
                "Train Right End Wall",
                center + Vector2.right * (CarWidth * 0.5f + WallThickness * 0.5f),
                new Vector2(WallThickness, CarHeight + WallThickness),
                "Wall",
                trainRoot.transform,
                true);
            CreateTrainLowerWallSegments(trainRoot.transform, center);

            var doors = new List<TrainDoorController>();
            for (int index = 0; index < TrainDoorLocalX.Length; index++)
            {
                doors.Add(CreateTrainDoor(
                    "Lower Door " + (index + 1),
                    center + new Vector2(TrainDoorLocalX[index], -SideWallY),
                    trainRoot.transform));
            }

            foreach (float seatX in new[] { -5.6f, 0f, 5.6f })
            {
                CreateTrainSeat(
                    trainRoot.transform,
                    center + new Vector2(seatX, SeatY),
                    false);
                CreateTrainSeat(
                    trainRoot.transform,
                    center + new Vector2(seatX, -SeatY),
                    false);
            }
            CreateTrainSeat(
                trainRoot.transform,
                center + new Vector2(10.85f, SeatY),
                true);
            CreateTrainSeat(
                trainRoot.transform,
                center + new Vector2(10.85f, -SeatY),
                true);

            CreateLabel(
                "SUBWAY CARRY",
                center + new Vector2(0f, 1.98f),
                trainRoot.transform,
                0.052f,
                -0.15f);
            Transform interiorSpawn = CreatePoint(
                name + " Interior Spawn",
                center,
                trainRoot.transform);
            return new TrainBuildData
            {
                InteriorSpawn = interiorSpawn,
                Doors = doors.ToArray()
            };
        }

        private static void CreateTrainLowerWallSegments(
            Transform parent,
            Vector2 center)
        {
            float segmentStart = -CarWidth * 0.5f;
            const float openingHalfWidth = DoorWidth * 0.5f + 0.05f;
            foreach (float doorX in TrainDoorLocalX)
            {
                CreateHorizontalWallSegment(
                    parent,
                    center,
                    segmentStart,
                    doorX - openingHalfWidth,
                    -SideWallY,
                    "Train Bottom Wall");
                segmentStart = doorX + openingHalfWidth;
            }

            CreateHorizontalWallSegment(
                parent,
                center,
                segmentStart,
                CarWidth * 0.5f,
                -SideWallY,
                "Train Bottom Wall");
        }

        private static void CreateHorizontalWallSegment(
            Transform parent,
            Vector2 center,
            float startX,
            float endX,
            float localY,
            string name)
        {
            float width = endX - startX;
            if (width <= 0.01f)
            {
                return;
            }

            CreateBlock(
                name,
                center + new Vector2((startX + endX) * 0.5f, localY),
                new Vector2(width, WallThickness),
                "Wall",
                parent,
                true);
        }

        private static TrainDoorController CreateTrainDoor(
            string name,
            Vector2 position,
            Transform parent)
        {
            GameObject doorRoot = new GameObject(name);
            doorRoot.transform.SetParent(parent);
            doorRoot.transform.position = position;

            CreateBlock(
                "Door Opening",
                position,
                new Vector2(DoorWidth, WallThickness),
                "DoorOpening",
                doorRoot.transform,
                false,
                -0.1f);
            GameObject leftPanel = CreateBlock(
                "Left Panel",
                position + Vector2.left * 0.46f,
                new Vector2(0.9f, WallThickness),
                "Door",
                doorRoot.transform,
                false,
                -0.2f);
            GameObject rightPanel = CreateBlock(
                "Right Panel",
                position + Vector2.right * 0.46f,
                new Vector2(0.9f, WallThickness),
                "Door",
                doorRoot.transform,
                false,
                -0.2f);
            AddKinematicDoorPanel(leftPanel);
            AddKinematicDoorPanel(rightPanel);

            TrainDoorController controller =
                doorRoot.AddComponent<TrainDoorController>();
            controller.Configure(leftPanel.transform, rightPanel.transform);
            SetFloatValue(controller, "transitionDuration", 1.2f);
            return controller;
        }

        private static void AddKinematicDoorPanel(GameObject panel)
        {
            Rigidbody2D body = panel.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            panel.AddComponent<BoxCollider2D>();
        }

        private static void CreateTrainSeat(
            Transform parent,
            Vector2 position,
            bool priority)
        {
            float width = priority ? 1.85f : 3.55f;
            CreateBlock(
                priority ? "Priority Seat" : "Seat",
                position,
                new Vector2(width, 0.85f),
                priority ? "PrioritySeat" : "Seat",
                parent,
                true);

            float dividerOffset = width * 0.5f + 0.06f;
            foreach (float side in new[] { -1f, 1f })
            {
                CreateBlock(
                    "Seat Side Wall",
                    position + Vector2.right * side * dividerOffset,
                    new Vector2(0.12f, priority ? 1.45f : 1.12f),
                    "SeatDivider",
                    parent,
                    true,
                    -0.05f);
            }
        }

        private static void CreatePlatformBoundary(
            Transform parent,
            Vector2 platformCenter)
        {
            const float platformHalfWidth = 12.5f;
            const float platformHalfHeight = 1.5f;
            CreateBlock(
                "Platform Left Boundary",
                platformCenter + Vector2.left * platformHalfWidth,
                new Vector2(0.22f, 3f),
                "Wall",
                parent,
                true);
            CreateBlock(
                "Platform Right Boundary",
                platformCenter + Vector2.right * platformHalfWidth,
                new Vector2(0.22f, 3f),
                "Wall",
                parent,
                true);

            float[] starts = { -12.5f, -5.4f, 1.6f, 8.6f };
            float[] ends = { -8.6f, -1.6f, 5.4f, 12.5f };
            for (int index = 0; index < starts.Length; index++)
            {
                float width = ends[index] - starts[index];
                CreateBlock(
                    "Platform Rear Wall",
                    platformCenter + new Vector2(
                        (starts[index] + ends[index]) * 0.5f,
                        -platformHalfHeight),
                    new Vector2(width, 0.22f),
                    "Wall",
                    parent,
                    true);
            }
        }

        private static PrototypeAreaTrigger CreateAccessBay(
            string routeName,
            Vector2 center,
            Transform parent,
            bool createTrigger)
        {
            GameObject bayRoot = new GameObject(routeName + " Access Bay");
            bayRoot.transform.SetParent(parent);
            bayRoot.transform.position = center;
            CreateBlock(
                routeName + " Landing",
                center,
                new Vector2(3.5f, 2.35f),
                "Connector",
                bayRoot.transform,
                false,
                0.5f);

            if (routeName == "STAIRS")
            {
                for (int index = 0; index < 5; index++)
                {
                    CreateBlock(
                        "Stair Step " + (index + 1),
                        center + new Vector2(0f, -0.72f + index * 0.36f),
                        new Vector2(2.9f, 0.1f),
                        "ConnectorThreshold",
                        bayRoot.transform,
                        false,
                        -0.08f);
                }
            }
            else if (routeName == "ESCALATOR")
            {
                CreateBlock(
                    "Escalator Belt",
                    center,
                    new Vector2(3f, 0.72f),
                    "Floor",
                    bayRoot.transform,
                    false,
                    -0.08f,
                    12f);
                foreach (float side in new[] { -0.72f, 0.72f })
                {
                    CreateBlock(
                        "Escalator Rail",
                        center + Vector2.up * side,
                        new Vector2(3.2f, 0.12f),
                        "ConnectorThreshold",
                        bayRoot.transform,
                        false,
                        -0.12f,
                        12f);
                }
            }
            else
            {
                CreateBlock(
                    "Elevator Left Door",
                    center + Vector2.left * 0.62f,
                    new Vector2(1.12f, 1.8f),
                    "Door",
                    bayRoot.transform,
                    false,
                    -0.08f);
                CreateBlock(
                    "Elevator Right Door",
                    center + Vector2.right * 0.62f,
                    new Vector2(1.12f, 1.8f),
                    "Door",
                    bayRoot.transform,
                    false,
                    -0.08f);
            }

            CreateLabel(
                routeName,
                center + Vector2.up * 1.55f,
                bayRoot.transform,
                0.057f);
            return createTrigger
                ? CreateAreaTrigger(
                    routeName + " Access Trigger",
                    center,
                    new Vector2(3.15f, 2.1f),
                    bayRoot.transform)
                : null;
        }

        private static PrototypeAreaTrigger CreateAreaTrigger(
            string name,
            Vector2 position,
            Vector2 size,
            Transform parent)
        {
            GameObject triggerObject = new GameObject(name);
            triggerObject.transform.SetParent(parent);
            triggerObject.transform.position = position;
            BoxCollider2D trigger = triggerObject.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = size;
            return triggerObject.AddComponent<PrototypeAreaTrigger>();
        }

        private static void ConfigureAreaTriggers(
            PrototypeAreaTrigger[] triggers,
            PrototypeGameFlowController flow,
            PrototypeAreaAction action)
        {
            foreach (PrototypeAreaTrigger trigger in triggers)
            {
                trigger.Configure(flow, action);
            }
        }

        private static PrototypeInteractable CreateFareGate(
            string name,
            Vector2 center,
            string label,
            Transform parent)
        {
            GameObject gateRoot = new GameObject(name + " Structure");
            gateRoot.transform.SetParent(parent);
            gateRoot.transform.position = center;
            foreach (float side in new[] { -1f, 1f })
            {
                CreateBlock(
                    "Fare Gate Pylon",
                    center + Vector2.right * side * 0.9f,
                    new Vector2(0.55f, 2.8f),
                    "ConnectorThreshold",
                    gateRoot.transform,
                    true,
                    -0.1f);
            }
            CreateBlock(
                "Card Reader",
                center + new Vector2(-0.9f, 1.05f),
                new Vector2(0.72f, 0.62f),
                "Door",
                gateRoot.transform,
                false,
                -0.2f);
            CreateLabel(
                label,
                center + Vector2.up * 2.05f,
                gateRoot.transform,
                0.062f);

            GameObject interactionObject = new GameObject(name);
            interactionObject.transform.SetParent(gateRoot.transform);
            interactionObject.transform.position = center;
            BoxCollider2D trigger = interactionObject.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1.35f, 2.6f);
            return interactionObject.AddComponent<PrototypeInteractable>();
        }

        private static void CreateTicketMachineBank(
            Vector2 start,
            Transform parent)
        {
            for (int index = 0; index < 3; index++)
            {
                CreateBlock(
                    "Ticket Machine " + (index + 1),
                    start + Vector2.right * index * 1.35f,
                    new Vector2(0.95f, 1.7f),
                    "Door",
                    parent,
                    true,
                    -0.05f);
            }
        }

        private static void CreateStationColumns(
            Vector2 center,
            float[] localX,
            Transform parent)
        {
            foreach (float x in localX)
            {
                CreateBlock(
                    "Station Column",
                    center + new Vector2(x, 3.15f),
                    new Vector2(0.65f, 0.65f),
                    "Wall",
                    parent,
                    true,
                    -0.1f);
            }
        }

        private static Camera CreateCamera(Transform parent)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(parent);
            cameraObject.transform.position = new Vector3(HubCenterX, 0f, -10f);
            cameraObject.tag = "MainCamera";

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6.8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f);
            return camera;
        }

        private static void CreateBoundaryRect(
            Transform parent,
            Vector2 center,
            Vector2 size)
        {
            const float thickness = 0.22f;
            CreateBlock(
                "Wall Top",
                center + Vector2.up * size.y * 0.5f,
                new Vector2(size.x, thickness),
                "Wall",
                parent,
                true);
            CreateBlock(
                "Wall Bottom",
                center + Vector2.down * size.y * 0.5f,
                new Vector2(size.x, thickness),
                "Wall",
                parent,
                true);
            CreateBlock(
                "Wall Left",
                center + Vector2.left * size.x * 0.5f,
                new Vector2(thickness, size.y),
                "Wall",
                parent,
                true);
            CreateBlock(
                "Wall Right",
                center + Vector2.right * size.x * 0.5f,
                new Vector2(thickness, size.y),
                "Wall",
                parent,
                true);
        }

        private static GameObject CreateBlock(
            string name,
            Vector2 position,
            Vector2 size,
            string materialName,
            Transform parent,
            bool obstacle,
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

            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                MaterialFolder + "/" + materialName + ".mat");
            if (material == null)
            {
                throw new InvalidOperationException(
                    "Prototype material is missing: " + materialName);
            }

            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (obstacle)
            {
                block.AddComponent<BoxCollider2D>();
            }

            return block;
        }

        private static Transform CreatePoint(
            string name,
            Vector2 position,
            Transform parent)
        {
            GameObject point = new GameObject(name);
            point.transform.SetParent(parent);
            point.transform.position = position;
            return point.transform;
        }

        private static void CreateLabel(
            string text,
            Vector2 position,
            Transform parent,
            float characterSize,
            float z = -0.2f)
        {
            GameObject labelObject = new GameObject(text + " Label");
            labelObject.transform.SetParent(parent);
            labelObject.transform.position = new Vector3(position.x, position.y, z);

            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = characterSize;
            label.fontSize = 42;
            label.color = Color.white;
        }

        private static void SetObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    target.GetType().Name + "." + propertyName + " is missing.");
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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

        private static void SetObjectReferenceList(
            UnityEngine.Object target,
            string propertyName,
            params UnityEngine.Object[] values)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                throw new InvalidOperationException(
                    target.GetType().Name + "." + propertyName + " is missing.");
            }

            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateScene(Scene scene)
        {
            PrototypeGameFlowController flow =
                UnityEngine.Object.FindFirstObjectByType<PrototypeGameFlowController>();
            PlayerController player =
                UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            DeliveryService delivery =
                UnityEngine.Object.FindFirstObjectByType<DeliveryService>();
            GeneralPassengerPrototype[] passengers =
                UnityEngine.Object.FindObjectsByType<GeneralPassengerPrototype>(
                    FindObjectsSortMode.None);
            TrainDoorController[] trainDoors =
                UnityEngine.Object.FindObjectsByType<TrainDoorController>(
                    FindObjectsSortMode.None);
            PrototypeAreaTrigger[] areaTriggers =
                UnityEngine.Object.FindObjectsByType<PrototypeAreaTrigger>(
                    FindObjectsSortMode.None);
            PrototypeTunnelMotion[] tunnelMotions =
                UnityEngine.Object.FindObjectsByType<PrototypeTunnelMotion>(
                    FindObjectsSortMode.None);

            var errors = new List<string>();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                errors.Add("scene is not loaded");
            }
            if (flow == null)
            {
                errors.Add("flow controller is missing");
            }
            if (player == null)
            {
                errors.Add("player is missing");
            }
            if (delivery == null)
            {
                errors.Add("delivery service is missing");
            }
            if (passengers.Length > 0)
            {
                errors.Add("passenger AI must not exist in this scene");
            }
            if (trainDoors.Length != 12)
            {
                errors.Add("expected 12 train doors across three one-car trains");
            }
            if (areaTriggers.Length != 14)
            {
                errors.Add("expected 14 route and doorway area triggers");
            }
            if (tunnelMotions.Length != 1)
            {
                errors.Add("moving tunnel visual is missing or duplicated");
            }

            string[] requiredHierarchyNames =
            {
                "Gachon Hub - Unpaid Concourse",
                "Gachon Paid Access Hall",
                "Gachon Departure Platform",
                "Travel Train Interior",
                "Jeongja Destination Platform",
                "Jeongja Access Hall",
                "Departure Platform Environment",
                "Departure Train Car",
                "Destination Platform Environment",
                "Destination Train Car",
                "Destination Fare Gate"
            };
            foreach (string requiredName in requiredHierarchyNames)
            {
                if (FindSceneObject(scene, requiredName) == null)
                {
                    errors.Add("required hierarchy object is missing: " + requiredName);
                }
            }

            GameObject departureEnvironment =
                FindSceneObject(scene, "Departure Platform Environment");
            GameObject departureTrain =
                FindSceneObject(scene, "Departure Train Car");
            GameObject destinationEnvironment =
                FindSceneObject(scene, "Destination Platform Environment");
            GameObject destinationTrain =
                FindSceneObject(scene, "Destination Train Car");
            if (departureEnvironment != null && departureTrain != null &&
                (departureEnvironment.transform.IsChildOf(departureTrain.transform) ||
                 departureTrain.transform.IsChildOf(departureEnvironment.transform)))
            {
                errors.Add("departure platform and train must be sibling hierarchies");
            }
            if (destinationEnvironment != null && destinationTrain != null &&
                (destinationEnvironment.transform.IsChildOf(destinationTrain.transform) ||
                 destinationTrain.transform.IsChildOf(destinationEnvironment.transform)))
            {
                errors.Add("destination platform and train must be sibling hierarchies");
            }

            GameObject destinationGate =
                FindSceneObject(scene, "Destination Fare Gate");
            GameObject destinationHall =
                FindSceneObject(scene, "Jeongja Access Hall");
            if (destinationGate != null && destinationHall != null &&
                !destinationGate.transform.IsChildOf(destinationHall.transform))
            {
                errors.Add("destination fare gate must be inside the access hall");
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Gameplay flow scene validation failed: " +
                    string.Join(", ", errors));
            }
        }

        private static GameObject FindSceneObject(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindChildRecursive(root.transform, name);
                if (found != null)
                {
                    return found.gameObject;
                }
            }

            return null;
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent.name == name)
            {
                return parent;
            }

            for (int index = 0; index < parent.childCount; index++)
            {
                Transform found = FindChildRecursive(parent.GetChild(index), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string fullPath = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private sealed class PlayerBuildData
        {
            public PlayerController Controller;
            public PlayerPosture Posture;
            public PackageDurability Durability;
        }

        private sealed class ServiceBuildData
        {
            public EconomyService Economy;
            public DeliveryService Delivery;
            public MockTransitProgressProvider Transit;
        }

        private sealed class HubBuildData
        {
            public Transform Spawn;
            public PrototypeInteractable MapTerminal;
            public PrototypeInteractable DepartureGate;
        }

        private sealed class AccessHallBuildData
        {
            public Transform Spawn;
            public PrototypeAreaTrigger[] PlatformAccessTriggers;
            public PrototypeInteractable ExitGate;
        }

        private sealed class PlatformBuildData
        {
            public Transform PlatformSpawn;
            public Transform TrainSpawn;
            public TrainDoorController[] Doors;
            public PrototypeAreaTrigger[] DoorwayTriggers;
            public PrototypeAreaTrigger[] ConcourseAccessTriggers;
        }

        private sealed class RideTrainBuildData
        {
            public Transform TrainSpawn;
        }

        private sealed class TrainBuildData
        {
            public Transform InteriorSpawn;
            public TrainDoorController[] Doors;
        }
    }
}
#endif
