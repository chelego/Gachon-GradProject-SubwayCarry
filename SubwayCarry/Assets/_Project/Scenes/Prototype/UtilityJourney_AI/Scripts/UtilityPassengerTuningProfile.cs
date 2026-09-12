using System;
using UnityEngine;

namespace SubwayCarry.AI.UtilityJourney
{
    [Serializable]
    public struct UtilityPassengerDecisionWeights
    {
        [Min(0f)] public float ProgressPriority;
        [Min(0f)] public float DistanceEfficiency;
        [Min(0f)] public float CrowdAvoidance;
        [Min(0f)] public float QueueAvoidance;
        [Min(0f)] public float PersonalPreference;
        [Min(0f)] public float Commitment;
        [Min(0f)] public float TrainUrgency;
        [Min(0f)] public float PreferredExitPriority;
        [Min(0f)] public float ComfortSeeking;
        [Min(0f)] public float TransferConvenience;

        public UtilityPassengerDecisionWeights(
            float progressPriority,
            float distanceEfficiency,
            float crowdAvoidance,
            float queueAvoidance,
            float personalPreference,
            float commitment,
            float trainUrgency,
            float preferredExitPriority,
            float comfortSeeking,
            float transferConvenience)
        {
            ProgressPriority = Mathf.Max(0f, progressPriority);
            DistanceEfficiency = Mathf.Max(0f, distanceEfficiency);
            CrowdAvoidance = Mathf.Max(0f, crowdAvoidance);
            QueueAvoidance = Mathf.Max(0f, queueAvoidance);
            PersonalPreference = Mathf.Max(0f, personalPreference);
            Commitment = Mathf.Max(0f, commitment);
            TrainUrgency = Mathf.Max(0f, trainUrgency);
            PreferredExitPriority = Mathf.Max(0f, preferredExitPriority);
            ComfortSeeking = Mathf.Max(0f, comfortSeeking);
            TransferConvenience = Mathf.Max(0f, transferConvenience);
        }

        public static UtilityPassengerDecisionWeights Default =>
            new UtilityPassengerDecisionWeights(
                1f,
                1f,
                1f,
                1f,
                1f,
                1f,
                1f,
                1f,
                1f,
                1f);
    }

    [Serializable]
    public struct UtilityPassengerSocialTuning
    {
        [Min(0.1f)] public float AwarenessRadius;
        [Min(0.1f)] public float PersonalSpaceRadius;
        [Min(0.05f)] public float MinimumCrowdedRadius;
        [Range(0f, 1f)] public float Courtesy;
        [Range(0f, 1f)] public float Assertiveness;
        [Range(0f, 1f)] public float QueueDiscipline;
        [Range(0f, 1f)] public float CrowdTolerance;
        [Range(-1f, 1f)] public float PassingSidePreference;
        [Min(0.5f)] public float DoorRushThresholdSeconds;

        public UtilityPassengerSocialTuning(
            float awarenessRadius,
            float personalSpaceRadius,
            float minimumCrowdedRadius,
            float courtesy,
            float assertiveness,
            float queueDiscipline,
            float crowdTolerance,
            float passingSidePreference,
            float doorRushThresholdSeconds)
        {
            AwarenessRadius = Mathf.Max(0.1f, awarenessRadius);
            PersonalSpaceRadius = Mathf.Max(0.1f, personalSpaceRadius);
            MinimumCrowdedRadius = Mathf.Clamp(
                minimumCrowdedRadius,
                0.05f,
                PersonalSpaceRadius);
            Courtesy = Mathf.Clamp01(courtesy);
            Assertiveness = Mathf.Clamp01(assertiveness);
            QueueDiscipline = Mathf.Clamp01(queueDiscipline);
            CrowdTolerance = Mathf.Clamp01(crowdTolerance);
            PassingSidePreference = Mathf.Clamp(passingSidePreference, -1f, 1f);
            DoorRushThresholdSeconds = Mathf.Max(0.5f, doorRushThresholdSeconds);
        }

        public static UtilityPassengerSocialTuning Default =>
            new UtilityPassengerSocialTuning(
                2.2f,
                0.78f,
                0.32f,
                0.72f,
                0.48f,
                0.82f,
                0.5f,
                0.65f,
                3.2f);
    }

    [CreateAssetMenu(
        fileName = "UtilityPassengerProfile",
        menuName = "SubwayCarry/AI/Utility Passenger Profile")]
    public sealed class UtilityPassengerTuningProfile : ScriptableObject
    {
        [SerializeField] private string displayName = "Balanced";
        [SerializeField] private UtilityPassengerArchetype archetype =
            UtilityPassengerArchetype.Balanced;
        [SerializeField] private UtilityPassengerDecisionWeights decisionWeights =
            default;
        [Header("Style preference")]
        [SerializeField, Range(0f, 1f)] private float doorQueuePreference = 0.6f;
        [SerializeField, Range(0f, 1f)] private float seatPreference = 0.6f;
        [SerializeField, Range(0f, 1f)] private float wallPreference = 0.5f;
        [Header("Motion")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 1.15f;
        [SerializeField, Min(0.1f)] private float runSpeed = 2.45f;
        [SerializeField, Min(0.1f)] private float acceleration = 4.5f;
        [SerializeField, Min(0.05f)] private float decisionInterval = 0.32f;
        [SerializeField, Min(0.05f)] private float repathInterval = 0.42f;
        [Header("Social movement")]
        [SerializeField] private UtilityPassengerSocialTuning socialTuning = default;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? archetype.ToString()
            : displayName;
        public UtilityPassengerArchetype Archetype => archetype;
        public UtilityPassengerDecisionWeights DecisionWeights =>
            HasConfiguredWeights() ? decisionWeights : UtilityPassengerDecisionWeights.Default;
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public float Acceleration => acceleration;
        public float DecisionInterval => decisionInterval;
        public float RepathInterval => repathInterval;
        public UtilityPassengerSocialTuning SocialTuning =>
            socialTuning.AwarenessRadius > 0f
                ? socialTuning
                : UtilityPassengerSocialTuning.Default;

        public float GetStylePreference(UtilityJourneyWaitingStyle style)
        {
            switch (style)
            {
                case UtilityJourneyWaitingStyle.DoorQueue:
                    return doorQueuePreference;
                case UtilityJourneyWaitingStyle.BenchSeat:
                    return seatPreference;
                case UtilityJourneyWaitingStyle.WallRest:
                    return wallPreference;
                default:
                    return 0.5f;
            }
        }

        public void ConfigurePrototype(
            string profileName,
            UtilityPassengerArchetype profileArchetype,
            UtilityPassengerDecisionWeights weights,
            Vector3 stylePreferences,
            float profileWalkSpeed,
            float profileRunSpeed,
            float profileAcceleration,
            float profileDecisionInterval,
            float profileRepathInterval,
            UtilityPassengerSocialTuning profileSocialTuning = default)
        {
            displayName = profileName;
            archetype = profileArchetype;
            decisionWeights = weights;
            doorQueuePreference = Mathf.Clamp01(stylePreferences.x);
            seatPreference = Mathf.Clamp01(stylePreferences.y);
            wallPreference = Mathf.Clamp01(stylePreferences.z);
            walkSpeed = Mathf.Max(0.1f, profileWalkSpeed);
            runSpeed = Mathf.Max(walkSpeed, profileRunSpeed);
            acceleration = Mathf.Max(0.1f, profileAcceleration);
            decisionInterval = Mathf.Max(0.05f, profileDecisionInterval);
            repathInterval = Mathf.Max(0.05f, profileRepathInterval);
            socialTuning = profileSocialTuning.AwarenessRadius > 0f
                ? profileSocialTuning
                : UtilityPassengerSocialTuning.Default;
        }

        private bool HasConfiguredWeights()
        {
            return decisionWeights.ProgressPriority > 0f ||
                   decisionWeights.DistanceEfficiency > 0f ||
                   decisionWeights.CrowdAvoidance > 0f ||
                   decisionWeights.QueueAvoidance > 0f ||
                   decisionWeights.PersonalPreference > 0f ||
                   decisionWeights.Commitment > 0f ||
                   decisionWeights.TrainUrgency > 0f ||
                   decisionWeights.PreferredExitPriority > 0f ||
                   decisionWeights.ComfortSeeking > 0f ||
                   decisionWeights.TransferConvenience > 0f;
        }
    }
}
