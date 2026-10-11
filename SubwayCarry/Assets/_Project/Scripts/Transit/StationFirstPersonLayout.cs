using UnityEngine;

namespace SubwayCarry.Transit
{
    /// <summary>Metre-scale station dimensions and 3D gameplay integration points.</summary>
    public sealed class StationFirstPersonLayout : MonoBehaviour
    {
        public string referenceScene;
        public string stationName;
        public bool islandPlatform;
        [Min(0)] public float platformLength;
        [Min(0)] public float platformWidth;
        public Transform playerSpawn;
        public Transform[] platformDoors;
        public Transform[] escalatorLandings;
        public Transform[] seatPositions;
    }
}

