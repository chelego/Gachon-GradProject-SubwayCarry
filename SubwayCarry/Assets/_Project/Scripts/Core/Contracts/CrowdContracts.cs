using System;

namespace SubwayCarry.Core.Contracts
{
    public enum CrowdLevel
    {
        Relaxed,
        Normal,
        Crowded,
        Severe
    }

    public readonly struct CrowdStateSnapshot
    {
        public CrowdStateSnapshot(
            int passengerCount,
            int practicalCapacity,
            CrowdLevel level)
        {
            PassengerCount = passengerCount < 0 ? 0 : passengerCount;
            PracticalCapacity = practicalCapacity < 0 ? 0 : practicalCapacity;
            Level = level;
        }

        public int PassengerCount { get; }
        public int PracticalCapacity { get; }
        public CrowdLevel Level { get; }
    }

    public interface ICrowdStateProvider
    {
        CrowdStateSnapshot CurrentCrowdState { get; }
        event Action<CrowdStateSnapshot> CrowdStateChanged;
    }
}
