using SubwayCarry.Prototype.ArtMapSlice;
using UnityEngine;
using UnityEngine.Rendering;

namespace SubwayCarry.Gameplay3D
{
    [CreateAssetMenu(menuName = "SubwayCarry/3D/Settings")]
    public sealed class Metro3DSettings : ScriptableObject
    {
        public SliceDeliveryCatalog deliveries;
        public RenderPipelineAsset renderPipeline;
        public GameObject passengerPrefab;
        public GameObject environmentLibrary;
        public AnimationClip idle, walk, sit;
        public Material tile, stone, metal, glass, yellow, rubber, seat, ceiling, luminous, box;
        public Material wood, green, brown, blue, white, red;
        public Font font;
        [Min(1)] public float walkSpeed = 2.3f;
        [Min(1)] public float runSpeed = 3.5f;
        [Range(.02f, .3f)] public float mouseSensitivity = .085f;
        [Range(0, .05f)] public float headBob = .012f;
        [Range(4, 40)] public int passengerCount = 16;
        [Min(1)] public float approachSeconds = 8, dwellSeconds = 22, travelSeconds = 30;
        [Min(.2f)] public float doorSeconds = 1.25f;
        public float departureFloor = 3.6f;
        public float npcSpeed = 1.25f;
        public string ModuleName(string name) => name;
    }
}
