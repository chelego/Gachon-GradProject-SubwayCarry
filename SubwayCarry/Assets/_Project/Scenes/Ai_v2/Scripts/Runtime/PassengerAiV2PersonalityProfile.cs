using System;
using UnityEngine;

namespace SubwayCarry.AI.V2
{
    [Serializable]
    public struct PassengerAiV2RuntimePersonality
    {
        public string ProfileId;
        public string DisplayName;
        public float Courtesy;
        public float Assertiveness;
        public float SelfInterest;
        public float PersonalSpace;
        public float CrowdTolerance;
        public float Patience;
        public float SeatPreference;
        public float DoorProximityPreference;
        public float WalkingSpeed;
        public float ReactionDelay;
        public float RouteFamiliarity;
        public float Mobility;
        public float FatigueSensitivity;
        public float RiskTolerance;
        public Color DebugColor;

        public static PassengerAiV2RuntimePersonality CreateFallback(int seed)
        {
            _ = seed;
            return new PassengerAiV2RuntimePersonality
            {
                ProfileId = "base_passenger",
                DisplayName = "Base Passenger",
                Courtesy = 0.62f,
                Assertiveness = 0.52f,
                SelfInterest = 0.5f,
                PersonalSpace = 0.25f,
                CrowdTolerance = 0.55f,
                Patience = 0.65f,
                SeatPreference = 0.55f,
                DoorProximityPreference = 0.5f,
                WalkingSpeed = 1.35f,
                ReactionDelay = 0.22f,
                RouteFamiliarity = 0.65f,
                Mobility = 0.85f,
                FatigueSensitivity = 0.5f,
                RiskTolerance = 0.45f,
                DebugColor = new Color(0.78f, 0.82f, 0.88f, 1f)
            };
        }
    }

    /// <summary>
    /// 승객의 성격을 고정 타입이 아닌 연속값 묶음으로 보관한다.
    /// Asset 값은 기본 성향이고, 실제 승객에는 Seed 기반의 작은 개인차가 적용된다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "AI_V2_PersonalityProfile",
        menuName = "SubwayCarry/AI V2/Passenger Personality Profile")]
    public sealed class PassengerAiV2PersonalityProfile : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string profileId = "base_passenger";
        [SerializeField] private string displayName = "Base Passenger";
        [SerializeField] private Color debugColor = new Color(0.78f, 0.82f, 0.88f, 1f);

        [Header("Social")]
        [SerializeField, Range(0f, 1f)] private float courtesy = 0.62f;
        [SerializeField, Range(0f, 1f)] private float assertiveness = 0.52f;
        [SerializeField, Range(0f, 1f)] private float selfInterest = 0.5f;
        [SerializeField, Min(0.05f)] private float personalSpace = 0.25f;
        [SerializeField, Range(0f, 1f)] private float crowdTolerance = 0.55f;
        [SerializeField, Range(0f, 1f)] private float patience = 0.65f;

        [Header("Preference")]
        [SerializeField, Range(0f, 1f)] private float seatPreference = 0.55f;
        [SerializeField, Range(0f, 1f)] private float doorProximityPreference = 0.5f;
        [SerializeField, Range(0f, 1f)] private float routeFamiliarity = 0.65f;
        [SerializeField, Range(0f, 1f)] private float riskTolerance = 0.45f;

        [Header("Physical")]
        [SerializeField, Min(0.2f)] private float walkingSpeed = 1.35f;
        [SerializeField, Range(0.05f, 1f)] private float reactionDelay = 0.22f;
        [SerializeField, Range(0f, 1f)] private float mobility = 0.85f;
        [SerializeField, Range(0f, 1f)] private float fatigueSensitivity = 0.5f;

        [Header("Individual Variation")]
        [SerializeField, Range(0f, 0.3f)] private float personalityVariation = 0f;
        [SerializeField, Range(0f, 0.3f)] private float walkingSpeedVariation = 0f;

        public string ProfileId => profileId;
        public string DisplayName => displayName;
        public Color DebugColor => debugColor;

        public PassengerAiV2RuntimePersonality CreateRuntimePersonality(int seed)
        {
            System.Random random = new System.Random(seed);
            return new PassengerAiV2RuntimePersonality
            {
                ProfileId = profileId,
                DisplayName = displayName,
                Courtesy = Vary01(courtesy, personalityVariation, random),
                Assertiveness = Vary01(assertiveness, personalityVariation, random),
                SelfInterest = Vary01(selfInterest, personalityVariation, random),
                PersonalSpace = Mathf.Max(0.05f, Vary(personalSpace, personalityVariation * 0.3f, random)),
                CrowdTolerance = Vary01(crowdTolerance, personalityVariation, random),
                Patience = Vary01(patience, personalityVariation, random),
                SeatPreference = Vary01(seatPreference, personalityVariation, random),
                DoorProximityPreference = Vary01(doorProximityPreference, personalityVariation, random),
                WalkingSpeed = Mathf.Max(0.2f, Vary(walkingSpeed, walkingSpeedVariation, random)),
                ReactionDelay = Mathf.Clamp(Vary(reactionDelay, personalityVariation * 0.15f, random), 0.05f, 1f),
                RouteFamiliarity = Vary01(routeFamiliarity, personalityVariation, random),
                Mobility = Vary01(mobility, personalityVariation, random),
                FatigueSensitivity = Vary01(fatigueSensitivity, personalityVariation, random),
                RiskTolerance = Vary01(riskTolerance, personalityVariation, random),
                DebugColor = debugColor
            };
        }

#if UNITY_EDITOR
        public void ConfigurePrototypeDefaults(
            string id,
            string label,
            Color color,
            float courtesyValue,
            float assertivenessValue,
            float selfInterestValue,
            float personalSpaceValue,
            float crowdToleranceValue,
            float patienceValue,
            float seatPreferenceValue,
            float doorPreferenceValue,
            float walkingSpeedValue,
            float reactionDelayValue,
            float familiarityValue,
            float mobilityValue,
            float fatigueValue,
            float riskValue)
        {
            profileId = id;
            displayName = label;
            debugColor = color;
            courtesy = courtesyValue;
            assertiveness = assertivenessValue;
            selfInterest = selfInterestValue;
            personalSpace = personalSpaceValue;
            crowdTolerance = crowdToleranceValue;
            patience = patienceValue;
            seatPreference = seatPreferenceValue;
            doorProximityPreference = doorPreferenceValue;
            walkingSpeed = walkingSpeedValue;
            reactionDelay = reactionDelayValue;
            routeFamiliarity = familiarityValue;
            mobility = mobilityValue;
            fatigueSensitivity = fatigueValue;
            riskTolerance = riskValue;
            personalityVariation = 0f;
            walkingSpeedVariation = 0f;
        }
#endif

        private static float Vary01(float value, float variation, System.Random random)
        {
            return Mathf.Clamp01(Vary(value, variation, random));
        }

        private static float Vary(float value, float variation, System.Random random)
        {
            float signed = (float)(random.NextDouble() * 2.0 - 1.0);
            return value + signed * variation;
        }
    }
}
