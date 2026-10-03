using SubwayCarry.AI.V2;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    // Bound once when a map is prepared, never searched per actor/per frame.
    public static class SliceFacilityAnchors
    {
        // Replaces only the integration builder's named collision cache, not teammate/manual colliders.
        public static void RepairImportedFurniture(SliceMap map)
        {
            AlignFloorSurface(map);
            var oldRoot = map.transform.Find("SolidFootprints");
            if (oldRoot == null) return;
            oldRoot.gameObject.SetActive(false);
            var root = new GameObject("AlignedFurnitureFootprints"); root.transform.SetParent(map.transform, false);
            var solids = new List<SliceSolidFootprint>();
            var spots = new List<SliceInterest>();
            foreach (var s in map.interests) if (s.kind != PassengerAiV2InteriorSpotKind.Seat && s.kind != PassengerAiV2InteriorSpotKind.Lean) spots.Add(s);
            foreach (var r in map.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (r.sprite == null || !r.gameObject.activeSelf) continue;
                string name = r.sprite.name.ToLowerInvariant();
                bool seat = name.Contains("bench"), pillar = name.Contains("pillar"), machine = name.Contains("vending");
                if (!seat && !pillar && !machine) continue;
                Vector2 ground, contact = default, facing = new Vector2(1, -.5f).normalized;
                float slope = map.depthSlope;
                if (seat && AlignBenchSeatHeight(r, out contact, out ground, out slope))
                {
                    facing = new Vector2(slope >= 0 ? 1 : -1, -.5f).normalized;
                }
                else
                {
                    var bounds = r.bounds;
                    ground = new Vector2(bounds.center.x, bounds.min.y + bounds.size.x * (pillar ? .25f : .25f * Mathf.Abs(slope)));
                    contact = ground + Vector2.up * .5f;
                }
                Bounds box = r.bounds;
                var solid = new SliceSolidFootprint { center = ground, slope = pillar ? 0 : slope,
                    halfSize = pillar ? new Vector2(box.size.x * .47f, box.size.x * .235f) : new Vector2(box.size.x * .42f, seat ? .22f : .35f), diamond = pillar };
                solids.Add(solid);
                var collider = new GameObject("Footprint_" + r.name); collider.transform.SetParent(root.transform, false);
                var poly = collider.AddComponent<PolygonCollider2D>();
                Vector2 h = solid.halfSize;
                var points = pillar ? new[] { new Vector2(-h.x, 0), new Vector2(0, h.y), new Vector2(h.x, 0), new Vector2(0, -h.y) }
                    : new[] { new Vector2(-h.x, -h.y-h.x*slope), new Vector2(h.x, -h.y+h.x*slope), new Vector2(h.x, h.y+h.x*slope), new Vector2(-h.x, h.y-h.x*slope) };
                for (int i = 0; i < points.Length; i++) points[i] = collider.transform.InverseTransformPoint(ground + points[i]);
                poly.points = points;
                var group = r.GetComponentInParent<SortingGroup>();
                int order = map.GroundOrder(ground);
                if (group != null) group.sortingOrder = order; else r.sortingOrder = order;
                if (seat)
                {
                    int count = Mathf.Max(1, Mathf.FloorToInt(box.size.x / .85f));
                    for (int i = 0; i < count; i++)
                    {
                        float x = count == 1 ? 0 : Mathf.Lerp(-box.size.x * .3f, box.size.x * .3f, i / (float)(count - 1));
                        Vector2 anchor = contact + new Vector2(x, x * slope);
                        spots.Add(new SliceInterest { kind = PassengerAiV2InteriorSpotKind.Seat, comfort = .8f, hasPoseAnchor = true,
                            contactPoint = anchor, position = anchor - Vector2.up * .5f + facing * .85f, facing = facing, poseSortingOrder = order + 2 });
                    }
                }
                if (pillar) spots.Add(new SliceInterest { kind = PassengerAiV2InteriorSpotKind.Lean, comfort = .7f, hasPoseAnchor = true,
                    position = ground + facing * (box.size.x * .45f + .3f), contactPoint = ground + facing * box.size.x * .4f + Vector2.up * .95f,
                    facing = facing, poseSortingOrder = order + 2 });
            }
            map.solidFootprints = solids.ToArray(); map.interests = spots.ToArray(); map.RebuildGrid();
        }
        static void AlignFloorSurface(SliceMap map)
        {
            if (map.floorTiles == null) return;
            var grid = map.floorTiles.GetComponentInParent<Grid>(); if (grid == null) return;
            Vector3 cellAxis = grid.CellToLocalInterpolated(Vector3.right) - grid.CellToLocalInterpolated(Vector3.zero);
            float cellStep = Mathf.Abs(map.floorTiles.transform.TransformVector(cellAxis).x);
            var floor = new List<SliceFloorDiamond>();
            foreach (var cell in map.floorTiles.cellBounds.allPositionsWithin)
            {
                var sprite = map.floorTiles.GetSprite(cell); if (sprite == null) continue;
                Vector3 local = grid.CellToLocalInterpolated((Vector3)cell + map.floorTiles.tileAnchor);
                Bounds b = sprite.bounds;
                // The visible TOP plane excludes the tile's downward slab/shadow and the old 0.25-cell expansion.
                local += new Vector3(b.center.x,b.min.y+b.size.y*.515f,0);
                Vector2 center = map.floorTiles.transform.TransformPoint(local);
                float half = b.extents.x * Mathf.Abs(map.floorTiles.transform.lossyScale.x) * .984f;
                half = SlicePresentationRules.FloorHalfWidth(half, cellStep);
                floor.Add(new SliceFloorDiamond {center=center,halfSize=new Vector2(half,half*.5f)});
            }
            if (floor.Count > 0) map.floorDiamonds = floor.ToArray();
        }
        public static bool AlignBenchSeatHeight(SpriteRenderer renderer, out Vector2 contact, out Vector2 ground, out float slope)
        {
            contact = ground = default; slope = .5f;
            string name = renderer.sprite.name.ToLowerInvariant();
            Vector2 a, b, foot;
            if (name.Contains("bench_gachon")) { a = new Vector2(115, 327); b = new Vector2(760, 713); foot = new Vector2(437.5f, 661); }
            else if (name.Contains("gachonuniv_bench")) { a = new Vector2(37, 134); b = new Vector2(249, 29); foot = new Vector2(143, 116.5f); }
            else return false;
            // Pixel landmarks are the seat edge and its floor contact. The two source benches face opposite ways.
            Vector2 oldGround = Pixel(renderer, foot);
            float height = ((Pixel(renderer, a) + Pixel(renderer, b)) * .5f - oldGround).magnitude;
            renderer.transform.localScale *= .5f / Mathf.Max(.1f, height);
            renderer.transform.position += (Vector3)(oldGround - Pixel(renderer, foot));
            Vector2 left = Pixel(renderer, a), right = Pixel(renderer, b);
            contact = (left + right) * .5f; ground = contact - Vector2.up * .5f;
            slope = (right.y - left.y) / (right.x - left.x);
            return true;
        }
        static Vector2 Pixel(SpriteRenderer renderer, Vector2 pixel)
        {
            var s = renderer.sprite;
            Vector2 p = new Vector2(pixel.x - s.rect.x - s.pivot.x, s.texture.height - pixel.y - s.rect.y - s.pivot.y) / s.pixelsPerUnit;
            if (renderer.flipX) p.x = -p.x;
            if (renderer.flipY) p.y = -p.y;
            return renderer.transform.TransformPoint(p);
        }
        public static void BindImported(SliceMap map)
        {
            var art = map.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < map.interests.Length; i++)
            {
                var spot = map.interests[i];
                if (spot.hasPoseAnchor || (spot.kind != PassengerAiV2InteriorSpotKind.Seat && spot.kind != PassengerAiV2InteriorSpotKind.Lean)) continue;
                SpriteRenderer best = null; float distance = 16;
                foreach (var r in art)
                {
                    if (r.sprite == null || !r.gameObject.activeSelf) continue;
                    string name = r.sprite.name.ToLowerInvariant();
                    bool matches = spot.kind == PassengerAiV2InteriorSpotKind.Seat
                        ? name.Contains("bench") || name.Contains("seat") : name.Contains("pillar");
                    if (!matches) continue;
                    float d = ((Vector2)r.bounds.center - spot.position).sqrMagnitude;
                    if (d < distance) { best = r; distance = d; }
                }
                // A vending machine/empty floor is not a leaning support.
                if (best == null) continue;
                var b = best.bounds;
                spot.contactPoint = new Vector2(b.center.x, b.min.y + b.size.y * (spot.kind == PassengerAiV2InteriorSpotKind.Seat ? .4f : .45f));
                spot.facing = new Vector2(1, -.5f).normalized;
                var group = best.GetComponentInParent<SortingGroup>();
                spot.poseSortingOrder = (group != null ? group.sortingOrder : best.sortingOrder) + 1;
                spot.hasPoseAnchor = true;
                map.interests[i] = spot;
            }
        }

        // Source sitting sheets use a feet pivot at 14% and a pelvis contact at 35%.
        public static Vector2 SpriteContact(Sprite sprite, bool sitting)
        {
            Bounds b = sprite.bounds;
            return new Vector2(b.center.x, b.min.y + b.size.y * (sitting ? .35f : .53f));
        }
    }
}
