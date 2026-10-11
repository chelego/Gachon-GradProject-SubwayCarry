using UnityEngine;
using UnityEngine.InputSystem;

namespace SubwayCarry.Transit
{
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterController))]
    public sealed class SubwayFirstPersonController : MonoBehaviour
    {
        public Transform eye;
        public float walkSpeed = 2.25f;
        public float sprintSpeed = 3.65f;
        public float mouseSensitivity = .085f;
        public float interactionRange = 2.4f;
        private CharacterController body;
        private Vector3 spawn;
        private Quaternion spawnRotation;
        private float pitch, fallSpeed;

        private void Awake()
        {
            body = GetComponent<CharacterController>();
            spawn = transform.position;
            spawnRotation = transform.rotation;
        }

        private void OnDisable() => ReleaseCursor();
        private void OnApplicationFocus(bool focused) { if (!focused) ReleaseCursor(); }
        private void ReleaseCursor() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        public void ResetToSpawn()
        {
            body.enabled = false;
            transform.SetPositionAndRotation(spawn, spawnRotation);
            body.enabled = true;
            pitch = fallSpeed = 0f;
            if (eye) eye.localRotation = Quaternion.identity;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || eye == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) ReleaseCursor();
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (keyboard.homeKey.wasPressedThisFrame) ResetToSpawn();
            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;
                transform.Rotate(0, delta.x, 0);
                pitch = Mathf.Clamp(pitch - delta.y, -82, 82);
                eye.localRotation = Quaternion.Euler(pitch, 0, 0);
            }
            bool crouch = keyboard.leftCtrlKey.isPressed;
            float targetHeight = crouch ? 1.15f : 1.8f;
            if (crouch || !Physics.SphereCast(transform.position + Vector3.up * .9f,
                .22f, Vector3.up, out _, .75f, ~0, QueryTriggerInteraction.Ignore))
            { body.height = targetHeight; body.center = Vector3.up * targetHeight * .5f; }
            eye.localPosition = Vector3.Lerp(eye.localPosition,
                Vector3.up * (body.height - .17f), 14f * Time.deltaTime);
            Vector2 input = new Vector2(
                (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            input = Vector2.ClampMagnitude(input, 1);
            float speed = crouch ? 1.15f : keyboard.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;
            if (body.isGrounded && fallSpeed < 0) fallSpeed = -2f;
            fallSpeed = Mathf.Max(fallSpeed - 20f * Time.deltaTime, -25f);
            body.Move(((transform.right * input.x + transform.forward * input.y) * speed
                + Vector3.up * fallSpeed) * Time.deltaTime);
            if (keyboard.eKey.wasPressedThisFrame && Physics.Raycast(eye.position, eye.forward,
                out RaycastHit hit, interactionRange, ~0, QueryTriggerInteraction.Ignore))
            {
                var door = hit.collider.GetComponentInParent<SubwayDoor3D>();
                if (door) door.SetOpen(!door.RequestedOpen);
            }
            if (transform.position.y < -3f) ResetToSpawn();
        }
    }
}

