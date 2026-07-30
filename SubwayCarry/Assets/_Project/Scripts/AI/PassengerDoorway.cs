using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.AI
{
    [DisallowMultipleComponent]
    public sealed class PassengerDoorway : MonoBehaviour
    {
        [SerializeField] private TrainDoorController door;
        [SerializeField] private Transform insidePoint;
        [SerializeField] private Transform outsidePoint;
        [SerializeField] private GridNavigation2D navigation;
        [SerializeField] private bool serviceEnabled = true;

        [SerializeField, Min(0f)] private float boardingDelayAfterOpening;
        [SerializeField, Min(0.5f)] private float boardingStartDistance = 3.2f;
        [SerializeField, Min(0.1f)] private float directBoardingLaneHalfWidth = 0.72f;

        private readonly List<GameObject> leftBoardingQueue = new List<GameObject>();
        private readonly List<GameObject> rightBoardingQueue = new List<GameObject>();
        private readonly List<GameObject> exitingPassengers = new List<GameObject>();
        private readonly HashSet<GameObject> activeBoardingPassengers = new HashSet<GameObject>();
        private readonly Dictionary<GameObject, int> boardingOrders = new Dictionary<GameObject, int>();
        private readonly Dictionary<GameObject, int> boardingLaneSlots =
            new Dictionary<GameObject, int>();
        private bool wasDoorOpen;
        private bool boardingAdmissionOpen;
        private float doorOpenedAt;
        private int nextBoardingOrder;
        private int nextLeftLaneSlot;
        private int nextRightLaneSlot;

        public TrainDoorController Door => door;
        public Transform InsidePoint => insidePoint;
        public Transform OutsidePoint => outsidePoint;
        public GridNavigation2D Navigation => navigation;
        public bool IsUsable => serviceEnabled &&
                                door != null &&
                                insidePoint != null &&
                                outsidePoint != null;
        public int BoardingReservationCount
        {
            get
            {
                RemoveMissingBoardingPassengers();
                return leftBoardingQueue.Count + rightBoardingQueue.Count;
            }
        }
        public int ExitReservationCount
        {
            get
            {
                RemoveMissingExitPassengers();
                return exitingPassengers.Count;
            }
        }

        public bool TryReserveBoarding(GameObject passenger)
        {
            RemoveMissingBoardingPassengers();
            if (!leftBoardingQueue.Contains(passenger) &&
                !rightBoardingQueue.Contains(passenger))
            {
                List<GameObject> queue = leftBoardingQueue.Count < rightBoardingQueue.Count
                    ? leftBoardingQueue
                    : leftBoardingQueue.Count > rightBoardingQueue.Count
                        ? rightBoardingQueue
                        : Random.value < 0.5f ? leftBoardingQueue : rightBoardingQueue;
                queue.Add(passenger);
            }

            return true;
        }

        public void ReleaseBoarding(GameObject passenger)
        {
            leftBoardingQueue.Remove(passenger);
            rightBoardingQueue.Remove(passenger);
            activeBoardingPassengers.Remove(passenger);
            boardingOrders.Remove(passenger);
            boardingLaneSlots.Remove(passenger);
        }

        public int GetBoardingOrder(GameObject passenger)
        {
            return passenger != null && boardingOrders.TryGetValue(passenger, out int order)
                ? order
                : int.MaxValue;
        }

        public void SetBoardingAdmission(bool open)
        {
            boardingAdmissionOpen = open;
        }

        public bool HasActiveBoardingTraffic()
        {
            RemoveMissingBoardingPassengers();
            foreach (GameObject passenger in activeBoardingPassengers)
            {
                if (passenger == null)
                {
                    continue;
                }

                float distance = DistancePointToSegment(
                    passenger.transform.position,
                    outsidePoint.position,
                    insidePoint.position);
                if (distance <= boardingStartDistance)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsInsideActiveBoardingFlow(Vector2 position)
        {
            bool hasIncomingPassengers = HasActiveBoardingTraffic() ||
                                         (boardingAdmissionOpen && BoardingReservationCount > 0);
            if (!hasIncomingPassengers || insidePoint == null || outsidePoint == null)
            {
                return false;
            }

            GetDoorAxes(out Vector2 outsideDirection, out _);
            Vector2 inwardDirection = -outsideDirection;
            Vector2 flowEnd = (Vector2)insidePoint.position + inwardDirection * 1.8f;
            return DistancePointToSegment(
                       position,
                       outsidePoint.position,
                       flowEnd) <= 0.95f;
        }

        public bool IsInsideDirectBoardingLane(Vector2 position)
        {
            bool hasIncomingPassengers = HasActiveBoardingTraffic() ||
                                         (boardingAdmissionOpen && BoardingReservationCount > 0);
            if (!hasIncomingPassengers || insidePoint == null || outsidePoint == null)
            {
                return false;
            }

            GetDoorAxes(out Vector2 outsideDirection, out _);
            Vector2 inwardDirection = -outsideDirection;
            Vector2 flowEnd = (Vector2)insidePoint.position + inwardDirection * 1.8f;
            return DistancePointToSegment(
                       position,
                       outsidePoint.position,
                       flowEnd) <= directBoardingLaneHalfWidth;
        }

        public Vector2 GetBoardingQueuePosition(GameObject passenger)
        {
            RemoveMissingBoardingPassengers();
            int index = leftBoardingQueue.IndexOf(passenger);
            float side = -1f;
            if (index < 0)
            {
                index = rightBoardingQueue.IndexOf(passenger);
                side = 1f;
            }

            if (index < 0)
            {
                return outsidePoint.position;
            }

            GetDoorAxes(out Vector2 outsideDirection, out Vector2 lateral);

            return (Vector2)outsidePoint.position +
                   lateral * side * 0.62f +
                   outsideDirection * index * 0.68f;
        }

        public Vector2 GetBoardingEntryPosition(GameObject passenger)
        {
            return GetBoardingLanePosition(passenger, outsidePoint.position);
        }

        public Vector2 GetBoardingInsidePosition(GameObject passenger)
        {
            GetDoorAxes(out Vector2 outsideDirection, out Vector2 lateral);
            int encodedSlot;
            if (passenger == null ||
                !boardingLaneSlots.TryGetValue(passenger, out encodedSlot))
            {
                bool usesLeftLane = leftBoardingQueue.Contains(passenger);
                int queueIndex = usesLeftLane
                    ? leftBoardingQueue.IndexOf(passenger)
                    : rightBoardingQueue.IndexOf(passenger);
                int recoveredSlot = Mathf.Max(0, queueIndex);
                encodedSlot = usesLeftLane
                    ? -(recoveredSlot + 1)
                    : recoveredSlot + 1;
            }

            float side = Mathf.Sign(encodedSlot);
            int sideSlot = Mathf.Max(0, Mathf.Abs(encodedSlot) - 1);
            int depthRow = sideSlot % 3;
            int lateralBand = sideSlot / 3;
            float inwardDepth = 1.75f - depthRow * 0.72f;
            float lateralOffset =
                side * (0.38f + Mathf.Min(1, lateralBand) * 0.14f);
            Vector2 target =
                (Vector2)insidePoint.position -
                outsideDirection * inwardDepth +
                lateral * lateralOffset;
            if (navigation != null)
            {
                Rect bounds = navigation.WorldBounds;
                target.x = Mathf.Clamp(
                    target.x,
                    bounds.xMin + 0.7f,
                    bounds.xMax - 0.7f);
                target.y = Mathf.Clamp(
                    target.y,
                    bounds.yMin + 0.9f,
                    bounds.yMax - 0.9f);
            }

            return target;
        }

        public Vector2 GetBoardingClearancePosition(GameObject passenger)
        {
            GetDoorAxes(out Vector2 outsideDirection, out Vector2 lateral);
            Vector2 inwardDirection = -outsideDirection;
            int order = GetBoardingOrder(passenger);
            if (order == int.MaxValue)
            {
                order = 8;
            }

            float inwardDepth = order <= 3
                ? 3f
                : order <= 7
                    ? 2.1f
                    : 1.3f;
            int laneIndex = (order - 1) % 5 - 2;
            float lateralOffset = laneIndex * 0.52f;
            Vector2 target = (Vector2)insidePoint.position +
                             inwardDirection * inwardDepth +
                             lateral * lateralOffset;
            if (navigation != null)
            {
                Rect bounds = navigation.WorldBounds;
                target.x = Mathf.Clamp(target.x, bounds.xMin + 1.1f, bounds.xMax - 1.1f);
                target.y = Mathf.Clamp(target.y, bounds.yMin + 1.2f, bounds.yMax - 1.2f);
            }

            return target;
        }

        public bool HasEnteredTrain(Vector2 passengerPosition)
        {
            Vector2 outside = outsidePoint.position;
            Vector2 inside = insidePoint.position;
            Vector2 crossing = inside - outside;
            if (crossing.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            float progress = Vector2.Dot(
                passengerPosition - outside,
                crossing) / crossing.sqrMagnitude;
            return progress >= 0.52f;
        }

        public bool HasLeftTrain(Vector2 passengerPosition)
        {
            Vector2 outside = outsidePoint.position;
            Vector2 inside = insidePoint.position;
            Vector2 crossing = inside - outside;
            if (crossing.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            float progress = Vector2.Dot(
                passengerPosition - outside,
                crossing) / crossing.sqrMagnitude;
            return progress <= 0.2f;
        }

        public bool TryBeginBoarding(GameObject passenger)
        {
            RefreshDoorOpenState();
            RemoveMissingBoardingPassengers();
            RemoveMissingExitPassengers();
            if (door == null ||
                !door.IsOpen ||
                !boardingAdmissionOpen ||
                Time.time < doorOpenedAt + boardingDelayAfterOpening ||
                exitingPassengers.Count > 0 ||
                Vector2.Distance(passenger.transform.position, outsidePoint.position) >
                boardingStartDistance)
            {
                return false;
            }

            bool isQueued = leftBoardingQueue.Contains(passenger) ||
                            rightBoardingQueue.Contains(passenger);
            if (!isQueued)
            {
                return false;
            }

            activeBoardingPassengers.Add(passenger);
            if (!boardingOrders.ContainsKey(passenger))
            {
                boardingOrders.Add(passenger, ++nextBoardingOrder);
                bool usesLeftLane = leftBoardingQueue.Contains(passenger);
                int laneSlot = usesLeftLane
                    ? nextLeftLaneSlot++
                    : nextRightLaneSlot++;
                boardingLaneSlots[passenger] = usesLeftLane
                    ? -(laneSlot + 1)
                    : laneSlot + 1;
            }

            return true;
        }

        public bool TryReserveExit(GameObject passenger)
        {
            RemoveMissingExitPassengers();
            if (!exitingPassengers.Contains(passenger))
            {
                exitingPassengers.Add(passenger);
            }

            return true;
        }

        public void ReleaseExit(GameObject passenger)
        {
            exitingPassengers.Remove(passenger);
        }

        public Vector2 GetExitWaitingPosition(GameObject passenger)
        {
            RemoveMissingExitPassengers();
            int index = exitingPassengers.IndexOf(passenger);
            if (index < 0)
            {
                return insidePoint.position;
            }

            GetDoorAxes(out Vector2 exitDirection, out Vector2 lateral);
            Vector2 doorSideBase =
                (Vector2)insidePoint.position + exitDirection * 1.15f;
            int row = index / 2;
            float lane = index % 2 == 0 ? -0.34f : 0.34f;

            return doorSideBase -
                   exitDirection * row * 0.72f +
                   lateral * lane;
        }

        public void Configure(
            TrainDoorController doorController,
            Transform insideWaitingPoint,
            Transform outsideWaitingPoint,
            GridNavigation2D gridNavigation,
            bool enableService = true)
        {
            door = doorController;
            insidePoint = insideWaitingPoint;
            outsidePoint = outsideWaitingPoint;
            navigation = gridNavigation;
            serviceEnabled = enableService;
        }

        private void Reset()
        {
            door = GetComponent<TrainDoorController>();
            navigation = GetComponentInParent<GridNavigation2D>();
        }

        private void Update()
        {
            RefreshDoorOpenState();
        }

        private void RefreshDoorOpenState()
        {
            bool isDoorOpen = door != null && door.IsOpen;
            if (isDoorOpen && !wasDoorOpen)
            {
                doorOpenedAt = Time.time;
                nextBoardingOrder = 0;
                nextLeftLaneSlot = 0;
                nextRightLaneSlot = 0;
                boardingOrders.Clear();
                boardingLaneSlots.Clear();
            }

            wasDoorOpen = isDoorOpen;
        }

        private void GetDoorAxes(out Vector2 outsideDirection, out Vector2 lateral)
        {
            outsideDirection =
                ((Vector2)outsidePoint.position - (Vector2)insidePoint.position).normalized;
            if (outsideDirection.sqrMagnitude <= 0.001f)
            {
                outsideDirection = Vector2.up;
            }

            lateral = new Vector2(-outsideDirection.y, outsideDirection.x);
        }

        private Vector2 GetBoardingLanePosition(GameObject passenger, Vector2 center)
        {
            GetDoorAxes(out _, out Vector2 lateral);
            float side = leftBoardingQueue.Contains(passenger) ? -1f : 1f;
            int passengerHash = passenger != null
                ? passenger.GetInstanceID() & int.MaxValue
                : 0;
            float laneVariation = (passengerHash % 3 - 1) * 0.04f;
            return center + lateral * (side * 0.4f + laneVariation);
        }

        private void RemoveMissingBoardingPassengers()
        {
            leftBoardingQueue.RemoveAll(passenger => passenger == null);
            rightBoardingQueue.RemoveAll(passenger => passenger == null);
            activeBoardingPassengers.RemoveWhere(passenger => passenger == null);
        }

        private void RemoveMissingExitPassengers()
        {
            exitingPassengers.RemoveAll(passenger => passenger == null);
        }

        private static float DistancePointToSegment(
            Vector2 point,
            Vector2 segmentStart,
            Vector2 segmentEnd)
        {
            Vector2 segment = segmentEnd - segmentStart;
            if (segment.sqrMagnitude <= 0.001f)
            {
                return Vector2.Distance(point, segmentStart);
            }

            float progress = Mathf.Clamp01(
                Vector2.Dot(point - segmentStart, segment) / segment.sqrMagnitude);
            return Vector2.Distance(point, segmentStart + segment * progress);
        }
    }
}
