using UnityEngine;
using SubwayCarry.Gameplay;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    // Constrains the teammate controller's intended move without changing that source or its posture/animation APIs.
    [DefaultExecutionOrder(500)]
    public sealed class SlicePlayerBoundary : MonoBehaviour
    {
        public float moveSpeed = 2.2f;
        public bool locked;
        public float radius = 0.28f;
        public SliceNavigationGrid grid;
        public SliceJourneyController journey;
        Vector2 impulse;
        public void AddImpulse(Vector2 velocity) { impulse = Vector2.ClampMagnitude(impulse + velocity, 3); }
        public void ClearImpulse() { impulse = Vector2.zero; }
        Rigidbody2D body; PlayerController input; PlayerPosture posture; PlayerBalance balance;
        void Awake() { body = GetComponent<Rigidbody2D>(); input = GetComponent<PlayerController>(); posture = GetComponent<PlayerPosture>(); balance = GetComponent<PlayerBalance>(); }
        void FixedUpdate()
        {
            // Guided stairs own the body while physics is suspended.
            if (grid == null || !body.simulated) return;
            Vector2 next = body.position;
            if (!locked && input.enabled && posture.CanMove && (balance == null || !balance.ConsumesMovementInput)) next += input.MoveInput * moveSpeed * posture.MoveSpeedMultiplier * Time.fixedDeltaTime;
            if (!locked) next += impulse * Time.fixedDeltaTime;
            impulse = Vector2.MoveTowards(impulse, Vector2.zero, Time.fixedDeltaTime * 7);
            next = grid.Constrain(body.position, next, radius);
            if (journey == null || journey.AllowsGateCrossing(body.position, next)) body.MovePosition(next);
            else body.MovePosition(body.position);
        }
    }
}
