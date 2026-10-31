using UnityEngine;
using UnityEngine.InputSystem;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    [DisallowMultipleComponent]
    public sealed class MotionPreviewOrbitCamera : MonoBehaviour
    {
        public Vector3 focus = new Vector3(.1f, .88f, 0);
        public Renderer wall;
        [Range(.05f, .5f)] public float orbitSensitivity = .18f;
        [Min(.3f)] public float minimumDistance = .9f;
        [Min(1f)] public float maximumDistance = 8f;
        public float Yaw => yaw;
        public float Pitch => pitch;
        public float Distance => distance;
        float yaw, pitch, distance;
        float initialYaw, initialPitch, initialDistance;
        bool initialized;
        void Start() { Initialize(); }
        void Initialize()
        {
            if (initialized) return;
            var angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = Mathf.DeltaAngle(0, angles.x);
            distance = Vector3.Distance(transform.position, focus);
            initialYaw = yaw; initialPitch = pitch; initialDistance = distance;
            initialized = true;
        }
        public void Orbit(Vector2 pixels)
        {
            Initialize();
            yaw += pixels.x * orbitSensitivity;
            pitch = Mathf.Clamp(pitch - pixels.y * orbitSensitivity, -65, 75);
            ApplyView();
        }
        public void Zoom(float wheelSteps)
        {
            Initialize();
            distance = Mathf.Clamp(distance * Mathf.Exp(-wheelSteps * .13f), minimumDistance, maximumDistance);
            ApplyView();
        }
        public void ResetView()
        {
            Initialize();
            yaw = initialYaw; pitch = initialPitch; distance = initialDistance;
            if (wall != null) wall.enabled = true;
            ApplyView();
        }
        void ApplyView()
        {
            transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            transform.position = focus - transform.forward * distance;
        }
        void LateUpdate()
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.rightButton.isPressed) Orbit(mouse.delta.ReadValue());
                Vector2 pointer = mouse.position.ReadValue();
                bool overControls = pointer.x < 400 && pointer.y > Screen.height - 240;
                float wheel = mouse.scroll.ReadValue().y;
                if (!overControls && Mathf.Abs(wheel) > .01f) Zoom(wheel / 120f);
            }
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.fKey.wasPressedThisFrame) ResetView();
            if (keyboard.vKey.wasPressedThisFrame && wall != null) wall.enabled = !wall.enabled;
        }
        void OnGUI()
        {
            GUI.Box(new Rect(18, Screen.height - 48, 590, 30),
                "Camera: right-drag orbit | wheel zoom | F reset | V show/hide wall");
        }
    }
}