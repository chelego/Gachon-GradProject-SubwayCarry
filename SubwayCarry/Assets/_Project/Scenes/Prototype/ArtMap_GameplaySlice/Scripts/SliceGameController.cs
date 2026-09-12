using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SubwayCarry.AI.V2;
using SubwayCarry.Gameplay;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    public sealed class SliceGameController : MonoBehaviour
    {
        public SliceMap[] maps;
        public GameObject playerPrefab, packagePrefab;
        public Material playerOutline;
        public PassengerAiV2PersonalityProfile baselineProfile;
        public PassengerAiV2ActivitySettings activitySettings;
        [Range(0, 30)] public int waitingPassengerCapacity = 10;
        public Sprite[] idleSprites, walkSprites;
        public Camera gameplayCamera;
        [Range(4, 40)] public int maximumPassengers = 18;
        [Min(0.5f)] public float spawnInterval = 1.5f;
        public int seed = 2701;
        GameObject player, actorsRoot;
        PlayerController playerInput;
        PlayerPosture posture;
        PackageDurability durability;
        PlayerBalance balance;
        SlicePlayerBoundary boundary;
        Rigidbody2D playerBody;
        PassengerAiV2CrowdManager crowd;
        SlicePlaceEnvironment environment;
        PassengerAiV2InteriorSpotSmartObject[] spots;
        readonly List<SlicePassengerIntent> passengers = new List<SlicePassengerIntent>(40);
        readonly List<PassengerAiV2Agent> nearby = new List<PassengerAiV2Agent>(20);
        Sprite sharedSprite;
        Texture2D whiteTexture;
        int currentMap, serial, exited, nearestPortal = -1;
        float nextSpawn, nextHud, fade, portalReadyAt;
        bool changing;
        string hud = "", prompt = "";
        GUIStyle hudStyle;
        public SliceMap CurrentMap => maps[currentMap];

        void Start()
        {
            whiteTexture = new Texture2D(1, 1); whiteTexture.SetPixel(0, 0, Color.white); whiteTexture.Apply();
            sharedSprite = Sprite.Create(whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
            for (int i = 0; i < maps.Length; i++) maps[i].gameObject.SetActive(false);
            player = Instantiate(playerPrefab); player.name = "PLAYER_CyanOutline"; player.transform.SetParent(transform, true);
            playerBody = player.GetComponent<Rigidbody2D>(); playerBody.gravityScale = 0; playerBody.freezeRotation = true;
            playerInput = player.GetComponent<PlayerController>(); posture = player.GetComponent<PlayerPosture>(); balance = player.GetComponent<PlayerBalance>();
            boundary = player.GetComponent<SlicePlayerBoundary>();
            if (boundary == null) boundary = player.AddComponent<SlicePlayerBoundary>();
            var visual = player.AddComponent<SliceCharacterVisual>(); visual.player = true;
            visual.body = player.transform.Find("BodyVisual").GetComponent<SpriteRenderer>(); visual.body.sharedMaterial = playerOutline;
            GameObject package = Instantiate(packagePrefab); durability = package.GetComponent<PackageDurability>();
            player.GetComponent<PlayerPackageCarrier>().SetPackage(package.transform);
            ActivateMap(0, maps[0].entry);
        }

        void ActivateMap(int index, Vector2 arrival)
        {
            if (actorsRoot != null) { actorsRoot.SetActive(false); Destroy(actorsRoot); }
            passengers.Clear();
            for (int i = 0; i < maps.Length; i++) maps[i].gameObject.SetActive(i == index);
            currentMap = index; CurrentMap.EnsureGrid();
            boundary.grid = CurrentMap.Grid;
            boundary.radius = CurrentMap.characterRadius;
            player.transform.localScale = new Vector3(CurrentMap.characterScale, CurrentMap.characterScale, 1);
            // The character's art size and its ground footprint are separate measurements.
            var playerCollider = player.GetComponent<BoxCollider2D>();
            if (playerCollider != null) playerCollider.size = Vector2.one * (CurrentMap.characterRadius * 1.414214f / CurrentMap.characterScale);
            player.GetComponent<SliceCharacterVisual>().map = CurrentMap;
            Vector2 spawn = CurrentMap.Grid.Nearest(arrival);
            playerBody.position = spawn; playerBody.linearVelocity = Vector2.zero; player.transform.position = spawn;
            Physics2D.SyncTransforms();
            // Spawning/teleporting is not a physical impact. Reset the sensor's previous-position sample.
            var activeSensors = player.GetComponentsInChildren<PackageImpactSensor>(true);
            for (int i = 0; i < activeSensors.Length; i++) if (activeSensors[i].enabled) { activeSensors[i].enabled = false; activeSensors[i].enabled = true; }
            gameplayCamera.transform.position = new Vector3(spawn.x, spawn.y + 1.2f, -30);
            gameplayCamera.orthographicSize = CurrentMap.cameraSize;
            actorsRoot = new GameObject("Runtime_Passengers_And_SmartObjects"); actorsRoot.transform.SetParent(CurrentMap.transform, false);
            // Art scale is not simulation scale: keep character speed/radius consistent between station and train.
            actorsRoot.transform.localScale = Vector3.one / CurrentMap.transform.lossyScale.x;
            crowd = actorsRoot.AddComponent<PassengerAiV2CrowdManager>();
            spots = new PassengerAiV2InteriorSpotSmartObject[CurrentMap.interests.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                var data = CurrentMap.interests[i]; var go = new GameObject("Facility_" + data.kind + "_" + i); go.transform.SetParent(actorsRoot.transform, false);
                go.transform.position = data.position; spots[i] = go.AddComponent<PassengerAiV2InteriorSpotSmartObject>();
                spots[i].Configure("slice_" + currentMap + "_" + i, data.kind, data.position, data.comfort, 0.5f, null);
            }
            environment = actorsRoot.AddComponent<SlicePlaceEnvironment>(); environment.Configure(CurrentMap, crowd, spots);
            var proxy = CreateAgent("Player_Perception_Only", spawn, 0);
            proxy.FollowExternalMotion(player.transform, CurrentMap.characterRadius);
            for (int i = 0; i < Mathf.Min(10, maximumPassengers); i++) SpawnPassenger(true);
            nextSpawn = Time.time + spawnInterval; portalReadyAt = Time.time + 1; nearestPortal = -1;
        }

        PassengerAiV2Agent CreateAgent(string label, Vector2 position, int id)
        {
            var go = new GameObject(label); go.transform.SetParent(actorsRoot.transform, false);
            go.AddComponent<SpriteRenderer>(); go.AddComponent<CircleCollider2D>(); go.AddComponent<Rigidbody2D>();
            var agent = go.AddComponent<PassengerAiV2Agent>();
            var p = baselineProfile.CreateRuntimePersonality(seed + id);
            agent.Initialize(crowd, position, position, p, (id % 17) / 17f,
                CurrentMap.floorBounds.yMin - 1, CurrentMap.floorBounds.yMax + 1, sharedSprite, Color.white);
            agent.SetAutoLoop(false); agent.SetMovementBounds(CurrentMap.floorBounds);
            var follower = new SlicePathFollower(CurrentMap.Grid);
            agent.ConfigureMovementSpace(follower.Resolve, CurrentMap.Grid.Constrain, CurrentMap.characterRadius);
            go.transform.localScale = Vector3.one;
            go.GetComponent<CircleCollider2D>().radius = CurrentMap.characterRadius;
            go.GetComponent<SpriteRenderer>().enabled = false; go.transform.Find("FacingDirection").gameObject.SetActive(false);
            return agent;
        }

        void SpawnPassenger(bool scatter)
        {
            if (passengers.Count >= maximumPassengers || CurrentMap.exits.Length == 0) return;
            int id = ++serial;
            var samples = CurrentMap.Grid.SamplePoints;
            Vector2 position = scatter && samples.Count > 0
                ? CurrentMap.Grid.Nearest(samples[(id * 37) % samples.Count])
                : CurrentMap.exits[id % CurrentMap.exits.Length].inside;
            if ((position - (Vector2)player.transform.position).sqrMagnitude < 1.2f) return;
            // Initial population is checked directly; later arrivals use the shared spatial hash.
            for (int i = 0; i < passengers.Count; i++) if (passengers[i] != null && ((Vector2)passengers[i].transform.position - position).sqrMagnitude < 0.8f) return;
            crowd.QueryNearby(position, 0.8f, null, nearby); if (nearby.Count > 0) return;
            var agent = CreateAgent("Passenger_" + id, position, id);
            var intent = agent.gameObject.AddComponent<SlicePassengerIntent>();
            int waiting = 0;
            for (int i = 0; i < passengers.Count; i++) if (passengers[i] != null && !passengers[i].IsPassingThrough) waiting++;
            PassengerAiV2LocalGoal goal;
            if (CurrentMap.placeKind == PassengerAiV2PlaceKind.TrainInterior) goal = PassengerAiV2LocalGoal.RideToDestination;
            else if (waiting >= waitingPassengerCapacity || id % 3 == 0) goal = PassengerAiV2LocalGoal.PassThrough;
            else goal = CurrentMap.placeKind == PassengerAiV2PlaceKind.Platform ? PassengerAiV2LocalGoal.WaitForService : PassengerAiV2LocalGoal.WaitHere;
            intent.Initialize(agent, CurrentMap, this, baselineProfile.CreateRuntimePersonality(seed + id), environment, activitySettings, goal, seed + id);
            var bodyObject = new GameObject("Passenger_Art"); bodyObject.transform.SetParent(agent.transform, false);
            var body = bodyObject.AddComponent<SpriteRenderer>(); body.sprite = idleSprites[0]; body.color = new Color(0.77f, 0.83f, 0.86f, 1);
            body.transform.localScale = Vector3.one * CurrentMap.characterScale;
            var visual = agent.gameObject.AddComponent<SliceCharacterVisual>(); visual.body = body; visual.agent = agent; visual.intent = intent;
            visual.idleSprites = idleSprites; visual.walkSprites = walkSprites;
            visual.map = CurrentMap; visual.visualScale = CurrentMap.characterScale;
            passengers.Add(intent);
        }

        void Update()
        {
            if (player == null) return;
            if (!changing && Time.time >= nextSpawn)
            {
                nextSpawn = Time.time + spawnInterval;
                for (int i = passengers.Count - 1; i >= 0; i--) if (passengers[i] == null) passengers.RemoveAt(i);
                SpawnPassenger(false);
            }
            if (Time.time >= nextHud)
            {
                int staying = 0, passing = 0, changes = 0, routeWaits = 0;
                for (int i = 0; i < passengers.Count; i++) if (passengers[i] != null)
                {
                    var intent = passengers[i];
                    if (intent.IsPassingThrough) passing++;
                    if (intent.State == PassengerAiV2ActivityState.Staying || intent.State == PassengerAiV2ActivityState.ReadyForTraversal) staying++;
                    if (intent.State == PassengerAiV2ActivityState.WaitingForRoute) routeWaits++;
                    changes += intent.TargetChanges;
                }
                nextHud = Time.time + 0.2f; nearestPortal = -1; float closest = 1.6f * 1.6f;
                for (int i = 0; i < CurrentMap.portals.Length; i++) { float d = (CurrentMap.portals[i].position - (Vector2)player.transform.position).sqrMagnitude; if (d < closest) { closest = d; nearestPortal = i; } }
                prompt = nearestPortal >= 0 ? "E : " + CurrentMap.portals[nearestPortal].label : "Move to a cyan doorway marker, then press E";
                hud = CurrentMap.displayName + "   |   Passengers " + passengers.Count + "/" + maximumPassengers + "   Exited " + exited
                    + "\nWASD move | E map doorway | 1 stand / 2 lean / 3 sit / 4 hold / 5 overhead | Wheel zoom"
                    + "\n" + prompt + "\nPosture: " + posture.CurrentState + "   Stamina " + Mathf.RoundToInt(posture.StaminaRatio * 100) + "%"
                    + "   |   Box " + Mathf.RoundToInt(durability.CurrentDurability.BoxDurability) + "   Cake " + Mathf.RoundToInt(durability.CurrentDurability.CakeDurability)
                    + "\nA* " + CurrentMap.Grid.PathRequests + "   Failed " + CurrentMap.Grid.PathFailures + "   Stuck " + crowd.DeadlockedCount
                    + (balance.IsActive ? "   BALANCE: " + balance.CurrentPromptKey : "   6-9: train balance test")
                    + "\nStaying " + staying + "   Passing " + passing + "   Target changes " + changes + "   Route waits " + routeWaits + "   Context " + environment.Context.Service
                    + "\nF6 approach / F7 doors open / F8 departed: context signals ONLY (no train animation)";
            }
            if (!changing && Keyboard.current != null)
            {
                if (Keyboard.current.f6Key.wasPressedThisFrame) environment.NotifyService(PassengerAiV2ServicePhase.Approaching, true, true);
                if (Keyboard.current.f7Key.wasPressedThisFrame) environment.NotifyService(PassengerAiV2ServicePhase.DoorsOpen, true, true);
                if (Keyboard.current.f8Key.wasPressedThisFrame) environment.NotifyService(PassengerAiV2ServicePhase.Departed, true, true);
            }
            if (!changing && nearestPortal >= 0 && Time.time >= portalReadyAt && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            { var portal = CurrentMap.portals[nearestPortal]; StartCoroutine(Transition(portal)); }
            if (Mouse.current != null) gameplayCamera.orthographicSize = Mathf.Clamp(gameplayCamera.orthographicSize - Mouse.current.scroll.ReadValue().y * 0.012f, 4f, 18f);
        }
        void LateUpdate()
        {
            if (player == null || changing) return;
            Vector3 target = new Vector3(player.transform.position.x, player.transform.position.y + 1.2f, -30);
            gameplayCamera.transform.position = Vector3.Lerp(gameplayCamera.transform.position, target, 1f - Mathf.Exp(-8f * Time.deltaTime));
        }
        IEnumerator Transition(SlicePortal portal)
        {
            changing = true; boundary.locked = true;
            var sensors = player.GetComponentsInChildren<PackageImpactSensor>(true);
            for (int i = 0; i < sensors.Length; i++) sensors[i].enabled = false;
            for (float t = 0; t < 0.25f; t += Time.unscaledDeltaTime) { fade = t / 0.25f; yield return null; }
            fade = 1; ActivateMap(portal.targetMap, portal.arrival); yield return null;
            for (int i = 0; i < sensors.Length; i++) sensors[i].enabled = true;
            for (float t = 0; t < 0.25f; t += Time.unscaledDeltaTime) { fade = 1 - t / 0.25f; yield return null; }
            fade = 0; boundary.locked = false; changing = false;
        }
        public void ReportPassengerExit() { exited++; }
        void OnGUI()
        {
            if (hudStyle == null) hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = Color.white } };
            GUI.Box(new Rect(12, 12, 850, 175), GUIContent.none); GUI.Label(new Rect(24, 20, 828, 160), hud, hudStyle);
            if (fade > 0) { Color previous = GUI.color; GUI.color = new Color(0, 0, 0, fade); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = previous; }
        }
        void OnDestroy() { if (sharedSprite != null) Destroy(sharedSprite); if (whiteTexture != null) Destroy(whiteTexture); }
    }
}
