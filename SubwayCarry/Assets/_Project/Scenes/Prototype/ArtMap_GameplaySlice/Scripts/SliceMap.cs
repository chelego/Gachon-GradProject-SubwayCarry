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
        public bool Contains(Vector2 p, float padding = 0)
        {
            Vector2 d = p - center;
            return Mathf.Abs(d.x) <= halfSize.x + padding && Mathf.Abs(d.y - slope * d.x) <= halfSize.y + padding * Mathf.Sqrt(1 + slope * slope);
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
    }
    [System.Serializable] public struct SliceExit { public Vector2 inside, outside; }
    [System.Serializable] public struct SliceInterest
    {
        public Vector2 position;
        public PassengerAiV2InteriorSpotKind kind;
        public float comfort;
    }
    public sealed class SliceMap : MonoBehaviour
    {
        public string displayName;
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
        public bool ContainsFloor(Vector2 p)
        {
            if (floorDiamonds.Length == 0) return floorBounds.Contains(p);
            for (int i = 0; i < floorDiamonds.Length; i++) { var d = floorDiamonds[i]; Vector2 q = p - d.center; if (Mathf.Abs(q.x) / d.halfSize.x + Mathf.Abs(q.y) / d.halfSize.y <= 1) return true; }
            return false;
        }
    }
}
