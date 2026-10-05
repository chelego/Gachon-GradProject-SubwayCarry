using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    [ExecuteAlways]
    public sealed class Metro3DSurfaceUv : MonoBehaviour
    {
        public Vector4 textureTransform=new Vector4(1,1,0,0);
        void OnEnable() { Apply(); }
        public void Apply()
        {
            var block=new MaterialPropertyBlock(); block.SetVector("_BaseMap_ST",textureTransform);
            foreach(var renderer in GetComponentsInChildren<Renderer>()) renderer.SetPropertyBlock(block);
        }
    }
}
