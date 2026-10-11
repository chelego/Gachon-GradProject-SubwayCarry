using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SubwayCarry.Transit
{
    /// <summary>Camera and cutaway inspection controls for the quarter-view car.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class SubwayQuarterPreview : MonoBehaviour
    {
        [SerializeField] private SubwayQuarterDoor[] doors = Array.Empty<SubwayQuarterDoor>();
        [SerializeField] private GameObject[] foregroundFullHeight = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] nearCutaway = Array.Empty<GameObject>();
        [SerializeField, Min(0f)] private float travelLimit = 10f;
        [SerializeField, Min(0.01f)] private float movementSpeed = 6f;
        [SerializeField, Min(0.01f)] private float zoomStep = 0.7f;
        [SerializeField] private bool cutaway = true;

        private Camera previewCamera;
        private float trainPosition;
        private float closeSize = 6.5f;
        private bool overview;
        private bool doorsOpen;

        public bool CutawayEnabled => cutaway;
        public bool IsOverview => overview;
        public bool DoorsRequestedOpen => doorsOpen;
        public float TrainPosition => trainPosition;

        public void Configure(SubwayQuarterDoor[] controlledDoors,
            GameObject[] fullHeightForeground = null, GameObject[] cutawayForeground = null)
        {
            doors = controlledDoors ?? Array.Empty<SubwayQuarterDoor>();
            foregroundFullHeight = fullHeightForeground ?? Array.Empty<GameObject>();
            nearCutaway = cutawayForeground ?? Array.Empty<GameObject>();
            previewCamera = GetComponent<Camera>();
            trainPosition = 0f;
            closeSize = 6.5f;
            overview = false;
            SetDoorsOpen(false);
            SetCutaway(true);
            ApplyCamera();
        }

        private void Awake()
        {
            previewCamera = GetComponent<Camera>();
            trainPosition = 0f;
            closeSize = 6.5f;
            overview = false;
            SetDoorsOpen(false);
            SetCutaway(true);
            ApplyCamera();
        }

        public void SetDoorsOpen(bool open)
        {
            doorsOpen = open;
            if (doors == null) return;
            foreach (SubwayQuarterDoor door in doors)
                if (door != null) door.SetOpen(open);
        }

        public void SetCutaway(bool enabled)
        {
            cutaway = enabled;
            SetObjectsActive(foregroundFullHeight, !enabled);
            SetObjectsActive(nearCutaway, enabled);
        }

        public void SetOverview(bool enabled)
        {
            overview = enabled;
            ApplyCamera();
        }

        private static void SetObjectsActive(GameObject[] objects, bool active)
        {
            if (objects == null) return;
            foreach (GameObject item in objects)
                if (item != null) item.SetActive(active);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame) SetDoorsOpen(!doorsOpen);
                if (keyboard.cKey.wasPressedThisFrame) SetCutaway(!cutaway);
                if (keyboard.fKey.wasPressedThisFrame) overview = !overview;
                if (keyboard.homeKey.wasPressedThisFrame)
                {
                    trainPosition = 0f;
                    closeSize = 6.5f;
                    overview = false;
                }

                float direction = 0f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) direction -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) direction += 1f;
                if (!Mathf.Approximately(direction, 0f))
                {
                    overview = false;
                    trainPosition = Mathf.Clamp(trainPosition + direction * movementSpeed *
                        Time.unscaledDeltaTime, -travelLimit, travelLimit);
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (!Mathf.Approximately(wheel, 0f))
                {
                    if (overview && previewCamera != null)
                        closeSize = previewCamera.orthographicSize;
                    overview = false;
                    closeSize = Mathf.Clamp(closeSize - Mathf.Sign(wheel) * zoomStep, 4.7f, 15f);
                }
            }

            ApplyCamera();
        }

        private void ApplyCamera()
        {
            if (previewCamera == null) previewCamera = GetComponent<Camera>();
            if (previewCamera == null) return;
            previewCamera.orthographic = true;

            if (overview)
            {
                previewCamera.orthographicSize = Mathf.Max(14.2f,
                    24f / Mathf.Max(0.1f, previewCamera.aspect));
                transform.position = new Vector3(0f, 1f, -10f);
            }
            else
            {
                previewCamera.orthographicSize = Mathf.Clamp(closeSize, 4.7f, 15f);
                // Follow along the projected train axis P(u, 0, 0).
                transform.position = new Vector3(trainPosition, trainPosition * 0.5f + 0.8f, -10f);
            }
        }
    }
}
