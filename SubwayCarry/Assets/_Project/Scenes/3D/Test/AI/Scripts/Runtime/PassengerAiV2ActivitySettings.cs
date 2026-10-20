using UnityEngine;
namespace SubwayCarry.AI.V2
{
    [CreateAssetMenu(fileName = "AI_V2_ContextualActivity_Default", menuName = "SubwayCarry/AI V2/Contextual Activity Settings")]
    public sealed class PassengerAiV2ActivitySettings : ScriptableObject
    {
        [Min(0.25f)] public float decisionInterval = 0.5f;
        [Min(0.5f)] public float stayingPerceptionInterval = 1f;
        [Min(1)] public float minimumCommitment = 12f;
        [Min(1)] public float persistentObstructionSeconds = 4f;
        [Min(1)] public float relocationCooldown = 20f;
        [Min(1)] public float routeStallSeconds = 8f;
        [Min(1)] public float failedSpotCooldown = 30f;
        [Min(1)] public float retryCooldown = 8f;
        [Range(0.05f, 1)] public float arrivalRadius = 0.45f;
        [Min(0)] public float distanceCost = 0.03f;
        [Min(0)] public float crowdCost = 0.35f;
        [Min(0)] public float stayBonus = 0.3f;
        [Min(0)] public float switchAdvantage = 0.2f;
    }
}
