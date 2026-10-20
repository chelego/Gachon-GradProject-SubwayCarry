using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.AI.V2
{
    public enum PassengerAiV2PlaceKind { PublicHall, Concourse, Platform, TrainInterior }
    public enum PassengerAiV2LocalGoal { WaitHere, WaitForService, RideToDestination, PassThrough }
    public enum PassengerAiV2ServicePhase { None, Waiting, Approaching, DoorsOpen, Departed }
    public enum PassengerAiV2ActivityState { Choosing, Moving, Settling, Staying, Preparing, ReadyForTraversal, Leaving, AtExit, WaitingForRoute }
    public enum PassengerAiV2DecisionReason { Initial, KeepCurrent, RelevantService, FacilityLost, PersistentObstruction, RouteFailure }

    public struct PassengerAiV2PlaceContext
    {
        public PassengerAiV2PlaceKind Kind;
        public PassengerAiV2ServicePhase Service;
        public bool RelevantService;
        public bool DestinationStop;
        public int Revision;
    }

    // A new map supplies semantics and perception/navigation queries, never an ordered waypoint script.
    public interface IPassengerAiV2Place
    {
        event System.Action Changed;
        PassengerAiV2PlaceContext Context { get; }
        IReadOnlyList<PassengerAiV2InteriorSpotSmartObject> Spots { get; }
        bool CanUse(PassengerAiV2InteriorSpotSmartObject spot, bool preparing);
        bool CanStand(Vector2 position, PassengerAiV2Agent observer);
        float CrowdCost(Vector2 position, PassengerAiV2Agent observer);
        bool IsObstructingFlow(Vector2 position, PassengerAiV2Agent observer);
        bool TryFindStandingPosition(Vector2 origin, PassengerAiV2Agent observer, out Vector2 position);
        bool TryGetExit(Vector2 origin, out Vector2 position);
    }

    public static class PassengerAiV2ActivityPolicy
    {
        public static bool ShouldPrepare(PassengerAiV2LocalGoal goal, PassengerAiV2PlaceContext context)
        {
            if (!context.RelevantService) return false;
            bool approaching = context.Service == PassengerAiV2ServicePhase.Approaching || context.Service == PassengerAiV2ServicePhase.DoorsOpen;
            return approaching && (goal == PassengerAiV2LocalGoal.WaitForService
                || (goal == PassengerAiV2LocalGoal.RideToDestination && context.DestinationStop));
        }

        public static bool MayRelocate(bool seatedOrLeaning, bool facilityValid, float heldFor, float obstructedFor,
            float sinceRelocation, PassengerAiV2ActivitySettings settings)
        {
            if (!facilityValid) return true;
            // A passing crowd is not a reason to get up from a valid seat or support.
            return !seatedOrLeaning && heldFor >= settings.minimumCommitment
                && obstructedFor >= settings.persistentObstructionSeconds && sinceRelocation >= settings.relocationCooldown;
        }

        public static float Score(PassengerAiV2InteriorSpotKind kind, PassengerAiV2RuntimePersonality p,
            PassengerAiV2PlaceKind place, float comfort, float distance, float crowd, PassengerAiV2ActivitySettings settings)
        {
            // 05's preference model is retained; environmental costs are supplied by the map adapter.
            float score;
            switch (kind)
            {
                case PassengerAiV2InteriorSpotKind.Seat: score = 0.45f + p.SeatPreference * 0.5f + p.FatigueSensitivity * 0.08f; break;
                case PassengerAiV2InteriorSpotKind.Lean: score = 0.42f + (1 - p.SeatPreference) * 0.22f + p.DoorProximityPreference * 0.08f; break;
                default: score = 0.38f + p.DoorProximityPreference * 0.18f + p.Mobility * 0.06f; break;
            }
            if (place == PassengerAiV2PlaceKind.Concourse && kind == PassengerAiV2InteriorSpotKind.Stand) score -= 0.08f;
            return score + comfort * 0.18f - distance * settings.distanceCost - crowd * settings.crowdCost;
        }
    }
}
