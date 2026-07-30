using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.AI
{
    public readonly struct PassengerExitIntent
    {
        public readonly GeneralPassengerPrototype Source;
        public readonly PassengerDoorway Doorway;
        public readonly Vector2 SourcePosition;
        public readonly Vector2 AisleEntry;
        public readonly Vector2 AisleExit;
        public readonly Vector2 Destination;
        public readonly float ExpiresAt;

        public PassengerExitIntent(
            GeneralPassengerPrototype source,
            PassengerDoorway doorway,
            Vector2 sourcePosition,
            Vector2 aisleEntry,
            Vector2 aisleExit,
            Vector2 destination,
            float expiresAt)
        {
            Source = source;
            Doorway = doorway;
            SourcePosition = sourcePosition;
            AisleEntry = aisleEntry;
            AisleExit = aisleExit;
            Destination = destination;
            ExpiresAt = expiresAt;
        }
    }

    public readonly struct PassengerPassIntent
    {
        public readonly GeneralPassengerPrototype Source;
        public readonly GeneralPassengerPrototype Recipient;
        public readonly Vector2 SourcePosition;
        public readonly Vector2 Direction;
        public readonly Vector2 Destination;
        public readonly float ExpiresAt;

        public PassengerPassIntent(
            GeneralPassengerPrototype source,
            GeneralPassengerPrototype recipient,
            Vector2 sourcePosition,
            Vector2 direction,
            Vector2 destination,
            float expiresAt)
        {
            Source = source;
            Recipient = recipient;
            SourcePosition = sourcePosition;
            Direction = direction;
            Destination = destination;
            ExpiresAt = expiresAt;
        }
    }

    [DisallowMultipleComponent]
    public sealed class PassengerIntentCoordinator : MonoBehaviour
    {
        private const float SpatialCellSize = 1.5f;

        private readonly HashSet<GeneralPassengerPrototype> passengers =
            new HashSet<GeneralPassengerPrototype>();
        private readonly Dictionary<Vector2Int, List<GeneralPassengerPrototype>>
            spatialPassengers =
                new Dictionary<Vector2Int, List<GeneralPassengerPrototype>>();

        private float lastSpatialRefreshTime = float.MinValue;
        private bool spatialIndexDirty = true;

        public void Register(GeneralPassengerPrototype passenger)
        {
            if (passenger != null)
            {
                passengers.Add(passenger);
                spatialIndexDirty = true;
            }
        }

        public void Unregister(GeneralPassengerPrototype passenger)
        {
            passengers.Remove(passenger);
            spatialIndexDirty = true;
        }

        public void CollectNearbyPassengers(
            Vector2 position,
            float radius,
            GeneralPassengerPrototype excludedPassenger,
            List<GeneralPassengerPrototype> results)
        {
            results.Clear();
            RefreshSpatialIndex();

            int minimumX = Mathf.FloorToInt(
                (position.x - radius) / SpatialCellSize);
            int maximumX = Mathf.FloorToInt(
                (position.x + radius) / SpatialCellSize);
            int minimumY = Mathf.FloorToInt(
                (position.y - radius) / SpatialCellSize);
            int maximumY = Mathf.FloorToInt(
                (position.y + radius) / SpatialCellSize);
            float radiusSquared = radius * radius;

            for (int x = minimumX; x <= maximumX; x++)
            {
                for (int y = minimumY; y <= maximumY; y++)
                {
                    if (!spatialPassengers.TryGetValue(
                            new Vector2Int(x, y),
                            out List<GeneralPassengerPrototype> cellPassengers))
                    {
                        continue;
                    }

                    foreach (GeneralPassengerPrototype passenger in cellPassengers)
                    {
                        if (passenger == null ||
                            passenger == excludedPassenger ||
                            Vector2.SqrMagnitude(
                                passenger.SimulationPosition - position) >
                            radiusSquared)
                        {
                            continue;
                        }

                        results.Add(passenger);
                    }
                }
            }
        }

        public void PublishExitIntent(PassengerExitIntent intent)
        {
            passengers.RemoveWhere(passenger => passenger == null);
            foreach (GeneralPassengerPrototype passenger in passengers)
            {
                if (passenger != null && passenger != intent.Source)
                {
                    passenger.ReceiveExitIntent(intent);
                }
            }
        }

        public void PublishPassIntent(PassengerPassIntent intent)
        {
            if (intent.Recipient == null ||
                intent.Recipient == intent.Source ||
                !passengers.Contains(intent.Recipient))
            {
                return;
            }

            intent.Recipient.ReceivePassIntent(intent);
        }

        public void PublishSeatOccupied(
            GeneralPassengerPrototype source,
            PassengerSeatPrototype seat,
            Transform sittingPoint)
        {
            if (source == null || seat == null || sittingPoint == null)
            {
                return;
            }

            passengers.RemoveWhere(passenger => passenger == null);
            foreach (GeneralPassengerPrototype passenger in passengers)
            {
                if (passenger != null && passenger != source)
                {
                    passenger.ReceiveSeatOccupied(
                        source,
                        seat,
                        sittingPoint);
                }
            }
        }

        private void FixedUpdate()
        {
            RefreshSpatialIndex();
        }

        private void RefreshSpatialIndex()
        {
            if (!spatialIndexDirty &&
                Mathf.Approximately(lastSpatialRefreshTime, Time.fixedTime))
            {
                return;
            }

            passengers.RemoveWhere(passenger => passenger == null);
            spatialPassengers.Clear();

            foreach (GeneralPassengerPrototype passenger in passengers)
            {
                Vector2Int cell = GetSpatialCell(passenger.SimulationPosition);
                if (!spatialPassengers.TryGetValue(
                        cell,
                        out List<GeneralPassengerPrototype> cellPassengers))
                {
                    cellPassengers = new List<GeneralPassengerPrototype>();
                    spatialPassengers.Add(cell, cellPassengers);
                }

                cellPassengers.Add(passenger);
            }

            spatialIndexDirty = false;
            lastSpatialRefreshTime = Time.fixedTime;
        }

        private static Vector2Int GetSpatialCell(Vector2 position)
        {
            return new Vector2Int(
                Mathf.FloorToInt(position.x / SpatialCellSize),
                Mathf.FloorToInt(position.y / SpatialCellSize));
        }
    }
}
