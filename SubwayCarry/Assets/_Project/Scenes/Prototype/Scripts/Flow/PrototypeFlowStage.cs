namespace SubwayCarry.Prototype
{
    public enum PrototypeFlowStage
    {
        Tutorial,
        GachonHub,
        DeliverySelected,
        DepartureConcourse,
        DeparturePlatform,
        TrainBoarding,
        TrainRide,
        DestinationPlatform,
        DestinationConcourse,
        Settlement
    }

    public enum PrototypeAreaAction
    {
        EnterDeparturePlatform,
        BoardTrain,
        LeaveTrainAtDestination,
        EnterDestinationConcourse
    }
}
