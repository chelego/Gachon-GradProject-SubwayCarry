namespace SubwayCarry.Prototype
{
    public enum PrototypeFlowStage
    {
        Tutorial,
        GachonHub,
        DeliverySelected,
        DepartureConcourse,
        DepartureEscalator,
        DeparturePlatform,
        TrainBoarding,
        TrainRide,
        DestinationPlatform,
        DestinationEscalator,
        DestinationConcourse,
        Settlement
    }

    public enum PrototypeAreaAction
    {
        EnterDepartureEscalator,
        EnterDeparturePlatform,
        BoardTrain,
        LeaveTrainAtDestination,
        EnterDestinationEscalator,
        EnterDestinationConcourse,
        CompleteDeliveryAtDestinationExit
    }
}
