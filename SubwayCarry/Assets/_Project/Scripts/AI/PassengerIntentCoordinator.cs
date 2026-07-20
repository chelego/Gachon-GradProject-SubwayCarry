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

    [DisallowMultipleComponent]
    public sealed class PassengerIntentCoordinator : MonoBehaviour
    {
        private readonly HashSet<GeneralPassengerPrototype> passengers =
            new HashSet<GeneralPassengerPrototype>();

        public void Register(GeneralPassengerPrototype passenger)
        {
            if (passenger != null)
            {
                passengers.Add(passenger);
            }
        }

        public void Unregister(GeneralPassengerPrototype passenger)
        {
            passengers.Remove(passenger);
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

        public void PublishSeatIntent(
            GeneralPassengerPrototype source,
            Vector2 sourcePosition,
            Vector2 destination,
            float expiresAt)
        {
            passengers.RemoveWhere(passenger => passenger == null);
            foreach (GeneralPassengerPrototype passenger in passengers)
            {
                if (passenger != null && passenger != source)
                {
                    passenger.ReceiveSeatIntent(
                        source,
                        sourcePosition,
                        destination,
                        expiresAt);
                }
            }
        }
    }
}
