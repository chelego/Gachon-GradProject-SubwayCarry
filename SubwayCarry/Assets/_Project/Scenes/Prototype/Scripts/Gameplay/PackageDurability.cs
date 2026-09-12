using System;
using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.Prototype.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PackageDurability : MonoBehaviour, IPackageDurabilityProvider, IPackageImpactReceiver
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [ContextMenu("Debug Set Intact (100/100)")]
        private void DebugSetIntact()
        {
            SetDebugDurability(100f, 100f);
        }

        [ContextMenu("Debug Set Slight Damage (70/100)")]
        private void DebugSetSlightDamage()
        {
            SetDebugDurability(70f, 100f);
        }

        [ContextMenu("Debug Set Heavy Damage (40/100)")]
        private void DebugSetHeavyDamage()
        {
            SetDebugDurability(40f, 100f);
        }

        [ContextMenu("Debug Set Destroyed (0/20)")]
        private void DebugSetDestroyed()
        {
            SetDebugDurability(0f, 20f);
        }

        private void SetDebugDurability(float box, float cake)
        {
            boxDurability = Mathf.Clamp(box, 0f, 100f);
            cakeDurability = Mathf.Clamp(cake, 0f, 100f);
            deliveryFailed = false;
            DurabilityChanged?.Invoke(CurrentDurability);
        }
#endif
    }
}
