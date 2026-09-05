using UnityEngine;

namespace SubwayCarry.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController), typeof(PlayerPosture))]
    public sealed class PlayerSpriteAnimator : MonoBehaviour
    {
        private const int DirectionCount = 8;

        [Header("References")]
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerPosture posture;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Sprites")]
        [Tooltip("South, South-West, West, North-West, North, North-East, East, South-East")]
        [SerializeField] private Sprite[] idleSprites = new Sprite[DirectionCount];
        [Tooltip("Direction-major order. Each direction contains walkFramesPerDirection consecutive frames.")]
        [SerializeField] private Sprite[] walkSprites = new Sprite[DirectionCount * 3];

        [Header("Playback")]
        [SerializeField, Min(1)] private int walkFramesPerDirection = 3;
        [SerializeField, Min(0.1f)] private float walkFramesPerSecond = 8f;
        [SerializeField] private bool useMovementDirectionWhileMoving = true;

        private float walkElapsed;
        private bool wasMoving;
        private int previousDirectionIndex = -1;

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponent<PlayerController>();
            }

            if (posture == null)
            {
                posture = GetComponent<PlayerPosture>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            RefreshSprite();
        }

        private void Update()
        {
            RefreshSprite();
        }

        private void RefreshSprite()
        {
            if (controller == null || spriteRenderer == null)
            {
                return;
            }

            bool isMoving = posture != null && posture.CanMove && controller.MoveInput.sqrMagnitude > 0.0001f;
            Vector2 direction = isMoving && useMovementDirectionWhileMoving
                ? controller.MoveInput
                : controller.FacingDirection;
            int directionIndex = GetDirectionIndex(direction);

            if (!isMoving)
            {
                walkElapsed = 0f;
                SetSprite(GetIdleSprite(directionIndex));
            }
            else
            {
                if (!wasMoving || directionIndex != previousDirectionIndex)
                {
                    walkElapsed = 0f;
                }
                else
                {
                    walkElapsed += Time.deltaTime;
                }

                int frameIndex = Mathf.FloorToInt(walkElapsed * walkFramesPerSecond) % walkFramesPerDirection;
                SetSprite(GetWalkSprite(directionIndex, frameIndex));
            }

            wasMoving = isMoving;
            previousDirectionIndex = directionIndex;
        }

        private Sprite GetIdleSprite(int directionIndex)
        {
            return idleSprites != null && directionIndex < idleSprites.Length
                ? idleSprites[directionIndex]
                : null;
        }

        private Sprite GetWalkSprite(int directionIndex, int frameIndex)
        {
            if (walkSprites == null || walkFramesPerDirection <= 0)
            {
                return null;
            }

            int spriteIndex = directionIndex * walkFramesPerDirection + frameIndex;
            return spriteIndex < walkSprites.Length ? walkSprites[spriteIndex] : null;
        }

        private void SetSprite(Sprite sprite)
        {
            if (sprite != null && spriteRenderer.sprite != sprite)
            {
                spriteRenderer.sprite = sprite;
            }
        }

        internal static int GetDirectionIndex(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return 0;
            }

            float clockwiseAngleFromSouth = Mathf.Atan2(-direction.x, -direction.y) * Mathf.Rad2Deg;
            if (clockwiseAngleFromSouth < 0f)
            {
                clockwiseAngleFromSouth += 360f;
            }

            return Mathf.RoundToInt(clockwiseAngleFromSouth / 45f) % DirectionCount;
        }
    }
}
