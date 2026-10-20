using SubwayCarry.AI;
using UnityEngine;

namespace SubwayCarry.AI.V2
{
    /// <summary>
    /// AI V2 5단계인 객차 내 좌석, 입석, 기대기 Utility 선택과 하차 준비를 재현한다.
    /// </summary>
    public sealed class PassengerAiV2TrainInteriorScenario : MonoBehaviour
    {
        public const string SceneTitle = "AI V2 05 - TRAIN INTERIOR";
        public const string ScenePurpose = "Utility seat / stand / lean choice -> prepare near door";

        [Header("Scenario")]
        [SerializeField, Range(4, 60)] private int passengerCount = 8;
        [SerializeField] private PassengerAiV2PersonalityProfile baselineProfile;
        [SerializeField] private Transform prototypeMapRoot;
        [SerializeField] private Transform prototypeActivityRoot;

        private Rect worldBounds = Rect.MinMaxRect(-13.4f, -7.6f, 13.4f, 7.6f);
        private PassengerAiV2CrowdManager crowdManager;
        private PassengerAiV2TrainInteriorEnvironment environment;
        private Sprite sharedSprite;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;

        public bool UsesPrototypeAiMap => prototypeMapRoot != null && prototypeActivityRoot != null;
        public int ImportedSeatPointCount { get; private set; }
        public int ImportedStandPointCount { get; private set; }
        public int ImportedLeanPointCount { get; private set; }
        public int ImportedDoorPreparePointCount { get; private set; }

        public void ConfigureBaselineProfile(PassengerAiV2PersonalityProfile profile)
        {
            baselineProfile = profile;
        }

        public void ConfigurePrototypeMap(
            PassengerAiV2PersonalityProfile profile,
            Transform mapRoot,
            Transform activityRoot,
            int importedPassengerCount)
        {
            baselineProfile = profile;
            prototypeMapRoot = mapRoot;
            prototypeActivityRoot = activityRoot;
            passengerCount = Mathf.Clamp(importedPassengerCount, 4, 60);
        }

        private void Awake()
        {
            sharedSprite = CreateSharedSprite();
            if (UsesPrototypeAiMap)
            {
                BuildPrototypeAiLayout();
            }
            else
            {
                BuildLayout();
            }

            SpawnPassengers();
        }

        private void OnDestroy()
        {
            if (sharedSprite != null)
            {
                Destroy(sharedSprite.texture);
                Destroy(sharedSprite);
            }
        }

        private void OnGUI()
        {
            if (titleStyle == null || bodyStyle == null)
            {
                BuildStyles();
            }

            const float left = 18f;
            GUI.Label(new Rect(left, 14f, 560f, 32f), SceneTitle, titleStyle);
            GUI.Label(new Rect(left, 43f, 760f, 24f), ScenePurpose, bodyStyle);

            int agents = crowdManager != null ? crowdManager.RegisteredCount : 0;
            int deadlocks = crowdManager != null ? crowdManager.DeadlockedCount : 0;
            string phase = environment != null ? environment.Phase.ToString() : "Missing";
            int cycle = environment != null ? environment.CycleIndex : -1;
            int seats = environment != null ? environment.SeatOccupiedCount : 0;
            int stands = environment != null ? environment.StandOccupiedCount : 0;
            int leans = environment != null ? environment.LeanOccupiedCount : 0;
            int prepared = environment != null ? environment.PreparedCount : 0;
            int decisions = environment != null ? environment.DecisionCount : 0;
            int replans = environment != null ? environment.ReplanCount : 0;
            int invalid = environment != null ? environment.InvalidReservationCount : 0;
            int completedCycles = environment != null ? environment.CompletedCycleCount : 0;
            int lastPrepared = environment != null ? environment.LastCyclePreparedCount : 0;
            int lastPeak = environment != null ? environment.LastCyclePeakActivityCount : 0;
            bool usedAllKinds = environment != null && environment.LastCycleUsedAllActivityKinds;

            GUI.Label(
                new Rect(left, 68f, 900f, 24f),
                $"Phase {phase} | Cycle {cycle} | Agents {agents} | Deadlocked {deadlocks}",
                bodyStyle);
            GUI.Label(
                new Rect(left, 91f, 900f, 24f),
                $"Occupied Seat/Stand/Lean {seats}/{stands}/{leans} | Prepared {prepared}/{agents}",
                bodyStyle);
            GUI.Label(
                new Rect(left, 114f, 900f, 24f),
                $"Utility decisions {decisions} | Replans {replans} | Invalid reservations {invalid}",
                bodyStyle);
            GUI.Label(
                new Rect(left, 137f, 1040f, 24f),
                $"Last cycle {completedCycles}: Activity peak {lastPeak} | All kinds {usedAllKinds} | Prepared {lastPrepared}/{agents}",
                bodyStyle);
            GUI.Label(
                new Rect(left, 160f, 1040f, 24f),
                "PASS: unique spots / stable activity / all prepare near door / no 3s deadlock",
                bodyStyle);
            if (UsesPrototypeAiMap)
            {
                GUI.Label(
                    new Rect(left, 183f, 1100f, 24f),
                    $"Prototype_AI anchors Seat/Stand/Lean/Door {ImportedSeatPointCount}/{ImportedStandPointCount}/{ImportedLeanPointCount}/{ImportedDoorPreparePointCount}",
                    bodyStyle);
            }
        }

        private void BuildPrototypeAiLayout()
        {
            GridNavigation2D navigation =
                prototypeActivityRoot.GetComponentInChildren<GridNavigation2D>(true);
            if (navigation != null)
            {
                Rect sourceBounds = navigation.WorldBounds;
                worldBounds = Rect.MinMaxRect(
                    sourceBounds.xMin + 0.42f,
                    sourceBounds.yMin + 0.42f,
                    sourceBounds.xMax - 0.42f,
                    sourceBounds.yMax - 0.42f);
            }

            GameObject managerObject = new GameObject("CrowdManager_SpatialHash");
            managerObject.transform.SetParent(transform, false);
            crowdManager = managerObject.AddComponent<PassengerAiV2CrowdManager>();

            GameObject environmentObject = new GameObject("SmartObjectSet_From_Prototype_AI");
            environmentObject.transform.SetParent(transform, false);
            environment = environmentObject.AddComponent<PassengerAiV2TrainInteriorEnvironment>();
            environment.ConfigurePhaseDurations(5f, 12f, 16f, 3f, 1f);

            PassengerDoorway[] sourceDoorways =
                prototypeActivityRoot.GetComponentsInChildren<PassengerDoorway>(true);
            RegisterPrototypeSeatSpots(environmentObject.transform, sourceDoorways);
            RegisterPrototypeStandSpots(environmentObject.transform, sourceDoorways);
            RegisterPrototypeLeanSpots(environmentObject.transform, sourceDoorways);
            RegisterPrototypeDoorPrepareSpots(environmentObject.transform, sourceDoorways);
        }

        private void RegisterPrototypeSeatSpots(
            Transform parent,
            PassengerDoorway[] sourceDoorways)
        {
            PassengerSeatPrototype[] seats =
                prototypeActivityRoot.GetComponentsInChildren<PassengerSeatPrototype>(true);
            int spotIndex = 0;
            for (int seatIndex = 0; seatIndex < seats.Length; seatIndex++)
            {
                PassengerSeatPrototype seat = seats[seatIndex];
                for (int slot = 0; slot < seat.Capacity; slot++)
                {
                    Transform sittingPoint = seat.GetSittingPoint(slot);
                    if (sittingPoint == null)
                    {
                        continue;
                    }

                    spotIndex++;
                    Vector2 position = sittingPoint.position;
                    CreatePrototypeSpot(
                        $"Seat_FromPrototype_{spotIndex:00}",
                        PassengerAiV2InteriorSpotKind.Seat,
                        position,
                        1f,
                        CalculateExitCost(position, sourceDoorways),
                        parent);
                }
            }

            ImportedSeatPointCount = spotIndex;
        }

        private void RegisterPrototypeStandSpots(
            Transform parent,
            PassengerDoorway[] sourceDoorways)
        {
            PassengerActivityPoint[] activityPoints =
                prototypeActivityRoot.GetComponentsInChildren<PassengerActivityPoint>(true);
            int spotIndex = 0;
            for (int i = 0; i < activityPoints.Length; i++)
            {
                if (activityPoints[i].ActivityType != PassengerActivityType.Handhold)
                {
                    continue;
                }

                spotIndex++;
                Vector2 position = activityPoints[i].transform.position;
                CreatePrototypeSpot(
                    $"Stand_FromPrototype_{spotIndex:00}",
                    PassengerAiV2InteriorSpotKind.Stand,
                    position,
                    0.48f,
                    CalculateExitCost(position, sourceDoorways),
                    parent);
            }

            ImportedStandPointCount = spotIndex;
        }

        private void RegisterPrototypeLeanSpots(
            Transform parent,
            PassengerDoorway[] sourceDoorways)
        {
            int spotIndex = 0;
            for (int i = 0; i < sourceDoorways.Length; i++)
            {
                PassengerDoorway doorway = sourceDoorways[i];
                if (doorway == null || doorway.IsUsable || doorway.Door == null)
                {
                    continue;
                }

                Vector2 doorPosition = doorway.Door.transform.position;
                float side = Mathf.Sign(doorPosition.y - prototypeActivityRoot.position.y);
                Vector2 leanPosition = doorPosition - Vector2.up * side * 0.42f;
                spotIndex++;
                CreatePrototypeSpot(
                    $"Lean_ClosedDoor_FromPrototype_{spotIndex:00}",
                    PassengerAiV2InteriorSpotKind.Lean,
                    leanPosition,
                    0.76f,
                    CalculateExitCost(leanPosition, sourceDoorways),
                    parent);
            }

            ImportedLeanPointCount = spotIndex;
        }

        private void RegisterPrototypeDoorPrepareSpots(
            Transform parent,
            PassengerDoorway[] sourceDoorways)
        {
            int usableDoorCount = 0;
            for (int i = 0; i < sourceDoorways.Length; i++)
            {
                if (sourceDoorways[i] != null && sourceDoorways[i].IsUsable)
                {
                    usableDoorCount++;
                }
            }

            int spotIndex = 0;
            int spotsPerDoor = Mathf.Max(1, Mathf.CeilToInt(passengerCount / (float)Mathf.Max(1, usableDoorCount)));
            for (int i = 0; i < sourceDoorways.Length; i++)
            {
                PassengerDoorway doorway = sourceDoorways[i];
                if (doorway == null || !doorway.IsUsable || doorway.InsidePoint == null)
                {
                    continue;
                }

                Vector2 inside = doorway.InsidePoint.position;
                float inwardSign = -Mathf.Sign(
                    doorway.OutsidePoint.position.y - doorway.InsidePoint.position.y);
                for (int localIndex = 0; localIndex < spotsPerDoor; localIndex++)
                {
                    int column = localIndex % 5;
                    int row = localIndex / 5;
                    Vector2 position = inside
                        + Vector2.right * (column - 2) * 0.92f
                        + Vector2.up * inwardSign * row * 0.9f;
                    spotIndex++;
                    CreatePrototypeSpot(
                        $"DoorPrepare_FromPrototype_{spotIndex:00}",
                        PassengerAiV2InteriorSpotKind.DoorPrepare,
                        position,
                        0.55f,
                        0f,
                        parent);
                }
            }

            ImportedDoorPreparePointCount = spotIndex;
        }

        private void CreatePrototypeSpot(
            string spotName,
            PassengerAiV2InteriorSpotKind kind,
            Vector2 worldPosition,
            float comfort,
            float exitCost,
            Transform parent)
        {
            GameObject spotObject = new GameObject(spotName);
            spotObject.transform.SetParent(parent, false);
            spotObject.transform.position = worldPosition;
            PassengerAiV2InteriorSpotSmartObject spot =
                spotObject.AddComponent<PassengerAiV2InteriorSpotSmartObject>();
            spot.Configure(
                $"prototype_ai_{spotName.ToLowerInvariant()}",
                kind,
                worldPosition,
                comfort,
                exitCost,
                null);
            environment.RegisterSpot(spot);
        }

        private static float CalculateExitCost(
            Vector2 position,
            PassengerDoorway[] sourceDoorways)
        {
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < sourceDoorways.Length; i++)
            {
                PassengerDoorway doorway = sourceDoorways[i];
                if (doorway == null || !doorway.IsUsable || doorway.InsidePoint == null)
                {
                    continue;
                }

                nearestDistance = Mathf.Min(
                    nearestDistance,
                    Vector2.Distance(position, doorway.InsidePoint.position));
            }

            return float.IsPositiveInfinity(nearestDistance)
                ? 0.5f
                : Mathf.Clamp01(nearestDistance / 7f);
        }

        private void BuildLayout()
        {
            GameObject runtimeRoot = new GameObject("Runtime_TrainInteriorLayout");
            runtimeRoot.transform.SetParent(transform, false);

            CreateRect(
                "Background",
                Vector2.zero,
                new Vector2(30f, 19f),
                new Color(0.018f, 0.03f, 0.045f, 1f),
                -2,
                runtimeRoot.transform);
            CreateRect(
                "TrainFloor",
                Vector2.zero,
                new Vector2(27f, 15.6f),
                new Color(0.18f, 0.28f, 0.34f, 1f),
                0,
                runtimeRoot.transform);
            CreateRect(
                "Wall_Top",
                new Vector2(0f, 7.45f),
                new Vector2(27f, 0.45f),
                new Color(0.5f, 0.61f, 0.66f, 1f),
                4,
                runtimeRoot.transform);
            CreateRect(
                "Wall_Left",
                new Vector2(-13.25f, 0f),
                new Vector2(0.45f, 15.3f),
                new Color(0.5f, 0.61f, 0.66f, 1f),
                4,
                runtimeRoot.transform);
            CreateRect(
                "Wall_Right",
                new Vector2(13.25f, 0f),
                new Vector2(0.45f, 15.3f),
                new Color(0.5f, 0.61f, 0.66f, 1f),
                4,
                runtimeRoot.transform);
            CreateRect(
                "Wall_Bottom_Left",
                new Vector2(-8.2f, -7.45f),
                new Vector2(10.6f, 0.45f),
                new Color(0.5f, 0.61f, 0.66f, 1f),
                4,
                runtimeRoot.transform);
            CreateRect(
                "Wall_Bottom_Right",
                new Vector2(8.2f, -7.45f),
                new Vector2(10.6f, 0.45f),
                new Color(0.5f, 0.61f, 0.66f, 1f),
                4,
                runtimeRoot.transform);
            CreateRect(
                "DoorPanel_Left",
                new Vector2(-2.15f, -7.35f),
                new Vector2(2.1f, 0.65f),
                new Color(0.18f, 0.62f, 0.78f, 1f),
                6,
                runtimeRoot.transform);
            CreateRect(
                "DoorPanel_Right",
                new Vector2(2.15f, -7.35f),
                new Vector2(2.1f, 0.65f),
                new Color(0.18f, 0.62f, 0.78f, 1f),
                6,
                runtimeRoot.transform);

            GameObject managerObject = new GameObject("CrowdManager_SpatialHash");
            managerObject.transform.SetParent(transform, false);
            crowdManager = managerObject.AddComponent<PassengerAiV2CrowdManager>();

            GameObject environmentObject = new GameObject("SmartObjectSet_TrainInterior");
            environmentObject.transform.SetParent(runtimeRoot.transform, false);
            environment = environmentObject.AddComponent<PassengerAiV2TrainInteriorEnvironment>();

            CreateActivitySpots(environmentObject.transform);
            CreateDoorPrepareSpots(environmentObject.transform);
        }

        private void CreateActivitySpots(Transform parent)
        {
            float[] seatX = { -7.5f, -2.5f, 2.5f, 7.5f };
            for (int i = 0; i < seatX.Length; i++)
            {
                CreateSpot(
                    $"Seat_{i + 1:00}",
                    PassengerAiV2InteriorSpotKind.Seat,
                    new Vector2(seatX[i], 5.75f),
                    new Vector2(1.8f, 0.72f),
                    new Color(0.12f, 0.48f, 0.68f, 1f),
                    1f,
                    0.82f,
                    parent);
            }

            float[] standX = { -6f, -2f, 2f, 6f };
            for (int i = 0; i < standX.Length; i++)
            {
                CreateSpot(
                    $"Stand_{i + 1:00}",
                    PassengerAiV2InteriorSpotKind.Stand,
                    new Vector2(standX[i], 1.35f),
                    new Vector2(0.62f, 0.62f),
                    new Color(0.82f, 0.7f, 0.22f, 0.86f),
                    0.48f,
                    0.4f,
                    parent);
            }

            CreateSpot(
                "Lean_01",
                PassengerAiV2InteriorSpotKind.Lean,
                new Vector2(-11.8f, 3.2f),
                new Vector2(0.58f, 1.55f),
                new Color(0.58f, 0.34f, 0.72f, 0.9f),
                0.74f,
                0.62f,
                parent);
            CreateSpot(
                "Lean_02",
                PassengerAiV2InteriorSpotKind.Lean,
                new Vector2(11.8f, 3.2f),
                new Vector2(0.58f, 1.55f),
                new Color(0.58f, 0.34f, 0.72f, 0.9f),
                0.74f,
                0.62f,
                parent);
        }

        private void CreateDoorPrepareSpots(Transform parent)
        {
            float[] xPositions = { -4.5f, -1.5f, 1.5f, 4.5f };
            for (int row = 0; row < 2; row++)
            {
                for (int column = 0; column < xPositions.Length; column++)
                {
                    CreateSpot(
                        $"DoorPrepare_{row + 1}_{column + 1}",
                        PassengerAiV2InteriorSpotKind.DoorPrepare,
                        new Vector2(xPositions[column] + row * 0.25f, -4.45f - row * 1.3f),
                        new Vector2(0.58f, 0.1f),
                        new Color(1f, 0.55f, 0.2f, 0.9f),
                        0.55f,
                        0f,
                        parent);
                }
            }
        }

        private void CreateSpot(
            string spotId,
            PassengerAiV2InteriorSpotKind kind,
            Vector2 position,
            Vector2 size,
            Color color,
            float comfort,
            float exitCost,
            Transform parent)
        {
            GameObject spotObject = CreateRect(
                spotId,
                position,
                size,
                color,
                3,
                parent);
            PassengerAiV2InteriorSpotSmartObject spot =
                spotObject.AddComponent<PassengerAiV2InteriorSpotSmartObject>();
            spot.Configure(
                $"train_interior_{spotId.ToLowerInvariant()}",
                kind,
                position,
                comfort,
                exitCost,
                spotObject.GetComponent<SpriteRenderer>());
            environment.RegisterSpot(spot);
        }

        private void SpawnPassengers()
        {
            int count = Mathf.Clamp(passengerCount, 4, 60);
            for (int i = 0; i < count; i++)
            {
                Vector2 start;
                if (UsesPrototypeAiMap)
                {
                    const int spawnColumns = 12;
                    int column = i % spawnColumns;
                    int row = i / spawnColumns;
                    start = new Vector2(
                        Mathf.Lerp(
                            worldBounds.xMin + 1.1f,
                            worldBounds.xMax - 1.1f,
                            column / (float)(spawnColumns - 1)),
                        Mathf.Lerp(
                            worldBounds.yMin + 1.05f,
                            worldBounds.yMax - 1.05f,
                            row / 4f));
                }
                else
                {
                    float normalized = count <= 1 ? 0.5f : i / (float)(count - 1);
                    start = new Vector2(
                        Mathf.Lerp(-8.4f, 8.4f, normalized),
                        -2.2f - (i % 2) * 0.42f);
                }
                PassengerAiV2RuntimePersonality personality = baselineProfile != null
                    ? baselineProfile.CreateRuntimePersonality(9000 + i)
                    : PassengerAiV2RuntimePersonality.CreateFallback(9000 + i);

                GameObject passengerObject = new GameObject(
                    $"Passenger_Interior_{i + 1:00}__{personality.ProfileId}");
                passengerObject.transform.SetParent(transform, false);
                passengerObject.AddComponent<SpriteRenderer>();
                passengerObject.AddComponent<CircleCollider2D>();
                passengerObject.AddComponent<Rigidbody2D>();
                PassengerAiV2Agent agent = passengerObject.AddComponent<PassengerAiV2Agent>();
                agent.Initialize(
                    crowdManager,
                    start,
                    start,
                    personality,
                    (i + 0.35f) / count,
                    worldBounds.yMin,
                    worldBounds.yMax,
                    sharedSprite,
                    new Color(0.22f, 0.83f, 0.76f, 1f));
                if (UsesPrototypeAiMap)
                {
                    agent.transform.localScale *= 0.85f;
                }

                agent.SetMovementBounds(worldBounds);

                PassengerAiV2TrainInteriorAction action =
                    passengerObject.AddComponent<PassengerAiV2TrainInteriorAction>();
                action.Initialize(
                    agent,
                    environment,
                    personality,
                    start,
                    worldBounds,
                    (i + 0.35f) / count);
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
            texture.name = "AI_V2_TrainInterior_RuntimeWhiteTexture";
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        private void BuildStyles()
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.94f, 0.97f, 1f, 1f) }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.84f, 0.89f, 0.92f, 1f) }
            };
        }
    }
}
