using System;
using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.Delivery.Mocks
{
    public sealed class MockPackageDurabilityProvider : MonoBehaviour, IPackageDurabilityProvider
    {
        [SerializeField, Range(0f, 100f)] private float boxDurability = 100f;
        [SerializeField, Range(0f, 100f)] private float cakeDurability = 100f;
        [SerializeField] private bool deliveryFailed;

        public PackageDurabilitySnapshot CurrentDurability => new PackageDurabilitySnapshot(boxDurability, cakeDurability, deliveryFailed);
        public event Action<PackageDurabilitySnapshot> DurabilityChanged;

        public void SetDurability(float box, float cake, bool failed)
        {
            boxDurability = Mathf.Clamp(box, 0f, 100f);
            cakeDurability = Mathf.Clamp(cake, 0f, 100f);
            deliveryFailed = failed;
            DurabilityChanged?.Invoke(CurrentDurability);
        }

        private void OnValidate()
        {
            DurabilityChanged?.Invoke(CurrentDurability);
        }
    }
}
