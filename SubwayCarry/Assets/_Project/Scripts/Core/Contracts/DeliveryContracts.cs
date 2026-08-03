using System;

namespace SubwayCarry.Core.Contracts
{
    public enum DeliveryPhase
    {
        None,
        Selected,
        TravellingToDeparture,
        InTransit,
        Transferring,
        Arrived,
        Completed,
        Failed
    }

    public enum DeliveryFailureReason
    {
        None,
        PackageDestroyed,
        MissedRequiredStop,
        Bankrupt
    }

    public readonly struct DeliveryStateSnapshot
    {
        public DeliveryStateSnapshot(
            string deliveryId,
            string destinationStationId,
            DeliveryPhase phase,
            DeliveryFailureReason failureReason)
        {
            DeliveryId = deliveryId;
            DestinationStationId = destinationStationId;
            Phase = phase;
            FailureReason = failureReason;
        }

        public string DeliveryId { get; }
        public string DestinationStationId { get; }
        public DeliveryPhase Phase { get; }
        public DeliveryFailureReason FailureReason { get; }
    }

    public readonly struct EconomyStateSnapshot
    {
        public EconomyStateSnapshot(int currentCash)
        {
            CurrentCash = currentCash;
        }

        public int CurrentCash { get; }
    }

    public readonly struct DeliverySettlementSnapshot
    {
        public DeliverySettlementSnapshot(
            int deliveryFee,
            int compensation,
            int outboundFare,
            int returnFare,
            int netIncome)
        {
            DeliveryFee = deliveryFee;
            Compensation = compensation;
            OutboundFare = outboundFare;
            ReturnFare = returnFare;
            NetIncome = netIncome;
        }

        public int DeliveryFee { get; }
        public int Compensation { get; }
        public int OutboundFare { get; }
        public int ReturnFare { get; }
        public int NetIncome { get; }
    }

    public interface IDeliveryStateProvider
    {
        DeliveryStateSnapshot CurrentDeliveryState { get; }
        event Action<DeliveryStateSnapshot> DeliveryStateChanged;
    }

    public interface IDeliveryService : IDeliveryStateProvider
    {
        bool TryStartDelivery(string deliveryId);
    }

    public interface IEconomyStateProvider
    {
        EconomyStateSnapshot CurrentEconomyState { get; }
        event Action<EconomyStateSnapshot> EconomyStateChanged;
    }

    public interface IDeliverySettlementProvider
    {
        DeliverySettlementSnapshot LastSettlement { get; }
        event Action<DeliverySettlementSnapshot> DeliverySettled;
    }
}
