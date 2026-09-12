using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.Prototype.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerPosture))]
    public sealed class PlayerCollisionImpact : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float fallSpeedThreshold = 4f;
        [SerializeField, Min(0f)] private float minimumPackageImpactSpeed = 0.5f;
        [SerializeField] private MonoBehaviour heldPackageSource;

        private Rigidbody2D body;
        private PlayerPosture posture;
        private IPackageImpactReceiver heldPackage;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
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

            TryApplyPackageImpact(
                collision.collider,
                collision.GetContact(0).point,
                relativeSpeed);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!TryGetImpactSource(other, out IPackageImpactSource impactSource))
            {
                return;
            }

            Vector2 playerVelocity = body != null
                ? body.linearVelocity
                : Vector2.zero;
            float relativeSpeed =
                (playerVelocity - impactSource.ImpactVelocity).magnitude;
            if (relativeSpeed >= fallSpeedThreshold)
            {
                posture.Fall();
            }

            TryApplyPackageImpact(
                other,
                other.ClosestPoint(transform.position),
                relativeSpeed);
        }

        private void TryApplyPackageImpact(
            Collider2D other,
            Vector2 contactPoint,
            float relativeSpeed)
        {
            if (heldPackage == null
                || relativeSpeed < minimumPackageImpactSpeed
                || !TryGetImpactSource(other, out _))
            {
                return;
            }

            var impact = new PackageImpactData(
                other.gameObject,
                contactPoint,
                relativeSpeed,
                0f);
            heldPackage.ApplyImpact(impact);
        }

        private static bool TryGetImpactSource(
            Collider2D other,
            out IPackageImpactSource impactSource)
        {
            MonoBehaviour[] behaviours =
                other.GetComponentsInParent<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IPackageImpactSource source)
                {
                    impactSource = source;
                    return true;
                }
            }

            impactSource = null;
            return false;
        }
    }
}
