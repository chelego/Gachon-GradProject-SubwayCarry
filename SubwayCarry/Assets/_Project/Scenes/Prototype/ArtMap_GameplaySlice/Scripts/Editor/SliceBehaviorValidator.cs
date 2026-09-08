using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SubwayCarry.AI.V2;

namespace SubwayCarry.Prototype.ArtMapSlice.Editor
{
    // Deterministic policy/action/path checks in an isolated preview scene. No Play mode, physics simulation or rendering.
    public static class SliceBehaviorValidator
    {
        sealed class Place : IPassengerAiV2Place
        {
            public event Action Changed;
            public PassengerAiV2PlaceContext context;
            public readonly List<PassengerAiV2InteriorSpotSmartObject> spots = new List<PassengerAiV2InteriorSpotSmartObject>();
            public PassengerAiV2PlaceContext Context => context;
            public IReadOnlyList<PassengerAiV2InteriorSpotSmartObject> Spots => spots;
            public bool obstructing;
            public int exitQueries;
            public bool CanUse(PassengerAiV2InteriorSpotSmartObject s, bool preparing) => s != null && s.isActiveAndEnabled && (preparing == (s.Kind == PassengerAiV2InteriorSpotKind.DoorPrepare));
            public bool CanStand(Vector2 p, PassengerAiV2Agent a) => true;
            public float CrowdCost(Vector2 p, PassengerAiV2Agent a) => 0;
            public bool IsObstructingFlow(Vector2 p, PassengerAiV2Agent a) => obstructing;
            public bool TryFindStandingPosition(Vector2 p, PassengerAiV2Agent a, out Vector2 result) { result = p + Vector2.right * 2; return true; }
            public bool TryGetExit(Vector2 p, out Vector2 result) { exitQueries++; result = p + Vector2.right * 10; return true; }
            public void Signal(PassengerAiV2ServicePhase phase, bool relevant, bool destination)
            { context.Service = phase; context.RelevantService = relevant; context.DestinationStop = destination; context.Revision++; Changed?.Invoke(); }
        }
        sealed class Fixture : IDisposable
        {
            public readonly Scene scene;
            public readonly GameObject root;
            public readonly PassengerAiV2CrowdManager crowd;
            public readonly PassengerAiV2Agent agent;
            public readonly PassengerAiV2ContextualActivity activity;
            public readonly PassengerAiV2ActivitySettings settings;
            public readonly Place place = new Place();
            public Fixture(Vector2 origin, PassengerAiV2LocalGoal goal)
            {
                scene = EditorSceneManager.NewPreviewScene(); root = new GameObject("ContextPolicy_Validation_Only");
                SceneManager.MoveGameObjectToScene(root, scene);
                crowd = root.AddComponent<PassengerAiV2CrowdManager>(); agent = AddAgent(origin);
                settings = ScriptableObject.CreateInstance<PassengerAiV2ActivitySettings>();
                place.context = new PassengerAiV2PlaceContext { Kind = PassengerAiV2PlaceKind.Platform, Service = PassengerAiV2ServicePhase.Waiting };
                activity = agent.gameObject.AddComponent<PassengerAiV2ContextualActivity>();
                activity.Initialize(agent, place, PassengerAiV2RuntimePersonality.CreateFallback(0), settings, goal, 0);
            }
            public PassengerAiV2Agent AddAgent(Vector2 p)
            {
                var go = new GameObject("Agent_Validation_Only"); go.transform.SetParent(root.transform);
                go.AddComponent<SpriteRenderer>(); go.AddComponent<CircleCollider2D>(); go.AddComponent<Rigidbody2D>();
                var a = go.AddComponent<PassengerAiV2Agent>();
                a.Initialize(crowd, p, p, PassengerAiV2RuntimePersonality.CreateFallback(0), 0, p.y - 20, p.y + 20, null, Color.white);
                // This test supplies arrivals directly; it does not run the locomotion/physics loop.
                typeof(PassengerAiV2Agent).GetField("velocity", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(a, Vector2.zero);
                return a;
            }
            public PassengerAiV2InteriorSpotSmartObject Spot(PassengerAiV2InteriorSpotKind kind, Vector2 p)
            {
                var go = new GameObject("Facility_Validation_Only"); go.transform.SetParent(root.transform);
                var s = go.AddComponent<PassengerAiV2InteriorSpotSmartObject>(); s.Configure("arbitrary", kind, p, 0.9f, 0, null); place.spots.Add(s); return s;
            }
            public void Settle() { activity.TickForValidation(0); activity.TickForValidation(1); activity.TickForValidation(2); }
            public void Dispose() { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(settings); EditorSceneManager.ClosePreviewScene(scene); }
        }
        public static string Validate()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Static checks require Edit mode.");
            int checks = 0; var failed = new List<string>();
            Action<bool, string> check = (ok, label) => { checks++; if (!ok) failed.Add(label); };
            foreach (Vector2 offset in new[] { Vector2.zero, new Vector2(41, -23) })
            using (var f = new Fixture(offset, PassengerAiV2LocalGoal.WaitForService))
            {
                var seat = f.Spot(PassengerAiV2InteriorSpotKind.Seat, offset);
                var door = f.Spot(PassengerAiV2InteriorSpotKind.DoorPrepare, offset + new Vector2(2, 1));
                f.Settle();
                for (int second = 3; second <= 603; second++) f.activity.TickForValidation(second);
                check(f.activity.Sitting && seat.IsOccupied, "Seat held for 600 simulated seconds at " + offset);
                check(f.activity.DecisionCount == 1 && f.activity.TargetChangeCount == 1, "No timed rescoring/target churn at " + offset);
                f.place.Signal(PassengerAiV2ServicePhase.Approaching, false, true); f.activity.TickForValidation(604);
                check(f.activity.Sitting, "Unrelated service does not evict seat");
                f.place.Signal(PassengerAiV2ServicePhase.Approaching, true, false); f.activity.TickForValidation(605);
                check(f.activity.State == PassengerAiV2ActivityState.Preparing && !seat.IsReserved && door.HasReservation(f.agent), "Relevant train prepares once");
                f.agent.GetComponent<Rigidbody2D>().position = door.UsePosition; f.activity.TickForValidation(606); f.activity.TickForValidation(607);
                check(f.activity.State == PassengerAiV2ActivityState.ReadyForTraversal, "Door handoff waits, no closed-door teleport");
                f.place.Signal(PassengerAiV2ServicePhase.Departed, true, false); f.activity.TickForValidation(608);
                check(!door.IsReserved && f.activity.State != PassengerAiV2ActivityState.ReadyForTraversal, "Departure releases preparation slot");
            }
            using (var f = new Fixture(Vector2.zero, PassengerAiV2LocalGoal.RideToDestination))
            {
                f.place.context.Kind = PassengerAiV2PlaceKind.TrainInterior;
                var seat = f.Spot(PassengerAiV2InteriorSpotKind.Seat, Vector2.zero); f.Spot(PassengerAiV2InteriorSpotKind.DoorPrepare, Vector2.right * 3); f.Settle();
                f.place.Signal(PassengerAiV2ServicePhase.DoorsOpen, true, false); f.activity.TickForValidation(3);
                check(f.activity.Sitting, "Passing station does not end ride");
                f.place.Signal(PassengerAiV2ServicePhase.Approaching, true, true); f.activity.TickForValidation(4);
                check(!f.activity.Sitting && !seat.IsReserved, "Destination arrival ends seat commitment");
            }
            using (var f = new Fixture(Vector2.zero, PassengerAiV2LocalGoal.WaitHere))
            {
                f.Spot(PassengerAiV2InteriorSpotKind.Lean, Vector2.zero); f.Settle();
                f.place.obstructing = true;
                for (int t = 3; t < 603; t++) f.activity.TickForValidation(t);
                check(f.activity.Leaning && f.activity.DecisionCount == 1, "A usable support survives passing crowd");
            }
            using (var f = new Fixture(Vector2.zero, PassengerAiV2LocalGoal.WaitHere))
            {
                f.Settle(); for (int t = 3; t < 603; t++) f.activity.TickForValidation(t);
                check(f.activity.State == PassengerAiV2ActivityState.Staying && f.activity.TargetChangeCount == 0, "No facility: remain still, no patrol");
                f.place.obstructing = true; for (int t = 603; t < 606; t++) f.activity.TickForValidation(t);
                check(f.activity.TargetChangeCount == 0, "Brief flow does not cause relocation");
                f.place.obstructing = false; f.activity.TickForValidation(606);
                f.place.obstructing = true; for (int t = 607; t < 611; t++) f.activity.TickForValidation(t);
                check(f.activity.TargetChangeCount == 1 && f.activity.LastReason == PassengerAiV2DecisionReason.PersistentObstruction, "Persistent flow permits one move aside");
                check(!PassengerAiV2ActivityPolicy.MayRelocate(false, true, 100, 100, 19, f.settings), "Relocation cooldown prevents oscillation");
            }
            using (var f = new Fixture(Vector2.zero, PassengerAiV2LocalGoal.WaitHere))
            {
                var seat = f.Spot(PassengerAiV2InteriorSpotKind.Seat, Vector2.zero); f.Settle();
                UnityEngine.Object.DestroyImmediate(seat.gameObject); f.activity.TickForValidation(3);
                check(f.activity.DecisionCount == 2 && !f.activity.Sitting, "Destroyed facility invalidates commitment");
            }
            using (var f = new Fixture(Vector2.zero, PassengerAiV2LocalGoal.WaitHere))
            {
                var seat = f.Spot(PassengerAiV2InteriorSpotKind.Seat, Vector2.zero); var other = f.AddAgent(Vector2.right);
                seat.RequestUse(other, 0, 1); f.Settle();
                check(!f.activity.Sitting && seat.HasReservation(other) && seat.InvalidReservationCount == 0, "Reserved chair cannot be stolen");
            }
            using (var f = new Fixture(Vector2.zero, PassengerAiV2LocalGoal.PassThrough))
            {
                for (int t = 0; t < 100; t++) f.activity.TickForValidation(t);
                check(f.place.exitQueries == 1, "Exit direction stays committed across stalled-route retries");
                check(f.activity.TargetChangeCount == 3, "Failed-route retries are bounded");
                f.place.Signal(PassengerAiV2ServicePhase.Waiting, false, false); f.activity.TickForValidation(1000000);
                check(f.activity.TargetChangeCount == 4, "Changed environment can wake a suspended route");
            }
            ValidatePaths(check);
            return "Behavior/path deterministic checks=" + checks + "\nFAILURES=" + failed.Count + "\n" + string.Join("\n", failed);
        }
        static void ValidatePaths(Action<bool, string> check)
        {
            foreach (Vector2 offset in new[] { Vector2.zero, new Vector2(31, -17) })
            foreach (bool detour in new[] { false, true })
            {
                Rect[] obstacles = detour ? new[] { new Rect(offset.x - 0.6f, offset.y - 4, 1.2f, 7) } : Array.Empty<Rect>();
                var grid = new SliceNavigationGrid(new Rect(offset.x - 12, offset.y - 8, 24, 16), Array.Empty<SliceFloorDiamond>(), obstacles);
                var follower = new SlicePathFollower(grid); Vector2 p = offset + new Vector2(-9, -3), goal = offset + new Vector2(9, 2);
                bool clear = true; int stagnant = 0;
                for (int step = 0; step < 1800 && Vector2.Distance(p, goal) > 0.1f; step++)
                {
                    Vector2 target = follower.ResolveForValidation(p, goal, step * 0.05f);
                    Vector2 next = grid.Constrain(p, Vector2.MoveTowards(p, target, 0.08f), grid.AgentRadius);
                    if ((next - p).sqrMagnitude < 0.00001f) stagnant++;
                    clear &= grid.Fits(next, grid.AgentRadius); p = next;
                }
                check(Vector2.Distance(p, goal) < 0.12f && stagnant < 5, "Look-ahead reaches goal without revisiting a stale corner: " + offset + " detour=" + detour);
                check(clear, "Path stays inside geometry: " + offset + " detour=" + detour);
            }
        }
    }
}
