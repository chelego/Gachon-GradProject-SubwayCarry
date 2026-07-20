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
            WaitingForDoorClose,
            Riding,
            Exiting,
            WaitingForNextBoarding
        }

        [SerializeField] private TrainDoorController door;
        [SerializeField] private Transform outsidePoint;
        [SerializeField] private Transform insidePoint;
        [SerializeField, Min(0.1f)] private float moveSpeed = 1.8f;
        [SerializeField, Min(0.01f)] private float arrivalDistance = 0.05f;
        [SerializeField] private BoardingState state;

        public string CurrentState => state.ToString();
        public string StateHistory => string.Join(" > ", stateHistory);

        private readonly List<string> stateHistory = new List<string>();

        public void Configure(
            TrainDoorController boardingDoor,
            Transform outsideWaitingPoint,
            Transform insideWaitingPoint)
        {
            door = boardingDoor;
            outsidePoint = outsideWaitingPoint;
            insidePoint = insideWaitingPoint;
        }

        private void Start()
        {
            SetState(BoardingState.WaitingOutside);
            if (outsidePoint != null)
            {
                transform.position = outsidePoint.position;
            }
        }

        private void Update()
        {
            if (door == null || outsidePoint == null || insidePoint == null)
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
                        SetState(BoardingState.WaitingForDoorClose);
                    }
                    break;

                case BoardingState.WaitingForDoorClose:
                    if (door.IsClosed)
                    {
                        SetState(BoardingState.Riding);
                    }
                    break;

                case BoardingState.Riding:
                    if (door.IsOpen)
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
                        SetState(BoardingState.WaitingOutside);
                    }
                    break;
            }
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

            if (stateHistory.Count > 12)
            {
                stateHistory.RemoveAt(0);
            }
        }
    }
}
