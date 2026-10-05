using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    public sealed class Metro3DStationReference : MonoBehaviour
    {
        public string referenceStation;
        public bool realTopology;
        public bool dimensionsAreEstimated;
        [TextArea] public string layoutSource="KRIC KR_K1_0011: B1 concourse, B2 opposing side platforms, end circulation, exits 1–5. Dimensions estimated; platform compressed for playable carriage.";
    }
}
