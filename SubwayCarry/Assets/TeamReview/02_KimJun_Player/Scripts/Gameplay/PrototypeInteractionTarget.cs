using SubwayCarry.TeamReview.KimJun.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.TeamReview.KimJun.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class PrototypeInteractionTarget : MonoBehaviour, IInteractable
    {
        [SerializeField] private InteractionKind kind;
        [SerializeField] private Transform interactionAnchor;

        public InteractionKind Kind => kind;
        public Transform InteractionAnchor => interactionAnchor != null ? interactionAnchor : transform;

        public void Configure(InteractionKind targetKind, Transform targetAnchor)
        {
            kind = targetKind;
            interactionAnchor = targetAnchor;
        }

        public bool CanInteract(in InteractionContext context)
        {
            return context.Interactor != null;
        }

        public InteractionResult TryInteract(in InteractionContext context)
        {
            return new InteractionResult(
                context.Interactor != null
                    ? InteractionResultCode.Succeeded
                    : InteractionResultCode.InvalidState);
        }
    }
}
