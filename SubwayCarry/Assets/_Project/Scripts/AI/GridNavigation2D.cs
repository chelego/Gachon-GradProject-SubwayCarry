using System;
using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.AI
{
    [DisallowMultipleComponent]
    public sealed class GridNavigation2D : MonoBehaviour
    {
        [SerializeField, Min(1)] private int width = 72;
        [SerializeField, Min(1)] private int height = 32;
        [SerializeField, Min(0.1f)] private float cellSize = 0.25f;
        [SerializeField, Range(0.1f, 1f)] private float obstacleCheckScale = 0.75f;
        [SerializeField] private bool drawGrid = true;

        public List<Vector3> FindWorldPath(Vector3 startWorld, Vector3 goalWorld)
        {
            return FindWorldPath(startWorld, goalWorld, null);
        }

        public List<Vector3> FindWorldPath(
            Vector3 startWorld,
            Vector3 goalWorld,
            Predicate<Vector2> dynamicWalkable)
        {
            Vector2Int start = WorldToCell(startWorld);
            Vector2Int goal = WorldToCell(goalWorld);
            List<Vector2Int> cells = AStarGrid.FindPath(
                start,
                goal,
                width,
                height,
                cell => IsWalkable(cell) &&
                        (dynamicWalkable == null || dynamicWalkable(CellToWorld(cell))));
            var worldPath = new List<Vector3>(cells.Count);

            foreach (Vector2Int cell in cells)
            {
                Vector3 point = CellToWorld(cell);
                point.z = startWorld.z;
                worldPath.Add(point);
            }

            if (worldPath.Count > 0)
            {
                goalWorld.z = startWorld.z;
                worldPath[worldPath.Count - 1] = goalWorld;
            }

            return worldPath;
        }

        public Vector2Int WorldToCell(Vector3 worldPosition)
        {
            Vector2 local = (Vector2)worldPosition - GridOrigin;
            return new Vector2Int(
                Mathf.FloorToInt(local.x / cellSize),
                Mathf.FloorToInt(local.y / cellSize));
        }

        public Vector3 CellToWorld(Vector2Int cell)
        {
            return GridOrigin + new Vector2(
                (cell.x + 0.5f) * cellSize,
                (cell.y + 0.5f) * cellSize);
        }

        public bool IsWalkable(Vector2Int cell)
        {
            if (cell.x < 0 || cell.y < 0 || cell.x >= width || cell.y >= height)
            {
                return false;
            }

            Vector2 checkSize = Vector2.one * cellSize * obstacleCheckScale;
            Collider2D[] overlaps = Physics2D.OverlapBoxAll(CellToWorld(cell), checkSize, 0f);

            foreach (Collider2D overlap in overlaps)
            {
                if (overlap.GetComponentInParent<NavigationObstacle>() != null)
                {
                    return false;
                }
            }

            return true;
        }

        private Vector2 GridOrigin
        {
            get
            {
                return (Vector2)transform.position - new Vector2(width * cellSize, height * cellSize) * 0.5f;
            }
        }

        private void OnDrawGizmos()
        {
            if (!drawGrid)
            {
                return;
            }

            Gizmos.color = new Color(1f, 1f, 1f, 0.15f);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Gizmos.DrawWireCube(CellToWorld(new Vector2Int(x, y)), Vector3.one * cellSize);
                }
            }
        }
    }
}
