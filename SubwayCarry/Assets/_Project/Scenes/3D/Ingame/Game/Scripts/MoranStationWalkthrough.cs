using UnityEngine;
using UnityEngine.InputSystem;

namespace SubwayCarry.Gameplay3D
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class MoranStationWalkthrough : MonoBehaviour
    {
        public Camera view;
        public Metro3DFareDoor[] gates;
        public Font guideFont;
        CharacterController controller;
        MoranStationLift[] lifts;
        float pitch, verticalSpeed;
        bool showGuide = true;
        Vector3 entrance;
        GUIStyle guideStyle;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            lifts = FindObjectsByType<MoranStationLift>(FindObjectsSortMode.None);
            entrance = transform.position;
        }

        void Start() => CaptureMouse(true);

        void OnDisable() => CaptureMouse(false);

        static void CaptureMouse(bool capture)
        {
            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !capture;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            var mouse = Mouse.current;
            if (keyboard.escapeKey.wasPressedThisFrame) CaptureMouse(false);
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame) CaptureMouse(true);
            if (keyboard.f1Key.wasPressedThisFrame) showGuide = !showGuide;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 look = mouse.delta.ReadValue() * .1f;
                transform.Rotate(0, look.x, 0);
                pitch = Mathf.Clamp(pitch - look.y, -80, 80);
                view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            }

            OpenNearbyGate();
            if (!controller.enabled) return;
            float right = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                        - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
            float forward = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0)
                          - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            float speed = keyboard.leftShiftKey.isPressed ? 5.2f : 3.2f;
            Vector3 movement = Vector3.ClampMagnitude(transform.right * right + transform.forward * forward, 1);
            verticalSpeed = controller.isGrounded ? -2 : Mathf.Max(-18, verticalSpeed - 20 * Time.deltaTime);
            controller.Move((movement * speed + Vector3.up * verticalSpeed) * Time.deltaTime);
            if (transform.position.y < -4)
            {
                controller.enabled = false;
                transform.SetPositionAndRotation(entrance, Quaternion.identity);
                view.transform.localRotation = Quaternion.identity;
                pitch = verticalSpeed = 0;
                controller.enabled = true;
            }
        }

        void OpenNearbyGate()
        {
            foreach (var gate in gates)
            {
                Vector3 offset = gate.transform.InverseTransformPoint(transform.position);
                if (Mathf.Abs(offset.x) < .61f && Mathf.Abs(offset.z) < 1.6f && Mathf.Abs(offset.y) < 1.35f)
                    gate.HoldForPassenger();
            }
        }

        void OnGUI()
        {
            if (!showGuide) return;
            if (guideStyle == null)
            {
                guideStyle = new GUIStyle(GUI.skin.label) { font = guideFont, alignment = TextAnchor.MiddleCenter };
                guideStyle.normal.textColor = new Color(.94f, .96f, .94f);
            }
            string text = "WASD  이동     마우스  시점     Shift  빠르게     E  엘리베이터     F1  안내 숨기기";
            foreach (var lift in lifts)
            {
                string hint = lift.InteractionHint(controller);
                if (hint == null) continue;
                text = hint;
                break;
            }
            guideStyle.fontSize = Mathf.Clamp(Screen.width / 98, 12, 17);
            float width = Mathf.Min(Screen.width, guideStyle.CalcSize(new GUIContent(text)).x + 32);
            var rect = new Rect((Screen.width - width) * .5f, Screen.height - 49, width, 32);
            var previous = GUI.color;
            GUI.color = new Color(.035f, .09f, .10f, .87f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(rect, text, guideStyle);
        }
    }
}
