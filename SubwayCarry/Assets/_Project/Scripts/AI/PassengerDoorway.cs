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
        private GameObject exitingPassenger;

        public TrainDoorController Door => door;
        public Transform InsidePoint => insidePoint;
        public Transform OutsidePoint => outsidePoint;
        public GridNavigation2D Navigation => navigation;
        public bool IsUsable => door != null && insidePoint != null && outsidePoint != null;

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
            if (exitingPassenger != null && exitingPassenger != passenger)
            {
                return false;
            }

            exitingPassenger = passenger;
            return true;
        }

        public void ReleaseExit(GameObject passenger)
        {
            if (exitingPassenger == passenger)
            {
                exitingPassenger = null;
            }
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
    }
}
