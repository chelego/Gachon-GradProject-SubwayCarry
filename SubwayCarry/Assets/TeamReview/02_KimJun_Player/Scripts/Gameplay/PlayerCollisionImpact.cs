using UnityEngine;

namespace SubwayCarry.TeamReview.KimJun.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerPosture))]
    public sealed class PlayerCollisionImpact : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float fallSpeedThreshold = 4f;

        private PlayerPosture posture;

        private void Awake()
        {
            posture = GetComponent<PlayerPosture>();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            float relativeSpeed = collision.relativeVelocity.magnitude;
            if (relativeSpeed <= 0f)
            {
                return;
            }

            if (relativeSpeed >= fallSpeedThreshold)
            {
                posture.Fall();
            }
        }
    }
}
