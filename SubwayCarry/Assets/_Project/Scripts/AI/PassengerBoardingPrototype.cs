using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.AI
{
    [DisallowMultipleComponent]
    public sealed class PassengerBoardingPrototype : MonoBehaviour
    {
        private enum BoardingState
        {
            WaitingOutside,
            Boarding,
            LookingForSeat,
            MovingToSeat,
            Seated,
            MovingToStandingPlace,
            Standing,
            Exiting,
            WaitingForNextBoarding
        }

        [SerializeField] private TrainDoorController door;
        [SerializeField] private Transform outsidePoint;
        [SerializeField] private Transform insidePoint;
        [SerializeField] private Transform standingPoint;
        [SerializeField] private PassengerSeatPrototype[] seats;
        [SerializeField, Min(0.1f)] private float moveSpeed = 1.8f;
        [SerializeField, Min(0.01f)] private float arrivalDistance = 0.05f;
        [SerializeField] private BoardingState state;

        private readonly List<string> stateHistory = new List<string>();
        private PassengerSeatPrototype reservedSeat;
        private Transform reservedSittingPoint;
        private bool nextStopReady;

        public string CurrentState => state.ToString();
        public string StateHistory => string.Join(" > ", stateHistory);

        public void Configure(
            TrainDoorController boardingDoor,
            Transform outsideWaitingPoint,
            Transform insideBoardingPoint,
            Transform insideStandingPoint,
            PassengerSeatPrototype[] availableSeats)
        {
            door = boardingDoor;
            outsidePoint = outsideWaitingPoint;
            insidePoint = insideBoardingPoint;
            standingPoint = insideStandingPoint;
            seats = availableSeats;
        }

        private void Start()
        {
            nextStopReady = false;
            SetState(BoardingState.WaitingOutside);
            if (outsidePoint != null)
            {
                transform.position = outsidePoint.position;
            }
        }

        private void Update()
        {
            if (door == null || outsidePoint == null || insidePoint == null || standingPoint == null)
            {
                return;
            }

            switch (state)
            {
                case BoardingState.WaitingOutside:
                    if (door.IsOpen)
                    {
                        SetState(BoardingState.Boarding);
                    }
                    break;

                case BoardingState.Boarding:
                    if (MoveTo(insidePoint.position))
                    {
                        SetState(BoardingState.LookingForSeat);
                    }
                    break;

                case BoardingState.LookingForSeat:
                    if (TryReserveSeat())
                    {
                        SetState(BoardingState.MovingToSeat);
                    }
                    else
                    {
                        SetState(BoardingState.MovingToStandingPlace);
                    }
                    break;

                case BoardingState.MovingToSeat:
                    if (MoveTo(reservedSittingPoint.position))
                    {
                        SetState(BoardingState.Seated);
                    }
                    break;

                case BoardingState.MovingToStandingPlace:
                    if (MoveTo(standingPoint.position))
                    {
                        SetState(BoardingState.Standing);
                    }
                    break;

                case BoardingState.Seated:
                case BoardingState.Standing:
                    UpdateRideState();
                    break;

                case BoardingState.Exiting:
                    if (MoveTo(outsidePoint.position))
                    {
                        SetState(BoardingState.WaitingForNextBoarding);
                    }
                    break;

                case BoardingState.WaitingForNextBoarding:
                    if (door.IsClosed)
                    {
                        nextStopReady = false;
                        SetState(BoardingState.WaitingOutside);
                    }
                    break;
            }
        }

        private bool TryReserveSeat()
        {
            if (seats == null)
            {
                return false;
            }

            foreach (PassengerSeatPrototype seat in seats)
            {
                if (seat != null && seat.TryReserve(gameObject, out reservedSittingPoint))
                {
                    reservedSeat = seat;
                    return true;
                }
            }

            return false;
        }

        private void UpdateRideState()
        {
            if (door.IsClosed)
            {
                nextStopReady = true;
            }

            if (!nextStopReady || !door.IsOpen)
            {
                return;
            }

            if (reservedSeat != null)
            {
                reservedSeat.Release(gameObject);
                reservedSeat = null;
                reservedSittingPoint = null;
            }

            SetState(BoardingState.Exiting);
        }

        private bool MoveTo(Vector3 destination)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                destination,
                moveSpeed * Time.deltaTime);
            return Vector3.Distance(transform.position, destination) <= arrivalDistance;
        }

        private void SetState(BoardingState nextState)
        {
            state = nextState;
            stateHistory.Add(nextState.ToString());

            if (stateHistory.Count > 16)
            {
                stateHistory.RemoveAt(0);
            }
        }
    }
}
