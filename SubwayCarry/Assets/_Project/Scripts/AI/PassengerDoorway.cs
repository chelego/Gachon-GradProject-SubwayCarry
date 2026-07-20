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

        [SerializeField, Min(0f)] private float boardingDelayAfterOpening = 0.8f;

        private readonly List<GameObject> leftBoardingQueue = new List<GameObject>();
        private readonly List<GameObject> rightBoardingQueue = new List<GameObject>();
        private readonly List<GameObject> exitingPassengers = new List<GameObject>();
        private GameObject activeLeftBoardingPassenger;
        private GameObject activeRightBoardingPassenger;
        private bool wasDoorOpen;
        private float doorOpenedAt;

        public TrainDoorController Door => door;
        public Transform InsidePoint => insidePoint;
        public Transform OutsidePoint => outsidePoint;
        public GridNavigation2D Navigation => navigation;
        public bool IsUsable => door != null && insidePoint != null && outsidePoint != null;
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
            if (activeLeftBoardingPassenger == passenger)
            {
                activeLeftBoardingPassenger = null;
            }

            if (activeRightBoardingPassenger == passenger)
            {
                activeRightBoardingPassenger = null;
            }
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
                   lateral * side * 0.72f +
                   outsideDirection * index * 0.9f;
        }

        public Vector2 GetBoardingEntryPosition(GameObject passenger)
        {
            return GetBoardingLanePosition(passenger, outsidePoint.position);
        }

        public Vector2 GetBoardingInsidePosition(GameObject passenger)
        {
            return GetBoardingLanePosition(passenger, insidePoint.position);
        }

        public bool TryBeginBoarding(GameObject passenger)
        {
            RemoveMissingBoardingPassengers();
            RemoveMissingExitPassengers();
            if (door == null ||
                !door.IsOpen ||
                Time.time < doorOpenedAt + boardingDelayAfterOpening ||
                exitingPassengers.Count > 0)
            {
                return false;
            }

            bool isLeft = leftBoardingQueue.Contains(passenger);
            List<GameObject> queue = isLeft ? leftBoardingQueue : rightBoardingQueue;
            GameObject activePassenger = isLeft
                ? activeLeftBoardingPassenger
                : activeRightBoardingPassenger;
            if (activePassenger != null && activePassenger != passenger)
            {
                return false;
            }

            if (queue.Count == 0 || queue[0] != passenger)
            {
                return false;
            }

            if (isLeft)
            {
                activeLeftBoardingPassenger = passenger;
            }
            else
            {
                activeRightBoardingPassenger = passenger;
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
            if (index == 0)
            {
                return insidePoint.position;
            }

            int queuedIndex = index - 1;
            int row = queuedIndex / 3 + 1;
            int laneIndex = queuedIndex % 3;
            float lane = laneIndex == 0 ? 0f : laneIndex == 1 ? -1f : 1f;

            return (Vector2)insidePoint.position -
                   exitDirection * row * 0.95f +
                   lateral * lane * 0.9f;
        }

        public void Configure(
            TrainDoorController doorController,
            Transform insideWaitingPoint,
            Transform outsideWaitingPoint,
            GridNavigation2D gridNavigation)
        {
            door = doorController;
            insidePoint = insideWaitingPoint;
            outsidePoint = outsideWaitingPoint;
            navigation = gridNavigation;
        }

        private void Reset()
        {
            door = GetComponent<TrainDoorController>();
            navigation = GetComponentInParent<GridNavigation2D>();
        }

        private void Update()
        {
            bool isDoorOpen = door != null && door.IsOpen;
            if (isDoorOpen && !wasDoorOpen)
            {
                doorOpenedAt = Time.time;
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
            return center + lateral * side * 0.43f;
        }

        private void RemoveMissingBoardingPassengers()
        {
            leftBoardingQueue.RemoveAll(passenger => passenger == null);
            rightBoardingQueue.RemoveAll(passenger => passenger == null);
        }

        private void RemoveMissingExitPassengers()
        {
            exitingPassengers.RemoveAll(passenger => passenger == null);
        }
    }
}
