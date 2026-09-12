using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.AI
{
    public enum PassengerJourneyWaypointAction
    {
        Walk,
        TapEntryGate,
        EnterVerticalConnector,
        LeaveVerticalConnector,
        ReachPlatform,
        ClearPlatform,
        EnterTransferPassage,
        ReachTransferPlatform,
        TapExitGate,
        LeaveStation
    }

    [DisallowMultipleComponent]
    public sealed class PassengerJourneyWaypoint : MonoBehaviour
    {
        [SerializeField] private PassengerJourneyWaypointAction action;
        [SerializeField, Min(0.05f)] private float acceptanceRadius = 0.18f;
        [SerializeField, Min(0f)] private float dwellDuration;
        [SerializeField, Min(1)] private int capacity = 2;
        [SerializeField] private Vector2 queueDirection = Vector2.down;
        [SerializeField, Min(0.1f)] private float queueSpacing = 0.62f;
        [SerializeField] private Transform transitionArrival;
        [SerializeField] private PassengerJourneyScreenSwitcherPrototype screenSwitcher;
        [SerializeField] private GameObject transitionScreen;

        private readonly List<PassengerStationJourneyPrototype> activeUsers =
            new List<PassengerStationJourneyPrototype>();
        private readonly List<PassengerStationJourneyPrototype> waitingUsers =
            new List<PassengerStationJourneyPrototype>();

        public PassengerJourneyWaypointAction Action => action;
        public float AcceptanceRadius => acceptanceRadius;
        public float DwellDuration => dwellDuration;
        public Transform TransitionArrival => transitionArrival;
        public int CompletionCount { get; private set; }

        public void Configure(
            PassengerJourneyWaypointAction waypointAction,
            float waitDuration,
            int simultaneousCapacity,
            Vector2 waitingDirection)
        {
            action = waypointAction;
            dwellDuration = Mathf.Max(0f, waitDuration);
            capacity = Mathf.Max(1, simultaneousCapacity);
            queueDirection = waitingDirection.sqrMagnitude > 0.001f
                ? waitingDirection.normalized
                : Vector2.down;
        }

        public void ConfigureTransition(Transform arrival)
        {
            transitionArrival = arrival;
        }

        public void ConfigureTransition(
            Transform arrival,
            PassengerJourneyScreenSwitcherPrototype switcher,
            GameObject targetScreen)
        {
            transitionArrival = arrival;
            screenSwitcher = switcher;
            transitionScreen = targetScreen;
        }

        public void ActivateTransitionScreen()
        {
            if (screenSwitcher != null && transitionScreen != null)
            {
                screenSwitcher.Activate(transitionScreen);
            }
        }

        public bool RequestAccess(PassengerStationJourneyPrototype passenger)
        {
            RemoveMissingUsers();
            if (passenger == null)
            {
                return false;
            }

            if (activeUsers.Contains(passenger))
            {
                return true;
            }

            if (!waitingUsers.Contains(passenger))
            {
                waitingUsers.Add(passenger);
            }

            if (activeUsers.Count >= capacity || waitingUsers[0] != passenger)
            {
                return false;
            }

            waitingUsers.RemoveAt(0);
            activeUsers.Add(passenger);
            return true;
        }

        public Vector2 GetQueuePosition(PassengerStationJourneyPrototype passenger)
        {
            RemoveMissingUsers();
            int index = waitingUsers.IndexOf(passenger);
            if (index < 0)
            {
                index = 0;
            }

            Vector2 lateral = new Vector2(-queueDirection.y, queueDirection.x);
            float side = index % 2 == 0 ? -1f : 1f;
            int row = index / 2 + 1;
            return (Vector2)transform.position +
                   queueDirection * row * queueSpacing +
                   lateral * side * queueSpacing * 0.32f;
        }

        public void Complete(PassengerStationJourneyPrototype passenger)
        {
            if (passenger != null && activeUsers.Remove(passenger))
            {
                CompletionCount++;
            }
        }

        public void Cancel(PassengerStationJourneyPrototype passenger)
        {
            activeUsers.Remove(passenger);
            waitingUsers.Remove(passenger);
        }

        private void RemoveMissingUsers()
        {
            activeUsers.RemoveAll(passenger => passenger == null);
            waitingUsers.RemoveAll(passenger => passenger == null);
        }
    }
}
