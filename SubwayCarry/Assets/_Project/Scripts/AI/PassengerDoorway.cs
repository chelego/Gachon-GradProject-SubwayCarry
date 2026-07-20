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

        private GameObject boardingPassenger;
        private readonly List<GameObject> exitingPassengers = new List<GameObject>();

        public TrainDoorController Door => door;
        public Transform InsidePoint => insidePoint;
        public Transform OutsidePoint => outsidePoint;
        public GridNavigation2D Navigation => navigation;
        public bool IsUsable => door != null && insidePoint != null && outsidePoint != null;
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
            if (boardingPassenger != null && boardingPassenger != passenger)
            {
                return false;
            }

            boardingPassenger = passenger;
            return true;
        }

        public void ReleaseBoarding(GameObject passenger)
        {
            if (boardingPassenger == passenger)
            {
                boardingPassenger = null;
            }
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

            Vector2 exitDirection =
                ((Vector2)outsidePoint.position - (Vector2)insidePoint.position).normalized;
            if (exitDirection.sqrMagnitude <= 0.001f)
            {
                exitDirection = Vector2.up;
            }

            Vector2 lateral = new Vector2(-exitDirection.y, exitDirection.x);
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

        private void RemoveMissingExitPassengers()
        {
            exitingPassengers.RemoveAll(passenger => passenger == null);
        }
    }
}
