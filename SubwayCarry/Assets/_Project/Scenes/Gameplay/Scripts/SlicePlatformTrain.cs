using System.Collections.Generic;
using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    // The platform view and the interior view share rider IDs. Only the visible place runs crowd AI.
    public sealed class SlicePlatformTrain : MonoBehaviour
    {
        sealed class Door
        {
            public Vector2 landing, cabin, inward;
            public Vector2 foot;
            public float halfWidth, height;
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
        readonly SliceTransitGeometry shell = new SliceTransitGeometry();
        SliceGameController world;
        SliceJourneyController flow;
        SliceMap map;
        Transform train;
        Material glass;
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
            if (flow.Catalog.stationGlassShader != null) glass = new Material(flow.Catalog.stationGlassShader);
            var art = map.GetComponentsInChildren<SpriteRenderer>(true);
            train = new GameObject("ServiceTrain_Behind_PlatformGlass").transform; train.SetParent(map.transform, false);
            train.localScale = Vector3.one / map.transform.lossyScale.x;
            train.position = Vector3.zero;
            var usedFrames = new HashSet<SpriteRenderer>();
            foreach (var portal in map.portals)
            {
                if (portal.stationConnection) continue;
                SpriteRenderer screen = null; float best = float.PositiveInfinity;
                foreach (var r in art)
                {
                    if (r.sprite == null || !r.sprite.name.Contains("door_fixed") || usedFrames.Contains(r)) continue;
                    Bounds candidate = OpeningBounds(r,art);
                    Vector2 sill = new Vector2(candidate.center.x,candidate.min.y+candidate.size.x*Mathf.Abs(map.depthSlope)*.5f);
                    float d = (sill-portal.position).sqrMagnitude;
                    if (d < best) { best = d; screen = r; }
                }
                if (screen == null) continue;
                usedFrames.Add(screen);
                Vector2 normal = new Vector2(map.depthSlope, -1).normalized;
                float wallDistance = float.PositiveInfinity;
                foreach (var wall in map.walls)
                {
                    float d = Mathf.Abs(Vector2.Dot(portal.position - wall.point, wall.inwardNormal));
                    if (d < wallDistance) { wallDistance = d; normal = wall.inwardNormal; }
                }
                Bounds opening = OpeningBounds(screen,art);
                float width = opening.size.x / tangent.x;
                Vector2 foot = new Vector2(opening.center.x, opening.min.y + opening.size.x * Mathf.Abs(map.depthSlope) * .5f) - normal * .18f;
                float height = Mathf.Max(2.5f, opening.size.y - opening.size.x * Mathf.Abs(map.depthSlope));
                var door = new Door { landing = portal.position, inward = normal, cabin = foot - normal * .85f,
                    frame = screen, foot = foot, halfWidth = width*.5f, height = height };
                doors.Add(door);
                int order = map.GroundOrder(foot);
                var go = new GameObject("Train_Door_In_Shell"); go.transform.SetParent(train, false);
                go.AddComponent<SliceSlidingDoor>().Build(foot, tangent, width, height, order);
                shell.Panel(foot-tangent*width*.5f-normal, foot+tangent*width*.5f-normal, height,
                    new Color32(105,126,130,255), order-110);
            }
            doors.Sort((a, b) => Vector2.Dot(a.landing, tangent).CompareTo(Vector2.Dot(b.landing, tangent)));
            BuildShell();
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
        static Bounds OpeningBounds(SpriteRenderer screen, SpriteRenderer[] art)
        {
            Bounds bounds = screen.bounds; bool found = false;
            foreach (var leaf in art)
            {
                if (leaf.sprite == null || leaf.transform.parent != screen.transform.parent || !leaf.sprite.name.Contains("door_leaf")) continue;
                if (!found) { bounds=leaf.bounds; found=true; } else bounds.Encapsulate(leaf.bounds);
            }
            return bounds;
        }
        void BuildShell()
        {
            if (doors.Count == 0) return;
            // Island platforms have two opposed walls. Never join their doors into one folded train body.
            var first = new List<Door>(); var opposite = new List<Door>();
            foreach (var door in doors) (Vector2.Dot(door.inward,doors[0].inward) > .5f ? first : opposite).Add(door);
            BuildShell(first); if (opposite.Count > 0) BuildShell(opposite);
        }
        void BuildShell(List<Door> sideDoors)
        {
            Vector2 normal = sideDoors[0].inward, rear = -normal * 2.7f;
            Vector2 start = sideDoors[0].foot-tangent*(sideDoors[0].halfWidth+3.5f);
            Vector2 end = sideDoors[sideDoors.Count-1].foot+tangent*(sideDoors[sideDoors.Count-1].halfWidth+3.5f);
            float h = sideDoors[0].height + .36f; int order = map.GroundOrder(sideDoors[0].foot);
            Vector2 next = start;
            foreach (var door in sideDoors)
            {
                Vector2 left = door.foot-tangent*(door.halfWidth+.12f), right = door.foot+tangent*(door.halfWidth+.12f);
                ShellBay(next, left, h, order);
                shell.Panel(left+Vector2.up*(h-.2f), right+Vector2.up*(h-.2f), .2f, new Color32(215,221,221,255), order+3);
                next = right;
            }
            ShellBay(next, end, h, order);
            // Continuous roof, rounded eaves and both cab/end faces give the passing train real volume.
            shell.Quad(start+Vector2.up*h, end+Vector2.up*h, end+rear+Vector2.up*(h+.18f), start+rear+Vector2.up*(h+.18f), new Color32(185,199,202,255), order+5);
            shell.Rail(start+Vector2.up*h, end+Vector2.up*h, .1f, new Color32(237,240,233,255), order+6);
            shell.Panel(start, start+rear, h, new Color32(180,187,175,255), order+4);
            shell.Panel(end, end+rear, h, new Color32(140,158,166,255), order-4);
            Vector2 noseA = start + rear*.08f, noseB = start + rear*.92f;
            shell.Panel(noseA+Vector2.up*.55f, noseB+Vector2.up*.55f, .48f, new Color32(230,185,38,255), order+5);
            shell.Panel(noseA+Vector2.up*1.35f, noseB+Vector2.up*1.35f, h-1.65f, new Color32(31,55,67,255), order+5);
            shell.Rail(noseA+Vector2.up*(h-.27f), noseB+Vector2.up*(h-.27f), .12f, new Color32(36,86,126,255), order+6);
            foreach (float fraction in new[] { .2f, .8f })
            {
                Vector2 p = Vector2.Lerp(start,start+rear,fraction) + Vector2.up*.68f;
                shell.Rail(p-tangent*.10f,p+tangent*.10f,.12f,new Color32(251,243,191,255),order+6);
            }
            // Roof equipment is low and attached to the roof plane, never floating above the cabin.
            for (float d = 3; d < Vector2.Distance(start,end)-2; d += 7)
            {
                Vector2 p = start+tangent*d+rear*.4f+Vector2.up*(h+.2f);
                shell.Quad(p,p+tangent*2,p+tangent*2+rear*.28f,p+rear*.28f,new Color32(132,154,163,255),order+6);
                for (int i = 0; i < 6; i++) shell.Rail(p+tangent*(.15f+i*.3f),p+tangent*(.15f+i*.3f)+rear*.28f,.035f,new Color32(82,102,115,255),order+7);
            }
            shell.Flush(train,"Exterior train shell");
        }
        void ShellBay(Vector2 a, Vector2 b, float height, int order)
        {
            if (Vector2.Dot(b-a,tangent) < .05f) return;
            var metal = new Color32(198,209,209,255);
            shell.Panel(a,b,.88f,metal,order+1);
            shell.Panel(a+Vector2.up*.55f,b+Vector2.up*.55f,.14f,new Color32(220,176,32,255),order+2);
            shell.Panel(a+Vector2.up*2.1f,b+Vector2.up*2.1f,Mathf.Max(.1f,height-2.1f),metal,order+1);
            shell.Panel(a+Vector2.up*.88f,a+tangent*.2f+Vector2.up*.88f,1.22f,metal,order+1);
            shell.Panel(b-tangent*.2f+Vector2.up*.88f,b+Vector2.up*.88f,1.22f,metal,order+1);
            shell.Panel(a+tangent*.2f+Vector2.up*.88f,b-tangent*.2f+Vector2.up*.88f,1.22f,new Color32(61,91,108,145),order+2);
            shell.Rail(a+Vector2.up*.88f,b+Vector2.up*.88f,.07f,new Color32(64,83,94,255),order+3);
            shell.Rail(a+Vector2.up*2.1f,b+Vector2.up*2.1f,.06f,new Color32(64,83,94,255),order+3);
            shell.Panel(a-Vector2.up*.15f,b-Vector2.up*.15f,.15f,new Color32(55,68,77,255),order);
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
        void ApplyGlass(SpriteRenderer renderer)
        {
            if (glass == null || renderer.sprite == null) return;
            renderer.sharedMaterial = glass;
            var sprite = renderer.sprite;
            string name = sprite.name.ToLowerInvariant();
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            // Only the platform screen is very transparent. Two near-transparent door skins looked like holes.
            block.SetFloat("_GlassAlpha", name.Contains("screen_door") ? .42f : .72f);
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
        void OnDestroy()
        {
            if (flow != null) flow.TrainDoorStateChanged -= DoorChanged;
            shell.Dispose();
            if (glass != null) Destroy(glass);
        }
    }
}
