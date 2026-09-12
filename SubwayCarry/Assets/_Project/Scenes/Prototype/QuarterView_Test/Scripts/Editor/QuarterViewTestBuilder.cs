#if UNITY_EDITOR
using System;
using SubwayCarry.Prototype.QuarterView;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SubwayCarry.Prototype.QuarterView.Editor
{
    /// <summary>
    /// Builds the isolated two-map station prototype with a real 2:1 isometric projection.
    /// World gameplay still uses Rigidbody2D, while every station footprint is projected
    /// from logical floor coordinates into the quarter-view screen plane.
    /// </summary>
    public static class QuarterViewTestBuilder
    {
        public const string ScenePath =
            "Assets/_Project/Scenes/Prototype/QuarterView_Test/QuarterView_Test.unity";

        private const string RootFolder =
            "Assets/_Project/Scenes/Prototype/QuarterView_Test";
        private const string MaterialFolder = RootFolder + "/Materials";

        private const float HalfTileWidth = 1.15f;
        private const float HalfTileHeight = 0.575f;
        private const float CameraZ = -10f;
        private const float RoomHalfWidth = 10.5f;
        private const float RoomHalfDepth = 7.5f;
        private const float TrackHalfLength = 12.8f;
        private const float BackWallHeight = 3.45f;

        private static readonly Color VoidColor = new Color32(6, 12, 21, 255);
        private static readonly Color FloorA = new Color32(180, 185, 184, 255);
        private static readonly Color FloorB = new Color32(160, 169, 170, 255);
        private static readonly Color FloorDark = new Color32(126, 139, 143, 255);
        private static readonly Color WallTop = new Color32(68, 91, 112, 255);
        private static readonly Color WallLeft = new Color32(36, 54, 72, 255);
        private static readonly Color WallRight = new Color32(48, 67, 86, 255);
        private static readonly Color WallAccent = new Color32(91, 111, 128, 255);
        private static readonly Color PillarTop = new Color32(161, 172, 176, 255);
        private static readonly Color PillarLeft = new Color32(86, 101, 109, 255);
        private static readonly Color PillarRight = new Color32(111, 125, 132, 255);
        private static readonly Color Orange = new Color32(243, 119, 24, 255);
        private static readonly Color OrangeLeft = new Color32(159, 67, 16, 255);
        private static readonly Color OrangeRight = new Color32(205, 86, 18, 255);
        private static readonly Color ReaderClosed = new Color32(177, 49, 40, 255);
        private static readonly Color ReaderOpen = new Color32(31, 205, 114, 255);
        private static readonly Color Yellow = new Color32(243, 205, 43, 255);
        private static readonly Color YellowLeft = new Color32(161, 128, 14, 255);
        private static readonly Color YellowRight = new Color32(205, 164, 21, 255);
        private static readonly Color Teal = new Color32(37, 161, 163, 255);
        private static readonly Color TealLeft = new Color32(20, 93, 97, 255);
        private static readonly Color TealRight = new Color32(27, 124, 127, 255);
        private static readonly Color Track = new Color32(17, 22, 28, 255);
        private static readonly Color TrackInset = new Color32(29, 36, 44, 255);
        private static readonly Color Rail = new Color32(142, 151, 156, 255);
        private static readonly Color Glass = new Color32(55, 98, 117, 255);
        private static readonly Color TrainTop = new Color32(211, 218, 219, 255);
        private static readonly Color TrainLeft = new Color32(78, 101, 118, 255);
        private static readonly Color TrainRight = new Color32(104, 128, 143, 255);
        private static readonly Color TrainStripe = new Color32(34, 169, 163, 255);
        private static readonly Color Bench = new Color32(153, 86, 48, 255);
        private static readonly Color Shadow = new Color32(27, 35, 42, 255);
        private static readonly Color PlayerBody = new Color32(54, 208, 145, 255);
        private static readonly Color PlayerDark = new Color32(26, 74, 83, 255);
        private static readonly Color Package = new Color32(235, 188, 123, 255);

        [MenuItem("SubwayCarry/Prototype/Build Quarter View Test")]
        public static void Build()
        {
            EnsureFolders();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("Quarter View Test");

            Camera camera = CreateCamera(root.transform);
            QuarterViewTestPlayer player = CreatePlayer(root.transform, camera);
            QuarterViewTestCameraFollow cameraFollow =
                camera.gameObject.AddComponent<QuarterViewTestCameraFollow>();
            cameraFollow.Configure(player.transform,
                new Vector2(-7.4f, -3.2f), new Vector2(7.4f, 3.3f));
            CanvasGroup fadeOverlay = CreateFadeOverlay(root.transform);

            GameObject flowObject = new GameObject("Quarter View Test Flow");
            flowObject.transform.SetParent(root.transform);
            QuarterViewTestFlow flow = flowObject.AddComponent<QuarterViewTestFlow>();

            ConcourseBuildData concourse = CreateConcourse(root.transform);
            PlatformBuildData platform = CreatePlatform(root.transform);

            flow.Configure(concourse.Root, platform.Root, player, fadeOverlay,
                platform.UpboundArrival, platform.DownboundArrival,
                concourse.UpboundReturn, concourse.DownboundReturn);

            concourse.UpboundPortal.Configure(flow, platform.Root, platform.UpboundArrival);
            concourse.DownboundPortal.Configure(flow, platform.Root, platform.DownboundArrival);
            platform.UpboundReturnPortal.Configure(flow, concourse.Root, concourse.UpboundReturn);
            platform.DownboundReturnPortal.Configure(flow, concourse.Root, concourse.DownboundReturn);

            player.Teleport(concourse.EntranceSpawn.position);
            concourse.Root.SetActive(true);
            platform.Root.SetActive(false);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("QuarterView_Test scene could not be saved.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log("[QuarterViewTest] Built true isometric two-map prototype at " + ScenePath);
        }

        private static ConcourseBuildData CreateConcourse(Transform parent)
        {
            GameObject map = new GameObject("MAP 1 - Isometric Fare Gate Concourse");
            map.transform.SetParent(parent);
            CreateRoomBase(map.transform, "Concourse", false);
            CreateStationHeader(map.transform, new Vector2(0f, 6.72f),
                "GACHON UNIVERSITY  |  FARE GATES");

            Vector2 entranceStair = new Vector2(-8.85f, -5.9f);
            CreateIsoStairs(map.transform, "Street Entrance Stair", entranceStair,
                2.15f, 2.9f, false, false, "STREET");
            Transform entranceSpawn = CreatePointLogical("Entrance Arrival Spawn",
                new Vector2(-8.7f, -4.05f), map.transform);

            CreateFareGateBank(map.transform);
            CreateServiceOffice(map.transform, new Vector2(-0.4f, 5.85f));
            CreateElevator(map.transform, new Vector2(6.75f, 6.0f));
            CreateTicketMachineBank(map.transform, new Vector2(-8.55f, 0.55f));

            CreateColumn(map.transform, new Vector2(-7f, 1.05f));
            CreateColumn(map.transform, new Vector2(-3.5f, 1.05f));
            CreateColumn(map.transform, new Vector2(0f, 1.05f));
            CreateColumn(map.transform, new Vector2(3.5f, 1.05f));
            CreateColumn(map.transform, new Vector2(7f, 1.05f));

            Vector2 upStair = new Vector2(-8.9f, 3.7f);
            Vector2 downStair = new Vector2(8.9f, 3.7f);
            CreateIsoStairs(map.transform, "Upbound Platform Stair", upStair,
                2.15f, 2.9f, false, false, "UPBOUND");
            CreateIsoStairs(map.transform, "Downbound Platform Stair", downStair,
                2.15f, 2.9f, false, false, "DOWNBOUND");

            QuarterViewTestPortal upPortal = CreatePortal("Upbound Stair Transition",
                upStair + new Vector2(0f, 0.95f), new Vector2(1.65f, 0.85f), map.transform);
            QuarterViewTestPortal downPortal = CreatePortal("Downbound Stair Transition",
                downStair + new Vector2(0f, 0.95f), new Vector2(1.65f, 0.85f), map.transform);

            Transform upReturn = CreatePointLogical("Upbound Concourse Return Spawn",
                upStair + new Vector2(0f, -1.75f), map.transform);
            Transform downReturn = CreatePointLogical("Downbound Concourse Return Spawn",
                downStair + new Vector2(0f, -1.75f), map.transform);

            return new ConcourseBuildData
            {
                Root = map, EntranceSpawn = entranceSpawn,
                UpboundPortal = upPortal, DownboundPortal = downPortal,
                UpboundReturn = upReturn, DownboundReturn = downReturn
            };
        }

        private static PlatformBuildData CreatePlatform(Transform parent)
        {
            GameObject map = new GameObject("MAP 2 - Isometric Bidirectional Platforms");
            map.transform.SetParent(parent);
            CreateRoomBase(map.transform, "Platform", true);
            CreateStationHeader(map.transform, new Vector2(0f, 6.72f),
                "GACHON UNIVERSITY  |  PLATFORMS");

            CreateTunnelApproaches(map.transform);
            CreatePlatformSurface(map.transform, 4.85f, true);
            CreatePlatformSurface(map.transform, -4.85f, false);
            CreateTrack(map.transform, 1.05f, "Downbound Track");
            CreateTrack(map.transform, -1.05f, "Upbound Track");
            CreatePlatformScreenDoors(map.transform, 2.2f, "Upper Platform Doors");
            CreatePlatformScreenDoors(map.transform, -2.2f, "Lower Platform Doors");
            CreateTrainPreview(map.transform, 1.05f, true);
            CreateTrainPreview(map.transform, -1.05f, false);

            Vector2 upStair = new Vector2(-8.95f, -5.15f);
            Vector2 downStair = new Vector2(8.95f, 5.15f);
            CreateIsoStairs(map.transform, "Upbound Platform Return Stair", upStair,
                2.1f, 2.9f, true, true, "CONCOURSE");
            CreateIsoStairs(map.transform, "Downbound Platform Return Stair", downStair,
                2.1f, 2.9f, true, true, "CONCOURSE");

            Transform upArrival = CreatePointLogical("Upbound Platform Arrival Spawn",
                upStair + new Vector2(1.75f, 0f), map.transform);
            Transform downArrival = CreatePointLogical("Downbound Platform Arrival Spawn",
                downStair + new Vector2(-1.75f, 0f), map.transform);

            QuarterViewTestPortal upReturnPortal = CreatePortal("Upbound Return To Concourse",
                upStair + new Vector2(-0.95f, 0f), new Vector2(0.85f, 1.65f), map.transform);
            QuarterViewTestPortal downReturnPortal = CreatePortal("Downbound Return To Concourse",
                downStair + new Vector2(0.95f, 0f), new Vector2(0.85f, 1.65f), map.transform);

            CreatePlatformFurniture(map.transform, 4.85f, true);
            CreatePlatformFurniture(map.transform, -4.85f, false);

            return new PlatformBuildData
            {
                Root = map, UpboundArrival = upArrival, DownboundArrival = downArrival,
                UpboundReturnPortal = upReturnPortal, DownboundReturnPortal = downReturnPortal
            };
        }

        private static Camera CreateCamera(Transform parent)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(parent);
            cameraObject.transform.position = new Vector3(-2.5f, -2.15f, CameraZ);
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7.2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = VoidColor;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            return camera;
        }

        private static QuarterViewTestPlayer CreatePlayer(Transform parent, Camera camera)
        {
            GameObject playerObject = new GameObject("Player");
            playerObject.transform.SetParent(parent);
            playerObject.transform.position = new Vector3(0f, 0f, -2.5f);
            playerObject.tag = "Player";
            Rigidbody2D body = playerObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            CapsuleCollider2D collider = playerObject.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.62f, 0.46f);
            collider.offset = new Vector2(0f, -0.28f);
            collider.direction = CapsuleDirection2D.Horizontal;

            CreateLocalBlock("Player Shadow", new Vector3(0.12f, -0.42f, 0.4f),
                new Vector2(0.92f, 0.3f), Shadow, playerObject.transform, "PlayerShadow", 0);
            CreateLocalBlock("Player Legs", new Vector3(0f, -0.1f, -0.05f),
                new Vector2(0.55f, 0.58f), PlayerDark, playerObject.transform, "PlayerDark", 4);
            CreateLocalBlock("Player Body", new Vector3(0f, 0.42f, -0.1f),
                new Vector2(0.82f, 1.12f), PlayerBody, playerObject.transform, "PlayerBody", 5);
            CreateLocalBlock("Package", new Vector3(0.32f, 0.28f, -0.2f),
                new Vector2(0.62f, 0.54f), Package, playerObject.transform, "Package", 7);
            CreateLocalBlock("Player Head", new Vector3(0f, 1.12f, -0.15f),
                new Vector2(0.66f, 0.62f), new Color32(240, 196, 151, 255),
                playerObject.transform, "PlayerHead", 8);
            GameObject indicator = CreateLocalBlock("Facing Indicator", new Vector3(0f, 0.75f, -0.3f),
                new Vector2(0.5f, 0.12f), Teal, playerObject.transform, "FacingIndicator", 9);
            QuarterViewTestPlayer player = playerObject.AddComponent<QuarterViewTestPlayer>();
            player.Configure(camera, indicator.transform);
            return player;
        }

        private static CanvasGroup CreateFadeOverlay(Transform parent)
        {
            GameObject canvasObject = new GameObject("Fade Canvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject panel = new GameObject("Fade Overlay", typeof(RectTransform),
                typeof(Image), typeof(CanvasGroup));
            panel.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = Color.black;
            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            return group;
        }

        private static void CreateRoomBase(Transform parent, string prefix, bool openTrackTunnels)
        {
            GameObject floorRoot = new GameObject(prefix + " Isometric Floor");
            floorRoot.transform.SetParent(parent);
            for (int x = -10; x <= 10; x++)
            {
                for (int y = -7; y <= 7; y++)
                {
                    Color tile = (x + y) % 2 == 0 ? FloorA : FloorB;
                    CreateIsoPlane(prefix + " Tile " + x + " " + y,
                        new Vector2(x, y), Vector2.one, tile,
                        floorRoot.transform, "Iso" + (tile == FloorA ? "FloorA" : "FloorB"), -500);
                }
            }

            CreateIsoPrism(prefix + " North Wall", new Vector2(0f, RoomHalfDepth - 0.12f),
                new Vector2(RoomHalfWidth * 2f + 0.15f, 0.28f), BackWallHeight,
                WallTop, WallLeft, WallRight, parent, true, "Wall");
            CreateIsoPrism(prefix + " South Cutaway", new Vector2(0f, -RoomHalfDepth + 0.07f),
                new Vector2(RoomHalfWidth * 2f + 0.1f, 0.14f), 0.3f,
                WallAccent, WallLeft, WallRight, parent, false, "Cutaway");

            if (openTrackTunnels)
            {
                const float sideSegmentDepth = 4.9f;
                const float sideSegmentY = 5.05f;
                CreateIsoPrism(prefix + " East Upper Wall", new Vector2(RoomHalfWidth - 0.12f, sideSegmentY),
                    new Vector2(0.28f, sideSegmentDepth), BackWallHeight,
                    WallTop, WallLeft, WallRight, parent, true, "Wall");
                CreateIsoPrism(prefix + " East Lower Wall", new Vector2(RoomHalfWidth - 0.12f, -sideSegmentY),
                    new Vector2(0.28f, sideSegmentDepth), BackWallHeight,
                    WallTop, WallLeft, WallRight, parent, true, "Wall");
                CreateIsoPrism(prefix + " West Upper Cutaway", new Vector2(-RoomHalfWidth + 0.07f, sideSegmentY),
                    new Vector2(0.14f, sideSegmentDepth), 0.3f,
                    WallAccent, WallLeft, WallRight, parent, false, "Cutaway");
                CreateIsoPrism(prefix + " West Lower Cutaway", new Vector2(-RoomHalfWidth + 0.07f, -sideSegmentY),
                    new Vector2(0.14f, sideSegmentDepth), 0.3f,
                    WallAccent, WallLeft, WallRight, parent, false, "Cutaway");
            }
            else
            {
                CreateIsoPrism(prefix + " East Wall", new Vector2(RoomHalfWidth - 0.12f, 0f),
                    new Vector2(0.28f, RoomHalfDepth * 2f + 0.15f), BackWallHeight,
                    WallTop, WallLeft, WallRight, parent, true, "Wall");
                CreateIsoPrism(prefix + " West Cutaway", new Vector2(-RoomHalfWidth + 0.07f, 0f),
                    new Vector2(0.14f, RoomHalfDepth * 2f + 0.1f), 0.3f,
                    WallAccent, WallLeft, WallRight, parent, false, "Cutaway");
            }
            CreateRoomBoundary(parent, prefix);
        }

        private static void CreateFareGateBank(Transform parent)
        {
            GameObject gateRoot = new GameObject("Full Width Isometric Fare Gate Bank");
            gateRoot.transform.SetParent(parent);
            const float gateY = -1.3f;
            float[] housingX = { -9f, -6f, -3f, 0f, 3f, 6f, 9f };
            float[] laneX = { -7.5f, -4.5f, -1.5f, 1.5f, 4.5f, 7.5f };

            CreateIsoPrism("Left Gate End Wall", new Vector2(-10f, gateY),
                new Vector2(1.15f, 1.25f), 0.95f,
                WallTop, WallLeft, WallRight, gateRoot.transform, true, "GateEnd");
            CreateIsoPrism("Right Gate End Wall", new Vector2(10f, gateY),
                new Vector2(1.15f, 1.25f), 0.95f,
                WallTop, WallLeft, WallRight, gateRoot.transform, true, "GateEnd");

            Renderer centralReader = null;
            for (int i = 0; i < housingX.Length; i++)
            {
                PrismBuild housing = CreateIsoPrism("Gate Housing " + i,
                    new Vector2(housingX[i], gateY), new Vector2(0.88f, 1.2f), 0.88f,
                    Orange, OrangeLeft, OrangeRight, gateRoot.transform, true, "GateHousing");
                PrismBuild reader = CreateIsoPrism("Card Reader " + i,
                    new Vector2(housingX[i], gateY - 0.08f), new Vector2(0.42f, 0.52f), 1.12f,
                    ReaderClosed, new Color32(107, 32, 29, 255), new Color32(139, 39, 34, 255),
                    housing.Root.transform, false, "ReaderClosed");
                if (i == 3) centralReader = reader.TopRenderer;
            }

            GameObject interactiveBarrier = null;
            for (int i = 0; i < laneX.Length; i++)
            {
                bool interactive = Mathf.Approximately(laneX[i], 1.5f);
                PrismBuild barrier = CreateIsoPrism(
                    interactive ? "Interactive Gate Barrier" : "Closed Gate Barrier " + i,
                    new Vector2(laneX[i], gateY), new Vector2(2.05f, 0.18f), 0.68f,
                    Orange, OrangeLeft, OrangeRight, gateRoot.transform, true, "GateBarrier");
                if (interactive) interactiveBarrier = barrier.Root;
            }

            GameObject interaction = new GameObject("Central Card Reader Interaction");
            interaction.transform.SetParent(gateRoot.transform);
            PolygonCollider2D trigger = interaction.AddComponent<PolygonCollider2D>();
            trigger.points = ProjectRect(new Vector2(1.5f, gateY - 0.95f), new Vector2(2.45f, 1.5f));
            trigger.isTrigger = true;
            QuarterViewFareGate gate = interaction.AddComponent<QuarterViewFareGate>();
            gate.Configure(interactiveBarrier != null ? interactiveBarrier.GetComponent<Collider2D>() : null,
                interactiveBarrier, centralReader, GetOrCreateMaterial("ReaderOpen", ReaderOpen));
            CreateLabel("E", Iso(new Vector2(1.5f, gateY - 1.42f)) + Vector2.up * 0.4f,
                gateRoot.transform, 0.08f, Yellow);
        }

        private static void CreateServiceOffice(Transform parent, Vector2 logicalPosition)
        {
            CreateIsoPrism("Station Office", logicalPosition, new Vector2(4f, 1.3f), 1.9f,
                WallTop, WallLeft, WallRight, parent, true, "Office");
            CreateIsoPrism("Office Counter", logicalPosition + new Vector2(0f, -0.78f),
                new Vector2(3.1f, 0.42f), 0.88f,
                WallAccent, WallLeft, WallRight, parent, true, "OfficeCounter");
            CreateIsoPrism("Office Glass", logicalPosition + new Vector2(0f, -0.72f),
                new Vector2(2.65f, 0.12f), 1.48f,
                Glass, new Color32(33, 66, 82, 255), new Color32(43, 81, 98, 255),
                parent, false, "OfficeGlass");
        }

        private static void CreateElevator(Transform parent, Vector2 logicalPosition)
        {
            CreateIsoPrism("Elevator Housing", logicalPosition, new Vector2(2.05f, 1.65f), 2.25f,
                WallTop, WallLeft, WallRight, parent, true, "Elevator");
            CreateIsoPrism("Elevator Door", logicalPosition + new Vector2(-0.12f, -0.88f),
                new Vector2(1.35f, 0.12f), 1.8f,
                Glass, new Color32(33, 66, 82, 255), new Color32(43, 81, 98, 255),
                parent, false, "ElevatorDoor");
            CreateIsoPrism("Elevator Header", logicalPosition + new Vector2(-0.12f, -0.92f),
                new Vector2(1.62f, 0.15f), 2.15f,
                Yellow, YellowLeft, YellowRight, parent, false, "ElevatorHeader");
        }

        private static void CreateTicketMachineBank(Transform parent, Vector2 logicalPosition)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector2 position = logicalPosition + new Vector2(0.62f * i, 0.62f * i);
                CreateIsoPrism("Ticket Machine " + (i + 1), position,
                    new Vector2(0.78f, 0.72f), 1.35f,
                    Teal, TealLeft, TealRight, parent, true, "TicketMachine");
                CreateIsoPrism("Ticket Screen " + (i + 1), position + new Vector2(0f, -0.39f),
                    new Vector2(0.48f, 0.08f), 1.12f,
                    WallTop, WallLeft, WallRight, parent, false, "TicketScreen");
            }
        }

        private static void CreateColumn(Transform parent, Vector2 logicalPosition)
        {
            CreateIsoPlane("Column Shadow", logicalPosition + new Vector2(-0.13f, -0.18f),
                new Vector2(1.05f, 0.72f), Shadow, parent, "ColumnShadow", -50);
            CreateIsoPrism("Column Foundation", logicalPosition, new Vector2(0.9f, 0.9f), 0.16f,
                PillarTop, PillarLeft, PillarRight, parent, true, "ColumnFoundation");
            CreateIsoPrism("Column Plinth", logicalPosition, new Vector2(0.72f, 0.72f), 0.2f,
                PillarTop, PillarLeft, PillarRight, parent, false, "ColumnPlinth", 0.16f);
            CreateIsoPrism("Column Shaft", logicalPosition, new Vector2(0.5f, 0.5f), 2.1f,
                PillarTop, PillarLeft, PillarRight, parent, false, "ColumnShaft", 0.36f);
            CreateIsoPrism("Column Capital", logicalPosition, new Vector2(0.68f, 0.68f), 0.16f,
                PillarTop, PillarLeft, PillarRight, parent, false, "ColumnCapital", 2.46f);
        }

        private static void CreateIsoStairs(Transform parent, string name, Vector2 logicalPosition,
            float width, float depth, bool alongLogicalX, bool highTowardPositiveAxis, string label)
        {
            GameObject stairRoot = new GameObject(name);
            stairRoot.transform.SetParent(parent);
            Vector2 openingSize = alongLogicalX
                ? new Vector2(depth, width)
                : new Vector2(width, depth);
            CreateIsoPlane("Stairwell Opening", logicalPosition, openingSize,
                Track, stairRoot.transform, "StairOpening", -20);
            Vector2 railOffset = alongLogicalX
                ? new Vector2(0f, width * 0.5f - 0.08f)
                : new Vector2(width * 0.5f - 0.08f, 0f);
            Vector2 railSize = alongLogicalX
                ? new Vector2(depth + 0.12f, 0.14f)
                : new Vector2(0.14f, depth + 0.12f);
            CreateIsoPrism("Left Stair Rail", logicalPosition - railOffset,
                railSize, 1.05f, WallAccent, WallLeft, WallRight,
                stairRoot.transform, false, "StairRail");
            CreateIsoPrism("Right Stair Rail", logicalPosition + railOffset,
                railSize, 1.05f, WallAccent, WallLeft, WallRight,
                stairRoot.transform, false, "StairRail");

            const int steps = 10;
            float stepDepth = depth / steps;
            for (int i = 0; i < steps; i++)
            {
                float t = (i + 0.5f) / steps;
                float axisPosition = Mathf.Lerp(-depth * 0.5f + stepDepth * 0.5f,
                    depth * 0.5f - stepDepth * 0.5f, t);
                float rise = highTowardPositiveAxis ? t : 1f - t;
                float height = 0.08f + rise * 1.02f;
                Vector2 stepOffset = alongLogicalX
                    ? new Vector2(axisPosition, 0f)
                    : new Vector2(0f, axisPosition);
                Vector2 stepSize = alongLogicalX
                    ? new Vector2(stepDepth + 0.025f, width - 0.3f)
                    : new Vector2(width - 0.3f, stepDepth + 0.025f);
                Color tread = i % 2 == 0 ? FloorA : FloorDark;
                CreateIsoPrism("Stair Step " + i, logicalPosition + stepOffset,
                    stepSize, height,
                    tread, new Color32(67, 79, 84, 255), new Color32(92, 105, 109, 255),
                    stairRoot.transform, false, i % 2 == 0 ? "StairStepLight" : "StairStepDark");

                float lowDirection = highTowardPositiveAxis ? -1f : 1f;
                float nosingAxis = axisPosition + lowDirection * stepDepth * 0.42f;
                Vector2 nosingOffset = alongLogicalX
                    ? new Vector2(nosingAxis, 0f)
                    : new Vector2(0f, nosingAxis);
                Vector2 nosingSize = alongLogicalX
                    ? new Vector2(0.045f, width - 0.32f)
                    : new Vector2(width - 0.32f, 0.045f);
                CreateIsoPrism("Stair Nosing " + i, logicalPosition + nosingOffset,
                    nosingSize, 0.025f,
                    Yellow, YellowLeft, YellowRight,
                    stairRoot.transform, false, "StairNosing", height);
            }

            float labelAxis = (highTowardPositiveAxis ? -1f : 1f) * depth * 0.7f;
            Vector2 labelOffset = alongLogicalX
                ? new Vector2(labelAxis, 0f)
                : new Vector2(0f, labelAxis);
            CreateLabel(label, Iso(logicalPosition + labelOffset) + Vector2.up * 0.35f,
                stairRoot.transform, 0.062f, Color.white);
        }

        private static void CreateTunnelApproaches(Transform parent)
        {
            CreateIsoPlane("West Tunnel Track Apron", new Vector2(-11.65f, 0f),
                new Vector2(2.8f, 5.15f), Track, parent, "TunnelVoid", -260);
            CreateIsoPlane("East Tunnel Track Apron", new Vector2(11.65f, 0f),
                new Vector2(2.8f, 5.15f), Track, parent, "TunnelVoid", -260);

            CreateIsoPrism("West Tunnel Portal", new Vector2(-10.35f, 0f),
                new Vector2(0.42f, 5.2f), BackWallHeight - 0.2f,
                WallTop, WallLeft, WallRight, parent, false, "TunnelPortal");
            CreateIsoPrism("East Tunnel Portal", new Vector2(10.35f, 0f),
                new Vector2(0.42f, 5.2f), BackWallHeight - 0.2f,
                WallTop, WallLeft, WallRight, parent, false, "TunnelPortal");

            CreateLabel("TRAIN ENTRY", Iso(new Vector2(-11.35f, -2.55f)) + Vector2.up * 0.35f,
                parent, 0.06f, Yellow);
            CreateLabel("TRAIN EXIT", Iso(new Vector2(11.35f, 2.55f)) + Vector2.up * 0.35f,
                parent, 0.06f, Yellow);
        }

        private static void CreateTrainPreview(Transform parent, float logicalY, bool entersFromWest)
        {
            GameObject trainRoot = new GameObject(
                entersFromWest ? "Downbound Train From West" : "Upbound Train From East");
            trainRoot.transform.SetParent(parent);

            for (int i = 0; i < 4; i++)
            {
                float x = -4.8f + i * 3.2f;
                GameObject carRoot = new GameObject("Train Car " + (i + 1));
                carRoot.transform.SetParent(trainRoot.transform);
                CreateIsoPrism("Car Body", new Vector2(x, logicalY),
                    new Vector2(3f, 1.18f), 1.55f,
                    TrainTop, TrainLeft, TrainRight, carRoot.transform, false, "TrainBody");
                CreateIsoPrism("Window Band", new Vector2(x, logicalY - 0.55f),
                    new Vector2(2.45f, 0.1f), 1.2f,
                    Glass, new Color32(27, 56, 70, 255), new Color32(38, 75, 88, 255),
                    carRoot.transform, false, "TrainWindow");
                CreateIsoPrism("Line Stripe", new Vector2(x, logicalY - 0.61f),
                    new Vector2(2.85f, 0.08f), 0.68f,
                    TrainStripe, TealLeft, TealRight, carRoot.transform, false, "TrainStripe");
                CreateIsoPrism("Center Door", new Vector2(x, logicalY - 0.62f),
                    new Vector2(0.16f, 0.09f), 1.42f,
                    WallAccent, WallLeft, WallRight, carRoot.transform, false, "TrainDoor");
            }

            QuarterViewTestTrainPreview preview =
                trainRoot.AddComponent<QuarterViewTestTrainPreview>();
            preview.Configure(Iso(new Vector2(entersFromWest ? -18f : 18f, 0f)), 5.2f);
        }

        private static void CreatePlatformSurface(Transform parent, float logicalY, bool upper)
        {
            CreateIsoPlane((upper ? "Upper" : "Lower") + " Platform Surface",
                new Vector2(0f, logicalY), new Vector2(RoomHalfWidth * 2f - 0.3f, 5.3f),
                FloorB, parent, "PlatformFloor", -250);
            float safetyY = logicalY + (upper ? -2.35f : 2.35f);
            CreateIsoPlane((upper ? "Upper" : "Lower") + " Safety Line",
                new Vector2(0f, safetyY), new Vector2(RoomHalfWidth * 2f - 0.5f, 0.22f),
                Yellow, parent, "SafetyLine", 35);
        }

        private static void CreateTrack(Transform parent, float logicalY, string name)
        {
            CreateIsoPlane(name + " Bed", new Vector2(0f, logicalY), new Vector2(TrackHalfLength * 2f, 1.5f),
                Track, parent, "TrackBed", -210);
            CreateIsoPlane(name + " Inner Bed", new Vector2(0f, logicalY), new Vector2(TrackHalfLength * 2f - 0.35f, 1.16f),
                TrackInset, parent, "TrackInset", -205);
            for (float x = -TrackHalfLength + 0.35f; x <= TrackHalfLength - 0.35f; x += 0.62f)
            {
                CreateIsoPlane(name + " Sleeper " + x.ToString("0.0"), new Vector2(x, logicalY),
                    new Vector2(0.14f, 1.05f), FloorDark, parent, "Sleeper", -190);
            }
            CreateIsoPrism(name + " Near Rail", new Vector2(0f, logicalY - 0.39f),
                new Vector2(TrackHalfLength * 2f - 0.2f, 0.085f), 0.12f, Rail,
                new Color32(77, 84, 88, 255), new Color32(103, 111, 115, 255),
                parent, false, "Rail");
            CreateIsoPrism(name + " Far Rail", new Vector2(0f, logicalY + 0.39f),
                new Vector2(TrackHalfLength * 2f - 0.2f, 0.085f), 0.12f, Rail,
                new Color32(77, 84, 88, 255), new Color32(103, 111, 115, 255),
                parent, false, "Rail");
        }

        private static void CreatePlatformScreenDoors(Transform parent, float logicalY, string name)
        {
            GameObject doorRoot = new GameObject(name);
            doorRoot.transform.SetParent(parent);
            for (int i = 0; i < 13; i++)
            {
                float x = -9f + i * 1.5f;
                CreateIsoPrism("Glass Panel " + i, new Vector2(x, logicalY),
                    new Vector2(1.25f, 0.12f), 1.05f,
                    Glass, new Color32(32, 63, 76, 255), new Color32(42, 79, 93, 255),
                    doorRoot.transform, false, "PlatformGlass");
                CreateIsoPrism("Door Post " + i, new Vector2(x - 0.7f, logicalY),
                    new Vector2(0.1f, 0.2f), 1.24f,
                    WallAccent, WallLeft, WallRight, doorRoot.transform, false, "DoorPost");
            }
            CreateLogicalObstacle(name + " Track Boundary", new Vector2(0f, logicalY),
                new Vector2(RoomHalfWidth * 2f - 0.3f, 0.18f), doorRoot.transform);
        }

        private static void CreatePlatformFurniture(Transform parent, float logicalY, bool upper)
        {
            float innerOffset = upper ? 0.78f : -0.78f;
            if (upper)
            {
                CreateColumn(parent, new Vector2(-8.3f, logicalY + innerOffset));
            }
            else
            {
                CreateColumn(parent, new Vector2(8.3f, logicalY + innerOffset));
            }
            CreateColumn(parent, new Vector2(-4.15f, logicalY + innerOffset));
            CreateColumn(parent, new Vector2(0f, logicalY + innerOffset));
            CreateColumn(parent, new Vector2(4.15f, logicalY + innerOffset));

            float[] benchPositions = upper
                ? new[] { -6.25f, -2.2f, 2.05f, 5.45f }
                : new[] { -5.45f, -2.05f, 2.2f, 6.25f };
            for (int i = 0; i < benchPositions.Length; i++)
            {
                CreateBench(parent, new Vector2(benchPositions[i], logicalY + innerOffset),
                    facesPositiveY: !upper);
            }

            Vector2 signPosition = new Vector2(upper ? -5f : 5f,
                logicalY + (upper ? 1.35f : -1.35f));
            CreateIsoPrism((upper ? "Upper" : "Lower") + " Direction Sign",
                signPosition, new Vector2(4.6f, 0.24f), 1.85f,
                WallTop, WallLeft, WallRight, parent, false, "DirectionSign");
            CreateLabel(upper ? "DOWNBOUND" : "UPBOUND", Iso(signPosition) + Vector2.up * 1.25f,
                parent, 0.068f, Color.white);
        }

        private static void CreateBench(
            Transform parent,
            Vector2 logicalPosition,
            bool facesPositiveY)
        {
            CreateIsoPrism("Bench Left Leg", logicalPosition + new Vector2(-0.55f, 0f),
                new Vector2(0.16f, 0.32f), 0.42f,
                WallAccent, WallLeft, WallRight, parent, false, "BenchLeg");
            CreateIsoPrism("Bench Right Leg", logicalPosition + new Vector2(0.55f, 0f),
                new Vector2(0.16f, 0.32f), 0.42f,
                WallAccent, WallLeft, WallRight, parent, false, "BenchLeg");
            CreateIsoPrism("Bench Seat", logicalPosition, new Vector2(1.7f, 0.58f), 0.16f,
                Bench, new Color32(93, 47, 27, 255), new Color32(121, 62, 33, 255),
                parent, true, "Bench", 0.42f);
            float backDirection = facesPositiveY ? -1f : 1f;
            CreateIsoPrism("Bench Back", logicalPosition + new Vector2(0f, backDirection * 0.25f),
                new Vector2(1.7f, 0.16f), 0.62f,
                Bench, new Color32(93, 47, 27, 255), new Color32(121, 62, 33, 255),
                parent, false, "BenchBack", 0.54f);
        }

        private static void CreateStationHeader(Transform parent, Vector2 logicalPosition, string text)
        {
            CreateIsoPrism(text + " Sign", logicalPosition, new Vector2(5.2f, 0.32f), 2.05f,
                WallTop, WallLeft, WallRight, parent, false, "StationSign");
            CreateIsoPlane(text + " Accent", logicalPosition + new Vector2(0f, -0.2f),
                new Vector2(4.9f, 0.1f), Teal, parent, "StationAccent", 80);
            CreateLabel(text, Iso(logicalPosition) + Vector2.up * 1.55f,
                parent, 0.067f, Color.white);
        }

        private static QuarterViewTestPortal CreatePortal(string name, Vector2 logicalPosition,
            Vector2 logicalSize, Transform parent)
        {
            GameObject portalObject = new GameObject(name);
            portalObject.transform.SetParent(parent);
            PolygonCollider2D trigger = portalObject.AddComponent<PolygonCollider2D>();
            trigger.points = ProjectRect(logicalPosition, logicalSize);
            trigger.isTrigger = true;
            return portalObject.AddComponent<QuarterViewTestPortal>();
        }

        private static PrismBuild CreateIsoPrism(string name, Vector2 logicalCenter,
            Vector2 logicalSize, float height, Color topColor, Color leftColor, Color rightColor,
            Transform parent, bool obstacle, string materialKey, float baseElevation = 0f)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent);
            Vector2[] basePoints = ProjectRect(logicalCenter, logicalSize);
            Vector2 bottomLift = Vector2.up * baseElevation;
            Vector2 topLift = Vector2.up * (baseElevation + height);
            int sort = GetSortOrder(Iso(logicalCenter));

            CreateFace(name + " Left Face",
                new[] { basePoints[0] + bottomLift, basePoints[3] + bottomLift,
                    basePoints[3] + topLift, basePoints[0] + topLift },
                leftColor, root.transform, materialKey + "Left", sort);
            CreateFace(name + " Right Face",
                new[] { basePoints[0] + bottomLift, basePoints[1] + bottomLift,
                    basePoints[1] + topLift, basePoints[0] + topLift },
                rightColor, root.transform, materialKey + "Right", sort + 1);
            MeshRenderer topRenderer = CreateFace(name + " Top Face",
                new[] { basePoints[0] + topLift, basePoints[1] + topLift,
                    basePoints[2] + topLift, basePoints[3] + topLift },
                topColor, root.transform, materialKey + "Top", sort + 2);

            if (obstacle)
            {
                PolygonCollider2D collider = root.AddComponent<PolygonCollider2D>();
                collider.points = basePoints;
            }
            return new PrismBuild { Root = root, TopRenderer = topRenderer };
        }

        private static MeshRenderer CreateIsoPlane(string name, Vector2 logicalCenter,
            Vector2 logicalSize, Color color, Transform parent, string materialKey, int sortingOrder)
        {
            return CreateFace(name, ProjectRect(logicalCenter, logicalSize),
                color, parent, materialKey, sortingOrder);
        }

        private static MeshRenderer CreateFace(string name, Vector2[] points, Color color,
            Transform parent, string materialKey, int sortingOrder)
        {
            if (points == null || points.Length != 4)
                throw new ArgumentException("Isometric faces require four vertices.", nameof(points));

            GameObject face = new GameObject(name);
            face.transform.SetParent(parent);
            Mesh mesh = new Mesh { name = name + " Mesh" };
            mesh.vertices = new[]
            {
                new Vector3(points[0].x, points[0].y, 0f),
                new Vector3(points[1].x, points[1].y, 0f),
                new Vector3(points[2].x, points[2].y, 0f),
                new Vector3(points[3].x, points[3].y, 0f)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            face.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = face.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetOrCreateMaterial(materialKey, color);
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static GameObject CreateLocalBlock(string name, Vector3 localPosition,
            Vector2 size, Color color, Transform parent, string materialKey, int sortingOrder)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Quad);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;
            block.transform.localScale = new Vector3(size.x, size.y, 1f);
            RemoveMeshCollider(block);
            MeshRenderer renderer = block.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GetOrCreateMaterial(materialKey, color);
            renderer.sortingOrder = sortingOrder;
            return block;
        }

        private static void CreateRoomBoundary(Transform parent, string prefix)
        {
            GameObject boundary = new GameObject(prefix + " Diamond Boundary");
            boundary.transform.SetParent(parent);
            EdgeCollider2D edge = boundary.AddComponent<EdgeCollider2D>();
            edge.edgeRadius = 0.04f;
            edge.points = new[]
            {
                Iso(new Vector2(-RoomHalfWidth, -RoomHalfDepth)),
                Iso(new Vector2(RoomHalfWidth, -RoomHalfDepth)),
                Iso(new Vector2(RoomHalfWidth, RoomHalfDepth)),
                Iso(new Vector2(-RoomHalfWidth, RoomHalfDepth)),
                Iso(new Vector2(-RoomHalfWidth, -RoomHalfDepth))
            };
        }

        private static void CreateLogicalObstacle(string name, Vector2 logicalCenter,
            Vector2 logicalSize, Transform parent)
        {
            GameObject obstacle = new GameObject(name);
            obstacle.transform.SetParent(parent);
            PolygonCollider2D collider = obstacle.AddComponent<PolygonCollider2D>();
            collider.points = ProjectRect(logicalCenter, logicalSize);
        }

        private static Transform CreatePointLogical(string name, Vector2 logicalPosition, Transform parent)
        {
            GameObject point = new GameObject(name);
            point.transform.SetParent(parent);
            Vector2 screenPosition = Iso(logicalPosition);
            point.transform.position = new Vector3(screenPosition.x, screenPosition.y, -1f);
            return point.transform;
        }

        private static void CreateLabel(string text, Vector2 screenPosition, Transform parent,
            float characterSize, Color color)
        {
            GameObject labelObject = new GameObject(text + " Label");
            labelObject.transform.SetParent(parent);
            labelObject.transform.position = new Vector3(screenPosition.x, screenPosition.y, -3.5f);
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = characterSize;
            label.fontSize = 48;
            label.color = color;
            MeshRenderer renderer = labelObject.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = 4500;
        }

        private static Vector2 Iso(Vector2 logicalPosition)
        {
            return new Vector2((logicalPosition.x - logicalPosition.y) * HalfTileWidth,
                (logicalPosition.x + logicalPosition.y) * HalfTileHeight);
        }

        private static Vector2[] ProjectRect(Vector2 logicalCenter, Vector2 logicalSize)
        {
            Vector2 half = logicalSize * 0.5f;
            return new[]
            {
                Iso(logicalCenter + new Vector2(-half.x, -half.y)),
                Iso(logicalCenter + new Vector2(half.x, -half.y)),
                Iso(logicalCenter + new Vector2(half.x, half.y)),
                Iso(logicalCenter + new Vector2(-half.x, half.y))
            };
        }

        private static int GetSortOrder(Vector2 screenPosition)
        {
            return 2400 - Mathf.RoundToInt(screenPosition.y * 100f);
        }

        private static Material GetOrCreateMaterial(string sourceName, Color color)
        {
            string safeName = sourceName.Replace(" ", string.Empty).Replace("/", string.Empty);
            string path = MaterialFolder + "/" + safeName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader == null) throw new InvalidOperationException("No unlit shader is available.");
                material = new Material(shader) { name = safeName };
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void RemoveMeshCollider(GameObject block)
        {
            MeshCollider meshCollider = block.GetComponent<MeshCollider>();
            if (meshCollider != null) UnityEngine.Object.DestroyImmediate(meshCollider);
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/Scenes/Prototype", "QuarterView_Test");
            EnsureFolder(RootFolder, "Scripts");
            EnsureFolder(RootFolder + "/Scripts", "Editor");
            EnsureFolder(RootFolder, "Materials");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }

        private sealed class PrismBuild
        {
            public GameObject Root;
            public MeshRenderer TopRenderer;
        }

        private sealed class ConcourseBuildData
        {
            public GameObject Root;
            public Transform EntranceSpawn;
            public QuarterViewTestPortal UpboundPortal;
            public QuarterViewTestPortal DownboundPortal;
            public Transform UpboundReturn;
            public Transform DownboundReturn;
        }

        private sealed class PlatformBuildData
        {
            public GameObject Root;
            public Transform UpboundArrival;
            public Transform DownboundArrival;
            public QuarterViewTestPortal UpboundReturnPortal;
            public QuarterViewTestPortal DownboundReturnPortal;
        }
    }
}
#endif
