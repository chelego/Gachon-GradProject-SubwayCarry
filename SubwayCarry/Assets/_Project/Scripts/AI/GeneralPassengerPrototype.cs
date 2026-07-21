using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class GeneralPassengerPrototype : MonoBehaviour
    {
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

        private struct DecisionOption
        {
            public PassengerBehavior Behavior;
            public float Weight;
            public bool Stay;

            public DecisionOption(PassengerBehavior behavior, float weight, bool stay = false)
            {
                Behavior = behavior;
                Weight = weight;
                Stay = stay;
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
        [SerializeField, Min(1f)] private float minimumDecisionInterval = 3.5f;
        [SerializeField, Min(1f)] private float maximumDecisionInterval = 6.5f;
        [SerializeField, Min(1f)] private float exitPreparationLeadTime = 7f;
        [SerializeField, Range(0f, 1f)] private float takeNewSeatChance = 0.65f;
        [SerializeField, Min(0.1f)] private float avoidanceLookAhead = 0.8f;
        [SerializeField, Range(0.1f, 0.9f)] private float avoidanceLateralStrength = 0.45f;
        [SerializeField, Min(0.05f)] private float avoidanceSideHoldDuration = 0.35f;
        [SerializeField, Min(0.1f)] private float movementAnticipationTime = 0.45f;
        [SerializeField, Min(0.05f)] private float headOnReactionInterval = 0.12f;
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
        [SerializeField, Min(0.1f)] private float stationaryPassengerClearance = 0.68f;
        [SerializeField, Min(0.1f)] private float courtesyStepDistance = 0.65f;
        [SerializeField, Min(0.01f)] private float courtesyRouteClearanceGain = 0.12f;
        [SerializeField, Min(0.1f)] private float exitIntentCorridorWidth = 0.78f;
        [SerializeField, Min(0.1f)] private float standingPersonalSpaceRadius = 0.62f;
        [SerializeField, Min(0.1f)] private float standingYieldDetectionRadius = 1.45f;
        [SerializeField, Min(0.1f)] private float obstructionYieldThreshold = 0.9f;
        [SerializeField, Min(0.01f)] private float obstructionScoreDecay = 0.35f;
        [SerializeField, Min(0.01f)] private float arrivalDistance = 0.08f;
        [SerializeField] private PassengerState state;
        [SerializeField] private PassengerBehavior behavior;

        private readonly List<Vector3> path = new List<Vector3>();
        private readonly List<string> decisionHistory = new List<string>();
        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private PassengerSeatPrototype reservedSeat;
        private Transform reservedSittingPoint;
        private PassengerActivityPoint reservedActivityPoint;
        private PassengerDoorway exitDoorway;
        private PassengerIntentCoordinator intentCoordinator;
        private GeneralPassengerPrototype[] passengerPeers;
        private Vector2 activityTarget;
        private Vector2 exitWaitingTarget;
        private Vector2 pathTarget;
        private int pathIndex;
        private int boardingStopNumber;
        private int plannedExitStopNumber;
        private int observedAvailableSeatCount;
        private float nextDecisionTime;
        private float nextRepathTime;
        private float avoidanceSide;
        private float avoidanceSideUntil;
        private float nextHeadOnReactionTime;
        private float movementIntentUntil;
        private float blockedByPassengerSince = -1f;
        private float blockedSqueezeUntil;
        private float blockedGiveUpAt;
        private float courtesy;
        private float courtesyWaitUntil;
        private float courtesyWaitCooldownUntil;
        private float nextIntentBroadcastTime;
        private float nextSeatIntentBroadcastTime;
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
        private Vector2 movementIntent;
        private Vector2 courtesyYieldOrigin;
        private Vector2 courtesyYieldTarget;
        private Vector2 platformExitTarget;
        private Vector2 blockedDetourTarget;
        private Vector2 obstructionDirection;
        private Vector2 abandonedActivityTarget;
        private GeneralPassengerPrototype courtesyYieldSource;
        private GeneralPassengerPrototype courtesyWaitPassenger;
        private GeneralPassengerPrototype blockingPassenger;
        private GeneralPassengerPrototype obstructionSource;
        private bool hasPathTarget;
        private bool hasBoardingReservation;
        private bool reachedBoardingDoorCenter;
        private bool hasExitReservation;
        private bool squeezeThroughPassengers;
        private bool reachedCourtesyYieldTarget;
        private bool blockedDetourAttempted;
        private bool hasBlockedDetourTarget;
        private int avoidanceAdjustmentCount;
        private int blockedPushCount;
        private int abandonedActivityCount;
        private int squeezeStepCount;
        private int anticipatedAvoidanceCount;
        private int passingSideCorrectionCount;
        private int blockedDetourCount;
        private int obstructionYieldCount;

        public string CurrentState => state.ToString();
        public string CurrentBehavior => behavior.ToString();
        public string DecisionHistory => string.Join(" > ", decisionHistory);
        public PassengerDoorway BoardingDoorway => boardingDoorway;
        public PassengerDoorway ExitDoorway => exitDoorway;
        public int AvoidanceAdjustmentCount => avoidanceAdjustmentCount;
        public int BlockedPushCount => blockedPushCount;
        public int AbandonedActivityCount => abandonedActivityCount;
        public int SqueezeStepCount => squeezeStepCount;
        public int AnticipatedAvoidanceCount => anticipatedAvoidanceCount;
        public int PassingSideCorrectionCount => passingSideCorrectionCount;
        public int BlockedDetourCount => blockedDetourCount;
        public int ObstructionYieldCount => obstructionYieldCount;
        public float ObstructionScore => obstructionScore;
        public float CurrentMoveSpeed => GetCurrentMoveSpeed();
        public float Courtesy => courtesy;
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
            body.linearDamping = 8f;
            squeezeThroughPassengers = Random.value < squeezeThroughChance;
            courtesy = Random.Range(0.15f, 0.95f);
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

            SetPosition(boardingDoorway.GetBoardingQueuePosition(gameObject));
            SetState(PassengerState.WaitingOutside, PassengerBehavior.None);
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
                    bool isAtQueueFront = MoveDirectly(queuePosition);
                    if (isAtQueueFront && boardingDoorway.TryBeginBoarding(gameObject))
                    {
                        boardingStopNumber = doorCycle.StopNumber;
                        plannedExitStopNumber = boardingStopNumber + Random.Range(1, 4);
                        reachedBoardingDoorCenter = false;
                        SetState(PassengerState.Boarding, PassengerBehavior.None);
                    }
                    break;

                case PassengerState.Boarding:
                    if (!reachedBoardingDoorCenter)
                    {
                        reachedBoardingDoorCenter = MoveDirectly(
                            boardingDoorway.GetBoardingEntryPosition(gameObject));
                    }
                    else if (MoveDirectly(
                                 boardingDoorway.GetBoardingInsidePosition(gameObject)))
                    {
                        ReleaseBoardingReservation();
                        SetState(PassengerState.ChoosingBehavior, PassengerBehavior.None);
                    }
                    break;

                case PassengerState.ChoosingBehavior:
                    if (ShouldPrepareToExit())
                    {
                        BeginExitPreparation();
                    }
                    else
                    {
                        ChooseBehavior();
                    }
                    break;

                case PassengerState.MovingToActivity:
                    if (ShouldPrepareToExit())
                    {
                        BeginExitPreparation();
                    }
                    else
                    {
                        BroadcastSeatIntent();
                        if (MoveUsingPath(activityTarget))
                        {
                            FinishActivityMove();
                        }
                        else if (ShouldAbandonBlockedActivity())
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
                    else if (TryYieldFromObstructionPressure())
                    {
                        break;
                    }
                    else if (TryYieldFromFreeStanding())
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
                        SetState(PassengerState.Exiting, PassengerBehavior.Exiting);
                    }
                    break;

                case PassengerState.Exiting:
                    if (exitDoorway != null && MoveDirectly(exitDoorway.OutsidePoint.position))
                    {
                        BeginLeavingPlatform();
                        ReleaseExitReservation();
                        SetState(PassengerState.LeavingPlatform, PassengerBehavior.None);
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

        private void ChooseBehavior()
        {
            List<DecisionOption> options = BuildDecisionOptions();
            DecisionOption selected = SelectWeightedOption(options);

            if (selected.Stay)
            {
                RecordDecision("Stay " + selected.Behavior);
                ScheduleNextDecision();
                state = PassengerState.Observing;
                UpdateLabel();
                return;
            }

            ReleaseCurrentActivity();

            PassengerBehavior nextBehavior = selected.Behavior;
            bool reserved = TryChooseBehaviorTarget(nextBehavior);

            if (!reserved)
            {
                nextBehavior = PassengerBehavior.AisleStanding;
                if (!TryChooseBehaviorTarget(nextBehavior))
                {
                    SetState(PassengerState.Observing, nextBehavior);
                    ScheduleNextDecision();
                    return;
                }
            }

            behavior = nextBehavior;
            RecordDecision(nextBehavior.ToString());
            ClearPath();
            state = PassengerState.MovingToActivity;
            UpdateLabel();
        }

        private List<DecisionOption> BuildDecisionOptions()
        {
            var options = new List<DecisionOption>();

            if (behavior != PassengerBehavior.None && behavior != PassengerBehavior.Exiting)
            {
                options.Add(new DecisionOption(behavior, ApplyRandomness(GetStayWeight(behavior)), true));
            }

            if (seats != null && seats.Length > 0 && behavior != PassengerBehavior.Seated)
            {
                options.Add(new DecisionOption(PassengerBehavior.Seated, ApplyRandomness(8f)));
            }

            AddPointOption(options, PassengerBehavior.Handhold, 5.5f);
            AddPointOption(options, PassengerBehavior.Leaning, 4.5f);
            AddPointOption(options, PassengerBehavior.DoorStanding, 2.2f);
            AddPointOption(options, PassengerBehavior.AisleStanding, 1.8f);

            if (options.Count == 0)
            {
                options.Add(new DecisionOption(PassengerBehavior.AisleStanding, 1f));
            }

            return options;
        }

        private void AddPointOption(
            List<DecisionOption> options,
            PassengerBehavior candidate,
            float baseWeight)
        {
            if (candidate == behavior || !HasAvailablePoint(candidate))
            {
                return;
            }

            options.Add(new DecisionOption(candidate, ApplyRandomness(baseWeight)));
        }

        private float ApplyRandomness(float baseWeight)
        {
            return baseWeight * Random.Range(0.55f, 1.75f);
        }

        private static float GetStayWeight(PassengerBehavior currentBehavior)
        {
            switch (currentBehavior)
            {
                case PassengerBehavior.Seated:
                    return 11f;
                case PassengerBehavior.Handhold:
                    return 7.5f;
                case PassengerBehavior.Leaning:
                    return 6.5f;
                case PassengerBehavior.DoorStanding:
                    return 3f;
                case PassengerBehavior.AisleStanding:
                    return 2.5f;
                default:
                    return 1f;
            }
        }

        private static DecisionOption SelectWeightedOption(List<DecisionOption> options)
        {
            float totalWeight = 0f;
            foreach (DecisionOption option in options)
            {
                totalWeight += Mathf.Max(0.01f, option.Weight);
            }

            float roll = Random.Range(0f, totalWeight);
            foreach (DecisionOption option in options)
            {
                roll -= Mathf.Max(0.01f, option.Weight);
                if (roll <= 0f)
                {
                    return option;
                }
            }

            return options[options.Count - 1];
        }

        private bool TryReserveSeat()
        {
            var candidates = new List<PassengerSeatPrototype>();
            foreach (PassengerSeatPrototype seat in seats)
            {
                if (seat != null)
                {
                    candidates.Add(seat);
                }
            }

            while (candidates.Count > 0)
            {
                int selectedIndex = 0;
                float bestScore = float.MaxValue;

                for (int i = 0; i < candidates.Count; i++)
                {
                    float distance = Vector2.Distance(body.position, candidates[i].transform.position);
                    float score = distance * Random.Range(0.65f, 1.45f);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        selectedIndex = i;
                    }
                }

                PassengerSeatPrototype seat = candidates[selectedIndex];
                candidates.RemoveAt(selectedIndex);
                if (seat == null ||
                    IsRecentlyAbandonedTarget(seat.transform.position, 1.4f) ||
                    !seat.TryReserve(gameObject, out Transform sittingPoint))
                {
                    continue;
                }

                reservedSeat = seat;
                reservedSittingPoint = sittingPoint;
                activityTarget = seat.GetApproachPosition(sittingPoint);
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
                if (IsRecentlyAbandonedTarget(selectedPoint.transform.position, 0.8f) ||
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

        private bool TryChooseBehaviorTarget(PassengerBehavior selectedBehavior)
        {
            if (selectedBehavior == PassengerBehavior.Seated)
            {
                return TryReserveSeat();
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
            return inside +
                   inward * Random.Range(0.15f, 0.85f) +
                   lateral * Random.Range(-0.72f, 0.72f);
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
            if (IsBlockedByNavigationObstacle(position))
            {
                return false;
            }

            GeneralPassengerPrototype[] passengers = passengerPeers;
            foreach (GeneralPassengerPrototype passenger in passengers)
            {
                if (passenger == null || passenger == this || passenger.body == null)
                {
                    continue;
                }

                if (Vector2.Distance(position, passenger.body.position) < standingPersonalSpaceRadius)
                {
                    return false;
                }

                bool hasFreeStandingTarget = IsFreeStandingBehavior(passenger.behavior) &&
                                             (passenger.state == PassengerState.MovingToActivity ||
                                              passenger.state == PassengerState.Observing ||
                                              passenger.state == PassengerState.Yielding);
                if (hasFreeStandingTarget &&
                    Vector2.Distance(position, passenger.activityTarget) < standingPersonalSpaceRadius)
                {
                    return false;
                }
            }

            return true;
        }

        private PassengerActivityPoint SelectActivityPoint(List<PassengerActivityPoint> candidates)
        {
            float totalWeight = 0f;
            var weights = new float[candidates.Count];

            for (int i = 0; i < candidates.Count; i++)
            {
                float distance = Vector2.Distance(body.position, candidates[i].transform.position);
                weights[i] = Random.Range(0.6f, 1.6f) / (1f + distance * 0.5f);
                totalWeight += weights[i];
            }

            float roll = Random.Range(0f, totalWeight);
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= weights[i];
                if (roll <= 0f)
                {
                    return candidates[i];
                }
            }

            return candidates[candidates.Count - 1];
        }

        private bool HasAvailablePoint(PassengerBehavior candidate)
        {
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
            if (behavior == PassengerBehavior.Seated)
            {
                SitDown();
            }

            observedAvailableSeatCount = CountAvailableSeats();
            ScheduleNextDecision();
            SetState(PassengerState.Observing, behavior);
        }

        private void EvaluateReasonedBehaviorChange()
        {
            int availableSeatCount = CountAvailableSeats();
            bool newSeatBecameAvailable = availableSeatCount > observedAvailableSeatCount;
            observedAvailableSeatCount = availableSeatCount;

            if (behavior != PassengerBehavior.Seated &&
                newSeatBecameAvailable &&
                Random.value <= takeNewSeatChance &&
                TryReserveSeat())
            {
                if (reservedActivityPoint != null)
                {
                    reservedActivityPoint.Release(gameObject);
                    reservedActivityPoint = null;
                }

                behavior = PassengerBehavior.Seated;
                RecordDecision("New seat available -> Seated");
                ClearPath();
                state = PassengerState.MovingToActivity;
                UpdateLabel();
                return;
            }

            ScheduleNextDecision();
            UpdateLabel();
        }

        private int CountAvailableSeats()
        {
            int count = 0;

            if (seats == null)
            {
                return count;
            }

            foreach (PassengerSeatPrototype seat in seats)
            {
                if (seat != null)
                {
                    count += seat.AvailableCount;
                }
            }

            return count;
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
                behavior == PassengerBehavior.Seated ||
                courtesy < 0.28f ||
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
                behavior == PassengerBehavior.Seated ||
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
            courtesyYieldSource = source;
            courtesyYieldOrigin = body.position;
            courtesyYieldTarget = yieldTarget;
            courtesyYieldCanReturnAt = Time.time + Random.Range(0.4f, 0.65f);
            courtesyYieldUntil = Time.time + Random.Range(0.9f, 1.3f);
            reachedCourtesyYieldTarget = false;
            ClearPath();
            RecordDecision(
                behavior == PassengerBehavior.Handhold
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
                position - routeDirection * courtesyStepDistance * 0.75f
            };

            float bestScore = float.MinValue;
            yieldTarget = position;
            foreach (Vector2 candidate in candidates)
            {
                if (!IsCourtesyPositionAvailable(candidate))
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

        private bool TryYieldFromFreeStanding()
        {
            if (!IsFreeStandingBehavior(behavior) ||
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

            return TryYieldFromLocalCrowding(passengers);
        }

        private bool TryYieldFromLocalCrowding(GeneralPassengerPrototype[] passengers)
        {
            Vector2 crowdCenter = Vector2.zero;
            int nearbyCount = 0;
            foreach (GeneralPassengerPrototype passenger in passengers)
            {
                if (passenger == null || passenger == this || passenger.body == null)
                {
                    continue;
                }

                float distance = Vector2.Distance(body.position, passenger.body.position);
                if (distance <= standingPersonalSpaceRadius * 1.65f)
                {
                    crowdCenter += passenger.body.position;
                    nearbyCount++;
                }
            }

            if (nearbyCount < 3)
            {
                return false;
            }

            crowdCenter /= nearbyCount;
            Vector2 away = body.position - crowdCenter;
            if (away.sqrMagnitude <= 0.001f)
            {
                away = Random.insideUnitCircle.normalized;
            }

            Vector2 target = body.position + away.normalized * courtesyStepDistance;
            if (!IsCourtesyPositionAvailable(target))
            {
                return false;
            }

            BeginCourtesyYield(null, target);
            return true;
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
                state != PassengerState.Observing ||
                behavior == PassengerBehavior.Seated ||
                behavior == PassengerBehavior.Exiting ||
                Time.time < obstructionYieldCooldownUntil ||
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

        private bool MoveUsingPath(Vector2 destination)
        {
            if (Vector2.Distance(body.position, destination) <= arrivalDistance)
            {
                return true;
            }

            if (hasBlockedDetourTarget)
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

            if ((state == PassengerState.MovingToActivity ||
                 state == PassengerState.PreparingToExit) &&
                CanTravelDirectly(destination))
            {
                ClearPath();
                return MoveDirectly(destination);
            }

            if (!hasPathTarget ||
                Vector2.Distance(pathTarget, destination) > 0.05f ||
                Time.time >= nextRepathTime)
            {
                RebuildPath(destination);
            }

            if (path.Count == 0 || pathIndex >= path.Count)
            {
                return MoveDirectly(destination);
            }

            Vector2 waypoint = path[pathIndex];
            MoveBodyTowards(waypoint);

            if (Vector2.Distance(body.position, waypoint) <= arrivalDistance + 0.04f)
            {
                pathIndex++;
            }

            return Vector2.Distance(body.position, destination) <= arrivalDistance;
        }

        private void RebuildPath(Vector2 destination)
        {
            path.Clear();
            List<Vector2> stationaryPassengers = CollectStationaryPassengerPositions(destination);
            Vector2 start = body.position;

            if (ShouldUseAisleRoute(destination, stationaryPassengers))
            {
                float aisleY = GetAisleCenterY() + GetPreferredAisleOffset();
                Vector2 aisleEntry = new Vector2(start.x, aisleY);
                float horizontalDirection = Mathf.Sign(destination.x - start.x);
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
            if (shortestPath == null || shortestPath.Count == 0)
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
                        point => IsDynamicallyWalkable(
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

        private List<Vector2> CollectStationaryPassengerPositions(Vector2 destination)
        {
            var positions = new List<Vector2>();
            GeneralPassengerPrototype[] passengers = passengerPeers;
            foreach (GeneralPassengerPrototype passenger in passengers)
            {
                if (passenger == null ||
                    passenger == this ||
                    passenger.bodyCollider == null ||
                    !passenger.bodyCollider.enabled ||
                    Vector2.Distance(passenger.body.position, destination) <= 0.5f)
                {
                    continue;
                }

                bool stationary = passenger.state == PassengerState.Observing ||
                                  passenger.state == PassengerState.WaitingAtExitDoor ||
                                  passenger.state == PassengerState.Yielding ||
                                  passenger.GetMovementIntent().sqrMagnitude <= 0.01f;
                if (stationary)
                {
                    positions.Add(passenger.body.position);
                }
            }

            return positions;
        }

        private bool IsDynamicallyWalkable(
            Vector2 point,
            Vector2 start,
            Vector2 destination,
            List<Vector2> stationaryPassengers)
        {
            if (Vector2.Distance(point, start) <= stationaryPassengerClearance ||
                Vector2.Distance(point, destination) <= stationaryPassengerClearance + 0.15f)
            {
                return true;
            }

            foreach (Vector2 passengerPosition in stationaryPassengers)
            {
                if (Vector2.Distance(point, passengerPosition) < stationaryPassengerClearance)
                {
                    return false;
                }
            }

            return true;
        }

        private bool ShouldUseAisleRoute(
            Vector2 destination,
            List<Vector2> stationaryPassengers)
        {
            float aisleY = GetAisleCenterY();
            bool sideDestination = Mathf.Abs(destination.y - aisleY) > 0.95f;
            bool meaningfulTravel = Vector2.Distance(body.position, destination) > 1.2f;
            if (sideDestination && meaningfulTravel)
            {
                return true;
            }

            foreach (Vector2 passengerPosition in stationaryPassengers)
            {
                if (DistancePointToSegment(
                        passengerPosition,
                        body.position,
                        destination) < stationaryPassengerClearance)
                {
                    return true;
                }
            }

            return false;
        }

        private float GetAisleCenterY()
        {
            return mapRoot != null ? mapRoot.position.y : 0f;
        }

        private float GetPreferredAisleOffset()
        {
            int lane = Mathf.Abs(GetInstanceID()) % 3;
            return lane == 0 ? -0.42f : lane == 1 ? 0f : 0.42f;
        }

        private bool CanTravelDirectly(Vector2 destination)
        {
            Vector2 direction = destination - body.position;
            float distance = direction.magnitude;
            if (distance <= 0.1f)
            {
                return true;
            }

            RaycastHit2D[] hits = Physics2D.CircleCastAll(
                body.position,
                GetAvoidanceRadius() * 0.8f,
                direction.normalized,
                distance);
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null ||
                    hit.collider == bodyCollider ||
                    hit.collider.isTrigger)
                {
                    continue;
                }

                GeneralPassengerPrototype passenger =
                    hit.collider.GetComponentInParent<GeneralPassengerPrototype>();
                if (passenger != null && passenger != this)
                {
                    return false;
                }

                if (hit.collider.GetComponentInParent<NavigationObstacle>() != null)
                {
                    return false;
                }
            }

            return true;
        }

        private bool MoveDirectly(Vector2 destination)
        {
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
            RememberMovementIntent(direction);
            body.MovePosition(body.position + direction * step);
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

            Vector2 nextPosition = body.position + movementDirection * step;
            GeneralPassengerPrototype passengerAtNextPosition =
                FindPassengerTooCloseTo(nextPosition, softOverlapRadius * 0.82f);
            if (passengerAtNextPosition != null)
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
                if (FindPassengerTooCloseTo(
                        nextPosition,
                        softOverlapRadius * 0.72f) != null)
                {
                    RememberMovementIntent(Vector2.zero);
                    return;
                }
            }

            body.MovePosition(nextPosition);
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

            GeneralPassengerPrototype approachingPassenger =
                FindApproachingPassenger(desiredDirection, radius);
            if (!HasActorAhead(desiredDirection, radius) && approachingPassenger == null)
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
                approachingPassenger,
                desiredDirection);
            bool passingConflict = approachingPassenger != null &&
                                   !headOnApproach &&
                                   Time.time >= nextHeadOnReactionTime &&
                                   IsPassingSideConflict(
                                       approachingPassenger,
                                       perpendicular,
                                       selectedSide);

            if (Time.time >= avoidanceSideUntil ||
                Mathf.Approximately(selectedSide, 0f) ||
                passingConflict)
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

                if (approachingPassenger != null)
                {
                    selectedSide = SelectPassingSide(
                        approachingPassenger,
                        desiredDirection,
                        perpendicular);

                    float selectedClearance = selectedSide > 0f
                        ? leftClearance
                        : rightClearance;
                    float alternateClearance = selectedSide > 0f
                        ? rightClearance
                        : leftClearance;
                    if (!headOnApproach &&
                        alternateClearance > selectedClearance + 0.08f)
                    {
                        selectedSide *= -1f;
                    }

                    anticipatedAvoidanceCount++;
                    if (passingConflict)
                    {
                        passingSideCorrectionCount++;
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
                float sideHoldDuration = approachingPassenger != null
                    ? headOnReactionInterval
                    : avoidanceSideHoldDuration;
                avoidanceSideUntil = Time.time + sideHoldDuration;
                nextHeadOnReactionTime = Time.time + headOnReactionInterval;
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
            if (courtesyWaitPassenger != null && Time.time < courtesyWaitUntil)
            {
                return courtesyWaitPassenger == passenger;
            }

            if (courtesyWaitPassenger != null)
            {
                courtesyWaitPassenger = null;
                courtesyWaitCooldownUntil = Time.time + Random.Range(0.35f, 0.65f);
            }

            if (passenger == null ||
                Time.time < courtesyWaitCooldownUntil ||
                passenger.GetMovementIntent().sqrMagnitude <= 0.01f)
            {
                return false;
            }

            if (IsHeadOnApproach(passenger, desiredDirection))
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

        private float GetMovementPriority()
        {
            if (state == PassengerState.Exiting ||
                state == PassengerState.PreparingToExit ||
                state == PassengerState.WaitingAtExitDoor)
            {
                return 3f;
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

        private GeneralPassengerPrototype FindApproachingPassenger(
            Vector2 desiredDirection,
            float radius)
        {
            float currentSpeed = GetCurrentMoveSpeed();
            float detectionDistance = avoidanceLookAhead +
                                      currentSpeed * movementAnticipationTime;
            RaycastHit2D[] hits = Physics2D.CircleCastAll(
                body.position,
                radius,
                desiredDirection,
                detectionDistance);
            GeneralPassengerPrototype nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (RaycastHit2D hit in hits)
            {
                GeneralPassengerPrototype passenger = hit.collider == null
                    ? null
                    : hit.collider.GetComponentInParent<GeneralPassengerPrototype>();
                if (passenger == null || passenger == this)
                {
                    continue;
                }

                Vector2 otherIntent = passenger.GetMovementIntent();
                if (otherIntent.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                Vector2 currentOffset =
                    (Vector2)passenger.transform.position - body.position;
                if (Vector2.Dot(currentOffset.normalized, desiredDirection) < 0.2f)
                {
                    continue;
                }

                Vector2 futureSelf = body.position +
                                     desiredDirection * currentSpeed * movementAnticipationTime;
                Vector2 futureOther = (Vector2)passenger.transform.position +
                                      otherIntent * passenger.GetCurrentMoveSpeed() *
                                      movementAnticipationTime;
                float currentDistance = currentOffset.magnitude;
                float futureDistance = Vector2.Distance(futureSelf, futureOther);
                if (futureDistance >= currentDistance ||
                    futureDistance > softOverlapRadius + radius)
                {
                    continue;
                }

                if (hit.distance < nearestDistance)
                {
                    nearest = passenger;
                    nearestDistance = hit.distance;
                }
            }

            return nearest;
        }

        private float SelectPassingSide(
            GeneralPassengerPrototype passenger,
            Vector2 desiredDirection,
            Vector2 perpendicular)
        {
            Vector2 otherIntent = passenger.GetMovementIntent();
            float otherLateralMovement = Vector2.Dot(otherIntent, perpendicular);
            if (Mathf.Abs(otherLateralMovement) > 0.08f)
            {
                return -Mathf.Sign(otherLateralMovement);
            }

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

        private bool IsPassingSideConflict(
            GeneralPassengerPrototype passenger,
            Vector2 perpendicular,
            float selectedSide)
        {
            if (Mathf.Approximately(selectedSide, 0f))
            {
                return false;
            }

            Vector2 selectedWorldDirection = perpendicular * selectedSide;
            bool movingToSameWorldSide = Vector2.Dot(
                passenger.GetMovementIntent(),
                selectedWorldDirection) > 0.08f;
            return movingToSameWorldSide &&
                   GetInstanceID() > passenger.GetInstanceID();
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
            bool essentialMovement = state != PassengerState.MovingToActivity;
            float blockedDuration = Time.time - blockedByPassengerSince;

            if (blockedDuration < blockedWaitDuration)
            {
                return Vector2.zero;
            }

            if (Time.time < blockedSqueezeUntil)
            {
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
            SetState(PassengerState.ChoosingBehavior, PassengerBehavior.None);
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

                Collider2D otherCollider = passenger.GetComponent<Collider2D>();
                if (otherCollider != null)
                {
                    Physics2D.IgnoreCollision(bodyCollider, otherCollider, true);
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
