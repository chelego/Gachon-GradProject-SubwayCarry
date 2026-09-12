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
        Rigidbody2D body; PlayerController input; PlayerPosture posture;
        void Awake() { body = GetComponent<Rigidbody2D>(); input = GetComponent<PlayerController>(); posture = GetComponent<PlayerPosture>(); }
        void FixedUpdate()
        {
            if (grid == null) return;
            Vector2 next = body.position;
            if (!locked && posture.CanMove) next += input.MoveInput * moveSpeed * posture.MoveSpeedMultiplier * Time.fixedDeltaTime;
            body.MovePosition(grid.Constrain(body.position, next, radius));
        }
    }
}
