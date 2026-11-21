using TMPro;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    // Prefab-local map data. The route supplies the station index and the live train service.
    public sealed class Metro3DStationLayout : MonoBehaviour
    {
        public Transform entrance, unpaidHall, westTop, westBottom, eastTop, eastBottom;
        public Transform exit;
        public Rect entryWaitingBounds;
        [Min(0)] public int entryWaitingLimit;
        public bool preferDirectEntranceWalk;
        public bool useLocalFareGateMovement;
        public Rect hallBounds, platformBounds;
        public float tunnelStart, tunnelEnd;
        public GameObject[] previewTunnelEnds;

        public Vector3 Local(Transform anchor) => transform.InverseTransformPoint(anchor.position);
        public Vector3 Stair(int side, bool top) =>
            Local(side > 0 ? (top ? westTop : westBottom) : (top ? eastTop : eastBottom));

        internal void ApplySignage(string station, string english, string number, string west, string westLine,
            string east, string eastLine)
        {
            string identity = string.IsNullOrEmpty(number) ? english :
                string.IsNullOrEmpty(english) ? number : number + "  ·  " + english;
            foreach (var sign in GetComponentsInChildren<TextMeshPro>(true))
            {
                if (!sign.text.Contains("{")) continue;
                sign.text = sign.text.Replace("{station}", station).Replace("{english}", english ?? "")
                    .Replace("{number}", number).Replace("{identity}", identity ?? "")
                    .Replace("{west}", west).Replace("{east}", east)
                    .Replace("{westLine}", westLine).Replace("{eastLine}", eastLine);
            }
            foreach (var facility in GetComponentsInChildren<Metro3DFacility>())
                if (facility.gateDoor)
                    facility.caption = station + "역 개찰구";
        }

        public void Connect(Metro3DWorld world, int index)
        {
            foreach (var end in previewTunnelEnds) end.SetActive(false);
            foreach (var facility in GetComponentsInChildren<Metro3DFacility>())
            {
                if (facility.gateDoor)
                {
                    // The shared gate queue, fare settlement and save slot all use station-local lanes.
                    facility.transform.SetParent(transform, true);
                    facility.kind = index == 0 ? Metro3DFacilityKind.FareGate : Metro3DFacilityKind.ExitGate;
                }
                world.facilities.Add(facility);
            }
            foreach (var bank in GetComponentsInChildren<Metro3DPlatformDoors>())
            {
                bank.world = world;
                bank.stationIndex = index;
                foreach (var indicator in bank.GetComponentsInChildren<Metro3DDoorStatus>())
                {
                    indicator.world = world;
                    indicator.stationIndex = index;
                    indicator.side = bank.trainSide;
                }
            }
        }
    }
}
