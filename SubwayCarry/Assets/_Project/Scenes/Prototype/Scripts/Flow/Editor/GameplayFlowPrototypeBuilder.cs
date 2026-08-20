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

        private const float HubCenterX = -120f;
        private const float DepartureEscalatorCenterX = -80f;
        private const float DeparturePlatformCenterX = -40f;
        private const float TravelTrainCenterX = 0f;
        private const float DestinationPlatformCenterX = 40f;
        private const float DestinationEscalatorCenterX = 80f;
        private const float DestinationConcourseCenterX = 120f;

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
            PlayerBuildData player = CreatePlayer(root.transform, camera);
            PrototypeCameraFollow cameraFollow =
                camera.gameObject.AddComponent<PrototypeCameraFollow>();
            cameraFollow.Configure(player.Controller.transform);

            ServiceBuildData services = CreateServices(root.transform, player);
            GameObject flowObject = new GameObject("Prototype Game Flow");
            flowObject.transform.SetParent(root.transform);
            PrototypeGameFlowController flow =
                flowObject.AddComponent<PrototypeGameFlowController>();

            HubBuildData hub = CreateGachonHub(root.transform);
            EscalatorBuildData departureEscalator = CreateEscalatorPassage(
                "Departure Escalator Passage",
                new Vector2(DepartureEscalatorCenterX, 0f),
                "GACHON UNIVERSITY - DOWN TO PLATFORM",
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
            EscalatorBuildData destinationEscalator = CreateEscalatorPassage(
                "Destination Escalator Passage",
                new Vector2(DestinationEscalatorCenterX, 0f),
                "JEONGJA - UP TO CONCOURSE",
                root.transform);
            AccessHallBuildData destinationConcourse = CreateAccessHall(
                "Jeongja Access Hall",
                new Vector2(DestinationConcourseCenterX, 0f),
                "JEONGJA - EXIT CONCOURSE",
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
                departureEscalator.Spawn,
                departure.PlatformSpawn,
                travel.TrainSpawn,
                destination.TrainSpawn,
                destinationEscalator.Spawn,
                destinationConcourse.Spawn,
                departure.Doors,
                destination.Doors,
                hub.DepartureGateBlocker,
                destinationConcourse.GateBlocker,
                departure.TrainArrival);

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
            hub.EscalatorEntryTrigger.Configure(
                flow,
                PrototypeAreaAction.EnterDepartureEscalator);
            departureEscalator.EndTrigger.Configure(
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
            destination.EscalatorEntryTrigger.Configure(
                flow,
                PrototypeAreaAction.EnterDestinationEscalator);
            destinationEscalator.EndTrigger.Configure(
                flow,
                PrototypeAreaAction.EnterDestinationConcourse);
            destinationConcourse.ExitGate.Configure(
                flow,
                PrototypeInteractionAction.CompleteAtDestinationGate,
                SubwayCarry.Core.Contracts.InteractionKind.StationFacility,
                "E: 교통카드 찍고 나가기");
            destinationConcourse.ExitTrigger.Configure(
                flow,
                PrototypeAreaAction.CompleteDeliveryAtDestinationExit);

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

        private static PlayerBuildData CreatePlayer(
            Transform parent,
            Camera facingCamera)
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
            GameObject facingIndicator = CreateBlock(
                "Player Facing Indicator",
                (Vector2)playerObject.transform.position + Vector2.down * 0.56f,
                new Vector2(0.42f, 0.13f),
                "Door",
                playerObject.transform,
                false,
                -0.28f);
            facingIndicator.transform.localPosition =
                new Vector3(0f, -0.56f, -0.28f);
            controller.ConfigureFacing(facingCamera, facingIndicator.transform);

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
                "Gachon Concourse Floor",
                center,
                new Vector2(36f, 12f),
                "Floor",
                hubRoot.transform,
                false,
                1f);
            CreateBoundaryRect(hubRoot.transform, center, new Vector2(36f, 12f));
            CreateLabel(
                "GACHON UNIVERSITY STATION - CONCOURSE",
                center + new Vector2(0f, 4.8f),
                hubRoot.transform,
                0.105f);

            Vector2 terminalPosition = center + new Vector2(-10.5f, 0.4f);
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

            FareGateBuildData departureGate = CreateLongFareGate(
                "Departure Long Fare Gate",
                center + new Vector2(1.5f, 0f),
                "TAP TRANSIT CARD",
                hubRoot.transform);
            PrototypeAreaTrigger escalatorEntry = CreateClippedEscalator(
                "Gachon Escalator Entrance",
                center + new Vector2(14.3f, 0f),
                hubRoot.transform,
                true,
                true);
            CreateTicketMachineBank(
                center + new Vector2(-15.1f, -3.8f),
                hubRoot.transform);
            CreateStationColumns(
                center,
                new[] { -16f, -5.2f, 9.1f, 16f },
                hubRoot.transform);

            Transform spawn = CreatePoint(
                "Hub Spawn",
                center + new Vector2(-15f, -3.6f),
                hubRoot.transform);

            return new HubBuildData
            {
                Spawn = spawn,
                MapTerminal = mapTerminal,
                DepartureGate = departureGate.Interactable,
                DepartureGateBlocker = departureGate.Blocker,
                EscalatorEntryTrigger = escalatorEntry
            };
        }

        private static AccessHallBuildData CreateAccessHall(
            string name,
            Vector2 center,
            string stationLabel,
            Transform parent)
        {
            GameObject hallRoot = new GameObject(name);
            hallRoot.transform.SetParent(parent);

            CreateBlock(
                "Jeongja Concourse Floor",
                center,
                new Vector2(36f, 12f),
                "Floor",
                hallRoot.transform,
                false,
                1f);
            CreateBoundaryRect(hallRoot.transform, center, new Vector2(36f, 12f));
            CreateLabel(
                stationLabel,
                center + new Vector2(0f, 4.9f),
                hallRoot.transform,
                0.1f);
            CreateLabel(
                "ESCALATOR EXIT / LONG FARE GATE",
                center + new Vector2(0f, 3.85f),
                hallRoot.transform,
                0.062f);

            CreateClippedEscalator(
                "Jeongja Escalator Exit",
                center + new Vector2(-14.2f, 0f),
                hallRoot.transform,
                false,
                false);
            FareGateBuildData exitGate = CreateLongFareGate(
                "Destination Long Fare Gate",
                center + new Vector2(2f, 0f),
                "TAP CARD TO EXIT",
                hallRoot.transform);
            PrototypeAreaTrigger exitTrigger = CreateAreaTrigger(
                "Destination Gate Exit Trigger",
                center + new Vector2(11.5f, 0f),
                new Vector2(4.5f, 8.5f),
                hallRoot.transform);
            CreateLabel(
                "EXIT / DELIVERY COMPLETE",
                center + new Vector2(12f, 4f),
                hallRoot.transform,
                0.058f);

            CreateStationColumns(
                center,
                new[] { -16f, -8.5f, 9f, 16f },
                hallRoot.transform);
            Transform spawn = CreatePoint(
                "Destination Concourse Spawn",
                center + new Vector2(-13.2f, -2.8f),
                hallRoot.transform);

            return new AccessHallBuildData
            {
                Spawn = spawn,
                ExitGate = exitGate.Interactable,
                GateBlocker = exitGate.Blocker,
                ExitTrigger = exitTrigger
            };
        }

        private static EscalatorBuildData CreateEscalatorPassage(
            string name,
            Vector2 center,
            string label,
            Transform parent)
        {
            GameObject passageRoot = new GameObject(name);
            passageRoot.transform.SetParent(parent);
            CreateBlock(
                "Escalator Passage Floor",
                center,
                new Vector2(32f, 10f),
                "Floor",
                passageRoot.transform,
                false,
                1f);
            CreateBoundaryRect(
                passageRoot.transform,
                center,
                new Vector2(32f, 10f));
            CreateBlock(
                "Escalator Belt",
                center,
                new Vector2(25f, 3.5f),
                "Connector",
                passageRoot.transform,
                false,
                0.4f);
            foreach (float railY in new[] { -2f, 2f })
            {
                CreateBlock(
                    "Escalator Side Rail",
                    center + Vector2.up * railY,
                    new Vector2(26f, 0.22f),
                    "ConnectorThreshold",
                    passageRoot.transform,
                    true,
                    -0.08f);
            }
            for (int index = 0; index < 15; index++)
            {
                CreateBlock(
                    "Escalator Step " + (index + 1),
                    center + new Vector2(-11.2f + index * 1.6f, 0f),
                    new Vector2(0.12f, 3.2f),
                    "ConnectorThreshold",
                    passageRoot.transform,
                    false,
                    -0.06f);
            }
            CreateLabel(
                label,
                center + new Vector2(0f, 3.8f),
                passageRoot.transform,
                0.082f);
            CreateLabel(
                "KEEP WALKING  >>>",
                center,
                passageRoot.transform,
                0.065f,
                -0.18f);

            Transform spawn = CreatePoint(
                name + " Spawn",
                center + new Vector2(-12.5f, 0f),
                passageRoot.transform);
            PrototypeAreaTrigger endTrigger = CreateAreaTrigger(
                name + " End Trigger",
                center + new Vector2(12.8f, 0f),
                new Vector2(2.2f, 3.4f),
                passageRoot.transform);
            return new EscalatorBuildData
            {
                Spawn = spawn,
                EndTrigger = endTrigger
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
            const float escalatorAccessX = -9.2f;
            CreatePlatformBoundary(
                platformRoot.transform,
                platformCenter,
                escalatorAccessX);
            CreateLabel(
                stationLabel + (destination ? " - ARRIVAL" : " - DEPARTURE"),
                platformCenter + new Vector2(0f, -1.12f),
                platformRoot.transform,
                0.085f);

            PrototypeAreaTrigger escalatorEntry = CreateClippedEscalator(
                destination
                    ? "Jeongja Escalator Entrance"
                    : "Gachon Escalator Exit",
                platformCenter + new Vector2(escalatorAccessX, -0.42f),
                platformRoot.transform,
                destination,
                destination);

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

            PrototypeTrainArrival trainArrival = null;
            if (!destination)
            {
                GameObject edgeBlocker = CreateBlock(
                    "Departure Platform Edge Blocker",
                    carCenter + new Vector2(0f, -2.63f),
                    new Vector2(24.4f, 0.24f),
                    "ConnectorThreshold",
                    platformRoot.transform,
                    true,
                    -0.22f);
                trainArrival = stationRoot.AddComponent<PrototypeTrainArrival>();
                trainArrival.Configure(
                    train.Root,
                    edgeBlocker,
                    carCenter,
                    new Vector3(30f, 0f, 0f),
                    3.2f);
            }

            Transform platformSpawn = CreatePoint(
                destination ? "Destination Platform Spawn" : "Departure Platform Spawn",
                platformCenter + new Vector2(escalatorAccessX, 0.4f),
                platformRoot.transform);

            return new PlatformBuildData
            {
                PlatformSpawn = platformSpawn,
                TrainSpawn = train.InteriorSpawn,
                Doors = train.Doors,
                DoorwayTriggers = doorwayTriggers.ToArray(),
                EscalatorEntryTrigger = escalatorEntry,
                TrainArrival = trainArrival
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
                Root = trainRoot.transform,
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
            Vector2 platformCenter,
            float accessLocalX)
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

            float accessHalfWidth = 1.8f;
            float[] starts = { -12.5f, accessLocalX + accessHalfWidth };
            float[] ends = { accessLocalX - accessHalfWidth, 12.5f };
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

        private static PrototypeAreaTrigger CreateClippedEscalator(
            string name,
            Vector2 center,
            Transform parent,
            bool pointsForward,
            bool createTrigger)
        {
            GameObject escalatorRoot = new GameObject(name);
            escalatorRoot.transform.SetParent(parent);
            escalatorRoot.transform.position = center;
            CreateBlock(
                "One Third Escalator Landing",
                center,
                new Vector2(4.2f, 2.7f),
                "Connector",
                escalatorRoot.transform,
                false,
                0.5f);
            CreateBlock(
                "Clipped Escalator Belt",
                center,
                new Vector2(3.7f, 1.25f),
                "Floor",
                escalatorRoot.transform,
                false,
                -0.08f,
                pointsForward ? 12f : -12f);
            foreach (float side in new[] { -0.82f, 0.82f })
            {
                CreateBlock(
                    "Clipped Escalator Rail",
                    center + Vector2.up * side,
                    new Vector2(4f, 0.13f),
                    "ConnectorThreshold",
                    escalatorRoot.transform,
                    false,
                    -0.12f,
                    pointsForward ? 12f : -12f);
            }
            for (int index = 0; index < 4; index++)
            {
                CreateBlock(
                    "Visible Escalator Step " + (index + 1),
                    center + new Vector2(-1.25f + index * 0.82f, 0f),
                    new Vector2(0.1f, 1.8f),
                    "ConnectorThreshold",
                    escalatorRoot.transform,
                    false,
                    -0.14f,
                    pointsForward ? 12f : -12f);
            }

            CreateLabel(
                pointsForward ? "ESCALATOR  >>>" : "<<<  ESCALATOR",
                center + Vector2.up * 1.75f,
                escalatorRoot.transform,
                0.057f);
            return createTrigger
                ? CreateAreaTrigger(
                    name + " Trigger",
                    center,
                    new Vector2(3.6f, 2.2f),
                    escalatorRoot.transform)
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

        private static FareGateBuildData CreateLongFareGate(
            string name,
            Vector2 center,
            string label,
            Transform parent)
        {
            GameObject gateRoot = new GameObject(name);
            gateRoot.transform.SetParent(parent);
            gateRoot.transform.position = center;
            foreach (float side in new[] { -1f, 1f })
            {
                CreateBlock(
                    "Long Gate Barrier",
                    center + Vector2.up * side * 3.6f,
                    new Vector2(0.48f, 4.8f),
                    "ConnectorThreshold",
                    gateRoot.transform,
                    true,
                    -0.1f);
                CreateBlock(
                    "Fare Gate Lane Pylon",
                    center + Vector2.up * side * 1.35f,
                    new Vector2(0.75f, 1f),
                    "ConnectorThreshold",
                    gateRoot.transform,
                    true,
                    -0.13f);
            }
            foreach (float pylonY in new[] { -4.8f, -2.55f, 2.55f, 4.8f })
            {
                CreateBlock(
                    "Fare Gate Bank Pylon",
                    center + Vector2.up * pylonY,
                    new Vector2(0.9f, 0.72f),
                    "Door",
                    gateRoot.transform,
                    true,
                    -0.16f);
            }
            CreateBlock(
                "Card Reader",
                center + new Vector2(-0.62f, 0.95f),
                new Vector2(0.72f, 0.62f),
                "Door",
                gateRoot.transform,
                false,
                -0.2f);
            CreateLabel(
                label,
                center + Vector2.up * 5.25f,
                gateRoot.transform,
                0.062f);

            GameObject blocker = CreateBlock(
                name + " Blocker",
                center,
                new Vector2(0.36f, 1.7f),
                "Door",
                gateRoot.transform,
                true,
                -0.22f);

            GameObject interactionObject = new GameObject(name);
            interactionObject.transform.SetParent(gateRoot.transform);
            interactionObject.transform.position = center;
            BoxCollider2D trigger = interactionObject.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(2.8f, 2.6f);
            return new FareGateBuildData
            {
                Interactable = interactionObject.AddComponent<PrototypeInteractable>(),
                Blocker = blocker
            };
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
            PrototypeTrainArrival[] trainArrivals =
                UnityEngine.Object.FindObjectsByType<PrototypeTrainArrival>(
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
            else
            {
                SerializedObject serializedPlayer = new SerializedObject(player);
                if (serializedPlayer.FindProperty("facingCamera")?.objectReferenceValue == null ||
                    serializedPlayer.FindProperty("facingIndicator")?.objectReferenceValue == null)
                {
                    errors.Add("player mouse-facing camera or indicator is not wired");
                }
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
            if (areaTriggers.Length != 13)
            {
                errors.Add("expected 13 gate, escalator, and doorway area triggers");
            }
            if (tunnelMotions.Length != 1)
            {
                errors.Add("moving tunnel visual is missing or duplicated");
            }
            if (trainArrivals.Length != 1)
            {
                errors.Add("departure train arrival controller is missing or duplicated");
            }

            string[] requiredHierarchyNames =
            {
                "Gachon Hub - Unpaid Concourse",
                "Departure Long Fare Gate",
                "Departure Long Fare Gate Blocker",
                "Gachon Escalator Entrance",
                "Departure Escalator Passage",
                "Gachon Departure Platform",
                "Gachon Escalator Exit",
                "Travel Train Interior",
                "Jeongja Destination Platform",
                "Jeongja Escalator Entrance",
                "Destination Escalator Passage",
                "Jeongja Access Hall",
                "Jeongja Escalator Exit",
                "Departure Platform Environment",
                "Departure Train Car",
                "Destination Platform Environment",
                "Destination Train Car",
                "Destination Long Fare Gate",
                "Destination Long Fare Gate Blocker",
                "Destination Gate Exit Trigger",
                "Player Facing Indicator"
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
                FindSceneObject(scene, "Destination Long Fare Gate");
            GameObject destinationHall =
                FindSceneObject(scene, "Jeongja Access Hall");
            if (destinationGate != null && destinationHall != null &&
                !destinationGate.transform.IsChildOf(destinationHall.transform))
            {
                errors.Add("destination fare gate must be inside the access hall");
            }

            foreach (string blockerName in new[]
                     {
                         "Departure Long Fare Gate Blocker",
                         "Destination Long Fare Gate Blocker"
                     })
            {
                GameObject blocker = FindSceneObject(scene, blockerName);
                if (blocker != null &&
                    (blocker.GetComponent<Collider2D>() == null ||
                     !blocker.activeSelf))
                {
                    errors.Add("fare gate blocker must start active with a collider: " + blockerName);
                }
            }

            foreach (string gateName in new[]
                     {
                         "Departure Long Fare Gate",
                         "Destination Long Fare Gate"
                     })
            {
                GameObject gate = FindSceneObject(scene, gateName);
                if (gate != null &&
                    gate.GetComponentsInChildren<Collider2D>(true).Length < 10)
                {
                    errors.Add("long fare gate does not fully block alternate lanes: " + gateName);
                }
            }

            if (flow != null)
            {
                SerializedObject serializedFlow = new SerializedObject(flow);
                foreach (string propertyName in new[]
                         {
                             "departureGateBlocker",
                             "destinationGateBlocker",
                             "departureTrainArrival"
                         })
                {
                    if (serializedFlow.FindProperty(propertyName)?.objectReferenceValue == null)
                    {
                        errors.Add("flow reference is not wired: " + propertyName);
                    }
                }
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
            public GameObject DepartureGateBlocker;
            public PrototypeAreaTrigger EscalatorEntryTrigger;
        }

        private sealed class AccessHallBuildData
        {
            public Transform Spawn;
            public PrototypeInteractable ExitGate;
            public GameObject GateBlocker;
            public PrototypeAreaTrigger ExitTrigger;
        }

        private sealed class EscalatorBuildData
        {
            public Transform Spawn;
            public PrototypeAreaTrigger EndTrigger;
        }

        private sealed class PlatformBuildData
        {
            public Transform PlatformSpawn;
            public Transform TrainSpawn;
            public TrainDoorController[] Doors;
            public PrototypeAreaTrigger[] DoorwayTriggers;
            public PrototypeAreaTrigger EscalatorEntryTrigger;
            public PrototypeTrainArrival TrainArrival;
        }

        private sealed class RideTrainBuildData
        {
            public Transform TrainSpawn;
        }

        private sealed class TrainBuildData
        {
            public Transform Root;
            public Transform InteriorSpawn;
            public TrainDoorController[] Doors;
        }

        private sealed class FareGateBuildData
        {
            public PrototypeInteractable Interactable;
            public GameObject Blocker;
        }
    }
}
#endif
