using UnityEngine;

namespace SubwayCarry.AI.V2
{
    /// <summary>3D field wiring only. Decisions/reservations remain in the original
    /// V2 Smart Objects and separate traversal / Utility actions.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PassengerAiV23DFieldScenario : MonoBehaviour
    {
        [SerializeField] private int fieldNumber;
        [SerializeField] private PassengerAiV2PersonalityProfile profile;
        [SerializeField] private PassengerAiV2CrowdManager crowd;
        [SerializeField] private PassengerAiV2Agent[] agents;
        [SerializeField] private Vector2[] starts, destinations;
        [SerializeField] private Rect worldBounds;
        [SerializeField] private PassengerAiV23DNavigation navigation;
        [SerializeField] private PassengerAiV2StairSmartObject stair;
        [SerializeField] private PassengerAiV2FareGateSmartObject[] gates;
        [SerializeField] private PassengerAiV2TrainDoorSmartObject door;
        [SerializeField] private PassengerAiV2TrainInteriorEnvironment interior;
        public int FieldNumber => fieldNumber;
        public void Configure(int number, PassengerAiV2PersonalityProfile baseline, PassengerAiV2CrowdManager manager,
            PassengerAiV2Agent[] people, Vector2[] spawnPoints, Vector2[] targets, Rect bounds,
            PassengerAiV23DNavigation paths, PassengerAiV2StairSmartObject stairs,
            PassengerAiV2FareGateSmartObject[] fareGates, PassengerAiV2TrainDoorSmartObject trainDoor,
            PassengerAiV2TrainInteriorEnvironment trainInterior)
        {
            fieldNumber=number; profile=baseline; crowd=manager; agents=people; starts=spawnPoints;
            destinations=targets; worldBounds=bounds; navigation=paths; stair=stairs; gates=fareGates;
            door=trainDoor; interior=trainInterior;
        }
        private void Awake()
        {
            // Configure runtime-only Smart Object state (queues intentionally do not serialize).
            if (fieldNumber == 2)
                stair.Configure("3d_stair",new Vector2(0,-4.8f),new Vector2(0,-3.9f),new Vector2(0,3.9f),new Vector2(0,4.8f),10,.78f);
            if (fieldNumber == 4) door.Configure("3d_train_door",Vector2.zero,null,null,2);
            if (fieldNumber == 5)
            {
                interior.ConfigurePhaseDurations(12,14,16,4,2);
                foreach (var spot in GetComponentsInChildren<PassengerAiV2InteriorSpotSmartObject>()) interior.RegisterSpot(spot);
            }
            for (int i=0;i<agents.Length;i++)
            {
                var agent=agents[i]; var motor=agent.GetComponentInChildren<PassengerAiV23DMotor>();
                if(fieldNumber==2)
                {
                    motor.SetGroundSurface(StairHeight);
                    motor.ConfigureStairMovement(.65f,.78f,-4.55f,4.55f);
                }
                var personality=profile!=null ? profile.CreateRuntimePersonality(9000+i) : PassengerAiV2RuntimePersonality.CreateFallback(9000+i);
                float stagger=(float)i/agents.Length;
                agent.Initialize(crowd,starts[i],destinations[i],personality,stagger,worldBounds.yMin,worldBounds.yMax,null,Color.white);
                agent.UsePlanarMotor(motor); agent.SetMovementBounds(worldBounds); navigation.Bind(agent);
                if(fieldNumber==5) agent.ConfigureArrivalTolerance(.12f);
                bool forward=i<agents.Length/2;
                switch(fieldNumber)
                {
                    case 2:
                        agent.gameObject.AddComponent<PassengerAiV2StairTraversalAction>().Initialize(agent,stair,
                            forward?PassengerAiV2StairDirection.LowerToUpper:PassengerAiV2StairDirection.UpperToLower,
                            starts[i],destinations[i],worldBounds,stagger);
                        break;
                    case 3:
                        agent.gameObject.AddComponent<PassengerAiV2FareGateTraversalAction>().Initialize(agent,gates,
                            forward?PassengerAiV2FareGateDirection.LowerToUpper:PassengerAiV2FareGateDirection.UpperToLower,
                            starts[i],destinations[i],worldBounds,stagger);
                        break;
                    case 4:
                        agent.gameObject.AddComponent<PassengerAiV2TrainDoorTraversalAction>().Initialize(agent,door,
                            forward?PassengerAiV2TrainDoorFlow.Alight:PassengerAiV2TrainDoorFlow.Board,
                            starts[i],destinations[i],worldBounds,stagger);
                        break;
                    case 5:
                        var action=agent.gameObject.AddComponent<PassengerAiV2TrainInteriorAction>();
                        action.Use3DPosture(); action.Initialize(agent,interior,personality,starts[i],worldBounds,stagger);
                        agent.GetComponent<PassengerAiV23DVisual>().UseInterior(action);
                        break;
                }
                agent.GetComponent<PassengerAiV23DVisual>().RefreshActivitySources();
            }
        }
        public static float StairHeight(Vector2 p)
        { return Mathf.Clamp(Mathf.Ceil((p.y+4.16f)/.32f)*.16f,0,4.16f); }
    }
}
