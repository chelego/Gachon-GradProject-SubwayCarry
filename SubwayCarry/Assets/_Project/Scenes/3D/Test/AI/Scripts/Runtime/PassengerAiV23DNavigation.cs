using System;
using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.AI.V2
{
    /// <summary>Shared obstacle visibility graph. At most four queued A* requests per frame;
    /// a passenger reuses its waypoints until the destination or visibility changes.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class PassengerAiV23DNavigation : MonoBehaviour
    {
        [SerializeField] private Rect[] obstacles = Array.Empty<Rect>();
        [SerializeField] private Rect bounds = new Rect(-18, -3.5f, 36, 7);
        private Vector2[] nodes;
        private float[,] edges;
        private readonly Queue<Route> requests = new Queue<Route>();
        public int ObstacleCount => obstacles.Length;
        public int PathCount { get; private set; }
        public int FailedPathCount { get; private set; }
#if UNITY_EDITOR
        public bool CheckRoute(Vector2 start, Vector2 target)
        {
            if(nodes==null) BuildGraph();
            if(IsClear(start,target)) return true;
            var route=new Route(this) {start=start,goal=target}; Solve(route);
            if(route.path.Count==0) return false;
            Vector2 previous=start;
            foreach(Vector2 waypoint in route.path) { if(!IsClear(previous,waypoint)) return false; previous=waypoint; }
            return (previous-target).sqrMagnitude<.0001f;
        }
#endif
        public void Configure(Rect worldBounds, Rect[] solidFootprints)
        { bounds = worldBounds; obstacles = solidFootprints; nodes = null; }
        public void Bind(PassengerAiV2Agent agent)
        {
            if (nodes == null) BuildGraph();
            var route = new Route(this);
            agent.ConfigureMovementSpace(route.Resolve, null, .28f);
        }
        private Rect Expanded(int index, float amount = .38f)
        { Rect r = obstacles[index]; r.xMin -= amount; r.xMax += amount; r.yMin -= amount; r.yMax += amount; return r; }
        public bool IsClear(Vector2 a, Vector2 b)
        {
            for (int i = 0; i < obstacles.Length; i++)
                if (Intersects(a, b, Expanded(i, .34f))) return false;
            return true;
        }
        private static bool Intersects(Vector2 a, Vector2 b, Rect r)
        {
            Vector2 d = b - a;
            float low = 0, high = 1;
            for (int axis = 0; axis < 2; axis++)
            {
                float v = axis == 0 ? a.x : a.y, step = axis == 0 ? d.x : d.y;
                float min = axis == 0 ? r.xMin : r.yMin, max = axis == 0 ? r.xMax : r.yMax;
                if (Mathf.Abs(step) < .00001f) { if (v < min || v > max) return false; }
                else
                {
                    float t0 = (min - v) / step, t1 = (max - v) / step;
                    low = Mathf.Max(low, Mathf.Min(t0, t1)); high = Mathf.Min(high, Mathf.Max(t0, t1));
                    if (low > high) return false;
                }
            }
            return high >= 0 && low <= 1;
        }
        private void BuildGraph()
        {
            var points = new List<Vector2>();
            for (int i = 0; i < obstacles.Length; i++)
            {
                Rect r = Expanded(i);
                foreach (Vector2 p in new[] {new Vector2(r.xMin,r.yMin),new Vector2(r.xMin,r.yMax),
                    new Vector2(r.xMax,r.yMin),new Vector2(r.xMax,r.yMax)})
                {
                    if (!bounds.Contains(p)) continue;
                    bool blocked = false;
                    for (int j = 0; j < obstacles.Length; j++) if (j != i && Expanded(j, .34f).Contains(p)) blocked = true;
                    if (!blocked) points.Add(p);
                }
            }
            nodes = points.ToArray(); edges = new float[nodes.Length, nodes.Length];
            for (int a = 0; a < nodes.Length; a++) for (int b = 0; b < nodes.Length; b++)
                edges[a,b] = a != b && IsClear(nodes[a], nodes[b]) ? Vector2.Distance(nodes[a],nodes[b]) : float.PositiveInfinity;
        }
        private void Update()
        {
            for (int budget = 0; budget < 4 && requests.Count > 0; budget++)
            { Route route = requests.Dequeue(); route.queued = false; Solve(route); }
        }
        private void Solve(Route route)
        {
            PathCount++;
            int n = nodes.Length;
            float[] g = route.cost; int[] prev = route.previous; bool[] closed = route.closed;
            for (int i = 0; i < n; i++)
            { g[i] = IsClear(route.start, nodes[i]) ? Vector2.Distance(route.start,nodes[i]) : float.PositiveInfinity; prev[i] = -1; closed[i] = false; }
            int last = -1; float best = float.PositiveInfinity;
            for (int step = 0; step < n; step++)
            {
                int at = -1; float priority = float.PositiveInfinity;
                for (int i = 0; i < n; i++) if (!closed[i] && g[i] + Vector2.Distance(nodes[i],route.goal) < priority)
                { priority = g[i] + Vector2.Distance(nodes[i],route.goal); at = i; }
                if (at < 0 || priority >= best) break;
                closed[at] = true;
                if (IsClear(nodes[at], route.goal)) { best = g[at] + Vector2.Distance(nodes[at],route.goal); last = at; }
                for (int j = 0; j < n; j++) if (g[at] + edges[at,j] < g[j])
                { g[j] = g[at] + edges[at,j]; prev[j] = at; }
            }
            route.path.Clear();
            if (last >= 0)
            {
                for (int at = last; at >= 0; at = prev[at]) route.path.Add(nodes[at]);
                route.path.Reverse(); route.path.Add(route.goal);
            }
            else FailedPathCount++;
        }
        private sealed class Route
        {
            private readonly PassengerAiV23DNavigation nav;
            public readonly List<Vector2> path = new List<Vector2>(32);
            public readonly float[] cost; public readonly int[] previous; public readonly bool[] closed;
            public Vector2 start, goal;
            public bool queued;
            private float retryAt;
            public Route(PassengerAiV23DNavigation owner)
            { nav = owner; cost = new float[owner.nodes.Length]; previous = new int[cost.Length]; closed = new bool[cost.Length]; }
            public Vector2 Resolve(Vector2 position, Vector2 destination)
            {
                // Facility use anchors can be outside the solid but inside the navigation
                // clearance margin. Route to the reachable approach, leaving the Smart Object's
                // actual use anchor untouched for its final reservation/contact alignment.
                for(int i=0;i<nav.obstacles.Length;i++)
                {
                    Rect r=nav.Expanded(i,.34f);
                    if(!r.Contains(destination)||nav.obstacles[i].Contains(destination)) continue;
                    Vector2 approach=new Vector2(r.xMin-.04f,destination.y); float nearest=Mathf.Abs(destination.x-approach.x);
                    for(int face=0;face<3;face++)
                    {
                        Vector2 p=face==0?new Vector2(r.xMax+.04f,destination.y):face==1?new Vector2(destination.x,r.yMin-.04f):new Vector2(destination.x,r.yMax+.04f);
                        float d=Vector2.Distance(destination,p); if(d<nearest) {nearest=d;approach=p;}
                    }
                    destination=approach;
                }
                // Local avoidance may have nudged a body into the inflated planning margin.
                // Recover to the closest outer face without teleporting or changing its goal.
                for (int i = 0; i < nav.obstacles.Length; i++)
                {
                    Rect r = nav.Expanded(i, .345f);
                    if (!r.Contains(position)) continue;
                    Vector2 escape = new Vector2(r.xMin-.04f,position.y); float nearest = Mathf.Abs(position.x-escape.x);
                    for(int face=0;face<3;face++)
                    {
                        Vector2 p=face==0?new Vector2(r.xMax+.04f,position.y):face==1?new Vector2(position.x,r.yMin-.04f):new Vector2(position.x,r.yMax+.04f);
                        float d=Vector2.Distance(position,p); if(d<nearest) {nearest=d;escape=p;}
                    }
                    return escape;
                }
                if (nav.IsClear(position, destination)) { path.Clear(); goal = destination; return destination; }
                bool changed = (goal-destination).sqrMagnitude > .04f;
                if (changed) { path.Clear(); goal=destination; }
                start = position;
                if (path.Count > 0)
                {
                    for (int i = path.Count-1; i >= 0; i--) if (nav.IsClear(position,path[i]))
                    { if (i > 0) path.RemoveRange(0,i); return path[0]; }
                }
                if (!queued && Time.time >= retryAt)
                { nav.requests.Enqueue(this); queued=true; retryAt=Time.time+.5f; }
                return position;
            }
        }
    }
}
