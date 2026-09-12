using UnityEngine;
using UnityEngine.InputSystem;

namespace SubwayCarry.Prototype.QuarterView
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class QuarterViewTestPlayer : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float moveSpeed = 4.2f;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Transform facingIndicator;
        [SerializeField, Min(0.1f)] private float indicatorDistance = 0.68f;

        private Rigidbody2D body;
        private Vector2 moveInput;
        private bool movementEnabled = true;
        private MeshRenderer[] visualRenderers;
        private int[] visualOrderOffsets;

        public Rigidbody2D Body => body;
        public bool MovementEnabled => movementEnabled;
        public Vector2 FacingDirection { get; private set; } = Vector2.up;

        public void Configure(Camera camera, Transform indicator)
        {
            worldCamera = camera;
            facingIndicator = indicator;
            UpdateFacingIndicator();
        }

        public void SetMovementEnabled(bool enabled)
        {
            movementEnabled = enabled;
            if (!enabled)
            {
                moveInput = Vector2.zero;
                if (body != null)
                {
                    body.linearVelocity = Vector2.zero;
                }
            }
        }

        public void Teleport(Vector2 destination)
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            body.position = destination;
            body.linearVelocity = Vector2.zero;
            transform.position = new Vector3(destination.x, destination.y, transform.position.z);
            Physics2D.SyncTransforms();
            UpdateVisualSorting();
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            CacheVisualSorting();
        }

        private void Update()
        {
            ReadMovement();
            ReadFacing();
        }

        private void LateUpdate()
        {
            UpdateVisualSorting();
        }

        private void FixedUpdate()
        {
            if (!movementEnabled)
            {
                return;
            }

            body.MovePosition(body.position + moveInput * moveSpeed * Time.fixedDeltaTime);
        }

        private void ReadMovement()
        {
            moveInput = Vector2.zero;
            if (!movementEnabled || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.wKey.isPressed) moveInput.y += 1f;
            if (Keyboard.current.sKey.isPressed) moveInput.y -= 1f;
            if (Keyboard.current.aKey.isPressed) moveInput.x -= 1f;
            if (Keyboard.current.dKey.isPressed) moveInput.x += 1f;
            moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        }

        private void ReadFacing()
        {
            if (worldCamera == null || Mouse.current == null || body == null)
            {
                return;
            }

            Vector2 screenPosition = Mouse.current.position.ReadValue();
            Vector3 worldPosition = worldCamera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, -worldCamera.transform.position.z));
            Vector2 direction = (Vector2)worldPosition - body.position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            FacingDirection = direction.normalized;
            UpdateFacingIndicator();
        }

        private void UpdateFacingIndicator()
        {
            if (facingIndicator == null)
            {
                return;
            }

            Vector2 direction = FacingDirection.sqrMagnitude > 0.0001f
                ? FacingDirection.normalized
                : Vector2.up;
            facingIndicator.localPosition = new Vector3(
                direction.x * indicatorDistance,
                direction.y * indicatorDistance + 0.08f,
                -0.3f);
            facingIndicator.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        private void CacheVisualSorting()
        {
            visualRenderers = GetComponentsInChildren<MeshRenderer>(true);
            visualOrderOffsets = new int[visualRenderers.Length];
            for (int i = 0; i < visualRenderers.Length; i++)
            {
                visualOrderOffsets[i] = visualRenderers[i].sortingOrder;
            }
            UpdateVisualSorting();
        }

        private void UpdateVisualSorting()
        {
            if (visualRenderers == null || visualOrderOffsets == null)
            {
                return;
            }

            int baseOrder = 2400 - Mathf.RoundToInt(transform.position.y * 100f);
            for (int i = 0; i < visualRenderers.Length; i++)
            {
                if (visualRenderers[i] != null)
                {
                    visualRenderers[i].sortingOrder = baseOrder + visualOrderOffsets[i];
                }
            }
        }
    }
}
