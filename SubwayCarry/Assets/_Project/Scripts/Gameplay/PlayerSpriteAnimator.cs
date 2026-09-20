using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController), typeof(PlayerPosture))]
    public sealed class PlayerSpriteAnimator : MonoBehaviour
    {
        private const int DirectionCount = 8;
        private const int SittingDirectionCount = 2;
        private const int OverheadTransitionDirectionCount = 2;
        private const int FallenDirectionCount = 4;

        [Header("References")]
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerPosture posture;
        [SerializeField] private PlayerPackageCarrier packageCarrier;
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

        [Header("Sprites - Sitting Transition (South, North)")]
        [Tooltip("Direction-major order. Each direction contains sittingTransitionFramesPerDirection consecutive frames from standing to sitting.")]
        [SerializeField] private Sprite[] sittingTransitionBodySprites =
            new Sprite[SittingDirectionCount * 5];
        [SerializeField] private Sprite[] sittingTransitionPackageSprites =
            new Sprite[SittingDirectionCount * 5];

        [Header("Sprites - Overhead Carry")]
        [SerializeField] private Sprite[] overheadIdleSprites = new Sprite[DirectionCount];
        [SerializeField] private Sprite[] overheadWalkSprites = new Sprite[DirectionCount * 3];
        [SerializeField] private Sprite[] overheadPackageIdleSprites = new Sprite[DirectionCount];
        [SerializeField] private Sprite[] overheadPackageWalkSprites = new Sprite[DirectionCount * 3];

        [Header("Sprites - Overhead Transition (South, North)")]
        [Tooltip("Direction-major order. Each direction contains overheadTransitionFramesPerDirection consecutive frames from standing carry to overhead carry.")]
        [SerializeField] private Sprite[] overheadTransitionBodySprites =
            new Sprite[OverheadTransitionDirectionCount * 5];
        [SerializeField] private Sprite[] overheadTransitionPackageSprites =
            new Sprite[OverheadTransitionDirectionCount * 5];

        [Header("Sprites - Fallen (South, West, North, East)")]
        [Tooltip("Direction-major order. Each cardinal direction contains fallenFramesPerDirection consecutive frames.")]
        [SerializeField] private Sprite[] fallenBodySprites = new Sprite[FallenDirectionCount * 5];
        [SerializeField] private Sprite[] fallenPackageSprites = new Sprite[FallenDirectionCount * 5];

        [Header("Playback")]
        [SerializeField, Min(1)] private int walkFramesPerDirection = 3;
        [SerializeField, Min(0.1f)] private float walkFramesPerSecond = 8f;
        [SerializeField, Min(1)] private int sittingTransitionFramesPerDirection = 5;
        [SerializeField, Min(1)] private int overheadTransitionFramesPerDirection = 5;
        [SerializeField, Min(1)] private int fallenFramesPerDirection = 5;
        [SerializeField, Min(0.1f)] private float fallenFramesPerSecond = 9f;
        [SerializeField] private bool useMovementDirectionWhileMoving;

        private float walkElapsed;
        private float fallenElapsed;
        private bool wasMoving;
        private bool wasFallen;
        private bool wasCarryingPackage;
        private int previousDirectionIndex = -1;
        private int lockedFallenDirectionIndex = -1;
        private PackageDamageVisual packageDamageVisual;

        public bool IsConfigured =>
            controller != null &&
            posture != null &&
            packageCarrier != null &&
            spriteRenderer != null &&
            HasAssignedSprites(idleSprites, DirectionCount) &&
            HasAssignedSprites(walkSprites, DirectionCount * walkFramesPerDirection) &&
            HasAssignedSprites(carryingIdleSprites, DirectionCount) &&
            HasAssignedSprites(carryingWalkSprites, DirectionCount * walkFramesPerDirection) &&
            HasAssignedSprites(packageIdleSprites, DirectionCount) &&
            HasAssignedSprites(packageWalkSprites, DirectionCount * walkFramesPerDirection) &&
            HasAssignedSprites(sittingTransitionBodySprites,
                SittingDirectionCount * sittingTransitionFramesPerDirection) &&
            HasAssignedSprites(sittingTransitionPackageSprites,
                SittingDirectionCount * sittingTransitionFramesPerDirection) &&
            HasAssignedSprites(overheadIdleSprites, DirectionCount) &&
            HasAssignedSprites(overheadWalkSprites, DirectionCount * walkFramesPerDirection) &&
            HasAssignedSprites(overheadPackageIdleSprites, DirectionCount) &&
            HasAssignedSprites(overheadPackageWalkSprites,
                DirectionCount * walkFramesPerDirection) &&
            HasAssignedSprites(overheadTransitionBodySprites,
                OverheadTransitionDirectionCount * overheadTransitionFramesPerDirection) &&
            HasAssignedSprites(overheadTransitionPackageSprites,
                OverheadTransitionDirectionCount * overheadTransitionFramesPerDirection) &&
            HasAssignedSprites(fallenBodySprites,
                FallenDirectionCount * fallenFramesPerDirection) &&
            HasAssignedSprites(fallenPackageSprites,
                FallenDirectionCount * fallenFramesPerDirection);

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

            if (packageCarrier == null)
            {
                packageCarrier = GetComponent<PlayerPackageCarrier>();
            }

            if (spriteRenderer == null)
            {
                Transform bodyVisual = transform.Find("BodyVisual");
                spriteRenderer = bodyVisual != null
                    ? bodyVisual.GetComponent<SpriteRenderer>()
                    : GetComponentInChildren<SpriteRenderer>();
            }

            ResolvePackageSpriteRenderer();
            EnsurePackageDamageVisual();
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
            bool isCarryingPackage = packageCarrier != null && packageCarrier.HasPackage;
            Vector2 direction = isMoving && useMovementDirectionWhileMoving
                ? controller.MoveInput
                : controller.FacingDirection;
            int directionIndex = GetDirectionIndex(direction);

            if (posture != null && posture.CurrentState == CarryPosture.Fallen)
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
                        lockedFallenDirectionIndex,
                        isCarryingPackage),
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

            if (IsSittingTransition())
            {
                walkElapsed = 0f;
                int sittingDirectionIndex = GetSittingDirectionIndex(directionIndex);
                int sittingFrameIndex = GetSittingTransitionFrameIndex(
                    posture.TransitionProgress,
                    posture.TargetState == CarryPosture.Sitting,
                    sittingTransitionFramesPerDirection);
                bool startsSeated = posture.CurrentState == CarryPosture.Sitting;
                SetSprites(
                    GetSittingTransitionBodySprite(
                        sittingDirectionIndex,
                        sittingFrameIndex,
                        directionIndex,
                        isCarryingPackage,
                        startsSeated),
                    isCarryingPackage
                        ? GetSittingTransitionPackageSprite(
                            sittingDirectionIndex,
                            sittingFrameIndex,
                            startsSeated)
                        : null,
                    directionIndex);
                wasMoving = false;
                wasCarryingPackage = isCarryingPackage;
                previousDirectionIndex = directionIndex;
                return;
            }

            if (posture != null && posture.CurrentState == CarryPosture.Sitting)
            {
                walkElapsed = 0f;
                int sittingDirectionIndex = GetSittingDirectionIndex(directionIndex);
                SetSprites(
                    GetSittingBodySprite(sittingDirectionIndex, directionIndex, isCarryingPackage),
                    isCarryingPackage ? GetSittingPackageSprite(sittingDirectionIndex) : null,
                    directionIndex);
                wasMoving = false;
                wasCarryingPackage = isCarryingPackage;
                previousDirectionIndex = directionIndex;
                return;
            }

            if (IsOverheadTransition())
            {
                walkElapsed = 0f;
                int overheadDirectionIndex = GetSittingDirectionIndex(directionIndex);
                int overheadFrameIndex = GetSittingTransitionFrameIndex(
                    posture.TransitionProgress,
                    posture.TargetState == CarryPosture.OverheadCarry,
                    overheadTransitionFramesPerDirection);
                bool startsOverhead = posture.CurrentState == CarryPosture.OverheadCarry;
                SetSprites(
                    GetOverheadTransitionBodySprite(
                        overheadDirectionIndex,
                        overheadFrameIndex,
                        directionIndex,
                        isCarryingPackage,
                        startsOverhead),
                    isCarryingPackage
                        ? GetOverheadTransitionPackageSprite(
                            overheadDirectionIndex,
                            overheadFrameIndex,
                            directionIndex,
                            startsOverhead)
                        : null,
                    directionIndex);
                wasMoving = false;
                wasCarryingPackage = isCarryingPackage;
                previousDirectionIndex = directionIndex;
                return;
            }

            bool isOverhead = posture != null &&
                posture.CurrentState == CarryPosture.OverheadCarry;

            if (!isMoving)
            {
                walkElapsed = 0f;
                SetSprites(
                    isOverhead
                        ? GetOverheadIdleSprite(directionIndex, isCarryingPackage)
                        : GetIdleSprite(directionIndex, isCarryingPackage),
                    isCarryingPackage
                        ? isOverhead
                            ? GetOverheadPackageIdleSprite(directionIndex)
                            : GetPackageIdleSprite(directionIndex)
                        : null,
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

                int frameIndex = Mathf.FloorToInt(walkElapsed * walkFramesPerSecond) % walkFramesPerDirection;
                SetSprites(
                    isOverhead
                        ? GetOverheadWalkSprite(directionIndex, frameIndex, isCarryingPackage)
                        : GetWalkSprite(directionIndex, frameIndex, isCarryingPackage),
                    isCarryingPackage
                        ? isOverhead
                            ? GetOverheadPackageWalkSprite(directionIndex, frameIndex)
                            : GetPackageWalkSprite(directionIndex, frameIndex)
                        : null,
                    directionIndex);
            }

            wasMoving = isMoving;
            wasCarryingPackage = isCarryingPackage;
            previousDirectionIndex = directionIndex;
        }

        private Sprite GetIdleSprite(int directionIndex, bool isCarryingPackage)
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

        private Sprite GetWalkSprite(
            int directionIndex,
            int frameIndex,
            bool isCarryingPackage)
        {
            if (walkFramesPerDirection <= 0)
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

            if (walkSprites == null)
            {
                return null;
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

        private Sprite GetOverheadIdleSprite(int directionIndex, bool isCarryingPackage)
        {
            if (overheadIdleSprites != null &&
                directionIndex >= 0 &&
                directionIndex < overheadIdleSprites.Length &&
                overheadIdleSprites[directionIndex] != null)
            {
                return overheadIdleSprites[directionIndex];
            }

            return GetIdleSprite(directionIndex, isCarryingPackage);
        }

        private Sprite GetOverheadWalkSprite(
            int directionIndex,
            int frameIndex,
            bool isCarryingPackage)
        {
            if (walkFramesPerDirection <= 0)
            {
                return null;
            }

            int spriteIndex = directionIndex * walkFramesPerDirection + frameIndex;
            if (overheadWalkSprites != null &&
                spriteIndex >= 0 &&
                spriteIndex < overheadWalkSprites.Length &&
                overheadWalkSprites[spriteIndex] != null)
            {
                return overheadWalkSprites[spriteIndex];
            }

            return GetWalkSprite(directionIndex, frameIndex, isCarryingPackage);
        }

        private Sprite GetOverheadPackageIdleSprite(int directionIndex)
        {
            if (overheadPackageIdleSprites != null &&
                directionIndex >= 0 &&
                directionIndex < overheadPackageIdleSprites.Length &&
                overheadPackageIdleSprites[directionIndex] != null)
            {
                return overheadPackageIdleSprites[directionIndex];
            }

            return GetPackageIdleSprite(directionIndex);
        }

        private Sprite GetOverheadPackageWalkSprite(int directionIndex, int frameIndex)
        {
            if (walkFramesPerDirection <= 0)
            {
                return null;
            }

            int spriteIndex = directionIndex * walkFramesPerDirection + frameIndex;
            if (overheadPackageWalkSprites != null &&
                spriteIndex >= 0 &&
                spriteIndex < overheadPackageWalkSprites.Length &&
                overheadPackageWalkSprites[spriteIndex] != null)
            {
                return overheadPackageWalkSprites[spriteIndex];
            }

            return GetPackageWalkSprite(directionIndex, frameIndex);
        }

        private Sprite GetSittingBodySprite(
            int sittingDirectionIndex,
            int directionIndex,
            bool isCarryingPackage)
        {
            Sprite transitionEndSprite = GetSittingTransitionSprite(
                sittingTransitionBodySprites,
                sittingDirectionIndex,
                Mathf.Max(0, sittingTransitionFramesPerDirection - 1));
            if (transitionEndSprite != null)
            {
                return transitionEndSprite;
            }

            if (sittingBodySprites != null &&
                sittingDirectionIndex < sittingBodySprites.Length &&
                sittingBodySprites[sittingDirectionIndex] != null)
            {
                return sittingBodySprites[sittingDirectionIndex];
            }

            return GetIdleSprite(directionIndex, isCarryingPackage);
        }

        private Sprite GetSittingPackageSprite(int sittingDirectionIndex)
        {
            Sprite transitionEndSprite = GetSittingTransitionSprite(
                sittingTransitionPackageSprites,
                sittingDirectionIndex,
                Mathf.Max(0, sittingTransitionFramesPerDirection - 1));
            if (transitionEndSprite != null)
            {
                return transitionEndSprite;
            }

            return sittingPackageSprites != null &&
                sittingDirectionIndex < sittingPackageSprites.Length
                ? sittingPackageSprites[sittingDirectionIndex]
                : null;
        }

        private Sprite GetFallenBodySprite(
            int fallenDirectionIndex,
            int frameIndex,
            int directionIndex,
            bool isCarryingPackage)
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

            return GetIdleSprite(directionIndex, isCarryingPackage);
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

        private bool IsSittingTransition()
        {
            return posture != null &&
                   posture.IsTransitioning &&
                   (posture.CurrentState == CarryPosture.Sitting ||
                    posture.TargetState == CarryPosture.Sitting);
        }

        private bool IsOverheadTransition()
        {
            return posture != null &&
                   posture.IsTransitioning &&
                   (posture.CurrentState == CarryPosture.OverheadCarry ||
                    posture.TargetState == CarryPosture.OverheadCarry);
        }

        private Sprite GetOverheadTransitionBodySprite(
            int overheadDirectionIndex,
            int frameIndex,
            int directionIndex,
            bool isCarryingPackage,
            bool startsOverhead)
        {
            Sprite sprite = GetOverheadTransitionSprite(
                overheadTransitionBodySprites,
                overheadDirectionIndex,
                frameIndex);
            if (sprite != null)
            {
                return sprite;
            }

            return startsOverhead
                ? GetOverheadIdleSprite(directionIndex, isCarryingPackage)
                : GetIdleSprite(directionIndex, isCarryingPackage);
        }

        private Sprite GetOverheadTransitionPackageSprite(
            int overheadDirectionIndex,
            int frameIndex,
            int directionIndex,
            bool startsOverhead)
        {
            Sprite sprite = GetOverheadTransitionSprite(
                overheadTransitionPackageSprites,
                overheadDirectionIndex,
                frameIndex);
            if (sprite != null)
            {
                return sprite;
            }

            return startsOverhead
                ? GetOverheadPackageIdleSprite(directionIndex)
                : GetPackageIdleSprite(directionIndex);
        }

        private Sprite GetOverheadTransitionSprite(
            Sprite[] sprites,
            int overheadDirectionIndex,
            int frameIndex)
        {
            if (sprites == null || overheadTransitionFramesPerDirection <= 0)
            {
                return null;
            }

            int spriteIndex =
                overheadDirectionIndex * overheadTransitionFramesPerDirection + frameIndex;
            return spriteIndex >= 0 && spriteIndex < sprites.Length
                ? sprites[spriteIndex]
                : null;
        }

        private Sprite GetSittingTransitionBodySprite(
            int sittingDirectionIndex,
            int frameIndex,
            int directionIndex,
            bool isCarryingPackage,
            bool startsSeated)
        {
            Sprite sprite = GetSittingTransitionSprite(
                sittingTransitionBodySprites,
                sittingDirectionIndex,
                frameIndex);
            if (sprite != null)
            {
                return sprite;
            }

            return startsSeated
                ? GetSittingBodySprite(sittingDirectionIndex, directionIndex, isCarryingPackage)
                : GetIdleSprite(directionIndex, isCarryingPackage);
        }

        private Sprite GetSittingTransitionPackageSprite(
            int sittingDirectionIndex,
            int frameIndex,
            bool startsSeated)
        {
            Sprite sprite = GetSittingTransitionSprite(
                sittingTransitionPackageSprites,
                sittingDirectionIndex,
                frameIndex);
            if (sprite != null)
            {
                return sprite;
            }

            return startsSeated
                ? GetSittingPackageSprite(sittingDirectionIndex)
                : GetPackageIdleSprite(sittingDirectionIndex == 1 ? 4 : 0);
        }

        private Sprite GetSittingTransitionSprite(
            Sprite[] sprites,
            int sittingDirectionIndex,
            int frameIndex)
        {
            if (sprites == null || sittingTransitionFramesPerDirection <= 0)
            {
                return null;
            }

            int spriteIndex =
                sittingDirectionIndex * sittingTransitionFramesPerDirection + frameIndex;
            return spriteIndex >= 0 && spriteIndex < sprites.Length
                ? sprites[spriteIndex]
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

            packageDamageVisual =
                packageSpriteRenderer.GetComponent<PackageDamageVisual>();
            if (packageDamageVisual == null)
            {
                packageDamageVisual = packageSpriteRenderer.gameObject
                    .AddComponent<PackageDamageVisual>();
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

        internal static int GetSittingTransitionFrameIndex(
            float transitionProgress,
            bool sittingDown,
            int frameCount)
        {
            int safeFrameCount = Mathf.Max(1, frameCount);
            int frameIndex = Mathf.Clamp(
                Mathf.FloorToInt(Mathf.Clamp01(transitionProgress) * safeFrameCount),
                0,
                safeFrameCount - 1);
            return sittingDown ? frameIndex : safeFrameCount - 1 - frameIndex;
        }

        private static bool HasAssignedSprites(Sprite[] sprites, int expectedCount)
        {
            if (sprites == null || sprites.Length != expectedCount)
            {
                return false;
            }

            foreach (Sprite sprite in sprites)
            {
                if (sprite == null)
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

            float clockwiseAngleFromSouth = Mathf.Atan2(-direction.x, -direction.y) * Mathf.Rad2Deg;
            if (clockwiseAngleFromSouth < 0f)
            {
                clockwiseAngleFromSouth += 360f;
            }

            return Mathf.RoundToInt(clockwiseAngleFromSouth / 45f) % DirectionCount;
        }
    }
}
