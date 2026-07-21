using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class GeneralPassengerPrototype : MonoBehaviour
    {
        private static Mesh passengerCircleMesh;

        private enum PassengerState
        {
            WaitingOutside,
            Boarding,
            ChoosingBehavior,
            MovingToActivity,
            Observing,
            Yielding,
            PreparingToExit,
            WaitingAtExitDoor,
            Exiting,
            LeavingPlatform
        }

        private enum PassengerBehavior
        {
            None,
            Seated,
            Handhold,
            Leaning,
            DoorStanding,
            AisleStanding,
            Exiting
        }

        private struct ObservedSeat
        {
            public PassengerSeatPrototype Seat;
            public Transform SittingPoint;

            public ObservedSeat(PassengerSeatPrototype seat, Transform sittingPoint)
            {
                Seat = seat;
                SittingPoint = sittingPoint;
            }
        }

        [SerializeField] private Transform mapRoot;
        [SerializeField] private PassengerDoorway boardingDoorway;
        [SerializeField] private bool randomizeBoardingDoorway = true;
        [SerializeField] private PassengerDoorway[] doorways;
        [SerializeField] private TrainDoorCyclePrototype doorCycle;
        [SerializeField] private GridNavigation2D[] navigationAreas;
        [SerializeField] private PassengerSeatPrototype[] seats;
        [SerializeField] private PassengerActivityPoint[] activityPoints;
        [SerializeField] private TextMesh stateLabel;
        [SerializeField, Min(0.1f)] private float moveSpeed = 2.2f;
        [SerializeField, Min(1f)] private float minimumDecisionInterval = 6f;
        [SerializeField, Min(1f)] private float maximumDecisionInterval = 10f;
        [SerializeField, Min(1f)] private float exitPreparationLeadTime = 7f;
        [SerializeField, Range(0f, 1f)] private float seatRecheckChance = 0.08f;
        [SerializeField, Range(0f, 1f)] private float initialSeatPursuitChance = 0.68f;
        [SerializeField, Min(0.1f)] private float seatApproachCrowdRadius = 1.35f;
        [SerializeField, Min(0.1f)] private float seatApproachCrowdLimit = 3.4f;
        [SerializeField, Min(0.1f)] private float seatCrowdRecheckInterval = 0.4f;
        [SerializeField, Min(0.1f)] private float avoidanceLookAhead = 0.8f;
        [SerializeField, Range(0.1f, 0.9f)] private float avoidanceLateralStrength = 0.45f;
        [SerializeField, Min(0.05f)] private float avoidanceSideHoldDuration = 0.35f;
        [SerializeField, Min(0.05f)] private float movementIntentMemory = 0.3f;
        [SerializeField, Range(0f, 1f)] private float leisurelyPassengerChance = 0.25f;
        [SerializeField, Range(0f, 1f)] private float hurriedPassengerChance = 0.2f;
        [SerializeField, Min(0.1f)] private float exitWaitingAcceptanceRadius = 0.4f;
        [SerializeField, Min(0.1f)] private float waitingPersonalSpaceRadius = 0.9f;
        [SerializeField, Min(0.1f)] private float waitingSeparationSpeed = 0.8f;
        [SerializeField, Min(0.1f)] private float maximumWaitingDrift = 0.65f;
        [SerializeField, Min(0.1f)] private float blockedActivityTimeout = 2.2f;
        [SerializeField, Min(0.05f)] private float blockedWaitDuration = 0.35f;
        [SerializeField, Min(0.1f)] private float blockedSqueezeMinimumDuration = 1f;
        [SerializeField, Min(0.1f)] private float blockedSqueezeMaximumDuration = 1.5f;
        [SerializeField, Range(0f, 1f)] private float squeezeThroughChance = 0.25f;
        [SerializeField, Range(0.1f, 0.8f)] private float squeezeSpeedMultiplier = 0.4f;
        [SerializeField, Min(0.1f)] private float softOverlapRadius = 0.65f;
        [SerializeField, Min(0.1f)] private float stationaryRouteClearance = 0.82f;
        [SerializeField, Min(0.1f)] private float courtesyStepDistance = 0.65f;
        [SerializeField, Min(0.01f)] private float courtesyRouteClearanceGain = 0.12f;
        [SerializeField, Min(0.1f)] private float exitIntentCorridorWidth = 0.78f;
        [SerializeField, Min(0.1f)] private float standingPersonalSpaceRadius = 0.62f;
        [SerializeField, Min(0.1f)] private float standingYieldDetectionRadius = 1.45f;
        [SerializeField, Min(0.1f)] private float obstructionYieldThreshold = 0.9f;
        [SerializeField, Min(0.01f)] private float obstructionScoreDecay = 0.35f;
        [SerializeField, Min(0.01f)] private float arrivalDistance = 0.08f;
        [SerializeField, Min(0.01f)] private float activityClaimLeadTime = 0.1f;
        [SerializeField, Min(0.1f)] private float activityClaimRadius = 0.42f;
        [SerializeField, Min(0.1f)] private float crowdEscapeThreshold = 7.5f;
        [SerializeField, Min(0.1f)] private float crowdEscapeMinimumImprovement = 1.2f;
        [SerializeField, Min(0.1f)] private float crowdEscapeCheckInterval = 2f;
        [SerializeField, Min(0.1f)] private float crowdEscapeCooldown = 12f;
        [SerializeField, Range(0f, 1f)] private float crowdEscapeChance = 0.08f;
        [SerializeField, Min(0.1f)] private float wallLeanReleaseDistance = 0.72f;
        [SerializeField, Min(0.1f)] private float nearbyHandholdDistance = 0.75f;
        [SerializeField, Min(0f)] private float settledComfortGap = 0.22f;
        [SerializeField, Min(0.1f)] private float crowdPressureRadius = 1.25f;
        [SerializeField, Min(0.01f)] private float crowdPressureSpeed = 0.34f;
        [SerializeField, Min(0.1f)] private float minimumBodySeparation = 0.46f;
        [SerializeField] private PassengerState state;
        [SerializeField] private PassengerBehavior behavior;

        private readonly List<Vector3> path = new List<Vector3>();
        private readonly List<string> decisionHistory = new List<string>();
        private readonly List<ObservedSeat> observedSeats = new List<ObservedSeat>();
        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private PassengerSeatPrototype reservedSeat;
        private Transform reservedSittingPoint;
        private PassengerActivityPoint reservedActivityPoint;
        private PassengerDoorway exitDoorway;
        private PassengerDoorway exitProgressDoorway;
        private PassengerIntentCoordinator intentCoordinator;
        private GeneralPassengerPrototype[] passengerPeers;
        private PlayerBoardingCyclePrototype playerPeer;
        private Collider2D[] wallColliders;
        private Vector2 activityTarget;
        private Vector2 boardingClearanceTarget;
        private Vector2 exitWaitingTarget;
        private Vector2 pathTarget;
        private int pathIndex;
        private int boardingStopNumber;
        private int plannedExitStopNumber;
        private int boardingOrder = int.MaxValue;
        private float nextDecisionTime;
        private float nextRepathTime;
        private float avoidanceSide;
        private float avoidanceSideUntil;
        private float movementIntentUntil;
        private float blockedByPassengerSince = -1f;
        private float blockedSqueezeUntil;
        private float blockedGiveUpAt;
        private float courtesy;
        private float courtesyWaitUntil;
        private float courtesyWaitCooldownUntil;
        private float nextIntentBroadcastTime;
        private float nextSeatIntentBroadcastTime;
        private float nextSeatCrowdCheckTime;
        private float nextStandingYieldCheckTime;
        private float standingYieldCooldownUntil;
        private float courtesyYieldCanReturnAt;
        private float courtesyYieldUntil;
        private float personalPaceMultiplier = 1f;
        private float exitPaceMultiplier = 1f;
        private float obstructionScore;
        private float lastObstructionReportTime;
        private float obstructionYieldCooldownUntil;
        private float avoidAbandonedTargetUntil;
        private float crowdTolerance = 1f;
        private float nextCrowdEscapeCheckTime;
        private float crowdEscapeCooldownUntil;
        private float lastActivityProgressTime;
        private float lastActivityProgressDistance = float.MaxValue;
        private float lastExitProgressTime;
        private float lastExitProgressDistance = float.MaxValue;
        private Vector2 movementIntent;
        private Vector2 viewDirection = Vector2.up;
        private Vector2 courtesyYieldOrigin;
        private Vector2 courtesyYieldTarget;
        private Vector2 platformExitTarget;
        private Vector2 blockedDetourTarget;
        private Vector2 obstructionDirection;
        private Vector2 abandonedActivityTarget;
        private Vector2 activityProgressTarget;
        private GeneralPassengerPrototype courtesyYieldSource;
        private GeneralPassengerPrototype courtesyWaitPassenger;
        private GeneralPassengerPrototype blockingPassenger;
        private GeneralPassengerPrototype obstructionSource;
        private bool hasPathTarget;
        private bool hasBoardingReservation;
        private bool reachedBoardingDoorCenter;
        private bool reachedBoardingInside;
        private bool initialPlacement;
        private bool hasExitReservation;
        private bool squeezeThroughPassengers;
        private bool reachedCourtesyYieldTarget;
        private bool blockedDetourAttempted;
        private bool hasBlockedDetourTarget;
        private int avoidanceAdjustmentCount;
        private int blockedPushCount;
        private int abandonedActivityCount;
        private int squeezeStepCount;
        private int blockedDetourCount;
        private int obstructionYieldCount;
        private int seatCrowdSwitchCount;

        public string CurrentState => state.ToString();
        public string CurrentBehavior => behavior.ToString();
        public string DecisionHistory => string.Join(" > ", decisionHistory);
        public PassengerDoorway BoardingDoorway => boardingDoorway;
        public PassengerDoorway ExitDoorway => exitDoorway;
        public int AvoidanceAdjustmentCount => avoidanceAdjustmentCount;
        public int BlockedPushCount => blockedPushCount;
        public int AbandonedActivityCount => abandonedActivityCount;
        public int SqueezeStepCount => squeezeStepCount;
        public int BlockedDetourCount => blockedDetourCount;
        public int ObstructionYieldCount => obstructionYieldCount;
        public int SeatCrowdSwitchCount => seatCrowdSwitchCount;
        public float ObstructionScore => obstructionScore;
        public float CurrentMoveSpeed => GetCurrentMoveSpeed();
        public float Courtesy => courtesy;
        public Vector2 ViewDirection => viewDirection;
        public int ObservedSeatCount => observedSeats.Count;
        public bool IsSeekingSeat => state == PassengerState.MovingToActivity &&
                                     behavior == PassengerBehavior.Seated;

        public void Configure(
            PassengerDoorway initialBoardingDoorway,
            TrainDoorCyclePrototype cycle,
            Transform passengerMapRoot,
            bool useRandomBoardingDoorway,
            TextMesh label)
        {
            boardingDoorway = initialBoardingDoorway;
            doorCycle = cycle;
            mapRoot = passengerMapRoot;
            randomizeBoardingDoorway = useRandomBoardingDoorway;
            stateLabel = label;
        }

        private void Start()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            ConfigureRoundPassengerBody();
            body.linearDamping = 8f;
            squeezeThroughPassengers = Random.value < squeezeThroughChance;
            courtesy = Random.Range(0.15f, 0.95f);
            crowdTolerance = Random.Range(0.82f, 1.25f);
            AssignMovementPace();
            IgnoreHardPassengerCollisions();
            RefreshMapReferences();
            RegisterWithIntentCoordinator();

            if (boardingDoorway == null || !boardingDoorway.IsUsable)
            {
                enabled = false;
                Debug.LogError("[Passenger AI] No usable PassengerDoorway was found.", this);
                return;
            }

            Vector2 initialLookDirection =
                (Vector2)boardingDoorway.InsidePoint.position -
                (Vector2)boardingDoorway.OutsidePoint.position;
            if (initialLookDirection.sqrMagnitude > 0.001f)
            {
                viewDirection = initialLookDirection.normalized;
            }

            SetPosition(boardingDoorway.GetBoardingQueuePosition(gameObject));
            SetState(PassengerState.WaitingOutside, PassengerBehavior.None);
        }

        private void ConfigureRoundPassengerBody()
        {
            transform.localScale = new Vector3(0.62f, 0.62f, 1f);

            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                meshFilter.sharedMesh = GetPassengerCircleMesh();
            }

            CircleCollider2D circleCollider = GetComponent<CircleCollider2D>();
            if (circleCollider == null)
            {
                circleCollider = gameObject.AddComponent<CircleCollider2D>();
            }

            circleCollider.radius = 0.46f;
            circleCollider.offset = Vector2.zero;
            circleCollider.isTrigger = false;
            circleCollider.enabled = true;

            Collider2D[] colliders = GetComponents<Collider2D>();
            foreach (Collider2D collider in colliders)
            {
                if (collider != circleCollider)
                {
                    collider.enabled = false;
                }
            }

            bodyCollider = circleCollider;
        }

        private static Mesh GetPassengerCircleMesh()
        {
            if (passengerCircleMesh != null)
            {
                return passengerCircleMesh;
            }

            const int segmentCount = 32;
            var vertices = new Vector3[segmentCount + 1];
            var triangles = new int[segmentCount * 3];
            vertices[0] = Vector3.zero;

            for (int i = 0; i < segmentCount; i++)
            {
                float angle = i / (float)segmentCount * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(
                    Mathf.Cos(angle) * 0.5f,
                    Mathf.Sin(angle) * 0.5f,
                    0f);

                int triangleIndex = i * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = i == segmentCount - 1 ? 1 : i + 2;
                triangles[triangleIndex + 2] = i + 1;
            }

            passengerCircleMesh = new Mesh { name = "Passenger Circle Mesh" };
            passengerCircleMesh.vertices = vertices;
            passengerCircleMesh.triangles = triangles;
            passengerCircleMesh.RecalculateNormals();
            passengerCircleMesh.RecalculateBounds();
            return passengerCircleMesh;
        }

        private void LateUpdate()
        {
            if (state == PassengerState.Observing &&
                behavior == PassengerBehavior.Leaning)
            {
                FaceAwayFromNearestWall();
            }
        }

        private void FixedUpdate()
        {
            StopResidualMotion();
            UpdateObstructionPressure();

            if (boardingDoorway == null || doorCycle == null)
            {
                return;
            }

            switch (state)
            {
                case PassengerState.WaitingOutside:
                    Vector2 queuePosition = boardingDoorway.GetBoardingQueuePosition(gameObject);
                    bool isAtQueuePosition = MoveWithoutAvoidance(queuePosition);
                    if (isAtQueuePosition && boardingDoorway.TryBeginBoarding(gameObject))
                    {
                        boardingOrder = boardingDoorway.GetBoardingOrder(gameObject);
                        boardingStopNumber = doorCycle.StopNumber;
                        plannedExitStopNumber = boardingStopNumber + Random.Range(1, 4);
                        reachedBoardingDoorCenter = false;
                        reachedBoardingInside = false;
                        initialPlacement = false;
                        SetState(PassengerState.Boarding, PassengerBehavior.None);
                    }
                    break;

                case PassengerState.Boarding:
                    if (!reachedBoardingDoorCenter)
                    {
                        reachedBoardingDoorCenter = MoveWithoutAvoidance(
                            boardingDoorway.GetBoardingEntryPosition(gameObject));
                    }
                    else if (!reachedBoardingInside)
                    {
                        reachedBoardingInside = MoveWithoutAvoidance(
                            boardingDoorway.GetBoardingInsidePosition(gameObject));
                        reachedBoardingInside |=
                            boardingDoorway.HasEnteredTrain(body.position);
                        if (reachedBoardingInside)
                        {
                            boardingClearanceTarget =
                                boardingDoorway.GetBoardingClearancePosition(gameObject);
                            ClearPath();
                        }
                    }
                    else
                    {
                        bool reachedClearance = MoveUsingPath(
                            boardingClearanceTarget,
                            true);
                        if (reachedClearance ||
                            Vector2.Distance(body.position, boardingClearanceTarget) <= 0.38f)
                        {
                            ReleaseBoardingReservation();
                            initialPlacement = true;
                            ClearPath();
                            SetState(PassengerState.ChoosingBehavior, PassengerBehavior.None);
                        }
                    }
                    break;

                case PassengerState.ChoosingBehavior:
                    if (ShouldPrepareToExit())
                    {
                        BeginExitPreparation();
                    }
                    else if (!initialPlacement && TryBeginCrowdEscape())
                    {
                        break;
                    }
                    else
                    {
                        ChooseKnownSeat();
                    }
                    break;

                case PassengerState.MovingToActivity:
                    if (ShouldPrepareToExit())
                    {
                        BeginExitPreparation();
                    }
                    else if (!initialPlacement && TryBeginCrowdEscape())
                    {
                        break;
                    }
                    else if (TryRerouteFromCrowdedSeat())
                    {
                        break;
                    }
                    else if (!initialPlacement && ShouldGiveUpContestedActivity())
                    {
                        ReconsiderContestedActivity();
                    }
                    else
                    {
                        BroadcastSeatIntent();
                        if (HasActivityMoveStalled())
                        {
                            AbandonBlockedActivity();
                        }
                        else if (MoveUsingPath(activityTarget))
                        {
                            FinishActivityMove();
                        }
                        else if (!initialPlacement && ShouldAbandonBlockedActivity())
                        {
                            AbandonBlockedActivity();
                        }
                    }
                    break;

                case PassengerState.Observing:
                    if (ShouldPrepareToExit())
                    {
                        BeginExitPreparation();
                    }
                    else if (TryLeaveActiveBoardingFlow())
                    {
                        break;
                    }
                    else if (ShouldHoldWallPositionDuringBoarding())
                    {
                        break;
                    }
                    else if (TryApplyCrowdPressure())
                    {
                        break;
                    }
                    else if (TryBeginCrowdEscape())
                    {
                        break;
                    }
                    else if (TryYieldFromObstructionPressure())
                    {
                        break;
                    }
                    else if (TryYieldFromDoorwayTraffic())
                    {
                        break;
                    }
                    else if (Time.time >= nextDecisionTime)
                    {
                        EvaluateReasonedBehaviorChange();
                    }
                    break;

                case PassengerState.Yielding:
                    UpdateCourtesyYield();
                    break;

                case PassengerState.PreparingToExit:
                    BroadcastExitIntent();
                    if (exitDoorway != null &&
                        (IsInsideExitWaitingArea() || MoveUsingPath(exitWaitingTarget)))
                    {
                        SetState(PassengerState.WaitingAtExitDoor, PassengerBehavior.Exiting);
                    }
                    break;

                case PassengerState.WaitingAtExitDoor:
                    MaintainPersonalSpaceNearExit();
                    if (exitDoorway != null &&
                        exitDoorway.Door.IsOpen &&
                        doorCycle.StopNumber > boardingStopNumber)
                    {
                        ResetExitProgressTracking();
                        SetState(PassengerState.Exiting, PassengerBehavior.Exiting);
                    }
                    break;

                case PassengerState.Exiting:
                    if (exitDoorway != null)
                    {
                        if (HasExitMovementStalled())
                        {
                            if (!TrySwitchToAlternativeExitDoorway())
                            {
                                ResetExitProgressTracking();
                            }

                            break;
                        }

                        bool reachedOutside = MoveDirectly(exitDoorway.OutsidePoint.position);
                        if (reachedOutside || exitDoorway.HasLeftTrain(body.position))
                        {
                            ReleaseExitReservation();
                            Destroy(gameObject);
                        }
                    }
                    break;

                case PassengerState.LeavingPlatform:
                    if (MoveWithoutAvoidance(platformExitTarget))
                    {
                        Destroy(gameObject);
                    }
                    break;
            }
        }

        private void RefreshMapReferences()
        {
            if (mapRoot == null)
            {
                mapRoot = transform.parent;
            }

            doorways = FindMapComponents<PassengerDoorway>();
            navigationAreas = FindMapComponents<GridNavigation2D>();
            seats = FindMapComponents<PassengerSeatPrototype>();
            activityPoints = FindMapComponents<PassengerActivityPoint>();
            passengerPeers = FindMapComponents<GeneralPassengerPrototype>();
            PlayerBoardingCyclePrototype[] players =
                FindSceneComponents<PlayerBoardingCyclePrototype>();
            playerPeer = players.Length > 0 ? players[0] : null;

            var walls = new List<Collider2D>();
            foreach (NavigationObstacle obstacle in FindMapComponents<NavigationObstacle>())
            {
                if (obstacle == null || !obstacle.gameObject.name.Contains("Wall"))
                {
                    continue;
                }

                Collider2D wallCollider = obstacle.GetComponent<Collider2D>();
                if (wallCollider != null)
                {
                    walls.Add(wallCollider);
                }
            }

            wallColliders = walls.ToArray();

            if (doorCycle == null)
            {
                TrainDoorCyclePrototype[] cycles = FindSceneComponents<TrainDoorCyclePrototype>();
                if (cycles.Length > 0)
                {
                    doorCycle = cycles[0];
                }
            }

            bool hasConfiguredDoorway = boardingDoorway != null &&
                                        boardingDoorway.IsUsable &&
                                        ContainsDoorway(boardingDoorway);

            if (!randomizeBoardingDoorway &&
                hasConfiguredDoorway &&
                boardingDoorway.TryReserveBoarding(gameObject))
            {
                hasBoardingReservation = true;
                return;
            }

            boardingDoorway = ReserveRandomBoardingDoorway();
        }

        private void RegisterWithIntentCoordinator()
        {
            if (mapRoot == null)
            {
                return;
            }

            intentCoordinator = mapRoot.GetComponent<PassengerIntentCoordinator>();
            if (intentCoordinator == null)
            {
                intentCoordinator = mapRoot.gameObject.AddComponent<PassengerIntentCoordinator>();
            }

            intentCoordinator.Register(this);
        }

        private PassengerDoorway ReserveRandomBoardingDoorway()
        {
            var candidates = new List<PassengerDoorway>();
            int smallestQueue = int.MaxValue;

            if (doorways != null)
            {
                foreach (PassengerDoorway doorway in doorways)
                {
                    if (doorway != null && doorway.IsUsable)
                    {
                        int queueCount = doorway.BoardingReservationCount;
                        if (queueCount < smallestQueue)
                        {
                            candidates.Clear();
                            smallestQueue = queueCount;
                        }

                        if (queueCount == smallestQueue)
                        {
                            candidates.Add(doorway);
                        }
                    }
                }
            }

            while (candidates.Count > 0)
            {
                int index = Random.Range(0, candidates.Count);
                PassengerDoorway candidate = candidates[index];
                candidates.RemoveAt(index);

                if (!candidate.TryReserveBoarding(gameObject))
                {
                    continue;
                }

                hasBoardingReservation = true;
                return candidate;
            }

            hasBoardingReservation = false;
            return FindNearestUsableDoorway(transform.position);
        }

        private void ReleaseBoardingReservation()
        {
            if (!hasBoardingReservation || boardingDoorway == null)
            {
                return;
            }

            boardingDoorway.ReleaseBoarding(gameObject);
            hasBoardingReservation = false;
        }

        private void OnDestroy()
        {
            ReleaseBoardingReservation();
            ReleaseExitReservation();
            intentCoordinator?.Unregister(this);
        }

        private bool ContainsDoorway(PassengerDoorway target)
        {
            if (doorways == null)
            {
                return false;
            }

            foreach (PassengerDoorway doorway in doorways)
            {
                if (doorway == target)
                {
                    return true;
                }
            }

            return false;
        }

        private T[] FindMapComponents<T>() where T : Component
        {
            if (mapRoot != null)
            {
                return mapRoot.GetComponentsInChildren<T>(true);
            }

            return FindSceneComponents<T>();
        }

        private T[] FindSceneComponents<T>() where T : Component
        {
            T[] found = FindObjectsByType<T>(FindObjectsSortMode.None);
            var sameScene = new List<T>(found.Length);

            foreach (T component in found)
            {
                if (component != null && component.gameObject.scene == gameObject.scene)
                {
                    sameScene.Add(component);
                }
            }

            return sameScene.ToArray();
        }

        private PassengerDoorway FindNearestUsableDoorway(Vector2 position)
        {
            PassengerDoorway nearest = null;
            float nearestDistance = float.MaxValue;

            if (doorways == null)
            {
                return null;
            }

            foreach (PassengerDoorway doorway in doorways)
            {
                if (doorway == null || !doorway.IsUsable)
                {
                    continue;
                }

                float distance = Vector2.SqrMagnitude(
                    (Vector2)doorway.OutsidePoint.position - position);
                if (distance < nearestDistance)
                {
                    nearest = doorway;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private void ChooseKnownSeat()
        {
            if (ShouldPrepareToExit())
            {
                observedSeats.Clear();
                BeginExitPreparation();
                return;
            }

            observedSeats.Clear();
            CollectAvailableSeats();
            bool sawAvailableSeat = observedSeats.Count > 0;
            bool pursuesSeat = !initialPlacement ||
                                Random.value <= GetInitialSeatPursuitChance();
            if (pursuesSeat &&
                observedSeats.Count > 0 &&
                TryReserveObservedSeat())
            {
                if (reservedActivityPoint != null)
                {
                    reservedActivityPoint.Release(gameObject);
                    reservedActivityPoint = null;
                }

                behavior = PassengerBehavior.Seated;
                RecordDecision("Nearest known seat -> Seated");
                ClearPath();
                state = PassengerState.MovingToActivity;
                UpdateLabel();
                return;
            }

            observedSeats.Clear();
            if (sawAvailableSeat && !pursuesSeat)
            {
                RecordDecision("Seat available -> Prefer open space");
            }
            if (behavior != PassengerBehavior.None &&
                behavior != PassengerBehavior.Exiting)
            {
                RecordDecision("No seat -> Stay " + behavior);
                ScheduleNextDecision();
                state = PassengerState.Observing;
                UpdateLabel();
                return;
            }

            ChooseBehavior();
        }

        private void CollectAvailableSeats()
        {
            if (seats == null)
            {
                return;
            }

            foreach (PassengerSeatPrototype seat in seats)
            {
                if (seat == null)
                {
                    continue;
                }

                for (int i = 0; i < seat.Capacity; i++)
                {
                    Transform sittingPoint = seat.GetSittingPoint(i);
                    if (sittingPoint == null ||
                        !seat.IsAvailable(sittingPoint) ||
                        HasObservedSeat(sittingPoint))
                    {
                        continue;
                    }

                    observedSeats.Add(new ObservedSeat(seat, sittingPoint));
                }
            }
        }

        private float GetInitialSeatPursuitChance()
        {
            float chance = initialSeatPursuitChance;
            if (boardingOrder <= 7)
            {
                chance += 0.18f;
            }

            float nearbyCrowdPenalty = Mathf.Clamp01(
                CalculateCrowdScore(body.position) / 12f) * 0.16f;
            return Mathf.Clamp01(chance - nearbyCrowdPenalty);
        }

        private bool HasObservedSeat(Transform sittingPoint)
        {
            foreach (ObservedSeat observedSeat in observedSeats)
            {
                if (observedSeat.SittingPoint == sittingPoint)
                {
                    return true;
                }
            }

            return false;
        }

        private void ChooseBehavior()
        {
            ReleaseCurrentActivity();

            if (initialPlacement && TryChooseInitialStandingBehavior())
            {
                ClearPath();
                state = PassengerState.MovingToActivity;
                RecordDecision("Initial standing choice -> " + behavior);
                UpdateLabel();
                return;
            }

            if (!TryChooseLeastCrowdedStandingPosition(out PassengerBehavior nextBehavior))
            {
                behavior = PassengerBehavior.AisleStanding;
                activityTarget = body.position;
                initialPlacement = false;
                RecordDecision("Stay in current open space");
                ScheduleNextDecision();
                SetState(PassengerState.Observing, behavior);
                return;
            }

            behavior = nextBehavior;
            RecordDecision(nextBehavior.ToString());
            ClearPath();
            state = PassengerState.MovingToActivity;
            UpdateLabel();
        }

        private bool TryChooseInitialStandingBehavior()
        {
            bool earlyStandingPassenger = boardingOrder <= 7;
            if (earlyStandingPassenger && TryChooseWallLeanPosition(true))
            {
                return true;
            }

            bool preferHandhold = Random.value < 0.5f;
            if (preferHandhold)
            {
                return TryReserveActivityPoint(PassengerBehavior.Handhold) ||
                       TryChooseWallLeanPosition(false);
            }

            return TryChooseWallLeanPosition(false) ||
                   TryReserveActivityPoint(PassengerBehavior.Handhold);
        }

        private bool HasActivityMoveStalled()
        {
            float distance = Vector2.Distance(body.position, activityTarget);
            bool newTarget = Vector2.Distance(activityProgressTarget, activityTarget) > 0.05f;
            if (newTarget || lastActivityProgressDistance == float.MaxValue)
            {
                activityProgressTarget = activityTarget;
                lastActivityProgressDistance = distance;
                lastActivityProgressTime = Time.time;
                return false;
            }

            if (distance < lastActivityProgressDistance - 0.12f)
            {
                lastActivityProgressDistance = distance;
                lastActivityProgressTime = Time.time;
                return false;
            }

            float stallTimeout = initialPlacement && boardingOrder <= 7
                ? 6.5f
                : 3.5f;
            return Time.time - lastActivityProgressTime >= stallTimeout;
        }

        private void ResetActivityProgressTracking()
        {
            activityProgressTarget = Vector2.zero;
            lastActivityProgressDistance = float.MaxValue;
            lastActivityProgressTime = Time.time;
        }

        private void ResetExitProgressTracking()
        {
            exitProgressDoorway = exitDoorway;
            lastExitProgressDistance = float.MaxValue;
            lastExitProgressTime = Time.time;
        }

        private bool HasExitMovementStalled()
        {
            if (exitDoorway == null)
            {
                return false;
            }

            float distance = Vector2.Distance(
                body.position,
                exitDoorway.OutsidePoint.position);
            if (exitProgressDoorway != exitDoorway ||
                lastExitProgressDistance == float.MaxValue)
            {
                exitProgressDoorway = exitDoorway;
                lastExitProgressDistance = distance;
                lastExitProgressTime = Time.time;
                return false;
            }

            if (distance < lastExitProgressDistance - 0.18f)
            {
                lastExitProgressDistance = distance;
                lastExitProgressTime = Time.time;
                return false;
            }

            return Time.time - lastExitProgressTime >= 3.5f;
        }

        private bool TrySwitchToAlternativeExitDoorway()
        {
            PassengerDoorway alternative = null;
            float bestScore = float.MaxValue;
            if (doorways != null)
            {
                foreach (PassengerDoorway doorway in doorways)
                {
                    if (doorway == null ||
                        doorway == exitDoorway ||
                        !doorway.IsUsable ||
                        !doorway.Door.IsOpen)
                    {
                        continue;
                    }

                    float score = Vector2.SqrMagnitude(
                                      (Vector2)doorway.InsidePoint.position - body.position) +
                                  doorway.ExitReservationCount * 0.8f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        alternative = doorway;
                    }
                }
            }

            if (alternative == null)
            {
                return false;
            }

            ReleaseExitReservation();
            exitDoorway = alternative;
            ReserveExitDoorway(exitDoorway);
            hasBlockedDetourTarget = false;
            blockedDetourAttempted = false;
            ClearPath();
            ResetPassengerBlock();
            ResetExitProgressTracking();
            SetState(PassengerState.PreparingToExit, PassengerBehavior.Exiting);
            BroadcastExitIntent(true);
            RecordDecision("Exit blocked -> Other door");
            return true;
        }

        private bool TryBeginCrowdEscape()
        {
            if (behavior == PassengerBehavior.Seated ||
                behavior == PassengerBehavior.Exiting ||
                Time.time < nextCrowdEscapeCheckTime ||
                Time.time < crowdEscapeCooldownUntil)
            {
                return false;
            }

            nextCrowdEscapeCheckTime = Time.time + crowdEscapeCheckInterval;
            if (Random.value > crowdEscapeChance)
            {
                return false;
            }

            GridNavigation2D navigation = FindCurrentNavigationArea();
            if (navigation == null)
            {
                return false;
            }

            float currentCrowdScore = CalculateCrowdScore(body.position);
            if (currentCrowdScore < crowdEscapeThreshold * crowdTolerance ||
                !TryFindQuietStandingPosition(
                    navigation,
                    out Vector2 quietPosition,
                    out float quietCrowdScore) ||
                currentCrowdScore - quietCrowdScore < crowdEscapeMinimumImprovement)
            {
                return false;
            }

            ReleaseCurrentActivity();
            ClearPath();
            ResetPassengerBlock();

            behavior = PassengerBehavior.AisleStanding;
            activityTarget = quietPosition;
            RecordDecision("Crowded -> Quiet open space");

            crowdEscapeCooldownUntil = Time.time + crowdEscapeCooldown;
            state = PassengerState.MovingToActivity;
            UpdateLabel();
            return true;
        }

        private bool TryLeaveActiveBoardingFlow()
        {
            if (!IsInsideActiveBoardingFlow(body.position) ||
                behavior == PassengerBehavior.Seated ||
                behavior == PassengerBehavior.Exiting)
            {
                return false;
            }

            if (behavior == PassengerBehavior.Leaning &&
                IsNearWall(body.position, wallLeanReleaseDistance) &&
                !IsInsideDirectBoardingLane(body.position))
            {
                return false;
            }

            GridNavigation2D navigation = FindCurrentNavigationArea();
            if (navigation == null ||
                !TryFindQuietStandingPosition(
                    navigation,
                    out Vector2 clearPosition,
                    out _))
            {
                return false;
            }

            ReleaseCurrentActivity();
            ClearPath();
            ResetPassengerBlock();
            behavior = PassengerBehavior.AisleStanding;
            activityTarget = clearPosition;
            state = PassengerState.MovingToActivity;
            RecordDecision("Incoming passengers -> Clear doorway");
            UpdateLabel();
            return true;
        }

        private bool ShouldHoldWallPositionDuringBoarding()
        {
            return behavior == PassengerBehavior.Leaning &&
                   IsNearWall(body.position, wallLeanReleaseDistance) &&
                   IsInsideActiveBoardingFlow(body.position) &&
                   !IsInsideDirectBoardingLane(body.position);
        }

        private bool IsInsideDirectBoardingLane(Vector2 position)
        {
            if (doorways == null)
            {
                return false;
            }

            foreach (PassengerDoorway doorway in doorways)
            {
                if (doorway != null &&
                    doorway.IsUsable &&
                    doorway.IsInsideDirectBoardingLane(position))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryFindQuietStandingPosition(
            GridNavigation2D navigation,
            out Vector2 selectedPosition,
            out float selectedCrowdScore)
        {
            const int maximumAttempts = 48;
            selectedPosition = body.position;
            selectedCrowdScore = float.MaxValue;
            float bestSelectionScore = float.MaxValue;
            bool found = false;

            for (int attempt = 0; attempt < maximumAttempts; attempt++)
            {
                Vector2 candidate = GetRandomOpenStandingPosition(navigation);
                if (!navigation.IsWorldWalkable(candidate) ||
                    !IsStandingPositionAvailable(candidate))
                {
                    continue;
                }

                float crowdScore = CalculateCrowdScore(candidate);
                float selectionScore = crowdScore * 4f +
                                       Vector2.Distance(body.position, candidate) * 0.06f +
                                       Random.Range(0f, 0.1f);
                if (selectionScore >= bestSelectionScore)
                {
                    continue;
                }

                bestSelectionScore = selectionScore;
                selectedPosition = candidate;
                selectedCrowdScore = crowdScore;
                found = true;
            }

            return found;
        }

        private Vector2 GetRandomOpenStandingPosition(GridNavigation2D navigation)
        {
            Rect bounds = navigation.WorldBounds;
            float horizontalMargin = bounds.width * 0.07f;
            float openAreaHalfWidth = Mathf.Min(2.75f, bounds.height * 0.34f);
            return new Vector2(
                Random.Range(bounds.xMin + horizontalMargin, bounds.xMax - horizontalMargin),
                Random.Range(
                    bounds.center.y - openAreaHalfWidth,
                    bounds.center.y + openAreaHalfWidth));
        }

        private bool TryAdoptNearbySupport()
        {
            if (!IsSettledPositionComfortable(body.position))
            {
                return false;
            }

            if (TryGetNearbyWall(body.position, out _))
            {
                behavior = PassengerBehavior.Leaning;
                FaceAwayFromNearestWall();
                RecordDecision("Stand near wall -> Lean");
                return true;
            }

            return TryGrabNearbyHandhold();
        }

        private bool TryGetNearbyWall(Vector2 position, out Collider2D selectedWall)
        {
            selectedWall = null;
            float nearestDistance = wallLeanReleaseDistance;
            if (wallColliders == null)
            {
                return false;
            }

            foreach (Collider2D wallCollider in wallColliders)
            {
                if (wallCollider == null)
                {
                    continue;
                }

                float distance = Vector2.Distance(position, wallCollider.ClosestPoint(position));
                if (distance <= nearestDistance)
                {
                    nearestDistance = distance;
                    selectedWall = wallCollider;
                }
            }

            return selectedWall != null;
        }

        private bool TryGrabNearbyHandhold()
        {
            PassengerActivityPoint selected = null;
            float nearestDistance = nearbyHandholdDistance;
            if (activityPoints == null)
            {
                return false;
            }

            foreach (PassengerActivityPoint point in activityPoints)
            {
                if (point == null ||
                    point.ActivityType != PassengerActivityType.Handhold ||
                    !point.IsAvailableFor(gameObject))
                {
                    continue;
                }

                float distance = Vector2.Distance(body.position, point.transform.position);
                if (distance <= nearestDistance)
                {
                    nearestDistance = distance;
                    selected = point;
                }
            }

            if (selected == null || !selected.TryReserve(gameObject))
            {
                return false;
            }

            reservedActivityPoint = selected;
            behavior = PassengerBehavior.Handhold;
            RecordDecision("Stand near handhold -> Hold");
            return true;
        }

        private bool TryApplyCrowdPressure()
        {
            if (behavior == PassengerBehavior.Leaning &&
                IsNearWall(body.position, wallLeanReleaseDistance))
            {
                return false;
            }

            bool canBePushed = behavior == PassengerBehavior.Leaning ||
                               IsFreeStandingBehavior(behavior);
            if (!canBePushed || passengerPeers == null)
            {
                return false;
            }

            Vector2 pressureDirection = Vector2.zero;
            int pressureSources = 0;
            foreach (GeneralPassengerPrototype passenger in passengerPeers)
            {
                if (passenger == null ||
                    passenger == this ||
                    passenger.body == null ||
                    (passenger.state != PassengerState.Boarding &&
                     passenger.state != PassengerState.MovingToActivity))
                {
                    continue;
                }

                Vector2 offset = body.position - passenger.body.position;
                float distance = offset.magnitude;
                Vector2 passengerIntent = passenger.GetMovementIntent();
                if (distance <= 0.001f ||
                    distance > crowdPressureRadius ||
                    passengerIntent.sqrMagnitude <= 0.001f ||
                    Vector2.Dot(passengerIntent.normalized, offset.normalized) < 0.25f)
                {
                    continue;
                }

                float weight = 1f - distance / crowdPressureRadius;
                pressureDirection += passengerIntent.normalized * weight;
                pressureSources++;
            }

            if (pressureSources < 4 || pressureDirection.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            GridNavigation2D navigation = FindCurrentNavigationArea();
            if (navigation == null)
            {
                return false;
            }

            Vector2 direction = pressureDirection.normalized;
            float pressureMultiplier = 1f + Mathf.Min(pressureSources - 2, 3) * 0.2f;
            Vector2 nextPosition = body.position +
                                   direction * crowdPressureSpeed *
                                   pressureMultiplier * Time.fixedDeltaTime;
            if (!navigation.IsWorldWalkable(nextPosition) ||
                IsBlockedByNavigationObstacle(nextPosition))
            {
                return false;
            }

            body.MovePosition(nextPosition);
            RememberMovementIntent(direction);
            if (behavior == PassengerBehavior.Leaning &&
                !IsNearWall(nextPosition, wallLeanReleaseDistance))
            {
                behavior = PassengerBehavior.AisleStanding;
                activityTarget = nextPosition;
                RecordDecision("Crowd pressure -> Leave wall");
                UpdateLabel();
            }
            else if (IsFreeStandingBehavior(behavior))
            {
                activityTarget = nextPosition;
            }

            return true;
        }

        private bool IsNearWall(Vector2 position, float maximumDistance)
        {
            if (wallColliders == null)
            {
                return false;
            }

            foreach (Collider2D wallCollider in wallColliders)
            {
                if (wallCollider != null &&
                    Vector2.Distance(position, wallCollider.ClosestPoint(position)) <= maximumDistance)
                {
                    return true;
                }
            }

            return false;
        }

        private float CalculateSeatApproachCrowdPressure(Vector2 position)
        {
            if (passengerPeers == null)
            {
                return 0f;
            }

            float pressure = 0f;
            float targetRadius = seatApproachCrowdRadius * 1.2f;
            foreach (GeneralPassengerPrototype passenger in passengerPeers)
            {
                if (passenger == null || passenger == this || passenger.body == null)
                {
                    continue;
                }

                if (passenger.bodyCollider != null &&
                    passenger.bodyCollider.enabled &&
                    passenger.behavior != PassengerBehavior.Seated)
                {
                    float bodyDistance = Vector2.Distance(position, passenger.body.position);
                    if (bodyDistance < seatApproachCrowdRadius)
                    {
                        pressure += (1f - bodyDistance / seatApproachCrowdRadius) * 1.25f;
                    }
                }

                if (passenger.IsSeekingSeat)
                {
                    float targetDistance = Vector2.Distance(
                        position,
                        passenger.activityTarget);
                    if (targetDistance < targetRadius)
                    {
                        pressure += (1f - targetDistance / targetRadius) * 1.8f;
                    }
                }
            }

            if (playerPeer != null)
            {
                float playerDistance = Vector2.Distance(position, playerPeer.transform.position);
                if (playerDistance < seatApproachCrowdRadius)
                {
                    pressure += (1f - playerDistance / seatApproachCrowdRadius) * 1.25f;
                }
            }

            return pressure;
        }

        private bool TryRerouteFromCrowdedSeat()
        {
            if (!IsSeekingSeat ||
                Time.time < nextSeatCrowdCheckTime ||
                seatCrowdSwitchCount >= 2)
            {
                return false;
            }

            nextSeatCrowdCheckTime = Time.time + seatCrowdRecheckInterval;
            float pressure = CalculateSeatApproachCrowdPressure(activityTarget);
            if (pressure < seatApproachCrowdLimit)
            {
                return false;
            }

            abandonedActivityTarget = activityTarget;
            avoidAbandonedTargetUntil = Time.time + 2.5f;
            seatCrowdSwitchCount++;
            ReleaseCurrentActivity();
            ClearPath();
            ResetPassengerBlock();
            ResetActivityProgressTracking();

            observedSeats.Clear();
            CollectAvailableSeats();
            if (observedSeats.Count > 0 && TryReserveObservedSeat())
            {
                behavior = PassengerBehavior.Seated;
                RecordDecision("Crowded seat route -> Other seat");
                state = PassengerState.MovingToActivity;
                UpdateLabel();
                return true;
            }

            observedSeats.Clear();
            initialPlacement = false;
            if (TryChooseLeastCrowdedStandingPosition(
                    out PassengerBehavior standingBehavior))
            {
                behavior = standingBehavior;
                RecordDecision("Crowded seat route -> Stand elsewhere");
                state = PassengerState.MovingToActivity;
                UpdateLabel();
                return true;
            }

            behavior = PassengerBehavior.AisleStanding;
            activityTarget = body.position;
            RecordDecision("Crowded seat route -> Stay nearby");
            ScheduleNextDecision();
            SetState(PassengerState.Observing, behavior);
            return true;
        }

        private bool TryReserveObservedSeat()
        {
            var candidates = new List<ObservedSeat>(observedSeats);

            while (candidates.Count > 0)
            {
                int selectedIndex = 0;
                float bestScore = float.MaxValue;

                for (int i = 0; i < candidates.Count; i++)
                {
                    if (candidates[i].Seat == null ||
                        candidates[i].SittingPoint == null)
                    {
                        continue;
                    }

                    Vector2 approachPosition = candidates[i].Seat.GetApproachPosition(
                        candidates[i].SittingPoint);
                    float distance = Vector2.Distance(
                        body.position,
                        approachPosition);
                    float score = distance;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        selectedIndex = i;
                    }
                }

                ObservedSeat observedSeat = candidates[selectedIndex];
                candidates.RemoveAt(selectedIndex);
                if (observedSeat.Seat == null ||
                    observedSeat.SittingPoint == null ||
                    CalculateSeatApproachCrowdPressure(
                        observedSeat.Seat.GetApproachPosition(
                            observedSeat.SittingPoint)) >= seatApproachCrowdLimit ||
                    IsRecentlyAbandonedTarget(
                        observedSeat.SittingPoint.position,
                        1.4f) ||
                    !observedSeat.Seat.TryReserve(
                        gameObject,
                        observedSeat.SittingPoint,
                        out Transform sittingPoint))
                {
                    continue;
                }

                reservedSeat = observedSeat.Seat;
                reservedSittingPoint = sittingPoint;
                activityTarget = observedSeat.Seat.GetApproachPosition(sittingPoint);
                observedSeats.Clear();
                return true;
            }

            return false;
        }

        private bool TryReserveActivityPoint(PassengerBehavior selectedBehavior)
        {
            PassengerActivityType pointType = ToActivityType(selectedBehavior);
            var candidates = new List<PassengerActivityPoint>();

            if (activityPoints != null)
            {
                foreach (PassengerActivityPoint point in activityPoints)
                {
                    if (point != null && point.ActivityType == pointType && point.IsAvailableFor(gameObject))
                    {
                        candidates.Add(point);
                    }
                }
            }

            while (candidates.Count > 0)
            {
                PassengerActivityPoint selectedPoint = SelectActivityPoint(candidates);
                candidates.Remove(selectedPoint);
                Vector2 usePosition = selectedPoint.GetUsePosition();
                if (!IsStandingPositionAvailable(usePosition) ||
                    IsRecentlyAbandonedTarget(usePosition, 0.8f) ||
                    !selectedPoint.TryReserve(gameObject))
                {
                    continue;
                }

                reservedActivityPoint = selectedPoint;
                activityTarget = selectedPoint.GetUsePosition();
                behavior = selectedBehavior;
                return true;
            }

            return false;
        }

        private bool TryChooseWallLeanPosition(bool endWallsOnly)
        {
            GridNavigation2D navigation = FindCurrentNavigationArea();
            if (navigation == null || wallColliders == null)
            {
                return false;
            }

            Rect navigationBounds = navigation.WorldBounds;
            Vector2 selectedPosition = body.position;
            float bestScore = float.MaxValue;
            bool found = false;
            float bodyClearance = GetBodyRadius() + 0.1f;

            foreach (Collider2D wall in wallColliders)
            {
                if (wall == null)
                {
                    continue;
                }

                Bounds bounds = wall.bounds;
                bool verticalWall = bounds.size.y > bounds.size.x;
                if (endWallsOnly && !verticalWall)
                {
                    continue;
                }

                for (int sample = 0; sample < 5; sample++)
                {
                    Vector2 candidate;
                    if (verticalWall)
                    {
                        float inward = bounds.center.x < navigationBounds.center.x ? 1f : -1f;
                        candidate = new Vector2(
                            bounds.center.x + inward * (bounds.extents.x + bodyClearance),
                            Random.Range(bounds.min.y + bodyClearance, bounds.max.y - bodyClearance));
                    }
                    else
                    {
                        float inward = bounds.center.y < navigationBounds.center.y ? 1f : -1f;
                        candidate = new Vector2(
                            Random.Range(bounds.min.x + bodyClearance, bounds.max.x - bodyClearance),
                            bounds.center.y + inward * (bounds.extents.y + bodyClearance));
                    }

                    if (!navigation.IsWorldWalkable(candidate) ||
                        !IsStandingPositionAvailable(candidate) ||
                        IsRecentlyAbandonedTarget(candidate, 0.85f))
                    {
                        continue;
                    }

                    float distanceFromDoor = boardingDoorway != null
                        ? Vector2.Distance(boardingDoorway.InsidePoint.position, candidate)
                        : 0f;
                    float score = CalculateCrowdScore(candidate) * 4f +
                                  Vector2.Distance(body.position, candidate) * 0.18f -
                                  distanceFromDoor * (endWallsOnly ? 0.38f : 0.08f) +
                                  Random.Range(0f, 0.08f);
                    if (score >= bestScore)
                    {
                        continue;
                    }

                    bestScore = score;
                    selectedPosition = candidate;
                    found = true;
                }
            }

            if (!found)
            {
                return false;
            }

            activityTarget = selectedPosition;
            behavior = PassengerBehavior.Leaning;
            return true;
        }

        private bool ShouldGiveUpContestedActivity()
        {
            if (!IsFixedStandingBehavior(behavior) || reservedActivityPoint == null)
            {
                return false;
            }

            if (!reservedActivityPoint.IsReservedBy(gameObject))
            {
                return true;
            }

            if (passengerPeers == null)
            {
                return false;
            }

            Vector2 ownTarget = reservedActivityPoint.transform.position;
            float ownArrivalTime = Vector2.Distance(body.position, ownTarget) /
                                   Mathf.Max(0.1f, GetCurrentMoveSpeed());

            foreach (GeneralPassengerPrototype passenger in passengerPeers)
            {
                if (passenger == null ||
                    passenger == this ||
                    passenger.body == null ||
                    !IsFixedStandingBehavior(passenger.behavior) ||
                    passenger.reservedActivityPoint == null)
                {
                    continue;
                }

                Vector2 otherTarget = passenger.reservedActivityPoint.transform.position;
                if (Vector2.Distance(ownTarget, otherTarget) > activityClaimRadius)
                {
                    continue;
                }

                bool alreadyUsingTarget = passenger.state == PassengerState.Observing ||
                                          passenger.state == PassengerState.Yielding;
                if (alreadyUsingTarget)
                {
                    return true;
                }

                if (passenger.state != PassengerState.MovingToActivity)
                {
                    continue;
                }

                float otherArrivalTime = Vector2.Distance(passenger.body.position, otherTarget) /
                                         Mathf.Max(0.1f, passenger.GetCurrentMoveSpeed());
                if (otherArrivalTime + activityClaimLeadTime < ownArrivalTime)
                {
                    return true;
                }

                if (Mathf.Abs(otherArrivalTime - ownArrivalTime) <= activityClaimLeadTime &&
                    passenger.GetInstanceID() < GetInstanceID())
                {
                    return true;
                }
            }

            return false;
        }

        private void ReconsiderContestedActivity()
        {
            PassengerBehavior previousBehavior = behavior;
            abandonedActivityTarget = activityTarget;
            avoidAbandonedTargetUntil = Time.time + 1.2f;
            ReleaseCurrentActivity();
            ClearPath();
            ResetPassengerBlock();

            if (TryChooseBehaviorTarget(previousBehavior))
            {
                RecordDecision("Taken spot -> Other " + previousBehavior);
                state = PassengerState.MovingToActivity;
                UpdateLabel();
                return;
            }

            if (previousBehavior == PassengerBehavior.Leaning &&
                TryChooseBehaviorTarget(PassengerBehavior.Handhold))
            {
                RecordDecision("Taken lean spot -> Handhold");
                state = PassengerState.MovingToActivity;
                UpdateLabel();
                return;
            }

            if (TryChooseLeastCrowdedStandingPosition(out PassengerBehavior standingBehavior))
            {
                behavior = standingBehavior;
                RecordDecision("Taken spot -> Stand nearby");
                state = PassengerState.MovingToActivity;
                UpdateLabel();
                return;
            }

            behavior = PassengerBehavior.AisleStanding;
            activityTarget = body.position;
            RecordDecision("Taken spot -> Stay nearby");
            ScheduleNextDecision();
            SetState(PassengerState.Observing, behavior);
        }

        private bool TryChooseBehaviorTarget(PassengerBehavior selectedBehavior)
        {
            if (selectedBehavior == PassengerBehavior.Seated)
            {
                return TryReserveObservedSeat();
            }

            if (selectedBehavior == PassengerBehavior.Leaning ||
                selectedBehavior == PassengerBehavior.Handhold)
            {
                return false;
            }

            if (IsFreeStandingBehavior(selectedBehavior))
            {
                return TryChooseFreeStandingPosition(selectedBehavior);
            }

            return TryReserveActivityPoint(selectedBehavior);
        }

        private bool TryChooseFreeStandingPosition(PassengerBehavior selectedBehavior)
        {
            const int maximumAttempts = 28;
            GridNavigation2D navigation = FindCurrentNavigationArea();
            if (navigation == null)
            {
                return false;
            }

            for (int attempt = 0; attempt < maximumAttempts; attempt++)
            {
                Vector2 candidate = selectedBehavior == PassengerBehavior.DoorStanding
                    ? GetRandomDoorStandingPosition()
                    : GetRandomAisleStandingPosition(navigation);
                if (!navigation.IsWorldWalkable(candidate) ||
                    !IsStandingPositionAvailable(candidate) ||
                    IsRecentlyAbandonedTarget(candidate, 0.85f))
                {
                    continue;
                }

                activityTarget = candidate;
                behavior = selectedBehavior;
                return true;
            }

            return false;
        }

        private bool TryChooseLeastCrowdedStandingPosition(
            out PassengerBehavior selectedBehavior)
        {
            const int maximumAttempts = 36;
            selectedBehavior = PassengerBehavior.AisleStanding;
            GridNavigation2D navigation = FindCurrentNavigationArea();
            if (navigation == null)
            {
                return false;
            }

            Vector2 selectedPosition = body.position;
            float bestScore = float.MaxValue;
            bool found = false;
            int placementTier = GetBoardingPlacementTier();

            for (int attempt = 0; attempt < maximumAttempts; attempt++)
            {
                PassengerBehavior candidateBehavior = PassengerBehavior.AisleStanding;
                Vector2 candidate = initialPlacement && placementTier > 0 && attempt < 26
                    ? GetRandomNearbyStandingPosition(
                        navigation,
                        placementTier == 1 ? 3f : 2.1f)
                    : GetRandomOpenStandingPosition(navigation);
                if (!navigation.IsWorldWalkable(candidate) ||
                    !IsStandingPositionAvailable(candidate) ||
                    IsRecentlyAbandonedTarget(candidate, 0.85f))
                {
                    continue;
                }

                float crowdScore = CalculateCrowdScore(candidate);
                float distanceFromCurrent = Vector2.Distance(body.position, candidate);
                float distanceFromDoor = boardingDoorway != null
                    ? Vector2.Distance(boardingDoorway.InsidePoint.position, candidate)
                    : 0f;
                float score;
                if (placementTier == 0)
                {
                    score = crowdScore * 3.5f -
                            distanceFromDoor * 0.6f +
                            Random.Range(0f, 0.12f);
                }
                else if (placementTier == 1)
                {
                    score = crowdScore * 3.8f +
                            distanceFromCurrent * 0.85f +
                            DistanceToNearestAvailableHandhold(candidate) * 0.5f +
                            Random.Range(0f, 0.12f);
                }
                else
                {
                    score = crowdScore * 3.2f +
                            distanceFromCurrent * 1.4f +
                            Random.Range(0f, 0.12f);
                }
                if (score < bestScore)
                {
                    bestScore = score;
                    selectedPosition = candidate;
                    selectedBehavior = candidateBehavior;
                    found = true;
                }
            }

            if (!found)
            {
                return false;
            }

            activityTarget = selectedPosition;
            behavior = selectedBehavior;
            return true;
        }

        private Vector2 GetRandomNearbyStandingPosition(
            GridNavigation2D navigation,
            float radius)
        {
            Rect bounds = navigation.WorldBounds;
            Vector2 candidate = body.position + Random.insideUnitCircle * radius;
            float horizontalMargin = Mathf.Max(0.45f, GetBodyRadius() + 0.12f);
            float verticalMargin = Mathf.Max(1.1f, GetBodyRadius() + 0.2f);
            candidate.x = Mathf.Clamp(
                candidate.x,
                bounds.xMin + horizontalMargin,
                bounds.xMax - horizontalMargin);
            candidate.y = Mathf.Clamp(
                candidate.y,
                bounds.yMin + verticalMargin,
                bounds.yMax - verticalMargin);
            return candidate;
        }

        private int GetBoardingPlacementTier()
        {
            if (boardingOrder <= 3)
            {
                return 0;
            }

            return boardingOrder <= 7 ? 1 : 2;
        }

        private float DistanceToNearestAvailableHandhold(Vector2 position)
        {
            float nearestDistance = 3f;
            if (activityPoints == null)
            {
                return nearestDistance;
            }

            foreach (PassengerActivityPoint point in activityPoints)
            {
                if (point == null ||
                    point.ActivityType != PassengerActivityType.Handhold ||
                    !point.IsAvailableFor(gameObject))
                {
                    continue;
                }

                nearestDistance = Mathf.Min(
                    nearestDistance,
                    Vector2.Distance(position, point.transform.position));
            }

            return nearestDistance;
        }

        private bool IsRecentlyAbandonedTarget(Vector2 candidate, float clearance)
        {
            return Time.time < avoidAbandonedTargetUntil &&
                   Vector2.Distance(candidate, abandonedActivityTarget) < clearance;
        }

        private Vector2 GetRandomAisleStandingPosition(GridNavigation2D navigation)
        {
            Rect bounds = navigation.WorldBounds;
            float horizontalMargin = bounds.width * 0.08f;
            float aisleHalfWidth = Mathf.Min(0.95f, bounds.height * 0.16f);
            return new Vector2(
                Random.Range(bounds.xMin + horizontalMargin, bounds.xMax - horizontalMargin),
                Random.Range(bounds.center.y - aisleHalfWidth, bounds.center.y + aisleHalfWidth));
        }

        private Vector2 GetRandomDoorStandingPosition()
        {
            var candidates = new List<PassengerDoorway>();
            if (doorways != null)
            {
                foreach (PassengerDoorway doorway in doorways)
                {
                    if (doorway != null && doorway.IsUsable)
                    {
                        candidates.Add(doorway);
                    }
                }
            }

            if (candidates.Count == 0)
            {
                return body.position;
            }

            PassengerDoorway selected = candidates[Random.Range(0, candidates.Count)];
            Vector2 inside = selected.InsidePoint.position;
            Vector2 outside = selected.OutsidePoint.position;
            Vector2 inward = (inside - outside).normalized;
            Vector2 lateral = new Vector2(-inward.y, inward.x);
            float side = Random.value < 0.5f ? -1f : 1f;
            return inside +
                   inward * Random.Range(0.25f, 0.65f) +
                   lateral * side * Random.Range(0.95f, 1.25f);
        }

        private GridNavigation2D FindCurrentNavigationArea()
        {
            if (navigationAreas != null)
            {
                foreach (GridNavigation2D navigation in navigationAreas)
                {
                    if (navigation != null && navigation.ContainsWorldPosition(body.position))
                    {
                        return navigation;
                    }
                }
            }

            return boardingDoorway != null ? boardingDoorway.Navigation : null;
        }

        private bool IsStandingPositionAvailable(Vector2 position)
        {
            if (IsBlockedByNavigationObstacle(position) ||
                IsInsideActiveBoardingFlow(position))
            {
                return false;
            }

            return IsSettledPositionComfortable(position);
        }

        private bool IsInsideActiveBoardingFlow(Vector2 position)
        {
            if (doorways == null)
            {
                return false;
            }

            foreach (PassengerDoorway doorway in doorways)
            {
                if (doorway != null &&
                    doorway.IsUsable &&
                    doorway.IsInsideActiveBoardingFlow(position))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsSettledPositionComfortable(Vector2 position)
        {
            if (passengerPeers == null)
            {
                return true;
            }

            float ownRadius = GetBodyRadius();
            foreach (GeneralPassengerPrototype passenger in passengerPeers)
            {
                if (passenger == null || passenger == this || passenger.body == null)
                {
                    continue;
                }

                float requiredDistance = Mathf.Max(
                    standingPersonalSpaceRadius,
                    ownRadius + passenger.GetBodyRadius() + settledComfortGap);
                bool settledPassenger = passenger.state == PassengerState.Observing ||
                                        passenger.state == PassengerState.Yielding ||
                                        passenger.state == PassengerState.WaitingAtExitDoor;
                bool occupiesStandingSpace = settledPassenger &&
                                               passenger.bodyCollider != null &&
                                               passenger.bodyCollider.enabled &&
                                               passenger.behavior != PassengerBehavior.Seated;
                if (occupiesStandingSpace &&
                    Vector2.Distance(position, passenger.body.position) < requiredDistance)
                {
                    return false;
                }

                bool hasSpatialStandingTarget = UsesSpatialStandingTarget(passenger) &&
                                                (passenger.state == PassengerState.MovingToActivity ||
                                                 passenger.state == PassengerState.Observing ||
                                                 passenger.state == PassengerState.Yielding);
                if (hasSpatialStandingTarget &&
                    Vector2.Distance(position, passenger.activityTarget) < requiredDistance)
                {
                    return false;
                }
            }

            if (playerPeer != null &&
                Vector2.Distance(position, playerPeer.transform.position) <
                standingPersonalSpaceRadius + 0.46f + settledComfortGap)
            {
                return false;
            }

            return true;
        }

        private PassengerActivityPoint SelectActivityPoint(List<PassengerActivityPoint> candidates)
        {
            PassengerActivityPoint selected = candidates[0];
            float bestScore = float.MaxValue;
            foreach (PassengerActivityPoint candidate in candidates)
            {
                Vector2 usePosition = candidate.GetUsePosition();
                float score = CalculateCrowdScore(usePosition) * 1.6f +
                              Vector2.Distance(body.position, usePosition) * 0.55f +
                              Random.Range(0f, 0.12f);
                if (score < bestScore)
                {
                    bestScore = score;
                    selected = candidate;
                }
            }

            return selected;
        }

        private float CalculateCrowdScore(Vector2 position)
        {
            if (passengerPeers == null)
            {
                return 0f;
            }

            float score = 0f;
            foreach (GeneralPassengerPrototype passenger in passengerPeers)
            {
                if (passenger == null || passenger == this || passenger.body == null)
                {
                    continue;
                }

                float distance = Vector2.Distance(position, passenger.body.position);
                if (distance < 3f)
                {
                    score += 1f - distance / 3f;
                }

                bool hasStandingTarget = UsesSpatialStandingTarget(passenger) &&
                                         (passenger.state == PassengerState.MovingToActivity ||
                                          passenger.state == PassengerState.Observing);
                if (hasStandingTarget)
                {
                    float targetDistance = Vector2.Distance(
                        position,
                        passenger.activityTarget);
                    if (targetDistance < 1.6f)
                    {
                        score += (1f - targetDistance / 1.6f) * 0.8f;
                    }
                }
            }

            if (playerPeer != null)
            {
                float playerDistance = Vector2.Distance(position, playerPeer.transform.position);
                if (playerDistance < 3f)
                {
                    score += 1f - playerDistance / 3f;
                }
            }

            return score;
        }

        private bool HasAvailablePoint(PassengerBehavior candidate)
        {
            if (candidate == PassengerBehavior.Leaning)
            {
                return wallColliders != null && wallColliders.Length > 0;
            }

            if (IsFreeStandingBehavior(candidate))
            {
                return FindCurrentNavigationArea() != null;
            }

            PassengerActivityType pointType = ToActivityType(candidate);
            if (activityPoints == null)
            {
                return false;
            }

            foreach (PassengerActivityPoint point in activityPoints)
            {
                if (point != null && point.ActivityType == pointType && point.IsAvailableFor(gameObject))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFreeStandingBehavior(PassengerBehavior candidate)
        {
            return candidate == PassengerBehavior.DoorStanding ||
                   candidate == PassengerBehavior.AisleStanding;
        }

        private static bool UsesSpatialStandingTarget(GeneralPassengerPrototype passenger)
        {
            return passenger != null &&
                   (IsFreeStandingBehavior(passenger.behavior) ||
                    passenger.behavior == PassengerBehavior.Handhold ||
                    passenger.behavior == PassengerBehavior.Leaning);
        }

        private static bool IsFixedStandingBehavior(PassengerBehavior candidate)
        {
            return candidate == PassengerBehavior.Handhold ||
                   candidate == PassengerBehavior.Leaning;
        }

        private static PassengerActivityType ToActivityType(PassengerBehavior selectedBehavior)
        {
            switch (selectedBehavior)
            {
                case PassengerBehavior.Handhold:
                    return PassengerActivityType.Handhold;
                case PassengerBehavior.Leaning:
                    return PassengerActivityType.Lean;
                case PassengerBehavior.DoorStanding:
                    return PassengerActivityType.DoorStanding;
                default:
                    return PassengerActivityType.AisleStanding;
            }
        }

        private void FinishActivityMove()
        {
            ResetActivityProgressTracking();
            initialPlacement = false;
            if (behavior == PassengerBehavior.Seated)
            {
                SitDown();
            }
            else
            {
                activityTarget = body.position;
                TryAdoptNearbySupport();
            }

            ScheduleNextDecision();
            SetState(PassengerState.Observing, behavior);
        }

        private void FaceAwayFromNearestWall()
        {
            GridNavigation2D navigation = FindCurrentNavigationArea();
            if (navigation == null)
            {
                Vector2 towardAisle = new Vector2(
                    0f,
                    GetAisleCenterY() - body.position.y);
                if (towardAisle.sqrMagnitude > 0.001f)
                {
                    viewDirection = towardAisle.normalized;
                }

                return;
            }

            Rect bounds = navigation.WorldBounds;
            Vector2 position = body.position;
            float leftDistance = Mathf.Abs(position.x - bounds.xMin);
            float rightDistance = Mathf.Abs(bounds.xMax - position.x);
            float bottomDistance = Mathf.Abs(position.y - bounds.yMin);
            float topDistance = Mathf.Abs(bounds.yMax - position.y);
            float nearestDistance = Mathf.Min(
                Mathf.Min(leftDistance, rightDistance),
                Mathf.Min(bottomDistance, topDistance));

            if (Mathf.Approximately(nearestDistance, leftDistance))
            {
                viewDirection = Vector2.right;
            }
            else if (Mathf.Approximately(nearestDistance, rightDistance))
            {
                viewDirection = Vector2.left;
            }
            else if (Mathf.Approximately(nearestDistance, bottomDistance))
            {
                viewDirection = Vector2.up;
            }
            else
            {
                viewDirection = Vector2.down;
            }
        }

        private void EvaluateReasonedBehaviorChange()
        {
            if (IsFreeStandingBehavior(behavior) && TryAdoptNearbySupport())
            {
                ScheduleNextDecision();
                UpdateLabel();
                return;
            }

            if (behavior != PassengerBehavior.Seated &&
                Random.value < Mathf.Clamp01(seatRecheckChance))
            {
                ChooseKnownSeat();
                return;
            }

            RecordDecision("Stay " + behavior);
            ScheduleNextDecision();
            UpdateLabel();
        }

        private void BeginExitPreparation()
        {
            ReleaseCurrentActivity();
            ClearPath();
            exitDoorway = SelectExitDoorway();

            if (exitDoorway == null)
            {
                exitDoorway = boardingDoorway;
            }

            ReserveExitDoorway(exitDoorway);

            SetState(PassengerState.PreparingToExit, PassengerBehavior.Exiting);
            BroadcastExitIntent(true);
        }

        private void BroadcastExitIntent(bool force = false)
        {
            if (intentCoordinator == null || exitDoorway == null ||
                (!force && Time.time < nextIntentBroadcastTime))
            {
                return;
            }

            float aisleY = GetAisleCenterY();
            Vector2 sourcePosition = body.position;
            Vector2 destination = exitWaitingTarget;
            var intent = new PassengerExitIntent(
                this,
                exitDoorway,
                sourcePosition,
                new Vector2(sourcePosition.x, aisleY),
                new Vector2(destination.x, aisleY),
                destination,
                Time.time + 0.8f);
            intentCoordinator.PublishExitIntent(intent);
            nextIntentBroadcastTime = Time.time + 0.35f;
        }

        public void ReceiveExitIntent(PassengerExitIntent intent)
        {
            if (intent.Source == null ||
                intent.ExpiresAt < Time.time ||
                state != PassengerState.Observing ||
                (behavior != PassengerBehavior.Handhold &&
                 behavior != PassengerBehavior.DoorStanding &&
                 behavior != PassengerBehavior.Leaning &&
                 behavior != PassengerBehavior.AisleStanding) ||
                !IsBlockingExitIntent(intent))
            {
                return;
            }

            if (!TryFindCourtesyYieldTarget(intent, out Vector2 yieldTarget))
            {
                return;
            }

            BeginCourtesyYield(intent.Source, yieldTarget);
        }

        private void BroadcastSeatIntent()
        {
            if (!IsSeekingSeat ||
                intentCoordinator == null ||
                Time.time < nextSeatIntentBroadcastTime)
            {
                return;
            }

            intentCoordinator.PublishSeatIntent(
                this,
                body.position,
                activityTarget,
                Time.time + 0.7f);
            nextSeatIntentBroadcastTime = Time.time + 0.3f;
        }

        public void ReceiveSeatIntent(
            GeneralPassengerPrototype source,
            Vector2 sourcePosition,
            Vector2 destination,
            float expiresAt)
        {
            if (source == null ||
                expiresAt < Time.time ||
                state != PassengerState.Observing ||
                behavior != PassengerBehavior.Handhold ||
                courtesy < 0.22f ||
                DistancePointToSegment(body.position, sourcePosition, destination) >
                exitIntentCorridorWidth)
            {
                return;
            }

            Vector2 direction = (destination - sourcePosition).normalized;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            Vector2[] candidates =
            {
                body.position + perpendicular * courtesyStepDistance,
                body.position - perpendicular * courtesyStepDistance,
                body.position - direction * courtesyStepDistance * 0.65f
            };

            Vector2 selected = body.position;
            float bestDistance = DistancePointToSegment(
                selected,
                sourcePosition,
                destination);
            foreach (Vector2 candidate in candidates)
            {
                float routeDistance = DistancePointToSegment(
                    candidate,
                    sourcePosition,
                    destination);
                if (routeDistance > bestDistance &&
                    IsCourtesyPositionAvailable(candidate))
                {
                    bestDistance = routeDistance;
                    selected = candidate;
                }
            }

            if (selected != body.position)
            {
                BeginCourtesyYield(source, selected);
            }
        }

        private void BeginCourtesyYield(
            GeneralPassengerPrototype source,
            Vector2 yieldTarget)
        {
            bool releasedHandhold = behavior == PassengerBehavior.Handhold;
            if (releasedHandhold)
            {
                if (reservedActivityPoint != null)
                {
                    reservedActivityPoint.Release(gameObject);
                    reservedActivityPoint = null;
                }

                behavior = PassengerBehavior.AisleStanding;
                activityTarget = body.position;
            }

            bool urgentExit = source != null && source.HasUrgentExitPriority();
            courtesyYieldSource = source;
            courtesyYieldOrigin = body.position;
            courtesyYieldTarget = yieldTarget;
            courtesyYieldCanReturnAt = Time.time +
                                       (urgentExit
                                           ? Random.Range(0.25f, 0.4f)
                                           : Random.Range(0.4f, 0.65f));
            courtesyYieldUntil = Time.time +
                                 (urgentExit
                                     ? Random.Range(1.3f, 1.8f)
                                     : Random.Range(0.9f, 1.3f));
            reachedCourtesyYieldTarget = false;
            ClearPath();
            RecordDecision(
                releasedHandhold
                    ? "Release handhold -> Yield"
                    : "Step back -> Yield");
            state = PassengerState.Yielding;
            UpdateLabel();
        }

        private bool IsBlockingExitIntent(PassengerExitIntent intent)
        {
            Vector2 position = body.position;
            return DistancePointToSegment(position, intent.SourcePosition, intent.AisleEntry) <=
                   exitIntentCorridorWidth ||
                   DistancePointToSegment(position, intent.AisleEntry, intent.AisleExit) <=
                   exitIntentCorridorWidth ||
                   DistancePointToSegment(position, intent.AisleExit, intent.Destination) <=
                   exitIntentCorridorWidth;
        }

        private bool TryFindCourtesyYieldTarget(
            PassengerExitIntent intent,
            out Vector2 yieldTarget)
        {
            Vector2 position = body.position;
            Vector2 routeDirection = GetNearestIntentSegmentDirection(position, intent);
            if (routeDirection.sqrMagnitude <= 0.001f)
            {
                routeDirection = (intent.Destination - intent.SourcePosition).normalized;
            }

            Vector2 perpendicular = new Vector2(-routeDirection.y, routeDirection.x);
            Vector2[] candidates =
            {
                position + perpendicular * courtesyStepDistance,
                position - perpendicular * courtesyStepDistance,
                position + perpendicular * courtesyStepDistance * 1.45f,
                position - perpendicular * courtesyStepDistance * 1.45f,
                position - routeDirection * courtesyStepDistance * 0.75f,
                position - routeDirection * courtesyStepDistance * 1.25f,
                GetWallTuckPosition(position)
            };

            float bestScore = float.MinValue;
            yieldTarget = position;
            foreach (Vector2 candidate in candidates)
            {
                if (!IsEmergencyYieldPositionAvailable(candidate))
                {
                    continue;
                }

                float score = DistanceToExitIntentRoute(candidate, intent);
                if (score > bestScore)
                {
                    bestScore = score;
                    yieldTarget = candidate;
                }
            }

            return bestScore > float.MinValue;
        }

        private Vector2 GetWallTuckPosition(Vector2 position)
        {
            if (!TryGetNearbyWall(position, out Collider2D wall))
            {
                return position;
            }

            Vector2 closestPoint = wall.ClosestPoint(position);
            Vector2 awayFromWall = position - closestPoint;
            if (awayFromWall.sqrMagnitude <= 0.001f)
            {
                awayFromWall = new Vector2(
                    GetAisleCenterY() - position.y,
                    0f);
            }

            return closestPoint + awayFromWall.normalized * (GetBodyRadius() + 0.08f);
        }

        private void UpdateCourtesyYield()
        {
            if (ShouldPrepareToExit())
            {
                state = PassengerState.Observing;
                BeginExitPreparation();
                return;
            }

            if (!reachedCourtesyYieldTarget)
            {
                reachedCourtesyYieldTarget = MoveDirectly(courtesyYieldTarget);
                return;
            }

            bool sourcePassed = courtesyYieldSource == null ||
                                courtesyYieldSource.body == null ||
                                Vector2.Distance(
                                    body.position,
                                    courtesyYieldSource.body.position) >
                                standingYieldDetectionRadius * 1.15f ||
                                courtesyYieldSource.state == PassengerState.WaitingAtExitDoor ||
                                courtesyYieldSource.state == PassengerState.Exiting ||
                                courtesyYieldSource.state == PassengerState.LeavingPlatform;
            if (Time.time < courtesyYieldCanReturnAt ||
                (!sourcePassed && Time.time < courtesyYieldUntil))
            {
                RememberMovementIntent(Vector2.zero);
                return;
            }

            if (IsFreeStandingBehavior(behavior))
            {
                activityTarget = body.position;
                CompleteCourtesyYield();
                standingYieldCooldownUntil = Time.time + Random.Range(1.2f, 2f);
                return;
            }

            if (!IsCourtesyPositionAvailable(courtesyYieldOrigin))
            {
                FinishCourtesyYieldInPlace();
                return;
            }

            if (!MoveDirectly(courtesyYieldOrigin))
            {
                return;
            }

            CompleteCourtesyYield();
        }

        private void CompleteCourtesyYield()
        {
            courtesyYieldSource = null;
            reachedCourtesyYieldTarget = false;
            ScheduleNextDecision();
            state = PassengerState.Observing;
            RecordDecision("Yield complete");
            UpdateLabel();
        }

        private void FinishCourtesyYieldInPlace()
        {
            ReleaseCurrentActivity();
            behavior = PassengerBehavior.AisleStanding;
            activityTarget = body.position;
            standingYieldCooldownUntil = Time.time + Random.Range(1.2f, 2f);
            CompleteCourtesyYield();
        }

        private bool TryYieldFromDoorwayTraffic()
        {
            if (behavior != PassengerBehavior.DoorStanding ||
                Time.time < nextStandingYieldCheckTime ||
                Time.time < standingYieldCooldownUntil)
            {
                return false;
            }

            nextStandingYieldCheckTime = Time.time + Random.Range(0.18f, 0.3f);
            GeneralPassengerPrototype nearestMover = null;
            Vector2 nearestIntent = Vector2.zero;
            float nearestDistance = float.MaxValue;
            GeneralPassengerPrototype[] passengers = passengerPeers;

            foreach (GeneralPassengerPrototype passenger in passengers)
            {
                if (passenger == null ||
                    passenger == this ||
                    passenger.body == null ||
                    !passenger.IsActivelyMoving() ||
                    !passenger.IsDoorwayTraffic() ||
                    passenger.state == PassengerState.Yielding)
                {
                    continue;
                }

                Vector2 intent = passenger.GetMovementIntent();
                float distance = Vector2.Distance(body.position, passenger.body.position);
                if (intent.sqrMagnitude <= 0.01f ||
                    distance > standingYieldDetectionRadius ||
                    DistancePointToSegment(
                        body.position,
                        passenger.body.position,
                        passenger.body.position + intent.normalized * standingYieldDetectionRadius) >
                    exitIntentCorridorWidth ||
                    distance >= nearestDistance)
                {
                    continue;
                }

                nearestMover = passenger;
                nearestIntent = intent.normalized;
                nearestDistance = distance;
            }

            if (nearestMover != null &&
                TryFindRouteYieldTarget(nearestMover.body.position, nearestIntent, out Vector2 moverYieldTarget))
            {
                BeginCourtesyYield(nearestMover, moverYieldTarget);
                return true;
            }

            return false;
        }

        private bool TryFindRouteYieldTarget(
            Vector2 moverPosition,
            Vector2 moverDirection,
            out Vector2 yieldTarget)
        {
            Vector2 perpendicular = new Vector2(-moverDirection.y, moverDirection.x);
            Vector2[] candidates =
            {
                body.position + perpendicular * courtesyStepDistance,
                body.position - perpendicular * courtesyStepDistance,
                body.position - moverDirection * courtesyStepDistance * 0.7f
            };

            yieldTarget = body.position;
            Vector2 routeEnd = moverPosition + moverDirection * standingYieldDetectionRadius;
            float currentRouteDistance = DistancePointToSegment(
                body.position,
                moverPosition,
                routeEnd);
            float bestScore = currentRouteDistance + courtesyRouteClearanceGain;
            foreach (Vector2 candidate in candidates)
            {
                GridNavigation2D navigation = FindCurrentNavigationArea();
                if (navigation == null ||
                    !navigation.IsWorldWalkable(candidate) ||
                    !IsCourtesyPositionAvailable(candidate))
                {
                    continue;
                }

                float score = DistancePointToSegment(candidate, moverPosition, routeEnd);
                if (score > bestScore)
                {
                    bestScore = score;
                    yieldTarget = candidate;
                }
            }

            return yieldTarget != body.position;
        }

        private void ReportObstruction(
            GeneralPassengerPrototype source,
            Vector2 sourceDirection)
        {
            if (source == null ||
                source == this ||
                !CanYieldForStationaryTraffic(source) ||
                (Time.time < obstructionYieldCooldownUntil &&
                 !source.HasUrgentExitPriority()) ||
                source.state == PassengerState.Yielding)
            {
                return;
            }

            Vector2 offset = body.position - source.body.position;
            if (offset.magnitude > standingYieldDetectionRadius ||
                sourceDirection.sqrMagnitude <= 0.001f ||
                Vector2.Dot(offset.normalized, sourceDirection.normalized) < 0.25f)
            {
                return;
            }

            obstructionSource = source;
            obstructionDirection = sourceDirection.normalized;
            lastObstructionReportTime = Time.time;
            if (source.HasUrgentExitPriority())
            {
                obstructionScore = obstructionYieldThreshold;
                obstructionYieldCooldownUntil = 0f;
                return;
            }

            float sourceUrgency = Mathf.Clamp01(source.GetMovementPriority() / 3f);
            obstructionScore = Mathf.Min(
                obstructionYieldThreshold * 1.5f,
                obstructionScore + Time.fixedDeltaTime * Mathf.Lerp(0.75f, 1.3f, sourceUrgency));
        }

        private void UpdateObstructionPressure()
        {
            if (Time.time - lastObstructionReportTime > 0.25f)
            {
                obstructionScore = Mathf.MoveTowards(
                    obstructionScore,
                    0f,
                    obstructionScoreDecay * Time.fixedDeltaTime);
            }

            if (obstructionSource == null || obstructionScore <= 0f)
            {
                obstructionSource = null;
            }
        }

        private bool TryYieldFromObstructionPressure()
        {
            if (obstructionScore < obstructionYieldThreshold ||
                obstructionSource == null ||
                !CanYieldForStationaryTraffic(obstructionSource) ||
                obstructionSource.state == PassengerState.Yielding ||
                Time.time < obstructionYieldCooldownUntil ||
                !TryFindRouteYieldTarget(
                    obstructionSource.body.position,
                    obstructionDirection,
                    out Vector2 yieldTarget))
            {
                return false;
            }

            GeneralPassengerPrototype source = obstructionSource;
            obstructionScore = 0f;
            obstructionSource = null;
            obstructionYieldCooldownUntil = Time.time + Random.Range(2f, 3f);
            obstructionYieldCount++;
            RecordDecision("Blocking route -> Yield");
            BeginCourtesyYield(source, yieldTarget);
            return true;
        }

        private bool CanYieldForStationaryTraffic(GeneralPassengerPrototype source)
        {
            if (source == null || state != PassengerState.Observing)
            {
                return false;
            }

            if (behavior == PassengerBehavior.Handhold)
            {
                return source.IsActivelyMoving();
            }

            return behavior == PassengerBehavior.DoorStanding &&
                   source.IsDoorwayTraffic();
        }

        private bool IsEmergencyYieldPositionAvailable(Vector2 position)
        {
            if (IsBlockedByNavigationObstacle(position))
            {
                return false;
            }

            Collider2D[] overlaps = Physics2D.OverlapCircleAll(position, 0.34f);
            foreach (Collider2D overlap in overlaps)
            {
                GeneralPassengerPrototype passenger =
                    overlap.GetComponentInParent<GeneralPassengerPrototype>();
                if (passenger != null && passenger != this)
                {
                    return false;
                }

                if (overlap.GetComponentInParent<PlayerBoardingCyclePrototype>() != null)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsCourtesyPositionAvailable(Vector2 position)
        {
            if (IsBlockedByNavigationObstacle(position))
            {
                return false;
            }

            Collider2D[] overlaps = Physics2D.OverlapCircleAll(position, 0.48f);
            foreach (Collider2D overlap in overlaps)
            {
                GeneralPassengerPrototype passenger =
                    overlap.GetComponentInParent<GeneralPassengerPrototype>();
                if (passenger != null && passenger != this)
                {
                    return false;
                }

                if (overlap.GetComponentInParent<PlayerBoardingCyclePrototype>() != null)
                {
                    return false;
                }
            }

            return true;
        }

        private static Vector2 GetNearestIntentSegmentDirection(
            Vector2 position,
            PassengerExitIntent intent)
        {
            Vector2[] starts = { intent.SourcePosition, intent.AisleEntry, intent.AisleExit };
            Vector2[] ends = { intent.AisleEntry, intent.AisleExit, intent.Destination };
            float nearestDistance = float.MaxValue;
            Vector2 nearestDirection = Vector2.zero;

            for (int i = 0; i < starts.Length; i++)
            {
                float distance = DistancePointToSegment(position, starts[i], ends[i]);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestDirection = (ends[i] - starts[i]).normalized;
                }
            }

            return nearestDirection;
        }

        private static float DistanceToExitIntentRoute(
            Vector2 position,
            PassengerExitIntent intent)
        {
            return Mathf.Min(
                DistancePointToSegment(position, intent.SourcePosition, intent.AisleEntry),
                DistancePointToSegment(position, intent.AisleEntry, intent.AisleExit),
                DistancePointToSegment(position, intent.AisleExit, intent.Destination));
        }

        private static float DistancePointToSegment(
            Vector2 point,
            Vector2 segmentStart,
            Vector2 segmentEnd)
        {
            Vector2 segment = segmentEnd - segmentStart;
            if (segment.sqrMagnitude <= 0.0001f)
            {
                return Vector2.Distance(point, segmentStart);
            }

            float t = Mathf.Clamp01(Vector2.Dot(point - segmentStart, segment) /
                                    segment.sqrMagnitude);
            return Vector2.Distance(point, segmentStart + segment * t);
        }

        private PassengerDoorway SelectExitDoorway()
        {
            PassengerDoorway nearest = null;
            float nearestDistance = float.MaxValue;

            if (doorways == null)
            {
                return boardingDoorway;
            }

            foreach (PassengerDoorway doorway in doorways)
            {
                if (doorway == null || !doorway.IsUsable)
                {
                    continue;
                }

                float distance = Vector2.SqrMagnitude(
                    (Vector2)doorway.InsidePoint.position - body.position);
                if (distance < nearestDistance)
                {
                    nearest = doorway;
                    nearestDistance = distance;
                }
            }

            return nearest ?? boardingDoorway;
        }

        private void ReserveExitDoorway(PassengerDoorway doorway)
        {
            if (doorway == null)
            {
                return;
            }

            doorway.TryReserveExit(gameObject);
            hasExitReservation = true;
            exitWaitingTarget = doorway.GetExitWaitingPosition(gameObject);
        }

        private void ReleaseExitReservation()
        {
            if (!hasExitReservation || exitDoorway == null)
            {
                return;
            }

            exitDoorway.ReleaseExit(gameObject);
            hasExitReservation = false;
        }

        private void ReleaseCurrentActivity()
        {
            if (reservedSeat != null)
            {
                if (bodyCollider != null && !bodyCollider.enabled)
                {
                    StandUp();
                }

                reservedSeat.Release(gameObject);
                reservedSeat = null;
                reservedSittingPoint = null;
            }

            if (reservedActivityPoint != null)
            {
                reservedActivityPoint.Release(gameObject);
                reservedActivityPoint = null;
            }
        }

        private void SitDown()
        {
            if (bodyCollider != null)
            {
                bodyCollider.enabled = false;
            }

            body.bodyType = RigidbodyType2D.Kinematic;
            SetPosition(reservedSittingPoint.position);
            Vector2 aisleDirection = activityTarget - (Vector2)reservedSittingPoint.position;
            if (aisleDirection.sqrMagnitude > 0.001f)
            {
                viewDirection = aisleDirection.normalized;
            }
        }

        private void StandUp()
        {
            SetPosition(reservedSeat.GetApproachPosition(reservedSittingPoint));
            body.bodyType = RigidbodyType2D.Dynamic;

            if (bodyCollider != null)
            {
                bodyCollider.enabled = true;
            }
        }

        private bool ShouldPrepareToExit()
        {
            return doorCycle.IsTravelling &&
                   plannedExitStopNumber == doorCycle.StopNumber + 1 &&
                   doorCycle.TravelTimeRemaining <= exitPreparationLeadTime;
        }

        private bool MoveUsingPath(
            Vector2 destination,
            bool ignorePassengerAvoidance = false)
        {
            if (Vector2.Distance(body.position, destination) <= arrivalDistance)
            {
                return true;
            }

            if (!ignorePassengerAvoidance && hasBlockedDetourTarget)
            {
                if (!MoveDirectly(blockedDetourTarget))
                {
                    return false;
                }

                hasBlockedDetourTarget = false;
                blockedDetourAttempted = false;
                RecordDecision("Detour clear -> Resume");
                ResetPassengerBlock();
                ClearPath();
            }

            if (!hasPathTarget ||
                Vector2.Distance(pathTarget, destination) > 0.05f ||
                Time.time >= nextRepathTime)
            {
                RebuildPath(destination, ignorePassengerAvoidance);
            }

            if (path.Count == 0 || pathIndex >= path.Count)
            {
                return ignorePassengerAvoidance
                    ? MoveWithoutAvoidance(destination)
                    : MoveDirectly(destination);
            }

            Vector2 waypoint = path[pathIndex];
            if (ignorePassengerAvoidance)
            {
                MoveWithoutAvoidance(waypoint);
            }
            else
            {
                MoveBodyTowards(waypoint);
            }

            if (Vector2.Distance(body.position, waypoint) <= arrivalDistance + 0.04f)
            {
                pathIndex++;
            }

            return Vector2.Distance(body.position, destination) <= arrivalDistance;
        }

        private void RebuildPath(
            Vector2 destination,
            bool ignorePassengerAvoidance)
        {
            path.Clear();
            Vector2 start = body.position;
            List<Vector2> stationaryPassengers = ignorePassengerAvoidance
                ? null
                : CollectStationaryPassengerPositions(destination);

            if (!ignorePassengerAvoidance && ShouldUseAisleRoute(destination))
            {
                float aisleY = GetAisleCenterY() + GetPreferredAisleOffset();
                float horizontalDirection = Mathf.Sign(destination.x - start.x);
                float mergeDistance = Mathf.Min(
                    1.8f,
                    Mathf.Abs(destination.x - start.x) * 0.35f);
                Vector2 aisleEntry = new Vector2(
                    start.x + horizontalDirection * mergeDistance,
                    aisleY);
                float approachLead = Mathf.Abs(destination.x - start.x) > 1.4f ? 1.1f : 0f;
                Vector2 aisleApproach = new Vector2(
                    destination.x - horizontalDirection * approachLead,
                    aisleY);

                AppendPathSegment(start, aisleEntry, stationaryPassengers);
                AppendPathSegment(aisleEntry, aisleApproach, stationaryPassengers);
                AppendPathSegment(aisleApproach, destination, stationaryPassengers);
            }
            else
            {
                AppendPathSegment(start, destination, stationaryPassengers);
            }

            while (path.Count > 0 &&
                   Vector2.Distance(body.position, path[0]) <= arrivalDistance + 0.12f)
            {
                path.RemoveAt(0);
            }

            pathIndex = 0;
            pathTarget = destination;
            hasPathTarget = true;
            nextRepathTime = Time.time + 1.5f;
        }

        private void AppendPathSegment(
            Vector2 start,
            Vector2 destination,
            List<Vector2> stationaryPassengers)
        {
            if (Vector2.Distance(start, destination) <= 0.1f)
            {
                return;
            }

            List<Vector3> shortestPath = FindShortestPath(
                start,
                destination,
                stationaryPassengers);
            if ((shortestPath == null || shortestPath.Count == 0) &&
                stationaryPassengers != null &&
                stationaryPassengers.Count > 0)
            {
                shortestPath = FindShortestPath(start, destination, null);
            }

            if (shortestPath == null || shortestPath.Count == 0)
            {
                if (path.Count == 0 || Vector2.Distance(path[path.Count - 1], destination) > 0.1f)
                {
                    path.Add(destination);
                }

                return;
            }

            foreach (Vector3 point in shortestPath)
            {
                if (path.Count == 0 || Vector2.Distance(path[path.Count - 1], point) > 0.08f)
                {
                    path.Add(point);
                }
            }
        }

        private List<Vector3> FindShortestPath(
            Vector2 start,
            Vector2 destination,
            List<Vector2> stationaryPassengers)
        {
            List<Vector3> shortestPath = null;
            if (navigationAreas == null)
            {
                return null;
            }

            foreach (GridNavigation2D navigation in navigationAreas)
            {
                if (navigation == null)
                {
                    continue;
                }

                List<Vector3> candidatePath = stationaryPassengers == null
                    ? navigation.FindWorldPath(start, destination)
                    : navigation.FindWorldPath(
                        start,
                        destination,
                        point => IsWalkableAroundStationaryPassengers(
                            point,
                            start,
                            destination,
                            stationaryPassengers));
                if (candidatePath.Count == 0 ||
                    (shortestPath != null && candidatePath.Count >= shortestPath.Count))
                {
                    continue;
                }

                shortestPath = candidatePath;
            }

            return shortestPath;
        }

        private List<Vector2> CollectStationaryPassengerPositions(
            Vector2 destination)
        {
            var positions = new List<Vector2>();
            if (passengerPeers == null)
            {
                return positions;
            }

            foreach (GeneralPassengerPrototype passenger in passengerPeers)
            {
                if (passenger == null ||
                    passenger == this ||
                    passenger.body == null ||
                    passenger.bodyCollider == null ||
                    !passenger.bodyCollider.enabled ||
                    (passenger.state != PassengerState.Observing &&
                     passenger.state != PassengerState.WaitingAtExitDoor) ||
                    Vector2.Distance(passenger.body.position, destination) < 0.45f)
                {
                    continue;
                }

                positions.Add(passenger.body.position);
            }

            if (playerPeer != null &&
                Vector2.Distance(playerPeer.transform.position, destination) >= 0.45f)
            {
                positions.Add(playerPeer.transform.position);
            }

            return positions;
        }

        private bool IsWalkableAroundStationaryPassengers(
            Vector2 point,
            Vector2 start,
            Vector2 destination,
            List<Vector2> stationaryPassengers)
        {
            if (Vector2.Distance(point, start) <= stationaryRouteClearance ||
                Vector2.Distance(point, destination) <= 0.3f)
            {
                return true;
            }

            foreach (Vector2 passengerPosition in stationaryPassengers)
            {
                if (Vector2.Distance(point, passengerPosition) <
                    stationaryRouteClearance)
                {
                    return false;
                }
            }

            return true;
        }

        private bool ShouldUseAisleRoute(Vector2 destination)
        {
            if (passengerPeers == null)
            {
                return false;
            }

            bool meaningfulTravel = Vector2.Distance(body.position, destination) > 1.2f;
            bool horizontalTravel = Mathf.Abs(destination.x - body.position.x) > 1.5f;
            if (!meaningfulTravel || !horizontalTravel)
            {
                return false;
            }

            float aisleY = GetAisleCenterY();
            float minimumX = Mathf.Min(body.position.x, destination.x) - 0.8f;
            float maximumX = Mathf.Max(body.position.x, destination.x) + 0.8f;
            bool upperSideBlocked = false;
            bool lowerSideBlocked = false;

            foreach (GeneralPassengerPrototype passenger in passengerPeers)
            {
                if (passenger == null ||
                    passenger == this ||
                    passenger.body == null ||
                    passenger.behavior != PassengerBehavior.Handhold ||
                    passenger.state != PassengerState.Observing)
                {
                    continue;
                }

                Vector2 passengerPosition = passenger.body.position;
                if (passengerPosition.x < minimumX || passengerPosition.x > maximumX)
                {
                    continue;
                }

                upperSideBlocked |= passengerPosition.y > aisleY + 0.45f;
                lowerSideBlocked |= passengerPosition.y < aisleY - 0.45f;
            }

            return upperSideBlocked && lowerSideBlocked;
        }

        private float GetAisleCenterY()
        {
            return mapRoot != null ? mapRoot.position.y : 0f;
        }

        private float GetPreferredAisleOffset()
        {
            int lane = Mathf.Abs(GetInstanceID()) % 5;
            return (lane - 2) * 0.16f;
        }

        private bool MoveDirectly(Vector2 destination)
        {
            bool detouringAroundPassenger = hasBlockedDetourTarget &&
                                             Vector2.Distance(destination, blockedDetourTarget) > 0.05f;
            if (detouringAroundPassenger)
            {
                if (Time.time >= blockedGiveUpAt)
                {
                    hasBlockedDetourTarget = false;
                    blockedDetourAttempted = false;
                    ResetPassengerBlock();
                }
                else
                {
                    MoveBodyTowards(blockedDetourTarget);
                    if (Vector2.Distance(body.position, blockedDetourTarget) <=
                        arrivalDistance + 0.08f)
                    {
                        hasBlockedDetourTarget = false;
                        blockedDetourAttempted = false;
                        ResetPassengerBlock();
                    }

                    return false;
                }
            }

            MoveBodyTowards(destination);
            return Vector2.Distance(body.position, destination) <= arrivalDistance;
        }

        private bool MoveWithoutAvoidance(Vector2 destination)
        {
            Vector2 toDestination = destination - body.position;
            if (toDestination.sqrMagnitude <= arrivalDistance * arrivalDistance)
            {
                return true;
            }

            float step = Mathf.Min(
                GetCurrentMoveSpeed() * Time.fixedDeltaTime,
                toDestination.magnitude);
            Vector2 direction = toDestination.normalized;
            viewDirection = Vector2.Lerp(
                viewDirection,
                direction,
                Mathf.Clamp01(8f * Time.fixedDeltaTime)).normalized;
            RememberMovementIntent(direction);
            Vector2 nextPosition = KeepMinimumBodySeparation(
                body.position + direction * step,
                direction);
            body.MovePosition(nextPosition);
            return Vector2.Distance(body.position, destination) <= arrivalDistance;
        }

        private void BeginLeavingPlatform()
        {
            Vector2 inside = exitDoorway.InsidePoint.position;
            Vector2 outside = exitDoorway.OutsidePoint.position;
            Vector2 outward = (outside - inside).normalized;
            Vector2 lateral = new Vector2(-outward.y, outward.x);
            platformExitTarget = outside +
                                 outward * 2.8f +
                                 lateral * Random.Range(-1.1f, 1.1f);

            if (bodyCollider != null)
            {
                bodyCollider.enabled = false;
            }

            body.bodyType = RigidbodyType2D.Kinematic;
            ClearPath();
        }

        private void MoveBodyTowards(Vector2 destination)
        {
            Vector2 toDestination = destination - body.position;
            if (toDestination.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float step = Mathf.Min(
                GetCurrentMoveSpeed() * Time.fixedDeltaTime,
                toDestination.magnitude);
            Vector2 desiredDirection = toDestination.normalized;
            Vector2 movementDirection = GetLocallyAvoidedDirection(desiredDirection);
            RememberMovementIntent(
                movementDirection.sqrMagnitude > 0.0001f
                    ? movementDirection
                    : desiredDirection * 0.15f);
            if (movementDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            viewDirection = Vector2.Lerp(
                viewDirection,
                movementDirection.normalized,
                Mathf.Clamp01(8f * Time.fixedDeltaTime)).normalized;

            Vector2 nextPosition = body.position + movementDirection * step;
            float nextPositionClearance = HasUrgentExitPriority()
                ? softOverlapRadius * 0.52f
                : softOverlapRadius * 0.58f;
            GeneralPassengerPrototype passengerAtNextPosition =
                FindPassengerTooCloseTo(nextPosition, nextPositionClearance);
            if (passengerAtNextPosition != null &&
                !IsUrgentExitSeparation(passengerAtNextPosition, nextPosition))
            {
                blockingPassenger = passengerAtNextPosition;
                passengerAtNextPosition.ReportObstruction(this, desiredDirection);
                Vector2 recoveryDirection = GetBlockedMovementDirection(desiredDirection);
                if (recoveryDirection.sqrMagnitude <= 0.0001f)
                {
                    RememberMovementIntent(Vector2.zero);
                    return;
                }

                nextPosition = body.position + recoveryDirection * step;
                float recoveryClearance = HasUrgentExitPriority()
                    ? softOverlapRadius * 0.4f
                    : softOverlapRadius * 0.38f;
                GeneralPassengerPrototype recoveryBlocker =
                    FindPassengerTooCloseTo(nextPosition, recoveryClearance);
                if (recoveryBlocker != null &&
                    !IsUrgentExitSeparation(recoveryBlocker, nextPosition))
                {
                    RememberMovementIntent(Vector2.zero);
                    return;
                }
            }

            nextPosition = KeepMinimumBodySeparation(nextPosition, movementDirection);
            body.MovePosition(nextPosition);
        }

        private Vector2 KeepMinimumBodySeparation(
            Vector2 proposedPosition,
            Vector2 movementDirection)
        {
            if (passengerPeers == null)
            {
                return proposedPosition;
            }

            Vector2 adjustedPosition = proposedPosition;
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (GeneralPassengerPrototype passenger in passengerPeers)
                {
                    if (passenger == null ||
                        passenger == this ||
                        passenger.body == null ||
                        passenger.bodyCollider == null)
                    {
                        continue;
                    }

                    Vector2 offset = adjustedPosition - passenger.body.position;
                    float distance = offset.magnitude;
                    if (distance >= minimumBodySeparation)
                    {
                        continue;
                    }

                    if (distance <= 0.001f)
                    {
                        Vector2 baseDirection = movementDirection.sqrMagnitude > 0.001f
                            ? movementDirection.normalized
                            : Vector2.up;
                        float side = GetInstanceID() < passenger.GetInstanceID() ? -1f : 1f;
                        offset = new Vector2(-baseDirection.y, baseDirection.x) * side;
                        distance = 0f;
                    }

                    adjustedPosition += offset.normalized *
                                        (minimumBodySeparation - distance);
                }
            }

            GridNavigation2D navigation = FindCurrentNavigationArea();
            if (navigation != null &&
                navigation.ContainsWorldPosition(body.position) &&
                (!navigation.IsWorldWalkable(adjustedPosition) ||
                 IsBlockedByNavigationObstacle(adjustedPosition)))
            {
                return body.position;
            }

            return adjustedPosition;
        }

        private bool IsUrgentExitSeparation(
            GeneralPassengerPrototype passenger,
            Vector2 nextPosition)
        {
            if (!HasUrgentExitPriority() || passenger == null || passenger.body == null)
            {
                return false;
            }

            float currentDistance = Vector2.Distance(
                body.position,
                passenger.body.position);
            float nextDistance = Vector2.Distance(
                nextPosition,
                passenger.body.position);
            return nextDistance > currentDistance + 0.002f;
        }

        private Vector2 GetLocallyAvoidedDirection(Vector2 desiredDirection)
        {
            float radius = GetAvoidanceRadius();
            GeneralPassengerPrototype passengerAhead =
                FindPassengerDirectlyAhead(desiredDirection, radius);
            if (passengerAhead != null)
            {
                blockingPassenger = passengerAhead;
                passengerAhead.ReportObstruction(this, desiredDirection);
            }

            if (ShouldWaitForPassenger(passengerAhead, desiredDirection))
            {
                RememberMovementIntent(Vector2.zero);
                return Vector2.zero;
            }

            if (!HasActorAhead(desiredDirection, radius))
            {
                if (!IsStillCloseToBlockingPassenger())
                {
                    ResetPassengerBlock();
                }

                return desiredDirection;
            }

            Vector2 perpendicular = new Vector2(-desiredDirection.y, desiredDirection.x);
            float selectedSide = avoidanceSide;
            bool headOnApproach = IsHeadOnApproach(
                passengerAhead,
                desiredDirection);

            if (Time.time >= avoidanceSideUntil ||
                Mathf.Approximately(selectedSide, 0f))
            {
                Vector2 leftDirection = BuildAvoidanceDirection(
                    desiredDirection,
                    perpendicular,
                    1f);
                Vector2 rightDirection = BuildAvoidanceDirection(
                    desiredDirection,
                    perpendicular,
                    -1f);
                float leftClearance = MeasureClearance(leftDirection, radius);
                float rightClearance = MeasureClearance(rightDirection, radius);

                if (Mathf.Max(leftClearance, rightClearance) <= radius * 0.55f)
                {
                    return GetBlockedMovementDirection(desiredDirection);
                }

                if (passengerAhead != null)
                {
                    selectedSide = SelectPassingSide(
                        passengerAhead,
                        desiredDirection);

                    float selectedClearance = selectedSide > 0f
                        ? leftClearance
                        : rightClearance;
                    float alternateClearance = selectedSide > 0f
                        ? rightClearance
                        : leftClearance;
                    if (alternateClearance > selectedClearance + 0.08f)
                    {
                        selectedSide *= -1f;
                    }
                }
                else if (Mathf.Abs(leftClearance - rightClearance) <= 0.05f)
                {
                    selectedSide = GetInstanceID() % 2 == 0 ? 1f : -1f;
                }
                else
                {
                    selectedSide = leftClearance > rightClearance ? 1f : -1f;
                }

                avoidanceSide = selectedSide;
                float sideHoldDuration = headOnApproach
                    ? Mathf.Max(avoidanceSideHoldDuration, 0.8f)
                    : avoidanceSideHoldDuration;
                avoidanceSideUntil = Time.time + sideHoldDuration;
            }

            Vector2 avoidedDirection = BuildAvoidanceDirection(
                desiredDirection,
                perpendicular,
                selectedSide);

            if (MeasureClearance(avoidedDirection, radius) <= radius * 0.35f)
            {
                return GetBlockedMovementDirection(desiredDirection);
            }

            if (!IsStillCloseToBlockingPassenger())
            {
                ResetPassengerBlock();
            }

            avoidanceAdjustmentCount++;
            return avoidedDirection;
        }

        private bool IsStillCloseToBlockingPassenger()
        {
            return blockingPassenger != null &&
                   blockingPassenger.body != null &&
                   Vector2.Distance(body.position, blockingPassenger.body.position) <
                   softOverlapRadius * 1.25f;
        }

        private GeneralPassengerPrototype FindPassengerTooCloseTo(
            Vector2 position,
            float minimumDistance)
        {
            GeneralPassengerPrototype[] passengers = passengerPeers;
            GeneralPassengerPrototype nearest = null;
            float nearestDistance = minimumDistance;
            foreach (GeneralPassengerPrototype passenger in passengers)
            {
                if (passenger == null ||
                    passenger == this ||
                    passenger.body == null ||
                    passenger.bodyCollider == null ||
                    !passenger.bodyCollider.enabled)
                {
                    continue;
                }

                float distance = Vector2.Distance(position, passenger.body.position);
                if (distance < nearestDistance)
                {
                    nearest = passenger;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private GeneralPassengerPrototype FindPassengerDirectlyAhead(
            Vector2 desiredDirection,
            float radius)
        {
            RaycastHit2D[] hits = Physics2D.CircleCastAll(
                body.position,
                radius,
                desiredDirection,
                avoidanceLookAhead * 0.85f);
            GeneralPassengerPrototype nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (RaycastHit2D hit in hits)
            {
                GeneralPassengerPrototype passenger = hit.collider == null
                    ? null
                    : hit.collider.GetComponentInParent<GeneralPassengerPrototype>();
                if (passenger == null || passenger == this || hit.distance >= nearestDistance)
                {
                    continue;
                }

                Vector2 offset = passenger.body.position - body.position;
                if (offset.sqrMagnitude <= 0.001f ||
                    Vector2.Dot(offset.normalized, desiredDirection) < 0.55f)
                {
                    continue;
                }

                nearest = passenger;
                nearestDistance = hit.distance;
            }

            return nearest;
        }

        private bool ShouldWaitForPassenger(
            GeneralPassengerPrototype passenger,
            Vector2 desiredDirection)
        {
            if (HasUrgentExitPriority())
            {
                courtesyWaitPassenger = null;
                return false;
            }

            if (passenger == null)
            {
                return false;
            }

            Vector2 otherIntent = passenger.GetMovementIntent();
            if (otherIntent.sqrMagnitude <= 0.01f)
            {
                return false;
            }

            float directionAlignment = Vector2.Dot(
                desiredDirection,
                otherIntent.normalized);
            if (directionAlignment > 0.55f)
            {
                return ShouldBrieflyWaitForPassenger(passenger, 0.35f, 0.75f);
            }

            if (directionAlignment < -0.25f)
            {
                Vector2 meetingPoint =
                    (body.position + passenger.body.position) * 0.5f;
                bool narrowCrowd =
                    CountNearbyMovingPassengers(meetingPoint, 1.15f) >= 3;
                bool yieldsFirst = GetInstanceID() > passenger.GetInstanceID();
                return narrowCrowd && yieldsFirst &&
                       ShouldBrieflyWaitForPassenger(passenger, 0.45f, 0.9f);
            }

            if (courtesyWaitPassenger != null && Time.time < courtesyWaitUntil)
            {
                return courtesyWaitPassenger == passenger;
            }

            if (courtesyWaitPassenger != null)
            {
                courtesyWaitPassenger = null;
                courtesyWaitCooldownUntil = Time.time + Random.Range(0.35f, 0.65f);
            }

            if (Time.time < courtesyWaitCooldownUntil)
            {
                return false;
            }

            float ownPriority = GetMovementPriority();
            float otherPriority = passenger.GetMovementPriority();
            bool shouldYield = ownPriority < otherPriority - 0.05f;
            if (Mathf.Abs(ownPriority - otherPriority) <= 0.05f)
            {
                shouldYield = GetInstanceID() > passenger.GetInstanceID();
            }

            if (!shouldYield)
            {
                return false;
            }

            courtesyWaitPassenger = passenger;
            courtesyWaitUntil = Time.time + Mathf.Lerp(0.3f, 0.85f, courtesy);
            RecordDecision("Wait for " + passenger.name);
            return true;
        }

        private bool ShouldBrieflyWaitForPassenger(
            GeneralPassengerPrototype passenger,
            float minimumDuration,
            float maximumDuration)
        {
            if (courtesyWaitPassenger == passenger)
            {
                if (Time.time < courtesyWaitUntil)
                {
                    return true;
                }

                courtesyWaitPassenger = null;
                courtesyWaitCooldownUntil = Time.time + Random.Range(0.65f, 1f);
                return false;
            }

            if (courtesyWaitPassenger != null ||
                Time.time < courtesyWaitCooldownUntil)
            {
                return false;
            }

            courtesyWaitPassenger = passenger;
            courtesyWaitUntil = Time.time + Random.Range(
                minimumDuration,
                maximumDuration);
            return true;
        }

        private int CountNearbyMovingPassengers(Vector2 position, float radius)
        {
            int count = 0;
            if (passengerPeers == null)
            {
                return count;
            }

            foreach (GeneralPassengerPrototype passenger in passengerPeers)
            {
                if (passenger == null ||
                    passenger.body == null ||
                    passenger.bodyCollider == null ||
                    !passenger.bodyCollider.enabled ||
                    !passenger.IsActivelyMoving())
                {
                    continue;
                }

                if (Vector2.Distance(position, passenger.body.position) <= radius)
                {
                    count++;
                }
            }

            return count;
        }

        private float GetMovementPriority()
        {
            if (state == PassengerState.Exiting ||
                state == PassengerState.PreparingToExit ||
                state == PassengerState.WaitingAtExitDoor)
            {
                return 4.5f;
            }

            if (state == PassengerState.Boarding)
            {
                return 2.6f;
            }

            if (IsSeekingSeat)
            {
                return 2.1f;
            }

            return 1f + (1f - courtesy) * 0.45f;
        }

        private float SelectPassingSide(
            GeneralPassengerPrototype passenger,
            Vector2 desiredDirection)
        {
            if (IsHeadOnApproach(passenger, desiredDirection))
            {
                return -1f;
            }

            return GetInstanceID() % 2 == 0 ? 1f : -1f;
        }

        private static bool IsHeadOnApproach(
            GeneralPassengerPrototype passenger,
            Vector2 desiredDirection)
        {
            if (passenger == null || desiredDirection.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            Vector2 otherIntent = passenger.GetMovementIntent();
            return otherIntent.sqrMagnitude > 0.001f &&
                   Vector2.Dot(desiredDirection, otherIntent.normalized) < -0.25f;
        }

        private bool IsActivelyMoving()
        {
            return state == PassengerState.Boarding ||
                   state == PassengerState.MovingToActivity ||
                   state == PassengerState.PreparingToExit ||
                   state == PassengerState.Exiting;
        }

        private bool IsDoorwayTraffic()
        {
            return state == PassengerState.Boarding ||
                   HasUrgentExitPriority();
        }

        private bool HasUrgentExitPriority()
        {
            return state == PassengerState.PreparingToExit ||
                   state == PassengerState.WaitingAtExitDoor ||
                   state == PassengerState.Exiting;
        }

        private void RememberMovementIntent(Vector2 direction)
        {
            movementIntent = direction;
            movementIntentUntil = Time.time + movementIntentMemory;
        }

        private Vector2 GetMovementIntent()
        {
            return Time.time <= movementIntentUntil
                ? movementIntent
                : Vector2.zero;
        }

        private void AssignMovementPace()
        {
            float paceRoll = Random.value;
            if (paceRoll < leisurelyPassengerChance)
            {
                personalPaceMultiplier = Random.Range(0.45f, 0.62f);
            }
            else if (paceRoll > 1f - hurriedPassengerChance)
            {
                personalPaceMultiplier = Random.Range(0.85f, 1f);
            }
            else
            {
                personalPaceMultiplier = Random.Range(0.63f, 0.82f);
            }

            float exitRoll = Random.value;
            if (exitRoll < 0.2f)
            {
                exitPaceMultiplier = Random.Range(0.75f, 0.9f);
            }
            else if (exitRoll > 0.75f)
            {
                exitPaceMultiplier = Random.Range(1.15f, 1.35f);
            }
            else
            {
                exitPaceMultiplier = Random.Range(0.95f, 1.1f);
            }
        }

        private float GetCurrentMoveSpeed()
        {
            float speed = moveSpeed * personalPaceMultiplier;
            if (initialPlacement && boardingOrder <= 7)
            {
                speed = Mathf.Max(speed, moveSpeed * 0.95f) * 1.15f;
            }

            if (IsSeekingSeat)
            {
                speed *= 1.12f;
            }

            if (state == PassengerState.PreparingToExit ||
                state == PassengerState.Exiting)
            {
                speed *= exitPaceMultiplier;
            }

            return Mathf.Clamp(speed, 0.8f, moveSpeed * 1.25f);
        }

        private Vector2 GetBlockedMovementDirection(Vector2 desiredDirection)
        {
            blockedPushCount++;
            RegisterPassengerBlock();
            blockingPassenger?.ReportObstruction(this, desiredDirection);
            if (HasUrgentExitPriority())
            {
                float urgentBlockedDuration = Time.time - blockedByPassengerSince;
                if (!hasBlockedDetourTarget &&
                    urgentBlockedDuration >= 0.35f &&
                    TryBeginUrgentExitDetour(desiredDirection))
                {
                    return Vector2.zero;
                }

                Vector2 perpendicular = new Vector2(
                    -desiredDirection.y,
                    desiredDirection.x);
                bool tightlyOverlapping = blockingPassenger != null &&
                                          blockingPassenger.body != null &&
                                          Vector2.Distance(
                                              body.position,
                                              blockingPassenger.body.position) <
                                          softOverlapRadius * 0.55f;
                if (tightlyOverlapping)
                {
                    Vector2 awayFromPassenger =
                        body.position - blockingPassenger.body.position;
                    if (awayFromPassenger.sqrMagnitude <= 0.001f)
                    {
                        awayFromPassenger = GetInstanceID() <
                                            blockingPassenger.GetInstanceID()
                            ? Vector2.up
                            : Vector2.down;
                    }

                    squeezeStepCount++;
                    return (awayFromPassenger.normalized * 0.85f +
                            desiredDirection * 0.35f).normalized * 0.9f;
                }

                float exitSide = Mathf.Approximately(avoidanceSide, 0f)
                    ? -1f
                    : avoidanceSide;
                squeezeStepCount++;
                return (desiredDirection * 0.82f +
                        perpendicular * exitSide * 0.38f).normalized * 0.9f;
            }

            bool essentialMovement = state != PassengerState.MovingToActivity;
            float blockedDuration = Time.time - blockedByPassengerSince;

            if (blockedDuration < blockedWaitDuration)
            {
                return Vector2.zero;
            }

            if (blockingPassenger != null &&
                IsHeadOnApproach(blockingPassenger, desiredDirection))
            {
                Vector2 perpendicular = new Vector2(
                    -desiredDirection.y,
                    desiredDirection.x);
                float side = Mathf.Approximately(avoidanceSide, 0f)
                    ? -1f
                    : avoidanceSide;
                squeezeStepCount++;
                return (desiredDirection * 0.42f +
                        perpendicular * side * 0.72f).normalized * 0.7f;
            }

            if (Time.time < blockedSqueezeUntil)
            {
                if (!essentialMovement)
                {
                    return Vector2.zero;
                }

                Vector2 perpendicular = new Vector2(-desiredDirection.y, desiredDirection.x);
                float side = Mathf.Approximately(avoidanceSide, 0f)
                    ? (GetInstanceID() % 2 == 0 ? 1f : -1f)
                    : avoidanceSide;
                Vector2 squeezedDirection = (desiredDirection * 0.45f +
                                              perpendicular * side * 0.55f).normalized;
                squeezeStepCount++;
                float willingness = essentialMovement || squeezeThroughPassengers ? 1f : 0.65f;
                return squeezedDirection * squeezeSpeedMultiplier * willingness;
            }

            if (!blockedDetourAttempted)
            {
                TryBeginBlockedDetour(desiredDirection);
            }

            return Vector2.zero;
        }

        private bool TryBeginUrgentExitDetour(Vector2 desiredDirection)
        {
            GridNavigation2D navigation = FindCurrentNavigationArea();
            if (navigation == null || desiredDirection.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            Vector2 perpendicular = new Vector2(-desiredDirection.y, desiredDirection.x);
            float blockerSide = 0f;
            if (blockingPassenger != null && blockingPassenger.body != null)
            {
                blockerSide = Mathf.Sign(Vector2.Dot(
                    blockingPassenger.body.position - body.position,
                    perpendicular));
            }

            float preferredSide = Mathf.Approximately(blockerSide, 0f)
                ? (Mathf.Approximately(avoidanceSide, 0f) ? -1f : avoidanceSide)
                : -blockerSide;
            Vector2[] candidates =
            {
                body.position + perpendicular * preferredSide * 0.9f - desiredDirection * 0.3f,
                body.position - perpendicular * preferredSide * 0.9f - desiredDirection * 0.3f,
                body.position + perpendicular * preferredSide * 1.2f - desiredDirection * 0.5f,
                body.position - perpendicular * preferredSide * 1.2f - desiredDirection * 0.5f,
                body.position + perpendicular * preferredSide * 0.85f + desiredDirection * 0.35f,
                body.position - perpendicular * preferredSide * 0.85f + desiredDirection * 0.35f
            };

            foreach (Vector2 candidate in candidates)
            {
                if (!navigation.IsWorldWalkable(candidate) ||
                    !IsEmergencyYieldPositionAvailable(candidate))
                {
                    continue;
                }

                blockedDetourTarget = candidate;
                hasBlockedDetourTarget = true;
                blockedDetourAttempted = true;
                blockedGiveUpAt = Time.time + 1.6f;
                avoidanceSide = Mathf.Sign(Vector2.Dot(
                    candidate - body.position,
                    perpendicular));
                blockedDetourCount++;
                ClearPath();
                RecordDecision("Exit blocked -> Go around");
                return true;
            }

            return false;
        }

        private void RegisterPassengerBlock()
        {
            if (blockedByPassengerSince >= 0f)
            {
                return;
            }

            blockedByPassengerSince = Time.time;
            blockedSqueezeUntil = Time.time + Random.Range(
                blockedSqueezeMinimumDuration,
                Mathf.Max(blockedSqueezeMinimumDuration, blockedSqueezeMaximumDuration));
            if (!blockedDetourAttempted)
            {
                blockedGiveUpAt = blockedSqueezeUntil + blockedActivityTimeout;
            }
        }

        private void TryBeginBlockedDetour(Vector2 desiredDirection)
        {
            blockedDetourAttempted = true;
            Vector2 perpendicular = new Vector2(-desiredDirection.y, desiredDirection.x);
            float previousSide = Mathf.Approximately(avoidanceSide, 0f)
                ? (GetInstanceID() % 2 == 0 ? 1f : -1f)
                : avoidanceSide;
            float oppositeSide = -previousSide;
            Vector2[] candidates =
            {
                body.position + perpendicular * oppositeSide * 0.95f - desiredDirection * 0.18f,
                body.position + perpendicular * previousSide * 0.95f - desiredDirection * 0.18f,
                body.position - desiredDirection * 0.72f + perpendicular * oppositeSide * 0.45f
            };

            GridNavigation2D navigation = FindCurrentNavigationArea();
            foreach (Vector2 candidate in candidates)
            {
                if (navigation == null ||
                    !navigation.IsWorldWalkable(candidate) ||
                    !IsCourtesyPositionAvailable(candidate))
                {
                    continue;
                }

                blockedDetourTarget = candidate;
                hasBlockedDetourTarget = true;
                blockedGiveUpAt = Time.time + blockedActivityTimeout;
                blockedDetourCount++;
                ClearPath();
                RecordDecision("Blocked -> Opposite detour");
                return;
            }

            blockedGiveUpAt = Time.time + 0.35f;
        }

        private bool ShouldAbandonBlockedActivity()
        {
            return state == PassengerState.MovingToActivity &&
                   blockedDetourAttempted &&
                   Time.time >= blockedGiveUpAt;
        }

        private void AbandonBlockedActivity()
        {
            abandonedActivityCount++;
            RecordDecision("Blocked -> Reconsider");
            abandonedActivityTarget = activityTarget;
            avoidAbandonedTargetUntil = Time.time + Random.Range(4f, 6f);
            ReleaseCurrentActivity();
            hasBlockedDetourTarget = false;
            blockedDetourAttempted = false;
            ClearPath();
            ResetPassengerBlock();
            ResetActivityProgressTracking();
            initialPlacement = false;
            if (TryChooseNearbyStandingPosition())
            {
                RecordDecision("Blocked -> Stand nearby");
                SetState(PassengerState.MovingToActivity, PassengerBehavior.AisleStanding);
                return;
            }

            behavior = PassengerBehavior.AisleStanding;
            activityTarget = body.position;
            ScheduleNextDecision();
            SetState(PassengerState.Observing, behavior);
        }

        private bool TryChooseNearbyStandingPosition()
        {
            GridNavigation2D navigation = FindCurrentNavigationArea();
            if (navigation == null)
            {
                return false;
            }

            Vector2 selected = body.position;
            float bestScore = float.MaxValue;
            bool found = false;
            for (int attempt = 0; attempt < 28; attempt++)
            {
                Vector2 candidate = GetRandomNearbyStandingPosition(navigation, 1.8f);
                if (!navigation.IsWorldWalkable(candidate) ||
                    !IsStandingPositionAvailable(candidate))
                {
                    continue;
                }

                float score = CalculateCrowdScore(candidate) * 3f +
                              Vector2.Distance(body.position, candidate) * 1.4f;
                if (score < bestScore)
                {
                    bestScore = score;
                    selected = candidate;
                    found = true;
                }
            }

            if (!found)
            {
                return false;
            }

            behavior = PassengerBehavior.AisleStanding;
            activityTarget = selected;
            return true;
        }

        private void ResetPassengerBlock()
        {
            blockedByPassengerSince = -1f;
            blockingPassenger = null;
            if (!hasBlockedDetourTarget)
            {
                blockedSqueezeUntil = 0f;
                blockedGiveUpAt = 0f;
                blockedDetourAttempted = false;
            }
        }

        private Vector2 BuildAvoidanceDirection(
            Vector2 desiredDirection,
            Vector2 perpendicular,
            float side)
        {
            return (desiredDirection +
                    perpendicular * side * avoidanceLateralStrength).normalized;
        }

        private bool HasActorAhead(Vector2 direction, float radius)
        {
            RaycastHit2D[] hits = Physics2D.CircleCastAll(
                body.position,
                radius,
                direction,
                avoidanceLookAhead);

            foreach (RaycastHit2D hit in hits)
            {
                if (IsAvoidableActor(hit.collider))
                {
                    return true;
                }
            }

            return false;
        }

        private float MeasureClearance(Vector2 direction, float radius)
        {
            RaycastHit2D[] hits = Physics2D.CircleCastAll(
                body.position,
                radius,
                direction,
                avoidanceLookAhead);
            float nearestDistance = avoidanceLookAhead;

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null ||
                    hit.collider == bodyCollider ||
                    hit.collider.isTrigger)
                {
                    continue;
                }

                nearestDistance = Mathf.Min(nearestDistance, hit.distance);
            }

            return nearestDistance;
        }

        private bool IsAvoidableActor(Collider2D candidate)
        {
            if (candidate == null ||
                candidate == bodyCollider ||
                candidate.isTrigger)
            {
                return false;
            }

            GeneralPassengerPrototype passenger =
                candidate.GetComponentInParent<GeneralPassengerPrototype>();
            if (passenger != null)
            {
                return passenger != this;
            }

            return candidate.GetComponentInParent<PlayerBoardingCyclePrototype>() != null;
        }

        private bool IsInsideExitWaitingArea()
        {
            return Vector2.Distance(body.position, exitWaitingTarget) <=
                   exitWaitingAcceptanceRadius;
        }

        private void MaintainPersonalSpaceNearExit()
        {
            Vector2 separation = CalculateSeparationVector(waitingPersonalSpaceRadius);
            if (separation.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector2 nextPosition = body.position +
                                   separation.normalized *
                                   waitingSeparationSpeed *
                                   Time.fixedDeltaTime;
            Vector2 fromWaitingTarget = nextPosition - exitWaitingTarget;
            if (fromWaitingTarget.magnitude > maximumWaitingDrift)
            {
                nextPosition = exitWaitingTarget +
                               fromWaitingTarget.normalized * maximumWaitingDrift;
            }

            if (!IsBlockedByNavigationObstacle(nextPosition))
            {
                body.MovePosition(nextPosition);
            }
        }

        private Vector2 CalculateSeparationVector(float radius)
        {
            Collider2D[] overlaps = Physics2D.OverlapCircleAll(body.position, radius);
            var actors = new HashSet<int>();
            Vector2 separation = Vector2.zero;

            foreach (Collider2D overlap in overlaps)
            {
                Transform actor = GetAvoidableActor(overlap);
                if (actor == null || !actors.Add(actor.GetInstanceID()))
                {
                    continue;
                }

                Vector2 away = body.position - (Vector2)actor.position;
                float distance = away.magnitude;
                if (distance <= 0.001f)
                {
                    float side = GetInstanceID() < actor.GetInstanceID() ? -1f : 1f;
                    away = Vector2.right * side;
                    distance = 0.001f;
                }

                float strength = Mathf.Clamp01((radius - distance) / radius);
                separation += away.normalized * strength;
            }

            return separation;
        }

        private Transform GetAvoidableActor(Collider2D candidate)
        {
            if (candidate == null ||
                candidate == bodyCollider ||
                candidate.isTrigger)
            {
                return null;
            }

            GeneralPassengerPrototype passenger =
                candidate.GetComponentInParent<GeneralPassengerPrototype>();
            if (passenger != null && passenger != this)
            {
                return passenger.transform;
            }

            PlayerBoardingCyclePrototype player =
                candidate.GetComponentInParent<PlayerBoardingCyclePrototype>();
            return player != null ? player.transform : null;
        }

        private void IgnoreHardPassengerCollisions()
        {
            GeneralPassengerPrototype[] passengers =
                FindObjectsByType<GeneralPassengerPrototype>(FindObjectsSortMode.None);

            foreach (GeneralPassengerPrototype passenger in passengers)
            {
                if (passenger == null || passenger == this)
                {
                    continue;
                }

                Collider2D[] otherColliders = passenger.GetComponents<Collider2D>();
                foreach (Collider2D otherCollider in otherColliders)
                {
                    if (otherCollider != null)
                    {
                        Physics2D.IgnoreCollision(bodyCollider, otherCollider, true);
                    }
                }
            }
        }

        private void StopResidualMotion()
        {
            if (body == null || body.bodyType != RigidbodyType2D.Dynamic)
            {
                return;
            }

            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        private bool IsBlockedByNavigationObstacle(Vector2 position)
        {
            Collider2D[] overlaps = Physics2D.OverlapCircleAll(
                position,
                GetAvoidanceRadius());

            foreach (Collider2D overlap in overlaps)
            {
                if (overlap.GetComponentInParent<NavigationObstacle>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private float GetAvoidanceRadius()
        {
            if (bodyCollider == null)
            {
                return 0.2f;
            }

            Vector2 extents = bodyCollider.bounds.extents;
            return Mathf.Max(0.12f, Mathf.Min(extents.x, extents.y) * 0.65f);
        }

        private float GetBodyRadius()
        {
            if (bodyCollider == null)
            {
                return 0.28f;
            }

            Vector2 extents = bodyCollider.bounds.extents;
            return Mathf.Max(0.2f, Mathf.Max(extents.x, extents.y));
        }

        private void ClearPath()
        {
            path.Clear();
            pathIndex = 0;
            hasPathTarget = false;
        }

        private void ScheduleNextDecision()
        {
            nextDecisionTime = Time.time + Random.Range(
                minimumDecisionInterval,
                Mathf.Max(minimumDecisionInterval, maximumDecisionInterval));
        }

        private void SetPosition(Vector3 position)
        {
            body.position = position;
        }

        private void SetState(PassengerState nextState, PassengerBehavior nextBehavior)
        {
            state = nextState;
            behavior = nextBehavior;
            UpdateLabel();
        }

        private void RecordDecision(string decision)
        {
            decisionHistory.Add(decision);
            if (decisionHistory.Count > 20)
            {
                decisionHistory.RemoveAt(0);
            }
        }

        private void UpdateLabel()
        {
            if (stateLabel == null)
            {
                return;
            }

            string action = state == PassengerState.Yielding
                ? "Yielding"
                : behavior == PassengerBehavior.None ? state.ToString() : behavior.ToString();
            stateLabel.text = gameObject.name + "\n" + action;
        }
    }
}
