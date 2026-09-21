using System.Collections.Generic;
using SubwayCarry.AI.V2;
using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    // Two straight cars share one 2:1 projection. Original imported children remain untouched/inactive.
    public sealed class SliceTrainLayout : MonoBehaviour
    {
        readonly List<Mesh> meshes = new List<Mesh>();
        Material surfaceMaterial;
        SliceMap map;
        Transform root;
        readonly List<SliceInterest> interests = new List<SliceInterest>();
        readonly List<SliceSolidFootprint> solids = new List<SliceSolidFootprint>();
        readonly List<SlicePortal> portals = new List<SlicePortal>();
        readonly List<SliceExit> exits = new List<SliceExit>();
        public static Vector2 Project(float along, float across) => new Vector2(along - across, (along + across) * .5f);
        public void Build(SliceMap train, SliceMap station)
        {
            map = train;
            Sprite seat = null, window = null, frame = null, left = null, right = null;
            Material artMaterial = null;
            foreach (var r in train.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (r.sprite == null) continue;
                string n = r.sprite.name.ToLowerInvariant();
                if (seat == null && n.Contains("seat")) seat = r.sprite;
                if (window == null && n.Contains("window")) window = r.sprite;
                if (n.Contains("door_fixed")) frame = r.sprite;
                if (n.Contains("door_leaf_left")) left = r.sprite;
                if (n.Contains("door_leaf_right")) right = r.sprite;
                if (artMaterial == null) artMaterial = r.sharedMaterial;
            }
            Sprite floor = null;
            if (station.floorTiles != null)
                foreach (var cell in station.floorTiles.cellBounds.allPositionsWithin)
                { floor = station.floorTiles.GetSprite(cell); if (floor != null) break; }
            if (floor == null || seat == null || frame == null || left == null || right == null)
            { Debug.LogError("Train layout requires the imported floor, seats and door sprites.", train); return; }
            for (int i = 0; i < train.transform.childCount; i++) train.transform.GetChild(i).gameObject.SetActive(false);
            train.transform.localScale = Vector3.one;
            root = new GameObject("Straight_TwoCar_Isometric_Layout").transform;
            root.SetParent(train.transform, false);
            surfaceMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            var diamonds = new List<SliceFloorDiamond>();
            for (int u = 0; u <= 26; u += 2) for (int v = 0; v <= 6; v += 2)
            {
                Vector2 p = Project(u, v);
                var r = Art("Floor", floor, p, 4.02f, artMaterial, -24000);
                r.color = new Color(.83f, .9f, .93f);
                diamonds.Add(new SliceFloorDiamond { center = p, halfSize = new Vector2(2.02f, 1.01f) });
            }
            map.depthSlope = .5f; map.characterScale = 1.1f; map.characterRadius = .28f; map.cameraSize = 6.8f;
            map.floorBounds = new Rect(-8, -2, 38, 21);
            map.floorDiamonds = diamonds.ToArray(); map.obstacles = new Rect[0];
            map.floorTiles = null;
            map.walls = new[] {
                new SliceWallBoundary { point = Project(-.5f, 0), inwardNormal = new Vector2(.5f, 1).normalized },
                new SliceWallBoundary { point = Project(26.5f, 0), inwardNormal = new Vector2(-.5f, -1).normalized },
                new SliceWallBoundary { point = Project(0, -.5f), inwardNormal = new Vector2(-.5f, 1).normalized },
                new SliceWallBoundary { point = Project(0, 6.5f), inwardNormal = new Vector2(.5f, -1).normalized }
            };
            // The central gangway is continuous; seats never occupy this passage.
            for (int u = 0; u <= 26; u += 2)
            {
                bool doorBay = u == 6 || u == 20;
                Vector2 rear = Project(u, 6.5f);
                if (!doorBay)
                {
                    Quad("Rear wall", rear + new Vector2(-1, -.5f), rear + new Vector2(1, .5f), 2.7f, new Color32(159, 178, 185, 255), map.GroundOrder(rear));
                    if (window != null) Art("Window", window, rear + Vector2.up * 1.35f, 1.7f, artMaterial, map.GroundOrder(rear) + 1);
                }
                if (u == 12 || u == 14 || doorBay) continue;
                foreach (int v in new[] { 0, 6 })
                {
                    Vector2 feet = Project(u, v);
                    Art("Seat", seat, feet + Vector2.up * .45f, 1.55f, artMaterial, map.GroundOrder(feet));
                    Vector2 use = Project(u, v == 0 ? 1.25f : 4.75f);
                    interests.Add(new SliceInterest { position = use, kind = PassengerAiV2InteriorSpotKind.Seat, comfort = .95f });
                    solids.Add(new SliceSolidFootprint { center = feet, halfSize = new Vector2(.62f, .28f), slope = .5f });
                    interests.Add(new SliceInterest { position = Project(u, v == 0 ? 2.1f : 3.9f), kind = PassengerAiV2InteriorSpotKind.Stand, comfort = .55f });
                }
            }
            foreach (int u in new[] { 6, 20 }) foreach (int v in new[] { 0, 6 })
            {
                Vector2 point = Project(u, v); int order = map.GroundOrder(point);
                // Near-side doors use a cutaway so the player remains legible. Their sill still marks the boundary.
                if (v == 6)
                {
                    Art("DoorFrame_ClosedSide", frame, point + Vector2.up, 2.8f, artMaterial, order);
                    Art("DoorLeaf_ClosedSide", left, point + new Vector2(-.35f, .8f), 1.1f, artMaterial, order + 1);
                    Art("DoorLeaf_ClosedSide", right, point + new Vector2(.35f, 1.15f), 1.1f, artMaterial, order + 1);
                }
                else
                {
                    Art("DoorLeaf_ServiceSide", left, point + new Vector2(-.4f, -.2f), 1.05f, artMaterial, order);
                    Art("DoorLeaf_ServiceSide", right, point + new Vector2(.4f, .2f), 1.05f, artMaterial, order);
                }
                Vector2 inside = Project(u, v == 0 ? 1 : 5);
                portals.Add(new SlicePortal { label = "Train door", position = inside, targetMap = 2, side = v == 0 ? DoorOpeningSide.Right : DoorOpeningSide.Left });
                if (v == 0) exits.Add(new SliceExit { inside = inside, outside = Project(u, -3) });
                interests.Add(new SliceInterest { position = Project(u + 1.5f, v == 0 ? 1 : 5), kind = PassengerAiV2InteriorSpotKind.Lean, comfort = .7f });
            }
            // Low front sill, and a contrasting connector across both cars; no V-shaped end walls.
            Quad("Front sill", Project(-.5f, -.5f), Project(26.5f, -.5f), .16f, new Color32(80, 104, 116, 255), 20000);
            Quad("Car connector", Project(13, 0), Project(13, 6), .12f, new Color32(60, 75, 82, 255), -23000);
            map.portals = portals.ToArray(); map.exits = exits.ToArray(); map.interests = interests.ToArray();
            map.solidFootprints = solids.ToArray(); map.entry = Project(6, 2);
            foreach (var solid in solids)
            {
                var go = new GameObject("Seat_Ground_Collider"); go.transform.SetParent(root, false); go.transform.position = solid.center;
                var collider = go.AddComponent<PolygonCollider2D>(); float x = solid.halfSize.x, y = solid.halfSize.y;
                collider.points = new[] { new Vector2(-x, -y - x * .5f), new Vector2(x, -y + x * .5f), new Vector2(x, y + x * .5f), new Vector2(-x, y - x * .5f) };
            }
            map.noWaitingZones = new Rect[0]; map.RebuildGrid();
            for (int i = 0; i < map.interests.Length; i++) map.interests[i].position = map.Grid.Nearest(map.interests[i].position);
            for (int i = 0; i < map.portals.Length; i++) map.portals[i].position = map.Grid.Nearest(map.portals[i].position);
            for (int i = 0; i < map.exits.Length; i++) map.exits[i].inside = map.Grid.Nearest(map.exits[i].inside);
        }
        SpriteRenderer Art(string name, Sprite sprite, Vector2 position, float width, Material material, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(root, false); go.transform.position = position;
            var r = go.AddComponent<SpriteRenderer>(); r.sprite = sprite; r.sharedMaterial = material; r.sortingOrder = order;
            float scale = width / Mathf.Max(.01f, sprite.bounds.size.x);
            go.transform.localScale = Vector3.one * scale;
            go.transform.position = (Vector3)position - sprite.bounds.center * scale;
            return r;
        }
        void Quad(string name, Vector2 a, Vector2 b, float height, Color color, int order)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = new[] { (Vector3)a, (Vector3)b, (Vector3)(b + Vector2.up * height), (Vector3)(a + Vector2.up * height) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 }; mesh.colors = new[] { color, color, color, color };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up }; mesh.RecalculateBounds(); meshes.Add(mesh);
            var go = new GameObject(name); go.transform.SetParent(root, false); go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = surfaceMaterial; r.sortingOrder = order;
        }
        void OnDestroy() { foreach (var mesh in meshes) if (mesh != null) Destroy(mesh); if (surfaceMaterial != null) Destroy(surfaceMaterial); }
    }
}
