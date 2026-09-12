using System;
using UnityEngine;

namespace SubwayCarry.Prototype.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController), typeof(PlayerPosture))]
    public sealed class PrototypePlayerSpriteAnimator : MonoBehaviour
    {
        private const int DirectionCount = 8;
        private const int FallenDirectionCount = 4;

        [Header("References")]
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerPosture posture;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private SpriteRenderer packageSpriteRenderer;

        [Header("Sprites - Unarmed")]
        [Tooltip("South, South-West, West, North-West, North, North-East, East, South-East")]
        [SerializeField] private Sprite[] idleSprites = new Sprite[DirectionCount];
        [Tooltip("Direction-major order. Each direction contains walkFramesPerDirection consecutive frames.")]
        [SerializeField] private Sprite[] walkSprites = new Sprite[DirectionCount * 3];

        [Header("Sprites - Carrying Pose")]
        [SerializeField] private Sprite[] carryingIdleSprites = new Sprite[DirectionCount];
        [SerializeField] private Sprite[] carryingWalkSprites = new Sprite[DirectionCount * 3];

        [Header("Sprites - Cake Package Overlay")]
        [SerializeField] private Sprite[] packageIdleSprites = new Sprite[DirectionCount];
        [SerializeField] private Sprite[] packageWalkSprites = new Sprite[DirectionCount * 3];

        [Header("Sprites - Sitting (Front, Back)")]
        [Tooltip("South/front and North/back. Side and diagonal facing use the nearest of these two views.")]
        [SerializeField] private Sprite[] sittingBodySprites = new Sprite[2];
        [SerializeField] private Sprite[] sittingPackageSprites = new Sprite[2];

        [Header("Sprites - Fallen (South, West, North, East)")]
        [Tooltip("Direction-major order. Each cardinal direction contains fallenFramesPerDirection consecutive frames.")]
        [SerializeField] private Sprite[] fallenBodySprites = new Sprite[FallenDirectionCount * 5];
        [SerializeField] private Sprite[] fallenPackageSprites = new Sprite[FallenDirectionCount * 5];

        [Header("Playback")]
        [SerializeField, Min(1)] private int walkFramesPerDirection = 3;
        [SerializeField, Min(0.1f)] private float walkFramesPerSecond = 8f;
        [SerializeField, Min(1)] private int fallenFramesPerDirection = 5;
        [SerializeField, Min(0.1f)] private float fallenFramesPerSecond = 9f;

        private float walkElapsed;
        private float fallenElapsed;
        private bool wasMoving;
        private bool wasFallen;
        private bool isCarryingPackage;
        private bool wasCarryingPackage;
        private int previousDirectionIndex = -1;
        private int lockedFallenDirectionIndex = -1;
        private SubwayCarry.Gameplay.PackageDamageVisual packageDamageVisual;

        public bool IsCarryingPackage => isCarryingPackage;

        public bool IsConfigured =>
            controller != null &&
            posture != null &&
            spriteRenderer != null &&
            spriteRenderer.sprite != null &&
            HasAssignedSprites(idleSprites, DirectionCount) &&
            HasAssignedSprites(walkSprites, DirectionCount * walkFramesPerDirection) &&
            HasAssignedSprites(carryingIdleSprites, DirectionCount) &&
            HasAssignedSprites(carryingWalkSprites, DirectionCount * walkFramesPerDirection) &&
            HasAssignedSprites(packageIdleSprites, DirectionCount) &&
            HasAssignedSprites(packageWalkSprites, DirectionCount * walkFramesPerDirection) &&
            HasAssignedSprites(sittingBodySprites, 2) &&
            HasAssignedSprites(sittingPackageSprites, 2) &&
            HasAssignedSprites(
                fallenBodySprites,
                FallenDirectionCount * fallenFramesPerDirection) &&
            HasAssignedSprites(
                fallenPackageSprites,
                FallenDirectionCount * fallenFramesPerDirection);

        public void Configure(
            PlayerController playerController,
            PlayerPosture playerPosture,
            SpriteRenderer playerSpriteRenderer,
            Sprite[] playerIdleSprites,
            Sprite[] playerWalkSprites,
            Sprite[] playerCarryingIdleSprites,
            Sprite[] playerCarryingWalkSprites,
            Sprite[] playerPackageIdleSprites,
            Sprite[] playerPackageWalkSprites,
            Sprite[] playerSittingBodySprites,
            Sprite[] playerSittingPackageSprites,
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
            if (playerCarryingIdleSprites == null ||
                playerCarryingIdleSprites.Length != DirectionCount)
            {
                throw new ArgumentException(
                    $"Expected {DirectionCount} carrying idle sprites.",
                    nameof(playerCarryingIdleSprites));
            }
            if (playerCarryingWalkSprites == null ||
                playerCarryingWalkSprites.Length != DirectionCount * walkFrameCount)
            {
                throw new ArgumentException(
                    "Carrying walk sprites must contain the same number of frames for all eight directions.",
                    nameof(playerCarryingWalkSprites));
            }
            if (playerPackageIdleSprites == null ||
                playerPackageIdleSprites.Length != DirectionCount)
            {
                throw new ArgumentException(
                    $"Expected {DirectionCount} package idle sprites.",
                    nameof(playerPackageIdleSprites));
            }
            if (playerPackageWalkSprites == null ||
                playerPackageWalkSprites.Length != DirectionCount * walkFrameCount)
            {
                throw new ArgumentException(
                    "Package walk sprites must contain the same number of frames for all eight directions.",
                    nameof(playerPackageWalkSprites));
            }
            if (playerSittingBodySprites == null || playerSittingBodySprites.Length != 2)
            {
                throw new ArgumentException(
                    "Sitting body sprites must contain front and back views.",
                    nameof(playerSittingBodySprites));
            }
            if (playerSittingPackageSprites == null || playerSittingPackageSprites.Length != 2)
            {
                throw new ArgumentException(
                    "Sitting package sprites must contain front and back views.",
                    nameof(playerSittingPackageSprites));
            }

            controller = playerController;
            posture = playerPosture;
            spriteRenderer = playerSpriteRenderer;
            idleSprites = (Sprite[])playerIdleSprites.Clone();
            walkSprites = (Sprite[])playerWalkSprites.Clone();
            carryingIdleSprites = (Sprite[])playerCarryingIdleSprites.Clone();
            carryingWalkSprites = (Sprite[])playerCarryingWalkSprites.Clone();
            packageIdleSprites = (Sprite[])playerPackageIdleSprites.Clone();
            packageWalkSprites = (Sprite[])playerPackageWalkSprites.Clone();
            sittingBodySprites = (Sprite[])playerSittingBodySprites.Clone();
            sittingPackageSprites = (Sprite[])playerSittingPackageSprites.Clone();
            walkFramesPerDirection = walkFrameCount;
            walkFramesPerSecond = Mathf.Max(0.1f, framesPerSecond);
            ResolvePackageSpriteRenderer();
            RefreshSprite();
        }

        public void ConfigureFallenSprites(
            Sprite[] playerFallenBodySprites,
            Sprite[] playerFallenPackageSprites,
            int fallenFrameCount,
            float framesPerSecond)
        {
            if (fallenFrameCount <= 0 ||
                playerFallenBodySprites == null ||
                playerFallenBodySprites.Length != FallenDirectionCount * fallenFrameCount)
            {
                throw new ArgumentException(
                    "Fallen body sprites must contain the same number of frames for all four cardinal directions.",
                    nameof(playerFallenBodySprites));
            }
            if (playerFallenPackageSprites == null ||
                playerFallenPackageSprites.Length != FallenDirectionCount * fallenFrameCount)
            {
                throw new ArgumentException(
                    "Fallen package sprites must contain the same number of frames for all four cardinal directions.",
                    nameof(playerFallenPackageSprites));
            }

            fallenBodySprites = (Sprite[])playerFallenBodySprites.Clone();
            fallenPackageSprites = (Sprite[])playerFallenPackageSprites.Clone();
            fallenFramesPerDirection = fallenFrameCount;
            fallenFramesPerSecond = Mathf.Max(0.1f, framesPerSecond);
            fallenElapsed = 0f;
            wasFallen = false;
            RefreshSprite();
        }

        public void SetCarryingPackage(bool carrying)
        {
            if (isCarryingPackage == carrying)
            {
                return;
            }

            isCarryingPackage = carrying;
            walkElapsed = 0f;
            RefreshSprite();
        }

        public void SetPackageDurabilityProvider(MonoBehaviour providerSource)
        {
            ResolvePackageSpriteRenderer();
            EnsurePackageDamageVisual();
            packageDamageVisual?.Configure(
                packageSpriteRenderer,
                providerSource);
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
                Transform bodyVisual = transform.Find("BodyVisual");
                spriteRenderer = bodyVisual != null
                    ? bodyVisual.GetComponent<SpriteRenderer>()
                    : GetComponentInChildren<SpriteRenderer>();
            }

            HidePrototypeHelperRenderers();

            ResolvePackageSpriteRenderer();
            EnsurePackageDamageVisual();
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

            if (posture != null && posture.CurrentState == PostureState.Fallen)
            {
                walkElapsed = 0f;
                if (!wasFallen)
                {
                    fallenElapsed = 0f;
                    lockedFallenDirectionIndex = directionIndex;
                }
                else
                {
                    fallenElapsed += Time.deltaTime;
                }

                int fallenDirectionIndex =
                    GetFallenDirectionIndex(lockedFallenDirectionIndex);
                int fallenFrameIndex = Mathf.Clamp(
                    Mathf.FloorToInt(fallenElapsed * fallenFramesPerSecond),
                    0,
                    Mathf.Max(0, fallenFramesPerDirection - 1));
                SetSprites(
                    GetFallenBodySprite(
                        fallenDirectionIndex,
                        fallenFrameIndex,
                        lockedFallenDirectionIndex),
                    isCarryingPackage
                        ? GetFallenPackageSprite(fallenDirectionIndex, fallenFrameIndex)
                        : null,
                    lockedFallenDirectionIndex);
                wasFallen = true;
                wasMoving = false;
                wasCarryingPackage = isCarryingPackage;
                previousDirectionIndex = lockedFallenDirectionIndex;
                return;
            }

            wasFallen = false;
            fallenElapsed = 0f;
            lockedFallenDirectionIndex = -1;

            if (posture != null && posture.CurrentState == PostureState.Sitting)
            {
                walkElapsed = 0f;
                int sittingDirectionIndex = GetSittingDirectionIndex(directionIndex);
                SetSprites(
                    GetSittingBodySprite(sittingDirectionIndex, directionIndex),
                    isCarryingPackage
                        ? GetSittingPackageSprite(sittingDirectionIndex)
                        : null,
                    directionIndex);
                wasMoving = false;
                wasCarryingPackage = isCarryingPackage;
                previousDirectionIndex = directionIndex;
                return;
            }

            if (!isMoving)
            {
                walkElapsed = 0f;
                SetSprites(
                    GetIdleSprite(directionIndex),
                    isCarryingPackage ? GetPackageIdleSprite(directionIndex) : null,
                    directionIndex);
            }
            else
            {
                if (!wasMoving ||
                    directionIndex != previousDirectionIndex ||
                    isCarryingPackage != wasCarryingPackage)
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
                SetSprites(
                    GetWalkSprite(directionIndex, frameIndex),
                    isCarryingPackage
                        ? GetPackageWalkSprite(directionIndex, frameIndex)
                        : null,
                    directionIndex);
            }

            wasMoving = isMoving;
            wasCarryingPackage = isCarryingPackage;
            previousDirectionIndex = directionIndex;
        }

        private Sprite GetIdleSprite(int directionIndex)
        {
            if (isCarryingPackage &&
                carryingIdleSprites != null &&
                directionIndex < carryingIdleSprites.Length &&
                carryingIdleSprites[directionIndex] != null)
            {
                return carryingIdleSprites[directionIndex];
            }

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
            if (isCarryingPackage &&
                carryingWalkSprites != null &&
                spriteIndex < carryingWalkSprites.Length &&
                carryingWalkSprites[spriteIndex] != null)
            {
                return carryingWalkSprites[spriteIndex];
            }

            return spriteIndex < walkSprites.Length ? walkSprites[spriteIndex] : null;
        }

        private void SetSprite(Sprite sprite)
        {
            if (sprite != null && spriteRenderer.sprite != sprite)
            {
                spriteRenderer.sprite = sprite;
            }
        }

        private Sprite GetPackageIdleSprite(int directionIndex)
        {
            return packageIdleSprites != null && directionIndex < packageIdleSprites.Length
                ? packageIdleSprites[directionIndex]
                : null;
        }

        private Sprite GetPackageWalkSprite(int directionIndex, int frameIndex)
        {
            if (packageWalkSprites == null || walkFramesPerDirection <= 0)
            {
                return null;
            }

            int spriteIndex = directionIndex * walkFramesPerDirection + frameIndex;
            return spriteIndex < packageWalkSprites.Length
                ? packageWalkSprites[spriteIndex]
                : null;
        }

        private Sprite GetSittingBodySprite(
            int sittingDirectionIndex,
            int directionIndex)
        {
            if (sittingBodySprites != null &&
                sittingDirectionIndex < sittingBodySprites.Length &&
                sittingBodySprites[sittingDirectionIndex] != null)
            {
                return sittingBodySprites[sittingDirectionIndex];
            }

            return GetIdleSprite(directionIndex);
        }

        private Sprite GetSittingPackageSprite(int sittingDirectionIndex)
        {
            return sittingPackageSprites != null &&
                sittingDirectionIndex < sittingPackageSprites.Length
                ? sittingPackageSprites[sittingDirectionIndex]
                : null;
        }

        private Sprite GetFallenBodySprite(
            int fallenDirectionIndex,
            int frameIndex,
            int directionIndex)
        {
            int spriteIndex = fallenDirectionIndex * fallenFramesPerDirection + frameIndex;
            if (fallenFramesPerDirection > 0 &&
                fallenBodySprites != null &&
                spriteIndex >= 0 &&
                spriteIndex < fallenBodySprites.Length &&
                fallenBodySprites[spriteIndex] != null)
            {
                return fallenBodySprites[spriteIndex];
            }

            return GetIdleSprite(directionIndex);
        }

        private Sprite GetFallenPackageSprite(int fallenDirectionIndex, int frameIndex)
        {
            int spriteIndex = fallenDirectionIndex * fallenFramesPerDirection + frameIndex;
            return fallenFramesPerDirection > 0 &&
                fallenPackageSprites != null &&
                spriteIndex >= 0 &&
                spriteIndex < fallenPackageSprites.Length
                    ? fallenPackageSprites[spriteIndex]
                    : null;
        }

        private void SetSprites(Sprite bodySprite, Sprite packageSprite, int directionIndex)
        {
            SetSprite(bodySprite);
            ResolvePackageSpriteRenderer();
            if (packageSpriteRenderer == null)
            {
                return;
            }

            packageSpriteRenderer.sprite = packageSprite;
            packageSpriteRenderer.enabled = packageSprite != null;
            packageSpriteRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            packageSpriteRenderer.sortingOrder = IsPackageBehindPlayer(directionIndex)
                ? spriteRenderer.sortingOrder - 1
                : spriteRenderer.sortingOrder + 1;
        }

        private void ResolvePackageSpriteRenderer()
        {
            if (packageSpriteRenderer != null || spriteRenderer == null)
            {
                return;
            }

            Transform packageVisual = transform.Find("PackageVisual");
            if (packageVisual == null)
            {
                var packageObject = new GameObject("PackageVisual");
                packageObject.layer = spriteRenderer.gameObject.layer;
                packageVisual = packageObject.transform;
                packageVisual.SetParent(
                    spriteRenderer.transform == transform
                        ? transform
                        : spriteRenderer.transform.parent,
                    false);
                packageVisual.localPosition = spriteRenderer.transform == transform
                    ? Vector3.zero
                    : spriteRenderer.transform.localPosition;
                packageVisual.localRotation = spriteRenderer.transform == transform
                    ? Quaternion.identity
                    : spriteRenderer.transform.localRotation;
                packageVisual.localScale = spriteRenderer.transform == transform
                    ? Vector3.one
                    : spriteRenderer.transform.localScale;
            }

            packageSpriteRenderer = packageVisual.GetComponent<SpriteRenderer>();
            if (packageSpriteRenderer == null)
            {
                packageSpriteRenderer = packageVisual.gameObject.AddComponent<SpriteRenderer>();
            }

            packageSpriteRenderer.color = spriteRenderer.color;
            packageSpriteRenderer.sharedMaterial = spriteRenderer.sharedMaterial;
            packageSpriteRenderer.drawMode = SpriteDrawMode.Simple;
            packageSpriteRenderer.enabled = false;
        }

        private void EnsurePackageDamageVisual()
        {
            if (packageSpriteRenderer == null)
            {
                return;
            }

            packageDamageVisual = packageSpriteRenderer
                .GetComponent<SubwayCarry.Gameplay.PackageDamageVisual>();
            if (packageDamageVisual == null)
            {
                packageDamageVisual = packageSpriteRenderer.gameObject
                    .AddComponent<SubwayCarry.Gameplay.PackageDamageVisual>();
            }

            packageDamageVisual.Configure(packageSpriteRenderer);
        }

        private static bool IsPackageBehindPlayer(int directionIndex)
        {
            return directionIndex >= 3 && directionIndex <= 5;
        }

        internal static int GetSittingDirectionIndex(int directionIndex)
        {
            return IsPackageBehindPlayer(directionIndex) ? 1 : 0;
        }

        internal static int GetFallenDirectionIndex(int directionIndex)
        {
            return ((directionIndex + 1) / 2) % FallenDirectionCount;
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
