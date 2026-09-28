using UnityEngine;
using UnityEngine.Rendering;
using SubwayCarry.AI.V2;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    [DefaultExecutionOrder(600)]
    public sealed class SliceCharacterVisual : MonoBehaviour
    {
        public SpriteRenderer body;
        public SpriteRenderer package;
        public Sprite[] idleSprites;
        public Sprite[] walkSprites;
        public Sprite[] sittingSprites;
        public PassengerAiV2Agent agent;
        public SlicePassengerIntent intent;
        public bool player;
        public SliceGameController world;
        public SliceMap map;
        public float visualScale = 1.1f;
        SortingGroup sortingGroup;
        Sprite previousSprite;
        Sprite previousPackageSprite;
        MaterialPropertyBlock block;
        readonly System.Collections.Generic.Dictionary<Sprite, Vector4> spriteUvRects = new System.Collections.Generic.Dictionary<Sprite, Vector4>(64);
        float nextFrame;
        float directionStableUntil;
        int direction;
        Vector3 baseBodyPosition, baseBodyScale;
        bool poseInitialized, poseActive;
        float poseBlend, nextPoseLookup;
        SliceInterest heldPose;
        SubwayCarry.Gameplay.PlayerPosture playerPosture;
        SubwayCarry.Gameplay.PlayerPackageCarrier carrier;
        SubwayCarry.Gameplay.PlayerController playerInput;
        Collider2D physicalBody;
        Vector2 originalBodyOffset;
        bool originalTrigger;
        SubwayCarry.Gameplay.PackageImpactSensor[] impactSensors;
        bool[] resumeSensors;
        bool sensorsSuspended;
        public Bounds VisibleBounds => body != null ? body.bounds : new Bounds(transform.position + Vector3.up * .7f, new Vector3(.8f, 1.4f, 0));
        void AlignContacts(Vector2 groundOffset, bool held, bool transitioning)
        {
            if (physicalBody != null)
            {
                Vector2 offset = originalBodyOffset + (Vector2)transform.InverseTransformVector(groundOffset);
                if ((physicalBody.offset - offset).sqrMagnitude > .000001f) physicalBody.offset = offset;
                // Occupancy is inside the chair footprint, so the chair must not push the navigation root.
                if (physicalBody.isTrigger != (held || originalTrigger)) physicalBody.isTrigger = held || originalTrigger;
            }
            if (impactSensors == null || world == null || world.deliveryCatalog == null) return;
            for (int i = 0; i < impactSensors.Length; i++)
            {
                var sensor = impactSensors[i];
                if (sensor == null || sensor.PackageHitbox == null) continue;
                var catalog = world.deliveryCatalog;
                Vector2 facing = held ? heldPose.facing : playerInput.FacingDirection;
                Vector2 centre = (Vector2)transform.position + groundOffset + facing * catalog.packageContactReach;
                Vector2 packageOffset = sensor.PackageHitbox.transform.InverseTransformPoint(centre);
                if ((sensor.PackageHitbox.offset - packageOffset).sqrMagnitude > .000001f) sensor.PackageHitbox.offset = packageOffset;
                if (sensor.PackageHitbox is BoxCollider2D box)
                {
                    Vector3 scale = box.transform.lossyScale;
                    Vector2 size = new Vector2(catalog.packageContactSize.x / Mathf.Max(.01f, Mathf.Abs(scale.x)), catalog.packageContactSize.y / Mathf.Max(.01f, Mathf.Abs(scale.y)));
                    if ((box.size - size).sqrMagnitude > .000001f) box.size = size;
                }
                // Pose interpolation is not an impact. Resume once, with a fresh velocity sample.
                if (transitioning && !sensorsSuspended) { resumeSensors[i] = sensor.enabled; sensor.enabled = false; }
                else if (!transitioning && sensorsSuspended && resumeSensors[i]) sensor.enabled = true;
            }
            sensorsSuspended = transitioning;
        }
        public void ResetFacilityPose()
        {
            EnsurePoseInitialized();
            poseBlend = 0; poseActive = false; nextPoseLookup = 0;
            AlignContacts(Vector2.zero, false, false);
            if (poseInitialized) { body.transform.localPosition = baseBodyPosition; body.transform.localScale = baseBodyScale; body.transform.localRotation = Quaternion.identity; body.flipX = false; }
            if (poseInitialized && package != null)
            {
                package.transform.localPosition = baseBodyPosition;
                package.transform.localScale = baseBodyScale;
                package.transform.localRotation = Quaternion.identity;
                package.flipX = false;
            }
        }
        void Awake() { sortingGroup = GetComponent<SortingGroup>(); if (sortingGroup == null) sortingGroup = gameObject.AddComponent<SortingGroup>(); }
        void EnsurePoseInitialized()
        {
            if (body == null) return;
            if (!poseInitialized)
            {
                poseInitialized = true; baseBodyPosition = body.transform.localPosition; baseBodyScale = body.transform.localScale;
                physicalBody = GetComponent<Collider2D>();
                if (physicalBody != null) { originalBodyOffset = physicalBody.offset; originalTrigger = physicalBody.isTrigger; }
                if (player)
                {
                    playerPosture = GetComponent<SubwayCarry.Gameplay.PlayerPosture>(); carrier = GetComponent<SubwayCarry.Gameplay.PlayerPackageCarrier>();
                    playerInput = GetComponent<SubwayCarry.Gameplay.PlayerController>();
                    impactSensors = GetComponentsInChildren<SubwayCarry.Gameplay.PackageImpactSensor>(true);
                    resumeSensors = new bool[impactSensors.Length];
                }
            }
        }
        void LateUpdate()
        {
            if (body == null) return;
            EnsurePoseInitialized();
            int order = map != null ? map.GroundOrder(transform.position) : Mathf.Clamp(Mathf.RoundToInt(-transform.position.y * 80f), -25000, 25000);
            sortingGroup.sortingOrder = order;
            if (package == null && player) { Transform t = transform.Find("PackageVisual"); if (t != null) package = t.GetComponent<SpriteRenderer>(); }
            // Keep the animator's front/back package decision inside the same occlusion group.
            if (package != null) package.sortingOrder = package.sortingOrder < body.sortingOrder ? -1 : 1;
            body.sortingOrder = 0;
            if (agent != null && Time.time >= nextFrame)
            {
                nextFrame = Time.time + 0.1f;
                Vector2 v = agent.Velocity;
                if (v.sqrMagnitude > 0.0324f && Time.time >= directionStableUntil)
                {
                    float angle = Mathf.Atan2(-v.x, -v.y) * Mathf.Rad2Deg;
                    if (angle < 0) angle += 360f;
                    int proposed = Mathf.RoundToInt(angle / 45f) % 8;
                    if (Mathf.Abs(Mathf.DeltaAngle(direction * 45f, angle)) > 28f) { direction = proposed; directionStableUntil = Time.time + 0.2f; }
                }
                int frame = direction * 3 + Mathf.FloorToInt(Time.time * 8) % 3;
                body.sprite = v.sqrMagnitude > 0.0225f && frame < walkSprites.Length ? walkSprites[frame] : idleSprites[direction];
                bool seatedArt = intent != null && intent.Sitting && sittingSprites != null && sittingSprites.Length >= 2;
                if (seatedArt)
                {
                    bool back = map != null && map.placeKind == PassengerAiV2PlaceKind.TrainInterior
                        ? transform.position.y - .5f * transform.position.x < 3 : direction >= 3 && direction <= 5;
                    body.sprite = sittingSprites[back ? 1 : 0];
                }
                body.transform.localScale = new Vector3(visualScale, visualScale * (intent != null && intent.Sitting && !seatedArt ? 0.76f : 1f), 1);
                body.transform.localRotation = Quaternion.Euler(0, 0, intent != null && intent.Leaning ? 7 : 0);
            }
            ApplyFacilityPose();
            if (player && body.sprite != previousSprite)
            {
                previousSprite = body.sprite;
                ApplySpriteRect(body);
            }
            if (player && package != null && package.sprite != previousPackageSprite) { previousPackageSprite = package.sprite; ApplySpriteRect(package); }
        }
        void ApplyFacilityPose()
        {
            if (world == null || world.deliveryCatalog == null) return;
            if (player && playerPosture.CurrentState == SubwayCarry.Core.Contracts.CarryPosture.Fallen) { ResetFacilityPose(); return; }
            bool sitting = player ? playerPosture.CurrentState == SubwayCarry.Core.Contracts.CarryPosture.Sitting : intent != null && intent.Sitting;
            bool leaning = player ? playerPosture.CurrentState == SubwayCarry.Core.Contracts.CarryPosture.Leaning : intent != null && intent.Leaning;
            if ((!sitting && !leaning) || (player && world.PlayerLeavingFacility)) poseActive = false;
            else if (Time.time >= nextPoseLookup)
            {
                nextPoseLookup = Time.time + .2f;
                poseActive = world.TryGetOccupiedPose(agent, out var pose);
                if (poseActive) heldPose = pose;
            }
            float oldBlend = poseBlend;
            poseBlend = Mathf.MoveTowards(poseBlend, poseActive ? 1 : 0, Time.deltaTime / Mathf.Max(.1f, world.deliveryCatalog.facilityPoseSeconds));
            if (poseBlend <= 0 && oldBlend <= 0) { AlignContacts(Vector2.zero, false, false); return; }
            bool seated = heldPose.kind == PassengerAiV2InteriorSpotKind.Seat;
            bool back = heldPose.facing.y > 0;
            int spriteIndex = back ? 1 : 0;
            float weight = Mathf.SmoothStep(0, 1, poseBlend);
            Vector2 occupiedGround = heldPose.contactPoint - Vector2.up * (seated ? .5f : .95f);
            AlignContacts((occupiedGround - (Vector2)transform.position) * weight, poseBlend > 0,
                poseBlend > 0 && poseBlend < 1);
            var catalog = world.deliveryCatalog;
            // Platform benches and carriage benches both require the diagonal seat views.
            bool trainPose = seated && poseBlend > .35f && Mathf.Abs(heldPose.facing.x) > .3f &&
                catalog.trainSittingSprites != null && catalog.trainSittingSprites.Length > spriteIndex && catalog.trainSittingSprites[spriteIndex] != null;
            if (trainPose) body.sprite = catalog.trainSittingSprites[spriteIndex];
            else if (seated && poseBlend > .35f && sittingSprites != null && sittingSprites.Length > spriteIndex)
                body.sprite = sittingSprites[spriteIndex];
            else if (idleSprites != null && idleSprites.Length == 8 && (poseActive || (player && world.PlayerLeavingFacility)))
            {
                float angle = Mathf.Repeat(Mathf.Atan2(-heldPose.facing.x, -heldPose.facing.y) * Mathf.Rad2Deg, 360);
                body.sprite = idleSprites[Mathf.RoundToInt(angle / 45f) % 8];
            }
            Vector3 scale = baseBodyScale;
            body.flipX = trainPose && (back ? heldPose.facing.x > 0 : heldPose.facing.x < 0);
            // A short knee bend before the held pose, not a permanent squashed standing sprite.
            if (seated && poseBlend < .35f) scale.y *= 1 - .12f * Mathf.Sin(poseBlend / .35f * Mathf.PI);
            body.transform.localScale = scale;
            body.transform.localRotation = Quaternion.Euler(0, 0, seated ? 0 : (back ? -6 : 6) * weight);
            if (body.sprite != null)
            {
                Vector3 baseline = transform.TransformPoint(baseBodyPosition);
                Vector2 contact = SliceFacilityAnchors.SpriteContact(body.sprite, seated);
                if (trainPose && catalog.trainSittingContacts != null && catalog.trainSittingContacts.Length > spriteIndex)
                {
                    var b = body.sprite.bounds; Vector2 normalized = catalog.trainSittingContacts[spriteIndex];
                    contact = new Vector2(b.min.x + b.size.x * normalized.x, b.min.y + b.size.y * normalized.y);
                }
                if (body.flipX) contact.x = -contact.x;
                Vector3 contactOffset = body.transform.TransformVector(contact);
                Vector3 aligned = (Vector3)heldPose.contactPoint - contactOffset;
                body.transform.position = Vector3.Lerp(baseline, aligned, weight);
            }
            if (poseBlend > .25f) sortingGroup.sortingOrder = heldPose.poseSortingOrder;
            if (player && package != null)
            {
                var sprites = trainPose ? catalog.trainSittingPackageSprites : catalog.sittingPackageSprites;
                if (seated && poseBlend > .35f && carrier != null && carrier.HasPackage && sprites != null && sprites.Length > spriteIndex)
                { package.sprite = sprites[spriteIndex]; package.enabled = true; }
                package.transform.localPosition = body.transform.localPosition;
                package.transform.localScale = body.transform.localScale;
                package.transform.localRotation = body.transform.localRotation;
                package.flipX = body.flipX;
                package.sortingOrder = back ? -1 : 1;
                if (trainPose && package.enabled && package.sprite != null)
                {
                    // Reuse the matching diagonal package view, but put its centre on the lap.
                    Bounds b = package.sprite.bounds;
                    Vector3 centre = new Vector3(b.center.x, b.min.y + b.size.y * .54f, 0);
                    Vector3 lap = (Vector3)(heldPose.contactPoint + heldPose.facing * .28f + Vector2.up * .18f);
                    package.transform.position = Vector3.Lerp(package.transform.position, lap - package.transform.TransformVector(centre), weight);
                }
            }
            if (poseBlend == 0) ResetFacilityPose();
        }
        void ApplySpriteRect(SpriteRenderer renderer)
        {
            if (renderer.sprite == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            if (!spriteUvRects.TryGetValue(renderer.sprite, out Vector4 uvRect))
            {
                Vector2[] uv = renderer.sprite.uv; Vector2 min = Vector2.one, max = Vector2.zero;
                for (int i = 0; i < uv.Length; i++) { min = Vector2.Min(min, uv[i]); max = Vector2.Max(max, uv[i]); }
                uvRect = new Vector4(min.x, min.y, max.x, max.y); spriteUvRects.Add(renderer.sprite, uvRect);
            }
            renderer.GetPropertyBlock(block); block.SetVector("_UvRect", uvRect); renderer.SetPropertyBlock(block);
        }
    }
}
