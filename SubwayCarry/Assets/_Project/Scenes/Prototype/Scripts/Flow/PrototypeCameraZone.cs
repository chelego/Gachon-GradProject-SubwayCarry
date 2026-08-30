using UnityEngine;

namespace SubwayCarry.Prototype
{
    [DisallowMultipleComponent]
    public sealed class PrototypeCameraZone : MonoBehaviour
    {
        [SerializeField] private Vector2 worldCenter;
        [SerializeField] private Vector2 worldSize = new Vector2(32f, 14f);

        public Vector2 WorldCenter => worldCenter;
        public Vector2 WorldSize => worldSize;

        public void Configure(Vector2 center, Vector2 size)
        {
            worldCenter = center;
            worldSize = new Vector2(
                Mathf.Max(1f, size.x),
                Mathf.Max(1f, size.y));
        }
    }
}
