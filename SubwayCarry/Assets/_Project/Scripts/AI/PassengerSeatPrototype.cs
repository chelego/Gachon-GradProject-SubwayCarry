using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.AI
{
    [DisallowMultipleComponent]
    public sealed class PassengerSeatPrototype : MonoBehaviour
    {
        [SerializeField] private Transform[] sittingPoints;
        [SerializeField] private GameObject[] initialOccupants;
        [SerializeField] private int[] debugSeatNumbers;

        private GameObject[] occupants;
        private TextMesh[] debugNumberLabels;

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

        public int GetSittingPointIndex(Transform sittingPoint)
        {
            if (sittingPoint == null || sittingPoints == null)
            {
                return -1;
            }

            for (int i = 0; i < sittingPoints.Length; i++)
            {
                if (sittingPoints[i] == sittingPoint)
                {
                    return i;
                }
            }

            return -1;
        }

        public int GetNearestOccupiedSlotDistance(
            Transform sittingPoint,
            GameObject ignoredOccupant = null)
        {
            EnsureRuntimeOccupants();
            int index = GetSittingPointIndex(sittingPoint);
            if (index < 0)
            {
                return 0;
            }

            int nearest = int.MaxValue;
            for (int i = 0; i < occupants.Length; i++)
            {
                if (i == index ||
                    occupants[i] == null ||
                    occupants[i] == ignoredOccupant)
                {
                    continue;
                }

                nearest = Mathf.Min(nearest, Mathf.Abs(i - index));
            }

            return nearest;
        }

        public int GetOccupiedNeighborCount(
            Transform sittingPoint,
            int slotRadius,
            GameObject ignoredOccupant = null)
        {
            EnsureRuntimeOccupants();
            int index = GetSittingPointIndex(sittingPoint);
            if (index < 0)
            {
                return 0;
            }

            int count = 0;
            int radius = Mathf.Max(0, slotRadius);
            for (int i = 0; i < occupants.Length; i++)
            {
                if (i == index ||
                    Mathf.Abs(i - index) > radius ||
                    occupants[i] == null ||
                    occupants[i] == ignoredOccupant)
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        public int GetDebugSeatNumber(Transform sittingPoint)
        {
            if (sittingPoint == null ||
                sittingPoints == null ||
                debugSeatNumbers == null)
            {
                return 0;
            }

            for (int i = 0; i < sittingPoints.Length; i++)
            {
                if (sittingPoints[i] == sittingPoint &&
                    i < debugSeatNumbers.Length)
                {
                    return debugSeatNumbers[i];
                }
            }

            return 0;
        }

        public static void EnsureDebugNumbering(PassengerSeatPrototype[] sceneSeats)
        {
            if (sceneSeats == null)
            {
                return;
            }

            var sortedSeats = new List<PassengerSeatPrototype>();
            foreach (PassengerSeatPrototype seat in sceneSeats)
            {
                if (seat != null)
                {
                    sortedSeats.Add(seat);
                }
            }

            sortedSeats.Sort((left, right) =>
            {
                float leftCarX = left.transform.parent != null
                    ? left.transform.parent.position.x
                    : left.transform.position.x;
                float rightCarX = right.transform.parent != null
                    ? right.transform.parent.position.x
                    : right.transform.position.x;
                int carOrder = leftCarX.CompareTo(rightCarX);
                if (carOrder != 0)
                {
                    return carOrder;
                }

                int sideOrder = -left.transform.position.y.CompareTo(
                    right.transform.position.y);
                return sideOrder != 0
                    ? sideOrder
                    : left.transform.position.x.CompareTo(right.transform.position.x);
            });

            int nextNumber = 1;
            foreach (PassengerSeatPrototype seat in sortedSeats)
            {
                seat.AssignDebugSeatNumbers(ref nextNumber);
            }
        }

        public bool IsAvailable(Transform sittingPoint)
        {
            EnsureRuntimeOccupants();

            if (sittingPoint == null || sittingPoints == null)
            {
                return false;
            }

            for (int i = 0; i < sittingPoints.Length; i++)
            {
                if (sittingPoints[i] == sittingPoint)
                {
                    return occupants[i] == null;
                }
            }

            return false;
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

        public bool TryReserve(
            GameObject passenger,
            Transform preferredPoint,
            out Transform sittingPoint)
        {
            EnsureRuntimeOccupants();

            if (preferredPoint == null || sittingPoints == null)
            {
                sittingPoint = null;
                return false;
            }

            for (int i = 0; i < sittingPoints.Length; i++)
            {
                if (sittingPoints[i] != preferredPoint || occupants[i] != null)
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

        public bool TryReserveClosestAvailable(
            GameObject passenger,
            Transform preferredPoint,
            int maximumSlotDistance,
            out Transform sittingPoint)
        {
            EnsureRuntimeOccupants();
            int preferredIndex = GetSittingPointIndex(preferredPoint);
            if (preferredIndex < 0)
            {
                sittingPoint = null;
                return false;
            }

            int allowedDistance = Mathf.Max(0, maximumSlotDistance);
            for (int distance = 0; distance <= allowedDistance; distance++)
            {
                int leftIndex = preferredIndex - distance;
                if (leftIndex >= 0 && occupants[leftIndex] == null)
                {
                    occupants[leftIndex] = passenger;
                    sittingPoint = sittingPoints[leftIndex];
                    return true;
                }

                int rightIndex = preferredIndex + distance;
                if (distance > 0 &&
                    rightIndex < occupants.Length &&
                    occupants[rightIndex] == null)
                {
                    occupants[rightIndex] = passenger;
                    sittingPoint = sittingPoints[rightIndex];
                    return true;
                }
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

        private void AssignDebugSeatNumbers(ref int nextNumber)
        {
            int capacity = Capacity;
            if (debugSeatNumbers == null || debugSeatNumbers.Length != capacity)
            {
                debugSeatNumbers = new int[capacity];
            }

            for (int i = 0; i < capacity; i++)
            {
                debugSeatNumbers[i] = nextNumber++;
            }

            EnsureDebugNumberLabels();
        }

        private void EnsureDebugNumberLabels()
        {
            if (sittingPoints == null)
            {
                return;
            }

            if (debugNumberLabels == null ||
                debugNumberLabels.Length != sittingPoints.Length)
            {
                debugNumberLabels = new TextMesh[sittingPoints.Length];
            }

            for (int i = 0; i < sittingPoints.Length; i++)
            {
                if (sittingPoints[i] == null)
                {
                    continue;
                }

                if (debugNumberLabels[i] == null)
                {
                    GameObject labelObject =
                        new GameObject("Seat Number " + debugSeatNumbers[i]);
                    Transform labelTransform = labelObject.transform;
                    labelTransform.SetParent(transform.parent);
                    labelTransform.position =
                        sittingPoints[i].position + new Vector3(0f, 0f, -0.45f);

                    TextMesh label = labelObject.AddComponent<TextMesh>();
                    label.anchor = TextAnchor.MiddleCenter;
                    label.alignment = TextAlignment.Center;
                    label.characterSize = 0.055f;
                    label.fontSize = 44;
                    label.color = new Color(0.95f, 0.98f, 1f);
                    debugNumberLabels[i] = label;
                }

                debugNumberLabels[i].text = debugSeatNumbers[i].ToString();
            }
        }
    }
}
