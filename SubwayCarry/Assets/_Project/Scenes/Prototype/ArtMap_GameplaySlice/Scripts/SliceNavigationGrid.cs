using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    // Shared floor mask and reusable A* arrays; at most two new routes per rendered frame.
    public sealed class SliceNavigationGrid
    {
        public readonly Rect Bounds;
        public const float CellSize = 0.4f;
        public readonly float AgentRadius;
        readonly int width, height;
        readonly bool[] walkable;
        readonly bool[] mainArea;
        readonly SliceWallBoundary[] walls;
        readonly int[] parent, stamp, closed, heap, heapPosition;
        readonly float[] cost, priority;
        int search, heapCount, budgetFrame = -1, pathsThisFrame;
        public int PathRequests { get; private set; }
        public int PathFailures { get; private set; }
        public int WalkableCellCount { get; private set; }
        public readonly List<Vector2> SamplePoints = new List<Vector2>();
        public SliceNavigationGrid(Rect bounds, SliceFloorDiamond[] floor, Rect[] obstacles, SliceWallBoundary[] wallBoundaries = null, float agentRadius = 0.23f, SliceSolidFootprint[] solids = null)
        {
            AgentRadius = agentRadius;
            walls = wallBoundaries ?? System.Array.Empty<SliceWallBoundary>();
            Bounds = bounds; width = Mathf.CeilToInt(bounds.width / CellSize); height = Mathf.CeilToInt(bounds.height / CellSize);
            int n = width * height;
            walkable = new bool[n]; mainArea = new bool[n]; parent = new int[n]; stamp = new int[n]; closed = new int[n];
            heap = new int[n]; heapPosition = new int[n]; cost = new float[n]; priority = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 p = Center(i); bool clear = floor.Length == 0 && bounds.Contains(p);
                for (int f = 0; f < floor.Length && !clear; f++) { Vector2 d = p - floor[f].center; clear = Mathf.Abs(d.x) / floor[f].halfSize.x + Mathf.Abs(d.y) / floor[f].halfSize.y <= 1; }
                if (clear) for (int w = 0; w < walls.Length; w++) if (!walls[w].Allows(p, 0.06f)) { clear = false; break; }
                if (clear && solids != null) for (int s = 0; s < solids.Length; s++) if (solids[s].Contains(p, 0.04f)) { clear = false; break; }
                if (clear) for (int o = 0; o < obstacles.Length; o++)
                { Rect r = obstacles[o]; r.xMin -= 0.15f; r.xMax += 0.15f; r.yMin -= 0.15f; r.yMax += 0.15f; if (r.Contains(p)) { clear = false; break; } }
                walkable[i] = clear;
                if (clear) WalkableCellCount++;
            }
            FindMainArea();
        }
        void FindMainArea()
        {
            var labels = new int[walkable.Length]; var queue = new int[walkable.Length]; int label = 0, largest = 0, largestCount = 0;
            for (int start = 0; start < walkable.Length; start++)
            {
                if (labels[start] != 0 || !Fits(Center(start), AgentRadius)) continue;
                label++; int head = 0, tail = 1; queue[0] = start; labels[start] = label;
                while (head < tail)
                {
                    int c = queue[head++], x = c % width, y = c / width;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        if ((dx == 0 && dy == 0) || x+dx<0 || x+dx>=width || y+dy<0 || y+dy>=height) continue;
                        int next = c + dy * width + dx;
                        if (labels[next] != 0 || !Fits(Center(next), AgentRadius)) continue;
                        if (dx != 0 && dy != 0 && (!walkable[c+dx] || !walkable[c+dy*width])) continue;
                        labels[next] = label; queue[tail++] = next;
                    }
                }
                if (tail > largestCount) { largestCount = tail; largest = label; }
            }
            for (int i = 0; i < labels.Length; i++) { mainArea[i] = largest != 0 && labels[i] == largest; if (mainArea[i] && i%7 == 0) SamplePoints.Add(Center(i)); }
        }
        int Index(Vector2 p) { int x = Mathf.FloorToInt((p.x - Bounds.xMin) / CellSize), y = Mathf.FloorToInt((p.y - Bounds.yMin) / CellSize); return x < 0 || y < 0 || x >= width || y >= height ? -1 : y * width + x; }
        Vector2 Center(int i) => new Vector2(Bounds.xMin + (i % width + 0.5f) * CellSize, Bounds.yMin + (i / width + 0.5f) * CellSize);
        public bool IsWalkable(Vector2 p) { int i = Index(p); return i >= 0 && walkable[i]; }
        public bool Fits(Vector2 p, float r)
        {
            for (int i = 0; i < walls.Length; i++) if (!walls[i].Allows(p, r)) return false;
            return IsWalkable(p) && IsWalkable(p + Vector2.right * r) && IsWalkable(p - Vector2.right * r) && IsWalkable(p + Vector2.up * r) && IsWalkable(p - Vector2.up * r);
        }
        public Vector2 Nearest(Vector2 p)
        {
            int index = Index(p); if (index >= 0 && mainArea[index] && Fits(p, AgentRadius)) return p;
            float best = float.PositiveInfinity; Vector2 result = Bounds.center;
            for (int i = 0; i < walkable.Length; i++) { if (!mainArea[i]) continue; Vector2 q = Center(i); float d = (q - p).sqrMagnitude; if (d < best && Fits(q, AgentRadius)) { best = d; result = q; } }
            return result;
        }
        public Vector2 Constrain(Vector2 from, Vector2 to, float radius)
        {
            if (Fits(to, radius)) return to;
            Vector2 h = new Vector2(to.x, from.y), v = new Vector2(from.x, to.y);
            if (Fits(h, radius)) return h; if (Fits(v, radius)) return v; return from;
        }
        public bool ClearLine(Vector2 a, Vector2 b)
        { int steps = Mathf.CeilToInt(Vector2.Distance(a, b) / (CellSize * 0.5f)); for (int i = 1; i <= steps; i++) if (!Fits(Vector2.Lerp(a, b, i / (float)steps), AgentRadius)) return false; return true; }
        public bool TryPath(Vector2 from, Vector2 to, List<Vector2> result, bool validationOnly = false)
        {
            if (budgetFrame != Time.frameCount) { budgetFrame = Time.frameCount; pathsThisFrame = 0; }
            if (!validationOnly && pathsThisFrame >= 2) return false;
            pathsThisFrame++; PathRequests++; result.Clear();
            int start = Index(Nearest(from)), goal = Index(Nearest(to));
            if (start < 0 || goal < 0) { PathFailures++; return true; }
            search++; heapCount = 0; stamp[start] = search; cost[start] = 0; parent[start] = -1; Push(start, Heuristic(start, goal));
            while (heapCount > 0)
            {
                int current = Pop(); closed[current] = search;
                if (current == goal) { for (int n = goal; n >= 0; n = parent[n]) result.Add(Center(n)); result.Reverse(); return true; }
                int x = current % width, y = current / width;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    if ((dx == 0 && dy == 0) || x + dx < 0 || y + dy < 0 || x + dx >= width || y + dy >= height) continue;
                    int next = current + dy * width + dx;
                    if (!walkable[next] || closed[next] == search || !Fits(Center(next), AgentRadius)) continue;
                    if (dx != 0 && dy != 0 && (!walkable[current + dx] || !walkable[current + dy * width])) continue;
                    float g = cost[current] + (dx != 0 && dy != 0 ? 1.414214f : 1f);
                    if (stamp[next] == search && g >= cost[next]) continue;
                    bool fresh = stamp[next] != search; stamp[next] = search; cost[next] = g; parent[next] = current;
                    float f = g + Heuristic(next, goal); if (fresh) Push(next, f); else { priority[next] = f; Up(heapPosition[next]); }
                }
            }
            PathFailures++; return true;
        }
        float Heuristic(int a, int b) { int dx = Mathf.Abs(a % width - b % width), dy = Mathf.Abs(a / width - b / width); return Mathf.Max(dx, dy) + 0.414214f * Mathf.Min(dx, dy); }
        void Push(int n, float f) { priority[n] = f; heapPosition[n] = heapCount; heap[heapCount++] = n; Up(heapCount - 1); }
        void Swap(int a, int b) { int n = heap[a]; heap[a] = heap[b]; heap[b] = n; heapPosition[heap[a]] = a; heapPosition[heap[b]] = b; }
        void Up(int p) { while (p > 0) { int q = (p - 1) / 2; if (priority[heap[q]] <= priority[heap[p]]) break; Swap(p, q); p = q; } }
        int Pop() { int n = heap[0]; heapCount--; if (heapCount > 0) { heap[0] = heap[heapCount]; heapPosition[heap[0]] = 0; int p = 0; while (p * 2 + 1 < heapCount) { int q = p * 2 + 1; if (q + 1 < heapCount && priority[heap[q + 1]] < priority[heap[q]]) q++; if (priority[heap[p]] <= priority[heap[q]]) break; Swap(p, q); p = q; } } return n; }
    }
    public sealed class SlicePathFollower
    {
        readonly SliceNavigationGrid grid;
        readonly List<Vector2> path = new List<Vector2>(128);
        Vector2 goal; int corner; bool hasGoal; float retryAfter;
        public SlicePathFollower(SliceNavigationGrid grid) { this.grid = grid; }
        public Vector2 Resolve(Vector2 position, Vector2 destination)
            => ResolveCore(position, destination, Time.time, false);
#if UNITY_EDITOR
        public Vector2 ResolveForValidation(Vector2 position, Vector2 destination, float time)
            => ResolveCore(position, destination, time, true);
#endif
        Vector2 ResolveCore(Vector2 position, Vector2 destination, float now, bool validationOnly)
        {
            if ((destination - goal).sqrMagnitude > 0.1f) { hasGoal = false; path.Clear(); corner = 0; goal = destination; retryAfter = 0; }
            if (!hasGoal && now >= retryAfter) { if (!grid.TryPath(position, destination, path, validationOnly)) return position; hasGoal = true; retryAfter = now + 2f; }
            if (path.Count == 0) { if (now >= retryAfter) hasGoal = false; return position; }
            while (corner < path.Count - 1 && (position - path[corner]).sqrMagnitude < 0.3f) corner++;
            int furthest = corner;
            for (int i = corner + 1; i < Mathf.Min(path.Count, corner + 10); i++) { if (!grid.ClearLine(position, path[i])) break; furthest = i; }
            // Commit the look-ahead progress. Otherwise a cut corner can keep targeting a point already behind the walker.
            corner = furthest;
            if (corner == path.Count - 1 && grid.ClearLine(position, destination)) return destination;
            return path[corner];
        }
    }
}
