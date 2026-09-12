using UnityEngine;

namespace SubwayCarry.AI
{
    public enum PassengerJourneyLeg
    {
        None,
        BoardingRoute,
        ExitRoute,
        TransferRoute
    }

    public enum PassengerPostAlightingPlan
    {
        ExitStation,
        TransferOnceThenExit
    }

    [DisallowMultipleComponent]
    public sealed class PassengerStationRoutePrototype : MonoBehaviour
    {
        [SerializeField] private PassengerJourneyWaypoint[] boardingRoute;
        [SerializeField] private PassengerJourneyWaypoint[] exitRoute;
        [SerializeField] private PassengerJourneyWaypoint[] postTransferExitRoute;
        [SerializeField] private PassengerJourneyWaypoint[] transferRoute;
        [SerializeField] private GridNavigation2D[] navigationAreas;
        [SerializeField] private PassengerIntentCoordinator intentCoordinator;
        [SerializeField] private PassengerDoorway transferBoardingDoorway;
        [SerializeField] private TrainDoorCyclePrototype transferDoorCycle;
        [SerializeField] private Transform transferMapRoot;

        public PassengerJourneyWaypoint[] BoardingRoute => boardingRoute;
        public PassengerJourneyWaypoint[] ExitRoute => exitRoute;
        public PassengerJourneyWaypoint[] PostTransferExitRoute => postTransferExitRoute;
        public PassengerJourneyWaypoint[] TransferRoute => transferRoute;
        public PassengerIntentCoordinator IntentCoordinator => intentCoordinator;
        public PassengerDoorway TransferBoardingDoorway => transferBoardingDoorway;
        public TrainDoorCyclePrototype TransferDoorCycle => transferDoorCycle;
        public Transform TransferMapRoot => transferMapRoot;
        public int BoardingCompletionCount { get; private set; }
        public int ExitCompletionCount { get; private set; }
        public int TransferCompletionCount { get; private set; }

        public void Configure(
            PassengerJourneyWaypoint[] approach,
            PassengerJourneyWaypoint[] stationExit,
            PassengerJourneyWaypoint[] transfer,
            GridNavigation2D[] navigations,
            PassengerIntentCoordinator coordinator,
            PassengerDoorway transferDoorway,
            TrainDoorCyclePrototype transferCycle,
            Transform transferRoot,
            PassengerJourneyWaypoint[] exitAfterTransfer = null)
        {
            boardingRoute = approach;
            exitRoute = stationExit;
            transferRoute = transfer;
            navigationAreas = navigations;
            intentCoordinator = coordinator;
            transferBoardingDoorway = transferDoorway;
            transferDoorCycle = transferCycle;
            transferMapRoot = transferRoot;
            postTransferExitRoute = exitAfterTransfer;
        }

        public GridNavigation2D FindNavigation(Vector2 start, Vector2 destination)
        {
            if (navigationAreas == null)
            {
                return null;
            }

            GridNavigation2D destinationArea = null;
            foreach (GridNavigation2D navigation in navigationAreas)
            {
                if (navigation == null || !navigation.ContainsWorldPosition(destination))
                {
                    continue;
                }

                if (navigation.ContainsWorldPosition(start))
                {
                    return navigation;
                }

                destinationArea = navigation;
            }

            return destinationArea;
        }

        public void ReportCompleted(PassengerJourneyLeg leg)
        {
            switch (leg)
            {
                case PassengerJourneyLeg.BoardingRoute:
                    BoardingCompletionCount++;
                    break;
                case PassengerJourneyLeg.ExitRoute:
                    ExitCompletionCount++;
                    break;
                case PassengerJourneyLeg.TransferRoute:
                    TransferCompletionCount++;
                    break;
            }
        }
    }
}
