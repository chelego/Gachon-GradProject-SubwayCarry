using UnityEngine;
using UnityEngine.InputSystem;

namespace SubwayCarry.AI.V2
{
    public sealed class PassengerAiV23DTestCamera : MonoBehaviour
    {
        [SerializeField] private Vector3 pivot = new Vector3(0f, 0.7f, 0f);
        [SerializeField] private float yaw = -25f, pitch = 43f, distance = 30f;
        [SerializeField, Min(.05f)] private float orbitSensitivity = .48f;
        [SerializeField, Min(.1f)] private float panSensitivity = 1.65f;
        [SerializeField, Min(.01f)] private float zoomSensitivity = .18f;
        public void Frame(Vector3 centre, float viewDistance)
        { pivot = centre; distance = viewDistance; }
        private void LateUpdate()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (mouse != null)
            {
                if (mouse.rightButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    yaw += delta.x * orbitSensitivity;
                    pitch = Mathf.Clamp(pitch - delta.y * orbitSensitivity, 8f, 88f);
                }
                if (mouse.middleButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    float metresPerPixel = distance * 1.02f / Mathf.Max(300, Screen.height);
                    Quaternion panAxes = Quaternion.Euler(0f, yaw, 0f);
                    pivot -= panAxes * new Vector3(delta.x, 0f, delta.y) * metresPerPixel * panSensitivity;
                }
                // Input System reports 120 units per normal Windows wheel notch.
                float scroll = mouse.scroll.ReadValue().y;
                distance = Mathf.Clamp(distance * Mathf.Exp(-scroll * zoomSensitivity / 120f), 2.5f, 85f);
            }
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            if (keyboard != null)
            {
                Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                float x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
                float z = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
                float fast = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed ? 3f : 1f;
                pivot += (right * x + forward * z) * Time.deltaTime * 12f * fast;
                pivot.y += ((keyboard.eKey.isPressed ? 1f : 0f) - (keyboard.qKey.isPressed ? 1f : 0f)) * Time.deltaTime * 8f * fast;
            }
            transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * distance, rotation);
        }
    }
}
