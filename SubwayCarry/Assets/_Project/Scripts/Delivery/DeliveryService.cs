using System;
using System.Collections.Generic;
using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.Delivery
{
    public sealed class DeliveryService : MonoBehaviour, IDeliveryService, IDeliverySettlementProvider
    {
        [SerializeField] private List<DeliveryData> catalog = new List<DeliveryData>();
        [SerializeField] private EconomyService economyService;
        [SerializeField] private SchoolServiceShop serviceShop;
        [SerializeField] private MonoBehaviour packageDurabilityProviderSource;
        [SerializeField] private MonoBehaviour transitProgressProviderSource;

        private IPackageDurabilityProvider packageDurabilityProvider;
        private IPackageDurabilityResetter packageDurabilityResetter;
        private ITransitProgressProvider transitProgressProvider;

        private DeliveryData activeDelivery;
        private DeliveryStateSnapshot currentState = new DeliveryStateSnapshot(null, null, DeliveryPhase.None, DeliveryFailureReason.None);
        private DeliverySettlementSnapshot lastSettlement;

        public DeliveryStateSnapshot CurrentDeliveryState => currentState;
        public event Action<DeliveryStateSnapshot> DeliveryStateChanged;

        public DeliverySettlementSnapshot LastSettlement => lastSettlement;
        public event Action<DeliverySettlementSnapshot> DeliverySettled;

        private void Awake()
        {
            packageDurabilityProvider = packageDurabilityProviderSource as IPackageDurabilityProvider;
            packageDurabilityResetter = packageDurabilityProviderSource as IPackageDurabilityResetter;
            transitProgressProvider = transitProgressProviderSource as ITransitProgressProvider;

            if (packageDurabilityProvider != null)
            {
                packageDurabilityProvider.DurabilityChanged += OnDurabilityChanged;
            }

            if (transitProgressProvider != null)
            {
                transitProgressProvider.TransitProgressChanged += OnTransitProgressChanged;
            }
        }

        public bool TryStartDelivery(string deliveryId)
        {
            if (activeDelivery != null)
            {
                return false;
            }

            DeliveryData data = FindData(deliveryId);
            if (data == null)
            {
                return false;
            }

            if (!economyService.TrySpend(data.OutboundFare))
            {
                return false;
            }

            packageDurabilityResetter?.ResetToFull();
            activeDelivery = data;
            SetState(new DeliveryStateSnapshot(data.DeliveryId, data.DestinationStationId, DeliveryPhase.InTransit, DeliveryFailureReason.None));
            return true;
        }

        [ContextMenu("Debug Start 1-1")]
        private void DebugStart11()
        {
            TryStartDelivery("1-1");
        }

        private void OnDurabilityChanged(PackageDurabilitySnapshot snapshot)
        {
            if (activeDelivery == null || currentState.Phase != DeliveryPhase.InTransit)
            {
                return;
            }

            if (snapshot.DeliveryFailed || snapshot.CakeDurability <= 0f)
            {
                FailDelivery(DeliveryFailureReason.PackageDestroyed);
            }
        }

        private void OnTransitProgressChanged(TransitProgressSnapshot snapshot)
        {
            if (activeDelivery == null || currentState.Phase != DeliveryPhase.InTransit)
            {
                return;
            }

            if (snapshot.IsDestination)
            {
                CompleteDelivery();
            }
        }

        private void CompleteDelivery()
        {
            PackageDurabilitySnapshot durability = packageDurabilityProvider != null
                ? packageDurabilityProvider.CurrentDurability
                : new PackageDurabilitySnapshot(100f, 100f, false);

            if (durability.CakeDurability < 20f)
            {
                FailDelivery(DeliveryFailureReason.PackageDestroyed);
                return;
            }

            int compensation = Mathf.RoundToInt(
                activeDelivery.PackageValue * (100f - durability.CakeDurability) / 100f);
            bool undamaged = durability.BoxDurability >= 100f && durability.CakeDurability >= 100f;
            int returnFare = undamaged ? 0 : activeDelivery.ReturnFare;

            if (compensation > 0 && serviceShop != null && serviceShop.TryConsume(SchoolServiceType.DeliveryInsurance))
            {
                compensation = 0;
            }

            if (returnFare > 0 && serviceShop != null && serviceShop.TryConsume(SchoolServiceType.TransitFareSupport))
            {
                returnFare = 0;
            }

            int netIncome = activeDelivery.DeliveryFee - compensation - activeDelivery.OutboundFare - returnFare;
            economyService.ApplyDelta(activeDelivery.DeliveryFee - compensation - returnFare);

            FinishSettlement(activeDelivery.DeliveryFee, compensation, returnFare, netIncome, DeliveryPhase.Completed, DeliveryFailureReason.None);
        }
        private void FailDelivery(DeliveryFailureReason reason)
        {
            int compensation = activeDelivery.PackageValue;
            int returnFare = activeDelivery.ReturnFare;

            if (serviceShop != null && serviceShop.TryConsume(SchoolServiceType.DeliveryInsurance))
            {
                compensation = 0;
            }

            if (serviceShop != null && serviceShop.TryConsume(SchoolServiceType.TransitFareSupport))
            {
                returnFare = 0;
            }

            int netIncome = -compensation - activeDelivery.OutboundFare - returnFare;
            economyService.ApplyDelta(-compensation - returnFare);

            FinishSettlement(0, compensation, returnFare, netIncome, DeliveryPhase.Failed, reason);
        }

        private void FinishSettlement(
            int deliveryFee, int compensation, int returnFare, int netIncome,
            DeliveryPhase phase, DeliveryFailureReason reason)
        {
            lastSettlement = new DeliverySettlementSnapshot(
                deliveryFee, compensation, activeDelivery.OutboundFare, returnFare, netIncome);
            DeliverySettled?.Invoke(lastSettlement);

            SetState(new DeliveryStateSnapshot(activeDelivery.DeliveryId, activeDelivery.DestinationStationId, phase, reason));

            activeDelivery = null;
            CheckBankruptcy();
        }

        private void CheckBankruptcy()
        {
            if (economyService.CurrentEconomyState.CurrentCash < 0)
            {
                economyService.ResetToInitial();
                SetState(new DeliveryStateSnapshot(null, null, DeliveryPhase.Failed, DeliveryFailureReason.Bankrupt));
            }
        }

        private void SetState(DeliveryStateSnapshot state)
        {
            currentState = state;
            DeliveryStateChanged?.Invoke(currentState);
        }

        private DeliveryData FindData(string deliveryId)
        {
            foreach (DeliveryData data in catalog)
            {
                if (data != null && data.DeliveryId == deliveryId)
                {
                    return data;
                }
            }

            return null;
        }
    }
}
