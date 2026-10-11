using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.Transit
{
    /// <summary>A short authored stair/elevator connection through the common interaction contract.</summary>
    public sealed class StationLevelLink : MonoBehaviour, IInteractable
    {
        [SerializeField] private StationSceneLayout layout;
        [SerializeField] private int destinationLevel;
        [SerializeField] private Transform arrival;
        [SerializeField, Min(0.1f)] private float interactionDistance = 1.8f;

        public InteractionKind Kind => InteractionKind.StationFacility;
        public Transform InteractionAnchor => transform;
        public int DestinationLevel => destinationLevel;
        public Transform Arrival => arrival;

        public void Configure(StationSceneLayout stationLayout, int level, Transform target)
        {
            layout = stationLayout;
            destinationLevel = level;
            arrival = target;
        }

        public bool CanInteract(in InteractionContext context)
        {
            return isActiveAndEnabled && layout != null && arrival != null &&
                context.Interactor != null && destinationLevel >= 0 &&
                destinationLevel < layout.Levels.Length &&
                Vector2.Distance(context.WorldPosition, transform.position) <= interactionDistance &&
                Vector2.Distance(context.Interactor.transform.position, transform.position) <= interactionDistance;
        }

        public InteractionResult TryInteract(in InteractionContext context)
        {
            if (!CanInteract(context)) return new InteractionResult(InteractionResultCode.Unavailable);
            // Actors belong to gameplay, outside the level geometry that is toggled off.
            foreach (StationSceneLayout.Level level in layout.Levels)
                if (context.Interactor.transform.IsChildOf(level.geometry.transform))
                    return new InteractionResult(InteractionResultCode.InvalidState);
            if (!layout.ShowLevel(destinationLevel))
                return new InteractionResult(InteractionResultCode.Unavailable);
            Vector3 destination = arrival.position;
            destination.z = context.Interactor.transform.position.z;
            Rigidbody2D body = context.Interactor.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = destination;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
            context.Interactor.transform.position = destination;
            return new InteractionResult(InteractionResultCode.Succeeded);
        }
    }
}
