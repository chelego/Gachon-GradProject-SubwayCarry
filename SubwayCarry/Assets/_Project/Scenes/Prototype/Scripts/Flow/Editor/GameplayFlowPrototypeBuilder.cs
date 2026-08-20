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

            HubBuildData hub = CreateGachonHub(root.transform, flow);
            StationBuildData departure = CreateStation(
                "Gachon Departure",
                Vector2.zero,
                "GACHON UNIVERSITY",
                false,
                root.transform,
                flow);
            StationBuildData destination = CreateStation(
                "Jeongja Destination",
                new Vector2(42f, 0f),
                "JEONGJA",
                true,
                root.transform,
                flow);

            flow.Configure(
                player.Controller,
                player.Posture,
                player.Durability,
                cameraFollow,
                services.Delivery,
                services.Economy,
                services.Transit,
                hub.Spawn,
                departure.PlatformSpawn,
                destination.TrainSpawn,
                departure.DoorBlocker);

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
            departure.BoardingTrigger.Configure(
                flow,
                PrototypeAreaAction.BoardTrain);
            destination.LeaveTrainTrigger.Configure(
                flow,
                PrototypeAreaAction.LeaveTrainAtDestination);
            destination.ExitGate.Configure(
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
                new Vector2(-49f, -1.5f),
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

        private static HubBuildData CreateGachonHub(
            Transform parent,
            PrototypeGameFlowController flow)
        {
            const float centerX = -44f;
            GameObject hubRoot = new GameObject("Gachon Hub");
            hubRoot.transform.SetParent(parent);

            CreateBlock(
                "Station Floor",
                new Vector2(centerX, 0f),
                new Vector2(22f, 10f),
                "Floor",
                hubRoot.transform,
                false,
                1f);
            CreateBoundaryRect(
                hubRoot.transform,
                new Vector2(centerX, 0f),
                new Vector2(22f, 10f));

            CreateLabel(
                "GACHON UNIVERSITY STATION",
                new Vector2(centerX, 3.8f),
                hubRoot.transform,
                0.12f);
            CreateLabel(
                "DELIVERY MAP",
                new Vector2(centerX - 4.5f, 1.2f),
                hubRoot.transform,
                0.075f);
            CreateLabel(
                "FARE GATE",
                new Vector2(centerX + 6.7f, 0.9f),
                hubRoot.transform,
                0.075f);

            GameObject terminal = CreateBlock(
                "Delivery Map Terminal",
                new Vector2(centerX - 4.5f, 0f),
                new Vector2(2.2f, 2.2f),
                "DoorOpening",
                hubRoot.transform,
                false,
                -0.15f);
            BoxCollider2D terminalTrigger = terminal.AddComponent<BoxCollider2D>();
            terminalTrigger.isTrigger = true;
            terminalTrigger.size = new Vector2(1.5f, 1.5f);
            PrototypeInteractable mapTerminal =
                terminal.AddComponent<PrototypeInteractable>();

            GameObject gate = CreateBlock(
                "Departure Fare Gate",
                new Vector2(centerX + 6.7f, 0f),
                new Vector2(0.8f, 2.6f),
                "ConnectorThreshold",
                hubRoot.transform,
                false,
                -0.15f);
            BoxCollider2D gateTrigger = gate.AddComponent<BoxCollider2D>();
            gateTrigger.isTrigger = true;
            gateTrigger.size = new Vector2(1.8f, 2.8f);
            PrototypeInteractable departureGate =
                gate.AddComponent<PrototypeInteractable>();

            Transform spawn = CreatePoint(
                "Hub Spawn",
                new Vector2(centerX - 6.5f, -1.5f),
                hubRoot.transform);

            return new HubBuildData
            {
                Spawn = spawn,
                MapTerminal = mapTerminal,
                DepartureGate = departureGate
            };
        }

        private static StationBuildData CreateStation(
            string name,
            Vector2 center,
            string stationLabel,
            bool destination,
            Transform parent,
            PrototypeGameFlowController flow)
        {
            GameObject stationRoot = new GameObject(name);
            stationRoot.transform.SetParent(parent);

            CreateBlock(
                "Train Floor",
                center + new Vector2(0f, 2.3f),
                new Vector2(22f, 5.4f),
                "Floor",
                stationRoot.transform,
                false,
                1f);
            CreateBlock(
                "Platform",
                center + new Vector2(0f, -2.8f),
                new Vector2(24f, 4.6f),
                "Platform",
                stationRoot.transform,
                false,
                1f);
            CreateBlock(
                "Platform Safety Line",
                center + new Vector2(0f, -0.75f),
                new Vector2(24f, 0.16f),
                "PlatformSafetyLine",
                stationRoot.transform,
                false,
                -0.05f);

            CreateBlock(
                "Train Top Wall",
                center + new Vector2(0f, 5f),
                new Vector2(22f, 0.22f),
                "Wall",
                stationRoot.transform,
                true);
            CreateBlock(
                "Train Left Wall",
                center + new Vector2(-11f, 2.3f),
                new Vector2(0.22f, 5.6f),
                "Wall",
                stationRoot.transform,
                true);
            CreateBlock(
                "Train Right Wall",
                center + new Vector2(11f, 2.3f),
                new Vector2(0.22f, 5.6f),
                "Wall",
                stationRoot.transform,
                true);
            CreateBlock(
                "Train Bottom Wall Left",
                center + new Vector2(-6.1f, -0.4f),
                new Vector2(9.8f, 0.22f),
                "Wall",
                stationRoot.transform,
                true);
            CreateBlock(
                "Train Bottom Wall Right",
                center + new Vector2(6.1f, -0.4f),
                new Vector2(9.8f, 0.22f),
                "Wall",
                stationRoot.transform,
                true);

            GameObject doorBlocker = CreateBlock(
                "Door Blocker",
                center + new Vector2(0f, -0.4f),
                new Vector2(2.4f, 0.22f),
                "Door",
                stationRoot.transform,
                true,
                -0.1f);
            doorBlocker.SetActive(false);
            if (destination)
            {
                UnityEngine.Object.DestroyImmediate(doorBlocker);
                doorBlocker = null;
            }

            CreateSeatRow(
                stationRoot.transform,
                center + new Vector2(0f, 4.2f),
                false);
            CreateSeatRow(
                stationRoot.transform,
                center + new Vector2(0f, 0.45f),
                true);

            CreateLabel(
                stationLabel,
                center + new Vector2(-6.6f, -3f),
                stationRoot.transform,
                0.11f);
            CreateLabel(
                destination ? "EXIT GATE" : "BOARD TRAIN",
                center + new Vector2(6.6f, -3f),
                stationRoot.transform,
                0.08f);

            Transform platformSpawn = CreatePoint(
                "Platform Spawn",
                center + new Vector2(0f, -3f),
                stationRoot.transform);
            Transform trainSpawn = CreatePoint(
                "Train Spawn",
                center + new Vector2(0f, 2f),
                stationRoot.transform);

            GameObject boardingArea = new GameObject(
                destination ? "Leave Train Trigger" : "Board Train Trigger");
            boardingArea.transform.SetParent(stationRoot.transform);
            boardingArea.transform.position = center + new Vector2(0f, 0.5f);
            BoxCollider2D boardingCollider =
                boardingArea.AddComponent<BoxCollider2D>();
            boardingCollider.isTrigger = true;
            boardingCollider.size = new Vector2(2.3f, 1.2f);
            PrototypeAreaTrigger areaTrigger =
                boardingArea.AddComponent<PrototypeAreaTrigger>();

            PrototypeInteractable exitGate = null;
            if (destination)
            {
                GameObject gate = CreateBlock(
                    "Destination Fare Gate",
                    center + new Vector2(7.5f, -2.8f),
                    new Vector2(0.8f, 2.4f),
                    "ConnectorThreshold",
                    stationRoot.transform,
                    false,
                    -0.15f);
                BoxCollider2D gateTrigger = gate.AddComponent<BoxCollider2D>();
                gateTrigger.isTrigger = true;
                gateTrigger.size = new Vector2(1.8f, 2.8f);
                exitGate = gate.AddComponent<PrototypeInteractable>();
            }

            return new StationBuildData
            {
                PlatformSpawn = platformSpawn,
                TrainSpawn = trainSpawn,
                DoorBlocker = doorBlocker,
                BoardingTrigger = destination ? null : areaTrigger,
                LeaveTrainTrigger = destination ? areaTrigger : null,
                ExitGate = exitGate
            };
        }

        private static void CreateSeatRow(
            Transform parent,
            Vector2 center,
            bool leaveDoorAisleOpen)
        {
            for (int index = 0; index < 3; index++)
            {
                if (leaveDoorAisleOpen && index == 1)
                {
                    continue;
                }

                float x = center.x + (index - 1) * 6.6f;
                CreateBlock(
                    "Seat Row " + (index + 1),
                    new Vector2(x, center.y),
                    new Vector2(4.5f, 0.72f),
                    "Seat",
                    parent,
                    true,
                    0f);
            }
        }

        private static Camera CreateCamera(Transform parent)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(parent);
            cameraObject.transform.position = new Vector3(-44f, 0f, -10f);
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
            float z = 0f)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Quad);
            block.name = name;
            block.transform.SetParent(parent);
            block.transform.position = new Vector3(position.x, position.y, z);
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

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Gameplay flow scene validation failed: " +
                    string.Join(", ", errors));
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

        private sealed class StationBuildData
        {
            public Transform PlatformSpawn;
            public Transform TrainSpawn;
            public GameObject DoorBlocker;
            public PrototypeAreaTrigger BoardingTrigger;
            public PrototypeAreaTrigger LeaveTrainTrigger;
            public PrototypeInteractable ExitGate;
        }
    }
}
#endif
