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
        public Vector2 ImpactVelocity => agent != null ? agent.Velocity : Vector2.zero;
        public bool Sitting => activity != null && activity.Sitting;
        public bool Leaning => activity != null && activity.Leaning;
        public bool IsPassingThrough => activity != null && activity.Goal == PassengerAiV2LocalGoal.PassThrough;
        public PassengerAiV2ActivityState State => activity != null ? activity.State : PassengerAiV2ActivityState.Choosing;
        public int TargetChanges => activity != null ? activity.TargetChangeCount : 0;
        public void Initialize(PassengerAiV2Agent a, SliceMap m, SliceGameController controller,
            PassengerAiV2RuntimePersonality personality, SlicePlaceEnvironment environment, PassengerAiV2ActivitySettings tuning,
            PassengerAiV2LocalGoal goal, int seed)
        {
            agent = a; map = m; owner = controller;
            activity = gameObject.AddComponent<PassengerAiV2ContextualActivity>();
            activity.Initialize(agent, environment, personality, tuning, goal, (seed % 17) / 17f);
        }
        void Update()
        {
            if (activity == null) return;
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
    }
}
