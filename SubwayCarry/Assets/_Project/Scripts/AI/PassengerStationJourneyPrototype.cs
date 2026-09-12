using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(GeneralPassengerPrototype))]
    public sealed class PassengerStationJourneyPrototype : MonoBehaviour
    {
        [SerializeField] private PassengerStationRoutePrototype route;
        [SerializeField] private PassengerPostAlightingPlan postAlightingPlan;
        [SerializeField, Min(0.1f)] private float stationMoveSpeed = 2.1f;
        [SerializeField, Min(0.05f)] private float repathInterval = 0.35f;
        [SerializeField, Min(0.1f)] private float avoidanceRadius = 0.72f;
        [SerializeField, Range(0f, 1f)] private float avoidanceStrength = 0.48f;

        private readonly List<Vector3> path = new List<Vector3>();
        private readonly List<GeneralPassengerPrototype> nearbyPassengers =
            new List<GeneralPassengerPrototype>();

        private Rigidbody2D body;
        private GeneralPassengerPrototype passenger;
        private PassengerJourneyWaypoint[] activeRoute;
        private PassengerJourneyWaypoint reservedWaypoint;
        private PassengerJourneyLeg currentLeg;
        private Vector2 pathTarget;
        private int waypointIndex;
        private int pathIndex;
        private int transferCount;
        private float dwellUntil = -1f;
        private float nextRepathTime;
        private bool hasPathTarget;

        public bool IsControlling { get; private set; }
        public PassengerJourneyLeg CurrentLeg => currentLeg;
        public int TransferCount => transferCount;
        public string CurrentStageName
        {
            get
            {
                PassengerJourneyWaypoint waypoint = CurrentWaypoint;
                return waypoint != null
                    ? waypoint.Action.ToString()
                    : currentLeg.ToString();
            }
        }

        private PassengerJourneyWaypoint CurrentWaypoint =>
            activeRoute != null && waypointIndex >= 0 && waypointIndex < activeRoute.Length
                ? activeRoute[waypointIndex]
                : null;

        public void Configure(
            PassengerStationRoutePrototype stationRoute,
            PassengerPostAlightingPlan alightingPlan,
            float movementSpeed = 2.1f)
        {
            route = stationRoute;
            postAlightingPlan = alightingPlan;
            stationMoveSpeed = Mathf.Max(0.1f, movementSpeed);
        }

        public bool TryBeginPreBoarding(GeneralPassengerPrototype owner)
        {
            EnsureReferences(owner);
            return BeginLeg(PassengerJourneyLeg.BoardingRoute, route?.BoardingRoute);
        }

        public bool TryBeginPostAlighting(GeneralPassengerPrototype owner)
        {
            EnsureReferences(owner);
            bool shouldTransfer =
                postAlightingPlan == PassengerPostAlightingPlan.TransferOnceThenExit &&
                transferCount == 0;
            PassengerJourneyWaypoint[] stationExit =
                transferCount > 0 &&
                route?.PostTransferExitRoute != null &&
                route.PostTransferExitRoute.Length > 0
                    ? route.PostTransferExitRoute
                    : route?.ExitRoute;
            return shouldTransfer
                ? BeginLeg(PassengerJourneyLeg.TransferRoute, route?.TransferRoute)
                : BeginLeg(PassengerJourneyLeg.ExitRoute, stationExit);
        }

        private void Awake()
        {
            EnsureReferences(null);
        }

        private void OnDisable()
        {
            reservedWaypoint?.Cancel(this);
            reservedWaypoint = null;
        }

        private void FixedUpdate()
        {
            if (!IsControlling || passenger == null || body == null)
            {
                return;
            }

            PassengerJourneyWaypoint waypoint = CurrentWaypoint;
            if (waypoint == null)
            {
                CompleteLeg();
                return;
            }

            passenger.SetStationJourneyStage(currentLeg, waypoint.Action);
            if (reservedWaypoint != waypoint)
            {
                reservedWaypoint?.Cancel(this);
                reservedWaypoint = null;
                dwellUntil = -1f;
                ClearPath();
            }

            if (!waypoint.RequestAccess(this))
            {
                MoveTowards(waypoint.GetQueuePosition(this), 0.2f);
                return;
            }

            reservedWaypoint = waypoint;
            if (!MoveTowards(waypoint.transform.position, waypoint.AcceptanceRadius))
            {
                return;
            }

            body.linearVelocity = Vector2.zero;
            if (dwellUntil < 0f)
            {
                dwellUntil = Time.time + waypoint.DwellDuration;
            }

            if (Time.time < dwellUntil)
            {
                return;
            }

            Transform transitionArrival = waypoint.TransitionArrival;
            waypoint.Complete(this);
            reservedWaypoint = null;
            dwellUntil = -1f;
            if (transitionArrival != null)
            {
                body.linearVelocity = Vector2.zero;
                waypoint.ActivateTransitionScreen();
                body.position = transitionArrival.position;
                transform.position = transitionArrival.position;
            }
            waypointIndex++;
            ClearPath();
        }

        private bool BeginLeg(
            PassengerJourneyLeg leg,
            PassengerJourneyWaypoint[] waypoints)
        {
            if (route == null || waypoints == null || waypoints.Length == 0)
            {
                return false;
            }

            reservedWaypoint?.Cancel(this);
            activeRoute = waypoints;
            currentLeg = leg;
            waypointIndex = 0;
            dwellUntil = -1f;
            IsControlling = true;
            ClearPath();
            passenger?.SetStationJourneyStage(leg, waypoints[0].Action);
            return true;
        }

        private void CompleteLeg()
        {
            PassengerJourneyLeg completedLeg = currentLeg;
            reservedWaypoint?.Cancel(this);
            reservedWaypoint = null;
            activeRoute = null;
            currentLeg = PassengerJourneyLeg.None;
            IsControlling = false;
            ClearPath();
            route?.ReportCompleted(completedLeg);

            switch (completedLeg)
            {
                case PassengerJourneyLeg.BoardingRoute:
                    passenger?.CompleteStationApproach();
                    break;

                case PassengerJourneyLeg.TransferRoute:
                    transferCount++;
                    if (route == null || route.TransferBoardingDoorway == null)
                    {
                        Destroy(gameObject);
                        return;
                    }

                    passenger?.BeginTransferBoarding(
                        route.TransferBoardingDoorway,
                        route.TransferDoorCycle,
                        route.TransferMapRoot);
                    break;

                case PassengerJourneyLeg.ExitRoute:
                    Destroy(gameObject);
                    break;
            }
        }

        private bool MoveTowards(Vector2 destination, float acceptanceRadius)
        {
            if (Vector2.Distance(body.position, destination) <= acceptanceRadius)
            {
                return true;
            }

            EnsurePath(destination);
            Vector2 movementTarget = destination;
            while (pathIndex < path.Count)
            {
                movementTarget = path[pathIndex];
                if (Vector2.Distance(body.position, movementTarget) > 0.12f)
                {
                    break;
                }

                pathIndex++;
            }

            if (pathIndex >= path.Count)
            {
                movementTarget = destination;
            }

            Vector2 desired = movementTarget - body.position;
            if (desired.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            Vector2 direction = ApplyLocalAvoidance(desired.normalized);
            float step = Mathf.Min(
                stationMoveSpeed * Time.fixedDeltaTime,
                Vector2.Distance(body.position, movementTarget));
            body.MovePosition(body.position + direction * step);
            return Vector2.Distance(body.position, destination) <= acceptanceRadius;
        }

        private void EnsurePath(Vector2 destination)
        {
            bool sameTarget = hasPathTarget &&
                              Vector2.SqrMagnitude(pathTarget - destination) <= 0.01f;
            if (sameTarget && Time.time < nextRepathTime && path.Count > 0)
            {
                return;
            }

            pathTarget = destination;
            hasPathTarget = true;
            nextRepathTime = Time.time + repathInterval;
            pathIndex = 0;
            path.Clear();

            GridNavigation2D navigation = route?.FindNavigation(body.position, destination);
            if (navigation == null)
            {
                return;
            }

            path.AddRange(navigation.FindWorldPath(body.position, destination));
        }

        private Vector2 ApplyLocalAvoidance(Vector2 desiredDirection)
        {
            PassengerIntentCoordinator coordinator = route?.IntentCoordinator;
            if (coordinator == null)
            {
                return desiredDirection;
            }

            coordinator.CollectNearbyPassengers(
                body.position,
                avoidanceRadius,
                passenger,
                nearbyPassengers);
            Vector2 separation = Vector2.zero;
            foreach (GeneralPassengerPrototype other in nearbyPassengers)
            {
                if (other == null)
                {
                    continue;
                }

                Vector2 away = body.position - other.SimulationPosition;
                float distance = away.magnitude;
                if (distance <= 0.001f || distance >= avoidanceRadius)
                {
                    continue;
                }

                separation += away.normalized *
                              (1f - distance / avoidanceRadius);
            }

            if (separation.sqrMagnitude <= 0.0001f)
            {
                return desiredDirection;
            }

            Vector2 combined =
                desiredDirection + separation.normalized * avoidanceStrength;
            return Vector2.Dot(combined, desiredDirection) > 0.25f
                ? combined.normalized
                : desiredDirection;
        }

        private void ClearPath()
        {
            path.Clear();
            pathIndex = 0;
            hasPathTarget = false;
        }

        private void EnsureReferences(GeneralPassengerPrototype owner)
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            passenger = owner != null
                ? owner
                : passenger != null
                    ? passenger
                    : GetComponent<GeneralPassengerPrototype>();
        }
    }
}
