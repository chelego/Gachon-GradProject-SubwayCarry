using SubwayCarry.Core.Contracts;
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

        private Rigidbody2D body;
        private PlayerPosture posture;
        private PlayerBalance balance;
        private Vector2 moveInput;
        private float agilityBonusPercent;

        public Vector2 FacingDirection { get; private set; } = Vector2.down;
        public Vector2 MoveInput => moveInput;
        public float BaseMoveSpeed => moveSpeed;
        public float AgilityBonusPercent => agilityBonusPercent;
        public float EffectiveMoveSpeed =>
            moveSpeed * (1f + agilityBonusPercent / 100f);

        public void SetAgilityBonusPercent(float bonusPercent)
        {
            agilityBonusPercent = Mathf.Max(0f, bonusPercent);
        }

        public void SetFacingDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            FacingDirection = direction.normalized;
        }

        // Integration scenes validate facilities themselves; existing prototypes retain their test keys.
        public bool PrototypePostureInputEnabled { get; set; } = true;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            posture = GetComponent<PlayerPosture>();
            balance = GetComponent<PlayerBalance>();

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
            if (!posture.CanMove ||
                (balance != null && balance.ConsumesMovementInput))
            {
                return;
            }

            body.MovePosition(
                body.position +
                moveInput * EffectiveMoveSpeed * posture.MoveSpeedMultiplier *
                Time.fixedDeltaTime);
        }

        private void ReadMovementInput()
        {
            moveInput = Vector2.zero;
            if (Keyboard.current == null ||
                (balance != null && balance.ConsumesMovementInput))
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
            if ((posture != null && !posture.CanChangeFacing) ||
                facingCamera == null ||
                Mouse.current == null)
            {
                return;
            }

            Vector2 screenPosition = Mouse.current.position.ReadValue();
            Vector3 worldPoint = facingCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -facingCamera.transform.position.z));
            Vector2 direction = (Vector2)worldPoint - body.position;
            SetFacingDirection(direction);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)FacingDirection * 1.5f);
        }

        private void ReadPostureTestInput()
        {
            if (!PrototypePostureInputEnabled) return;
            if (Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                posture.TryTransition(CarryPosture.Standing);
            }

            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                posture.TryTransition(CarryPosture.Leaning);
            }

            if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                posture.TryTransition(CarryPosture.Sitting);
            }

            if (Keyboard.current.digit4Key.wasPressedThisFrame)
            {
                posture.TryTransition(CarryPosture.HoldingSupport);
            }

            if (Keyboard.current.digit5Key.wasPressedThisFrame)
            {
                posture.TryTransition(CarryPosture.OverheadCarry);
            }
        }
    }
}
