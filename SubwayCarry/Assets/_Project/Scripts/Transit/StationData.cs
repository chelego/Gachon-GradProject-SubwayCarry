using System;
using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.Transit
{
    public enum DoorOpeningSide
    {
        Left,
        Right,
        Both
    }

    public enum StationFacilityType
    {
        Stairs,
        Escalator,
        Elevator,
        TransferPassage,
        Exit
    }

    public enum PassengerCategory
    {
        OfficeWorker,
        Student,
        General,
        Senior,
        MiddleAged,
        ChildWithGuardian,
        Tourist,
        LateNight,
        EventGroup,
        MobilityAssistance,
        LargeEquipment
    }

    [Serializable]
    public sealed class StationFacilityPlacement
    {
        [SerializeField] private string facilityId;
        [SerializeField] private StationFacilityType facilityType;
        [SerializeField] private Vector2 localPosition;
        [SerializeField] private float rotationDegrees;

        public string FacilityId => facilityId;
        public StationFacilityType FacilityType => facilityType;
        public Vector2 LocalPosition => localPosition;
        public float RotationDegrees => rotationDegrees;
    }

    [Serializable]
    public sealed class DoorTrafficData
    {
        [SerializeField, Min(1)] private int doorNumber = 1;
        [SerializeField, Range(0f, 1f)] private float crowdWeight = 0.5f;
        [SerializeField, Min(0)] private int boardingPassengers;
        [SerializeField, Min(0)] private int alightingPassengers;

        public int DoorNumber => doorNumber;
        public float CrowdWeight => crowdWeight;
        public int BoardingPassengers => boardingPassengers;
        public int AlightingPassengers => alightingPassengers;
    }

    [Serializable]
    public sealed class PassengerMixEntry
    {
        [SerializeField] private PassengerCategory category;
        [SerializeField, Range(0f, 1f)] private float ratio;

        public PassengerCategory Category => category;
        public float Ratio => ratio;
    }

    [Serializable]
    public sealed class StationTimeProfile
    {
        [SerializeField] private string profileName;
        [SerializeField, Range(0, 23)] private int startHour;
        [SerializeField, Range(0, 23)] private int endHour;
        [SerializeField] private List<DoorTrafficData> doorTraffic = new List<DoorTrafficData>();
        [SerializeField] private List<PassengerMixEntry> passengerMix = new List<PassengerMixEntry>();

        public string ProfileName => profileName;
        public int StartHour => startHour;
        public int EndHour => endHour;
        public IReadOnlyList<DoorTrafficData> DoorTraffic => doorTraffic;
        public IReadOnlyList<PassengerMixEntry> PassengerMix => passengerMix;

        public bool ContainsHour(int hour)
        {
            hour = Mathf.Clamp(hour, 0, 23);
            return startHour <= endHour
                ? hour >= startHour && hour <= endHour
                : hour >= startHour || hour <= endHour;
        }
    }

    [Serializable]
    public sealed class StationPlatformData
    {
        [SerializeField] private string platformId;
        [SerializeField] private string lineName;
        [SerializeField] private string boundFor;
        [SerializeField] private DoorOpeningSide doorOpeningSide;
        [SerializeField] private List<StationFacilityPlacement> facilities =
            new List<StationFacilityPlacement>();
        [SerializeField] private List<StationTimeProfile> timeProfiles =
            new List<StationTimeProfile>();

        public string PlatformId => platformId;
        public string LineName => lineName;
        public string BoundFor => boundFor;
        public DoorOpeningSide DoorOpeningSide => doorOpeningSide;
        public IReadOnlyList<StationFacilityPlacement> Facilities => facilities;
        public IReadOnlyList<StationTimeProfile> TimeProfiles => timeProfiles;

        public StationTimeProfile GetTimeProfile(int hour)
        {
            foreach (StationTimeProfile profile in timeProfiles)
            {
                if (profile != null && profile.ContainsHour(hour))
                {
                    return profile;
                }
            }

            return null;
        }
    }

    [CreateAssetMenu(
        fileName = "StationData_",
        menuName = "SubwayCarry/Transit/Station Data")]
    public sealed class StationData : ScriptableObject
    {
        [SerializeField] private string stationId;
        [SerializeField] private string stationName;
        [SerializeField] private bool isTransferStation;
        [SerializeField] private List<string> connectedLines = new List<string>();
        [SerializeField] private List<StationPlatformData> platforms =
            new List<StationPlatformData>();

        public string StationId => stationId;
        public string StationName => stationName;
        public bool IsTransferStation => isTransferStation;
        public IReadOnlyList<string> ConnectedLines => connectedLines;
        public IReadOnlyList<StationPlatformData> Platforms => platforms;

        public StationPlatformData GetPlatform(string platformId)
        {
            foreach (StationPlatformData platform in platforms)
            {
                if (platform != null && platform.PlatformId == platformId)
                {
                    return platform;
                }
            }

            return null;
        }
    }
}
