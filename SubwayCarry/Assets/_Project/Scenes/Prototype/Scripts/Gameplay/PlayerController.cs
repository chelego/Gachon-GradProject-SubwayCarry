using UnityEngine;
using UnityEngine.InputSystem;

namespace SubwayCarry.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(PlayerPosture))]

    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float moveSpeed = 3.5f;
        [SerializeField] private Camera facingCamera;
        [SerializeField] private Transform facingIndicator;
        [SerializeField, Min(0f)] private float facingIndicatorDistance = 0.56f;

        private Rigidbody2D body;
        private PlayerPosture posture;
        private Vector2 moveInput;

        public Vector2 FacingDirection { get; private set; } = Vector2.down;
        public Vector2 MoveInput => moveInput;

        public void ConfigureFacing(
            Camera camera,
            Transform indicator)
        {
            facingCamera = camera;
            facingIndicator = indicator;
            UpdateFacingIndicator();
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            posture = GetComponent<PlayerPosture>();

            body.gravityScale = 0f;
            body.freezeRotation = true;

            if (facingCamera == null)
            {
                facingCamera = Camera.main;
            }
        }

        private void Update()
        {
            ReadMovementInput();
            UpdateFacingDirection();
            ReadPostureTestInput();
        }

        private void FixedUpdate()
        {
            if (!posture.CanMove)
            {
                return;
            }

            body.MovePosition(body.position + moveInput * moveSpeed * posture.MoveSpeedMultiplier * Time.fixedDeltaTime);
        }

        private void ReadMovementInput()
        {
            moveInput = Vector2.zero;
            if (Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.wKey.isPressed)
            {
                moveInput.y += 1f;
            }

            if (Keyboard.current.sKey.isPressed)
            {
                moveInput.y -= 1f;
            }

            if (Keyboard.current.aKey.isPressed)
            {
                moveInput.x -= 1f;
            }

            if (Keyboard.current.dKey.isPressed)
            {
                moveInput.x += 1f;
            }

            moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        }

        private void UpdateFacingDirection()
        {
            if (facingCamera == null || Mouse.current == null)
            {
                return;
            }

            Vector2 screenPosition = Mouse.current.position.ReadValue();
            Vector3 worldPoint = facingCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -facingCamera.transform.position.z));
            Vector2 direction = (Vector2)worldPoint - body.position;

            if (direction.sqrMagnitude > 0.0001f)
            {
                FacingDirection = direction.normalized;
                UpdateFacingIndicator();
            }
        }

        private void UpdateFacingIndicator()
        {
            if (facingIndicator == null)
            {
                return;
            }

            Vector2 direction = FacingDirection.sqrMagnitude > 0.0001f
                ? FacingDirection.normalized
                : Vector2.down;
            facingIndicator.position = transform.position + new Vector3(
                direction.x * facingIndicatorDistance,
                direction.y * facingIndicatorDistance,
                -0.28f);
            facingIndicator.rotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)FacingDirection * 1.5f);
        }

        private void ReadPostureTestInput()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                posture.TryTransition(PostureState.Standing);
            }

            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                posture.TryTransition(PostureState.Leaning);
            }

            if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                posture.TryTransition(PostureState.Sitting);
            }

            if (Keyboard.current.digit4Key.wasPressedThisFrame)
            {
                posture.TryTransition(PostureState.Holding);
            }

            if (Keyboard.current.digit5Key.wasPressedThisFrame)
            {
                posture.TryTransition(PostureState.OverheadCarry);
            }
        }
    }
}
