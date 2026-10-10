using UnityEngine;

namespace SubwayCarry.Transit
{
    /// <summary>
    /// Marks a root GameObject as a station section within the unified
    /// StationGameplay scene. Each section contains the visual layout
    /// (Grid, Tilemap, sprites, prefab instances) for one station.
    /// The <see cref="StationGameplayController"/> activates and deactivates
    /// sections as the player moves between stations.
    /// </summary>
    public sealed class StationSection : MonoBehaviour
    {
        [Header("Station Identity")]
        [SerializeField] private StationData stationData;
        [SerializeField] private string sectionLabel;

        [Header("Key Points")]
        [Tooltip("Where the player appears when arriving at this station.")]
        [SerializeField] private Transform spawnPoint;

        [Tooltip("Camera target position when this section is active.")]
        [SerializeField] private Transform cameraTarget;

        [Tooltip("Orthographic size override for the camera. 0 uses the default.")]
        [SerializeField, Min(0f)] private float cameraOrthoSize;

        /// <summary>Station data asset. May be null during early development.</summary>
        public StationData Data => stationData;

        /// <summary>Human-readable label shown in debug views.</summary>
        public string SectionLabel => string.IsNullOrEmpty(sectionLabel)
            ? gameObject.name
            : sectionLabel;

        /// <summary>Player spawn point within this station.</summary>
        public Transform SpawnPoint => spawnPoint;

        /// <summary>Where the camera should look when this station is active.</summary>
        public Transform CameraTarget => cameraTarget;

        /// <summary>Camera orthographic size. Zero means use the controller default.</summary>
        public float CameraOrthoSize => cameraOrthoSize;

        /// <summary>Show or hide the entire station section.</summary>
        public void Activate(bool active)
        {
            gameObject.SetActive(active);
        }
    }
}
