using System.Collections.Generic;
using SubwayCarry.Core.Contracts;
using UnityEngine;
using UnityEngine.Rendering;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    // The platform view and the interior view share rider IDs. Only the visible place runs crowd AI.
    public sealed class SlicePlatformTrain : MonoBehaviour
    {
        sealed class Door
        {
            public Vector2 landing, cabin, inward;
            public SpriteRenderer frame;
        }
        sealed class Rider
        {
            public int id, destination, door;
            public Transform art;
        }
        readonly List<Door> doors = new List<Door>();
        readonly List<Rider> riders = new List<Rider>();
        readonly List<Collider2D> boundaryColliders = new List<Collider2D>();
        readonly List<Mesh> meshes = new List<Mesh>();
        SliceGameController world;
        SliceJourneyController flow;
        SliceMap map;
        Transform train;
        Material glass, surfaces;
        Vector2 tangent;
        float offset, departureAt = -1000, nextAlighter;
        bool stocked;
        bool clearedDeparture, wasDocked;
        int crossingAlighters;
        public bool HasCrossingAlighters => crossingAlighters > 0;
        public bool CanBoard => !HasCrossingAlighters && !HasDueRiders;
        bool HasDueRiders
        {
            get { foreach (var r in riders) if (r.destination <= world.ApproachingStopIndex) return true; return false; }
        }
        public void Initialize(SliceGameController source, SliceJourneyController journey, SliceMap platform)
        {
            world = source; flow = journey; map = platform; tangent = new Vector2(1, map.depthSlope).normalized;
            surfaces = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            if (flow.Catalog.stationGlassShader != null) glass = new Material(flow.Catalog.stationGlassShader);
            var art = map.GetComponentsInChildren<SpriteRenderer>(true);
            Sprite frame = null, left = null, right = null, window = null;
            SpriteRenderer sourceFrame = null, sourceLeft = null, sourceRight = null;
            var trainArt = world.maps[1].GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var r in trainArt)
            {
                if (r.sprite == null) continue;
                string n = r.sprite.name.ToLowerInvariant();
                if (n.Contains("door_fixed")) frame = r.sprite;
                if (n.Contains("door_leaf_left")) left = r.sprite;
                if (n.Contains("door_leaf_right")) right = r.sprite;
                if (n.Contains("window")) window = r.sprite;
                // The fixed sprite is only the header. Preserve the imported assembly's leaf offsets
                // instead of centering that short header at the middle of a full-height doorway.
                if (sourceFrame == null && n.Contains("door_fixed") && r.transform.parent.name.StartsWith("ClosedDoor_")) sourceFrame = r;
            }
            if (sourceFrame != null)
                foreach (var r in trainArt)
                {
                    if (r.sprite == null || r.transform.parent != sourceFrame.transform.parent) continue;
                    if (r.sprite.name.Contains("door_leaf_left")) sourceLeft = r;
                    if (r.sprite.name.Contains("door_leaf_right")) sourceRight = r;
                }
            bool assembled = sourceFrame != null && sourceLeft != null && sourceRight != null;
            Bounds assemblyBounds = assembled ? sourceFrame.bounds : default;
            if (assembled) { assemblyBounds.Encapsulate(sourceLeft.bounds); assemblyBounds.Encapsulate(sourceRight.bounds); }
            if (frame == null || left == null || right == null) { enabled = false; return; }
            train = new GameObject("ServiceTrain_Behind_PlatformGlass").transform; train.SetParent(map.transform, false);
            train.localScale = Vector3.one / map.transform.lossyScale.x;
            train.position = Vector3.zero;
            foreach (var portal in map.portals)
            {
                if (portal.stationConnection) continue;
                SpriteRenderer screen = null; float best = float.PositiveInfinity;
                foreach (var r in art)
                {
                    if (r.sprite == null || !r.sprite.name.Contains("door_fixed")) continue;
                    float d = Mathf.Abs(r.bounds.center.x - portal.position.x);
                    if (d < best) { best = d; screen = r; }
                }
                if (screen == null) continue;
                Vector2 normal = new Vector2(map.depthSlope, -1).normalized;
                float wallDistance = float.PositiveInfinity;
                foreach (var wall in map.walls)
                {
                    float d = Mathf.Abs(Vector2.Dot(portal.position - wall.point, wall.inwardNormal));
                    if (d < wallDistance) { wallDistance = d; normal = wall.inwardNormal; }
                }
                var door = new Door { landing = portal.position, inward = normal,
                    cabin = portal.position - normal * 2.25f, frame = screen };
                doors.Add(door);
                Bounds opening = screen.bounds; bool foundLeaf = false;
                foreach (var leaf in art)
                {
                    if (leaf.sprite == null || leaf.transform.parent != screen.transform.parent || !leaf.sprite.name.Contains("door_leaf")) continue;
                    if (!foundLeaf) { opening = leaf.bounds; foundLeaf = true; } else opening.Encapsulate(leaf.bounds);
                }
                float width = opening.size.x;
                // Parallel door planes: a narrow gap, not a second doorway shifted half a person's width.
                Vector2 center = (Vector2)opening.center - normal * .12f;
                int order = map.GroundOrder(door.cabin);
                // Lit interior is behind the moving shell and passengers, not an opaque plate over the doorway.
                Wall("Cabin light", center - tangent * width * .65f - Vector2.up * 1.4f,
                    center + tangent * width * .65f - Vector2.up * 1.4f, 2.8f, new Color32(74, 94, 99, 255), order - 12, train);
                if (assembled)
                {
                    float scale = width / Mathf.Max(.01f, assemblyBounds.size.x);
                    DrawPart("TrainDoorFrame", sourceFrame, center, assemblyBounds.center, scale, order + 2);
                    DrawPart("TrainDoorLeaf_ServiceSide_Left", sourceLeft, center, assemblyBounds.center, scale, order + 1);
                    DrawPart("TrainDoorLeaf_ServiceSide_Right", sourceRight, center, assemblyBounds.center, scale, order + 1);
                }
                else
                {
                    Draw("TrainDoorFrame", frame, center + Vector2.up, width, order + 2);
                    Draw("TrainDoorLeaf_ServiceSide_Left", left, center - tangent * width * .25f, width * .5f, order + 1);
                    Draw("TrainDoorLeaf_ServiceSide_Right", right, center + tangent * width * .25f, width * .5f, order + 1);
                }
            }
            doors.Sort((a, b) => Vector2.Dot(a.landing, tangent).CompareTo(Vector2.Dot(b.landing, tangent)));
            // Windows and shell panels move together; the platform's glass/frames remain stationary.
            for (int i = 1; i < doors.Count; i++)
            {
                Vector2 a = doors[i - 1].cabin, b = doors[i].cabin;
                float length = Vector2.Distance(a, b);
                if (length > 20) continue;
                Wall("Train body", a, b, 2.1f, new Color32(184, 195, 193, 255), map.GroundOrder(a) - 10, train);
                if (window != null) Draw("Train window", window, (a + b) * .5f + Vector2.up * 1.15f,
                    Mathf.Max(1, Mathf.Abs(b.x - a.x) * .55f), map.GroundOrder(a) + 2);
            }
            foreach (var r in art)
            {
                if (r.sprite == null) continue;
                string n = r.sprite.name.ToLowerInvariant();
                if (!n.Contains("window") && !n.Contains("screen_door")) continue;
                // Remove only the old artificial opaque fill. Original frame pixels are preserved.
                var fill = r.transform.Find("OpaqueClosedSurface"); if (fill != null) fill.gameObject.SetActive(false);
                ApplyGlass(r);
            }
            var boundary = map.transform.Find("ClosedWallBoundaries");
            if (boundary != null) boundaryColliders.AddRange(boundary.GetComponentsInChildren<Collider2D>(true));
            flow.TrainDoorStateChanged += DoorChanged;
            Reenter();
        }
        public void Reenter()
        {
            if (train == null) return;
            crossingAlighters = 0;
            foreach (var r in riders) if (r.art != null) Destroy(r.art.gameObject);
            riders.Clear(); stocked = false; clearedDeparture = false;
            foreach (var record in world.AboardPassengers) AddRider(record.Key, record.Value, -1);
            stocked = riders.Count > 0;
            SyncPosition();
        }
        void DoorChanged(TrainDoorSnapshot state)
        {
            if (state.State == TrainDoorState.Open) nextAlighter = Time.time;
        }
        void Update()
        {
            if (train == null || world.CurrentMap != map) return;
            SyncPosition();
            if (!flow.PlatformTrainDocked && offset <= -OffscreenDistance * .99f && !clearedDeparture)
            {
                clearedDeparture = true; world.ClearDepartedTrain();
                foreach (var r in riders) if (r.art != null) Destroy(r.art.gameObject);
                riders.Clear();
            }
            if ((flow.PlatformTrainDocked || flow.PlatformTrainApproach01 > 0) && !stocked)
            {
                stocked = true; clearedDeparture = false;
                // New service occupants are persistent records, not decorative untracked sprites.
                for (int i = 0; i < Mathf.Min(6, doors.Count * 2); i++)
                {
                    int id = world.AllocatePassengerId();
                    int destination = flow.StopIndex + (i % 3 == 0 ? 1 : 0);
                    world.RememberTrainPassenger(id, destination); AddRider(id, destination, i % doors.Count);
                }
            }
            if (!world.DoorsOpen || Time.time < nextAlighter) return;
            nextAlighter = Time.time + .4f;
            for (int i = 0; i < riders.Count; i++)
            {
                var r = riders[i]; if (r.destination > flow.StopIndex) continue;
                var door = doors[r.door];
                if (!world.SpawnPlatformAlighter(r.id, r.destination, door.cabin, door.landing, this)) return;
                crossingAlighters++; Destroy(r.art.gameObject); riders.RemoveAt(i); return;
            }
        }
        void SyncPosition()
        {
            if (wasDocked && !flow.PlatformTrainDocked) { departureAt = Time.time; stocked = false; }
            if (flow.PlatformTrainDocked) departureAt = -1000;
            offset = SlicePresentationRules.TrainOffset(flow.PlatformTrainDocked, flow.PlatformTrainApproach01,
                departureAt > -999 ? Time.time - departureAt : -1, OffscreenDistance);
            wasDocked = flow.PlatformTrainDocked;
            train.position = tangent * offset;
        }
        float OffscreenDistance => Mathf.Max(80, map.floorBounds.width + map.floorBounds.height + 45);
        public bool TryCabin(Vector2 landing, out Vector2 cabin)
        {
            cabin = default; if (doors.Count == 0) return false;
            cabin = doors[ClosestDoor(landing)].cabin; return true;
        }
        int ClosestDoor(Vector2 point)
        {
            int result = 0; float best = float.PositiveInfinity;
            for (int i = 0; i < doors.Count; i++) { float d = (doors[i].landing - point).sqrMagnitude; if (d < best) { best = d; result = i; } }
            return result;
        }
        public void Boarded(int id, int destination, Vector2 from) => AddRider(id, destination, ClosestDoor(from));
        public void AlighterCleared() { crossingAlighters = Mathf.Max(0, crossingAlighters - 1); }
        public void SetPassageCollision(Collider2D actor, bool passing)
        {
            if (actor == null) return;
            foreach (var wall in boundaryColliders) if (wall != null) Physics2D.IgnoreCollision(actor, wall, passing);
        }
        void AddRider(int id, int destination, int doorIndex)
        {
            if (doors.Count == 0) return;
            foreach (var existing in riders) if (existing.id == id) return;
            int index = doorIndex < 0 ? id % doors.Count : doorIndex;
            var go = new GameObject("Rider_" + id); go.transform.SetParent(train, false);
            go.transform.localPosition = doors[index].cabin + tangent * ((id % 3 - 1) * .6f);
            go.transform.localScale = Vector3.one * map.characterScale;
            var body = go.AddComponent<SpriteRenderer>(); body.sprite = world.idleSprites[0];
            body.color = world.PassengerColor(id); body.sortingOrder = map.GroundOrder(doors[index].cabin);
            riders.Add(new Rider { id = id, destination = destination, door = index, art = go.transform });
        }
        void Draw(string label, Sprite sprite, Vector2 center, float width, int order)
        {
            var go = new GameObject(label); go.transform.SetParent(train, false);
            float scale = width / Mathf.Max(.01f, sprite.bounds.size.x);
            go.transform.localScale = Vector3.one * scale; go.transform.localPosition = (Vector3)center - sprite.bounds.center * scale;
            var r = go.AddComponent<SpriteRenderer>(); r.sprite = sprite; r.sortingOrder = order; r.sharedMaterial = surfaces;
            ApplyGlass(r);
        }
        void DrawPart(string label, SpriteRenderer source, Vector2 center, Vector2 assemblyCenter, float scale, int order)
            => Draw(label, source.sprite, center + ((Vector2)source.bounds.center - assemblyCenter) * scale, source.bounds.size.x * scale, order);
        void ApplyGlass(SpriteRenderer renderer)
        {
            if (glass == null || renderer.sprite == null) return;
            renderer.sharedMaterial = glass;
            var sprite = renderer.sprite;
            string name = sprite.name.ToLowerInvariant();
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            // Only the platform screen is very transparent. Two near-transparent door skins looked like holes.
            block.SetFloat("_GlassAlpha", name.Contains("screen_door") ? .24f : .72f);
            if (!name.Contains("screen_door")) { renderer.SetPropertyBlock(block); return; }
            Vector2 min = Vector2.one, max = Vector2.zero;
            foreach (var uv in sprite.uv) { min = Vector2.Min(min, uv); max = Vector2.Max(max, uv); }
            block.SetVector("_SpriteRect", new Vector4(min.x, min.y, max.x, max.y));
            block.SetFloat("_PaneSlope", sprite.rect.width * .5f / sprite.rect.height);
            // Regions follow the existing split PSD sprites; frames/crossbars keep their original pixels.
            if (name.Contains("leaf")) block.SetVector("_PaneA", new Vector4(.21f, .80f, .09f, .75f));
            else if (name.Contains("fixed"))
            {
                block.SetVector("_PaneA", new Vector4(.12f, .255f, .04f, .407f));
                block.SetVector("_PaneB", new Vector4(.785f, .913f, .04f, .407f));
            }
            renderer.SetPropertyBlock(block);
        }
        void Wall(string label, Vector2 a, Vector2 b, float height, Color color, int order, Transform parent)
        {
            var mesh = new Mesh { name = label };
            mesh.vertices = new[] { (Vector3)a, (Vector3)b, (Vector3)(b + Vector2.up * height), (Vector3)(a + Vector2.up * height) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 }; mesh.colors = new[] { color, color, color, color };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up }; mesh.RecalculateBounds(); meshes.Add(mesh);
            var go = new GameObject(label); go.transform.SetParent(parent, false); go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = surfaces; r.sortingOrder = order;
        }
        void OnDestroy()
        {
            if (flow != null) flow.TrainDoorStateChanged -= DoorChanged;
            if (glass != null) Destroy(glass); if (surfaces != null) Destroy(surfaces);
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
        }
    }
}
