using System.Collections.Generic;
using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PackageImpactSensor : MonoBehaviour
    {
        [SerializeField] private Collider2D packageHitbox;
        [SerializeField] private MonoBehaviour impactReceiverSource;
        [SerializeField] private Transform ignoredOwnerRoot;
        [SerializeField] private LayerMask impactLayers = ~0;
        [SerializeField] private bool includeTriggerColliders;
        [SerializeField, Min(0f)] private float minimumImpactSpeed = 0.5f;
        [SerializeField, Min(0f)] private float impactCooldownSeconds = 0.25f;
        [SerializeField, Range(0f, 1f)] private float minimumCompressionRatio = 0.01f;
        [SerializeField, Min(0.1f)] private float compressionDamageIntervalSeconds = 1f;
        [SerializeField, Min(1)] private int maximumOverlapResults = 16;

        private readonly Dictionary<int, float> nextAllowedImpactTimes = new Dictionary<int, float>();
        private readonly List<int> expiredCooldownIds = new List<int>();

        private IPackageImpactReceiver impactReceiver;
        private ContactFilter2D contactFilter;
        private Collider2D[] overlapResults;
        private Vector2 previousHitboxCenter;
        private bool hasPreviousHitboxCenter;
        private float compressionDuration;

        public Collider2D PackageHitbox => packageHitbox;
        public Transform IgnoredOwnerRoot => ignoredOwnerRoot;

        public void SetIgnoredOwnerRoot(Transform ownerRoot)
        {
            ignoredOwnerRoot = ownerRoot;
        }

        private void Awake()
        {
            ResolveReferences();
            if (packageHitbox == null || impactReceiver == null)
            {
                enabled = false;
                return;
            }

            contactFilter = new ContactFilter2D
            {
                useTriggers = includeTriggerColliders
            };
            contactFilter.SetLayerMask(impactLayers);
            overlapResults = new Collider2D[Mathf.Max(1, maximumOverlapResults)];
        }

        private void OnEnable()
        {
            nextAllowedImpactTimes.Clear();
            expiredCooldownIds.Clear();

            if (packageHitbox == null)
            {
                hasPreviousHitboxCenter = false;
                return;
            }

            previousHitboxCenter = packageHitbox.bounds.center;
            hasPreviousHitboxCenter = true;
            compressionDuration = 0f;
        }

        private void OnDisable()
        {
            nextAllowedImpactTimes.Clear();
            expiredCooldownIds.Clear();
            hasPreviousHitboxCenter = false;
            compressionDuration = 0f;
        }

        private void FixedUpdate()
        {
            if (packageHitbox == null || !packageHitbox.enabled || impactReceiver == null)
            {
                return;
            }

            Vector2 currentHitboxCenter = packageHitbox.bounds.center;
            Vector2 hitboxVelocity = CalculateHitboxVelocity(currentHitboxCenter);
            previousHitboxCenter = currentHitboxCenter;
            hasPreviousHitboxCenter = true;

            float currentTime = Time.time;
            RemoveExpiredCooldowns(currentTime);

            int overlapCount = packageHitbox.Overlap(contactFilter, overlapResults);
            for (int i = 0; i < overlapCount; i++)
            {
                TryApplyImpact(overlapResults[i], hitboxVelocity, currentTime);
            }

            TryApplyCompression(overlapCount);
        }

        private Vector2 CalculateHitboxVelocity(Vector2 currentHitboxCenter)
        {
            if (!hasPreviousHitboxCenter || Time.fixedDeltaTime <= 0f)
            {
                return Vector2.zero;
            }

            return (currentHitboxCenter - previousHitboxCenter) / Time.fixedDeltaTime;
        }

        private void TryApplyImpact(Collider2D other, Vector2 hitboxVelocity, float currentTime)
        {
            if (!IsEligibleImpactCollider(other))
            {
                return;
            }

            int colliderId = other.GetInstanceID();
            if (nextAllowedImpactTimes.TryGetValue(colliderId, out float nextAllowedTime)
                && currentTime < nextAllowedTime)
            {
                return;
            }

            ColliderDistance2D distance = packageHitbox.Distance(other);
            if (!distance.isOverlapped)
            {
                return;
            }

            Vector2 otherVelocity = other.attachedRigidbody != null
                ? other.attachedRigidbody.linearVelocity
                : Vector2.zero;
            Vector2 relativeVelocity = hitboxVelocity - otherVelocity;
            Vector2 contactNormal = distance.normal;
            if (contactNormal.sqrMagnitude <= 0.0001f)
            {
                contactNormal = relativeVelocity.normalized;
            }

            float approachSpeed = Mathf.Max(0f, Vector2.Dot(relativeVelocity, contactNormal));
            if (approachSpeed < minimumImpactSpeed)
            {
                return;
            }

            var impact = new PackageImpactData(
                other.gameObject,
                distance.pointA,
                approachSpeed,
                0f);
            impactReceiver.ApplyImpact(impact);
            nextAllowedImpactTimes[colliderId] = currentTime + impactCooldownSeconds;
        }

        private void TryApplyCompression(int overlapCount)
        {
            Bounds packageBounds = packageHitbox.bounds;
            float horizontalCompression = GetAxisCompression(
                overlapCount,
                packageBounds,
                true,
                out Collider2D horizontalSource,
                out Vector2 horizontalContactPoint);
            float verticalCompression = GetAxisCompression(
                overlapCount,
                packageBounds,
                false,
                out Collider2D verticalSource,
                out Vector2 verticalContactPoint);

            float compressionRatio;
            Collider2D source;
            Vector2 contactPoint;
            if (horizontalCompression >= verticalCompression)
            {
                compressionRatio = horizontalCompression;
                source = horizontalSource;
                contactPoint = horizontalContactPoint;
            }
            else
            {
                compressionRatio = verticalCompression;
                source = verticalSource;
                contactPoint = verticalContactPoint;
            }

            if (source == null || compressionRatio < minimumCompressionRatio)
            {
                compressionDuration = 0f;
                return;
            }

            compressionDuration += Time.fixedDeltaTime;
            if (compressionDuration < compressionDamageIntervalSeconds)
            {
                return;
            }

            compressionDuration -= compressionDamageIntervalSeconds;
            var impact = new PackageImpactData(
                source.gameObject,
                contactPoint,
                0f,
                compressionRatio);
            impactReceiver.ApplyImpact(impact);
        }

        private float GetAxisCompression(
            int overlapCount,
            Bounds packageBounds,
            bool horizontal,
            out Collider2D compressionSource,
            out Vector2 contactPoint)
        {
            compressionSource = null;
            contactPoint = packageBounds.center;

            float axisCenter = horizontal ? packageBounds.center.x : packageBounds.center.y;
            float axisSize = horizontal ? packageBounds.size.x : packageBounds.size.y;
            if (axisSize <= Mathf.Epsilon)
            {
                return 0f;
            }

            float packageAxisMin = horizontal ? packageBounds.min.x : packageBounds.min.y;
            float packageAxisMax = horizontal ? packageBounds.max.x : packageBounds.max.y;
            float packageOtherAxisMin = horizontal ? packageBounds.min.y : packageBounds.min.x;
            float packageOtherAxisMax = horizontal ? packageBounds.max.y : packageBounds.max.x;

            Collider2D negativeCollider = null;
            Collider2D positiveCollider = null;
            float negativeInnerEdge = float.NegativeInfinity;
            float positiveInnerEdge = float.PositiveInfinity;

            for (int i = 0; i < overlapCount; i++)
            {
                Collider2D other = overlapResults[i];
                if (!IsEligibleImpactCollider(other))
                {
                    continue;
                }

                Bounds otherBounds = other.bounds;
                float otherAxisCenter = horizontal ? otherBounds.center.x : otherBounds.center.y;
                float otherAxisMin = horizontal ? otherBounds.min.x : otherBounds.min.y;
                float otherAxisMax = horizontal ? otherBounds.max.x : otherBounds.max.y;
                float otherOtherAxisMin = horizontal ? otherBounds.min.y : otherBounds.min.x;
                float otherOtherAxisMax = horizontal ? otherBounds.max.y : otherBounds.max.x;
                float otherAxisOverlap = Mathf.Min(packageOtherAxisMax, otherOtherAxisMax)
                    - Mathf.Max(packageOtherAxisMin, otherOtherAxisMin);
                if (otherAxisOverlap <= Mathf.Epsilon)
                {
                    continue;
                }

                if (otherAxisCenter < axisCenter && otherAxisMax > negativeInnerEdge)
                {
                    negativeCollider = other;
                    negativeInnerEdge = otherAxisMax;
                }
                else if (otherAxisCenter > axisCenter && otherAxisMin < positiveInnerEdge)
                {
                    positiveCollider = other;
                    positiveInnerEdge = otherAxisMin;
                }
            }

            if (negativeCollider == null || positiveCollider == null)
            {
                return 0f;
            }

            float availableGap = Mathf.Max(0f, positiveInnerEdge - negativeInnerEdge);
            float compressionRatio = Mathf.Clamp01(1f - availableGap / axisSize);
            float negativePenetration = Mathf.Max(0f, negativeInnerEdge - packageAxisMin);
            float positivePenetration = Mathf.Max(0f, packageAxisMax - positiveInnerEdge);
            compressionSource = negativePenetration >= positivePenetration
                ? negativeCollider
                : positiveCollider;

            float contactAxis = (negativeInnerEdge + positiveInnerEdge) * 0.5f;
            contactPoint = horizontal
                ? new Vector2(contactAxis, packageBounds.center.y)
                : new Vector2(packageBounds.center.x, contactAxis);
            return compressionRatio;
        }

        private bool IsEligibleImpactCollider(Collider2D other)
        {
            if (other == null || other == packageHitbox || !other.enabled)
            {
                return false;
            }

            if (other.transform.IsChildOf(transform))
            {
                return false;
            }

            if (ignoredOwnerRoot != null && other.transform.IsChildOf(ignoredOwnerRoot))
            {
                return false;
            }

            return true;
        }

        private void RemoveExpiredCooldowns(float currentTime)
        {
            expiredCooldownIds.Clear();
            foreach (KeyValuePair<int, float> pair in nextAllowedImpactTimes)
            {
                if (currentTime >= pair.Value)
                {
                    expiredCooldownIds.Add(pair.Key);
                }
            }

            foreach (int colliderId in expiredCooldownIds)
            {
                nextAllowedImpactTimes.Remove(colliderId);
            }
        }

        private void ResolveReferences()
        {
            if (packageHitbox == null)
            {
                packageHitbox = GetComponentInChildren<Collider2D>(true);
            }

            impactReceiver = impactReceiverSource as IPackageImpactReceiver;
            if (impactReceiver == null && impactReceiverSource == null)
            {
                MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
                foreach (MonoBehaviour behaviour in behaviours)
                {
                    if (behaviour is IPackageImpactReceiver receiver)
                    {
                        impactReceiverSource = behaviour;
                        impactReceiver = receiver;
                        break;
                    }
                }
            }

            if (packageHitbox == null)
            {
                Debug.LogWarningFormat(this, "{0}: PackageImpactSensor requires a package Collider2D.", name);
            }

            if (impactReceiver == null)
            {
                Debug.LogWarningFormat(
                    this,
                    "{0}: impactReceiverSource must implement IPackageImpactReceiver.",
                    name);
            }
        }
    }
}
