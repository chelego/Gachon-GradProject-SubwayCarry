using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController), typeof(PlayerPosture))]
    public sealed class PlayerSpriteAnimator : MonoBehaviour
    {
        private const int DirectionCount = 8;
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

        [Header("Sprites - Fallen (South, West, North, East)")]
        [Tooltip("Direction-major order. Each cardinal direction contains fallenFramesPerDirection consecutive frames.")]
        [SerializeField] private Sprite[] fallenBodySprites = new Sprite[FallenDirectionCount * 5];
        [SerializeField] private Sprite[] fallenPackageSprites = new Sprite[FallenDirectionCount * 5];

        [Header("Playback")]
        [SerializeField, Min(1)] private int walkFramesPerDirection = 3;
        [SerializeField, Min(0.1f)] private float walkFramesPerSecond = 8f;
        [SerializeField, Min(1)] private int fallenFramesPerDirection = 5;
        [SerializeField, Min(0.1f)] private float fallenFramesPerSecond = 9f;
        [SerializeField] private bool useMovementDirectionWhileMoving = true;

        private float walkElapsed;
        private float fallenElapsed;
        private bool wasMoving;
        private bool wasFallen;
        private bool wasCarryingPackage;
        private int previousDirectionIndex = -1;
        private int lockedFallenDirectionIndex = -1;
        private PackageDamageVisual packageDamageVisual;
        // Opt-in integration mode; prototype prefabs keep their existing locomotion.
        public bool MouseRelativeLocomotion { get; set; }
        SpriteRenderer legs;
        readonly System.Collections.Generic.Dictionary<Sprite, Sprite[]> splitSprites = new System.Collections.Generic.Dictionary<Sprite, Sprite[]>();
        float stride;
        Vector2 lastGroundPosition;
        bool groundSampled;
        int legDirection, legFrame;
        bool splitLocomotion;
        MaterialPropertyBlock legProperties;
        float lastMovedAt;

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
            Vector2 travelDirection = controller.MoveInput;
            bool isCarryingPackage = packageCarrier != null && packageCarrier.HasPackage;
            Vector2 direction = isMoving && useMovementDirectionWhileMoving && !MouseRelativeLocomotion
                ? controller.MoveInput
                : controller.FacingDirection;
            int directionIndex = GetDirectionIndex(direction);
            splitLocomotion = false;
            if (legs != null) legs.enabled = false;
            if (MouseRelativeLocomotion)
            {
                Vector2 current = transform.position;
                Vector2 displacement = groundSampled ? current - lastGroundPosition : Vector2.zero;
                float distance = groundSampled ? Vector2.Distance(current, lastGroundPosition) : 0;
                lastGroundPosition = current; groundSampled = true;
                if (distance < .4f && distance > .001f) { stride += distance * 2.8f; lastMovedAt = Time.time; }
                // Actual displacement prevents walking against a wall or while a cut-in has paused time.
                // Guided stairs disable input, but should still animate the visible traversal.
                if (!controller.enabled)
                {
                    travelDirection = displacement.normalized;
                    isMoving = posture != null && posture.CanMove && distance > .001f && distance < .4f;
                }
                isMoving &= Time.time - lastMovedAt < .09f && Time.deltaTime > 0;
                legDirection = GetDirectionIndex(travelDirection);
                legFrame = Mathf.FloorToInt(stride) % walkFramesPerDirection;
            }

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

            if (!isMoving)
            {
                walkElapsed = 0f;
                SetSprites(
                    GetIdleSprite(directionIndex, isCarryingPackage),
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

                int frameIndex = Mathf.FloorToInt(walkElapsed * walkFramesPerSecond) % walkFramesPerDirection;
                if (MouseRelativeLocomotion)
                {
                    float relative = Vector2.Dot(controller.FacingDirection, travelDirection.normalized);
                    bool backing = relative < -.5f;
                    frameIndex = backing ? (walkFramesPerDirection - 1 - legFrame) : legFrame;
                    // Reverse the facing cycle for backpedaling; side steps use travel-facing legs only.
                    if (backing) { legDirection = directionIndex; legFrame = frameIndex; }
                    splitLocomotion = true;
                }
                SetSprites(
                    GetWalkSprite(directionIndex, frameIndex, isCarryingPackage),
                    isCarryingPackage
                        ? GetPackageWalkSprite(directionIndex, frameIndex)
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

        private Sprite GetSittingBodySprite(
            int sittingDirectionIndex,
            int directionIndex,
            bool isCarryingPackage)
        {
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

        private void SetSprites(Sprite bodySprite, Sprite packageSprite, int directionIndex)
        {
            if (splitLocomotion && bodySprite != null)
            {
                Sprite legSource = GetWalkSprite(legDirection, legFrame, false);
                if (legSource != null)
                {
                    EnsureLegs();
                    var upper = Split(bodySprite); var lower = Split(legSource);
                    bodySprite = upper[1]; legs.sprite = lower[0]; legs.enabled = true;
                    legs.color = spriteRenderer.color; legs.sharedMaterial = spriteRenderer.sharedMaterial;
                    legs.transform.localPosition = spriteRenderer.transform.localPosition;
                    legs.transform.localScale = spriteRenderer.transform.localScale;
                    legs.sortingLayerID = spriteRenderer.sortingLayerID; legs.sortingOrder = spriteRenderer.sortingOrder;
                    Rect r = legs.sprite.textureRect; Texture t = legs.sprite.texture;
                    if (legProperties == null) legProperties = new MaterialPropertyBlock();
                    legProperties.SetVector("_UvRect", new Vector4(r.xMin / t.width, r.yMin / t.height, r.xMax / t.width, r.yMax / t.height));
                    legs.SetPropertyBlock(legProperties);
                }
            }
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

        void EnsureLegs()
        {
            if (legs != null) return;
            var go = new GameObject("Locomotion_Legs"); go.transform.SetParent(spriteRenderer.transform.parent, false);
            legs = go.AddComponent<SpriteRenderer>();
        }
        Sprite[] Split(Sprite source)
        {
            if (splitSprites.TryGetValue(source, out var result)) return result;
            // Source sheets are full-rect, unatlased sprites. Cut at the hips, keeping the original foot pivot.
            Rect r = source.rect; float cut = Mathf.Round(r.height * .40f);
            var low = new Rect(r.x, r.y, r.width, cut); var high = new Rect(r.x, r.y + cut, r.width, r.height - cut);
            Vector2 pivot = source.pivot;
            result = new[] {
                Sprite.Create(source.texture, low, new Vector2(pivot.x / low.width, pivot.y / low.height), source.pixelsPerUnit, 0, SpriteMeshType.FullRect),
                Sprite.Create(source.texture, high, new Vector2(pivot.x / high.width, (pivot.y - cut) / high.height), source.pixelsPerUnit, 0, SpriteMeshType.FullRect)
            };
            splitSprites.Add(source, result); return result;
        }
        void OnDestroy()
        {
            foreach (var pair in splitSprites) foreach (var sprite in pair.Value) if (sprite != null) Destroy(sprite);
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
