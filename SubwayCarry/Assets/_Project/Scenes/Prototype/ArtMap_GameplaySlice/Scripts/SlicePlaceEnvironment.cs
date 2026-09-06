using System.Collections.Generic;
using UnityEngine;
using SubwayCarry.AI.V2;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    // The common AI reads this contract, not art/map names or an ordered waypoint script.
    public sealed class SlicePlaceEnvironment : MonoBehaviour, IPassengerAiV2Place
    {
        SliceMap map;
        PassengerAiV2CrowdManager crowd;
        readonly List<PassengerAiV2InteriorSpotSmartObject> spots = new List<PassengerAiV2InteriorSpotSmartObject>(96);
        readonly List<PassengerAiV2Agent> nearby = new List<PassengerAiV2Agent>(32);
        readonly List<Vector2> standing = new List<Vector2>(256);
        PassengerAiV2PlaceContext context;
        public event System.Action Changed;
        public PassengerAiV2PlaceContext Context => context;
        public IReadOnlyList<PassengerAiV2InteriorSpotSmartObject> Spots => spots;

        public void Configure(SliceMap source, PassengerAiV2CrowdManager manager, PassengerAiV2InteriorSpotSmartObject[] facilities)
        {
            map = source; crowd = manager; spots.AddRange(facilities);
            context = new PassengerAiV2PlaceContext { Kind = source.placeKind, Service = PassengerAiV2ServicePhase.Waiting };
            foreach (Vector2 p in map.Grid.SamplePoints) if (CanStandStatic(p)) standing.Add(p);
            for (int i = 0; i < map.portals.Length; i++)
            {
                Vector2 normal = (map.floorBounds.center - map.portals[i].position).normalized;
                float best = float.PositiveInfinity;
                foreach (var wall in map.walls) { float d = Mathf.Abs(Vector2.Dot(map.portals[i].position - wall.point, wall.inwardNormal)); if (d < best) { best = d; normal = wall.inwardNormal; } }
                Vector2 tangent = new Vector2(-normal.y, normal.x);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 p = map.Grid.Nearest(map.portals[i].position + tangent * side * 0.95f + normal * 0.45f);
                    bool duplicate = false;
                    foreach (var s in spots) if ((s.UsePosition - p).sqrMagnitude < 0.7f) { duplicate = true; break; }
                    if (duplicate) continue;
                    var go = new GameObject("ServiceQueue_" + i + "_" + side); go.transform.SetParent(transform, false); go.transform.position = p;
                    var spot = go.AddComponent<PassengerAiV2InteriorSpotSmartObject>();
                    spot.Configure("service_queue_" + i + "_" + side, PassengerAiV2InteriorSpotKind.DoorPrepare, p, 0.3f, 0, null);
                    spots.Add(spot);
                }
            }
        }
        public void NotifyService(PassengerAiV2ServicePhase phase, bool relevantService, bool destinationStop)
        {
            if (context.Service == phase && context.RelevantService == relevantService && context.DestinationStop == destinationStop) return;
            context.Service = phase; context.RelevantService = relevantService; context.DestinationStop = destinationStop; context.Revision++;
            Changed?.Invoke();
        }
        public void NotifyFacilitiesChanged() { context.Revision++; Changed?.Invoke(); }
        public bool CanUse(PassengerAiV2InteriorSpotSmartObject spot, bool preparing)
        {
            if (spot == null || !spot.isActiveAndEnabled || !map.Grid.Fits(spot.UsePosition, map.characterRadius)) return false;
            if (!spot.IsReserved) foreach (var other in spots)
                if (other != spot && other.IsReserved && (other.UsePosition - spot.UsePosition).sqrMagnitude < map.characterRadius * map.characterRadius * 5f) return false;
            if (preparing) return spot.Kind == PassengerAiV2InteriorSpotKind.DoorPrepare;
            if (spot.Kind == PassengerAiV2InteriorSpotKind.DoorPrepare) return false;
            return spot.Kind != PassengerAiV2InteriorSpotKind.Stand || CanStandStatic(spot.UsePosition);
        }
        bool CanStandStatic(Vector2 position)
        {
            if (!map.Grid.Fits(position, map.characterRadius)) return false;
            foreach (var p in map.portals) if ((position - p.position).sqrMagnitude < 2.25f) return false;
            foreach (var e in map.exits) if ((position - e.inside).sqrMagnitude < 3.24f) return false;
            foreach (var r in map.noWaitingZones) if (r.Contains(position)) return false;
            if ((position - map.entry).sqrMagnitude < 2.25f) return false;
            if (context.Kind != PassengerAiV2PlaceKind.TrainInterior && map.exits.Length >= 2)
            {
                Vector2 a = map.exits[0].inside, ab = map.exits[1].inside - a;
                float t = Mathf.Clamp01(Vector2.Dot(position - a, ab) / Mathf.Max(0.01f, ab.sqrMagnitude));
                if ((position - (a + ab * t)).sqrMagnitude < 1.0f) return false;
            }
            return true;
        }
        public bool CanStand(Vector2 position, PassengerAiV2Agent observer) => CanStandStatic(position) && CrowdCost(position, observer) < 0.65f;
        public float CrowdCost(Vector2 position, PassengerAiV2Agent observer)
        {
            crowd.QueryNearby(position, 1.4f, observer, nearby); float cost = 0;
            for (int i = 0; i < nearby.Count; i++) cost += Mathf.Clamp01(1 - Vector2.Distance(position, nearby[i].Position) / 1.4f);
            return Mathf.Clamp01(cost / 2f);
        }
        public bool IsObstructingFlow(Vector2 position, PassengerAiV2Agent observer)
        {
            crowd.QueryNearby(position, 1.7f, observer, nearby); int approaching = 0;
            foreach (var other in nearby)
            {
                if (other.Velocity.sqrMagnitude < 0.16f) continue;
                Vector2 delta = position - other.Position, forward = other.Velocity.normalized;
                float ahead = Vector2.Dot(delta, forward), across = Mathf.Abs(delta.x * forward.y - delta.y * forward.x);
                if (ahead > 0 && across < observer.Radius + other.Radius + 0.2f) approaching++;
            }
            return approaching >= 2;
        }
        public bool TryFindStandingPosition(Vector2 origin, PassengerAiV2Agent observer, out Vector2 position)
        {
            float best = float.PositiveInfinity; position = origin; bool found = false;
            bool moveAside = IsObstructingFlow(origin, observer);
            foreach (var p in standing)
            {
                float distance = Vector2.Distance(origin, p); if (distance >= best) continue;
                if (moveAside && distance < 0.9f) continue;
                float density = CrowdCost(p, observer); if (density > 0.65f) continue;
                float cost = distance + density * 5f;
                if (cost < best) { best = cost; position = p; found = true; }
            }
            return found;
        }
        public bool TryGetExit(Vector2 origin, out Vector2 position)
        {
            float best = -1; position = origin;
            foreach (var exit in map.exits) { float d = (origin - exit.inside).sqrMagnitude; if (d > best) { best = d; position = exit.inside; } }
            return best >= 0;
        }
    }
}
