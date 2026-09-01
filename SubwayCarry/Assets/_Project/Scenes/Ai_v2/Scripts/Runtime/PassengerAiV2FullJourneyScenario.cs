using SubwayCarry.AI;
using UnityEngine;

namespace SubwayCarry.AI.V2
{
    public enum PassengerAiV2JourneyMap
    {
        OriginConcourse,
        OriginPlatform,
        TrainInterior,
        DestinationPlatform,
        DestinationConcourse
    }

    /// <summary>
    /// 06 통합 시험의 각 공간과 Map 전환을 소유한다. 한 번에 한 Map만 활성화한다.
    /// </summary>
    public sealed class PassengerAiV2FullJourneyScenario : MonoBehaviour
    {
        public const string SceneTitle = "AI V2 06 - FULL JOURNEY";
        public const string ScenePurpose =
            "Outside -> gate -> stair -> platform -> train -> alight -> destination exit";

        [SerializeField] private PassengerAiV2PersonalityProfile baselineProfile;
        [SerializeField] private Transform prototypeMapRoot;
        [SerializeField] private Transform prototypeActivityRoot;
        [SerializeField, Min(0.1f)] private float fadeDuration = 0.45f;

        private readonly Rect stationBounds = Rect.MinMaxRect(-13.4f, -7.8f, 13.4f, 7.8f);
        private GameObject originConcourseRoot;
        private GameObject originPlatformRoot;
        private GameObject destinationPlatformRoot;
        private GameObject destinationConcourseRoot;
        private PassengerAiV2CrowdManager crowdManager;
        private PassengerAiV2Agent journeyAgent;
        private PassengerAiV2JourneyBlackboard blackboard;
        private PassengerAiV2FullJourneyPlanner planner;
        private PassengerAiV2FullJourneyAction journeyAction;
        private PassengerAiV2TrainInteriorEnvironment interiorEnvironment;
        private PassengerAiV2TrainInteriorAction interiorAction;
        private Sprite sharedSprite;
        private Texture2D fadeTexture;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private bool transitionActive;
        private bool transitionSwitched;
        private PassengerAiV2JourneyMap transitionTarget;
        private Vector2 transitionSpawn;
        private Rect transitionBounds;
        private float fadeAlpha;

        private Transform originDoorLeftPanel;
        private Transform originDoorRightPanel;
        private Transform destinationDoorLeftPanel;
        private Transform destinationDoorRightPanel;

        public PassengerAiV2JourneyMap ActiveMap { get; private set; }
        public bool IsTransitioning => transitionActive;
        public int TransitionSerial { get; private set; }
        public PassengerAiV2FareGateSmartObject OriginEntryGate { get; private set; }
        public PassengerAiV2FareGateSmartObject DestinationExitGate { get; private set; }
        public PassengerAiV2StairSmartObject OriginStreetStair { get; private set; }
        public PassengerAiV2StairSmartObject OriginPlatformStair { get; private set; }
        public PassengerAiV2StairSmartObject DestinationPlatformStair { get; private set; }
        public PassengerAiV2StairSmartObject DestinationStreetStair { get; private set; }
        public PassengerAiV2TrainDoorSmartObject OriginTrainDoor { get; private set; }
        public PassengerAiV2TrainDoorSmartObject DestinationTrainDoor { get; private set; }
        public Vector2 OriginOutsideStart => new Vector2(-7f, -7.15f);
        public Vector2 OriginPlatformSpawn => new Vector2(9f, -5.2f);
        public Vector2 OriginPlatformWaitPoint => new Vector2(-3.2f, -4.35f);
        public Vector2 TrainInteriorSpawn { get; private set; }
        public Vector2 DestinationTrainSpawn => new Vector2(0.62f, 4.8f);
        public Vector2 DestinationConcourseSpawn => new Vector2(7f, 6.8f);
        public Vector2 DestinationOutsidePoint => new Vector2(-7f, -7.25f);
        public Rect StationBounds => stationBounds;
        public Rect TrainInteriorBounds { get; private set; } =
            Rect.MinMaxRect(-9f, -2f, 12f, 2f);

        public void Configure(
            PassengerAiV2PersonalityProfile profile,
            Transform mapRoot,
            Transform activityRoot)
        {
            baselineProfile = profile;
            prototypeMapRoot = mapRoot;
            prototypeActivityRoot = activityRoot;
        }

        private void Awake()
        {
            sharedSprite = CreateSharedSprite();
            fadeTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            fadeTexture.name = "AI_V2_06_FadeTexture";
            fadeTexture.SetPixel(0, 0, Color.white);
            fadeTexture.Apply(false, true);

            BuildRuntimeMaps();
            BuildTrainInteriorSmartObjects();
            SpawnJourneyPassenger();
            ActivateMap(PassengerAiV2JourneyMap.OriginConcourse);
            journeyAgent.TeleportAndSetTarget(OriginOutsideStart, OriginOutsideStart);
            journeyAgent.SetMovementBounds(stationBounds);
        }

        private void Update()
        {
            if (!transitionActive)
            {
                return;
            }

            float step = Time.deltaTime / Mathf.Max(0.1f, fadeDuration);
            if (!transitionSwitched)
            {
                fadeAlpha = Mathf.MoveTowards(fadeAlpha, 1f, step);
                if (fadeAlpha >= 0.999f)
                {
                    ActivateMap(transitionTarget);
                    journeyAgent.SetMovementBounds(transitionBounds);
                    journeyAgent.TeleportAndSetTarget(transitionSpawn, transitionSpawn);
                    transitionSwitched = true;
                }
            }
            else
            {
                fadeAlpha = Mathf.MoveTowards(fadeAlpha, 0f, step);
                if (fadeAlpha <= 0.001f)
                {
                    fadeAlpha = 0f;
                    transitionActive = false;
                    transitionSwitched = false;
                    TransitionSerial++;
                }
            }
        }

        private void OnGUI()
        {
            if (titleStyle == null || bodyStyle == null)
            {
                BuildStyles();
            }

            GUI.Label(new Rect(18f, 14f, 650f, 30f), SceneTitle, titleStyle);
            GUI.Label(new Rect(18f, 43f, 1000f, 24f), ScenePurpose, bodyStyle);
            string stage = blackboard != null ? blackboard.CurrentStage.ToString() : "Missing";
            int completed = blackboard != null ? blackboard.CompletedJourneyCount : 0;
            int recoveries = blackboard != null ? blackboard.RecoveryCount : 0;
            int deadlocks = crowdManager != null ? crowdManager.DeadlockedCount : 0;
            GUI.Label(
                new Rect(18f, 67f, 1000f, 24f),
                $"Map {ActiveMap} | Stage {stage} | Transition {transitionActive}",
                bodyStyle);
            GUI.Label(
                new Rect(18f, 90f, 1000f, 24f),
                $"Entry tag {BoolMark(blackboard != null && blackboard.EntryCardTagged)} | Board {BoolMark(blackboard != null && blackboard.BoardedTrain)} | Activity {BoolMark(blackboard != null && blackboard.UsedTrainActivity)} | Prepared {BoolMark(blackboard != null && blackboard.PreparedToAlight)}",
                bodyStyle);
            GUI.Label(
                new Rect(18f, 113f, 1000f, 24f),
                $"Alight {BoolMark(blackboard != null && blackboard.AlightedTrain)} | Exit tag {BoolMark(blackboard != null && blackboard.ExitCardTagged)} | Completed {completed} | Recoveries {recoveries} | Deadlocked {deadlocks}",
                bodyStyle);

            if (fadeAlpha > 0f)
            {
                Color previous = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, fadeAlpha);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), fadeTexture);
                GUI.color = previous;
            }
        }

        private void OnDestroy()
        {
            if (sharedSprite != null)
            {
                Destroy(sharedSprite.texture);
                Destroy(sharedSprite);
            }

            if (fadeTexture != null)
            {
                Destroy(fadeTexture);
            }
        }

        public bool RequestMapTransition(
            PassengerAiV2JourneyMap targetMap,
            Vector2 spawnPoint,
            Rect movementBounds)
        {
            if (transitionActive)
            {
                return false;
            }

            transitionTarget = targetMap;
            transitionSpawn = spawnPoint;
            transitionBounds = movementBounds;
            transitionActive = true;
            transitionSwitched = false;
            journeyAgent.SetHoldPosition(true);
            return true;
        }

        public PassengerAiV2TrainInteriorAction StartTrainInteriorActivity(
            PassengerAiV2Agent agent,
            PassengerAiV2RuntimePersonality personality)
        {
            if (interiorAction != null)
            {
                return interiorAction;
            }

            interiorAction = agent.gameObject.AddComponent<PassengerAiV2TrainInteriorAction>();
            interiorAction.Initialize(
                agent,
                interiorEnvironment,
                personality,
                TrainInteriorSpawn,
                TrainInteriorBounds,
                0f);
            return interiorAction;
        }

        public void StopTrainInteriorActivity()
        {
            if (interiorAction != null)
            {
                Destroy(interiorAction);
                interiorAction = null;
            }

            journeyAgent.SetHoldPosition(false);
        }

        private void BuildRuntimeMaps()
        {
            GameObject managerObject = new GameObject("CrowdManager_SpatialHash");
            managerObject.transform.SetParent(transform, false);
            crowdManager = managerObject.AddComponent<PassengerAiV2CrowdManager>();

            originConcourseRoot = BuildConcourseMap(true);
            destinationConcourseRoot = BuildConcourseMap(false);
            originPlatformRoot = BuildPlatformMap(true);
            destinationPlatformRoot = BuildPlatformMap(false);
        }

        private GameObject BuildConcourseMap(bool origin)
        {
            GameObject root = new GameObject(origin
                ? "MAP_Origin_OutsideAndFareConcourse"
                : "MAP_Destination_FareConcourseAndOutside");
            root.transform.SetParent(transform, false);
            CreateRect("Background", Vector2.zero, new Vector2(30f, 19f),
                new Color(0.025f, 0.04f, 0.055f), -2, root.transform);
            CreateRect("ConcourseFloor", Vector2.zero, new Vector2(27f, 15.5f),
                new Color(0.28f, 0.33f, 0.35f), 0, root.transform);
            CreateRect("FareBoundary_Left", new Vector2(-9.1f, 0f), new Vector2(8.1f, 0.5f),
                new Color(0.16f, 0.22f, 0.26f), 4, root.transform);
            CreateRect("FareBoundary_Right", new Vector2(9.1f, 0f), new Vector2(8.1f, 0.5f),
                new Color(0.16f, 0.22f, 0.26f), 4, root.transform);
            CreateRect(origin ? "EXIT_1_Outside" : "EXIT_4_Outside",
                new Vector2(-7f, -7.25f), new Vector2(4.2f, 0.55f),
                new Color(0.95f, 0.76f, 0.12f), 4, root.transform);

            PassengerAiV2FareGateDirection gateDirection = origin
                ? PassengerAiV2FareGateDirection.LowerToUpper
                : PassengerAiV2FareGateDirection.UpperToLower;
            PassengerAiV2FareGateSmartObject gate = CreateGate(
                origin ? "Gate_Origin_Entry" : "Gate_Destination_Exit",
                Vector2.zero,
                gateDirection,
                root.transform);
            if (origin)
            {
                OriginEntryGate = gate;
                OriginStreetStair = CreateStair(
                    "Stair_Origin_StreetToConcourse",
                    new Vector2(-7f, -7.2f),
                    new Vector2(-7f, -6.45f),
                    new Vector2(-7f, -5.25f),
                    new Vector2(-7f, -4.5f),
                    root.transform);
                OriginPlatformStair = CreateStair(
                    "Stair_Origin_ConcourseToPlatform",
                    new Vector2(7f, 4.2f),
                    new Vector2(7f, 4.95f),
                    new Vector2(7f, 6.15f),
                    new Vector2(7f, 6.9f),
                    root.transform);
            }
            else
            {
                DestinationExitGate = gate;
                DestinationStreetStair = CreateStair(
                    "Stair_Destination_ConcourseToStreet",
                    new Vector2(-7f, -7.2f),
                    new Vector2(-7f, -6.45f),
                    new Vector2(-7f, -5.25f),
                    new Vector2(-7f, -4.5f),
                    root.transform);
                CreateStairVisual(
                    "Stair_Destination_PlatformArrival",
                    new Vector2(7f, 5.7f),
                    root.transform);
            }

            return root;
        }

        private GameObject BuildPlatformMap(bool origin)
        {
            GameObject root = new GameObject(origin
                ? "MAP_Origin_PlatformAndTrainDoor"
                : "MAP_Destination_TrainDoorAndPlatform");
            root.transform.SetParent(transform, false);
            CreateRect("Background", Vector2.zero, new Vector2(30f, 19f),
                new Color(0.018f, 0.03f, 0.045f), -2, root.transform);
            CreateRect("TrainSide", new Vector2(0f, 4.65f), new Vector2(28f, 8.1f),
                new Color(0.16f, 0.26f, 0.33f), 0, root.transform);
            CreateRect("Platform", new Vector2(0f, -4.65f), new Vector2(28f, 8.1f),
                new Color(0.31f, 0.35f, 0.37f), 0, root.transform);
            CreateRect("SafetyLine", new Vector2(0f, -2.3f), new Vector2(28f, 0.13f),
                new Color(0.98f, 0.8f, 0.18f), 4, root.transform);
            CreateRect("TrainWall_Left", new Vector2(-8.1f, 0f), new Vector2(12.8f, 0.55f),
                new Color(0.43f, 0.54f, 0.62f), 5, root.transform);
            CreateRect("TrainWall_Right", new Vector2(8.1f, 0f), new Vector2(12.8f, 0.55f),
                new Color(0.43f, 0.54f, 0.62f), 5, root.transform);

            PassengerAiV2TrainDoorSmartObject door = CreateTrainDoor(
                origin ? "Door_Origin_Board" : "Door_Destination_Alight",
                root.transform,
                origin);
            if (origin)
            {
                OriginTrainDoor = door;
                CreateStairVisual("Stair_FromOriginConcourse", OriginPlatformSpawn, root.transform);
                CreateRect("WaitHere", OriginPlatformWaitPoint, new Vector2(2.4f, 0.12f),
                    new Color(0.15f, 0.75f, 0.72f), 3, root.transform);
            }
            else
            {
                DestinationTrainDoor = door;
                DestinationPlatformStair = CreateStair(
                    "Stair_Destination_PlatformToConcourse",
                    new Vector2(9f, -7f),
                    new Vector2(9f, -6.25f),
                    new Vector2(9f, -5.05f),
                    new Vector2(9f, -4.3f),
                    root.transform);
            }

            return root;
        }

        private PassengerAiV2FareGateSmartObject CreateGate(
            string objectName,
            Vector2 center,
            PassengerAiV2FareGateDirection direction,
            Transform parent)
        {
            GameObject gateRoot = new GameObject(objectName);
            gateRoot.transform.SetParent(parent, false);
            gateRoot.transform.position = center;
            CreateRect("Housing_Left", new Vector2(-0.62f, 0f), new Vector2(0.34f, 2.6f),
                new Color(0.19f, 0.25f, 0.29f), 5, gateRoot.transform);
            CreateRect("Housing_Right", new Vector2(0.62f, 0f), new Vector2(0.34f, 2.6f),
                new Color(0.19f, 0.25f, 0.29f), 5, gateRoot.transform);
            GameObject barrier = CreateRect("OrangeBarrier", Vector2.zero, new Vector2(0.88f, 0.15f),
                new Color(1f, 0.46f, 0.1f), 6, gateRoot.transform);
            float sign = direction == PassengerAiV2FareGateDirection.LowerToUpper ? 1f : -1f;
            CreateRect("CardReader", new Vector2(-0.62f, -sign * 1.02f), new Vector2(0.38f, 0.38f),
                new Color(0.15f, 0.88f, 0.92f), 7, gateRoot.transform);
            PassengerAiV2FareGateSmartObject gate =
                gateRoot.AddComponent<PassengerAiV2FareGateSmartObject>();
            gate.Configure(
                objectName.ToLowerInvariant(),
                direction,
                center + Vector2.down * sign * 2.05f,
                center + Vector2.down * sign * 1.02f,
                center + Vector2.up * sign * 0.9f,
                center + Vector2.up * sign * 2.05f,
                barrier.GetComponent<SpriteRenderer>());
            return gate;
        }

        private PassengerAiV2StairSmartObject CreateStair(
            string objectName,
            Vector2 lowerApproach,
            Vector2 lowerEntry,
            Vector2 upperEntry,
            Vector2 upperApproach,
            Transform parent)
        {
            CreateRect(objectName + "_Body", (lowerEntry + upperEntry) * 0.5f,
                new Vector2(3.1f, Mathf.Abs(upperEntry.y - lowerEntry.y) + 0.8f),
                new Color(0.9f, 0.72f, 0.14f), 2, parent);
            GameObject stairObject = new GameObject(objectName);
            stairObject.transform.SetParent(parent, false);
            PassengerAiV2StairSmartObject stair =
                stairObject.AddComponent<PassengerAiV2StairSmartObject>();
            stair.Configure(
                objectName.ToLowerInvariant(),
                lowerApproach,
                lowerEntry,
                upperEntry,
                upperApproach,
                4,
                0.28f);
            return stair;
        }

        private void CreateStairVisual(string objectName, Vector2 center, Transform parent)
        {
            CreateRect(objectName, center, new Vector2(3.2f, 2.5f),
                new Color(0.9f, 0.72f, 0.14f), 2, parent);
        }

        private PassengerAiV2TrainDoorSmartObject CreateTrainDoor(
            string objectName,
            Transform parent,
            bool origin)
        {
            GameObject doorRoot = new GameObject(objectName);
            doorRoot.transform.SetParent(parent, false);
            GameObject leftPanel = CreateRect("DoorPanel_Left", new Vector2(-0.78f, 0f),
                new Vector2(1.45f, 0.42f), new Color(0.2f, 0.58f, 0.74f), 7, doorRoot.transform);
            GameObject rightPanel = CreateRect("DoorPanel_Right", new Vector2(0.78f, 0f),
                new Vector2(1.45f, 0.42f), new Color(0.2f, 0.58f, 0.74f), 7, doorRoot.transform);
            PassengerAiV2TrainDoorSmartObject door =
                doorRoot.AddComponent<PassengerAiV2TrainDoorSmartObject>();
            door.Configure(objectName.ToLowerInvariant(), Vector2.zero,
                leftPanel.transform, rightPanel.transform, 2);
            if (origin)
            {
                originDoorLeftPanel = leftPanel.transform;
                originDoorRightPanel = rightPanel.transform;
            }
            else
            {
                destinationDoorLeftPanel = leftPanel.transform;
                destinationDoorRightPanel = rightPanel.transform;
            }

            return door;
        }

        private void BuildTrainInteriorSmartObjects()
        {
            if (prototypeMapRoot == null || prototypeActivityRoot == null)
            {
                return;
            }

            GameObject systemRoot = new GameObject("Journey_TrainInteriorSmartObjects");
            systemRoot.transform.SetParent(prototypeMapRoot, false);
            interiorEnvironment = systemRoot.AddComponent<PassengerAiV2TrainInteriorEnvironment>();
            interiorEnvironment.ConfigurePhaseDurations(3f, 7f, 8f, 2f, 1f);

            GridNavigation2D navigation =
                prototypeActivityRoot.GetComponentInChildren<GridNavigation2D>(true);
            if (navigation != null)
            {
                Rect bounds = navigation.WorldBounds;
                TrainInteriorBounds = Rect.MinMaxRect(
                    bounds.xMin + 0.42f,
                    bounds.yMin + 0.42f,
                    bounds.xMax - 0.42f,
                    bounds.yMax - 0.42f);
            }

            PassengerDoorway[] doorways =
                prototypeActivityRoot.GetComponentsInChildren<PassengerDoorway>(true);
            PassengerSeatPrototype[] seats =
                prototypeActivityRoot.GetComponentsInChildren<PassengerSeatPrototype>(true);
            int index = 0;
            for (int i = 0; i < seats.Length; i++)
            {
                for (int slot = 0; slot < seats[i].Capacity; slot++)
                {
                    Transform point = seats[i].GetSittingPoint(slot);
                    if (point != null)
                    {
                        CreateInteriorSpot(systemRoot.transform, $"Seat_{++index:00}",
                            PassengerAiV2InteriorSpotKind.Seat, point.position, 1f,
                            CalculateExitCost(point.position, doorways));
                    }
                }
            }

            PassengerActivityPoint[] standPoints =
                prototypeActivityRoot.GetComponentsInChildren<PassengerActivityPoint>(true);
            index = 0;
            for (int i = 0; i < standPoints.Length; i++)
            {
                if (standPoints[i].ActivityType == PassengerActivityType.Handhold)
                {
                    CreateInteriorSpot(systemRoot.transform, $"Stand_{++index:00}",
                        PassengerAiV2InteriorSpotKind.Stand, standPoints[i].transform.position,
                        0.48f, CalculateExitCost(standPoints[i].transform.position, doorways));
                }
            }

            index = 0;
            for (int i = 0; i < doorways.Length; i++)
            {
                PassengerDoorway doorway = doorways[i];
                if (doorway == null || doorway.Door == null)
                {
                    continue;
                }

                if (!doorway.IsUsable)
                {
                    Vector2 doorPosition = doorway.Door.transform.position;
                    float side = Mathf.Sign(doorPosition.y - prototypeActivityRoot.position.y);
                    Vector2 leanPosition = doorPosition - Vector2.up * side * 0.42f;
                    CreateInteriorSpot(systemRoot.transform, $"Lean_{++index:00}",
                        PassengerAiV2InteriorSpotKind.Lean, leanPosition, 0.76f,
                        CalculateExitCost(leanPosition, doorways));
                }
                else if (doorway.InsidePoint != null)
                {
                    Vector2 position = doorway.InsidePoint.position;
                    CreateInteriorSpot(systemRoot.transform, $"DoorPrepare_{i:00}",
                        PassengerAiV2InteriorSpotKind.DoorPrepare, position, 0.55f, 0f);
                    if (TrainInteriorSpawn == Vector2.zero)
                    {
                        TrainInteriorSpawn = position + Vector2.up * 0.85f;
                    }
                }
            }

            if (TrainInteriorSpawn == Vector2.zero)
            {
                TrainInteriorSpawn = new Vector2(0f, -0.4f);
            }
        }

        private void CreateInteriorSpot(
            Transform parent,
            string spotName,
            PassengerAiV2InteriorSpotKind kind,
            Vector2 position,
            float comfort,
            float exitCost)
        {
            GameObject spotObject = new GameObject("Journey_" + spotName);
            spotObject.transform.SetParent(parent, false);
            spotObject.transform.position = position;
            PassengerAiV2InteriorSpotSmartObject spot =
                spotObject.AddComponent<PassengerAiV2InteriorSpotSmartObject>();
            spot.Configure("journey_" + spotName.ToLowerInvariant(), kind, position,
                comfort, exitCost, null);
            interiorEnvironment.RegisterSpot(spot);
        }

        private static float CalculateExitCost(Vector2 position, PassengerDoorway[] doorways)
        {
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < doorways.Length; i++)
            {
                if (doorways[i] != null && doorways[i].IsUsable && doorways[i].InsidePoint != null)
                {
                    nearest = Mathf.Min(nearest,
                        Vector2.Distance(position, doorways[i].InsidePoint.position));
                }
            }

            return float.IsPositiveInfinity(nearest) ? 0.5f : Mathf.Clamp01(nearest / 7f);
        }

        private void SpawnJourneyPassenger()
        {
            PassengerAiV2RuntimePersonality personality = baselineProfile != null
                ? baselineProfile.CreateRuntimePersonality(10600)
                : PassengerAiV2RuntimePersonality.CreateFallback(10600);
            GameObject passengerObject = new GameObject("Passenger_FullJourney_BasePassenger");
            passengerObject.transform.SetParent(transform, false);
            passengerObject.AddComponent<SpriteRenderer>();
            passengerObject.AddComponent<CircleCollider2D>();
            passengerObject.AddComponent<Rigidbody2D>();
            journeyAgent = passengerObject.AddComponent<PassengerAiV2Agent>();
            journeyAgent.Initialize(
                crowdManager,
                OriginOutsideStart,
                OriginOutsideStart,
                personality,
                0f,
                stationBounds.yMin,
                stationBounds.yMax,
                sharedSprite,
                new Color(0.22f, 0.83f, 0.76f));
            journeyAgent.transform.localScale *= 0.85f;
            journeyAgent.SetAutoLoop(false);
            journeyAgent.SetMovementBounds(stationBounds);

            blackboard = passengerObject.AddComponent<PassengerAiV2JourneyBlackboard>();
            planner = passengerObject.AddComponent<PassengerAiV2FullJourneyPlanner>();
            planner.Initialize(blackboard);
            journeyAction = passengerObject.AddComponent<PassengerAiV2FullJourneyAction>();
            journeyAction.Initialize(journeyAgent, this, planner, blackboard, personality);
        }

        private void ActivateMap(PassengerAiV2JourneyMap map)
        {
            originConcourseRoot.SetActive(map == PassengerAiV2JourneyMap.OriginConcourse);
            originPlatformRoot.SetActive(map == PassengerAiV2JourneyMap.OriginPlatform);
            destinationPlatformRoot.SetActive(map == PassengerAiV2JourneyMap.DestinationPlatform);
            destinationConcourseRoot.SetActive(map == PassengerAiV2JourneyMap.DestinationConcourse);
            if (prototypeMapRoot != null)
            {
                prototypeMapRoot.gameObject.SetActive(map == PassengerAiV2JourneyMap.TrainInterior);
            }

            ActiveMap = map;
            if (map == PassengerAiV2JourneyMap.OriginPlatform && OriginTrainDoor != null)
            {
                OriginTrainDoor.Configure("door_origin_board", Vector2.zero,
                    originDoorLeftPanel, originDoorRightPanel, 2);
            }
            else if (map == PassengerAiV2JourneyMap.DestinationPlatform
                     && DestinationTrainDoor != null)
            {
                DestinationTrainDoor.Configure("door_destination_alight", Vector2.zero,
                    destinationDoorLeftPanel, destinationDoorRightPanel, 2);
            }
        }

        private GameObject CreateRect(
            string objectName,
            Vector2 position,
            Vector2 size,
            Color color,
            int sortingOrder,
            Transform parent)
        {
            GameObject rectangle = new GameObject(objectName);
            rectangle.transform.SetParent(parent, false);
            rectangle.transform.localPosition = position;
            rectangle.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = rectangle.AddComponent<SpriteRenderer>();
            renderer.sprite = sharedSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return rectangle;
        }

        private static Sprite CreateSharedSprite()
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = "AI_V2_06_RuntimeWhiteTexture";
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 1f);
        }

        private void BuildStyles()
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.94f, 0.97f, 1f) }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.84f, 0.89f, 0.92f) }
            };
        }

        private static string BoolMark(bool value)
        {
            return value ? "YES" : "NO";
        }
    }
}
