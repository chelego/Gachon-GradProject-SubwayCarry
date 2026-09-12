using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class StationFareGatePrototype : MonoBehaviour, IInteractable
    {
        [SerializeField] private Collider2D barrierCollider;
        [SerializeField] private Renderer barrierRenderer;
        [SerializeField, Min(0.1f)] private float openDuration = 2.5f;

        private float closeAt;

        public InteractionKind Kind => InteractionKind.StationFacility;
        public Transform InteractionAnchor => transform;
        public bool IsOpen => barrierCollider != null && !barrierCollider.enabled;

        public void Configure(
            Collider2D barrier,
            Renderer visual,
            float duration)
        {
            barrierCollider = barrier;
            barrierRenderer = visual;
            openDuration = Mathf.Max(0.1f, duration);
            CloseGate();
        }

        public bool CanInteract(in InteractionContext context)
        {
            return barrierCollider != null;
        }

        public InteractionResult TryInteract(in InteractionContext context)
        {
            if (barrierCollider == null)
            {
                return new InteractionResult(InteractionResultCode.Unavailable);
            }

            barrierCollider.enabled = false;
            if (barrierRenderer != null)
            {
                barrierRenderer.enabled = false;
            }

            closeAt = Time.time + openDuration;
            return new InteractionResult(InteractionResultCode.Succeeded);
        }

        private void Update()
        {
            if (IsOpen && Time.time >= closeAt)
            {
                CloseGate();
            }
        }

        private void OnDisable()
        {
            CloseGate();
        }

        private void CloseGate()
        {
            if (barrierCollider != null)
            {
                barrierCollider.enabled = true;
            }

            if (barrierRenderer != null)
            {
                barrierRenderer.enabled = true;
            }
        }
    }
}
