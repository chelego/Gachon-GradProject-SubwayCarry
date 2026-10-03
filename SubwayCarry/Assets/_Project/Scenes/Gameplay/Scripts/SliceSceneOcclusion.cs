using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    // Cache only map art on entry. No per-passenger searches, raycasts, material instances or frame allocations.
    [DefaultExecutionOrder(650)]
    public sealed class SliceSceneOcclusion : MonoBehaviour
    {
        sealed class Surface
        {
            public Renderer renderer;
            public SpriteRenderer sprite;
            public Color color;
            public Mesh mesh;
            public Color[] original, faded;
            public float alpha = 1;
            public int order;
        }
        sealed class InteractionArt
        {
            public SpriteRenderer renderer;
            public SpriteRenderer[] assembly;
        }
        readonly List<Surface> surfaces = new List<Surface>();
        readonly List<InteractionArt> interactionArt = new List<InteractionArt>();
        SliceGameController world;
        SliceCharacterVisual actor;
        SortingGroup actorGroup;
        float nextCheck;
        public void Initialize(SliceGameController source) { world = source; actor = world.Player.GetComponent<SliceCharacterVisual>(); actorGroup = actor.GetComponent<SortingGroup>(); Rebind(); }
        public void Rebind()
        {
            Restore(); surfaces.Clear(); interactionArt.Clear();
            if (world == null) return;
            foreach (var renderer in world.CurrentMap.GetComponentsInChildren<Renderer>(false))
            {
                if (renderer.GetComponentInParent<SliceCharacterVisual>() != null) continue;
                string name = renderer.name.ToLowerInvariant();
                var sprite = renderer as SpriteRenderer;
                if (sprite != null && sprite.sprite != null) name += " " + sprite.sprite.name.ToLowerInvariant();
                if (sprite != null && (name.Contains("bench") || name.Contains("seat") || name.Contains("pillar") || name.Contains("column") || name.Contains("stairwell") || name.Contains("escalator")))
                    interactionArt.Add(new InteractionArt { renderer = sprite });
                if (sprite != null && name.Contains("door_fixed") && !renderer.name.StartsWith("TrainDoorFrame"))
                    interactionArt.Add(new InteractionArt { renderer = sprite, assembly = sprite.transform.parent.name.StartsWith("ClosedDoor_") || sprite.transform.parent.name.StartsWith("CarriageDoor_")
                        ? sprite.transform.parent.GetComponentsInChildren<SpriteRenderer>(false) : null });
                bool wallMesh = renderer is MeshRenderer && name.StartsWith("occluding carriage");
                if (!wallMesh && (sprite == null || !(name.Contains("wall") || name.Contains("pillar") || name.Contains("column") ||
                    name.Contains("bench") || name.Contains("door") || name.Contains("window") || name.Contains("booth")))) continue;
                var s = new Surface { renderer = renderer, sprite = sprite, order = renderer.sortingOrder };
                var group = renderer.GetComponentInParent<SortingGroup>();
                if (group != null) s.order = group.sortingOrder;
                if (sprite != null) s.color = sprite.color;
                else
                {
                    s.mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    s.original = s.mesh.colors; s.faded = (Color[])s.original.Clone();
                }
                surfaces.Add(s);
            }
            nextCheck = 0;
        }
        public Bounds InteractionBounds(Vector2 point)
        {
            Bounds result = new Bounds(point + Vector2.up * .4f, new Vector3(.8f, .8f, 0));
            float nearest = .8f;
            foreach (var target in interactionArt)
            {
                var r = target.renderer;
                if (r == null || !r.enabled) continue;
                var b = r.bounds;
                if (target.assembly != null) foreach (var part in target.assembly) if (part != null && part.enabled) b.Encapsulate(part.bounds);
                float distance = b.SqrDistance(point);
                if (distance < nearest) { nearest = distance; result = b; }
            }
            return result;
        }
        void LateUpdate()
        {
            if (actor == null || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + .05f;
            Bounds body = actor.VisibleBounds; body.Expand(new Vector3(.16f, .12f, 0));
            int order = actorGroup.sortingOrder;
            foreach (var s in surfaces)
            {
                if (s.renderer == null) continue;
                Bounds b = s.renderer.bounds;
                bool overlaps = b.min.x < body.max.x && b.max.x > body.min.x && b.min.y < body.max.y && b.max.y > body.min.y;
                float target = s.order >= order && overlaps ? .24f : 1;
                float alpha = Mathf.MoveTowards(s.alpha, target, .2f);
                if (Mathf.Approximately(alpha, s.alpha)) continue;
                s.alpha = alpha; Apply(s);
            }
        }
        static void Apply(Surface s)
        {
            if (s.sprite != null) { Color c = s.color; c.a *= s.alpha; s.sprite.color = c; }
            else if (s.mesh != null)
            {
                for (int i = 0; i < s.faded.Length; i++) { s.faded[i] = s.original[i]; s.faded[i].a *= s.alpha; }
                s.mesh.colors = s.faded;
            }
        }
        void Restore() { foreach (var s in surfaces) { s.alpha = 1; Apply(s); } }
        void OnDestroy() { Restore(); }
    }
}
