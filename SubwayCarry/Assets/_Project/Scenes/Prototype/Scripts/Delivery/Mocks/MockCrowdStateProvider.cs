using System;
using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.Delivery.Mocks
{
    public sealed class MockCrowdStateProvider : MonoBehaviour, ICrowdStateProvider
    {
        [SerializeField, Min(0)] private int passengerCount = 8;
        [SerializeField, Min(0)] private int practicalCapacity = 60;
        [SerializeField] private CrowdLevel level = CrowdLevel.Relaxed;

        public CrowdStateSnapshot CurrentCrowdState =>
            new CrowdStateSnapshot(passengerCount, practicalCapacity, level);

        public event Action<CrowdStateSnapshot> CrowdStateChanged;

        private void OnValidate()
        {
            CrowdStateChanged?.Invoke(CurrentCrowdState);
        }
    }
}
