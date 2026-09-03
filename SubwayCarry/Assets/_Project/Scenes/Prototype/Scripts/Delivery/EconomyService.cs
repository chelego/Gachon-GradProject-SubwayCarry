using System;
using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.Prototype.Delivery
{
    public sealed class EconomyService : MonoBehaviour, IEconomyStateProvider
    {
        [SerializeField, Min(0)] private int initialCash = 15000;

        private int currentCash;

        public EconomyStateSnapshot CurrentEconomyState => new EconomyStateSnapshot(currentCash);
        public event Action<EconomyStateSnapshot> EconomyStateChanged;

        private void Awake()
        {
            currentCash = initialCash; 
        }

        public void ApplyDelta(int delta)
        {
            currentCash += delta;
            EconomyStateChanged?.Invoke(CurrentEconomyState);
        }

        public bool TrySpend(int amount)
        {
            if(amount <= 0 || currentCash < amount)
            {
                return false;
            }
            ApplyDelta(-amount);
            return true;
        }

        public void ResetToInitial()
        {
            currentCash = initialCash;
            EconomyStateChanged?.Invoke(CurrentEconomyState);
        }
    }
}
