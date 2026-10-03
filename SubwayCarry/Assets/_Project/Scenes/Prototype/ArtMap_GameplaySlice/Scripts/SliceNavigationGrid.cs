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
        readonly SliceFloorDiamond[] floor;
        readonly SliceSolidFootprint[] solids;
        readonly Rect[] obstacles;
        readonly List<int>[] floorBuckets, solidBuckets;
        readonly int[] parent, stamp, closed, heap, heapPosition;
        readonly float[] cost, priority;
        int search, heapCount, budgetFrame = -1, pathsThisFrame;
        public int PathRequests { get; private set; }
        public int PathFailures { get; private set; }
        public int WalkableCellCount { get; private set; }
        public System.Func<Vector2, Vector2, float, bool> PassageAllowed;
        public readonly List<Vector2> SamplePoints = new List<Vector2>();
        public SliceNavigationGrid(Rect bounds, SliceFloorDiamond[] floor, Rect[] obstacles, SliceWallBoundary[] wallBoundaries = null, float agentRadius = 0.23f, SliceSolidFootprint[] solids = null)
        {
            AgentRadius = agentRadius;
            this.floor = floor;
            this.solids = solids ?? System.Array.Empty<SliceSolidFootprint>();
            this.obstacles = obstacles;
            walls = wallBoundaries ?? System.Array.Empty<SliceWallBoundary>();
            Bounds = bounds; width = Mathf.CeilToInt(bounds.width / CellSize); height = Mathf.CeilToInt(bounds.height / CellSize);
            int n = width * height;
            walkable = new bool[n]; mainArea = new bool[n]; parent = new int[n]; stamp = new int[n]; closed = new int[n];
            heap = new int[n]; heapPosition = new int[n]; cost = new float[n]; priority = new float[n];
            floorBuckets = new List<int>[n]; solidBuckets = new List<int>[n];
            for (int i = 0; i < floor.Length; i++) AddToBuckets(floorBuckets, i, floor[i].center, floor[i].halfSize);
            for (int i = 0; i < this.solids.Length; i++)
            {
                var s = this.solids[i];
                AddToBuckets(solidBuckets, i, s.center, new Vector2(s.halfSize.x, s.halfSize.y + Mathf.Abs(s.slope) * s.halfSize.x) + Vector2.one * (agentRadius + CellSize));
            }
            for (int i = 0; i < n; i++)
            {
                bool clear = Fits(Center(i), 0);
                walkable[i] = clear;
                if (clear) WalkableCellCount++;
            }
            FindMainArea();
        }
        void AddToBuckets(List<int>[] buckets, int value, Vector2 center, Vector2 half)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt((center.x - half.x - Bounds.xMin) / CellSize));
            int x1 = Mathf.Min(width - 1, Mathf.FloorToInt((center.x + half.x - Bounds.xMin) / CellSize));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((center.y - half.y - Bounds.yMin) / CellSize));
            int y1 = Mathf.Min(height - 1, Mathf.FloorToInt((center.y + half.y - Bounds.yMin) / CellSize));
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
            { int at = y * width + x; if (buckets[at] == null) buckets[at] = new List<int>(4); buckets[at].Add(value); }
        }
        bool OnFloor(Vector2 p)
        {
            int at = Index(p); if (at < 0) return false;
            if (floor.Length == 0) return true;
            var bucket = floorBuckets[at]; if (bucket == null) return false;
            for (int i = 0; i < bucket.Count; i++)
            { var f = floor[bucket[i]]; var d = p - f.center; if (Mathf.Abs(d.x) / f.halfSize.x + Mathf.Abs(d.y) / f.halfSize.y <= 1) return true; }
            return false;
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
            // A* uses coarse cells; final movement uses continuous foot geometry, never the rounded cell edge.
            int at = Index(p); if (at < 0) return false;
            var bucket = solidBuckets[at];
            if (bucket != null) for (int i = 0; i < bucket.Count; i++) if (solids[bucket[i]].Contains(p, r)) return false;
            for (int i = 0; i < obstacles.Length; i++)
            { Rect b = obstacles[i]; b.xMin -= r; b.xMax += r; b.yMin -= r; b.yMax += r; if (b.Contains(p)) return false; }
            float diagonal = r * .707107f;
            return OnFloor(p) && OnFloor(p + Vector2.right * r) && OnFloor(p - Vector2.right * r) &&
                OnFloor(p + Vector2.up * r) && OnFloor(p - Vector2.up * r) &&
                OnFloor(p + new Vector2(diagonal, diagonal)) && OnFloor(p - new Vector2(diagonal, diagonal)) &&
                OnFloor(p + new Vector2(diagonal, -diagonal)) && OnFloor(p + new Vector2(-diagonal, diagonal));
        }
        public Vector2 Nearest(Vector2 p)
        {
            int index = Index(p);
            if (index >= 0 && Fits(p, AgentRadius))
            {
                if (mainArea[index]) return p;
                // A legal continuous landing can share a coarse cell with an invalid cell centre.
                // Preserve it when connected to an adjacent navigation cell instead of shifting the visible entrance.
                int x = index % width, y = index / width;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    if (x+dx<0 || x+dx>=width || y+dy<0 || y+dy>=height) continue;
                    int next = index + dy*width + dx;
                    if (mainArea[next] && ClearLine(p,Center(next))) return p;
                }
            }
            float best = float.PositiveInfinity; Vector2 result = Bounds.center;
            for (int i = 0; i < walkable.Length; i++) { if (!mainArea[i]) continue; Vector2 q = Center(i); float d = (q - p).sqrMagnitude; if (d < best && Fits(q, AgentRadius)) { best = d; result = q; } }
            return result;
        }
        public Vector2 Constrain(Vector2 from, Vector2 to, float radius)
        {
            Vector2 delta = to - from;
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / .08f));
            Vector2 step = delta / steps, p = from;
            for (int i = 0; i < steps; i++)
            {
                Vector2 next = p + step;
                if (Fits(next, radius) && (PassageAllowed == null || PassageAllowed(p, next, radius))) { p = next; continue; }
                // Slide along the two projected floor axes before screen X/Y, preserving diagonal walls.
                Vector2 best = p; float progress = 0;
                TrySlide(p, step, new Vector2(1, .5f).normalized, radius, ref best, ref progress);
                TrySlide(p, step, new Vector2(-1, .5f).normalized, radius, ref best, ref progress);
                TrySlide(p, step, Vector2.right, radius, ref best, ref progress);
                TrySlide(p, step, Vector2.up, radius, ref best, ref progress);
                p = best;
            }
            return p;
        }
        void TrySlide(Vector2 p, Vector2 step, Vector2 tangent, float radius, ref Vector2 best, ref float progress)
        {
            Vector2 slide = tangent * Vector2.Dot(step, tangent);
            float score = Vector2.Dot(slide, step);
            if (score > progress && Fits(p + slide, radius) && (PassageAllowed == null || PassageAllowed(p, p + slide, radius))) { best = p + slide; progress = score; }
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
