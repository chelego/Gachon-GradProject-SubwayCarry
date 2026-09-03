using UnityEngine;

namespace SubwayCarry.AI.V2
{
    /// <summary>
    /// AI V2 1단계인 양방향 마주침과 같은 방향 흐름 추종을 재현한다.
    /// Scene에는 이 컴포넌트만 연결하고, 반복 테스트용 오브젝트는 Play 시 생성한다.
    /// </summary>
    public sealed class PassengerAiV2SocialCorridorScenario : MonoBehaviour
    {
        public const string SceneTitle = "AI V2 01 - SOCIAL CORRIDOR";
        public const string ScenePurpose = "Two-way passing + same-direction flow following";

        [Header("Scenario")]
        [SerializeField, Range(2, 16)] private int passengersPerDirection = 8;
        [SerializeField] private float corridorHalfWidth = 18f;
        [SerializeField] private float corridorMinY = -3.5f;
        [SerializeField] private float corridorMaxY = 3.5f;
        [SerializeField] private PassengerAiV2PersonalityProfile baselineProfile;

        private PassengerAiV2CrowdManager crowdManager;
        private Sprite sharedSprite;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private string personalitySummary = "Personality: Base Passenger (all agents use identical values)";

        public void ConfigureBaselineProfile(PassengerAiV2PersonalityProfile profile)
        {
            baselineProfile = profile;
        }

        private void Awake()
        {
            sharedSprite = CreateSharedSprite();
            BuildPersonalitySummary();
            BuildCorridor();
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
                new Rect(left, 66f, 720f, 24f),
                "PASS: no 3-second deadlock / no left-right jitter / crowd speed matching",
                bodyStyle);

            int count = crowdManager != null ? crowdManager.RegisteredCount : 0;
            int deadlocks = crowdManager != null ? crowdManager.DeadlockedCount : 0;
            float spatialHz = crowdManager != null ? crowdManager.SpatialUpdateHz : 0f;
            GUI.Label(
                new Rect(left, 89f, 520f, 24f),
                $"Agents {count}  |  Deadlocked {deadlocks}  |  Spatial query {spatialHz:0} Hz",
                bodyStyle);
            GUI.Label(new Rect(left, 112f, 720f, 24f), personalitySummary, bodyStyle);
        }

        private void BuildCorridor()
        {
            GameObject runtimeRoot = new GameObject("Runtime_TestLayout");
            runtimeRoot.transform.SetParent(transform, false);

            CreateRect(
                "CorridorFloor",
                Vector2.zero,
                new Vector2(corridorHalfWidth * 2f, corridorMaxY - corridorMinY),
                new Color(0.20f, 0.25f, 0.29f, 1f),
                0,
                runtimeRoot.transform);

            CreateRect(
                "LeftSpawnZone",
                new Vector2(-corridorHalfWidth + 0.7f, 0f),
                new Vector2(1.4f, corridorMaxY - corridorMinY),
                new Color(0.08f, 0.45f, 0.62f, 0.55f),
                1,
                runtimeRoot.transform);
            CreateRect(
                "RightSpawnZone",
                new Vector2(corridorHalfWidth - 0.7f, 0f),
                new Vector2(1.4f, corridorMaxY - corridorMinY),
                new Color(0.86f, 0.36f, 0.19f, 0.55f),
                1,
                runtimeRoot.transform);

            float corridorWidth = corridorHalfWidth * 2f;
            CreateWall(
                "TopWall",
                new Vector2(0f, corridorMaxY + 0.28f),
                new Vector2(corridorWidth + 1.2f, 0.56f),
                runtimeRoot.transform);
            CreateWall(
                "BottomWall",
                new Vector2(0f, corridorMinY - 0.28f),
                new Vector2(corridorWidth + 1.2f, 0.56f),
                runtimeRoot.transform);

            for (float x = -16f; x <= 16f; x += 2f)
            {
                CreateRect(
                    "CenterGuide",
                    new Vector2(x, 0f),
                    new Vector2(0.85f, 0.06f),
                    new Color(0.55f, 0.63f, 0.67f, 0.5f),
                    2,
                    runtimeRoot.transform);
            }

            GameObject managerObject = new GameObject("CrowdManager_SpatialHash");
            managerObject.transform.SetParent(transform, false);
            crowdManager = managerObject.AddComponent<PassengerAiV2CrowdManager>();
        }

        private void SpawnPassengers()
        {
            float[] lanes = { -2.35f, -0.8f, 0.8f, 2.35f };
            int count = Mathf.Max(2, passengersPerDirection);
            for (int i = 0; i < count; i++)
            {
                int laneIndex = i % lanes.Length;
                int column = i / lanes.Length;
                float laneY = lanes[laneIndex];
                PassengerAiV2RuntimePersonality leftPersonality = baselineProfile != null
                    ? baselineProfile.CreateRuntimePersonality(1000 + i)
                    : PassengerAiV2RuntimePersonality.CreateFallback(1000 + i);
                PassengerAiV2RuntimePersonality rightPersonality = baselineProfile != null
                    ? baselineProfile.CreateRuntimePersonality(2000 + i)
                    : PassengerAiV2RuntimePersonality.CreateFallback(2000 + i);

                Vector2 leftStart = new Vector2(-corridorHalfWidth + 1.35f + column * 2.1f, laneY);
                Vector2 leftGoal = new Vector2(corridorHalfWidth - 1.05f, laneY);
                CreatePassenger(
                    $"Passenger_LeftToRight_{i + 1:00}__{leftPersonality.ProfileId}",
                    leftStart,
                    leftGoal,
                    leftPersonality,
                    (i + 0.25f) / count,
                    new Color(0.17f, 0.82f, 0.72f, 1f));

                Vector2 rightStart = new Vector2(corridorHalfWidth - 1.35f - column * 2.1f, laneY + 0.18f);
                Vector2 rightGoal = new Vector2(-corridorHalfWidth + 1.05f, laneY + 0.18f);
                CreatePassenger(
                    $"Passenger_RightToLeft_{i + 1:00}__{rightPersonality.ProfileId}",
                    rightStart,
                    rightGoal,
                    rightPersonality,
                    (i + 0.75f) / count,
                    new Color(1f, 0.57f, 0.25f, 1f));
            }
        }

        private void CreatePassenger(
            string objectName,
            Vector2 start,
            Vector2 goal,
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
                goal,
                personality,
                stagger,
                corridorMinY,
                corridorMaxY,
                sharedSprite,
                color);
        }

        private void BuildPersonalitySummary()
        {
            if (baselineProfile == null)
            {
                personalitySummary = "Personality: Base Passenger fallback (all agents use identical values)";
                return;
            }

            personalitySummary = $"Personality: {baselineProfile.DisplayName} (all agents use identical values)";
        }

        private void CreateWall(string objectName, Vector2 position, Vector2 size, Transform parent)
        {
            GameObject wall = CreateRect(
                objectName,
                position,
                size,
                new Color(0.42f, 0.52f, 0.59f, 1f),
                4,
                parent);
            BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
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
            texture.name = "AI_V2_RuntimeWhiteTexture";
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
