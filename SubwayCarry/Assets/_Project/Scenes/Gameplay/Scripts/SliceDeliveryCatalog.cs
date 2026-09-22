using System;
using UnityEngine;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    [Serializable]
    public sealed class SliceRouteStop
    {
        public string id, label, line;
        public bool transfer;
        public int platformMap = 2;
    }

    [Serializable]
    public sealed class SliceDeliveryOrder
    {
        public string id, title;
        [Range(1, 5)] public int stars = 1;
        public bool provisional = true;
        [Min(0)] public int value = 25000, fee = 3000, outboundFare = 1500, returnFare = 1500;
        [Range(0, 23)] public int hour = 13;
        [Range(4, 40)] public int passengers = 18;
        public SliceRouteStop[] stops;
    }

    [Serializable]
    public sealed class SliceRouteHighlight
    {
        public string orderId;
        public TextAsset png;
        // Top-left coordinates normalized against the unchanged full map.
        public Rect normalizedRect;
    }

    [CreateAssetMenu(menuName = "SubwayCarry/Art Slice/Delivery Catalog")]
    public sealed class SliceDeliveryCatalog : ScriptableObject
    {
        public Font uiFont;
        public Texture2D damageAtlas;
        public Texture2D damageBurst;
        public Texture2D metroMapImage;
        public SliceRouteHighlight[] routeHighlights;
        public Texture2D stationArtAtlas;
        [Header("Damage presentation (percent points / real seconds)")]
        [Range(1, 50)] public float damageCutInThreshold = 18;
        [Range(1, 6)] public float damageCutInSeconds = 2.8f;
        [Range(0, 8)] public float damageCutInCooldown = 4;
        [Range(.2f, 3)] public float recoilScale = .8f;
        public Sprite[] passengerSittingSprites;
        public SubwayCarry.AI.V2.PassengerAiV2PersonalityProfile[] passengerProfiles;
        [Min(1)] public float travelSeconds = 30;
        [Min(1)] public float doorsOpenSeconds = 12;
        [Min(0.1f)] public float doorAnimationSeconds = 1;
        [Min(1)] public float arrivalDelaySeconds = 2;
        [Min(0)] public int insurancePrice = 1200, fareSupportPrice = 1000;
        [Min(1)] public int upgradeBasePrice = 1800, maximumUpgradeLevel = 3;
        [Range(0, 1)] public float staminaPerLevel = 0.2f, balancePerLevel = 0.12f, agilityPerLevel = 0.08f;
        public SliceDeliveryOrder[] orders;
        [HideInInspector] public SliceDeliveryOrder[] legacyOrders;

        public bool IsValid(out string error)
        {
            error = null;
            if (uiFont == null || travelSeconds < 5 || doorsOpenSeconds < 1 || doorAnimationSeconds <= 0 || arrivalDelaySeconds < 1)
            { error = "UI 폰트 또는 운행 시간을 확인하세요."; return false; }
            if (orders == null || orders.Length == 0) { error = "배송 데이터가 없습니다."; return false; }
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var order in orders)
            {
                if (order == null || string.IsNullOrEmpty(order.id) || !ids.Add(order.id) ||
                    order.stops == null || order.stops.Length < 2 || order.stops[0] == null || order.stops[0].id != "gachon" ||
                    order.value < 0 || order.fee < 0 || order.outboundFare < 0 || order.returnFare < 0)
                { error = "배송 ID 또는 가천대 출발 노선을 확인하세요."; return false; }
                foreach (var stop in order.stops)
                    if (stop == null || string.IsNullOrEmpty(stop.id) || string.IsNullOrEmpty(stop.line))
                    { error = "역 ID와 노선이 필요합니다."; return false; }
            }
            return true;
        }
    }
}
