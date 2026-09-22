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
            for (int u = -3; u < 21; u += 4)
                layout.art.Draw(root.transform, 0, "Tiled rear wall", P(u, 11) + Vector2.right * 1.65f, 4.25f, -1850);
            for (int v = 11; v > -3; v -= 4)
                layout.art.Draw(root.transform, 1, "Tiled return wall", P(-3, v - 4) - Vector2.right * 1.65f, 4.25f, -1840);
            foreach (var point in new[] { P(2, 8), P(10, 8), P(18, -1), P(10, -1) })
            {
                if (pillar != null) AddArt(root.transform, "Concourse_Column", pillar, point + Vector2.up * 1.1f, 1.25f, material, map.GroundOrder(point));
                solids.Add(new SliceSolidFootprint { center = point, halfSize = new Vector2(.48f, .32f), slope = .5f });
            }
            // Three vending/card machines, service room and seating kept out of the circulation lane.
            for (int i = 0; i < 2; i++)
            {
                Vector2 p = P(i * 3, 9);
                layout.art.Draw(root.transform, 3, "Ticket and card machines", p, 2.1f, map.GroundOrder(p));
                solids.Add(new SliceSolidFootprint { center = p, halfSize = new Vector2(.95f, .4f), slope = .5f });
            }
            layout.signs.Add(P(1, 9) + Vector2.up * 2); layout.signLabels.Add("승차권 · 교통카드");
            layout.signs.Add(P(1, -2) + Vector2.up); layout.signLabels.Add(platform == 0 ? "출구 · 가천대 비전타워 방면" : "나가는 곳");
            layout.signs.Add(P(9, 9) + Vector2.up * 3.5f); layout.signLabels.Add("수인분당선 · 타는 곳 →");
            layout.art.Draw(root.transform, 4, "Customer service booth", P(8, 9.3f), 3.2f, map.GroundOrder(P(8, 9.3f)));
            solids.Add(new SliceSolidFootprint { center = P(8, 9.7f), halfSize = new Vector2(1.3f, .65f), slope = .5f });
            // Visible barriers match the u=6 fare boundary outside the usable reader lane.
            if (gate)
            {
                for (float v = -3; v < 1; v += 1.5f)
                    layout.art.Draw(root.transform, 7, "Glass fare barrier", P(6, v) - new Vector2(.75f, 0), 1.65f, map.GroundOrder(P(6, v)), true);
                for (float v = 4.5f; v < 11; v += 1.5f)
                    layout.art.Draw(root.transform, 7, "Glass fare barrier", P(6, v) - new Vector2(.75f, 0), 1.65f, map.GroundOrder(P(6, v)), true);
            }
            var portals = new List<SlicePortal>();
            portals.Add(layout.Stairs(map, 14, 2, 1, platform, source.entry, solids));
            portals.Add(layout.Stairs(map, 18, 7, -1, platform, source.entry, solids));
            if (bench != null)
            {
                Vector2 p = SliceTrainLayout.Project(2, 6);
                AddArt(root.transform, "Waiting_Bench", bench, p + Vector2.up * .5f, 2.4f, material, map.GroundOrder(p));
                solids.Add(new SliceSolidFootprint { center = p, halfSize = new Vector2(1, .3f), slope = .5f });
                interests.Add(new SliceInterest { position = p + new Vector2(.7f, -.6f), kind = PassengerAiV2InteriorSpotKind.Seat, comfort = .8f });
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
            map.EnsureGrid();
            for (int i = 0; i < map.portals.Length; i++) map.portals[i].position = map.Grid.Nearest(map.portals[i].position);
            if (gate) root.AddComponent<SliceStationGate>().Initialize(map);
            root.SetActive(false); return map;
        }
        SlicePortal Stairs(SliceMap map, float u, float v, float direction, int target, Vector2 arrival, List<SliceSolidFootprint> solids)
        {
            Vector2 start = P(u, v), finish = P(u + direction * 3.4f, v);
            var stair = art.Draw(transform, direction > 0 ? 6 : 5, "Recessed platform stairwell", (start + finish) * .5f - Vector2.up * 1.4f,
                5.3f, map.GroundOrder(start) - 20, direction < 0);
            if (stair != null)
            {
                var b = stair.bounds;
                start = new Vector2(b.min.x + b.size.x * (direction > 0 ? .24f : .76f), b.min.y + b.size.y * .24f);
                finish = new Vector2(b.min.x + b.size.x * (direction > 0 ? .74f : .26f), b.min.y + b.size.y * .68f);
            }
            Vector2 floorCenter = (start + finish) * .5f;
            // Solid ground footprint: only the guided entry can enter the stairwell, never a side wall.
            solids.Add(new SliceSolidFootprint { center = floorCenter, halfSize = new Vector2(2.25f, .9f), slope = direction * .5f });
            return new SlicePortal { label = "승강장으로", stationConnection = true, guidedTraversal = true,
                position = start - new Vector2(direction, .5f) * 1.4f, traversalEnd = finish, traversalSeconds = 2.6f, targetMap = target, arrival = arrival };
        }
        void Panel(string name, Vector2 a, Vector2 b, float height, Color color, int order)
            => Quad(name, a, b, b + Vector2.up * height, a + Vector2.up * height, color, order);
        void Quad(string name, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color, int order)
        {
            var mesh = new Mesh { name = name }; meshes.Add(mesh);
            mesh.vertices = new[] { (Vector3)a, (Vector3)b, (Vector3)c, (Vector3)d };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 }; mesh.colors = new[] { color, color, color, color };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up }; mesh.RecalculateBounds();
            var go = new GameObject(name); go.transform.SetParent(transform, false); go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = surfaces; renderer.sortingOrder = order;
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
        void OnDestroy() { foreach (var mesh in meshes) Destroy(mesh); if (surfaces != null) Destroy(surfaces); if (floorMaterial != null) Destroy(floorMaterial); }
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
            if (!found) return;
            map.solidFootprints = solids.ToArray(); map.portals = portals.ToArray(); map.RebuildGrid();
            map.entry = map.Grid.Nearest(map.entry);
            for (int i = 0; i < map.portals.Length; i++) if (map.portals[i].stationConnection) map.portals[i].position = map.Grid.Nearest(map.portals[i].position);
        }
        static void AddArt(Transform root, string label, Sprite sprite, Vector2 p, float width, Material material, int order)
        {
            if (sprite == null) return;
            var go = new GameObject(label); go.transform.SetParent(root, false);
            float scale = width / sprite.bounds.size.x;
            go.transform.position = (Vector3)p - sprite.bounds.center * scale; go.transform.localScale = Vector3.one * scale;
            var r = go.AddComponent<SpriteRenderer>(); r.sprite = sprite; if (material != null) r.sharedMaterial = material; r.sortingOrder = order;
        }
    }
}
