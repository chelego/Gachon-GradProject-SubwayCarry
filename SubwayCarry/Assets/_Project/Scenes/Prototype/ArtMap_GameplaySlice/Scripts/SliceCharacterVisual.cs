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
        public PassengerAiV2Agent agent;
        public SlicePassengerIntent intent;
        public bool player;
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
        void Awake() { sortingGroup = GetComponent<SortingGroup>(); if (sortingGroup == null) sortingGroup = gameObject.AddComponent<SortingGroup>(); }
        void LateUpdate()
        {
            if (body == null) return;
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
                body.transform.localScale = new Vector3(visualScale, visualScale * (intent != null && intent.Sitting ? 0.76f : 1f), 1);
                body.transform.localRotation = Quaternion.Euler(0, 0, intent != null && intent.Leaning ? 7 : 0);
            }
            if (player && body.sprite != previousSprite)
            {
                previousSprite = body.sprite;
                ApplySpriteRect(body);
            }
            if (player && package != null && package.sprite != previousPackageSprite) { previousPackageSprite = package.sprite; ApplySpriteRect(package); }
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
