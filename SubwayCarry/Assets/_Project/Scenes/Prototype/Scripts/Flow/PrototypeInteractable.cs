using SubwayCarry.Core.Contracts;
using SubwayCarry.Prototype.Gameplay;
using UnityEngine;

namespace SubwayCarry.Prototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class PrototypeInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private PrototypeGameFlowController flowController;
        [SerializeField] private PrototypeInteractionAction action;
        [SerializeField] private InteractionKind interactionKind =
            InteractionKind.StationFacility;
        [SerializeField] private string prompt = "E: Interact";

        public InteractionKind Kind => interactionKind;
        public Transform InteractionAnchor => transform;
        public string Prompt => prompt;
        public PrototypeInteractionAction Action => action;

        public void Configure(
            PrototypeGameFlowController flow,
            PrototypeInteractionAction interactionAction,
            InteractionKind kind,
            string interactionPrompt)
        {
            flowController = flow;
            action = interactionAction;
            interactionKind = kind;
            prompt = interactionPrompt;
        }

        public bool CanInteract(in InteractionContext context)
        {
            return flowController != null && flowController.CanPerform(action);
        }

        public InteractionResult TryInteract(in InteractionContext context)
        {
            bool succeeded = flowController != null &&
                             flowController.TryPerform(action);
            return new InteractionResult(
                succeeded
                    ? InteractionResultCode.Succeeded
                    : InteractionResultCode.InvalidState);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() != null)
            {
                flowController?.SetNearbyInteraction(this, true);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() != null)
            {
                flowController?.SetNearbyInteraction(this, false);
            }
        }

        private void OnDisable()
        {
            flowController?.SetNearbyInteraction(this, false);
        }
    }
}
