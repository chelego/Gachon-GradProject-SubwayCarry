using System;
using UnityEngine;

namespace SubwayCarry.Transit
{
    /// <summary>
    /// Manages multiple <see cref="StationSection"/> instances in a single
    /// unified scene. Only one section is visible at a time. The controller
    /// handles camera repositioning and section activation when the active
    /// station changes.
    ///
    /// Usage in StationGameplay scene:
    ///   1. Attach to a root-level manager GameObject.
    ///   2. Assign all StationSection references in the inspector.
    ///   3. Call <see cref="SwitchToStation(int)"/> or
    ///      <see cref="SwitchToStation(string)"/> from game-flow code.
    /// </summary>
    public sealed class StationGameplayController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private StationSection[] stations = Array.Empty<StationSection>();
        [SerializeField] private Camera mainCamera;

        [Header("Camera Defaults")]
        [SerializeField, Min(1f)] private float defaultOrthoSize = 15f;
        [SerializeField, Min(0f)] private float cameraTransitionSpeed = 12f;

        private int activeIndex = -1;
        private Vector3 cameraTargetPosition;
        private float cameraTargetOrtho;
        private bool isTransitioning;

        /// <summary>Fires when the active station changes. Argument is the new section.</summary>
        public event Action<StationSection> StationChanged;

        /// <summary>Currently active station section, or null if none.</summary>
        public StationSection ActiveStation =>
            activeIndex >= 0 && activeIndex < stations.Length
                ? stations[activeIndex]
                : null;

        /// <summary>Number of registered station sections.</summary>
        public int StationCount => stations.Length;

        private void Awake()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
        }

        private void Start()
        {
            if (stations.Length > 0 && activeIndex < 0)
            {
                SwitchToStation(0);
            }
        }

        private void LateUpdate()
        {
            if (!isTransitioning || mainCamera == null)
            {
                return;
            }

            Vector3 camPos = mainCamera.transform.position;
            camPos = Vector3.Lerp(camPos, cameraTargetPosition,
                cameraTransitionSpeed * Time.deltaTime);
            mainCamera.transform.position = camPos;
            mainCamera.orthographicSize = Mathf.Lerp(
                mainCamera.orthographicSize,
                cameraTargetOrtho,
                cameraTransitionSpeed * Time.deltaTime);

            float distance = Vector3.Distance(camPos, cameraTargetPosition);
            float orthoDiff = Mathf.Abs(mainCamera.orthographicSize - cameraTargetOrtho);
            if (distance < 0.01f && orthoDiff < 0.01f)
            {
                mainCamera.transform.position = cameraTargetPosition;
                mainCamera.orthographicSize = cameraTargetOrtho;
                isTransitioning = false;
            }
        }

        /// <summary>
        /// Switch to the station at the given index.
        /// All other sections are deactivated.
        /// </summary>
        public void SwitchToStation(int index)
        {
            if (index < 0 || index >= stations.Length)
            {
                Debug.LogWarning(
                    $"[StationGameplayController] Invalid station index: {index}");
                return;
            }

            activeIndex = index;
            for (int i = 0; i < stations.Length; i++)
            {
                if (stations[i] != null)
                {
                    stations[i].Activate(i == index);
                }
            }

            StationSection section = stations[index];
            UpdateCameraTarget(section);
            StationChanged?.Invoke(section);

            Debug.Log(
                $"[StationGameplayController] Switched to station: {section.SectionLabel}");
        }

        /// <summary>
        /// Switch to the station whose <see cref="StationData.StationId"/>
        /// matches the given ID.
        /// </summary>
        public bool SwitchToStation(string stationId)
        {
            for (int i = 0; i < stations.Length; i++)
            {
                StationSection section = stations[i];
                if (section == null)
                {
                    continue;
                }

                if (section.Data != null &&
                    section.Data.StationId == stationId)
                {
                    SwitchToStation(i);
                    return true;
                }

                if (section.SectionLabel == stationId)
                {
                    SwitchToStation(i);
                    return true;
                }
            }

            Debug.LogWarning(
                $"[StationGameplayController] Station not found: {stationId}");
            return false;
        }

        /// <summary>
        /// Switch to the next station in the list. Wraps around.
        /// </summary>
        public void SwitchToNextStation()
        {
            if (stations.Length == 0)
            {
                return;
            }

            int next = (activeIndex + 1) % stations.Length;
            SwitchToStation(next);
        }

        /// <summary>
        /// Switch to the previous station in the list. Wraps around.
        /// </summary>
        public void SwitchToPreviousStation()
        {
            if (stations.Length == 0)
            {
                return;
            }

            int prev = activeIndex <= 0 ? stations.Length - 1 : activeIndex - 1;
            SwitchToStation(prev);
        }

        /// <summary>
        /// Immediately snap the camera to the active station without
        /// smooth transition.
        /// </summary>
        public void SnapCameraToActiveStation()
        {
            if (ActiveStation == null || mainCamera == null)
            {
                return;
            }

            UpdateCameraTarget(ActiveStation);
            mainCamera.transform.position = cameraTargetPosition;
            mainCamera.orthographicSize = cameraTargetOrtho;
            isTransitioning = false;
        }

        private void UpdateCameraTarget(StationSection section)
        {
            if (section.CameraTarget != null)
            {
                cameraTargetPosition = new Vector3(
                    section.CameraTarget.position.x,
                    section.CameraTarget.position.y,
                    mainCamera != null ? mainCamera.transform.position.z : -10f);
            }

            cameraTargetOrtho = section.CameraOrthoSize > 0f
                ? section.CameraOrthoSize
                : defaultOrthoSize;
            isTransitioning = true;
        }

#if UNITY_EDITOR
        [ContextMenu("Debug: Switch to Next Station")]
        private void DebugSwitchNext()
        {
            SwitchToNextStation();
        }

        [ContextMenu("Debug: Snap Camera")]
        private void DebugSnapCamera()
        {
            SnapCameraToActiveStation();
        }
#endif
    }
}
