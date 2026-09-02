using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.Prototype.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerPosture))]
    public sealed class PlayerCollisionImpact : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float fallSpeedThreshold = 4f;
        [SerializeField] private MonoBehaviour heldPackageSource;

        private PlayerPosture posture;
        private IPackageImpactReceiver heldPackage;

        private void Awake()
        {
            posture = GetComponent<PlayerPosture>();
            heldPackage = heldPackageSource as IPackageImpactReceiver;
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

            if (heldPackage != null)
            {
                var impact = new PackageImpactData(collision.gameObject, collision.GetContact(0).point, relativeSpeed, 0f);
                heldPackage.ApplyImpact(impact);
            }
        }
    }
}
