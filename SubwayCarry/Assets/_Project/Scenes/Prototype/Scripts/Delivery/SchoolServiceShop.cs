using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.Delivery
{
    public sealed class SchoolServiceShop : MonoBehaviour
    {
        [SerializeField] private EconomyService economyService;
        [SerializeField] private List<ServiceData> catalog = new List<ServiceData>();

        private readonly Dictionary<SchoolServiceType, int> ownedCount = new Dictionary<SchoolServiceType, int>();

        public int GetOwnedCount(SchoolServiceType type)
        {
            return ownedCount.TryGetValue(type, out int count) ? count : 0;
        }

        public bool TryPurchase(SchoolServiceType type)
        {
            ServiceData data = FindData(type);

            if (data == null)
            {
                return false;
            }

            if (!economyService.TrySpend(data.Price))
            {
                return false;
            }

            ownedCount[type] = GetOwnedCount(type) + 1;
            return true;
        }

        public bool TryConsume(SchoolServiceType type)
        {
            int count = GetOwnedCount(type);
            if (count <= 0)
            {
                return false;
            }

            ownedCount[type] = count - 1;
            return true;
        }

        private ServiceData FindData(SchoolServiceType type)
        {
            foreach (ServiceData data in catalog)
            {
                if (data != null && data.ServiceType == type)
                {
                    return data;
                }
            }

            return null;
        }
    }
}
