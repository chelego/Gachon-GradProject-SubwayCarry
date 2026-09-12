using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.Prototype.Delivery
{
    [CreateAssetMenu(
        fileName = "Delivery_Data_",
        menuName = "SubwayCarry/Delivery/Delivery Data")]

    public sealed class DeliveryData : ScriptableObject
    {
        [SerializeField] private string deliveryId;
        [SerializeField] private string destinationStationId;
        [SerializeField, Range(1, 5)] private int starRating = 1;
        [SerializeField, Range(0, 23)] private int timeOfDayHour;
        [SerializeField, Min(0)] private int packageValue;
        [SerializeField, Min(0)] private int deliveryFee;
        [SerializeField, Min(0)] private int outboundFare;
        [SerializeField, Min(0)] private int returnFare;
        [SerializeField] private List<string> unlockedDeliveryIds = new List<string>();

        public string DeliveryId => deliveryId;
        public string DestinationStationId => destinationStationId;
        public int StarRating => starRating;
        public int TimeOfDayHour => timeOfDayHour;
        public int PackageValue => packageValue;
        public int DeliveryFee => deliveryFee;
        public int OutboundFare => outboundFare;
        public int ReturnFare => returnFare;
        public IReadOnlyList<string> UnlockedDeliveryIds => unlockedDeliveryIds;
    }
}
