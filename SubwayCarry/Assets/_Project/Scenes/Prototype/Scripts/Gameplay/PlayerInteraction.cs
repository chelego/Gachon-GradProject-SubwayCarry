using SubwayCarry.Core.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SubwayCarry.Prototype.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController), typeof(PlayerPosture))]
    public sealed class PlayerInteraction : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float interactionRadius = 1f;

        private PlayerController controller;
        private PlayerPosture posture;
        private IInteractable activeInteractable;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            posture = GetComponent<PlayerPosture>();
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame)
            {
                return;
            }

            if (activeInteractable != null)
            {
                Release();
                return;
            }

            TryInteractWithNearest();
        }

        private void TryInteractWithNearest()
        {
            var context = new InteractionContext(gameObject, transform.position, controller.FacingDirection);
            IInteractable nearest = FindNearestInteractable(context);
            if (nearest == null)
            {
                return;
            }

            InteractionResult result = nearest.TryInteract(context);
            if (!result.Succeeded || !IsPostureKind(nearest.Kind))
            {
                return;
            }

            if (posture.TryTransition(GetPostureFor(nearest.Kind)))
            {
                activeInteractable = nearest;
            }
        }

        private IInteractable FindNearestInteractable(in InteractionContext context)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(context.WorldPosition, interactionRadius);
            IInteractable best = null;
            float bestDistanceSqr = float.MaxValue;

            foreach (var hit in hits)
            {
                var interactable = hit.GetComponentInParent<IInteractable>();
                if (interactable == null || !interactable.CanInteract(context))
                {
                    continue;
                }

                float distanceSqr = ((Vector2)interactable.InteractionAnchor.position - context.WorldPosition).sqrMagnitude;
                if (distanceSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distanceSqr;
                    best = interactable;
                }
            }

            return best;
        }

        private void Release()
        {
            activeInteractable = null;
            posture.TryTransition(PostureState.Standing);
        }

        private static bool IsPostureKind(InteractionKind kind)
        {
            return kind == InteractionKind.Seat
                || kind == InteractionKind.LeanSurface
                || kind == InteractionKind.Support;
        }

        private static PostureState GetPostureFor(InteractionKind kind)
        {
            switch (kind)
            {
                case InteractionKind.Seat:
                    return PostureState.Sitting;

                case InteractionKind.LeanSurface:
                    return PostureState.Leaning;

                case InteractionKind.Support:
                    return PostureState.Holding;

                default:
                    return PostureState.Standing;
            }
        }
    }
}
