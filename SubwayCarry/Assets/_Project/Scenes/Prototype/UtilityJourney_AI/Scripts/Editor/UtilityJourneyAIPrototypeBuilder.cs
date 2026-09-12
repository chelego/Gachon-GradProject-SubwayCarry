#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using SubwayCarry.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SubwayCarry.AI.UtilityJourney.Editor
{
    public static class UtilityJourneyAIPrototypeBuilder
    {
        public const string ScenePath =
            "Assets/_Project/Scenes/Prototype/UtilityJourney_AI/UtilityJourney_AI.unity";
        public const string BidirectionalTrainLoopScenePath =
            "Assets/_Project/Scenes/Prototype/UtilityJourney_AI/BidirectionalTrainLoop_AI.unity";
        private const string RootFolder =
            "Assets/_Project/Scenes/Prototype/UtilityJourney_AI";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string ProfileFolder = RootFolder + "/Profiles";
        private const float DownboundTrackCenterY = -2.3f;
        private const float DownboundPlatformEdgeY = -4.62f;
        private const float PlatformTrainTargetWidth = 34f;
        private const float PlatformTrainTargetHeight = 4.15f;
        private const float OverviewStationAX = -24f;
        private const float OverviewStationBX = 24f;
        private const float OverviewUpperTrackY = 2.3f;
        private const float OverviewLowerTrackY = -2.3f;
        private const float StairLaneCenterX = 4.15f;
        private const float PlatformSideWallSegmentHeight = 4.6f;
        private const float PlatformSideWallSegmentY = 6.9f;

        private static readonly Color Background = new Color32(6, 12, 21, 255);
        private static readonly Color Floor = new Color32(31, 45, 59, 255);
        private static readonly Color FloorAlt = new Color32(42, 58, 71, 255);
        private static readonly Color Wall = new Color32(70, 91, 109, 255);
        private static readonly Color GateHousing = new Color32(12, 17, 23, 255);
        private static readonly Color Gate = new Color32(241, 124, 37, 255);
        private static readonly Color Stair = new Color32(242, 205, 52, 255);
        private static readonly Color Platform = new Color32(101, 114, 121, 255);
        private static readonly Color Track = new Color32(10, 14, 19, 255);
        private static readonly Color Rail = new Color32(139, 151, 158, 255);
        private static readonly Color Train = new Color32(44, 151, 173, 255);
        private static readonly Color Passenger = new Color32(48, 211, 144, 255);

        [MenuItem("SubwayCarry/Prototype/Build Utility Journey AI Scene")]
        public static void Build()
        {
            EnsureFolders();
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            GameObject root = new GameObject("Utility Journey AI Prototype");
            CreateCamera(root.transform);
            UiBuildData ui = CreateUi(root.transform);

            var allFacilities = new List<UtilityJourneyFacilityPrototype>();
            var navigationBindings = new List<UtilityJourneyNavigationBinding>();

            ConcourseBuildData originConcourse = CreateConcourse(
                root.transform,
                UtilityJourneyArea.OriginConcourse,
                "GACHON UNIVERSITY / FARE CONCOURSE",
                true,
                allFacilities,
                navigationBindings);
            PlatformBuildData originPlatform = CreatePlatform(
                root.transform,
                scene,
                UtilityJourneyArea.OriginPlatform,
                "GACHON UNIVERSITY / PLATFORM",
                true,
                allFacilities,
                navigationBindings);
            ConcourseBuildData destinationConcourse = CreateConcourse(
                root.transform,
                UtilityJourneyArea.DestinationConcourse,
                "DESTINATION STATION / EXIT CONCOURSE",
                false,
                allFacilities,
                navigationBindings);
            PlatformBuildData destinationPlatform = CreatePlatform(
                root.transform,
                scene,
                UtilityJourneyArea.DestinationPlatform,
                "DESTINATION STATION / PLATFORM",
                false,
                allFacilities,
                navigationBindings);
            TrainBuildData train = CreateTrainInterior(
                root.transform,
                scene,
                allFacilities,
                navigationBindings);

            ConfigureStairTransitions(
                originConcourse.UpboundStairs,
                UtilityJourneyArea.OriginPlatform,
                originPlatform.UpboundArrivals);
            ConfigureStairTransitions(
                originConcourse.DownboundStairs,
                UtilityJourneyArea.OriginPlatform,
                originPlatform.DownboundArrivals);
            foreach (UtilityJourneyFacilityPrototype door in originPlatform.BoardingDoors)
            {
                door.ConfigureTransition(
                    UtilityJourneyArea.TrainInterior,
                    train.BoardingSpawn);
            }

            train.AlightDoor.ConfigureTransition(
                UtilityJourneyArea.DestinationPlatform,
                destinationPlatform.AlightArrival.Spawn,
                destinationPlatform.AlightArrival.Entry,
                destinationPlatform.AlightArrival.Release);
            ConfigureStairTransitions(
                destinationPlatform.UpStairs,
                UtilityJourneyArea.DestinationConcourse,
                destinationConcourse.LowerArrivals);

            UtilityPassengerBrainPrototype passenger = CreatePassenger(
                root.transform,
                ui.ThoughtText);
            GameObject worldObject = new GameObject("Utility Journey World Model");
            worldObject.transform.SetParent(root.transform);
            UtilityJourneyWorldPrototype world =
                worldObject.AddComponent<UtilityJourneyWorldPrototype>();

            var screens = new[]
            {
                new UtilityJourneyAreaScreen(
                    UtilityJourneyArea.OriginConcourse,
                    originConcourse.Root),
                new UtilityJourneyAreaScreen(
                    UtilityJourneyArea.OriginPlatform,
                    originPlatform.Root),
                new UtilityJourneyAreaScreen(
                    UtilityJourneyArea.TrainInterior,
                    train.Root),
                new UtilityJourneyAreaScreen(
                    UtilityJourneyArea.DestinationPlatform,
                    destinationPlatform.Root),
                new UtilityJourneyAreaScreen(
                    UtilityJourneyArea.DestinationConcourse,
                    destinationConcourse.Root)
            };
            world.Configure(
                passenger,
                screens,
                originConcourse.ExternalSpawns,
                destinationConcourse.StreetExits,
                ui.FadeOverlay,
                ui.WorldText,
                originPlatform.TrainVisual,
                destinationPlatform.TrainVisual,
                train.TrainVisual,
                originPlatform.TrainDoors,
                destinationPlatform.TrainDoors,
                UtilityJourneyDirection.Downbound);
            passenger.Configure(
                world,
                allFacilities.ToArray(),
                navigationBindings.ToArray(),
                ui.ThoughtText,
                0,
                GetOrCreatePassengerProfiles());

            foreach (UtilityJourneyAreaScreen screen in screens)
            {
                screen.Root.SetActive(
                    screen.Area == UtilityJourneyArea.OriginConcourse);
            }

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException(
                    "UtilityJourney_AI scene could not be saved.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log(
                "[UtilityJourneyAI] Built one-passenger utility journey at " +
                ScenePath + ". No authored PassengerJourneyWaypoint is used.");
        }

        [MenuItem("SubwayCarry/Prototype/Build Bidirectional Train Loop AI Scene")]
        public static void BuildBidirectionalTrainLoop()
        {
            EnsureFolders();
            Scene previousScene = SceneManager.GetActiveScene();
            bool rebuildingOpenLoopScene =
                previousScene.IsValid() &&
                string.Equals(
                    previousScene.path,
                    BidirectionalTrainLoopScenePath,
                    StringComparison.OrdinalIgnoreCase);
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                rebuildingOpenLoopScene
                    ? NewSceneMode.Single
                    : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            try
            {
                GameObject root = new GameObject("Bidirectional Train Loop Prototype");
                CreateOverviewCamera(root.transform);
                CreateOverviewLight(root.transform);
                CreateOverviewTrackLayout(root.transform);
                OverviewStationBuildData stationA = CreateExistingOverviewStation(
                    root.transform,
                    scene,
                    OverviewStationAX,
                    "STATION A",
                    true);
                OverviewStationBuildData stationB = CreateExistingOverviewStation(
                    root.transform,
                    scene,
                    OverviewStationBX,
                    "STATION B",
                    false);

                GameObject trains = new GameObject("TRAINS");
                trains.transform.SetParent(root.transform);
                OverviewTrainBuildData downboundTrain = CreateOverviewTrain(
                    trains.transform,
                    scene,
                    "Downbound Train A to B",
                    new Vector3(OverviewStationAX, OverviewLowerTrackY, 0f),
                    false,
                    UtilityJourneyDirection.Downbound);
                OverviewTrainBuildData upboundTrain = CreateOverviewTrain(
                    trains.transform,
                    scene,
                    "Upbound Train B to A",
                    new Vector3(OverviewStationBX, OverviewUpperTrackY, 0f),
                    true,
                    UtilityJourneyDirection.Upbound);

                Text statusText = CreateOverviewStatusUi(root.transform);
                GameObject controllerObject = new GameObject("20s Dwell - 30s Travel Controller");
                controllerObject.transform.SetParent(root.transform);
                BidirectionalTrainLoopPrototype controller =
                    controllerObject.AddComponent<BidirectionalTrainLoopPrototype>();
                controller.Configure(
                    downboundTrain.Root,
                    downboundTrain.PlatformDoors,
                    upboundTrain.Root,
                    upboundTrain.PlatformDoors,
                    statusText,
                    new Vector3(OverviewStationAX, OverviewLowerTrackY, 0f),
                    new Vector3(OverviewStationBX, OverviewLowerTrackY, 0f),
                    new Vector3(OverviewStationAX, OverviewUpperTrackY, 0f),
                    new Vector3(OverviewStationBX, OverviewUpperTrackY, 0f),
                    30f,
                    6f,
                    90f,
                    2f,
                    10f,
                    2f,
                    1f);

                OverviewJourneyBuildData downboundJourney = CreateOverviewJourney(
                    root.transform,
                    stationA,
                    stationB,
                    downboundTrain);
                OverviewJourneyBuildData upboundJourney = CreateReverseOverviewJourney(
                    root.transform,
                    stationA,
                    stationB,
                    upboundTrain);
                UtilityPassengerTuningProfile[] profiles =
                    GetOrCreatePassengerProfiles();
                CreateOverviewPassengerGroup(
                    root.transform,
                    controller,
                    downboundTrain,
                    downboundJourney,
                    UtilityJourneyDirection.Downbound,
                    "A-to-B",
                    4,
                    1000,
                    profiles);
                CreateOverviewPassengerGroup(
                    root.transform,
                    controller,
                    upboundTrain,
                    upboundJourney,
                    UtilityJourneyDirection.Upbound,
                    "B-to-A",
                    4,
                    2000,
                    profiles);
                RemoveAllWorldText(root);

                if (!EditorSceneManager.SaveScene(scene, BidirectionalTrainLoopScenePath))
                {
                    throw new InvalidOperationException(
                        "BidirectionalTrainLoop_AI scene could not be saved.");
                }
            }
            finally
            {
                if (!rebuildingOpenLoopScene &&
                    previousScene.IsValid() &&
                    previousScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousScene);
                }

                if (!rebuildingOpenLoopScene && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SceneAsset builtScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                BidirectionalTrainLoopScenePath);
            Selection.activeObject = builtScene;
            EditorGUIUtility.PingObject(builtScene);
            Debug.Log(
                "[BidirectionalTrainLoop] Built one-passenger full-cycle overview at " +
                BidirectionalTrainLoopScenePath +
                ". A gate > platform > board > choose ride activity > B exit; " +
                "station dwell uses 2s/open10s/2s and A-B travel=30s.");
        }

        private static void CreateOverviewTrackLayout(Transform parent)
        {
            GameObject layout = new GameObject("A-B OVERVIEW LAYOUT");
            layout.transform.SetParent(parent);

            CreateBlock(layout.transform, "Track Bed - Upbound", new Vector2(0f, OverviewUpperTrackY),
                new Vector2(84f, 4.35f), Track, false, -18);
            CreateBlock(layout.transform, "Track Bed - Downbound", new Vector2(0f, OverviewLowerTrackY),
                new Vector2(84f, 4.35f), Track, false, -18);

            float[] trackCenters = { OverviewUpperTrackY, OverviewLowerTrackY };
            foreach (float centerY in trackCenters)
            {
                CreateBlock(layout.transform, "Rail North", new Vector2(0f, centerY + 0.72f),
                    new Vector2(84f, 0.12f), Rail, false, -16);
                CreateBlock(layout.transform, "Rail South", new Vector2(0f, centerY - 0.72f),
                    new Vector2(84f, 0.12f), Rail, false, -16);
                for (float x = -41f; x <= 41f; x += 2f)
                {
                    CreateBlock(layout.transform, "Sleeper", new Vector2(x, centerY),
                        new Vector2(0.18f, 1.75f), FloorAlt, false, -17);
                }
            }
        }

        private static OverviewStationBuildData CreateExistingOverviewStation(
            Transform parent,
            Scene targetScene,
            float stationX,
            string stationName,
            bool origin)
        {
            GameObject station = new GameObject(stationName);
            station.transform.SetParent(parent);
            station.transform.position = Vector3.zero;

            GameObject concourse = CloneUtilityJourneyMapVisual(
                targetScene,
                station.transform,
                origin
                    ? "MAP 1A - Origin Fare Concourse"
                    : "MAP 1B - Destination Fare Concourse",
                stationName + " - Existing Fare Concourse",
                new Vector3(stationX, 17f, 0f),
                false);
            GameObject platform = CloneUtilityJourneyMapVisual(
                targetScene,
                station.transform,
                origin
                    ? "MAP 2A - Origin Bidirectional Platform"
                    : "MAP 2B - Destination Bidirectional Platform",
                stationName + " - Existing Bidirectional Platform",
                new Vector3(stationX, 0f, 0f),
                true);
            RestoreOverviewStationOpenings(concourse, platform);
            MoveWallWaitingSpotsAwayFromCentralStairs(platform, stationX);

            return new OverviewStationBuildData
            {
                Root = station.transform,
                Concourse = concourse,
                Platform = platform,
                ConcourseNavigation = concourse
                    .GetComponentsInChildren<GridNavigation2D>(true)
                    .First(item => item.name == "Concourse Navigation"),
                LowerPlatformNavigation = platform
                    .GetComponentsInChildren<GridNavigation2D>(true)
                    .First(item => item.name == "Lower Platform Navigation"),
                UpperPlatformNavigation = platform
                    .GetComponentsInChildren<GridNavigation2D>(true)
                    .First(item => item.name == "Upper Platform Navigation"),
                Facilities = station
                    .GetComponentsInChildren<UtilityJourneyFacilityPrototype>(true),
                ExternalSpawns = concourse
                    .GetComponentsInChildren<Transform>(true)
                    .Where(item => item.name.StartsWith(
                        "External Stair Spawn ",
                        StringComparison.Ordinal))
                    .OrderBy(item => item.name)
                    .ToArray(),
                ExternalStairVisuals = concourse
                    .GetComponentsInChildren<Transform>(true)
                    .Where(item => item.name.StartsWith(
                                       "Exit ",
                                       StringComparison.Ordinal) &&
                                   item.name.EndsWith(
                                       " Street Stair",
                                       StringComparison.Ordinal))
                    .OrderBy(item => item.name)
                    .ToArray()
            };
        }

        private static void MoveWallWaitingSpotsAwayFromCentralStairs(
            GameObject platform,
            float stationX)
        {
            UtilityJourneyFacilityPrototype[] wallWaitingSpots = platform
                .GetComponentsInChildren<UtilityJourneyFacilityPrototype>(true)
                .Where(item =>
                    item.Kind == UtilityJourneyFacilityKind.PlatformWaitingArea &&
                    item.WaitingStyle == UtilityJourneyWaitingStyle.WallRest)
                .ToArray();
            foreach (UtilityJourneyFacilityPrototype spot in wallWaitingSpots)
            {
                float offset = spot.transform.position.x - stationX;
                if (Mathf.Abs(offset) >= 7.25f || Mathf.Abs(offset) < 0.01f)
                {
                    continue;
                }

                Vector3 position = spot.transform.position;
                position.x = stationX + Mathf.Sign(offset) * 10f;
                spot.transform.position = position;
            }
        }

        private static void RestoreOverviewStationOpenings(
            GameObject concourse,
            GameObject platform)
        {
            NormalizeOverviewStairLane(
                concourse,
                "Upper Platform Stairs left",
                -StairLaneCenterX);
            NormalizeOverviewStairLane(
                concourse,
                "Upper Platform Stairs right",
                StairLaneCenterX);
            NormalizeOverviewStairLane(
                concourse,
                "Lower Platform Stairs left",
                -StairLaneCenterX);
            NormalizeOverviewStairLane(
                concourse,
                "Lower Platform Stairs right",
                StairLaneCenterX);
            NormalizeOverviewStairLane(
                platform,
                "Upper Center Stairs left",
                -StairLaneCenterX);
            NormalizeOverviewStairLane(
                platform,
                "Upper Center Stairs right",
                StairLaneCenterX);
            NormalizeOverviewStairLane(
                platform,
                "Lower Center Stairs left",
                -StairLaneCenterX);
            NormalizeOverviewStairLane(
                platform,
                "Lower Center Stairs right",
                StairLaneCenterX);
            SplitOverviewPlatformEndWall(platform, "Left Wall");
            SplitOverviewPlatformEndWall(platform, "Right Wall");
        }

        private static void NormalizeOverviewStairLane(
            GameObject root,
            string lanePrefix,
            float targetCenterX)
        {
            Transform[] laneParts = root
                .GetComponentsInChildren<Transform>(true)
                .Where(item => item != root.transform &&
                               item.name.StartsWith(
                                   lanePrefix,
                                   StringComparison.Ordinal))
                .ToArray();
            Transform laneVisual = laneParts.FirstOrDefault(item =>
                string.Equals(item.name, lanePrefix, StringComparison.Ordinal));
            if (laneVisual == null)
            {
                return;
            }

            var facilityOffsets = root
                .GetComponentsInChildren<UtilityJourneyFacilityPrototype>(true)
                .Where(item => item.TraversalEntry != null &&
                               laneParts.Contains(item.TraversalEntry))
                .ToDictionary(
                    item => item,
                    item => item.transform.position - item.TraversalEntry.position);
            float deltaX = targetCenterX - laneVisual.localPosition.x;
            foreach (Transform part in laneParts)
            {
                Vector3 position = part.localPosition;
                position.x += deltaX;
                part.localPosition = position;
            }

            foreach (KeyValuePair<UtilityJourneyFacilityPrototype, Vector3> pair in
                     facilityOffsets)
            {
                pair.Key.transform.position =
                    pair.Key.TraversalEntry.position + pair.Value;
            }
        }

        private static void SplitOverviewPlatformEndWall(
            GameObject platform,
            string wallName)
        {
            Transform wall = platform
                .GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item != platform.transform &&
                                        string.Equals(
                                            item.name,
                                            wallName,
                                            StringComparison.Ordinal));
            if (wall == null)
            {
                return;
            }

            CreateOverviewPlatformWallSegment(
                wall,
                wallName + " Upper Platform Segment",
                PlatformSideWallSegmentY);
            CreateOverviewPlatformWallSegment(
                wall,
                wallName + " Lower Platform Segment",
                -PlatformSideWallSegmentY);
            UnityEngine.Object.DestroyImmediate(wall.gameObject);
        }

        private static void CreateOverviewPlatformWallSegment(
            Transform source,
            string name,
            float localY)
        {
            GameObject segment = UnityEngine.Object.Instantiate(
                source.gameObject,
                source.parent);
            segment.name = name;
            Vector3 position = segment.transform.localPosition;
            position.y = localY;
            segment.transform.localPosition = position;
            Vector3 scale = segment.transform.localScale;
            scale.y = PlatformSideWallSegmentHeight;
            segment.transform.localScale = scale;
        }

        private static GameObject CloneUtilityJourneyMapVisual(
            Scene targetScene,
            Transform parent,
            string sourceObjectName,
            string cloneName,
            Vector3 position,
            bool removePlatformTrain)
        {
            Scene sourceScene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !sourceScene.IsValid() || !sourceScene.isLoaded;
            if (openedHere)
            {
                sourceScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            try
            {
                GameObject source = sourceScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Select(item => item.gameObject)
                    .FirstOrDefault(item => item.name == sourceObjectName);
                if (source == null)
                {
                    throw new InvalidOperationException(
                        sourceObjectName + " was not found in " + ScenePath + ".");
                }

                SceneManager.SetActiveScene(targetScene);
                GameObject clone = UnityEngine.Object.Instantiate(source);
                clone.name = cloneName;
                SceneManager.MoveGameObjectToScene(clone, targetScene);
                clone.transform.SetParent(parent, true);
                clone.transform.position = position;
                clone.transform.localScale = Vector3.one;
                clone.SetActive(true);
                if (removePlatformTrain)
                {
                    RemoveNamedDescendants(
                        clone,
                        "Arriving Train Visual",
                        "Destination Train Visual");
                }

                StripOverviewMapToJourney(clone);
                NormalizeClonedFacilityReferences(clone);
                return clone;
            }
            finally
            {
                if (openedHere && sourceScene.IsValid() && sourceScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(sourceScene, true);
                }

                SceneManager.SetActiveScene(targetScene);
            }
        }

        private static void RemoveNamedDescendants(
            GameObject root,
            params string[] names)
        {
            GameObject[] targets = root.GetComponentsInChildren<Transform>(true)
                .Where(item => item != null && item.gameObject != root)
                .Where(item => names.Contains(item.name))
                .Select(item => item.gameObject)
                .Distinct()
                .ToArray();
            foreach (GameObject target in targets)
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void StripOverviewMapToJourney(GameObject root)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null &&
                    !(behaviour is GridNavigation2D) &&
                    !(behaviour is NavigationObstacle) &&
                    !(behaviour is UtilityJourneyFacilityPrototype) &&
                    !(behaviour is UtilityJourneyPassagePrototype))
                {
                    UnityEngine.Object.DestroyImmediate(behaviour);
                }
            }

            foreach (Collider2D collider in root.GetComponentsInChildren<Collider2D>(true))
            {
                if (collider.GetComponentInParent<NavigationObstacle>() == null)
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                }
            }

            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        private static void NormalizeClonedFacilityReferences(GameObject root)
        {
            foreach (UtilityJourneyFacilityPrototype facility in
                     root.GetComponentsInChildren<UtilityJourneyFacilityPrototype>(true))
            {
                Transform traversalEntry = IsInsideClone(root, facility.TraversalEntry)
                    ? facility.TraversalEntry
                    : null;
                Transform traversalDestination =
                    IsInsideClone(root, facility.TraversalDestination)
                        ? facility.TraversalDestination
                        : null;
                UtilityJourneyPassagePrototype passage =
                    facility.Passage != null &&
                    IsInsideClone(root, facility.Passage.transform)
                        ? facility.Passage
                        : null;
                facility.ConfigureTraversal(
                    traversalEntry,
                    traversalDestination,
                    passage);

                Transform destinationSpawn = IsInsideClone(root, facility.DestinationSpawn)
                    ? facility.DestinationSpawn
                    : null;
                Transform destinationEntry = IsInsideClone(root, facility.DestinationEntry)
                    ? facility.DestinationEntry
                    : null;
                Transform destinationRelease =
                    IsInsideClone(root, facility.DestinationRelease)
                        ? facility.DestinationRelease
                        : null;
                facility.ConfigureTransition(
                    facility.DestinationArea,
                    destinationSpawn,
                    destinationEntry,
                    destinationRelease);
            }
        }

        private static bool IsInsideClone(GameObject root, Transform target)
        {
            return target != null &&
                   (target == root.transform || target.IsChildOf(root.transform));
        }

        private static void RemoveAllWorldText(GameObject root)
        {
            TextMesh[] labels = root.GetComponentsInChildren<TextMesh>(true);
            foreach (TextMesh label in labels)
            {
                if (label != null)
                {
                    UnityEngine.Object.DestroyImmediate(label.gameObject);
                }
            }
        }

        private static OverviewJourneyBuildData CreateOverviewJourney(
            Transform parent,
            OverviewStationBuildData stationA,
            OverviewStationBuildData stationB,
            OverviewTrainBuildData downboundTrain)
        {
            var facilities = new List<UtilityJourneyFacilityPrototype>();

            UtilityJourneyFacilityPrototype[] originDownStairs = stationA.Facilities
                .Where(item => item.Area == UtilityJourneyArea.OriginConcourse &&
                               item.Kind == UtilityJourneyFacilityKind.PlatformDownStair &&
                               item.Direction == UtilityJourneyDirection.Downbound)
                .ToArray();
            ConfigureOverviewStairTransitions(
                originDownStairs,
                stationA.Platform.transform,
                "Lower Center Stairs",
                UtilityJourneyArea.OriginPlatform);

            UtilityJourneyFacilityPrototype[] destinationUpStairs = stationB.Facilities
                .Where(item => item.Area == UtilityJourneyArea.DestinationPlatform &&
                               item.Kind == UtilityJourneyFacilityKind.PlatformUpStair)
                .ToArray();
            ConfigureOverviewStairTransitions(
                destinationUpStairs,
                stationB.Concourse.transform,
                "Lower Platform Stairs",
                UtilityJourneyArea.DestinationConcourse);

            facilities.AddRange(stationA.Facilities.Where(item =>
                (item.Area == UtilityJourneyArea.OriginConcourse &&
                 item.Kind == UtilityJourneyFacilityKind.EntryGate) ||
                (item.Area == UtilityJourneyArea.OriginConcourse &&
                 item.Kind == UtilityJourneyFacilityKind.PlatformDownStair &&
                 item.Direction == UtilityJourneyDirection.Downbound) ||
                (item.Area == UtilityJourneyArea.OriginPlatform &&
                 item.Kind == UtilityJourneyFacilityKind.PlatformWaitingArea)));

            float stationDeltaX = OverviewStationBX - OverviewStationAX;
            for (int index = 0; index < downboundTrain.PlatformDoors.Length; index++)
            {
                TrainDoorController door = downboundTrain.PlatformDoors[index];
                Transform inside = downboundTrain.InsideDoorPoints[index];
                UtilityJourneyFacilityPrototype boarding = CreateFacility(
                    stationA.Platform.transform,
                    "overview-board-door-" + (index + 1),
                    UtilityJourneyFacilityKind.TrainDoor,
                    UtilityJourneyArea.OriginPlatform,
                    UtilityJourneyDirection.Downbound,
                    new Vector2(door.transform.position.x, -5.32f),
                    0.34f,
                    0.12f,
                    index * 0.02f,
                    0f,
                    facilities,
                    UtilityJourneyWaitingStyle.None,
                    "downbound-door-" + (index + 1));
                boarding.ConfigureTraversal(door.transform, inside);
                boarding.ConfigureTransition(UtilityJourneyArea.TrainInterior, inside);

                float destinationDoorX = door.transform.position.x + stationDeltaX;
                Transform destinationDoor = CreatePoint(
                    stationB.Platform.transform,
                    "Downbound Arrival Door " + (index + 1),
                    new Vector2(destinationDoorX, DownboundPlatformEdgeY));
                Transform destinationRelease = CreatePoint(
                    stationB.Platform.transform,
                    "Downbound Arrival Release " + (index + 1),
                    new Vector2(destinationDoorX, -5.45f));
                UtilityJourneyFacilityPrototype alight =
                    downboundTrain.AlightFacilities[index];
                alight.ConfigureTraversal(door.transform, destinationDoor);
                alight.ConfigureTransition(
                    UtilityJourneyArea.DestinationPlatform,
                    destinationDoor,
                    destinationRelease,
                    null);
            }

            facilities.AddRange(downboundTrain.RideFacilities);
            facilities.AddRange(downboundTrain.AlightFacilities);
            facilities.AddRange(stationB.Facilities.Where(item =>
                (item.Area == UtilityJourneyArea.DestinationPlatform &&
                 item.Kind == UtilityJourneyFacilityKind.PlatformUpStair) ||
                (item.Area == UtilityJourneyArea.DestinationConcourse &&
                 item.Kind == UtilityJourneyFacilityKind.ExitGate) ||
                (item.Area == UtilityJourneyArea.DestinationConcourse &&
                 item.Kind == UtilityJourneyFacilityKind.StreetExit)));

            UtilityJourneyFacilityPrototype[] exits = stationB.Facilities
                .Where(item => item.Area == UtilityJourneyArea.DestinationConcourse &&
                               item.Kind == UtilityJourneyFacilityKind.StreetExit)
                .ToArray();
            UtilityJourneyFacilityPrototype[] orderedExits = exits
                .OrderBy(item => item.FacilityId)
                .ToArray();
            int pairedExitCount = Mathf.Min(
                orderedExits.Length,
                stationB.ExternalStairVisuals.Length);
            for (int index = 0; index < pairedExitCount; index++)
            {
                orderedExits[index].ConfigureTraversal(
                    null,
                    stationB.ExternalStairVisuals[index]);
            }

            return new OverviewJourneyBuildData
            {
                Facilities = facilities.Where(item => item != null).Distinct().ToArray(),
                NavigationBindings = new[]
                {
                    new UtilityJourneyNavigationBinding(
                        UtilityJourneyArea.OriginConcourse,
                        stationA.ConcourseNavigation),
                    new UtilityJourneyNavigationBinding(
                        UtilityJourneyArea.OriginPlatform,
                        stationA.LowerPlatformNavigation),
                    new UtilityJourneyNavigationBinding(
                        UtilityJourneyArea.TrainInterior,
                        downboundTrain.InteriorNavigation),
                    new UtilityJourneyNavigationBinding(
                        UtilityJourneyArea.DestinationPlatform,
                        stationB.LowerPlatformNavigation),
                    new UtilityJourneyNavigationBinding(
                        UtilityJourneyArea.DestinationConcourse,
                        stationB.ConcourseNavigation)
                },
                OriginEntranceOutsidePoints = stationA.ExternalStairVisuals,
                OriginEntranceSpawns = stationA.ExternalSpawns,
                DestinationExits = exits
            };
        }

        private static OverviewJourneyBuildData CreateReverseOverviewJourney(
            Transform parent,
            OverviewStationBuildData stationA,
            OverviewStationBuildData stationB,
            OverviewTrainBuildData upboundTrain)
        {
            GameObject routeRootObject = new GameObject("B-to-A Utility Facilities");
            routeRootObject.transform.SetParent(parent);
            Transform routeRoot = routeRootObject.transform;
            var facilities = new List<UtilityJourneyFacilityPrototype>();

            foreach (UtilityJourneyFacilityPrototype source in stationB.Facilities
                         .Where(item => item.Kind == UtilityJourneyFacilityKind.ExitGate &&
                                        item.TraversalEntry != null &&
                                        item.TraversalDestination != null))
            {
                UtilityJourneyFacilityPrototype entry = CreateFacility(
                    routeRoot,
                    "reverse-entry-" + source.FacilityId,
                    UtilityJourneyFacilityKind.EntryGate,
                    UtilityJourneyArea.OriginConcourse,
                    UtilityJourneyDirection.Any,
                    source.TraversalDestination.position,
                    source.AcceptanceRadius,
                    source.InteractionSeconds,
                    source.CrowdCost,
                    source.EstimatedQueueSeconds,
                    facilities,
                    UtilityJourneyWaitingStyle.None,
                    source.RouteGroup);
                entry.ConfigureTraversal(
                    source.TraversalEntry,
                    source.transform,
                    source.Passage);
            }

            string[] sides = { "left", "right" };
            foreach (string side in sides)
            {
                Transform approach = FindNamedTransform(
                    stationB.Concourse.transform,
                    "Upper Platform Stairs " + side + " Approach");
                Transform entry = FindNamedTransform(
                    stationB.Concourse.transform,
                    "Upper Platform Stairs " + side + " Entry");
                Transform deep = FindNamedTransform(
                    stationB.Concourse.transform,
                    "Upper Platform Stairs " + side + " Deep");
                Transform destinationSpawn = FindNamedTransform(
                    stationB.Platform.transform,
                    "Upper Center Stairs " + side + " Deep");
                Transform destinationEntry = FindNamedTransform(
                    stationB.Platform.transform,
                    "Upper Center Stairs " + side + " Entry");
                Transform destinationRelease = FindNamedTransform(
                    stationB.Platform.transform,
                    "Upper Center Stairs " + side + " Approach");
                RequireOverviewTransitionPoints(
                    "B upbound stair " + side,
                    approach,
                    entry,
                    deep,
                    destinationSpawn,
                    destinationEntry,
                    destinationRelease);
                UtilityJourneyFacilityPrototype stair = CreateFacility(
                    routeRoot,
                    "upbound-platform-stair-" + side,
                    UtilityJourneyFacilityKind.PlatformDownStair,
                    UtilityJourneyArea.OriginConcourse,
                    UtilityJourneyDirection.Upbound,
                    approach.position,
                    0.38f,
                    0.22f,
                    side == "left" ? 0.08f : 0.1f,
                    0f,
                    facilities,
                    UtilityJourneyWaitingStyle.None,
                    side);
                stair.ConfigureTraversal(entry, deep);
                stair.ConfigureTransition(
                    UtilityJourneyArea.OriginPlatform,
                    destinationSpawn,
                    destinationEntry,
                    destinationRelease);
            }

            float stationBX = stationB.Platform.transform.position.x;
            float[] benchOffsets = { -15f, -8.5f, 8.5f, 15f };
            for (int index = 0; index < benchOffsets.Length; index++)
            {
                CreateBenchVisual(
                    stationB.Platform.transform,
                    new Vector2(stationBX + benchOffsets[index], 8.05f));
                CreateWaitingFacility(
                    routeRoot,
                    "upbound-bench-seat-" + (index + 1),
                    UtilityJourneyArea.OriginPlatform,
                    new Vector2(stationBX + benchOffsets[index], 7.78f),
                    UtilityJourneyWaitingStyle.BenchSeat,
                    index * 0.025f,
                    facilities,
                    UtilityJourneyDirection.Upbound);
            }

            float[] wallOffsets = { -13f, -10f, 10f, 13f };
            for (int index = 0; index < wallOffsets.Length; index++)
            {
                CreateWaitingFacility(
                    routeRoot,
                    "upbound-wall-rest-" + (index + 1),
                    UtilityJourneyArea.OriginPlatform,
                    new Vector2(stationBX + wallOffsets[index], 8.45f),
                    UtilityJourneyWaitingStyle.WallRest,
                    index * 0.02f,
                    facilities,
                    UtilityJourneyDirection.Upbound);
            }

            float stationDeltaX = OverviewStationAX - OverviewStationBX;
            for (int index = 0; index < upboundTrain.PlatformDoors.Length; index++)
            {
                TrainDoorController door = upboundTrain.PlatformDoors[index];
                Transform inside = upboundTrain.InsideDoorPoints[index];
                for (int side = -1; side <= 1; side += 2)
                {
                    CreateWaitingFacility(
                        routeRoot,
                        "upbound-door-queue-" + (index + 1) + "-" +
                        (side < 0 ? "left" : "right"),
                        UtilityJourneyArea.OriginPlatform,
                        new Vector2(door.transform.position.x + side * 0.9f, 5.65f),
                        UtilityJourneyWaitingStyle.DoorQueue,
                        index * 0.03f,
                        facilities,
                        UtilityJourneyDirection.Upbound);
                }

                UtilityJourneyFacilityPrototype boarding = CreateFacility(
                    routeRoot,
                    "overview-upbound-board-door-" + (index + 1),
                    UtilityJourneyFacilityKind.TrainDoor,
                    UtilityJourneyArea.OriginPlatform,
                    UtilityJourneyDirection.Upbound,
                    new Vector2(door.transform.position.x, 5.32f),
                    0.34f,
                    0.12f,
                    index * 0.02f,
                    0f,
                    facilities,
                    UtilityJourneyWaitingStyle.None,
                    "upbound-door-" + (index + 1));
                boarding.ConfigureTraversal(door.transform, inside);
                boarding.ConfigureTransition(UtilityJourneyArea.TrainInterior, inside);

                float destinationDoorX = door.transform.position.x + stationDeltaX;
                Transform destinationDoor = CreatePoint(
                    stationA.Platform.transform,
                    "Upbound Arrival Door " + (index + 1),
                    new Vector2(destinationDoorX, -DownboundPlatformEdgeY));
                Transform destinationRelease = CreatePoint(
                    stationA.Platform.transform,
                    "Upbound Arrival Release " + (index + 1),
                    new Vector2(destinationDoorX, 5.45f));
                UtilityJourneyFacilityPrototype alight =
                    upboundTrain.AlightFacilities[index];
                alight.ConfigureTraversal(door.transform, destinationDoor);
                alight.ConfigureTransition(
                    UtilityJourneyArea.DestinationPlatform,
                    destinationDoor,
                    destinationRelease,
                    null);
            }

            facilities.AddRange(upboundTrain.RideFacilities);
            facilities.AddRange(upboundTrain.AlightFacilities);

            foreach (string side in sides)
            {
                Transform approach = FindNamedTransform(
                    stationA.Platform.transform,
                    "Upper Center Stairs " + side + " Approach");
                Transform entry = FindNamedTransform(
                    stationA.Platform.transform,
                    "Upper Center Stairs " + side + " Entry");
                Transform deep = FindNamedTransform(
                    stationA.Platform.transform,
                    "Upper Center Stairs " + side + " Deep");
                Transform destinationSpawn = FindNamedTransform(
                    stationA.Concourse.transform,
                    "Upper Platform Stairs " + side + " Deep");
                Transform destinationEntry = FindNamedTransform(
                    stationA.Concourse.transform,
                    "Upper Platform Stairs " + side + " Entry");
                Transform destinationRelease = FindNamedTransform(
                    stationA.Concourse.transform,
                    "Upper Platform Stairs " + side + " Approach");
                RequireOverviewTransitionPoints(
                    "A upbound exit stair " + side,
                    approach,
                    entry,
                    deep,
                    destinationSpawn,
                    destinationEntry,
                    destinationRelease);
                UtilityJourneyFacilityPrototype stair = CreateFacility(
                    routeRoot,
                    "reverse-upbound-platform-up-stair-" + side,
                    UtilityJourneyFacilityKind.PlatformUpStair,
                    UtilityJourneyArea.DestinationPlatform,
                    UtilityJourneyDirection.Upbound,
                    approach.position,
                    0.38f,
                    0.22f,
                    side == "left" ? 0.08f : 0.1f,
                    0f,
                    facilities,
                    UtilityJourneyWaitingStyle.None,
                    side);
                stair.ConfigureTraversal(entry, deep);
                stair.ConfigureTransition(
                    UtilityJourneyArea.DestinationConcourse,
                    destinationSpawn,
                    destinationEntry,
                    destinationRelease);
            }

            foreach (UtilityJourneyFacilityPrototype source in stationA.Facilities
                         .Where(item => item.Kind == UtilityJourneyFacilityKind.EntryGate &&
                                        item.TraversalEntry != null &&
                                        item.TraversalDestination != null))
            {
                UtilityJourneyFacilityPrototype exitGate = CreateFacility(
                    routeRoot,
                    "reverse-exit-" + source.FacilityId,
                    UtilityJourneyFacilityKind.ExitGate,
                    UtilityJourneyArea.DestinationConcourse,
                    UtilityJourneyDirection.Any,
                    source.TraversalDestination.position,
                    source.AcceptanceRadius,
                    source.InteractionSeconds,
                    source.CrowdCost,
                    source.EstimatedQueueSeconds,
                    facilities,
                    UtilityJourneyWaitingStyle.None,
                    source.RouteGroup);
                exitGate.ConfigureTraversal(
                    source.TraversalEntry,
                    source.transform,
                    source.Passage);
            }

            var exits = new List<UtilityJourneyFacilityPrototype>();
            for (int index = 0; index < stationA.ExternalSpawns.Length; index++)
            {
                UtilityJourneyFacilityPrototype exit = CreateFacility(
                    routeRoot,
                    "exit-" + (index + 1),
                    UtilityJourneyFacilityKind.StreetExit,
                    UtilityJourneyArea.DestinationConcourse,
                    UtilityJourneyDirection.Any,
                    stationA.ExternalSpawns[index].position,
                    0.42f,
                    0.2f,
                    index == 1 ? 0.12f : 0.04f,
                    0f,
                    facilities,
                    UtilityJourneyWaitingStyle.None,
                    index < 2 ? "left" : "right");
                if (index < stationA.ExternalStairVisuals.Length)
                {
                    exit.ConfigureTraversal(null, stationA.ExternalStairVisuals[index]);
                }
                exits.Add(exit);
            }

            return new OverviewJourneyBuildData
            {
                Facilities = facilities.Where(item => item != null).Distinct().ToArray(),
                NavigationBindings = new[]
                {
                    new UtilityJourneyNavigationBinding(
                        UtilityJourneyArea.OriginConcourse,
                        stationB.ConcourseNavigation),
                    new UtilityJourneyNavigationBinding(
                        UtilityJourneyArea.OriginPlatform,
                        stationB.UpperPlatformNavigation),
                    new UtilityJourneyNavigationBinding(
                        UtilityJourneyArea.TrainInterior,
                        upboundTrain.InteriorNavigation),
                    new UtilityJourneyNavigationBinding(
                        UtilityJourneyArea.DestinationPlatform,
                        stationA.UpperPlatformNavigation),
                    new UtilityJourneyNavigationBinding(
                        UtilityJourneyArea.DestinationConcourse,
                        stationA.ConcourseNavigation)
                },
                OriginEntranceOutsidePoints = stationB.ExternalStairVisuals,
                OriginEntranceSpawns = stationB.ExternalSpawns,
                DestinationExits = exits.ToArray()
            };
        }

        private static void RequireOverviewTransitionPoints(
            string label,
            params Transform[] points)
        {
            if (points.Any(item => item == null))
            {
                throw new InvalidOperationException(
                    "Overview transition points missing for " + label + ".");
            }
        }

        private static void CreateOverviewPassengerGroup(
            Transform parent,
            BidirectionalTrainLoopPrototype controller,
            OverviewTrainBuildData train,
            OverviewJourneyBuildData journey,
            UtilityJourneyDirection direction,
            string label,
            int count,
            int seedBase,
            UtilityPassengerTuningProfile[] profiles)
        {
            GameObject groupObject = new GameObject(label + " Utility Passengers");
            groupObject.transform.SetParent(parent);
            Color routeColor = direction == UtilityJourneyDirection.Upbound
                ? new Color32(67, 186, 255, 255)
                : new Color32(52, 215, 157, 255);
            for (int index = 0; index < count; index++)
            {
                string passengerName = label + " Passenger " + (index + 1);
                float tint = count <= 1 ? 0f : index / (float)(count - 1);
                UtilityPassengerBrainPrototype passenger = CreatePassenger(
                    groupObject.transform,
                    null,
                    passengerName,
                    Color.Lerp(routeColor, Color.white, tint * 0.22f));
                GameObject worldObject = new GameObject(passengerName + " World");
                worldObject.transform.SetParent(groupObject.transform);
                OverviewUtilityJourneyWorldPrototype world =
                    worldObject.AddComponent<OverviewUtilityJourneyWorldPrototype>();
                world.Configure(
                    passenger,
                    controller,
                    train.Root,
                    journey.OriginEntranceOutsidePoints,
                    journey.OriginEntranceSpawns,
                    journey.DestinationExits,
                    direction,
                    passengerName,
                    index * 2.4f +
                    (direction == UtilityJourneyDirection.Upbound ? 1.2f : 0f),
                    true);
                passenger.Configure(
                    world,
                    journey.Facilities,
                    journey.NavigationBindings,
                    null,
                    seedBase + index,
                    profiles);
            }
        }

        private static void ConfigureOverviewStairTransitions(
            UtilityJourneyFacilityPrototype[] stairs,
            Transform destinationMap,
            string destinationStairName,
            UtilityJourneyArea destinationArea)
        {
            foreach (UtilityJourneyFacilityPrototype stair in stairs)
            {
                string side = string.IsNullOrEmpty(stair.RouteGroup)
                    ? "left"
                    : stair.RouteGroup;
                Transform spawn = FindNamedTransform(
                    destinationMap,
                    destinationStairName + " " + side + " Deep");
                Transform entry = FindNamedTransform(
                    destinationMap,
                    destinationStairName + " " + side + " Entry");
                Transform release = FindNamedTransform(
                    destinationMap,
                    destinationStairName + " " + side + " Approach");
                if (spawn == null || entry == null || release == null)
                {
                    throw new InvalidOperationException(
                        "Overview stair pairing failed for " + stair.FacilityId + ".");
                }

                stair.ConfigureTransition(destinationArea, spawn, entry, release);
            }
        }

        private static Transform FindNamedTransform(Transform root, string name)
        {
            return root
                .GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == name);
        }

        private static OverviewTrainBuildData CreateOverviewTrain(
            Transform parent,
            Scene targetScene,
            string name,
            Vector3 startPosition,
            bool useUpperDoors,
            UtilityJourneyDirection direction)
        {
            string routePrefix = direction == UtilityJourneyDirection.Upbound
                ? "upbound"
                : "downbound";
            GameObject motionRoot = new GameObject(name);
            motionRoot.transform.SetParent(parent);
            motionRoot.transform.position = startPosition;

            GameObject carClone = ClonePrototypeTrainCar(targetScene, motionRoot.transform);
            RemovePrototypeActors(carClone);
            RemovePrototypePlatformObjects(carClone);
            var rideAnchors = new List<OverviewRideAnchorData>();
            int seatNumber = 0;
            foreach (PassengerSeatPrototype seat in
                     carClone.GetComponentsInChildren<PassengerSeatPrototype>(true))
            {
                for (int index = 0; index < seat.Capacity; index++)
                {
                    Transform point = seat.GetSittingPoint(index);
                    if (point != null)
                    {
                        rideAnchors.Add(new OverviewRideAnchorData
                        {
                            Anchor = point,
                            Id = routePrefix + "-train-seat-" + (++seatNumber),
                            Style = UtilityJourneyWaitingStyle.BenchSeat,
                            Comfort = 0.96f
                        });
                    }
                }
            }

            int activityNumber = 0;
            foreach (PassengerActivityPoint activity in
                     carClone.GetComponentsInChildren<PassengerActivityPoint>(true))
            {
                rideAnchors.Add(new OverviewRideAnchorData
                {
                    Anchor = activity.transform,
                    Id = routePrefix + "-train-activity-" + (++activityNumber) + "-" +
                         activity.ActivityType,
                    Style = activity.ActivityType == PassengerActivityType.Lean
                        ? UtilityJourneyWaitingStyle.WallRest
                        : UtilityJourneyWaitingStyle.DoorQueue,
                    Comfort = GetActivityComfort(activity.ActivityType)
                });
            }

            TrainDoorController[] allDoors = carClone
                .GetComponentsInChildren<TrainDoorController>(true);
            if (allDoors.Length == 0)
            {
                throw new InvalidOperationException(
                    "Train Car 1 requires TrainDoorController components.");
            }

            float platformSideY = useUpperDoors
                ? allDoors.Max(door => door.transform.position.y)
                : allDoors.Min(door => door.transform.position.y);
            TrainDoorController[] platformDoors = allDoors
                .Where(door => Mathf.Abs(
                    door.transform.position.y - platformSideY) < 0.25f)
                .OrderBy(door => door.transform.position.x)
                .ToArray();
            if (platformDoors.Length == 0)
            {
                throw new InvalidOperationException(
                    "Train Car 1 platform-side doors could not be resolved.");
            }

            StripPrototypeRuntimeLogicExceptDoors(carClone);

            Bounds initialBounds = CalculateRendererBounds(carClone);
            float scale = Mathf.Min(
                PlatformTrainTargetWidth / Mathf.Max(1f, initialBounds.size.x),
                PlatformTrainTargetHeight / Mathf.Max(1f, initialBounds.size.y));
            carClone.transform.localScale *= scale;

            Bounds scaledBounds = CalculateRendererBounds(carClone);
            carClone.transform.position += startPosition - scaledBounds.center;
            scaledBounds = CalculateRendererBounds(carClone);
            foreach (Renderer renderer in carClone.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sortingOrder += 20;
            }

            GridNavigation2D interiorNavigation = CreateNavigation(
                motionRoot.transform,
                (direction == UtilityJourneyDirection.Upbound
                    ? "Upbound"
                    : "Downbound") + " Train Interior Navigation",
                scaledBounds.center,
                Mathf.Max(12, Mathf.CeilToInt(scaledBounds.size.x / 0.25f)),
                Mathf.Max(8, Mathf.CeilToInt((scaledBounds.size.y - 0.35f) / 0.25f)),
                0.25f);

            var rideFacilities = new List<UtilityJourneyFacilityPrototype>();
            foreach (OverviewRideAnchorData anchor in rideAnchors
                         .Where(item => item.Anchor != null)
                         .GroupBy(item => item.Anchor)
                         .Select(group => group.First()))
            {
                UtilityJourneyFacilityPrototype facility =
                    anchor.Anchor.gameObject.AddComponent<UtilityJourneyFacilityPrototype>();
                facility.Configure(
                    anchor.Id,
                    UtilityJourneyFacilityKind.TrainRideSpot,
                    UtilityJourneyArea.TrainInterior,
                    direction,
                    0.22f,
                    0.2f,
                    0f,
                    0f);
                facility.ConfigureBehavior(anchor.Style, routePrefix + "-car-1");
                float nearestDoorDistance = platformDoors
                    .Min(door => Mathf.Abs(
                        door.transform.position.x - anchor.Anchor.position.x));
                float transferConvenience = 1f - Mathf.Clamp01(
                    nearestDoorDistance / 9f);
                if (anchor.Style == UtilityJourneyWaitingStyle.DoorQueue)
                {
                    transferConvenience = Mathf.Clamp01(
                        transferConvenience + 0.16f);
                }
                facility.ConfigureDecisionTraits(
                    anchor.Comfort,
                    transferConvenience);
                rideFacilities.Add(facility);
            }

            var insideDoorPoints = new Transform[platformDoors.Length];
            var alightFacilities = new UtilityJourneyFacilityPrototype[platformDoors.Length];
            for (int index = 0; index < platformDoors.Length; index++)
            {
                Transform inside = CreatePoint(
                    motionRoot.transform,
                    (direction == UtilityJourneyDirection.Upbound
                        ? "Upbound"
                        : "Downbound") + " Door " + (index + 1) + " Inside Point",
                    new Vector2(
                        platformDoors[index].transform.position.x,
                        Mathf.Lerp(
                            platformDoors[index].transform.position.y,
                            scaledBounds.center.y,
                            0.72f)));
                UtilityJourneyFacilityPrototype alight =
                    inside.gameObject.AddComponent<UtilityJourneyFacilityPrototype>();
                alight.Configure(
                    routePrefix + "-alight-door-" + (index + 1),
                    UtilityJourneyFacilityKind.TrainDoor,
                    UtilityJourneyArea.TrainInterior,
                    direction,
                    0.3f,
                    0.15f,
                    index * 0.025f,
                    0f);
                alight.ConfigureBehavior(
                    UtilityJourneyWaitingStyle.None,
                    routePrefix + "-door-" + (index + 1));
                insideDoorPoints[index] = inside;
                alightFacilities[index] = alight;
            }

            return new OverviewTrainBuildData
            {
                Root = motionRoot.transform,
                PlatformDoors = platformDoors,
                InteriorNavigation = interiorNavigation,
                InsideDoorPoints = insideDoorPoints,
                RideFacilities = rideFacilities.ToArray(),
                AlightFacilities = alightFacilities
            };
        }

        private static Camera CreateOverviewCamera(Transform parent)
        {
            GameObject cameraObject = new GameObject("Main Camera - Whole A-B Line");
            cameraObject.transform.SetParent(parent);
            cameraObject.transform.position = new Vector3(0f, 7f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 24.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            return camera;
        }

        private static Light CreateOverviewLight(Transform parent)
        {
            GameObject lightObject = new GameObject("Main Directional Light");
            lightObject.transform.SetParent(parent);
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.65f;
            return light;
        }

        private static Text CreateOverviewStatusUi(Transform parent)
        {
            GameObject canvasObject = new GameObject("Train Loop Status UI");
            canvasObject.transform.SetParent(parent);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
            return CreateUiText(
                canvasObject.transform,
                "Train Service Status",
                new Vector2(14f, -12f),
                new Vector2(900f, 150f),
                13,
                TextAnchor.UpperLeft);
        }

        private static ConcourseBuildData CreateConcourse(
            Transform parent,
            UtilityJourneyArea area,
            string title,
            bool origin,
            List<UtilityJourneyFacilityPrototype> facilities,
            List<UtilityJourneyNavigationBinding> navigations)
        {
            GameObject map = new GameObject(
                origin
                    ? "MAP 1A - Origin Fare Concourse"
                    : "MAP 1B - Destination Fare Concourse");
            map.transform.SetParent(parent);
            CreateBlock(map.transform, "Floor", Vector2.zero,
                new Vector2(26f, 12f), Floor, false, -20);
            CreateConcourseBoundary(map.transform);

            GridNavigation2D navigation = CreateNavigation(
                map.transform,
                "Concourse Navigation",
                Vector2.zero,
                104,
                48,
                0.25f);
            navigations.Add(new UtilityJourneyNavigationBinding(area, navigation));

            Vector2[] stairVisualPositions =
            {
                new Vector2(-13.5f, 6.85f),
                new Vector2(-13.5f, -6.85f),
                new Vector2(13.5f, -6.85f),
                new Vector2(13.5f, 6.85f)
            };
            Vector2[] insidePositions =
            {
                new Vector2(-11.7f, 5.3f),
                new Vector2(-11.7f, -5.3f),
                new Vector2(11.7f, -5.3f),
                new Vector2(11.7f, 5.3f)
            };
            Transform[] externalSpawns = new Transform[stairVisualPositions.Length];
            var streetExits =
                new UtilityJourneyFacilityPrototype[stairVisualPositions.Length];
            for (int index = 0; index < stairVisualPositions.Length; index++)
            {
                CreateStairVisual(
                    map.transform,
                    "Exit " + (index + 1) + " Street Stair",
                    stairVisualPositions[index],
                    new Vector2(5.2f, 1.75f));
                CreateLabel(
                    map.transform,
                    "EXIT " + (index + 1) + " STAIRS",
                    stairVisualPositions[index],
                    0.085f);
                externalSpawns[index] = CreatePoint(
                    map.transform,
                    "External Stair Spawn " + (index + 1),
                    insidePositions[index]);
                if (!origin)
                {
                    streetExits[index] = CreateFacility(
                        map.transform,
                        "exit-" + (index + 1),
                        UtilityJourneyFacilityKind.StreetExit,
                        area,
                        UtilityJourneyDirection.Any,
                        insidePositions[index],
                        0.42f,
                        0.2f,
                        index == 1 ? 0.12f : 0.04f,
                        0f,
                        facilities,
                        UtilityJourneyWaitingStyle.None,
                        index < 2 ? "left" : "right");
                }
            }

            GateLaneBuildData[] gateLanes = CreateGateBanksVisual(map.transform);
            for (int index = 0; index < gateLanes.Length; index++)
            {
                GateLaneBuildData lane = gateLanes[index];
                Vector2 approach = origin
                    ? lane.OutsideApproach.position
                    : lane.InsideApproach.position;
                Transform traversalDestination = origin
                    ? lane.InsideApproach
                    : lane.OutsideApproach;
                UtilityJourneyFacilityPrototype gateFacility = CreateFacility(
                    map.transform,
                    (origin ? "entry-gate-" : "exit-gate-") +
                    lane.RouteGroup + "-" + (index % 5 + 1),
                    origin
                        ? UtilityJourneyFacilityKind.EntryGate
                        : UtilityJourneyFacilityKind.ExitGate,
                    area,
                    UtilityJourneyDirection.Any,
                    approach,
                    0.35f,
                    0.7f,
                    (index % 5) * 0.035f,
                    (index % 5) * 0.18f,
                    facilities,
                    UtilityJourneyWaitingStyle.None,
                    lane.RouteGroup);
                gateFacility.ConfigureTraversal(
                    lane.Center,
                    traversalDestination,
                    lane.Passage);
            }

            StairLaneBuildData[] upperLanes = CreateInternalStairPair(
                map.transform,
                "Upper Platform Stairs",
                6.75f,
                true);
            StairLaneBuildData[] lowerLanes = CreateInternalStairPair(
                map.transform,
                "Lower Platform Stairs",
                -6.75f,
                false);

            var upStairs = new List<UtilityJourneyFacilityPrototype>();
            var downStairs = new List<UtilityJourneyFacilityPrototype>();
            if (origin)
            {
                for (int index = 0; index < upperLanes.Length; index++)
                {
                    upStairs.Add(CreateStairFacility(
                        map.transform,
                        "upbound-platform-stair-" + upperLanes[index].RouteGroup,
                        area,
                        UtilityJourneyDirection.Upbound,
                        upperLanes[index],
                        facilities));
                    downStairs.Add(CreateStairFacility(
                        map.transform,
                        "downbound-platform-stair-" + lowerLanes[index].RouteGroup,
                        area,
                        UtilityJourneyDirection.Downbound,
                        lowerLanes[index],
                        facilities));
                }
            }

            return new ConcourseBuildData
            {
                Root = map,
                ExternalSpawns = externalSpawns,
                StreetExits = streetExits.Where(item => item != null).ToArray(),
                UpboundStairs = upStairs.ToArray(),
                DownboundStairs = downStairs.ToArray(),
                LowerArrivals = lowerLanes.Select(lane =>
                    new StairArrivalBuildData(
                        lane.Deep,
                        lane.Entry,
                        lane.Approach)).ToArray()
            };
        }

        private static PlatformBuildData CreatePlatform(
            Transform parent,
            Scene targetScene,
            UtilityJourneyArea area,
            string title,
            bool origin,
            List<UtilityJourneyFacilityPrototype> facilities,
            List<UtilityJourneyNavigationBinding> navigations)
        {
            GameObject map = new GameObject(
                origin
                    ? "MAP 2A - Origin Bidirectional Platform"
                    : "MAP 2B - Destination Bidirectional Platform");
            map.transform.SetParent(parent);
            CreateBlock(map.transform, "Void", Vector2.zero,
                new Vector2(36f, 18.4f), Background, false, -30);
            CreateBlock(map.transform, "Upper Platform", new Vector2(0f, 6.9f),
                new Vector2(35.6f, 4.35f), Platform, false, -20);
            CreateBlock(map.transform, "Upper Track", new Vector2(0f, 2.3f),
                new Vector2(35.6f, 4.35f), Track, true, -18);
            CreateBlock(map.transform, "Lower Track", new Vector2(0f, -2.3f),
                new Vector2(35.6f, 4.35f), Track, true, -18);
            CreateBlock(map.transform, "Lower Platform", new Vector2(0f, -6.9f),
                new Vector2(35.6f, 4.35f), Platform, false, -20);
            CreateBlock(map.transform, "Upper Platform Edge", new Vector2(0f, 4.62f),
                new Vector2(35.6f, 0.13f), Stair, false, -16);
            CreateBlock(map.transform, "Track Divider", Vector2.zero,
                new Vector2(35.6f, 0.12f), Rail, false, -16);
            CreateBlock(map.transform, "Lower Platform Edge", new Vector2(0f, -4.62f),
                new Vector2(35.6f, 0.13f), Stair, false, -16);
            CreateBoundary(map.transform, new Vector2(36f, 18.4f));
            CreateLabel(map.transform, "UPBOUND PLATFORM", new Vector2(-12.5f, 7.05f), 0.105f);
            CreateLabel(map.transform, "UPBOUND TRACK", new Vector2(-12.7f, 2.3f), 0.095f);
            CreateLabel(map.transform, "DOWNBOUND TRACK", new Vector2(-12.4f, -2.3f), 0.095f);
            CreateLabel(map.transform, "DOWNBOUND PLATFORM", new Vector2(-12.1f, -7.05f), 0.105f);

            StairLaneBuildData[] upperStairLanes = CreatePlatformStairLanes(
                map.transform,
                "Upper Center Stairs",
                7.75f,
                true);
            StairLaneBuildData[] lowerStairLanes = CreatePlatformStairLanes(
                map.transform,
                "Lower Center Stairs",
                -7.75f,
                false);

            GridNavigation2D upperNavigation = CreateNavigation(
                map.transform,
                "Upper Platform Navigation",
                new Vector2(0f, 6.9f),
                140,
                17,
                0.25f);
            GridNavigation2D lowerNavigation = CreateNavigation(
                map.transform,
                "Lower Platform Navigation",
                new Vector2(0f, -6.9f),
                140,
                17,
                0.25f);
            navigations.Add(new UtilityJourneyNavigationBinding(area, upperNavigation));
            navigations.Add(new UtilityJourneyNavigationBinding(area, lowerNavigation));

            StairArrivalBuildData[] upboundArrivals = upperStairLanes
                .Select(lane => new StairArrivalBuildData(
                    lane.Deep,
                    lane.Entry,
                    lane.Approach))
                .ToArray();
            StairArrivalBuildData[] downboundArrivals = lowerStairLanes
                .Select(lane => new StairArrivalBuildData(
                    lane.Deep,
                    lane.Entry,
                    lane.Approach))
                .ToArray();

            TrainVisualBuildData platformTrain = CreatePlatformTrainVisual(
                map.transform,
                origin,
                targetScene);
            CreatePlatformScreenDoors(
                map.transform,
                DownboundPlatformEdgeY,
                platformTrain.DoorPositions);

            var boardingDoors = new List<UtilityJourneyFacilityPrototype>();
            var upStairs = new List<UtilityJourneyFacilityPrototype>();
            float[] benchX = { -15f, -8.5f, 8.5f, 15f };
            for (int index = 0; index < benchX.Length; index++)
            {
                CreateBenchVisual(map.transform, new Vector2(benchX[index], -8.05f));
            }

            if (origin)
            {
                Vector2[] trainDoorPositions = platformTrain.DoorPositions;
                for (int index = 0; index < trainDoorPositions.Length; index++)
                {
                    float doorX = trainDoorPositions[index].x;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        CreateWaitingFacility(
                            map.transform,
                            "door-queue-" + (index + 1) + "-" +
                            (side < 0 ? "left" : "right"),
                            area,
                            new Vector2(doorX + side * 0.9f, -5.65f),
                            UtilityJourneyWaitingStyle.DoorQueue,
                            index * 0.03f,
                            facilities);
                    }

                    UtilityJourneyFacilityPrototype door = CreateFacility(
                        map.transform,
                        "downbound-train-door-" + (index + 1),
                        UtilityJourneyFacilityKind.TrainDoor,
                        area,
                        UtilityJourneyDirection.Downbound,
                        new Vector2(doorX, -5.18f),
                        0.42f,
                        0.25f,
                        index == 1 ? 0.06f : 0.14f,
                        0f,
                        facilities);
                    Transform doorCenter = CreatePoint(
                        map.transform,
                        "Train Door Passage " + (index + 1),
                        new Vector2(doorX, DownboundPlatformEdgeY));
                    Transform trainInside = CreatePoint(
                        map.transform,
                        "Train Door Inside " + (index + 1),
                        new Vector2(doorX, -3.45f));
                    door.ConfigureTraversal(
                        doorCenter,
                        trainInside,
                        platformTrain.Doors[index]);
                    boardingDoors.Add(door);
                }

                for (int index = 0; index < benchX.Length; index++)
                {
                    CreateWaitingFacility(
                        map.transform,
                        "bench-seat-" + (index + 1),
                        area,
                        new Vector2(benchX[index], -7.78f),
                        UtilityJourneyWaitingStyle.BenchSeat,
                        index * 0.025f,
                        facilities);
                }

                float[] wallX = { -13f, -10f, 10f, 13f };
                for (int index = 0; index < wallX.Length; index++)
                {
                    CreateWaitingFacility(
                        map.transform,
                        "wall-rest-" + (index + 1),
                        area,
                        new Vector2(wallX[index], -8.45f),
                        UtilityJourneyWaitingStyle.WallRest,
                        index * 0.02f,
                        facilities);
                }
            }
            else
            {
                foreach (StairLaneBuildData lane in lowerStairLanes)
                {
                    upStairs.Add(CreateStairFacility(
                        map.transform,
                        lane.RouteGroup + "-platform-up-stair",
                        area,
                        UtilityJourneyDirection.Any,
                        lane,
                        facilities,
                        UtilityJourneyFacilityKind.PlatformUpStair));
                }
            }

            Vector2 alightDoorPosition = platformTrain.DoorPositions
                .OrderBy(position => Mathf.Abs(position.x))
                .First();
            Transform alightSpawn = CreatePoint(
                map.transform,
                "Destination Alight Spawn",
                new Vector2(alightDoorPosition.x, -3.45f));
            Transform alightEntry = CreatePoint(
                map.transform,
                "Destination Alight Door",
                new Vector2(alightDoorPosition.x, DownboundPlatformEdgeY));
            Transform alightRelease = CreatePoint(
                map.transform,
                "Destination Alight Platform Release",
                new Vector2(alightDoorPosition.x, -5.55f));
            return new PlatformBuildData
            {
                Root = map,
                UpboundArrivals = upboundArrivals,
                DownboundArrivals = downboundArrivals,
                AlightArrival = new StairArrivalBuildData(
                    alightSpawn,
                    alightEntry,
                    alightRelease),
                BoardingDoors = boardingDoors.ToArray(),
                UpStairs = upStairs.ToArray(),
                TrainVisual = platformTrain.Root,
                TrainDoors = platformTrain.Doors
            };
        }

        private static TrainBuildData CreateTrainInterior(
            Transform parent,
            Scene targetScene,
            List<UtilityJourneyFacilityPrototype> facilities,
            List<UtilityJourneyNavigationBinding> navigations)
        {
            GameObject screen = new GameObject("MAP 3 - Prototype_AI Train Interior");
            screen.transform.SetParent(parent);
            CreateBlock(screen.transform, "Train Screen Background", Vector2.zero,
                new Vector2(25f, 15f), Background, false, -30);
            CreateLabel(screen.transform,
                "PROTOTYPE_AI / TRAIN CAR 1 / 60 SECOND RIDE",
                new Vector2(0f, 6.35f),
                0.16f);

            GameObject visualRoot = new GameObject("Train Visual - Shakes Only");
            visualRoot.transform.SetParent(screen.transform);
            GameObject carClone = ClonePrototypeTrainCar(targetScene, visualRoot.transform);
            RemovePrototypeActors(carClone);
            RemovePrototypePlatformObjects(carClone);
            Bounds bounds = CalculateRendererBounds(carClone);
            Vector3 center = bounds.center;
            carClone.transform.position -= new Vector3(center.x, center.y, 0f);
            bounds = CalculateRendererBounds(carClone);
            float scale = Mathf.Min(
                1f,
                22f / Mathf.Max(1f, bounds.size.x),
                11f / Mathf.Max(1f, bounds.size.y));
            visualRoot.transform.localScale = Vector3.one * scale;

            PassengerActivityPoint[] activityPoints =
                carClone.GetComponentsInChildren<PassengerActivityPoint>(true);
            PassengerDoorway[] doorways = carClone
                .GetComponentsInChildren<PassengerDoorway>(true);
            PassengerDoorway interiorDoor = doorways
                .Where(doorway => doorway != null && doorway.InsidePoint != null)
                .OrderBy(doorway => doorway.transform.position.y)
                .ThenBy(doorway => Mathf.Abs(doorway.transform.position.x))
                .FirstOrDefault();
            Vector2 doorPosition = interiorDoor != null
                ? interiorDoor.InsidePoint.position
                : new Vector2(-8f, -2.5f);
            var usablePositions = activityPoints
                .Where(point => point != null)
                .Select(point => (Vector2)point.transform.position)
                .OrderBy(position => position.sqrMagnitude)
                .Take(4)
                .ToList();
            if (usablePositions.Count == 0)
            {
                usablePositions.Add(Vector2.zero);
                usablePositions.Add(new Vector2(-2f, 0f));
                usablePositions.Add(new Vector2(2f, 0f));
            }

            StripPrototypeRuntimeLogic(carClone);
            Bounds scaledBounds = CalculateRendererBounds(carClone);
            int columns = Mathf.Max(24, Mathf.CeilToInt((scaledBounds.size.x + 2f) / 0.25f));
            int rows = Mathf.Max(16, Mathf.CeilToInt((scaledBounds.size.y + 2f) / 0.25f));
            GridNavigation2D navigation = CreateNavigation(
                screen.transform,
                "Train Interior Navigation",
                Vector2.zero,
                columns,
                rows,
                0.25f);
            navigations.Add(new UtilityJourneyNavigationBinding(
                UtilityJourneyArea.TrainInterior,
                navigation));

            Transform boardingSpawn = CreatePoint(
                screen.transform,
                "Train Boarding Spawn",
                doorPosition);
            UtilityJourneyFacilityPrototype alightDoor = CreateFacility(
                screen.transform,
                "train-interior-alight-door",
                UtilityJourneyFacilityKind.TrainDoor,
                UtilityJourneyArea.TrainInterior,
                UtilityJourneyDirection.Downbound,
                doorPosition,
                0.42f,
                0.25f,
                0f,
                0f,
                facilities);
            for (int index = 0; index < usablePositions.Count; index++)
            {
                CreateFacility(
                    screen.transform,
                    "train-ride-spot-" + (index + 1),
                    UtilityJourneyFacilityKind.TrainRideSpot,
                    UtilityJourneyArea.TrainInterior,
                    UtilityJourneyDirection.Any,
                    usablePositions[index],
                    0.28f,
                    0.2f,
                    index * 0.08f,
                    0f,
                    facilities);
            }

            return new TrainBuildData
            {
                Root = screen,
                TrainVisual = visualRoot.transform,
                BoardingSpawn = boardingSpawn,
                AlightDoor = alightDoor
            };
        }

        private static GameObject ClonePrototypeTrainCar(
            Scene targetScene,
            Transform parent)
        {
            if (!System.IO.File.Exists(
                    System.IO.Path.GetFullPath(
                        "Assets/_Project/Scenes/Prototype_AI.unity")))
            {
                throw new InvalidOperationException(
                    "Prototype_AI.unity is required to clone Train Car 1.");
            }

            Scene sourceScene = EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/Prototype_AI.unity",
                OpenSceneMode.Additive);
            try
            {
                GameObject sourceCar = sourceScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Select(item => item.gameObject)
                    .FirstOrDefault(item => item.name == "Train Car 1");
                if (sourceCar == null)
                {
                    throw new InvalidOperationException(
                        "Train Car 1 was not found in Prototype_AI.unity.");
                }

                SceneManager.SetActiveScene(targetScene);
                GameObject clone = UnityEngine.Object.Instantiate(sourceCar);
                clone.name = "Train Car 1 - Cloned From Prototype_AI";
                SceneManager.MoveGameObjectToScene(clone, targetScene);
                clone.transform.SetParent(parent, true);
                return clone;
            }
            finally
            {
                EditorSceneManager.CloseScene(sourceScene, true);
                SceneManager.SetActiveScene(targetScene);
            }
        }

        private static void StripPrototypeRuntimeLogic(GameObject clone)
        {
            MonoBehaviour[] behaviours = clone.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null || behaviour is NavigationObstacle)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(behaviour);
            }
        }

        private static void StripPrototypeRuntimeLogicExceptDoors(GameObject clone)
        {
            MonoBehaviour[] behaviours = clone.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null || behaviour is TrainDoorController)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(behaviour);
            }
        }

        private static void RemovePrototypeActors(GameObject clone)
        {
            var actorObjects = new HashSet<GameObject>();
            foreach (GeneralPassengerPrototype actor in
                     clone.GetComponentsInChildren<GeneralPassengerPrototype>(true))
            {
                if (actor != null && actor.gameObject != clone)
                {
                    actorObjects.Add(actor.gameObject);
                }
            }

            foreach (PlayerBoardingCyclePrototype actor in
                     clone.GetComponentsInChildren<PlayerBoardingCyclePrototype>(true))
            {
                if (actor != null && actor.gameObject != clone)
                {
                    actorObjects.Add(actor.gameObject);
                }
            }

            foreach (PassengerPrototypeAgent actor in
                     clone.GetComponentsInChildren<PassengerPrototypeAgent>(true))
            {
                if (actor != null && actor.gameObject != clone)
                {
                    actorObjects.Add(actor.gameObject);
                }
            }

            foreach (GameObject actorObject in actorObjects)
            {
                UnityEngine.Object.DestroyImmediate(actorObject);
            }
        }

        private static void RemovePrototypePlatformObjects(GameObject clone)
        {
            GameObject[] platformObjects = clone
                .GetComponentsInChildren<Transform>(true)
                .Where(item =>
                    item != null &&
                    item.gameObject != clone &&
                    item.name.StartsWith(
                        "Platform ",
                        StringComparison.OrdinalIgnoreCase))
                .Select(item => item.gameObject)
                .Distinct()
                .ToArray();
            foreach (GameObject platformObject in platformObjects)
            {
                UnityEngine.Object.DestroyImmediate(platformObject);
            }
        }

        private static Bounds CalculateRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(root.transform.position, new Vector3(20f, 8f, 1f));
            }

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        private static TrainVisualBuildData CreatePlatformTrainVisual(
            Transform parent,
            bool origin,
            Scene targetScene)
        {
            GameObject root = new GameObject(
                origin ? "Arriving Train Visual" : "Destination Train Visual");
            root.transform.SetParent(parent);
            root.transform.localPosition = new Vector3(
                0f,
                DownboundTrackCenterY,
                0f);
            GameObject carClone = ClonePrototypeTrainCar(targetScene, root.transform);
            RemovePrototypeActors(carClone);
            RemovePrototypePlatformObjects(carClone);

            PassengerDoorway[] authoredDoorways = carClone
                .GetComponentsInChildren<PassengerDoorway>(true);
            if (authoredDoorways.Length == 0)
            {
                throw new InvalidOperationException(
                    "Train Car 1 requires authored PassengerDoorway components.");
            }

            float lowerDoorY = authoredDoorways.Min(
                doorway => doorway.transform.position.y);
            Transform[] lowerDoorTransforms = authoredDoorways
                .Where(doorway => Mathf.Abs(
                    doorway.transform.position.y - lowerDoorY) < 0.25f)
                .OrderBy(doorway => doorway.transform.position.x)
                .Select(doorway => doorway.transform)
                .ToArray();
            if (lowerDoorTransforms.Length == 0)
            {
                throw new InvalidOperationException(
                    "Train Car 1 requires lower-side boarding doors.");
            }

            Bounds bounds = CalculateRendererBounds(carClone);
            float scale = Mathf.Min(
                PlatformTrainTargetWidth / Mathf.Max(1f, bounds.size.x),
                PlatformTrainTargetHeight / Mathf.Max(1f, bounds.size.y));
            carClone.transform.localScale *= scale;

            Vector3 averageDoorPosition = Vector3.zero;
            foreach (Transform doorTransform in lowerDoorTransforms)
            {
                averageDoorPosition += doorTransform.position;
            }
            averageDoorPosition /= lowerDoorTransforms.Length;
            Vector3 targetDoorCenter = parent.TransformPoint(
                new Vector3(0f, DownboundPlatformEdgeY, 0f));
            carClone.transform.position += new Vector3(
                targetDoorCenter.x - averageDoorPosition.x,
                targetDoorCenter.y - averageDoorPosition.y,
                0f);

            StripPrototypeRuntimeLogic(carClone);

            var doors = new UtilityJourneyPassagePrototype[lowerDoorTransforms.Length];
            var doorPositions = new Vector2[lowerDoorTransforms.Length];
            for (int index = 0; index < lowerDoorTransforms.Length; index++)
            {
                Vector3 doorLocalPosition = root.transform.InverseTransformPoint(
                    lowerDoorTransforms[index].position);
                doors[index] = CreateSlidingPassage(
                    root.transform,
                    "Train Door " + (index + 1),
                    doorLocalPosition,
                    new Vector2(1.5f, 0.2f),
                    Stair,
                    new Vector3(-0.62f, 0f, 0f),
                    new Vector3(0.62f, 0f, 0f));
                doorPositions[index] = parent.InverseTransformPoint(
                    lowerDoorTransforms[index].position);
            }

            return new TrainVisualBuildData
            {
                Root = root.transform,
                Doors = doors,
                DoorPositions = doorPositions
            };
        }

        private static UtilityPassengerBrainPrototype CreatePassenger(
            Transform parent,
            Text thoughtText)
        {
            return CreatePassenger(
                parent,
                thoughtText,
                "Utility Passenger 1",
                Passenger);
        }

        private static UtilityPassengerBrainPrototype CreatePassenger(
            Transform parent,
            Text thoughtText,
            string passengerName,
            Color passengerColor)
        {
            GameObject passenger = CreateBlock(
                parent,
                passengerName,
                Vector2.zero,
                new Vector2(0.58f, 0.78f),
                passengerColor,
                false,
                50);
            CapsuleCollider2D collider = passenger.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = Vector2.one;
            Rigidbody2D body = passenger.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            UtilityPassengerBrainPrototype brain =
                passenger.AddComponent<UtilityPassengerBrainPrototype>();
            CreateBlock(passenger.transform, "Facing", new Vector2(0f, 0.42f),
                new Vector2(0.12f, 0.28f), Color.white, false, 51);
            return brain;
        }

        private static UtilityJourneyFacilityPrototype CreateFacility(
            Transform parent,
            string id,
            UtilityJourneyFacilityKind kind,
            UtilityJourneyArea area,
            UtilityJourneyDirection direction,
            Vector2 position,
            float acceptanceRadius,
            float interactionSeconds,
            float crowdCost,
            float queueSeconds,
            List<UtilityJourneyFacilityPrototype> facilities,
            UtilityJourneyWaitingStyle waitingStyle = UtilityJourneyWaitingStyle.None,
            string routeGroup = "")
        {
            GameObject marker = new GameObject("FACILITY - " + id);
            marker.transform.SetParent(parent);
            marker.transform.position = position;
            UtilityJourneyFacilityPrototype facility =
                marker.AddComponent<UtilityJourneyFacilityPrototype>();
            facility.Configure(
                id,
                kind,
                area,
                direction,
                acceptanceRadius,
                interactionSeconds,
                crowdCost,
                queueSeconds);
            facility.ConfigureBehavior(waitingStyle, routeGroup);
            facilities.Add(facility);
            return facility;
        }

        private static GateLaneBuildData[] CreateGateBanksVisual(Transform parent)
        {
            float[] bankX = { -4.45f, 4.45f };
            float[] housingY = { -5f, -3f, -1f, 1f, 3f, 5f };
            float[] laneY = { -4f, -2f, 0f, 2f, 4f };
            var lanes = new List<GateLaneBuildData>(bankX.Length * laneY.Length);
            foreach (float x in bankX)
            {
                foreach (float y in housingY)
                {
                    CreateBlock(parent, "Fare Gate Housing", new Vector2(x, y),
                        new Vector2(1.45f, 0.58f), GateHousing, true, 1);
                }

                for (int index = 0; index < laneY.Length; index++)
                {
                    float y = laneY[index];
                    string side = x < 0f ? "left" : "right";
                    UtilityJourneyPassagePrototype passage =
                        CreateVerticalSlidingPassage(
                            parent,
                            "Fare Gate Door " + side + " " + (index + 1),
                            new Vector2(x, y),
                            new Vector2(0.18f, 1.32f),
                            Gate);
                    float outsideX = x < 0f ? x - 0.82f : x + 0.82f;
                    float insideX = x < 0f ? x + 0.82f : x - 0.82f;
                    lanes.Add(new GateLaneBuildData
                    {
                        RouteGroup = side,
                        Passage = passage,
                        Center = CreatePoint(
                            parent,
                            "Gate Center " + side + " " + (index + 1),
                            new Vector2(x, y)),
                        OutsideApproach = CreatePoint(
                            parent,
                            "Gate Outside " + side + " " + (index + 1),
                            new Vector2(outsideX, y)),
                        InsideApproach = CreatePoint(
                            parent,
                            "Gate Inside " + side + " " + (index + 1),
                            new Vector2(insideX, y))
                    });
                }
            }

            return lanes.ToArray();
        }

        private static StairLaneBuildData[] CreateInternalStairPair(
            Transform parent,
            string name,
            float y,
            bool upper)
        {
            const float halfWidth = 2.9f;
            const float halfHeight = 0.875f;
            var lanes = new StairLaneBuildData[2];
            for (int index = 0; index < lanes.Length; index++)
            {
                bool left = index == 0;
                float centerX = left ? -StairLaneCenterX : StairLaneCenterX;
                string side = left ? "left" : "right";
                CreateStairVisual(parent, name + " " + side,
                    new Vector2(centerX, y), new Vector2(5.8f, 1.75f));
                CreateBlock(parent, name + " " + side + " Side Wall A",
                    new Vector2(centerX, y - halfHeight),
                    new Vector2(5.8f, 0.14f), Wall, true, 5);
                CreateBlock(parent, name + " " + side + " Side Wall B",
                    new Vector2(centerX, y + halfHeight),
                    new Vector2(5.8f, 0.14f), Wall, true, 5);
                CreateBlock(parent, name + " " + side + " End Wall",
                    new Vector2(centerX + (left ? -halfWidth : halfWidth), y),
                    new Vector2(0.14f, 1.75f), Wall, true, 5);

                float approachY = upper ? 5.35f : -5.35f;
                float innerX = left ? -1.35f : 1.35f;
                lanes[index] = new StairLaneBuildData
                {
                    RouteGroup = side,
                    Approach = CreatePoint(parent, name + " " + side + " Approach",
                        new Vector2(innerX, approachY)),
                    Entry = CreatePoint(parent, name + " " + side + " Entry",
                        new Vector2(innerX, y)),
                    Deep = CreatePoint(parent, name + " " + side + " Deep",
                        new Vector2(left ? -6.25f : 6.25f, y))
                };
            }

            CreateLabel(parent, "PLATFORM STAIRS", new Vector2(0f, y), 0.08f);
            return lanes;
        }

        private static StairLaneBuildData[] CreatePlatformStairLanes(
            Transform parent,
            string name,
            float y,
            bool upper)
        {
            const float halfWidth = 2.9f;
            const float halfHeight = 0.825f;
            var lanes = new StairLaneBuildData[2];
            for (int index = 0; index < lanes.Length; index++)
            {
                bool left = index == 0;
                float centerX = left ? -StairLaneCenterX : StairLaneCenterX;
                string side = left ? "left" : "right";
                CreateStairVisual(parent, name + " " + side,
                    new Vector2(centerX, y), new Vector2(5.8f, 1.65f));
                CreateBlock(parent, name + " " + side + " Side Wall A",
                    new Vector2(centerX, y - halfHeight),
                    new Vector2(5.8f, 0.14f), Wall, true, 5);
                CreateBlock(parent, name + " " + side + " Side Wall B",
                    new Vector2(centerX, y + halfHeight),
                    new Vector2(5.8f, 0.14f), Wall, true, 5);
                CreateBlock(parent, name + " " + side + " Inner End Wall",
                    new Vector2(centerX + (left ? halfWidth : -halfWidth), y),
                    new Vector2(0.14f, 1.65f), Wall, true, 5);

                lanes[index] = new StairLaneBuildData
                {
                    RouteGroup = side,
                    Approach = CreatePoint(parent, name + " " + side + " Approach",
                        new Vector2(left ? -7.45f : 7.45f, y)),
                    Entry = CreatePoint(parent, name + " " + side + " Entry",
                        new Vector2(left ? -6.25f : 6.25f, y)),
                    Deep = CreatePoint(parent, name + " " + side + " Deep",
                        new Vector2(left ? -1.65f : 1.65f, y))
                };
            }

            CreateLabel(parent, "PLATFORM STAIRS", new Vector2(0f, y), 0.08f);
            return lanes;
        }

        private static UtilityJourneyFacilityPrototype CreateStairFacility(
            Transform parent,
            string id,
            UtilityJourneyArea area,
            UtilityJourneyDirection direction,
            StairLaneBuildData lane,
            List<UtilityJourneyFacilityPrototype> facilities,
            UtilityJourneyFacilityKind kind =
                UtilityJourneyFacilityKind.PlatformDownStair)
        {
            UtilityJourneyFacilityPrototype facility = CreateFacility(
                parent,
                id,
                kind,
                area,
                direction,
                lane.Approach.position,
                0.38f,
                0.22f,
                lane.RouteGroup == "left" ? 0.08f : 0.1f,
                0f,
                facilities,
                UtilityJourneyWaitingStyle.None,
                lane.RouteGroup);
            facility.ConfigureTraversal(lane.Entry, lane.Deep);
            return facility;
        }

        private static void ConfigureStairTransitions(
            UtilityJourneyFacilityPrototype[] stairs,
            UtilityJourneyArea destinationArea,
            StairArrivalBuildData[] arrivals)
        {
            if (stairs == null || arrivals == null)
            {
                return;
            }

            int count = Mathf.Min(stairs.Length, arrivals.Length);
            for (int index = 0; index < count; index++)
            {
                stairs[index].ConfigureTransition(
                    destinationArea,
                    arrivals[index].Spawn,
                    arrivals[index].Entry,
                    arrivals[index].Release);
            }
        }

        private static UtilityJourneyFacilityPrototype CreateWaitingFacility(
            Transform parent,
            string id,
            UtilityJourneyArea area,
            Vector2 position,
            UtilityJourneyWaitingStyle style,
            float crowdCost,
            List<UtilityJourneyFacilityPrototype> facilities,
            UtilityJourneyDirection direction = UtilityJourneyDirection.Downbound)
        {
            UtilityJourneyFacilityPrototype facility = CreateFacility(
                parent,
                id,
                UtilityJourneyFacilityKind.PlatformWaitingArea,
                area,
                direction,
                position,
                0.42f,
                0f,
                crowdCost,
                0f,
                facilities,
                style,
                string.Empty);
            switch (style)
            {
                case UtilityJourneyWaitingStyle.DoorQueue:
                    facility.ConfigureDecisionTraits(0.28f, 0.96f);
                    break;
                case UtilityJourneyWaitingStyle.BenchSeat:
                    facility.ConfigureDecisionTraits(0.95f, 0.36f);
                    break;
                case UtilityJourneyWaitingStyle.WallRest:
                    facility.ConfigureDecisionTraits(0.72f, 0.5f);
                    break;
                default:
                    facility.ConfigureDecisionTraits(0.5f, 0.5f);
                    break;
            }
            return facility;
        }

        private static float GetActivityComfort(PassengerActivityType activityType)
        {
            switch (activityType)
            {
                case PassengerActivityType.Lean:
                    return 0.74f;
                case PassengerActivityType.Handhold:
                    return 0.58f;
                case PassengerActivityType.AisleStanding:
                    return 0.48f;
                case PassengerActivityType.DoorStanding:
                    return 0.32f;
                default:
                    return 0.45f;
            }
        }

        private static void CreateBenchVisual(Transform parent, Vector2 position)
        {
            CreateBlock(parent, "Waiting Bench", position,
                new Vector2(2.8f, 0.48f), new Color32(137, 74, 42, 255), true, 3);
            CreateBlock(parent, "Waiting Bench Back",
                position + new Vector2(0f, 0.28f),
                new Vector2(2.8f, 0.16f), new Color32(168, 91, 51, 255), true, 4);
        }

        private static void CreatePlatformScreenDoors(
            Transform parent,
            float y,
            Vector2[] trainDoorPositions)
        {
            foreach (Vector2 trainDoorPosition in trainDoorPositions)
            {
                float x = trainDoorPosition.x;
                CreateBlock(parent, "Screen Door Left", new Vector2(x - 1.45f, y),
                    new Vector2(1.35f, 0.14f), Wall, false, -14);
                CreateBlock(parent, "Screen Door Right", new Vector2(x + 1.45f, y),
                    new Vector2(1.35f, 0.14f), Wall, false, -14);
                CreateBlock(parent, "Queue Mark Left",
                    new Vector2(x - 0.9f, y - 0.65f),
                    new Vector2(0.55f, 0.08f), Train, false, -14);
                CreateBlock(parent, "Queue Mark Right",
                    new Vector2(x + 0.9f, y - 0.65f),
                    new Vector2(0.55f, 0.08f), Train, false, -14);
            }
        }

        private static UtilityJourneyPassagePrototype CreateSlidingPassage(
            Transform parent,
            string name,
            Vector2 localCenter,
            Vector2 totalSize,
            Color color,
            Vector3 leftOffset,
            Vector3 rightOffset)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.localPosition = localCenter;
            GameObject left = CreateBlock(root.transform, name + " Left Panel",
                Vector2.zero, new Vector2(totalSize.x * 0.48f, totalSize.y),
                color, false, 6);
            GameObject right = CreateBlock(root.transform, name + " Right Panel",
                Vector2.zero, new Vector2(totalSize.x * 0.48f, totalSize.y),
                color, false, 6);
            AddPassageBlocker(left);
            AddPassageBlocker(right);
            left.transform.localPosition = new Vector3(-totalSize.x * 0.25f, 0f, 0f);
            right.transform.localPosition = new Vector3(totalSize.x * 0.25f, 0f, 0f);
            UtilityJourneyPassagePrototype passage =
                root.AddComponent<UtilityJourneyPassagePrototype>();
            passage.Configure(
                new[] { left.transform, right.transform },
                new[] { leftOffset, rightOffset });
            return passage;
        }

        private static UtilityJourneyPassagePrototype CreateVerticalSlidingPassage(
            Transform parent,
            string name,
            Vector2 localCenter,
            Vector2 totalSize,
            Color color)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.localPosition = localCenter;
            GameObject lower = CreateBlock(root.transform, name + " Lower Panel",
                Vector2.zero, new Vector2(totalSize.x, totalSize.y * 0.48f),
                color, false, 6);
            GameObject upper = CreateBlock(root.transform, name + " Upper Panel",
                Vector2.zero, new Vector2(totalSize.x, totalSize.y * 0.48f),
                color, false, 6);
            AddPassageBlocker(lower);
            AddPassageBlocker(upper);
            lower.transform.localPosition = new Vector3(0f, -totalSize.y * 0.25f, 0f);
            upper.transform.localPosition = new Vector3(0f, totalSize.y * 0.25f, 0f);
            UtilityJourneyPassagePrototype passage =
                root.AddComponent<UtilityJourneyPassagePrototype>();
            passage.Configure(
                new[] { lower.transform, upper.transform },
                new[]
                {
                    new Vector3(0f, -totalSize.y * 0.42f, 0f),
                    new Vector3(0f, totalSize.y * 0.42f, 0f)
                });
            return passage;
        }

        private static void AddPassageBlocker(GameObject panel)
        {
            if (panel.GetComponent<Collider2D>() == null)
            {
                panel.AddComponent<BoxCollider2D>();
            }

            if (panel.GetComponent<NavigationObstacle>() == null)
            {
                panel.AddComponent<NavigationObstacle>();
            }
        }

        private static void CreateStairVisual(
            Transform parent,
            string name,
            Vector2 center,
            Vector2 size)
        {
            CreateBlock(parent, name, center, size, Stair, false, 2);
            for (int index = -2; index <= 2; index++)
            {
                CreateBlock(parent, name + " Tread " + index,
                    center + new Vector2(index * size.x / 5f, 0f),
                    new Vector2(0.06f, size.y * 0.82f), Wall, false, 3);
            }
        }

        private static void CreateBoundary(Transform parent, Vector2 size)
        {
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            CreateBlock(parent, "Top Wall", new Vector2(0f, halfHeight),
                new Vector2(size.x, 0.28f), Wall, true, 5);
            CreateBlock(parent, "Bottom Wall", new Vector2(0f, -halfHeight),
                new Vector2(size.x, 0.28f), Wall, true, 5);
            CreateBlock(parent, "Left Wall Upper Platform Segment",
                new Vector2(-halfWidth, PlatformSideWallSegmentY),
                new Vector2(0.28f, PlatformSideWallSegmentHeight), Wall, true, 5);
            CreateBlock(parent, "Left Wall Lower Platform Segment",
                new Vector2(-halfWidth, -PlatformSideWallSegmentY),
                new Vector2(0.28f, PlatformSideWallSegmentHeight), Wall, true, 5);
            CreateBlock(parent, "Right Wall Upper Platform Segment",
                new Vector2(halfWidth, PlatformSideWallSegmentY),
                new Vector2(0.28f, PlatformSideWallSegmentHeight), Wall, true, 5);
            CreateBlock(parent, "Right Wall Lower Platform Segment",
                new Vector2(halfWidth, -PlatformSideWallSegmentY),
                new Vector2(0.28f, PlatformSideWallSegmentHeight), Wall, true, 5);
        }

        private static void CreateConcourseBoundary(Transform parent)
        {
            const float halfWidth = 13f;
            const float halfHeight = 6f;
            const float segmentWidth = 5.1f;
            const float segmentCenter = 8.45f;
            CreateBlock(parent, "Top Left Wall", new Vector2(-segmentCenter, halfHeight),
                new Vector2(segmentWidth, 0.28f), Wall, true, 5);
            CreateBlock(parent, "Top Right Wall", new Vector2(segmentCenter, halfHeight),
                new Vector2(segmentWidth, 0.28f), Wall, true, 5);
            CreateBlock(parent, "Bottom Left Wall", new Vector2(-segmentCenter, -halfHeight),
                new Vector2(segmentWidth, 0.28f), Wall, true, 5);
            CreateBlock(parent, "Bottom Right Wall", new Vector2(segmentCenter, -halfHeight),
                new Vector2(segmentWidth, 0.28f), Wall, true, 5);
            CreateBlock(parent, "Left Wall", new Vector2(-halfWidth, 0f),
                new Vector2(0.28f, 12f), Wall, true, 5);
            CreateBlock(parent, "Right Wall", new Vector2(halfWidth, 0f),
                new Vector2(0.28f, 12f), Wall, true, 5);
        }

        private static GridNavigation2D CreateNavigation(
            Transform parent,
            string name,
            Vector2 center,
            int width,
            int height,
            float cellSize)
        {
            GameObject navigationObject = new GameObject(name);
            navigationObject.transform.SetParent(parent);
            navigationObject.transform.position = center;
            GridNavigation2D navigation =
                navigationObject.AddComponent<GridNavigation2D>();
            navigation.ConfigureDimensions(width, height, cellSize);
            return navigation;
        }

        private static Transform CreatePoint(
            Transform parent,
            string name,
            Vector2 position)
        {
            GameObject point = new GameObject(name);
            point.transform.SetParent(parent);
            point.transform.position = position;
            return point.transform;
        }

        private static GameObject CreateBlock(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            Color color,
            bool obstacle,
            int sortingOrder)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Quad);
            block.name = name;
            block.transform.SetParent(parent);
            block.transform.position = new Vector3(position.x, position.y, 0f);
            block.transform.localScale = new Vector3(size.x, size.y, 1f);
            MeshCollider meshCollider = block.GetComponent<MeshCollider>();
            if (meshCollider != null)
            {
                UnityEngine.Object.DestroyImmediate(meshCollider);
            }

            MeshRenderer renderer = block.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GetOrCreateMaterial(name + " Material", color);
            renderer.sortingOrder = sortingOrder;
            if (obstacle)
            {
                block.AddComponent<BoxCollider2D>();
                block.AddComponent<NavigationObstacle>();
            }

            return block;
        }

        private static void CreateLabel(
            Transform parent,
            string text,
            Vector2 position,
            float characterSize)
        {
            GameObject labelObject = new GameObject("Label - " + text);
            labelObject.transform.SetParent(parent);
            labelObject.transform.position = new Vector3(position.x, position.y, -0.2f);
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = characterSize;
            label.fontSize = 48;
            label.color = Color.white;
        }

        private static Camera CreateCamera(Transform parent)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(parent);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 10.65f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            return camera;
        }

        private static UiBuildData CreateUi(Transform parent)
        {
            GameObject canvasObject = new GameObject("Utility Journey Debug UI");
            canvasObject.transform.SetParent(parent);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            Text worldText = CreateUiText(canvasObject.transform, "World Status",
                new Vector2(14f, -12f), new Vector2(900f, 70f), 14,
                TextAnchor.UpperLeft);
            Text thoughtText = CreateUiText(canvasObject.transform, "Passenger Thought",
                new Vector2(14f, -86f), new Vector2(1000f, 108f), 13,
                TextAnchor.UpperLeft);

            GameObject fade = new GameObject("Fade Overlay");
            fade.transform.SetParent(canvasObject.transform);
            RectTransform fadeRect = fade.AddComponent<RectTransform>();
            fadeRect.anchorMin = Vector2.zero;
            fadeRect.anchorMax = Vector2.one;
            fadeRect.offsetMin = Vector2.zero;
            fadeRect.offsetMax = Vector2.zero;
            Image image = fade.AddComponent<Image>();
            image.color = Color.black;
            CanvasGroup fadeGroup = fade.AddComponent<CanvasGroup>();
            fadeGroup.alpha = 1f;
            fade.transform.SetAsLastSibling();
            return new UiBuildData
            {
                FadeOverlay = fadeGroup,
                WorldText = worldText,
                ThoughtText = thoughtText
            };
        }

        private static Text CreateUiText(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 size,
            int fontSize,
            TextAnchor anchor)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent);
            RectTransform rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Material GetOrCreateMaterial(string sourceName, Color color)
        {
            Color32 colorKey = color;
            string safeName = $"SharedColor_{colorKey.r:X2}{colorKey.g:X2}{colorKey.b:X2}{colorKey.a:X2}";
            string path = MaterialFolder + "/" + safeName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Color");
                }
                material = new Material(shader) { name = safeName };
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", 0f);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static UtilityPassengerTuningProfile[] GetOrCreatePassengerProfiles()
        {
            return new[]
            {
                GetOrCreatePassengerProfile(
                    "QueuePlanner",
                    UtilityPassengerArchetype.QueuePlanner,
                    new UtilityPassengerDecisionWeights(
                        1.12f, 1.1f, 0.9f, 1.05f, 1.18f,
                        1f, 1.35f, 1f, 0.55f, 1.45f),
                    new Vector3(0.98f, 0.28f, 0.24f),
                    1.2f, 2.6f, 5f, 0.27f, 0.36f,
                    new UtilityPassengerSocialTuning(
                        2.35f, 0.78f, 0.32f, 0.82f, 0.42f,
                        0.96f, 0.44f, 0.7f, 3.5f)),
                GetOrCreatePassengerProfile(
                    "SeatSeeker",
                    UtilityPassengerArchetype.SeatSeeker,
                    new UtilityPassengerDecisionWeights(
                        0.92f, 0.86f, 1.15f, 0.9f, 1.42f,
                        1.16f, 0.9f, 1f, 1.58f, 0.62f),
                    new Vector3(0.22f, 1f, 0.5f),
                    1.05f, 2.25f, 4.1f, 0.36f, 0.48f,
                    new UtilityPassengerSocialTuning(
                        2.45f, 0.86f, 0.34f, 0.8f, 0.3f,
                        0.9f, 0.3f, 0.65f, 2.8f)),
                GetOrCreatePassengerProfile(
                    "WallRelaxed",
                    UtilityPassengerArchetype.WallRelaxed,
                    new UtilityPassengerDecisionWeights(
                        0.88f, 0.84f, 1.38f, 1.16f, 1.35f,
                        1.22f, 0.8f, 1f, 1.25f, 0.66f),
                    new Vector3(0.18f, 0.48f, 1f),
                    1f, 2.1f, 3.8f, 0.42f, 0.54f,
                    new UtilityPassengerSocialTuning(
                        2.55f, 0.92f, 0.36f, 0.88f, 0.24f,
                        0.92f, 0.2f, -0.55f, 2.6f)),
                GetOrCreatePassengerProfile(
                    "Balanced",
                    UtilityPassengerArchetype.Balanced,
                    UtilityPassengerDecisionWeights.Default,
                    new Vector3(0.64f, 0.64f, 0.56f),
                    1.15f, 2.45f, 4.5f, 0.32f, 0.42f,
                    UtilityPassengerSocialTuning.Default),
                GetOrCreatePassengerProfile(
                    "QuickTransfer",
                    UtilityPassengerArchetype.QuickTransfer,
                    new UtilityPassengerDecisionWeights(
                        1.22f, 1.22f, 0.72f, 0.78f, 1.12f,
                        0.86f, 1.62f, 1.12f, 0.34f, 1.82f),
                    new Vector3(1f, 0.12f, 0.14f),
                    1.28f, 2.82f, 5.6f, 0.22f, 0.3f,
                    new UtilityPassengerSocialTuning(
                        2.05f, 0.62f, 0.27f, 0.38f, 0.84f,
                        0.64f, 0.84f, 0.8f, 4.4f))
            };
        }

        private static UtilityPassengerTuningProfile GetOrCreatePassengerProfile(
            string profileName,
            UtilityPassengerArchetype archetype,
            UtilityPassengerDecisionWeights weights,
            Vector3 stylePreferences,
            float walkSpeed,
            float runSpeed,
            float acceleration,
            float decisionInterval,
            float repathInterval,
            UtilityPassengerSocialTuning socialTuning)
        {
            string path = ProfileFolder + "/" + profileName + ".asset";
            UtilityPassengerTuningProfile profile =
                AssetDatabase.LoadAssetAtPath<UtilityPassengerTuningProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<UtilityPassengerTuningProfile>();
                profile.name = profileName;
                AssetDatabase.CreateAsset(profile, path);
            }

            profile.ConfigurePrototype(
                profileName,
                archetype,
                weights,
                stylePreferences,
                walkSpeed,
                runSpeed,
                acceleration,
                decisionInterval,
                repathInterval,
                socialTuning);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/Scenes/Prototype", "UtilityJourney_AI");
            EnsureFolder(RootFolder, "Scripts");
            EnsureFolder(RootFolder + "/Scripts", "Editor");
            EnsureFolder(RootFolder, "Tests");
            EnsureFolder(RootFolder + "/Tests", "Editor");
            EnsureFolder(RootFolder, "Materials");
            EnsureFolder(RootFolder, "Profiles");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private sealed class UiBuildData
        {
            public CanvasGroup FadeOverlay;
            public Text WorldText;
            public Text ThoughtText;
        }

        private sealed class ConcourseBuildData
        {
            public GameObject Root;
            public Transform[] ExternalSpawns;
            public UtilityJourneyFacilityPrototype[] StreetExits;
            public UtilityJourneyFacilityPrototype[] UpboundStairs;
            public UtilityJourneyFacilityPrototype[] DownboundStairs;
            public StairArrivalBuildData[] LowerArrivals;
        }

        private sealed class PlatformBuildData
        {
            public GameObject Root;
            public StairArrivalBuildData[] UpboundArrivals;
            public StairArrivalBuildData[] DownboundArrivals;
            public StairArrivalBuildData AlightArrival;
            public UtilityJourneyFacilityPrototype[] BoardingDoors;
            public UtilityJourneyFacilityPrototype[] UpStairs;
            public Transform TrainVisual;
            public UtilityJourneyPassagePrototype[] TrainDoors;
        }

        private sealed class GateLaneBuildData
        {
            public string RouteGroup;
            public UtilityJourneyPassagePrototype Passage;
            public Transform Center;
            public Transform OutsideApproach;
            public Transform InsideApproach;
        }

        private sealed class StairLaneBuildData
        {
            public string RouteGroup;
            public Transform Approach;
            public Transform Entry;
            public Transform Deep;
        }

        private readonly struct StairArrivalBuildData
        {
            public readonly Transform Spawn;
            public readonly Transform Entry;
            public readonly Transform Release;

            public StairArrivalBuildData(
                Transform spawn,
                Transform entry,
                Transform release)
            {
                Spawn = spawn;
                Entry = entry;
                Release = release;
            }
        }

        private sealed class TrainVisualBuildData
        {
            public Transform Root;
            public UtilityJourneyPassagePrototype[] Doors;
            public Vector2[] DoorPositions;
        }

        private sealed class OverviewTrainBuildData
        {
            public Transform Root;
            public TrainDoorController[] PlatformDoors;
            public GridNavigation2D InteriorNavigation;
            public Transform[] InsideDoorPoints;
            public UtilityJourneyFacilityPrototype[] RideFacilities;
            public UtilityJourneyFacilityPrototype[] AlightFacilities;
        }

        private sealed class OverviewRideAnchorData
        {
            public Transform Anchor;
            public string Id;
            public UtilityJourneyWaitingStyle Style;
            public float Comfort;
        }

        private sealed class OverviewStationBuildData
        {
            public Transform Root;
            public GameObject Concourse;
            public GameObject Platform;
            public GridNavigation2D ConcourseNavigation;
            public GridNavigation2D LowerPlatformNavigation;
            public GridNavigation2D UpperPlatformNavigation;
            public UtilityJourneyFacilityPrototype[] Facilities;
            public Transform[] ExternalSpawns;
            public Transform[] ExternalStairVisuals;
        }

        private sealed class OverviewJourneyBuildData
        {
            public UtilityJourneyFacilityPrototype[] Facilities;
            public UtilityJourneyNavigationBinding[] NavigationBindings;
            public Transform[] OriginEntranceOutsidePoints;
            public Transform[] OriginEntranceSpawns;
            public UtilityJourneyFacilityPrototype[] DestinationExits;
        }

        private sealed class TrainBuildData
        {
            public GameObject Root;
            public Transform TrainVisual;
            public Transform BoardingSpawn;
            public UtilityJourneyFacilityPrototype AlightDoor;
        }
    }
}
#endif
