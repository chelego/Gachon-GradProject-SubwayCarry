using UnityEngine;
using UnityEngine.Tilemaps;
using SubwayCarry.AI.V2;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    [System.Serializable] public struct SliceFloorDiamond { public Vector2 center, halfSize; }
    [System.Serializable] public struct SliceSolidFootprint
    {
        public Vector2 center, halfSize;
        public float slope;
        public bool diamond;
        public Vector2[] outline;
        public bool Contains(Vector2 p, float padding = 0)
        {
            if (outline != null && outline.Length >= 3)
            {
                float area = 0;
                for (int i = 0; i < outline.Length; i++) { Vector2 a = outline[i], b = outline[(i+1)%outline.Length]; area += a.x*b.y-b.x*a.y; }
                float sign = area >= 0 ? 1 : -1;
                for (int i = 0; i < outline.Length; i++)
                { Vector2 a = outline[i], edge = outline[(i+1)%outline.Length]-a, d0 = p-a; if (sign*(edge.x*d0.y-edge.y*d0.x) < -padding*edge.magnitude) return false; }
                return true;
            }
            Vector2 d = p - center;
            // Expand each sloped edge by the actor's ground radius, not by a guessed sprite margin.
            if (diamond) return Mathf.Abs(d.x) / Mathf.Max(.001f, halfSize.x) +
                Mathf.Abs(d.y) / Mathf.Max(.001f, halfSize.y) <= 1 + padding *
                Mathf.Sqrt(1 / (halfSize.x * halfSize.x) + 1 / (halfSize.y * halfSize.y));
            return Mathf.Abs(d.x) <= halfSize.x + padding && Mathf.Abs(d.y - slope * d.x) <= halfSize.y + padding * Mathf.Sqrt(1 + slope * slope);
        }
        public Vector2[] Corners()
        {
            if (outline != null && outline.Length >= 3) return (Vector2[])outline.Clone();
            float x = halfSize.x, y = halfSize.y, rise = x * slope;
            return diamond ? new[] { center + new Vector2(-x, 0), center + new Vector2(0, y), center + new Vector2(x, 0), center + new Vector2(0, -y) }
                : new[] { center + new Vector2(-x, -rise-y), center + new Vector2(x, rise-y), center + new Vector2(x, rise+y), center + new Vector2(-x, -rise+y) };
        }
        public static SliceSolidFootprint Polygon(Vector2[] points)
        {
            Vector2 min = points[0], max = min;
            foreach (var p in points) { min = Vector2.Min(min,p); max = Vector2.Max(max,p); }
            return new SliceSolidFootprint { center = (min+max)*.5f, halfSize = (max-min)*.5f, outline = points };
        }
    }
    [System.Serializable] public struct SliceWallBoundary
    {
        public Vector2 point, inwardNormal;
        public bool Allows(Vector2 p, float clearance = 0) => Vector2.Dot(p - point, inwardNormal) >= clearance;
    }
    [System.Serializable] public struct SlicePortal
    {
        public string label;
        public Vector2 position;
        public int targetMap;
        public Vector2 arrival;
        public SubwayCarry.Core.Contracts.DoorOpeningSide side;
        public bool stationConnection;
        public bool guidedTraversal;
        public Vector2 traversalEnd;
        public float traversalSeconds;
        public bool arriveThroughDoor;
        public Vector2 arrivalTraversalEnd;
        public SliceTravelDirection direction;
    }
    [System.Serializable] public struct SliceExit { public Vector2 inside, outside; }
    [System.Serializable] public struct SliceInterest
    {
        public Vector2 position;
        public PassengerAiV2InteriorSpotKind kind;
        public float comfort;
        // Navigation stays on the floor; presentation aligns the pelvis/back to the furniture.
        public bool hasPoseAnchor;
        public Vector2 contactPoint;
        public Vector2 facing;
        public int poseSortingOrder;
    }
    public enum SliceTravelDirection { Unspecified, Jeongja, Wangsimni }
    public sealed class SliceMap : MonoBehaviour
    {
        public string displayName;
        public bool hasFareGate;
        public SliceTravelDirection travelDirection;
        public Vector2 fareGate, streetExit;
        public PassengerAiV2PlaceKind placeKind;
        public Rect[] noWaitingZones = new Rect[0];
        public Tilemap floorTiles;
        public Rect floorBounds;
        public SliceFloorDiamond[] floorDiamonds = new SliceFloorDiamond[0];
        public Rect[] obstacles = new Rect[0];
        public SliceWallBoundary[] walls = new SliceWallBoundary[0];
        public SliceSolidFootprint[] solidFootprints = new SliceSolidFootprint[0];
        [Tooltip("Ground depth is y - slope*x; station art follows a 2:1 diagonal.")]
        public float depthSlope;
        public float characterScale = 1.1f;
        public float characterRadius = 0.28f;
        public Vector2 entry;
        public float cameraSize = 7;
        public SlicePortal[] portals = new SlicePortal[0];
        public SliceExit[] exits = new SliceExit[0];
        public SliceInterest[] interests = new SliceInterest[0];
        public SliceNavigationGrid Grid { get; private set; }
        public int GroundOrder(Vector2 feet) => Mathf.Clamp(Mathf.RoundToInt(-(feet.y - depthSlope * feet.x) * 80f), -25000, 25000);
        public void RebuildGrid() { Grid = null; EnsureGrid(); }
        public void EnsureGrid() { if (Grid == null) Grid = new SliceNavigationGrid(floorBounds, floorDiamonds, obstacles, walls, characterRadius, solidFootprints); }
        public void BuildFootprintColliders()
        {
            // Only integration-generated caches are replaced; imported art/manual colliders are untouched.
            foreach (string name in new[] { "SolidFootprints", "AlignedFurnitureFootprints", "GroundContactColliders" })
            {
                var old = transform.Find(name);
                if (old != null) { old.gameObject.SetActive(false); if (name == "GroundContactColliders") Destroy(old.gameObject); }
            }
            var root = new GameObject("GroundContactColliders"); root.transform.SetParent(transform, false);
            foreach (var solid in solidFootprints)
            {
                var go = new GameObject("Ground footprint"); go.transform.SetParent(root.transform, false);
                var points = solid.Corners();
                for (int i = 0; i < points.Length; i++) points[i] = go.transform.InverseTransformPoint(points[i]);
                go.AddComponent<PolygonCollider2D>().points = points;
            }
        }
        public bool ContainsFloor(Vector2 p)
        {
            if (floorDiamonds.Length == 0) return floorBounds.Contains(p);
            for (int i = 0; i < floorDiamonds.Length; i++) { var d = floorDiamonds[i]; Vector2 q = p - d.center; if (Mathf.Abs(q.x) / d.halfSize.x + Mathf.Abs(q.y) / d.halfSize.y <= 1) return true; }
            return false;
        }
    }
}
