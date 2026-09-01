using UnityEngine;

namespace SubwayCarry.Prototype.Delivery
{
    public enum SchoolServiceType
    {
        DeliveryInsurance,
        TransitFareSupport
    }

    [CreateAssetMenu(
        fileName = "ServiceData_",
        menuName = "SubwayCarry/Delivery/Service Data")]
    public sealed class ServiceData : ScriptableObject
    {
        [SerializeField] private SchoolServiceType serviceType;
        [SerializeField, Min(0)] private int price;

        public SchoolServiceType ServiceType => serviceType;
        public int Price => price;
    }
}
