using UnityEngine;

namespace SubwayCarry.AI.V2
{
    // Navigation stays planar; the opt-in motor owns the physical world plane.
    public interface IPassengerAiV2PlanarMotor
    {
        Vector2 Position { get; }
        void Teleport(Vector2 position);
        Vector2 Move(Vector2 position);
    }
}
