using System;
using SubwayCarry.TeamReview.KimJun.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.TeamReview.KimJun.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PackageDurability : MonoBehaviour,
        IPackageDurabilityProvider,
        IPackageDurabilityResetter,
        IPackageImpactReceiver
    {
        [SerializeField, Min(0f)] private float speedDamagePerUnit = 12f;
        [SerializeField, Min(0f)] private float compressionDamageScale = 60f;

        private float boxDurability = 100f;
        private float cakeDurability = 100f;
        private bool deliveryFailed;

        public PackageDurabilitySnapshot CurrentDurability => new PackageDurabilitySnapshot(boxDurability, cakeDurability, deliveryFailed);
        public event Action<PackageDurabilitySnapshot> DurabilityChanged;

        public void ApplyImpact(in PackageImpactData impact)
        {
            float damage = impact.RelativeSpeed * speedDamagePerUnit + impact.CompressionRatio * compressionDamageScale;
            if (damage <= 0f || deliveryFailed)
            {
                return;
            }

            float boxDamage = Mathf.Min(damage, boxDurability);
            boxDurability -= boxDamage;

            float overflowDamage = damage - boxDamage;
            if (overflowDamage > 0f)
            {
                cakeDurability = Mathf.Max(0f, cakeDurability - overflowDamage);
            }

            if (cakeDurability <= 0f)
            {
                deliveryFailed = true;
            }

            DurabilityChanged?.Invoke(CurrentDurability);
        }

        public void ResetToFull()
        {
            boxDurability = 100f;
            cakeDurability = 100f;
            deliveryFailed = false;
            DurabilityChanged?.Invoke(CurrentDurability);
        }
    }
}
