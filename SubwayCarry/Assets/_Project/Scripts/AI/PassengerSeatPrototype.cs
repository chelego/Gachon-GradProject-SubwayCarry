using UnityEngine;

namespace SubwayCarry.AI
{
    [DisallowMultipleComponent]
    public sealed class PassengerSeatPrototype : MonoBehaviour
    {
        [SerializeField] private Transform[] sittingPoints;
        [SerializeField] private GameObject[] initialOccupants;

        private GameObject[] occupants;

        public int Capacity => sittingPoints?.Length ?? 0;
        public int AvailableCount
        {
            get
            {
                EnsureRuntimeOccupants();
                int available = 0;

                foreach (GameObject occupant in occupants)
                {
                    if (occupant == null)
                    {
                        available++;
                    }
                }

                return available;
            }
        }

        public void Configure(Transform[] points)
        {
            sittingPoints = points;
            initialOccupants = new GameObject[points.Length];
        }

        public Transform GetSittingPoint(int index)
        {
            if (sittingPoints == null || index < 0 || index >= sittingPoints.Length)
            {
                return null;
            }

            return sittingPoints[index];
        }

        public Vector2 GetApproachPosition(Transform sittingPoint)
        {
            float carCenterY = transform.parent != null ? transform.parent.position.y : 0f;
            float aisleDirection = transform.position.y >= carCenterY ? -1f : 1f;
            return new Vector2(sittingPoint.position.x, transform.position.y + aisleDirection * 0.9f);
        }

        public void SetInitialOccupant(int index, GameObject occupant)
        {
            if (initialOccupants == null || index < 0 || index >= initialOccupants.Length)
            {
                return;
            }

            initialOccupants[index] = occupant;
        }

        public bool TryReserve(GameObject passenger, out Transform sittingPoint)
        {
            EnsureRuntimeOccupants();

            for (int i = 0; i < occupants.Length; i++)
            {
                if (occupants[i] != null)
                {
                    continue;
                }

                occupants[i] = passenger;
                sittingPoint = sittingPoints[i];
                return true;
            }

            sittingPoint = null;
            return false;
        }

        public void Release(GameObject passenger)
        {
            EnsureRuntimeOccupants();

            for (int i = 0; i < occupants.Length; i++)
            {
                if (occupants[i] == passenger)
                {
                    occupants[i] = null;
                }
            }
        }

        private void Awake()
        {
            EnsureRuntimeOccupants();
        }

        private void EnsureRuntimeOccupants()
        {
            int capacity = Capacity;
            if (occupants != null && occupants.Length == capacity)
            {
                return;
            }

            occupants = new GameObject[capacity];
            if (initialOccupants == null)
            {
                return;
            }

            int copyCount = Mathf.Min(initialOccupants.Length, occupants.Length);
            for (int i = 0; i < copyCount; i++)
            {
                occupants[i] = initialOccupants[i];
            }
        }
    }
}
