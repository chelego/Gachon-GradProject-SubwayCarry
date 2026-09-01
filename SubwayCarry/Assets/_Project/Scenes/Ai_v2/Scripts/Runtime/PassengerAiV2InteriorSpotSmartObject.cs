using UnityEngine;

namespace SubwayCarry.AI.V2
{
    public enum PassengerAiV2InteriorSpotKind
    {
        Seat,
        Stand,
        Lean,
        DoorPrepare
    }

    /// <summary>
    /// 객차 안의 좌석, 입석, 기대기와 하차 준비 위치를 같은 예약 계약으로 제공한다.
    /// </summary>
    public sealed class PassengerAiV2InteriorSpotSmartObject : MonoBehaviour, IPassengerAiV2SmartObject
    {
        [SerializeField] private string smartObjectId;
        [SerializeField] private PassengerAiV2InteriorSpotKind kind;
        [SerializeField] private Vector2 usePosition;
        [SerializeField, Range(0f, 1f)] private float comfort = 0.5f;
        [SerializeField, Range(0f, 1f)] private float exitPreparationCost = 0.5f;
        [SerializeField] private SpriteRenderer markerRenderer;

        private PassengerAiV2Agent reservedBy;
        private PassengerAiV2Agent occupiedBy;
        private Color availableColor;

        public string SmartObjectId => smartObjectId;
        public int Capacity => 1;
        public PassengerAiV2InteriorSpotKind Kind => kind;
        public Vector2 UsePosition => usePosition;
        public float Comfort => comfort;
        public float ExitPreparationCost => exitPreparationCost;
        public bool IsReserved => reservedBy != null;
        public bool IsOccupied => occupiedBy != null;
        public int InvalidReservationCount { get; private set; }

        public void Configure(
            string objectId,
            PassengerAiV2InteriorSpotKind spotKind,
            Vector2 position,
            float comfortValue,
            float exitCost,
            SpriteRenderer visual)
        {
            smartObjectId = objectId;
            kind = spotKind;
            usePosition = position;
            comfort = Mathf.Clamp01(comfortValue);
            exitPreparationCost = Mathf.Clamp01(exitCost);
            markerRenderer = visual;
            availableColor = markerRenderer != null ? markerRenderer.color : Color.white;
            UpdateVisual();
        }

        public bool RequestUse(PassengerAiV2Agent agent, int direction, float approachDistance)
        {
            _ = direction;
            _ = approachDistance;
            if (agent == null)
            {
                return false;
            }

            if (reservedBy != null && reservedBy != agent)
            {
                InvalidReservationCount++;
                return false;
            }

            reservedBy = agent;
            UpdateVisual();
            return true;
        }

        public bool HasReservation(PassengerAiV2Agent agent)
        {
            return agent != null && reservedBy == agent;
        }

        public bool IsAvailableFor(PassengerAiV2Agent agent)
        {
            return reservedBy == null || reservedBy == agent;
        }

        public bool MarkOccupied(PassengerAiV2Agent agent)
        {
            if (!HasReservation(agent))
            {
                InvalidReservationCount++;
                return false;
            }

            occupiedBy = agent;
            UpdateVisual();
            return true;
        }

        public void Release(PassengerAiV2Agent agent)
        {
            if (agent == null || (reservedBy != agent && occupiedBy != agent))
            {
                return;
            }

            if (reservedBy == agent)
            {
                reservedBy = null;
            }

            if (occupiedBy == agent)
            {
                occupiedBy = null;
            }

            UpdateVisual();
        }

        public void Cancel(PassengerAiV2Agent agent)
        {
            Release(agent);
        }

        public void ForceReset()
        {
            reservedBy = null;
            occupiedBy = null;
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            if (markerRenderer == null)
            {
                return;
            }

            if (occupiedBy != null)
            {
                markerRenderer.color = Color.Lerp(availableColor, new Color(0.28f, 0.9f, 0.68f, 1f), 0.58f);
            }
            else if (reservedBy != null)
            {
                markerRenderer.color = Color.Lerp(availableColor, Color.white, 0.28f);
            }
            else
            {
                markerRenderer.color = availableColor;
            }
        }
    }
}
