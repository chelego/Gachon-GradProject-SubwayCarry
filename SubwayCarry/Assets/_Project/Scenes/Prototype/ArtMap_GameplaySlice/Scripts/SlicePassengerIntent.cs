using UnityEngine;
using SubwayCarry.AI.V2;
using GameplayContracts = SubwayCarry.Core.Contracts;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    // Only art/lifetime/package integration remains here. The shared AI owns activity choices.
    public sealed class SlicePassengerIntent : MonoBehaviour, GameplayContracts.IPackageImpactSource
    {
        PassengerAiV2Agent agent;
        PassengerAiV2ContextualActivity activity;
        SliceMap map;
        SliceGameController owner;
        bool exiting;
        float outsideAt = -1;
        PassengerAiV2LocalGoal goal;
        bool traversing, crossing;
        Vector2 doorEntry, doorOutside;
        PassengerAiV2RuntimePersonality personality;
        SlicePlaceEnvironment environment;
        PassengerAiV2ActivitySettings tuning;
        float stagger;
        float nextPaceEvaluation;
        public bool IsCrossingDoor => traversing && crossing;
        public int PassengerId { get; private set; }
        public int DestinationStop { get; private set; }
        public bool HasLeftTrain { get; private set; }
        public Vector2 ImpactVelocity => agent != null ? agent.Velocity : Vector2.zero;
        public bool Sitting => activity != null && activity.Sitting;
        public bool Leaning => activity != null && activity.Leaning;
        public bool IsPassingThrough => activity != null && activity.Goal == PassengerAiV2LocalGoal.PassThrough;
        public PassengerAiV2ActivityState State => activity != null ? activity.State : PassengerAiV2ActivityState.Choosing;
        public int TargetChanges => activity != null ? activity.TargetChangeCount : 0;
        public void Initialize(PassengerAiV2Agent a, SliceMap m, SliceGameController controller,
            PassengerAiV2RuntimePersonality personality, SlicePlaceEnvironment environment, PassengerAiV2ActivitySettings tuning,
            PassengerAiV2LocalGoal goal, int seed, int passengerId = 0, int destinationStop = 1)
        {
            agent = a; map = m; owner = controller;
            this.goal = goal; PassengerId = passengerId; DestinationStop = destinationStop;
            this.personality = personality; this.environment = environment; this.tuning = tuning; stagger = (seed % 17) / 17f;
            ResumeActivity();
        }
        void ResumeActivity()
        {
            activity = gameObject.AddComponent<PassengerAiV2ContextualActivity>();
            activity.Initialize(agent, new PassengerView(environment, this), personality, tuning, goal, stagger);
        }
        void Update()
        {
            if (traversing) { Traverse(); return; }
            if (activity == null) return;
            bool train = map.placeKind == PassengerAiV2PlaceKind.TrainInterior;
            bool wantsTraversal = goal == PassengerAiV2LocalGoal.WaitForService || (train && goal == PassengerAiV2LocalGoal.RideToDestination && owner.ApproachingStopIndex >= DestinationStop);
            if (!exiting && wantsTraversal && owner.DoorsOpen)
            {
                float nearest = float.PositiveInfinity;
                foreach (var portal in map.portals)
                {
                    if (train && portal.side != GameplayContracts.DoorOpeningSide.Right) continue;
                    float distance = (agent.Position - portal.position).sqrMagnitude;
                    if (distance >= nearest) continue;
                    nearest = distance; doorEntry = portal.position;
                }
                if (!float.IsPositiveInfinity(nearest))
                {
                    // Ending the use action releases the seat immediately; do not reserve it throughout alighting.
                    activity.enabled = false; Destroy(activity); activity = null;
                    Vector2 normal = new Vector2(-.5f, 1).normalized;
                    if (!train)
                    {
                        float nearestWall = float.PositiveInfinity;
                        foreach (var wall in map.walls)
                        { float d = Mathf.Abs(Vector2.Dot(doorEntry - wall.point, wall.inwardNormal)); if (d < nearestWall) { nearestWall = d; normal = wall.inwardNormal; } }
                    }
                    doorOutside = doorEntry - normal * 2.2f;
                    traversing = true; agent.SetMovementTarget(doorEntry); return;
                }
            }
            if (!exiting && activity.IsAtExit)
            {
                int exit = 0; float nearest = float.PositiveInfinity;
                for (int i = 0; i < map.exits.Length; i++) { float d = (agent.Position - map.exits[i].inside).sqrMagnitude; if (d < nearest) { nearest = d; exit = i; } }
                exiting = true;
                agent.ConfigureMovementSpace(null, null, map.characterRadius);
                agent.SetMovementBounds(Rect.MinMaxRect(map.floorBounds.xMin - 6, map.floorBounds.yMin - 6, map.floorBounds.xMax + 6, map.floorBounds.yMax + 6));
                agent.SetMovementTarget(map.exits[exit].outside);
            }
            if (!exiting) return;
            bool outside = !map.ContainsFloor(agent.Position);
            if (outside && outsideAt < 0) outsideAt = Time.time;
            if ((outside && Time.time - outsideAt > 0.65f) || agent.HasReachedTarget) { owner.ReportPassengerExit(); Destroy(gameObject); }
        }
        void Traverse()
        {
            if (Time.time >= nextPaceEvaluation)
            {
                nextPaceEvaluation = Time.time + .25f;
                float timeNeeded = Vector2.Distance(agent.Position, crossing ? doorOutside : doorEntry) / Mathf.Max(.2f, personality.WalkingSpeed);
                bool hurry = owner.DoorSecondsRemaining < timeNeeded + 2 && owner.DoorSecondsRemaining > .8f &&
                    personality.Mobility > .65f && personality.RiskTolerance >= .4f && environment.CrowdCost(agent.Position, agent) < .65f;
                agent.SetMovementSpeedScale(hurry ? 1.25f : 1);
            }
            if (!crossing && !owner.DoorsOpen)
            {
                traversing = false; agent.SetMovementSpeedScale(1); ResumeActivity(); return;
            }
            if (!crossing && (agent.Position - doorEntry).sqrMagnitude < .2f)
            {
                crossing = true;
                agent.ConfigureMovementSpace(null, null, map.characterRadius);
                agent.SetMovementBounds(new Rect(map.floorBounds.xMin - 6, map.floorBounds.yMin - 6, map.floorBounds.width + 12, map.floorBounds.height + 12));
                agent.SetMovementTarget(doorOutside);
            }
            if (!crossing || !agent.HasReachedTarget) return;
            bool boarding = map.placeKind != PassengerAiV2PlaceKind.TrainInterior;
            HasLeftTrain = !boarding; owner.PassengerTraversed(PassengerId, DestinationStop, boarding); Destroy(gameObject);
        }
        // Each passenger sees the same service event but evaluates it against their own destination memory.
        sealed class PassengerView : IPassengerAiV2Place
        {
            readonly SlicePlaceEnvironment source; readonly SlicePassengerIntent passenger;
            public PassengerView(SlicePlaceEnvironment environment, SlicePassengerIntent intent) { source = environment; passenger = intent; }
            public event System.Action Changed { add { source.Changed += value; } remove { source.Changed -= value; } }
            public PassengerAiV2PlaceContext Context
            {
                get { var c = source.Context; if (c.Kind == PassengerAiV2PlaceKind.TrainInterior) c.DestinationStop = passenger.owner.ApproachingStopIndex >= passenger.DestinationStop; return c; }
            }
            public System.Collections.Generic.IReadOnlyList<PassengerAiV2InteriorSpotSmartObject> Spots => source.Spots;
            public bool CanUse(PassengerAiV2InteriorSpotSmartObject spot, bool preparing) => source.CanUse(spot, preparing);
            public bool CanStand(Vector2 p, PassengerAiV2Agent a) => source.CanStand(p, a);
            public float CrowdCost(Vector2 p, PassengerAiV2Agent a) => source.CrowdCost(p, a);
            public bool IsObstructingFlow(Vector2 p, PassengerAiV2Agent a) => source.IsObstructingFlow(p, a);
            public bool TryFindStandingPosition(Vector2 p, PassengerAiV2Agent a, out Vector2 result) => source.TryFindStandingPosition(p, a, out result);
            public bool TryGetExit(Vector2 p, out Vector2 result) => source.TryGetExit(p, out result);
        }
    }
}
