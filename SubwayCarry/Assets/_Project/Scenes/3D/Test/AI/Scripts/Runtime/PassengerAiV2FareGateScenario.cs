using UnityEngine;

namespace SubwayCarry.AI.V2
{
    /// <summary>
    /// AI V2 3단계인 방향별 개찰구 선택, 줄서기, 카드 태그와 동일 통로 통과를 재현한다.
    /// 01, 02와 동일한 Agent, Personality와 CrowdManager를 사용한다.
    /// </summary>
    public sealed class PassengerAiV2FareGateScenario : MonoBehaviour
    {
        public const string SceneTitle = "AI V2 03 - FARE GATE";
        public const string ScenePurpose = "Queue / card tag / one-way gate / same-lane passage";

        [Header("Scenario")]
        [SerializeField, Range(2, 20)] private int passengersPerDirection = 15;
        [SerializeField] private PassengerAiV2PersonalityProfile baselineProfile;

        private readonly Rect worldBounds = Rect.MinMaxRect(-14f, -8.8f, 14f, 8.8f);
        private PassengerAiV2CrowdManager crowdManager;
        private PassengerAiV2FareGateSmartObject[] gates;
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
            GUI.Label(new Rect(left, 43f, 680f, 24f), ScenePurpose, bodyStyle);
            GUI.Label(
                new Rect(left, 66f, 840f, 24f),
                "PASS: wait in lane / tag before opening / pass reserved gate / close after exit",
                bodyStyle);

            int agents = crowdManager != null ? crowdManager.RegisteredCount : 0;
            int deadlocks = crowdManager != null ? crowdManager.DeadlockedCount : 0;
            int waiting = 0;
            int open = 0;
            int tags = 0;
            int passed = 0;
            int invalid = 0;
            if (gates != null)
            {
                for (int i = 0; i < gates.Length; i++)
                {
                    PassengerAiV2FareGateSmartObject gate = gates[i];
                    if (gate == null)
                    {
                        continue;
                    }

                    waiting += gate.WaitingCount;
                    open += gate.IsOpen ? 1 : 0;
                    tags += gate.CardTagCount;
                    passed += gate.CompletedPassCount;
                    invalid += gate.InvalidPassAttemptCount;
                }
            }

            GUI.Label(
                new Rect(left, 89f, 820f, 24f),
                $"Agents {agents} | Deadlocked {deadlocks} | Waiting {waiting} | Open {open}/4",
                bodyStyle);
            GUI.Label(
                new Rect(left, 112f, 820f, 24f),
                $"Card tags {tags} | Passed {passed} | Invalid pass {invalid} | Base Passenger only",
                bodyStyle);
        }

        private void BuildLayout()
        {
            GameObject runtimeRoot = new GameObject("Runtime_FareGateLayout");
            runtimeRoot.transform.SetParent(transform, false);

            CreateRect(
                "Background",
                Vector2.zero,
                new Vector2(30f, 19f),
                new Color(0.025f, 0.04f, 0.055f, 1f),
                -2,
                runtimeRoot.transform);
            CreateRect(
                "LowerConcourse",
                new Vector2(0f, -4.7f),
                new Vector2(28f, 8.1f),
                new Color(0.27f, 0.32f, 0.35f, 1f),
                0,
                runtimeRoot.transform);
            CreateRect(
                "UpperConcourse",
                new Vector2(0f, 4.7f),
                new Vector2(28f, 8.1f),
                new Color(0.27f, 0.32f, 0.35f, 1f),
                0,
                runtimeRoot.transform);

            CreateRect(
                "LowerToUpperLaneGuide",
                new Vector2(-3f, 0f),
                new Vector2(5.6f, 16.5f),
                new Color(0.12f, 0.52f, 0.5f, 0.16f),
                1,
                runtimeRoot.transform);
            CreateRect(
                "UpperToLowerLaneGuide",
                new Vector2(3f, 0f),
                new Vector2(5.6f, 16.5f),
                new Color(0.72f, 0.32f, 0.12f, 0.16f),
                1,
                runtimeRoot.transform);

            GameObject managerObject = new GameObject("CrowdManager_SpatialHash");
            managerObject.transform.SetParent(transform, false);
            crowdManager = managerObject.AddComponent<PassengerAiV2CrowdManager>();

            gates = new PassengerAiV2FareGateSmartObject[4];
            gates[0] = CreateGate(
                "Gate_Entry_01",
                -4.5f,
                PassengerAiV2FareGateDirection.LowerToUpper,
                runtimeRoot.transform);
            gates[1] = CreateGate(
                "Gate_Entry_02",
                -1.5f,
                PassengerAiV2FareGateDirection.LowerToUpper,
                runtimeRoot.transform);
            gates[2] = CreateGate(
                "Gate_Exit_01",
                1.5f,
                PassengerAiV2FareGateDirection.UpperToLower,
                runtimeRoot.transform);
            gates[3] = CreateGate(
                "Gate_Exit_02",
                4.5f,
                PassengerAiV2FareGateDirection.UpperToLower,
                runtimeRoot.transform);

            CreateWall("LeftBarrier", new Vector2(-10.1f, 0f), new Vector2(7f, 0.55f), runtimeRoot.transform);
            CreateWall("CenterBarrier", Vector2.zero, new Vector2(1.05f, 0.55f), runtimeRoot.transform);
            CreateWall("RightBarrier", new Vector2(10.1f, 0f), new Vector2(7f, 0.55f), runtimeRoot.transform);
            CreateWall("LowerOuterWall", new Vector2(0f, -9.05f), new Vector2(29f, 0.5f), runtimeRoot.transform);
            CreateWall("UpperOuterWall", new Vector2(0f, 9.05f), new Vector2(29f, 0.5f), runtimeRoot.transform);
        }

        private PassengerAiV2FareGateSmartObject CreateGate(
            string objectName,
            float x,
            PassengerAiV2FareGateDirection direction,
            Transform parent)
        {
            GameObject gateRoot = new GameObject(objectName);
            gateRoot.transform.SetParent(parent, false);
            gateRoot.transform.position = new Vector3(x, 0f, 0f);

            CreateRect(
                "Housing_Left",
                new Vector2(-0.62f, 0f),
                new Vector2(0.34f, 2.6f),
                new Color(0.19f, 0.25f, 0.29f, 1f),
                5,
                gateRoot.transform,
                true);
            CreateRect(
                "Housing_Right",
                new Vector2(0.62f, 0f),
                new Vector2(0.34f, 2.6f),
                new Color(0.19f, 0.25f, 0.29f, 1f),
                5,
                gateRoot.transform,
                true);
            GameObject barrier = CreateRect(
                "OrangeBarrier_ClosedUntilCardTag",
                Vector2.zero,
                new Vector2(0.88f, 0.15f),
                new Color(1f, 0.46f, 0.1f, 1f),
                6,
                gateRoot.transform);

            float directionSign = direction == PassengerAiV2FareGateDirection.LowerToUpper
                ? 1f
                : -1f;
            CreateRect(
                "CardReader",
                new Vector2(-0.62f, -directionSign * 1.02f),
                new Vector2(0.38f, 0.38f),
                new Color(0.15f, 0.88f, 0.92f, 1f),
                7,
                gateRoot.transform);
            CreateRect(
                "DirectionMark",
                new Vector2(0f, -directionSign * 1.58f),
                new Vector2(0.85f, 0.1f),
                direction == PassengerAiV2FareGateDirection.LowerToUpper
                    ? new Color(0.17f, 0.82f, 0.72f, 1f)
                    : new Color(1f, 0.57f, 0.25f, 1f),
                4,
                gateRoot.transform);

            Vector2 approach = new Vector2(x, -directionSign * 2.05f);
            Vector2 reader = new Vector2(x, -directionSign * 1.02f);
            Vector2 pass = new Vector2(x, directionSign * 0.9f);
            Vector2 release = new Vector2(x, directionSign * 2.05f);
            PassengerAiV2FareGateSmartObject smartObject =
                gateRoot.AddComponent<PassengerAiV2FareGateSmartObject>();
            smartObject.Configure(
                objectName.ToLowerInvariant(),
                direction,
                approach,
                reader,
                pass,
                release,
                barrier.GetComponent<SpriteRenderer>());
            return smartObject;
        }

        private void SpawnPassengers()
        {
            int count = Mathf.Max(2, passengersPerDirection);
            for (int i = 0; i < count; i++)
            {
                float normalized = count <= 1 ? 0.5f : i / (float)(count - 1);
                float x = Mathf.Lerp(-10.5f, 10.5f, normalized);
                float lowerY = -7.1f - (i % 2) * 0.55f;
                float upperY = 7.1f + (i % 2) * 0.55f;
                PassengerAiV2RuntimePersonality lowerPersonality = baselineProfile != null
                    ? baselineProfile.CreateRuntimePersonality(5000 + i)
                    : PassengerAiV2RuntimePersonality.CreateFallback(5000 + i);
                PassengerAiV2RuntimePersonality upperPersonality = baselineProfile != null
                    ? baselineProfile.CreateRuntimePersonality(6000 + i)
                    : PassengerAiV2RuntimePersonality.CreateFallback(6000 + i);

                CreatePassenger(
                    $"Passenger_Entry_{i + 1:00}__{lowerPersonality.ProfileId}",
                    new Vector2(x, lowerY),
                    new Vector2(x, 7.7f),
                    PassengerAiV2FareGateDirection.LowerToUpper,
                    lowerPersonality,
                    (i + 0.25f) / count,
                    new Color(0.17f, 0.82f, 0.72f, 1f));
                CreatePassenger(
                    $"Passenger_Exit_{i + 1:00}__{upperPersonality.ProfileId}",
                    new Vector2(-x, upperY),
                    new Vector2(-x, -7.7f),
                    PassengerAiV2FareGateDirection.UpperToLower,
                    upperPersonality,
                    (i + 0.75f) / count,
                    new Color(1f, 0.57f, 0.25f, 1f));
            }
        }

        private void CreatePassenger(
            string objectName,
            Vector2 start,
            Vector2 destination,
            PassengerAiV2FareGateDirection direction,
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

            PassengerAiV2FareGateTraversalAction action =
                passengerObject.AddComponent<PassengerAiV2FareGateTraversalAction>();
            action.Initialize(agent, gates, direction, start, destination, worldBounds, stagger);
        }

        private void CreateWall(string objectName, Vector2 position, Vector2 size, Transform parent)
        {
            CreateRect(
                objectName,
                position,
                size,
                new Color(0.42f, 0.52f, 0.59f, 1f),
                5,
                parent,
                true);
        }

        private GameObject CreateRect(
            string objectName,
            Vector2 position,
            Vector2 size,
            Color color,
            int sortingOrder,
            Transform parent,
            bool addCollider = false)
        {
            GameObject rectangle = new GameObject(objectName);
            rectangle.transform.SetParent(parent, false);
            rectangle.transform.localPosition = position;
            rectangle.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = rectangle.AddComponent<SpriteRenderer>();
            renderer.sprite = sharedSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            if (addCollider)
            {
                rectangle.AddComponent<BoxCollider2D>();
            }

            return rectangle;
        }

        private static Sprite CreateSharedSprite()
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = "AI_V2_FareGate_RuntimeWhiteTexture";
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
