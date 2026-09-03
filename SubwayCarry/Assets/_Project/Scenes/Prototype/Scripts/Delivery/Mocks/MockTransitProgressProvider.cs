using System;
using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.Prototype.Delivery.Mocks
{
    public sealed class MockTransitProgressProvider : MonoBehaviour, ITransitProgressProvider
    {
        [SerializeField] private string currentStationId = "gachon";
        [SerializeField] private string nextStationId = "moran";
        [SerializeField] private int routeIndex;
        [SerializeField] private int routeStopCount = 6;
        [SerializeField] private bool isTransferStop;
        [SerializeField] private bool isDestination;
        [SerializeField] private DoorOpeningSide openingSide = DoorOpeningSide.Left;

        public TransitProgressSnapshot CurrentTransitProgress => new TransitProgressSnapshot(
            currentStationId,
            nextStationId,
            routeIndex,
            routeStopCount,
            isTransferStop,
            isDestination,
            openingSide);

        public event Action<TransitProgressSnapshot> TransitProgressChanged;

        public void ArriveAt(string stationId, bool destination)
        {
            currentStationId = stationId;
            isDestination = destination;
            TransitProgressChanged?.Invoke(CurrentTransitProgress);
        }

        private void OnValidate()
        {
            TransitProgressChanged?.Invoke(CurrentTransitProgress);
        }
    }
}
