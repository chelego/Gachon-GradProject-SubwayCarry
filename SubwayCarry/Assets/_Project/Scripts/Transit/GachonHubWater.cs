using UnityEngine;

namespace SubwayCarry.Transit
{
    public sealed class GachonHubWater : MonoBehaviour
    {
        public Renderer[] waterSurfaces;
        private MaterialPropertyBlock properties;
        private static readonly int Map = Shader.PropertyToID("_BaseMap_ST");
        private void Awake() { properties = new MaterialPropertyBlock(); }
        private void Update()
        {
            if (waterSurfaces == null) return;
            properties.SetVector(Map, new Vector4(5, 5, Time.time * .018f, Time.time * .027f));
            foreach (var surface in waterSurfaces)
                if (surface) surface.SetPropertyBlock(properties);
        }
    }
}

