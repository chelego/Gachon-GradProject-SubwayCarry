using UnityEngine;

namespace SubwayCarry.AI.V2
{
    [DefaultExecutionOrder(-100)]
    public sealed class PassengerAiV2Corridor3DScenario : MonoBehaviour
    {
        [SerializeField] private PassengerAiV2PersonalityProfile baselineProfile;
        [SerializeField] private PassengerAiV2CrowdManager crowd;
        [SerializeField] private PassengerAiV2Agent[] agents;
        [SerializeField] private PassengerAiV23DNavigation navigation;
        public void ConfigureNavigation(PassengerAiV23DNavigation value) { navigation = value; }

        public void Configure(PassengerAiV2PersonalityProfile profile,
            PassengerAiV2CrowdManager manager, PassengerAiV2Agent[] passengers)
        { baselineProfile = profile; crowd = manager; agents = passengers; }

        private void Awake()
        {
            float[] lanes = { -2.35f, -0.8f, 0.8f, 2.35f };
            for (int i = 0; i < agents.Length; i++)
            {
                PassengerAiV2Agent agent = agents[i];
                bool fromLeft = i < agents.Length / 2;
                int index = i % (agents.Length / 2);
                float lane = lanes[index % lanes.Length];
                float column = index / lanes.Length;
                float sign = fromLeft ? 1f : -1f;
                Vector2 start = new Vector2(sign * (-18f + 1.35f + column * 2.1f), lane);
                Vector2 target = new Vector2(sign * (18f - 1.05f), lane);
                var personality = baselineProfile != null ? baselineProfile.CreateRuntimePersonality(i + 1000)
                    : PassengerAiV2RuntimePersonality.CreateFallback(i + 1000);
                agent.Initialize(crowd, start, target, personality, (float)i / agents.Length,
                    -3.5f, 3.5f, null, Color.white);
                agent.UsePlanarMotor(agent.GetComponentInChildren<PassengerAiV23DMotor>());
                agent.SetMovementBounds(new Rect(-18f, -3.5f, 36f, 7f));
                if (navigation != null) navigation.Bind(agent);
            }
        }
    }
}
