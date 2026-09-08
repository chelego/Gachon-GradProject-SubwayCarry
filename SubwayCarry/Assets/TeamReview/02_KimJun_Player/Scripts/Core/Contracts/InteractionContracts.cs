using UnityEngine;

namespace SubwayCarry.TeamReview.KimJun.Core.Contracts
{
    public enum InteractionKind
    {
        Seat,
        LeanSurface,
        Support,
        TrainDoor,
        StationFacility,
        Vendor,
        Staff
    }

    public enum InteractionResultCode
    {
        Succeeded,
        Unavailable,
        Occupied,
        Blocked,
        InvalidState
    }

    public readonly struct InteractionContext
    {
        public InteractionContext(
            GameObject interactor,
            Vector2 worldPosition,
            Vector2 facingDirection)
        {
            Interactor = interactor;
            WorldPosition = worldPosition;
            FacingDirection = facingDirection;
        }

        public GameObject Interactor { get; }
        public Vector2 WorldPosition { get; }
        public Vector2 FacingDirection { get; }
    }

    public readonly struct InteractionResult
    {
        public InteractionResult(InteractionResultCode code)
        {
            Code = code;
        }

        public InteractionResultCode Code { get; }
        public bool Succeeded => Code == InteractionResultCode.Succeeded;
    }

    public interface IInteractable
    {
        InteractionKind Kind { get; }
        Transform InteractionAnchor { get; }
        bool CanInteract(in InteractionContext context);
        InteractionResult TryInteract(in InteractionContext context);
    }
}
