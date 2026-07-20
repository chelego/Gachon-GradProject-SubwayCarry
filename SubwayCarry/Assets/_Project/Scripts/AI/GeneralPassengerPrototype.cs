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
        [SerializeField, Range(0f, 1f)] private float differentDoorPreference = 0.75f;
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
        private Vector2 pathTarget;
        private int pathIndex;
        private int boardingStopNumber;
        private float nextDecisionTime;
        private float nextRepathTime;
        private bool hasPathTarget;

        public string CurrentState => state.ToString();
        public string CurrentBehavior => behavior.ToString();
        public string DecisionHistory => string.Join(" > ", decisionHistory);
        public PassengerDoorway BoardingDoorway => boardingDoorway;
        public PassengerDoorway ExitDoorway => exitDoorway;

        public void Configure(
            PassengerDoorway initialBoardingDoorway,
            TrainDoorCyclePrototype cycle,
            Transform passengerMapRoot,
            TextMesh label)
        {
            boardingDoorway = initialBoardingDoorway;
            doorCycle = cycle;
            mapRoot = passengerMapRoot;
            stateLabel = label;
        }

        private void Start()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
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
                    break;

                case PassengerState.Observing:
                    if (ShouldPrepareToExit())
                    {
                        BeginExitPreparation();
                    }
                    else if (Time.time >= nextDecisionTime)
                    {
                        state = PassengerState.ChoosingBehavior;
                    }
                    break;

                case PassengerState.PreparingToExit:
                    if (exitDoorway != null && MoveUsingPath(exitDoorway.InsidePoint.position))
                    {
                        SetState(PassengerState.WaitingAtExitDoor, PassengerBehavior.Exiting);
                    }
                    break;

                case PassengerState.WaitingAtExitDoor:
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

            if (boardingDoorway == null ||
                !boardingDoorway.IsUsable ||
                !ContainsDoorway(boardingDoorway))
            {
                boardingDoorway = FindNearestUsableDoorway(transform.position);
            }
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
                activityTarget = selectedPoint.transform.position;
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

            ScheduleNextDecision();
            SetState(PassengerState.Observing, behavior);
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

            SetState(PassengerState.PreparingToExit, PassengerBehavior.Exiting);
        }

        private PassengerDoorway SelectExitDoorway()
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

            if (candidates.Count > 1 && Random.value < differentDoorPreference)
            {
                candidates.Remove(boardingDoorway);
            }

            if (candidates.Count == 0)
            {
                return boardingDoorway;
            }

            float totalWeight = 0f;
            var weights = new float[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                float distance = Vector2.Distance(body.position, candidates[i].InsidePoint.position);
                weights[i] = Random.Range(0.7f, 1.3f) / (1f + distance * 0.15f);
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

        private void ReleaseCurrentActivity()
        {
            if (reservedSeat != null)
            {
                StandUp();
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
            Vector2 nextPosition = Vector2.MoveTowards(
                body.position,
                destination,
                moveSpeed * Time.fixedDeltaTime);
            body.MovePosition(nextPosition);
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
            stateLabel.text = "Passenger 1\n" + action;
        }
    }
}
