using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using SubwayCarry.AI.V2;

namespace SubwayCarry.Prototype.ArtMapSlice.Editor
{
    public static class SliceStaticValidator
    {
        public static string Validate()
        {
            var scene = EditorSceneManager.OpenPreviewScene(SliceSceneBuilder.ScenePath);
            try
            {
                SliceGameController controller = null;
                foreach (var root in scene.GetRootGameObjects()) if (root.TryGetComponent(out SliceGameController candidate)) controller = candidate;
                if (controller == null) throw new InvalidOperationException("Slice controller missing");
                var failures = new List<string>(); var lines = new List<string>();
                if (controller.playerPrefab == null || controller.packagePrefab == null || controller.baselineProfile == null) failures.Add("Missing prefab/profile reference");
                if (controller.activitySettings == null) failures.Add("Missing contextual activity settings");
                if (controller.idleSprites.Length != 8 || controller.walkSprites.Length != 24) failures.Add("Incomplete 8-direction sprite set");
                foreach (var sprite in controller.idleSprites) if (sprite == null) failures.Add("Null idle sprite");
                foreach (var sprite in controller.walkSprites) if (sprite == null) failures.Add("Null walk sprite");
                if (controller.playerOutline == null || ShaderUtil.ShaderHasError(controller.playerOutline.shader)) failures.Add("Outline shader error");
                foreach (var map in controller.maps)
                {
                    map.EnsureGrid(); var path = new List<Vector2>(); int tested = 0, boundaryChecks = 0;
                    Action<Vector2, string> check = (target, label) =>
                    {
                        tested++;
                        if (!map.Grid.Fits(target, map.characterRadius)) failures.Add(map.displayName + " non-walkable " + label + " " + target);
                        map.Grid.TryPath(map.entry, target, path, true);
                        if (path.Count == 0) failures.Add(map.displayName + " unreachable " + label + " " + target);
                        else
                        {
                            // Numerically advance a point along the adapter; this is not an Agent/physics Play test.
                            var follower = new SlicePathFollower(map.Grid); Vector2 p = map.entry;
                            int maxSteps = Mathf.Max(500, path.Count * 30);
                            for (int step = 0; step < maxSteps && Vector2.Distance(p, target) > 0.44f; step++)
                            {
                                Vector2 steering = follower.ResolveForValidation(p, target, step * 0.05f);
                                p = map.Grid.Constrain(p, Vector2.MoveTowards(p, steering, 0.06f), map.characterRadius);
                            }
                            if (Vector2.Distance(p, target) > 0.44f) failures.Add(map.displayName + " path follower stalled " + label + " at " + p);
                        }
                    };
                    check(map.entry, "entry");
                    for (int i = 0; i < map.exits.Length; i++)
                    {
                        check(map.exits[i].inside, "exit " + i);
                        bool outside = !map.ContainsFloor(map.exits[i].outside);
                        if (!outside) failures.Add(map.displayName + " exit not outside floor " + i);
                        var exit = map.exits[i]; int steps = Mathf.CeilToInt(Vector2.Distance(exit.inside, exit.outside) / 0.1f);
                        for (int step = 0; step <= steps; step++)
                        {
                            Vector2 p = Vector2.Lerp(exit.inside, exit.outside, step / (float)Mathf.Max(1, steps));
                            foreach (var wall in map.walls) if (!wall.Allows(p, map.characterRadius)) failures.Add(map.displayName + " exit crosses closed wall " + i);
                            foreach (var solid in map.solidFootprints) if (solid.Contains(p, map.characterRadius)) failures.Add(map.displayName + " exit crosses furniture " + i);
                        }
                    }
                    for (int i = 0; i < map.portals.Length; i++)
                    {
                        check(map.portals[i].position, "portal " + i);
                        var p = map.portals[i];
                        if (p.targetMap < 0 || p.targetMap >= controller.maps.Length) failures.Add("Bad portal target");
                        else { var target = controller.maps[p.targetMap]; target.EnsureGrid(); if (!target.Grid.Fits(p.arrival, target.characterRadius)) failures.Add("Portal arrival outside floor"); }
                    }
                    for (int i = 0; i < map.interests.Length; i++) check(map.interests[i].position, "interest " + i);
                    foreach (var wall in map.walls)
                    {
                        Vector2 tangent = new Vector2(-wall.inwardNormal.y, wall.inwardNormal.x);
                        for (int n = -20; n <= 20; n++)
                        {
                            Vector2 behind = wall.point + tangent * n * 0.5f - wall.inwardNormal * 0.1f;
                            boundaryChecks++;
                            if (map.Grid.Fits(behind, map.characterRadius)) failures.Add(map.displayName + " closed wall allows entry");
                        }
                    }
                    if (map.walls.Length == 0) failures.Add(map.displayName + " no closed wall boundary");
                    var solidsRoot = map.transform.Find("SolidFootprints");
                    if (solidsRoot == null || solidsRoot.GetComponentsInChildren<PolygonCollider2D>(true).Length != map.solidFootprints.Length) failures.Add(map.displayName + " collider/footprint mismatch");
                    foreach (var renderer in map.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        if (renderer.name.Equals("Floor", StringComparison.OrdinalIgnoreCase)) continue;
                        var group = renderer.GetComponentInParent<SortingGroup>(true);
                        if (group == null) failures.Add(map.displayName + " ungrouped art " + renderer.name);
                        if (renderer.name.Contains("door_fixed") || renderer.name.Contains("window"))
                        {
                            var backing = renderer.transform.Find("OpaqueClosedSurface");
                            if (backing == null) failures.Add(map.displayName + " missing opaque backing " + renderer.name);
                            else
                            {
                                var mesh = backing.GetComponent<MeshFilter>().sharedMesh; var mr = backing.GetComponent<MeshRenderer>();
                                if (mesh == null || mesh.vertexCount < 3 || mr.sharedMaterial == null || ShaderUtil.ShaderHasError(mr.sharedMaterial.shader)) failures.Add("Bad closed surface asset");
                            }
                        }
                    }
                    foreach (var marker in map.GetComponentsInChildren<LineRenderer>(true)) if (marker.sortingOrder != -29000) failures.Add("Portal marker renders over art");
                    if (map.characterScale < 0.75f || map.characterScale > 1.2f) failures.Add("Unexpected human scale");
                    ValidatePlaceProvider(map, failures, lines);
                    lines.Add(map.displayName + ": route checks=" + tested + ", blocked-wall probes=" + boundaryChecks + ", scale=" + map.characterScale + ", solid footprints=" + map.solidFootprints.Length);
                }
                return string.Join("\n", lines) + "\nFAILURES=" + failures.Count + "\n" + string.Join("\n", failures);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        static void ValidatePlaceProvider(SliceMap map, List<string> failures, List<string> lines)
        {
            // A preview-scene-only adapter; no actual agents, movement, art edits or Play mode.
            map.gameObject.SetActive(true);
            var holder = new GameObject("PlaceProvider_Validation_Only"); holder.transform.SetParent(map.transform, false);
            try
            {
                var crowd = holder.AddComponent<PassengerAiV2CrowdManager>();
                var spots = new PassengerAiV2InteriorSpotSmartObject[map.interests.Length];
                for (int i = 0; i < spots.Length; i++)
                {
                    var go = new GameObject("Facility_Validation_Only"); go.transform.SetParent(holder.transform, false);
                    spots[i] = go.AddComponent<PassengerAiV2InteriorSpotSmartObject>(); var d = map.interests[i];
                    spots[i].Configure("validation_" + i, d.kind, d.position, d.comfort, 0, null);
                }
                var place = holder.AddComponent<SlicePlaceEnvironment>(); place.Configure(map, crowd, spots);
                int seats = 0, lean = 0, stand = 0, prepare = 0;
                foreach (var s in place.Spots)
                {
                    if (place.CanUse(s, true)) prepare++;
                    if (!place.CanUse(s, false)) continue;
                    if (s.Kind == PassengerAiV2InteriorSpotKind.Seat) seats++;
                    else if (s.Kind == PassengerAiV2InteriorSpotKind.Lean) lean++;
                    else stand++;
                }
                int declaredSeats = 0, declaredSupports = 0;
                foreach (var s in spots) { if (s.Kind == PassengerAiV2InteriorSpotKind.Seat) declaredSeats++; if (s.Kind == PassengerAiV2InteriorSpotKind.Lean) declaredSupports++; }
                // A new map need not contain every facility kind. Only declared facilities must be usable.
                if (declaredSeats > 0 && seats == 0) failures.Add(map.displayName + " declared seats are unusable");
                if (declaredSupports > 0 && lean == 0) failures.Add(map.displayName + " declared supports are unusable");
                if (prepare == 0) failures.Add(map.displayName + " missing door preparation positions");
                if (!place.TryFindStandingPosition(map.entry, null, out Vector2 standing) || !place.CanStand(standing, null)) failures.Add(map.displayName + " no safe standing fallback");
                foreach (var p in map.portals) if (place.CanStand(p.position, null)) failures.Add(map.displayName + " allows ordinary waiting in doorway");
                foreach (var e in map.exits) if (place.CanStand(e.inside, null)) failures.Add(map.displayName + " allows ordinary waiting at exit");
                lines.Add(map.displayName + ": context=" + place.Context.Kind + ", usable seat/lean/stand/prepare=" + seats + "/" + lean + "/" + stand + "/" + prepare);
            }
            finally { UnityEngine.Object.DestroyImmediate(holder); }
        }
    }
}
