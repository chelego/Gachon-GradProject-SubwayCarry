using System.Collections.Generic;
using SubwayCarry.AI.V2;
using UnityEngine;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    // Station modules use the same source floor/escalator art and 2:1 basis as the existing platforms.
    public sealed class SliceStationLayout : MonoBehaviour
    {
        readonly List<Mesh> meshes = new List<Mesh>();
        Material surfaces, floorMaterial;
        SliceStationArt art;
        readonly SliceTransitGeometry stairs = new SliceTransitGeometry();
        public readonly List<Vector2> signs = new List<Vector2>();
        public readonly List<string> signLabels = new List<string>();
        static Vector2 P(float u, float v) => SliceTrainLayout.Project(u, v);
        public static SliceMap Create(Transform parent, SliceMap source, string label, bool gate, int platform)
        {
            var root = new GameObject(label); root.transform.SetParent(parent, false);
            var map = root.AddComponent<SliceMap>(); map.displayName = label;
            var layout = root.AddComponent<SliceStationLayout>();
            layout.art = SliceStationArt.Get(parent);
            layout.surfaces = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            map.placeKind = PassengerAiV2PlaceKind.Concourse; map.depthSlope = .5f;
            map.characterScale = 1.1f; map.characterRadius = .28f; map.cameraSize = 7;
            map.floorBounds = new Rect(-14, -4, 40, 23); map.hasFareGate = gate;
            map.entry = SliceTrainLayout.Project(2, 3); map.fareGate = SliceTrainLayout.Project(5, 3);
            map.streetExit = SliceTrainLayout.Project(0, 3);
            Sprite floor = null, escalator = null, bench = null, pillar = null; Material material = null;
            foreach (var cell in source.floorTiles.cellBounds.allPositionsWithin)
            { floor = source.floorTiles.GetSprite(cell); if (floor != null) break; }
            foreach (var renderer in source.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sprite == null) continue;
                string n = renderer.sprite.name.ToLowerInvariant();
                if (n.Contains("escalator")) { escalator = renderer.sprite; material = renderer.sharedMaterial; }
                if (n.Contains("bench")) bench = renderer.sprite;
                if (n.Contains("pillar")) pillar = renderer.sprite;
            }
            var diamonds = new List<SliceFloorDiamond>();
            for (int u = -2; u <= 20; u += 2) for (int v = -2; v <= 10; v += 2)
            {
                Vector2 p = SliceTrainLayout.Project(u, v);
                diamonds.Add(new SliceFloorDiamond { center = p, halfSize = new Vector2(2.02f, 1.01f) });
            }
            map.floorDiamonds = diamonds.ToArray();
            layout.FlatFloor(floor, diamonds);
            var solids = new List<SliceSolidFootprint>();
            var interests = new List<SliceInterest>();
            // Large unpaid hall -> visible gate barrier -> separate paid circulation and two platform approaches.
            // This is an authored playable concourse, NOT a measured reconstruction of unavailable station drawings.
            for (float u = -2.5f; u < 20.5f; u += 4)
                layout.art.GroundWall(root.transform, 0, P(u, 10.5f), P(Mathf.Min(u + 4, 20.5f), 10.5f), map.GroundOrder(P(u, 10.5f)));
            for (float v = 10.5f; v > -2.5f; v -= 4)
                layout.art.GroundWall(root.transform, 1, P(-2.5f, v), P(-2.5f, Mathf.Max(v - 4, -2.5f)), map.GroundOrder(P(-2.5f, v - 2)));
            foreach (var point in new[] { P(2, 8), P(10, 8), P(18, -1), P(10, -1) })
            {
                if (pillar != null)
                {
                    var column = AddArt(root.transform, "Concourse_Column", pillar, point, 1.05f, material, map.GroundOrder(point));
                    var b = column.bounds;
                    column.transform.position += (Vector3)(point - new Vector2(b.center.x, b.min.y + b.size.x * .25f));
                }
                solids.Add(new SliceSolidFootprint { center = point, halfSize = new Vector2(.49f, .245f), diamond = true });
            }
            // Three vending/card machines, service room and seating kept out of the circulation lane.
            for (int i = 0; i < 2; i++)
            {
                Vector2 p = P(i * 3, 9);
                var machine = layout.art.FloorProp(root.transform, 3, "Ticket and card machines", p, 2.1f, map.GroundOrder(p));
                if (machine != null) solids.Add(SliceStationArt.PropFootprint(machine,3));
            }
            layout.signs.Add(P(1, 9) + Vector2.up * 2); layout.signLabels.Add("승차권 · 교통카드");
            layout.signs.Add(P(1, -2) + Vector2.up); layout.signLabels.Add(platform == 0 ? "출구 · 가천대 비전타워 방면" : "나가는 곳");
            var booth = layout.art.FloorProp(root.transform, 4, "Customer service booth", P(8, 9.1f), 3.2f, map.GroundOrder(P(8, 9.1f)));
            if (booth != null) solids.Add(SliceStationArt.PropFootprint(booth,4));
            // Visible barriers match the u=6 fare boundary outside the usable reader lane.
            if (gate)
            {
                layout.FareBarrier(map, -2.5f, 2, solids);
                layout.FareBarrier(map, 4, 10.5f, solids);
                foreach (float v in new[] { 2f, 4f })
                {
                    Vector2 p = P(6, v);
                    var reader = layout.art.FloorProp(root.transform, 2, "Fare reader pedestal", p, 1.15f, map.GroundOrder(p));
                    if (reader != null) solids.Add(SliceStationArt.PropFootprint(reader,2));
                }
            }
            var portals = new List<SlicePortal>();
            portals.Add(layout.Stairs(map, P(14, 6.4f), false, platform == 0 ? 6 : platform, source.entry, solids,
                platform == 0 ? SliceTravelDirection.Wangsimni : SliceTravelDirection.Unspecified));
            portals.Add(layout.Stairs(map, P(14, .4f), true, platform, source.entry, solids,
                platform == 0 ? SliceTravelDirection.Jeongja : SliceTravelDirection.Unspecified));
            if (bench != null)
            {
                Vector2 p = SliceTrainLayout.Project(2, 6);
                var benchArt = AddArt(root.transform, "Waiting_Bench", bench, p, 3.1f, material, map.GroundOrder(p));
                float slope = .5f;
                if (SliceFacilityAnchors.AlignBenchSeatHeight(benchArt, out _, out Vector2 ground, out float measuredSlope))
                { benchArt.transform.position += (Vector3)(p - ground); slope = measuredSlope; }
                Vector2 facing = new Vector2(slope >= 0 ? 1 : -1, -.5f).normalized;
                solids.Add(new SliceSolidFootprint { center = p, halfSize = new Vector2(benchArt.bounds.size.x * .42f, .23f), slope = slope });
                interests.Add(new SliceInterest { position = p + facing * .85f, kind = PassengerAiV2InteriorSpotKind.Seat, comfort = .8f,
                    hasPoseAnchor = true, contactPoint = p + Vector2.up * .5f, facing = facing, poseSortingOrder = map.GroundOrder(p) + 2 });
            }
            map.walls = new[] {
                new SliceWallBoundary { point = P(-2.5f, 0), inwardNormal = new Vector2(.5f, 1).normalized },
                new SliceWallBoundary { point = P(20.5f, 0), inwardNormal = new Vector2(-.5f, -1).normalized },
                new SliceWallBoundary { point = P(0, -2.5f), inwardNormal = new Vector2(-.5f, 1).normalized },
                new SliceWallBoundary { point = P(0, 10.5f), inwardNormal = new Vector2(.5f, -1).normalized }
            };
            map.portals = portals.ToArray();
            map.exits = new SliceExit[0]; // Public concourse crowd is connected separately, never spawned through a closed fare gate.
            map.interests = interests.ToArray(); map.solidFootprints = solids.ToArray();
            map.BuildFootprintColliders();
            map.EnsureGrid();
            if (gate) root.AddComponent<SliceStationGate>().Initialize(map);
            root.SetActive(false); return map;
        }
        void FareBarrier(SliceMap map, float start, float end, List<SliceSolidFootprint> solids)
        {
            for (float v = start; v < end; v += 1.5f)
            {
                Vector2 a = P(6, v), b = P(6, Mathf.Min(end, v + 1.5f));
                art.Barrier(transform, a, b, map.GroundOrder((a+b)*.5f));
                solids.Add(new SliceSolidFootprint { center = (a+b)*.5f, halfSize = new Vector2(Mathf.Abs(b.x-a.x)*.5f + .045f, .06f), slope = -.5f });
            }
        }
        SlicePortal Stairs(SliceMap map, Vector2 entry, bool right, int target, Vector2 arrival, List<SliceSolidFootprint> solids, SliceTravelDirection direction)
        {
            // One common projection for landing, each tread, riser, wall cap and railing. No flipped painted hole.
            Vector2 run = P(right ? 1 : 0, right ? 0 : 1), across = P(right ? 0 : 1, right ? 1 : 0);
            const int count = 10; const float tread = .32f, rise = .12f, half = 1.05f;
            Vector2 end = entry + run * (count * tread), left = across * half;
            int backOrder = map.GroundOrder(entry) - 8, frontOrder = map.GroundOrder(entry) + 8;
            Color dark = new Color32(31, 38, 40, 255), stone = new Color32(124, 131, 130, 255), edge = new Color32(224, 186, 46, 255);
            stairs.Quad(entry-left, entry+left, end+left, end-left, dark, backOrder);
            // The opening is never walkable from its sides. Traversal alone owns the descending feet.
            for (int i = 0; i <= count; i++)
            {
                Vector2 ground = entry + run * (i * tread);
                solids.Add(new SliceSolidFootprint { center = ground, halfSize = new Vector2(half + .12f, .20f), slope = right ? -.5f : .5f });
            }
            for (int i = count - 1; i >= 0; i--)
            {
                Vector2 near = entry + run * (i * tread) - Vector2.up * (i * rise);
                Vector2 far = entry + run * ((i+1)*tread) - Vector2.up * (i*rise);
                stairs.Quad(near-left, near+left, far+left, far-left, i%2==0 ? stone : new Color32(115, 123, 124, 255), backOrder+1);
                stairs.Quad(far-left, far+left, far+left-Vector2.up*rise, far-left-Vector2.up*rise, new Color32(67, 77, 80, 255), backOrder+1);
                stairs.Rail(far-left, far+left, .035f, edge, backOrder+1);
                stairs.Rail(near-left, far-left, .02f, dark, backOrder+1);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 a = entry + left*side, b = end + left*side, outside = across * (.17f*side);
                int order = (right ? side > 0 : side < 0) ? backOrder+2 : frontOrder;
                stairs.Quad(a, b-Vector2.up*(count*rise), b+Vector2.up*.36f, a+Vector2.up*.36f, stone, order);
                stairs.Quad(a+Vector2.up*.36f, b+Vector2.up*.36f, b+outside+Vector2.up*.36f, a+outside+Vector2.up*.36f, new Color32(191, 193, 184, 255), order+1);
                for (int p = 0; p <= 2; p++)
                {
                    float t = p / 2f; Vector2 basePoint = Vector2.Lerp(a, b, t) - Vector2.up * count*rise*t;
                    stairs.Rail(basePoint, basePoint+Vector2.up*.9f, .045f, new Color32(166, 184, 188, 255), order+2);
                }
                stairs.Rail(a+Vector2.up*.9f, b+Vector2.up*(.9f-count*rise), .07f, new Color32(220, 229, 228, 255), order+2);
            }
            // Top landing is exactly at hall level; only this edge accepts interaction.
            stairs.Quad(entry-run*.45f-left, entry-run*.45f+left, entry+left, entry-left, edge, -23980);
            for (int i = 0; i < 9; i++)
                stairs.Rail(entry-run*.4f+across*(-.95f+i*.23f), entry-run*.08f+across*(-.95f+i*.23f), .025f, new Color32(164, 132, 36, 255), -23979);
            stairs.Flush(transform, "Recessed stairwell structure");
            string label = direction == SliceTravelDirection.Wangsimni ? "왕십리 방면 ↓" : direction == SliceTravelDirection.Jeongja ? "정자 방면 ↓" : "승강장 ↓";
            signs.Add(entry + Vector2.up * 2.2f); signLabels.Add(label);
            return new SlicePortal { label = label, stationConnection = true, guidedTraversal = true, direction = direction,
                position = entry - run.normalized * .75f, traversalEnd = end - Vector2.up * (count*rise),
                traversalSeconds = 2.6f, targetMap = target, arrival = arrival };
        }
        void FlatFloor(Sprite tile, List<SliceFloorDiamond> diamonds)
        {
            if (tile == null) return;
            // Map only the TOP diamond, excluding the sprite's bottom slab/shadow. All tiles share one plane.
            floorMaterial = new Material(surfaces); floorMaterial.mainTexture = tile.texture;
            var vertices = new Vector3[diamonds.Count * 4]; var uv = new Vector2[vertices.Length]; var colors = new Color[vertices.Length]; var tris = new int[diamonds.Count * 6];
            Rect r = tile.rect;
            Vector2[] textureCorners = { new Vector2(.5f, .991f), new Vector2(.992f, .515f), new Vector2(.5f, .058f), new Vector2(.008f, .515f) };
            for (int i = 0; i < diamonds.Count; i++)
            {
                Vector2 p = diamonds[i].center;
                vertices[i * 4] = p + new Vector2(0, 1.005f); vertices[i * 4 + 1] = p + new Vector2(2.01f, 0);
                vertices[i * 4 + 2] = p - new Vector2(0, 1.005f); vertices[i * 4 + 3] = p - new Vector2(2.01f, 0);
                for (int j = 0; j < 4; j++)
                {
                    uv[i * 4 + j] = new Vector2((r.x + r.width * textureCorners[j].x) / tile.texture.width, (r.y + r.height * textureCorners[j].y) / tile.texture.height);
                    colors[i * 4 + j] = Color.white;
                }
                int t = i * 6, q = i * 4; tris[t] = q; tris[t + 1] = q + 1; tris[t + 2] = q + 2; tris[t + 3] = q; tris[t + 4] = q + 2; tris[t + 5] = q + 3;
            }
            var mesh = new Mesh { name = "Continuous concourse top surface" }; mesh.vertices = vertices; mesh.uv = uv; mesh.colors = colors; mesh.triangles = tris; mesh.RecalculateBounds(); meshes.Add(mesh);
            var go = new GameObject(mesh.name); go.transform.SetParent(transform, false); go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = floorMaterial; renderer.sortingOrder = -24000;
        }
        void OnDestroy() { stairs.Dispose(); foreach (var mesh in meshes) Destroy(mesh); if (surfaces != null) Destroy(surfaces); if (floorMaterial != null) Destroy(floorMaterial); }
        public static void ConnectPlatformAccess(SliceMap map, int target, Vector2 arrival)
        {
            var portals = new List<SlicePortal>(map.portals);
            var solids = new List<SliceSolidFootprint>(map.solidFootprints);
            bool found = false;
            foreach (var r in map.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (r.sprite == null || !r.sprite.name.ToLowerInvariant().Contains("escalator")) continue;
                Bounds b = r.bounds;
                Vector2 landing = new Vector2(b.min.x + b.size.x * .32f, b.min.y + b.size.y * .11f);
                Vector2 top = new Vector2(b.min.x + b.size.x * .7f, b.min.y + b.size.y * .72f);
                Vector2 approach = landing - (top - landing).normalized * .65f;
                solids.Add(new SliceSolidFootprint { center = landing + new Vector2(b.size.x * .21f, b.size.x * .105f), halfSize = new Vector2(b.size.x * .34f, .48f), slope = .5f });
                portals.Add(new SlicePortal { label = "대합실로", position = approach, traversalEnd = top,
                    guidedTraversal = true, stationConnection = true, traversalSeconds = 2.4f, targetMap = target, arrival = arrival });
                if (!found) map.entry = approach;
                found = true;
            }
            map.solidFootprints = solids.ToArray(); map.portals = portals.ToArray(); map.RebuildGrid();
            map.BuildFootprintColliders();
            map.entry = map.Grid.Nearest(map.entry);
            // Imported anchors were authored with a padded floor mask. Keep all spawn/approach feet on the
            // final visible top surface after furniture and access structures have been added.
            for (int i = 0; i < map.portals.Length; i++) map.portals[i].position = map.Grid.Nearest(map.portals[i].position);
            for (int i = 0; i < map.exits.Length; i++) map.exits[i].inside = map.Grid.Nearest(map.exits[i].inside);
            for (int i = 0; i < map.interests.Length; i++) map.interests[i].position = map.Grid.Nearest(map.interests[i].position);
        }
        static SpriteRenderer AddArt(Transform root, string label, Sprite sprite, Vector2 p, float width, Material material, int order)
        {
            if (sprite == null) return null;
            var go = new GameObject(label); go.transform.SetParent(root, false);
            float scale = width / sprite.bounds.size.x;
            go.transform.position = (Vector3)p - sprite.bounds.center * scale; go.transform.localScale = Vector3.one * scale;
            var r = go.AddComponent<SpriteRenderer>(); r.sprite = sprite; if (material != null) r.sharedMaterial = material; r.sortingOrder = order; return r;
        }
    }
}
