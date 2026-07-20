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
            PreparingToExit,
            WaitingAtExitDoor,
            Exiting,
            WaitingAfterExit
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
        [SerializeField, Min(0.1f)] private float blockedActivityTimeout = 1.25f;
        [SerializeField, Range(0f, 1f)] private float squeezeThroughChance = 0.35f;
        [SerializeField, Range(0.1f, 0.8f)] private float squeezeSpeedMultiplier = 0.4f;
        [SerializeField, Min(0.1f)] private float softOverlapRadius = 0.65f;
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
        private Vector2 activityTarget;
        private Vector2 exitWaitingTarget;
        private Vector2 pathTarget;
        private int pathIndex;
        private int boardingStopNumber;
        private int observedAvailableSeatCount;
        private float nextDecisionTime;
        private float nextRepathTime;
        private float avoidanceSide;
        private float avoidanceSideUntil;
        private float nextHeadOnReactionTime;
        private float movementIntentUntil;
        private float blockedByPassengerSince = -1f;
        private float personalPaceMultiplier = 1f;
        private float exitPaceMultiplier = 1f;
        private Vector2 movementIntent;
        private bool hasPathTarget;
        private bool hasBoardingReservation;
        private bool hasExitReservation;
        private bool squeezeThroughPassengers;
        private int avoidanceAdjustmentCount;
        private int blockedPushCount;
        private int abandonedActivityCount;
        private int squeezeStepCount;
        private int anticipatedAvoidanceCount;
        private int passingSideCorrectionCount;

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
        public float CurrentMoveSpeed => GetCurrentMoveSpeed();

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
            AssignMovementPace();
            IgnoreHardPassengerCollisions();
            RefreshMapReferences();

            if (boardingDoorway == null || !boardingDoorway.IsUsable)
            {
                enabled = false;
                Debug.LogError("[Passenger AI] No usable PassengerDoorway was found.", this);
                return;
            }

            SetPosition(boardingDoorway.OutsidePoint.position);
            SetState(PassengerState.WaitingOutside, PassengerBehavior.None);
        }

        private void FixedUpdate()
        {
            StopResidualMotion();

            if (boardingDoorway == null || doorCycle == null)
            {
                return;
            }

            switch (state)
            {
                case PassengerState.WaitingOutside:
                    if (boardingDoorway.Door.IsOpen)
                    {
                        boardingStopNumber = doorCycle.StopNumber;
                        SetState(PassengerState.Boarding, PassengerBehavior.None);
                    }
                    break;

                case PassengerState.Boarding:
                    if (MoveDirectly(boardingDoorway.InsidePoint.position))
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
                    else if (MoveUsingPath(activityTarget))
                    {
                        FinishActivityMove();
                    }
                    else if (ShouldAbandonBlockedActivity())
                    {
                        AbandonBlockedActivity();
                    }
                    break;

                case PassengerState.Observing:
                    if (ShouldPrepareToExit())
                    {
                        BeginExitPreparation();
                    }
                    else if (Time.time >= nextDecisionTime)
                    {
                        EvaluateReasonedBehaviorChange();
                    }
                    break;

                case PassengerState.PreparingToExit:
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
                        ReleaseExitReservation();
                        SetState(PassengerState.WaitingAfterExit, PassengerBehavior.None);
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

        private PassengerDoorway ReserveRandomBoardingDoorway()
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
            bool reserved = nextBehavior == PassengerBehavior.Seated
                ? TryReserveSeat()
                : TryReserveActivityPoint(nextBehavior);

            if (!reserved)
            {
                nextBehavior = PassengerBehavior.AisleStanding;
                if (!TryReserveActivityPoint(nextBehavior))
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
                if (seat == null || !seat.TryReserve(gameObject, out Transform sittingPoint))
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
                if (!selectedPoint.TryReserve(gameObject))
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
                   doorCycle.TravelTimeRemaining <= exitPreparationLeadTime;
        }

        private bool MoveUsingPath(Vector2 destination)
        {
            if (Vector2.Distance(body.position, destination) <= arrivalDistance)
            {
                return true;
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

            List<Vector3> shortestPath = null;
            if (navigationAreas != null)
            {
                foreach (GridNavigation2D navigation in navigationAreas)
                {
                    if (navigation == null)
                    {
                        continue;
                    }

                    List<Vector3> candidatePath = navigation.FindWorldPath(body.position, destination);
                    if (candidatePath.Count == 0 ||
                        (shortestPath != null && candidatePath.Count >= shortestPath.Count))
                    {
                        continue;
                    }

                    shortestPath = candidatePath;
                }
            }

            if (shortestPath != null)
            {
                path.AddRange(shortestPath);
            }

            pathIndex = path.Count > 1 ? 1 : 0;
            pathTarget = destination;
            hasPathTarget = true;
            nextRepathTime = Time.time + 1.5f;
        }

        private bool MoveDirectly(Vector2 destination)
        {
            MoveBodyTowards(destination);
            return Vector2.Distance(body.position, destination) <= arrivalDistance;
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
            body.MovePosition(nextPosition);
        }

        private Vector2 GetLocallyAvoidedDirection(Vector2 desiredDirection)
        {
            float radius = GetAvoidanceRadius();
            GeneralPassengerPrototype approachingPassenger =
                FindApproachingPassenger(desiredDirection, radius);
            if (!HasActorAhead(desiredDirection, radius) && approachingPassenger == null)
            {
                ResetPassengerBlock();
                return desiredDirection;
            }

            Vector2 perpendicular = new Vector2(-desiredDirection.y, desiredDirection.x);
            float selectedSide = avoidanceSide;
            bool passingConflict = approachingPassenger != null &&
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
                    if (alternateClearance > selectedClearance + 0.08f)
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

            ResetPassengerBlock();
            avoidanceAdjustmentCount++;
            return avoidedDirection;
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

            bool headOn = Vector2.Dot(
                desiredDirection,
                otherIntent.normalized) < -0.25f;
            if (headOn)
            {
                int pairKey = GetInstanceID() + passenger.GetInstanceID();
                return pairKey % 2 == 0 ? 1f : -1f;
            }

            return GetInstanceID() % 2 == 0 ? 1f : -1f;
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
            if (blockedByPassengerSince < 0f)
            {
                blockedByPassengerSince = Time.time;
            }

            Vector2 separation = CalculateSeparationVector(softOverlapRadius);
            bool essentialMovement = state != PassengerState.MovingToActivity;
            bool canSqueeze = essentialMovement || squeezeThroughPassengers;
            float blockedDuration = Time.time - blockedByPassengerSince;

            if (canSqueeze && blockedDuration >= blockedActivityTimeout * 0.5f)
            {
                Vector2 squeezedDirection = desiredDirection;
                if (separation.sqrMagnitude > 0.0001f)
                {
                    squeezedDirection = (desiredDirection +
                                         separation.normalized * 0.35f).normalized;
                }

                squeezeStepCount++;
                return squeezedDirection * squeezeSpeedMultiplier;
            }

            return separation.sqrMagnitude > 0.0001f
                ? separation.normalized * 0.2f
                : Vector2.zero;
        }

        private bool ShouldAbandonBlockedActivity()
        {
            return state == PassengerState.MovingToActivity &&
                   !squeezeThroughPassengers &&
                   blockedByPassengerSince >= 0f &&
                   Time.time - blockedByPassengerSince >= blockedActivityTimeout;
        }

        private void AbandonBlockedActivity()
        {
            abandonedActivityCount++;
            RecordDecision("Blocked -> Reconsider");
            ReleaseCurrentActivity();
            ClearPath();
            ResetPassengerBlock();
            SetState(PassengerState.ChoosingBehavior, PassengerBehavior.None);
        }

        private void ResetPassengerBlock()
        {
            blockedByPassengerSince = -1f;
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

            string action = behavior == PassengerBehavior.None ? state.ToString() : behavior.ToString();
            stateLabel.text = gameObject.name + "\n" + action;
        }
    }
}
