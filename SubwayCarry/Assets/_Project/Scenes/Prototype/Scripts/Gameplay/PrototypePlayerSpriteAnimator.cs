using System;
using UnityEngine;

namespace SubwayCarry.Prototype.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController), typeof(PlayerPosture))]
    public sealed class PrototypePlayerSpriteAnimator : MonoBehaviour
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

        private float walkElapsed;
        private bool wasMoving;
        private int previousDirectionIndex = -1;

        public bool IsConfigured =>
            controller != null &&
            posture != null &&
            spriteRenderer != null &&
            spriteRenderer.sprite != null &&
            HasAssignedSprites(idleSprites, DirectionCount) &&
            HasAssignedSprites(walkSprites, DirectionCount * walkFramesPerDirection);

        public void Configure(
            PlayerController playerController,
            PlayerPosture playerPosture,
            SpriteRenderer playerSpriteRenderer,
            Sprite[] playerIdleSprites,
            Sprite[] playerWalkSprites,
            int walkFrameCount,
            float framesPerSecond)
        {
            if (playerController == null || playerPosture == null || playerSpriteRenderer == null)
            {
                throw new ArgumentNullException(nameof(playerController));
            }
            if (playerIdleSprites == null || playerIdleSprites.Length != DirectionCount)
            {
                throw new ArgumentException(
                    $"Expected {DirectionCount} idle sprites.",
                    nameof(playerIdleSprites));
            }
            if (walkFrameCount <= 0 ||
                playerWalkSprites == null ||
                playerWalkSprites.Length != DirectionCount * walkFrameCount)
            {
                throw new ArgumentException(
                    "Walk sprites must contain the same number of frames for all eight directions.",
                    nameof(playerWalkSprites));
            }

            controller = playerController;
            posture = playerPosture;
            spriteRenderer = playerSpriteRenderer;
            idleSprites = (Sprite[])playerIdleSprites.Clone();
            walkSprites = (Sprite[])playerWalkSprites.Clone();
            walkFramesPerDirection = walkFrameCount;
            walkFramesPerSecond = Mathf.Max(0.1f, framesPerSecond);
            RefreshSprite();
        }

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

            HidePrototypeHelperRenderers();

            RefreshSprite();
        }

        private void HidePrototypeHelperRenderers()
        {
            foreach (string childName in new[]
                     {
                         "Cake Package",
                         "Player Facing Indicator"
                     })
            {
                Transform child = transform.Find(childName);
                MeshRenderer helperRenderer =
                    child != null ? child.GetComponent<MeshRenderer>() : null;
                if (helperRenderer != null)
                {
                    helperRenderer.enabled = false;
                }
            }
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

            bool isMoving =
                (posture == null || posture.CanMove) &&
                controller.MoveInput.sqrMagnitude > 0.0001f;
            Vector2 direction = isMoving
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

                int frameIndex =
                    Mathf.FloorToInt(walkElapsed * walkFramesPerSecond) %
                    walkFramesPerDirection;
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

        private static bool HasAssignedSprites(Sprite[] sprites, int expectedCount)
        {
            if (sprites == null || sprites.Length != expectedCount)
            {
                return false;
            }

            for (int index = 0; index < sprites.Length; index++)
            {
                if (sprites[index] == null)
                {
                    return false;
                }
            }

            return true;
        }

        internal static int GetDirectionIndex(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return 0;
            }

            float clockwiseAngleFromSouth =
                Mathf.Atan2(-direction.x, -direction.y) * Mathf.Rad2Deg;
            if (clockwiseAngleFromSouth < 0f)
            {
                clockwiseAngleFromSouth += 360f;
            }

            return Mathf.RoundToInt(clockwiseAngleFromSouth / 45f) % DirectionCount;
        }
    }
}
