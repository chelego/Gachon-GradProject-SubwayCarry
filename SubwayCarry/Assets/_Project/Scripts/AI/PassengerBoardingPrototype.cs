using System.Collections.Generic;
using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.AI
{
    [DisallowMultipleComponent]
    public sealed class PassengerBoardingPrototype : MonoBehaviour, IPackageImpactSource
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
            MovingToExitDoor,
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
        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private PassengerSeatPrototype reservedSeat;
        private Transform reservedSittingPoint;
        private bool nextStopReady;

        public string CurrentState => state.ToString();
        public string StateHistory => string.Join(" > ", stateHistory);
        public Vector2 ImpactVelocity => body != null ? body.linearVelocity : Vector2.zero;

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
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            nextStopReady = false;
            SetState(BoardingState.WaitingOutside);
            if (outsidePoint != null)
            {
                SetPosition(outsidePoint.position);
            }
        }

        private void FixedUpdate()
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
                    if (MoveTo(reservedSeat.GetApproachPosition(reservedSittingPoint)))
                    {
                        SitDown();
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

                case BoardingState.MovingToExitDoor:
                    if (MoveTo(insidePoint.position))
                    {
                        SetState(BoardingState.Exiting);
                    }
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
                StandUp();
                reservedSeat.Release(gameObject);
                reservedSeat = null;
                reservedSittingPoint = null;
            }

            SetState(BoardingState.MovingToExitDoor);
        }

        private bool MoveTo(Vector3 destination)
        {
            Vector2 currentPosition = body != null ? body.position : (Vector2)transform.position;
            Vector2 nextPosition = Vector2.MoveTowards(
                currentPosition,
                destination,
                moveSpeed * Time.fixedDeltaTime);

            if (body != null)
            {
                body.MovePosition(nextPosition);
            }
            else
            {
                transform.position = nextPosition;
            }

            return Vector2.Distance(nextPosition, destination) <= arrivalDistance;
        }

        private void SitDown()
        {
            if (bodyCollider != null)
            {
                bodyCollider.enabled = false;
            }

            if (body != null)
            {
                body.bodyType = RigidbodyType2D.Kinematic;
            }

            SetPosition(reservedSittingPoint.position);
        }

        private void StandUp()
        {
            Vector2 approachPosition = reservedSeat.GetApproachPosition(reservedSittingPoint);
            SetPosition(approachPosition);

            if (body != null)
            {
                body.bodyType = RigidbodyType2D.Dynamic;
            }

            if (bodyCollider != null)
            {
                bodyCollider.enabled = true;
            }
        }

        private void SetPosition(Vector3 position)
        {
            if (body != null)
            {
                body.position = position;
            }
            else
            {
                transform.position = position;
            }
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
