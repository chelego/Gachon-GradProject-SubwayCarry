using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace SubwayCarry.Prototype.ArtMapSlice.Editor
{
    // Repairs only the integrated copy. Source transforms, art and teammate prefabs remain untouched.
    public static class SlicePresentationRepair
    {
        const string Generated = SliceSceneBuilder.Root + "/ClosedSurfaceMeshes";
        static readonly Dictionary<Sprite, Vector2[]> outlines = new Dictionary<Sprite, Vector2[]>();

        [MenuItem("SubwayCarry/Art Map Slice/Repair Occlusion And Human Scale")]
        public static string ApplyToSavedScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before applying scene data.");
            Scene scene = SceneManager.GetSceneByPath(SliceSceneBuilder.ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("Save your scene edits first; repair will not save unrelated unsaved edits.");
            if (opened) scene = EditorSceneManager.OpenScene(SliceSceneBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                SliceGameController controller = null;
                foreach (var root in scene.GetRootGameObjects()) if (root.TryGetComponent(out SliceGameController c)) controller = c;
                if (controller == null) throw new InvalidOperationException("Missing slice controller.");
                Apply(controller);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
                AssetDatabase.SaveAssets();
                return "Repaired integrated scene only. No Play Mode or visual test performed.";
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); outlines.Clear(); }
        }

        public static void Apply(SliceGameController controller)
        {
            if (!AssetDatabase.IsValidFolder(Generated)) AssetDatabase.CreateFolder(SliceSceneBuilder.Root, "ClosedSurfaceMeshes");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(SliceSceneBuilder.Root + "/Scripts/ClosedSurface.shader");
            if (shader == null) throw new InvalidOperationException("Closed-surface shader missing.");
            string materialPath = Generated + "/OpaqueBacking.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
            for (int i = 0; i < controller.maps.Length; i++) RepairMap(controller.maps[i], i == 1, material);
            foreach (var map in controller.maps)
                for (int i = 0; i < map.portals.Length; i++)
                {
                    var p = map.portals[i]; p.arrival = controller.maps[p.targetMap].Grid.Nearest(p.arrival); map.portals[i] = p;
                }
        }

        static void RepairMap(SliceMap map, bool train, Material material)
        {
            map.depthSlope = train ? 0 : 0.5f;
            map.characterScale = train ? 0.78f : 1.1f;
            map.characterRadius = train ? 0.23f : 0.28f;
            var sprites = map.GetComponentsInChildren<SpriteRenderer>(true);
            var frames = new List<SpriteRenderer>();
            var walls = new List<SliceWallBoundary>();
            var solids = new List<SliceSolidFootprint>();
            for (int i = map.transform.childCount - 1; i >= 0; i--)
            {
                var child = map.transform.GetChild(i);
                if (child.name.StartsWith("Footprint_", StringComparison.Ordinal)) UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            Transform solidRoot = map.transform.Find("SolidFootprints");
            if (solidRoot == null) { var go = new GameObject("SolidFootprints"); go.transform.SetParent(map.transform, false); solidRoot = go.transform; }
            for (int i = solidRoot.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(solidRoot.GetChild(i).gameObject);
            foreach (var r in sprites) if (r.name.ToLowerInvariant().Contains("door_fixed")) frames.Add(r);
            var assigned = new HashSet<SpriteRenderer>();
            foreach (var frame in frames)
            {
                Vector2 feet = GroundFoot(frame, map.depthSlope);
                var group = GroupFor(frame, map, "ClosedDoor_", feet);
                AddBacking(frame, material);
                frame.sortingOrder = 2;
                foreach (var leaf in sprites)
                {
                    string name = leaf.name.ToLowerInvariant();
                    bool isLeaf = train ? name.StartsWith("subway_door_leaf") : name.StartsWith("screen_door_leaf");
                    if (!isLeaf || assigned.Contains(leaf)) continue;
                    SpriteRenderer closest = null; float best = float.PositiveInfinity;
                    foreach (var candidate in frames)
                    {
                        float d = ((Vector2)leaf.bounds.center - (Vector2)candidate.bounds.center).sqrMagnitude;
                        if (d < best) { best = d; closest = candidate; }
                    }
                    if (closest != frame || best > 12f) continue;
                    leaf.transform.SetParent(group.transform, true); leaf.sortingOrder = 1; assigned.Add(leaf);
                }
                if (!train) AddWall(walls, feet, map);
            }
            foreach (var r in sprites)
            {
                if (r.sprite == null || frames.Contains(r) || assigned.Contains(r) || r.name.Equals("Floor", StringComparison.OrdinalIgnoreCase)) continue;
                Vector2 feet = GroundFoot(r, map.depthSlope);
                var group = GroupFor(r, map, "Depth_", feet); r.sortingOrder = 0;
                string name = r.name.ToLowerInvariant();
                if (name.Contains("bench") || name.Contains("pillar") || name.Contains("vending") || name.StartsWith("subway_seat_"))
                {
                    var b = r.bounds;
                    var footprint = new SliceSolidFootprint {
                        center = train ? (Vector2)b.center : feet + Vector2.up * 0.3f,
                        halfSize = new Vector2(b.extents.x * (name.Contains("pillar") ? 0.7f : 0.8f), train ? 0.22f : 0.3f),
                        slope = map.depthSlope
                    };
                    solids.Add(footprint);
                    var go = new GameObject("Footprint_" + solids.Count + "_" + r.name); go.transform.SetParent(solidRoot, false);
                    var poly = go.AddComponent<PolygonCollider2D>(); float x = footprint.halfSize.x, y = footprint.halfSize.y, rise = x * footprint.slope;
                    var vertices = new[] { new Vector2(-x, -rise-y), new Vector2(x, rise-y), new Vector2(x, rise+y), new Vector2(-x, -rise+y) };
                    for (int n = 0; n < vertices.Length; n++) vertices[n] = go.transform.InverseTransformPoint(footprint.center + vertices[n]);
                    poly.points = vertices; go.AddComponent<SliceStaticImpactSource>();
                }
                if (r.name.ToLowerInvariant().Contains("window"))
                {
                    AddBacking(r, material);
                    if (!train) AddWall(walls, feet, map);
                }
            }
            if (train)
            {
                walls.Add(new SliceWallBoundary { point = new Vector2(map.floorBounds.xMin, 0), inwardNormal = Vector2.right });
                walls.Add(new SliceWallBoundary { point = new Vector2(map.floorBounds.xMax, 0), inwardNormal = Vector2.left });
            }
            map.walls = walls.ToArray();
            map.solidFootprints = solids.ToArray();
            // Replace the original generated box approximations, not user-authored additional obstacles.
            // The previous builder's obstacles correspond one-to-one with its Footprint_ objects.
            if (map.obstacles.Length == solids.Count) map.obstacles = Array.Empty<Rect>();
            var boundaryRoot = map.transform.Find("ClosedWallBoundaries");
            if (boundaryRoot == null) { var go = new GameObject("ClosedWallBoundaries"); go.transform.SetParent(map.transform, false); boundaryRoot = go.transform; }
            // Only repair-generated colliders are replaced; user art and existing colliders are retained.
            for (int i = boundaryRoot.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(boundaryRoot.GetChild(i).gameObject);
            for (int i = 0; i < walls.Count; i++)
            {
                var w = walls[i]; var go = new GameObject("ClosedWall_" + i); go.transform.SetParent(boundaryRoot, false);
                Vector2 tangent = new Vector2(-w.inwardNormal.y, w.inwardNormal.x);
                float length = map.floorBounds.width + map.floorBounds.height;
                var edge = go.AddComponent<EdgeCollider2D>(); edge.edgeRadius = 0.04f / map.transform.lossyScale.x;
                edge.points = new[] { (Vector2)go.transform.InverseTransformPoint(w.point - tangent * length), (Vector2)go.transform.InverseTransformPoint(w.point + tangent * length) };
                go.AddComponent<SliceStaticImpactSource>();
            }
            map.RebuildGrid();
            if (map.Grid.SamplePoints.Count == 0) throw new InvalidOperationException("Closed walls leave no floor in " + map.displayName);
            map.entry = map.Grid.Nearest(map.entry);
            for (int i = 0; i < map.interests.Length; i++) { var p = map.interests[i]; p.position = map.Grid.Nearest(p.position); map.interests[i] = p; }
            for (int i = 0; i < map.portals.Length; i++)
            {
                var p = map.portals[i]; p.position = map.Grid.Nearest(p.position); map.portals[i] = p;
                // Floor markings must never render on top of people, doors or furniture.
                var marker = map.transform.Find("Portal_" + i + "_To_" + p.label);
                if (marker != null) { marker.position = p.position; marker.GetComponent<LineRenderer>().sortingOrder = -29000; }
            }
            Vector2 direction = train ? Vector2.up : new Vector2(1, map.depthSlope).normalized;
            map.exits = new[] { SafeExit(map, direction), SafeExit(map, -direction) };
        }

        static SliceExit SafeExit(SliceMap map, Vector2 direction)
        {
            Vector2 start = map.entry; float best = float.NegativeInfinity;
            // Choose an open end, not a point that stops early at a bench and then walks through it.
            foreach (Vector2 sample in map.Grid.SamplePoints)
            {
                float score = Vector2.Dot(sample, direction);
                if (score <= best || !ExitRayClear(map, sample, direction)) continue;
                best = score; start = sample;
            }
            if (float.IsNegativeInfinity(best)) throw new InvalidOperationException("No obstacle-free exit: " + map.displayName);
            Vector2 p = start;
            for (int i = 0; i < 1000; i++) { Vector2 next = p + direction * 0.1f; if (!map.Grid.Fits(next, map.characterRadius)) break; p = next; }
            Vector2 inside = map.Grid.Nearest(p), outside = inside;
            for (int i = 0; i < 1500; i++) { outside += direction * 0.1f; if (!map.ContainsFloor(outside)) { outside += direction; break; } }
            return new SliceExit { inside = inside, outside = outside };
        }

        static bool ExitRayClear(SliceMap map, Vector2 start, Vector2 direction)
        {
            float length = map.floorBounds.width + map.floorBounds.height;
            for (float d = 0; d < length; d += 0.2f)
            {
                Vector2 p = start + direction * d;
                foreach (var w in map.walls) if (!w.Allows(p, map.characterRadius)) return false;
                foreach (var f in map.solidFootprints) if (f.Contains(p, map.characterRadius)) return false;
                foreach (var r in map.obstacles) if (r.Contains(p)) return false;
                if (!map.floorBounds.Contains(p)) return true;
            }
            return false;
        }

        static void AddWall(List<SliceWallBoundary> walls, Vector2 feet, SliceMap map)
        {
            Vector2 normal = new Vector2(-map.depthSlope, 1).normalized;
            if (Vector2.Dot(map.floorBounds.center - feet, normal) < 0) normal = -normal;
            var wall = new SliceWallBoundary { point = feet, inwardNormal = normal };
            for (int i = 0; i < walls.Count; i++)
            {
                if (Vector2.Dot(walls[i].inwardNormal, normal) < 0.99f) continue;
                // Continuous platform wall: use the most conservative panel base, so tiny art seams are not openings.
                if (Vector2.Dot(feet - walls[i].point, normal) > 0) walls[i] = wall;
                return;
            }
            walls.Add(wall);
        }

        static SortingGroup GroupFor(SpriteRenderer sprite, SliceMap map, string prefix, Vector2 feet)
        {
            var group = sprite.transform.parent != null ? sprite.transform.parent.GetComponent<SortingGroup>() : null;
            if (group == null)
            {
                var root = new GameObject(prefix + sprite.name); root.transform.SetParent(sprite.transform.parent, false);
                group = root.AddComponent<SortingGroup>(); sprite.transform.SetParent(root.transform, true);
            }
            group.sortingLayerID = sprite.sortingLayerID; group.sortingOrder = map.GroundOrder(feet);
            return group;
        }

        static Vector2 GroundFoot(SpriteRenderer r, float slope)
        {
            var hull = Outline(r.sprite); float ground = float.PositiveInfinity;
            foreach (var vertex in hull) { Vector2 p = r.transform.TransformPoint(vertex); ground = Mathf.Min(ground, p.y - slope * p.x); }
            float x = r.bounds.center.x;
            return new Vector2(x, ground + slope * x);
        }

        static void AddBacking(SpriteRenderer r, Material material)
        {
            if (r.transform.Find("OpaqueClosedSurface") != null) return;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(r.sprite, out string guid, out long id);
            string path = Generated + "/Surface_" + guid + "_" + id + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                var hull = Outline(r.sprite); var vertices = new Vector3[hull.Length]; var triangles = new int[(hull.Length - 2) * 3];
                for (int i = 0; i < hull.Length; i++) vertices[i] = hull[i];
                for (int i = 0; i < hull.Length - 2; i++) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = i + 2; }
                mesh = new Mesh { name = "ClosedSurface_" + r.sprite.name, vertices = vertices, triangles = triangles }; mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, path);
            }
            var go = new GameObject("OpaqueClosedSurface"); go.transform.SetParent(r.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.sortingLayerID = r.sortingLayerID; renderer.sortingOrder = -2;
        }

        static Vector2[] Outline(Sprite sprite)
        {
            if (outlines.TryGetValue(sprite, out var cached)) return cached;
            // Asset-only alpha geometry extraction, not a screenshot or runtime visual test.
            var texture = new Texture2D(2, 2); var points = new List<Vector2>();
            try
            {
                texture.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
                var pixels = texture.GetPixels32(); Rect rect = sprite.rect;
                for (int x = (int)rect.xMin; x < (int)rect.xMax; x++)
                {
                    int bottom = -1, top = -1;
                    for (int y = (int)rect.yMin; y < (int)rect.yMax; y++) if (pixels[y * texture.width + x].a > 64) { if (bottom < 0) bottom = y; top = y; }
                    if (bottom < 0) continue;
                    points.Add((new Vector2(x - rect.xMin, bottom - rect.yMin) - sprite.pivot) / sprite.pixelsPerUnit);
                    points.Add((new Vector2(x + 1 - rect.xMin, top + 1 - rect.yMin) - sprite.pivot) / sprite.pixelsPerUnit);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            points.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var hull = new List<Vector2>();
            foreach (var p in points) { while (hull.Count > 1 && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 1]) <= 0) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
            int lower = hull.Count;
            for (int i = points.Count - 2; i >= 0; i--) { var p = points[i]; while (hull.Count > lower && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 1]) <= 0) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
            if (hull.Count > 1) hull.RemoveAt(hull.Count - 1);
            if (hull.Count < 3) throw new InvalidOperationException("No usable opaque silhouette: " + sprite.name);
            return outlines[sprite] = hull.ToArray();
        }
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
