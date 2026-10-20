using UnityEngine;

namespace SubwayCarry.AI.V2
{
    /// <summary>
    /// AI V2 2단계인 좁은 계단 입구 합류, 양방향 연속 통행과 계단 내부 무대기를 재현한다.
    /// 01과 동일한 Agent, Personality와 CrowdManager를 사용한다.
    /// </summary>
    public sealed class PassengerAiV2StairMergeScenario : MonoBehaviour
    {
        public const string SceneTitle = "AI V2 02 - STAIR MERGE";
        public const string ScenePurpose = "Narrow entrance / continuous two-way flow / emergent crowd lanes";

        [Header("Scenario")]
        [SerializeField, Range(2, 12)] private int passengersPerDirection = 6;
        [SerializeField] private PassengerAiV2PersonalityProfile baselineProfile;

        private readonly Rect worldBounds = Rect.MinMaxRect(-14f, -8.8f, 14f, 8.8f);
        private PassengerAiV2CrowdManager crowdManager;
        private PassengerAiV2StairSmartObject stair;
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
            GUI.Label(new Rect(left, 43f, 620f, 24f), ScenePurpose, bodyStyle);
            GUI.Label(
                new Rect(left, 66f, 760f, 24f),
                "PASS: both directions form and follow open flow channels / no 3-second deadlock",
                bodyStyle);

            int agents = crowdManager != null ? crowdManager.RegisteredCount : 0;
            int deadlocks = crowdManager != null ? crowdManager.DeadlockedCount : 0;
            int lowerQueue = stair != null ? stair.WaitingLowerToUpper : 0;
            int upperQueue = stair != null ? stair.WaitingUpperToLower : 0;
            int inside = stair != null ? stair.TraversingCount : 0;
            int completed = stair != null ? stair.CompletedTraversalCount : 0;
            int activeLower = stair != null ? stair.ActiveLowerToUpper : 0;
            int activeUpper = stair != null ? stair.ActiveUpperToLower : 0;
            GUI.Label(
                new Rect(left, 89f, 800f, 24f),
                $"Agents {agents} | Deadlocked {deadlocks} | Queue L/U {lowerQueue}/{upperQueue} | Inside {inside}",
                bodyStyle);
            GUI.Label(
                new Rect(left, 112f, 760f, 24f),
                $"Active L/U {activeLower}/{activeUpper} | Completed {completed} | Base Passenger only",
                bodyStyle);
        }

        private void BuildLayout()
        {
            GameObject runtimeRoot = new GameObject("Runtime_StairMergeLayout");
            runtimeRoot.transform.SetParent(transform, false);

            CreateRect(
                "BackgroundVoid",
                Vector2.zero,
                new Vector2(30f, 19f),
                new Color(0.025f, 0.04f, 0.055f, 1f),
                -2,
                runtimeRoot.transform);
            CreateRect(
                "LowerLobbyFloor",
                new Vector2(0f, -6.65f),
                new Vector2(28f, 4.3f),
                new Color(0.24f, 0.29f, 0.32f, 1f),
                0,
                runtimeRoot.transform);
            CreateRect(
                "UpperLobbyFloor",
                new Vector2(0f, 6.65f),
                new Vector2(28f, 4.3f),
                new Color(0.24f, 0.29f, 0.32f, 1f),
                0,
                runtimeRoot.transform);
            CreateRect(
                "StairWalkableArea",
                Vector2.zero,
                new Vector2(3.8f, 9.4f),
                new Color(0.74f, 0.59f, 0.16f, 1f),
                1,
                runtimeRoot.transform);

            for (float y = -4.1f; y <= 4.1f; y += 0.7f)
            {
                CreateRect(
                    "StairTread",
                    new Vector2(0f, y),
                    new Vector2(3.45f, 0.08f),
                new Color(0.98f, 0.82f, 0.28f, 1f),
                    3,
                    runtimeRoot.transform);
            }

            CreateRect(
                "StairCenterFlowGuide",
                Vector2.zero,
                new Vector2(0.08f, 8.5f),
                new Color(0.88f, 0.73f, 0.24f, 0.8f),
                4,
                runtimeRoot.transform);

            CreateRect(
                "LowerWaitingBoundary",
                new Vector2(0f, -4.8f),
                new Vector2(4.8f, 0.1f),
                new Color(0.16f, 0.76f, 0.75f, 1f),
                4,
                runtimeRoot.transform);
            CreateRect(
                "UpperWaitingBoundary",
                new Vector2(0f, 4.8f),
                new Vector2(4.8f, 0.1f),
                new Color(0.95f, 0.45f, 0.22f, 1f),
                4,
                runtimeRoot.transform);

            CreateWall("StairLeftWall", new Vector2(-2.15f, 0f), new Vector2(0.5f, 9.6f), runtimeRoot.transform);
            CreateWall("StairRightWall", new Vector2(2.15f, 0f), new Vector2(0.5f, 9.6f), runtimeRoot.transform);
            CreateWall("LowerOuterWall", new Vector2(0f, -9.05f), new Vector2(29f, 0.5f), runtimeRoot.transform);
            CreateWall("UpperOuterWall", new Vector2(0f, 9.05f), new Vector2(29f, 0.5f), runtimeRoot.transform);

            GameObject managerObject = new GameObject("CrowdManager_SpatialHash");
            managerObject.transform.SetParent(transform, false);
            crowdManager = managerObject.AddComponent<PassengerAiV2CrowdManager>();

            GameObject stairObject = new GameObject("SmartObject_Stair_NarrowBidirectional");
            stairObject.transform.SetParent(transform, false);
            stair = stairObject.AddComponent<PassengerAiV2StairSmartObject>();
            stair.Configure(
                "stair_merge_01",
                new Vector2(0f, -4.8f),
                new Vector2(0f, -3.9f),
                new Vector2(0f, 3.9f),
                new Vector2(0f, 4.8f),
                10,
                0.78f);
        }

        private void SpawnPassengers()
        {
            int count = Mathf.Max(2, passengersPerDirection);
            for (int i = 0; i < count; i++)
            {
                float normalized = count <= 1 ? 0.5f : i / (float)(count - 1);
                float x = Mathf.Lerp(-6.5f, 6.5f, normalized);
                float lowerY = -7.25f - (i % 2) * 0.65f;
                float upperY = 7.25f + (i % 2) * 0.65f;
                PassengerAiV2RuntimePersonality lowerPersonality = baselineProfile != null
                    ? baselineProfile.CreateRuntimePersonality(3000 + i)
                    : PassengerAiV2RuntimePersonality.CreateFallback(3000 + i);
                PassengerAiV2RuntimePersonality upperPersonality = baselineProfile != null
                    ? baselineProfile.CreateRuntimePersonality(4000 + i)
                    : PassengerAiV2RuntimePersonality.CreateFallback(4000 + i);

                CreatePassenger(
                    $"Passenger_LowerToUpper_{i + 1:00}__{lowerPersonality.ProfileId}",
                    new Vector2(x, lowerY),
                    new Vector2(x, 7.7f),
                    PassengerAiV2StairDirection.LowerToUpper,
                    lowerPersonality,
                    (i + 0.25f) / count,
                    new Color(0.17f, 0.82f, 0.72f, 1f));

                CreatePassenger(
                    $"Passenger_UpperToLower_{i + 1:00}__{upperPersonality.ProfileId}",
                    new Vector2(-x, upperY),
                    new Vector2(-x, -7.7f),
                    PassengerAiV2StairDirection.UpperToLower,
                    upperPersonality,
                    (i + 0.75f) / count,
                    new Color(1f, 0.57f, 0.25f, 1f));
            }
        }

        private void CreatePassenger(
            string objectName,
            Vector2 start,
            Vector2 destination,
            PassengerAiV2StairDirection direction,
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

            PassengerAiV2StairTraversalAction action =
                passengerObject.AddComponent<PassengerAiV2StairTraversalAction>();
            action.Initialize(agent, stair, direction, start, destination, worldBounds, stagger);
        }

        private void CreateWall(string objectName, Vector2 position, Vector2 size, Transform parent)
        {
            GameObject wall = CreateRect(
                objectName,
                position,
                size,
                new Color(0.42f, 0.52f, 0.59f, 1f),
                5,
                parent);
            wall.AddComponent<BoxCollider2D>();
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
            rectangle.transform.position = position;
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
            texture.name = "AI_V2_StairMerge_RuntimeWhiteTexture";
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
