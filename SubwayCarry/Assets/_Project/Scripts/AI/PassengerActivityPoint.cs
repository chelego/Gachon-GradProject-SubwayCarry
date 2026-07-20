using UnityEngine;

namespace SubwayCarry.AI
{
    public enum PassengerActivityType
    {
        Handhold,
        Lean,
        DoorStanding,
        AisleStanding
    }

    [DisallowMultipleComponent]
    public sealed class PassengerActivityPoint : MonoBehaviour
    {
        [SerializeField] private PassengerActivityType activityType;

        private GameObject occupant;

        public PassengerActivityType ActivityType => activityType;

        public void Configure(PassengerActivityType type)
        {
            activityType = type;
        }

        public bool IsAvailableFor(GameObject passenger)
        {
            return occupant == null || occupant == passenger;
        }

        public bool TryReserve(GameObject passenger)
        {
            if (!IsAvailableFor(passenger))
            {
                return false;
            }

            occupant = passenger;
            return true;
        }

        public void Release(GameObject passenger)
        {
            if (occupant == passenger)
            {
                occupant = null;
            }
        }
    }
}
