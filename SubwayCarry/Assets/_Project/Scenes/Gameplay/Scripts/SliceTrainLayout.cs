using System.Collections.Generic;
using SubwayCarry.AI.V2;
using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    // Gameplay-only dressing; imported team art and source scenes remain untouched.
    public sealed class SliceTrainLayout : MonoBehaviour
    {
        sealed class Geometry
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<int> triangles = new List<int>();
            public readonly List<Color> colors = new List<Color>();
        }
        struct DoorPart { public Sprite sprite; public Vector2 offset; public float width; public string name; }
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Sprite> createdSprites = new List<Sprite>();
        readonly Dictionary<(int order, bool wall), Geometry> surfaces = new Dictionary<(int, bool), Geometry>();
        bool wallSurface;
        readonly List<SliceInterest> interests = new List<SliceInterest>();
        readonly List<SliceSolidFootprint> solids = new List<SliceSolidFootprint>();
        readonly List<SlicePortal> portals = new List<SlicePortal>();
        readonly List<SliceExit> exits = new List<SliceExit>();
        Material surfaceMaterial;
        SliceMap map;
        Transform root;
        public static Vector2 Project(float along, float across) => new Vector2(along - across, (along + across) * .5f);

        public void Build(SliceMap train, SliceMap station)
        {
            var catalog = GetComponent<SliceGameController>().deliveryCatalog;
            var doorArt = ReadDoorAssembly(train);
            if (catalog.trainBenchAtlas == null || doorArt.Count != 3)
            { Debug.LogError("Carriage requires the front/back bench atlas and the imported three-part train door.", train); return; }
            map = train;
            for (int i = 0; i < train.transform.childCount; i++) train.transform.GetChild(i).gameObject.SetActive(false);
            train.transform.localScale = Vector3.one;
            root = new GameObject("Straight_TwoCar_Isometric_Layout").transform;
            root.SetParent(train.transform, false);
            surfaceMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            map.depthSlope = .5f; map.characterScale = 1.1f; map.characterRadius = .28f; map.cameraSize = 6.8f;
            map.floorBounds = new Rect(-8, -2, 38, 21);
            var diamonds = new List<SliceFloorDiamond>();
            for (int u = 0; u <= 26; u += 2) for (int v = 0; v <= 6; v += 2)
                diamonds.Add(new SliceFloorDiamond { center = Project(u, v), halfSize = new Vector2(2.02f, 1.01f) });
            map.floorDiamonds = diamonds.ToArray(); map.obstacles = new Rect[0]; map.floorTiles = null;
            map.walls = new[] {
                new SliceWallBoundary { point = Project(-.5f, 0), inwardNormal = new Vector2(.5f, 1).normalized },
                new SliceWallBoundary { point = Project(26.5f, 0), inwardNormal = new Vector2(-.5f, -1).normalized },
                new SliceWallBoundary { point = Project(0, -.5f), inwardNormal = new Vector2(-.5f, 1).normalized },
                new SliceWallBoundary { point = Project(0, 6.5f), inwardNormal = new Vector2(.5f, -1).normalized }
            };
            // Vinyl floor rather than the platform's stone tiles.
            Floor(-.5f, 26.5f, -.5f, 6.5f, new Color32(157, 173, 181, 255), -24000);
            Floor(-.5f, 26.5f, -.5f, .9f, new Color32(121, 140, 151, 255), -23999);
            Floor(-.5f, 26.5f, 5.1f, 6.5f, new Color32(121, 140, 151, 255), -23999);
            Floor(12.7f, 13.3f, -.5f, 6.5f, new Color32(54, 66, 73, 255), -23998);
            for (float u = 12.76f; u < 13.3f; u += .12f)
                Floor(u, u + .035f, -.5f, 6.5f, new Color32(125, 142, 150, 255), -23997);

            wallSurface = true;
            foreach (int side in new[] { 0, 6 })
            {
                SideWall(-.5f, 4.55f, side);
                SideWall(7.45f, 18.55f, side);
                SideWall(21.45f, 26.5f, side);
                foreach (float center in new[] { 2f, 10f, 16f, 24f }) Window(center, side);
            }
            EndWall(-.5f); EndWall(26.5f);
            wallSurface = false;
            // Gangway jambs/bellows and header leave the centre traversable.
            foreach (float v in new[] { -.35f, 0f, 6f, 6.35f })
                Panel(Project(13, v), Project(13, v + .12f), 3.15f, new Color32(49, 60, 67, 255), map.GroundOrder(Project(13, v)));
            Panel(Project(13, 0) + Vector2.up * 3.05f, Project(13, 6) + Vector2.up * 3.05f, .18f, new Color32(92, 111, 120, 255), 20);

            // Distinct front/rear views, never a mirrored seat front.
            Sprite front = AtlasPiece(catalog.trainBenchAtlas, new Rect(170, 803, 715, 577));
            Sprite back = AtlasPiece(catalog.trainBenchAtlas, new Rect(164, 123, 715, 538));
            foreach (float center in new[] { 2f, 10f, 16f, 24f })
            {
                Bench(front, center, 5.65f, false);
                Bench(back, center, .35f, true);
            }
            foreach (int u in new[] { 6, 20 }) foreach (int side in new[] { 0, 6 })
            {
                Vector2 point = Project(u, side == 0 ? -.5f : 6.5f);
                int order = map.GroundOrder(point);
                var doorRoot = new GameObject("CarriageDoor_" + u + "_" + side).transform; doorRoot.SetParent(root, false);
                foreach (var part in doorArt)
                    Art(part.name + (side == 0 ? "_ServiceSide" : "_ClosedSide"), part.sprite,
                        point + Vector2.up * 1.45f + part.offset * 2.8f, part.width * 2.8f, order + (part.name.Contains("Frame") ? 2 : 1)).transform.SetParent(doorRoot, true);
                Floor(u - 1.4f, u + 1.4f, side == 0 ? -.5f : 5.8f, side == 0 ? .2f : 6.5f, new Color32(95, 110, 119, 255), -23990);
                Vector2 inside = Project(u, side == 0 ? 1 : 5);
                portals.Add(new SlicePortal { label = "Train door", position = inside, targetMap = 2, side = side == 0 ? DoorOpeningSide.Right : DoorOpeningSide.Left });
                if (side == 0) exits.Add(new SliceExit { inside = inside, outside = Project(u, -3) });
                foreach (float offset in new[] { -1.55f, 1.55f }) Support(u + offset, side == 0 ? 1.05f : 4.95f);
                if (side == 6) interests.Add(new SliceInterest {
                    position = Project(u + 1.75f, 5.2f), kind = PassengerAiV2InteriorSpotKind.Lean, comfort = .7f,
                    hasPoseAnchor = true, contactPoint = Project(u + 1.75f, 6.35f) + Vector2.up * .95f,
                    facing = Project(0, -1).normalized, poseSortingOrder = order + 4 });
            }
            foreach (float v in new[] { 1.2f, 4.8f })
            {
                int order = map.GroundOrder(Project(0, v)) + 5;
                foreach (var span in new[] { new Vector2(-.1f, 12.4f), new Vector2(13.6f, 26.1f) })
                    Rail(Project(span.x, v) + Vector2.up * 2.65f, Project(span.y, v) + Vector2.up * 2.65f, .07f, order);
                foreach (float center in new[] { 2f, 10f, 16f, 24f })
                    for (int i = 0; i < 4; i++) Handle(Project(center - 1.65f + i * 1.1f, v) + Vector2.up * 2.62f, order);
            }
            FlushSurfaces();
            map.portals = portals.ToArray(); map.exits = exits.ToArray(); map.interests = interests.ToArray();
            map.solidFootprints = solids.ToArray(); map.entry = Project(6, 2);
            foreach (var solid in solids)
            {
                var go = new GameObject("Carriage_Furniture_Collider"); go.transform.SetParent(root, false); go.transform.position = solid.center;
                var collider = go.AddComponent<PolygonCollider2D>(); float x = solid.halfSize.x, y = solid.halfSize.y;
                collider.points = new[] { new Vector2(-x, -y - x * .5f), new Vector2(x, -y + x * .5f), new Vector2(x, y + x * .5f), new Vector2(-x, y - x * .5f) };
            }
            map.noWaitingZones = new Rect[0]; map.RebuildGrid();
            for (int i = 0; i < map.interests.Length; i++) map.interests[i].position = map.Grid.Nearest(map.interests[i].position);
            for (int i = 0; i < map.portals.Length; i++) map.portals[i].position = map.Grid.Nearest(map.portals[i].position);
            for (int i = 0; i < map.exits.Length; i++) map.exits[i].inside = map.Grid.Nearest(map.exits[i].inside);
        }

        void Bench(Sprite sprite, float u, float v, bool rearView)
        {
            Vector2 ground = Project(u, v), facing = Project(0, rearView ? 1 : -1).normalized;
            // Four places at ~0.76 projected units each; a backrest must not hide a seated head.
            float scale = 4f / sprite.bounds.size.x;
            // Contact points measured on the source atlas, top-left pixel convention.
            Vector2 reference = rearView ? new Vector2(540, 1163) : new Vector2(546, 476);
            Vector2 localContact = SpritePixel(sprite, reference);
            Vector2 contact = ground + Vector2.up * .5f;
            var go = new GameObject(rearView ? "Bench_Row_RearView_Facing_Aisle" : "Bench_Row_FrontView_Facing_Aisle");
            go.transform.SetParent(root, false); go.transform.position = contact - localContact * scale;
            go.transform.localScale = Vector3.one * scale;
            var r = go.AddComponent<SpriteRenderer>(); r.sprite = sprite; r.sharedMaterial = surfaceMaterial;
            r.sortingOrder = map.GroundOrder(ground);
            for (int i = 0; i < 4; i++)
            {
                float x = rearView ? 338 + i * 135 : 344 + i * 135;
                Vector2 pixel = new Vector2(x, reference.y - (x - reference.x) * .5f);
                Vector2 anchor = go.transform.TransformPoint(SpritePixel(sprite, pixel));
                Vector2 feet = anchor - Vector2.up * .5f;
                interests.Add(new SliceInterest { position = feet + facing * 1.05f,
                    kind = PassengerAiV2InteriorSpotKind.Seat, comfort = .95f, hasPoseAnchor = true,
                    contactPoint = anchor, facing = facing, poseSortingOrder = r.sortingOrder + (rearView ? -1 : 2) });
                interests.Add(new SliceInterest { position = feet + facing * 1.7f, kind = PassengerAiV2InteriorSpotKind.Stand, comfort = .55f });
            }
            solids.Add(new SliceSolidFootprint { center = ground, halfSize = new Vector2(1.7f, .32f), slope = .5f });
        }
        static Vector2 SpritePixel(Sprite sprite, Vector2 pixel)
            => new Vector2(pixel.x * sprite.texture.width / 1024f - sprite.rect.x - sprite.pivot.x,
                (1536 - pixel.y) * sprite.texture.height / 1536f - sprite.rect.y - sprite.pivot.y) / sprite.pixelsPerUnit;
        Sprite AtlasPiece(Texture2D texture, Rect rect)
        {
            float sx = texture.width / 1024f, sy = texture.height / 1536f;
            var sprite = Sprite.Create(texture, new Rect(rect.x * sx, rect.y * sy, rect.width * sx, rect.height * sy),
                new Vector2(.5f, 0), 100, 0, SpriteMeshType.FullRect);
            createdSprites.Add(sprite); return sprite;
        }
        void SideWall(float start, float end, int side)
        {
            float v = side == 0 ? -.5f : 6.5f;
            Vector2 a = Project(start, v), b = Project(end, v); int order = map.GroundOrder(a);
            // Keep the carriage closed; nearby blocking panels fade dynamically, not permanently.
            Panel(a, b, .58f, new Color32(120, 139, 148, 255), order);
            Panel(a + Vector2.up * .58f, b + Vector2.up * .58f, 2.57f,
                side == 0 ? new Color32(176, 190, 196, 255) : new Color32(207, 216, 219, 255), order);
            Panel(a + Vector2.up * 3.05f, b + Vector2.up * 3.05f, .14f,
                new Color32(95, 121, 132, 255), order + 1);
            Panel(a + Vector2.up * .58f, b + Vector2.up * .58f, .06f, new Color32(68, 84, 93, 255), order + 1);
        }
        void Window(float center, int side)
        {
            float v = side == 0 ? -.51f : 6.49f;
            Vector2 a = Project(center - 1.9f, v) + Vector2.up * 1.28f, b = Project(center + 1.9f, v) + Vector2.up * 1.28f;
            int order = map.GroundOrder(Project(center, v)) + 2;
            Panel(a, b, 1.4f, new Color32(49, 66, 75, 255), order);
            Panel(a + new Vector2(.1f, .12f), b + new Vector2(-.1f, .02f), 1.2f,
                new Color32(79, 115, 127, 255), order + 1);
            Rail(a + Vector2.up * 1.44f, b + Vector2.up * 1.44f, .04f, order + 2);
        }
        void EndWall(float u)
        {
            for (int side = 0; side < 2; side++)
            {
                Vector2 a = Project(u, side == 0 ? -.5f : 4.05f), b = Project(u, side == 0 ? 1.95f : 6.5f);
                Panel(a, b, 3.15f, new Color32(159, 178, 186, 255), map.GroundOrder(a));
            }
            Vector2 left = Project(u, 2), right = Project(u, 4); int order = map.GroundOrder(Project(u, 2));
            Panel(left, right, 2.65f, new Color32(91, 111, 121, 255), order);
            Panel(left + Vector2.up * 1.1f, right + Vector2.up * 1.1f, 1.2f, new Color32(46, 71, 84, 255), order + 1);
            Panel(left + Vector2.up * 2.65f, right + Vector2.up * 2.65f, .5f, new Color32(199, 210, 215, 255), order);
        }
        void Support(float u, float v)
        {
            Vector2 p = Project(u, v); int order = map.GroundOrder(p) + 3;
            Rail(p, p + Vector2.up * 2.7f, .08f, order);
            solids.Add(new SliceSolidFootprint { center = p, halfSize = new Vector2(.07f, .07f), slope = .5f });
            Vector2 towardAisle = Project(0, v < 3 ? 1 : -1).normalized;
            interests.Add(new SliceInterest { position = p + towardAisle * .55f,
                kind = PassengerAiV2InteriorSpotKind.Stand, comfort = .65f, hasPoseAnchor = true,
                contactPoint = p + Vector2.up * .7f, facing = -towardAisle, poseSortingOrder = order });
        }
        void Handle(Vector2 top, int order)
        {
            Vector2 neck = top - Vector2.up * .35f;
            Rail(top, neck, .045f, order);
            Vector2 left = neck + new Vector2(-.13f, -.18f), right = neck + new Vector2(.13f, -.18f);
            Rail(neck, left, .05f, order); Rail(left, right, .05f, order); Rail(right, neck, .05f, order);
        }
        void Rail(Vector2 a, Vector2 b, float width, int order)
        {
            Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x) * width * .5f;
            Face(a - n, b - n, b + n, a + n, new Color32(63, 79, 87, 255), order);
            n *= .42f;
            Face(a - n, b - n, b + n, a + n, new Color32(204, 221, 228, 255), order);
        }
        void Floor(float a, float b, float c, float d, Color color, int order)
            => Face(Project(a, c), Project(b, c), Project(b, d), Project(a, d), color, order);
        void Panel(Vector2 a, Vector2 b, float height, Color color, int order)
            => Face(a, b, b + Vector2.up * height, a + Vector2.up * height, color, order);
        void Face(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color, int order)
        {
            var key = (order, wallSurface);
            if (!surfaces.TryGetValue(key, out var g)) { g = new Geometry(); surfaces.Add(key, g); }
            int i = g.vertices.Count;
            g.vertices.Add(a); g.vertices.Add(b); g.vertices.Add(c); g.vertices.Add(d);
            for (int n = 0; n < 4; n++) g.colors.Add(color);
            g.triangles.Add(i); g.triangles.Add(i + 2); g.triangles.Add(i + 1);
            g.triangles.Add(i); g.triangles.Add(i + 3); g.triangles.Add(i + 2);
        }
        void FlushSurfaces()
        {
            // Batch rail/handle segments by depth instead of creating hundreds of renderers.
            foreach (var pair in surfaces)
            {
                var mesh = new Mesh { name = (pair.Key.wall ? "Occluding carriage wall " : "Carriage surfaces ") + pair.Key.order };
                mesh.SetVertices(pair.Value.vertices); mesh.SetTriangles(pair.Value.triangles, 0); mesh.SetColors(pair.Value.colors);
                mesh.uv = new Vector2[mesh.vertexCount]; mesh.RecalculateBounds(); meshes.Add(mesh);
                var go = new GameObject(mesh.name); go.transform.SetParent(root, false); go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = surfaceMaterial; r.sortingOrder = pair.Key.order;
            }
            surfaces.Clear();
        }
        static List<DoorPart> ReadDoorAssembly(SliceMap train)
        {
            var result = new List<DoorPart>(); var renderers = train.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var frame in renderers)
            {
                if (frame.sprite == null || !frame.sprite.name.Contains("door_fixed") || !frame.transform.parent.name.StartsWith("ClosedDoor_")) continue;
                SpriteRenderer left = null, right = null;
                foreach (var leaf in renderers)
                {
                    if (leaf.sprite == null || leaf.transform.parent != frame.transform.parent) continue;
                    if (leaf.sprite.name.Contains("door_leaf_left")) left = leaf;
                    if (leaf.sprite.name.Contains("door_leaf_right")) right = leaf;
                }
                if (left == null || right == null) continue;
                Bounds bounds = frame.bounds; bounds.Encapsulate(left.bounds); bounds.Encapsulate(right.bounds);
                foreach (var r in new[] { frame, left, right })
                    result.Add(new DoorPart { sprite = r.sprite, offset = ((Vector2)r.bounds.center - (Vector2)bounds.center) / bounds.size.x,
                        width = r.bounds.size.x / bounds.size.x, name = r == frame ? "DoorFrame" : "DoorLeaf" });
                break;
            }
            return result;
        }
        SpriteRenderer Art(string name, Sprite sprite, Vector2 position, float width, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(root, false);
            float scale = width / Mathf.Max(.01f, sprite.bounds.size.x);
            go.transform.localScale = Vector3.one * scale; go.transform.position = (Vector3)position - sprite.bounds.center * scale;
            var r = go.AddComponent<SpriteRenderer>(); r.sprite = sprite; r.sharedMaterial = surfaceMaterial; r.sortingOrder = order; return r;
        }
        void OnDestroy()
        {
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            foreach (var sprite in createdSprites) if (sprite != null) Destroy(sprite);
            if (surfaceMaterial != null) Destroy(surfaceMaterial);
        }
    }
}
