using UnityEngine;

namespace SubwayCarry.AI.V2
{
    /// <summary>
    /// AI V2 4단계인 열차 문 하차 우선, 탑승 대기, 두 Lane 통과와 닫힘 주기를 재현한다.
    /// 01~03과 동일한 Agent, Personality와 CrowdManager를 사용한다.
    /// </summary>
    public sealed class PassengerAiV2TrainDoorScenario : MonoBehaviour
    {
        public const string SceneTitle = "AI V2 04 - TRAIN DOOR";
        public const string ScenePurpose = "Fast alight flow / local-side FIFO queue / late alight overlap";

        [Header("Scenario")]
        [SerializeField, Range(2, 10)] private int passengersPerFlow = 6;
        [SerializeField] private PassengerAiV2PersonalityProfile baselineProfile;

        private readonly Rect worldBounds = Rect.MinMaxRect(-14f, -8.8f, 14f, 8.8f);
        private PassengerAiV2CrowdManager crowdManager;
        private PassengerAiV2TrainDoorSmartObject door;
        private Sprite sharedSprite;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;

        public void ConfigureBaselineProfile(PassengerAiV2PersonalityProfile profile)
        {
            baselineProfile = profile;
        }

        private void Awake()
        {
            sharedSprite = CreateSharedSprite();
            BuildLayout();
            SpawnPassengers();
        }

        private void OnDestroy()
        {
            if (sharedSprite != null)
            {
                Destroy(sharedSprite.texture);
                Destroy(sharedSprite);
            }
        }

        private void OnGUI()
        {
            if (titleStyle == null || bodyStyle == null)
            {
                BuildStyles();
            }

            const float left = 18f;
            GUI.Label(new Rect(left, 14f, 520f, 32f), SceneTitle, titleStyle);
            GUI.Label(new Rect(left, 43f, 720f, 24f), ScenePurpose, bodyStyle);
            GUI.Label(
                new Rect(left, 66f, 900f, 24f),
                "PASS: fast alight group / nearest-side queue / front boards first / safe late overlap",
                bodyStyle);

            int agents = crowdManager != null ? crowdManager.RegisteredCount : 0;
            int deadlocks = crowdManager != null ? crowdManager.DeadlockedCount : 0;
            string phase = door != null ? door.Phase.ToString() : "Missing";
            int cycle = door != null ? door.CycleIndex : -1;
            int waitingAlight = door != null ? door.WaitingToAlight : 0;
            int waitingBoard = door != null ? door.WaitingToBoard : 0;
            int active = door != null ? door.ActivePassageCount : 0;
            int alighted = door != null ? door.AlightedCount : 0;
            int boarded = door != null ? door.BoardedCount : 0;
            int invalid = door != null ? door.InvalidPassAttemptCount : 0;
            int earlyBoard = door != null ? door.BoardingBeforeAlightingClearCount : 0;
            int overlapStarts = door != null ? door.BoardingOverlapStartCount : 0;
            int outOfOrder = door != null ? door.OutOfOrderBoardingCount : 0;

            GUI.Label(
                new Rect(left, 89f, 880f, 24f),
                $"Phase {phase} | Cycle {cycle} | Agents {agents} | Deadlocked {deadlocks}",
                bodyStyle);
            GUI.Label(
                new Rect(left, 112f, 900f, 24f),
                $"Waiting A/B {waitingAlight}/{waitingBoard} | In doorway {active} | Alighted {alighted} | Boarded {boarded}",
                bodyStyle);
            GUI.Label(
                new Rect(left, 135f, 900f, 24f),
                $"Invalid {invalid} | Unsafe same-lane {earlyBoard} | Overlap {overlapStarts} | Out-of-order {outOfOrder}",
                bodyStyle);
        }

        private void BuildLayout()
        {
            GameObject runtimeRoot = new GameObject("Runtime_TrainDoorLayout");
            runtimeRoot.transform.SetParent(transform, false);

            CreateRect(
                "Background",
                Vector2.zero,
                new Vector2(30f, 19f),
                new Color(0.018f, 0.03f, 0.045f, 1f),
                -2,
                runtimeRoot.transform);
            CreateRect(
                "TrainInterior",
                new Vector2(0f, 4.7f),
                new Vector2(28f, 8.2f),
                new Color(0.16f, 0.26f, 0.33f, 1f),
                0,
                runtimeRoot.transform);
            CreateRect(
                "Platform",
                new Vector2(0f, -4.7f),
                new Vector2(28f, 8.2f),
                new Color(0.31f, 0.35f, 0.37f, 1f),
                0,
                runtimeRoot.transform);
            CreateRect(
                "PlatformSafetyLine",
                new Vector2(0f, -2.35f),
                new Vector2(28f, 0.13f),
                new Color(0.98f, 0.8f, 0.18f, 1f),
                4,
                runtimeRoot.transform);

            CreateRect(
                "TrainWall_Left",
                new Vector2(-8.15f, 0f),
                new Vector2(12.7f, 0.55f),
                new Color(0.43f, 0.54f, 0.62f, 1f),
                5,
                runtimeRoot.transform);
            CreateRect(
                "TrainWall_Right",
                new Vector2(8.15f, 0f),
                new Vector2(12.7f, 0.55f),
                new Color(0.43f, 0.54f, 0.62f, 1f),
                5,
                runtimeRoot.transform);

            CreateSeatRow(runtimeRoot.transform, -7f);
            CreateSeatRow(runtimeRoot.transform, 7f);
            CreateWaitingMarks(runtimeRoot.transform);

            GameObject managerObject = new GameObject("CrowdManager_SpatialHash");
            managerObject.transform.SetParent(transform, false);
            crowdManager = managerObject.AddComponent<PassengerAiV2CrowdManager>();

            GameObject doorRoot = new GameObject("SmartObject_TrainDoor_AlightBeforeBoard");
            doorRoot.transform.SetParent(runtimeRoot.transform, false);
            CreateRect(
                "DoorJamb_Left",
                new Vector2(-1.7f, 0f),
                new Vector2(0.35f, 1.1f),
                new Color(0.53f, 0.64f, 0.7f, 1f),
                6,
                doorRoot.transform);
            CreateRect(
                "DoorJamb_Right",
                new Vector2(1.7f, 0f),
                new Vector2(0.35f, 1.1f),
                new Color(0.53f, 0.64f, 0.7f, 1f),
                6,
                doorRoot.transform);
            GameObject leftPanel = CreateRect(
                "DoorPanel_Left",
                new Vector2(-0.78f, 0f),
                new Vector2(1.45f, 0.42f),
                new Color(0.2f, 0.58f, 0.74f, 1f),
                7,
                doorRoot.transform);
            GameObject rightPanel = CreateRect(
                "DoorPanel_Right",
                new Vector2(0.78f, 0f),
                new Vector2(1.45f, 0.42f),
                new Color(0.2f, 0.58f, 0.74f, 1f),
                7,
                doorRoot.transform);

            door = doorRoot.AddComponent<PassengerAiV2TrainDoorSmartObject>();
            door.Configure(
                "train_door_01",
                Vector2.zero,
                leftPanel.transform,
                rightPanel.transform,
                2);
        }

        private void SpawnPassengers()
        {
            int count = Mathf.Max(2, passengersPerFlow);
            for (int i = 0; i < count; i++)
            {
                float normalized = count <= 1 ? 0.5f : i / (float)(count - 1);
                float x = Mathf.Lerp(-8.5f, 8.5f, normalized);
                float alightY = 5.5f + (i % 2) * 0.55f;
                float boardY = -6.2f - (i % 2) * 0.55f;
                PassengerAiV2RuntimePersonality alightPersonality = baselineProfile != null
                    ? baselineProfile.CreateRuntimePersonality(7000 + i)
                    : PassengerAiV2RuntimePersonality.CreateFallback(7000 + i);
                PassengerAiV2RuntimePersonality boardPersonality = baselineProfile != null
                    ? baselineProfile.CreateRuntimePersonality(8000 + i)
                    : PassengerAiV2RuntimePersonality.CreateFallback(8000 + i);

                CreatePassenger(
                    $"Passenger_Alight_{i + 1:00}__{alightPersonality.ProfileId}",
                    new Vector2(x, alightY),
                    new Vector2(x, -6.8f),
                    PassengerAiV2TrainDoorFlow.Alight,
                    alightPersonality,
                    (i + 0.2f) / count,
                    new Color(0.18f, 0.84f, 0.78f, 1f));
                CreatePassenger(
                    $"Passenger_Board_{i + 1:00}__{boardPersonality.ProfileId}",
                    new Vector2(-x, boardY),
                    new Vector2(-x, 6.4f),
                    PassengerAiV2TrainDoorFlow.Board,
                    boardPersonality,
                    (i + 0.7f) / count,
                    new Color(1f, 0.56f, 0.22f, 1f));
            }
        }

        private void CreatePassenger(
            string objectName,
            Vector2 start,
            Vector2 destination,
            PassengerAiV2TrainDoorFlow flow,
            PassengerAiV2RuntimePersonality personality,
            float stagger,
            Color color)
        {
            GameObject passengerObject = new GameObject(objectName);
            passengerObject.transform.SetParent(transform, false);
            passengerObject.AddComponent<SpriteRenderer>();
            passengerObject.AddComponent<CircleCollider2D>();
            passengerObject.AddComponent<Rigidbody2D>();
            PassengerAiV2Agent agent = passengerObject.AddComponent<PassengerAiV2Agent>();
            agent.Initialize(
                crowdManager,
                start,
                destination,
                personality,
                stagger,
                worldBounds.yMin,
                worldBounds.yMax,
                sharedSprite,
                color);
            agent.SetMovementBounds(worldBounds);

            PassengerAiV2TrainDoorTraversalAction action =
                passengerObject.AddComponent<PassengerAiV2TrainDoorTraversalAction>();
            action.Initialize(agent, door, flow, start, destination, worldBounds, stagger);
        }

        private void CreateSeatRow(Transform parent, float x)
        {
            for (int i = 0; i < 3; i++)
            {
                CreateRect(
                    $"TrainSeat_{x}_{i}",
                    new Vector2(x + (i - 1) * 2.1f, 7.25f),
                    new Vector2(1.7f, 0.62f),
                    new Color(0.17f, 0.48f, 0.65f, 1f),
                    3,
                    parent);
            }
        }

        private void CreateWaitingMarks(Transform parent)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = 0; row < 3; row++)
                {
                    CreateRect(
                        $"BoardQueueMark_{side}_{row}",
                        new Vector2(side * (2.05f + row * 0.22f), -1.65f - row * 0.88f),
                        new Vector2(0.55f, 0.08f),
                        new Color(1f, 0.56f, 0.22f, 0.8f),
                        3,
                        parent);
                }
            }
        }

        private GameObject CreateRect(
            string objectName,
            Vector2 position,
            Vector2 size,
            Color color,
            int sortingOrder,
            Transform parent)
        {
            GameObject rectangle = new GameObject(objectName);
            rectangle.transform.SetParent(parent, false);
            rectangle.transform.localPosition = position;
            rectangle.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = rectangle.AddComponent<SpriteRenderer>();
            renderer.sprite = sharedSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return rectangle;
        }

        private static Sprite CreateSharedSprite()
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = "AI_V2_TrainDoor_RuntimeWhiteTexture";
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        private void BuildStyles()
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.94f, 0.97f, 1f, 1f) }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.84f, 0.89f, 0.92f, 1f) }
            };
        }
    }
}
