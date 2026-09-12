using System;
using UnityEngine;

namespace SubwayCarry.AI.UtilityJourney
{
    public enum UtilityJourneyArea
    {
        OriginConcourse,
        OriginPlatform,
        TrainInterior,
        DestinationPlatform,
        DestinationConcourse,
        Completed
    }

    public enum UtilityJourneyDirection
    {
        Any,
        Upbound,
        Downbound
    }

    public enum UtilityJourneyFacilityKind
    {
        EntryGate,
        PlatformDownStair,
        PlatformWaitingArea,
        TrainDoor,
        TrainRideSpot,
        PlatformUpStair,
        ExitGate,
        StreetExit
    }

    public enum UtilityJourneyWaitingStyle
    {
        None,
        DoorQueue,
        BenchSeat,
        WallRest
    }

    public enum UtilityPassengerArchetype
    {
        QueuePlanner,
        SeatSeeker,
        WallRelaxed,
        Balanced,
        QuickTransfer
    }

    public enum UtilityPassengerSocialState
    {
        FreeWalking,
        QueueFollowing,
        Yielding,
        SideStepping,
        CrowdedContact,
        WaitingForAlighting,
        RushingForDoor
    }

    public enum UtilityJourneyAction
    {
        None,
        TapIn,
        DescendToPlatform,
        WaitForTrain,
        BoardTrain,
        SettleInsideTrain,
        RideTrain,
        AlightTrain,
        AscendToConcourse,
        TapOut,
        LeaveStation
    }

    [Serializable]
    public struct UtilityPassengerFacts
    {
        public UtilityJourneyArea Area;
        public UtilityJourneyDirection RequiredDirection;
        public bool FarePaid;
        public bool TrainAtPlatform;
        public bool TrainDoorOpen;
        public bool IsOnTrain;
        public bool IsSettledInsideTrain;
        public bool DestinationReady;
        public bool IsTransitioning;
        public bool IsComplete;
        public string PreferredExitId;
        public float SecondsUntilTrainArrival;
        public float SecondsUntilDoorClose;
    }

    public readonly struct UtilityFacilityObservation
    {
        public readonly string FacilityId;
        public readonly UtilityJourneyFacilityKind Kind;
        public readonly UtilityJourneyArea Area;
        public readonly UtilityJourneyDirection Direction;
        public readonly bool IsAvailable;
        public readonly bool IsReachable;
        public readonly float PathDistance;
        public readonly float CrowdCost;
        public readonly float QueueSeconds;
        public readonly float PersonalPreference;
        public readonly bool IsCurrentChoice;
        public readonly UtilityJourneyWaitingStyle WaitingStyle;
        public readonly string RouteGroup;
        public readonly float ComfortValue;
        public readonly float TransferConvenience;

        public UtilityFacilityObservation(
            string facilityId,
            UtilityJourneyFacilityKind kind,
            UtilityJourneyArea area,
            UtilityJourneyDirection direction,
            bool isAvailable,
            bool isReachable,
            float pathDistance,
            float crowdCost,
            float queueSeconds,
            float personalPreference,
            bool isCurrentChoice)
            : this(
                facilityId,
                kind,
                area,
                direction,
                isAvailable,
                isReachable,
                pathDistance,
                crowdCost,
                queueSeconds,
                personalPreference,
                isCurrentChoice,
                UtilityJourneyWaitingStyle.None,
                string.Empty,
                0.5f,
                0.5f)
        {
        }

        public UtilityFacilityObservation(
            string facilityId,
            UtilityJourneyFacilityKind kind,
            UtilityJourneyArea area,
            UtilityJourneyDirection direction,
            bool isAvailable,
            bool isReachable,
            float pathDistance,
            float crowdCost,
            float queueSeconds,
            float personalPreference,
            bool isCurrentChoice,
            UtilityJourneyWaitingStyle waitingStyle,
            string routeGroup)
            : this(
                facilityId,
                kind,
                area,
                direction,
                isAvailable,
                isReachable,
                pathDistance,
                crowdCost,
                queueSeconds,
                personalPreference,
                isCurrentChoice,
                waitingStyle,
                routeGroup,
                0.5f,
                0.5f)
        {
        }

        public UtilityFacilityObservation(
            string facilityId,
            UtilityJourneyFacilityKind kind,
            UtilityJourneyArea area,
            UtilityJourneyDirection direction,
            bool isAvailable,
            bool isReachable,
            float pathDistance,
            float crowdCost,
            float queueSeconds,
            float personalPreference,
            bool isCurrentChoice,
            UtilityJourneyWaitingStyle waitingStyle,
            string routeGroup,
            float comfortValue,
            float transferConvenience)
        {
            FacilityId = facilityId ?? string.Empty;
            Kind = kind;
            Area = area;
            Direction = direction;
            IsAvailable = isAvailable;
            IsReachable = isReachable;
            PathDistance = Mathf.Max(0f, pathDistance);
            CrowdCost = Mathf.Clamp01(crowdCost);
            QueueSeconds = Mathf.Max(0f, queueSeconds);
            PersonalPreference = Mathf.Clamp01(personalPreference);
            IsCurrentChoice = isCurrentChoice;
            WaitingStyle = waitingStyle;
            RouteGroup = routeGroup ?? string.Empty;
            ComfortValue = Mathf.Clamp01(comfortValue);
            TransferConvenience = Mathf.Clamp01(transferConvenience);
        }
    }

    public readonly struct UtilityCandidateScore
    {
        public readonly UtilityJourneyAction Action;
        public readonly float Score;
        public readonly bool IsValid;
        public readonly string Reason;

        public UtilityCandidateScore(
            UtilityJourneyAction action,
            float score,
            bool isValid,
            string reason)
        {
            Action = action;
            Score = score;
            IsValid = isValid;
            Reason = reason ?? string.Empty;
        }

        public static UtilityCandidateScore Invalid(string reason)
        {
            return new UtilityCandidateScore(
                UtilityJourneyAction.None,
                float.NegativeInfinity,
                false,
                reason);
        }
    }

    /// <summary>
    /// Scores every currently perceived facility. The passenger does not consume an authored
    /// waypoint sequence; trip facts decide which affordances are useful now, and A* is only
    /// used later to execute the selected intention.
    /// </summary>
    public static class UtilityJourneyDecisionModel
    {
        public static UtilityCandidateScore Evaluate(
            in UtilityPassengerFacts facts,
            in UtilityFacilityObservation facility)
        {
            return Evaluate(
                facts,
                facility,
                UtilityPassengerDecisionWeights.Default);
        }

        public static UtilityCandidateScore Evaluate(
            in UtilityPassengerFacts facts,
            in UtilityFacilityObservation facility,
            in UtilityPassengerDecisionWeights weights,
            bool includeReason = true)
        {
            if (facts.IsComplete || facts.Area == UtilityJourneyArea.Completed)
            {
                return UtilityCandidateScore.Invalid("journey complete");
            }

            if (facts.IsTransitioning)
            {
                return UtilityCandidateScore.Invalid("map transition in progress");
            }

            if (facility.Area != facts.Area)
            {
                return UtilityCandidateScore.Invalid("facility is on another map");
            }

            if (!facility.IsAvailable)
            {
                return UtilityCandidateScore.Invalid("facility unavailable");
            }

            if (!facility.IsReachable)
            {
                return UtilityCandidateScore.Invalid("no walkable route");
            }

            UtilityJourneyAction action = ResolveAction(facts, facility.Kind);
            if (action == UtilityJourneyAction.None)
            {
                return UtilityCandidateScore.Invalid("does not satisfy a current need");
            }

            bool directionMatches =
                facility.Direction == UtilityJourneyDirection.Any ||
                facility.Direction == facts.RequiredDirection;
            if (facility.Kind == UtilityJourneyFacilityKind.PlatformDownStair &&
                !directionMatches)
            {
                return UtilityCandidateScore.Invalid("wrong travel direction");
            }

            float progress = GetProgressValue(action) * weights.ProgressPriority;
            float distance = 26f *
                             (1f - Mathf.Clamp01(facility.PathDistance / 26f)) *
                             weights.DistanceEfficiency;
            float availability = 12f;
            float direction = directionMatches ? 12f : -8f;
            float crowd = -facility.CrowdCost * 24f * weights.CrowdAvoidance;
            float queue = -Mathf.Min(18f, facility.QueueSeconds * 1.8f) *
                          weights.QueueAvoidance;
            float preference = facility.PersonalPreference *
                               (action == UtilityJourneyAction.WaitForTrain
                                   ? 34f
                                   : action == UtilityJourneyAction.DescendToPlatform ||
                                     action == UtilityJourneyAction.AscendToConcourse
                                       ? 20f
                                       : 10f) *
                               weights.PersonalPreference;
            float commitment = facility.IsCurrentChoice
                ? 11f * weights.Commitment
                : 0f;
            float trainAnticipation = GetTrainAnticipationValue(
                facts,
                facility,
                action) * weights.TrainUrgency;
            float preferredExit =
                facility.Kind == UtilityJourneyFacilityKind.StreetExit &&
                !string.IsNullOrEmpty(facts.PreferredExitId) &&
                string.Equals(
                    facts.PreferredExitId,
                    facility.FacilityId,
                    StringComparison.Ordinal)
                    ? 48f * weights.PreferredExitPriority
                    : 0f;
            bool isComfortChoice =
                action == UtilityJourneyAction.WaitForTrain ||
                action == UtilityJourneyAction.SettleInsideTrain ||
                action == UtilityJourneyAction.RideTrain;
            float comfort = isComfortChoice
                ? facility.ComfortValue * 22f * weights.ComfortSeeking
                : 0f;
            float transferConvenience = isComfortChoice
                ? facility.TransferConvenience * 24f *
                  weights.TransferConvenience
                : 0f;

            float score = progress + distance + availability + direction + crowd +
                          queue + preference + commitment + preferredExit +
                          trainAnticipation + comfort + transferConvenience;
            string reason = includeReason
                ? action +
                  " | progress " + progress.ToString("0") +
                  ", path " + facility.PathDistance.ToString("0.0") +
                  ", crowd " + facility.CrowdCost.ToString("0.00") +
                  ", queue " + facility.QueueSeconds.ToString("0.0") +
                  (isComfortChoice
                      ? ", comfort " + facility.ComfortValue.ToString("0.00") +
                        ", transfer " + facility.TransferConvenience.ToString("0.00")
                      : string.Empty) +
                  (facility.WaitingStyle == UtilityJourneyWaitingStyle.None
                      ? string.Empty
                      : ", wait " + facility.WaitingStyle)
                : string.Empty;
            return new UtilityCandidateScore(action, score, true, reason);
        }

        private static float GetTrainAnticipationValue(
            in UtilityPassengerFacts facts,
            in UtilityFacilityObservation facility,
            UtilityJourneyAction action)
        {
            if (action != UtilityJourneyAction.WaitForTrain)
            {
                return 0f;
            }

            bool trainSoon = facts.TrainAtPlatform ||
                             facts.SecondsUntilTrainArrival <= 2.4f;
            if (trainSoon)
            {
                return facility.WaitingStyle == UtilityJourneyWaitingStyle.DoorQueue
                    ? 42f
                    : -18f;
            }

            return facility.WaitingStyle == UtilityJourneyWaitingStyle.DoorQueue
                ? -5f
                : 6f;
        }

        private static UtilityJourneyAction ResolveAction(
            in UtilityPassengerFacts facts,
            UtilityJourneyFacilityKind kind)
        {
            switch (facts.Area)
            {
                case UtilityJourneyArea.OriginConcourse:
                    if (!facts.FarePaid && kind == UtilityJourneyFacilityKind.EntryGate)
                    {
                        return UtilityJourneyAction.TapIn;
                    }

                    if (facts.FarePaid && kind == UtilityJourneyFacilityKind.PlatformDownStair)
                    {
                        return UtilityJourneyAction.DescendToPlatform;
                    }
                    break;

                case UtilityJourneyArea.OriginPlatform:
                    if (facts.TrainAtPlatform && facts.TrainDoorOpen &&
                        kind == UtilityJourneyFacilityKind.TrainDoor)
                    {
                        return UtilityJourneyAction.BoardTrain;
                    }

                    if (kind == UtilityJourneyFacilityKind.PlatformWaitingArea)
                    {
                        // A preferred seat or wall spot must never beat an open train
                        // door. Once boarding is possible, force a fresh door choice.
                        if (facts.TrainAtPlatform && facts.TrainDoorOpen)
                        {
                            return UtilityJourneyAction.None;
                        }

                        return UtilityJourneyAction.WaitForTrain;
                    }
                    break;

                case UtilityJourneyArea.TrainInterior:
                    if (facts.DestinationReady && facts.TrainDoorOpen &&
                        kind == UtilityJourneyFacilityKind.TrainDoor)
                    {
                        return UtilityJourneyAction.AlightTrain;
                    }

                    if (kind == UtilityJourneyFacilityKind.TrainRideSpot)
                    {
                        // Once the destination doors open, remaining in or moving
                        // between ride spots must never compete with alighting.
                        if (facts.DestinationReady && facts.TrainDoorOpen)
                        {
                            return UtilityJourneyAction.None;
                        }

                        return facts.IsSettledInsideTrain
                            ? UtilityJourneyAction.RideTrain
                            : UtilityJourneyAction.SettleInsideTrain;
                    }
                    break;

                case UtilityJourneyArea.DestinationPlatform:
                    if (kind == UtilityJourneyFacilityKind.PlatformUpStair)
                    {
                        return UtilityJourneyAction.AscendToConcourse;
                    }
                    break;

                case UtilityJourneyArea.DestinationConcourse:
                    if (facts.FarePaid && kind == UtilityJourneyFacilityKind.ExitGate)
                    {
                        return UtilityJourneyAction.TapOut;
                    }

                    if (!facts.FarePaid && kind == UtilityJourneyFacilityKind.StreetExit)
                    {
                        return UtilityJourneyAction.LeaveStation;
                    }
                    break;
            }

            return UtilityJourneyAction.None;
        }

        private static float GetProgressValue(UtilityJourneyAction action)
        {
            switch (action)
            {
                case UtilityJourneyAction.TapIn:
                case UtilityJourneyAction.DescendToPlatform:
                case UtilityJourneyAction.BoardTrain:
                case UtilityJourneyAction.AlightTrain:
                case UtilityJourneyAction.AscendToConcourse:
                case UtilityJourneyAction.TapOut:
                case UtilityJourneyAction.LeaveStation:
                    return 100f;
                case UtilityJourneyAction.SettleInsideTrain:
                    return 88f;
                case UtilityJourneyAction.WaitForTrain:
                    return 72f;
                case UtilityJourneyAction.RideTrain:
                    return 76f;
                default:
                    return 0f;
            }
        }
    }
}
